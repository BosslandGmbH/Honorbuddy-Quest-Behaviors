// Behavior originally contributed by Nesox.
//
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_InteractWith
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Styx.Logic.BehaviorTree;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Inventory.Frames.LootFrame;
using Styx.Logic.Inventory.Frames.Merchant;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// Allows you to do quests that requires you to interact with nearby objects.
    /// ##Syntax##
    /// [Optional]QuestId: Id of the quest.
    /// NpcId: Id of the object to interact with.
    /// NumOfTimes: Number of times to interact with object.
    /// [Optional]GossipOption: The Dialog numbers you wish to choose. Should be seperated with commas. ie. GossipOption="1,1,4,2" or GossipOption="1"
    /// [Optional]CollectionDistance: The distance it will use to collect objects. DefaultValue:100 yards
    /// [Optional]BuySlot: Buys the item from the slot. Slots are: 1 2
    ///                                                            3 4
    ///                                                            5 6
    ///                                                            7 8
    ///                                                            page2
    ///                                                            9 10 etc.
    /// [Optional]BuyItemId: Buys the item with that id from vendor.
    /// [Optional]BuyItemCount: The amount to buy the item. Default: 1
    /// [Optional]WaitTime: The time to wait once it has interacted with an object. DefaultValue:3000
    /// ObjectType: the type of object to interact with, expected value: Npc/Gameobject
    /// X,Y,Z: The general location where theese objects can be found
    /// </summary>
    public class InteractWith : CustomForcedBehavior
    {
        public enum ObjectType
        {
            Npc,
            GameObject,
        }
        
        public InteractWith(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                WaitForNpcs = GetAttributeAsBoolean("WaitForNpcs", false, null) ?? true;
                BuyItemCount = GetAttributeAsInteger("BuyItemCount", false, 1, 1000, null) ?? 1;
                BuyItemId   = GetAttributeAsItemId("BuyItemId", false, null) ?? 0;
                BuySlot     = GetAttributeAsInteger("BuySlot", false, -1, 100, null) ?? -1;
                CollectionDistance = GetAttributeAsInteger("CollectionDistance", false, 1, 10000, null) ?? 100;
                GossipOptions = GetAttributeAsIntegerArray("GossipOptions", false, -1, 10, new [] { "GossipOption" }) ?? new int[0];
                Location    = GetXYZAttributeAsWoWPoint("", true, null) ?? WoWPoint.Empty;
                Loot        = GetAttributeAsBoolean("Loot", false, null) ?? false;
                MobId       = GetAttributeAsMobId("MobId", true, new [] { "NpcId" }) ?? 0;
                ObjType     = GetAttributeAsEnum<ObjectType>("ObjectType", false, new [] { "MobType" }) ?? ObjectType.Npc;
                NotMoving   = GetAttributeAsBoolean("NotMoving", false, null) ?? false;
                NumOfTimes  = GetAttributeAsNumOfTimes("NumOfTimes", false, null) ?? 1;
                QuestId     = GetAttributeAsQuestId("QuestId", false, null) ?? 0;
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;
                Range       = GetAttributeAsRange("Range", false, null) ?? 4;
                WaitTime    = GetAttributeAsWaitTime("WaitTime", false, null) ?? 3000;

                for (int i = 0;  i < GossipOptions.Length;  ++i)
                    { GossipOptions[i] -= 1; }


                WoWUnit     mob     = ObjectManager.GetObjectsOfType<WoWUnit>()
                                      .Where(unit => unit.Entry == MobId)
                                      .FirstOrDefault();

                MobName = ((mob != null) && !string.IsNullOrEmpty(mob.Name))
                            ? mob.Name
                            : ("Mob(" + MobId + ")");
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
			}
        }

        // Attributes provided by caller
        public int                      BuyItemCount { get; private set; }
        public int                      BuyItemId { get; private set; }
        public int                      BuySlot { get; private set; }
        public int                      CollectionDistance { get; private set; }
        public int[]                    GossipOptions { get; private set; }
        public WoWPoint                 Location { get; private set; }
        public bool                     Loot { get; private set; }
        public int                      MobId { get; private set; }
        public string                   MobName { get; private set; }
        public ObjectType               ObjType { get; private set; }
        public bool                     NotMoving { get; private set; }
        public int                      NumOfTimes { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }
        public int                      Range { get; private set; }
        public int                      WaitTime { get; private set; }
        public bool                     WaitForNpcs { get; private set; }

        // Private variables for internal state
        private bool                    _isBehaviorDone;
        private readonly List<ulong>    _npcBlacklist = new List<ulong>();
        private Composite               _root;

        // Private properties
        public int                      Counter { get; private set; }
        private LocalPlayer             Me { get { return (ObjectManager.Me); } }


        /// <summary> Current object we should interact with.</summary>
        /// <value> The object.</value>
        private WoWObject CurrentObject
        {
            get
            {
                bool test = ObjectManager.GetObjectsOfType<WoWGameObject>().Any(delegate(WoWGameObject obj) { return obj.Entry == 191092; });
                WoWObject @object = null;
                switch (ObjType)
                {
                    case ObjectType.GameObject:
                        @object = ObjectManager.GetObjectsOfType<WoWGameObject>().OrderBy(ret => ret.Distance).FirstOrDefault(obj =>
                            !_npcBlacklist.Contains(obj.Guid) &&
                            obj.Distance < CollectionDistance &&
                            obj.Entry == MobId);

                        break;

                    case ObjectType.Npc:
                        @object = ObjectManager.GetObjectsOfType<WoWUnit>().OrderBy(ret => ret.Distance).FirstOrDefault(obj =>
                            !_npcBlacklist.Contains(obj.Guid) &&
                            obj.Distance < CollectionDistance &&
                            (NotMoving ? !obj.IsMoving : true) &&
                            obj.Entry == MobId);

                        break;

                }

                if (@object != null)
                    { UtilLogMessage("debug", @object.Name); }

                return @object;
            }
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                    new Decorator(ret => Counter >= NumOfTimes,
                        new Action(ret => _isBehaviorDone = true)),

                        new PrioritySelector(

                            new Decorator(ret => CurrentObject != null && CurrentObject.Location.DistanceSqr(Me.Location) > Range * Range,
                                new Sequence(
                                    new Action(ret => { TreeRoot.StatusText = "Moving to interact with - " + CurrentObject.Name; }),
                                    new Action(ret => Navigator.MoveTo(CurrentObject.Location))
                                    )
                                ),

                            new Decorator(ret => CurrentObject != null && CurrentObject.Location.DistanceSqr(Me.Location) <= Range * Range,
                                new Sequence(
                                    new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                        new Action(ret =>
                                            {
                                                WoWMovement.MoveStop();
                                                StyxWoW.SleepForLagDuration();
                                            })),

                                    new Action(ret =>
                                    {
                                        TreeRoot.StatusText = "Interacting with - " + CurrentObject.Name;
                                        CurrentObject.Interact();
                                        _npcBlacklist.Add(CurrentObject.Guid);

                                        Thread.Sleep(2000);
                                        Counter++;
                                    }),

                                    new DecoratorContinue(
                                        ret => GossipOptions.Length > 0,
                                        new Action(ret =>
                                            {
                                                foreach (var gos in GossipOptions)
                                                {
                                                    GossipFrame.Instance.SelectGossipOption(gos);
                                                    Thread.Sleep(1000);
                                                }
                                            })),

                                    new DecoratorContinue(
                                        ret => Loot && LootFrame.Instance.IsVisible,
                                        new Action(ret => LootFrame.Instance.LootAll())),
                                    
                                    new DecoratorContinue(
                                        ret => BuyItemId != 0 && MerchantFrame.Instance.IsVisible,
                                        new Action(ret =>
                                            {
                                                var items = MerchantFrame.Instance.GetAllMerchantItems();
                                                var item = items.FirstOrDefault(i => i.ItemId == BuyItemId && (i.BuyPrice * (ulong)BuyItemCount) <= Me.Copper && (i.NumAvailable >= BuyItemCount || i.NumAvailable == -1));

                                                if (item != null)
                                                {
                                                    MerchantFrame.Instance.BuyItem(item.Index, BuyItemCount);
                                                    Thread.Sleep(1500);
                                                }
                                            })),

                                    new DecoratorContinue(
                                        ret => BuySlot != -1 && BuyItemId == 0 && MerchantFrame.Instance.IsVisible,
                                        new Action(ret =>
                                        {
                                            var item = MerchantFrame.Instance.GetMerchantItemByIndex(BuySlot);
                                            if (item != null && (item.BuyPrice * (ulong)BuyItemCount) <= Me.Copper && (item.NumAvailable >= BuyItemCount || item.NumAvailable == -1))
                                            {
                                                MerchantFrame.Instance.BuyItem(BuySlot, BuyItemCount);
                                                Thread.Sleep(1500);
                                            }
                                        })),
                                    new DecoratorContinue(
                                        ret => Me.CurrentTarget != null && Me.CurrentTarget == CurrentObject,
                                        new Action(ret => Me.ClearTarget())),

                                    new Action(ret => Thread.Sleep(WaitTime))

                                )),

                            new Decorator(
                                ret => Location.DistanceSqr(Me.Location) > 2 * 2,
                                new Sequence(
                                    new Action(ret => { TreeRoot.StatusText = "Moving towards - " + Location; }),
                                    new Action(ret => Navigator.MoveTo(Location)))),

                            new Decorator(
                                ret => !WaitForNpcs && CurrentObject == null,
                                new Action(ret => _isBehaviorDone = true)),

                            new Action(ret => TreeRoot.StatusText = "Waiting for object to spawn")

                    )));
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
                TreeRoot.GoalText = "Interacting with " + MobName;
            }
        }

        #endregion
    }
}
