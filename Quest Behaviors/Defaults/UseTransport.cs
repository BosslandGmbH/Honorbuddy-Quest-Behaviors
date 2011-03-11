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
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Styx.Logic.BehaviorTree;
using Action = TreeSharp.Action;
using Styx.Logic;

namespace Styx.Bot.Quest_Behaviors
{
    public class UseTransport : CustomForcedBehavior
    {
        

        /// <summary>
        /// Transport by raphus
        /// Allows you to use Transports.
        /// ##Syntax##
        /// TransportId: ID of the transport.
        /// TransportStart: Start point of the transport that we will get on when its close enough to that point.
        /// TransportEnd: End point of the transport that we will get off when its close enough to that point.
        /// WaitAt: Where you wish to wait the transport at
        /// GetOff: Where you wish to end up at when transport reaches TransportEnd point
        /// StandOn: The point you wish the stand while you are in the transport
        /// </summary>
        /// 

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"TransportId",null},
            {"Transport", null},
            {"Start", null},
            {"End", null},
            {"Entry", null},
            {"Exit", null},
            {"TransportStartX",null},
            {"TransportStartY",null},
            {"TransportStartZ",null},
            {"TransportEndX",null},
            {"TransportEndY",null},
            {"TransportEndZ",null},
            {"WaitAtX",null},
            {"WaitAtY",null},
            {"WaitAtZ",null},
            {"StandOnX", null},
            {"StandOnY", null},
            {"StandOnZ", null},
            {"GetOffX",null},
            {"GetOffY",null},
            {"GetOffZ",null},

        };

        bool success = true;

        public UseTransport(Dictionary<string, string> args)
            : base(args)
        {

            CheckForUnrecognizedAttributes(recognizedAttributes);

            WoWPoint waitAtLocation = WoWPoint.Empty;
            WoWPoint getOfflocation = WoWPoint.Empty;
            WoWPoint startlocation = WoWPoint.Empty;
            WoWPoint endlocation = WoWPoint.Empty;
            WoWPoint standLocation = WoWPoint.Empty;
            int transportId = 0;
            int transport = 0;

            string entry = "";
            string exit = "";
            string start = "";
            string end = "";

            success = success && GetAttributeAsInteger("TransportId", false, "0", 0, int.MaxValue, out transportId);
            success = success && GetAttributeAsInteger("Transport", false, "0", 0, int.MaxValue, out transport);
            success = success && GetXYZAttributeAsWoWPoint("TransportStartX", "TransportStartY", "TransportStartZ", false, WoWPoint.Empty, out startlocation);
            success = success && GetAttributeAsString("Start", false, "", out start);
            if (start != "")
            {
                var split = start.Split(',');
                startlocation = new WoWPoint(float.Parse(split[0]), float.Parse(split[1]), float.Parse(split[2]));
            }
            success = success && GetXYZAttributeAsWoWPoint("TransportEndX", "TransportEndY", "TransportEndZ", false, WoWPoint.Empty, out endlocation);
            success = success && GetAttributeAsString("End", false, "", out end);
            if (end != "")
            {
                var split = end.Split(',');
                endlocation = new WoWPoint(float.Parse(split[0]), float.Parse(split[1]), float.Parse(split[2]));
            }
            success = success && GetXYZAttributeAsWoWPoint("WaitAtX", "WaitAtY", "WaitAtZ", false, WoWPoint.Empty, out waitAtLocation);
            success = success && GetAttributeAsString("Entry", false, "", out entry);
            if (entry != "")
            {
                var split = entry.Split(',');
                waitAtLocation = new WoWPoint(float.Parse(split[0]), float.Parse(split[1]), float.Parse(split[2]));
            }
            success = success && GetXYZAttributeAsWoWPoint("GetOffX", "GetOffY", "GetOffZ", false, WoWPoint.Empty, out getOfflocation);
            success = success && GetAttributeAsString("Exit", false, "", out exit);
            if (exit != "")
            {
                var split = exit.Split(',');
                getOfflocation = new WoWPoint(float.Parse(split[0]), float.Parse(split[1]), float.Parse(split[2]));
            }
            success = success && GetXYZAttributeAsWoWPoint("StandOnX", "StandOnY", "StandOnZ", false, WoWPoint.Empty, out standLocation);
            
            TransportId = transportId != 0 && transportId != int.MaxValue ? transportId : transport;

            WaitAtLocation = waitAtLocation;
            GetOffLocation = getOfflocation;
            StartLocation = startlocation;
            EndLocation = endlocation;
            StandLocation = standLocation;

            Counter = 0;
        }

        public WoWPoint WaitAtLocation { get; private set; }
        public WoWPoint GetOffLocation { get; private set; }
        public WoWPoint StartLocation { get; private set; }
        public WoWPoint EndLocation { get; private set; }
        public WoWPoint StandLocation { get; private set; }
        public int Counter { get; set; }
        public bool MovedOnShip = false;
        public bool OnShip = false;
        public bool MovedToTarget = false;
        public int TransportId { get; set; }
        public uint QuestId { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        #region Overrides of CustomForcedBehavior

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

            if (quest != null)
            {
                TreeRoot.GoalText = "UseTransport - " + quest.Name;
            }
            else
            {
                TreeRoot.GoalText = "UseTransport: Running";
            }
        }
        private bool usedTransport;
        private bool wasOnWaitLocation;
        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(
                    new Decorator(
                        ret => !wasOnWaitLocation,
                        new PrioritySelector(
                            new Decorator(
                                ret => WaitAtLocation.Distance(me.Location) > 3,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Moving to wait location"),
                                    new Action(ret => Navigator.MoveTo(WaitAtLocation)))),
                            new Sequence(
                                new Action(ret => Navigator.PlayerMover.MoveStop()),
                                new Action(ret => Mount.Dismount()),
                                new Action(ret => wasOnWaitLocation = true),
                                new Action(ret => TreeRoot.StatusText = "Waiting for transport")))),
                    new Decorator(
                        ret => TransportLocation != WoWPoint.Empty && TransportLocation.Distance(EndLocation) < 2 && usedTransport,
                        new PrioritySelector(
                            new Decorator(
                                ret => me.Location.Distance(GetOffLocation) > 2,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Moving out of transport"),
                                    new Action(ret => Navigator.PlayerMover.MoveTowards(GetOffLocation)),
                                    new Action(ret => StyxWoW.SleepForLagDuration()),
                                    new DecoratorContinue(
                                        ret => me.IsOnTransport,
                                        new Action(ret => WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend, TimeSpan.FromMilliseconds(50)))))),
                            new Action(ret => _isDone = true))),
                    new Decorator(
                        ret => me.IsOnTransport && StandLocation != WoWPoint.Empty && !usedTransport,
                        new PrioritySelector(
                            new Decorator(
                                ret => me.Location.Distance2D(StandLocation) > 2,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Moving to stand location"),
                                    new Action(ret => Navigator.PlayerMover.MoveTowards(StandLocation)))),
                            new Sequence(
                                new Action(ret => usedTransport = true),
                                new Action(ret => Navigator.PlayerMover.MoveStop()),
                                new Action(ret => TreeRoot.StatusText = "Waiting for the end location"))
                        )),
                    new Decorator(
                        ret => TransportLocation != WoWPoint.Empty && TransportLocation.Distance(StartLocation) < 2 && !usedTransport,
                        new PrioritySelector(
                            new Decorator(
                                ret => me.Location.Distance2D(TransportLocation) > 2,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Moving inside transport"),
                                    new Action(ret => Navigator.PlayerMover.MoveTowards(TransportLocation)),
                                    new Action(ret => StyxWoW.SleepForLagDuration()),
                                    new DecoratorContinue(
                                        ret => !me.IsOnTransport,
                                        new Action(ret => WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend, TimeSpan.FromMilliseconds(50)))))),
                            new Sequence(
                                new Action(ret => usedTransport = true),
                                new Action(ret => Navigator.PlayerMover.MoveStop()),
                                new Action(ret => TreeRoot.StatusText = "Waiting for the end location"))))
                    ));
        }

        private WoWPoint TransportLocation
        {
            get
            {
               var transport = ObjectManager.GetObjectsOfType<WoWGameObject>(true, false).FirstOrDefault(o => o.Entry == TransportId);

               if (transport == null)
                   return WoWPoint.Empty;

               Tripper.Tools.Math.Matrix m = transport.GetWorldMatrix();

               return new WoWPoint(m.M41, m.M42, m.M43);
            }
        }

        private bool _isDone;
        public override bool IsDone
        {
            get { return _isDone; }
        }

        #endregion
    }
}

