// Behavior originally contributed by Bobby53.
//
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_WaitTimer
//
using System;
using System.Collections.Generic;
using System.Threading;

using Styx.Logic;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class ForcedDismount : CustomForcedBehavior
    {
        public enum ForcedDismountType
        {
            Any,
            Ground,
            Flying,
            Water
        }

        /// <summary>
        /// forces character to dismount.  additionally forces Druids
        /// to leave Flight Form and Swift Flight Form. if in flight,
        /// will descend straight down before dismount        
        /// 
        /// ##Syntax##
        /// [Optional] QuestId: The id of the quest (defaults to 0)
        /// [Optional] QuestName:  documentation only
        /// [Optional] MountType:  ignored currently
        /// </summary>
        /// 
        public ForcedDismount(Dictionary<string, string> args)
            : base(args)
        {
			try
			{
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                QuestId     = GetAttributeAsQuestId("QuestId", false, null) ?? 0;
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;

                GetAttributeAsString_NonEmpty("QuestName", false, null);     // (doc only - not used)
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
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }

        // Private variables for internal state
        private bool                _isBehaviorDone;
        private Composite           _root;

        // Private properties
        private LocalPlayer         Me { get { return (ObjectManager.Me); } }


        private void Dismount()
        {
            // if in the air, 
            if (StyxWoW.Me.IsFlying)
            {
                UtilLogMessage("info", "Descending before dismount");
                Navigator.PlayerMover.Move(WoWMovement.MovementDirection.Descend);
                while (StyxWoW.Me.IsFlying)
                    { Thread.Sleep(250); }

                Navigator.PlayerMover.MoveStop();
            }

            if (StyxWoW.Me.Auras.ContainsKey("Flight Form"))
            {
                UtilLogMessage("info", "Cancelling Flight Form");
                CancelAura("Flight Form");
            }

            else if (StyxWoW.Me.Auras.ContainsKey("Swift Flight Form"))
            {
                UtilLogMessage("info", "Cancelling Swift Flight Form");
                CancelAura("Swift Flight Form");
            }

            else
            {
                UtilLogMessage("info", "Dismounting");
                Mount.Dismount();
            }
        }


        private void CancelAura(string sAura)
        {
            Lua.DoString(string.Format("RunMacroText(\"/cancelaura {0}\")", sAura), 0);
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            if (_root == null)
            {
                _root = new PrioritySelector(
                    new Decorator(
                        ret => !Me.Mounted,
                        new Action(ret => _isBehaviorDone = true)),
                    new Decorator(
                        ret => Me.Mounted,
                        new Action(ret => Dismount()))
                );
            }
            return _root;
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
                TreeRoot.GoalText = "Dismounting";
			}
		}

        #endregion
    }
}
