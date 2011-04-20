// Behavior originally contributed by Unknown.
//
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_ForcedMount
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Styx.Combat.CombatRoutine;
using Styx.Logic;
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
    public class ForcedMount : CustomForcedBehavior
    {
        public enum ForcedMountType
        {
            Ground,
            Flying,
            Water,
        }


        public ForcedMount(Dictionary<string, string> args)
            : base(args)
        {
			try
			{
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                MountType   = GetAttributeAsEnum<ForcedMountType>("MountType", false, null)
                                ?? ((Me.IsSwimming && (MountHelper.UnderwaterMounts.Count > 0)) ? ForcedMountType.Water
                                    : (CanFly()) ? ForcedMountType.Flying
                                    : ForcedMountType.Ground);
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
        public ForcedMountType          MountType { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }

        // Private variables for internal state
        private bool                _isBehaviorDone;
        private Composite           _root;

        // Private properties
        private LocalPlayer         Me { get { return (ObjectManager.Me); } }


        private void    MountForFlying()
        {
            if (StyxWoW.Me.Class == WoWClass.Druid && (SpellManager.HasSpell("Flight Form") || SpellManager.HasSpell("Swift Flight Form")))
            {
                if (SpellManager.CanCast("Swift Flight Form"))
                    { SpellManager.Cast("Swift Flight Form"); }

                else if (SpellManager.CanCast("Flight Form"))
                    { SpellManager.Cast("Flight Form"); }
            }

            else
            {
                MountHelper.FlyingMounts.First().CreatureSpell.Cast();
                while (StyxWoW.Me.IsCasting)
                    { Thread.Sleep(100); }
            }

            // Hop off the ground. Kthx
            Navigator.PlayerMover.Move(WoWMovement.MovementDirection.JumpAscend);
            Thread.Sleep(250);
            Navigator.PlayerMover.MoveStop();
        }


        public bool     CanFly()
        {
            bool    flyingCapable   = ((MountHelper.FlyingMounts.Count > 0)
                                        ||
                                        ((StyxWoW.Me.Class == WoWClass.Druid)
                                         && (SpellManager.HasSpell(33943 /*Flight Form*/)
                                             || SpellManager.HasSpell(40120 /*Swift Flight Form*/)))
                                      );
            bool    flyingPermitted = true;


            // Location-based permission...
            if ((Me.MapId == 0 /*EasternKingdoms*/) || (Me.MapId == 1 /*Kalmdor*/) || (Me.MapId ==  646 /*Deepholm*/))
                { flyingPermitted = SpellManager.HasSpell(90267 /*Flight Master's License*/); }

            else if (Me.MapId == 571 /*Northrend*/)
                { flyingPermitted = SpellManager.HasSpell(54197 /*Cold Weather Flying*/); }


            // Special circumstance permission...
            if ((Me.Combat || !Me.IsAlive)
                ||
                (Me.IsInInstance || Me.IsIndoors || Battlegrounds.IsInsideBattleground))
            {
                flyingPermitted = false;
            }

            return (flyingCapable && flyingPermitted);
        }


        private Composite CreateActualBehavior()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => MountType == ForcedMountType.Ground,
                    new Action(ret => Mount.MountUp())),

                new Decorator(
                    ret => MountType == ForcedMountType.Water && MountHelper.UnderwaterMounts.Count != 0 && StyxWoW.Me.IsSwimming,
                    new Action(ret => MountHelper.UnderwaterMounts.First().CreatureSpell.Cast())),

                new Decorator(
                    ret => MountType == ForcedMountType.Flying && CanFly(),
                    new Action(ret => MountForFlying()))
                );
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            if (_root == null)
            {
                _root = new Sequence(
                    CreateActualBehavior(),
                    new Action(ret => _isBehaviorDone = true));
            }
            return _root;
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
                TreeRoot.GoalText = "Mounting for " + MountType.ToString() + " travel";
			}
		}

        #endregion
    }
}
