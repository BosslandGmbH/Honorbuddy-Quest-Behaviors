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
using Styx.Helpers;
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
            public SpankMob(ProvideWoWUnitDelegate selectedTargetDelegate,
                            ProvideMovementByDelegate movementByDelegate = null)
            {
                Contract.Requires(selectedTargetDelegate != null, context => "selectedTargetDelegate != null");

                MovementByDelegate = movementByDelegate ?? (context => MovementByType.NavigatorPreferred);
                SelectedTargetDelegate = selectedTargetDelegate;

                Children = CreateChildren();
            }


            // BT contruction-time properties...
            private ProvideMovementByDelegate MovementByDelegate { get; set; }
            private ProvideWoWUnitDelegate SelectedTargetDelegate { get; set; }

            // BT visit-time properties...
            private WaitTimer WatchdogTimer_TimeToEngage { get; set; }
            private WoWUnit SelectedTarget { get; set; }

            // Convenience properties...
            private readonly TimeSpan BlacklistForPullTime = TimeSpan.FromSeconds(3 * 60);
            private readonly TimeSpan MinTimeToEngagement = TimeSpan.FromSeconds(3);


            private List<Composite> CreateChildren()
            {
                return new List<Composite>()
                {
                    new Decorator(context =>
                    {
                        var mob = SelectedTargetDelegate(context);
                        var isMobChanged = (mob != SelectedTarget);

                        SelectedTarget = mob;

                        if (!Query.IsViableForFighting(SelectedTarget))
                        {
                            // If mob was valid, let user know of problem...
                            if (Query.IsViable(SelectedTarget))
                            {
                                QBCLog.DeveloperInfo("Mob {0} is not viable for fighting--ignoring SpankMob directive.",
                                    SelectedTarget.SafeName());
                            }
                            return false;
                        }

                        // If we have mob aggro, cancel the watchdog timer and allow combat to proceed...
                        if (SelectedTarget.Aggro)
                        {
                            WatchdogTimer_TimeToEngage = null;
                            return true;
                        }
                        
                        // NB: This twisted logic with watchdog timers is to handle bugged mobs...
                        // Once we've decided to go after a mob, we must close the distance and start the fight
                        // within a reasonable amount of time.  If that doesn't happen, then we blacklist
                        // the mob for combat (and move on).

                        // If mob changed, any running Watchdog timer is no longer valid...
                        if (isMobChanged)
                            { WatchdogTimer_TimeToEngage = null; }

                        // Start watchdog timer for engaging mob...
                        if (WatchdogTimer_TimeToEngage == null)
                        {
                            WatchdogTimer_TimeToEngage =
                                new WaitTimer(Utility.CalculateMaxTimeToDestination(SelectedTarget.Location, false)
                                                + MinTimeToEngagement);
                            WatchdogTimer_TimeToEngage.Reset();
                        }

                        // If we are unable to engage an unaggro'd mob in a reasonable amount of time,
                        // cancel attack, and blacklist mob...
                        if (WatchdogTimer_TimeToEngage.IsFinished)
                        {
                            QBCLog.Warning("Unable to engage {0} in {1}--pull-blacklisted for {2}",
                                SelectedTarget.Name,
                                Utility.PrettyTime(WatchdogTimer_TimeToEngage.WaitTime),
                                Utility.PrettyTime(BlacklistForPullTime));
                            Query.BlacklistForPulling(SelectedTarget, BlacklistForPullTime);
                            SelectedTarget = null;
                            BotPoi.Clear();
                            Me.ClearTarget();
                            return false;
                        }

                        return true;
                    },
                        new PrioritySelector(
                            // Mark target and notify Combat Routine of target, if needed...
                            new ActionFail(context => { Utility.Target(SelectedTarget, false, PoiType.Kill); }),

                            // Move within 'pull distance' of the target...
                            // NB: Combat Routines and/or HBcore will 'stall' if the selected target is beyond a certain
                            // range.  Thus, we must make certain that the target is within 'pull distance' before we attempt
                            // to engage the mob.
                            new Decorator(context => SelectedTarget.Distance > CharacterSettings.Instance.PullDistance,
                                new UtilityBehaviorPS.MoveTo(
                                    context => SelectedTarget.Location,
                                    context => SelectedTarget.SafeName(),
                                    MovementByDelegate)),

                            // TODO: This needs to go, eventually.  Let the Kill POI do its job.
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
        // TODO: Retire this behavior, once we get target filters installed in all the behaviors that use this class.
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
        // TODO: Retire this behavior, once we get target filters installed in all the behaviors that use this class.
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