using Hera.Config;
using Hera.Helpers;
using Hera.SpellsMan;
using Styx.Logic;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;

namespace Hera
{
    public partial class Fpsware
    {

        // ******************************************************************************************
        //
        // All our BTs and logic will be in here - LET THE GAMES BEGIN!
        //
        // Everything we tell HB to do will be in here:
        //   * Rest
        //   * Heal
        //   * Buffs
        //   * Pull
        //   * Combat
        //
        // ******************************************************************************************



        // Combat. 
        // Today is a good day to die!
        #region Combat!
        // _combatBehavior is a class that will hold our logic. 
        private Composite _combatBehavior;
        public override Composite CombatBehavior
        {
            get
            {
                if (_combatBehavior == null) { Utils.Log("Creating 'Combat' behavior"); _combatBehavior = CreateCombatBehavior(); }  
                
                return _combatBehavior;
            }
        }

        // Our entire combat sequence takes place in here
        //
        private PrioritySelector CreateCombatBehavior()
        {
            return new PrioritySelector(

                // All spells are checked in the order listed below, a prioritised order
                // Put the most important spells at the top of the list

                // This should ALWAYS put us within melee (interact) range.
                new Decorator( ret => Me.GotTarget && !CT.Fleeing && Target.IsDistanceMoreThan(CT.InteractRange) && !Me.IsMoving,
                    new Action(ret => Movement.MoveTo(CT.Location))),

                // If we are within melee (interact) range and we're moving then stop moving.
                new Decorator( ret => Me.GotTarget && !CT.Fleeing && Target.IsDistanceLessThan(CT.InteractRange - 2.5f) && Me.IsMoving,
                    new Action(ret => Movement.StopMoving())),

                // Check if we get aggro during the pull
                // This is in here and not the pull because we are in combat at this point
                new NeedToCheckAggroOnPull(new CheckAggroOnPull()),

                // Pummel - Prevent casters
                new NeedToPummel(new Pummel()),

                // Battle Shout
                new NeedToShout(new Shout()),

                // Target the most suitable Mob; lowest health and closest
                new NeedToTargetMob(new TargetMob()),

                // Abort combat is the target's health is 95% + after 30 seconds of combat
                new NeedToCheckCombatTimer(new CheckCombatTimer()),

                // Berserker Rage
                new NeedToBerserkerRage(new BerserkerRage()),

                // Kind of important that we face the target
                new NeedToFaceTarget(new FaceTarget()),

                // Auto attack
                new NeedToAutoAttack(new AutoAttack()),

                // Distance check. Make sure we are in attacking range at all times
                new NeedToCheckDistance(new CheckDistance()),

                // Face Target
                new NeedToFaceTarget(new FaceTarget()),

                // Rend
                new NeedToRend(new Rend()),

                // Mortal Strike
                new NeedToMortalStrike(new MortalStrike()),

                // ************************************************************************************
                // Important/time sensative spells here
                // These are spells that need to be case asap

                // Stance Dance
                // If we're Fury spec we should be in Berserker Stance 
                // If we're Arms spec we should be in Battle Stance
                new NeedToStanceDanceBerserker(new StanceDanceBerserker()),
                new NeedToStanceDanceBattle(new StanceDanceBattle()),

                // Intercept
                // If we can use Intercept to charge the target
                new NeedToIntercept(new Intercept()),

                // Charge
                new NeedToCharge(new Charge()),

                // Bloodsurge - SLAM!
                new NeedToBloodsurge(new Bloodsurge()),

                // Battle Trance
                new NeedToBattleTrance(new BattleTrance()),

                // Hamstring - Try to prevent runners
                new NeedToHamstring(new Hamstring()),

                // Heroic Throw
                // Used on runners
                new NeedToHeroicThrow(new HeroicThrow()),

                // Execute
                new NeedToExecute(new Execute()),

                // Victory Rush
                new NeedToVictoryRush(new VictoryRush()),

                // Colossus Smash
                new NeedToColossusSmash(new ColossusSmash()),

                // Sunder Armor
                new NeedToSunderArmor(new SunderArmor()),

                // Deadly Calm
                // When you have adds and if your rage is below 30
                new NeedToDeadlyCalm(new DeadlyCalm()),

                // Sweeping Strikes
                new NeedToSweepingStrikes(new SweepingStrikes()),

                // Bladestorm
                // Used only when you have adds
                // If you know Sweeping Strikes it will use that first, then Bladestorm
                new NeedToBladestorm(new Bladestorm()),

                // Overpower
                new NeedToOverpower(new Overpower()),

                // Retaliation
                new NeedToRetaliation(new Retaliation()),

                // Recklessness
                new NeedToRecklessness(new Recklessness()),

                // Colossus Smash
                new NeedToColossusSmash(new ColossusSmash()),

                // Death Wish
                new NeedToDeathWish(new DeathWish()),

                // Raging Blow
                new NeedToRagingBlow(new RagingBlow()),

                // ===================================================



                // Thunder Clap
                // Always
                new NeedToThunderClap(new ThunderClap()),

                // Whirlwind
                // Always on adds
                new NeedToWhirlwind(new Whirlwind()),

                // Cleave
                new NeedToCleave(new Cleave()),



                
                // Revenge
                new NeedToRevenge(new Revenge()),

                // Bloodthirst
                new NeedToBloodthirst(new Bloodthirst()),
                
                // Heroic Strike
                new NeedToHeroicStrike(new HeroicStrike()),

                // Strike
                new NeedToStrike(new Strike())

                );
        }
        #endregion


