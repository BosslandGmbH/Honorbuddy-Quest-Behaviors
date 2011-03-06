using Hera.Config;
using Hera.Helpers;
using Hera.SpellsMan;
using Styx.Logic;
using Styx.WoWInternals;
using TreeSharp;

namespace Hera
{
    public partial class Fpsware
    {
        // ******************************************************************************************
        // This is where the pull action takes place

        #region Pull
        // _pullBehavior is a class that will hold our logic. 
        private Composite _pullBehavior;
        public override Composite PullBehavior
        {
            get { if (_pullBehavior == null) { Utils.Log("Creating 'Pull' behavior"); _pullBehavior = CreatePullBehavior(); }  return _pullBehavior; }
        }

        private PrioritySelector CreatePullBehavior()
        {
            return new PrioritySelector(

                // All spells are checked in the order listed below, a prioritised order
                // Put the most important spells at the top of the list

                // If we can't reach the target blacklist it.
                new Decorator(ret => !Target.CanGenerateNavPath, new Action(ret => Target.BlackList(1000))),

                // Check if the target is suitable for pulling, if not blacklist it
                new NeedToBlacklistPullTarget(new BlacklistPullTarget()),

                // Dismount if mounted. Here to work around a bug in HB
                new Decorator(ret => Me.Mounted, new Action(ret => Mount.Dismount())),

                // Check pull timers and blacklist bad pulls where required
                new NeedToCheckPullTimer(new BlacklistPullTarget()),

                // Face the target before attempting to cast spells
                // *** DISABLED - CAUSES PROBLEMS
                //new NeedToFaceTarget(new FaceTarget()),

                // Auto Attack
                // Don't auto attack if you're prowling
                new Decorator(ret => Me.IsAutoAttacking && Self.IsBuffOnMe("Prowl"), new Action(ret => Utils.AutoAttack(false))),
                // DO auto attack if we don't know Faerie Fire
                new Decorator(ret => !Me.IsAutoAttacking && !Spell.IsKnown("Faerie Fire"), new Action(ret => Utils.AutoAttack(true))),

                // Shapeshift
                new NeedToShapeshift(new Shapeshift()),

                // *******************************************************
                // Feral pulling

                // Prowl
                new NeedToProwlPull(new ProwlPull()),

                // Dash 
                new NeedToDashPull(new DashPull()),

                // Feral Charge
                new NeedToFeralChargePull(new FeralChargePull()),

                // Ravage
                new NeedToRavagePull(new RavagePull()),
                
                // Pounce
                new NeedToPouncePull(new PouncePull()),

                // Rake
                new NeedToRakePull(new RakePull()),
                

                // *******************************************************
                // Balance pulling

                // Caster Pull 
                new NeedToCasterPullSpell(new CasterPullSpell()),


                // *******************************************************
                // Everything else

                // Faerie Fire
                new NeedToFaerieFireFeralPull(new FaerieFireFeralPull()),

                // Finally just move to target.
                // If we are Cat/Bear make some exceptions with the pull range - move to at least 1.75 yards on pull
                new Decorator(ret => ClassHelper.Shapeshift.IsCatForm || ClassHelper.Shapeshift.IsBearForm, new Action(ret => Movement.DistanceCheck(5.7,1.0))),
                new Action(ret => Movement.DistanceCheck())
                );
        }
        #endregion

        #region Pull Timer / Timeout
        public class NeedToCheckPullTimer : Decorator
        {
            public NeedToCheckPullTimer(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                return Target.PullTimerExpired;
            }
        }

        public class CheckPullTimer : Action
        {
            protected override RunStatus Run(object context)
            {
                Utils.Log(string.Format("Unable to pull {0}, blacklisting and finding another target.", Me.CurrentTarget.Name), System.Drawing.Color.FromName("Red"));
                Target.BlackList(1200);
                Me.ClearTarget();

                return RunStatus.Success;
            }
        }
        #endregion

        #region Combat Timer / Timeout
        public class NeedToCheckCombatTimer : Decorator
        {
            public NeedToCheckCombatTimer(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToCheckCombatTimer", 1);
                if (!Me.GotTarget || Me.CurrentTarget.Dead) return false;
                if (Target.IsElite) return false;
                

                return Target.CombatTimerExpired && Target.IsHealthAbove(95);
            }
        }

        public class CheckCombatTimer : Action
        {
            protected override RunStatus Run(object context)
            {
                Utils.Log(string.Format("Combat with {0} is bugged, blacklisting and finding another target.", Me.CurrentTarget.Name), System.Drawing.Color.FromName("Red"));
                Target.BlackList(1200);
                Utils.LagSleep();

                return RunStatus.Success;
            }
        }
        #endregion



