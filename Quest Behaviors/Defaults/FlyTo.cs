using System;
using System.Collections.Generic;

using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    class FlyTo : CustomForcedBehavior
    {
        public FlyTo(Dictionary<string, string> args)
            : base(args)
        {
			try
			{
                float       distance;
                WoWPoint    location;

                CheckForUnrecognizedAttributes(new Dictionary<string, object>()
                                                {
                                                    { "Distance",   null },
                                                    { "Name",       null },
                                                    { "X",          null },
                                                    { "Y",          null },
                                                    { "Z",          null },
                                                });

                _isAttributesOkay = true;
                _isAttributesOkay &= GetAttributeAsFloat("Distance", false, "10.0", 1.0f, float.MaxValue, out distance); 
                _isAttributesOkay &= GetAttributeAsString("Name", false, "", out _destinationName);
                _isAttributesOkay &= GetXYZAttributeAsWoWPoint(true, WoWPoint.Empty, out location);

                if (_isAttributesOkay)
                {
                    Location = location;
                    Distance = distance;
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


        public float    Distance { get; private set; }
        public WoWPoint Location { get; private set; }

        private bool        _isAttributesOkay;
        private string      _destinationName;
        private Composite   _root;


        #region Overrides of CustomForcedBehavior

        protected override TreeSharp.Composite CreateBehavior()
        {
            return (_root ?? (_root = new Action(ret => Flightor.MoveTo(Location))));
        }


        public override bool IsDone
        {
            get { return (Location.Distance(StyxWoW.Me.Location) <= Distance); }
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
                TreeRoot.GoalText = string.Format("Flying to {0}", (!string.IsNullOrEmpty(_destinationName)
                                                                    ? _destinationName
                                                                    : Location.ToString()));
            }
		}

        #endregion
    }
}
