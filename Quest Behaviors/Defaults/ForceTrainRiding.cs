using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Styx.Database;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.Logic.BehaviorTree;

using TreeSharp;
using Action = TreeSharp.Action;
using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.Inventory.Frames.Trainer;


namespace Styx.Bot.Quest_Behaviors
{
    public class ForceTrainRiding : CustomForcedBehavior
    {
        /// <summary>
        /// ForceTrainRiding by Natfoth
        /// Allows you to Interact with Mobs that are Nearby.
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// NpcId: Id of the Mob to interact with.
        /// X,Y,Z: The general location where theese objects can be found
        /// </summary>
        public ForceTrainRiding(Dictionary<string, string> args)
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
                                                    { "NpcID",      null },
                                                    { "QuestId",    null },
                                                    { "X",          null },
                                                    { "Y",          null },
                                                    { "Z",          null },
                                                });

                _isAttributesOkay = true;
                _isAttributesOkay &= GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
                _isAttributesOkay &= GetXYZAttributeAsWoWPoint(false, new WoWPoint(0, 0, 0), out location);

                // "NpcID" is allowed for legacy purposes --
                // If it was not supplied, then its new name "NpcId" is required.
                _isAttributesOkay &= GetAttributeAsInteger("NpcID", false, "0", 0, int.MaxValue, out mobId);
                if (mobId == 0)
                    _isAttributesOkay &= GetAttributeAsInteger("NpcId", true, "0", 0, int.MaxValue, out mobId);


                if (_isAttributesOkay)
                {
                    Counter = 0;
                    Location = location;
                    MobId = mobId;
                    QuestId = (uint)questId;
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
        public WoWPoint     MovePoint { get; private set; }
        public uint         QuestId { get; set; }

        private bool        _isAttributesOkay;
        private bool        _isBehaviorDone;
        private Composite   _root;


        private List<WoWUnit>   mobList
        {
            get
            {
                    return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                         .Where(u => u.Entry == MobId && !u.Dead)
                                         .OrderBy(u => u.Distance).ToList());
            }
        }


        private NpcResult   ridingTrainer
        {
            get
            {
                return (NpcQueries.GetNpcById((uint)MobId));
            }
        }


        #region Overrides of CustomForcedBehavior.

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

                            new Decorator(ret => mobList.Count > 0 && !mobList[0].WithinInteractRange,
                                new Action(ret => Navigator.MoveTo(mobList[0].Location))),

                            new Decorator(ret => mobList.Count > 0 && mobList[0].WithinInteractRange,
                                new Sequence(
                                    new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                        new Action(ret =>
                                        {
                                            WoWMovement.MoveStop();
                                            StyxWoW.SleepForLagDuration();
                                        })),
                                    new Action(ret => TreeRoot.StatusText = "Opening Trainer - " + mobList[0].Name + " X: " + mobList[0].X + " Y: " + mobList[0].Y + " Z: " + mobList[0].Z),
                                    new Action(ret => mobList[0].Interact()),
                                    new WaitContinue(5, 
                                        ret => TrainerFrame.Instance.IsVisible,
                                        new Action(ret => TrainerFrame.Instance.BuyAll())),
                                    new Action(ret => TrainerFrame.Instance.Close()),
                                    new Action(ret => Counter++)
                                    )
                            ),

                            new Decorator(ret => ridingTrainer != null,
                                new Action(ret => Navigator.MoveTo(ridingTrainer.Location))
                                ),

                            new Action(ret => Counter++)
                    )));
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

                if (quest != null)
                    { TreeRoot.GoalText = string.Format("{0} - \"{1}\"", this.GetType().Name, quest.Name); }

                else
                    { TreeRoot.GoalText = string.Format("{0}: Running", this.GetType().Name); }
            }
        }

        #endregion
    }
}

