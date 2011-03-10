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
    /// UseItemOn by Nesox
    /// Allows you to use items on nearby gameobjects/npc's
    /// ##Syntax##
    /// QuestId: The id of the quest.
    /// MobId: The id of the object.
    /// ItemId: The id of the item to use.
    /// NumOfTimes: Number of times to use said item.
    /// [Optional]WaitTime: Time to wait after using an item. DefaultValue: 1500 ms
    /// [Optional]CollectionDistance: The distance it will use to collect objects. DefaultValue:100 yards
    /// [Optional]HasAura: If a unit has a certian aura to check before using item. (By: j0achim)
    /// ObjectType: the type of object to interact with, expected value: Npc/Gameobject
    /// X,Y,Z: The general location where theese objects can be found
    /// </summary>
    public class UseItemOn : CustomForcedBehavior
    {
        public UseItemOn(Dictionary<string, string> args)
            : base(args)
        {
            bool error = false;

            uint questId;
            if (!uint.TryParse(Args["QuestId"], out questId))
            {
                Logging.Write("Parsing attribute 'QuestId' in UseItemOn behavior failed! please check your profile!");
                error = true;
            }

            uint mobId;
            if (!uint.TryParse(Args["MobId"], out mobId))
            {
                Logging.Write("Parsing attribute 'MobId' in UseItemOn behavior failed! please check your profile!");
                error = true;
            }

            uint itemId;
            if(!uint.TryParse(Args["ItemId"], out itemId))
            {
                Logging.Write("Parsing attribute 'ItemId' in UseItemOn behavior failed! please check your profile!");
                error = true;
            }

            int numOfTimes;
            if (!int.TryParse(Args["NumOfTimes"], out numOfTimes))
            {
                Logging.Write("Parsing attribute 'NumOfTimes' in UseItemOn behavior failed! please check your profile!");
                error = true;
            }

            if(Args.ContainsKey("WaitTime"))
            {
                int waitTime;
                int.TryParse(Args["WaitTime"], out waitTime);
                WaitTime = waitTime != 0 ? waitTime : 1500;
            }

            if (Args.ContainsKey("CollectionDistance"))
            {
                int distance;
                int.TryParse(Args["CollectionDistance"], out distance);
                CollectionDistance = distance != 0 ? distance : 100;
            }

            if (Args.ContainsKey("HasAura"))
            {
                int HasAura;
                int.TryParse(Args["HasAura"], out HasAura);
                Aura = HasAura != 0 ? HasAura : 0;
            }

            if (!Args.ContainsKey("ObjectType"))
            {
                Logging.Write("Could not find attribute 'ObjectType' in UseItemOn behavior! please check your profile!");
                error = true;
            }

            var type = (ObjectType)Enum.Parse(typeof(ObjectType), Args["ObjectType"], true);

            float x, y, z;
            if (!float.TryParse(Args["X"], out x))
            {
                Logging.Write("Parsing attribute 'X' in UseItemOn behavior failed! please check your profile!");
                error = true;
            }

            if (!float.TryParse(Args["Y"], out y))
            {
                Logging.Write("Parsing attribute 'Y' in UseItemOn behavior failed! please check your profile!");
                error = true;
            }

            if (!float.TryParse(Args["Z"], out z))
            {
                Logging.Write("Parsing attribute 'Z' in UseItemOn behavior failed! please check your profile!");
                error = true;
            }

            if (error)
                TreeRoot.Stop();

            ObjectType = type;
            QuestId = questId;
            NumOfTimes = numOfTimes;
            MobId = mobId;
            ItemId = itemId;
            Location = new WoWPoint(x, y, z);
        }

        public WoWPoint Location { get; private set; }
        public int WaitTime { get; private set; }
        public int Aura { get; private set; }
        public int Counter { get; private set; }
        public uint MobId { get; private set; }
        public uint ItemId { get; private set; }
        public int NumOfTimes { get; private set; }
        public uint QuestId { get; private set; }
        public ObjectType ObjectType { get; private set; }
        public int CollectionDistance = 100;

        private readonly List<ulong> _npcBlacklist = new List<ulong>();
        private readonly List<ulong> _npcAuraWait = new List<ulong>();

        /// <summary> Current object we should interact with.</summary>
        /// <value> The object.</value>
        private WoWObject CurrentObject
        {
            get
            {
                WoWObject @object = null;
                switch (ObjectType)
                {
                    case ObjectType.Gameobject:
                        @object = ObjectManager.GetObjectsOfType<WoWGameObject>().OrderBy(ret => ret.Distance).FirstOrDefault(obj =>
                            !_npcBlacklist.Contains(obj.Guid) &&
                            obj.Distance < CollectionDistance &&
                            obj.Entry == MobId);

                        break;

                    case ObjectType.Npc:
                        if (Aura != 0)
                        {
                            string tmp;
                            try 
                            { 
                                tmp = WoWSpell.FromId(Aura).Name; 
                            }
                            catch
                            {
                                Logging.Write(Color.Red, "Could not find spell with id:{0} for UseItemOn behavior!", Aura);
                                Logging.Write(Color.Red, "Honorbuddy stopped!");
                                TreeRoot.Stop();
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
                            @object = ObjectManager.GetObjectsOfType<WoWUnit>().OrderBy(ret => ret.Distance).FirstOrDefault(obj =>
                                !_npcBlacklist.Contains(obj.Guid) &&
                                obj.Distance < CollectionDistance &&
                                obj.Entry == MobId);
                        }

                        break;
                }

                if (@object != null)
                {
                    Logging.Write(@object.Name);
                }
                return @object;
            }
        }

        #region Overrides of CustomForcedBehavior

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
            new PrioritySelector(

                new Decorator(ret => Counter >= NumOfTimes,
                    new Action(ret => _isDone = true)),

                    new PrioritySelector(
                        new Decorator(ret => CurrentObject != null && !CurrentObject.WithinInteractRange,
                            new Sequence(
                                new Action(delegate { TreeRoot.StatusText = "Moving to use item on - " + CurrentObject.Name; }),
                                new Action(ret => Navigator.MoveTo(CurrentObject.Location))
                                )
                            ),

                        new Decorator(ret => CurrentObject != null && CurrentObject.WithinInteractRange,
                            new Sequence(
                                new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                    new Action(delegate
                                    {
                                        WoWMovement.MoveStop();
                                        StyxWoW.SleepForLagDuration();
                                    })),

                                    new Action(delegate
                                    {
                                        TreeRoot.StatusText = "Using item on - " + CurrentObject.Name;
                                        if (CurrentObject is WoWUnit)
                                        {
                                            (CurrentObject as WoWUnit).Target();
                                        }

                                        var item = StyxWoW.Me.CarriedItems.FirstOrDefault(ret => ret.Entry == ItemId);
                                        if (item == null)
                                        {
                                            Logging.Write(Color.Red, "Could not find item with id:{0} for UseItemOn behavior!", ItemId);
                                            Logging.Write(Color.Red, "Honorbuddy stopped!");
                                            TreeRoot.Stop();
                                            return;
                                        }

                                        WoWMovement.Face(CurrentObject.Guid);

                                        item.UseContainerItem();
                                        _npcBlacklist.Add(CurrentObject.Guid);

                                        StyxWoW.SleepForLagDuration();
                                        Counter++;
                                        Thread.Sleep(WaitTime);
                                    }))
                                    ),

                        new Sequence(
                            new Action(delegate { TreeRoot.StatusText = "Moving towards - " + Location; }),
                            new Action(ret => Navigator.MoveTo(Location))))
                ));
        }

        private bool _isDone;
        public override bool IsDone
        {
            get
            {
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

                return
                    _isDone ||
                    (quest != null && quest.IsCompleted) ||
                    quest == null;
            }
        }

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
            if (quest != null)
            {
                var item = StyxWoW.Me.CarriedItems.FirstOrDefault(ret => ret.Entry == ItemId);
                if(item != null)
                    TreeRoot.GoalText = string.Format("Using item {0} for {1}",
                    item.Name,
                    quest.Name);
                
                else
                {
                    TreeRoot.GoalText = string.Format("Use item {0} times on mob with id:{1} for quest:{2}",
                        NumOfTimes,
                        MobId,
                        quest.Name);    
                }
            }
        }

        #endregion
    }
}