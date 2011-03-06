using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Styx.Database;
using Styx.Helpers;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Styx.Logic.Combat;
using Styx.Logic.BehaviorTree;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    public class EjectVeh : CustomForcedBehavior
    {

        /// <summary>
        /// EjectVeh by Natfoth
        /// Will Eject from the current vehicle, nothing more and nothing less.
        /// ##Syntax##
        /// Eject: Not required but just incase it messes with the args.
        /// </summary>
        /// 

        

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"Eject",null},
            {"QuestId",null},

        };

        bool success = true;

        public EjectVeh(Dictionary<string, string> args)
            : base(args)
        {

            CheckForUnrecognizedAttributes(recognizedAttributes);

            int itemId = 0;
            int questId = 0;

            success = success && GetAttributeAsInteger("Eject", false, "1", 0, int.MaxValue, out itemId);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);

            Counter = 0;
        }

        public WoWPoint MovePoint { get; private set; }
        public int Counter { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        #region Overrides of CustomForcedBehavior

        public override void OnStart()
        {
            TreeRoot.GoalText = "EjectVeh";
        }

        private Composite _root;
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
                                            _isDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )),

                           new Decorator(ret => Counter == 0,
                                new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Ejecting Vehicle"),
                                        new Action(ret => Lua.DoString("VehicleExit()")),
                                        new Action(ret => Thread.Sleep(300)),
                                        new Action(ret => Counter++)
                                    ))
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

