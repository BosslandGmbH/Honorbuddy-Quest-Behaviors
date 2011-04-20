// Behavior originally contributed by Nesox.
//
// DOCUMENTATION:
//     
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// Allows you to use items on nearby gameobjects/npc's
    /// ##Syntax##
    /// QuestId: The id of the quest.
    /// ItemId: The id of the item to use.
    /// NumOfTimes: Number of times to use said item.
    /// [Optional]WaitTime: Time to wait after using an item. DefaultValue: 1500 ms
    /// </summary>
    public class UseItem: CustomForcedBehavior
    {
        public UseItem(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                ItemId      = GetAttributeAsItemId("ItemId", true, null) ?? 0;
                Location    = GetXYZAttributeAsWoWPoint("", false, null) ?? WoWPoint.Empty;
                NumOfTimes  = GetAttributeAsNumOfTimes("NumOfTimes", false, null) ?? 1;
                QuestId     = GetAttributeAsQuestId("QuestId", false, null) ?? 0;
                WaitTime    = GetAttributeAsWaitTime("WaitTime", false, null) ?? 1500;
			}

			catch (Exception except)
			{
				// Maintenance problems occur for a number of reasons.  The primary two are...
				// * Changes were made to the behavior, and boundary conditions weren't properly tested.
				// * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
				// In any case, we pinpoint the source of the problem area here, and hopefully it
				// can be quickly resolved.
				UtilLogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
										+ "\nFROM HERE:\n"
										+ except.StackTrace + "\n");
				IsAttributeProblem = true;
			}
        }

        // Attributes provided by caller
        public WoWPoint                 Location { get; private set; }
        public int                      ItemId { get; private set; }
        public int                      NumOfTimes { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }
        public int                      WaitTime { get; private set; }

        // Private variables for internal state
        private bool                    _isBehaviorDone;
        private Composite               _root;

        // Private properties
        private int                     Counter { get; set; }
        private WoWItem                 Item { get { return (StyxWoW.Me.CarriedItems.FirstOrDefault(i => i.Entry == ItemId)); }}


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
            new PrioritySelector(

                new Decorator(ret => Counter >= NumOfTimes,
                    new Action(ret => _isBehaviorDone = true)),

                new Decorator(
                    ret => Location != WoWPoint.Empty && Location.Distance(StyxWoW.Me.Location) > 2,
                    new Sequence(
                        new Action(ret => TreeRoot.StatusText = "Moving to location"),
                        new Action(ret => Navigator.MoveTo(Location)))),

                new Decorator(
                    ret => StyxWoW.Me.IsMoving,
                    new Action(ret =>
                        {
                            Navigator.PlayerMover.MoveStop();
                            StyxWoW.SleepForLagDuration();
                        })),

                new Decorator(
                    ret => Item != null && Item.Cooldown == 0,
                    new Action(ret =>
                    {
                        TreeRoot.StatusText = "Using item - Count: " + Counter;

                        Item.UseContainerItem();

                        StyxWoW.SleepForLagDuration();
                        Counter++;
                        Thread.Sleep(WaitTime);
                    }))));
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
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = this.GetType().Name + ": " + ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
            }
        }

        #endregion
    }

}
