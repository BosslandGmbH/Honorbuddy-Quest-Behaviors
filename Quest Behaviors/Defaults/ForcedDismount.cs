using System;
using System.Collections.Generic;
using System.Threading;

using Styx.Logic;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class ForcedDismount : CustomForcedBehavior
    {
        private enum ForcedDismountType
        {
            Any,
            Ground,
            Flying,
            Water
        }

        /// <summary>
        /// ForcedDismount by Bobby53
        /// 
        /// forces character to dismount.  additionally forces Druids
        /// to leave Flight Form and Swift Flight Form. if in flight,
        /// will descend straight down before dismount        
        /// 
        /// ##Syntax##
        /// [Optional] QuestId: The id of the quest (defaults to 0)
        /// [Optional] QuestName:  documentation only
        /// [Optional] MountType:  ignored currently
        /// </summary>
        /// 
        public ForcedDismount(Dictionary<string, string> args)
            : base(args)
        {
			try
			{
                int                 questId;
                ForcedDismountType  typeMount;

                CheckForUnrecognizedAttributes(new Dictionary<string, object>()
                                                {
                                                    { "QuestId",    null },     // optional quest id (defaults to 0)
                                                    { "QuestName",  null },     // (doc only - not used)
                                                    { "MountType",  null },     // ignored currently
                                                });

                _isAttributesOkay = true;
                _isAttributesOkay &= GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
                _isAttributesOkay &= GetAttributeAsEnum<ForcedDismountType>("MountType", false, ForcedDismountType.Any, out typeMount);

                if (_isAttributesOkay)
                {
                    this.QuestId = (uint)questId;
                    this.MountType = typeMount;
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


        public LocalPlayer          Me { get { return ObjectManager.Me; } }
        private ForcedDismountType  MountType { get; set; }
        private uint                QuestId { get; set; }

        private bool        _isAttributesOkay;
        private bool        _isBehaviorDone;
        private Composite   _root;


        private void Dismount()
        {
            // if in the air, 
            if (StyxWoW.Me.IsFlying)
            {
                UtilLogMessage("info", "Descending before dismount");
                Navigator.PlayerMover.Move(WoWMovement.MovementDirection.Descend);
                while (StyxWoW.Me.IsFlying)
                    { Thread.Sleep(250); }

                Navigator.PlayerMover.MoveStop();
            }

            if (StyxWoW.Me.Auras.ContainsKey("Flight Form"))
            {
                UtilLogMessage("info", "Cancelling Flight Form");
                CancelAura("Flight Form");
            }

            else if (StyxWoW.Me.Auras.ContainsKey("Swift Flight Form"))
            {
                UtilLogMessage("info", "Cancelling Swift Flight Form");
                CancelAura("Swift Flight Form");
            }

            else
            {
                UtilLogMessage("info", "Dismounting");
                Mount.Dismount();
            }
        }


        private void CancelAura(string sAura)
        {
            Lua.DoString(string.Format("RunMacroText(\"/cancelaura {0}\")", sAura), 0);
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            if (_root == null)
            {
                _root = new PrioritySelector(
                    new Decorator(
                        ret => !Me.Mounted,
                        new Action(ret => _isBehaviorDone = true)),
                    new Decorator(
                        ret => Me.Mounted,
                        new Action(ret => Dismount()))
                );
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
