using System;
using System.Collections.Generic;
using System.Linq;

using Styx.Database;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Inventory.Frames.Trainer;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class ForceTrainRiding : CustomForcedBehavior
    {
        /// <summary>
        /// ForceTrainRiding by Natfoth
        /// Allows you to Interact with Mobs that are Nearby.
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// NpcId: Id of the Mob to interact with.
        /// X,Y,Z: The general location where theese objects can be found
        /// </summary>
        public ForceTrainRiding(Dictionary<string, string> args)
            : base(args)
        {
			try
			{
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                Counter     = 0;
                Location    = GetXYZAttributeAsWoWPoint("", false, null) ?? WoWPoint.Empty;
                MobId       = GetAttributeAsMobId("NpcId", true, new [] { "NpcID" }) ?? 0;
                QuestId     = GetAttributeAsQuestId("QuestId", false, null) ?? 0;
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;
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


        public WoWPoint                 Location { get; private set; }
        public int                      MobId { get; private set; }
        public WoWPoint                 MovePoint { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }

        private bool                _isBehaviorDone;
        private Composite           _root;

        public int                  Counter { get; set; }
        private List<WoWUnit>       MobList
        {
            get
            {
                    return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                         .Where(u => u.Entry == MobId && !u.Dead)
                                         .OrderBy(u => u.Distance).ToList());
            }
        }
        private NpcResult           RidingTrainer
        {
            get
            {
                return (NpcQueries.GetNpcById((uint)MobId));
            }
        }


        #region Overrides of CustomForcedBehavior.

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                    new Decorator(ret => Counter >= 1,
                        new Action(ret => _isBehaviorDone = true)),

                        new PrioritySelector(

                            new Decorator(ret => Counter > 0,
                                new Sequence(

                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new Action(ret => _isBehaviorDone = true),
                                    new WaitContinue(1,
                                        new Action(delegate
                                        {
                                            _isBehaviorDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )
                                ),

                            new Decorator(ret => MobList.Count > 0 && !MobList[0].WithinInteractRange,
                                new Action(ret => Navigator.MoveTo(MobList[0].Location))),

                            new Decorator(ret => MobList.Count > 0 && MobList[0].WithinInteractRange,
                                new Sequence(
                                    new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                        new Action(ret =>
                                        {
                                            WoWMovement.MoveStop();
                                            StyxWoW.SleepForLagDuration();
                                        })),
                                    new Action(ret => TreeRoot.StatusText = "Opening Trainer - " + MobList[0].Name + " X: " + MobList[0].X + " Y: " + MobList[0].Y + " Z: " + MobList[0].Z),
                                    new Action(ret => MobList[0].Interact()),
                                    new WaitContinue(5, 
                                        ret => TrainerFrame.Instance.IsVisible,
                                        new Action(ret => TrainerFrame.Instance.BuyAll())),
                                    new Action(ret => TrainerFrame.Instance.Close()),
                                    new Action(ret => Counter++)
                                    )
                            ),

                            new Decorator(ret => RidingTrainer != null,
                                new Action(ret => Navigator.MoveTo(RidingTrainer.Location))
                                ),

                            new Action(ret => Counter++)
                    )));
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
                TreeRoot.GoalText = "Train Riding: In Progress";
            }
        }

        #endregion
    }
}

