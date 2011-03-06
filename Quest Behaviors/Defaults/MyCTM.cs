using System.Collections.Generic;
using System.Threading;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    public class MyCTM : CustomForcedBehavior
    {
        #region Overrides of CustomForcedBehavior

        readonly Dictionary<string, object> _recognizedAttributes = new Dictionary<string, object>()
        {
            {"X",null},
            {"Y",null},
            {"Z",null},
            {"QuestId",null},
        };

        readonly bool _success = true;

        public MyCTM(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(_recognizedAttributes);
            
            int questId = 0;
            WoWPoint location = WoWPoint.Empty;

            _success = _success && GetXYZAttributeAsWoWPoint("X", "Y", "Z", true, WoWPoint.Empty, out location);
            _success = _success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);

            MovePoint = location;
            QuestId = (uint)questId;
        }

        public WoWPoint MovePoint { get; private set; }
        public uint QuestId { get; set; }
        
        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        private int _counter;
        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new Decorator(ret => _counter == 0,
                    new Action(delegate
                        {
                            if (MovePoint.Distance(Me.Location) < 3)
                            {
                                _counter++;
                                return RunStatus.Success;
                            }
                                    
                            WoWMovement.ClickToMove(MovePoint);
                            Thread.Sleep(300);
                            return RunStatus.Running;
                        }))
                    );
        }

        public override bool IsDone { get  { return _counter >= 1; } }

        #endregion
    }
}

