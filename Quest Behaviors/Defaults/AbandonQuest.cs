﻿// Behavior originally contributed by Bobby53.
//
// WIKI DOCUMENTATION:
//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_AbandonQuest
//
// QUICK DOX:
//      Allows you to abandon a quest in your quest log.
//
//  Parameters (required, then optional--both listed alphabetically):
//      QuestId: The id of the quest.
//
//      Type[Default:Incomplete]: The state in which the quest must reside for the abandon
//              to succeed.  The allowed values for this attribute are:
//                  All:        abandon quest if its in log regardless of status
//                  Failed:     abandon quest only if failed
//                  Incomplete: abandon incomplete quests (failed and any not complete)  
//
//  Examples:   
//     <CustomBehavior File="AbandonQuest" QuestId="25499" />
//     <CustomBehavior File="AbandonQuest" QuestId="25499" Type="All" />
//     <CustomBehavior File="AbandonQuest" QuestId="25499" Type="Failed" />
//     <CustomBehavior File="AbandonQuest" QuestId="25499" Type="Incomplete" />
//
using System;
using System.Collections.Generic;

using Styx.Logic.BehaviorTree;
using Styx.Logic.Questing;


namespace Styx.Bot.Quest_Behaviors
{
    public class AbandonQuest : CustomForcedBehavior
    {
        public enum AbandonType
        {
            All,
            Failed,
            Incomplete
        };


        public AbandonQuest(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                QuestId = GetAttributeAsQuestId("QuestId", true, null) ?? 0;
                Type    = GetAttributeAsEnum<AbandonType>("Type", false, null) ?? AbandonType.Incomplete;
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
        public int                      QuestId { get; private set; }
        public AbandonType              Type { get; private set; }

        // Private variables for internal state
        private bool        _isBehaviorDone;

        
        #region Overrides of CustomForcedBehavior

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

                if (quest == null)
                    { UtilLogMessage("fatal", "Cannot find quest with QuestId({0}).", QuestId); }

                else if ((quest != null)  &&  quest.IsCompleted  &&  (Type != AbandonType.All))
                    { UtilLogMessage("warning", "Quest({0}, \"{1}\") is Complete--skipping abandon.", QuestId, quest.Name); }

                else if ((quest != null)  &&  !quest.IsFailed  &&  (Type == AbandonType.Failed))
                    { UtilLogMessage("warning", "Quest({0}, \"{1}\") has not Failed--skipping abandon.", QuestId, quest.Name); }

                else
                {
                    TreeRoot.GoalText = string.Format("Abandoning QuestId({0}): \"{1}\"", QuestId, quest.Name);
                    QuestLog ql = new QuestLog();
                    ql.AbandonQuestById((uint)QuestId);
                    UtilLogMessage("info", "Quest({0}, \"{1}\") successfully abandoned", QuestId, quest.Name);
                }

                _isBehaviorDone = true;
            }
        }

        #endregion
    }
}