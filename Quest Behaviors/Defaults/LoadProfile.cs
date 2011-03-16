using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Styx.Helpers;
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
        /// QuestId: Id of the quest.
        /// ProfileName: The exact name of the profile including the extension.
        /// </summary>
        /// 

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"ProfileName",null},
            {"Profile",null},
            {"QuestId",null},

        };

        bool success = true;

        public LoadProfile(Dictionary<string, string> args)
            : base(args)
        {

            CheckForUnrecognizedAttributes(recognizedAttributes);

            string profileName = "";
            int questId = 0;
            Logging.Write(CurrentProfile);
            success = success && GetAttributeAsString("ProfileName", false, "1", out profileName);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);

            if (profileName == "1")
                success = success && GetAttributeAsString("Profile", false, "1", out profileName);

            Counter = 0;
            ProfileName = profileName + ".xml";

        }

        public int Counter { get; set; }
        public String ProfileName { get; set; }
        public String CurrentProfile { get { return ProfileManager.XmlLocation; } }
        public static LocalPlayer Me { get { return ObjectManager.Me; } }

        public String NewProfilePath
        {
            get
            {
                string directory = Path.GetDirectoryName(CurrentProfile);
                return Path.Combine(directory, ProfileName);
            }
        }


        #region Overrides of CustomForcedBehavior

        public override void OnStart()
        {
            TreeRoot.GoalText = "LoadProfile: Running";
        }

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                            new Decorator(ret => Counter > 0,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new Action(ret => _isDone = true))),

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

        private bool _isDone;
        public override bool IsDone
        {
            get { return _isDone; }
        }

        #endregion
    }
}