        // ******************************************************************************************
        //
        // This is where the priority selectors start
        //


        

        #region Strike
        public class NeedToStrike : Decorator
        {
            public NeedToStrike(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                string spellName = "Strike";
                if (!Utils.IsCommonChecksOk(spellName, false)) return false;
                if (Spell.IsKnown("Heroic Strike")) return false;
                if (Spell.IsKnown("Bloodthirst")) return false;
                if (!Self.IsRageAbove(Settings.AttackRage)) return false;

                return (Spell.CanCast(spellName));
            }
        }

        public class Strike : Action
        {
            protected override RunStatus Run(object context)
            {
                string spellName = "Strike";
                bool result = Spell.Cast(spellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Heroic Strike

        public class NeedToHeroicStrike : Decorator
        {
            public NeedToHeroicStrike(Composite child) : base(child)
            {
            }

            protected override bool CanRun(object context)
            {
                string spellName = "Heroic Strike";
                if (!Utils.IsCommonChecksOk(spellName, false)) return false;
                if (!Self.IsRageAbove(Settings.AttackRage)) return false;
                if (Spell.IsKnown("Bloodthirst") && !Self.IsRageAbove(60)) return false;

                return (Spell.CanCast(spellName));
            }
        }

        public class HeroicStrike : Action
        {
            protected override RunStatus Run(object context)
            {
                string spellName = "Heroic Strike";
                bool result = Spell.Cast(spellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

        #region Bloodthirst

        public class NeedToBloodthirst : Decorator
        {
            public NeedToBloodthirst(Composite child) : base(child)
            {
            }

            protected override bool CanRun(object context)
            {
                string spellName = "Bloodthirst";
                if (!Utils.IsCommonChecksOk(spellName, false)) return false;
                //if (!Self.IsRageAbove(45)) return false;

                return (Spell.CanCast(spellName));
            }
        }

        public class Bloodthirst : Action
        {
            protected override RunStatus Run(object context)
            {
                string spellName = "Bloodthirst";
                bool result = Spell.Cast(spellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

        #region Bloodsurge SLAM

        public class NeedToBloodsurge : Decorator
        {
            public NeedToBloodsurge(Composite child)
                : base(child)
            {
            }

            protected override bool CanRun(object context)
            {
                string spellName = "Slam";
                string procBuffName = "Bloodsurge";
                if (!Utils.IsCommonChecksOk(spellName, false)) return false;
                if (!Me.ActiveAuras.ContainsKey(procBuffName)) return false;

                return (Spell.CanCast(spellName));
            }
        }

        public class Bloodsurge : Action
        {
            protected override RunStatus Run(object context)
            {
                string spellName = "Slam";
                bool result = Spell.Cast(spellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

        #region Victory Rush
        public class NeedToVictoryRush : Decorator
        {
            public NeedToVictoryRush(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                string SpellName = "Victory Rush";
                if (!Utils.IsCommonChecksOk(SpellName, false)) return false;

                return (Spell.CanCast(SpellName));
            }
        }

        public class VictoryRush : Action
        {
            protected override RunStatus Run(object context)
            {
                string SpellName = "Victory Rush";
                bool result = Spell.Cast(SpellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Overpower

        public class NeedToOverpower : Decorator
        {
            public NeedToOverpower(Composite child) : base(child)
            {
            }

            protected override bool CanRun(object context)
            {
                string spellName = "Overpower";
                if (!Utils.IsCommonChecksOk(spellName, false)) return false;
                if (!ClassHelper.Stance.IsBattleStance) return false;

                if (Me.ActiveAuras.ContainsKey("Taste for Blood")) return true;

                return Spell.CanCast(spellName);
                //return (Spell.CanCastLUA(spellName));
            }
        }

        public class Overpower : Action
        {
            protected override RunStatus Run(object context)
            {
                string spellName = "Overpower";
                bool result = Spell.Cast(spellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

        #region Thunder Clap

        public class NeedToThunderClap : Decorator
        {
            public NeedToThunderClap(Composite child) : base(child)
            {
            }

            protected override bool CanRun(object context)
            {
                string spellName = "Thunder Clap";
                if (!Utils.IsCommonChecksOk(spellName, false)) return false;
                if (!ClassHelper.Stance.IsBattleStance && !ClassHelper.Stance.IsDefensiveStance) return false;
                if (!CLC.ResultOK(Settings.ThunderClap)) return false;
                if (Spell.IsKnown("Bloodthirst") && !Spell.IsOnCooldown("Bloodthirst") && !Utils.Adds) return false;    // Prefer to use Bloodthirst first, if known

                return (Spell.CanCast(spellName));
            }
        }

        public class ThunderClap : Action
        {
            protected override RunStatus Run(object context)
            {
                string spellName = "Thunder Clap";
                Spell.Cast(spellName);
                Utils.LagSleep();
                bool result = Target.IsDebuffOnTarget(spellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

        #region Rend

        public class NeedToRend : Decorator
        {
            public NeedToRend(Composite child) : base(child)
            {
            }

            protected override bool CanRun(object context)
            {
                string SpellName = "Rend";
                if (!Utils.IsCommonChecksOk(SpellName, false)) return false;
                if (ClassHelper.Stance.IsDefensiveStance) return false;
                //if (!CLC.ResultOK(Settings.Rend)) return false;
                if (!Target.IsHealthAbove(40) && !Target.IsElite) return false;
                if (Target.IsDebuffOnTarget(SpellName)) return false;
                //if (Target.IsLowLevel) return false;
                if (Settings.LowLevelCheck.Contains("always"))
                    if (Target.IsLowLevel) return false;

                return (Spell.CanCast(SpellName));
            }
        }

        public class Rend : Action
        {
            protected override RunStatus Run(object context)
            {
                string SpellName = "Rend";
                Spell.Cast(SpellName);
                Utils.LagSleep();
                
                bool result = Target.IsDebuffOnTarget(SpellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

        #region Retaliation

        public class NeedToRetaliation : Decorator
        {
            public NeedToRetaliation(Composite child) : base(child)
            {
            }

            protected override bool CanRun(object context)
            {
                string spellName = "Retaliation";
                if (!Utils.IsCommonChecksOk(spellName, false)) return false;
                if (!ClassHelper.Stance.IsBattleStance) return false;
                //if (Target.IsLowLevel) return false;
                if (Settings.LowLevelCheck.Contains("always"))
                    if (Target.IsLowLevel) return false;

                return (Spell.CanCast(spellName));
            }
        }

        public class Retaliation : Action
        {
            protected override RunStatus Run(object context)
            {
                string spellName = "Retaliation";
                bool result = Spell.Cast(spellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

        #region Cleave

        public class NeedToCleave : Decorator
        {
            public NeedToCleave(Composite child) : base(child)
            {
            }

            protected override bool CanRun(object context)
            {
                string SpellName = "Cleave";
                if (!Utils.IsCommonChecksOk(SpellName, false)) return false;
                if (!Me.IsInInstance && !Utils.Adds) return false;
                if (Me.IsInInstance && !RAF.CanAoEInstance) return false;
                if (!CLC.ResultOK(Settings.Cleave)) return false;

                return (Spell.CanCast(SpellName));
            }
        }

        public class Cleave : Action
        {
            protected override RunStatus Run(object context)
            {
                string SpellName = "Cleave";
                bool result = Spell.Cast(SpellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

        #region Execute

        public class NeedToExecute : Decorator
        {
            public NeedToExecute(Composite child) : base(child)
            {
            }

            protected override bool CanRun(object context)
            {
                string SpellName = "Execute";
                if (!Utils.IsCommonChecksOk(SpellName, false)) return false;
                if (!ClassHelper.Stance.IsBattleStance && !ClassHelper.Stance.IsBerserkerStance) return false;
                if (Target.IsHealthAbove(20)) return false;
                if (!CLC.ResultOK(Settings.Execute)) return false;

                return (Spell.CanCast(SpellName));
            }
        }

        public class Execute : Action
        {
            protected override RunStatus Run(object context)
            {
                string SpellName = "Execute";
                bool result = Spell.Cast(SpellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

        #region Pummel

        public class NeedToPummel : Decorator
        {
            public NeedToPummel(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                string spellName = "Pummel";
                if (!Utils.IsCommonChecksOk(spellName, false)) return false;
                if (!ClassHelper.Stance.IsBattleStance && !ClassHelper.Stance.IsBerserkerStance) return false;
                if (!Target.IsCasting) return false;

                return (Spell.CanCast(spellName));
            }
        }

        public class Pummel : Action
        {
            protected override RunStatus Run(object context)
            {
                string spellName = "Pummel";
                bool result = Spell.Cast(spellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

        #region Whirlwind

        public class NeedToWhirlwind : Decorator
        {
            public NeedToWhirlwind(Composite child) : base(child)
            {
            }

            protected override bool CanRun(object context)
            {
                string SpellName = "Whirlwind";
                if (!Utils.IsCommonChecksOk(SpellName, false)) return false;
                if (!ClassHelper.Stance.IsBerserkerStance) return false;
                if (!CLC.ResultOK(Settings.Whirlwind)) return false;

                return (Spell.CanCast(SpellName));
            }
        }

        public class Whirlwind : Action
        {
            protected override RunStatus Run(object context)
            {
                string SpellName = "Whirlwind";
                bool result = Spell.Cast(SpellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

        #region Revenge

        public class NeedToRevenge : Decorator
        {
            public NeedToRevenge(Composite child) : base(child)
            {
            }

            protected override bool CanRun(object context)
            {
                string SpellName = "Revenge";
                if (!Utils.IsCommonChecksOk(SpellName, false)) return false;
                if (!ClassHelper.Stance.IsDefensiveStance) return false;

                return (Spell.CanCast(SpellName));
            }
        }

        public class Revenge : Action
        {
            protected override RunStatus Run(object context)
            {
                string SpellName = "Revenge";
                bool result = Spell.Cast(SpellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

        #region Hamstring

        public class NeedToHamstring : Decorator
        {
            public NeedToHamstring(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                string SpellName = "Hamstring";
                if (!Utils.IsCommonChecksOk(SpellName, false)) return false;
                if (!ClassHelper.Stance.IsBattleStance && !ClassHelper.Stance.IsBerserkerStance) return false;
                if (Target.IsDebuffOnTarget(SpellName)) return false;
                if (!CLC.ResultOK(Settings.Hamstring)) return false;

                return (Spell.CanCast(SpellName));
            }
        }

        public class Hamstring : Action
        {
            protected override RunStatus Run(object context)
            {
                string SpellName = "Hamstring";
                Spell.Cast(SpellName);
                Utils.LagSleep();
                
                bool result = Target.IsDebuffOnTarget(SpellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

        #region Mortal Strike

        public class NeedToMortalStrike : Decorator
        {
            public NeedToMortalStrike(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                string SpellName = "Mortal Strike";
                if (!Utils.IsCommonChecksOk(SpellName, false)) return false;

                return (Spell.CanCast(SpellName));
            }
        }

        public class MortalStrike : Action
        {
            protected override RunStatus Run(object context)
            {
                string SpellName = "Mortal Strike";
                bool result = Spell.Cast(SpellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

        #region Death Wish

        public class NeedToDeathWish : Decorator
        {
            public NeedToDeathWish(Composite child) : base(child)
            {
            }

            protected override bool CanRun(object context)
            {
                string SpellName = "Death Wish";
                if (!Utils.IsCommonChecksOk(SpellName, false)) return false;
                if (Utils.Adds) return false;
                if (!Me.IsInInstance && !Target.IsHealthAbove(60)) return false;
                //if (Target.IsLowLevel) return false;
                if (Settings.LowLevelCheck.Contains("always"))
                    if (Target.IsLowLevel) return false;
                if (!CLC.ResultOK(Settings.DeathWish)) return false;

                return (Spell.CanCast(SpellName));
            }
        }

        public class DeathWish : Action
        {
            protected override RunStatus Run(object context)
            {
                string SpellName = "Death Wish";
                Spell.Cast(SpellName);
                Utils.LagSleep();
                
                bool result = Self.IsBuffOnMe(SpellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

        #region Raging Blow

        public class NeedToRagingBlow : Decorator
        {
            public NeedToRagingBlow(Composite child) : base(child)
            {
            }

            protected override bool CanRun(object context)
            {
                string SpellName = "Raging Blow";
                if (!Utils.IsCommonChecksOk(SpellName, false)) return false;
                if (!ClassHelper.Stance.IsBerserkerStance) return false;
                if (!Self.IsBuffOnMe("Enrage") && !Self.IsBuffOnMe("Berserker Rage") && !Self.IsBuffOnMe("Death Wish")) return false;


                return (Spell.CanCast(SpellName));
            }
        }

        public class RagingBlow : Action
        {
            protected override RunStatus Run(object context)
            {
                string SpellName = "Raging Blow";
                bool result = Spell.Cast(SpellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

        #region Berserker Rage

        public class NeedToBerserkerRage : Decorator
        {
            public NeedToBerserkerRage(Composite child) : base(child)
            {
            }

            protected override bool CanRun(object context)
            {
                string spellName = "Berserker Rage";
                if (!Utils.IsCommonChecksOk(spellName, false)) return false;
                if (!Self.IsBuffOnMe("Sap") && !Self.IsBuffOnMe("Fear")) return false;
                if (!CLC.ResultOK(Settings.BerserkerRage)) return false;

                return (Spell.CanCast(spellName));
            }
        }

        public class BerserkerRage : Action
        {
            protected override RunStatus Run(object context)
            {
                string spellName = "Berserker Rage";
                Spell.Cast(spellName);
                Utils.LagSleep();

                bool result = Self.IsBuffOnMe(spellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

        #region Battle Trance

        public class NeedToBattleTrance : Decorator
        {
            public NeedToBattleTrance(Composite child) : base(child)
            {
            }

            protected override bool CanRun(object context)
            {
                if (!Utils.IsCommonChecksOk("", false)) return false;
                if (!Me.ActiveAuras.ContainsKey("Battle Trance")) return false;

                return (Spell.CanCast("Heroic Strike") || Spell.CanCast("Strike"));
            }
        }

        public class BattleTrance : Action
        {
            protected override RunStatus Run(object context)
            {
                Utils.Log("BATTLE TRANCE!",Utils.Colour("Blue"));
                string spellName = Spell.CanCast("Heroic Strike") ? "Heroic Strike" : "Strike";
                bool result = Spell.Cast(spellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

        #region Stance Dance - Berserker
        public class NeedToStanceDanceBerserker : Decorator
        {
            public NeedToStanceDanceBerserker(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (!Utils.IsCommonChecksOk("", true)) return false;
                if (!Me.Combat) return false;
                if (ClassHelper.Stance.IsBerserkerStance) return false;
                if (!Spell.IsKnown("Berserker Stance")) return false;
                //if (!Spell.IsKnown("Whirlwind") && !Spell.IsKnown("Raging Blow")) return false;
                if (!Spell.IsKnown("Raging Blow")) return false;

                return true;
            }
        }

        public class StanceDanceBerserker : Action
        {
            protected override RunStatus Run(object context)
            {
                Spell.Cast("Berserker Stance");
                Utils.LagSleep();

                bool result = Self.IsBuffOnMe("Berserker Stance");
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Stance Dance - Battle
        public class NeedToStanceDanceBattle: Decorator
        {
            public NeedToStanceDanceBattle(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (!Utils.IsCommonChecksOk("", true)) return false;
                if (ClassHelper.Stance.IsBattleStance) return false;
                //if (Spell.IsKnown("Whirlwind") || Spell.IsKnown("Raging Blow")) return false;
                if (Spell.IsKnown("Raging Blow")) return false;

                return true;
            }
        }

        public class StanceDanceBattle : Action
        {
            protected override RunStatus Run(object context)
            {
                Spell.Cast("Battle Stance");
                Utils.LagSleep();

                bool result = Self.IsBuffOnMe("Battle Stance");
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Deadly Calm

        public class NeedToDeadlyCalm : Decorator
        {
            public NeedToDeadlyCalm(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                string SpellName = "Deadly Calm";
                if (!Utils.IsCommonChecksOk(SpellName, false)) return false;
                if (!Utils.Adds) return false;
                if (Self.IsRageAbove(25)) return false;
                //if (Target.IsLowLevel) return false;
                if (Settings.LowLevelCheck.Contains("always"))
                    if (Target.IsLowLevel) return false;
                if (!CLC.ResultOK(Settings.DeadlyCalm)) return false;
                

                return (Spell.CanCast(SpellName));
            }
        }

        public class DeadlyCalm : Action
        {
            protected override RunStatus Run(object context)
            {
                string SpellName = "Deadly Calm";
                Spell.Cast(SpellName);
                Utils.LagSleep();
                
                bool result = Self.IsBuffOnMe(SpellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

        #region Sweeping Strikes

        public class NeedToSweepingStrikes : Decorator
        {
            public NeedToSweepingStrikes(Composite child) : base(child)
            {
            }

            protected override bool CanRun(object context)
            {
                string SpellName = "Sweeping Strikes";
                if (!Utils.IsCommonChecksOk(SpellName, false)) return false;
                if (ClassHelper.Stance.IsDefensiveStance) return false;
                if (!CLC.ResultOK(Settings.SweepingStrikes)) return false;
                //if (Target.IsLowLevel) return false;
                if (Settings.LowLevelCheck.Contains("always"))
                    if (Target.IsLowLevel) return false;

                return (Spell.CanCast(SpellName));
            }
        }

        public class SweepingStrikes : Action
        {
            protected override RunStatus Run(object context)
            {
                string SpellName = "Sweeping Strikes";
                bool result = Spell.Cast(SpellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

        #region Bladestorm

        public class NeedToBladestorm : Decorator
        {
            public NeedToBladestorm(Composite child) : base(child)
            {
            }

            protected override bool CanRun(object context)
            {
                string spellName = "Bladestorm";
                if (!Utils.IsCommonChecksOk(spellName, false)) return false;
                if (!Utils.Adds) return false;
                //if (Target.IsLowLevel) return false;
                if (Settings.LowLevelCheck.Contains("always"))
                    if (Target.IsLowLevel) return false;
                if (Spell.IsKnown("Sweeping Strikes") && !Spell.IsOnCooldown("Sweeping Strikes")) return false;

                return (Spell.CanCast(spellName));
            }
        }

        public class Bladestorm : Action
        {
            protected override RunStatus Run(object context)
            {
                string spellName = "Bladestorm";
                bool result = Spell.Cast(spellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

        #region Colossus Smash

        public class NeedToColossusSmash : Decorator
        { 
            public NeedToColossusSmash(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                string SpellName = "Colossus Smash";
                if (!Utils.IsCommonChecksOk(SpellName, false)) return false;
                if (!ClassHelper.Stance.IsBattleStance && !ClassHelper.Stance.IsBerserkerStance) return false;
                if (Settings.LowLevelCheck.Contains("always"))
                    if (Target.IsLowLevel) return false;

                return (Spell.CanCast(SpellName));
            }
        }

        public class ColossusSmash : Action
        {
            protected override RunStatus Run(object context)
            {
                string SpellName = "Colossus Smash";
                bool result = Spell.Cast(SpellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

        #region Heroic Throw

        public class NeedToHeroicThrow : Decorator
        {
            public NeedToHeroicThrow(Composite child) : base(child)
            {
            }

            protected override bool CanRun(object context)
            {
                string SpellName = "Heroic Throw";
                if (!Utils.IsCommonChecksOk(SpellName, false)) return false;
                if (!Target.IsFleeing) return false;
                //if (Target.IsLowLevel) return false;
                if (Settings.LowLevelCheck.Contains("always"))
                    if (Target.IsLowLevel) return false;

                return (Spell.CanCast(SpellName));
            }
        }

        public class HeroicThrow : Action
        {
            protected override RunStatus Run(object context)
            {
                string SpellName = "Heroic Throw";
                bool result = Spell.Cast(SpellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

        #region Recklessness
        public class NeedToRecklessness : Decorator
        {
            public NeedToRecklessness(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (!Utils.IsCommonChecksOk("Recklessness", false)) return false;
                if (!ClassHelper.Stance.IsBerserkerStance) return false;
                if (!Utils.Adds) return false;
                if (!Self.IsHealthAbove(40)) return false;
                //if (!CLC.ResultOK(Settings.Recklessness)) return false;
                //if (Target.IsLowLevel) return false;
                if (Settings.LowLevelCheck.Contains("always"))
                    if (Target.IsLowLevel) return false;

                return (Spell.CanCast("Recklessness"));
            }
        }

        public class Recklessness : Action
        {
            protected override RunStatus Run(object context)
            {
                bool result = Spell.Cast("Recklessness");
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Devastate

        public class NeedToDevastate : Decorator
        {
            public NeedToDevastate(Composite child) : base(child)
            {
            }

            protected override bool CanRun(object context)
            {
                string SpellName = "Devastate";
                if (!Utils.IsCommonChecksOk(SpellName, false)) return false;

                return (Spell.CanCast(SpellName));
            }
        }

        public class Devastate : Action
        {
            protected override RunStatus Run(object context)
            {
                string SpellName = "Devastate";
                bool result = Spell.Cast(SpellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

        #region Shockwave

        public class NeedToShockwave : Decorator
        {
            public NeedToShockwave(Composite child) : base(child)
            {
            }

            protected override bool CanRun(object context)
            {
                string SpellName = "Shockwave";
                if (!Utils.IsCommonChecksOk(SpellName, false)) return false;

                return (Spell.CanCast(SpellName));
            }
        }

        public class Shockwave : Action
        {
            protected override RunStatus Run(object context)
            {
                string SpellName = "Shockwave";
                bool result = Spell.Cast(SpellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

        #region NeedToTargetMob
        public class NeedToTargetMob : Decorator
        {
            public NeedToTargetMob(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (!Me.Combat) return false;
                if (Me.GotTarget && Me.CurrentTarget.IsAlive) return false;
                if (Me.IsInInstance) return false;
                if (Targeting.Instance.TargetList.Count <= 0) return false;

                if (Utils.BestTarget != null) return true;

                return false;

            }
        }

        public class TargetMob : Action
        {
            protected override RunStatus Run(object context)
            {
                WoWUnit t = Utils.BestTarget;

                if (t == null) return RunStatus.Failure;
                Utils.Log("Targeting most suitable mob, " + t.Name);
                t.Target();
                Utils.LagSleep();

                bool result = Me.GotTarget && Me.CurrentTarget.IsAlive;
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Sunder Armor

        public class NeedToSunderArmor : Decorator
        {
            public NeedToSunderArmor(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                string SpellName = "Sunder Armor";
                if (!Utils.IsCommonChecksOk(SpellName, false)) return false;
                //if (!CLC.ResultOK(Settings.SunderArmor)) return false;
                if (Settings.SunderArmor.Contains("never")) return false;
                if (Target.IsLowLevel) return false;
                if (!Target.IsHealthAbove(50) && !Target.IsElite) return false;
                if (Target.IsDebuffOnTarget("Sunder Armor") && Settings.SunderArmor.Contains("1 stack")) return false;
                if (Target.DebuffStackCount("Sunder Armor") >= 3 && Settings.SunderArmor.Contains("3 stacks")) return false;
                if (Target.DebuffStackCount("Sunder Armor") >= 2 && Settings.SunderArmor.Contains("2 stacks")) return false;

                return (Spell.CanCast(SpellName));
            }
        }

        public class SunderArmor : Action
        {
            protected override RunStatus Run(object context)
            {
                string SpellName = "Sunder Armor";
                Spell.Cast(SpellName);
                Utils.LagSleep();
                
                bool result = Target.IsDebuffOnTarget(SpellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

        #region Enraged Regenaration

        public class NeedToEnragedRegeneration : Decorator
        {
            public NeedToEnragedRegeneration(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                string SpellName = "Enraged Regeneration";
                if (!Utils.IsCommonChecksOk(SpellName, false)) return false;
                if (Self.IsBuffOnMe("Enraged Regeneration")) return false;
                if (Self.IsHealthAbove(Settings.EnragedRegenerationHealth)) return false;
                if (Spell.IsOnCooldown("Berserker Rage")) return false;

                return (Spell.CanCast(SpellName));
            }
        }

        public class EnragedRegeneration : Action
        {
            protected override RunStatus Run(object context)
            {
                string SpellName = "Enraged Regeneration";
                Spell.Cast("Berserker Rage");
                Utils.LagSleep();
                System.Threading.Thread.Sleep(1000);
                if (!Self.IsBuffOnMe("Berserker Rage")) return RunStatus.Failure;
                while (Spell.IsGCD) { System.Threading.Thread.Sleep(100); }

                bool result = Spell.Cast(SpellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion

    }
}