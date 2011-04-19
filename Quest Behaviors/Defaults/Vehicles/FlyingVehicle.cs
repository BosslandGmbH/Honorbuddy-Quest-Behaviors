using System;
using System.Collections.Generic;
using System.Linq;
using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using System.Diagnostics;
using Styx.Logic.Combat;
using System.Globalization;

using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// FlyingVehicle by HighVoltz
    /// Moves to along a path in a vehicle using the specific actionbar butons until quest is complete
    /// ##Syntax##
    /// VehicleId: ID of the vehicle
    /// Buttons: A series of numbers that represent the buttons to press in order of importance, separated by comma, for example Buttons ="2,1"     
    /// NpcList: a comma separated list of Npcs IDs to kill for this quest. example: NpcList ="2323,4231,4324"
    /// ItemId:(Optional) Id of item that summons Vehicle
    /// (Optional) HealButton: the button number that's used to heal: 1-20
    /// (Optional) HealPercent: Vehicle Health percent at which to wait to use heal. Default:35
    /// Path formats are x,y,z|x,y,z. example: Path = "2331.773,-5752.029,153.9199|2310.267,-5742.212,161.2074"
    /// StartPath: The Path to follow at the start. This leads to the quest area.
    /// Path: The Path to follow while completing the quests objectives, This Path should loop..
    /// EndPath:  The Path to follow when quest completes. This leads to the quest turnin NPC
    /// PickUpPassengerButton: (optional) this is button used to pickup NPCs durring search and rescue operations
    /// DropPassengerButton: (optional) this is button used to drop NPCs durring search and rescue operations
    /// SpeedButton: (optional) this button presses a speed boost ability if specified
    /// NpcScanRange: (optional) Maximum range from player to scan for NPCs
    /// Precision: (optional) This behavior moves on to the next waypoint when at Precision distance or less to current waypoint. Default 4;
    /// </summary>
    public class FlyingVehicle : CustomForcedBehavior
    {
        public FlyingVehicle(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                Buttons         = GetAttributeAsIntegerArray("Buttons", false, 1, 12, null) ?? new int[0];
                DropPassengerButton     = GetAttributeAsHotbarButton("DropPassengerButton", false, null) ?? 0;
                EndPath         = GetAttributeAsWoWPoints("EndPath", true, null) ?? new WoWPoint[0];
                HealButton      = GetAttributeAsHotbarButton("HealButton", false, null) ?? 0;
                HealPercent     = GetAttributeAsInteger("HealPercent", false, 0, 99, null) ?? 35;
                ItemId          = GetAttributeAsItemId("ItemId", false, null) ?? 0;
                NpcList         = GetAttributeAsIntegerArray("NpcList", true, 1, int.MaxValue, null) ?? new int[0];
                NpcScanRange    = GetAttributeAsRange("NpcScanRange", false, null) ?? 10000;
                Path            = GetAttributeAsWoWPoints("Path", true, null) ?? new WoWPoint[0];
                PickUpPassengerButton   = GetAttributeAsHotbarButton("PickUpPassengerButton", false, null) ?? 0;
                Precision       = GetAttributeAsInteger("Precision", false, 2, 100, null) ?? 4;
                QuestId         = GetAttributeAsQuestId("QuestId", true, null) ?? 0;
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;
                SpeedButton     = GetAttributeAsHotbarButton("SpeedButton", false, null) ?? 0;
                StartPath       = GetAttributeAsWoWPoints("StartPath", true, null) ?? new WoWPoint[0];
                VehicleId       = GetAttributeAsMobId("VehicleId", true, null) ?? 0;
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

 
        enum State { liftoff, start, looping, finshed, landing, remount }

        public int[]                    Buttons { get; private set; }
        public int                      DropPassengerButton { get; private set; }
        public WoWPoint[]               EndPath { get; private set; }
        public int                      HealPercent { get; private set; }
        public int                      HealButton { get; private set; }
        public int                      ItemId { get; private set; }
        public int[]                    NpcList { get; private set; }
        public int                      NpcScanRange { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }
        public WoWPoint[]               Path { get; private set; }
        public int                      PickUpPassengerButton { get; private set; }
        public int                      Precision { get; private set; }
        public int                      SpeedButton { get; private set; }
        public WoWPoint[]               StartPath { get; private set; }
        public int                      VehicleId { get; private set; }

        private bool                            _casting = false;
        private WoWMovement.MovementDirection   _direction;
        private bool                            _doingUnstuck;
        private Stopwatch                       _flightStopwatch = new Stopwatch(); // after like 15 minutes the dragon auto dies, so we need to resummon before this
        private bool                            _isBehaviorDone = false;
        private WoWPoint                        _lastPoint = WoWPoint.Empty;
        private Stopwatch                       _liftoffStopwatch = new Stopwatch();
        private LocalPlayer                     Me = ObjectManager.Me;
        private int                             _pathIndex = 0;
        private System.Random                   _rand = new System.Random();
        private Composite                       _root;
        private State                           _state = State.liftoff;
        private Stopwatch                       _stuckTimer = new Stopwatch();


        bool InVehicle
        {
            get { return Lua.GetReturnVal<int>("if IsPossessBarVisible() or UnitInVehicle('player') then return 1 else return 0 end", 0) == 1; }
        }


        public WoWUnit GetVehicle()
        {
            return ObjectManager.Me.Minions.Where(o => o.Entry == VehicleId).
                OrderBy(o => o.Distance).FirstOrDefault();
        }

        WoWUnit GetNpc()
        {
            WoWUnit veh = GetVehicle();
            return ObjectManager.GetObjectsOfType<WoWUnit>().OrderBy(u => veh.Location.Distance2D(u.Location)).
                FirstOrDefault(u => u.Location.Distance2D(veh.Location) <= NpcScanRange && !u.Dead && NpcList.Contains((int)u.Entry));
        }

        bool TargetIsInVehicle
        {
            get
            {
                //return me.CurrentTarget.Location.Distance2D(new WoWPoint(0, 0, 0)) < 10;// relative location
                return Lua.GetReturnVal<int>("if UnitInVehicle('target') == 1 then return 1 else return 0 end", 0) == 1;
            }
        }


        bool StuckCheck()
        {
            if (!_stuckTimer.IsRunning || _stuckTimer.ElapsedMilliseconds >= 3000)
            {
                _stuckTimer.Reset();
                _stuckTimer.Start();

                WoWUnit veh = GetVehicle();
                if (veh.Location.Distance(_lastPoint)<=5 || _doingUnstuck)
                {
                    if (!_doingUnstuck)
                    {
                        UtilLogMessage("info", "Stuck... Doing unstuck routine");
                        _direction = WoWMovement.MovementDirection.JumpAscend |
                            (_rand.Next(0, 2) == 1 ? WoWMovement.MovementDirection.StrafeRight : WoWMovement.MovementDirection.StrafeLeft)
                            | WoWMovement.MovementDirection.Backwards;
                        WoWMovement.Move(_direction);
                        _doingUnstuck = true;
                        return true;
                    }
                    else
                    {
                        _doingUnstuck = false;
                        WoWMovement.MoveStop(_direction);
                    }
                }
                _lastPoint = veh.Location;
            }
            return false;
        }


        WoWPoint MoveToLoc
        {
            get
            {
                WoWUnit veh = GetVehicle();
                if (veh == null && _state != State.liftoff && _state != State.landing)
                    { UtilLogMessage("fatal", "Something went seriously wrong..."); }

                switch (_state)
                {
                    case State.start:
                        if (_pathIndex < StartPath.Length)
                        {
                            if (veh.Location.Distance(StartPath[_pathIndex]) <= Precision)
                                _pathIndex++;
                            if (_pathIndex < StartPath.Length)
                                return StartPath[_pathIndex];
                        }
                        return WoWPoint.Zero;
                    case State.looping:
                        if (_pathIndex < Path.Length)
                        {
                            if (veh.Location.Distance(Path[_pathIndex]) <= Precision)
                            {
                                _pathIndex++;
                            }
                            if (_pathIndex >= Path.Length)
                                _pathIndex = 0;
                            return Path[_pathIndex];
                        }
                        return WoWPoint.Zero;
                    case State.finshed:
                        if (_pathIndex < EndPath.Length)
                        {
                            if (veh.Location.Distance(EndPath[_pathIndex]) <= Precision)
                                _pathIndex++;
                            if (_pathIndex < EndPath.Length)
                                return EndPath[_pathIndex];
                        }
                        return WoWPoint.Zero;
                }
                return WoWPoint.Zero;
            }
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ??
                (_root = new PrioritySelector(
                    new Action(c =>
                    { // looping since HB becomes unresposive for periods of time if I don't, bugged
                        while (true)
                        {
                            ObjectManager.Update();
                            WoWUnit npc = GetNpc();
                            WoWUnit vehicle = GetVehicle();
                            var quest = Me.QuestLog.GetQuestById((uint)QuestId);
                            WoWPoint waypoint = MoveToLoc;
                            if (_state != State.finshed && _state != State.landing && (quest.IsCompleted || _flightStopwatch.ElapsedMilliseconds >= 780000))
                            {
                                _state = State.finshed;
                                TreeRoot.StatusText = quest.IsCompleted ? "Turning Quest in!" : "Moving to landing spot and resummoning";
                                _pathIndex = 0;
                            }
                            switch (_state)
                            {
                                case State.liftoff:
                                    bool inVehicle = InVehicle;

                                    if (!_liftoffStopwatch.IsRunning || !inVehicle)
                                    {
                                        if (ItemId > 0 && !inVehicle && !Me.IsFalling && vehicle == null)
                                        {
                                            if (!Me.IsCasting)
                                                Lua.DoString("UseItemByName({0})", ItemId);
                                            return RunStatus.Running;
                                        }
                                        else if (inVehicle)
                                        {
                                            _liftoffStopwatch.Reset();
                                            _liftoffStopwatch.Start();
                                            TreeRoot.StatusText = "Liftoff!";
                                            WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
                                        }
                                    }
                                    else if (_liftoffStopwatch.ElapsedMilliseconds > 2000)
                                    {
                                        TreeRoot.StatusText = "Moving to quest Area";
                                        WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
                                        _state = !quest.IsCompleted ? State.start : State.landing;
                                        _flightStopwatch.Reset();
                                        _flightStopwatch.Start();
                                        _liftoffStopwatch.Stop();
                                        _liftoffStopwatch.Reset();
                                    }
                                    break;
                                case State.start:
                                case State.finshed:
                                    if (waypoint == WoWPoint.Zero)
                                    {
                                        _state = _state == State.start ? State.looping : State.landing;
                                        _pathIndex = 0;
                                    }
                                    else
                                    {
                                        using (new FrameLock())
                                        {
                                            Lua.DoString("if GetPetActionCooldown({0}) == 0 then CastPetAction({0}) end ", SpeedButton);
                                            WoWMovement.ClickToMove(waypoint);
                                        }
                                    }
                                    break;
                                case State.looping:
                                    if (StuckCheck())
                                        break;
                                    if (PickUpPassengerButton == 0)
                                    {
                                        TreeRoot.StatusText = string.Format("Blowing stuff up. {0} mins before resummon is required", ((780000 -_flightStopwatch.ElapsedMilliseconds) / 1000) / 60);
                                        using (new FrameLock())
                                        {
                                            if ((vehicle.HealthPercent <= HealPercent || vehicle.ManaPercent <= HealPercent) && HealButton > 0
                                                && npc != null && npc.Location.Distance2D(vehicle.Location) < 60)
                                            {
                                                TreeRoot.StatusText = string.Format("Using heal button {0} on NPC:{1}, {2} Units away",
                                                    HealButton,npc.Name,vehicle.Location.Distance2D(npc.Location));
                                                Lua.DoString("if GetPetActionCooldown({0}) == 0 then CastPetAction({0}) end ", HealButton);
                                            }
                                            foreach (int b in Buttons)
                                            {
                                                //  Lua.DoString("local a=VehicleAimGetNormAngle() if a < 0.55 then local _,s,_ = GetActionInfo({0}) local c = GetSpellCooldown(s) if c == 0 then CastSpellByID(s) end end", b);
                                                Lua.DoString("local a=VehicleAimGetNormAngle() if a < 0.55 then if GetPetActionCooldown({0}) == 0 then CastPetAction({0}) end  end", b);
                                            }
                                            Lua.DoString("if GetPetActionCooldown({0}) == 0 then CastPetAction({0}) end ", SpeedButton);
                                            WoWMovement.ClickToMove(waypoint);
                                        }
                                    }
                                    else
                                    {
                                        TreeRoot.StatusText = string.Format("Rescuing NPCs ");
                                        if (npc != null)
                                        {
                                            WoWPoint clickLocation = npc.Location.RayCast(npc.Rotation,6);
                                            clickLocation.Z += 3;
                                            if (!Me.GotTarget || Me.CurrentTarget != npc)
                                                npc.Target();
                                            if (TargetIsInVehicle || quest.IsCompleted)
                                            {
                                                _state = State.finshed;
                                                TreeRoot.StatusText = quest.IsCompleted ? "Turning Quest in!" : "Returning to base";
                                                _pathIndex = 0;
                                                break;
                                            }
                                            else
                                            {
                                                if (npc.Distance > 25)
                                                    _casting = false;
                                                if (vehicle.Location.Distance(clickLocation) > 5 && !_casting)
                                                {
                                                    WoWMovement.ClickToMove(clickLocation);
                                                }
                                                else
                                                {
                                                    WoWMovement.MoveStop();
                                                    Lua.DoString("if GetPetActionCooldown({0}) == 0 then CastPetAction({0}) end ", PickUpPassengerButton);
                                                    _casting = true;
                                                }
                                                break;
                                            }
                                        }
                                        using (new FrameLock())
                                        {
                                            Lua.DoString("if GetPetActionCooldown({0}) == 0 then CastPetAction({0}) end ", SpeedButton);
                                            WoWMovement.ClickToMove(waypoint);
                                        }
                                    }
                                    break;
                                case State.landing:
                                    if (PickUpPassengerButton == 0 || quest.IsCompleted)
                                    {
                                        Lua.DoString("VehicleExit()");
                                        if ((Me.Combat || !quest.IsCompleted) && ItemId > 0)
                                        {
                                            _state = State.liftoff;
                                            TreeRoot.StatusText = "Remounting to drop combat";
                                        }
                                        else
                                            _isBehaviorDone = true;
                                    }
                                    else
                                    {
                                        WoWMovement.MoveStop();
                                        Styx.StyxWoW.SleepForLagDuration();
                                        Lua.DoString("if GetPetActionCooldown({0}) == 0 then CastPetAction({0}) end ", DropPassengerButton);
                                        _state = State.start;
                                        _pathIndex = 0;
                                    }
                                    return RunStatus.Success;
                            }
                            System.Threading.Thread.Sleep(100);
                        }
                    })
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
