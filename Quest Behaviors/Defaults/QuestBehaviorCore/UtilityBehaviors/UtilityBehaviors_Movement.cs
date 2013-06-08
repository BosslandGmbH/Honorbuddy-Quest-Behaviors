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

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public abstract partial class QuestBehaviorBase
    {
        // 29Apr2013-05:20UTC chinajade
        public Composite UtilityBehaviorPS_FaceMob(ProvideWoWObjectDelegate wowObjectDelegate)
        {
            Contract.Requires(wowObjectDelegate != null, context => "wowObjectDelegate != null");

            return new Decorator(context => !MovementObserver.IsSafelyFacing(wowObjectDelegate(context)),
                new Action(context => { Me.SetFacing(wowObjectDelegate(context).Location); }));
        }

        // 29Apr2013-05:20UTC chinajade
        public Composite UtilityBehaviorPS_MoveStop()
        {
            return new Decorator(context => MovementObserver.IsMoving,
                new Sequence(
                    new Action(context => { Navigator.PlayerMover.MoveStop(); }),
                    new Wait(Delay.LagDuration, context => MovementObserver.IsMoving, new ActionAlwaysSucceed())
                ));
        }


        // 22Apr2013-12:45UTC chinajade
        public Composite UtilityBehaviorPS_MoveTo(ProvideHuntingGroundsDelegate huntingGroundsProvider)
        {
            Contract.Requires(huntingGroundsProvider != null, context => "huntingGroundsProvider may not be null");

            return new PrioritySelector(
                UtilityBehaviorPS_MoveTo(
                    context => huntingGroundsProvider(context).CurrentWaypoint().Location,
                    context => string.Format("hunting ground waypoint '{0}'",
                                            huntingGroundsProvider(context).CurrentWaypoint().Name))
                                            );
        }

    
        // 24Feb2013-08:11UTC chinajade
        public Composite UtilityBehaviorPS_MoveTo(ProvideWoWPointDelegate destinationDelegate,
                                                    ProvideStringDelegate destinationNameDelegate,
                                                    ProvideDoubleDelegate precisionDelegate = null,
                                                    CanRunDecoratorDelegate suppressMountUse = null,
                                                    ProvideWoWPointDelegate locationObserver = null)
        {
            Contract.Requires(destinationDelegate != null, context => "destinationDelegate may not be null");
            Contract.Requires(destinationNameDelegate != null, context => "destinationNameDelegate may not be null");
            precisionDelegate = precisionDelegate ?? (context => Navigator.PathPrecision);
            locationObserver = locationObserver ?? (context => MovementObserver.Location);

            return new Decorator(context => MovementBy != MovementByType.None,
                new PrioritySelector(
                    new Action(context =>
                    {
                        _ubpsMoveTo_Location = destinationDelegate(context);
                        return RunStatus.Failure;   // fall through
                    }),
                    UtilityBehaviorPS_MountAsNeeded(destinationDelegate, suppressMountUse),

                    new Decorator(context => (locationObserver(context).Distance(_ubpsMoveTo_Location) > precisionDelegate(context)),
                        new Sequence(
                            new CompositeThrottleContinue(TimeSpan.FromMilliseconds(1000),
                                new Action(context => { TreeRoot.StatusText = "Moving to " + (destinationNameDelegate(context) ?? _ubpsMoveTo_Location.ToString()); })),
                            new CompositeThrottleContinue(Throttle.WoWClientMovement,
                                new Action(context =>
                                {
                                    var moveResult = MoveResult.Failed;

                                    // Use Flightor, if allowed...
                                    if ((MovementBy == MovementByType.FlightorPreferred) && Me.IsOutdoors && Me.MovementInfo.CanFly)
                                    {
                                        var immediateDestination = _ubpsMoveTo_Location.FindFlightorUsableLocation();

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
                                    if ((MovementBy == MovementByType.NavigatorPreferred)
                                        || (MovementBy == MovementByType.NavigatorOnly)
                                        || (moveResult == MoveResult.Failed))
                                    {
                                        moveResult = Navigator.MoveTo(_ubpsMoveTo_Location);
                                    }

                                    // If Navigator couldn't move us, resort to click-to-move if allowed...
                                    if (!((moveResult == MoveResult.Moved)
                                            || (moveResult == MoveResult.ReachedDestination)
                                            || (moveResult == MoveResult.PathGenerated)))
                                    {
                                        if (MovementBy == MovementByType.NavigatorOnly)
                                        {
                                            QBCLog.Warning("Failed to mesh move--is area unmeshed? Or, are we flying or swimming?");
                                            return RunStatus.Failure;
                                        }

                                        WoWMovement.ClickToMove(_ubpsMoveTo_Location);
                                    }

                                    return RunStatus.Success;
                                }))
                        ))  
                    ));
        }
        private WoWPoint _ubpsMoveTo_Location;
        

        // 22Apr2013-01:15UTC chinajade
        public Composite UtilityBehaviorPS_NoMobsAtCurrentWaypoint(ProvideHuntingGroundsDelegate huntingGroundsProvider,
                                                                    ProvideBoolDelegate terminateBehaviorIfNoTargetsProvider = null,
                                                                    Func<object, IEnumerable<string>> huntedMobNamesProvider = null,
                                                                    ProvideStringDelegate huntedMobExclusions = null)
        {
            Contract.Requires(huntingGroundsProvider != null, context => "huntingGroundsProvider may not be null");
            terminateBehaviorIfNoTargetsProvider = terminateBehaviorIfNoTargetsProvider ?? (context => false);
            huntedMobNamesProvider = huntedMobNamesProvider ?? (context => Enumerable.Empty<string>());
            huntedMobExclusions = huntedMobExclusions ?? (context => string.Empty);

            return
                new PrioritySelector(
                    // Move to next hunting ground waypoint...
                    UtilityBehaviorPS_MoveTo(huntingGroundsProvider),

                    // Terminate of no targets available?
                    new Decorator(context => terminateBehaviorIfNoTargetsProvider(context),
                        new Action(context =>
                        {
                            string message = "No mobs in area--terminating due to WaitForNpcs=\"false\"";
                            TreeRoot.StatusText = message;

                            // Show excluded units before terminating.  This aids in profile debugging if WaitForNpcs="false"...
                            string excludedUnitReasons = huntedMobExclusions(context);
                            if (!string.IsNullOrEmpty(excludedUnitReasons))
                            {
                                message += excludedUnitReasons;
                                QBCLog.DeveloperInfo("{0}", message);                                            
                            }
                            BehaviorDone();
                        })),

                    // Only one hunting ground waypoint to move to?
                    new CompositeThrottle(context => huntingGroundsProvider(context).Waypoints.Count() <= 1,
                        TimeSpan.FromSeconds(30),
                        new Action(context =>
                        {
                            string message = "Waiting for respawn";

                            if (huntedMobNamesProvider(context).Any())
                            {
                                message += " of ";
                                message += string.Join(", ", huntedMobNamesProvider(context));
                            }
                                
                            TreeRoot.StatusText = message;

                            string excludedUnitReasons = huntedMobExclusions(context);
                            if (!string.IsNullOrEmpty((excludedUnitReasons)))
                            {
                                message += excludedUnitReasons;
                                QBCLog.DeveloperInfo("{0}", message);
                            }
                        }))
                );
        }
    }
}