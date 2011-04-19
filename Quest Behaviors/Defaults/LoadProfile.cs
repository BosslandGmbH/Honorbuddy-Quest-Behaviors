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
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                ProfileName = GetAttributeAsString_NonEmpty("ProfileName", true, new [] { "Profile" }) ?? "";

                if (!ProfileName.ToLower().EndsWith(".xml"))
                    { ProfileName += ".xml"; }
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
        public String               ProfileName { get; private set; }

        // Private variables for internal state
        private bool                _isBehaviorDone;
        private Composite           _root;

        // Private properties
        private int                 Counter { get; set; }
        private String              CurrentProfile { get { return (ProfileManager.XmlLocation); } }
        private String              NewProfilePath { get { string directory = Path.GetDirectoryName(CurrentProfile);
                                                            return (Path.Combine(directory, ProfileName));
                                                    }}


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
                                        new Action(ret => UtilLogMessage("info", "Loading profile: \"{0}\"", ProfileName)),
                                        new Action(ret => ProfileManager.LoadNew(NewProfilePath, false)),
                                        new Action(ret => Counter++),
                                        new Action(ret => Thread.Sleep(300))
                                    )
                                )
                    ));
        }


        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone);
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
                TreeRoot.GoalText = this.GetType().Name + ": In Progress";
            }
        }

        #endregion
    }
}
