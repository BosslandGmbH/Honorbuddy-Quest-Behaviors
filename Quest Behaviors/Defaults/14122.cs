using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
namespace Styx.Bot.Quest_Behaviors
{
    public class _14122:CustomForcedBehavior
    {
        public _14122(Dictionary<string, string> args):base(args)
        {
            QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
        }
        public int QuestId { get; set; }
        private bool IsAttached;
        private bool IsBehaviorDone;
        private WoWPoint wp = new WoWPoint(-8361.689, 1726.248, 39.94792);
        public QuestCompleteRequirement questCompleteRequirement = QuestCompleteRequirement.NotComplete;
        public QuestInLogRequirement questInLogRequirement = QuestInLogRequirement.InLog;
        public List<WoWGameObject> q14122bank
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWGameObject>().Where(ret => (ret.Entry == 195449 && !StyxWoW.Me.IsDead)).OrderBy(ret => ret.Distance).ToList();
            }
        }
        private TreeSharp.Composite _root;

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
                                    new Action(ret => Logging.Write("Detaching")),
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
                PlayerQuest Quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
                TreeRoot.GoalText = ((Quest != null) ? ("\"" + Quest.Name + "\"") : "In Progress");
            }
        }
        public void q14122msg(object sender, LuaEventArgs arg)
        {
            if (arg.Args[0].ToString().Contains("Infinifold Lockpick"))
            {
                Thread.Sleep(1000);
                Lua.DoString("CastPetAction(4)");
            }
            if (arg.Args[0].ToString().Contains("Amazing G-Ray"))
            {
                Thread.Sleep(1000);
                Lua.DoString("CastPetAction(1)");
            }
            if (arg.Args[0].ToString().Contains("Kaja'mite Drill"))
            {
                Thread.Sleep(1000);
                Lua.DoString("CastPetAction(5)");
            }
            if (arg.Args[0].ToString().Contains("Ear-O-Scope"))
            {
                Thread.Sleep(1000);
                Lua.DoString("CastPetAction(3)");
            }
            if (arg.Args[0].ToString().Contains("Blastcrackers"))
            {
                Thread.Sleep(1000);
                Lua.DoString("CastPetAction(2)");
            }
        }
    }
}
