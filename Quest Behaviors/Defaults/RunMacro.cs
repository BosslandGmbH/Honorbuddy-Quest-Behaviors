// This work is part of the Buddy Wiki.  You may find it here:
//    http://www.thebuddyforum.com/mediawiki/index.php?title=Category:Honorbuddy_CustomBehavior
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
//  Version 1.3 -- Built-in error handlers (15-Feb-2011, chinajade)
//                   Converted to the new buit-in error handlers provided by
//                   CustomForcedBehavior.  Eliminated the CustomBehaviorUtils
//                   class as a consequence. Yay!
//                   Now accepts an optional QuestId.
//  Version 1.2 -- More error handling (22-Jan-2011, chinajade)
//					 Standardized with other Wiki-provided behaviors.  Includes:
//                   The profile writer will now be told specifically what is at
//                   fault in a failed call to this behavior.  Stray attributes
//					 will also be flagged as these are usually spelling or case-sensitive
//					 errors.
//					 NumOfTimes is now optional, and defaults to '1'.
//  Version 1.1 -- Error handling improvements (17-Jan-2011, chinajade)
//				     Sharpened the error handling for malformed input from Profiles.
//                   Also, fixed a faulty initialization of WaitTime--it was wrongly
//                   set to zero, but the design default was 1500 (milliseconds).
//  Version 1.0 -- Initial release to BuddyWiki (12-Jan-2011, AxaZol)
//

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;

using Styx.Database;
using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;


namespace BuddyWiki.CustomBehavior.RunMacro
{
    public class RunMacro : CustomForcedBehavior
    {
        public RunMacro(Dictionary<string,string> args)
            : base(args)
        {
			_isBehaviorDone		= false;

			CheckForUnrecognizedAttributes(s_recognizedAttributeNames);

			_isAttributesOkay = true;
			_isAttributesOkay = _isAttributesOkay && GetAttributeAsString("Macro", true, "", out _macro);
            _isAttributesOkay = _isAttributesOkay && GetAttributeAsInteger("NumOfTimes", false, "1", 1, 100, out _numOfTimes);
            _isAttributesOkay = _isAttributesOkay && GetAttributeAsInteger("WaitTime", false, "1500", 0, int.MaxValue, out _waitTime);
            _isAttributesOkay = _isAttributesOkay && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out _questId);

			if (_isAttributesOkay)
            {
                // successful parse, now check content coherency
				if (string.IsNullOrEmpty(_macro))
				{
                    UtilLogMessage("error", "'Macro' attribute may not be empty.");
					_isAttributesOkay = false;
                }
            }
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
                PlayerQuest        quest = Styx.StyxWoW.Me.QuestLog.GetQuestById((uint)_questId);

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
				TreeRoot.GoalText = string.Format("RunMacro {0} Times", _numOfTimes);

				for (int counter = 1;   counter <= _numOfTimes;   ++counter)
				{
					TreeRoot.StatusText = string.Format("RunMacro progress... {0}/{1} Times", counter, _numOfTimes);
					Lua.DoString(string.Format("RunMacroText(\"{0}\")", _macro), 0);
					Thread.Sleep(_waitTime);
				}

                _isBehaviorDone = true;
            }
        }

        #endregion


		private bool		_isAttributesOkay;
		private bool		_isBehaviorDone;
		private string		_macro;
		private int			_numOfTimes;
        private int         _questId;
		private int			_waitTime;

		private static Dictionary<string, object>	s_recognizedAttributeNames = new Dictionary<string, object>()
					   {
							{ "Macro",				null },
							{ "NumOfTimes",			null },
							{ "QuestId",		    null },
							{ "WaitTime",			null },
					   };

    }
}  