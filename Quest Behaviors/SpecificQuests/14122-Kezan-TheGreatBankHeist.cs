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

using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.TheGreatBankHeist
{
    [CustomBehaviorFileName(@"SpecificQuests\14122-Kezan-TheGreatBankHeist")]
    public class _14122:CustomForcedBehavior
    {
        public _14122(Dictionary<string, string> args):base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
        }
        public int QuestId { get; set; }
        private bool IsAttached;
        private bool IsBehaviorDone;
        private WoWPoint wp = new WoWPoint(-8361.689, 1726.248, 39.94792);
        public List<WoWGameObject> q14122bank
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWGameObject>().Where(ret => (ret.Entry == 195449 && !StyxWoW.Me.IsDead)).OrderBy(ret => ret.Distance).ToList();
            }
        }
        private Composite _root;

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(
                    new Decorator(
                        ret => wp.Distance(StyxWoW.Me.Location) > 5,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Moving to location"),
                                    new Action(ret => Navigator.MoveTo(wp)))),
                    new Decorator(
                        ret => !StyxWoW.Me.HasAura("Vault Cracking Toolset"),
                                new Sequence(
                                    new Action(ret => q14122bank[0].Interact()),
                                    new Action(ret => StyxWoW.SleepForLagDuration())
                                    )),
                        
                    new Decorator(
                        ret => !IsAttached,
                                new Sequence(
                                    new Action(ret => Lua.Events.AttachEvent("CHAT_MSG_RAID_BOSS_WHISPER", q14122msg)),
                                    new Action(ret => IsAttached = true))),
                    new Decorator(
                        ret => StyxWoW.Me.QuestLog.GetQuestById(14122).IsCompleted,
                        new PrioritySelector(
                            new Decorator(
                                ret => IsAttached,
                                new Sequence(
                                    new Action(ret => QBCLog.Info("Detaching")),
                                    new Action(ret => Lua.Events.DetachEvent("CHAT_MSG_RAID_BOSS_WHISPER", q14122msg)),
                                    new Action(ret => IsBehaviorDone = true)
                                    )))),
                    new Decorator(
                        ret => StyxWoW.Me.QuestLog.GetQuestById(14122).IsCompleted && StyxWoW.Me.HasAura("Vault Cracking Toolset"),
                        new Sequence(
                            new Action(ret => Lua.DoString("VehicleExit()"))
                            ))
                    ));

        }
        public override bool IsDone
        {
            get
            {
                return (IsBehaviorDone);
            }
        }
        public override void OnStart()
        {
            OnStart_HandleAttributeProblem();
            if (!IsDone)
            {
                this.UpdateGoalText(QuestId);
            }
        }
        public void q14122msg(object sender, LuaEventArgs arg)
        {
            if (arg.Args[0].ToString().Contains("Infinifold Lockpick"))
            {
                StyxWoW.Sleep(1000);
                Lua.DoString("CastPetAction(4)");
            }
            if (arg.Args[0].ToString().Contains("Amazing G-Ray"))
            {
                StyxWoW.Sleep(1000);
                Lua.DoString("CastPetAction(1)");
            }
            if (arg.Args[0].ToString().Contains("Kaja'mite Drill"))
            {
                StyxWoW.Sleep(1000);
                Lua.DoString("CastPetAction(5)");
            }
            if (arg.Args[0].ToString().Contains("Ear-O-Scope"))
            {
                StyxWoW.Sleep(1000);
                Lua.DoString("CastPetAction(3)");
            }
            if (arg.Args[0].ToString().Contains("Blastcrackers"))
            {
                StyxWoW.Sleep(1000);
                Lua.DoString("CastPetAction(2)");
            }
        }
    }
}
