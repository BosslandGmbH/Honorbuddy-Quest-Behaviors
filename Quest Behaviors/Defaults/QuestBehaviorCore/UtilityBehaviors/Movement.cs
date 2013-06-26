// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Usings
using System;
using System.Collections.Generic;
using System.Linq;

using CommonBehaviors.Actions;
using Styx;
using Styx.CommonBot;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public static partial class UtilityBehaviorPS
    {
        public class MoveStop : PrioritySelector
        {
            // 29Apr2013-05:20UTC chinajade
            public MoveStop()
            {
                Children = CreateChildren();
            }

            
            // 29Apr2013-05:20UTC chinajade
            private List<Composite> CreateChildren()
            {
                return new List<Composite>()
                {
                    new Decorator(context => Utility.MovementObserver.IsMoving,
                        new Sequence(
                            new Action(context => { Navigator.PlayerMover.MoveStop(); }),
                            new Wait(Delay.LagDuration, context => Utility.MovementObserver.IsMoving, new ActionAlwaysSucceed())
                        ))
                };
            }
        }
    }


    public static partial class UtilityBehaviorPS
    {
        public class MoveTo : PrioritySelector
        {
            // 22Apr2013-12:45UTC chinajade
            public MoveTo(ProvideHuntingGroundsDelegate huntingGroundsProvider,
                            ProvideMovementByDelegate movementByDelegate,
                            CanRunDecoratorDelegate suppressMountUse = null)
            {
                Contract.Requires(huntingGroundsProvider != null, context => "huntingGroundsProvider may not be null");
                Contract.Requires(movementByDelegate != null, context => "movementByDelegate may not be null");

                DestinationDelegate = (context => huntingGroundsProvider(context).CurrentWaypoint().Location);
                DestinationNameDelegate = (context => string.Format("hunting ground waypoint '{0}'",
                                                                    huntingGroundsProvider(context).CurrentWaypoint().Name));
                MovementByDelegate = movementByDelegate ?? (context => MovementByType.FlightorPreferred);
                PrecisionDelegate = (context => Navigator.PathPrecision);
                SuppressMountUse = suppressMountUse ?? (context => false);
                LocationObserver = (context => Utility.MovementObserver.Location);

                Children = CreateChildren();
            }


            // 24Feb2013-08:11UTC chinajade
            public MoveTo(ProvideWoWPointDelegate destinationDelegate,
                            ProvideStringDelegate destinationNameDelegate,
                            ProvideMovementByDelegate movementByDelegate,
                            ProvideDoubleDelegate precisionDelegate = null,
                            CanRunDecoratorDelegate suppressMountUse = null,
                            ProvideWoWPointDelegate locationObserver = null)
            {
                Contract.Requires(destinationDelegate != null, context => "destinationDelegate may not be null");
                Contract.Requires(destinationNameDelegate != null, context => "destinationNameDelegate may not be null");
                Contract.Requires(movementByDelegate != null, context => "movementByDelegate may not be null");

                DestinationDelegate = destinationDelegate;
                DestinationNameDelegate = destinationNameDelegate;
                MovementByDelegate = movementByDelegate ?? (context => MovementByType.FlightorPreferred);
                PrecisionDelegate = precisionDelegate ?? (context => Navigator.PathPrecision);
                SuppressMountUse = suppressMountUse ?? (context => false);
                LocationObserver = locationObserver ?? (context => Utility.MovementObserver.Location);

                Children = CreateChildren();
            }


            // BT contruction-time properties...
            private ProvideWoWPointDelegate DestinationDelegate { get; set;  }
            private ProvideStringDelegate DestinationNameDelegate { get; set; }
            private ProvideWoWPointDelegate LocationObserver { get; set; }
            private ProvideMovementByDelegate MovementByDelegate { get; set; }
            private ProvideDoubleDelegate PrecisionDelegate { get; set; }
            private CanRunDecoratorDelegate SuppressMountUse { get; set; }

            // BT visit-time properties...
            private WoWPoint CachedDestination { get; set; }
            private MovementByType CachedMovementBy { get; set; }


            private List<Composite> CreateChildren()
            {
                return new List<Composite>()
                {
                    new Decorator(context => MovementByDelegate(context) != MovementByType.None,
                        new PrioritySelector(
                            new ActionFail(context =>
                            {
                                CachedDestination = DestinationDelegate(context);
                                CachedMovementBy = MovementByDelegate(context);
                            }),
                            new UtilityBehaviorPS.MountAsNeeded(DestinationDelegate, SuppressMountUse),

                            new Decorator(context => (LocationObserver(context).Distance(CachedDestination) > PrecisionDelegate(context)),
                                new Sequence(
                                    new CompositeThrottleContinue(TimeSpan.FromMilliseconds(1000),
                                        new Action(context =>
                                        {
                                            TreeRoot.StatusText =
                                                "Moving to " + (DestinationNameDelegate(context) ?? CachedDestination.ToString());
                                        })),

                                    new CompositeThrottleContinue(Throttle.WoWClientMovement,
                                        new Action(context =>
                                        {
                                            var moveResult = MoveResult.Failed;

                                            // Use Flightor, if allowed...
                                            if ((CachedMovementBy == MovementByType.FlightorPreferred) && Me.IsOutdoors && Me.MovementInfo.CanFly)
                                            {
                                                var immediateDestination = CachedDestination.FindFlightorUsableLocation();

                                                if (immediateDestination == default(WoWPoint))
                                                    { moveResult = MoveResult.Failed; }

                                                else if (Me.Location.Distance(immediateDestination) > Navigator.PathPrecision)
                                                {
                                                    // <sigh> Its simply a crime that Flightor doesn't implement the INavigationProvider interface...
                                                    Flightor.MoveTo(immediateDestination, 15.0f);
                                                    moveResult = MoveResult.Moved;
                                                }

                                                else if (Me.IsFlying)
                                                {
                                                    WoWMovement.Move(WoWMovement.MovementDirection.Descend, TimeSpan.FromMilliseconds(400));
                                                    moveResult = MoveResult.Moved;
                                                }
                                            }

                                            // Use Navigator to get there, if allowed...
                                            if ((CachedMovementBy == MovementByType.NavigatorPreferred)
                                                || (CachedMovementBy == MovementByType.NavigatorOnly)
                                                || (moveResult == MoveResult.Failed))
                                            {
                                                moveResult = Navigator.MoveTo(CachedDestination);
                                            }

                                            // If Navigator couldn't move us, resort to click-to-move if allowed...
                                            if (!((moveResult == MoveResult.Moved)
                                                    || (moveResult == MoveResult.ReachedDestination)
                                                    || (moveResult == MoveResult.PathGenerated)))
                                            {
                                                if (CachedMovementBy == MovementByType.NavigatorOnly)
                                                {
                                                    QBCLog.Warning("Failed to mesh move--is area unmeshed? Or, are we flying or swimming?");
                                                    return RunStatus.Failure;
                                                }

                                                WoWMovement.ClickToMove(CachedDestination);
                                            }

                                            return RunStatus.Success;
                                        }))
                                ))
                            ))
                    };
            }

        }
    }


    public static partial class UtilityBehaviorPS
    {
        public class NoMobsAtCurrentWaypoint : PrioritySelector
        {
            public NoMobsAtCurrentWaypoint(ProvideHuntingGroundsDelegate huntingGroundsProvider,
                                            ProvideMovementByDelegate movementByDelegate,
                                            Action<object> terminateBehaviorIfNoTargetsProvider = null,
                                            Func<object, IEnumerable<string>> huntedMobNamesProvider = null,
                                            ProvideStringDelegate huntedMobExclusions = null)
            {
                Contract.Requires(huntingGroundsProvider != null, context => "huntingGroundsProvider may not be null");
                Contract.Requires(movementByDelegate != null, context => "movementByDelegate may not be null");

                HuntingGroundsProvider = huntingGroundsProvider;
                MovementByDelegate = movementByDelegate ?? (context => MovementByType.FlightorPreferred);
                TerminateBehaviorIfNoTargetsProvider = terminateBehaviorIfNoTargetsProvider;
                HuntedMobNamesProvider = huntedMobNamesProvider ?? (context => Enumerable.Empty<string>());
                HuntedMobExclusions = huntedMobExclusions ?? (context => string.Empty);

                Children = CreateChildren();
            }


            // BT contruction-time properties...
            private ProvideHuntingGroundsDelegate HuntingGroundsProvider { get; set; }
            private ProvideStringDelegate HuntedMobExclusions { get; set; }
            private Func<object, IEnumerable<string>> HuntedMobNamesProvider { get; set; }
            private ProvideMovementByDelegate MovementByDelegate { get; set; }
            private Action<object> TerminateBehaviorIfNoTargetsProvider { get; set; }


            // 22Apr2013-01:15UTC chinajade
            private List<Composite> CreateChildren()
            {
                return new List<Composite>()
                {
                    // Move to next hunting ground waypoint...
                    new UtilityBehaviorPS.MoveTo(HuntingGroundsProvider, MovementByDelegate),

                    // Terminate of no targets available?
                    new Decorator(context => TerminateBehaviorIfNoTargetsProvider != null,
                        new Action(context =>
                        {
                            string message = "No mobs in area--terminating due to WaitForNpcs=\"false\"";
                            TreeRoot.StatusText = message;

                            // Show excluded units before terminating.  This aids in profile debugging if WaitForNpcs="false"...
                            string excludedUnitReasons = HuntedMobExclusions(context);
                            if (!string.IsNullOrEmpty(excludedUnitReasons))
                            {
                                message += excludedUnitReasons;
                                QBCLog.DeveloperInfo("{0}", message);
                            }
                            TerminateBehaviorIfNoTargetsProvider(context);
                        })),

                    // Only one hunting ground waypoint to move to?
                    new CompositeThrottle(context => HuntingGroundsProvider(context).Waypoints.Count() <= 1,
                        TimeSpan.FromSeconds(30),
                        new Action(context =>
                        {
                            string message = "Waiting for respawn";

                            if (HuntedMobNamesProvider(context).Any())
                            {
                                message += " of ";
                                message += string.Join(", ", HuntedMobNamesProvider(context));
                            }

                            TreeRoot.StatusText = message;

                            string excludedUnitReasons = HuntedMobExclusions(context);
                            if (!string.IsNullOrEmpty((excludedUnitReasons)))
                            {
                                message += excludedUnitReasons;
                                QBCLog.DeveloperInfo("{0}", message);
                            }
                        }))
                };
            }
        }
    }
}