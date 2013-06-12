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
            private WoWUnit CachedMob { get; set; }
            private WaitTimer _engagementTimer = new WaitTimer(TimeSpan.FromMilliseconds(7000));

            // Convenience properties...
            private static readonly TimeSpan BlacklistForPullTime = TimeSpan.FromSeconds(3 * 60);
            private static readonly TimeSpan MinTimeToEngagement = TimeSpan.FromSeconds(3);


            private List<Composite> CreateChildren()
            {
                return new List<Composite>()
                {
                    new PrioritySelector(
                        new ActionFail(context =>
                        {
                            var selectedTarget = SelectedTargetDelegate(context);
                            var isMobChanged = (CachedMob != selectedTarget);

                            CachedMob = selectedTarget;

                            bool hasAggro = Query.IsViable(CachedMob) && CachedMob.Aggro;
                            if (hasAggro)
                                { _engagementTimer = null; }

                            else if (Query.IsViable(CachedMob) && (isMobChanged || (_engagementTimer == null)))
                            {
                                _engagementTimer =
                                    new WaitTimer(Utility.CalculateMaxTimeToDestination(CachedMob.Location, false)
                                                  + MinTimeToEngagement);
                                _engagementTimer.Reset();
                            }    
                        }),

                        new Decorator(context => Query.IsViableForFighting(CachedMob),
                            new PrioritySelector(
                                // If we are unable to engage an unaggro'd mob in a reasonable amount of time,
                                // cancel attack, and blacklist mob...
                                new Decorator(context => (_engagementTimer != null)
                                                            && _engagementTimer.IsFinished,
                                    new Action(context =>
                                    {
                                        if (!CachedMob.Aggro)
                                        {
                                            QBCLog.Warning("Unable to  engage {0} in {1}--pull-blacklisted for {2}",
                                                CachedMob.Name,
                                                Utility.PrettyTime(_engagementTimer.WaitTime),
                                                Utility.PrettyTime(BlacklistForPullTime));
                                            Query.BlacklistForPulling(CachedMob, BlacklistForPullTime);
                                            BotPoi.Clear();
                                            Me.ClearTarget();
                                            CachedMob = null;
                                        }
                                        _engagementTimer = null;
                                    })), 
                    
                                new Decorator(context => Me.CurrentTarget != CachedMob,
                                    new Action(context =>
                                    {
                                        CachedMob.Target();
                                        BotPoi.Current = new BotPoi(CachedMob, PoiType.Kill);
                                        return RunStatus.Failure; // fall through
                                    })),

                                // NB: Some Combat Routines (CR) will stall when asked to kill things from too far away.
                                // So, we manually move the toon within reasonable range before asking the CR to kill it.
                                // Note that some behaviors will set the PullDistance to zero or one while they run, but we don't want to
                                // actually get that close to engage, so we impose a lower bound of 23 feet that we move before handing
                                // things over to the combat routine.
                                // new Decorator(context => _ubpsSpankMob_Mob.Distance > Math.Max(23, CharacterSettings.Instance.PullDistance),
                                //    UtilityBehaviorPS_MoveTo(context => _ubpsSpankMob_Mob.Location,
                                //                            context => _ubpsSpankMob_Mob.Name)),
                                new Decorator(context => Me.Mounted,
                                    new Action(context => { Mount.Dismount(); })),

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
                        )
                };
            }
        }
    }


    public partial class UtilityBehaviorPS
    {
        /// <summary>
        /// Targets and kills any mob targeting Self or Pet.
        /// </summary>
        /// <returns></returns>
        public class SpankMobTargetingUs : PrioritySelector
        {
            public SpankMobTargetingUs(Func<object, IEnumerable<WoWUnit>> excludedUnitsDelegate = null)
            {
                ExcludedUnitsDelegate = excludedUnitsDelegate ?? (context => Enumerable.Empty<WoWUnit>());

                Children = CreateChildren();
            }


            // BT contruction-time properties...
            Func<object, IEnumerable<WoWUnit>> ExcludedUnitsDelegate { get; set; }

            // BT visit-time properties...
            private WoWUnit CachedMob;


            private List<Composite> CreateChildren()
            {            
                return new List<Composite>()
                {
                    new PrioritySelector(
                        // If a mob is targeting us, deal with it immediately, so subsequent activities won't be interrupted...
                        // NB: This can happen if we 'drag mobs' behind us on the way to our destination.
                        new Decorator(context => !Query.IsViableForPulling(CachedMob, true, 0.0),
                            new PrioritySelector(
                                new ActionFail(context =>
                                {
                                    using (StyxWoW.Memory.AcquireFrame())
                                    {
                                        CachedMob =
                                           (from wowUnit in ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                                            where IsInterestingToUs(wowUnit)
                                            orderby wowUnit.SurfacePathDistance()
                                            select wowUnit)
                                            .FirstOrDefault();
                                    }
                                }),
                                new Decorator(context => Query.IsViable(CachedMob),
                                    new UtilityBehaviorPS.MoveStop())
                            )),

                        // Spank any mobs we find being naughty...
                        new Decorator(context => Query.IsViable(CachedMob),
                            new PrioritySelector(
                                new CompositeThrottle(TimeSpan.FromMilliseconds(3000),
                                    new Action(context =>
                                    {
                                        TreeRoot.StatusText = string.Format("Spanking {0} that has targeted us.",
                                            CachedMob.Name);                                    
                                    })),
                                new UtilityBehaviorPS.SpankMob(context => CachedMob)
                            ))
                    )
                };
            }


            public bool IsInterestingToUs(object context)
            {
                var wowUnit = context as WoWUnit;

                return
                    Query.IsViableForPulling(wowUnit, true, 0.0)
                    && (wowUnit.IsTargetingMeOrPet
                        || wowUnit.IsTargetingAnyMinion
                        || wowUnit.IsTargetingMyPartyMember)
                    // exclude opposing faction: both players and their pets show up as "PlayerControlled"
                    && !wowUnit.PlayerControlled
                    // Do not pull mobs on the AvoidMobs list
                    && !ProfileManager.CurrentOuterProfile.AvoidMobs.Contains(wowUnit.Entry)
                    // exclude any units that are candidates for interacting
                    && !ExcludedUnitsDelegate(context).Contains(wowUnit);                                                     
            }
        }
    }


    public partial class UtilityBehaviorPS
    {
        public class SpankMobWithinAggroRange : PrioritySelector
        {
            public SpankMobWithinAggroRange(ProvideWoWPointDelegate destinationDelegate,
                                            ProvideDoubleDelegate extraRangePaddingDelegate = null,
                                            Func<IEnumerable<int>> excludedUnitIdsDelegate = null)
            {
                Contract.Requires(destinationDelegate != null, context => "destinationDelegate != null");

                DestinationDelegate = destinationDelegate;
                ExcludedUnitIdsDelegate = excludedUnitIdsDelegate ?? (() => Enumerable.Empty<int>());
                ExtraRangePaddingDelegate = extraRangePaddingDelegate ?? (context => 0.0);

                Children = CreateChildren();
            }


            // BT contruction-time properties...
            private ProvideWoWPointDelegate DestinationDelegate { get; set; }
            private Func<IEnumerable<int>> ExcludedUnitIdsDelegate { get; set; }
            private ProvideDoubleDelegate ExtraRangePaddingDelegate { get; set; }

            // BT visit-time properties...
            private WoWUnit CachedMob { get; set; }


            // 24Feb2013-08:11UTC chinajade
            private List<Composite> CreateChildren()
            {
                return new List<Composite>()
                {
                    new Decorator(context => !Me.Combat,
                        new PrioritySelector(
                            // If a mob is within aggro range of our destination, deal with it immediately...
                            // Otherwise, it will interrupt our attempt to interact or use items.
                            new Decorator(context => !Query.IsViableForPulling(CachedMob, true, 20.0),
                                new Action(context =>
                                {
                                    CachedMob =
                                       (from wowUnit in ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                                        where IsInterestingToUs(context, wowUnit)
                                        orderby wowUnit.SurfacePathDistance()
                                        select wowUnit)
                                        .FirstOrDefault();

                                    return RunStatus.Failure;   // fall through
                                })),

                            // Spank any mobs we find being naughty...
                            new CompositeThrottle(context => Query.IsViable(CachedMob),
                                TimeSpan.FromMilliseconds(3000),
                                new Action(context =>
                                {
                                    TreeRoot.StatusText = string.Format("Spanking {0}({1}) within aggro range ({2:F1}) of our destination.",
                                        CachedMob.Name,
                                        CachedMob.Entry,
                                        (CachedMob.MyAggroRange + ExtraRangePaddingDelegate(context)));                                    
                                })),
                            new Decorator(context => Query.IsViable(CachedMob),
                                new UtilityBehaviorPS.SpankMob(context => CachedMob))
                        ))
                };
            }


            private bool IsInterestingToUs(object context, WoWUnit wowUnit)
            {
                return
                    Query.IsViableForPulling(wowUnit, true, 0.0)
                    && wowUnit.IsHostile
                    && wowUnit.IsUntagged()
                    // exclude opposing faction: both players and their pets show up as "PlayerControlled"
                    && !wowUnit.PlayerControlled
                    // exclude any units that are candidates for interacting
                    && !ExcludedUnitIdsDelegate().Contains((int)wowUnit.Entry)
                    // Do not pull mobs on the AvoidMobs list
                    && !ProfileManager.CurrentOuterProfile.AvoidMobs.Contains(wowUnit.Entry)
                    && (wowUnit.Location.SurfacePathDistance(DestinationDelegate(context)) <= (wowUnit.MyAggroRange + ExtraRangePaddingDelegate(context)));
            }
        }
    }
}