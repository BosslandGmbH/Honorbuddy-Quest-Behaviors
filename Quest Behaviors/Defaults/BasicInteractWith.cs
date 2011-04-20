// Behavior originally contributed by Natfoth.
//
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_BasicInteractWith
//
using System;
using System.Collections.Generic;
using System.Linq;

using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class BasicInteractWith : CustomForcedBehavior
    {
        /// <summary>
        /// Allows you to Interact with Mobs that are Nearby.
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// NpcId: Id of the Mob to interact with.
        /// UseCTM, MoveTo(Optional): Will move to the Npc Location
        /// LUATarget: Should be used for those Mobs that are inside vehicles and return a location of 0,0,0
        /// Faction: The faction the mobs needs to be before interacting
        /// X,Y,Z: The general location where theese objects can be found
        /// </summary>       
        /// 
        public BasicInteractWith(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                FactionId   = GetAttributeAsInteger("FactionId", false, 1, int.MaxValue, new [] { "Faction" }) ?? 0;
                IsMoveToMob = GetAttributeAsBoolean("MoveTo", false, new [] { "UseCTM" }) ?? false;;
                MobId       = GetAttributeAsMobId("MobId", true, new [] { "NpcId", "NpcID" })  ?? 0;
                QuestId     = GetAttributeAsQuestId("QuestId", false, null) ?? 0;
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;
                UseLuaTarget = GetAttributeAsBoolean("UseLuaTarget", false, new [] { "LUATarget" }) ?? false;


                WoWUnit     mob     = ObjectManager.GetObjectsOfType<WoWUnit>()
                                      .Where(unit => unit.Entry == MobId)
                                      .FirstOrDefault();

                MobName     = ((mob != null) && !string.IsNullOrEmpty(mob.Name))
                                ? mob.Name
                                : ("Mob(" + MobId + ")");
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
        public int                      FactionId { get; private set; }
        public bool                     IsMoveToMob { get; private set; }
        public int                      MobId { get; private set; }
        public string                   MobName { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }
        public bool                     UseLuaTarget { get; private set; }

        // Private variables for internal state
        private bool                _isBehaviorDone;
        private Composite           _root;

        // Private properties
        private int                 Counter { get; set; }
        private LocalPlayer         Me { get { return (ObjectManager.Me); } }
        private List<WoWUnit>       MobList { get { if (FactionId > 1)
                                                {
                                                    return ObjectManager.GetObjectsOfType<WoWUnit>()
                                                                    .Where(u => u.Entry == MobId && !u.Dead && u.FactionId == FactionId)
                                                                    .OrderBy(u => u.Distance).ToList(); 
                                                }
                                                else
                                                {
                                                    return ObjectManager.GetObjectsOfType<WoWUnit>()
                                                                            .Where(u => u.Entry == MobId && !u.Dead)
                                                                            .OrderBy(u => u.Distance).ToList();
                                                }
                                            }}


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                    new Decorator(ret => Counter >= 1,
                        new Action(ret => _isBehaviorDone = true)),

                        new PrioritySelector(

                            new Decorator(ret => Counter > 0,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new Action(ret => _isBehaviorDone = true),
                                    new WaitContinue(1,
                                        new Action(delegate
                                        {
                                            _isBehaviorDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )
                                ),

                             new Decorator(ret => MobList.Count > 0 && !MobList[0].WithinInteractRange && IsMoveToMob,
                                new Sequence(
                                    new DecoratorContinue(ret => IsMoveToMob,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Moving To Mob MyCTM - " + MobList[0].Name + " X: " + MobList[0].X + " Y: " + MobList[0].Y + " Z: " + MobList[0].Z),
                                            new Action(ret => WoWMovement.ClickToMove(MobList[0].Location))
                                            )),

                                      new DecoratorContinue(ret => !IsMoveToMob,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Moving To Mob MyCTM - " + MobList[0].Name + " X: " + MobList[0].X + " Y: " + MobList[0].Y + " Z: " + MobList[0].Z),
                                            new Action(ret => Navigator.MoveTo(MobList[0].Location))
                                            ))


                                    )),

                            new Decorator(ret => MobList.Count > 0 && MobList[0].WithinInteractRange,
                                new Sequence(
                                    new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                        new Action(ret =>
                                        {
                                            WoWMovement.MoveStop();
                                            StyxWoW.SleepForLagDuration();
                                        })),
                                        new Action(ret => MobList[0].Interact()),
                                        new Action(ret => Counter++)
                                    )
                            ),

                            new Decorator(ret => (MobList.Count > 0) && !IsMoveToMob && !UseLuaTarget,
                                new Sequence(
                                    new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                        new Action(ret =>
                                        {
                                            WoWMovement.MoveStop();
                                            StyxWoW.SleepForLagDuration();
                                        })),
                                        new Action(ret => MobList[0].Interact()),
                                        new Action(ret => Counter++)
                                    )
                            ),

                            new Decorator(ret => MobList.Count > 0 && UseLuaTarget,
                                new Sequence(
                                    new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                        new Action(ret =>
                                        {
                                            WoWMovement.MoveStop();
                                            StyxWoW.SleepForLagDuration();
                                        })),
                                        new Action(ret => Lua.DoString("TargetNearest()")),
                                        new Action(ret => Me.CurrentTarget.Interact()),
                                        new Action(ret => Counter++)
                                    )
                            )
                    )));
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
                TreeRoot.GoalText = "Interacting with " + MobName;
            }
        }

        #endregion
    }
}

