using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using Styx.Logic.BehaviorTree;
using Styx.Logic.Profiles;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class LoadProfile : CustomForcedBehavior
    {
        /// <summary>
        /// ForceLoadProfile by Natfoth
        /// Allows you to load a profile, it needs to be in the same folder as your current profile.
        /// ##Syntax##
        /// ProfileName: The name of the profile with the ".xml" extension.
        /// </summary>
        /// 
        public LoadProfile(Dictionary<string, string> args)
            : base(args)
        {
			try
			{
                string  profileName;

                CheckForUnrecognizedAttributes(new Dictionary<string, object>()
                                                {
                                                    { "Profile",        null},
                                                    { "ProfileName",    null},
                                                });

                UtilLogMessage("info", string.Format("Current profile is \"{0}\"", CurrentProfile));

                _isAttributesOkay = true;

                _isAttributesOkay &= GetAttributeAsString("Profile", false, "", out profileName);
                if (string.IsNullOrEmpty(profileName))
                    _isAttributesOkay = _isAttributesOkay && GetAttributeAsString("ProfileName", true, "1", out profileName);


                // Semantic coherency --
                if (_isAttributesOkay)
                {
                    if (Args.ContainsKey("Profile"))
                        { UtilLogMessage("warning", "Prefer \"ProfileName\" (\"Profile\" is deprecated)."); }

                    if (Args.ContainsKey("ProfileName")  &&  Args.ContainsKey("Profile"))
                    {
                        UtilLogMessage("error", "\"ProfileName\" and \"Profile\" attributes are mutually exclusive.   Use \"ProfileName\" (\"Profile\" is deprecated).");
                        _isAttributesOkay = false;
                    }
                }


                if (_isAttributesOkay)
                {
                    Counter = 0;

                    if (!profileName.ToLower().EndsWith(".xml"))
                        { profileName += ".xml"; }

                    ProfileName = profileName;
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

        public int      Counter { get; set; }
        public String   CurrentProfile { get { return ProfileManager.XmlLocation; } }
        public String   ProfileName { get; set; }

        public static LocalPlayer Me { get { return ObjectManager.Me; } }

        private bool        _isAttributesOkay;
        private bool        _isBehaviorDone;
        private Composite   _root;


        public String NewProfilePath
        {
            get
            {
                string directory = Path.GetDirectoryName(CurrentProfile);
                return (Path.Combine(directory, ProfileName));
            }
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                            new Decorator(ret => Counter > 0,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new Action(ret => _isBehaviorDone = true))),

                           new Decorator(ret => Counter == 0,
                                new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "LoadingProfile - " + NewProfilePath),
                                        new Action(ret => ProfileManager.LoadNew(NewProfilePath, false)),
                                        new Action(ret => Counter++),
                                        new Action(ret => Thread.Sleep(300))
                                    )
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

            else if (!IsDone)
            {
                TreeRoot.GoalText = string.Format("{0}: Running", this.GetType().Name);
            }
        }

        #endregion
    }
}
