using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Styx.Database;
using Styx.Logic.Combat;
using Styx.Helpers;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Pathing;
using Styx.Logic.Profiles.Quest.Order;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    public class WaitTimer : CustomForcedBehavior
    {
        #region Overrides of CustomForcedBehavior

        public WaitTimer(Dictionary<string, string> args)
            : base(args)
        {
                int waitTime;
                if (!int.TryParse(Args["WaitTime"], out waitTime))
                    Logging.Write("Parsing WaitTime in WaitTimer behavior failed! please check your profile!");
                HowLongToWait = waitTime;


            Counter = 0;


        }

        public WoWPoint MovePoint { get; private set; }
        public int Counter { get; set; }
        public int HowLongToWait { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                    new Decorator(ret => Counter >= 1,
                        new Action(ret => _isDone = true)),

                        new PrioritySelector(

                            new Decorator(ret => Counter == 0,
                                new Action(delegate
                                {
                                    Thread.Sleep(HowLongToWait);
                                    Counter++;
                                })
                                ),

                            new Action(ret => Logging.Write(""))
                        )
                    ));
        }

        private bool _isDone;
        public override bool IsDone
        {
            get { return _isDone; }
        }

        #endregion
    }
}

