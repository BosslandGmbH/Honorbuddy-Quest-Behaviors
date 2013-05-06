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
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
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
            ContractRequires(selectedTargetDelegate != null, context => "selectedTargetDelegate != null");

            return new PrioritySelector(
                new Action(context =>
                {
                    _ubpsSpankMob_Mob = selectedTargetDelegate(context);
                    return RunStatus.Failure;   // fall through     
                }),
                new Decorator(context => IsViableForFighting(_ubpsSpankMob_Mob),
                    new PrioritySelector(
                        new Decorator(context => Me.CurrentTarget != _ubpsSpankMob_Mob,
                            new Action(context =>
                            {
                                _ubpsSpankMob_Mob.Target();
                                BotPoi.Current = new BotPoi(_ubpsSpankMob_Mob, PoiType.Kill);
                                return RunStatus.Failure; // fall through
                            })),

                        // NB: Some Combat Routines (CR) will stall when asked to kill things from too far away.
                        // So, we manually move the toon within reasonable range before asking the CR to kill it.
                        // Note that some behaviors will set the PullDistance to zero or one while they run, but we don't want to
                        // actually get that close to engage, so we impose a lower bound of 23 feet that we move before handing
                        // things over to the combat routine.
                        new Decorator(context => _ubpsSpankMob_Mob.Distance > Math.Max(23, CharacterSettings.Instance.PullDistance),
                            UtilityBehaviorPS_MoveTo(context => _ubpsSpankMob_Mob.Location,
                                                    context => _ubpsSpankMob_Mob.Name)),
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
                );
        }
        private WoWUnit _ubpsSpankMob_Mob;

        
        /// <summary>
        /// Targets and kills any mob targeting Self or Pet.
        /// </summary>
        /// <returns></returns>
        public Composite UtilityBehaviorPS_SpankMobTargetingUs(Func<object, IEnumerable<WoWUnit>> excludedUnitsDelegate = null)
        {
            excludedUnitsDelegate = excludedUnitsDelegate ?? (context => Enumerable.Empty<WoWUnit>());

            Func<object, bool> isInterestingToUs =
                (obj) =>
                {
                    var wowUnit = obj as WoWUnit;

                    return
                        IsViableForFighting(wowUnit)
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
                            
            return new PrioritySelector(
                // If a mob is targeting us, deal with it immediately, so subsequent activities won't be interrupted...
                // NB: This can happen if we 'drag mobs' behind us on the way to our destination.
                new Decorator(context => !isInterestingToUs(_ubpsSpankMobTargetingUs_Mob),
                    new Action(context =>
                    {
                        using (StyxWoW.Memory.AcquireFrame())
                        {
                            _ubpsSpankMobTargetingUs_Mob =
                                ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                                .Where(u => isInterestingToUs(u))
                                .OrderBy(u => u.DistanceSqr)
                                .FirstOrDefault();
                        }

                        return RunStatus.Failure;   // fall through
                    })),

                // Spank any mobs we find being naughty...
                new CompositeThrottle(context => _ubpsSpankMobTargetingUs_Mob != null,
                    TimeSpan.FromMilliseconds(3000),
                    new Action(context =>
                    {
                        TreeRoot.StatusText = string.Format("Spanking {0} that has targeted us.",
                            _ubpsSpankMobTargetingUs_Mob.Name);                                    
                    })),
                UtilityBehaviorPS_SpankMob(context => _ubpsSpankMobTargetingUs_Mob)
            );
        }
        private WoWUnit _ubpsSpankMobTargetingUs_Mob;


        // 24Feb2013-08:11UTC chinajade
        public Composite UtilityBehaviorPS_SpankMobWithinAggroRange(ProvideWoWPointDelegate destinationDelegate,
                                                                    ProvideDoubleDelegate extraRangePaddingDelegate = null,
                                                                    Func<IEnumerable<int>> excludedUnitIdsDelegate = null)
        {
            ContractRequires(destinationDelegate != null, context => "destinationDelegate != null");
            extraRangePaddingDelegate = extraRangePaddingDelegate ?? (context => 0.0);
            excludedUnitIdsDelegate = excludedUnitIdsDelegate ?? (() => Enumerable.Empty<int>());

            Func<object, bool> isInterestingToUs =
                (obj) =>
                {
                    WoWUnit wowUnit = obj as WoWUnit;

                    return
                        IsViableForFighting(wowUnit)
                        && wowUnit.IsHostile
                        && wowUnit.IsUntagged()
                        // exclude opposing faction: both players and their pets show up as "PlayerControlled"
                        && !wowUnit.PlayerControlled
                        // exclude any units that are candidates for interacting
                        && !excludedUnitIdsDelegate().Contains((int)wowUnit.Entry)
                        // Do not pull mobs on the AvoidMobs list
                        && !ProfileManager.CurrentOuterProfile.AvoidMobs.Contains(wowUnit.Entry)
                        && (wowUnit.Location.SurfacePathDistance(destinationDelegate(obj)) <= (wowUnit.MyAggroRange + extraRangePaddingDelegate(obj)));
                };
        
            return new Decorator(context => !Me.Combat,
                new PrioritySelector(
                    // If a mob is within aggro range of our destination, deal with it immediately...
                    // Otherwise, it will interrupt our attempt to interact or use items.
                    new Decorator(context => !isInterestingToUs(_ubpsSpankMobWithinAggroRange_Mob),
                        new Action(context =>
                        {
                            _ubpsSpankMobWithinAggroRange_Mob =
                                ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                                .Where(o => isInterestingToUs(o))
                                .OrderBy(u => u.DistanceSqr)
                                .FirstOrDefault();

                            return RunStatus.Failure;   // fall through
                        })),

                    // Spank any mobs we find being naughty...
                    new CompositeThrottle(context => _ubpsSpankMobWithinAggroRange_Mob != null,
                        TimeSpan.FromMilliseconds(3000),
                        new Action(context =>
                        {
                            TreeRoot.StatusText = string.Format("Spanking {0}({1}) within aggro range ({2:F1}) of our destination.",
                                _ubpsSpankMobWithinAggroRange_Mob.Name,
                                _ubpsSpankMobWithinAggroRange_Mob.Entry,
                                (_ubpsSpankMobWithinAggroRange_Mob.MyAggroRange + extraRangePaddingDelegate(context)));                                    
                        })),
                    UtilityBehaviorPS_SpankMob(context => _ubpsSpankMobWithinAggroRange_Mob)
            ));
        }
        private WoWUnit _ubpsSpankMobWithinAggroRange_Mob;
    }
}