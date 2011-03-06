using System.Collections.Generic;
using System.Linq;
using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Styx.Logic.Inventory;

using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// EquipItem by HighVoltz
    /// Equips an item.. optionally to a specified slot
    /// ##Syntax##
    /// ItemId: ID of item to equip
    /// QuestId: (optional) QuestID that this behavior belongs to;
    /// Slot: (optional) Slot that the item will go to. Default(equipts item in 1st free slot that it can fit into);
    /// None = -1,
    /// AmmoSlot = 0,
    /// HeadSlot = 1,
    /// NeckSlot = 2,
    /// ShoulderSlot = 3,
    /// ShirtSlot = 4,
    /// ChestSlot = 5,
    /// WaistSlot = 6,
    /// LegsSlot = 7,
    /// FeetSlot = 8,
    /// WristSlot = 9,
    /// HandsSlot = 10,
    /// Finger0Slot = 11,
    /// Finger1Slot = 12,
    /// Trinket0Slot = 13,
    /// Trinket1Slot = 14,
    /// BackSlot = 15,
    /// MainHandSlot = 16,
    /// SecondaryHandSlot = 17,
    /// RangedSlot = 18,
    /// TabardSlot = 19,
    /// Bag0Slot = 20,
    /// Bag1Slot = 21,
    /// Bag2Slot = 22,
    /// Bag3Slot = 23,
    /// </summary>
    /// 
    public class EquipItem : CustomForcedBehavior
    {
        readonly Dictionary<string, object> _recognizedAttributes = new Dictionary<string, object>
        {
            {"ItemId",null},
            {"QuestId",null},
            {"Slot",null},
        };

        bool success = true;
        public EquipItem(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(_recognizedAttributes);
            int itemId = 0;
            int questId = 0;
            int slot = 0;
            success = success && GetAttributeAsInteger("ItemId", true, "0", 0, int.MaxValue, out itemId);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
            success = success && GetAttributeAsInteger("Slot", false, "-1", -1, int.MaxValue, out slot);
            if (!success)
            {
                Err("Invalid or missing Attributes, Stopping HB");
            }
            ItemId = itemId;
            Slot = slot;
        }

        public int ItemId { get; private set; }
        public int QuestId { get; private set; }
        public int Slot { get; private set; }

        #region Overrides of CustomForcedBehavior

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ??
                (_root = new PrioritySelector(
                    new Action(c =>
                    {
                        if (Slot == (int)InventorySlot.None)
                        {
                            Lua.DoString("EquipItemByName (\"" + ItemId + "\")");
                        }
                        else
                        {
                            WoWItem item = ObjectManager.Me.BagItems.FirstOrDefault(i => i.Entry == ItemId);
                            if (item != null)
                            {
                                Lua.DoString("PickupContainerItem({0},{1}) EquipCursorItem({2})",
                                    item.BagIndex + 1, item.BagSlot + 1, Slot);
                            }
                        }
                        _isDone = true;
                    })
                ));
        }

        private static void Err(string format, params object[] args)
        {
            Logging.Write(System.Drawing.Color.Red, "EquipItem: " + format, args);
            TreeRoot.Stop();
        }

        private bool _isDone;
        public override bool IsDone 
        {
            get 
            {
                var quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
                return _isDone || (QuestId > 0 && ((quest != null && quest.IsCompleted) || quest == null));
            } 
        }
 
        public override void OnStart()
        {
            WoWItem item = StyxWoW.Me.CarriedItems.FirstOrDefault(ret => ret.Entry == ItemId);
            if(item != null)
                TreeRoot.GoalText = string.Format("Equiping [{0}] Into Slot: {1}", item.Name, (InventorySlot)Slot);    
        }

        #endregion
    }
}
