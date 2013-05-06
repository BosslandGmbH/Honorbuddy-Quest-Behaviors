// Behavior originally contributed by Bobby53.
//
// WIKI DOCUMENTATION:
//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_AbandonQuest
//
// QUICK DOX:
//      Allows you to abandon a quest in your quest log.
//
//  Parameters (required, then optional--both listed alphabetically):
//      QuestId: The id of the quest.
//
//      Type[Default:Incomplete]: The state in which the quest must reside for the abandon
//              to succeed.  The allowed values for this attribute are:
//                  All:        abandon quest if its in log regardless of status
//                  Failed:     abandon quest only if failed
//                  Incomplete: abandon incomplete quests (failed and any not complete)  
//
//      WaitTime [optional; Default: 1500ms]
//          Defines the number of milliseconds to wait after the quest abandon before carrying on.
//          This allows the WoWclient to update its state (and HBcore to 'catch up' to it),
//          before proceeding with the rest of the profile.
//
//  Examples:   
//     <CustomBehavior File="AbandonQuest" QuestId="25499" />
//     <CustomBehavior File="AbandonQuest" QuestId="25499" Type="All" />
//     <CustomBehavior File="AbandonQuest" QuestId="25499" Type="Failed" />
//     <CustomBehavior File="AbandonQuest" QuestId="25499" Type="Incomplete" />
//
using System;
using System.Collections.Generic;

using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;

using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.AbandonQuest
{
    [CustomBehaviorFileName(@"AbandonQuest")]
    public class AbandonQuest : CustomForcedBehavior
    {
        public enum AbandonType
        {
            All,
            Failed,
            Incomplete
        };


        public AbandonQuest(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                QuestId = GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
                Type = GetAttributeAsNullable<AbandonType>("Type", false, null, null) ?? AbandonType.Incomplete;
                WaitTime = GetAttributeAsNullable<int>("WaitTime", false, ConstrainAs.Milliseconds, null) ?? 1500;   
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


        // Attributes provided by caller
        private int QuestId { get; set; }
        private AbandonType Type { get; set; }
        private int WaitTime { get; set; }

        // Private variables for internal state
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private Composite _root;
        private readonly WaitTimer _waitTimerAfterAbandon = new WaitTimer(TimeSpan.Zero);

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id$"); } }
        public override string SubversionRevision { get { return ("$Revision$"); } }


        ~AbandonQuest()
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

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return (_root ?? (_root = new PrioritySelector(
                // Delay, if necessary...
                new Decorator(context => !_waitTimerAfterAbandon.IsFinished,
                    new Action(context =>
                    {
                        TreeRoot.StatusText = string.Format("Completing {0} wait of {1}",
                            QuestBehaviorBase.PrettyTime(TimeSpan.FromSeconds((int)_waitTimerAfterAbandon.TimeLeft.TotalSeconds)),
                            QuestBehaviorBase.PrettyTime(_waitTimerAfterAbandon.WaitTime));
                    })),

                new Action(context => { _isBehaviorDone = true; })
            )));
        }


        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone);
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
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                if (quest == null)
                {
                    LogMessage("warning", "Cannot find quest with QuestId({0}).", QuestId);
                    _isBehaviorDone = true;
                }

                else if ((quest != null) && quest.IsCompleted && (Type != AbandonType.All))
                {
                    LogMessage("warning", "Quest({0}, \"{1}\") is Complete--skipping abandon.", QuestId, quest.Name);
                    _isBehaviorDone = true;
                }

                else if ((quest != null) && !quest.IsFailed && (Type == AbandonType.Failed))
                {
                    LogMessage("warning", "Quest({0}, \"{1}\") has not Failed--skipping abandon.", QuestId, quest.Name);
                    _isBehaviorDone = true;
                }

                else
                {
                    TreeRoot.GoalText = string.Format("Abandoning QuestId({0}): \"{1}\"", QuestId, quest.Name);
                    QuestLog ql = new QuestLog();
                    ql.AbandonQuestById((uint)QuestId);
                    LogMessage("info", "Quest({0}, \"{1}\") successfully abandoned", QuestId, quest.Name);

                    _waitTimerAfterAbandon.WaitTime = TimeSpan.FromMilliseconds(WaitTime);
                    _waitTimerAfterAbandon.Reset();
                }
            }
        }

        #endregion
    }
}