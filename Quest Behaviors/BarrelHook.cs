using System;
using System.Collections.Generic;
using System.Linq;

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


namespace Honorbuddy.Quest_Behaviors.BarrelHook
{
    [CustomBehaviorFileName(@"BarrelHook")]
    [Obsolete(@"Use Hooks\BarrelHook instead")]
    public class BarrelHook : Hooks.BarrelHook
    {
        public BarrelHook(Dictionary<string, string> args) : base(args)
        {
        }
    }
}
