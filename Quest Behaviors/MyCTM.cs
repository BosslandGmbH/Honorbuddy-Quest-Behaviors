// Behavior originally contributed by Natfoth.
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
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_MyCTM
//
//     Allows you to physically click on the screen so that your bot can get around non meshed locations or off objects.
//     *** There is no navigation with this ****
//     ##Syntax##
//     QuestId: Id of the quest.
//     X,Y,Z: Where you wish to move.
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
using Vector3 = Tripper.Tools.Math.Vector3;
#endregion


namespace Honorbuddy.Quest_Behaviors.MyCTM
{
    [CustomBehaviorFileName(@"MyCTM")]
    public class MyCTM : CustomForcedBehavior
    {
        private static Random _rnd = new Random();
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private Composite _root;
        private WaitTimer _runTimer;

        public MyCTM(Dictionary<string, string> args) : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                DestinationName = GetAttributeAs<string>("DestName", false, ConstrainAs.StringNonEmpty, new[] {"Name"}) ?? "";
                Destination = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ??
                              WoWPoint.Empty;
                QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
                QuestRequirementComplete =
                    GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ??
                    QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog =
                    GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ??
                    QuestInLogRequirement.InLog;
                UseRealitiveLocation = GetAttributeAsNullable<bool>("UseRelative", false, null, new[] {"useRelative"}) ??
                                       false;

                OrigDestination = Destination;

                if (string.IsNullOrEmpty(DestinationName))
                {
                    DestinationName = Destination.ToString();
                }
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                QBCLog.Error("[MAINTENANCE PROBLEM]: " + except.Message
                        + "\nFROM HERE:\n"
                        + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }


        // Attributes provided by caller
        public string DestinationName { get; private set; }
        public WoWPoint OrigDestination { get; private set; }
        public WoWPoint Destination { get; private set; }
        public int QuestId { get; private set; }
        public bool UseRealitiveLocation { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }

        // Private variables for internal state
        readonly WaitTimer _stuckTimer = new WaitTimer(TimeSpan.FromSeconds(2));
        // Private properties

        private LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
        }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId
        {
            get { return ("$Id$"); }
        }

        public override string SubversionRevision
        {
            get { return ("$Revision$"); }
        }

        private bool ShouldPerformCTM
        {
            get
            {
                return !WoWMovement.ActiveMover.IsMoving ||
                       WoWMovement.ClickToMoveInfo.ClickPos.DistanceSqr(Destination) > 0.5*0.5;
            }
        }


        ~MyCTM()
        {
            Dispose(false);
        }


        public void Dispose(bool isExplicitlyInitiatedDispose)
        {
            if (!_isDisposed)
            {
                // NOTE: we should call any Dispose() method for any managed or unmanaged
                // resource, if that resource provides a Dispose() method.

                // Clean up managed resources, if explicit disposal...
                if (isExplicitlyInitiatedDispose)
                {
                    TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_CombatMain());
                }

                // Clean up unmanaged resources (if any) here...
                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }

        private RunStatus UpdateRelativeLocation()
        {
            Vector3 worldLoc = Vector3.Transform(OrigDestination, Me.Transport.GetWorldMatrix());
            Destination = worldLoc;
            // return failure so behavior drops down
            return RunStatus.Failure;
        }

        private TimeSpan CalculateMaxRuntime(WoWPoint destination)
        {
            const double upperLimitOnMaxTime = 3 /*mins*/*60 /*secs*/;
            // these speeds have been verified.
            double myMovementSpeed = Me.IsSwimming
                ? Me.MovementInfo.SwimmingForwardSpeed
                : Me.IsFlying ? Me.MovementInfo.FlyingForwardSpeed : Me.MovementInfo.ForwardSpeed;
            var distanceToCover = WoWMovement.ActiveMover.Location.Distance(destination);

            double timeToDestination = distanceToCover / myMovementSpeed;

            timeToDestination = Math.Max(timeToDestination, 15.0); // 15sec hard lower limit
            timeToDestination *= 2.5; // factor of safety

            // Place an upper limit on the maximum time to reach the destination...
            // NB: We can get times that are effectively 'infinite' in situations where the Navigator
            // was unable to calculate a path to the target.  This puts an upper limit on such
            // bogus values.
            timeToDestination = Math.Min(timeToDestination, upperLimitOnMaxTime);

            return (TimeSpan.FromSeconds(timeToDestination));
        }

        protected Composite CreateBehavior_PerformCTM()
        {
            // CTM only if not moving or current ctm position isn't our destination.
            return
                new Decorator(
                    ctx => ShouldPerformCTM,
                    new Sequence(
                        new Action(ctx => WoWMovement.ClickToMove(Destination)),
                        new Sleep(100)));
        }

