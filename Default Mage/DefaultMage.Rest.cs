using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Styx;
using Styx.Helpers;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;

namespace DefaultMage
{
    public class ActionIdle : Action
    {
        protected override RunStatus Run(object context)
        {
            if (Parent is Selector || Parent is Decorator)
                return RunStatus.Success;

            return RunStatus.Failure;
        }
    }

    public partial class DefaultMage
    {
        private Composite _restBehavior;

        public override Composite RestBehavior
        {
            get
            {
                if (_restBehavior == null)
                {
                    Log("Creating 'Rest' behavior");
                    _restBehavior = CreateRestBehavior();
                }

                return _restBehavior;
            }
        }

        /// <summary>
        /// Creates the behavior used for resting. Eating/Drinking
        /// </summary>
        /// <returns></returns>
        private Composite CreateRestBehavior()
        {
            return new PrioritySelector(

                // If our health and mana is 100 we make it return failure to make sure 'NeedRest' returns false and allow it to proceed executing other code!
                // also cacel any existing food/drink buff.
                new Decorator(ret => Me.HealthPercent == 100 && Me.ManaPercent == 100,
                              new Action(
                                  delegate
                                      {
                                          if (Me.HasAura("Food") || Me.HasAura("Drink"))
                                              Lua.DoString(
                                                  "CancelUnitBuff('player', 'Food') CancelUnitBuff('player', 'Drink')");

                                          return RunStatus.Failure;
                                      })),

                new Decorator(ctx => Me.IsCasting || Me.HasAura("Food") || Me.HasAura("Drink"),
                              new ActionIdle()),

                // Don't rest if we are mounted or swimming
                new Decorator(ctx => !Me.Mounted && !Me.IsSwimming,
                              // Decide what and if to do          
                              new PrioritySelector(
                                  // Check if we need to  take a drink or bite
                                  new Decorator(
                                      ret =>
                                      Me.ManaPercent <= RestManaPercentage ||
                                      Me.HealthPercent <= RestHealthPercentage,
                                      new Sequence(
                                          // Clear our target
                                          new Action(ctx => Me.ClearTarget()),
                                          // Stop moving if we are moving
                                          new Action(
                                              delegate
                                                  {
                                                      WoWMovement.MoveStop();
                                                      return Me.IsMoving ? RunStatus.Running : RunStatus.Success;
                                                  }),

                                          new Action(ctx => MageRest(null)))
                                      ),

                                  new Decorator(ctx => !StyxWoW.Me.NormalBagsFull,
                                                new PrioritySelector(
                                                    // Do we need to make Refreshments?
                                                    new Decorator(
                                                        ctx =>
                                                        SpellManager.HasSpell("Conjure Refreshment") &&
                                                        !Gotfood(),
                                                        new Action(ctx => ConjureRefreshment())),

                                                    // Do we need to make a Gem?, don't create any if we got refreshments!
                                                    new Decorator(
                                                        ctx =>
                                                        !HaveManaGem() && SpellManager.CanCast("Conjure Mana Gem"),
                                                        new Action(ctx => ConjureManaGem()))
                                                    )),
                                  // Return 'Success' if we have the food or drink buff as then we should already be drinking
                                  new Decorator(ret => Me.HasAura("Food") || Me.HasAura("Drink"),
                                                new ActionIdle())
                                  )));
        }

        private static RunStatus MageRest(object context)
        {
            if (LegacySpellManager.GlobalCooldown)
                return RunStatus.Running;

            if (SpellManager.HasSpell("Conjure Refreshment"))
            {
                if (Gotfood())
                {
                    //Set Food and Drink names then try and drink.
                    Styx.Helpers.LevelbotSettings.Instance.FoodName = GotRefreshments.ToString();
                    Styx.Helpers.LevelbotSettings.Instance.DrinkName = GotRefreshments.ToString();
                    Styx.Logic.Common.Rest.Feed();
                }
            }
            else
            {
                //Low Level Food / Water, if you can conjure, then it should use conjured food instead of whats in your bag.
                if (Me.ManaPercent <= RestManaPercentage && !Me.HasAura("Drink"))
                {
                 Styx.Logic.Common.Rest.Feed();
                }

                if (Me.HealthPercent <= RestHealthPercentage && !Me.HasAura("Food"))
                {
                  Styx.Logic.Common.Rest.Feed();
                }
            }

            return RunStatus.Success;
        }

        #region Food & Water / Rest

        private void ConjureManaGem()
        {
            Log("Make Mana Gem.");
            SpellManager.Cast(759);
        }

        private static void ConjureRefreshment()
        {
            Log("Conjuring Refreshment");
            SpellManager.Cast("Conjure Refreshment");
        }
        //Version 1.0.1 Added Conjured Mana Lolipop to the list, 65517;
        private static List<int> ConjureRefreshmentItems = new List<int>
    {
        65500, 65515, 65516, 65517, 43518, 43523, 65499
    };
        private static int GotRefreshments
        {
            get
            {
                foreach (int items in ConjureRefreshmentItems)
                {
                    foreach (WoWItem Thing in ObjectManager.GetObjectsOfType<WoWItem>(false))
                    {
                        if (Thing.Entry == items)
                        {
                            return items;
                        }

                    }
                }
                return 0;
            }
        }
        private static bool Gotfood()
        {
            if (GotRefreshments != 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
 
        #endregion
    }
}
