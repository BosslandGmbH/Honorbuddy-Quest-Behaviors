// Behavior originally contributed by Natfoth.
//
// WIKI DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_BasicMoveTo
//
// QUICK DOX:
//      Moves the toon to a desired location in the game world.
//      NOTE: This behavior is deprecated--use the built-in <RunTo> element instead.    
//
//  Parameters (required, then optional--both listed alphabetically):
//      X, Y, Z:  World coordinates to which the toon should move.
//
//      DestName [Default:"<X,Y,Z>"]:   a human-readable name of the location to which the toon is moving.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.AscendInWater
{
    [CustomBehaviorFileName(@"AscendInWater")]
    public class AscendInWater : CustomForcedBehavior
    {
        // Private variables for internal state
        private static readonly WaitTimer MaxAscendTimer = new WaitTimer(TimeSpan.FromSeconds(30));
        private bool _isDisposed;
        private Composite _root;

        public AscendInWater(Dictionary<string, string> args) : base(args)
        {
            try {}

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message + "\nFROM HERE:\n" + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }

        // Private properties
        private LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
        }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId
        {
            get { return ("$Id: AscendInWater.cs 501 2013-05-10 16:29:10Z chinajade $"); }
        }

        public override string SubversionRevision
        {
            get { return ("$Revision: 501 $"); }
        }


        ~AscendInWater()
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
                    // empty, for now
                }

                // Clean up unmanaged resources (if any) here...
                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                WoWMovement.MoveStop();

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }

        #region Overrides of CustomForcedBehavior

        public override bool IsDone
        {
            get { return MaxAscendTimer.IsFinished || !Me.IsSwimming || !Me.GetMirrorTimerInfo(MirrorTimerType.Breath).IsVisible; }
        }

        protected override Composite CreateBehavior()
        {
            return _root ??
                   (_root =
                       new PrioritySelector(new Decorator(ctx => !Me.MovementInfo.IsAscending, 
                           new Action(ctx => WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend)))));
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

            MaxAscendTimer.Reset();

            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (!IsDone)
            {
                TreeRoot.GoalText = GetType().Name + ": Ascending in water";
            }
        }

        #endregion
    }
}