        protected Composite CreateBehavior_Antistuck()
        {
            var stuckSucceedTimer = new WaitTimer(TimeSpan.FromSeconds(6));

            var prevPosition = WoWPoint.Empty;
            WoWPoint myLoc = WoWPoint.Empty;
            bool performSimpleSequence = false;
            var moveDirection = WoWMovement.MovementDirection.None;

            return new PrioritySelector(
                new Decorator(
                    ctx => _stuckTimer.IsFinished,

                    new Sequence(
                        ctx => myLoc = WoWMovement.ActiveMover.Location,

                        // checks if stuck
                        new DecoratorContinue(
                            ctx => myLoc.DistanceSqr(prevPosition) < 3*3,
                            new Sequence(
                                ctx => performSimpleSequence = stuckSucceedTimer.IsFinished,

                                new DecoratorContinue(
                                    ctx => Me.IsMounted() && !Me.IsFlying,
                                    new Action(ctx => Mount.Dismount("Stuck"))),

                                // perform simple unstuck proceedure
                                new DecoratorContinue(
                                    ctx => performSimpleSequence,
                                    new Sequence(
                                        new Action(ctx => QBCLog.Debug("Stuck. Trying to jump")),
                                        new Action(
                                            ctx =>
                                            {
                                                // ensure bot is moving forward when jumping (Wow will sometimes automatically stop moving if running against a wall)
                                                if (ShouldPerformCTM)
                                                    WoWMovement.ClickToMove(Destination);
                                                WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
                                            }),
                                        new WaitContinue(1, ctx => false, new ActionAlwaysSucceed()),
                                        new Action(ctx => WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend )))),

                                // perform less simple unstuck proceedure
                                new DecoratorContinue(
                                    ctx => !performSimpleSequence,
                                    new Sequence(
                                        ctx => moveDirection = GetRandomMovementDirection(),
                                        new Action(ctx => QBCLog.Debug("Stuck. Movement Directions: {0}", moveDirection)),
                                        new Action(ctx => WoWMovement.Move(moveDirection)),
                                        new WaitContinue(2, ctx => false, new ActionAlwaysSucceed()),
                                        new Action(ctx => WoWMovement.MoveStop(moveDirection)))),

                                new Action(ctx => stuckSucceedTimer.Reset()))),

                        new Action(ctx => prevPosition = myLoc),
                        new Action(ctx => _stuckTimer.Reset()))));
        }

        private WoWMovement.MovementDirection GetRandomMovementDirection()
        {
            // randomly move left or ritht
            WoWMovement.MovementDirection ret = _rnd.Next(2) == 0
                ? WoWMovement.MovementDirection.StrafeLeft
                : WoWMovement.MovementDirection.StrafeRight;

            // randomly choose to go diagonal backwords + left or right
            if (_rnd.Next(2) == 0)
                ret |= WoWMovement.MovementDirection.Backwards;

            // randomly choose to jump (or descend if flying or swimming)
            if (_rnd.Next(2) == 0)
            {
                var activeMover = WoWMovement.ActiveMover;
                if (activeMover.IsFlying || activeMover.IsSwimming)
                {
                    ret |= _rnd.Next(2) == 0
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

        #region Overrides of CustomForcedBehavior

        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone // normal completion
                        || !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
            }
        }

        protected Composite CreateBehavior_CombatMain()
        {
            return _root ??
                   (_root =
                       new PrioritySelector(
                           new Decorator(
                               ctx => !_isBehaviorDone && !Me.IsActuallyInCombat,
                               new PrioritySelector(

                                   // Update Location if relative coord is used.
                                   // N.B. Relative locations are only used while on transports and because transports are usualy moving around 'Location' needs to be updated on every frame.
                                   new Decorator(ret => UseRealitiveLocation, new Action(ctx => UpdateRelativeLocation())),
                                   
                                   // initialize the timer.
                                   new Decorator(
                                       ctx => _runTimer == null,
                                       new Action(
                                           ctx =>
                                           {
                                               _runTimer = new WaitTimer(CalculateMaxRuntime(Destination));
                                               QBCLog.Debug(
                                                   "Maximum allowed time to reach destination: {0} seconds",
                                                   _runTimer.WaitTime.TotalSeconds);
                                               _runTimer.Reset();
                                               return RunStatus.Failure;
                                           })),

                                   // stop HB if _runTimer finishes
                                   new Decorator(
                                       ctx => _runTimer.IsFinished,
                                       new Sequence(
                                           new Action(ctx => WoWMovement.MoveStop()),
                                           // N.B. set the runtimer to null so if player manually correct problem and starts bot up it restarts the timer.
                                           new Action(ctx => _runTimer = null),
                                           new Action(
                                               ctx => QBCLog.Fatal(
                                                   "MyCTM is not able to reach {0} from {1}",
                                                   DestinationName,
                                                   WoWMovement.ActiveMover.Location)))),

                                   // run stuckhandler
                                   CreateBehavior_Antistuck(),
                                   /* Default anti-stuck has issues.
                                   new Decorator(
                                       ctx => Navigator.NavigationProvider.StuckHandler.IsStuck(),
                                       new Action(ctx => Navigator.NavigationProvider.StuckHandler.Unstick())),
                                    */

                                   // check if bot has reached the destination.
                                   new Decorator(
                                       ret => Destination.DistanceSqr(Me.Location) <= 3 * 3,
                                       new Sequence(
                                           new Action(ctx => QBCLog.Info("MyCTM finished moving to {0}", DestinationName)),
                                           new Action(ctx => _isBehaviorDone = true),
                                           // Drop down to 'CreateBehavior_PerformCTM' to ensure ctm is performed at least once if start and destination locations are very close on start 
                                           new ActionAlwaysFail())),
                                   CreateBehavior_PerformCTM())),

                           // _runTimer needs to be recalculated after combat is over and stuck timer needs to rest.
                           new Decorator(
                               ctx => Me.IsActuallyInCombat ,
                               new Action(
                                   ctx =>
                                   {
                                       _runTimer = null;
                                       _stuckTimer.Reset();
                                       return RunStatus.Failure;
                                   }))));
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        public override void OnStart()
        {
            // This reports problems, and stops BT processing if there was a problem with attributes...
            // We had to defer this action, as the 'profile line number' is not available during the element's
            // constructor call.
            OnStart_HandleAttributeProblem();

            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (!IsDone)
            {
                Navigator.NavigationProvider.StuckHandler.Reset();
                TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_CombatMain());

                this.UpdateGoalText(QuestId, "CTMoving to " + DestinationName);
            }
        }

        #endregion
    }
}