// Behavior originally contributed by Natfoth.
//
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_FollowNpcUntil
//
using System;
using System.Collections.Generic;
using System.Linq;

using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class FollowNpcUntil : CustomForcedBehavior
    {
        public FollowNpcUntil(Dictionary<string, string> args)
            : base(args)
        {
			try
			{
                UtilLogMessage("warning",   "*****\n"
                                          + "* THIS BEHAVIOR IS DEPRECATED, and may be retired in a near, future release.\n"
                                          + "*\n"
                                          + "* Escort is the replacement behavior for FollowNpcUntil.\n"
                                          + "* Please update the profile to use Escort in preference to this behavior.\n"
                                          + "*****");

                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                Location    = GetXYZAttributeAsWoWPoint("", true, null) ?? WoWPoint.Empty;
                MovedToTarget = false;
                MobId       = GetAttributeAsMobId("MobId", true, new [] { "NpcId" }) ?? 0;
                MobName     = GetAttributeAsString_NonEmpty("MobName", false, null) ?? "";
                QuestId     = GetAttributeAsQuestId("QuestId", false, null) ?? 0;
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;

                if (string.IsNullOrEmpty(MobName))
                {
                    WoWUnit     mob     = ObjectManager.GetObjectsOfType<WoWUnit>()
                                          .Where(unit => unit.Entry == MobId)
                                          .FirstOrDefault();

                    MobName = !string.IsNullOrEmpty(mob.Name)  ? mob.Name   : ("Mob(" + MobId + ")");
                }

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
        public int                      Counter { get; set; }
        public WoWPoint                 Location { get; private set; }
        public bool                     MovedToTarget { get; private set; }
        public int                      MobId { get; private set; }
        public string                   MobName { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }

        // Private variables for internal state
        private bool                _isBehaviorDone;
        private List<WoWUnit>       _npcList;
        private Composite           _root;

        // Private properties
        private LocalPlayer         Me { get { return (ObjectManager.Me); } }


        /// <summary>
        /// A Queue for npc's we need to talk to
        /// </summary>
        //private WoWUnit CurrentUnit { get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(unit => unit.Distance < 100 && unit.Entry == MobId); } }

        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(
				
				new Decorator(ret => (QuestId != 0 && Me.QuestLog.GetQuestById((uint)QuestId) != null &&
                                        Me.QuestLog.GetQuestById((uint)QuestId).IsCompleted),
                        new Action(ret => _isBehaviorDone = true)),

                    new Decorator(ret => Counter > 0,
                        new Action(ret => _isBehaviorDone = true)),

                        new PrioritySelector(

                           new Decorator(ret => !MovedToTarget,
                                new Action(delegate
                                {
                                    ObjectManager.Update();

                                    _npcList = ObjectManager.GetObjectsOfType<WoWUnit>()
                                        .Where(u => u.Entry == MobId)
                                        .OrderBy(u => u.Distance).ToList();

                                    PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
                                    if (quest.IsCompleted)
                                    {
                                        Counter++;
                                        return RunStatus.Success;
                                    }
                                    else if (_npcList.Count >= 1)
                                    {
                                        Navigator.MoveTo(_npcList[0].Location);

                                    }
                                    else
                                    {
                                        Navigator.MoveTo(Location);
                                    }

                                    return RunStatus.Running;

                                })
                                ),

                            new Action(ret => Navigator.MoveTo(Location))
                        )
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
                TreeRoot.GoalText = "Following " + MobName;
            }
		}

        #endregion
    }
}
