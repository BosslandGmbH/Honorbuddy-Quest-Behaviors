using System.Linq;
using System.Threading;
using Hera.Helpers;
using Hera.SpellsMan;
using Styx.Logic;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Settings = Hera.Config.Settings;

namespace Hera
{
    public partial class Fpsware
    {
        #region Lifeblood
        public class NeedToLifeblood : Decorator
        {
            public NeedToLifeblood(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                string spellName = "Lifeblood";
                if (!Utils.IsCommonChecksOk(spellName, false)) return false;
                if (Self.IsHealthAbove(Settings.LifebloodHealth)) return false;
                if (Self.IsBuffOnMe("Lifeblood")) return false;

                return (Spell.CanCast(spellName));
            }
        }

        public class Lifeblood : Action
        {
            protected override RunStatus Run(object context)
            {
                string spellName = "Lifeblood";
                bool result = Spell.Cast(spellName);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion
        
        #region Select RAF Target
        public class NeedToSelectRAFTarget : Decorator
        {
            public NeedToSelectRAFTarget(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                CLC.RawSetting = Settings.RAFTarget;
                if (!CLC.IsOkToRun) return false;

                bool result = Me.IsInParty
                              && RaFHelper.Leader != null
                              && RaFHelper.Leader.GotTarget
                              && Me.GotTarget
                              && Me.CurrentTargetGuid != RaFHelper.Leader.CurrentTargetGuid;

                return result;
            }
        }

        public class SelectRAFTarget : Action
        {
            protected override RunStatus Run(object context)
            {
                RaFHelper.Leader.CurrentTarget.Target();
                Thread.Sleep(250);
                bool result = (Me.GotTarget && Me.CurrentTargetGuid == RaFHelper.Leader.CurrentTargetGuid);

                return result ? RunStatus.Success : RunStatus.Failure;

            }
        }
        #endregion

        #region Use Mana Potion
        public class NeedToUseManaPot : Decorator
        {
            public NeedToUseManaPot(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (!Utils.IsCommonChecksOk("", true)) return false;
                if (Self.IsManaAbove(Settings.ManaPotion)) return false;
                if (Inventory.ManaPotions.IsUseable) return true;

                return false;
            }
        }

        public class UseManaPot : Action
        {
            protected override RunStatus Run(object context)
            {
                Inventory.ManaPotions.Use();
                return RunStatus.Success;
            }
        }
        #endregion

        #region Use Health Potion
        public class NeedToUseHealthPot : Decorator
        {
            public NeedToUseHealthPot(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (!Utils.IsCommonChecksOk("", true)) return false;
                if (Self.IsHealthAbove(Settings.HealthPotion)) return false;
                if (Inventory.HealthPotions.IsUseable) return true;

                return false;
            }
        }

        public class UseHealthPot : Action
        {
            protected override RunStatus Run(object context)
            {
                Inventory.HealthPotions.Use();
                return RunStatus.Success;
            }
        }
        #endregion

        #region We got aggro during pull
        public class NeedToCheckAggroOnPull : Decorator
        {
            public NeedToCheckAggroOnPull(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToCheckAggroOnPull", 1);
                if (Me.Combat && !Me.Mounted)
                {

                    if (Targeting.Instance.TargetList.Count <= 0) return false;

                    if (Me.GotTarget && Target.IsDistanceMoreThan(10) && !CT.Combat)
                    {
                        return Targeting.Instance.TargetList.Where(mob => mob.CurrentTargetGuid == Me.Guid && Me.CurrentTargetGuid != mob.CurrentTargetGuid).Any();
                    }

                    if (!Me.GotTarget && Me.Combat)
                    {
                        return true;
                    }
                }


                return false;
            }
        }

        public class CheckAggroOnPull : Action
        {
            protected override RunStatus Run(object context)
            {
                foreach (WoWUnit mob in Targeting.Instance.TargetList.Where(mob => mob.CurrentTargetGuid == Me.Guid && Me.CurrentTargetGuid != mob.CurrentTargetGuid))
                {
                    //Utils.Log(string.Format("Looks like we got aggro from {0}", mob.Name), Utils.Colour("Red"));
                    Movement.StopMoving();
                    mob.Target();
                    Thread.Sleep(500);
                    //Target.Face();
                    break;
                }

                bool result = Me.GotTarget;
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Face Target
        public class NeedToFaceTarget : Decorator
        {
            public NeedToFaceTarget(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToFaceTarget", 1);
                if (!Me.GotTarget || Me.CurrentTarget.Dead) return false;
                if (Me.IsMoving) return false;
                return (!Me.IsSafelyFacing(CT));
            }
        }

        public class FaceTarget : Action
        {
            protected override RunStatus Run(object context)
            {
                CT.Face();
                bool result = true;
                Utils.Log("-Face the target", Utils.Colour("Blue"));

                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Face Target - Pull
        public class NeedToFaceTargetPull : Decorator
        {
            public NeedToFaceTargetPull(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToFaceTarget", 1);
                if (!Me.GotTarget || Me.CurrentTarget.Dead) return false;
                if (Me.IsMoving) return false;
                return (!Me.IsSafelyFacing(CT));
            }
        }

        public class FaceTargetPull : Action
        {
            protected override RunStatus Run(object context)
            {
                CT.Face();
                bool result = true;
                Utils.Log("-Face the target", Utils.Colour("Blue"));

                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Blacklist Pull Target
        public class NeedToBlacklistPullTarget : Decorator
        {
            public NeedToBlacklistPullTarget(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToBlacklistPullTarget", 1);
                return !Target.IsValidPullTarget;
            }
        }

        public class BlacklistPullTarget : Action
        {
            protected override RunStatus Run(object context)
            {
                Utils.Log(string.Format("Bad pull target blacklisting and finding another target."), Utils.Colour("Red"));
                Target.BlackList(100);
                Me.ClearTarget();

                return RunStatus.Success;
            }
        }
        #endregion
        
        #region Eat and Drink
        public class NeedToEatDrink : Decorator
        {
            public NeedToEatDrink(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToEatDrink", 1);
                if (Me.IsSwimming) return false;

                return (!Self.IsBuffOnMe("Food") && Me.HealthPercent < Settings.RestHealth || !Self.IsBuffOnMe("Drink") && Me.ManaPercent < Settings.RestMana);
            }
        }

        public class EatDrink : Action
        {
            protected override RunStatus Run(object context)
            {
                bool result = false;

                Inventory.Drink(false);
                Inventory.Eat(false);

                if (Self.IsBuffOnMe("Food") || Self.IsBuffOnMe("Drink")) result = true;

                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Cancel Food Drink Buff
        public class NeedToCancelFoodDrink : Decorator
        {
            public NeedToCancelFoodDrink(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToCancelFoodDrink", 1);
                return Self.IsHealthAbove(98) && Self.IsBuffOnMe("Food");
            }
        }

        public class CancelFoodDrink : Action
        {
            protected override RunStatus Run(object context)
            {
                Lua.DoString("CancelUnitBuff('player', 'Food') CancelUnitBuff('player', 'Drink')");
                return RunStatus.Success;
            }
        }
        #endregion

        #region DistanceCheck
        public class NeedToCheckDistance : Decorator
        {
            public NeedToCheckDistance(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToCheckDistance", 1);
                if (!Me.GotTarget || Me.CurrentTarget.Dead) return false;
                if (!Utils.IsCommonChecksOk("", false)) return false;
                return Movement.NeedToCheck;
            }
        }

        public class CheckDistance : Action
        {
            protected override RunStatus Run(object context)
            {
                Movement.DistanceCheck();
                return RunStatus.Success;
            }
        }
        #endregion


        #region Auto Attack
        public class NeedToAutoAttack : Decorator
        {
            public NeedToAutoAttack(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToAutoAttack", 1);
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

        #region LOS Check
        public class NeedToLOSCheck : Decorator
        {
            public NeedToLOSCheck(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (!Me.GotTarget || CT.Dead) return false;
                bool result = !GameWorld.IsInLineOfSight(Me.Location, Me.CurrentTarget.Location);

                return result;
            }
        }

        public class LOSCheck : Action
        {
            protected override RunStatus Run(object context)
            {
                if (Me.IsInInstance)
                {
                    //Utils.Log(string.Format("We don't have LOS on {0} moving closer...", CT.Name),System.Drawing.Color.FromName("DarkRed"));
                    Movement.MoveTo(1);
                    Thread.Sleep(250);
                    while (!GameWorld.IsInLineOfSight(Me.Location, Me.CurrentTarget.Location))
                    {
                        Movement.MoveTo(1);
                        Thread.Sleep(250);
                    }
                }
                else
                {
                    float distance = (float)CT.Distance2D * 0.5f;

                    Utils.Log(string.Format("We don't have LOS on {0} moving closer...", CT.Name), System.Drawing.Color.FromName("DarkRed"));
                    Movement.MoveTo(distance);

                    Thread.Sleep(250);
                    while (Me.IsMoving)
                    {
                        Thread.Sleep(250);
                    }
                }

                return RunStatus.Success;
            }
        }
        #endregion



    }
}
