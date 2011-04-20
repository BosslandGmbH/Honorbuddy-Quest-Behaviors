// Behavior originally contributed by Natfoth.
//
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_VehicleBehavior
//
using System;
using System.Collections.Generic;
using System.Linq;
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
    public class VehicleBehavior : CustomForcedBehavior
    {
        /// <summary>
        /// Will control a vehicle and fire on locations/Mobs
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// NpcMountID: MobId of the vehicle before it is mounted.
        /// VehicleID: Mob of the actual Vehicle, sometimes it will be the some but sometimes it will not be.
        /// SpellIndex: Button bar Number starting from 1
        /// FireHeight: Between 0 - 99 The lower the number the closer to the ground it will be
        /// FireTillFinish: This is used for a few quests that the mob is flying but respawns fast, So the bot can fire in the same spot over and over.
        /// FireLocation Coords: Where you want to be at when you fire.
        /// TargetLocation Coords: Where you want to aim.
        /// PreviousFireLocation Coords: This should only be used if you are already inside of the vehicle when you call the behaviors again, and
        ///                                 should be the same coords as FireLocation on the call before it, Check the Wiki for more info or examples.
        /// </summary>
        /// 
        public VehicleBehavior(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                AttackButton    = GetAttributeAsHotbarButton("AttackButton", true, new [] { "SpellIndex" }) ?? 0;
                FirePoint       = GetXYZAttributeAsWoWPoint("FireLocation", true, null) ?? WoWPoint.Empty;
                FireHeight      = GetAttributeAsInteger("FireHeight", true, 1, 999, null) ?? 1;
                FireUntilFinished = GetAttributeAsBoolean("FireUntilFinished", false, new [] { "FireTillFinish" }) ?? false;
                PreviousLocation    = GetXYZAttributeAsWoWPoint("PreviousFireLocation", false, null);
                QuestId         = GetAttributeAsQuestId("QuestId", false, null) ?? 0;
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;
                TargetPoint     = GetXYZAttributeAsWoWPoint("TargetLocation", true, null) ?? WoWPoint.Empty;
                VehicleId       = GetAttributeAsMobId("VehicleId", true, new [] { "VehicleID" }) ?? 0;
                VehicleMountId      = GetAttributeAsMobId("VehicleMountId", true, new [] { "NpcMountId", "NpcMountID" }) ?? 0;
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
        public int                      AttackButton { get; set; }
        public int                      FireHeight { get; private set; }
        public WoWPoint                 FirePoint { get; private set; }
        public bool                     FireUntilFinished { get; set; }
        public WoWPoint?                PreviousLocation { get; private set; }
        public int                      QuestId  { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }
        public WoWPoint                 TargetPoint { get; private set; }
        public int                      VehicleId { get; set; }
        public int                      VehicleMountId { get; private set; }

        // Private variables for internal state
        private bool                _isBehaviorDone;
        private int                 _pathIndex;
        private Composite           _root;

        // Private properties
        private int                 Counter { get; set; }
        private bool                InVehicle { get { return Lua.GetReturnVal<bool>("return  UnitUsingVehicle(\"player\")", 0); } }
        private LocalPlayer         Me { get { return (ObjectManager.Me); } }
        private List<WoWUnit>       NpcVehicleList { get { return ObjectManager.GetObjectsOfType<WoWUnit>()
                                                                                .Where(ret => (ret.Entry == VehicleMountId) && !ret.Dead)
                                                                                .OrderBy(u => u.Distance)
                                                                                .ToList(); }}
        private WoWPoint[]          Path { get; set; }
        private WoWUnit             Vehicle;
        private List<WoWUnit>       VehicleList { get { if (PreviousLocation.HasValue)
                                                        {
                                                            return ObjectManager.GetObjectsOfType<WoWUnit>()
                                                                                .Where(ret => (ret.Entry == VehicleId) && !ret.Dead)
                                                                                .OrderBy(u => u.Location.Distance(PreviousLocation.Value))
                                                                                .ToList();
                                                        }
                                                        else
                                                        {
                                                            return ObjectManager.GetObjectsOfType<WoWUnit>()
                                                                                .Where(ret => (ret.Entry == VehicleId) && !ret.Dead)
                                                                                .OrderBy(u => u.Distance)
                                                                                .ToList();
                                                        }}}


        WoWPoint moveToLocation
        {
            get
            {

                Path = Navigator.GeneratePath(Vehicle.Location, FirePoint);
                _pathIndex = 0;

                while (Path[_pathIndex].Distance(Vehicle.Location) <= 3 && _pathIndex < Path.Length - 1)
                    _pathIndex++;
                return Path[_pathIndex];

            }
        }

            
        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                           new Decorator(ret => (Counter > 0 && !FireUntilFinished) || (Me.QuestLog.GetQuestById((uint)QuestId) != null && Me.QuestLog.GetQuestById((uint)QuestId).IsCompleted),
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new WaitContinue(120,
                                        new Action(delegate
                                        {
                                            _isBehaviorDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )),

                           new Decorator(c => !InVehicle,
                            new Action(c =>
                            {
                                if (!NpcVehicleList[0].WithinInteractRange)
                                {
                                    Navigator.MoveTo(NpcVehicleList[0].Location);
                                    TreeRoot.StatusText = "Moving To Vehicle - " + NpcVehicleList[0].Name + " Yards Away: " + NpcVehicleList[0].Location.Distance(Me.Location);
                                }
                                else
                                {
                                    NpcVehicleList[0].Interact();
                                    PreviousLocation = Me.Location;
                                    
                                }

                            })
                        ),
                        new Decorator(c => InVehicle,
                            new Action(c =>
                            {
                                if (Vehicle == null)
                                {
                                    Vehicle = VehicleList[0];
                                }

                                if (Vehicle.Location.Distance(FirePoint) <= 5)
                                {
                                    TreeRoot.StatusText = "Firing Vehicle - " + Vehicle.Name + " Using Spell Index: " + AttackButton + " Height: " + FireHeight;
                                    WoWMovement.ClickToMove(TargetPoint);
                                    Thread.Sleep(500);
                                    WoWMovement.MoveStop();

                                    using (new FrameLock())
                                    {
                                        Lua.DoString("VehicleAimRequestNormAngle(0.{0})", FireHeight);
                                        Lua.DoString("CastPetAction({0})", AttackButton);
                                        Counter++;
                                        return RunStatus.Success;
                                    }
                                }
                                else if (Vehicle.Location.Distance(FirePoint) > 5)
                                {
                                    TreeRoot.StatusText = "Moving To FireLocation - Yards Away: " + FirePoint.Distance(Vehicle.Location);
                                    WoWMovement.ClickToMove(moveToLocation);
                                    Vehicle.Target();
                                }
                                return RunStatus.Running;
                            }))
                                   
                    ));
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
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = this.GetType().Name + ": " + ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
            }
        }

        #endregion
    }
}
