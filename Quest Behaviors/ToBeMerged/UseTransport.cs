using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Styx.Logic.Questing;
using Styx.Logic.Pathing;
using TreeSharp;
using Styx;
using Styx.WoWInternals.WoWObjects;
using Styx.Helpers;
using Styx.WoWInternals.World;
using Styx.WoWInternals;

namespace regecksqt
{
    public class UseTransport : CustomForcedBehavior
    {
        private int i_transport;

        private WoWPoint p_entry;
        private WoWPoint p_exit;
        private WoWPoint p_start;
        private WoWPoint p_end;

        private Composite _root;

        public UseTransport(Dictionary<string, string> args)
            : base(args)
        {
            i_transport = int.Parse(args["Transport"]);
            string[] entry = args["Entry"].Split(',');
            string[] exit = args["Exit"].Split(',');
            string[] start = args["Start"].Split(',');
            string[] end = args["End"].Split(',');

            p_entry = new WoWPoint(float.Parse(entry[0]), float.Parse(entry[1]), float.Parse(entry[2]));
            p_exit = new WoWPoint(float.Parse(exit[0]), float.Parse(exit[1]), float.Parse(exit[2]));
            p_start = new WoWPoint(float.Parse(start[0]), float.Parse(start[1]), float.Parse(start[2]));
            p_end = new WoWPoint(float.Parse(end[0]), float.Parse(end[1]), float.Parse(end[2]));
        }

        private void BehaviorTreesSuck()
        {
            LocalPlayer Me = StyxWoW.Me;
            WoWPoint p_transport = GetTransportLocation();

            if (p_transport != WoWPoint.Empty && Me.IsOnTransport)
            {
                if (p_transport.Distance(p_end) <= 1)
                {
                    Logging.Write("Exiting transport");
                    WoWMovement.ClickToMove(p_exit);
                }
                else if (Me.IsMoving)
                {
                    WoWMovement.MoveStop();
                }
            }
            else if (p_transport != WoWPoint.Empty && p_transport.Distance(p_start) <= 1 && p_entry.Distance(Me.Location) <= 5)
            {
                Logging.Write("Moving onto transport");
                WoWMovement.ClickToMove(p_transport);
            }
            else if(p_entry.Distance(Me.Location) > 5)
            {
                Logging.Write("Moving to entry");
                Navigator.MoveTo(p_entry);
            }
        }

        private WoWPoint GetTransportLocation()
        {
            WoWGameObject elevator = ObjectManager.GetObjectsOfType<WoWGameObject>(true, false).Where(o => o.Entry == i_transport).FirstOrDefault();
            if (elevator != null)
            {
                Tripper.Tools.Math.Matrix m = elevator.GetWorldMatrix();
                return new WoWPoint(m.M41, m.M42, m.M43);
            }
            else
            {
                return WoWPoint.Empty;
            }

        }

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new TreeSharp.Action(
                        a => BehaviorTreesSuck()
                    ));
        }

        public override bool IsDone
        {
            get 
            {
                return !StyxWoW.Me.IsOnTransport && StyxWoW.Me.Location.Distance(p_exit) <= 5;
            }
        }

    }
}
