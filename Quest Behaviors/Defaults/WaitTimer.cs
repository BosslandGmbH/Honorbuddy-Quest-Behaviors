using System;
using System.Collections.Generic;
using System.Drawing;

using Styx.Logic.BehaviorTree;
using Styx.Helpers;
using Styx.Logic.Questing;

using TreeSharp;
using Action = TreeSharp.Action;
using Timer = Styx.Helpers.WaitTimer;


namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// WaitTimer by Nesox
    /// Simple behavior that forces hb to wait until the timer runs out.
    /// ##Syntax##
    /// WaitTime: time in milliseconds to wait. eg; 15000 for 15 seconds.
    /// Important: this does not have a QuestId attribute and therefor you have to put this inside the body of an If tag!!!
    /// </summary>
    public class WaitTimer : CustomForcedBehavior
    {
        #region Overrides of CustomForcedBehavior

        private readonly Timer _timer;

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {
            {"GoalText",null},
            {"WaitTime",null},
            {"QuestId",null},
        };

        bool success = true;
        private string      _goalText;


        public WaitTimer(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);

            int waitTime = 0;

            success = success && GetAttributeAsString("GoalText", false, "", out _goalText);
            success = success && GetAttributeAsInteger("WaitTime", true, "1000", 0, int.MaxValue, out waitTime);

            _timer = new Timer(new TimeSpan(0, 0, 0, 0, waitTime));
        }
        

        public override void OnStart()
        {
            _timer.Reset();
        }

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new Decorator(ret => !_timer.IsFinished,
                    new Sequence(
                        new Action(ret => TreeRoot.GoalText = (!string.IsNullOrEmpty(_goalText)
                                                               ? UtilSubstituteInMessage(_goalText)
                                                               : "Waiting for timer expiration")),
                        new Action(ret => TreeRoot.StatusText = "Wait time remaining... " + UtilBuildTimeAsString(_timer.TimeLeft) + "."),
                        new Action(delegate { return RunStatus.Success; }))
                        )
                       );
        }

        public override bool IsDone { get { return _timer.IsFinished; } }


        private string   UtilSubstituteInMessage(string   message)
        {
            message = message.Replace("{TimeRemaining}", UtilBuildTimeAsString(_timer.TimeLeft));
            message = message.Replace("{TimeDuration}", UtilBuildTimeAsString(_timer.WaitTime));

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

            return (string.Format(formatString,
                                  timeSpan.Hours,
                                  timeSpan.Minutes,
                                  timeSpan.Seconds));
        }
        #endregion
    }
}

