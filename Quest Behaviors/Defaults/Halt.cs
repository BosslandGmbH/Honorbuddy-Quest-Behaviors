// Behavior originally contributed by Bobby53.
//
// DOCUMENTATION:
//     
//
using System;
using System.Collections.Generic;
using System.Drawing;

using Styx.Logic.BehaviorTree;
using Styx.Logic.Questing;

using TreeSharp;


namespace Styx.Bot.Quest_Behaviors
{
    public class Halt : CustomForcedBehavior
    {
        /// <summary>
        /// Stops the Quest Bot.  Will write 'Msg' to the log and Goal Text.
        /// Also write the line number it halted at for easily locating in profile.
        /// 
        /// Useful for testing assumptions in quest profile and during profile
        /// development to force profile to automatically stop at designated point
        /// 
        /// ##Syntax##
        /// [optional] QuestId: Id of the quest (default is 0)
        /// [optional] Msg: text value to display (default says stopped by profile)
        /// [optional] Color: color to use for message in log (default is red)
        /// 
        /// Note:  QuestId behaves the same as on every other behavior.  If 0, then
        /// halt always occurs.  Otherwise, for non-zero QuestId only halts if the
        /// character has the quest and its not completed
        /// </summary>
        public Halt(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                Color   = GetAttributeAsColor("Color", false, null) ?? Color.Red;
                Message = GetAttributeAsString_NonEmpty("Message", false, new [] { "Msg", "Text" }) ?? "Quest Profile HALT";
                QuestId = GetAttributeAsQuestId("QuestId", false, null) ?? 0;
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


        // Attributes provided by caller
        public Color                    Color { get; private set; }
        public string                   Message { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return (null);
        }


        public override bool IsDone
        {
            get
            {
                return (!UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
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
                UtilLogMessage("", Color, "\n\n    " + Message + "\n");

                TreeRoot.GoalText = Message;
                TreeRoot.Stop();
            }
        }

        #endregion
    }
}

