using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
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
    public class CombatUseItemOn : CustomForcedBehavior
    {

        /// <summary>
        /// CombatUseItemOn by Natfoth
        /// Allows you to use an Item after you gain an Aura.
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// ItemId: Id of the Item you wish to use once you have an aura.
        /// HasAura: Aura ID of the aura when to use the item
        /// NpcId: NpcID of the mob that will attack you.
        /// NumOfTimes: How times needed to use the item.
        /// X,Y,Z: The general location where these objects can be found
        /// </summary>
        /// 

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"ItemId",null},
            {"NpcId",null},
            {"NumOfTimes",null},
            {"X",null},
            {"Y",null},
            {"Z",null},
            {"HasAura",null},
            {"QuestId",null},

        };

        bool success = true;


        public CombatUseItemOn(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);

            int itemId = 0;
            int mobid = 0;
            int hasaura = 0;
            int numberoftimes = 0;
            int questId = 0;
            WoWPoint location = new WoWPoint(0, 0, 0);

            success = success && GetAttributeAsInteger("ItemId", true, "1", 0, int.MaxValue, out itemId);
            success = success && GetAttributeAsInteger("NpcId", true, "1", 0, int.MaxValue, out mobid);
            success = success && GetAttributeAsInteger("HasAura", true, "1", 0, int.MaxValue, out hasaura);
            success = success && GetAttributeAsInteger("NumOfTimes", false, "1", 0, int.MaxValue, out numberoftimes);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
            success = success && GetXYZAttributeAsWoWPoint("X", "Y", "Z", true, new WoWPoint(0, 0, 0), out location);

            QuestId = (uint)questId;
            ItemID = itemId;
            MobId = mobid;
            HasAura = hasaura;
            Counter = 0;
            MovedToTarget = false;
            NumberOfTimes = numberoftimes;
            Location = location;
        }

        public WoWPoint Location { get; private set; }
        public int Counter { get; set; }
        public int ItemID { get; set; }
        public int MobId { get; set; }
        public int HasAura { get; set; }
        public bool MovedToTarget;
        public int NumberOfTimes { get; set; }
        public uint QuestId { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        public List<WoWUnit> mobList
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>()
                                       .Where(u => u.Entry == MobId && !u.Dead)
                                       .OrderBy(u => u.Distance).ToList();
            }
        }

        public String auraName
        {
            get
            {
                return Styx.Logic.Combat.WoWSpell.FromId(HasAura).Name;
            }
        }

        public WoWItem wowItem
        {
            get
            {
                List<WoWItem> inventory = ObjectManager.GetObjectsOfType<WoWItem>(false);

                foreach (WoWItem item in inventory)
                {
                    if (item.Entry == ItemID)
                        return item;
                }

                return inventory[0];
            }
        }


        #region Overrides of CustomForcedBehavior

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

            if (quest != null)
            {
                TreeRoot.GoalText = "CombatUseItem - " + quest.Name;
            }
            else
            {
                TreeRoot.GoalText = "CombatUseItem";
            }
        }

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                            new Decorator(ret => (Counter > 0) || (me.QuestLog.GetQuestById(QuestId) != null && me.QuestLog.GetQuestById(QuestId).IsCompleted),
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new WaitContinue(120,
                                        new Action(delegate
                                        {
                                            _isDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )),

                        new Decorator(c => Counter == 0,
                            new Action(c =>
                            {
                                if (Location.Distance(me.Location) > 3)
                                {
                                    TreeRoot.StatusText = "Moving To Location - X: " + Location.X + " Y: " + Location.Y;
                                    Navigator.MoveTo(Location);
                                }
                                else if (me.HasAura(auraName))
                                {
                                    TreeRoot.StatusText = "Has Aura - " + auraName + " Using Item: " + wowItem.Name;
                                    wowItem.Interact();
                                    Counter++;
                                    return RunStatus.Success;
                                }
                                return RunStatus.Running;

                            }))

                        
                    ));
        }

        private bool _isDone;
        public override bool IsDone
        {
            get
            {
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
                return _isDone && quest != null && quest.IsCompleted || quest == null;
            }
        }

        #endregion
    }
}
