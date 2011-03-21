// This work is part of the Buddy Wiki.  You may find it here:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Category:Honorbuddy_CustomBehavior
//
// This work is licensed under the 
//    Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//    http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//    Creative Commons
//    171 Second Street, Suite 300
//    San Francisco, California, 94105, USA. 
//
// Release History:
//  Version 1.1 -- Built-in error handlers (15-Feb-2011, chinajade)
//                   Converted to the new buit-in error handlers provided by
//                   CustomForcedBehavior.
//                   Now accepts an optional QuestId.
//  Version 1.0 -- Initial Release created and contributed to the Buddy Wiki by Caytchen
//
using System;
using System.Collections.Generic;
using System.Drawing;

using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Questing;


namespace Styx.Bot.Quest_Behaviors
{
    public class Message : CustomForcedBehavior
    {
        public Message(Dictionary<string,string> args)
            : base(args)
        {
			try
			{
                string  colorNameGoal;
                string  colorNameLog;

			    CheckForUnrecognizedAttributes(new Dictionary<string, object>()
					                           {
							                        { "GoalColor",		null },
							                        { "LogColor",		null },
							                        { "QuestId",		null },
							                        { "Text",			null },
					                           });

                _isAttributesOkay = true;
                _isAttributesOkay &= GetAttributeAsString("Text", true, "", out _text);
                _isAttributesOkay &= GetAttributeAsString("LogColor", false, "Black", out colorNameLog);
                _isAttributesOkay &= GetAttributeAsString("GoalColor", false, "", out colorNameGoal);
                _isAttributesOkay &= GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out _questId);


                // Convert the color names into actual colors...
                if (!string.IsNullOrEmpty(colorNameLog))
                    _colorLog = Color.FromName(colorNameLog);

                if (!string.IsNullOrEmpty(colorNameGoal))
                    _colorGoal = Color.FromName(colorNameGoal);
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


        private Color       _colorGoal;
        private Color       _colorLog;
        private bool		_isAttributesOkay;
		private bool		_isBehaviorDone;
        private int         _questId;
        private string      _text;


        #region Overrides of CustomForcedBehavior

        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone    // normal completion
                        ||  !UtilIsProgressRequirementsMet(_questId, 
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
                string		logMessage	= "[Profile Message]: " + _text;
			
                Logging.Write(_colorLog, logMessage);
			
                // TODO: Goal has no color, so GoalColor is ignored
                if (_colorGoal != null)
                    {  TreeRoot.GoalText = _text; }

                _isBehaviorDone = true;
            }
        }

        #endregion
    }
}
