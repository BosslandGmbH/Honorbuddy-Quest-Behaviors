using System.Collections.Generic;
using System.Linq;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
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
        private static Composite _myHook;

        public BarrelHook(Dictionary<string, string> args) : base(args)
        {
            QuestId = 0; //GetAttributeAsQuestId("QuestId", true, null) ?? 0;
        }

        public int QuestId { get; set; }

        public override bool IsDone
        {
            get { return true; }
        }

        public override void OnStart()
        {
            WoWUnit ookOok = null;

            OnStart_HandleAttributeProblem();
            // 1st call will install hook and 2nd call will remove it.
            if (_myHook == null)
            {
                 QBCLog.DeveloperInfo("Installing BarrelHook");
                _myHook = new Decorator(
                    r => StyxWoW.Me.HasAura("Chuck Barrel"),
                    new PrioritySelector(
                        ctx => ookOok = ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(u => u.Entry == 57628),
                        // only click the 'Break Barrel' button if ook is further than 20 units to - 
                        // prevent getting barrel thrown at character again right after removing it.
                        new Decorator(
                            ctx => ookOok == null || ookOok.Distance > 20,
                            new Action(r => Lua.DoString("RunMacroText('/click ExtraActionButton1')")))));


                TreeHooks.Instance.InsertHook("Questbot_Main", 0, _myHook);
            }
            else
            {
                QBCLog.DeveloperInfo("Removing BarrelHook");
                TreeHooks.Instance.RemoveHook("Questbot_Main", _myHook);
                _myHook = null;
            }
        }
    }
}