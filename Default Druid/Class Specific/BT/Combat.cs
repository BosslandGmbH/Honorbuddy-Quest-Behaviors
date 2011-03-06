using Hera.Config;
using Hera.Helpers;
using Hera.SpellsMan;
using Styx.Combat.CombatRoutine;
using Styx.Logic;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using TreeSharp;
using System.Threading;

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

                new Decorator(ret => Me.GotTarget && !CT.Fleeing && Target.IsDistanceLessThan(2) && Me.IsMoving,
                    new Action(ret => Movement.StopMoving())),

                // Check if we get aggro during the pull
                // This is in here and not the pull because we are in combat at this point
                new NeedToCheckAggroOnPull(new CheckAggroOnPull()),

                // Abort combat is the target's health is 95% + after 30 seconds of combat
                new NeedToCheckCombatTimer(new CheckCombatTimer()),

                // Innervate                
                new NeedToInnervate(new Innervate()),

                // Thorns (RAF leader only)
                new NeedToThornsRAF(new ThornsRAF()),

                // --------------------------------------------------------------------------------
                // Dedicated Healer Check - If you're Restoration spec DO NOT go past this point.
                new NeedToDedicatedHealerCheck(new DedicatedHealerCheck()),
                // --------------------------------------------------------------------------------

                // Kind of important that we face the target
                new NeedToFaceTarget(new FaceTarget()),

                // Auto attack
                new NeedToAutoAttack(new AutoAttack()),

                // Faerie Fire
                new NeedToFaerieFire(new FaerieFire()),

                // Shapeshift, its a pretty good to be in some kind of shapeshifted form
                new NeedToShapeshift(new Shapeshift()),
                
                // Distance check. Make sure we are in attacking range at all times
                new NeedToCheckDistance(new CheckDistance()),

                // Feral Charge Bear - Combat only
                new NeedToFeralChargeBear(new FeralChargeBear()),

                // ************************************************************************************
                // Important/time sensative spells here
                // These are spells that need to be case asap

                // Feral silence spell, important to cast asap
                // Skull Bash                
                new NeedToSkullBash(new SkullBash()),

                // Feral silence / stun spell important in PVP cast asap
                // Maim
                new NeedToMaim(new Maim()),

                // Bear spell like a finishing move, also increases melee auto attack damage
                // Pulverize
                new NeedToPulverize(new Pulverize()),

                // Enrage
                new NeedToEnrage(new Enrage()),


                // ************************************************************************************
                // Balance combat spells here
                // Putting balance before feral because casting is usually more time sensative

                // Force Of Nature
                new NeedToTrents(new Trents()),

                // Hurricane In Instance
                new NeedToHurricaneInInstance(new HurricaneInInstance()),

                // Starsurge                
                new NeedToStarsurge(new Starsurge()),

                // Typhoon
                new NeedToTyphoon(new Typhoon()),

                // Moonfire                
                new NeedToMoonfire(new Moonfire()),

                // Insect Swarm                
                new NeedToInsectSwarm(new InsectSwarm()),

                // Wrath / Starfire
                new NeedToBalanceDPS(new BalanceDPS()),



                // ************************************************************************************
                // Feral combat spells here

                // Ferocious Bite                
                new NeedToFerociousBite(new FerociousBite()),

                // Thrash
                new NeedToThrash(new Thrash()),

                // Rip
                new NeedToRip(new Rip()),

                // Swipe
                new NeedToSwipe(new Swipe()),

                // Savage Roar
                new NeedToSavageRoar(new SavageRoar()),

                // Rake
                new NeedToRake(new Rake()),

                // Tiger's Fury
                new NeedToTigersFury(new TigersFury()),

                // Shred
                new NeedToShred(new Shred()),


                // ===================================================
                // Bear Stuff

                // Demoralizing Roar
                new NeedToDemoralizingRoar(new DemoralizingRoar()),
                
                // Maul
                new NeedToMaul(new Maul()),

                // Lacerate
                new NeedToLacerate(new Lacerate()),

                // Mangle (Bear)
                new NeedToBearMangle(new BearMangle()),


                // ===================================================
                

                // If we got this far we don't have enough combo points to do anything or all buffs/debuffs are in place
                // Claw / Mangle
                new NeedToClawOrMangle(new ClawOrMangle())

                //new Decorator(ret => ClassHelper.Shapeshift.IsCatForm || ClassHelper.Shapeshift.IsBearForm, new Action(ret => Movement.DistanceCheck(5.7,3.0)))
                
                );
        }
        #endregion


        // ******************************************************************************************
        //
        // This is where the priority selectors start
        //


        #region Auto Attack
        public class NeedToAutoAttack : Decorator
        {
            public NeedToAutoAttack(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToAutoAttack", 1);
                if (ClassHelper.ClassSpec != ClassType.Feral) return false;
                return (Me.GotTarget && CT.IsAlive && !Me.IsAutoAttacking);
            }
        }

        public class AutoAttack : Action
        {
            protected override RunStatus Run(object context)
            {
                Utils.AutoAttack(true);
                return RunStatus.Failure;

            }
        }
        #endregion

        /*
        #region Line Of Sight Check
        public class NeedToLineOfSightCheck : Decorator
        {
            public NeedToLineOfSightCheck(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (Me.IsInParty) return false;
                if (!Me.GotTarget) return false;
                if (Utils.IsInLineOfSight(CT.Location)) return false;

                return true;
            }
        }

        public class LineOfSightCheck : Action
        {
            protected override RunStatus Run(object context)
            {
                Utils.MoveToLineOfSight(CT.Location);

                bool result = Utils.IsInLineOfSight(CT.Location);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion
         */

        #region DedicatedHealerCheck
        public class NeedToDedicatedHealerCheck : Decorator
        {
            public NeedToDedicatedHealerCheck(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                return (ClassHelper.IsHealerOnly);
            }
        }

        public class DedicatedHealerCheck : Action
        {
            protected override RunStatus Run(object context)
            {
                return RunStatus.Success;
            }
        }
        #endregion


        // ************************************************
        // Boomkin

        #region Moonfire
        public class NeedToMoonfire : Decorator
        {
            public NeedToMoonfire(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToMoonfire", 1);
                if (ClassHelper.IsHealerOnly) return false;
                if (!Utils.IsCommonChecksOk("Moonfire", false)) return false;
                if (!ClassHelper.Shapeshift.IsCasterCapable) return false;
                if (Target.IsDebuffOnTarget("Moonfire")) return false;
                if (Target.IsDebuffOnTarget("Sunfire")) return false;
                if (!Target.IsHealthAbove(20) && !Target.IsElite) return false;
                if (ClassHelper.ClassSpec == ClassType.Feral && Spell.IsKnown("Cat Form")) return false;
                if (!CLC.ResultOK(Settings.Moonfire)) return false;
                if (Target.IsDistanceMoreThan(Spell.MaxDistance("Moonfire"))) return false;
                if (!Utils.IsInLineOfSight(CT.Location)) { Utils.MoveToLineOfSight(CT.Location); return false; }

                if (Me.IsInParty && !Target.IsHealthAbove(35)) return false;

                bool result = Spell.CanCast("Moonfire");
                return result;
            }
        }

        public class Moonfire : Action
        {
            protected override RunStatus Run(object context)
            {
                //if (!Utils.IsInLineOfSight(CT.Location)) Utils.MoveToLineOfSight(CT.Location);
                Spell.Cast("Moonfire");
                Utils.LagSleep();
                bool result = Target.IsDebuffOnTarget("Moonfire");
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Insect Swarm
        public class NeedToInsectSwarm : Decorator
        {
            public NeedToInsectSwarm(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToInsectSwarm", 1);
                if (ClassHelper.IsHealerOnly) return false;
                if (!Utils.IsCommonChecksOk("Insect Swarm", false)) return false;
                if (!ClassHelper.Shapeshift.IsCasterCapable) return false;
                if (!Target.CanDebuffTarget("Insect Swarm")) return false;
                if (!Target.IsHealthAbove(40) && !Target.IsElite) return false;
                if (ClassHelper.ClassSpec == ClassType.Feral && Spell.IsKnown("Cat Form")) return false;
                if (!CLC.ResultOK(Settings.InsectSwarm)) return false;
                if (Target.IsDistanceMoreThan(Spell.MaxDistance("Insect Swarm"))) return false;
                if (!Utils.IsInLineOfSight(CT.Location)) { Utils.MoveToLineOfSight(CT.Location); return false; }


                bool result = Spell.CanCast("Insect Swarm");
                return result;
            }
        }

        public class InsectSwarm : Action
        {
            protected override RunStatus Run(object context)
            {
                Spell.Cast("Insect Swarm");
                Utils.LagSleep();
                bool result = Target.IsDebuffOnTarget("Insect Swarm");
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Innervate
        public class NeedToInnervate : Decorator
        {
            public NeedToInnervate(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (!ClassHelper.Shapeshift.IsCasterCapable) return false;
                if (!Utils.IsCommonChecksOk("Innervate", true)) return false;
                if (Self.IsManaAbove(Settings.InnervateMana)) return false;
                if (Self.IsBuffOnMe("Innervate")) return false;

                bool result = Spell.CanCast("Innervate");
                return result;
            }
        }

        public class Innervate : Action
        {
            protected override RunStatus Run(object context)
            {
                Spell.Cast("Innervate");
                Utils.LagSleep();
                bool result = Self.IsBuffOnMe("Innervate");
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Force Of Nature
        public class NeedToTrents : Decorator
        {
            public NeedToTrents(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                string dpsSpell = "Force of Nature";
                if (!Utils.IsCommonChecksOk(dpsSpell, false)) return false;
                if (!ClassHelper.Shapeshift.IsCasterCapable) return false;
                if (ClassHelper.ClassSpec == ClassType.Feral && Spell.IsKnown("Cat Form")) return false;
                if (!Spell.IsEnoughMana(dpsSpell)) return false;
                if (!Me.IsInParty && !CLC.ResultOK(Settings.ForceOfNature)) return false;
                if (Me.IsInParty && !RAF.AddsInstance) return false;
                if (!Utils.IsInLineOfSight(CT.Location)) { Utils.MoveToLineOfSight(CT.Location); return false; }

                return (Spell.CanCast(dpsSpell));
            }
        }

        public class Trents : Action
        {
            protected override RunStatus Run(object context)
            {
                string dpsSpell = "Force of Nature";
                
                bool result = Spell.Cast(dpsSpell, Me.CurrentTarget.Location);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Hurricane - Instance / RAF
        public class NeedToHurricaneInInstance : Decorator
        {
            public NeedToHurricaneInInstance(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                string dpsSpell = "Hurricane";
                if (ClassHelper.IsHealerOnly) return false;
                if (!Utils.IsCommonChecksOk(dpsSpell, false)) return false;
                if (!ClassHelper.Shapeshift.IsCasterCapable) return false;
                if (ClassHelper.ClassSpec == ClassType.Feral && Spell.IsKnown("Cat Form")) return false;
                if (ClassHelper.ClassSpec == ClassType.Restoration) return false;
                if (!Spell.IsEnoughMana(dpsSpell)) return false;
                if (!RAF.CanAoEInstance) return false;
                if (RaFHelper.Leader !=null && RaFHelper.Leader.IsMoving) return false;
                //if (RaFHelper.Leader.Distance2D > Spell.MaxDistance(dpsSpell)) return false;

                return (Spell.CanCast(dpsSpell));
            }
        }

        public class HurricaneInInstance : Action
        {
            protected override RunStatus Run(object context)
            {
                string dpsSpell = "Hurricane";
                double maxDistance = Spell.MaxDistance(dpsSpell) - 1;
                
                while (RaFHelper.Leader.Distance2D > maxDistance)
                {
                    // if we're out of Hurricane range then move closer
                    Movement.MoveTo(RaFHelper.Leader.Location);
                    Utils.Log("Out of range for Hurricane, moving closer");
                    
                }

                
                if (!Utils.IsInLineOfSight()) Utils.MoveToLineOfSight();
                if (Me.IsMoving) Movement.StopMoving();

                // Once we finally get here, if the tank is moving then just bail out
                if (RaFHelper.Leader.IsMoving) return RunStatus.Failure;

                // Cast AoE at this location
                WoWPoint AoELocation = RaFHelper.Leader.Location;

                bool result = Spell.Cast(dpsSpell, AoELocation);
                Thread.Sleep(500);
                while (Me.IsCasting)
                {
                    // If the tank is not within 6 yards of our original casting location then bail out (assuming mobs won't be there either)
                    // If we're out of combat bail out (assuming mobs are all dead)
                    if (RaFHelper.Leader.Location.Distance(AoELocation) > 9 || !RaFHelper.Leader.Combat)
                    {
                        string reasonForStopping = "";
                        if (RaFHelper.Leader.Location.Distance(AoELocation) > 9) reasonForStopping = "RAF leader out of range";
                        if (!Me.Combat) reasonForStopping = "Combat ended";

                        Utils.Log(string.Format("** Stop casting Hurricane - {0} **", reasonForStopping));
                        //Utils.Log("** Combat ended? " + !Me.Combat + "**");
                        //tils.Log("** RAF leader out of range " + (RaFHelper.Leader.Location.Distance(AoELocation) > 6) + "**");
                        WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
                        WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
                        break;
                    }

                    Thread.Sleep(250);
                    
                }
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Primary DPS Spell
        public class NeedToBalanceDPS : Decorator
        {
            public NeedToBalanceDPS(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToWrath", 1);
                if (ClassHelper.IsHealerOnly) return false;

                string dpsSpell = Settings.PrimaryDPSSpell;
                if (dpsSpell == "Automatic") dpsSpell = ClassHelper.BalanceDPSSpell;
                
                if (Self.IsBuffOnMe("Eclipse (Solar)")) dpsSpell = "Wrath";
                if (Self.IsBuffOnMe("Eclipse (Lunar)")) dpsSpell = "Starfire";
                ClassHelper.BalanceDPSSpell = dpsSpell;

                // Fall back if the primary DPS spell is on cooldown
                if (Spell.IsOnCooldown(dpsSpell)) { switch (dpsSpell) { case "Starfire": dpsSpell = "Wrath"; break; case "Wrath": dpsSpell = "Starfire"; break; } }

                if (!Utils.IsCommonChecksOk(dpsSpell, false)) return false;
                if (!ClassHelper.Shapeshift.IsCasterCapable) return false;
                if (ClassHelper.ClassSpec == ClassType.Feral && Spell.IsKnown("Cat Form")) return false;
                if (!Spell.IsEnoughMana(dpsSpell)) return false;
                if (!Utils.IsInLineOfSight(CT.Location)) { Utils.MoveToLineOfSight(CT.Location); return false; }

                bool result = (Spell.CanCast(dpsSpell));

                return result;
            }
        }

        public class BalanceDPS : Action
        {
            protected override RunStatus Run(object context)
            {
                //if (!Utils.IsInLineOfSight(CT.Location)) Utils.MoveToLineOfSight(CT.Location);

                Target.Face();
                string dpsSpell = ClassHelper.BalanceDPSSpell;

                if (Self.IsBuffOnMe("Eclipse (Solar)")) dpsSpell = "Wrath";
                if (Self.IsBuffOnMe("Eclipse (Lunar)")) dpsSpell = "Starfire";
                
                // Fall back if the primary DPS spell is on cooldown
                if (Spell.IsOnCooldown(dpsSpell)) { switch (dpsSpell) { case "Starfire": dpsSpell = "Wrath"; break; case "Wrath": dpsSpell = "Starfire"; break; } }

                bool result = Spell.Cast(dpsSpell);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Starsurge
        public class NeedToStarsurge : Decorator
        {
            public NeedToStarsurge(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToStarsurge", 1);
                string dpsSpell = "Starsurge";
                if (ClassHelper.IsHealerOnly) return false;
                if (!Utils.IsCommonChecksOk(dpsSpell, false)) return false;
                if (!ClassHelper.Shapeshift.IsCasterCapable) return false;
                if (ClassHelper.ClassSpec == ClassType.Feral && Spell.IsKnown("Cat Form")) return false;
                if (!Spell.IsEnoughMana(dpsSpell)) return false;
                if (!CLC.ResultOK(Settings.Starsurge)) return false;
                if (Target.IsDistanceMoreThan(Spell.MaxDistance(dpsSpell))) return false;
                if (Me.IsInParty && !Target.IsHealthAbove(40) && !Me.ActiveAuras.ContainsKey("Shooting Stars")) return false;
                if (!Utils.IsInLineOfSight(CT.Location)) { Utils.MoveToLineOfSight(CT.Location); return false; }

                return (Spell.CanCast(dpsSpell));
            }
        }

        public class Starsurge : Action
        {
            protected override RunStatus Run(object context)
            {
                //if (!Utils.IsInLineOfSight(CT.Location)) Utils.MoveToLineOfSight(CT.Location);

                Target.Face();
                string dpsSpell = "Starsurge";
                bool result = Spell.Cast(dpsSpell);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Typhoon
        public class NeedToTyphoon : Decorator
        {
            public NeedToTyphoon(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                string dpsSpell = "Typhoon";
                if (ClassHelper.IsHealerOnly) return false;
                if (!Utils.IsCommonChecksOk(dpsSpell, false)) return false;
                if (!ClassHelper.Shapeshift.IsCasterCapable) return false;
                if (ClassHelper.ClassSpec == ClassType.Feral && Spell.IsKnown("Cat Form")) return false;
                if (!Spell.IsEnoughMana(dpsSpell)) return false;
                if (Target.IsDistanceMoreThan(Spell.MaxDistance(dpsSpell))) return false;
                if (!Utils.IsBattleground && !Me.IsInParty) return false;

                return (Spell.CanCast(dpsSpell));
            }
        }

        public class Typhoon : Action
        {
            protected override RunStatus Run(object context)
            {
                string dpsSpell = "Typhoon";
                Target.Face();
                Thread.Sleep(700);

                bool result = Spell.Cast(dpsSpell);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Thorns RAF leader only
        public class NeedToThornsRAF : Decorator
        {
            public NeedToThornsRAF(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                string DPSSpell = "Thorns";
                if (RaFHelper.Leader == null) return false;
                if (!Me.Combat) return false;
                if (!Utils.IsCommonChecksOk(DPSSpell, true)) return false;
                if (!ClassHelper.Shapeshift.IsCasterCapable) return false;
                if (ClassHelper.ClassSpec == ClassType.Feral && Spell.IsKnown("Cat Form")) return false;
                if (RaFHelper.Leader.ActiveAuras.ContainsKey("Thorns")) return false;
                if (!Utils.IsInLineOfSight(RaFHelper.Leader.Location)) return false;
                if (RaFHelper.Leader.Distance > 29) return false;

                return (Spell.CanCast(DPSSpell));
            }
        }

        public class ThornsRAF : Action
        {
            protected override RunStatus Run(object context)
            {
                string DPSSpell = "Thorns";
                bool result = Spell.Cast(DPSSpell,RaFHelper.Leader);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        // ************************************************
        // Cat and Bear

        #region Claw or Mangle
        public class NeedToClawOrMangle : Decorator
        {
            public NeedToClawOrMangle(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToClawOrMangle", 1);
                if (!ClassHelper.Shapeshift.IsCatForm) return false;
                if (!Utils.IsCommonChecksOk("", false)) return false;
                
                if (!Self.IsEnergyAbove(Settings.AttackEnergy)) return false;

                if (Spell.CanCast("Mangle (Cat)")) return true;
                if (Spell.CanCast("Claw")) return true;
                return false;
            }
        }

        public class ClawOrMangle : Action
        {
            protected override RunStatus Run(object context)
            {
                bool result = false;
                string mangleSpell = "Mangle (Cat)";
                //if (ClassHelper.Shapeshift.IsBearForm) mangleSpell = "Mangle (Bear)";

                if (Spell.IsKnown(mangleSpell)) { result = Spell.Cast(mangleSpell); }
                else if (!Spell.IsKnown(mangleSpell) && Spell.CanCast("Claw")) { result = Spell.Cast("Claw"); }

                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Rake
        public class NeedToRake : Decorator
        {
            public NeedToRake(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToRake", 1);
                string dpsSpell = "Rake";

                if (!ClassHelper.Shapeshift.IsCatForm) return false;
                
                if (!Utils.IsCommonChecksOk(dpsSpell, false)) return false;
                if (Target.IsDebuffOnTarget(dpsSpell)) return false;
                if (Target.IsDistanceMoreThan(5.7)) return false;
                if (!Target.IsHealthAbove(20) && !Target.IsElite) return false;
                if (Target.IsLowLevel) return false;
                //if (Target.IsDistanceMoreThan(Spell.MaxDistance(dpsSpell))) return false;

                return (Spell.CanCast(dpsSpell));
            }
        }

        public class Rake : Action
        {
            protected override RunStatus Run(object context)
            {
                string dpsSpell = "Rake";

                Spell.Cast(dpsSpell);
                Utils.LagSleep();
                bool result = Target.IsDebuffOnTarget(dpsSpell);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Faerie Fire
        public class NeedToFaerieFire : Decorator
        {
            public NeedToFaerieFire(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                bool result = false;

                if (ClassHelper.IsHealerOnly) return false;
                if (!Utils.IsCommonChecksOk("", false)) return false;

                if (!Spell.IsKnown("Faerie Fire")) return false;
                if (Target.IsDebuffOnTarget("Faerie Fire")) return false;
                if (Target.IsDebuffOnTarget("Faerie Fire (Feral)")) return false;
                if (ClassHelper.ClassSpec == ClassType.Feral && ClassHelper.Shapeshift.IsHumanForm && Spell.IsKnown("Cat Form")) return false;
                
                if (!Target.IsHealthAbove(50) && (CT.Class == WoWClass.Rogue || CT.Class == WoWClass.Druid)) return false;
                if (Target.IsLowLevel) return false;

                if (ClassHelper.Shapeshift.IsCatForm || ClassHelper.Shapeshift.IsBearForm)
                {
                    if (!CLC.ResultOK(Settings.FaerieFireFeral)) return false;
                    if (Self.IsBuffOnMe("Prowl")) return false;
                    if (Spell.CanCast("Faerie Fire (Feral)")) result = true;
                }
                else
                {
                    if (!CLC.ResultOK(Settings.FaerieFireBalance)) return false;
                    if (Spell.CanCast("Faerie Fire")) result = true;
                }

                if (Me.IsInParty && !Target.IsHealthAbove(40)) return false;

                return result;

            }
        }

        public class FaerieFire : Action
        {
            protected override RunStatus Run(object context)
            {
                bool result = false;

                if (!Utils.IsInLineOfSight(CT.Location)) Utils.MoveToLineOfSight(CT.Location);

                switch (Me.Shapeshift)
                {
                    case Styx.ShapeshiftForm.Cat:
                    case Styx.ShapeshiftForm.Bear:
                        result = Spell.Cast("Faerie Fire (Feral)");
                        break;

                    case Styx.ShapeshiftForm.Normal:
                    case Styx.ShapeshiftForm.Moonkin:
                    case Styx.ShapeshiftForm.TreeOfLife:
                        result = Spell.Cast("Faerie Fire");
                        break;

                }

                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Ferocious Bite
        public class NeedToFerociousBite : Decorator
        {
            public NeedToFerociousBite(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToFerociousBite", 1);
                if (!ClassHelper.Shapeshift.IsCatForm) return false;
                if (!Utils.IsCommonChecksOk("Ferocious Bite", false)) return false;

                if (Me.ComboPoints >= 2 && !Target.IsHealthAbove(20) && Spell.IsKnown("Ferocious Bite") && Self.IsEnergyAbove(35)) return true;
                if (!CLC.ResultOK(Settings.FerociousBite)) return false;
                if (Target.IsDistanceMoreThan(Spell.MaxDistance("Ferocious Bite"))) return false;

                return Spell.CanCast("Ferocious Bite");
            }
        }

        public class FerociousBite : Action
        {
            protected override RunStatus Run(object context)
            {
                bool result = Spell.Cast("Ferocious Bite");
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Tiger's Fury
        public class NeedToTigersFury : Decorator
        {
            public NeedToTigersFury(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToTigersFury", 1);
                if (!ClassHelper.Shapeshift.IsCatForm) return false;
                if (!Utils.IsCommonChecksOk("Tiger's Fury", false)) return false;
                //if (ClassHelper.IsEnergyAbove((30))) return false;
                if (Self.IsBuffOnMe("Tiger's Fury")) return false;
                if (!Target.IsHealthAbove(35) && !Utils.Adds) return false;
                if (!CLC.ResultOK(Settings.TigersFury)) return false;

                bool result = (Spell.CanCast("Tiger's Fury"));
                return result;
            }
        }

        public class TigersFury : Action
        {
            protected override RunStatus Run(object context)
            {
                bool result = Spell.Cast("Tiger's Fury");
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Shapeshift
        public class NeedToShapeshift : Decorator
        {
            public NeedToShapeshift(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToShapeshift", 1);
                return ClassHelper.Shapeshift.NeedToShapeshift;
            }
        }

        public class Shapeshift : Action
        {
            protected override RunStatus Run(object context)
            {
                bool result = ClassHelper.Shapeshift.AutoShapeshift();
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Savage Roar
        public class NeedToSavageRoar : Decorator
        {
            public NeedToSavageRoar(Composite child) : base(child)
            {
            }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToSavageRoar", 1);
                if (!ClassHelper.Shapeshift.IsCatForm) return false;
                if (!Utils.IsCommonChecksOk("Savage Roar", false)) return false;
                if (Self.IsBuffOnMe("Savage Roar")) return false;
                if (!CLC.ResultOK(Settings.SavageRoar)) return false;
                //if (Target.CanDebuffTarget("Savage Roar")) return false;
                //if (Target.IsLowLevel) return false;)

                return (Spell.CanCast("Savage Roar"));
            }
        }

        public class SavageRoar : Action
        {
            protected override RunStatus Run(object context)
            {
                Spell.Cast("Savage Roar");
                Utils.LagSleep();
                bool result = Self.IsBuffOnMe("Savage Roar");
                
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Mangle Bear only
        public class NeedToBearMangle : Decorator
        {
            public NeedToBearMangle(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (!ClassHelper.Shapeshift.IsBearForm) return false;
                if (!Utils.IsCommonChecksOk("", false)) return false;
                //if (!Self.IsRageAbove(50)) return false;

                return (Spell.CanCast("Mangle (Bear)"));
            }
        }

        public class BearMangle : Action
        {
            protected override RunStatus Run(object context)
            {
                bool result = Spell.Cast("Mangle (Bear)");
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Skull Bash
        public class NeedToSkullBash : Decorator
        {
            public NeedToSkullBash(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToSkullBash", 1);
                string dpsSpell = ClassHelper.Shapeshift.IsCatForm ? "Skull Bash(Cat form)" : "Skull Bash(Bear form)";
                //string dpsSpell = "Skull Bash";

                if (!Me.GotTarget || CT.Dead) return false;
                if (!ClassHelper.Shapeshift.IsCatForm && !ClassHelper.Shapeshift.IsBearForm) return false;
                if (!Utils.IsCommonChecksOk(dpsSpell, false)) return false;
                if (!Target.IsCasting) return false;
                if (!CLC.ResultOK(Settings.SkullBash)) return false;
                //if (Target.IsDistanceMoreThan(Spell.MaxDistance(dpsSpell))) return false;
                

                return (Spell.CanCast(dpsSpell));
            }
        }

        public class SkullBash : Action
        {
            protected override RunStatus Run(object context)
            {
                //string dpsSpell = "Skull Bash";
                string dpsSpell = ClassHelper.Shapeshift.IsCatForm ? "Skull Bash(Cat form)" : "Skull Bash(Bear form)";
                bool result = Spell.Cast(dpsSpell);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Dash
        public class NeedToDash : Decorator
        {
            public NeedToDash(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (!ClassHelper.Shapeshift.IsCatForm) return false;
                if (!Utils.IsCommonChecksOk("Dash", false)) return false;
                if (Target.IsDistanceLessThan(30)) return false;

                return (Spell.CanCast("Dash"));
            }
        }

        public class Dash : Action
        {
            protected override RunStatus Run(object context)
            {
                Spell.Cast("Dash");
                Utils.LagSleep();
                bool result = Self.IsBuffOnMe("Dash");
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Swipe - Cat and Bear
        public class NeedToSwipe : Decorator
        {
            public NeedToSwipe(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToSwipe", 1);
                string swipeSpell = "Swipe (Cat)";
                if (ClassHelper.Shapeshift.IsBearForm) swipeSpell = "Swipe (Bear)";

                if (!ClassHelper.Shapeshift.IsCatForm && !ClassHelper.Shapeshift.IsBearForm) return false;
                if (!Utils.IsCommonChecksOk(swipeSpell, false)) return false;
                if (!Utils.Adds) return false;
                if (!Me.IsInParty && ClassHelper.Shapeshift.IsCatForm && !Self.IsEnergyAbove(80)) return false;
                if (Me.IsInParty && ClassHelper.Shapeshift.IsCatForm && !Self.IsEnergyAbove(30)) return false;
                if (ClassHelper.Shapeshift.IsBearForm && !Self.IsRageAbove(50)) return false;
                if (!CLC.ResultOK(Settings.Swipe)) return false;

                return (Spell.CanCast(swipeSpell));
            }
        }

        public class Swipe : Action
        {
            protected override RunStatus Run(object context)
            {
                string swipeSpell = "Swipe (Cat)";
                if (ClassHelper.Shapeshift.IsBearForm) swipeSpell = "Swipe (Bear)";

                bool result = Spell.Cast(swipeSpell);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        // SHRED DISABLED WHILE THE ISBEHIND BUG IS PRESENT IN hb
        #region Shred
        public class NeedToShred : Decorator
        {
            public NeedToShred(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                // IS BEHIND IS CURRENTLY BUGGED IN HB. FOR NOW DISABLE THIS SPELL
                return false;
                Debug.Log("NeedToShred", 1);
                if (!ClassHelper.Shapeshift.IsCatForm) return false;
                if (!Utils.IsCommonChecksOk("Shred", false)) return false;
                //Utils.Log("**** Me.IsBehind " + Me.IsSafelyBehind(CT));
                //Utils.Log("**** CT.MeIsBehing " + CT.MeIsSafelyBehind);
                //if (!CT.MeIsSafelyBehind) return false;
                if (Target.IsDistanceMoreThan(Spell.MaxDistance("Shred"))) return false;

                return (Spell.CanCastLUA("Shred"));
            }
        }

        public class Shred : Action
        {
            protected override RunStatus Run(object context)
            {
                bool result = Spell.Cast("Shred");
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Rip
        public class NeedToRip : Decorator
        {
            public NeedToRip(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToRip", 1);
                if (!ClassHelper.Shapeshift.IsCatForm) return false;
                if (!Utils.IsCommonChecksOk("Rip", false)) return false;
                if (!ClassHelper.ComboCheck(1,5)) return false;
                if (Target.IsDebuffOnTarget("Rip")) return false;
                if (!CLC.ResultOK(Settings.Rip)) return false;
                if (Target.IsDistanceMoreThan(Spell.MaxDistance("Rip"))) return false;

                return (Spell.CanCast("Rip"));
            }
        }

        public class Rip : Action
        {
            protected override RunStatus Run(object context)
            {
                Spell.Cast("Rip");
                Utils.LagSleep();
                bool result = Target.IsDebuffOnTarget("Rip");
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Maim
        public class NeedToMaim : Decorator
        {
            public NeedToMaim(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToMaim", 1);
                if (!ClassHelper.Shapeshift.IsCatForm) return false;
                if (!Utils.IsCommonChecksOk("Maim", false)) return false;
                if (!ClassHelper.ComboCheck(1,5)) return false;
                if (Target.IsDebuffOnTarget("Maim")) return false;
                if (!CLC.ResultOK(Settings.Maim)) return false;
                if (Target.IsDistanceMoreThan(Spell.MaxDistance("Maim"))) return false;

                return (Spell.CanCast("Maim"));
            }
        }

        public class Maim : Action
        {
            protected override RunStatus Run(object context)
            {
                Spell.Cast("Maim");
                Utils.LagSleep();
                bool result = Target.IsDebuffOnTarget("Maim");
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Thrash
        public class NeedToThrash : Decorator
        {
            public NeedToThrash(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToThrash", 1);
                if (!ClassHelper.Shapeshift.IsBearForm) return false;
                if (!Utils.Adds) return false;
                if (!Utils.IsCommonChecksOk("Thrash", true)) return false;
                if (Target.IsLowLevel) return false;
                if (!CLC.ResultOK(Settings.Thrash)) return false;

                return (Spell.CanCast("Thrash"));
            }
        }

        public class Thrash : Action
        {
            protected override RunStatus Run(object context)
            {
                bool result = Spell.Cast("Thrash");
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        // Bear stuff

        #region Feral Charge Bear - During combat only
        public class NeedToFeralChargeBear : Decorator
        {
            public NeedToFeralChargeBear(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                string dpsSpell = "Feral Charge (Bear)";
                if (!ClassHelper.Shapeshift.IsBearForm) return false;
                if (!Utils.IsCommonChecksOk(dpsSpell, false)) return false;
                if (Target.IsDistanceMoreThan(Spell.MaxDistance(dpsSpell))) return false;
                if (Target.IsDistanceLessThan(Spell.MinDistance(dpsSpell))) return false;

                return (Spell.CanCast(dpsSpell));
            }
        }

        public class FeralChargeBear : Action
        {
            protected override RunStatus Run(object context)
            {
                string dpsSpell = "Feral Charge (Bear)";
                bool result = Spell.Cast(dpsSpell);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Lacerate
        public class NeedToLacerate : Decorator
        {
            public NeedToLacerate(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (!ClassHelper.Shapeshift.IsBearForm) return false;
                if (!Utils.IsCommonChecksOk("Lacerate", false)) return false;
                if (Target.DebuffStackCount("Lacerate") >= 3) return false;
                if (Target.IsLowLevel) return false;
                if (Target.IsDistanceMoreThan(Spell.MaxDistance("Lacerate"))) return false;

                return (Spell.CanCast("Lacerate"));
            }
        }

        public class Lacerate : Action
        {
            protected override RunStatus Run(object context)
            {
                bool result = Spell.Cast("Lacerate");
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Pulverize
        public class NeedToPulverize : Decorator
        {
            public NeedToPulverize(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToPulverize", 1);
                if (!ClassHelper.Shapeshift.IsBearForm) return false;
                if (!Utils.IsCommonChecksOk("Pulverize", false)) return false;
                if (Target.DebuffStackCount("Pulverize") < 3) return false;
                //if (!CLC.ResultOK(Settings.Pulverize)) return false;
                //if (Target.IsLowLevel) return false;
                if (Target.IsDistanceMoreThan(Spell.MaxDistance("Pulverize"))) return false;

                return (Spell.CanCast("Pulverize"));
            }
        }

        public class Pulverize : Action
        {
            protected override RunStatus Run(object context)
            {
                bool result = Spell.Cast("Pulverize");
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Enrage
        public class NeedToEnrage : Decorator
        {
            public NeedToEnrage(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToEnrage", 1);
                if (!ClassHelper.Shapeshift.IsBearForm) return false;
                if (!Utils.IsCommonChecksOk("Enrage", false)) return false;
                if (Self.IsRageAbove(30)) return false;

                return (Spell.CanCast("Enrage"));
            }
        }

        public class Enrage : Action
        {
            protected override RunStatus Run(object context)
            {
                Spell.Cast("Enrage");
                Utils.LagSleep();
                bool result = Self.IsBuffOnMe("Enrage");
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Demoralizing Roar
        public class NeedToDemoralizingRoar : Decorator
        {
            public NeedToDemoralizingRoar(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToDemoralizingRoar", 1);
                if (!ClassHelper.Shapeshift.IsBearForm) return false;
                if (!Utils.IsCommonChecksOk("", false)) return false;
                if (Target.IsDebuffOnTarget("Demoralizing Roar")) return false;
                //if (!Utils.Adds) return false;
                if (Target.IsDistanceMoreThan(20)) return false;
                
                if (Target.IsLowLevel) return false;

                return (Spell.CanCast("Demoralizing Roar"));
            }
        }

        public class DemoralizingRoar : Action
        {
            protected override RunStatus Run(object context)
            {
                Spell.Cast("Demoralizing Roar");
                Utils.LagSleep();
                bool result = Target.IsDebuffOnTarget("Demoralizing Roar");
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Maul
        public class NeedToMaul : Decorator
        {
            public NeedToMaul(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToMaul", 1);
                string dpsSpell = "Maul(Bear Form)";

                if (!ClassHelper.Shapeshift.IsBearForm) return false;
                if (!Utils.IsCommonChecksOk(dpsSpell, false)) return false;
                //if (!Self.IsRageAbove(Settings.AttackEnergy)) return false;
                if (Spell.IsKnown("Mangle(Bear Form)") && !Spell.IsOnCooldown("Mangle(Bear Form)")) return false;

                if (Target.IsDistanceMoreThan(Spell.MaxDistance(dpsSpell))) return false;
                if (Spell.CanCast(dpsSpell)) return true;
                return false;
            }
        }

        public class Maul : Action
        {
            protected override RunStatus Run(object context)
            {
                string dpsSpell = "Maul";
                bool result = Spell.Cast(dpsSpell);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Bash - NOT FINISHED
        public class NeedToBash : Decorator
        {
            public NeedToBash(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                string spellName = "Bash";
                if (!Utils.IsCommonChecksOk(spellName, false)) return false;
                //if (!CLC.ResultOK(Settings.Bash)) return false;
                //if (Self.CanBuffMe(SpellName)) return false;
                //if (Target.CanDebuffTarget(SpellName)) return false;
                //if (Target.IsLowLevel) return false;

                return (Spell.CanCast(spellName));
            }
        }

        public class Bash : Action
        {
            protected override RunStatus Run(object context)
            {
                string spellName = "Bash";
                bool result = Spell.Cast(spellName);
                //Utils.LagSleep();
                //bool result = Target.IsDebuffOnTarget(SpellName);
                //bool result = Self.IsBuffOnMe(SpellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion








    }
}