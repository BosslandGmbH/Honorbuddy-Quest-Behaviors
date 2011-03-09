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
    public class Escort : CustomForcedBehavior
    {

        /// <summary>
        /// Escort by Natfoth
        /// Allows you to follow and/or defend an NPC until the quest is completed
        /// ##Syntax##
        /// QuestId: Required, it is what the bot uses to see if you are done.
        /// NpcId: Id of the Mob to interact with.
        /// X,Y,Z: The general location where theese objects can be found
        /// </summary>
        /// 

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"NpcId",null},
            {"MobId",null},
            {"QuestId",null},
            {"X",null},
            {"Y",null},
            {"Z",null},

        };

        bool success = true;


        public Escort(Dictionary<string, string> args)
            : base(args)
        {

            CheckForUnrecognizedAttributes(recognizedAttributes);

            int mobID = 0;
            int questId = 0;
            WoWPoint location = new WoWPoint(0, 0, 0);

            success = success && GetAttributeAsInteger("NpcId", false, "1", 0, int.MaxValue, out mobID);
            success = success && GetAttributeAsInteger("QuestId", true, "0", 0, int.MaxValue, out questId);
            success = success && GetXYZAttributeAsWoWPoint("X", "Y", "Z", true, new WoWPoint(0, 0, 0), out location);

            if (mobID == 1)
                success = success && GetAttributeAsInteger("MobId", false, "1", 0, int.MaxValue, out mobID);

            QuestId = (uint)questId;
            MobId = mobID;
            Counter = 1;
            MovedToTarget = false;
            Location = location;
        }

        public WoWPoint Location { get; private set; }
        public int Counter { get; set; }
        public int MobId { get; set; }
        public bool MovedToTarget;
        public uint QuestId { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        public List<WoWUnit> mobList
        {
            get
            {
               return ObjectManager.GetObjectsOfType<WoWUnit>()
                                   .Where(u => u.Entry == MobId && !u.Dead)
                                   .OrderBy(u => u.Distance).ToList();
            }
        }

        /// <summary>
        /// A Queue for npc's we need to talk to
        /// </summary>
        //private WoWUnit CurrentUnit { get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(unit => unit.Distance < 100 && unit.Entry == MobId); } }

        #region Overrides of CustomForcedBehavior

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

            if (quest != null)
                TreeRoot.GoalText = "Escort - " + quest.Name;
        }

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                            new Decorator(ret => me.QuestLog.GetQuestById(QuestId) != null && me.QuestLog.GetQuestById(QuestId).IsCompleted,
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

                           new Decorator(ret => mobList.Count > 0 && !me.Combat && !mobList[0].Combat,
                                new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Following Mob - " + mobList[0].Name + " At X: " + mobList[0].X + " Y: " + mobList[0].Y + " Z: " + mobList[0].Z),
                                            new Action(ret => Navigator.MoveTo(mobList[0].Location)),
                                            new Action(ret => Thread.Sleep(100))
                                       )
                                ),

                           new Decorator(ret => mobList.Count > 0 && (me.Combat || mobList[0].Combat),
                                new Sequence(
                                    new DecoratorContinue(ret => mobList[0].CurrentTarget.Location.Distance(me.Location) > 3,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Combat Detected, Moving to Attacking Target - " + mobList[0].CurrentTarget.Name + " Yards Away: " + mobList[0].CurrentTarget.Location.Distance(me.Location)),
                                            new Action(ret => Navigator.MoveTo(mobList[0].Location)),
                                            new Action(ret => Thread.Sleep(100))
                                            )
                                    ),
                                    new DecoratorContinue(ret => mobList[0].CurrentTarget.Location.Distance(me.Location) <= 3,
                                        new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Combat Detected, Attacking Target - " + mobList[0].CurrentTarget.Name + " Yards Away: " + mobList[0].CurrentTarget.Location.Distance(me.Location)),
                                        new Action(ret => SpellManager.Cast(RangeSpell)),
                                        new Action(ret => Thread.Sleep(300))
                                            ))
                                    ))

                        )
                    );
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
