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


namespace Honorbuddy.Quest_Behaviors.Hooks
{
    [CustomBehaviorFileName(@"BarrelHook")]
	[CustomBehaviorFileName(@"Hooks\BarrelHook")]
    public class BarrelHook : CustomForcedBehavior
    {
        public BarrelHook(Dictionary<string, string> args)
            : base(args)
        {
            QuestId = 0;//GetAttributeAsQuestId("QuestId", true, null) ?? 0;
        }
        public int QuestId { get; set; }
        
        public override bool IsDone
        {
            get
            {
                return true;
            }
        }


        private Composite myHook;
        public override void OnStart()
        {
            OnStart_HandleAttributeProblem();

            if (myHook == null)
            {
                myHook = new Decorator(r => StyxWoW.Me.HasAura("Chuck Barrel"), new Action(r => Lua.DoString("RunMacroText('/click ExtraActionButton1')")));
                TreeHooks.Instance.InsertHook("Combat_Main", 0, myHook);
            }
            else
            {
                TreeHooks.Instance.RemoveHook("Combat_Main",myHook);
            }
        }
    }
}
