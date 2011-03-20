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
                WoWPoint destination;

                UtilLogMessage("warning",   "*****\n"
                                          + "* THIS BEHAVIOR IS DEPRECATED, and may be retired in a near, future release.\n"
                                          + "*\n"
                                          + "* BasicMoveTo adds _no_ _additonal_ _value_ over Honorbuddy's built-in RunTo command.\n"
                                          + "* Please update the profile to use RunTo in preference to the BasicMoveTo Behavior.\n"
                                          + "*****");

                CheckForUnrecognizedAttributes(new Dictionary<string, object>()
                                                {
                                                    {"Location",    null},
                                                    {"Name",        null},
                                                    {"X",           null},
                                                    {"Y",           null},
                                                    {"Z",           null},
                                                });

                _isAttributesOkay = true;
                _isAttributesOkay &= GetAttributeAsWoWPoint("Location", false, WoWPoint.Empty, out destination);
                _isAttributesOkay &= GetAttributeAsString("Name", false, "", out _destinationName);

                // If attribute Location was not present, then X/Y/Z *must* be specified --
                //  Location is allowed for backward-compatibility, but X/Y/Z is modern and preferred.
                if (destination == WoWPoint.Empty)
                    { _isAttributesOkay &= GetXYZAttributeAsWoWPoint(true, WoWPoint.Empty, out destination); }


                if (_isAttributesOkay)
                {
                    Counter   =  0;
                    MovePoint =  destination;
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


        public int          Counter { get; set; }
        public WoWPoint     MovePoint { get; private set; }

        private string      _destinationName;
        private bool        _isAttributesOkay;
        private bool        _isBehaviorDone;
        private Composite   _root;

        private static LocalPlayer  s_me = ObjectManager.Me;


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

                                    WoWPoint destination1 = new WoWPoint(MovePoint.X, MovePoint.Y, MovePoint.Z);
                                    WoWPoint[] pathtoDest1 = Styx.Logic.Pathing.Navigator.GeneratePath(s_me.Location, destination1);

                                    foreach (WoWPoint p in pathtoDest1)
                                    {
                                        while (!s_me.Dead && p.Distance(s_me.Location) > 3)
                                        {
                                            if (s_me.Combat)
                                            {
                                                break;
                                            }
                                            Thread.Sleep(100);
                                            WoWMovement.ClickToMove(p);
                                        }

                                        if (s_me.Combat)
                                        {
                                            break;
                                        }
                                    }

                                    if (s_me.Combat)
                                    {
                                        
                                        return RunStatus.Success;
                                    }
                                    else if (!s_me.Combat)
                                    {
                                        Counter++;
                                        return RunStatus.Success;
                                    }

                                    return RunStatus.Running;
                                })
                                ),

                            new Action(ret => Logging.Write(""))
                        )
                    ));
        }


        public override bool IsDone
        {
            get { return (_isBehaviorDone); }
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

            else
            {
                TreeRoot.GoalText = "Moving to " + ((!string.IsNullOrEmpty(_destinationName))
                                                    ? _destinationName
                                                    : MovePoint.ToString());
            }
        }

        #endregion


        #region Legacy XML support

        // The methods below support antiquated (and non-XML-like) Attributes (e.g., Location) --
        // As such, we don't want them in the CustomForcedBehavior base class.  You may find these methods
        // in several of the 'old style' behaviors.  To prevent namespace collisions,
        // we have to assure our namespace is unique, thus we've appended the behavior name to the namespace
        // that originally existed.

        public bool GetAttributeAsWoWPoint(string attributeName,
                                           bool isAttributeRequired,
                                           WoWPoint defaultValue,
                                           out WoWPoint returnedValue
                                           )
        {
            string  attributeValueAsString;
            bool    isSuccess;

            returnedValue = new WoWPoint(defaultValue.X, defaultValue.Y, defaultValue.Z);

            isSuccess = UtilFetchAttributeValue(attributeName,
                                                isAttributeRequired,
                                                "", // no default value
                                                out attributeValueAsString);
            if (!isSuccess)
                { return (false); }


            string[] discreteCoordinates = attributeValueAsString.Split(' ');

            if (discreteCoordinates.Length != 3)
            {
                UtilLogMessage("error", string.Format("The '{0}' attribute's value expected 3 space-separated floating point numbers"
                                                      + "--found {1} (saw '{2}').",
                                                      attributeName,
                                                      discreteCoordinates.Length,
                                                      attributeValueAsString));
                return (false);
            }


            float   x;
            float   y;
            float   z;

            // Report problems with each component that is in error before returning --
            // This minimizes nickel-and-diming the caller with error messages.
            isSuccess = true;
            if (!float.TryParse(discreteCoordinates[0], out x))
            {
                UtilReportMalformed(attributeName + "-X", discreteCoordinates[0]);
                isSuccess = false;
            }

            if (!float.TryParse(discreteCoordinates[1], out y))
            {
                UtilReportMalformed(attributeName + "-Y", discreteCoordinates[1]);
                isSuccess = false;
            }

            if (!float.TryParse(discreteCoordinates[2], out z))
            {
                UtilReportMalformed(attributeName + "-Z", discreteCoordinates[2]);
                isSuccess = false;
            }

            if (isSuccess)
            {
                returnedValue.X = x;
                returnedValue.Y = y;
                returnedValue.Z = z;
            }

            return (isSuccess);
        }


        private bool UtilFetchAttributeValue(string attributeName,
                                             bool isAttributeRequired,
                                             string defaultValueAsString,
                                             out string attributeValueAsString)
        {
            bool    isAttributePresent = Args.TryGetValue(attributeName, out attributeValueAsString);


            // Is required attribute missing?
            if (isAttributeRequired && !isAttributePresent)
            {
                UtilLogMessage("error", string.Format("The '{0}' attribute is required, but missing."
                                                      + "  (Attribute names are case-sensitive.)",
                                                      attributeName));
                return (false);
            }

            // Attribute is either present, or not required --
            // If attribute is not present, then use the default value
            if (!isAttributePresent)
                { attributeValueAsString = defaultValueAsString; }


            return (true);
        }


        private void UtilReportMalformed(string attributeName,
                                    string attributeValue)
        {
            UtilLogMessage("error", string.Format("The '{0}' attribute's value is malformed. (saw '{1}')",
                                                  attributeName,
                                                  attributeValue));
        }

        #endregion
    }

}

