using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

namespace _24817
{
    public class _24817:CustomForcedBehavior
    {
        public _24817(Dictionary<string, string> Args)
            : base(Args)
        {
            QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
        }

        public int QuestId { get; set; }
        public QuestCompleteRequirement questCompleteRequirement = QuestCompleteRequirement.NotComplete;
        public QuestInLogRequirement questInLogRequirement = QuestInLogRequirement.InLog;
        private bool IsBehaviorDone = false;
        private Composite _root;
        public List<WoWGameObject> q24817controller
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWGameObject>().Where(ret => (ret.Entry == 202108 && !StyxWoW.Me.IsDead)).OrderBy(ret => ret.Distance).ToList();
            }
        }
        public List<WoWUnit> q24817_hammer
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(ret => (ret.Entry == 36682 && !StyxWoW.Me.IsDead)).OrderBy(ret => ret.Distance).ToList();
            }
        }
        public List<WoWUnit> q24817_vehicle
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(ret => (ret.Entry == 38318 && !StyxWoW.Me.IsDead)).OrderBy(ret => ret.Distance).ToList();
            }
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
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(
                    new Decorator(
                        ret => !StyxWoW.Me.HasAura("Mechashark X-Steam"),
                                new Sequence(
                                    new Action(ret => Navigator.MoveTo(q24817controller[0].Location)),
                                    new Action(ret => q24817controller[0].Interact()),
                                    new Action(ret => Thread.Sleep(5000))
                                    )),
                    new Decorator(
                        ret => q24817_hammer[0].IsAlive,
                        new PrioritySelector(
                            new Decorator(
                                ret => StyxWoW.Me.CurrentTarget != q24817_hammer[0],
                                new Sequence(
                                    new Action(ret => 
                                    {
							            if (q24817_hammer.Count > 0 && q24817_hammer[0].Location.Distance(StyxWoW.Me.Location) > 45)
							            {
								            Navigator.MoveTo(q24817_hammer[0].Location);
								            Thread.Sleep(100);
							            }
							            if (q24817_hammer.Count > 0 && (q24817_hammer[0].Location.Distance(StyxWoW.Me.Location) <= 45))
							            {
                                            while (!StyxWoW.Me.QuestLog.GetQuestById(24817).IsCompleted)
                                            {
                                                q24817_hammer[0].Face();
                                                q24817_hammer[0].Target();
                                                WoWMovement.Move(WoWMovement.MovementDirection.Backwards);
                                                Thread.Sleep(200);
                                                WoWMovement.MoveStop(WoWMovement.MovementDirection.Backwards);
                                                Lua.DoString("CastPetAction(3)");
                                                Lua.DoString("CastPetAction(2)");
                                                Lua.DoString("CastPetAction(1)");
                                            }
							            }
                                    }))))),
                     new Decorator(
                         ret => StyxWoW.Me.QuestLog.GetQuestById(24817).IsCompleted,
                         new Sequence(
                             new Action(ret => Lua.DoString("VehicleExit()")),
                             new Action(ret => IsBehaviorDone = true)))
                    ));

        }
    }
}
