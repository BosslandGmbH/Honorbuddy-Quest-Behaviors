using System;
using System.Collections.Generic;
using System.Threading;

using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class NoCombatMoveTo : CustomForcedBehavior
    {

        /// <summary>
        /// NoCombatMoveTo by Natfoth
        /// Allows you to move to a specific target with engaging in Combat, to avoid endless combat loops.
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// X,Y,Z: Where you want to go to.
        /// </summary>
        ///
        public NoCombatMoveTo(Dictionary<string, string> args)
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
                _isAttributesOkay &= GetXYZAttributeAsWoWPoint(true, WoWPoint.Empty, out location);

                if (_isAttributesOkay)
                {
                    QuestId = (uint)questId;
                    Location = location;

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

        public static LocalPlayer s_me = ObjectManager.Me;


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


                           new Decorator(c => Location.Distance(s_me.Location) > 3,
                            new Action(c =>
                            {
                                if (Location.Distance(s_me.Location) <= 3)
                                {
                                    return RunStatus.Success;
                                }
                                TreeRoot.StatusText = "Moving To Location - X: " + Location.X + " Y: " + Location.Y + " Z: " + Location.Z;

                                WoWPoint[] pathtoDest1 = Styx.Logic.Pathing.Navigator.GeneratePath(s_me.Location, Location);

                                foreach (WoWPoint p in pathtoDest1)
                                {
                                    while (!s_me.Dead && p.Distance(s_me.Location) > 2)
                                    {
                                        Thread.Sleep(100);
                                        WoWMovement.ClickToMove(p);
                                    }
                                }


                                return RunStatus.Running;
                            }))
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

