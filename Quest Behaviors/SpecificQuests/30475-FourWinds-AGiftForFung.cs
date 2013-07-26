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

namespace Honorbuddy.Quest_Behaviors.SpecificQuests.AGiftForFung
{
	[CustomBehaviorFileName(@"SpecificQuests\30475-FourWinds-AGiftForFung")]
	public class AGiftForFung : CustomForcedBehavior
	{
		public AGiftForFung(Dictionary<string, string> args)
			: base(args)
		{
			try
			{
				QuestId = 30475;//GetAttributeAsQuestId("QuestId", true, null) ?? 0;
				SpellIds = GetNumberedAttributesAsArray<int>("SpellId", 1, ConstrainAs.SpellId, null);
				//SpellId = GetAttributeAsNullable<int>("SpellId", false, ConstrainAs.SpellId, null) ?? 0;
				SpellId = SpellIds.FirstOrDefault(id => SpellManager.HasSpell(id));
			}
			catch
			{
				Logging.Write("Problem parsing a QuestId in behavior: A Gift For Fung");
			}
		}
		public int QuestId { get; set; }
		private bool _isBehaviorDone;
		public int MobIdHawk = 59641;
		public int[] SpellIds { get; private set; }
		public int SpellId { get; private set; }
		private Composite _root;
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

		public WoWSpell CurrentBehaviorSpell
		{
			get
			{
				return WoWSpell.FromId(SpellId);
			}
		}

		public List<WoWUnit> Hawk
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == MobIdHawk && !u.IsDead && u.Distance < 10000).OrderBy(u => u.Distance).ToList();
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


		public Composite HawkFlyTo
		{
			get
			{
				return
					new Decorator(ret => !IsObjectiveComplete(1, (uint)QuestId), new Action(c =>
					{
			if (Hawk[0].Location.Distance(Me.Location) < 30)
			{
				TreeRoot.StatusText = "Pulling Monstrous Plainshawk";
				Hawk[0].Target();
					Hawk[0].Face();
							Thread.Sleep(1000);
							SpellManager.Cast(SpellId);
							Thread.Sleep(1000);
			}
							TreeRoot.StatusText = "Finished Pulling!";
							_isBehaviorDone = true;
							return RunStatus.Success;
					}));

			}
		}
		
		protected override Composite CreateBehavior()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, HawkFlyTo, new ActionAlwaysSucceed())));
		}
	}
}

