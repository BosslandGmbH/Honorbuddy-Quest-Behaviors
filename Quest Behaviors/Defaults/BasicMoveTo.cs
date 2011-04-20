// Behavior originally contributed by Natfoth.
//
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_BasicMoveTo
//
using System;
using System.Collections.Generic;
using System.Threading;

using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors.BasicMoveTo
{
    public class BasicMoveTo : CustomForcedBehavior
    {
        public BasicMoveTo(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                UtilLogMessage("warning",   "*****\n"
                                          + "* THIS BEHAVIOR IS DEPRECATED, and may be retired in a near, future release.\n"
                                          + "*\n"
                                          + "* BasicMoveTo adds _no_ _additonal_ _value_ over Honorbuddy's built-in RunTo command.\n"
                                          + "* Please update the profile to use RunTo in preference to the BasicMoveTo Behavior.\n"
                                          + "*****");

                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                Destination     = LegacyGetAttributeAsWoWPoint("Location", false, null, "X/Y/Z")
                                    ?? GetXYZAttributeAsWoWPoint("", true, null)
                                    ?? WoWPoint.Empty;
                DestinationName = GetAttributeAsString_NonEmpty("DestName", false, new [] { "Name" }) ?? "";

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
				UtilLogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
										+ "\nFROM HERE:\n"
										+ except.StackTrace + "\n");
				IsAttributeProblem = true;
			}
        }


        // Attributes provided by caller
        public WoWPoint     Destination { get; private set; }
        public string       DestinationName { get; private set; }

        // Private variables for internal state
        private bool        _isBehaviorDone;
        private Composite   _root;

        // Private properties
        private int                 Counter { get; set; }
        private LocalPlayer         Me { get { return (ObjectManager.Me); } }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                    new Decorator(ret => Counter >= 1,
                        new Action(ret => _isBehaviorDone = true)),

                        new PrioritySelector(

                            new Decorator(ret => Counter == 0,
                                new Action(delegate
                                {

                                    WoWPoint destination1 = new WoWPoint(Destination.X, Destination.Y, Destination.Z);
                                    WoWPoint[] pathtoDest1 = Styx.Logic.Pathing.Navigator.GeneratePath(Me.Location, destination1);

                                    foreach (WoWPoint p in pathtoDest1)
                                    {
                                        while (!Me.Dead && p.Distance(Me.Location) > 3)
                                        {
                                            if (Me.Combat)
                                            {
                                                break;
                                            }
                                            Thread.Sleep(100);
                                            WoWMovement.ClickToMove(p);
                                        }

                                        if (Me.Combat)
                                        {
                                            break;
                                        }
                                    }

                                    if (Me.Combat)
                                    {
                                        
                                        return RunStatus.Success;
                                    }
                                    else if (!Me.Combat)
                                    {
                                        Counter++;
                                        return RunStatus.Success;
                                    }

                                    return RunStatus.Running;
                                })
                                ),

                            new Action(ret => UtilLogMessage("debug", ""))
                        )
                    ));
        }


        public override bool IsDone
        {
            get { return (_isBehaviorDone); }
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
                TreeRoot.GoalText = this.GetType().Name + ": " + DestinationName;
            }
        }

        #endregion
    }
}

