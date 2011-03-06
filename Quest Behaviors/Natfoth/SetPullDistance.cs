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
    public class SetPullDistance : CustomForcedBehavior
    {

        /// <summary>
        /// SetPullDistance by Natfoth
        /// Allows you change the Bot Pull Distance so you can turn in a quest safely or leave an area safely
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// Distance: Generally 5 or 20-30
        /// </summary>
        /// 

        

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"Distance",null},
            {"QuestId",null},

        };

        bool success = true;

        public SetPullDistance(Dictionary<string, string> args)
            : base(args)
        {

            CheckForUnrecognizedAttributes(recognizedAttributes);

            int distance = 0;
            int questId = 0;

            success = success && GetAttributeAsInteger("Distance", true, "1", 0, int.MaxValue, out distance);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);

            QuestId = (uint)questId;
            Counter = 0;
            Distance = distance;
        }

        public int Counter { get; set; }
        public int Distance { get; set; }
        public uint QuestId { get; set; }
        public static LocalPlayer me = ObjectManager.Me;


        #region Overrides of CustomForcedBehavior

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

            if (quest != null)
            {
                TreeRoot.GoalText = "SetPullDistance - " + quest.Name;
            }
            else
            {
                TreeRoot.GoalText = "SetPullDistance: Running";
            }
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
                                        new Action(ret => TreeRoot.StatusText = "Setting Distance: " + Distance),
                                        new Action(ret => LevelbotSettings.Instance.PullDistance = Distance),
                                        new Action(ret => Counter++)
                                ))
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
