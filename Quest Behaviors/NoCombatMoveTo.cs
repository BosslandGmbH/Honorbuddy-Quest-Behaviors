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
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_NoCombatMoveTo
//
// Allows you to move to a specific target with engaging in Combat, to avoid endless combat loops.
// ##Syntax##
// QuestId: Id of the quest.
// X,Y,Z: Where you want to go to.
//
#endregion


#region Examples
#endregion


#region Usings

using System;
using System.Collections.Generic;
using System.Numerics;
using Bots.Grind;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.NoCombatMoveTo
{
    [CustomBehaviorFileName(@"NoCombatMoveTo")]
    public class NoCombatMoveTo : CustomForcedBehavior
    {
        public NoCombatMoveTo(Dictionary<string, string> args)
            : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;

                Destination = GetAttributeAsNullable<Vector3>("", true, ConstrainAs.Vector3NonEmpty, null) ?? Vector3.Zero;
                DestinationName = GetAttributeAs<string>("DestName", false, ConstrainAs.StringNonEmpty, null) ?? "";

                if (string.IsNullOrEmpty(DestinationName))
                { DestinationName = Destination.ToString(); }
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

        // DON'T EDIT THIS--it is auto-populated by Git
        public override string VersionId => QuestBehaviorBase.GitIdToVersionId("$Id$");


        // Attributes provided by caller
        public string DestinationName { get; private set; }
        public Vector3 Destination { get; private set; }
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }

        // Private variables for internal state
        private bool _isBehaviorDone;
        private Composite _root;
        private BehaviorFlags? _orginalBehaviorFlags;

        // Private properties
        private LocalPlayer Me { get { return (StyxWoW.Me); } }

        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                           new Decorator(ret => Destination.Distance(Me.Location) <= 3,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new Action(ctx => _isBehaviorDone = true))),

                          new Action(ctx => Navigator.MoveTo(Destination))
                    ));
        }

        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone     // normal completion
                        || !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
            }
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
                this.UpdateGoalText(QuestId, "Moving to " + (string.IsNullOrEmpty(DestinationName) ? "Unspecified destination" : DestinationName));
                _orginalBehaviorFlags = LevelBot.BehaviorFlags;
                LevelBot.BehaviorFlags &= ~(BehaviorFlags.Combat | BehaviorFlags.Loot | BehaviorFlags.Vendor);
            }
        }

        public override void OnFinished()
        {
            if (_orginalBehaviorFlags.HasValue)
                LevelBot.BehaviorFlags = _orginalBehaviorFlags.Value;
            this.UpdateGoalText(QuestId);
            TreeRoot.StatusText = string.Empty;
        }

        #endregion
    }
}

