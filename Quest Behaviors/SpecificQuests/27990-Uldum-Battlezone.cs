// Behavior originally contributed by mastahg
// 24/9/2013 - Practically rewritten, the old behaviour didn't do anything. - Aevitas
//
// DOCUMENTATION:
//     
//

using System;
using System.Collections.Generic;
using System.Linq;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Levelbot.Actions.General;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Vector3 = Tripper.Tools.Math.Vector3;


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.Battlezone
{
    [CustomBehaviorFileName(@"SpecificQuests\27990-Uldum-Battlezone")]
    public class Battlezone : CustomForcedBehavior
    {
        ~Battlezone()
        {
            Dispose(false);
        }

        public Battlezone(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                QuestId = 27990;
                QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = QuestInLogRequirement.InLog;
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                LogMessage("error",
                           "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message + "\nFROM HERE:\n" + except.StackTrace +
                           "\n");
                IsAttributeProblem = true;
            }
        }


        // Attributes provided by caller
        public uint[] MobIds { get; private set; }
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }
        public WoWPoint Location { get; private set; }

        // Private variables for internal state
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private Composite _root;


        // Private properties
        private LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
        }


        public void Dispose(bool isExplicitlyInitiatedDispose)
        {
            if (!_isDisposed)
            {
                // NOTE: we should call any Dispose() method for any managed or unmanaged
                // resource, if that resource provides a Dispose() method.

                // Clean up managed resources, if explicit disposal...
                if (isExplicitlyInitiatedDispose)
                {
                    // empty, for now
                }

                // Clean up unmanaged resources (if any) here...
                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }


        public Composite DoDps
        {
            get
            {
                return
                    new PrioritySelector(
                        new Decorator(ret => RoutineManager.Current.CombatBehavior != null,
                                      RoutineManager.Current.CombatBehavior),
                        new Action(c => RoutineManager.Current.Combat()));
            }
        }

        #region Overrides of CustomForcedBehavior

        public bool IsQuestComplete()
        {
            var quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
            return quest == null || quest.IsCompleted;
        }


        public Composite CreateQuestDoneBehaviour()
        {
            return new Decorator(ret => IsObjectiveComplete(1, (uint)QuestId), new Action(delegate
                                                                                        {
                                                                                            TreeRoot.StatusText =
                                                                                                "Finished!";
                                                                                            _isBehaviorDone = true;
                                                                                            return RunStatus.Success;
                                                                                        }));
        }



        private bool IsObjectiveComplete(int objectiveId, uint questId)
        {
            if (Me.QuestLog.GetQuestById(questId) == null)
            {
                return false;
            }
            int returnVal = Lua.GetReturnVal<int>("return GetQuestLogIndexByID(" + questId + ")", 0);
            return
                Lua.GetReturnVal<bool>(
                    string.Concat(new object[] { "return GetQuestLogLeaderBoard(", objectiveId, ",", returnVal, ")" }), 2);
        }



        public WoWUnit Watcher
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 47385 && u.IsAlive).OrderBy(
                        u => u.Distance).FirstOrDefault();
            }
        }

        public Composite CreateTargetWatcher()
        {
            return new Action(ret =>
            {
                if (Watcher != null)
                    Watcher.Target();

                return RunStatus.Failure;
            });
        }

        private readonly VehicleWeapon _cannon = new VehicleWeapon(1, new WeaponArticulation(-0.456, 0.780) , 65.0);

        public Composite CreateVehicleBehaviour()
        {
            return new Action(ret =>
            {
                var target = Me.CurrentTarget;
                var vehicle = ObjectManager.GetObjectsOfType<WoWUnit>()
                                    .FirstOrDefault(v => v.CreatedByUnitGuid == Me.Guid);

                if (vehicle != null)
                {
                    var flightTime = _cannon.CalculateTimeOfProjectileFlight(target.Location);
                    var probableTargetLocation = target.AnticipatedLocation(flightTime);

                    var isAimed = _cannon.WeaponAim(probableTargetLocation);

                    if (isAimed)
                        _cannon.WeaponFire(probableTargetLocation);
                }
            });
        }

        private WoWPoint _endspot = new WoWPoint(1076.7, 455.7638, -44.20478);
        private WoWPoint _spot = new WoWPoint(1109.848, 462.9017, -45.03053);
       
        public Composite CreateLockTargetBehaviour()
        {
            // We only want to grab a new target when we either don't have one, or it's further away than 100 yards - we're not likely to hit those anyway.
            return new Decorator(r => Me.CurrentTarget == null || Me.CurrentTarget.Distance > 100 || Me.CurrentTarget != Watcher, CreateTargetWatcher());
        }

        public Composite CreateFreeBehaviour()
        {
            return new Action(hue =>
            {
                // Because this bloody thing always seems to think we get stuck inside the tank. We're not. We're free men. Hasta la victoria siempre. Kthx.
                Navigator.NavigationProvider.StuckHandler.Reset();

                //if (Me.Mounted)
                //    Mount.Dismount("Entering vehicle");

                // Fall through pls, thxhtkxkhtbai.
                return RunStatus.Failure;
            });
        }

        protected override Composite CreateBehavior()
        {
            return new PrioritySelector(
                CreateFreeBehaviour(),
                CreateQuestDoneBehaviour(), 
                CreateLockTargetBehaviour(), 
                CreateVehicleBehaviour()
                );
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone // normal completion
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
                if (TreeRoot.Current != null && TreeRoot.Current.Root != null &&
                    TreeRoot.Current.Root.LastStatus != RunStatus.Running)
                {
                    var currentRoot = TreeRoot.Current.Root;
                    if (currentRoot is GroupComposite)
                    {
                        var root = (GroupComposite)currentRoot;
                        root.InsertChild(0, CreateBehavior());
                    }
                }

                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = this.GetType().Name + ": " +
                                    ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
            }
        }

        #endregion
    }
}