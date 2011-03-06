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

                // Lifeblood
                new NeedToLifeblood(new Lifeblood()),

                // Use a health potion if we need it
                new NeedToUseHealthPot(new UseHealthPot())

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

                // Eat and Drink
                new NeedToEatDrink(new EatDrink()),

                // Battle Shout
                new NeedToShout(new Shout())

                );
        }

        #endregion




        // ******************************************************************************************
        //
        // This is where the common priority selectors start
        // These can be used with ANY class, they are all non-class specific
        //


        #region Battle Shout / Commanding Shout
        public class NeedToShout: Decorator
        {
            public NeedToShout(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                Debug.Log("NeedToShout", 1);
                if (!Utils.IsCommonChecksOk(Settings.Shout, false)) return false;
                if (!Self.CanBuffMe(Settings.Shout)) return false;

                bool result = Spell.CanCast(Settings.Shout);
                return result;
            }
        }

        public class Shout : Action
        {
            protected override RunStatus Run(object context)
            {
                bool result = Spell.Cast(Settings.Shout, Me);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

       
    }
}