        // ******************************************************************************************
        // Pull spells are here

        #region Pull - Prowl
        public class NeedToProwlPull : Decorator
        {
            public NeedToProwlPull(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (Settings.PullFeral.Contains("Faerie")) return false;
                if (!Spell.IsKnown("Pounce")) return false;

                if (Me.Level < 10)
                {
                    if (ClassHelper.ClassSpec == ClassType.None && !Spell.IsKnown("Cat Form")) return false;
                }
                else
                {
                    if (ClassHelper.ClassSpec != ClassType.Feral) return false;
                }

                string dpsSpell = "Prowl";
                if (!ClassHelper.Shapeshift.IsCatForm) return false;
                if (!Utils.IsCommonChecksOk(dpsSpell, false)) return false;

                return (Spell.CanCast(dpsSpell));
            }
        }

        public class ProwlPull : Action
        {
            protected override RunStatus Run(object context)
            {
                string dpsSpell = "Prowl";
                Spell.Cast(dpsSpell);
                Utils.LagSleep();
                bool result = Target.IsDebuffOnTarget(dpsSpell);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Pull - Faerie Fire
        public class NeedToFaerieFireFeralPull : Decorator
        {
            public NeedToFaerieFireFeralPull(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                string dpsSpell = "Faerie Fire (Feral)";
                if (!ClassHelper.Shapeshift.IsCatForm) return false;
                if (!Settings.PullFeral.Contains("Faerie"))
                    if (Spell.IsKnown("Ravage") && Spell.IsKnown("Pounce")) return false;
                if (!Utils.IsCommonChecksOk(dpsSpell, false)) return false;
                if (!Target.CanDebuffTarget(dpsSpell)) return false;
                if (Target.IsDistanceMoreThan(Spell.MaxDistance(dpsSpell))) return false;

                return (Spell.CanCast(dpsSpell));
            }
        }

        public class FaerieFireFeralPull : Action
        {
            protected override RunStatus Run(object context)
            {
                string dpsSpell = "Faerie Fire (Feral)";
                Spell.Cast(dpsSpell);
                Utils.LagSleep();
                bool result = Target.IsDebuffOnTarget(dpsSpell);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Pull - Dash
        public class NeedToDashPull : Decorator
        {
            public NeedToDashPull(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                string dpsSpell = "Dash";
                if (!ClassHelper.Shapeshift.IsCatForm) return false;
                if (!Self.IsBuffOnMe("Prowl")) return false;
                if (!Utils.IsCommonChecksOk(dpsSpell, false)) return false;
                if (!Target.IsDistanceMoreThan(30)) return false;

                return (Spell.CanCast(dpsSpell));
            }
        }

        public class DashPull : Action
        {
            protected override RunStatus Run(object context)
            {
                string dpsSpell = "Dash";
                Spell.Cast(dpsSpell);
                Utils.LagSleep();
                bool result = Target.IsDebuffOnTarget(dpsSpell);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Pull - Feral Charge
        public class NeedToFeralChargePull : Decorator
        {
            public NeedToFeralChargePull(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                string dpsSpell = ClassHelper.Shapeshift.IsBearForm ? "Feral Charge (Bear)" : "Feral Charge (Cat)";
                if (!ClassHelper.Shapeshift.IsCatForm && !ClassHelper.Shapeshift.IsBearForm) return false;
                if (Settings.PullFeral.Contains("Faerie")) return false;
                if (!Utils.IsCommonChecksOk(dpsSpell, false)) return false;
                //if (CT.Distance < 8 || CT.Distance > 25) return false;
                if (Target.IsDistanceMoreThan(Spell.MaxDistance(dpsSpell))) return false;
                if (Target.IsDistanceLessThan(Spell.MinDistance(dpsSpell))) return false;

                return (Spell.CanCast(dpsSpell));
            }
        }

        public class FeralChargePull : Action
        {
            protected override RunStatus Run(object context)
            {
                string dpsSpell = ClassHelper.Shapeshift.IsBearForm ? "Feral Charge (Bear)" : "Feral Charge (Cat)";
                Target.Face();
                bool result = Spell.Cast(dpsSpell);
                Target.Face();
                // TEMP WORK AROUND FOR BUG IN HB. TRY TO RAVAGE IMMEDIATELY AFTER CASTING FERAL CHARGE
                if (ClassHelper.Shapeshift.IsCatForm) { Spell.Cast("Ravage"); Utils.LagSleep(); Spell.Cast("Ravage"); }
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        // RAVAGE DISABLED WHILE THE ISBEHIND BUG IS PRESENT IN HB
        #region Pull - Ravage
        public class NeedToRavagePull : Decorator
        {
            public NeedToRavagePull(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                return false;
                string dpsSpell = "Ravage";
                if (!Self.IsBuffOnMe("Prowl")) return false;                    // Will always be Cat Form if Prowl buff is present
                if (!Utils.IsCommonChecksOk(dpsSpell, false)) return false;
                if (Target.IsDistanceMoreThan(Spell.MaxDistance(dpsSpell))) return false;
                if (!Me.IsSafelyBehind(CT)) return false;

                return (Spell.CanCast(dpsSpell));
            }
        }

        public class RavagePull : Action
        {
            protected override RunStatus Run(object context)
            {
                Target.Face();
                string dpsSpell = "Ravage";
                bool result = Spell.Cast(dpsSpell);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        // RAVAGE PORTION OF THIS REMOVED DUE TO BUG IN HB
        #region Pull - Pounce
        public class NeedToPouncePull : Decorator
        {
            public NeedToPouncePull(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                string dpsSpell = "Pounce";
                if (!ClassHelper.Shapeshift.IsCatForm) return false;
                if (!Utils.IsCommonChecksOk(dpsSpell, false)) return false;
                if (!Me.Auras.ContainsKey("Prowl")) return false;
                if (Target.IsDistanceMoreThan(5.75)) return false;
                
                // This is an expierement to see if we can prioritise Ravage over Pounce if we are BEHIND the target, even if you don't have enough energy - yet
                //if (Me.IsSafelyBehind(CT) && Spell.IsKnown("Ravage")) return false;);
                return (Spell.CanCast(dpsSpell));
            }
        }

        public class PouncePull : Action
        {
            protected override RunStatus Run(object context)
            {
                Target.Face();
                string dpsSpell = "Pounce";
                bool result = Spell.Cast(dpsSpell);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Pull - Rake
        public class NeedToRakePull : Decorator
        {
            public NeedToRakePull(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                string dpsSpell = "Rake";
                if (!Me.GotTarget || Me.GotTarget && Target.IsDistanceMoreThan(5.75)) return false;
                if (!ClassHelper.Shapeshift.IsCatForm) return false;
                if (Spell.IsKnown("Ravage") && Spell.IsKnown("Pounce")) return false;       // Only use Rake if you don't know Pounce or Ravage
                if (!Utils.IsCommonChecksOk(dpsSpell, false)) return false;
                if (!CLC.ResultOK(Settings.Rake)) return false;

                return (Spell.CanCast(dpsSpell));
            }
        }

        public class RakePull : Action
        {
            protected override RunStatus Run(object context)
            {
                string dpsSpell = "Rake";
                bool result = Spell.Cast(dpsSpell);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Pull - Caster / Balance
        public class NeedToCasterPullSpell : Decorator
        {
            public NeedToCasterPullSpell(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (ClassHelper.ClassSpec == ClassType.Feral) return false;

                string dpsSpell = Settings.PullBalance;
                if (dpsSpell.Contains("Automatic"))
                {
                    if (Spell.CanCast("Starsurge"))
                    {
                        dpsSpell = "Starsurge";
                    }
                    else if (Spell.CanCast("Starfire"))
                    {
                        dpsSpell = "Starfire";
                    }
                    else if (Spell.CanCast("Starfire"))
                    {
                        dpsSpell = "Starfire";
                    }
                    else dpsSpell = "Wrath";
                }


                if (!Utils.IsCommonChecksOk(dpsSpell, false)) return false;
                if (!ClassHelper.Shapeshift.IsCasterCapable) return false;
                if (Target.IsDistanceMoreThan(Spell.MaxDistance(dpsSpell))) return false;

                bool result = (Spell.CanCast(dpsSpell));
                return result;
            }
        }

        public class CasterPullSpell : Action
        {
            protected override RunStatus Run(object context)
            {
                string dpsSpell = Settings.PullBalance;
                if (dpsSpell.Contains("Automatic"))
                {
                    if (Spell.CanCast("Starsurge"))
                    {
                        dpsSpell = "Starsurge";
                    }
                    else if (Spell.CanCast("Starfire"))
                    {
                        dpsSpell = "Starfire";
                    }
                    else if (Spell.CanCast("Starfire"))
                    {
                        dpsSpell = "Starfire";
                    }
                    else dpsSpell = "Wrath";
                }

                if (Me.IsMoving) WoWMovement.MoveStop();
                Target.Face();
                Utils.LagSleep();

                bool result = Spell.Cast(dpsSpell);
                if (result)
                {
                    Utils.LagSleep();
                    Utils.WaitWhileCasting(); 
                    if (Target.CanDebuffTarget("Moonfire") && CLC.ResultOK(Settings.Moonfire)) { Spell.Cast("Moonfire"); }
                }


                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

    
    }
}