using System.Linq;
using System.Threading;
using Hera.Helpers;
using Hera.SpellsMan;
using Styx.Logic;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Settings = Hera.Config.Settings;

namespace Hera
{
    public partial class Fpsware
    {

        // ******************************************************************************************
        //
        // This is where the Composites start
        //


        // Healing logic, everyone needs to heal (almost everyone) and this will be our first BT example
        #region Heal Behaviour

        // _healBehavior is a class that will hold our logic. This code is created a bit further down
        private Composite _healBehavior;

        // We override the HB HealBehavior and return our own, in this case we return _healBehavior
        public override Composite HealBehavior
        {
            get
            {
                // First check if _healBehavior has anything in it. If its null then its not going to do anything
                if (_healBehavior == null)
                {
                    // We're here because _healBehavior is null so we need to assign our logic to it. 
                    Utils.Log("Creating 'Heal' behavior");

                    // CreateHealBehavior is a class we create that holds all our logic and we assign that to _healBehaviour
                    // But we only do it once and this happens during the CC initialisation
                    _healBehavior = CreateHealBehavior();
                }

                return _healBehavior;
            }
        }

        // This is the place we create our healing logic
        // Instead of getting overly complicated (and messy) and using Decorators, Selectors and Actions I use seperate classes 
        // Basically CreateHealBehavior is a single line of code, we just add some white space to make it readable
        //
        private static Composite CreateHealBehavior()
        {
            return new PrioritySelector(

                // Use a mana potion if we need it
                //new NeedToUseManaPot(new UseManaPot()),

                // AoE!
                new NeedToDedicatedHealingAoE(new DedicatedHealingAoE()),

                // Heal the tank!
                new NeedToDedicatedHealingTank(new DedicatedHealingTank()),

                // Heal the party!
                new NeedToDedicatedHealingParty(new DedicatedHealingParty()),

                // --------------------------------------------------------------------------------
                // Dedicated Healer Check - If you're Restoration spec DO NOT go past this point.
                new NeedToDedicatedHealerCheck(new DedicatedHealerCheck()),
                // --------------------------------------------------------------------------------
                
                // Healing Touch
                new NeedToHealingTouch(new HealingTouch()),

                // Regrowth
                new NeedToRegrowth(new Regrowth()),

                // Check if party / raid members need healing
                new NeedToHealPartyMembers(new HealPartyMembers()),

                // Mark of the Wild
                new NeedToMarkOfTheWild(new MarkOfTheWild())

                );
        }

        #endregion



        // Rest Behaviour
        //   * Healing
        //   * Eat and Drink
        //   * Buffs
        #region Rest Behaviour
        private Composite _restBehavior;
        public override Composite RestBehavior
        {
            get { if (_restBehavior == null) { Utils.Log("Creating 'Rest' behavior"); _restBehavior = CreateRestBehavior(); } return _restBehavior; }
        }

        private Composite CreateRestBehavior()
        {
            return new PrioritySelector(

                // We're full. Stop eating/drinking
                // No point sitting there doing nothing wating for the Eat/Drink buff to disapear
                new NeedToCancelFoodDrink(new CancelFoodDrink()),

                // Healing Touch
                new NeedToHealingTouch(new HealingTouch()),

                // Regrowth
                new NeedToRegrowth(new Regrowth()),

                // Eat and Drink
                new NeedToEatDrink(new EatDrink()),

                // Mark of the Wild
                new NeedToMarkOfTheWild(new MarkOfTheWild())

                

                );
        }

        #endregion




        // ******************************************************************************************
        //
        // This is where the common priority selectors start
        // These can be used with ANY class, they are all non-class specific
        //


