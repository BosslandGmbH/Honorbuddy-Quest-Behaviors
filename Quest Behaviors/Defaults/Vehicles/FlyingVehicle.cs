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
            int health = 0;
            string rawStartPath = "";
            string rawEndPath = "";
            string rawPath = "";
            string rawButtons = "";
            string rawNpcList = "";

            success = success && GetAttributeAsInteger("VehicleId", true, "0", 0, int.MaxValue, out vehicleId);
            success = success && GetAttributeAsInteger("ItemId", false, "0", 0, int.MaxValue, out itemId);
            success = success && GetAttributeAsInteger("QuestId", true, "0", 0, int.MaxValue, out questId);
            success = success && GetAttributeAsInteger("HealPercent", false, "35", 0, int.MaxValue, out health);
            success = success && GetAttributeAsInteger("HealButton", false, "0", 0, int.MaxValue, out healbutton);
            success = success && GetAttributeAsInteger("Precision", false, "4", 2, int.MaxValue, out precision);
            success = success && GetAttributeAsString("NpcList", true, "", out rawNpcList);
            success = success && GetAttributeAsString("StartPath", true, "", out rawStartPath);
            success = success && GetAttributeAsString("Path", true, "", out rawPath);
            success = success && GetAttributeAsString("EndPath", true, "", out rawEndPath);
            success = success && GetAttributeAsString("Buttons", true, "", out rawButtons);

            StartPath = ParseWoWPointListString(rawStartPath);
            Path = ParseWoWPointListString(rawPath);
            EndPath = ParseWoWPointListString(rawEndPath);
            Buttons = ParseIntString(rawButtons);
            NpcList = ParseIntString(rawNpcList);

            for (int i=0;i < Buttons.Length;i++)
                Buttons[i] += + 120; // Possess/vehicle buttons start at 120

            if (!success || StartPath == null || Path == null || EndPath == null || Buttons == null)
                Err("There was an error parsing the profile\nStoping HB");
            VehicleId = vehicleId;
            ItemId = itemId;
            Precision = precision;
            QuestId = questId;
            HealPercent = health;
            HealthButton = healbutton > 0 ? healbutton + 120 : 0;
        }

        bool InVehicle
        {
            get { return Lua.GetReturnVal<bool>("return UnitInVehicle('player')", 0); }
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
        public int HealthButton { get; private set; }
        public int QuestId { get; private set; }
        public int[] NpcList { get; private set; }
        public WoWPoint[] StartPath { get; private set; }
        public WoWPoint[] Path { get; private set; }
        public WoWPoint[] EndPath { get; private set; }
        public int[] Buttons { get; private set; }
        Stopwatch liftoffSw = new Stopwatch();
        LocalPlayer me = ObjectManager.Me;
        int pathIndex = 0;
        bool refuel = false; // after like 15 minutes the dragon auto dies, so we need to resummon before this
        Stopwatch flightSW = new Stopwatch();

        #region Overrides of CustomForcedBehavior
        private Composite root;
        protected override Composite CreateBehavior()
        {
            return root ??
                (root = new PrioritySelector(
                    new Action(c =>
                    {
                        while (true)
                        {
                            var quest = me.QuestLog.GetQuestById((uint)QuestId);
                            WoWPoint waypoint = MoveToLoc;
                            switch (state)
                            {
                                case State.liftoff:
                                    if (!liftoffSw.IsRunning)
                                    {
                                        if (ItemId > 0 && !InVehicle && !me.IsFalling && Vehicle == null)
                                        {
                                            if (!me.IsCasting)
                                                Lua.DoString("UseItemByName({0})", ItemId);
                                            return RunStatus.Running;
                                        }
                                        liftoffSw.Start();
                                        TreeRoot.StatusText = "Liftoff!";
                                        WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
                                    }
                                    else if (liftoffSw.ElapsedMilliseconds > 2000)
                                    {
                                        TreeRoot.StatusText = "Moving to quest Area";
                                        WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
                                        state = !quest.IsCompleted ? State.start : State.landing;
                                        refuel = false;
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
                                        WoWMovement.ClickToMove(waypoint);
                                    break;
                                case State.looping:
                                    if (flightSW.ElapsedMilliseconds >= 780000)
                                    {
                                        state = State.finshed;
                                        refuel = true;
                                        TreeRoot.GoalText = "Moving to landing spot and resummoning";
                                    }
                                    if (quest.IsCompleted)
                                    {
                                        state = State.finshed;
                                        TreeRoot.StatusText = "Turning Quest in!";
                                        pathIndex = 0;
                                    }
                                    else
                                    {
                                        using (new FrameLock())
                                        {
                                            TreeRoot.StatusText = string.Format("Blowing stuff up while moving to {0}", waypoint);
                                            if ((Vehicle.HealthPercent <= HealPercent || Vehicle.ManaPercent <= HealPercent) && HealthButton > 0 
                                                && Enemy != null && Enemy.Location.Distance2D(Vehicle.Location) < 60)
                                                Lua.DoString("local _,s,_ = GetActionInfo({0}) local c = GetSpellCooldown(s) if c == 0 then CastSpellByID(s) end ", HealthButton);
                                            foreach (int b in Buttons)
                                            {
                                              //  Lua.DoString("local a=VehicleAimGetNormAngle() if a < 0.55 then local _,s,_ = GetActionInfo({0}) local c = GetSpellCooldown(s) if c == 0 then CastSpellByID(s) end end", b);
                                                Lua.DoString("local a=VehicleAimGetNormAngle() if a < 0.55 then local _,s,_ = GetActionInfo({0}) CastSpellByID(s) end", b);
                                            }
                                            WoWMovement.ClickToMove(waypoint);
                                        }
                                    }
                                    break;
                                case State.landing:
                                    Lua.DoString("VehicleExit()");
                                    if ((me.Combat || refuel) && ItemId > 0 )
                                    {
                                        state = State.liftoff;
                                        TreeRoot.StatusText = "Remounting to drop combat";
                                    }
                                    else
                                        isDone = true;
                                    return RunStatus.Success;
                            }
                            System.Threading.Thread.Sleep(100);
                        }
                        return RunStatus.Running;
                    })
                ));
        }

        public WoWUnit Vehicle
        {
            get
            {
                return ObjectManager.Me.Minions.Where(o => o.Entry == VehicleId).
                    OrderBy(o => o.Distance).FirstOrDefault();
            }
        }

        WoWUnit Enemy
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().OrderBy(u => u.Distance).
                    FirstOrDefault(u => !u.Dead && NpcList.Contains((int)u.Entry));
            }
        }

        WoWPoint MoveToLoc
        {
            get
            {
                WoWUnit veh = Vehicle;
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
