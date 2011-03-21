using System;
using System.Collections.Generic;

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
                int         questId;
                VendorType  vendorType;


                CheckForUnrecognizedAttributes(new Dictionary<string, object>()
                                                {
                                                    { "QuestId",        null },
                                                    { "VendorType",     null },
                                                });

                _isAttributesOkay = true;
                _isAttributesOkay &= GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
                _isAttributesOkay &= GetAttributeAsEnum<VendorType>("VendorType", true, VendorType.Repair, out vendorType);

                if (_isAttributesOkay)
                {
                    QuestId = (uint)questId;
                    Type    = vendorType;
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
				_isAttributesOkay = false;
			}
        }


        public uint         QuestId { get; private set; }
        public VendorType   Type { get; private set; }
  
        private bool    _isAttributesOkay;
        private bool    _isBehaviorDone;

      
        #region Overrides of CustomForcedBehavior

        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone    // normal completion
                        ||  !UtilIsProgressRequirementsMet((int)QuestId, 
                                                           QuestInLogRequirement.InLog, 
                                                           QuestCompleteRequirement.NotComplete));
            }
        }


        public override void OnStart()
        {
			if (!_isAttributesOkay)
			{
				UtilLogMessage("error", "Stopping Honorbuddy.  Please repair the profile!");

                // *Never* want to stop Honorbuddy (e.g., TreeRoot.Stop()) in the constructor --
                // This would defeat the "ProfileDebuggingMode" configurable that builds an instance of each
                // used behavior when the profile is loaded.
				TreeRoot.Stop();
			}

            else if (!IsDone)
            {
                switch (Type)
                {
                    case VendorType.Mail:
                        Vendors.ForceMail = true;
                        break;

                    case VendorType.Repair:
                        Vendors.ForceRepair = true;
                        break;

                    case VendorType.Sell:
                        Vendors.ForceSell = true;
                        break;

                    case VendorType.Train:
                        Vendors.ForceTrainer = true;
                        break;
                }

                _isBehaviorDone = true;
            }
        }

        #endregion
    }
}
