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
using Styx.Logic.BehaviorTree;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    public class Halt : CustomForcedBehavior
    {
        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {
            {"MSG",null},
        };


        public Halt(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);
            string sMsg = "Quest Profile HALT";
            GetAttributeAsString("Msg", false, "Quest Profile HALT", out sMsg);
            Logging.Write("{0}", sMsg);
            TreeRoot.Stop();
        }

        protected override Composite CreateBehavior()
        {
            return null;
        }

        public override bool IsDone
        {
            get
            {
                return false;
            }
        }
    }
}

