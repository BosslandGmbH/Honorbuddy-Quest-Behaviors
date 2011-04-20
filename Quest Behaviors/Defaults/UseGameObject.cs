// Behavior originally contributed by Unknown.
//
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_UseGameObject
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class UseGameObject : CustomForcedBehavior
    {
        public UseGameObject(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                ObjectId    = GetAttributeAsMobId("ObjectId", true, null) ?? 0;
                QuestId     = GetAttributeAsQuestId("QuestId", false, null) ?? 0;
                NumOfTimes  = GetAttributeAsNumOfTimes("NumOfTimes", false, null) ?? 1;
                Location    = GetXYZAttributeAsWoWPoint("", true, null) ?? WoWPoint.Empty;
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
        public int                      ObjectId { get; private set; }
        public int                      NumOfTimes { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }
        public int                      WaitTime { get; private set; }

        // Private variables for internal state
        private int                     _counter;
        private Composite               _root;

        // Private properties
        private WoWGameObject           GameObject { get { return (ObjectManager.GetObjectsOfType<WoWGameObject>()
                                                                            .Where(u => u.Entry == ObjectId && !u.InUse && !u.IsDisabled)
                                                                            .OrderBy(u => u.Distance)
                                                                            .FirstOrDefault());  }}


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                    // Move to the gameobject if it isn't null and we aren't withing interact range.
                    new Decorator(ret => GameObject != null && !GameObject.WithinInteractRange,
                        new Sequence(
                            new Action(ret => TreeRoot.StatusText = "Moving to \"" + GameObject.Name + "\" at location " + GameObject.Location),
                            new Action(ret => Navigator.MoveTo(GameObject.Location))
                            )
                        ),

                    // Interact etc. 
                    new Decorator(ret => GameObject != null && GameObject.WithinInteractRange,
                        // Set the context to the gameobject
                        new Sequence(ret => GameObject,

                            new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                new Sequence(
                                    new Action(ret => WoWMovement.MoveStop()),
                                    new WaitContinue(5, ret => !StyxWoW.Me.IsMoving,
                                        new Action(ret => StyxWoW.SleepForLagDuration()))
                                    )),

                            new Action(ret => UtilLogMessage("info", "Using Object \"{0}\" {1}/{2} times",
                                                                        ((WoWGameObject)ret).Name, _counter+1, NumOfTimes)),
                            new Action(ret => ((WoWGameObject)ret).Interact()),
                            new Action(ret => StyxWoW.SleepForLagDuration()),
                            new Action(ret => Thread.Sleep(WaitTime)),
                            new Action(delegate { _counter++; })
                        )),

                        new Decorator(ret => Location != WoWPoint.Empty,
                            new Sequence(
                                new Action(ret => TreeRoot.StatusText = "Moving to location " + Location.ToString()),
                                new Action(ret => Navigator.MoveTo(Location))
                                )
                            )
                        ));
        }

        public override bool IsDone
        {
            get
            {
                return ((_counter >= NumOfTimes)     // normal completion
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
