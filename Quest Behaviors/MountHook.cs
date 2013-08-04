using System;
using System.Collections.Generic;
using System.Linq;

using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Profiles.Quest.Order;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Honorbuddy.Quest_Behaviors.ForceSetVendor;
using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.MountHook
{
    [CustomBehaviorFileName(@"MountHook")]
    [Obsolete(@"Use Hooks\MountHook instead")]
    public class MountHook : Hooks.MountHook
    {
        public MountHook(Dictionary<string, string> args)
            : base(args)
        {
        }
    }
}
