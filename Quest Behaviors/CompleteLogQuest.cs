// Behavior originally contributed by Natfoth.
//
// WIKI DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_CompleteLogQuest
//
// QUICK DOX:
//      Allows you to 'turn in' a quest to your quest log.
//
//  Parameters (required, then optional--both listed alphabetically):
//      QuestId: Id of the quest to turn into your quest log.  It is a _fatal_ error
//               if the quest is not complete.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Bots.Quest.Actions;
using CommonBehaviors.Actions;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;

using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.CompleteLogQuest
{
    [CustomBehaviorFileName(@"CompleteLogQuest")]
    public class CompleteLogQuest : CustomForcedBehavior
    {
        public CompleteLogQuest(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                QuestId = GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), new[] { "QuestID" }) ?? 0;


                // Final initialization...
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                QuestName = (quest != null) ? quest.Name : string.Format("QuestId({0})", QuestId);
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
        public static int QuestId { get; private set; }

        // Private variables for internal state
        private bool _isDisposed;
        private Composite _root;

	    private bool _forcedDone;

        // Private properties
        private string QuestName { get; set; }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id: CompleteLogQuest.cs 501 2013-05-10 16:29:10Z chinajade $"); } }
        public override string SubversionRevision { get { return ("$Revision: 501 $"); } }


        ~CompleteLogQuest()
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
		    return (_root ?? (_root =
		                      new PrioritySelector(
			                      // If we don't have the quest in our logs, we have already turned it in
			                      new Decorator(ctx => !StyxWoW.Me.QuestLog.ContainsQuest((uint)QuestId),
			                                    new ActionAlwaysSucceed()),

			                      // Make sure we've the quest frame opened with the correct quest
			                      new Decorator(
				                      ctx => !QuestFrame.Instance.IsVisible || QuestFrame.Instance.CurrentShownQuestId != QuestId,
				                      new Sequence(
					                      new Action(ctx => Lua.DoString("ShowQuestComplete(GetQuestLogIndexByID({0}))", QuestId)),
					                      new Wait(3,
					                               ctx =>
					                               QuestFrame.Instance.IsVisible && QuestFrame.Instance.CurrentShownQuestId == QuestId,
					                               new ActionAlwaysSucceed()))),

			                      // Complete the quest, and accept the next quest if there is one. This is needed to make this compatible with the CompleteQuestLog quest behavior
			                      // which automatically accepted new quests.
			                      new Sequence(
				                      new DecoratorContinue(QuestHasRewards,
										  new ActionSelectReward()),
				                      // Just wait for a bit before we turn in, so it's easier to see what's going on
				                      new Sleep(1000),
				                      new Action(ctx => QuestFrame.Instance.CompleteQuest()),
				                      new SleepForLagDuration(),
				                      new Action(ctx => QuestFrame.Instance.AcceptQuest()),
				                      new Sleep(500),
				                      new Action(ctx => Lua.DoString("CloseQuest()"))
				                      ))));
	    }

	    private bool QuestHasRewards(object context)
		{
			uint id = QuestFrame.Instance.CurrentShownQuestId;
			Quest quest = Quest.FromId(id);

			if (quest == null)
				return false;

			var choices = quest.GetRewardChoices();
			return choices.Any(choice => choice.ItemId != 0);
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
				var isDone = _forcedDone || !StyxWoW.Me.QuestLog.ContainsQuest((uint)QuestId);

				return isDone;
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

                TreeRoot.GoalText = this.GetType().Name + ": " + QuestName;

                if (quest != null)
                {
                    if (!quest.IsCompleted)
                    {
                        LogMessage("fatal", "Quest({0}, \"{1}\") is not complete.", QuestId, QuestName);
                        _forcedDone = true;
                    }
                }

                else
                {
                    LogMessage("warning", "Quest({0}) is not in our log--skipping turn in.", QuestId);
                    _forcedDone = true;
                }
            }
        }

        #endregion
    }
}

