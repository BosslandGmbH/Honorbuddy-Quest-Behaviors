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
    public class ForceLoadProfile : CustomForcedBehavior
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
            {"QuestId",null},

        };

        bool success = true;

        public ForceLoadProfile(Dictionary<string, string> args)
            : base(args)
        {

            CheckForUnrecognizedAttributes(recognizedAttributes);

            string fileName = "";
            int questId = 0;
            CurrentProfile = Logic.Profiles.ProfileManager.XmlLocation;

            success = success && GetAttributeAsString("ProfileName", true, "1", out fileName);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);

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

                CurrentProfile += FileName;

                return CurrentProfile;
            }
        }

        #region Overrides of CustomForcedBehavior

        public override void OnStart()
        {
            TreeRoot.GoalText = "ForceLoadProfile: Running";
        }

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                            new Decorator(ret => Counter > 0,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new WaitContinue(120,
                                        new Action(delegate
                                        {
                                            _isDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )),

                           new Decorator(ret => Counter == 0,
                                new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "ForceLoadingProfile - " + fileLocation),
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
