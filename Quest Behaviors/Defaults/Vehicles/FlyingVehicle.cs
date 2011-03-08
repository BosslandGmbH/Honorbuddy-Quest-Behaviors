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
        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {
            {"VehicleId",null},
            {"NpcList",null},
            {"ItemId",null},
            {"QuestId",null},
            {"Buttons",null},
            {"HealButton",null},
            {"HealPercent",null},
            {"Precision",null},
            {"StartPath",null},
            {"Path",null},
            {"EndPath",null},
            {"PickUpPassengerButton",null},
            {"DropPassengerButton",null},
            {"SpeedButton",null},
            {"NpcScanRange",null}
        };
        bool success = true;
        public FlyingVehicle(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);
            int vehicleId = 0;
            int itemId = 0;
            int precision = 0;
            int questId = 0;
            int healbutton = 0;
            int speedbutton = 0;
            int pickupButton = 0;
            int dropButton = 0;
            int health = 0;
            int npcRange = 0;
            string rawStartPath = "";
            string rawEndPath = "";
            string rawPath = "";
            string rawButtons = "";
            string rawNpcList = "";

            success &= GetAttributeAsInteger("VehicleId", true, "0", 0, int.MaxValue, out vehicleId);
            success &= GetAttributeAsInteger("ItemId", false, "0", 0, int.MaxValue, out itemId);
            success &= GetAttributeAsInteger("QuestId", true, "0", 0, int.MaxValue, out questId);
            success &= GetAttributeAsInteger("HealPercent", false, "35", 0, int.MaxValue, out health);
            success &= GetAttributeAsInteger("HealButton", false, "0", 0, int.MaxValue, out healbutton);
            success &= GetAttributeAsInteger("PickUpPassengerButton", false, "0", 0, int.MaxValue, out pickupButton);
            success &= GetAttributeAsInteger("DropPassengerButton", false, "0", 0, int.MaxValue, out dropButton);
            success &= GetAttributeAsInteger("SpeedButton", false, "0", 0, int.MaxValue, out speedbutton);
            success &= GetAttributeAsInteger("NpcScanRange", false, "10000", 0, int.MaxValue, out npcRange);
            success &= GetAttributeAsInteger("Precision", false, "4", 2, int.MaxValue, out precision);
            success &= GetAttributeAsString("NpcList", true, "", out rawNpcList);
            success &= GetAttributeAsString("StartPath", true, "", out rawStartPath);
            success &= GetAttributeAsString("Path", true, "", out rawPath);
            success &= GetAttributeAsString("EndPath", true, "", out rawEndPath);
            success &= GetAttributeAsString("Buttons", false, "", out rawButtons);

            StartPath = ParseWoWPointListString(rawStartPath);
            Path = ParseWoWPointListString(rawPath);
            EndPath = ParseWoWPointListString(rawEndPath);
            Buttons = ParseIntString(rawButtons);
            if (!success || StartPath == null || Path == null || EndPath == null || Buttons == null)
                Err("There was an error parsing the profile\nStoping HB");

            NpcList = ParseIntString(rawNpcList);
            VehicleId = vehicleId;
            ItemId = itemId;
            Precision = precision;
            QuestId = questId;
            HealPercent = health;
            HealthButton = healbutton;
            DropPassengerButton = dropButton;
            PickUpPassengerButton = pickupButton;
            NpcScanRange = npcRange;
            SpeedButton = speedbutton;
        }

        bool InVehicle
        {
            get { return Lua.GetReturnVal<int>("if IsPossessBarVisible() or UnitInVehicle('player') then return 1 else return 0 end", 0) == 1; }
        }
        WoWPoint[] ParseWoWPointListString(string points)
        {
            try
            {
                List<WoWPoint> pointList = new List<WoWPoint>();
                string[] buf = points.Split('|');
                float x, y, z;
                foreach (string rawPoint in buf)
                {
                    string[] point = rawPoint.Split(',');
                    float.TryParse(point[0].Trim(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture, out x);
                    float.TryParse(point[1].Trim(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture, out y);
                    float.TryParse(point[2].Trim(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture, out z);
                    pointList.Add(new WoWPoint(x, y, z));
                }
                return pointList.ToArray();
            }
            catch (System.Exception ex) { Err(ex.ToString()); return null; }
        }

        int[] ParseIntString(string buttons)
        {
            try
            {
                List<int> buttonList = new List<int>();
                string[] buf = buttons.Split(',');
                int val = 0;
                foreach (string but in buf)
                {
                    int.TryParse(but.Trim(), out val);
                    buttonList.Add(val); // Possess/vehicle buttons start at 120
                }
                return buttonList.ToArray();
            }
            catch (System.Exception ex) { Err(ex.ToString()); return null; }
        }

        enum State { liftoff, start, looping, finshed, landing, remount }
        State state = State.liftoff;

        public int VehicleId { get; private set; }
        public int ItemId { get; private set; }
        public int Precision { get; private set; }
        public int HealPercent { get; private set; }
        public int DropPassengerButton { get; private set; }
        public int PickUpPassengerButton { get; private set; }
        public int SpeedButton { get; private set; }
        public int NpcScanRange { get; private set; }
        public int HealthButton { get; private set; }
        public int QuestId { get; private set; }
        public int[] NpcList { get; private set; }
        public WoWPoint[] StartPath { get; private set; }
        public WoWPoint[] Path { get; private set; }
        public WoWPoint[] EndPath { get; private set; }
        public int[] Buttons { get; private set; }
        Stopwatch liftoffSw = new Stopwatch();
        LocalPlayer me = ObjectManager.Me;
        System.Random rand = new System.Random();
        int pathIndex = 0;
        // after like 15 minutes the dragon auto dies, so we need to resummon before this
        Stopwatch flightSW = new Stopwatch();

        bool casting = false;
        #region Overrides of CustomForcedBehavior
        private Composite root;
        protected override Composite CreateBehavior()
        {
            return root ??
                (root = new PrioritySelector(
                    new Action(c =>
                    { // looping since HB becomes unresposive for periods of time if I don't, bugged
                        while (true)
                        {
                            ObjectManager.Update();
                            WoWUnit npc = GetNpc();
                            WoWUnit vehicle = GetVehicle();
                            var quest = me.QuestLog.GetQuestById((uint)QuestId);
                            WoWPoint waypoint = MoveToLoc;
                            if (state != State.finshed && state != State.landing && (quest.IsCompleted || flightSW.ElapsedMilliseconds >= 780000))
                            {
                                state = State.finshed;
                                TreeRoot.StatusText = quest.IsCompleted ? "Turning Quest in!" : "Moving to landing spot and resummoning";
                                pathIndex = 0;
                            }
                            switch (state)
                            {
                                case State.liftoff:
                                    bool inVehicle = InVehicle;

                                    if (!liftoffSw.IsRunning || !inVehicle)
                                    {
                                        if (ItemId > 0 && !inVehicle && !me.IsFalling && vehicle == null)
                                        {
                                            if (!me.IsCasting)
                                                Lua.DoString("UseItemByName({0})", ItemId);
                                            return RunStatus.Running;
                                        }
                                        else if (inVehicle)
                                        {
                                            liftoffSw.Reset();
                                            liftoffSw.Start();
                                            TreeRoot.StatusText = "Liftoff!";
                                            WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
                                        }
                                    }
                                    else if (liftoffSw.ElapsedMilliseconds > 2000)
                                    {
                                        TreeRoot.StatusText = "Moving to quest Area";
                                        WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
                                        state = !quest.IsCompleted ? State.start : State.landing;
                                        flightSW.Reset();
                                        flightSW.Start();
                                        liftoffSw.Stop();
                                        liftoffSw.Reset();
                                    }
                                    break;
                                case State.start:
                                case State.finshed:
                                    if (waypoint == WoWPoint.Zero)
                                    {
                                        state = state == State.start ? State.looping : State.landing;
                                        pathIndex = 0;
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
                                        TreeRoot.StatusText = string.Format("Blowing stuff up. {0} mins before resummon is required", ((780000 -flightSW.ElapsedMilliseconds) / 1000) / 60);
                                        using (new FrameLock())
                                        {
                                            if ((vehicle.HealthPercent <= HealPercent || vehicle.ManaPercent <= HealPercent) && HealthButton > 0
                                                && npc != null && npc.Location.Distance2D(vehicle.Location) < 60)
                                            {
                                                TreeRoot.StatusText = string.Format("Using heal button {0} on NPC:{1}, {2} Units away",
                                                    HealthButton,npc.Name,vehicle.Location.Distance2D(npc.Location));
                                                Lua.DoString("if GetPetActionCooldown({0}) == 0 then CastPetAction({0}) end ", HealthButton);
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
                                            if (!me.GotTarget || me.CurrentTarget != npc)
                                                npc.Target();
                                            if (TargetIsInVehicle || quest.IsCompleted)
                                            {
                                                state = State.finshed;
                                                TreeRoot.StatusText = quest.IsCompleted ? "Turning Quest in!" : "Returning to base";
                                                pathIndex = 0;
                                                break;
                                            }
                                            else
                                            {
                                                if (npc.Distance > 25)
                                                    casting = false;
                                                if (vehicle.Location.Distance(clickLocation) > 5 && !casting)
                                                {
                                                    WoWMovement.ClickToMove(clickLocation);
                                                }
                                                else
                                                {
                                                    WoWMovement.MoveStop();
                                                    Lua.DoString("if GetPetActionCooldown({0}) == 0 then CastPetAction({0}) end ", PickUpPassengerButton);
                                                    casting = true;
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
                                        if ((me.Combat || !quest.IsCompleted) && ItemId > 0)
                                        {
                                            state = State.liftoff;
                                            TreeRoot.StatusText = "Remounting to drop combat";
                                        }
                                        else
                                            isDone = true;
                                    }
                                    else
                                    {
                                        WoWMovement.MoveStop();
                                        Styx.StyxWoW.SleepForLagDuration();
                                        Lua.DoString("if GetPetActionCooldown({0}) == 0 then CastPetAction({0}) end ", DropPassengerButton);
                                        state = State.start;
                                        pathIndex = 0;
                                    }
                                    return RunStatus.Success;
                            }
                            System.Threading.Thread.Sleep(100);
                        }
                    })
                ));
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
        Stopwatch stuckTimer = new Stopwatch();
        WoWPoint lastPoint = new WoWPoint();
        bool doingUnstuck =false;
        WoWMovement.MovementDirection dir;
        bool StuckCheck()
        {
            if (!stuckTimer.IsRunning || stuckTimer.ElapsedMilliseconds >= 3000)
            {
                stuckTimer.Reset();
                stuckTimer.Start();

                WoWUnit veh = GetVehicle();
                if (veh.Location.Distance(lastPoint)<=5 || doingUnstuck)
                {
                    if (!doingUnstuck)
                    {
                        Log("Stuck... Doing unstuck routine");
                        dir = WoWMovement.MovementDirection.JumpAscend |
                            (rand.Next(0, 2) == 1 ? WoWMovement.MovementDirection.StrafeRight : WoWMovement.MovementDirection.StrafeLeft)
                            | WoWMovement.MovementDirection.Backwards;
                        WoWMovement.Move(dir);
                        doingUnstuck = true;
                        return true;
                    }
                    else
                    {
                        doingUnstuck = false;
                        WoWMovement.MoveStop(dir);
                    }
                }
                lastPoint = veh.Location;
            }
            return false;
        }
        WoWPoint MoveToLoc
        {
            get
            {
                WoWUnit veh = GetVehicle();
                if (veh == null && state != State.liftoff && state != State.landing)
                    Err("Something went seriously wrong...");
                switch (state)
                {
                    case State.start:
                        if (pathIndex < StartPath.Length)
                        {
                            if (veh.Location.Distance(StartPath[pathIndex]) <= Precision)
                                pathIndex++;
                            if (pathIndex < StartPath.Length)
                                return StartPath[pathIndex];
                        }
                        return WoWPoint.Zero;
                    case State.looping:
                        if (pathIndex < Path.Length)
                        {
                            if (veh.Location.Distance(Path[pathIndex]) <= Precision)
                            {
                                pathIndex++;
                            }
                            if (pathIndex >= Path.Length)
                                pathIndex = 0;
                            return Path[pathIndex];
                        }
                        return WoWPoint.Zero;
                    case State.finshed:
                        if (pathIndex < EndPath.Length)
                        {
                            if (veh.Location.Distance(EndPath[pathIndex]) <= Precision)
                                pathIndex++;
                            if (pathIndex < EndPath.Length)
                                return EndPath[pathIndex];
                        }
                        return WoWPoint.Zero;
                }
                return WoWPoint.Zero;
            }
        }

        void Err(string format, params object[] args)
        {
            Logging.Write(System.Drawing.Color.Red, "FlyingVehicle: " + format, args);
            TreeRoot.Stop();
        }

        void Log(string format, params object[] args)
        {
            Logging.Write("FlyingVehicle: " + format, args);
        }

        private bool isDone = false;

        public override bool IsDone { get { return isDone; } }

        public override void OnStart()
        {
            TreeRoot.GoalText = string.Format("Starting FlyingVehicle Sequence");
        }

        #endregion
    }
}
