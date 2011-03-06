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
using Styx.Logic;
using Styx.Logic.BehaviorTree;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    public class KillUntilComplete : CustomForcedBehavior
    {
        

        /// <summary>
        /// KillUntilComplete by Natfoth
        /// This is only used when you get a quest that Says, Kill anything x times. Or on the chance the wowhead ID is wrong
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// MobId, MobId2, MobId3: Currently Accepts 3 Mob Values that it will kill.
        /// X,Y,Z: The general location where theese objects can be found
        /// </summary>
        /// 

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"NpcID",null},
            {"NpcID2",null},
            {"NpcID3",null},
            {"X",null},
            {"Y",null},
            {"Z",null},
            {"QuestId",null},

        };

        bool success = true;

        public KillUntilComplete(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);

            int mobID = 0;
            int mobID2 = 0;
            int mobID3 = 0;
            int questId = 0;
            WoWPoint location = new WoWPoint(0, 0, 0);

            success = success && GetAttributeAsInteger("NpcID", true, "0", 0, int.MaxValue, out mobID);
            success = success && GetAttributeAsInteger("NpcID2", false, "0", 0, int.MaxValue, out mobID2);
            success = success && GetAttributeAsInteger("NpcID3", false, "0", 0, int.MaxValue, out mobID3);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
            success = success && GetXYZAttributeAsWoWPoint("X", "Y", "Z", true, new WoWPoint(0, 0, 0), out location);

            MobId = mobID;
            MobId2 = mobID2;
            MobId3 = mobID3;
            QuestId = (uint)questId;
            Location = location;

            Counter = 0;
            TimesUsedCounter = 1;
            MovedToLocation = false;

        }

        public WoWPoint Location { get; private set; }
        public int Counter { get; set; }
        public int TimesUsedCounter { get; set; }
        public int MobId { get; set; }
        public int MobId2 { get; set; }
        public int MobId3 { get; set; }
        public bool MovedToLocation { get; set; }
        public int MoveToNPC { get; set; }
        public uint QuestId { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        #region Overrides of CustomForcedBehavior

        public List<WoWUnit> mobList
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>()
                                    .Where(u => (u.Entry == MobId || u.Entry == MobId2 || u.Entry == MobId3) && !u.Dead)
                                    .OrderBy(u => u.Distance).ToList();
            }
        }

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

            if (quest != null)
                TreeRoot.GoalText = "KillUntilComplete - " + quest.Name;
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

                           new Decorator(ret => mobList.Count == 0,
                                new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Moving To Location - X: " + Location.X + " Y: " + Location.Y),
                                        new Action(ret => Navigator.MoveTo(Location)),
                                        new Action(ret => Thread.Sleep(300))
                                    )
                                ),

                           new Decorator(ret => mobList.Count > 0 && !me.IsCasting,
                                new Sequence(
                                    new DecoratorContinue(ret => mobList[0].Location.Distance(me.Location) > range,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Moving to Mob - " + mobList[0].Name + " Yards Away " + mobList[0].Location.Distance(me.Location)),
                                            new Action(ret => Navigator.MoveTo(mobList[0].Location)),
                                            new Action(ret => Thread.Sleep(300))
                                            )
                                    ),
                                    new DecoratorContinue(ret => mobList[0].Location.Distance(me.Location) <= range,
                                        new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Attacking Mob - " + mobList[0].Name + " With Spell: " + RangeSpell.Name),
                                        new Action(ret => WoWMovement.MoveStop()),
                                        new Action(ret => mobList[0].Target()),
                                        new Action(ret => mobList[0].Face()),
                                        new Action(ret => Thread.Sleep(200)),
                                        new Action(ret => SpellManager.Cast(RangeSpell)),
                                        new Action(ret => Thread.Sleep(300))
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

