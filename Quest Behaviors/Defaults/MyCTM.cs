using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Styx.Database;
using Styx.Logic.Combat;
using Styx.Helpers;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Pathing;
using Styx.Logic.Profiles.Quest;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Styx.Logic;
using Styx.Logic.BehaviorTree;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    public class MyCTM : CustomForcedBehavior
    {

        /// <summary>
        /// MyCTM by Natfoth
        /// Allows you to physically click on the screen so that your bot can get around non meshed locations or off objects. *** There is no navigation with this ****
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// X,Y,Z: Where you wish to move.
        /// </summary>
        /// 
        

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"X",null},
            {"Y",null},
            {"Z",null},
            {"QuestId",null},

        };

        bool success = true;

        public MyCTM(Dictionary<string, string> args)
            : base(args)
        {

            CheckForUnrecognizedAttributes(recognizedAttributes);

            WoWPoint location = new WoWPoint(0, 0, 0);
            int questId = 0;

            success = success && GetXYZAttributeAsWoWPoint("X", "Y", "Z", true, new WoWPoint(0, 0, 0), out location);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);

            Location = location;
            QuestId = (uint)questId;

            Counter = 0;
        }

        public WoWPoint Location { get; private set; }
        public int Counter { get; set; }
        public uint QuestId { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        #region Overrides of CustomForcedBehavior

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

            if (quest != null)
            {
                TreeRoot.GoalText = "MyCTM - " + quest.Name;
            }
            else
            {
                TreeRoot.GoalText = "MyCTM: Running";
            }
        }

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                 new PrioritySelector(

                            new Decorator(ret => Location.Distance(me.Location) <= 3,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new WaitContinue(120,
                                        new Action(delegate
                                        {
                                            _isDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )),

                            new Decorator(ret =>Location.Distance(me.Location) > 3,
                                new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Moving To Location - X: " + Location.X + " Y: " + Location.Y),
                                        new Action(ret => WoWMovement.ClickToMove(Location)),
                                        new Action(ret => Thread.Sleep(50))
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

