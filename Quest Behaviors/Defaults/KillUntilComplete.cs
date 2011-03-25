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
        public KillUntilComplete(Dictionary<string, string> args)
            : base(args)
        {
			try
			{
                WoWPoint    location;
                int         mobId;
                int         mobId2;
                int         mobId3;
                int         questId;


                CheckForUnrecognizedAttributes(new Dictionary<string, object>()
                                                {
                                                    { "MobId",      null},
                                                    { "MobId2",     null},
                                                    { "MobId3",     null},
                                                    { "NpcID",      null},
                                                    { "NpcID2",     null},
                                                    { "NpcID3",     null},
                                                    { "QuestId",    null},
                                                    { "X",          null},
                                                    { "Y",          null},
                                                    { "Z",          null},
                                                });

                _isAttributesOkay = true;

                _isAttributesOkay &= GetAttributeAsInteger("NpcID", false, "0", 0, int.MaxValue, out mobId);
                if (mobId == 0)
                    { _isAttributesOkay &= GetAttributeAsInteger("MobId", false, "0", 0, int.MaxValue, out mobId); }

                _isAttributesOkay &= GetAttributeAsInteger("NpcID2", false, "0", 0, int.MaxValue, out mobId2);
                if (mobId2 == 0)
                    { _isAttributesOkay &= GetAttributeAsInteger("MobId2", false, "0", 0, int.MaxValue, out mobId2); }

                _isAttributesOkay &= GetAttributeAsInteger("NpcID3", false, "0", 0, int.MaxValue, out mobId3);
                if (mobId3 == 0)
                    { _isAttributesOkay &= GetAttributeAsInteger("MobId3", false, "0", 0, int.MaxValue, out mobId3); }

                _isAttributesOkay &= GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
                _isAttributesOkay &= GetXYZAttributeAsWoWPoint(true, WoWPoint.Empty, out location);

                // Semantic coherency --
                if (_isAttributesOkay)
                {
                    if (Args.ContainsKey("MobId")  &&  Args.ContainsKey("NpcID"))
                    {
                        UtilLogMessage("error", "\"MobId\" and \"NpcID\" attributes are mutually exclusive.   Use \"MobId\" (\"NpcID\" is deprecated).");
                        _isAttributesOkay = false;
                    }

                    if (Args.ContainsKey("MobId2")  &&  Args.ContainsKey("NpcID2"))
                    {
                        UtilLogMessage("error", "\"MobId2\" and \"NpcID2\" attributes are mutually exclusive.   Use \"MobId2\" (\"NpcID2\" is deprecated).");
                        _isAttributesOkay = false;
                    }

                    if (Args.ContainsKey("MobId3")  &&  Args.ContainsKey("NpcID3"))
                    {
                        UtilLogMessage("error", "\"MobId3\" and \"NpcID3\" attributes are mutually exclusive.   Use \"MobId3\" (\"NpcID3\" is deprecated).");
                        _isAttributesOkay = false;
                    }

                    if (mobId == 0)
                    {
                        UtilLogMessage("error", "\"MobId\" may not be zero");
                        _isAttributesOkay = false;
                    }
                }


                if (_isAttributesOkay)
                {
                    Location = location;
                    MobId = mobId;
                    MobId2 = mobId2;
                    MobId3 = mobId3;
                    QuestId = (uint)questId;

                    Counter = 0;
                    MovedToLocation = false;
                    TimesUsedCounter = 1;
                }
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
				_isAttributesOkay = false;
			}
        }


        public int          Counter { get; set; }
        public WoWPoint     Location { get; private set; }
        public int          MobId { get; set; }
        public int          MobId2 { get; set; }
        public int          MobId3 { get; set; }
        public bool         MovedToLocation { get; set; }
        public int          MoveToNPC { get; set; }
        public uint         QuestId { get; set; }
        public int          TimesUsedCounter { get; set; }

        private bool        _isAttributesOkay;
        private bool        _isBehaviorDone;
        private Composite   _root;

        public static LocalPlayer s_me = ObjectManager.Me;


        public List<WoWUnit> MobList
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => (u.Entry == MobId || u.Entry == MobId2 || u.Entry == MobId3) && !u.Dead)
                                     .OrderBy(u => u.Distance).ToList());
            }
        }

        WoWSpell RangeSpell
        {
            get
            {
                switch (s_me.Class)
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

        private bool IsRanged
        {
            get
            {
               return (((s_me.Class == WoWClass.Druid) && 
                        (SpellManager.HasSpell("balanceSpell") || SpellManager.HasSpell("RestoSpell")))
                       ||
                       ((s_me.Class == WoWClass.Shaman) && 
                        (SpellManager.HasSpell("ElementalSpell") || SpellManager.HasSpell("RestoSpell")))
                       ||
                       (s_me.Class == WoWClass.Hunter) ||
                       (s_me.Class == WoWClass.Mage) ||
                       (s_me.Class == WoWClass.Priest) ||
                       (s_me.Class == WoWClass.Warlock));
            }
        }

        private int Range
        {
            get
            {
                return (IsRanged ? 25 : 3);
            }
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                            new Decorator(ret => (s_me.QuestLog.GetQuestById(QuestId) != null && s_me.QuestLog.GetQuestById(QuestId).IsCompleted),
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new WaitContinue(120,
                                        new Action(delegate
                                        {
                                            _isBehaviorDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )),

                           new Decorator(ret => MobList.Count == 0,
                                new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Moving To Location - X: " + Location.X + " Y: " + Location.Y),
                                        new Action(ret => Navigator.MoveTo(Location)),
                                        new Action(ret => Thread.Sleep(300))
                                    )
                                ),

                           new Decorator(ret => MobList.Count > 0 && !s_me.IsCasting,
                                new Sequence(
                                    new DecoratorContinue(ret => MobList[0].Location.Distance(s_me.Location) > Range,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Moving to Mob - " + MobList[0].Name + " Yards Away " + MobList[0].Location.Distance(s_me.Location)),
                                            new Action(ret => Navigator.MoveTo(MobList[0].Location)),
                                            new Action(ret => Thread.Sleep(300))
                                            )
                                    ),
                                    new DecoratorContinue(ret => MobList[0].Location.Distance(s_me.Location) <= Range,
                                        new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Attacking Mob - " + MobList[0].Name + " With Spell: " + RangeSpell.Name),
                                        new Action(ret => WoWMovement.MoveStop()),
                                        new Action(ret => MobList[0].Target()),
                                        new Action(ret => MobList[0].Face()),
                                        new Action(ret => Thread.Sleep(200)),
                                        new Action(ret => SpellManager.Cast(RangeSpell)),
                                        new Action(ret => Thread.Sleep(300))
                                            ))
                                    ))
                    ));
        }


        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone    // normal completion
                        ||  !UtilIsProgressRequirementsMet((int)QuestId, 
                                                           QuestInLogRequirement.InLog, 
                                                           QuestCompleteRequirement.NotComplete));
            }
        }


        public override void OnStart()
        {
			if (!_isAttributesOkay)
			{
				UtilLogMessage("error", "Stopping Honorbuddy.  Please repair the profile!");

                // *Never* want to stop Honorbuddy (e.g., TreeRoot.Stop()) in the constructor --
                // This would defeat the "ProfileDebuggingMode" configurable that builds an instance of each
                // used behavior when the profile is loaded.
				TreeRoot.Stop();
			}

            else if (!IsDone)
            {
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

                TreeRoot.GoalText = string.Format("{0}: {1}",
                                                  this.GetType().Name,
                                                  (quest == null) ?  "Running" :  ("\"" + quest.Name + "\""));
            }
        }

        #endregion
    }
}

