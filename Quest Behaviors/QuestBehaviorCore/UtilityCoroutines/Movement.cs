using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Buddy.Coroutines;
using Honorbuddy.QuestBehaviorCore.XmlElements;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.CommonBot.Coroutines;
using Action = Styx.TreeSharp.Action;

namespace Honorbuddy.QuestBehaviorCore
{
	public static partial class UtilityCoroutine
	{
		private static WaitTimer _moveToMessageThrottle = null;

		private static bool IsMoveToMessageThrottled
		{
			get
			{
				if (_moveToMessageThrottle == null)
					_moveToMessageThrottle = new WaitTimer(TimeSpan.FromMilliseconds(1000));

				if (!_moveToMessageThrottle.IsFinished)
					return true;
				_moveToMessageThrottle.Reset();
				return false;
			}
		}

		public static IEnumerator MoveStop()
		{
			Navigator.PlayerMover.MoveStop();
			yield return StyxCoroutine.Wait((int) Delay.LagDuration.TotalMilliseconds, () => !WoWMovement.ActiveMover.IsMoving);
		}

		public static IEnumerator MoveTo(
			HuntingGroundsType huntingGrounds,
			MovementByType movementBy = MovementByType.FlightorPreferred)
		{
			Contract.Requires(huntingGrounds != null, context => "huntingGrounds may not be null");
			var destination = huntingGrounds.CurrentWaypoint().Location;
			var destinationName = String.Format("hunting ground waypoint '{0}'", huntingGrounds.CurrentWaypoint().Name);
			yield return MoveTo(destination, destinationName, movementBy);
		}

		public static IEnumerator MoveTo(
			WoWPoint destination,
			string destinationName,
			MovementByType movementBy = MovementByType.FlightorPreferred)
		{
			Contract.Requires(destinationName != null, context => "destinationName may not be null");

			if (movementBy == MovementByType.None)
			{
				yield return false;
				yield break;
			}

			var activeMover = WoWMovement.ActiveMover;
			if (activeMover == null)
			{
				yield return false;
				yield break;
			}

			if (!IsMoveToMessageThrottled)
			{
				if (destinationName == null)
					destinationName = destination.ToString();
				TreeRoot.StatusText = "Moving to " + destinationName;
			}

			if (!Navigator.AtLocation(destination))
			{
				switch (movementBy)
				{
					case MovementByType.FlightorPreferred:
						yield return TryFlightor(destination);
						if ((bool)Coroutine.Current.SubRoutineResult)
							yield break;

						yield return TryNavigator(destination, destinationName);
						if ((bool)Coroutine.Current.SubRoutineResult)
							yield break;

						yield return TryClickToMove(destination, NavType.Fly);
						if ((bool)Coroutine.Current.SubRoutineResult)
							yield break;
						break;
					case MovementByType.NavigatorPreferred:
						yield return TryNavigator(destination, destinationName);
						if ((bool)Coroutine.Current.SubRoutineResult)
							yield break;

						yield return TryClickToMove(destination, NavType.Run);
						if ((bool)Coroutine.Current.SubRoutineResult)
							yield break;
						break;
					case MovementByType.NavigatorOnly:
						yield return TryNavigator(destination, destinationName);
						if ((bool)Coroutine.Current.SubRoutineResult)
							yield break;
						break;
					case MovementByType.ClickToMoveOnly:
						var navType = activeMover.MovementInfo.CanFly ? NavType.Fly : NavType.Run;
						yield return TryClickToMove(destination,  navType);
						if ((bool)Coroutine.Current.SubRoutineResult)
							yield break;
						break;
					case MovementByType.None:
						break;
					default:
						QBCLog.MaintenanceError("Unhandled MovementByType of {0}", movementBy);
						break;
				}
			}
			yield return false;
		}

		private static IEnumerator SetMountState(WoWPoint destination, NavType navType = NavType.Fly)
		{
			// Are we mounted, and not supposed to be?
			if (!Mount.UseMount && Me.IsMounted())
			{
				yield return ExecuteMountStrategy(MountStrategyType.Dismount);
				if ((bool)Coroutine.Current.SubRoutineResult)
					yield break;
			}
			// Are we unmounted, and mount use is permitted?
			// NB: We don't check for IsMounted(), in case the ExecuteMountStrategy decides a mount switch is necessary
			// (based on NavType).
			if (Mount.UseMount && destination.CollectionDistance(WoWMovement.ActiveMover.Location) > CharacterSettings.Instance.MountDistance)
			{
				yield return ExecuteMountStrategy(MountStrategyType.Mount, navType);
			}
			yield return false;
		}


