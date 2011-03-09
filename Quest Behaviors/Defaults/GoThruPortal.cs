using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Styx.Database;
using Styx.Logic.Combat;
using Styx.Helpers;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Pathing;
using Styx.Logic.Profiles.Quest;
using Styx.Logic.Questing;
using Styx.Logic.BehaviorTree ;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    public class GoThruPortal : CustomForcedBehavior
    {
        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {
            {"X",null},
            {"Y",null},
            {"Z",null},
            {"QuestId",null},
            {"Timeout",null}
        };

        bool success = true;
        bool inPortal = false;
        string zoneText;

        public GoThruPortal(Dictionary<string, string> args)
            : base(args)
        {

            CheckForUnrecognizedAttributes(recognizedAttributes);

            WoWPoint location = new WoWPoint(0, 0, 0);
            int questId = 0;
            int timeOut = 0;

            success = success && GetXYZAttributeAsWoWPoint("X", "Y", "Z", true, new WoWPoint(0, 0, 0), out location);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
            success = success && GetAttributeAsInteger("Timeout", false, "10000", 1, 60000, out timeOut);

            zoneText = StyxWoW.Me.ZoneText;
            MovePoint = WoWMovement.CalculatePointFrom(location, -15);
            QuestId = (uint)questId;
            Timeout =  System.Environment.TickCount + timeOut;
        }

        public WoWPoint MovePoint { get; private set; }
        public int Counter { get; set; }
        public uint QuestId { get; set; }
        public int Timeout { get; set; }


        public static LocalPlayer me = ObjectManager.Me;
        public bool _isDone;

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                    // first state catches if we are done just in case
                    new Decorator(ret => _isDone,
                        new Action(delegate
                        {
                            return RunStatus.Success;
                        })),

                    // if we hit the load screen and we are back in game
                    new Decorator(ret => inPortal && ObjectManager.IsInGame && StyxWoW.Me != null,
                        new Action(delegate
                        {
                            _isDone = true;
                            Logging.WriteDebug("GoThruPortal: went thru portal");
                            Thread.Sleep(500);
                            WoWMovement.MoveStop();
                            Thread.Sleep(500);
                            return RunStatus.Success;
                        })),

                    // if zone name changed
                    new Decorator(ret => zoneText != StyxWoW.Me.ZoneText,
                        new Action(ret => inPortal = true)),

                    // if load screen is visible
                    new Decorator(ret => !ObjectManager.IsInGame || StyxWoW.Me == null,
                        new Action(ret => inPortal = true)),

                    // if we are within 2 yards of calculated end point we should never reach
                    new Decorator(ret => MovePoint.Distance(me.Location) < 2,
                        new Action(delegate
                        {
                            _isDone = true;
                            WoWMovement.MoveStop();
                            Logging.Write("GoThruPortal: ERROR reached end point - failed to go through portal");
                            TreeRoot.Stop();
                            return RunStatus.Success;
                        })),

                    new Decorator(ret => Timeout != 0 && Timeout < System.Environment.TickCount,
                        new Action(delegate
                        {
                            _isDone = true;
                            WoWMovement.MoveStop();
                            Logging.Write("GoThruPortal: ERROR timed out after {0} ms - failed to go through portal", Timeout );
                            TreeRoot.Stop();
                            return RunStatus.Success;
                        })),

                    new Decorator(ret => !StyxWoW.Me.IsMoving,
                        new Action(delegate
                        {
                            Logging.WriteDebug("GoThruPortal: moving to {0}", MovePoint);
                            WoWMovement.ClickToMove(MovePoint);
                            return RunStatus.Success;
                        }))
                    )
                );

        }

        public override bool IsDone
        {
            get
            {
                return _isDone;
            }
        }
    }
}

