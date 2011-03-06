using System.Collections.Generic;
using Styx.Helpers;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    public class EjectVeh : CustomForcedBehavior
    {
        #region Overrides of CustomForcedBehavior

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"Eject",null},

        };

        bool success = true;

        public EjectVeh(Dictionary<string, string> args)
            : base(args)
        {

            CheckForUnrecognizedAttributes(recognizedAttributes);

            int itemId = 0;

            success = success && GetAttributeAsInteger("Eject", true, "1", 0, int.MaxValue, out itemId);

            Counter = 0;
        }

        public WoWPoint MovePoint { get; private set; }
        public int Counter { get; set; }

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
                                    Lua.DoString("VehicleExit()");
                                    Counter++;
                                    return RunStatus.Success;
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

