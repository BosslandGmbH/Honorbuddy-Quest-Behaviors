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

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class ForcedMount : CustomForcedBehavior
    {
        private enum ForcedMountType
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
                ForcedMountType     mountType;
                int                 questId;

                CheckForUnrecognizedAttributes(new Dictionary<string, object>()
                                                {
                                                    {"MountType",null},
                                                    {"QuestId",null},
                                                });

                _isAttributesOkay = true;
                _isAttributesOkay &= GetAttributeAsEnum<ForcedMountType>("MountType", true, ForcedMountType.Flying, out mountType);
                _isAttributesOkay &= GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);

                if (_isAttributesOkay)
                {
                    this.MountType = mountType;
                    this.QuestId = (uint)questId;
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

        private ForcedMountType MountType { get; set; }
        private uint            QuestId { get; set; }

        private bool        _isAttributesOkay;
        private bool        _isBehaviorDone;
        private Composite   _root;


        private void MountForFlying()
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
                    ret =>
                    MountType == ForcedMountType.Flying && (MountHelper.FlyingMounts.Count != 0 ||
                    (StyxWoW.Me.Class == WoWClass.Druid && (SpellManager.HasSpell("Flight Form") || SpellManager.HasSpell("Swift Flight Form")))),
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
		}

        #endregion
    }
}
