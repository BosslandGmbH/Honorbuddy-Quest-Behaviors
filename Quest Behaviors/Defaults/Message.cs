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
            string  colorNameGoal  = "";
            string  colorNameLog   = "";

			CheckForUnrecognizedAttributes(s_recognizedAttributeNames);

            _isAttributesOkay = true;
            _isAttributesOkay = _isAttributesOkay && GetAttributeAsString("Text", true, "", out _text);
            _isAttributesOkay = _isAttributesOkay && GetAttributeAsString("LogColor", false, "Black", out colorNameLog);
            _isAttributesOkay = _isAttributesOkay && GetAttributeAsString("GoalColor", false, "", out colorNameGoal);
            _isAttributesOkay = _isAttributesOkay && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out _questId);


            // Convert the color names into actual colors...
            if (!string.IsNullOrEmpty(colorNameLog))
                _colorLog = Color.FromName(colorNameLog);

            if (!string.IsNullOrEmpty(colorNameGoal))
                _colorGoal = Color.FromName(colorNameGoal);
        }


        private void UtilLogMessage(string messageType,
                                    string message)
        {
            string  behaviorName = this.GetType().Name;
            Color   messageColor = Color.Black;

            if (messageType == "error")
                messageColor = Color.Red;
            else if (messageType == "warning")
                messageColor = Color.DarkOrange;
            else if (messageType == "info")
                messageColor = Color.Navy;

            Logging.Write(messageColor, String.Format("[Behavior: {0}({1})]: {2}", behaviorName, messageType, message));
        }


        #region Overrides of CustomForcedBehavior

        public override bool IsDone
        {
            get
            {
                PlayerQuest        quest = StyxWoW.Me.QuestLog.GetQuestById((uint)_questId);

                // Note that a _questId of zero is never complete (by definition), it requires the behavior to complete...
                return (_isBehaviorDone                                                         // normal completion
                        ||  ((_questId != 0) && (quest == null))                                // quest not in our log
                        ||  ((_questId != 0) && (quest != null) && quest.IsCompleted));         // quest is done
            }
        }


        public override void OnStart()
        {
			if (!_isAttributesOkay)
			{
				UtilLogMessage("error", "Stopping Honorbuddy.  Please repair the profile!");
				TreeRoot.Stop();
			}

			else if (!IsDone)
            {
                string		logMessage	= "[Profile Message]: " + _text;
			
                Logging.Write(_colorLog, logMessage);
			
                // TODO: Goal has no color, so GoalColor is ignored
                if (_colorGoal != null)
                {
                    TreeRoot.GoalText = _text;
                }

                _isBehaviorDone = true;
            }
        }

        #endregion


        private Color       _colorGoal;
        private Color       _colorLog;
        private bool		_isAttributesOkay;
		private bool		_isBehaviorDone;
        private int         _questId;
        private string      _text;

		private static Dictionary<string, object>	s_recognizedAttributeNames = new Dictionary<string, object>()
					   {
							{ "GoalColor",		null },
							{ "LogColor",		null },
							{ "QuestId",		null },
							{ "Text",			null },
					   };
    }
}
