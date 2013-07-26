#region Information
// Behavior originally contributed by AknA.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

// Custom behavior created for Liquid Zebra.
// 
// Examples :
// This would make the profile pause until all party members are within interact range (and then continue the profile).
// <CustomBehavior File="Misc\LoadProfileOn" CheckRange="1" />
// This would make the profile pause until all party members are within interact range IF all party members are above level 10 (if not all is above level 10 QB won't check range.)
// <CustomBehavior File="Misc\LoadProfileOn" CheckRange="1" MinLevel="10" />
// This would load a localy stored profile (it needs to be in the same directory as the last localy loaded profile)
// <CustomBehavior File="Misc\LoadProfileOn" ProfileName="[Rep] Nat Pagle.xml" />
// This would load a remote profile from my SVN.
// <CustomBehavior File="Misc\LoadProfileOn" ProfileName="[Rep] Nat Pagle.xml" RemotePath="http://akna-hb.googlecode.com/svn/trunk/Profiles/[Rep] Nat Pagle/" />
// And to combine everything.
// This would load a remote profile if everyone in your party is above level 50 and within interact range.
// <CustomBehavior File="Misc\LoadProfileOn" MinLevel="50" CheckRange="1" ProfileName="[Rep] Nat Pagle.xml" RemotePath="http://akna-hb.googlecode.com/svn/trunk/Profiles/[Rep] Nat Pagle/" />
// NOTE : In the last example, if everyone in the party is >= 50, the profile will pause until everyone is within interact range and then load the remote profile.
// NOTE : However if someone in your party is below level 50, it won't load the profile and it won't check range.
//
// MinLevel    : (OPTIONAL) If not everyone in your party (including yourself) is above level then QB will exit doing nothing.
// CheckRange  : (OPTIONAL, default = 0) If set to 1 QB will pause the profile until every party member is within your interact range.
// ProfileName : (OPTIONAL) Name of the profile to load (If RemotePath isn't included then the profile to load must be in the same directory as previous local profile).
// RemotePath  : (OPTIONAL) URL to where you have your remote profile stored.
//
// Special thanks to Natfoth for helping me understand and improving my Behavior Tree programming.
#endregion

#region using
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Media;

using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using CommonBehaviors.Actions;
using Action = Styx.TreeSharp.Action;
#endregion

