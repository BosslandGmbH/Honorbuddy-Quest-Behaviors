using CommonBehaviors.Actions;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Action = Styx.TreeSharp.Action;

namespace Behaviors
{
    class TheBurlapGrind : CustomForcedBehavior
    {

        public TheBurlapGrind(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                QuestId = GetAttributeAsNullable("QuestId", false, ConstrainAs.QuestId(this), null) ?? 30747;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
                                    + "\nFROM HERE:\n"
                                    + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }

       
        // Attributes provided by caller
        public int CastNum = 5900000;
        public int CastTime = 3000;
        public bool Hop = false;
        public bool IgnoreCombat = false;
        public WoWPoint Location = new WoWPoint(2748.791, 1803.984, 653.4359);
        public List<int> MobIds = new List<int>() { 60749, 60746, 60752, 60753, 60743, }; 
        public WoWPoint[] Path { get; private set; }
        public double Precision = 50;
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }
        public int[] SpellIds = new int[] { 117671, 117677 };
        public bool UseNavigator = true;
        public int[] VehicleIds = new int[] { 60754, 60587 };

        // Private variables for internal state
        private int _castCounter;
        private bool _casted = false;
        private Stopwatch _castStopwatch = new Stopwatch();// cast timer.
        private bool _isBehaviorDone = false;
        private bool _isDisposed;
        private WoWPoint _lastPoint;
        private int _pathIndex;
        private Stopwatch _pauseStopwatch = new Stopwatch();// add a small pause before casting.. 
        private Composite _root;
        private Stopwatch _stuckTimer = new Stopwatch();

        // Private properties
        private static LocalPlayer Me { get { return (StyxWoW.Me); } }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id: VehicleMover.cs 249 2012-09-19 01:31:37Z natfoth $"); } }
        public override string SubversionRevision { get { return ("$Revision: 249 $"); } }


        ~TheBurlapGrind()
        {
            Dispose(false);
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


        bool InVehicle
        {
            get { return Lua.GetReturnVal<int>("return UnitIsControlling('player')", 0) == 1; }
        }


        public WoWUnit Vehicle
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>(true)
                                    .Where(o => VehicleIds.Contains((int)o.Entry))
                                    .OrderBy(o => o.Distance)
                                    .FirstOrDefault();
            }
        }

        public WoWUnit Enemy
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>(true)
                                    .Where(o => MobIds.Contains((int)o.Entry))
                                    .OrderBy(o => o.Distance)
                                    .FirstOrDefault();
            }
        }

        public List<WoWUnit> Enemies
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>(true)
                                    .Where(o => MobIds.Contains((int)o.Entry))
                                    .OrderBy(o => o.Distance).ToList();
            }
        }


        public Composite CreateSpellBehavior
        {
            get
            {
                return new PrioritySelector(
                    new Action(r => 
                        {
                            BotPoi CurrentPoi = BotPoi.Current;
                            if(CurrentPoi == null || MobIds.Contains((int) CurrentPoi.Entry)) 
                            {
                                BotPoi.Current = new BotPoi(Enemy, PoiType.Kill);
                            }
                        }),
                    new Decorator(cond => Vehicle.HealthPercent <= 60, 
                        new Action(r => CastSpell(117671))),
                    new Decorator(cond => Enemies.Where(o => Vehicle.Location.Distance(o.Location) < 15).Count() >= 1,
                        new Action(r => CastSpell(117677)))
                    );
            }
        }

        public void CastSpell(int SpellId)
        {
            Lua.DoString("CastSpellByID({0})", SpellId);
        }


        public WoWPoint moveToLocation
        {
            get
            {
                if (UseNavigator)
                {
                    WoWUnit vehicle = Vehicle;
                    if (Hop && (!_stuckTimer.IsRunning || _stuckTimer.ElapsedMilliseconds > 2000))
                    {
                        _stuckTimer.Reset();
                        if (!_stuckTimer.IsRunning)
                            _stuckTimer.Start();
                        if (_lastPoint.Distance(vehicle.Location) <= 3)
                        {
                            WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend | WoWMovement.MovementDirection.StrafeLeft);
                            WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend | WoWMovement.MovementDirection.StrafeLeft);
                        }
                        _lastPoint = vehicle.Location;
                    }

                    if (vehicle.Location.Distance(Path[_pathIndex]) <= Precision && _pathIndex < Path.Length - 1)
                        _pathIndex++;
                    return Path[_pathIndex];
                }
                else
                    return Location;
            }
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ??
                (_root = new PrioritySelector(
                    new Decorator(c => !StyxWoW.Me.IsAlive, // if we ignore combat and die... 
                        new Action(c =>
                        {
                            return RunStatus.Failure;
                        })),
                    new Decorator(c => Vehicle == null,
                        new Action(c =>
                        {
                            return RunStatus.Failure;
                        })),
                    new Action(c =>
                    {
                        if (!InVehicle)
                        {
                            try
                            {
                                LogMessage("info", "Moving to Vehicle {0}", Vehicle.Name);
                                if (!Vehicle.WithinInteractRange)
                                    Navigator.MoveTo(Vehicle.Location);
                                else
                                {
                                    Vehicle.Interact();
                                    GossipFrame.Instance.SelectGossipOption(0);
                                }

                                if (IgnoreCombat && StyxWoW.Me.IsAlive)
                                    return RunStatus.Running;
                                else
                                    return RunStatus.Success;
                            }
                            catch { }
                        }
                        return RunStatus.Failure;
                    }),
                    new Decorator(c => UseNavigator && Path == null,
                        new Action(c =>
                        {
                            WoWUnit vehicle = Vehicle;
                            Path = Navigator.GeneratePath(vehicle.Location, Location);
                            if (Path == null || Path.Length == 0)
                            { LogMessage("fatal", "Unable to genorate path to {0}", Location); }

                            if (IgnoreCombat)
                                return RunStatus.Failure;
                            return RunStatus.Success;

                        })),
                    new Action(c =>
                    {
                        if (Vehicle.Location.Distance(Location) > Precision && !StyxWoW.Me.IsDead)
                        {
                            WoWMovement.ClickToMove(moveToLocation);
                            if (IgnoreCombat)
                                return RunStatus.Running;
                            else
                                return RunStatus.Success;
                        }
                        return RunStatus.Failure;
                    }),
                    new Decorator(c => Vehicle.Location.Distance(Location) <= Precision,
                        CreateSpellBehavior)
               ));
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

                TreeRoot.StatusText = string.Format("{0}: {1} while in VehicleId({2}) using {3}",
                                                    this.GetType().Name,
                                                    Location, VehicleIds[0], UseNavigator ? "Navigator" : "Click-To-Move");
            }
        }

        #endregion
    }
}
