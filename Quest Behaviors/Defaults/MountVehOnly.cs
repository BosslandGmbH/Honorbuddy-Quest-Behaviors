using System;
using System.Collections.Generic;
using System.Linq;
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
    public class MountVehOnly : CustomForcedBehavior
    {
        /// <summary>
        /// MountVehOnly by Natfoth
        /// Only use this when you need to mount a Vehicle but it will require nothing else, wow has to auto dismount you at the end or you use EjectVeh.
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// MobMountId: The ID of the Vehicle you want to mount.
        /// X,Y,Z: The general location where these objects can be found
        /// </summary>
        ///
        public MountVehOnly(Dictionary<string, string> args)
            : base(args)
        {
			try
			{
                WoWPoint    location;
                int         mobMountId;
                int         questId;


                CheckForUnrecognizedAttributes(new Dictionary<string, object>()
                                                {
                                                    { "MobMountId", null},
                                                    { "NpcMountId", null},
                                                    { "QuestId",    null},
                                                    { "X",          null},
                                                    { "Y",          null},
                                                    { "Z",          null},
                                                });

                _isAttributesOkay = true;

                _isAttributesOkay &= GetAttributeAsInteger("NpcMountId", false, "0", 0, int.MaxValue, out mobMountId);
                if (mobMountId == 0)
                    { _isAttributesOkay &= GetAttributeAsInteger("MobMountId", true, "0", 0, int.MaxValue, out mobMountId); }

                _isAttributesOkay &= GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
                _isAttributesOkay &= GetXYZAttributeAsWoWPoint(true, WoWPoint.Empty, out location);

                // Semantic coherency --
                if (_isAttributesOkay)
                {
                    if (mobMountId == 0)
                    {
                        UtilLogMessage("error", "\"MobMountId\" may not be zero.");
                        _isAttributesOkay = false;
                    }
                }


                if (_isAttributesOkay)
                {
                    Location = location;
                    MobMountId = mobMountId;
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

        public int          Counter { get; set; }
        public WoWPoint     Location { get; private set; }
        public int          MobMountId { get; set; }
        public uint         QuestId { get; set; }

        private bool        _isAttributesOkay;
        private bool        _isBehaviorDone;
        private Composite   _root;

        private static LocalPlayer s_me = ObjectManager.Me;


        static private bool     InVehicle { get { return Lua.GetReturnVal<bool>("return  UnitUsingVehicle(\"player\")", 0); } }

        private List<WoWUnit>   MobList
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => u.Entry == MobMountId && !u.Dead)
                                     .OrderBy(u => u.Distance).ToList());
            }
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                            new Decorator(ret => Counter > 0,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new WaitContinue(120,
                                        new Action(delegate
                                        {
                                            _isBehaviorDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )),

                           new Decorator(ret => InVehicle,
                                new Sequence(
                                        new Action(ret => Counter++)
                                    )
                                ),

                           new Decorator(ret => MobList.Count == 0,
                                new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Moving To Location - X: " + Location.X + " Y: " + Location.Y),
                                        new Action(ret => Navigator.MoveTo(Location)),
                                        new Action(ret => Thread.Sleep(300))
                                    )
                                ),

                           new Decorator(ret => MobList.Count > 0,
                                new Sequence(
                                    new DecoratorContinue(ret => MobList[0].WithinInteractRange,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Mounting Vehicle - " + MobList[0].Name),
                                            new Action(ret => WoWMovement.MoveStop()),
                                            new Action(ret => MobList[0].Interact())
                                            )
                                    ),
                                    new DecoratorContinue(ret => !MobList[0].WithinInteractRange,
                                        new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Moving To Vehicle - " + MobList[0].Name + " X: " + MobList[0].X + " Y: " + MobList[0].Y + " Z: " + MobList[0].Z + " Yards Away: " + MobList[0].Location.Distance(s_me.Location)),
                                        new Action(ret => Navigator.MoveTo(MobList[0].Location)),
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
                                                  (quest == null) ? "Running" : ("\"" + quest.Name + "\""));
            }
        }

        #endregion
    }
}
