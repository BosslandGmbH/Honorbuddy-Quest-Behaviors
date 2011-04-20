// Behavior originally contributed by Natfoth.
//
// DOCUMENTATION:
//     
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Styx.Combat.CombatRoutine;
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
    public class WaterBehavior : CustomForcedBehavior
    {
        /// <summary>
        /// Kill/Collect things within the Water
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// NpcID: MobId of the vehicle before it is mounted.
        /// ObjectID, ObjectID2, ObjectID3: Mob of the actual Vehicle, sometimes it will be the some but sometimes it will not be.
        /// NumberOfTimes: Button bar Number starting from 1
        /// X,Y,Z: Where you want to be at when you fire.
        /// </summary>
        /// 
        public WaterBehavior(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                Location    = GetXYZAttributeAsWoWPoint("", true, null) ?? WoWPoint.Empty;
                MobId       = GetAttributeAsMobId("MobId", false, new [] { "NpcId", "NpcID" }) ?? 0;
                ObjectId    = GetAttributeAsMobId("ObjectId", false, new [] { "ObjectID" }) ?? 0;
                ObjectId2   = GetAttributeAsMobId("ObjectId2", false, new [] { "ObjectID2" }) ?? 0;
                ObjectId3   = GetAttributeAsMobId("ObjectId3", false, new [] { "ObjectID3" }) ?? 0;
                QuestId     = GetAttributeAsQuestId("QuestId", false, null) ?? 0;
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;
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
        public WoWPoint                 Location { get; private set; }
        public int                      MobId { get; private set; }
        public int                      ObjectId { get; private set; }
        public int                      ObjectId2 { get; private set; }
        public int                      ObjectId3 { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }

        // Private variables for internal state
        private bool                    _isBehaviorDone;
        private Composite               _root;

        // Private properties
        private int                     Counter { get; set; }
        private LocalPlayer             Me { get { return (ObjectManager.Me); } }


        private List<WoWUnit> NpcList
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>()
                                            .Where(u => u.Entry == MobId && !u.Dead)
                                            .OrderBy(u => u.Distance).ToList();
            }
        }

        private List<WoWUnit> LootList
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>()
                                            .Where(u => u.Entry == MobId && u.Dead && u.Lootable)
                                            .OrderBy(u => u.Distance).ToList();
            }
        }

        public List<WoWGameObject> ObjectList
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWGameObject>()
                                        .Where(u => (u.Entry == ObjectId || u.Entry == ObjectId2 || u.Entry == ObjectId3) && !u.InUse && !u.IsDisabled)
                                        .OrderBy(u => u.Distance).ToList();
            }
        }

        WoWSpell RangeSpell
        {
            get
            {
                switch (Me.Class)
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
               return (Me.Class == WoWClass.Druid && 
                   (SpellManager.HasSpell("balanceSpell") || SpellManager.HasSpell("RestoSpell"))||
                   Me.Class == WoWClass.Shaman && 
                   (SpellManager.HasSpell("ElementalSpell") || SpellManager.HasSpell("RestoSpell"))||
                   Me.Class == WoWClass.Hunter || Me.Class == WoWClass.Mage || Me.Class == WoWClass.Priest ||
                   Me.Class == WoWClass.Warlock);
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


        #region Overrides of CustomForcedBehavior.

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                            new Decorator(ret => (Me.QuestLog.GetQuestById((uint)QuestId) != null && Me.QuestLog.GetQuestById((uint)QuestId).IsCompleted),
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new WaitContinue(120,
                                        new Action(delegate
                                        {
                                            _isBehaviorDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )),

                            new Decorator(ret => Me.GetMirrorTimerInfo(MirrorTimerType.Breath).CurrentTime < 20000 && Me.GetMirrorTimerInfo(MirrorTimerType.Breath).CurrentTime != 0,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new Action(ret => WoWMovement.ClickToMove(Location)),
                                    new Action(ret => Thread.Sleep(100))
                                )),

                           new Decorator(ret => LootList.Count > 0,
                                new Sequence(
                                    new DecoratorContinue(ret => !LootList[0].WithinInteractRange,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Moving to Loot - " + LootList[0].Name + " Yards Away " + LootList[0].Location.Distance(Me.Location)),
                                            new Action(ret => WoWMovement.ClickToMove(LootList[0].Location)),
                                            new Action(ret => Thread.Sleep(300))
                                            )
                                    ),
                                    new DecoratorContinue(ret => LootList[0].WithinInteractRange,
                                        new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Looting - " + LootList[0].Name),
                                        new Action(ret => WoWMovement.MoveStop()),
                                        new Action(ret => LootList[0].Interact()),
                                        new Action(ret => Thread.Sleep(1000))
                                            ))
                                    )),

                           new Decorator(ret => NpcList.Count == 0 || ObjectList.Count == 0,
                                new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Moving To Location - X: " + Location.X + " Y: " + Location.Y),
                                        new Action(ret => Navigator.MoveTo(Location)),
                                        new Action(ret => Thread.Sleep(100))
                                    )
                                ),

                           new Decorator(ret => NpcList.Count > 0,
                                new Sequence(
                                    new DecoratorContinue(ret => NpcList[0].Location.Distance(Me.Location) > range,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Moving to Mob - " + NpcList[0].Name + " Yards Away " + NpcList[0].Location.Distance(Me.Location)),
                                            new Action(ret => WoWMovement.ClickToMove(NpcList[0].Location)),
                                            new Action(ret => Thread.Sleep(300))
                                            )
                                    ),
                                    new DecoratorContinue(ret => NpcList[0].Location.Distance(Me.Location) <= range,
                                        new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Attacking Mob - " + NpcList[0].Name + " With Spell: " + RangeSpell.Name),
                                        new Action(ret => WoWMovement.MoveStop()),
                                        new Action(ret => NpcList[0].Target()),
                                        new Action(ret => NpcList[0].Face()),
                                        new Action(ret => Thread.Sleep(200)),
                                        new Action(ret => SpellManager.Cast(RangeSpell)),
                                        new Action(ret => Thread.Sleep(300))
                                            ))
                                    )),

                            new Decorator(ret => ObjectList.Count > 0,
                                new Sequence(
                                    new DecoratorContinue(ret => !ObjectList[0].WithinInteractRange,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Moving to Object - " + ObjectList[0].Name + " Yards Away " + ObjectList[0].Location.Distance(Me.Location)),
                                            new Action(ret => WoWMovement.ClickToMove(ObjectList[0].Location)),
                                            new Action(ret => Thread.Sleep(300))
                                            )
                                    ),
                                    new DecoratorContinue(ret => ObjectList[0].WithinInteractRange,
                                        new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Opening Object - " + ObjectList[0].Name),
                                        new Action(ret => WoWMovement.MoveStop()),
                                        new Action(ret => ObjectList[0].Interact()),
                                        new Action(ret => Thread.Sleep(1000)),
                                        new Action(ret => Counter++)
                                            ))
                                    ))
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

