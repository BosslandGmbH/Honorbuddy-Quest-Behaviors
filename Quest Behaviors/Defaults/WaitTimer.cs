// Behavior originally contributed by Natfoth.
//
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_WaitTimer
//
using System;
using System.Collections.Generic;

using Styx.Logic.BehaviorTree;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;
using Timer = Styx.Helpers.WaitTimer;


namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// WaitTimer by Nesox
    /// Simple behavior that forces hb to wait until the timer runs out.
    /// ##Syntax##
    /// QuestId[Optional]:
    /// QuestCompleteRequirement[Optional, Default:NotComplete]:
    /// QuestInLogRequirement[Optional, Default:InLog]:
    /// WaitTime: time (in milliseconds) to wait. eg; 15000 for 15 seconds.
    /// VariantTime[Optional]: a random amount of time between [0..VariantTime] will be selected and added to the WaitTime
    /// </summary>
    public class WaitTimer : CustomForcedBehavior
    {
        public WaitTimer(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                GoalText    = GetAttributeAsString_NonEmpty("GoalText", false, null) ?? "Waiting for {TimeRemaining}  of  {TimeDuration}";
                QuestId     = GetAttributeAsQuestId("QuestId", false, null) ?? 0; 
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;
                WaitTime    = GetAttributeAsWaitTime("WaitTime", true, null) ?? 1000;
                VariantTime = GetAttributeAsWaitTime("VariantTime", false, null) ?? 0;
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
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }
        public int                      WaitTime { get; private set; }
        public int                      VariantTime { get; private set; }
       
        // Private variables for internal state
        private Composite       _root;
        private Timer           _timer;
        private string          _waitTimeAsString;
        

        private string   UtilSubstituteInMessage(string   message)
        {
            message = message.Replace("{TimeRemaining}", UtilBuildTimeAsString(_timer.TimeLeft));
            message = message.Replace("{TimeDuration}", _waitTimeAsString);

            return (message);
        }


        private static string   UtilBuildTimeAsString(TimeSpan timeSpan)
        {
            string      formatString    =  "";

            if (timeSpan.Hours > 0)
                { formatString = "{0:D2}h:{1:D2}m:{2:D2}s"; }
            else if (timeSpan.Minutes > 0)
                { formatString = "{1:D2}m:{2:D2}s"; }
            else
                { formatString = "{2:D2}s"; }

            return (string.Format(formatString, timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds));
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new Decorator(ret => !_timer.IsFinished,
                    new Sequence(
                        new Action(ret => TreeRoot.GoalText = (!string.IsNullOrEmpty(GoalText)
                                                               ? UtilSubstituteInMessage(GoalText)
                                                               : "Waiting for timer expiration")),
                        new Action(ret => TreeRoot.StatusText = "Wait time remaining... "
                                         + UtilBuildTimeAsString(_timer.TimeLeft)
                                         + "... of "
                                         + _waitTimeAsString),
                        new Action(delegate { return RunStatus.Success; }))
                        )
                       );
        }


        public override bool IsDone
        {
            get
            {
                return (((_timer != null) && _timer.IsFinished)     // normal completion
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
                int     waitDuration = WaitTime + (new Random(Environment.TickCount + WaitTime + VariantTime)).Next(VariantTime);

                _timer = new Timer(new TimeSpan(0, 0, 0, 0, waitDuration));
                _waitTimeAsString = UtilBuildTimeAsString(_timer.WaitTime);

                _timer.Reset();
            }
        }

        #endregion
    }
}
