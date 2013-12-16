// Behavior originally contributed by Unknown.
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
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Xml.Linq;

using Bots.Grind;
using CommonBehaviors.Decorators;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.FlyTo
{
    [CustomBehaviorFileName(@"FlyTo")]
    class FlyTo : QuestBehaviorBase
    {
        #region Constructor and Argument Processing
        public FlyTo(Dictionary<string, string> args)
            : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            try
            {
                Destination = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
                DestinationName = GetAttributeAs<string>("DestName", false, ConstrainAs.StringNonEmpty, new[] { "Name" }) ?? string.Empty;
                Distance = GetAttributeAsNullable<double>("Distance", false, new ConstrainTo.Domain<double>(0.25, double.MaxValue), null) ?? 10.0;
                Land = GetAttributeAsNullable<bool>("Land", false, null, null) ?? false;
                IgnoreIndoors = GetAttributeAsNullable<bool>("IgnoreIndoors", false, null, null) ?? false;

                if (string.IsNullOrEmpty(DestinationName))
                    { DestinationName = Destination.ToString(); }
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


        // Attributes provided by caller
        private WoWPoint Destination { get; set; }
        private string DestinationName { get; set; }
        private double Distance { get; set; }
        private bool Land { get; set; }
        private bool IgnoreIndoors { get; set; }
        #endregion


        #region Private and Convenience variables
        #endregion


        #region Overrides of CustomForcedBehavior
        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id$"); } }
        public override string SubversionRevision { get { return ("$Revision$"); } }

        // CreateBehavior supplied by QuestBehaviorBase.
        // Instead, provide CreateMainBehavior definition.

        // Dispose provided by QuestBehaviorBase.

        // IsDone provided by QuestBehaviorBase.
        // Call the QuestBehaviorBase.BehaviorDone() method when you want to indicate your behavior is complete.

        // OnFinished provided by QuestBehaviorBase.

        public override void OnStart()
        {
            // Let QuestBehaviorBase do basic initializaion of the behavior, deal with bad or deprecated attributes,
            // capture configuration state, install BT hooks, etc.  This will also update the goal text.
            var isBehaviorShouldRun = OnStart_QuestBehaviorCore(string.Format("Flying to Destination: {0} ({1})", DestinationName, Destination));

            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (isBehaviorShouldRun)
            {
                // Disable any settings that may cause us to dismount --
                // When we mount for travel via FlyTo, we don't want to be distracted by other things.
                // NOTE: the ConfigMemento in QuestBehaviorBase restores these settings to their
                // normal values when OnFinished() is called.
                LevelBot.BehaviorFlags &= ~(BehaviorFlags.Loot | BehaviorFlags.Pull);
            }
        }
        #endregion


        #region Main Behaviors
        protected override Composite CreateMainBehavior()
        {
            return new PrioritySelector(
                // Arrived at destination...
                new Decorator(context => Destination.DistanceSqr(StyxWoW.Me.Location) < (Distance * Distance),
                    new Sequence(
                        // Land if we need to...
                        // NB: The act of landing may cause us to exceed the Distance specified.
                        new DecoratorContinue(context => Land && Me.Mounted,
                            new Mount.ActionLandAndDismount()),
                        // Done...
                        new Action(context => BehaviorDone("Arrived at destination"))
                    )),

                // Don't run FlyTo when there is a poi set
                new DecoratorIsPoiType(PoiType.None,
                    new Action(context => Flightor.MoveTo(Destination, !IgnoreIndoors))),

                // Tell user why we've suspended FlyTo...
                new CompositeThrottle(TimeSpan.FromSeconds(10),
                    new Action(context =>
                        {
                            QBCLog.DeveloperInfo("FlyTo temporarily suspended due to {0}", BotPoi.Current);
                        }))
            );
        }
        #endregion
    }
}
