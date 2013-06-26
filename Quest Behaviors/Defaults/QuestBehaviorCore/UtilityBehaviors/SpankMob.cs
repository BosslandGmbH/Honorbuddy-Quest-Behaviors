// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Usings
using System;
using System.Collections.Generic;
using System.Linq;

using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public partial class UtilityBehaviorPS
    {
        /// <summary>
        /// Unequivocally engages mob in combat.  Does no checking for being untagged, etc.
        /// </summary>
        /// <remarks>24Feb2013-08:11UTC chinajade</remarks>
        public class SpankMob : PrioritySelector
        {
            public SpankMob(ProvideWoWUnitDelegate selectedTargetDelegate)
            {
                Contract.Requires(selectedTargetDelegate != null, context => "selectedTargetDelegate != null");

                SelectedTargetDelegate = selectedTargetDelegate;

                Children = CreateChildren();
            }


            // BT contruction-time properties...
            private ProvideWoWUnitDelegate SelectedTargetDelegate { get; set; }

            // BT visit-time properties...
            private WaitTimer TimeToEngageWatchdogTimer { get; set; }
            private WoWUnit SelectedTarget { get; set; }

            // Convenience properties...
            private readonly TimeSpan BlacklistForPullTime = TimeSpan.FromSeconds(3 * 60);
            private readonly TimeSpan MinTimeToEngagement = TimeSpan.FromSeconds(3);


            private List<Composite> CreateChildren()
            {
                return new List<Composite>()
                {
                    new ActionFail(context =>
                    {
                        var mob = SelectedTargetDelegate(context);

                        if (mob != null)
                        {
                            var isMobChanged = (mob != SelectedTarget);

                            SelectedTarget = mob;

                            // NB: This twisted logic is to handle bugged mobs...
                            // Once we've decided to go after a mob, we close the distance and start the fight
                            // within a reasonable amount of time.  If that doesn't happen, then we blacklist then
                            // we blacklist the mob for combat (and move on).
                            if (Query.IsViableForFighting(SelectedTarget))
                            {
                                // If we have mob aggro, cancel the engagement timer...
                                if (SelectedTarget.Aggro)
                                    { TimeToEngageWatchdogTimer = null; }

                                // Otherwise, start watchdog timer for engaging mob...
                                else if (isMobChanged || (TimeToEngageWatchdogTimer == null))
                                {
                                    TimeToEngageWatchdogTimer =
                                        new WaitTimer(Utility.CalculateMaxTimeToDestination(SelectedTarget.Location, false)
                                                      + MinTimeToEngagement);
                                    TimeToEngageWatchdogTimer.Reset();
                                }

                                // If we are unable to engage an unaggro'd mob in a reasonable amount of time,
                                // cancel attack, and blacklist mob...
                                if ((TimeToEngageWatchdogTimer != null) && TimeToEngageWatchdogTimer.IsFinished)
                                {
                                    QBCLog.Warning("Unable to  engage {0} in {1}--pull-blacklisted for {2}",
                                        SelectedTarget.Name,
                                        Utility.PrettyTime(TimeToEngageWatchdogTimer.WaitTime),
                                        Utility.PrettyTime(BlacklistForPullTime));
                                    Query.BlacklistForPulling(SelectedTarget, BlacklistForPullTime);
                                    BotPoi.Clear();
                                    Me.ClearTarget();
                                    SelectedTarget = null;
                                    return RunStatus.Failure;
                                }

                                // Mark target and notify Combat Routine of target, if needed...
                                Utility.Target(SelectedTarget, false, PoiType.Kill);

                                // TODO: May need to leave this decision to Combat Routine--needs testing to see if we can.
                                if (Me.Mounted)
                                    { Mount.Dismount(); }
                            }
                        }

                        return RunStatus.Failure;   // fall through
                    }),

                    new Decorator(context => Query.IsViableForFighting(SelectedTarget),
                        new PrioritySelector(
                            // The NeedHeal and NeedCombatBuffs are part of legacy custom class support
                            // and pair with the Heal and CombatBuff virtual methods.  If a legacy custom class is loaded,
                            // HonorBuddy automatically wraps calls to Heal and CustomBuffs it in a Decorator checking those for you.
                            // So, no need to duplicate that work here.
                            new Decorator(ctx => RoutineManager.Current.HealBehavior != null,
                                RoutineManager.Current.HealBehavior),
                            new Decorator(ctx => RoutineManager.Current.CombatBuffBehavior != null,
                                RoutineManager.Current.CombatBuffBehavior),
                            RoutineManager.Current.CombatBehavior,

                            // Keep fighting until mob is dead...
                            new ActionAlwaysSucceed()
                        ))
                };
            }
        }


        /// <summary>
        /// Targets and kills any mob targeting Self or Pet.
        /// </summary>
        /// <returns></returns>
        public class SpankMobTargetingUs : PrioritySelector
        {
            public SpankMobTargetingUs(ProvideBoolDelegate ignoreMobsInBlackspotsDelegate,
                                        ProvideDoubleDelegate nonCompeteDistanceDelegate,
                                        Func<object, IEnumerable<WoWUnit>> excludedUnitsDelegate = null)
            {
                Contract.Requires(ignoreMobsInBlackspotsDelegate != null, context => "ignoreMobsInBlackspotsDelegate != null");
                Contract.Requires(nonCompeteDistanceDelegate != null, context => "nonCompeteDistanceDelegate != null");

                ExcludedUnitsDelegate = excludedUnitsDelegate ?? (context => Enumerable.Empty<WoWUnit>());
                IgnoreMobsInBlackspotsDelegate = ignoreMobsInBlackspotsDelegate;
                NonCompeteDistanceDelegate = nonCompeteDistanceDelegate;

                Children = CreateChildren();
            }


            // BT contruction-time properties...
            Func<object, IEnumerable<WoWUnit>> ExcludedUnitsDelegate { get; set; }
            private ProvideBoolDelegate IgnoreMobsInBlackspotsDelegate { get; set; }
            private ProvideDoubleDelegate NonCompeteDistanceDelegate { get; set; }

            // BT visit-time properties...
            private WoWUnit SelectedTarget { get; set; }

            // Convenience properties...
            
            
            private List<Composite> CreateChildren()
            {
                return new List<Composite>()
                {
                    new SpankMob(MobTargetingUs)
                };
            }


            private bool IsInterestingToUs(WoWUnit wowUnit, bool ignoreMobsInBlackspots, double nonCompeteDistance, IEnumerable<WoWUnit> excludedUnits)
            {
                return
                    Query.IsViableForPulling(wowUnit, ignoreMobsInBlackspots, nonCompeteDistance)
                    && (wowUnit.IsTargetingMeOrPet
                        || wowUnit.IsTargetingAnyMinion
                        || wowUnit.IsTargetingMyPartyMember)
                    // exclude opposing faction: both players and their pets show up as "PlayerControlled"
                    && !wowUnit.PlayerControlled
                    // Do not pull mobs on the AvoidMobs list
                    && !ProfileManager.CurrentOuterProfile.AvoidMobs.Contains(wowUnit.Entry)
                    // exclude any units that are candidates for interacting
                    && !excludedUnits.Contains(wowUnit);
            }


            private WoWUnit MobTargetingUs(object context)
            {
                var ignoreMobsInBlackspots = IgnoreMobsInBlackspotsDelegate(context);
                var nonCompeteDistance = NonCompeteDistanceDelegate(context);

                // If a mob is targeting us, deal with it immediately, so subsequent activities won't be interrupted...
                // NB: This can happen if we 'drag mobs' behind us on the way to our destination.
                if (!Query.IsViableForPulling(SelectedTarget, ignoreMobsInBlackspots, nonCompeteDistance))
                {
                    using (StyxWoW.Memory.AcquireFrame())
                    {
                        var excludedUnits = ExcludedUnitsDelegate(context);

                        SelectedTarget =
                            (from wowUnit in ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                             where IsInterestingToUs(wowUnit, ignoreMobsInBlackspots, nonCompeteDistance, excludedUnits)
                             orderby wowUnit.SurfacePathDistance()
                             select wowUnit)
                            .FirstOrDefault();
                    }

                    if (Query.IsViable(SelectedTarget))
                    {
                        Navigator.PlayerMover.MoveStop();
                        Utility.Target(SelectedTarget, true, PoiType.Kill);
                        TreeRoot.StatusText = string.Format("Spanking {0} that has targeted us.",
                            SelectedTarget.Name);
                    }
                }

                return SelectedTarget;
            }
        }


        // 24Feb2013-08:11UTC chinajade
        public class SpankMobWithinAggroRange : PrioritySelector
        {
            public SpankMobWithinAggroRange(ProvideWoWPointDelegate destinationDelegate,
                                            ProvideBoolDelegate ignoreMobsInBlackspotsDelegate,
                                            ProvideDoubleDelegate nonCompeteDistanceDelegate,
                                            ProvideDoubleDelegate extraRangePaddingDelegate = null,
                                            Func<object, IEnumerable<int>> excludedUnitIdsDelegate = null)
            {
                Contract.Requires(destinationDelegate != null, context => "destinationDelegate != null");
                Contract.Requires(ignoreMobsInBlackspotsDelegate != null, context => "ignoreMobsInBlackspotsDelegate != null");
                Contract.Requires(nonCompeteDistanceDelegate != null, context => "nonCompeteDistanceDelegate != null");

                DestinationDelegate = destinationDelegate;
                ExtraRangePaddingDelegate = extraRangePaddingDelegate ?? (context => 0.0);
                ExcludedUnitIdsDelegate = excludedUnitIdsDelegate ?? (context => Enumerable.Empty<int>());
                IgnoreMobsInBlackspotsDelegate = ignoreMobsInBlackspotsDelegate;
                NonCompeteDistanceDelegate = nonCompeteDistanceDelegate;

                Children = CreateChildren();
            }


            // BT contruction-time properties...
            private ProvideWoWPointDelegate DestinationDelegate { get; set; }
            private Func<object, IEnumerable<int>> ExcludedUnitIdsDelegate { get; set; }
            private ProvideDoubleDelegate ExtraRangePaddingDelegate { get; set; }
            private ProvideBoolDelegate IgnoreMobsInBlackspotsDelegate { get; set; }
            private ProvideDoubleDelegate NonCompeteDistanceDelegate { get; set; }

            // BT visit-time properties...
            private WoWUnit SelectedTarget { get; set; }

            // Convenience properties...


            private List<Composite> CreateChildren()
            {
                return new List<Composite>()
                {
                    new SpankMob(MobWithinAggroRange)
                };
            }


            private bool IsInterestingToUs(WoWUnit wowUnit, bool ignoreMobsInBlackspots, double nonCompeteDistance, IEnumerable<int> excludedUnitIds)
            {
                return
                    Query.IsViableForPulling(wowUnit, ignoreMobsInBlackspots, nonCompeteDistance)
                    && wowUnit.IsHostile
                    && wowUnit.IsUntagged()
                    // exclude opposing faction: both players and their pets show up as "PlayerControlled"
                    && !wowUnit.PlayerControlled
                    // exclude any units that are candidates for interacting
                    && !excludedUnitIds.Contains((int)wowUnit.Entry)
                    // Do not pull mobs on the AvoidMobs list
                    && !ProfileManager.CurrentOuterProfile.AvoidMobs.Contains(wowUnit.Entry)
                    && (wowUnit.Location.SurfacePathDistance(DestinationDelegate(wowUnit)) <= (wowUnit.MyAggroRange + ExtraRangePaddingDelegate(wowUnit)));
            }


            private WoWUnit MobWithinAggroRange(object context)
            {
                var ignoreMobsInBlackspots = IgnoreMobsInBlackspotsDelegate(context);
                var nonCompeteDistance = NonCompeteDistanceDelegate(context);

                if (!Query.IsViableForPulling(SelectedTarget, ignoreMobsInBlackspots, nonCompeteDistance))
                {
                    var excludedUnitIds = ExcludedUnitIdsDelegate(context);

                    // If a mob is within aggro range of our destination, deal with it immediately...
                    // Otherwise, it will interrupt our attempt to interact or use items.
                    using (StyxWoW.Memory.AcquireFrame())
                    {
                        SelectedTarget =
                            (from wowUnit in ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                             where IsInterestingToUs(wowUnit, ignoreMobsInBlackspots, nonCompeteDistance, excludedUnitIds)
                             orderby wowUnit.SurfacePathDistance()
                             select wowUnit)
                            .FirstOrDefault();
                    }

                    if (Query.IsViable(SelectedTarget))
                    {
                        Utility.Target(SelectedTarget, false, PoiType.Kill);
                        TreeRoot.StatusText = string.Format("Spanking {0}({1}) within aggro range ({2:F1}) of our destination.",
                            SelectedTarget.Name,
                            SelectedTarget.Entry,
                            (SelectedTarget.MyAggroRange + ExtraRangePaddingDelegate(context)));
                    }
                }

                return SelectedTarget;
            }
        }
    }
}