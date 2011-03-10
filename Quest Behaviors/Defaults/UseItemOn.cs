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
            Gameobject
        }

        public enum NpcState
        {
            None,
            Dead,
            Alive,
            BelowHp
        }

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {
            {"QuestId",null},
            {"MobId",null},
            {"NpcId",null},
            {"ItemId",null},
            {"NumOfTimes",null},
            {"WaitTime",null},
            {"CollectionDistance",null},
            {"X",null},
            {"Y",null},
            {"Z",null},
            {"HasAura",null},
            {"Range",null},
            {"ObjectType",null}
        };

        public UseItemOn(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                CheckForUnrecognizedAttributes(recognizedAttributes);

                bool error = false;

                uint questId;
                if (!uint.TryParse(Args["QuestId"], out questId))
                {
                    Logging.Write("Parsing attribute 'QuestId' in UseItemOn behavior failed! please check your profile!");
                    error = true;
                }

                uint mobId;
                if (Args.ContainsKey("MobId"))
                {
                    if (!uint.TryParse(Args["MobId"], out mobId))
                    {
                        Logging.Write("Parsing attribute 'MobId' and 'NpcId' in UseItemOn behavior failed! please check your profile!");
                        error = true;
                    }
                }
                else if (!uint.TryParse(Args["NpcId"], out mobId))
                {
                    Logging.Write("Parsing attribute 'MobId' and 'NpcId' in UseItemOn behavior failed! please check your profile!");
                    error = true;
                }

                uint itemId;
                if (!uint.TryParse(Args["ItemId"], out itemId))
                {
                    Logging.Write("Parsing attribute 'ItemId' in UseItemOn behavior failed! please check your profile!");
                    error = true;
                }

                int numOfTimes = 1;
                if (Args.ContainsKey("NumOfTimes"))
                {
                    if (!int.TryParse(Args["NumOfTimes"], out numOfTimes))
                    {
                        Logging.Write("Parsing attribute 'NumOfTimes' in UseItemOn behavior failed! please check your profile!");
                        error = true;
                    }
                }

                if (Args.ContainsKey("WaitTime"))
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

                if (Args.ContainsKey("Range"))
                {
                    int range;
                    int.TryParse(Args["Range"], out range);
                    Range = range != 0 ? range : 4;
                }

                if (!Args.ContainsKey("ObjectType"))
                {
                    _ObjectType = ObjectType.Npc;
                }
                else
                {
                    _ObjectType = (ObjectType)Enum.Parse(typeof(ObjectType), Args["ObjectType"], true);
                }

                if (!Args.ContainsKey("NpcState"))
                {
                    _NpcState = NpcState.None;
                }
                else
                {
                    _NpcState = (NpcState)Enum.Parse(typeof(NpcState), Args["NpcState"], true);
                }

                if (Args.ContainsKey("HpLeftAmount"))
                {
                    int hpleft = -1;
                    if (!int.TryParse(Args["HpLeftAmount"], out hpleft))
                    {
                        Logging.Write("Parsing attribute 'HpLeftAmount' in UseItemOn behavior failed! please check your profile!");
                        error = true;
                    }
                    HpLeftAmount = hpleft != -1 ? hpleft : 100;
                }


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

                QuestId = questId;
                NumOfTimes = numOfTimes;
                MobId = mobId;
                ItemId = itemId;
                Location = new WoWPoint(x, y, z);
            }
            catch (Exception ex)
            {
                Logging.Write("UseItemOn failed");
                Logging.WriteException(ex);
            }
        }

        public WoWPoint Location { get; private set; }
        public int WaitTime { get; private set; }
        public int Aura { get; private set; }
        public int Counter { get; private set; }
        public int Range { get; private set; }
        public int HpLeftAmount { get; private set; }
        public uint MobId { get; private set; }
        public uint ItemId { get; private set; }
        public int NumOfTimes { get; private set; }
        public uint QuestId { get; private set; }
        public ObjectType _ObjectType { get; private set; }
        public NpcState _NpcState { get; private set; }
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
                switch (_ObjectType)
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
                            switch (_NpcState)
                            {
                                case NpcState.None:
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
                {
                    Logging.Write(@object.Name);
                }
                return @object;
            }
        }

        public WoWItem Item
        {
            get
            {
                return StyxWoW.Me.CarriedItems.FirstOrDefault(ret => ret.Entry == ItemId && ret.Usable && ret.Cooldown == 0);
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
                        new Decorator(ret => CurrentObject != null && CurrentObject.DistanceSqr > Range * Range,
                            new Sequence(
                                new Action(delegate { TreeRoot.StatusText = "Moving to use item on - " + CurrentObject.Name; }),
                                new Action(ret => Navigator.MoveTo(CurrentObject.Location))
                                )
                            ),

                        new Decorator(ret => CurrentObject != null && CurrentObject.DistanceSqr <= Range * Range && Item != null,
                            new Sequence(
                                new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                    new Action(ret =>
                                    {
                                        WoWMovement.MoveStop();
                                        StyxWoW.SleepForLagDuration();
                                    })),

                                new Action(ret =>
                                {
                                    TreeRoot.StatusText = "Using item on - " + CurrentObject.Name;
                                    if (CurrentObject is WoWUnit && (StyxWoW.Me.CurrentTarget == null || StyxWoW.Me.CurrentTarget != CurrentObject))
                                    {
                                        (CurrentObject as WoWUnit).Target();

                                        StyxWoW.SleepForLagDuration();
                                    }
                                        
                                    WoWMovement.Face(CurrentObject.Guid);

                                    Item.UseContainerItem();
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