        // Check your distance to the target and move appropriately
        //      * If you're too close and moving then stop moving
        //      * If you're too far away then move closer
        #region DistanceCheck
        public class NeedToCheckDistance : Decorator
        {
            public NeedToCheckDistance(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToCheckDistance", 1);
                if (!Me.GotTarget) return false;
                if (Me.CurrentTarget.Dead) return false;
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

        // Cancel the food / drink buff if we're above 98%
        #region Cancel Food Drink Buff
        public class NeedToCancelFoodDrink : Decorator
        {
            public NeedToCancelFoodDrink(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToCancelFoodDrink", 1);
                return Self.IsHealthAbove(98) && Self.IsManaAbove(98) && (Self.IsBuffOnMe("Food") || Self.IsBuffOnMe("Drink"));
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

        #region Eat and Drink
        public class NeedToEatDrink : Decorator
        {
            public NeedToEatDrink(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToEatDrink", 1);
                if (Me.IsSwimming) return false;
                if (Self.IsBuffOnMe("Innervate")) return false;
                if (Me.IsInParty && (RaFHelper.Leader !=null && RaFHelper.Leader.Distance > 34)) return false;
                //if (Styx.Logic.Common.Rest.NoDrink) return false;

                return !Self.IsBuffOnMe("Food") && !Self.IsBuffOnMe("Drink") && Me.ManaPercent < Settings.RestMana;
            }
        }

        public class EatDrink : Action
        {
            protected override RunStatus Run(object context)
            {
                bool result = false;

                Inventory.Drink(false);

                if (Self.IsBuffOnMe("Food") || Self.IsBuffOnMe("Drink")) result = true;

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
                return false;       // Trying to fix a potion use isse

                if (!Utils.IsCommonChecksOk("", true)) return false;

                if (Self.IsManaAbove(Settings.PotionMana)) return false;

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

        #region We got aggro during pull
        public class NeedToCheckAggroOnPull : Decorator
        {
            public NeedToCheckAggroOnPull(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToCheckAggroOnPull",1);
                if (Me.Combat && !Me.Mounted)
                {

                    if (Targeting.Instance.TargetList.Count <= 0) return false;

                    if (Me.GotTarget && Target.IsDistanceMoreThan(15) && !CT.Combat)
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
                    //Movement.StopMoving();
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
        public class NeedToFaceTarget: Decorator
        {
            public NeedToFaceTarget(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToFaceTarget", 1);
                if (!Me.GotTarget || Me.CurrentTarget.Dead) return false;
                return (!Me.IsSafelyFacing(CT));
            }
        }

        public class FaceTarget : Action
        {
            protected override RunStatus Run(object context)
            {
                CT.Face();
                bool result = true;
                Utils.Log("- Face the target",Utils.Colour("Blue"));

                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Blacklist Pull Target
        public class NeedToBlacklistPullTarget: Decorator
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

        #region Heal Party Members
        public class NeedToHealPartyMembers : Decorator
        {
            public NeedToHealPartyMembers(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (!Utils.IsCommonChecksOk("Regrowth", true)) return false;
                Debug.Log("NeedToHealPartyMembers", 1);


                if (!CLC.ResultOK(Settings.HealPartyMembers)) return false;
                if ((ClassHelper.ClassSpec == ClassType.Feral || ClassHelper.ClassSpec == ClassType.Balance) && Me.IsInParty) return false;

                WoWUnit p = RAF.PlayerNeedsHealing(Settings.RegrowthHealth);
                if (p != null)
                {
                    Debug.Log(".. result = " + p.Name, 1);
                }
                return p != null;
            }
        }

        public class HealPartyMembers : Action
        {
            protected override RunStatus Run(object context)
            {
                WoWUnit p = RAF.PlayerNeedsHealing(Settings.RegrowthHealth);
                Utils.Log("Healing player " + p.Name);
                bool result = Spell.Cast("Regrowth", p);
                Utils.LagSleep();
                Utils.WaitWhileCasting();
                //Utils.WaitWhileCasting(Utils.CastingBreak.HealthIsAbove,Settings.RegrowthHealth);

                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Regrowth
        public class NeedToRegrowth : Decorator
        {
            public NeedToRegrowth(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToRegrowth", 1);
                double regrowthHealth = Settings.RegrowthHealth;
                if (ClassHelper.Shapeshift.IsBearForm) regrowthHealth = regrowthHealth * 0.75;

                if (Self.IsHealthAbove((int)regrowthHealth)) return false;

                //if (Self.IsHealthAbove(Settings.RegrowthHealth)) return false;
                if (!Utils.IsCommonChecksOk("Regrowth", true)) return false;
                if (!Self.CanBuffMe("Regrowth")) return false;
                if (!CLC.ResultOK(Settings.Regrowth)) return false;
                if (Me.GotTarget && !Utils.Adds && Me.HealthPercent > 25 && (Me.HealthPercent * 1.2) > CT.HealthPercent) return false;

                bool result = (Spell.IsKnown("Regrowth") && !Self.IsBuffOnMe("Regrowth"));
                return result;
            }
        }

        public class Regrowth : Action
        {
            protected override RunStatus Run(object context)
            {
                bool result = Spell.Cast("Regrowth");
                Utils.LagSleep();
                Thread.Sleep(150);
                Utils.WaitWhileCasting(Utils.CastingBreak.HealthIsAbove, Settings.RegrowthHealth, Me);
                
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Healing Touch
        public class NeedToHealingTouch : Decorator
        {
            public NeedToHealingTouch(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToHealingTouch", 1);
                double healingthouchHealth = Settings.HealingTouchHealth;
                if (ClassHelper.Shapeshift.IsBearForm) healingthouchHealth = healingthouchHealth*0.75;
                if (Self.IsHealthAbove((int)healingthouchHealth)) return false;
                if (!Utils.IsCommonChecksOk("Healing Touch", true)) return false;
                if (!CLC.ResultOK(Settings.HealingTouch)) return false;
                if (Me.GotTarget && !Utils.Adds && Me.HealthPercent > 25 && (Me.HealthPercent * 1.2) > CT.HealthPercent) return false;
                bool result = (Spell.IsKnown("Healing Touch"));
                return result;
            }
        }

        public class HealingTouch : Action
        {
            protected override RunStatus Run(object context)
            {
                bool result = Spell.Cast("Healing Touch");
                Utils.LagSleep();
                Thread.Sleep(150);
                Utils.WaitWhileCasting(Utils.CastingBreak.HealthIsAbove, Settings.HealingTouchHealth, Me);
                
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Mark of the Wild
        public class NeedToMarkOfTheWild : Decorator
        {
            public NeedToMarkOfTheWild(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToMarkOfTheWild", 1);
                if (Me.Combat) return false;
                if (!Self.CanBuffMe("Mark of the Wild")) return false;

                bool result = Spell.CanCast("Mark of the Wild");
                return result;
            }
        }

        public class MarkOfTheWild : Action
        {
            protected override RunStatus Run(object context)
            {
                bool result = Spell.Cast("Mark of the Wild", Me);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion


        // Dedicated Healing Logic

        #region Dedicated Healing AoE
        public class NeedToDedicatedHealingAoE: Decorator
        {
            public NeedToDedicatedHealingAoE(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (!ClassHelper.IsHealerOnly) return false;
                if (!Spell.CanCast("Wild Growth")) return false;

                if (RAF.CountOfPlayersInNeed(75) > 2)return true;


                return false;
            }
        }

        public class DedicatedHealingAoE : Action
        {
            protected override RunStatus Run(object context)
            {
                bool result;
                WoWUnit p = RAF.PlayerNeedsHealing(75);
                result = Spell.Cast("Wild Growth", p);

                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Dedicated Healing Tank
        public class NeedToDedicatedHealingTank : Decorator
        {
            public NeedToDedicatedHealingTank(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (!ClassHelper.IsHealerOnly) return false;

                if (RaFHelper.Leader.Combat)
                {
                    if (!RaFHelper.Leader.HasAura("Rejuvenation") && Spell.CanCast("Rejuvenation")) return true;
                    if (!RaFHelper.Leader.HasAura("Lifebloom") && Spell.CanCast("Lifebloom")) return true;
                    if (RaFHelper.Leader.HealthPercent < 90 && !RaFHelper.Leader.HasAura("Regrowth") && Spell.CanCast("Regrowth")) return true;
                    if (RaFHelper.Leader.HealthPercent < 65) return true;
                }
                else
                {
                    if (!RaFHelper.Leader.HasAura("Regrowth") && RaFHelper.Leader.HealthPercent < 90 && Spell.CanCast("Regrowth")) return true;
                }

                return false;
            }
        }

        public class DedicatedHealingTank : Action
        {
            protected override RunStatus Run(object context)
            {
                bool result = false;

                if (RaFHelper.Leader.Combat)
                {
                    if (RaFHelper.Leader.HealthPercent < 90 && !RaFHelper.Leader.HasAura("Regrowth") && Spell.CanCast("Regrowth"))
                    {
                        result = Spell.Cast("Regrowth", RaFHelper.Leader);
                        Utils.LagSleep();
                        Utils.WaitWhileCasting();
                        //if (result) return RunStatus.Success;
                    }

                    if (!RaFHelper.Leader.HasAura("Rejuvenation") && Spell.CanCast("Rejuvenation"))
                    {
                        result = Spell.Cast("Rejuvenation", RaFHelper.Leader);
                        Utils.LagSleep();
                        Utils.WaitWhileCasting();
                        //if (result) return RunStatus.Success;
                    }

                    if (!RaFHelper.Leader.HasAura("Lifebloom") && Spell.CanCast("Lifebloom"))
                    {
                        result = Spell.Cast("Lifebloom", RaFHelper.Leader);
                        Utils.LagSleep();
                        Utils.WaitWhileCasting();
                        if (result) return RunStatus.Success;
                    }


                    if (RaFHelper.Leader.HealthPercent < 65 && RaFHelper.Leader.HealthPercent > 50)
                    {
                        result = Spell.Cast("Healing Touch", RaFHelper.Leader);
                        Utils.LagSleep();
                        Utils.WaitWhileCasting();
                        if (result) return RunStatus.Success;
                    }

                    if (RaFHelper.Leader.HealthPercent < 50)
                    {
                        result = Spell.Cast("Regrowth", RaFHelper.Leader);
                        Utils.WaitWhileCasting();
                        if (result) return RunStatus.Success;
                    }

                }



                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Dedicated Healing Party
        public class NeedToDedicatedHealingParty: Decorator
        {
            public NeedToDedicatedHealingParty(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (!ClassHelper.IsHealerOnly) return false;

                WoWUnit p = RAF.PlayerNeedsHealingExcludeTank(90);
                if (p == null) return false;

                if (p.Combat)
                {
                    if (p.HealthPercent < 90 && !p.HasAura("Rejuvenation") && Spell.CanCast("Rejuvenation")) return true;
                    if (p.HealthPercent < 80 && !p.HasAura("Regrowth") && Spell.CanCast("Regrowth")) return true;
                    if (p.HealthPercent < 65) return true;
                }
                else
                {
                    if (!p.HasAura("Regrowth") && p.HealthPercent < 90 && Spell.CanCast("Regrowth")) return true;
                }

                return false;
            }
        }

        public class DedicatedHealingParty : Action
        {
            protected override RunStatus Run(object context)
            {
                bool result = false;
                WoWUnit p = RAF.PlayerNeedsHealingExcludeTank(90);

                if (p.Combat)
                {
                    if (p.HealthPercent < 90 && !p.HasAura("Rejuvenation") && Spell.CanCast("Rejuvenation"))
                    {
                        result = Spell.Cast("Rejuvenation", p);
                        Utils.LagSleep();
                        Utils.WaitWhileCasting();
                        //if (result) return RunStatus.Success;
                    }

                    if (p.HealthPercent < 80 && !p.HasAura("Regrowth") && Spell.CanCast("Regrowth"))
                    {
                        result = Spell.Cast("Regrowth", p);
                        Utils.LagSleep();
                        Utils.WaitWhileCasting();
                        //if (result) return RunStatus.Success;
                    }

                   
                    if (p.HealthPercent < 65 && p.HealthPercent > 50)
                    {
                        result = Spell.Cast("Healing Touch", p);
                        Utils.LagSleep();
                        Utils.WaitWhileCasting();
                        if (result) return RunStatus.Success;
                    }

                    if (p.HealthPercent < 50)
                    {
                        result = Spell.Cast("Regrowth", p);
                        Utils.LagSleep();
                        Utils.WaitWhileCasting();
                        if (result) return RunStatus.Success;
                    }

                }



                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

       
    }
}
