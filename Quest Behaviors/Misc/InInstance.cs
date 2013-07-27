//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

#region information
// Behavior originally contributed by AknA.
// A variation of MyCTM with extra check if you're in a instance or not.
// Primary use is to zone in/out of instances and not have strange behaviors once you have, otherwize it works exactly like MyCTM.
// How to use : <CustomBehavior File="InInstance" X="123" Y="456" Z="789" /> 
#endregion

#region using
using System;
using System.Collections.Generic;

using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using CommonBehaviors.Actions;
using Action = Styx.TreeSharp.Action;
#endregion

namespace Honorbuddy.Quest_Behaviors.InInstance
{
    [CustomBehaviorFileName(@"Misc\InInstance")]
    public class InInstance : CustomForcedBehavior
    {
        public InInstance(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                DestinationName = GetAttributeAs("DestName", false, ConstrainAs.StringNonEmpty, new[] { "Name" }) ?? "";
                Destination = GetAttributeAsNullable("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
                if (string.IsNullOrEmpty(DestinationName)) { DestinationName = Destination.ToString(); }
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
                                    + "\nFROM HERE:\n"
                                    + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }

        #region variables
        // Attributes provided by caller
        public string DestinationName { get; private set; }
        public WoWPoint Destination { get; private set; }

        // Private variables for internal state
        private bool _IsBehaviorDone;
        private bool _IsDisposed;
        private readonly bool _InInstanceOrNot = CheckInstance();
        private Composite _Root;

        // Private properties
        public int Counter { get; set; }
        public bool IsConverted { get; set; }
        private static LocalPlayer Me { get { return (StyxWoW.Me); } }

        ~InInstance() { Dispose(false); }
        #endregion

        #region Dispose
        public void Dispose(bool isExplicitlyInitiatedDispose)
        {
            if (!_IsDisposed)
            {
                // NOTE: we should call any Dispose() method for any managed or unmanaged
                // resource, if that resource provides a Dispose() method.

                // Clean up managed resources, if explicit disposal...
                if (isExplicitlyInitiatedDispose) { /* empty, for now */ }

                // Clean up unmanaged resources (if any) here...
                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }
            _IsDisposed = true;
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        #region Methods
        public static bool CheckInstance()
        {
            return Lua.GetReturnVal<string>("return select(2, IsInInstance())", 0) != "none";
        }
        #endregion

        #region Overrides of CustomForcedBehavior
        protected override Composite CreateBehavior()
        {
            return _Root ?? (_Root =
                new PrioritySelector(
                    new Decorator(context => Destination.Distance(Me.Location) <= 3 || (_InInstanceOrNot != CheckInstance()),
                        new Sequence(
                            new Action(context => TreeRoot.StatusText = "Finished!"),
                            new WaitContinue(TimeSpan.FromMilliseconds(120), context => false, new ActionAlwaysSucceed()),
                            new Action(context => _IsBehaviorDone = true)
                        )
                    ),
                    new Decorator(ret => Destination.Distance(Me.Location) > 3 && (_InInstanceOrNot == CheckInstance()),
                        new Sequence(
                            new Action(context => TreeRoot.StatusText = "Moving To Location - X: " + Destination.X + " Y: " + Destination.Y),
                            new Action(context => WoWMovement.ClickToMove(Destination)),
                            new WaitContinue(TimeSpan.FromMilliseconds(50), context => false, new ActionAlwaysSucceed())
                        )
                    )
                )
            );
        }

        public override bool IsDone { get { return _IsBehaviorDone; } }

        public override void OnStart()
        {
            // This reports problems, and stops BT processing if there was a problem with attributes...
            // We had to defer this action, as the 'profile line number' is not available during the element's
            // constructor call.
            OnStart_HandleAttributeProblem();

            if (!IsDone) { TreeRoot.GoalText = "CTMoving to " + DestinationName; }
        }
        #endregion
    }
}

