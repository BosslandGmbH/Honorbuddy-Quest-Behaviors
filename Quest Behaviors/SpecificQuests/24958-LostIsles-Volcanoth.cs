using CommonBehaviors.Actions;

using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Action = Styx.TreeSharp.Action;

namespace Honorbuddy.Quest_Behaviors.SpecificQuests.Volcanoth
{
	[CustomBehaviorFileName(@"SpecificQuests\24958-LostIsles-Volcanoth")]
	public class Volcanoth : CustomForcedBehavior
	{
		public Volcanoth(Dictionary<string, string> args)
			: base(args)
		{
			try
			{
				QuestId = 24958;
			}
			catch
			{
				Logging.Write("Problem parsing a QuestId in behavior: Volcanoth");
			}
		}
		private Composite _behaviorTreeHook_CombatMain;
		public int QuestId { get; set; }
		private bool _isBehaviorDone;
		public int TurtleMobId = 38855;
		private Composite _root;
		public QuestCompleteRequirement questCompleteRequirement = QuestCompleteRequirement.NotComplete;
		public QuestInLogRequirement questInLogRequirement = QuestInLogRequirement.InLog;

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
				_behaviorTreeHook_CombatMain = CreateBehavior_CombatMain();
				TreeHooks.Instance.InsertHook("Combat_Main", 0, _behaviorTreeHook_CombatMain);
			}
		}
		
		protected virtual Composite CreateBehavior_CombatMain()
		{
			return new Decorator(context => !IsDone,
				new PrioritySelector(
					// Disable the CombatRoutine
					DoneYet, TurtleCast, TurtlePull, new ActionAlwaysSucceed()
			));
		}

		public WoWUnit Turtle
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == TurtleMobId && !u.IsDead && u.Distance < 500).OrderBy(u => u.Distance).FirstOrDefault();
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
					new Decorator(ret => IsQuestComplete(), new Action(delegate
					{
						TreeRoot.StatusText = "Finished!";
						_isBehaviorDone = true;
						return RunStatus.Success;
					}));
			}
		}
		
		public Composite TurtleCast
		{
			get
			{
				return 
					new Decorator(c => Turtle != null && Turtle.IsCasting, 
						new Action(c => BarryDurex.QuestHelper.AvoidEnemyCast(Turtle, 80, 100))
					);
			}
		}
		
		public Composite TurtlePull
		{
			get
			{
				return
					new Decorator(c => Turtle != null && !Turtle.IsCasting, 
						new Sequence(
							new Action(c =>	Turtle.Target()),
							new DecoratorContinue(c => Turtle.Location.Distance(Me.Location) > 20,
								new Action(c =>	Navigator.MoveTo(Turtle.Location))),
							new DecoratorContinue(c => Turtle.Location.Distance(Me.Location) < 20,
								new Sequence(
									new Action(c =>	Turtle.Face()),
									new Action(c =>	WoWMovement.MoveStop()),
									new Action(c =>	Lua.DoString("UseItemByName(52043)")),
									new WaitContinue(TimeSpan.FromSeconds(1),
										context => Me.IsCasting || Turtle.IsCasting,
										new ActionAlwaysSucceed()),
									new WaitContinue(TimeSpan.FromSeconds(10),
										context => !Me.IsCasting || Turtle.IsCasting,
										new ActionAlwaysSucceed())
					))));
			}
		}
		
		protected override Composite CreateBehavior()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(new ActionAlwaysSucceed())));
		}
	}
}

namespace BarryDurex
{
	static class QuestHelper
	{
		public static void AvoidEnemyCast(WoWUnit Unit, float EnemyAttackRadius, float SaveDistance)
		{
			if (!StyxWoW.Me.IsFacing(Unit))
			{ Unit.Face(); Thread.Sleep(300); }

			float BehemothRotation = getPositive(Unit.RotationDegrees);
			float invertEnemyRotation = getInvert(BehemothRotation);

			WoWMovement.MovementDirection move = WoWMovement.MovementDirection.None;

			if (getPositive(StyxWoW.Me.RotationDegrees) > invertEnemyRotation)
			{ move = WoWMovement.MovementDirection.StrafeRight; }
			else
			{ move = WoWMovement.MovementDirection.StrafeLeft; }

			while (Unit.Distance2D <= SaveDistance && Unit.IsCasting && ((EnemyAttackRadius == 0 && !StyxWoW.Me.IsSafelyBehind(Unit)) ||
				(EnemyAttackRadius != 0 && Unit.IsSafelyFacing(StyxWoW.Me, EnemyAttackRadius)) || Unit.Distance2D <= 2 ))
			{
				WoWMovement.Move(move);
				Unit.Face();
			}
			WoWMovement.MoveStop();
		}

		private static float getInvert(float f)
		{
			if (f < 180)
				return (f + 180);
			//else if (f >= 180)
			return (f - 180);
		}

		private static float getPositive(float f)
		{
			if (f < 0)
				return (f + 360);
			return f;
		}
	}
}

