using System.Collections.Generic;
using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    class FlyTo : CustomForcedBehavior
    {
        public WoWPoint Location { get; private set; }
        public float Distance { get; private set; }

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"X",null},
            {"Y",null},
            {"Z",null},
            {"Distance",null},
        };

        public FlyTo(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);

            WoWPoint p;
            float f;
            bool HasLocation = GetXYZAttributeAsWoWPoint("X", "Y", "Z", true, WoWPoint.Empty, out p);
            if (!HasLocation)
            {
                Logging.Write("FlyTo has no location! {0}", this);
                TreeRoot.Stop();
                return;
            }
            Location = p;
            GetAttributeAsFloat("Distance", false, "10.0", 1, float.MaxValue, out f);
            Distance = f;
        }

        public override bool IsDone
        {
            get { return Location.Distance(StyxWoW.Me.Location) <= Distance; }
        }

        private Composite _root;
        protected override TreeSharp.Composite CreateBehavior()
        {
            return _root ?? (_root = new Action(ret => Flightor.MoveTo(Location)));
        }
    }
}
