using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Styx.Database;
using Styx.Logic.Combat;
using Styx.Helpers;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Pathing;
using Styx.Logic.Profiles.Quest;
using Styx.Logic.Questing;
using Styx.Logic.POI;
using Styx.Combat.CombatRoutine;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Styx.Logic.BehaviorTree;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    public class WaterBehavior : CustomForcedBehavior
    {

        /// <summary>
        /// WaterBehavior by Natfoth
        /// Kill/Collect things within the Water
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// NpcID: MobId of the vehicle before it is mounted.
        /// ObjectID, ObjectID2, ObjectID3: Mob of the actual Vehicle, sometimes it will be the some but sometimes it will not be.
        /// NumberOfTimes: Button bar Number starting from 1
        /// X,Y,Z: Where you want to be at when you fire.
        /// </summary>
        /// 

        #region Overrides of CustomForcedBehavior.

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"NpcID",null},
            {"ObjectID",null},
            {"ObjectID2",null},
            {"ObjectID3",null},
            {"NumberOfTimes",null},
            {"X",null},
            {"Y",null},
            {"Z",null},
            {"QuestId",null},

        };

        bool success = true;

        public WaterBehavior(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);

            int npcID = 0;
            int objectID = 0;
            int objectID2 = 0;
            int objectID3 = 0;
            int numberoftimes = 0;
            int questId = 0;
            WoWPoint location = new WoWPoint(0, 0, 0);
            
            success = success && GetAttributeAsInteger("NpcID", false, "0", 0, int.MaxValue, out npcID);
            success = success && GetAttributeAsInteger("ObjectID", false, "0", 0, int.MaxValue, out objectID);
            success = success && GetAttributeAsInteger("ObjectID2", false, "0", 0, int.MaxValue, out objectID2);
            success = success && GetAttributeAsInteger("ObjectID3", false, "0", 0, int.MaxValue, out objectID3);
            success = success && GetAttributeAsInteger("NumberOfTimes", false, "0", 0, int.MaxValue, out numberoftimes);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
            success = success && GetXYZAttributeAsWoWPoint("X", "Y", "Z", true, new WoWPoint(0, 0, 0), out location);

            NPCID = npcID;
            ObjectID = objectID;
            ObjectID2 = objectID2;
            ObjectID3 = objectID3;
            NumberOfTimes = numberoftimes;
            QuestId = (uint)questId;
            Location = location;

            Counter = 0;
            TimesUsedCounter = 1;
            MovedToLocation = false;

        }

        public WoWPoint Location { get; private set; }
        public int Counter { get; set; }
        public int TimesUsedCounter { get; set; }
        public int NPCID { get; set; }
        public int ObjectID { get; set; }
        public int ObjectID2 { get; set; }
        public int ObjectID3 { get; set; }
        public bool MovedToLocation { get; set; }
        public int NumberOfTimes { get; set; }
        public int MoveToNPC { get; set; }
        public uint QuestId { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        public List<WoWUnit> npcList
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>()
                                            .Where(u => u.Entry == NPCID && !u.Dead)
                                            .OrderBy(u => u.Distance).ToList();
            }
        }

        public List<WoWUnit> lootList
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>()
                                            .Where(u => u.Entry == NPCID && u.Dead && u.Lootable)
                                            .OrderBy(u => u.Distance).ToList();
            }
        }

        public List<WoWGameObject> objectList
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWGameObject>()
                                        .Where(u => (u.Entry == ObjectID || u.Entry == ObjectID2 || u.Entry == ObjectID3) && !u.InUse && !u.IsDisabled)
                                        .OrderBy(u => u.Distance).ToList();
            }
        }

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                            new Decorator(ret => (me.QuestLog.GetQuestById(QuestId) != null && me.QuestLog.GetQuestById(QuestId).IsCompleted),
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new WaitContinue(120,
                                        new Action(delegate
                                        {
                                            _isDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )),

                            new Decorator(ret => me.GetMirrorTimerInfo(MirrorTimerType.Breath).CurrentTime < 20000 && me.GetMirrorTimerInfo(MirrorTimerType.Breath).CurrentTime != 0,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new Action(ret => WoWMovement.ClickToMove(Location)),
                                    new Action(ret => Thread.Sleep(100))
                                )),

                           new Decorator(ret => lootList.Count > 0,
                                new Sequence(
                                    new DecoratorContinue(ret => !lootList[0].WithinInteractRange,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Moving to Loot - " + lootList[0].Name + " Yards Away " + lootList[0].Location.Distance(me.Location)),
                                            new Action(ret => WoWMovement.ClickToMove(lootList[0].Location)),
                                            new Action(ret => Thread.Sleep(300))
                                            )
                                    ),
                                    new DecoratorContinue(ret => lootList[0].WithinInteractRange,
                                        new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Looting - " + lootList[0].Name),
                                        new Action(ret => WoWMovement.MoveStop()),
                                        new Action(ret => lootList[0].Interact()),
                                        new Action(ret => Thread.Sleep(1000))
                                            ))
                                    )),

                           new Decorator(ret => npcList.Count == 0 || objectList.Count == 0,
                                new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Moving To Location - X: " + Location.X + " Y: " + Location.Y),
                                        new Action(ret => Navigator.MoveTo(Location)),
                                        new Action(ret => Thread.Sleep(100))
                                    )
                                ),

                           new Decorator(ret => npcList.Count > 0,
                                new Sequence(
                                    new DecoratorContinue(ret => npcList[0].Location.Distance(me.Location) > range,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Moving to Mob - " + npcList[0].Name + " Yards Away " + npcList[0].Location.Distance(me.Location)),
                                            new Action(ret => WoWMovement.ClickToMove(npcList[0].Location)),
                                            new Action(ret => Thread.Sleep(300))
                                            )
                                    ),
                                    new DecoratorContinue(ret => npcList[0].Location.Distance(me.Location) <= range,
                                        new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Attacking Mob - " + npcList[0].Name + " With Spell: " + RangeSpell.Name),
                                        new Action(ret => WoWMovement.MoveStop()),
                                        new Action(ret => npcList[0].Target()),
                                        new Action(ret => npcList[0].Face()),
                                        new Action(ret => Thread.Sleep(200)),
                                        new Action(ret => SpellManager.Cast(RangeSpell)),
                                        new Action(ret => Thread.Sleep(300))
                                            ))
                                    )),

                            new Decorator(ret => objectList.Count > 0,
                                new Sequence(
                                    new DecoratorContinue(ret => !objectList[0].WithinInteractRange,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Moving to Object - " + objectList[0].Name + " Yards Away " + objectList[0].Location.Distance(me.Location)),
                                            new Action(ret => WoWMovement.ClickToMove(objectList[0].Location)),
                                            new Action(ret => Thread.Sleep(300))
                                            )
                                    ),
                                    new DecoratorContinue(ret => objectList[0].WithinInteractRange,
                                        new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Opening Object - " + objectList[0].Name),
                                        new Action(ret => WoWMovement.MoveStop()),
                                        new Action(ret => objectList[0].Interact()),
                                        new Action(ret => Thread.Sleep(1000)),
                                        new Action(ret => Counter++)
                                            ))
                                    ))
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

        bool isRanged
        {
            get
            {
               return (me.Class == WoWClass.Druid && 
                   (SpellManager.HasSpell("balanceSpell") || SpellManager.HasSpell("RestoSpell"))||
                   me.Class == WoWClass.Shaman && 
                   (SpellManager.HasSpell("ElementalSpell") || SpellManager.HasSpell("RestoSpell"))||
                   me.Class == WoWClass.Hunter || me.Class == WoWClass.Mage || me.Class == WoWClass.Priest ||
                   me.Class == WoWClass.Warlock);
            }
        }

        int range
        {
            get
            {
                if (isRanged)
                {
                    return 25;
                }
                else
                {
                    return 3;
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

