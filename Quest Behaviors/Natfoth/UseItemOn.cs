using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Styx.Database;
using Styx.Logic.Combat;
using Styx.Helpers;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Pathing;
using Styx.Logic.Profiles.Quest.Order;
using Styx.Logic.Questing;
using Styx.Logic.POI;
using Styx.Combat.CombatRoutine;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Styx.Logic.BehaviorTree;
using Action = TreeSharp.Action;
using Timer = Styx.Helpers.WaitTimer;

namespace Styx.Bot.Quest_Behaviors
{
    public class UseItemOn : CustomForcedBehavior
    {

        /// <summary>
        /// UseItemOn by Natfoth
        /// Allows you to use an item on an Mob.
        /// ##Syntax##
        /// QuestId: If you have a questID and it is > 0 then it will do the behavior over and over until the quest is complete, otherwise it will use NumOfTimes.
        /// ItemId: The ItemID to use on the mob.
        /// Range: The Range you want to stop and use the item from the Mob
        /// NpcId NpcId2 NpcId3: The MobId that you will use your item on, accept 3 mob Ids
        /// NumOfTimes: How many times will it use the item.
        /// HpLeftAmount: At what HP do you want to use the item on, such as a wounded guard. 0 will cause it to use it only on dead targets.
        /// InCombatHPUse: This is used for Capture quests or when you need to use an item at say 20% Mob HP.
        /// FactionID: Which factions to target.
        /// WaitTime: How long to wait after using the item before you move on.
        /// X,Y,Z: The general location where these mobs can be found
        /// </summary>
        /// 

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"ItemId",null},
            {"Range",null},
            {"NpcId",null},
            {"NpcId2",null},
            {"NpcId3",null},
            {"NumOfTimes",null},
            {"HpLeftAmount",null},
            {"InCombatHPUse",null},
            {"FactionID",null},
            {"WaitTime",null},
            {"QuestId",null},
            {"X",null},
            {"Y",null},
            {"Z",null},

        };

        bool success = true;
        public Timer _timer;


        public UseItemOn(Dictionary<string, string> args)
            : base(args)
        {

            CheckForUnrecognizedAttributes(recognizedAttributes);

            int itemId = 0;
            int range = 0;
            int npcID = 0;
            int npcID2 = 0;
            int npcID3 = 0;
            int factionid = 0;
            int numberoftimes = 0;
            int hpAmountLeft = 0;
            int inCombatHpUse = 0;
            int waittime = 0;
            int questId = 0;
            WoWPoint location = new WoWPoint(0, 0, 0);
            
            success = success && GetAttributeAsInteger("ItemId", true, "1", 0, int.MaxValue, out itemId);
            success = success && GetAttributeAsInteger("Range", false, "5", 0, int.MaxValue, out range);
            success = success && GetAttributeAsInteger("NpcId", true, "1", 0, int.MaxValue, out npcID);
            success = success && GetAttributeAsInteger("NpcId2", false, "1", 0, int.MaxValue, out npcID2);
            success = success && GetAttributeAsInteger("NpcId3", false, "1", 0, int.MaxValue, out npcID3);
            success = success && GetAttributeAsInteger("FactionID", false, "0", 0, int.MaxValue, out factionid);
            success = success && GetAttributeAsInteger("NumOfTimes", false, "1", 0, int.MaxValue, out numberoftimes);
            success = success && GetAttributeAsInteger("HpLeftAmount", false, "110", 0, int.MaxValue, out hpAmountLeft);
            success = success && GetAttributeAsInteger("InCombatHPUse", false, "0", 0, int.MaxValue, out inCombatHpUse);
            success = success && GetAttributeAsInteger("WaitTime", false, "1000", 0, int.MaxValue, out waittime);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
            success = success && GetXYZAttributeAsWoWPoint("X", "Y", "Z", true, new WoWPoint(0, 0, 0), out location);

            QuestId = (uint)questId;
            ItemID = itemId;
            Range = range;
            NPCID = npcID;
            NPCID2 = npcID2;
            NPCID3 = npcID3;
            FactionID = factionid;
            Counter = 1;
            WaitTime = waittime;
            HpLeftAmount = hpAmountLeft;
            InCombatHPUse = inCombatHpUse;
            MovedToTarget = false;
            NumberOfTimes = numberoftimes;
            Location = location;
        }

        public WoWPoint Location { get; private set; }
        public int Counter { get; set; }
        public int ItemID { get; set; }
        public int Range { get; set; }
        public int NPCID { get; set; }
        public int NPCID2 { get; set; }
        public int NPCID3 { get; set; }
        public int FactionID { get; set; }
        public bool MovedToTarget;
        public int NumberOfTimes { get; set; }
        public int WaitTime { get; set; }
        public int HpLeftAmount { get; set; }
        public int InCombatHPUse { get; set; }
        public uint QuestId { get; set; }

        public static LocalPlayer me = ObjectManager.Me;
        public List<ulong> usedGUID = new List<ulong>();

        public List<WoWUnit> npcList
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>()
                                            .Where(u => u.Entry == NPCID && !u.Dead && u.HealthPercent <= HpLeftAmount)
                                            .OrderBy(u => u.Distance).ToList();
            }
        }

        public List<WoWUnit> npcDeadList
        {
            get
            {
                if (usedGUID != null && usedGUID.Count >= 1)
                {
                    return ObjectManager.GetObjectsOfType<WoWUnit>()
                        .Where(u => (u.Entry == NPCID || u.Entry == NPCID2 || u.Entry == NPCID3) && u.Dead && !usedGUID.Contains(u.Guid))
                        .OrderBy(u => u.Distance).ToList();
                }
                else
                {
                    return ObjectManager.GetObjectsOfType<WoWUnit>()
                        .Where(u => (u.Entry == NPCID || u.Entry == NPCID2 || u.Entry == NPCID3) && u.Dead)
                        .OrderBy(u => u.Distance).ToList();
                }
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
            _timer = null;
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

            if (quest != null)
            {
                TreeRoot.GoalText = "UseItemOn - " + quest.Name;
            }
            else
            {
                TreeRoot.GoalText = "UseItemOn: Running";
            }
        }

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                           new Decorator(ret => (Counter > NumberOfTimes && QuestId == 0)  || (me.QuestLog.GetQuestById(QuestId) != null && me.QuestLog.GetQuestById(QuestId).IsCompleted),
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new WaitContinue(120,
                                        new Action(delegate
                                        {
                                            _isDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )),

                           new Decorator(ret => _timer != null && !_timer.IsFinished,
                                new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "UseItemOn - WaitTimer: " + _timer.TimeLeft.Seconds + " Seconds Left"),
                                        new Action(delegate { return RunStatus.Success; })
                                    )
                                ),

                           new Decorator(ret => _timer != null && _timer.IsFinished,
                                new Sequence(
                                        new Action(ret => _timer.Reset()),
                                        new Action(ret => _timer = null),
                                        new Action(delegate { return RunStatus.Success; })
                                    )
                                ),


                           new Decorator(c => npcList.Count == 0,
                            new Action(c =>
                            {
                                if (!me.IsIndoors && !me.Mounted && me.Level > 19)
                                {
                                    TreeRoot.StatusText = "Mounting!";
                                    Styx.Logic.Mount.MountUp();
                                }

                                TreeRoot.StatusText = "Moving To Location - X: " + Location.X + " Y: " + Location.Y + " Z: " + Location.Z;
                                Navigator.MoveTo(Location);
                            })
                        ),
                        new Decorator(c => npcDeadList.Count > 0,
                            new Action(c =>
                            {
                                if (!npcDeadList[0].WithinInteractRange)
                                {
                                    TreeRoot.StatusText = "Moving To Loot - " + npcDeadList[0].Name + " Yards Away: " + npcDeadList[0].Location.Distance(me.Location);
                                    Navigator.MoveTo(npcDeadList[0].Location);
                                }
                                else
                                {
                                    TreeRoot.StatusText = "Using Item - " + wowItem.Name + " On Mob: " + npcDeadList[0].Name;
                                    WoWMovement.MoveStop();
                                    npcDeadList[0].Target();
                                    npcDeadList[0].Face();
                                    Thread.Sleep(300);
                                    wowItem.Interact();
                                    Counter++;
                                    _timer = new Timer(new TimeSpan(0, 0, 0, 0, WaitTime));
                                    _timer.Reset();
                                }
                            })),

                        new Decorator(c => npcList.Count > 0 && InCombatHPUse > 0,
                            new Action(c =>
                            {

                                if (npcList[0].Location.Distance(me.Location) <= Range && InCombatHPUse > 0 && !me.IsCasting)
                                {
                                    if (npcList[0].HealthPercent > InCombatHPUse)
                                    {
                                        TreeRoot.StatusText = "Attacking Mob - Current Health %:" + npcList[0].HealthPercent + " Attacking Until: " + InCombatHPUse;
                                        WoWMovement.MoveStop();
                                        if (me.CurrentTarget == npcList[0])
                                        {
                                            npcList[0].Target();
                                            npcList[0].Face();
                                            Thread.Sleep(300);
                                        }
                                        SpellManager.Cast(RangeSpell);
                                        return RunStatus.Running;
                                    }
                                    else
                                    {
                                        TreeRoot.StatusText = "Using Item - " + wowItem.Name + " On Mob: " + npcDeadList[0].Name;
                                        WoWMovement.MoveStop();
                                        npcList[0].Target();
                                        npcList[0].Face();
                                        Thread.Sleep(300);
                                        wowItem.Interact();
                                        Counter++;
                                        return RunStatus.Success;
                                    }
                                }

                                return RunStatus.Failure;

                            })),

                         new Decorator(c => npcList.Count > 0 && InCombatHPUse == 0,
                            new Action(c =>
                            {

                                if (npcList[0].Location.Distance(me.Location) < Range)
                                {
                                    TreeRoot.StatusText = "Using Item - " + wowItem.Name + " On Mob: " + npcList[0].Name;
                                    WoWMovement.MoveStop();
                                    Thread.Sleep(300);
                                    npcList[0].Target();
                                    npcList[0].Face();
                                    Thread.Sleep(300);
                                    wowItem.Interact();
                                    Counter++;
                                    _timer = new Timer(new TimeSpan(0, 0, 0, 0, WaitTime));
                                    _timer.Reset();

                                    return RunStatus.Success;
                                }
                                else
                                {

                                    TreeRoot.StatusText = "Moving To Mob - " + npcList[0].Name + " Yards Away: " + npcList[0].Location.Distance(me.Location);
                                    Navigator.MoveTo(npcList[0].Location);
                                    return RunStatus.Running;
                                }
                            }))

                    ));
        }

        WoWSpell RangeSpell
        {
            get
            {
                switch (me.Class)
                {
                    case Styx.Combat.CombatRoutine.WoWClass.Druid:
                        return SpellManager.Spells["Starfire"];
                    case Styx.Combat.CombatRoutine.WoWClass.Hunter:
                        return SpellManager.Spells["Arcane Shot"];
                    case Styx.Combat.CombatRoutine.WoWClass.Mage:
                        return SpellManager.Spells["Frost Bolt"];
                    case Styx.Combat.CombatRoutine.WoWClass.Priest:
                        return SpellManager.Spells["Shoot"];
                    case Styx.Combat.CombatRoutine.WoWClass.Shaman:
                        return SpellManager.Spells["Lightning Bolt"];
                    case Styx.Combat.CombatRoutine.WoWClass.Warlock:
                        return SpellManager.Spells["Curse of Agony"];
                    default: // should never get to here but adding this since the compiler complains
                        return SpellManager.Spells["Auto Attack"]; ;

                }
            }
        }



        private bool _isDone;
        public override bool IsDone
        {
            get { return _isDone; }
        }

        #endregion
    }
}
