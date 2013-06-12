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
    public abstract partial class QuestBehaviorBase
    {
        /// <summary>
        /// Unequivocally engages mob in combat.  Does no checking for being untagged, etc.
        /// </summary>
        /// <remarks>24Feb2013-08:11UTC chinajade</remarks>
        public Composite UtilityBehaviorPS_SpankMob(ProvideWoWUnitDelegate selectedTargetDelegate)
        {
            Contract.Requires(selectedTargetDelegate != null, context => "selectedTargetDelegate != null");

            WoWUnit mob = null;
            var engagementWatchdogTimer = new WaitTimer(TimeSpan.FromMilliseconds(7000));

            var blacklistForPullTime = TimeSpan.FromSeconds(3 * 60);
            var minTimeToEngagement = TimeSpan.FromSeconds(3);

            return new PrioritySelector(
                new ActionFail(context =>
                {
                    var selectedTarget = selectedTargetDelegate(context);
                    var isMobChanged = (mob != selectedTarget);

                    mob = selectedTarget;

                    if (Query.IsViableForFighting(mob))
                    {
                        // If we have mob aggro, cancel the engagement timer...
                        if (mob.Aggro)
                            { engagementWatchdogTimer = null; }

                        // Otherwise, start watchdog timer for engaging mob...
                        else if (isMobChanged || (engagementWatchdogTimer == null))
                        {
                            engagementWatchdogTimer =
                                new WaitTimer(Utility.CalculateMaxTimeToDestination(mob.Location, false)
                                              + minTimeToEngagement);
                            engagementWatchdogTimer.Reset();
                        }

                        // If we are unable to engage an unaggro'd mob in a reasonable amount of time,
                        // cancel attack, and blacklist mob...
                        if ((engagementWatchdogTimer != null) && engagementWatchdogTimer.IsFinished)
                        {
                            QBCLog.Warning("Unable to  engage {0} in {1}--pull-blacklisted for {2}",
                                mob.Name,
                                Utility.PrettyTime(engagementWatchdogTimer.WaitTime),
                                Utility.PrettyTime(blacklistForPullTime));
                            Query.BlacklistForPulling(mob, blacklistForPullTime);
                            BotPoi.Clear();
                            Me.ClearTarget();
                            mob = null;
                            return RunStatus.Failure;
                        }

                        // Mark target and notify Combat Routine of target, if needed...
                        if (Me.CurrentTarget != mob)
                        {
                            mob.Target();
                            BotPoi.Current = new BotPoi(mob, PoiType.Kill);
                        }

                        if (Me.Mounted)
                            { Mount.Dismount(); }
                    }

                    return RunStatus.Failure;   // fall through
                }),

                new Decorator(context => Query.IsViable(mob),
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
            );
        }

        
        /// <summary>
        /// Targets and kills any mob targeting Self or Pet.
        /// </summary>
        /// <returns></returns>
        public Composite UtilityBehaviorPS_SpankMobTargetingUs(Func<object, IEnumerable<WoWUnit>> excludedUnitsDelegate = null)
        {
            excludedUnitsDelegate = excludedUnitsDelegate ?? (context => Enumerable.Empty<WoWUnit>());

            WoWUnit mob = null;

            Func<object, bool> isInterestingToUs =
                (obj) =>
                {
                    var wowUnit = obj as WoWUnit;

                    return
                        Query.IsViableForPulling(wowUnit, IgnoreMobsInBlackspots, NonCompeteDistance)
                        && (wowUnit.IsTargetingMeOrPet
                            || wowUnit.IsTargetingAnyMinion
                            || wowUnit.IsTargetingMyPartyMember)
                        // exclude opposing faction: both players and their pets show up as "PlayerControlled"
                        && !wowUnit.PlayerControlled
                        // Do not pull mobs on the AvoidMobs list
                        && !ProfileManager.CurrentOuterProfile.AvoidMobs.Contains(wowUnit.Entry)
                        // exclude any units that are candidates for interacting
                        && !excludedUnitsDelegate(obj).Contains(wowUnit);                                                     
                };
                    
            Func<object, WoWUnit> mobTargetingUs =
            (context) =>
            {
                // If a mob is targeting us, deal with it immediately, so subsequent activities won't be interrupted...
                // NB: This can happen if we 'drag mobs' behind us on the way to our destination.
                if (!Query.IsViableForPulling(mob, IgnoreMobsInBlackspots, NonCompeteDistance))
                {
                    using (StyxWoW.Memory.AcquireFrame())
                    {
                        mob =
                            (from wowUnit in ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                            where isInterestingToUs(wowUnit)
                            orderby wowUnit.SurfacePathDistance()
                            select wowUnit)
                            .FirstOrDefault();
                    }

                    if (Query.IsViable(mob))
                    {
                        mob.Target();
                        BotPoi.Current = new BotPoi(mob, PoiType.Kill);
                        Navigator.PlayerMover.MoveStop();
                        Me.SetFacing(mob.Location);
                        TreeRoot.StatusText = string.Format("Spanking {0} that has targeted us.",
                            mob.Name);
                    }
                }

                return mob;
            };

            // Spank any mobs we found being naughty...
            return UtilityBehaviorPS_SpankMob(context => mobTargetingUs(context));
        }


        // 24Feb2013-08:11UTC chinajade
        public Composite UtilityBehaviorPS_SpankMobWithinAggroRange(ProvideWoWPointDelegate destinationDelegate,
                                                                    ProvideDoubleDelegate extraRangePaddingDelegate = null,
                                                                    Func<IEnumerable<int>> excludedUnitIdsDelegate = null)
        {
            Contract.Requires(destinationDelegate != null, context => "destinationDelegate != null");
            extraRangePaddingDelegate = extraRangePaddingDelegate ?? (context => 0.0);
            excludedUnitIdsDelegate = excludedUnitIdsDelegate ?? (() => Enumerable.Empty<int>());

            WoWUnit mob = null;

            Func<WoWUnit, bool> isInterestingToUs =
                (wowUnit) =>
                {
                    return
                        Query.IsViableForPulling(wowUnit, IgnoreMobsInBlackspots, NonCompeteDistance)
                        && wowUnit.IsHostile
                        && wowUnit.IsUntagged()
                        // exclude opposing faction: both players and their pets show up as "PlayerControlled"
                        && !wowUnit.PlayerControlled
                        // exclude any units that are candidates for interacting
                        && !excludedUnitIdsDelegate().Contains((int)wowUnit.Entry)
                        // Do not pull mobs on the AvoidMobs list
                        && !ProfileManager.CurrentOuterProfile.AvoidMobs.Contains(wowUnit.Entry)
                        && (wowUnit.Location.SurfacePathDistance(destinationDelegate(wowUnit)) <= (wowUnit.MyAggroRange + extraRangePaddingDelegate(wowUnit)));
                };
        
            Func<object, WoWUnit> mobWithinAggroRange =
            (context) =>
            {
                if (!Query.IsViableForPulling(mob, IgnoreMobsInBlackspots, NonCompeteDistance))
                {
                    // If a mob is within aggro range of our destination, deal with it immediately...
                    // Otherwise, it will interrupt our attempt to interact or use items.
                    using (StyxWoW.Memory.AcquireFrame())
                    {
                        mob =
                            (from wowUnit in ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                             where isInterestingToUs(wowUnit)
                             orderby wowUnit.SurfacePathDistance()
                             select wowUnit)
                            .FirstOrDefault();
                    }

                    if (Query.IsViable(mob))
                    {
                        mob.Target();
                        BotPoi.Current = new BotPoi(mob, PoiType.Kill);
                        TreeRoot.StatusText = string.Format("Spanking {0}({1}) within aggro range ({2:F1}) of our destination.",
                            mob.Name,
                            mob.Entry,
                            (mob.MyAggroRange + extraRangePaddingDelegate(context)));
                    }
                }

                return mob;
            };

            // Spank any mobs we found being naughty...
            return UtilityBehaviorPS_SpankMob(context => mobWithinAggroRange(context));
        }
    }
}