		private static IEnumerator TryClickToMove(
			WoWPoint destination,
			NavType navType = NavType.Fly)
		{
			var activeMover = WoWMovement.ActiveMover;
			// NB: Do not 'dismount' for CtM.  We may be using it for aerial navigation, also.
			yield return SetMountState(destination,  navType);
			if ((bool)Coroutine.Current.SubRoutineResult)
				yield break;

			// If Navigator can generate a parital path for us, take advantage of it...
			var tempDestination =
				Navigator.GeneratePath(activeMover.Location, destination)
					.Where(p => !Navigator.AtLocation(p))
					.DefaultIfEmpty(destination)
					.FirstOrDefault();

			WoWMovement.ClickToMove(tempDestination);
			yield return true;
		}


		private static IEnumerator TryFlightor(WoWPoint destination)
		{
			// If a toon can't fly, skip this...
			// NB: Although Flightor will fall back to Navigator, there are side-effects.
			// Flightor will mount even if UseMount is disabled.  So, if we don't want to mount
			// we don't want to even try Flightor; otherwise, unexpected side-effects can ensue.
			var activeMover = WoWMovement.ActiveMover;
			if (Mount.UseMount || activeMover.IsSwimming
				|| !Navigator.CanNavigateWithin(activeMover.Location, destination, 5))
			{
				Flightor.MoveTo(destination, 15.0f, true);
				yield return true;
				yield break;
			}
			yield return false;
		}


		private static IEnumerator TryNavigator(WoWPoint destination,  string destinationName = null)
		{
			var activeMover = WoWMovement.ActiveMover;
			// If we can navigate to destination, use navigator...
			if (Navigator.CanNavigateFully(activeMover.Location, destination))
			{
				var moveResult = Navigator.MoveTo(destination);
				if (Navigator.GetRunStatusFromMoveResult(moveResult) == RunStatus.Success)
				{
					yield return true;
					yield break;
				}
			}
			if (destinationName == null)
				destinationName = destination.ToString();
			QBCLog.DeveloperInfo(
				"Navigator unable to move from {0} to destination({1}, {2}) on ground --try MovementBy=\"FlightorPreferred\".",
				activeMover.Location,
				destinationName,
				destination.ToString());
			yield return false;
		}


		public class NoMobsAtCurrentWaypoint : CoroutineTask
		{
			private ThrottleCoroutineTask _messageThrottle;

			public NoMobsAtCurrentWaypoint(
				Func<HuntingGroundsType> huntingGroundsProvider,
				Func<MovementByType> movementByDelegate,
				System.Action terminateBehaviorIfNoTargetsProvider = null,
				Func<string> huntedMobExclusions = null)
			{
				Contract.Requires(huntingGroundsProvider != null, context => "huntingGroundsProvider may not be null");
				Contract.Requires(movementByDelegate != null, context => "movementByDelegate may not be null");

				HuntingGroundsProvider = huntingGroundsProvider;
				MovementByDelegate = movementByDelegate ?? (() => MovementByType.FlightorPreferred);
				TerminateBehaviorIfNoTargetsProvider = terminateBehaviorIfNoTargetsProvider;
				HuntedMobExclusions = huntedMobExclusions ?? (() => String.Empty);
			}

			// BT contruction-time properties...
			private Func<HuntingGroundsType> HuntingGroundsProvider { get; set; }
			private Func<string> HuntedMobExclusions { get; set; }
			private Func<MovementByType> MovementByDelegate { get; set; }
			private System.Action TerminateBehaviorIfNoTargetsProvider { get; set; }

			protected override IEnumerator Run()
			{
				// Move to next hunting ground waypoint...
				yield return MoveTo(HuntingGroundsProvider(), MovementByDelegate());
				if ((bool)Coroutine.Current.SubRoutineResult)
					yield break;

				// Only one hunting ground waypoint to move to?
				yield return _messageThrottle ?? (_messageThrottle = new ThrottleCoroutineTask(TimeSpan.FromSeconds(10), LogMessage));

				// Terminate of no targets available?
				if (TerminateBehaviorIfNoTargetsProvider != null)
					TerminateBehaviorIfNoTargetsProvider();

				yield return false;
			}


			private IEnumerator LogMessage()
			{
				string message = "No viable mobs in area.";
				TreeRoot.StatusText = message;

				// Show excluded units before terminating.  This aids in profile debugging if WaitForNpcs="false"...
				string excludedUnitReasons = HuntedMobExclusions();
				if (!String.IsNullOrEmpty(excludedUnitReasons))
				{
					message += excludedUnitReasons;
					QBCLog.DeveloperInfo("{0}", message);
				}
				yield return false;
			}
		}
	}
}