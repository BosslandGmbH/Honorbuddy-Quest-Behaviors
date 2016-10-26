#region Usings

using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

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
using Styx.WoWInternals.WoWObjects;
using Styx.Common;

#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public static partial class UtilityCoroutine
    {
        private static WaitTimer s_moveToMessageThrottle = null;

        private static bool IsMoveToMessageThrottled
        {
            get
            {
                if (s_moveToMessageThrottle == null)
                    s_moveToMessageThrottle = new WaitTimer(TimeSpan.FromMilliseconds(1000));

                if (!s_moveToMessageThrottle.IsFinished)
                    return true;
                s_moveToMessageThrottle.Reset();
                return false;
            }
        }

        [Obsolete("Use CommonCoroutines.StopMoving() instead")]
        public static async Task MoveStop()
        {
            if (!(WoWMovement.ActiveMover ?? Me).IsMoving)
                return;

            Navigator.PlayerMover.MoveStop();
            await Coroutine.Wait((int)Delay.LagDuration.TotalMilliseconds * 2 + 50, () => !WoWMovement.ActiveMover.IsMoving);
        }

        public static async Task<bool> MoveTo(
            HuntingGroundsType huntingGrounds,
            MovementByType movementBy = MovementByType.FlightorPreferred,
            float? distanceTolerance = null)
        {
            Contract.Requires(huntingGrounds != null, context => "huntingGrounds may not be null");
            var destination = huntingGrounds.CurrentWaypoint().Location;
            var destinationName = String.Format("hunting ground waypoint '{0}'", huntingGrounds.CurrentWaypoint().Name);
            return await MoveTo(destination, destinationName, movementBy, distanceTolerance);
        }

        public static async Task<bool> MoveTo(
            Vector3 destination,
            string destinationName,
            MovementByType movementBy = MovementByType.FlightorPreferred,
            float? distanceTolerance = null)
        {
            Contract.Requires(destinationName != null, context => "destinationName may not be null");

            if (movementBy == MovementByType.None)
            {
                return false;
            }

            var activeMover = WoWMovement.ActiveMover;
            if (activeMover == null)
            {
                return false;
            }

            if (!IsMoveToMessageThrottled)
            {
                if (string.IsNullOrEmpty(destinationName))
                    destinationName = destination.ToString();
                TreeRoot.StatusText = "Moving to " + destinationName;
            }

            switch (movementBy)
            {
                case MovementByType.FlightorPreferred:
                    if (await TryFlightor(destination, distanceTolerance))
                        return true;

                    if (await TryNavigator(destination, distanceTolerance, destinationName))
                        return true;

                    if (await TryClickToMove(destination, NavType.Fly))
                        return true;
                    break;
                case MovementByType.NavigatorPreferred:
                    if (await TryNavigator(destination, distanceTolerance, destinationName))
                        return true;

                    if (await TryClickToMove(destination, NavType.Run))
                        return true;
                    break;
                case MovementByType.NavigatorOnly:
                    if (await TryNavigator(destination, distanceTolerance, destinationName))
                        return true;
                    break;
                case MovementByType.ClickToMoveOnly:
                    var navType = activeMover.MovementInfo.CanFly ? NavType.Fly : NavType.Run;
                    if (await TryClickToMove(destination, navType))
                        return true;
                    break;
                case MovementByType.None:
                    break;
                default:
                    QBCLog.MaintenanceError("Unhandled MovementByType of {0}", movementBy);
                    break;
            }
            return false;
        }

        private static async Task<bool> SetMountState(Vector3 destination, NavType navType = NavType.Fly)
        {
            // Are we mounted, and not supposed to be?
            if (!Mount.UseMount && Me.IsMounted())
            {
                if (await ExecuteMountStrategy(MountStrategyType.Dismount))
                    return true;
            }
            // Are we unmounted, and mount use is permitted?
            // NB: We don't check for IsMounted(), in case the ExecuteMountStrategy decides a mount switch is necessary
            // (based on NavType).
            // Also use euclidean distance -- this is fine as it is more human like than mesh distance (a human will eye-ball
            // whether to use the mount).
            if (Mount.UseMount && destination.Distance(WoWMovement.ActiveMover.Location) > 60)
            {
                if (await ExecuteMountStrategy(MountStrategyType.Mount, navType))
                    return true;
            }
            return false;
        }

        private static async Task<bool> TryClickToMove(
            Vector3 destination,
            NavType navType = NavType.Fly)
        {
            // NB: Do not 'dismount' for CtM.  We may be using it for aerial navigation, also.
            if (await SetMountState(destination, navType))
                return true;

            WoWMovement.ClickToMove(destination);
            return true;
        }

        private static async Task<bool> TryFlightor(Vector3 destination, float? distanceTolerance)
        {
            // If a toon can't fly, skip this...
            // NB: Although Flightor will fall back to Navigator, there are side-effects.
            // Flightor will mount even if UseMount is disabled.  So, if we don't want to mount
            // we don't want to even try Flightor; otherwise, unexpected side-effects can ensue.

            // TODO: There used to be a CanNavigateWithin check here, which is hard to replace.
            // Possibly make flightor's MoveTo return something to indicate whether it fails.

            FlyToParameters flyToParams = new FlyToParameters(destination)
            {
                MinHeight = 15f,
                CheckIndoors = true,
            };

            if (distanceTolerance.HasValue)
                flyToParams.GroundNavParameters.DistanceTolerance = distanceTolerance.Value;

            Flightor.MoveTo(flyToParams);
            return true;
        }


        private static async Task<bool> TryNavigator(Vector3 destination, float? distanceTolerance, string destinationName = null)
        {
            MoveToParameters moveToParams = new MoveToParameters(destination);

            if (distanceTolerance.HasValue)
                moveToParams.DistanceTolerance = distanceTolerance.Value;

            // If we can navigate to destination, use navigator...
            var moveResult = Navigator.MoveTo(moveToParams);
            if (moveResult.IsSuccessful())
                return true;

            // Make sure we are on ground if navigation failed.
            if (StyxWoW.Me.IsFlying)
            {
                if (await CommonCoroutines.LandAndDismount("For ground navigation"))
                    return true;
            }

            if (destinationName == null)
                destinationName = destination.ToString();
            QBCLog.DeveloperInfo(
                "Navigator unable to move from {0} to destination({1}, {2}) on ground --try MovementBy=\"FlightorPreferred\".",
                WoWMovement.ActiveMover.Location,
                destinationName,
                destination.ToString());
            return false;
        }


        public class NoMobsAtCurrentWaypoint : CoroutineTask<bool>
        {
            private ThrottleCoroutineTask _messageThrottle;
            private readonly float? _distanceTolerance;

            public NoMobsAtCurrentWaypoint(
                Func<HuntingGroundsType> huntingGroundsProvider,
                Func<MovementByType> movementByDelegate,
                float? distanceTolerance,
                System.Action terminateBehaviorIfNoTargetsProvider = null,
                Func<string> huntedMobExclusions = null)
            {
                Contract.Requires(huntingGroundsProvider != null, context => "huntingGroundsProvider may not be null");
                Contract.Requires(movementByDelegate != null, context => "movementByDelegate may not be null");

                HuntingGroundsProvider = huntingGroundsProvider;
                MovementByDelegate = movementByDelegate ?? (() => MovementByType.FlightorPreferred);
                _distanceTolerance = distanceTolerance;
                TerminateBehaviorIfNoTargetsProvider = terminateBehaviorIfNoTargetsProvider;
                HuntedMobExclusions = huntedMobExclusions ?? (() => String.Empty);
            }

            // BT contruction-time properties...
            private Func<HuntingGroundsType> HuntingGroundsProvider { get; set; }
            private Func<string> HuntedMobExclusions { get; set; }
            private Func<MovementByType> MovementByDelegate { get; set; }
            private System.Action TerminateBehaviorIfNoTargetsProvider { get; set; }

            public override async Task<bool> Run()
            {
                var huntingGrounds = HuntingGroundsProvider();

                // Only one hunting ground waypoint to move to and at that waywpoint?
                if (huntingGrounds.Waypoints.Count == 1 && huntingGrounds.CurrentWaypoint().AtLocation(WoWMovement.ActiveMover.Location))
                {
                    await (_messageThrottle ?? (_messageThrottle = new ThrottleCoroutineTask(TimeSpan.FromSeconds(10), LogMessage)));

                    // Terminate of no targets available?
                    TerminateBehaviorIfNoTargetsProvider?.Invoke();
                    return false;
                }

                // Move to next hunting ground waypoint...
                if (await MoveTo(huntingGrounds, MovementByDelegate(), _distanceTolerance))
                    return true;


                return false;
            }


            private async Task<bool> LogMessage()
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
                return false;
            }
        }
    }
}
