// Behavior originally contributed by AknA.
// A variation of MyCTM with extra check if you're in a instance or not.
// Primary use is to zone in/out of instances and not have strange behaviors once you have, otherwize it works exactly like MyCTM.
// How to use : <CustomBehavior File="InInstance" X="123" Y="456" Z="789" /> 

using System;
using System.Collections.Generic;
using System.Threading;

using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Tripper.Tools.Math;
using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.InInstance
{
    [CustomBehaviorFileName(@"Misc\InInstance")]
    public class InInstance : CustomForcedBehavior {
        /// <summary>
        /// Allows you to physically click on the screen so that your bot can get around non meshed locations or off objects. *** There is no navigation with this ****
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// X,Y,Z: Where you wish to move.
        /// </summary>
        /// 
        public InInstance(Dictionary<string, string> args)
            : base(args) {
            try {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                DestinationName = GetAttributeAs("DestName", false, ConstrainAs.StringNonEmpty, new[] { "Name" }) ?? "";
                Destination = GetAttributeAsNullable("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
                QuestId = GetAttributeAsNullable("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
                UseRealitiveLocation = GetAttributeAsNullable<bool>("UseRelative", false, null, new[] { "useRelative" }) ?? false;

                OrigDestination = Destination;

                IsConverted = false;

                if (string.IsNullOrEmpty(DestinationName)) { DestinationName = Destination.ToString(); }
            }

            catch (Exception except) {
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


        // Attributes provided by caller
        public string DestinationName { get; private set; }
        public WoWPoint OrigDestination { get; private set; }
        public WoWPoint Destination { get; private set; }
        public int QuestId { get; private set; }
        public bool UseRealitiveLocation { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }

        // Private variables for internal state
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private readonly bool _inInstanceOrNot = CheckInstance();
        private Composite _root;

        // Private properties
        public int Counter { get; set; }
        private static LocalPlayer Me { get { return (StyxWoW.Me); } }
        public bool IsConverted { get; set; }

        ~InInstance() { Dispose(false); }

        public void Dispose(bool isExplicitlyInitiatedDispose) {
            if (!_isDisposed) {
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
            _isDisposed = true;
        }

        public static bool CheckInstance() {
            Lua.DoString("a = select(2, IsInInstance())");
            var a = Lua.GetReturnVal<string>("return a", 0);
            return a != "none";
        }

        #region Overrides of CustomForcedBehavior
        protected override Composite CreateBehavior() {
            return _root ?? (_root =
                new PrioritySelector(
                    new Decorator(ret => UseRealitiveLocation,
                        new Sequence(
                            new Action(delegate {
                                Vector3 relLoc = OrigDestination;
                                Vector3 worldLoc = Vector3.Transform(relLoc, Me.Transport.GetWorldMatrix());
                                Destination = worldLoc;
                                return RunStatus.Failure;
                            }
                    ))),
                    new Decorator(ret => Destination.Distance(Me.Location) <= 3 || (_inInstanceOrNot != CheckInstance()),
                        new Sequence(
                            new Action(ret => TreeRoot.StatusText = "Finished!"),
                            new WaitContinue(120,
                                new Action(delegate {
                                    _isBehaviorDone = true;
                                    return RunStatus.Success;
                                }
                    )))),
                    new Decorator(ret => Destination.Distance(Me.Location) > 3 && (_inInstanceOrNot == CheckInstance()),
                        new Sequence(
                            new Action(ret => TreeRoot.StatusText = "Moving To Location - X: " + Destination.X + " Y: " + Destination.Y),
                            new Action(ret => WoWMovement.ClickToMove(Destination)),
                            new Action(ret => Thread.Sleep(50))
            ))));
        }

        public override void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public override bool IsDone { get {
            return (_isBehaviorDone     // normal completion
                || !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
        }}

        public override void OnStart() {
            // This reports problems, and stops BT processing if there was a problem with attributes...
            // We had to defer this action, as the 'profile line number' is not available during the element's
            // constructor call.
            OnStart_HandleAttributeProblem();

            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (!IsDone) { TreeRoot.GoalText = "CTMoving to " + DestinationName; }
        }
        #endregion
    }
}

