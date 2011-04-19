﻿using System;
using System.Collections.Generic;
using System.Diagnostics;

using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// RunLua by HighVoltz
    /// Runs the lua script x amount of times waiting x milliseconds inbetween
    /// ##Syntax##           
    /// Lua: the lua script to run
    /// NumOfTimes: (Optional) - The number of times to execute this script. default:1
    /// QuestId: (Optional) - the quest to perform this action on
    /// WaitTime: (Optional) - The time in milliseconds to wait before executing the next. default:0
    /// </summary>
    public class RunLua : CustomForcedBehavior
    {
        public RunLua(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                Lua         = GetAttributeAsString_NonEmpty("Lua", true, null) ?? "";
                NumOfTimes  = GetAttributeAsInteger("NumOfTimes", false, 1,1000, null) ?? 1;
                QuestId     = GetAttributeAsQuestId("QuestId", false, null) ?? 0;
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;
                WaitTime    = GetAttributeAsInteger("WaitTime", false, 0, int.MaxValue, null) ?? 0;
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
        public string                   Lua { get; private set; }
        public int                      NumOfTimes { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }
        public int                      WaitTime { get; private set; }

        // Private variables for internal state
        private int                 _counter;
        private Composite           _root;
        private readonly Stopwatch  _waitStopwatch = new Stopwatch();


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ??
                (_root = new PrioritySelector(

                    new Action(c =>
                    {
                        if (!_waitStopwatch.IsRunning && WaitTime >0)
                            _waitStopwatch.Start();

                        if (_waitStopwatch.ElapsedMilliseconds < WaitTime)
                            return;

                        WoWInternals.Lua.DoString(Lua);
                        _counter++;
                        _waitStopwatch.Reset();
                    })
                ));
        }

        public override bool IsDone
        {
            get
            {
                return ((_counter >= NumOfTimes)     // normal completion
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
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = this.GetType().Name + ": " + ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
                TreeRoot.StatusText = string.Format("{0}: {1} {2} number of times while waiting {3} inbetween",
                                                    this.GetType().Name, Lua, NumOfTimes, WaitTime);
            }
        }

        #endregion
    }
}
