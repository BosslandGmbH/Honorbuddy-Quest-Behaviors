using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Styx.Database;
using Styx.Helpers;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Styx.Logic.BehaviorTree;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    public class BasicUseItem : CustomForcedBehavior
    {
        /// <summary>
        /// BasicUseItem by Natfoth
        /// Allows you to use an item on the spot, even if the quest is completed.
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// ItemId: ID of the item you wish to use.
        /// WaitTime: How long to wait after you use the Item.
        /// </summary>
        /// 

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"ItemId",null},
            {"WaitTime",null},
            {"QuestId",null},

        };

        bool success = true;

        public BasicUseItem(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);

            int itemId = 0;
            int waittime = 0;
            int questId = 0;


            success = success && GetAttributeAsInteger("ItemId", true, "1", 0, int.MaxValue, out itemId);
            success = success && GetAttributeAsInteger("WaitTime", true, "1000", 0, int.MaxValue, out waittime);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);


            QuestId = (uint)questId;
            WaitTime = waittime;
            ItemID = itemId;
            Counter = 0;
            MovedToTarget = true;
        }

        public int Counter { get; set; }
        public int ItemID { get; set; }
        public int WaitTime { get; set; }
        public bool MovedToTarget;
        public uint QuestId { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        public WoWItem wowItem
        {
            get
            {
                List<WoWItem> inventory = ObjectManager.GetObjectsOfType<WoWItem>(false);

                foreach (WoWItem item in inventory)
                {
                    if ( item.Entry == ItemID)
                        return item;
                }

                return inventory[0];
            }
        }


        #region Overrides of CustomForcedBehavior.

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

            if (quest != null)
                TreeRoot.GoalText = "BasicUseItem Running - " + quest.Name;
        }

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                        new PrioritySelector(

                            new Decorator(ret => Counter > 0,
                                new Sequence(

                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new Action(ret => _isDone = true),
                                    new WaitContinue(1,
                                        new Action(delegate
                                        {
                                            _isDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )
                                ),

                             new Decorator(ret => ItemID > 0,
                                new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Using Item - " + wowItem.Name),
                                        new Action(ret => wowItem.Interact()),
                                        new Action(ret => Thread.Sleep(WaitTime)),
                                        new Action(ret => Counter++)
                                        )
                                    )
                    )));
        }

        private bool _isDone;
        public override bool IsDone
        {
            get { return _isDone; }
        }

        #endregion
    }
}
