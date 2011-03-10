using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Styx.Database;
using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Pathing;
using Styx.Logic.Profiles.Quest;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Styx.Logic.BehaviorTree;
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

            string fileName = "";
            int questId = 0;
            CurrentProfile = Logic.Profiles.ProfileManager.XmlLocation;
            Logging.Write(CurrentProfile);
            success = success && GetAttributeAsString("ProfileName", true, "1", out fileName);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);

            if (fileName == "1")
                success = success && GetAttributeAsString("Profile", true, "1", out fileName);

            Counter = 0;
            FileName = fileName;
        }

        public int Counter { get; set; }
        public String FileName { get; set; }
        public String CurrentProfile { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        public String fileLocation
        {
            get
            {
                int index = CurrentProfile.LastIndexOf("\\");
                if (index > 0)
                    CurrentProfile = CurrentProfile.Substring(0, index + 1);

                return CurrentProfile += FileName;
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
                                        new Action(ret => TreeRoot.StatusText = "LoadingProfile - " + fileLocation),
                                        new Action(ret => Styx.Logic.Profiles.ProfileManager.LoadNew(fileLocation)),
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
