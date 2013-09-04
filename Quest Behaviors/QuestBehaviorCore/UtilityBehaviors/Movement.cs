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
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;

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
                            ProvideMovementByDelegate movementByDelegate = null,
                            CanRunDecoratorDelegate suppressMountUse = null)
            {
                Contract.Requires(huntingGroundsProvider != null, context => "huntingGroundsProvider may not be null");

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
                            ProvideMovementByDelegate movementByDelegate = null,
                            ProvideDoubleDelegate precisionDelegate = null,
                            CanRunDecoratorDelegate suppressMountUse = null,
                            ProvideWoWPointDelegate locationObserver = null)
            {
                Contract.Requires(destinationDelegate != null, context => "destinationDelegate may not be null");
                Contract.Requires(destinationNameDelegate != null, context => "destinationNameDelegate may not be null");

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
            private string CachedDestinationName { get; set; }
            private MovementByType CachedMovementBy { get; set; }
            private WaitTimer _messageThrottle = null;


            private List<Composite> CreateChildren()
            {
                return new List<Composite>()
                {
                    new Decorator(context => MovementByDelegate(context) != MovementByType.None,
                        new PrioritySelector(
                            new ActionFail(context =>
                            {
                                CachedDestination = DestinationDelegate(context);
                                CachedDestinationName = DestinationNameDelegate(context) ?? CachedDestination.ToString();
                                CachedMovementBy = MovementByDelegate(context);

                                if ((_messageThrottle == null) || _messageThrottle.IsFinished)
                                {
                                    if (_messageThrottle == null)
                                        { _messageThrottle = new WaitTimer(TimeSpan.FromMilliseconds(1000)); }

                                    TreeRoot.StatusText = "Moving to " + CachedDestinationName;
                                    _messageThrottle.Reset();
                                }                            
                            }),

                            new Decorator(context => (CachedDestination.CollectionDistance(LocationObserver(context)) > PrecisionDelegate(context)),
                                new PrioritySelector(
                                    new Switch<MovementByType>(context => CachedMovementBy,
                                        // default
                                        new Action(context => QBCLog.MaintenanceError("Unhandled MovementByType of {0}", CachedMovementBy)),

                                        new SwitchArgument<MovementByType>(MovementByType.FlightorPreferred,
                                            new PrioritySelector(
                                                TryFlightor(),
                                                TryNavigator(),
                                                TryClickToMove(context => NavType.Fly)
                                            )),

                                        new SwitchArgument<MovementByType>(MovementByType.NavigatorPreferred,
                                            new PrioritySelector(
                                                TryNavigator(),
                                                TryClickToMove(context => NavType.Run)
                                            )),

                                        new SwitchArgument<MovementByType>(MovementByType.NavigatorOnly,
                                            new PrioritySelector(
                                                TryNavigator()
                                            )),

                                        new SwitchArgument<MovementByType>(MovementByType.ClickToMoveOnly,
                                            new PrioritySelector(
                                                // NB: CtM can be used for either flying or ground, so we must assume 'fly'...
                                                TryClickToMove(context => NavType.Fly)
                                            )),

                                        new SwitchArgument<MovementByType>(MovementByType.None,
                                            new PrioritySelector(
                                                // empty
                                            ))
                                    ),

                                    new Decorator(context => CachedMovementBy != MovementByType.None,
                                        new Action(context => QBCLog.Warning("Unable to reach destination({0})", CachedDestination)))
                                ))
                        ))
                    };
            }


            private Composite SetMountState(CanRunDecoratorDelegate extraWantToMountQualifier = null,
                                            ProvideNavTypeDelegate navTypeDelegate = null)
            {
                extraWantToMountQualifier = extraWantToMountQualifier ?? (context => false);
                navTypeDelegate = navTypeDelegate ?? (context => NavType.Fly);

                return new PrioritySelector(
                    // Are we mounted, and not supposed to be?
                    new Decorator(context => SuppressMountUse(context) && Me.IsMounted(),
                        new UtilityBehaviorPS.DescendForDismount(context => "Request for 'no mount use'.")),

                    // Are we unmounted, and mount use is permitted?
                    // NB: We don't check for IsMounted(), in case the ExecuteMountStrategy decides a mount switch is necessary
                    // (based on NavType).
                    new Decorator(context => !SuppressMountUse(context)
                                            && (extraWantToMountQualifier(context)
                                                || (CachedDestination.CollectionDistance(LocationObserver(context)) > CharacterSettings.Instance.MountDistance)),
                        new UtilityBehaviorPS.ExecuteMountStrategy(context => MountStrategyType.Mount, navTypeDelegate))
                );
            }


            private Composite TryClickToMove(ProvideNavTypeDelegate navTypeDelegate)
            {
                navTypeDelegate = navTypeDelegate ?? (context => NavType.Fly);

                return new PrioritySelector(
                    // NB: Do not 'dismount' for CtM.  We may be using it for aerial navigation, also.

                    // If Navigator can generate a parital path for us, take advantage of it...
                    SetMountState(null, navTypeDelegate),
                    new Action(context =>
                    {
                        var precision = PrecisionDelegate(context);
                        var tempDestination =
                            Navigator.GeneratePath(LocationObserver(context), CachedDestination)
                            .Where(p => LocationObserver(context).Distance(p) > precision)
                            .DefaultIfEmpty(CachedDestination)
                            .FirstOrDefault();

                        WoWMovement.ClickToMove(tempDestination);
                    })
                );
            }


            private Composite TryFlightor()
            {
                var jumpAscendTime = TimeSpan.FromMilliseconds(250);

                return new PrioritySelector(
                    // NB: On unevent terrain, we want to force Flightor to mount if it cannot get to the destination...
                    SetMountState(context => !Navigator.CanNavigateFully(LocationObserver(context), CachedDestination), context => NavType.Fly),

                    // Sometimes when flightor is on the ground, it needs to be pushed into the air to get started...
                    new Decorator(context => !Me.IsFlying
                                            && Me.MovementInfo.CanFly
                                            && Flightor.MountHelper.Mounted,
                        new Sequence(
                            new Action(context => WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend)),
                            new WaitContinue(jumpAscendTime, context => Me.IsFlying, new ActionAlwaysSucceed()),
                            new ActionFail(context => WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend))
                            // fall through
                        )),

                    new Action(context => { Flightor.MoveTo(CachedDestination, 15.0f, true); })
                );
            }


            private Composite TryNavigator()
            {
                return new PrioritySelector(
                    // If we are flying, land and set up for ground travel...
                    new Decorator(context => Me.IsFlying,
                        new UtilityBehaviorPS.DescendForDismount(context => "Preparing for ground travel")),

                    // If we can navigate to destination, use navigator...
                    new Decorator(context => Navigator.CanNavigateFully(LocationObserver(context), CachedDestination),
                        new PrioritySelector(
                            SetMountState(null, context => NavType.Run),
                            new Action(context =>
                            {
                                var moveResult = Navigator.MoveTo(CachedDestination);
                                return Navigator.GetRunStatusFromMoveResult(moveResult);
                            })
                        )),

                    new ActionFail(context =>
                    {
                       QBCLog.DeveloperInfo("Navigator unable to move to destination({0}, {1}) on ground--try MovementBy=\"FlightorPreferred\".",
                           CachedDestinationName, CachedDestination.ToString());
                    })
                );
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
                                            ProvideStringDelegate huntedMobExclusions = null)
            {
                Contract.Requires(huntingGroundsProvider != null, context => "huntingGroundsProvider may not be null");
                Contract.Requires(movementByDelegate != null, context => "movementByDelegate may not be null");

                HuntingGroundsProvider = huntingGroundsProvider;
                MovementByDelegate = movementByDelegate ?? (context => MovementByType.FlightorPreferred);
                TerminateBehaviorIfNoTargetsProvider = terminateBehaviorIfNoTargetsProvider;
                HuntedMobExclusions = huntedMobExclusions ?? (context => string.Empty);

                Children = CreateChildren();
            }


            // BT contruction-time properties...
            private ProvideHuntingGroundsDelegate HuntingGroundsProvider { get; set; }
            private ProvideStringDelegate HuntedMobExclusions { get; set; }
            private ProvideMovementByDelegate MovementByDelegate { get; set; }
            private Action<object> TerminateBehaviorIfNoTargetsProvider { get; set; }


            // 22Apr2013-01:15UTC chinajade
            private List<Composite> CreateChildren()
            {
                return new List<Composite>()
                {
                    // Move to next hunting ground waypoint...
                    new UtilityBehaviorPS.MoveTo(HuntingGroundsProvider, MovementByDelegate),

                    // Only one hunting ground waypoint to move to?
                    new CompositeThrottle(TimeSpan.FromSeconds(10),
                        new ActionFail(context =>
                        {
                            string message = "No viable mobs in area.";

                            TreeRoot.StatusText = message;
                            
                            // Show excluded units before terminating.  This aids in profile debugging if WaitForNpcs="false"...
                            string excludedUnitReasons = HuntedMobExclusions(context);
                            if (!string.IsNullOrEmpty(excludedUnitReasons))
                            {
                                message += excludedUnitReasons;
                                QBCLog.DeveloperInfo("{0}", message);
                            }
                        })),

                    // Terminate of no targets available?
                    new Decorator(context => TerminateBehaviorIfNoTargetsProvider != null,
                        new ActionFail(context => { TerminateBehaviorIfNoTargetsProvider(context); }))
                };
            }
        }
    }
}