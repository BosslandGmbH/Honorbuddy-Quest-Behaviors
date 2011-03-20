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
    public class EjectVeh : CustomForcedBehavior
    {
        /// <summary>
        /// EjectVeh by Natfoth
        /// Will Eject from the current vehicle, nothing more and nothing less.
        /// ##Syntax##
        /// Eject: Not required but just incase it messes with the args.
        /// </summary>
        /// 
        public EjectVeh(Dictionary<string, string> args)
            : base(args)
        {
			try
			{
                int itemId = 0;
                int questId = 0;

                CheckForUnrecognizedAttributes(new Dictionary<string, object>()
                                                {
                                                    { "Eject",      null },
                                                    { "QuestId",    null },
                                                });

                _isAttributesOkay = true;
                _isAttributesOkay &= GetAttributeAsInteger("Eject", false, "1", 0, int.MaxValue, out itemId);
                _isAttributesOkay &= GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);

                if (_isAttributesOkay)
                {
                    Counter = 0;
                    QuestId = questId;
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
        public int      QuestId { get; private set; }

        private bool        _isAttributesOkay;
        private bool        _isBehaviorDone;
        private Composite   _root;

        public static LocalPlayer   s_me = ObjectManager.Me;


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                    new Decorator(ret => Counter > 0,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Vehicle eject complete"),
                                    new WaitContinue(120,
                                        new Action(delegate
                                        {
                                            _isBehaviorDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )),

                           new Decorator(ret => Counter == 0,
                                new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Ejecting from vehicle"),
                                        new Action(ret => Lua.DoString("VehicleExit()")),
                                        new Action(ret => Thread.Sleep(300)),
                                        new Action(ret => Counter++)
                                    ))
                    ));
        }


        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone    // normal completion
                        ||  !UtilIsProgressRequirementsMet(QuestId,
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
                TreeRoot.GoalText = "Ejecting from Vehicle";
            }
        }

        #endregion
    }
}

