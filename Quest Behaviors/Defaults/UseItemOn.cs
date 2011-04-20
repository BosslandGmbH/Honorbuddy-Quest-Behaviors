// Behavior originally contributed by Nesox.
//
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_UseItemOn
//
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;

using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// Allows you to use items on nearby gameobjects/npc's
    /// ##Syntax##
    /// QuestId: The id of the quest.
    /// MobId: The id of the object.
    /// ItemId: The id of the item to use.
    /// NumOfTimes: Number of times to use said item.
    /// [Optional]WaitTime: Time to wait after using an item. DefaultValue: 1500 ms
    /// [Optional]CollectionDistance: The distance it will use to collect objects. DefaultValue:100 yards
    /// [Optional]HasAura: If a unit has a certian aura to check before using item. (By: j0achim)
    /// [Optional]Range: The range to object that it will use the item
    /// [Optional]NpcState: The state of the npc -> Dead, Alive, BelowHp. None is default
    /// [Optional]HpLeftAmount: Will only be used when NpcState is BelowHp
    /// ObjectType: the type of object to interact with, expected value: Npc/Gameobject
    /// X,Y,Z: The general location where theese objects can be found
    /// </summary>
    public class UseItemOn : CustomForcedBehavior
    {
        public enum ObjectType
        {
            Npc,
            GameObject,
        }

        public enum NpcState
        {
            Alive,
            BelowHp,
            Dead,
            DontCare,
        }


        public UseItemOn(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                CollectionDistance = GetAttributeAsInteger("CollectionDistance", false, 1, 10000, null) ?? 100;
                HasAuraId   = GetAttributeAsSpellId("HasAuraId", false, new [] { "HasAura" }) ?? 0;
                HpLeftAmount = GetAttributeAsInteger("HpLeftAmount", false, 0, int.MaxValue, null) ?? 100;
                ItemId      = GetAttributeAsItemId("ItemId", true, null) ?? 0;
                Location    = GetXYZAttributeAsWoWPoint("", true, null) ?? WoWPoint.Empty;
                MobId       = GetAttributeAsMobId("MobId", true, new [] { "NpcId" }) ?? 0;
                MobType     = GetAttributeAsEnum<ObjectType>("MobType", false, new [] { "ObjectType" }) ?? ObjectType.Npc;
                NumOfTimes  = GetAttributeAsNumOfTimes("NumOfTimes", false, null) ?? 1;
                _NpcState   = GetAttributeAsEnum<NpcState>("NpcState", false, null) ?? NpcState.DontCare;
                Range       = GetAttributeAsRange("Range", false, null) ?? 4;
                QuestId     = GetAttributeAsQuestId("QuestId", false, null) ?? 0;
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;
                WaitTime    = GetAttributeAsWaitTime("WaitTime", false, null) ?? 1500;
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
        public int                      CollectionDistance { get; private set; }
        public int                      HasAuraId { get; private set; }
        public int                      HpLeftAmount { get; private set; }
        public int                      ItemId { get; private set; }
        public WoWPoint                 Location { get; private set; }
        public int                      MobId { get; private set; }
        public ObjectType               MobType { get; private set; }
        public NpcState                 _NpcState { get; private set; }
        public int                      NumOfTimes { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }
        public int                      Range { get; private set; }
        public int                      WaitTime { get; private set; }

        // Private variables for internal state
        private bool                    _isBehaviorDone;
        private readonly List<ulong>    _npcAuraWait = new List<ulong>();
        private readonly List<ulong>    _npcBlacklist = new List<ulong>();
        private Composite               _root;

        // Private properties
        private int                     Counter { get; set; }


        /// <summary> Current object we should interact with.</summary>
        /// <value> The object.</value>
        private WoWObject CurrentObject
        {
            get
            {
                WoWObject @object = null;
                switch (MobType)
                {
                    case ObjectType.GameObject:
                        @object = ObjectManager.GetObjectsOfType<WoWGameObject>().OrderBy(ret => ret.Distance).FirstOrDefault(obj =>
                            !_npcBlacklist.Contains(obj.Guid) &&
                            obj.Distance < CollectionDistance &&
                            obj.Entry == MobId);

                        break;

                    case ObjectType.Npc:
                        if (HasAuraId != 0)
                        {
                            string tmp;
                            try 
                            { 
                                tmp = WoWSpell.FromId(HasAuraId).Name; 
                            }
                            catch
                            {
                                UtilLogMessage("fatal", "Could not find HasAuraId({0}) for UseItemOn behavior!", HasAuraId);
                                break;
                            }

                            @object = ObjectManager.GetObjectsOfType<WoWUnit>().OrderBy(ret => ret.Distance).FirstOrDefault(obj =>
                                !_npcBlacklist.Contains(obj.Guid) &&
                                obj.Distance < CollectionDistance &&
                                obj.HasAura(tmp) &&
                                obj.Entry == MobId);
                        }
                        else
                        {
                            switch (_NpcState)
                            {
                                case NpcState.DontCare:
                                    @object = ObjectManager.GetObjectsOfType<WoWUnit>().OrderBy(ret => ret.Distance).FirstOrDefault(obj =>
                                        !_npcBlacklist.Contains(obj.Guid) &&
                                        obj.Distance < CollectionDistance &&
                                        obj.Entry == MobId);
                                    break;
                                case NpcState.Dead:
                                    @object = ObjectManager.GetObjectsOfType<WoWUnit>().OrderBy(ret => ret.Distance).FirstOrDefault(obj =>
                                        !_npcBlacklist.Contains(obj.Guid) &&
                                        obj.Dead &&
                                        obj.Distance < CollectionDistance &&
                                        obj.Entry == MobId);
                                    break;
                                case NpcState.Alive:
                                    @object = ObjectManager.GetObjectsOfType<WoWUnit>().OrderBy(ret => ret.Distance).FirstOrDefault(obj =>
                                        !_npcBlacklist.Contains(obj.Guid) &&
                                        obj.IsAlive &&
                                        obj.Distance < CollectionDistance &&
                                        obj.Entry == MobId);
                                    break;
                                case NpcState.BelowHp:
                                    @object = ObjectManager.GetObjectsOfType<WoWUnit>().OrderBy(ret => ret.Distance).FirstOrDefault(obj =>
                                        !_npcBlacklist.Contains(obj.Guid) &&
                                        obj.HealthPercent <= HpLeftAmount &&
                                        obj.IsAlive &&
                                        obj.Distance < CollectionDistance &&
                                        obj.Entry == MobId);
                                    break;
                            }
                            
                        }

                        break;
                }

                if (@object != null)
                    { UtilLogMessage("debug", @object.Name); }

                return @object;
            }
        }

        public WoWItem Item
        {
            get
            {
                return StyxWoW.Me.CarriedItems.FirstOrDefault(ret => ret.Entry == ItemId);
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
                        new Decorator(ret => CurrentObject != null && CurrentObject.DistanceSqr > Range * Range,
                            new Sequence(
                                new Action(delegate { TreeRoot.StatusText = "Moving to use item on \"" + CurrentObject.Name + "\""; }),
                                new Action(ret => Navigator.MoveTo(CurrentObject.Location))
                                )
                            ),

                        new Decorator(ret => CurrentObject != null && CurrentObject.DistanceSqr <= Range * Range && Item != null && Item.Cooldown == 0,
                            new Sequence(
                                new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                    new Action(ret =>
                                    {
                                        WoWMovement.MoveStop();
                                        StyxWoW.SleepForLagDuration();
                                    })),

                                new Action(ret =>
                                {
                                    bool targeted = false;
                                    TreeRoot.StatusText = "Using item on \"" + CurrentObject.Name + "\"";
                                    if (CurrentObject is WoWUnit && (StyxWoW.Me.CurrentTarget == null || StyxWoW.Me.CurrentTarget != CurrentObject))
                                    {
                                        (CurrentObject as WoWUnit).Target();
                                        targeted = true;
                                        StyxWoW.SleepForLagDuration();
                                    }
                                        
                                    WoWMovement.Face(CurrentObject.Guid);

                                    Item.UseContainerItem();
                                    _npcBlacklist.Add(CurrentObject.Guid);

                                    StyxWoW.SleepForLagDuration();
                                    Counter++;
                                    Thread.Sleep(WaitTime);

                                    if (targeted)
                                        StyxWoW.Me.ClearTarget();
                                }))
                                    ),

                        new Sequence(
                            new Action(delegate { TreeRoot.StatusText = "Moving to location " + Location; }),
                            new Action(ret => Navigator.MoveTo(Location))))
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
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = this.GetType().Name + ": " + ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
            }
        }

        #endregion
    }
}