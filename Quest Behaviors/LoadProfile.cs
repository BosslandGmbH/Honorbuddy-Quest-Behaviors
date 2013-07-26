// Behavior originally contributed by Natfoth.
//
// DOCUMENTATION:
//     
//
using System;
using System.Collections.Generic;
using System.IO;

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;

using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.LoadProfile
{
    [CustomBehaviorFileName(@"LoadProfile")]
    public class LoadProfile : CustomForcedBehavior
    {
        /// <summary>
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
                ProfileName = GetAttributeAs<string>("ProfileName", true, ConstrainAs.StringNonEmpty, new[] { "Profile" }) ?? "";

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
                LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
                                    + "\nFROM HERE:\n"
                                    + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }

        // Attributes provided by caller
        public String ProfileName { get; private set; }

        // Private variables for internal state
        private bool _isBehaviorDone;
        private bool _isDisposed;
        // private Composite           _root;

        // Private properties
        private String CurrentProfile { get { return (ProfileManager.XmlLocation); } }
        private String NewProfilePath { get; set; }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id: LoadProfile.cs 533 2013-05-29 23:45:55Z chinajade $"); } }
        public override string SubversionRevision { get { return ("$Revision: 533 $"); } }


        ~LoadProfile()
        {
            Dispose(false);
        }


        public void Dispose(bool isExplicitlyInitiatedDispose)
        {
            if (!_isDisposed)
            {
                // NOTE: we should call any Dispose() method for any managed or unmanaged
                // resource, if that resource provides a Dispose() method.

                // Clean up managed resources, if explicit disposal...
                if (isExplicitlyInitiatedDispose)
                {
                    // empty, for now
                }

                // Clean up unmanaged resources (if any) here...
                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }

        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return (
                new PrioritySelector(
                // If behavior is complete, nothing to do, so bail...
                            new Decorator(ret => _isBehaviorDone,
                                new Action(delegate { LogMessage("info", "Behavior complete"); })),

                            // If file does not exist, notify of problem...
                            new Decorator(ret => !File.Exists(NewProfilePath),
                                new Action(delegate
                                {
                                    LogMessage("fatal", "Profile '{0}' does not exist.  Download or unpack problem with profile?", NewProfilePath);
                                    _isBehaviorDone = true;
                                })),

                            // Load the specified profile...
                            new Sequence(
                                new Action(delegate
                                {
                                    TreeRoot.StatusText = "Loading profile '" + NewProfilePath + "'";
                                    LogMessage("info", "Loading profile '{0}'", ProfileName);
                                    ProfileManager.LoadNew(NewProfilePath, false);
                                }),
                                new WaitContinue(TimeSpan.FromMilliseconds(300), ret => false, new ActionAlwaysSucceed()),
                                new Action(delegate { _isBehaviorDone = true; })
                                )
                    ));
        }


        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
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

                // Convert path name to absolute, and canonicalize it...
                var absolutePath = Path.Combine(Path.GetDirectoryName(CurrentProfile), ProfileName);
                absolutePath = Path.GetFullPath(absolutePath);
                var canonicalPath = new Uri(absolutePath).LocalPath;
                NewProfilePath = canonicalPath;
            }
        }

        #endregion
    }
}
