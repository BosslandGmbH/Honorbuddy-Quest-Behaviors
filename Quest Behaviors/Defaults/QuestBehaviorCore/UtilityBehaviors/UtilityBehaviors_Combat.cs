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

using Bots.Grind;
using Styx.CommonBot;
using Styx.CommonBot.Routines;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public abstract partial class QuestBehaviorBase
    {
        /// <summary>
        /// This behavior quits attacking the mob, once the mob is targeting us.
        /// </summary>
        // 24Feb2013-08:11UTC chinajade
        public Composite UtilityBehaviorPS_GetMobsAttention(ProvideWoWUnitDelegate selectedTargetDelegate)
        {
            ContractRequires(selectedTargetDelegate != null, context => "selectedTargetDelegate != null");

            return new PrioritySelector(
                new Action(context =>
                {
                    _ubpsGetMobsAttention_Mob = selectedTargetDelegate(context);
                    return RunStatus.Failure; // fall through
                }),
                new Decorator(context => IsViableForFighting(_ubpsGetMobsAttention_Mob)
                                        && !_ubpsGetMobsAttention_Mob.IsTargetingMeOrPet,
                    new PrioritySelector(
                        new CompositeThrottle(TimeSpan.FromSeconds(3),
                            new Action(context =>
                            {
                                TreeRoot.StatusText = string.Format("Getting attention of {0}", _ubpsGetMobsAttention_Mob.Name);
                                return RunStatus.Failure;   // fall through
                            })),
                        UtilityBehaviorPS_SpankMob(selectedTargetDelegate)))
                    );
        }
        private WoWUnit _ubpsGetMobsAttention_Mob;


        // 11Apr2013-04:52UTC chinajade
        public Composite UtilityBehaviorPS_HealAndRest()
        {
            // The NeedHeal and NeedCombatBuffs are part of legacy custom class support
            // and pair with the Heal and CombatBuff virtual methods.  If a legacy custom class is loaded,
            // HonorBuddy automatically wraps calls to Heal and CustomBuffs it in a Decorator checking those for you.
            // So, no need to duplicate that work here.
            return new Decorator(context => !Me.Combat,
                new PrioritySelector(
                    new Decorator(context => RoutineManager.Current.HealBehavior != null,
                        RoutineManager.Current.HealBehavior),
                    new Decorator(context => RoutineManager.Current.RestBehavior != null,
                        RoutineManager.Current.RestBehavior),
                    LevelBot.CreateLootBehavior()
                ));
        }
        
    }
}