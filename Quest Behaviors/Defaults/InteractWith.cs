using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// InteractWith by Nesox
    /// Allows you to do quests that requires you to interact with nearby objects.
    /// ##Syntax##
    /// [Optional]QuestId: Id of the quest.
    /// NpcId: Id of the object to interact with.
    /// NumOfTimes: Number of times to interact with object.
    /// [Optional]GossipOption: The Dialog number you wish to choose. DefaultValue:1
    /// [Optional]CollectionDistance: The distance it will use to collect objects. DefaultValue:100 yards
    /// [Optional]BuySlot: Buys the item from the slot. Slots are: 1 2
    ///                                                            3 4
    ///                                                            5 6
    ///                                                            7 8
    ///                                                            page2
    ///                                                            9 10 etc.
    /// [Optional]WaitTime: The time to wait once it has interacted with an object. DefaultValue:3000
    /// ObjectType: the type of object to interact with, expected value: Npc/Gameobject
    /// X,Y,Z: The general location where theese objects can be found
    /// </summary>
    public class InteractWith : CustomForcedBehavior
    {
        
        public InteractWith(Dictionary<string, string> args)
            : base(args)
        {
            bool error = false;

            uint questId = 0;
            if (Args.ContainsKey("QuestId"))
            {
                if (!uint.TryParse(Args["QuestId"], out questId))
                {
                    Logging.Write(
                        "Parsing attribute 'QuestId' in InteractWith behavior failed! please check your profile!");
                    error = true;
                }
            }

            int waitTime;
            if (Args.ContainsKey("WaitTime") && !int.TryParse(Args["WaitTime"], out waitTime))
            {
                Logging.Write("Parsing attribute 'MobId' in InteractWith behavior failed! please check your profile!");
                error = true;
            }
            else
                waitTime = 3000;

            uint mobId = 0;
            if (Args.ContainsKey("MobId"))
            {
                if (!uint.TryParse(Args["MobId"], out mobId))
                {
                    Logging.Write("Parsing attribute 'MobId' in InteractWith behavior failed! please check your profile!");
                    error = true;
                }
            }
            else if (!uint.TryParse(Args["NpcId"], out mobId))
            {
                Logging.Write("Parsing attribute 'NpcId' in InteractWith behavior failed! please check your profile!");
                error = true;
            }

            int numOfTimes;
            if (!int.TryParse(Args["NumOfTimes"], out numOfTimes))
            {
                Logging.Write("Parsing attribute 'NumOfTimes' in InteractWith behavior failed! please check your profile!");
                error = true;
            }

            if (Args.ContainsKey("CollectionDistance"))
            {
                int distance;
                int.TryParse(Args["CollectionDistance"], out distance);
                CollectionDistance = distance != 0 ? distance : 100;
            }

            if (!Args.ContainsKey("ObjectType"))
            {
                Logging.Write("Could not find attribute 'ObjectType' in InteractWith behavior! please check your profile!");
                error = true;
            }

            var type = (ObjectType)Enum.Parse(typeof(ObjectType), Args["ObjectType"], true);

            float x, y, z;
            if (!float.TryParse(Args["X"], out x))
            {
                Logging.Write("Parsing attribute 'X' in InteractWith behavior failed! please check your profile!");
                error = true;
            }

            if (!float.TryParse(Args["Y"], out y))
            {
                Logging.Write("Parsing attribute 'Y' in InteractWith behavior failed! please check your profile!");
                error = true;
            }

            if (!float.TryParse(Args["Z"], out z))
            {
                Logging.Write("Parsing attribute 'Z' in InteractWith behavior failed! please check your profile!");
                error = true;
            }

            int bSlot = 0;
            if (Args.ContainsKey("BuySlot"))
            {
                if (!int.TryParse(Args["BuySlot"], out bSlot))
                {
                    Logging.Write("Parsing attribute 'BuySlot' in InteractWith behavior failed! please check your profile!");
                    error = true;
                }
            }

            int gossipOption = 0;
            if (Args.ContainsKey("GossipOption"))
            {
                int gossipopt;
                int.TryParse(Args["GossipOption"], out gossipopt);
                gossipOption = gossipopt != 0 ? gossipopt : 1;
            }

            if (error)
                TreeRoot.Stop();

            GossipOption = gossipOption;
            WaitTime = waitTime;
            ObjectType = type;
            QuestId = questId;
            NumOfTimes = numOfTimes;
            MobId = mobId;
            BuySlot = bSlot;
            Location = new WoWPoint(x, y, z);
        }

        public WoWPoint Location { get; private set; }
        public int Counter { get; set; }
        public uint MobId { get; set; }
        public int NumOfTimes { get; set; }
        public int BuySlot { get; set; }
        private int WaitTime { get; set; }
        public uint QuestId { get; private set; }
        public ObjectType ObjectType { get; private set; }
        public int CollectionDistance = 100;
        public int GossipOption { get; private set; }

        private readonly List<ulong> _npcBlacklist = new List<ulong>();

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
                        @object = ObjectManager.GetObjectsOfType<WoWUnit>().OrderBy(ret => ret.Distance).FirstOrDefault(obj =>
                            !_npcBlacklist.Contains(obj.Guid) &&
                            obj.Distance < CollectionDistance &&
                            obj.Entry == MobId);

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
                                    new Action(delegate { TreeRoot.StatusText = "Moving to interact with - " + CurrentObject.Name; }),
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
                                        TreeRoot.StatusText = "Interacting with - " + CurrentObject.Name;
                                        CurrentObject.Interact();
                                        _npcBlacklist.Add(CurrentObject.Guid);

                                        StyxWoW.SleepForLagDuration();
                                        Counter++;
                                    }),

                                    new DecoratorContinue(
                                        ret => GossipOption != 0,
                                        new Action(delegate
                                            {
                                                Lua.DoString("SelectGossipOption(" + GossipOption + ")");
                                                Thread.Sleep(1000);
                                            })),
                                    
                                    new DecoratorContinue(
                                        ret => BuySlot != 0,
                                        new Action(delegate
                                            {
                                                Lua.DoString("BuyMerchantItem(" + BuySlot + ",1)");
                                                Thread.Sleep(1500);
                                            })),

                                    new Action(ret => Thread.Sleep(WaitTime))

                                )),

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
					(QuestId > 0 && quest == null) ||
					(quest != null && quest.IsCompleted);
            }
        }

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
            if (quest != null)
                TreeRoot.GoalText = string.Format("Interacting with Mob Id:{0} {1} Times for quest:{2}", MobId, NumOfTimes, quest.Name);
            else
                TreeRoot.GoalText = "Running: " + GetType().Name;
        }

        #endregion
    }

    public enum ObjectType
    {
        Npc,
        Gameobject
    }
}
