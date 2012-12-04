using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.Plugins;
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
                QuestId = 31061;//GetAttributeAsQuestId("QuestId", true, null) ?? 0;
                SpellIds = GetNumberedAttributesAsArray<int>("SpellId", 1, ConstrainAs.SpellId, null);
                //SpellId = GetAttributeAsNullable<int>("SpellId", false, ConstrainAs.SpellId, null) ?? 0;
                SpellId = SpellIds.FirstOrDefault(id => SpellManager.HasSpell(id));
            }
            catch
            {
                Logging.Write("Problem parsing a QuestId in behavior: Riding The Storm");
            }
        }
        public int QuestId { get; set; }
        private bool _isBehaviorDone;
        public int MobIdCloudrunner = 62586;
        public int BronzeClawId = 83134;
        public int[] SpellIds { get; private set; }
        public int SpellId { get; private set; }
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

        public WoWSpell CurrentBehaviorSpell
        {
            get
            {
                return WoWSpell.FromId(SpellId);
            }
        }

        public List<WoWUnit> CloudrunnerOutRange
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == MobIdCloudrunner && !u.IsDead && u.Distance < 10000 && u.HealthPercent == 100).OrderBy(u => u.Distance).ToList();
            }
        }


        public List<WoWUnit> CloudrunnerInRange
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == MobIdCloudrunner && !u.IsDead && u.Distance < 10000).OrderBy(u => u.Distance).ToList();
            }
        }

        public WoWItem BronzeClaw { get { return (StyxWoW.Me.CarriedItems.FirstOrDefault(i => i.Entry == BronzeClawId)); } }


	
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


        public Composite CloudrunnerKill
        {
            get
            {
                return
                    new Decorator(ret => !IsObjectiveComplete(1, (uint)QuestId), new PrioritySelector(

			new Decorator(ret => CloudrunnerOutRange[0].Location.Distance(Me.Location) > 20 && !Me.Combat, new Action(c =>
			{

			TreeRoot.StatusText = "Using Bronze Claw on CloudRunner";
			CloudrunnerOutRange[0].Target();

			if (BronzeClaw.Cooldown == 0)
				{

				BronzeClaw.UseContainerItem();
                                Thread.Sleep(1000);

				}

                        return RunStatus.Success;

			}

			)),

			new Decorator(ret => CloudrunnerInRange[0].Location.Distance(Me.Location) < 10, new Action(c =>
			{

			TreeRoot.StatusText = "Killing CloudRunner";
                    	SpellManager.Cast(SpellId);
                        Thread.Sleep(1000);

			if (CloudrunnerInRange[0].IsFriendly)
				{

				TreeRoot.StatusText = "CloudRunner is friendly, switching to new one";
				Thread.Sleep(2000);
                        	return RunStatus.Success;

				}

			if (IsObjectiveComplete(1, (uint)QuestId))
				{

                        	TreeRoot.StatusText = "Finished!";
                        	_isBehaviorDone = true;
                        	return RunStatus.Success;

				}

                        return RunStatus.Running;


			}
			
			))));


	



            }
        }







		
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, CloudrunnerKill, new ActionAlwaysSucceed())));
        }
    }
}
