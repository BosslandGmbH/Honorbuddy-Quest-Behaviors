// Behavior originally contributed by Unknown.
//
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_ForceSetVendor
//
using System;
using System.Collections.Generic;

using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Questing;


namespace Styx.Bot.Quest_Behaviors
{
    public class ForceSetVendor : CustomForcedBehavior
    {
        public enum VendorType
        {
            Mail,
            Repair,
            Sell,
            Train,
        }

        /// <summary>
        /// Behavior for forcing train/mail/vendor/repair
        /// Example usage: <CustomBehavior QuestId="14324" File="ForceSetVendor" VendorType="Train" />
        /// QuestId is optional, if you don't use it make sure you put this tag inside an 'If'
        /// </summary> 
        public ForceSetVendor(Dictionary<string, string> args)
            : base(args)
        {
			try
			{
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                DoMail  = GetAttributeAsBoolean("DoMail", false, null) ?? false;
                DoRepair = GetAttributeAsBoolean("DoRepair", false, null) ?? false;
                DoSell  = GetAttributeAsBoolean("DoSell", false, null) ?? false;
                DoTrain = GetAttributeAsBoolean("DoTrain", false, null) ?? false;
                QuestId = GetAttributeAsQuestId("QuestId", false, null) ?? 0;
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;

                // "VendorType" attribute is required if no Do* attribute is specified
                VendorType  type    = GetAttributeAsEnum<VendorType>("VendorType", !(DoMail || DoRepair || DoSell || DoTrain), null) ?? VendorType.Repair;
                switch (type)
                {
                  case VendorType.Mail:
                    DoMail = true;
                    break;
                  case VendorType.Repair:
                    DoRepair = true;
                    break;
                  case VendorType.Sell:
                    DoSell = true;
                    break;
                  case VendorType.Train:
                    DoTrain = true;
                    break;
                  default:
                    IsAttributeProblem = true;
                    throw (new NotImplementedException("Unexpected VendorType"));
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
        public bool                     DoMail { get; private set; }
        public bool                     DoRepair { get; private set; }
        public bool                     DoSell { get; private set; }
        public bool                     DoTrain { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }

        // Private variables for internal state
        private bool    _isBehaviorDone;

      
        #region Overrides of CustomForcedBehavior

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
                List<string>        reasons     = new List<string>();

                if (DoMail)
                    { reasons.Add("Mail"); }
                if (DoRepair)
                    { reasons.Add("Repair"); }
                if (DoSell)
                    { reasons.Add("Sell"); }
                if (DoTrain)
                    { reasons.Add("Train"); }

                TreeRoot.GoalText = "Scheduled run for " + string.Join(", ", reasons.ToArray());

                LevelbotSettings.Instance.FindVendorsAutomatically = true;
                Vendors.ForceMail       |= DoMail;
                Vendors.ForceRepair     |= DoRepair;
                Vendors.ForceSell       |= DoSell;
                Vendors.ForceTrainer    |= DoTrain;

                _isBehaviorDone = true;
            }
        }

        #endregion
    }
}
