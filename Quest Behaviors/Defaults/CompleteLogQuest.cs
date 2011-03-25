using System;
using System.Collections.Generic;
using System.Threading;

using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.Logic.BehaviorTree;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class CompleteLogQuest : CustomForcedBehavior
    {
        /// <summary>
        /// CompleteLogQuest by Natfoth
        /// Will complete a "In-The-Field" Quest.
        /// ##Syntax##
        /// QuestId: The Entire purpose of this behavior, so this one is required.
        /// </summary>
        /// 
        public CompleteLogQuest(Dictionary<string, string> args)
            : base(args)
        {
            try
			{
                int     questId;

                CheckForUnrecognizedAttributes(new Dictionary<string, object>()
                                                {
                                                    { "QuestID",    null },
                                                });

                _isAttributesOkay = true;
                _isAttributesOkay &= GetAttributeAsInteger("QuestID", true, "0", 0, int.MaxValue, out questId);

                if (_isAttributesOkay)
                {
                    Counter = 0;
                    QuestID = (uint)questId;
                }
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
				_isAttributesOkay = false;
			}
        }


        public int          Counter { get; set; }
        public static uint  QuestID { get; set; }

        private bool        _isAttributesOkay;
        private bool        _isBehaviorDone;
        private Composite   _root;

        static public int questIndexID { get { return (Lua.GetReturnVal<int>("return  GetQuestLogIndexByID(" + QuestID + ")", 0)); } }


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
                                        new Action(ret => TreeRoot.StatusText = "Completing Log Quest - " + QuestID),
                                        new Action(ret => Lua.DoString("ShowQuestComplete({0})", questIndexID)),
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
            get { return (_isBehaviorDone); }
        }


        public override void OnStart()
        {
            if (!_isAttributesOkay)
			{
				UtilLogMessage("error", "Stopping Honorbuddy.  Please repair the profile!");

                // *Never* want to stop Honorbuddy (e.g., TreeRoot.Stop()) in the constructor --
                // This would defeat the "ProfileDebuggingMode" configurable that builds an instance of each
                // used behavior when the profile is loaded.
				TreeRoot.Stop();
			}

            else
            {
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestID);

                if (quest != null)
                    { TreeRoot.GoalText = "CompleteLogQuest - " + quest.Name; }

                else
                {
                    //UtilLogMessage("error", string.Format("QuestId {0} could not be located in log.\n"
                    //                                      + "Stopping Honorbudy.  Please repair the profile.",
                    //                                      QuestID));
                    //TreeRoot.Stop();
                    _isBehaviorDone = true;
                }
            }
        }

        #endregion
    }
}

