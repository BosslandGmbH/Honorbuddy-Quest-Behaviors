// Behavior originally contributed by Natfoth.
//
// WIKI DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_CompleteLogQuest
//
// QUICK DOX:
//      Allows you to 'turn in' a quest to your quest log.
//
//  Parameters (required, then optional--both listed alphabetically):
//      QuestId: (required)
//
using System;
using System.Collections.Generic;
using System.Threading;

using Styx.Logic.BehaviorTree;
using Styx.Logic.Questing;
using Styx.WoWInternals;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class CompleteLogQuest : CustomForcedBehavior
    {
        public CompleteLogQuest(Dictionary<string, string> args)
            : base(args)
        {
            try
			{
                QuestId     = GetAttributeAsQuestId("QuestId", true, new [] { "QuestID" }) ?? 0;
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
        public static int               QuestId { get; private set; }

        // Private variables for internal state
        private bool            _isBehaviorDone;
        private Composite       _root;

        // Private properties
        private int             Counter { get; set; }
        private int             QuestIndexId { get { return (Lua.GetReturnVal<int>("return  GetQuestLogIndexByID(" + QuestId + ")", 0)); } }


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

                           new Decorator(ret => Counter == 0,
                                new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Completing Log Quest - " + QuestId),
                                        new Action(ret => Lua.DoString("ShowQuestComplete({0})", QuestIndexId)),
                                        new Action(ret => Thread.Sleep(300)),
                                        new Action(ret => Lua.DoString("CompleteQuest()")),
                                        new Action(ret => Thread.Sleep(300)),
                                        new Action(ret => Lua.DoString("GetQuestReward({0})", 1)),
                                        new Action(ret => Thread.Sleep(300)),
                                        new Action(ret => Lua.DoString("AcceptQuest()")),
                                        new Action(ret => Counter++)
                                    ))
                               
                        )
                    );
        }


        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone);
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

                if (quest != null)
                {
                    if (!quest.IsCompleted)
                    {
                        UtilLogMessage("fatal", "Quest({0}, \"{1}\") is not complete.", QuestId, quest.Name);
                        _isBehaviorDone = true;
                    }
                }

                else
                {
                    UtilLogMessage("warning", "Quest({0}) is not in our log--skipping turn in.", QuestId);
                    _isBehaviorDone = true;
                }
            }
        }

        #endregion
    }
}

