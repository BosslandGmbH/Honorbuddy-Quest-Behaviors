using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.TheEndlessFlowHook
{
    [CustomBehaviorFileName(@"TheEndlessFlowHook")]
    [Obsolete(@"Use Hooks\TheEndlessFlowHook instead")]
    public class TheEndlessFlowHook : Hooks.TheEndlessFlowHook
    {
        public TheEndlessFlowHook(Dictionary<string, string> args)
            : base(args)
        {
        }
    }
}
