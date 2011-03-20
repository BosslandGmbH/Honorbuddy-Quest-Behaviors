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
        /// BasicInteractWith by Natfoth
        /// Allows you to Interact with Mobs that are Nearby.
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// NpcId: Id of the Mob to interact with.
        /// UseCTM, MoveTo(Optional): Will move to the Npc Location
        /// LUATarget: Should be used for those Mobs that are inside vehicles and return a location of 0,0,0
        /// Faction: The faction the mobs needs to be before interacting
        /// X,Y,Z: The general location where theese objects can be found
        /// </summary>       

        public BasicInteractWith(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                CheckForUnrecognizedAttributes(new Dictionary<string, object>()
                                                {
                                                    { "Faction",    null },
                                                    { "LUATarget",  null },
                                                    { "MoveTo",     null },
                                                    { "NpcID",      null },
                                                    { "NpcId",      null },
                                                    { "QuestId",    null },
                                                    { "UseCTM",     null },
                                                });

                int mobId;
                int useCTM;
                int luatarget;
                int usefaction;
                int questId;

                _isAttributesOkay &= GetAttributeAsInteger("Faction", false, "0", 0, int.MaxValue, out usefaction);
                _isAttributesOkay &= GetAttributeAsInteger("NpcID", false, "0", 0, int.MaxValue, out mobId);
                _isAttributesOkay &= GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
                _isAttributesOkay &= GetAttributeAsInteger("LUATarget", false, "0", 0, int.MaxValue, out luatarget);
                _isAttributesOkay &= GetAttributeAsInteger("UseCTM", false, "0", 0, int.MaxValue, out useCTM);

                // "NpcID" is allowed for legacy purposes --
                // If it was not supplied, then its new name "NpcId" is required.
                if (mobId == 0)
                    { _isAttributesOkay &= GetAttributeAsInteger("NpcId", true, "0", 0, int.MaxValue, out mobId); }

                // "UseCTM" is allowed for legacy purposes --
                // If it was not supplied, then we need to check its new name "MoveTo", also.
                if (useCTM == 0)
                    {  _isAttributesOkay &= GetAttributeAsInteger("MoveTo", false, "0", 0, int.MaxValue, out useCTM); }


                // Weed out Profile Writer sloppiness --
                if (_isAttributesOkay)
                {
                    if (mobId == 0)
                    {
                        UtilLogMessage("error", "MobId may not be zero");
                        _isAttributesOkay = false;
                    }
                }


                if (_isAttributesOkay)
                {
                    MobId = mobId;
                    LUATarget = luatarget;
                    UseCTM = useCTM;
                    FactionID = usefaction;
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
        public int      FactionID { get; set; }
        public int      LUATarget { get; set; }
        public int      MobId { get; set; }
        public WoWPoint MovePoint { get; private set; }
        public uint     QuestId { get; set; }
        public int      UseCTM { get; set; }

        private bool        _isAttributesOkay = true;
        private bool        _isBehaviorDone;
        private Composite   _root;

        private static LocalPlayer  s_me = ObjectManager.Me;


        public List<WoWUnit> mobList
        {
            get
            {
                if (FactionID > 1)
                {
                    return ObjectManager.GetObjectsOfType<WoWUnit>()
                                    .Where(u => u.Entry == MobId && !u.Dead && u.FactionId == FactionID)
                                    .OrderBy(u => u.Distance).ToList(); 
                }
                else
                {
                    return ObjectManager.GetObjectsOfType<WoWUnit>()
                                            .Where(u => u.Entry == MobId && !u.Dead)
                                            .OrderBy(u => u.Distance).ToList();
                }
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

                             new Decorator(ret => mobList.Count > 0 && !mobList[0].WithinInteractRange && UseCTM > 0,
                                new Sequence(
                                    new DecoratorContinue(ret => UseCTM > 0,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Moving To Mob MyCTM - " + mobList[0].Name + " X: " + mobList[0].X + " Y: " + mobList[0].Y + " Z: " + mobList[0].Z),
                                            new Action(ret => WoWMovement.ClickToMove(mobList[0].Location))
                                            )),

                                      new DecoratorContinue(ret => UseCTM == 0,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Moving To Mob MyCTM - " + mobList[0].Name + " X: " + mobList[0].X + " Y: " + mobList[0].Y + " Z: " + mobList[0].Z),
                                            new Action(ret => Navigator.MoveTo(mobList[0].Location))
                                            ))


                                    )),

                            new Decorator(ret => mobList.Count > 0 && mobList[0].WithinInteractRange,
                                new Sequence(
                                    new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                        new Action(ret =>
                                        {
                                            WoWMovement.MoveStop();
                                            StyxWoW.SleepForLagDuration();
                                        })),
                                        new Action(ret => mobList[0].Interact()),
                                        new Action(ret => Counter++)
                                    )
                            ),

                            new Decorator(ret => mobList.Count > 0 && UseCTM == 0 && LUATarget == 0,
                                new Sequence(
                                    new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                        new Action(ret =>
                                        {
                                            WoWMovement.MoveStop();
                                            StyxWoW.SleepForLagDuration();
                                        })),
                                        new Action(ret => mobList[0].Interact()),
                                        new Action(ret => Counter++)
                                    )
                            ),

                            new Decorator(ret => mobList.Count > 0 && LUATarget > 0,
                                new Sequence(
                                    new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                        new Action(ret =>
                                        {
                                            WoWMovement.MoveStop();
                                            StyxWoW.SleepForLagDuration();
                                        })),
                                        new Action(ret => Lua.DoString("TargetNearest()")),
                                        new Action(ret => s_me.CurrentTarget.Interact()),
                                        new Action(ret => Counter++)
                                    )
                            )
                    )));
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
                    { TreeRoot.GoalText = "BasicInteractWith - " + quest.Name; }
                else
                    { TreeRoot.GoalText = "BasicInteractWith: Running"; }
            }
        }

        #endregion
    }
}

