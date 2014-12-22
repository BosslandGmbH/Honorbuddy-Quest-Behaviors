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
using System;
using System.Collections.Generic;
using System.Linq;

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
using WaitTimer = Styx.Common.Helpers.WaitTimer;
#endregion

namespace Honorbuddy.Quest_Behaviors.SpecificQuests.AllianceBorntoShred
{
	[CustomBehaviorFileName(@"SpecificQuests\34097-Talador-AllianceBorntoShred")]
	public class BorntoShred : CustomForcedBehavior
	{
		public uint[] MobIds = new uint[] {75943, 75944, 75945, 75946, 75947, 75948, 77066,};
		private WoWPoint StartPoint = new WoWPoint(3384.808, 2071.451, 158.9176);
		private WoWPoint WaitPoint = new WoWPoint(2895.472, 2250.659, 109.0454);
		private bool _isBehaviorDone;

		private Composite _root;
		private bool _useMount;

		#region Cleanup

        	public override void OnFinished()
        	{
         	   CharacterSettings.Instance.UseMount = _useMount;
        	   TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_MainCombat());
         	   TreeRoot.GoalText = string.Empty;
         	   TreeRoot.StatusText = string.Empty;
         	   base.OnFinished();
        	}

		#endregion

		public BorntoShred(Dictionary<string, string> args) : base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = 34097; //GetAttributeAsQuestId("QuestId", true, null) ?? 0;
			}
			catch (Exception except)
			{
				// Maintenance problems occur for a number of reasons.  The primary two are...
				// * Changes were made to the behavior, and boundary conditions weren't properly tested.
				// * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
				// In any case, we pinpoint the source of the problem area here, and hopefully it
				// can be quickly resolved.
				QBCLog.Exception(except);
				IsAttributeProblem = true;
			}
		}

		public int QuestId { get; set; }

		public override bool IsDone
		{
			get { return _isBehaviorDone; }
		}

		private LocalPlayer Me
		{
			get { return (StyxWoW.Me); }
		}

		public List<WoWUnit> GromKars
		{
			get
			{
				var myLoc = Me.Location;
				return (from u in ObjectManager.GetObjectsOfType<WoWUnit>()
					where MobIds.Contains(u.Entry) && !u.IsDead
					let loc = u.Location
					orderby loc.DistanceSqr(myLoc)
					select u).ToList();
			}
		}

		public Composite CreateBehavior_CheckCompletion()
		{

			return new Decorator(r => Me.IsQuestComplete(QuestId),
				new PrioritySelector(
					new Decorator(r => Me.Location.Distance(StartPoint) > 15,
						new Action(r => Navigator.MoveTo(StartPoint))
					),
					new Decorator(r => Me.Location.Distance(StartPoint) <= 15,
                             			new Action(delegate
						{
							TreeRoot.StatusText = "Finished!";
							Lua.DoString("VehicleExit()");
							_isBehaviorDone = true;
							return RunStatus.Success;
						}))));

		}

		public WoWUnit IronShredder
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>()
					.FirstOrDefault(r => r.NpcFlags == 1 && r.Entry == 75721 && r.Location.DistanceSqr(StartPoint) < 30*30);
			}
		}

		public Composite CreateBehavior_KillMantid()
		{
			WoWUnit attackTarget = null;
			WoWUnit IronShredder = null;
			WaitTimer DeathFromAboveTimer = WaitTimer.TenSeconds;

			return new Decorator(r => !Me.IsQuestComplete(QuestId) && Query.IsInVehicle() && (IronShredder = Me.CharmedUnit) != null,
				new PrioritySelector(ctx => attackTarget = GetAttackTarget(),
					new Decorator(ctx => attackTarget != null,
						new PrioritySelector(
							new ActionFail(ctx => _stuckTimer.Reset()),
							new ActionSetActivity("Moving to Attack"),
							new Decorator(ctx => Me.CurrentTargetGuid != attackTarget.Guid,
								new ActionFail(ctx => attackTarget.Target())),
							new Decorator(ctx => !Me.IsSafelyFacing(attackTarget) || !IronShredder.IsSafelyFacing(attackTarget),
								new ActionFail(ctx => attackTarget.Face())),

							// cast 'Death From Above' ability on targets outside of melee
							new Decorator(
								ctx =>
									IronShredder.Location.DistanceSqr(attackTarget.Location) > 10*10 && IronShredder.Location.DistanceSqr(attackTarget.Location) < 70 * 70 && DeathFromAboveTimer.IsFinished,
								new Sequence(
									new Action(ctx => Lua.DoString("CastPetAction(2)")),
									new WaitContinue(2, ctx => StyxWoW.Me.CurrentPendingCursorSpell != null, new ActionAlwaysSucceed()),
									new Action(ctx => SpellManager.ClickRemoteLocation(attackTarget.Location)),
									new Action(ctx => DeathFromAboveTimer.Reset()))),

							// cast 'Elecrostatic Distortion' ability on melee range target.
							new Decorator(
								ctx => IronShredder.Location.DistanceSqr(attackTarget.Location) <= 25*25,
								new PrioritySelector(
									new Decorator(
										ctx => IronShredder.Location.DistanceSqr(attackTarget.Location) <= 25* 25 && (Me.IsMoving || Me.CharmedUnit.IsMoving),
										new ActionFail(ctx => WoWMovement.ClickToMove(Me.CharmedUnit.Location))),
									new Action(ctx => Lua.DoString("CastPetAction(1)")))),
							new Decorator(ctx => IronShredder.Location.DistanceSqr(attackTarget.Location) > 25 * 25,
								new Action(ctx => Navigator.MoveTo(attackTarget.Location))))),
					new Decorator(
						ctx => attackTarget == null,
						new PrioritySelector(
							new Decorator(
								ctx => IronShredder.Location.DistanceSqr(WaitPoint) > 10 * 10,
								new PrioritySelector(
									// can't set path precision so I'll just handle it directly...
									// the IronShredder takes wide turns so needs a higher path precision than normal
									new Decorator(
										ctx =>
										{
											var nav = Navigator.NavigationProvider as MeshNavigator;
											if (nav == null)
												return false;
											if (nav.CurrentMovePath == null || nav.CurrentMovePath.Index >= nav.CurrentMovePath.Path.Points.Length)
												return false;
											WoWPoint point = nav.CurrentMovePath.Path.Points[nav.CurrentMovePath.Index];
											return point.DistanceSqr(IronShredder.Location) < 6 * 6;
										},
										new Action(ctx => ((MeshNavigator) Navigator.NavigationProvider).CurrentMovePath.Index++)),
									
									CreateBehavior_Antistuck(),

									new Action(ctx => Navigator.MoveTo(WaitPoint)))),
							new ActionSetActivity("No viable targets, waiting."))),
					new ActionAlwaysSucceed()));
		}

		public Composite CreateBehavior_GetIn()
		{
			return new Decorator(r => !Query.IsInVehicle() && !Me.IsActuallyInCombat,
				new PrioritySelector(
					new Decorator(r => IronShredder != null,
						new Action(r =>
						{
							IronShredder.Interact();
						})),
					new Decorator(r => IronShredder == null, new Action(r => Navigator.MoveTo(StartPoint)))));
		}

		private WoWUnit GetAttackTarget()
		{
			var target = Me.CurrentTarget;
			if (target != null && target.IsHostile && target.Attackable && target.IsAlive && target.DistanceSqr < 25 * 25)
			{
				return target;
			}

			var charmedUnit = Me.CharmedUnit;

			var nearestHostileAttackingPlayer =
				ObjectManager.GetObjectsOfType<WoWUnit>()
					.Where(r => r.Entry != 75721 && r.GotTarget && r.CurrentTarget == charmedUnit)
					.OrderBy(r => r.DistanceSqr)
					.FirstOrDefault();

			if (nearestHostileAttackingPlayer != null)
			{
				return nearestHostileAttackingPlayer;
			}
			return GromKars.FirstOrDefault();
		}

		public override void OnStart()
		{
			OnStart_HandleAttributeProblem();
			if (!IsDone)
			{
				TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_MainCombat());
				_useMount = CharacterSettings.Instance.UseMount;
				CharacterSettings.Instance.UseMount = false;

				this.UpdateGoalText(QuestId);
			}
		}


		protected Composite CreateBehavior_MainCombat()
		{
			return _root ?? (_root = 
				new Decorator(ret => !_isBehaviorDone,
					new PrioritySelector(
						CreateBehavior_CheckCompletion(), 
						CreateBehavior_GetIn(), 
						CreateBehavior_KillMantid())));
		}

		#region StuckHandler

		readonly WaitTimer _stuckTimer = new WaitTimer(TimeSpan.FromSeconds(2));

		protected Composite CreateBehavior_Antistuck()
		{
			var prevPosition = WoWPoint.Empty;
			WoWPoint myLoc = WoWPoint.Empty;
			var moveDirection = WoWMovement.MovementDirection.None;

			return new PrioritySelector(
				new Decorator(
					ctx => _stuckTimer.IsFinished,
					new Sequence(
						ctx => myLoc = WoWMovement.ActiveMover.Location,
						// checks if stuck
						new DecoratorContinue(
							ctx => myLoc.DistanceSqr(prevPosition) < 3 * 3,
							new Sequence(         
										ctx => moveDirection = GetRandomMovementDirection(),
										new Action(ctx => QBCLog.Debug("Stuck. Movement Directions: {0}", moveDirection)),
										new Action(ctx => WoWMovement.Move(moveDirection)),
										new WaitContinue(2, ctx => false, new ActionAlwaysSucceed()),
										new Action(ctx => WoWMovement.MoveStop(moveDirection)))),

						new Action(ctx => prevPosition = myLoc),
						new Action(ctx => _stuckTimer.Reset()))));
		}

		private WoWMovement.MovementDirection GetRandomMovementDirection()
		{
			// randomly move left or ritht
			WoWMovement.MovementDirection ret = StyxWoW.Random.Next(2) == 0
				? WoWMovement.MovementDirection.StrafeLeft
				: WoWMovement.MovementDirection.StrafeRight;

			// randomly choose to go diagonal backwords + left or right
			if (StyxWoW.Random.Next(2) == 0)
				ret |= WoWMovement.MovementDirection.Backwards;

			// randomly choose to jump (or descend if flying or swimming)
			if (StyxWoW.Random.Next(2) == 0)
			{
				var activeMover = WoWMovement.ActiveMover;
				if (activeMover.IsFlying || activeMover.IsSwimming)
				{
					ret |= StyxWoW.Random.Next(2) == 0
						? WoWMovement.MovementDirection.JumpAscend
						: WoWMovement.MovementDirection.Descend;
				}
				else
				{
					ret |= WoWMovement.MovementDirection.JumpAscend;
				}
			}
			return ret;
		}
		#endregion

	}
}