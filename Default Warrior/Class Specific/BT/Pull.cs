using Hera.Helpers;
using Hera.SpellsMan;
using Styx.Logic;
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
                new Decorator(ret => Me.GotTarget && !Target.CanGenerateNavPath,
                              new Action(ret => Target.BlackList(1000))),

                // Check if the target is suitable for pulling, if not blacklist it
                new NeedToBlacklistPullTarget(new BlacklistPullTarget()),

                // Dismount if mounted. Here to work around a bug in HB
                new Decorator(ret => Me.Mounted, new Action(ret => Mount.Dismount())),

                // Check pull timers and blacklist bad pulls where required
                new NeedToCheckPullTimer(new BlacklistPullTarget()),

                // Auto Attack
                new Decorator(ret => !Me.IsAutoAttacking, new Action(ret => Utils.AutoAttack(true))),


                // *******************************************************

                // Face Target Pull
                new NeedToFaceTargetPull(new FaceTargetPull()),

                // Charge
                new NeedToCharge(new Charge()),

                // Intercept
                new NeedToIntercept(new Intercept()),

                // Stance Dance
                new NeedToStanceDanceBattlePull(new StanceDanceBattlePull()),

                // Heroic Throw Pull
                new NeedToHeroicThrowPull(new HeroicThrowPull()),

                // Finally just move to target.
                new Action(ret => Movement.DistanceCheck(ClassHelper.MaximumDistance, 2.0))
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

        #region Charge
        public class NeedToCharge : Decorator
        {
            public NeedToCharge(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                string spellName = "Charge";
                if (!Me.GotTarget) return false;
                if (!Spell.IsKnown("Charge")) return false;
                if (Spell.IsOnCooldown("Charge")) return false;
                
                //if (!Utils.IsCommonChecksOk(spellName, false)) return false;
                if (!ClassHelper.Stance.IsBattleStance) return false;

                //Styx.WoWInternals.ObjectManager.Update();
                //if (!Spell.CanCastLUA("Charge")) return false;
                if (Target.IsDistanceLessThan(9)) return false;
                if (Target.IsDistanceMoreThan(24)) return false;

                return Spell.CanCast(spellName);
                //return (Spell.CanCastLUA(spellName));
            }
        }

        public class Charge : Action
        {
            protected override RunStatus Run(object context)
            {
                Target.Face();
                Utils.LagSleep();

                string spellName = "Charge";
                bool result = Spell.Cast(spellName);
                while (Target.IsDistanceMoreThan(8)) { System.Threading.Thread.Sleep(150); }

                if (Spell.IsOnCooldown("Charge"))
                {
                    System.Threading.Thread.Sleep(1000);
                    if (CLC.ResultOK(Config.Settings.ThunderClap) && Spell.CanCast("Thunder Clap"))
                    {
                        Spell.Cast("Thunder Clap");
                    }

                    if (Spell.IsKnown("Hamstring") && !Target.IsDebuffOnTarget("Hamstring") && CLC.ResultOK(Config.Settings.Hamstring))
                    {
                        System.Threading.Thread.Sleep(1000);
                        Spell.CanCast("Hamstring");
                    }
                }

                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Intercept
        public class NeedToIntercept: Decorator
        {
            public NeedToIntercept(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                string spellName = "Intercept";
                if (!Utils.IsCommonChecksOk(spellName, false)) return false;
                if (!ClassHelper.Stance.IsBerserkerStance) return false;
                if (Target.IsDistanceLessThan(Spell.MinDistance(spellName))) return false;
                if (Target.IsDistanceMoreThan(Spell.MaxDistance(spellName))) return false;

                return (Spell.CanCast(spellName));
            }
        }

        public class Intercept : Action
        {
            protected override RunStatus Run(object context)
            {
                Target.Face();
                Utils.LagSleep();

                string spellName = "Intercept";
                bool result = Spell.Cast(spellName);

                /*
                if (Utils.IsBattleground && Spell.CanCast("Hamstring") && !Target.IsDebuffOnTarget("Hamstring"))
                {
                    System.Threading.Thread.Sleep(1000);
                    Spell.CanCast("Hamstring");
                }
                 */


                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Stance Dance - Battle Stance
        public class NeedToStanceDanceBattlePull : Decorator
        {
            public NeedToStanceDanceBattlePull(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {

                if (ClassHelper.Stance.IsBattleStance) return false;
                if (Spell.IsKnown("Intercept") && !Spell.IsOnCooldown("Intercept") && Self.IsRageAbove(12)) return false;
                if (Spell.IsOnCooldown("Charge")) return false;
                if (Target.IsDistanceLessThan(15)) return false;

                return (Spell.CanCast("Battle Stance"));
            }
        }

        public class StanceDanceBattlePull : Action
        {
            protected override RunStatus Run(object context)
            {
                Spell.Cast("Battle Stance");
                Utils.LagSleep();

                bool result = ClassHelper.Stance.IsBattleStance;
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Heroic Throw Pull

        public class NeedToHeroicThrowPull : Decorator
        {
            public NeedToHeroicThrowPull(Composite child) : base(child)
            {
            }

            protected override bool CanRun(object context)
            {
                string SpellName = "Heroic Throw";
                if (!Utils.IsCommonChecksOk(SpellName, false)) return false;
                if (ClassHelper.Stance.IsBattleStance && Spell.IsKnown("Charge") && !Spell.IsOnCooldown("Charge")) return false;
                if (ClassHelper.Stance.IsBerserkerStance && Spell.IsKnown("Intercept") && !Spell.IsOnCooldown("Intercept")) return false;

                return (Spell.CanCast(SpellName));
            }
        }

        public class HeroicThrowPull : Action
        {
            protected override RunStatus Run(object context)
            {
                string SpellName = "Heroic Throw";
                bool result = Spell.Cast(SpellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion
    
    }
}