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



namespace Blastranaar
{
    public class Blastranaar : CustomForcedBehavior
    {
        public Blastranaar(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                QuestId = 31808;//GetAttributeAsQuestId("QuestId", true, null) ?? 0;
            }
            catch
            {
                Logging.Write("Problem parsing a QuestId in behavior: Rampage Against The Machine");
            }
        }
        public int QuestId { get; set; }
        private bool _isBehaviorDone;
        public int MobIdMantid1 = 67035;
        public int MobIdMantid2 = 67034;
        public int MobIdKunchong = 63625;
		public int Xaril = 63765;
        private Composite _root;
        public QuestCompleteRequirement questCompleteRequirement = QuestCompleteRequirement.NotComplete;
        public QuestInLogRequirement questInLogRequirement = QuestInLogRequirement.InLog;
		static public bool InVehicle { get { return Lua.GetReturnVal<int>("if IsPossessBarVisible() or UnitInVehicle('player') or not(GetBonusBarOffset()==0) then return 1 else return 0 end", 0) == 1; } }
        public override bool IsDone
        {
            get
            {
                return _isBehaviorDone;
            }
        }
        private LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
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

        public List<WoWUnit> Mantid
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == MobIdMantid1 || u.Entry == MobIdMantid2 && !u.IsDead && u.Distance < 350).OrderBy(u => u.Distance).ToList();
            }
        }
        public List<WoWUnit> Kunchong
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == MobIdKunchong && !u.IsDead && u.Distance < 500).OrderBy(u => u.Distance).ToList();
            }
        }

        public WoWUnit Kovok
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>(true)
                                    .Where(o => o.Entry == Xaril)
                                    .OrderBy(o => o.Distance)
                                    .FirstOrDefault();
            }
        }
	
        public bool IsQuestComplete()
        {
            var quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
            return quest == null || quest.IsCompleted;
        }
        private bool IsObjectiveComplete(int objectiveId, uint questId)
        {
            if (Me.QuestLog.GetQuestById(questId) == null)
            {
                return false;
            }
            int returnVal = Lua.GetReturnVal<int>("return GetQuestLogIndexByID(" + questId + ")", 0);
            return
                Lua.GetReturnVal<bool>(
                    string.Concat(new object[] { "return GetQuestLogLeaderBoard(", objectiveId, ",", returnVal, ")" }), 2);
        }

        public Composite DoneYet
        {
            get
            {
                return
                    new Decorator(ret => IsObjectiveComplete(2, (uint)QuestId), new Action(delegate
                    {
                        TreeRoot.StatusText = "Finished!";
                        _isBehaviorDone = true;
                        return RunStatus.Success;
                    }));

            }
        }


        public Composite MantidKill
        {
            get
            {
                return
                    new Decorator(ret => !IsObjectiveComplete(2, (uint)QuestId), new Action(c =>
                    {
                       TreeRoot.StatusText = "Moving to Attack";

			if (Mantid[0].Location.Distance(Kovok.Location) > 15)
			{
				WoWMovement.ClickToMove(Mantid[0].Location);
				Mantid[0].Target();
				Mantid[0].Face();
			}
				WoWMovement.ClickToMove(Mantid[0].Location);
				Mantid[0].Target();
                                WoWMovement.MoveStop();
                                Thread.Sleep(400);
				Mantid[0].Face();
				Lua.DoString("CastPetAction(1)");
				Lua.DoString("CastPetAction(3)");
                                WoWMovement.MoveStop();

			if (IsObjectiveComplete(2, (uint)QuestId))
			{
                        	TreeRoot.StatusText = "Finished!";
                        	_isBehaviorDone = true;
                        	return RunStatus.Success;
			}
                                return RunStatus.Running;

                    }));

            }
        }







		
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, MantidKill, new ActionAlwaysSucceed())));
        }
    }
}



