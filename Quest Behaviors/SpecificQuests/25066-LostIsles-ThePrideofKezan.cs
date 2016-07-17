//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//
// Originally contributed by Unknown
// Updated by MaxMuster, 21/06/2016

#region Summary and Documentation
#endregion

#region Examples
#endregion

#region Usings

using System;
using System.Collections.Generic;
using System.Linq;

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
using WaitTimer = Styx.Common.Helpers.WaitTimer;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.ThePrideofKezan
{
    [CustomBehaviorFileName(@"SpecificQuests\25066-LostIsles-ThePrideofKezan")]
    public class ThePrideofKezan : CustomForcedBehavior
    {
        private WoWPoint _startAndEndPoint = new WoWPoint(1624.326, 2693.647, 89.21473);
        private WoWPoint _waitPoint = new WoWPoint(1782.963, 2884.958, 157.274);

        private bool _isBehaviorDone;

        private Composite _root;

        #region Cleanup

        public override void OnFinished()
        {
            TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_MainCombat());
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }

        #endregion

        public ThePrideofKezan(Dictionary<string, string> args) : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            try
            {
                QuestId = 25066; //GetAttributeAsQuestId("QuestId", true, null) ?? 0;
            }
            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                QBCLog.Exception(except);
                IsAttributeProblem = true;
            }
        }

        public int QuestId { get; set; }

        public override bool IsDone
        {
            get { return _isBehaviorDone; }
        }

        private LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
        }

        public List<WoWUnit> GnomereganStealthFighter
        {
            get
            {
                var myLoc = Me.Location;
                return (from u in ObjectManager.GetObjectsOfType<WoWUnit>()
                        where u.Entry == 39039 && !u.IsDead
                        let loc = u.Location
                        orderby loc.DistanceSqr(myLoc)
                        select u).ToList();
            }
        }

        public Composite CreateBehavior_CheckCompletion()
        {
            return new Decorator(r => Me.IsQuestComplete(QuestId),
                new PrioritySelector(
                    new Decorator(r => Me.Location.Distance(_startAndEndPoint) > 10,
                        new Action(r => WoWMovement.ClickToMove(_startAndEndPoint))
                    ),
                    new Decorator(r => Me.Location.Distance(_startAndEndPoint) <= 10,
                                         new Action(delegate
                        {
                            TreeRoot.StatusText = "Finished!";
                            Lua.DoString("VehicleExit()");
                            _isBehaviorDone = true;
                            return RunStatus.Success;
                        }))));
        }

        public WoWUnit SassyHardwrench
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>()
                    .FirstOrDefault(u => u.Entry == 38387 && u.Location.DistanceSqr(_startAndEndPoint) < 10 * 10);
            }
        }

        public WoWUnit PrideofKezan
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>()
                    .FirstOrDefault(u => u.Entry == 39074);
            }
        }

        public Composite CreateBehavior_KillGnomereganStealthFighter()
        {
            WoWUnit attackTarget = null;
            WoWUnit PrideofKezan = null;
            WaitTimer WildWeaselRocketsTimer = WaitTimer.FiveSeconds;

            return new Decorator(r => !Me.IsQuestComplete(QuestId) && Query.IsInVehicle() && (PrideofKezan = Me.CharmedUnit) != null,
                new PrioritySelector(ctx => attackTarget = GetAttackTarget(),
                    new Decorator(ctx => attackTarget != null,
                        new PrioritySelector(
                            new ActionSetActivity("Moving to Attack"),
                            new Decorator(ctx => Me.CurrentTargetGuid != attackTarget.Guid,
                                new ActionFail(ctx => attackTarget.Target())),
                            new Decorator(ctx => !Me.IsSafelyFacing(attackTarget) || !PrideofKezan.IsSafelyFacing(attackTarget),
                                new ActionFail(ctx => attackTarget.Face())),

                            // cast 'Wild Weasel Rockets' ability
                            new Decorator(
                                ctx => PrideofKezan.Location.DistanceSqr(attackTarget.Location) < 25 * 25 && WildWeaselRocketsTimer.IsFinished,
                                new Sequence(
                                    new Action(ctx => Lua.DoString("CastPetAction(2)")),
                                    new Action(ctx => WildWeaselRocketsTimer.Reset()))),

                            // cast 'Machine Gun' ability
                            new Decorator(
                                ctx => PrideofKezan.Location.DistanceSqr(attackTarget.Location) <= 25 * 25,
                                new Sequence(
                                    new Action(ctx => Lua.DoString("CastPetAction(1)")))),

                            new Decorator(ctx => PrideofKezan.Location.DistanceSqr(attackTarget.Location) > 25 * 25,
                                new Action(ctx => WoWMovement.ClickToMove(attackTarget.Location))))),
                    new Decorator(
                        ctx => attackTarget == null,
                        new PrioritySelector(
                            new Decorator(
                                ctx => PrideofKezan.Location.DistanceSqr(_waitPoint) > 10 * 10,
                                new Sequence(
                                    new Action(ctx => WoWMovement.ClickToMove(_waitPoint)))),
                            new ActionSetActivity("No viable targets, waiting."))),
                    new ActionAlwaysSucceed()));
        }

        public Composite CreateBehavior_GetIn()
        {
            return new Decorator(r => !Query.IsInVehicle(),
                new PrioritySelector(
                    new Decorator(r => SassyHardwrench == null || SassyHardwrench.Distance > 10, new Action(r => Navigator.MoveTo(_startAndEndPoint))),
                    new Decorator(r => SassyHardwrench != null,
                        new Sequence(
                            new Action(r => SassyHardwrench.Interact()),
                            new Sleep(1000),
                            new Action(ret => Lua.DoString("SelectGossipOption(1)")),
                            new Sleep(2000)))));
        }



        private WoWUnit GetAttackTarget()
        {
            var target = Me.CurrentTarget;
            if (target != null && target.IsHostile && target.Attackable && target.IsAlive)
            {
                return target;
            }

            return GnomereganStealthFighter.FirstOrDefault();
        }

        public override void OnStart()
        {
            OnStart_HandleAttributeProblem();
            if (!IsDone)
            {
                TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_MainCombat());
                this.UpdateGoalText(QuestId);
            }
        }


        protected Composite CreateBehavior_MainCombat()
        {
            return _root ?? (_root =
                new Decorator(ret => !_isBehaviorDone,
                    new PrioritySelector(
                        CreateBehavior_CheckCompletion(),
                        CreateBehavior_GetIn(),
                        CreateBehavior_KillGnomereganStealthFighter())));
        }
    }
}
