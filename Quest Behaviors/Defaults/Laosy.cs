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
                QuestId = 31758;//GetAttributeAsQuestId("QuestId", true, null) ?? 0;
            }
            catch
            {
                Logging.Write("Problem parsing a QuestId in behavior: Laosy Scouting");
            }
        }
        public int QuestId { get; set; }
        private bool _isBehaviorDone;
        public int MobIdLao = 65868;
        private Composite _root;
        public WoWPoint Location1 = new WoWPoint(1578.794, 1446.312, 512.7374);
        public WoWPoint Location2 = new WoWPoint(1574.712, 1428.84, 484.7786);
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

        public List<WoWUnit> Lao
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == MobIdLao && !u.IsDead && u.Distance < 10000).OrderBy(u => u.Distance).ToList();
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
                    new Decorator(ret => IsObjectiveComplete(1, (uint)QuestId), new Action(delegate
                    {
                        TreeRoot.StatusText = "Finished!";
                        _isBehaviorDone = true;
                        return RunStatus.Success;
                    }));

            }
        }

        public Composite LaoMove
        {
            get
            {
                return
                    new Decorator(ret => !IsObjectiveComplete(1, (uint)QuestId), new PrioritySelector(

                    	new Decorator(ret => Lao.Count > 0, new Action(c =>
			{
			  TreeRoot.StatusText = "Got Lao, moving to him";
			  Lao[0].Target();
			  Flightor.MoveTo(Lao[0].Location);

				if(Lao[0].Location.Distance(Me.Location) < 10)
				{
                        		TreeRoot.StatusText = "Finished!";
                        		_isBehaviorDone = true;
                        		return RunStatus.Success;
				}

                          return RunStatus.Success;


			})),


                    	new Decorator(ret => Lao.Count == 0, new PrioritySelector(
                    	  new Decorator(ret => Location1.Distance(Me.Location) > 50  && Me.CurrentTarget == null, new Action(c =>
			  {
			  TreeRoot.StatusText = "Moving to 1st location";
			  Flightor.MoveTo(Location1);
			    if(Lao.Count > 0)
			    {
			  	Lao[0].Target();
			    }
                          return RunStatus.Success;

			  }

			  )),

                    	  new Decorator(ret => Location2.Distance(Me.Location) > 50 && Me.CurrentTarget == null, new Action(c =>
			  {
			  TreeRoot.StatusText = "Moving to 2nd location";
			  Flightor.MoveTo(Location2);
			  Lao[0].Target();
			    if(Lao.Count > 0)
			    {
			  	Lao[0].Target();
			    }
                          return RunStatus.Success;

			  }

			  ))))));



            }
        }






		
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, LaoMove, new ActionAlwaysSucceed())));
        }
    }
}

