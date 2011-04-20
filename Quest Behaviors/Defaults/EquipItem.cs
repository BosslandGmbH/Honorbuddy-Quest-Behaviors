// Behavior originally contributed by HighVoltz.
//
// DOCUMENTATION:
//     
//
using System;
using System.Collections.Generic;
using System.Linq;

using Styx.Logic.BehaviorTree;
using Styx.Logic.Inventory;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
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
        public EquipItem(Dictionary<string, string> args)
            : base(args)
        {
			try
			{
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                ItemId  = GetAttributeAsItemId("ItemId", true, null) ?? 0;
                QuestId = GetAttributeAsQuestId("QuestId", false, null) ?? 0;
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;
                Slot    = GetAttributeAsInteger("Slot", false, -1, 100, null) ?? -1;
			}

			catch (Exception except)
			{
				// Maintenance problems occur for a number of reasons.  The primary two are...
				// * Changes were made to the behavior, and boundary conditions weren't properly tested.
				// * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
				// In any case, we pinpoint the source of the problem area here, and hopefully it
				// can be quickly resolved.
				UtilLogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
										+ "\nFROM HERE:\n"
										+ except.StackTrace + "\n");
				IsAttributeProblem = true;
			}        }


        // Attributes provided by caller
        public int                      ItemId { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }
        public int                      Slot { get; private set; }

        // Private variables for internal state
        private bool        _isBehaviorDone;
        private Composite   _root;


        #region Overrides of CustomForcedBehavior

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
                        _isBehaviorDone = true;
                    })
                ));
        }


        public override bool IsDone 
        {
            get
            {
                return (_isBehaviorDone     // normal completion
                        || !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
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
                WoWItem item = StyxWoW.Me.CarriedItems.FirstOrDefault(ret => ret.Entry == ItemId);

                if (item != null)
                    { TreeRoot.GoalText = string.Format("Equipping [{0}] Into Slot: {1}", item.Name, (InventorySlot)Slot); }
            }
        }

        #endregion
    }
}
