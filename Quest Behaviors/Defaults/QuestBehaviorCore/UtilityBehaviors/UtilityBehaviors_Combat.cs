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
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Routines;
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
        /// This behavior quits attacking the mob, once the mob is targeting us.
        /// </summary>
        // 24Feb2013-08:11UTC chinajade
        public Composite UtilityBehaviorPS_GetMobsAttention(ProvideWoWUnitDelegate selectedTargetDelegate)
        {
            Contract.Requires(selectedTargetDelegate != null, context => "selectedTargetDelegate != null");

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
                    new Decorator(context => RoutineManager.Current.NeedHeal,
                        new Action(context => { RoutineManager.Current.Heal(); })),
                    new Decorator(context => RoutineManager.Current.RestBehavior != null,
                        RoutineManager.Current.RestBehavior),
                    new Decorator(context => RoutineManager.Current.NeedRest,
                        new Action(context => { RoutineManager.Current.Rest(); })),
                    LevelBot.CreateLootBehavior()
                ));
        }


        // 30May2013-04:52UTC chinajade
        public Composite UtilityBehaviorPS_Target(ProvideWoWObjectDelegate selectedTargetDelegate)
        {
            return new Action(context =>
            {
                var selectedTarget = selectedTargetDelegate(context).ToUnit();

                if ((selectedTarget != null) && (Me.CurrentTarget != selectedTarget))
                {
                    selectedTarget.Target();
                    return RunStatus.Success;
                }

                return RunStatus.Failure;
            });
        }


        // 16May2013-04:52UTC chinajade
        public static Composite UtilityBehaviorPS_MiniCombatRoutine()
        {
            return new Switch<WoWClass>(context => Me.Class,
                // Default
                TryAutoAttack(),

                new SwitchArgument<WoWClass>(WoWClass.DeathKnight,
                    new PrioritySelector(
                        TryCast(49998),     // Death Strike: http://wowhead.com/spell=49998
                        TryAutoAttack()
                    )),

                new SwitchArgument<WoWClass>(WoWClass.Druid,
                    new PrioritySelector(
                        TryCast(5176, context => !Me.HasAura(768)),      // Wrath: http://wowhead.com/spell=5176
                        TryCast(768, context => !Me.HasAura(768)),       // Cat Form: http://wowhead.com/spell=768
                        TryCast(1822),      // Rake: http://wowhead.com/spell=1822
                        TryCast(22568),     // Ferocious Bite: http://wowhead.com/spell=22568
                        TryCast(33917),     // Mangle: http://wowhead.com/spell=33917
                        TryAutoAttack()
                    )),

                new SwitchArgument<WoWClass>(WoWClass.Hunter,
                    new PrioritySelector(
                        TryCast(3044),      // Arcane Shot: http://wowhead.com/spell=3044
                        TryCast(56641),     // Steady Shot: http://wowhead.com/spell=56641
                        TryAutoAttack()
                    )),

                new SwitchArgument<WoWClass>(WoWClass.Mage,
                    new PrioritySelector(
                        TryCast(44614),     // Frostfire Bolt: http://wowhead.com/spell=44614
                        TryCast(126201),    // Frostbolt: http://wowhead.com/spell=126201
                        TryCast(2136),      // Fire Blast: http://wowhead.com/spell=2136
                        TryAutoAttack()
                    )),

                new SwitchArgument<WoWClass>(WoWClass.Monk,
                    new PrioritySelector(
                        TryCast(100780),    // Jab: http://wowhead.com/spell=100780
                        TryCast(100787),    // Tiger Palm: http://wowhead.com/spell=100787
                        TryAutoAttack()
                    )),

                new SwitchArgument<WoWClass>(WoWClass.Paladin,
                    new PrioritySelector(
                        TryCast(35395),     // Crusader Strike: http://wowhead.com/spell=35395
                        TryCast(20271),     // Judgment: http://wowhead.com/spell=20271
                        TryAutoAttack()
                    )),

                new SwitchArgument<WoWClass>(WoWClass.Priest,
                    new PrioritySelector(
                        TryCast(15407),     // Mind Flay: http://wowhead.com/spell=15407
                        TryCast(585),       // Smite: http://wowhead.com/spell=585
                        TryAutoAttack()
                    )),

                new SwitchArgument<WoWClass>(WoWClass.Rogue,
                    new PrioritySelector(
                        TryCast(2098),      // Eviscerate: http://wowhead.com/spell=2098
                        TryCast(1752),      // Sinster Strike: http://wowhead.com/spell=1752
                        TryAutoAttack()
                    )),

                new SwitchArgument<WoWClass>(WoWClass.Shaman,
                    new PrioritySelector(
                        TryCast(403),       // Lightning Bolt: http://wowhead.com/spell=403
                        TryCast(73899),     // Primal Strike: http://wowhead.com/spell=73899
                        TryAutoAttack()
                    )),

                new SwitchArgument<WoWClass>(WoWClass.Warlock,
                    new PrioritySelector(
                        TryCast(686),       // Shadow Bolt: http://wowhead.com/spell=686
                        TryAutoAttack()
                    )),

                new SwitchArgument<WoWClass>(WoWClass.Warrior,
                    new PrioritySelector(
                        TryCast(78),        // Heroic Strike: http://wowhead.com/spell=78
                        TryCast(34428),     // Victory Rush: http://wowhead.com/spell=34428
                        TryAutoAttack()
                    ))
            );
        }


        private static Composite TryCast(int spellId, ProvideBoolDelegate requirements = null)
        {
            requirements = requirements ?? (context => true);

            return new Decorator(context => SpellManager.CanCast(spellId) && requirements(context),
                new Action(context =>
                {
                    QBCLog.DeveloperInfo("MiniCombatRoutine used {0}", GetSpellNameFromId(spellId));
                    SpellManager.Cast(spellId);
                }));
        }


        private static Composite TryAutoAttack()
        {
            return new Decorator(context => !Me.IsAutoAttacking,
                new Action(context => { Lua.DoString("StartAttack()"); }));
        }
    }
}