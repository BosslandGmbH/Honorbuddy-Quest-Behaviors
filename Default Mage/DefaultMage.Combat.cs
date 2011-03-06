using System;
using System.Collections.Generic;
using System.Threading;
using CommonBehaviors.Actions;
using Styx;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Sequence = TreeSharp.Sequence;
using System.Linq;
using Action = TreeSharp.Action;

namespace DefaultMage
{
    public partial class DefaultMage
    {
        private Composite _combatBehavior;
        public override Composite CombatBehavior
        {
            get
            {
                if (_combatBehavior == null)
                {
                    Log("Creating 'Combat' behavior");
                    _combatBehavior = CreateCombatBehavior();
                }

                return _combatBehavior;
            }
        }

        /// <summary>s
        /// Creates the behavior used for combat. Castsequences, add management, crowd control etc.
        /// </summary>
        /// <returns></returns>
        private Composite CreateCombatBehavior()
        {
            return new PrioritySelector(

                    CreateCombat()
                );
        }


        private Composite CreateCombat()
        {

            return new PrioritySelector(

                new Decorator(ret => RaFHelper.Leader != null,
                // Use leaders target
                              new Decorator(ret =>

                                            (RaFHelper.Leader.GotTarget && Me.GotTarget &&
                                             Me.CurrentTarget.Guid != RaFHelper.Leader.CurrentTargetGuid &&
                                             !RaFHelper.Leader.CurrentTarget.Dead &&
                                             RaFHelper.Leader.CurrentTarget.Attackable &&
                                             !RaFHelper.Leader.CurrentTarget.IsFriendly) ||

                                            (!Me.GotTarget &&
                                             RaFHelper.Leader.GotTarget &&
                                             !RaFHelper.Leader.CurrentTarget.Dead &&
                                             RaFHelper.Leader.CurrentTarget.Attackable &&
                                             !RaFHelper.Leader.CurrentTarget.IsFriendly),

                                            new Sequence(
                                                new Action(delegate
                                                {
                                                    RaFHelper.Leader.CurrentTarget.Target();
                                                }),

                                                new Wait(3, ret => Me.GotTarget,
                                                    new ActionIdle())
                                                ))),

              /*  // Make sure we got a proper target
                new Decorator(ret => (Me.GotTarget && !GotSheep && Targeting.Instance.FirstUnit != null && Me.CurrentTarget != Targeting.Instance.FirstUnit) || !Me.GotTarget,
                    new Sequence(

                        // Set Target!
                        new Action(ret => Targeting.Instance.FirstUnit.Target()),

                        // Wait until we got a target
                        new Wait(3, ret => Me.GotTarget,
                            new ActionIdle()))
                    ),
                */

                //If we have an active pet, make it attack the same target we are.
                new Decorator( ret => Me.GotTarget && Me.GotAlivePet && (!Me.Pet.GotTarget || Me.Pet.CurrentTarget != Me.CurrentTarget),
                    new Action(ctx => Lua.DoString("PetAttack()"))),

                // Face thege tart if we aren't
                new Decorator(ret => Me.GotTarget && !Me.IsFacing(Me.CurrentTarget),
                              new Action(ret => Me.CurrentTarget.Face())),

                new Decorator(ret => Me.IsCasting || Me.Silenced,
                    new ActionIdle()),

                // Move closer to the target if we are too far away or in !Los
                new Decorator(ret => Me.GotTarget && (Me.CurrentTarget.Distance > PullDistance + 3 || !Me.CurrentTarget.InLineOfSight),
                    new NavigationAction(ret => Me.CurrentTarget.Location)),

                // At this point we shouldn't be moving. Atleast not with this 'simple' kind of logic
                new Decorator(ret => Me.IsMoving,
                              new Action(ret => WoWMovement.MoveStop())),
                //used to pickup an add if by chance we dont have a target.
                new Decorator(ret => Me.CurrentTarget == null && getAdds().Count > 0,
                              new Action(ret => getAdds()[0].Target())),

                //Retarget Polymorphed add if we dont have a current target.
                new Decorator(ret => !IsInPartyOrRaid() && GotSheep && Me.CurrentTarget == null,
                              new Action(ctx => retargetSheep())),
            //Switch Based on Level.
            new Switch<bool>(r => Me.Level < 10, 
                                   new SwitchArgument<bool>(Low_rotation(), true),
                                    new SwitchArgument<bool>(Frost_rotation(), false))
                                    );

        }
       
       




       
    }
}
