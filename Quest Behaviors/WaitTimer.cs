// Behavior originally contributed by Natfoth.
//
// WaitTimer by Nesox
// Simple behavior that forces hb to wait until the timer runs out.
// ##Syntax##
// QuestId[Optional]:
// QuestCompleteRequirement[Optional, Default:NotComplete]:
// QuestInLogRequirement[Optional, Default:InLog]:
// WaitTime: time (in milliseconds) to wait. eg; 15000 for 15 seconds.
// VariantTime[Optional]: a random amount of time between [0..VariantTime] will be selected and added to the WaitTime
//
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_WaitTimer
//
using System;
using System.Collections.Generic;

using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;

using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.WaitTimerBehavior // This prevents a conflict with the new Styx.Common.Helpers.WaitTimer
{
    [CustomBehaviorFileName(@"WaitTimer")]
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
                StatusText = GetAttributeAs<string>("GoalText", false, ConstrainAs.StringNonEmpty, null) ?? "Wait time remaining... {TimeRemaining} of {TimeDuration}.";
                QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
                WaitTime = GetAttributeAsNullable<int>("WaitTime", true, ConstrainAs.Milliseconds, null) ?? 1000;
                VariantTime = GetAttributeAsNullable<int>("VariantTime", false, ConstrainAs.Milliseconds, null) ?? 0;
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
                                    + "\nFROM HERE:\n"
                                    + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }

        // Attributes provided by caller
        public string StatusText { get; private set; }
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }
        public int WaitTime { get; private set; }
        public int VariantTime { get; private set; }

        // Private variables for internal state
        private bool _isDisposed;
        private Composite _root;
        private Styx.Common.Helpers.WaitTimer _timer;
        private string _waitTimeAsString;

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id: WaitTimer.cs 555 2013-06-12 09:00:14Z chinajade $"); } }
        public override string SubversionRevision { get { return ("$Revision: 555 $"); } }


        ~WaitTimer()
        {
            Dispose(false);
        }


        public void Dispose(bool isExplicitlyInitiatedDispose)
        {
            if (!_isDisposed)
            {
                // NOTE: we should call any Dispose() method for any managed or unmanaged
                // resource, if that resource provides a Dispose() method.

                // Clean up managed resources, if explicit disposal...
                if (isExplicitlyInitiatedDispose)
                {
                    // empty, for now
                }

                // Clean up unmanaged resources (if any) here...
                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }


        private string UtilSubstituteInMessage(string message)
        {
            message = message.Replace("{TimeRemaining}", Utility.PrettyTime(_timer.TimeLeft));
            message = message.Replace("{TimeDuration}", _waitTimeAsString);

            return (message);
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new Decorator(ret => !_timer.IsFinished,
                    new CompositeThrottle(TimeSpan.FromMilliseconds(500),
                        new Action(context =>
                        {
                            TreeRoot.StatusText =
                                !string.IsNullOrEmpty(StatusText)
                                ? UtilSubstituteInMessage(StatusText)
                                : string.Format("Wait time remaining... {0} of {1}.",
                                    Utility.PrettyTime(_timer.TimeLeft), _waitTimeAsString);
                        })))
            );
        }


        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
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
                int waitDuration = WaitTime + (new Random(Environment.TickCount + WaitTime + VariantTime)).Next(VariantTime);

                _timer = new Styx.Common.Helpers.WaitTimer(new TimeSpan(0, 0, 0, 0, waitDuration));
                _waitTimeAsString = Utility.PrettyTime(_timer.WaitTime);

                _timer.Reset();

                TreeRoot.GoalText = "Waiting for " + _waitTimeAsString;
            }
        }

        #endregion
    }
}
