using System;
using System.Collections.Generic;
using System.Drawing;
using System.Xml;

using Styx.Helpers;
using Styx.Logic.Questing;
using Styx.Logic.BehaviorTree;

using TreeSharp;


namespace Styx.Bot.Quest_Behaviors
{
    public class Halt : CustomForcedBehavior
    {
        /// <summary>
        /// Halt by Bobby53
        /// 
        /// Stops the Quest Bot.  Will write 'Msg' to the log and Goal Text.
        /// Also write the line number it halted at for easily locating in profile.
        /// 
        /// Useful for testing assumptions in quest profile and during profile
        /// development to force profile to automatically stop at designated point
        /// 
        /// ##Syntax##
        /// [optional] QuestId: Id of the quest (default is 0)
        /// [optional] Msg: text value to display (default says stopped by profile)
        /// [optional] Color: color to use for message in log (default is red)
        /// 
        /// Note:  QuestId behaves the same as on every other behavior.  If 0, then
        /// halt always occurs.  Otherwise, for non-zero QuestId only halts if the
        /// character has the quest and its not completed
        /// </summary>
        public Halt(Dictionary<string, string> args)
            : base(args)
        {
			try
			{
                Color   color = Color.Black;
                string  colorName;
                string  message;
                int     questId;

                CheckForUnrecognizedAttributes(new Dictionary<string, object>()
                                                {
                                                    { "Msg",        null },
                                                    { "QuestId",    null },
                                                    { "Color",      null },
                                                });

                _isAttributesOkay = true;
                _isAttributesOkay &= GetAttributeAsString("Color", false, "Red", out colorName);
                _isAttributesOkay &= GetAttributeAsString("Msg", false, "Quest Profile HALT", out message);
                _isAttributesOkay &= GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);

                if (!string.IsNullOrEmpty(colorName))
                    { color = Color.FromName(colorName); }


                if (_isAttributesOkay)
                {
                    Color = color;
                    Message = message;
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

        public Color    Color { get; set; }
        public string   Message { get; set; }
        public uint     QuestId { get; set; }

        private bool    _isAttributesOkay;


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return (null);
        }


        public override bool IsDone
        {
            get
            {
                return (!UtilIsProgressRequirementsMet((int)QuestId, 
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
                UtilLogMessage("info", "Halting due to profile request.");

                Logging.Write(Color, "\n{0}", Message);
                TreeRoot.GoalText = Message;

                if (((IXmlLineInfo)Element).HasLineInfo())
                    { Logging.Write(Color, "stopped @ line {0}\n", ((IXmlLineInfo)Element).LineNumber); }

                Logging.Write(" ");
                TreeRoot.Stop();
            }
        }

        #endregion
    }
}

