using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

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
        public Escort(Dictionary<string, string> args)
            : base(args)
        {
			try
			{
                WoWPoint    location;
                int         mobId;
                int         questId;

                CheckForUnrecognizedAttributes(new Dictionary<string, object>()
                                                {
                                                    { "NpcId",      null },
                                                    { "MobId",      null },
                                                    { "QuestId",    null },
                                                    { "X",          null },
                                                    { "Y",          null },
                                                    { "Z",          null },
                                                });

                _isAttributesOkay = true;
                _isAttributesOkay &= GetAttributeAsInteger("NpcId", false, "0", 0, int.MaxValue, out mobId);
                _isAttributesOkay &= GetAttributeAsInteger("QuestId", true, "0", 0, int.MaxValue, out questId);
                _isAttributesOkay &= GetXYZAttributeAsWoWPoint(true, WoWPoint.Empty, out location);

                // "NpcId" is allowed for legacy purposes --
                // If it was not supplied, then its new name "NpcId" is required.
                if (mobId == 0)
                    { _isAttributesOkay &= GetAttributeAsInteger("MobId", true, "0", 0, int.MaxValue, out mobId); }

                // Weed out Profile Writer sloppiness --
                if (_isAttributesOkay)
                {
                    if (mobId == 0)
                    {
                        UtilLogMessage("error", "MobId may not be zero");
                        _isAttributesOkay = false;
                    }

                    if (questId == 0)
                    {
                        UtilLogMessage("error", "QuestId may not be zero");
                        _isAttributesOkay = false;
                    }
                }


                if (_isAttributesOkay)
                {
                    QuestId = (uint)questId;
                    MobId = mobId;
                    Counter = 1;
                    MovedToTarget = false;
                    Location = location;
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

        public int      Counter { get; set; }
        public WoWPoint Location { get; private set; }
        public int      MobId { get; set; }
        public bool     MovedToTarget { get; set; }
        public uint     QuestId { get; set; }

        private bool        _isAttributesOkay;
        private bool        _isBehaviorDone;
        private Composite   _root;

        public static LocalPlayer   s_me = ObjectManager.Me;


        public List<WoWUnit> mobList
        {
            get
            {
               return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                    .Where(u => u.Entry == MobId && !u.Dead)
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


        /// <summary>
        /// A Queue for npc's we need to talk to
        /// </summary>
        //private WoWUnit CurrentUnit { get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(unit => unit.Distance < 100 && unit.Entry == MobId); } }

        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                            new Decorator(ret => s_me.QuestLog.GetQuestById(QuestId) != null && s_me.QuestLog.GetQuestById(QuestId).IsCompleted,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new WaitContinue(120,
                                        new Action(delegate
                                        {
                                            _isBehaviorDone = true;
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

                           new Decorator(
                               ret => mobList.Count > 0 && mobList[0].IsHostile,
                               new PrioritySelector(
                                   new Decorator(
                                       ret => s_me.CurrentTarget != mobList[0],
                                       new Action(ret =>
                                           {
                                               mobList[0].Target();
                                               StyxWoW.SleepForLagDuration();
                                           })),
                                   new Decorator(
                                       ret => !s_me.Combat,
                                       new PrioritySelector(
                                            new Decorator(
                                                ret => RoutineManager.Current.PullBehavior != null,
                                                RoutineManager.Current.PullBehavior),
                                            new Action(ret => RoutineManager.Current.Pull()))))),


                           new Decorator(
                               ret => mobList.Count > 0 && (!s_me.Combat || s_me.CurrentTarget == null || s_me.CurrentTarget.Dead) && 
                                      mobList[0].CurrentTarget == null && mobList[0].DistanceSqr > 5f * 5f,
                                new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Following Mob - " + mobList[0].Name + " At X: " + mobList[0].X + " Y: " + mobList[0].Y + " Z: " + mobList[0].Z),
                                            new Action(ret => Navigator.MoveTo(mobList[0].Location)),
                                            new Action(ret => Thread.Sleep(100))
                                       )
                                ),

                           new Decorator(ret => mobList.Count > 0 && (s_me.Combat || mobList[0].Combat),
                                new PrioritySelector(
                                    new Decorator(
                                        ret => s_me.CurrentTarget == null && mobList[0].CurrentTarget != null,
                                        new Sequence(
                                        new Action(ret => mobList[0].CurrentTarget.Target()),
                                        new Action(ret => StyxWoW.SleepForLagDuration()))),
                                    new Decorator(
                                        ret => !s_me.Combat,
                                        new PrioritySelector(
                                            new Decorator(
                                                ret => RoutineManager.Current.PullBehavior != null,
                                                RoutineManager.Current.PullBehavior),
                                            new Action(ret => RoutineManager.Current.Pull())))))

                        )
                    );
        }


        public override bool IsDone
        {
            get { return (_isBehaviorDone); }
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

            else
            {
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

                if (quest != null)
                    TreeRoot.GoalText = string.Format("Escorting for \"{0}\"", quest.Name);
            }
        }


        #endregion
    }
}
