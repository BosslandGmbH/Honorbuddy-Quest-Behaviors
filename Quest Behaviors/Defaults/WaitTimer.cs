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

        public WaitTimer(Dictionary<string, string> args)
            : base(args)
        {
            if (Args.Count == 1)
            {
                int waitTime;
                if (!int.TryParse(Args["WaitTime"], out waitTime))
                {
                    Logging.Write(Color.Red, "Parsing attribute 'WaitTime' in WaitTimer behavior failed! please check your profile!");
                    TreeRoot.Stop();
                }

                _timer = new Timer(new TimeSpan(0, 0, 0, 0, waitTime));
            }
            else
            {
                Logging.Write(Color.Red, "Invalid amount of Args!");
                TreeRoot.Stop();
            }
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
                        new Action(ret => TreeRoot.GoalText = "Waiting for wait timer to expire"),
                        new Action(ret => TreeRoot.StatusText = "Waiting for wait timer to expire, " + _timer.TimeLeft.Seconds + " seconds left"),
                        new Action(delegate { return RunStatus.Success; })) 
                        )                           
                       );
        }

        public override bool IsDone { get { return _timer.IsFinished; } }

        #endregion
    }
}

