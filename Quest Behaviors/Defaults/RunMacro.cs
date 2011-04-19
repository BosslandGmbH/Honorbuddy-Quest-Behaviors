// This work is part of the Buddy Wiki.  You may find it here:
//    http://www.thebuddyforum.com/mediawiki/index.php?title=Category:Honorbuddy_CustomBehavior
//
// This work is licensed under the 
//    Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//    http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//    Creative Commons
//    171 Second Street, Suite 300
//    San Francisco, California, 94105, USA. 
//
// Release History:
//  Version 1.3 -- Built-in error handlers (15-Feb-2011, chinajade)
//                   Converted to the new buit-in error handlers provided by
//                   CustomForcedBehavior.  Eliminated the CustomBehaviorUtils
//                   class as a consequence. Yay!
//                   Now accepts an optional QuestId.
//  Version 1.2 -- More error handling (22-Jan-2011, chinajade)
//					 Standardized with other Wiki-provided behaviors.  Includes:
//                   The profile writer will now be told specifically what is at
//                   fault in a failed call to this behavior.  Stray attributes
//					 will also be flagged as these are usually spelling or case-sensitive
//					 errors.
//					 NumOfTimes is now optional, and defaults to '1'.
//  Version 1.1 -- Error handling improvements (17-Jan-2011, chinajade)
//				     Sharpened the error handling for malformed input from Profiles.
//                   Also, fixed a faulty initialization of WaitTime--it was wrongly
//                   set to zero, but the design default was 1500 (milliseconds).
//  Version 1.0 -- Initial release to BuddyWiki (12-Jan-2011, AxaZol)
//

using System;
using System.Collections.Generic;
using System.Threading;

using Styx.Logic.BehaviorTree;
using Styx.Logic.Questing;
using Styx.WoWInternals;


namespace BuddyWiki.CustomBehavior.RunMacro
{
    public class RunMacro : CustomForcedBehavior
    {
        public RunMacro(Dictionary<string,string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                GoalText    = GetAttributeAsString_NonEmpty("GoalText", false, null) ?? "";
                Macro       = GetAttributeAsString_NonEmpty("Macro", true, null) ?? "";
                NumOfTimes  = GetAttributeAsInteger("NumOfTimes", false, 1, 1000, null) ?? 1;
                QuestId     = GetAttributeAsQuestId("QuestId", false, null) ?? 0;
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;
                WaitTime    = GetAttributeAsInteger("WaitTime", false, 1, int.MaxValue, null) ?? 1500;
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
        public string                   GoalText { get; private set; }
		public string		            Macro { get; private set; }
		public int			            NumOfTimes { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }
		public int			            WaitTime { get; private set; }

        // Private variables for internal state
		private bool		_isBehaviorDone;


        #region Overrides of CustomForcedBehavior

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
				for (int counter = 1;   counter <= NumOfTimes;   ++counter)
				{
                    if (!string.IsNullOrEmpty(GoalText))
                    {
                        TreeRoot.GoalText = GoalText;
                        UtilLogMessage("info", GoalText);
                    }

                    TreeRoot.StatusText = string.Format("RunMacro {0}/{1} Times", counter, NumOfTimes);

					Lua.DoString(string.Format("RunMacroText(\"{0}\")", Macro), 0);
					Thread.Sleep(WaitTime);
				}

                _isBehaviorDone = true;
            }
        }

        #endregion
    }
}  