using System;
using System.Collections.Generic;
using System.Threading;

using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.Logic.BehaviorTree;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class MyCTM : CustomForcedBehavior
    {
        /// <summary>
        /// MyCTM by Natfoth
        /// Allows you to physically click on the screen so that your bot can get around non meshed locations or off objects. *** There is no navigation with this ****
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// X,Y,Z: Where you wish to move.
        /// </summary>
        /// 
        public MyCTM(Dictionary<string, string> args)
            : base(args)
        {
			try
			{
                WoWPoint    location;
                int         questId;

                CheckForUnrecognizedAttributes(new Dictionary<string, object>()
                                                {
                                                    { "QuestId",    null },
                                                    { "X",          null },
                                                    { "Y",          null },
                                                    { "Z",          null },
                                                });



                _isAttributesOkay = true;
                _isAttributesOkay &= GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
                _isAttributesOkay &= GetXYZAttributeAsWoWPoint(true, new WoWPoint(0, 0, 0), out location);

                if (_isAttributesOkay)
                {
                    Location = location;
                    QuestId = (uint)questId;

                    Counter = 0;
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
        public uint     QuestId { get; set; }

        private bool        _isAttributesOkay;
        private bool        _isBehaviorDone;
        private Composite   _root;

        private static LocalPlayer s_me = ObjectManager.Me;


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                 new PrioritySelector(

                            new Decorator(ret => Location.Distance(s_me.Location) <= 3,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new WaitContinue(120,
                                        new Action(delegate
                                        {
                                            _isBehaviorDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )),

                            new Decorator(ret =>Location.Distance(s_me.Location) > 3,
                                new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Moving To Location - X: " + Location.X + " Y: " + Location.Y),
                                        new Action(ret => WoWMovement.ClickToMove(Location)),
                                        new Action(ret => Thread.Sleep(50))
                                    )
                                )
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
                                                  (quest == null) ? "Running" : ("\"" + quest.Name + "\""));
            }
        }

        #endregion
    }
}

