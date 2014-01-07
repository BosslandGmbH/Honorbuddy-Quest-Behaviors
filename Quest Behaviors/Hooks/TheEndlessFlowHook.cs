//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

#region Summary and Documentation
#endregion


#region Examples
#endregion


#region Usings
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
#endregion


namespace Honorbuddy.Quest_Behaviors.Hooks
{
	[CustomBehaviorFileName(@"Hooks\TheEndlessFlowHook")]
    public class TheEndlessFlowHook : CustomForcedBehavior
    {
        public TheEndlessFlowHook(Dictionary<string, string> args)
            : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;
        }


        private bool _inserted;

		public override bool IsDone { get { return true; } }

        private LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
        }


        private WoWUnit Bunny
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(r => r.Entry == 44360).OrderBy(r=>r.Distance2D).FirstOrDefault();
            }
        }


        //44329/
        private WoWUnit Stickbone
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(r => r.Entry == 44329 && r.IsAlive).OrderBy(r => r.Distance2D).FirstOrDefault();
            }
        }

        private WoWItem Bomb
        {
            get
            {
                return StyxWoW.Me.BagItems.FirstOrDefault(r => r.Entry == 60849);
            }
        }


        private Composite _myHook;
        public override void OnStart()
        {
            OnStart_HandleAttributeProblem();
            
            if (_myHook == null)
            {
                _myHook = new PrioritySelector(
                    //Remove hook once were done with the quest
                    new Decorator(r=> Bomb == null, new Action(r=>TreeHooks.Instance.RemoveHook("Questbot_Main", _myHook))),
                    new Decorator(r=> Bunny != null && Bunny.Distance < 35 && Bunny.Distance > 5, new Action(r=>Navigator.MoveTo(Bunny.Location))),
                    
                   
                    new Decorator(r=> Bunny != null && Bunny.Distance <=5, new Sequence(
							new Action(ret => Bomb.Use()),
							new WaitContinue(1, ret => Me.CurrentPendingCursorSpell != null, new ActionAlwaysSucceed()),
							new Action(ret => SpellManager.ClickRemoteLocation(Bunny.Location)))),
							
							
                  new Decorator(r => Stickbone != null && Stickbone.Distance <= 5, new Sequence(
							new Action(ret => Bomb.Use()),
							new WaitContinue(1, ret => Me.CurrentPendingCursorSpell != null, new ActionAlwaysSucceed()),
							new Action(ret => SpellManager.ClickRemoteLocation(Stickbone.Location))))

                        
                    
                    );
                TreeHooks.Instance.InsertHook("Questbot_Main", 0, _myHook);
                _inserted = true;
            }

            else
            {
                TreeHooks.Instance.RemoveHook("Questbot_Main", _myHook);
                _inserted = false;
            }
        }
    }
}
