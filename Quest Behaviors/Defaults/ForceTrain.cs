using System;
using System.Collections.Generic;

using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Questing;


namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// ForceTrain by Nesox
    /// Allows you to force HB to go train spells. 
    /// ForceSetVendor can also be used to achieve this.
    /// ##Syntax##
    /// QuestId: Id of the quest. If 0 is specified it will run anways.
    /// </summary>
    public class ForceTrain : CustomForcedBehavior
    {
        public ForceTrain(Dictionary<string, string> args)
            : base(args)
        {
			try
			{
                int     questId;


                UtilLogMessage("warning",   "*****\n"
                                          + "* THIS BEHAVIOR IS DEPRECATED, and may be retired in a near, future release.\n"
                                          + "*\n"
                                          + "* ForceTrain adds _no_ _additonal_ _value_ over the ForceSetVendor behavior.\n"
                                          + "* Please update the profile to use ForceSetVendor in preference to this Behavior.\n"
                                          + "*****");

                CheckForUnrecognizedAttributes(new Dictionary<string, object>()
                                                {
                                                    { "QuestId",    null },
                                                });

                _isAttributesOkay = true;
                _isAttributesOkay &= GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);


                if (_isAttributesOkay)
                {
                    QuestId = (uint)questId;
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


        public uint     QuestId { get; set; }

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
                LevelbotSettings.Instance.FindVendorsAutomatically = true;
                Vendors.ForceTrainer = true;
                _isBehaviorDone = true;
            }
        }

        #endregion
    }
}