namespace Styx.Bot.Quest_Behaviors {
    [CustomBehaviorFileName(@"Misc\LoadProfileOn")]
    public class LoadProfileOn : CustomForcedBehavior {
        public LoadProfileOn(Dictionary<string, string> args)
            : base(args) {
            try {
                MinLevel = GetAttributeAsNullable("MinLevel", false, ConstrainAs.Milliseconds, null) ?? 0;
                CheckRange = GetAttributeAsNullable("CheckRange", false, ConstrainAs.Milliseconds, null) ?? 0;
                ProfileName = GetAttributeAs("ProfileName", false, ConstrainAs.StringNonEmpty, null) ?? "";
                RemotePath = GetAttributeAs(@"RemotePath", false, ConstrainAs.StringNonEmpty, null) ?? "";
            }

            catch (Exception except) {
                LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
                                    + "\nFROM HERE:\n"
                                    + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }

        #region Variables
        // Attributes provided by caller
        public int MinLevel { get; private set; }
        public int CheckRange { get; private set; }
        public string ProfileName { get; private set; }
        public string RemotePath { get; private set; }

        // Private variables for internal state
        private static bool _isBehaviorDone;
        private bool _IsDisposed;
        private Composite _Root;
        public static LocalPlayer Me { get { return StyxWoW.Me; } }
        private String CurrentProfile { get { return (ProfileManager.XmlLocation); } }
        private String NewLocalProfilePath {
            get { return (Path.Combine(Path.GetDirectoryName(CurrentProfile), ProfileName)); }
        }
        private String NewRemoteProfilePath {
            get { return (Path.Combine(RemotePath, ProfileName)); }
        }
        #endregion

        #region Dispose
        ~LoadProfileOn() { Dispose(false); }

        public void Dispose(bool isExplicitlyInitiatedDispose) {
            if (!_IsDisposed) {
                // NOTE: we should call any Dispose() method for any managed or unmanaged
                // resource, if that resource provides a Dispose() method.

                // Clean up managed resources, if explicit disposal...
                if (isExplicitlyInitiatedDispose) { }  // empty, for now

                // Clean up unmanaged resources (if any) here...
                BotEvents.OnBotStop -= BotEvents_OnBotStop;
                _isBehaviorDone = false;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }
            _IsDisposed = true;
        }

        public override void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        public void BotEvents_OnBotStop(EventArgs args) { Dispose(); }
        #endregion

        #region Methods
        private bool CheckLevel() {
            return Me.Level >= MinLevel && Me.GroupInfo.RaidMembers.All(a => a.ToPlayer() == null || (!a.ToPlayer().IsPet && (a.ToPlayer().Level >= MinLevel)));
        }

        private bool CheckPartyRange() {
            foreach (var p in StyxWoW.Me.GroupInfo.RaidMembers.Select(a => a.ToPlayer())) {
                if (p == null) { 
                    Logging.Write(Colors.LightBlue, "Can't scan party member, assuming member is too far away");
                    return false;
                }
                if (p.Name != Me.Name && WoWMovement.CalculatePointFrom(p.Location, 3).Distance(Me.Location) > p.InteractRange) {
                    return false;
                }
            }
            return true;
        }

        static private bool UrlExists(string url) {
            var a = true;
            var fileExists = WebRequest.Create(new Uri(url));
            fileExists.Method = "HEAD";
            fileExists.Timeout = 30000;
            try {
                using (fileExists.GetResponse()) { }
            }
            catch (WebException request) {
                Logging.WriteDiagnostic(Colors.Red, request.Message);
                a = false;
            }
            return a;
        }
        #endregion

        #region Overrides of CustomForcedBehavior
        protected override Composite CreateBehavior() {
            return _Root ?? (_Root =
                new PrioritySelector(context => !_isBehaviorDone,
                    // Should we check for partymember minumum level ?
                    new Decorator(context => (MinLevel > 0),
                        new Sequence(
                            // Someone is below MinLevel.
                            new DecoratorContinue(context => !CheckLevel(),
                                new Sequence(
                                    new Action(context => Logging.Write(Colors.DeepSkyBlue, string.Format("[LoadProfileOn]: Someone in your party is below level {0}.", MinLevel))),
                                    new Action(context => _isBehaviorDone = true)
                                )
                            ),
                            // Everyone is equal or above MinLevel.
                            new DecoratorContinue(context => CheckLevel(),
                                new Action(context => MinLevel = 0)
                            )
                        )
                    ),

                    // Should we wait for party members to be in range ?
                    new Decorator(context => (CheckRange != 0),
                        new Sequence(
                            // Everyone isn't within interact range, lets wait abit before checking again.
                            new DecoratorContinue(context => !CheckPartyRange(),
                                new WaitContinue(TimeSpan.FromMilliseconds(300), context => false, new ActionAlwaysSucceed())
                            ),
                            // Everyone is within interact range.
                            new DecoratorContinue(context => CheckPartyRange(),
                                new Sequence(
                                    new Action(context => Logging.Write(Colors.DeepSkyBlue, "[LoadProfileOn]: Everyone is within range.")),
                                    new Action(context => CheckRange = 0)
                                )
                            )
                        )
                    ),

                    // Load the remote profile...
                    new Decorator(context => RemotePath != "",
                        new Sequence(
                            // You have included a RemotePath but not a ProfileName.
                            new DecoratorContinue(context => ProfileName == "",
                                new Sequence(
                                    new Action(context => Logging.Write(Colors.Red, "[LoadProfileOn]: You need to include a ProfileName.")),
                                    new Action(context => _isBehaviorDone = true)
                                )
                            ),
                            // Remote Profile doesn't exist.
                            new DecoratorContinue(context => (ProfileName != "" && !UrlExists(NewRemoteProfilePath)),
                                new Sequence(
                                    new Action(context => Logging.Write(Colors.Red, "[LoadProfileOn]: Profile '{0}' does not exist.", ProfileName)),
                                    new Action(context => _isBehaviorDone = true)
                                )
                            ),
                            // Everything is ok, Load the remote Profile
                            new DecoratorContinue(context => (ProfileName != "" && UrlExists(NewRemoteProfilePath)),
                                new Sequence(
                                    new Action(context => TreeRoot.StatusText = "Loading profile '" + ProfileName + "'"),
                                    new Action(context => Logging.Write(Colors.DeepSkyBlue, "[LoadProfileOn]: Loading profile '{0}'", ProfileName)),
                                    new Action(context => ProfileManager.LoadNew(new MemoryStream(new WebClient().DownloadData(NewRemoteProfilePath)))),
                                    new WaitContinue(TimeSpan.FromMilliseconds(300), context => false, new ActionAlwaysSucceed()),
                                    new Action(context => _isBehaviorDone = true)
                                )
                            )
                        )
                    ),

                    // Load the local profile...
                    new Decorator(context => (ProfileName != "" && RemotePath == ""),
                        new Sequence(
                            // Local Profile doesn't exist.
                            new DecoratorContinue(context => !File.Exists(NewLocalProfilePath),
                                new Sequence(
                                    new Action(context => Logging.Write(Colors.Red, "[LoadProfileOn]: Profile '{0}' does not exist.", ProfileName)),
                                    new Action(context => _isBehaviorDone = true)
                                )
                            ),
                            // Everything is ok, Load the local Profile.
                            new DecoratorContinue(context => File.Exists(NewLocalProfilePath),
                                new Sequence(
                                    new Action(context => TreeRoot.StatusText = "Loading profile '" + ProfileName + "'"),
                                    new Action(context => Logging.Write(Colors.DeepSkyBlue, "[LoadProfileOn]: Loading profile '{0}'", ProfileName)),
                                    new Action(context => ProfileManager.LoadNew(NewLocalProfilePath, false)),
                                    new WaitContinue(TimeSpan.FromMilliseconds(300), context => false, new ActionAlwaysSucceed()),
                                    new Action(context => _isBehaviorDone = true)
                                )
                            )
                        )
                    ),

                    // Everyone is within interact range and we shouldn't load a profile, then end the Quest Behavior.
                    new Decorator(context => !_isBehaviorDone,
                        new Action(context => _isBehaviorDone = true)
                    )
                )
            );
        }

        public override bool IsDone { get { return _isBehaviorDone; } }

        public override void OnStart() {
            // This reports problems, and stops BT processing if there was a problem with attributes...
            // We had to defer this action, as the 'profile line number' is not available during the element's
            // constructor call.
            OnStart_HandleAttributeProblem();

            if (!IsDone) {
                BotEvents.OnBotStop += BotEvents_OnBotStop;
            }
        }
        #endregion
    }
}
