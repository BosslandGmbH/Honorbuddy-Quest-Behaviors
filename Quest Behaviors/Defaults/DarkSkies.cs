using System.Collections.Generic;
using System.Linq;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
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
                QuestId = 31216;//GetAttributeAsQuestId("QuestId", true, null) ?? 0;
            }
            catch
            {
                Logging.Write("Problem parsing a QuestId in behavior: Dark Skies");
            }
        }
        public int QuestId { get; set; }
        private bool _isBehaviorDone;
        public int MobIdMantid1 = 63635;
        public int MobIdMantid2 = 63613;
        public int MobIdMantid3 = 63615;
        public int MobIdKunchong = 63625;
		public int Xaril = 62151;
        private Composite _root;
        public WoWPoint Location = new WoWPoint(138.3817, 225.952, 214.7609);
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
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == MobIdMantid1 || u.Entry == MobIdMantid2 || u.Entry == MobIdMantid3 && !u.IsDead && u.Distance < 10000).OrderBy(u => u.Distance).ToList();
            }
        }
        public List<WoWUnit> Kunchong
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == MobIdKunchong && !u.IsDead && u.Distance < 10000).OrderBy(u => u.Distance).ToList();
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
                    new Decorator(ret => IsObjectiveComplete(2, (uint)QuestId) && IsObjectiveComplete(3, (uint)QuestId), new Action(delegate
                    {
						Lua.DoString("CastPetAction(6)");
                        TreeRoot.StatusText = "Finished!";
                        _isBehaviorDone = true;
                        return RunStatus.Success;
                    }));

            }
        }


        public Composite KillOne
        {
            get
            {
                return new Decorator(r => !IsObjectiveComplete(3, (uint)QuestId), new Action(r =>
                                                                                                {
                                                                                                    Lua.DoString(
                                                                                                        "CastPetAction(2)");
                                                                                                    SpellManager.
                                                                                                        ClickRemoteLocation
                                                                                                        (Kunchong[0].
                                                                                                             Location);
                                                                                                    Lua.DoString(
                                                                                                        "CastPetAction(1)");
                                                                                                    SpellManager.
                                                                                                        ClickRemoteLocation
                                                                                                        (Kunchong[0].
                                                                                                             Location);
                                                                                                }));
            }
        }


        public Composite KillTwo
        {
            get
            {
                return new Decorator(r => !IsObjectiveComplete(2, (uint)QuestId), new Action(r =>
                {
                    Lua.DoString(
                        "CastPetAction(1)");
                    SpellManager.
                        ClickRemoteLocation
                        (Mantid[0].
                             Location);
                }));
            }
        }


		
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, KillOne, KillTwo, new ActionAlwaysSucceed())));
        }
    }
}
