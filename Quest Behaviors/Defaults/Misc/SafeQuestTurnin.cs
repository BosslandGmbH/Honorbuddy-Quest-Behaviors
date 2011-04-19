using System;
using System.Collections.Generic;
using System.Drawing;

using QuestBot.QuestOrder;

using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;


namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// SafeQuestTurnin by Nesox
    /// Removes all units from targeting while running.
    /// ##Syntax##
    /// QuestId: Id of the quest, duh!
    /// QuestName: Name of the quest.
    /// TurnInName: Name of the npc to turn in quest at.
    /// TurnInId: id of the npc to turn in the quest at.
    /// X: X axis of this npc
    /// Y: Y axis of this npc
    /// Z: Z axis of this npc
    /// </summary>
    public class SafeQuestTurnin : CustomForcedBehavior
    {
        public SafeQuestTurnin(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                QuestId         = GetAttributeAsQuestId("QuestId", true, null) ?? 0;
                QuestName       = GetAttributeAsString_NonEmpty("QuestName", true, null) ?? "";
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;
                TurnInId        = GetAttributeAsMobId("TurnInId", true, null) ?? 0;
                TurnInLocation  = GetXYZAttributeAsWoWPoint("", true, null) ?? WoWPoint.Empty;
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
        public int                      QuestId { get; private set; }
        public string                   QuestName { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }
        public int                      TurnInId { get; private set; }
        public WoWPoint                 TurnInLocation { get; private set; }

        // Private properties
        private ForcedQuestTurnIn       QuestTurnIn { get; set; }


        private static void Instance_RemoveTargetsFilter(List<WoWObject> units)
        {
            units.Clear();
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior() { return QuestTurnIn.Branch; }


        public override void Dispose()
        {
            Targeting.Instance.RemoveTargetsFilter -= Instance_RemoveTargetsFilter;
            QuestTurnIn.Dispose();
        }


        public override bool IsDone
        {
            get
            {
                return (((QuestTurnIn != null) && QuestTurnIn.IsDone)     // normal completion
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
                QuestTurnIn = new ForcedQuestTurnIn((uint)QuestId, QuestName, (uint)TurnInId, TurnInLocation);

                if (QuestTurnIn == null)
                {
                    UtilLogMessage("fatal", string.Format("Unable to complete {0}", this.GetType().Name));
                    TreeRoot.Stop();
                }

                Targeting.Instance.RemoveTargetsFilter += Instance_RemoveTargetsFilter;
                QuestTurnIn.OnStart();
            }
        }


        #endregion
    }
}
