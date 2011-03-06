using System.Collections.Generic;
using System.Linq;
using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using System.Diagnostics;
using Styx.Logic.Combat;
using Styx.Logic;

using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// TheGiftThatKeepsOnGiving/UseItemOn by Nesox, rewriten by Highvoltz
    /// Allows you to use items on nearby gameobjects/npc's
    /// ##Syntax##
    /// [Optional]QuestId: The id of the quest.
    /// MobId The id of the object.
    /// [Optional]MobId2: The id of a 2nd object.
    /// [Optional]MobId3: The id of a 3rd object.
    /// ItemId: The id of the item to use.
    /// [Optional]NumOfTimes: Number of times to use said item.
    /// [Optional]WaitTime: Time to wait after using an item. DefaultValue: 1500 ms
    /// [Optional]CollectionDistance: The distance it will use to collect objects. DefaultValue:10000 yards( some NPCs can be view further then 100 yards)
    /// [Optional]HasAura: If a unit has a certian aura to check before using item. (By: j0achim)
    /// [Optional]StopMovingOnUse: (true/false) stops moving when using item. Default:true (By:HighVoltz)
    /// [Optional]HasGroundTarget: (true/false) true if you need to click the ground to cast spell in that area(Default: false)(By:HighVoltz)
    /// [Optional]IsDead: (true/false) true item is to be used on dead targets (Default: false)(By:HighVoltz)
    /// [Optional]InteractRange: The distance from the Object/NPC to use the item. Default: 4.5(By:HighVoltz)
    /// [Optional]MinionCount: Number of minions to gather. Used for the quest "The Gift That Keeps On Giving" Default: 0(By:HighVoltz)
    /// [Optional] X,Y,Z: The general location where theese objects can be found
    /// </summary>
    public class TheGiftThatKeepsOnGiving : CustomForcedBehavior
    {
        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {
            {"QuestId",null},
            {"MobId",null},
            {"MobId2",null},
            {"MobId3",null},
            {"ItemId",null},
            {"NumOfTimes",null},
            {"WaitTime",null},
            {"CollectionDistance",null}, 
            {"HasAura",null}, 
            {"StopMovingOnUse",null},
            {"HasGroundTarget",null},
            {"InteractRange",null},
            {"IsDead",null},
            {"MinionCount",null},
            {"X",null},
            {"Y",null},
            {"Z",null},
        };

        bool success = true;
        public TheGiftThatKeepsOnGiving(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);
            int questId = 0;
            int mobId = 0;
            int mobId2 = 0;
            int mobId3 = 0;
            int itemId = 0;
            int numOfTimes = 0;
            int waitTime = 0;
            float interactRange = 0;
            int collectionDistance = 0;
            int hasAura = 0;
            bool stopMoving = false;
            bool groundTarget = false;
            bool isDead = false;
            int minionCount = 0;
            WoWPoint point = WoWPoint.Empty;

            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
            success = success && GetAttributeAsInteger("MobId", true, "0", 0, int.MaxValue, out mobId);
            success = success && GetAttributeAsInteger("MobId2", false, "0", 0, int.MaxValue, out mobId2);
            success = success && GetAttributeAsInteger("MobId3", false, "0", 0, int.MaxValue, out mobId3);
            success = success && GetAttributeAsInteger("ItemId", true, "0", 0, int.MaxValue, out itemId);
            success = success && GetAttributeAsInteger("NumOfTimes", false, "1", 0, int.MaxValue, out numOfTimes);
            success = success && GetAttributeAsInteger("WaitTime", false, "1500", 0, int.MaxValue, out waitTime);
            success = success && GetAttributeAsFloat("InteractRange", false, "4.5", 0, int.MaxValue, out interactRange);
            success = success && GetAttributeAsInteger("CollectionDistance", false, "100000", 0, int.MaxValue, out collectionDistance);
            success = success && GetAttributeAsInteger("HasAura", false, "0", 0, int.MaxValue, out hasAura);
            success = success && GetAttributeAsInteger("MinionCount", false, "0", 0, int.MaxValue, out minionCount);
            success = success && GetAttributeAsBoolean("StopMovingOnUse", false, "true", out stopMoving);
            success = success && GetAttributeAsBoolean("IsDead", false, "false", out isDead);
            success = success && GetAttributeAsBoolean("HasGroundTarget", false, "false", out groundTarget);
            success = success && GetXYZAttributeAsWoWPoint("X", "Y", "Z", true, WoWPoint.Empty, out point);
            if (!success)
                Err("Stopping HB due to profile errors");

            QuestId = questId;
            MobID = mobId;
            MobID2 = mobId2;
            MobID3 = mobId3;
            ItemId = itemId;
            NumOfTimes = numOfTimes;
            WaitTime = waitTime;
            if (hasAura > 0)
            {
                var a = Styx.Logic.Combat.WoWSpell.FromId(hasAura);
                if (a != null)
                    Aura = a.Name;
                else
                    Err("Unable to find Aura: {0}", hasAura);
            }
            else
                Aura = "";
            ItemId = itemId;
            CollectionDistance = collectionDistance;
            StopMovingOnUse = stopMoving;
            HasGroundTarget = groundTarget;
            InteractRange = interactRange;
            Location = point;
            Counter = 0;
            IsDead = isDead;
            MinionCount = minionCount;
        }

        public WoWPoint Location { get; private set; }
        public int WaitTime { get; private set; }
        public string Aura { get; private set; }
        public int Counter { get; private set; }
        public int MobID { get; private set; }
        public int MobID2 { get; private set; }
        public int MobID3 { get; private set; }
        public int ItemId { get; private set; }
        public int NumOfTimes { get; private set; }
        public int MinionCount { get; private set; }
        public float InteractRange { get; private set; }
        public int QuestId { get; private set; }
        public bool StopMovingOnUse { get; private set; }
        public bool HasGroundTarget { get; private set; }
        public bool IsDead { get; private set; }
        public int CollectionDistance { get; private set; }
        private readonly List<ulong> _npcBlacklist = new List<ulong>();
        LocalPlayer me = ObjectManager.Me;
        Stopwatch waitTimer = new Stopwatch();
        public WoWObject Object
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWObject>(true)
                    .Where(o => ObjCheck(o, MobID) ||
                        (MobID2 > 0 && ObjCheck(o, MobID2)) ||
                        (MobID3 > 0 && ObjCheck(o, MobID3)))
                        .OrderBy(o => o.Distance).FirstOrDefault();
            }
        }
        bool ObjCheck(WoWObject obj, int Id)
        {
            bool ret = false;
            if (obj.Entry == Id && obj.Distance <= CollectionDistance &&
                !_npcBlacklist.Contains(obj.Guid) && AuraCheck(obj))
            {
                if ((IsDead && obj is WoWUnit && !((WoWUnit)obj).Dead) || !IsDead && obj is WoWUnit && ((WoWUnit)obj).Dead)
                    ret = false;
                else 
                    ret = true;
            }
            // temp fix to HB killing targets without letting us using item...
            if (ret && obj is WoWUnit)
                Blacklist.Add(obj, new System.TimeSpan(0, 10, 0));
            return ret;
        }

        bool AuraCheck(WoWObject obj)
        {
            if (string.IsNullOrEmpty(Aura) || !(obj is WoWUnit))
                return true;
            if (((WoWUnit)obj).HasAura(Aura))
                return true;
            return false;
        }

        #region Overrides of CustomForcedBehavior
        private Composite root;
        protected override Composite CreateBehavior()
        {
            return root ??
                (root = new PrioritySelector(
                    new Decorator(c => Counter >= NumOfTimes,
                        new Action(ret => isDone = true)),
                    //new Decorator(c => { Log("{0}", waitTimer.IsRunning); if (((waitTimer.IsRunning && waitTimer.ElapsedMilliseconds < WaitTime) || me.Combat)) return true; return false; },
                    //        new Action(c => { Log("wewewe"); return RunStatus.Success; })
                    //    ),
                    new Decorator(c => Object != null && Object.Distance > InteractRange,
                        new Action(c =>
                        {
                            TreeRoot.StatusText = "Moving to use item on - " + Object.Name;
                            Navigator.MoveTo(Object.Location);
                        })
                    ),
                    new Decorator(c => Object != null && Object.Distance <= InteractRange,
                        new Sequence(
                            new DecoratorContinue(c => StopMovingOnUse && me.IsMoving,
                                new Action(c => { WoWMovement.MoveStop(); })
                            ),
                            new Action(c => {
                                TreeRoot.StatusText = "Using item on " + Object.Name;
                                if (Object is WoWUnit)
                                {
                                    (Object as WoWUnit).Target();
                                }
                                var item = StyxWoW.Me.CarriedItems.FirstOrDefault(ret => ret.Entry == ItemId);
                                if (item == null)
                                {
                                    Err("Could not find item with id:{0} for UseItemOn behavior!\n Stopping HB", ItemId);
                                }
                                WoWMovement.Face(Object.Guid);
                                item.UseContainerItem();
                                if (HasGroundTarget)
                                    LegacySpellManager.ClickRemoteLocation(Object.Location);
                                _npcBlacklist.Add(Object.Guid);
                                waitTimer.Reset();
                                if (!waitTimer.IsRunning)
                                    waitTimer.Start();
                                Counter++;
                            })
                        )),
                    new Action(c =>
                    {
                        Log("Moving to {0}", Location);
                        Navigator.MoveTo(Location);
                    })
                 ));
        }

        void Err(string format, params object[] args)
        {
            Logging.Write(System.Drawing.Color.Red, "UseItemOn: " + format, args);
            TreeRoot.Stop();
        }

        void Log(string format, params object[] args)
        {
            Logging.Write("UseItemOn: " + format, args);
        }

        private bool isDone = false;
        public override bool IsDone
        {
            get
            {
                var quest = ObjectManager.Me.QuestLog.GetQuestById((uint)QuestId);
                return isDone || (QuestId > 0 && ((quest != null && quest.IsCompleted) || quest == null) ||
                    (MinionCount > 0 && MinionCount <= me.Minions.Count));
            }
        }

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
            var item = StyxWoW.Me.CarriedItems.FirstOrDefault(ret => ret.Entry == ItemId);
            if (item != null)
                TreeRoot.GoalText = string.Format("Using item {0} for {1}",
                item.Name,
                quest != null ? quest.Name : "Fun");

            else
            {
                TreeRoot.GoalText = string.Format("Use item {0} times on mob with id:{1} for quest:{2}",
                    NumOfTimes,
                    MobID,
                    quest != null ? quest.Name : "");
            }
        }

        #endregion
    }
    static class WoWUnitExtentions
    {
        public static bool IsTargetingMinion(this WoWUnit unit)
        {
            if (unit.GotTarget)
            {
                foreach (var minion in ObjectManager.Me.Minions)
                {
                    if (unit.CurrentTarget == minion)
                        return true;
                }
            }
            return false;
        }
    }
}
