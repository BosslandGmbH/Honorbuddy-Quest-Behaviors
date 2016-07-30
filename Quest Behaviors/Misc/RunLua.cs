// Behavior originally contributed by HighVoltz.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

#region Summary and Documentation
// Runs the lua script x amount of times waiting x milliseconds inbetween
// ##Syntax##
// Lua: the lua script to run
// NumOfTimes: (Optional) - The number of times to execute this script. default:1
// QuestId: (Optional) - the quest to perform this action on
// WaitTime: (Optional) - The time in milliseconds to wait before executing the next. default: 0ms
//                         This is a Post-LUA delay.
#endregion


#region Examples
#endregion


#region Usings

using System;
using System.Collections.Generic;
using System.Xml.Linq;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.RunLua
{
    [CustomBehaviorFileName(@"Misc\RunLua")]
    public class RunLua : QuestBehaviorBase
    {
        public RunLua(Dictionary<string, string> args)
            : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            try
            {
                LuaCommand = GetAttributeAs<string>("Lua", true, ConstrainAs.StringNonEmpty, null) ?? string.Empty;
                NumOfTimes = GetAttributeAsNullable<int>("NumOfTimes", false, ConstrainAs.RepeatCount, null) ?? 1;
                WaitTime = GetAttributeAsNullable<int>("WaitTime", false, ConstrainAs.Milliseconds, null) ?? 0;

                GoalText = GetAttributeAs("GoalText", false, ConstrainAs.StringNonEmpty, null) ?? "Running Lua";
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                QBCLog.Exception(except);
                IsAttributeProblem = true;
            }
        }


        // Attributes provided by caller
        private string LuaCommand { get; set; }
        public string GoalText { get; set; }
        private int NumOfTimes { get; set; }
        private int WaitTime { get; set; }

        // Private variables for internal state
        private int _counter;
        private readonly WaitTimer _waitTimer = new WaitTimer(TimeSpan.Zero);

        // DON'T EDIT THIS--it is auto-populated by Git
        protected override string GitId => "$Id$";

        #region Overrides of QuestBehaviorBase

        protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
        {
            //// EXAMPLE:
            //UsageCheck_DeprecatedAttribute(xElement,
            //    Args.Keys.Contains("Nav"),
            //    "Nav",
            //    context => string.Format("Automatically converted Nav=\"{0}\" attribute into MovementBy=\"{1}\"."
            //                              + "  Please update profile to use MovementBy, instead.",
            //                              Args["Nav"], MovementBy));
        }

        protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
        {
            //// EXAMPLE:
            //UsageCheck_SemanticCoherency(xElement,
            //    (!MobIds.Any() && !FactionIds.Any()),
            //    context => "You must specify one or more MobIdN, one or more FactionIdN, or both.");
            //
            //const double rangeEpsilon = 3.0;
            //UsageCheck_SemanticCoherency(xElement,
            //    ((RangeMax - RangeMin) < rangeEpsilon),
            //    context => string.Format("Range({0}) must be at least {1} greater than MinRange({2}).",
            //                  RangeMax, rangeEpsilon, RangeMin));
        }

        protected override Composite CreateMainBehavior()
        {
            return new PrioritySelector(
                // Wait for post-LUA timer to expire...
                new Decorator(context => !_waitTimer.IsFinished,
                    new ActionAlwaysSucceed()),

                // If we've met our completion count, we're done...
                new Decorator(context => _counter >= NumOfTimes,
                    new Action(context => { BehaviorDone(); })),

                // Run the LUA command...
                new Action(c =>
                {
                    Lua.DoString(LuaCommand);
                    _counter++;
                    _waitTimer.WaitTime = TimeSpan.FromMilliseconds(WaitTime);
                    _waitTimer.Reset();
                }));
        }

        public override void OnStart()
        {
            // Acquisition and checking of any sub-elements go here.
            // A common example:
            //     HuntingGrounds = HuntingGroundsType.GetOrCreate(Element, "HuntingGrounds", HuntingGroundCenter);
            //     IsAttributeProblem |= HuntingGrounds.IsAttributeProblem;

            // Let QuestBehaviorBase do basic initialization of the behavior, deal with bad or deprecated attributes,
            // capture configuration state, install BT hooks, etc.  This will also update the goal text.
            var isBehaviorShouldRun = OnStart_QuestBehaviorCore();

            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (isBehaviorShouldRun)
            {
                this.UpdateGoalText(QuestId);
                TreeRoot.StatusText = string.Format("{0}: {1} {2} number of times while waiting {3} inbetween",
                                                    GetType().Name, LuaCommand, NumOfTimes, WaitTime);

                // NB: The _waitTimer is initialzed to zero, so there will be no 'initial delay'.
                // This is what the user expects.
            }
        }

        #endregion
    }
}
