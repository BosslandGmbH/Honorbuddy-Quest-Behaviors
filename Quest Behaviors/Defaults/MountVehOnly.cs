// Behavior originally contributed by Natfoth.
//
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_MountVehOnly
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class MountVehOnly : CustomForcedBehavior
    {
        /// <summary>
        /// Only use this when you need to mount a Vehicle but it will require nothing else, wow has to auto dismount you at the end or you use EjectVeh.
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// MobMountId: The ID of the Vehicle you want to mount.
        /// X,Y,Z: The general location where these objects can be found
        /// </summary>
        ///
        public MountVehOnly(Dictionary<string, string> args)
            : base(args)
        {
			try
			{
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                Location    = GetXYZAttributeAsWoWPoint("", true, null) ?? WoWPoint.Empty;
                QuestId     = GetAttributeAsQuestId("QuestId", false, null) ?? 0;
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;
                VehicleMountId   = GetAttributeAsMobId("VehicleMountId", true, new [] { "MobMountId", "NpcMountId" }) ?? 0;
			}

			catch (Exception except)
			{
				// Maintenance problems occur for a number of reasons.  The primary two are...
				// * Changes were made to the behavior, and boundary conditions weren't properly tested.
				// * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
				// In any case, we pinpoint the source of the problem area here, and hopefully it
				// can be quickly resolved.
				UtilLogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
										+ "\nFROM HERE:\n"
										+ except.StackTrace + "\n");
				IsAttributeProblem = true;
			}
        }


        // Attributes provided by caller
        public WoWPoint                 Location { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }
        public int                      VehicleMountId { get; private set; }

        // Private variables for internal state
        private bool                _isBehaviorDone;
        private Composite           _root;

        // Private properties
        public int                  Counter { get; set; }
        private bool                InVehicle { get { return Lua.GetReturnVal<bool>("return  UnitUsingVehicle(\"player\")", 0); } }
        private LocalPlayer         Me { get { return (ObjectManager.Me); } }
        private List<WoWUnit>       VehicleList {  get { return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                                                                 .Where(u => u.Entry == VehicleMountId && !u.Dead)
                                                                                 .OrderBy(u => u.Distance).ToList());
                                             } }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                            new Decorator(ret => Counter > 0,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new WaitContinue(120,
                                        new Action(delegate
                                        {
                                            _isBehaviorDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )),

                           new Decorator(ret => InVehicle,
                                new Sequence(
                                        new Action(ret => Counter++)
                                    )
                                ),

                           new Decorator(ret => VehicleList.Count == 0,
                                new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Moving To Location - X: " + Location.X + " Y: " + Location.Y),
                                        new Action(ret => Navigator.MoveTo(Location)),
                                        new Action(ret => Thread.Sleep(300))
                                    )
                                ),

                           new Decorator(ret => VehicleList.Count > 0,
                                new Sequence(
                                    new DecoratorContinue(ret => VehicleList[0].WithinInteractRange,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Mounting Vehicle - " + VehicleList[0].Name),
                                            new Action(ret => WoWMovement.MoveStop()),
                                            new Action(ret => VehicleList[0].Interact())
                                            )
                                    ),
                                    new DecoratorContinue(ret => !VehicleList[0].WithinInteractRange,
                                        new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Moving To Vehicle - " + VehicleList[0].Name + " X: " + VehicleList[0].X + " Y: " + VehicleList[0].Y + " Z: " + VehicleList[0].Z + " Yards Away: " + VehicleList[0].Location.Distance(Me.Location)),
                                        new Action(ret => Navigator.MoveTo(VehicleList[0].Location)),
                                        new Action(ret => Thread.Sleep(300))
                                            ))
                                    ))
                    ));
        }


        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone     // normal completion
                        || !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
            }
        }


        public override void OnStart()
        {
            // This reports problems, and stops BT processing if there was a problem with attributes...
            // We had to defer this action, as the 'profile line number' is not available during the element's
            // constructor call.
            OnStart_HandleAttributeProblem();

            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (!IsDone)
            {
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = this.GetType().Name + ": " + ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
            }
        }

        #endregion
    }
}
