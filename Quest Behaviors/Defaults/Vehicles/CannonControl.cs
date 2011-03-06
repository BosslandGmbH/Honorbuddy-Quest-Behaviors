using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using System.Diagnostics;
using System;

using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// CannonControl by HighVoltz
    /// Shoots a Cannon
    /// ##Syntax##
    /// VehicleId: ID of the vehicle
    /// QuestId: Id of the quest to perform this behavior on
    /// MaxAngle: Maximum Angle to aim, use /dump VehicleAimGetNormAngle() in game to get the angle
    /// MinAngle: Minimum Angle to aim, use /dump VehicleAimGetNormAngle() in game to get the angle
    /// Buttons:A series of numbers that represent the buttons to press in order of importance, separated by comma, for example Buttons ="2,1" 
    /// ExitButton: (Optional)Button to press to exit the cannon. 1-12
    /// </summary>
    public class CannonControl : CustomForcedBehavior
    {
        readonly Dictionary<string, object> _recognizedAttributes = new Dictionary<string, object>
        {
            {"VehicleId",null},
            {"QuestId",null},
            {"MaxAngle",null},
            {"MinAngle",null},
            {"ExitButton",null},
            {"Buttons",null},
        };

        readonly bool _success = true;

        public CannonControl(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(_recognizedAttributes);
            int vehicleId = 0;
            int questId = 0;
            string buttons = "";
            int exit = 0;
            float maxAngle = 0;
            float minAngle = 0;

            _success = _success && GetAttributeAsInteger("VehicleId", true, "0", 0, int.MaxValue, out vehicleId);
            _success = _success && GetAttributeAsInteger("QuestId", true, "0", 0, int.MaxValue, out questId);
            _success = _success && GetAttributeAsFloat("MaxAngle", true, "0", 0, float.MaxValue, out maxAngle);
            _success = _success && GetAttributeAsFloat("MinAngle", true, "0", 0, float.MaxValue, out minAngle);
            _success = _success && GetAttributeAsString("Buttons", true, "1", out buttons);
            _success = _success && GetAttributeAsInteger("ExitButton", true, "1", 0, int.MaxValue, out exit);
            if (!_success)
                Err("Check Tags for errors\nStopping HB");

            VehicleId = vehicleId;
            QuestId = questId;
            MinAngle = minAngle;
            MaxAngle = maxAngle;
            ExitButton = exit + 120;

            string[] _buttons = buttons.Split(',');
            try
            {
                Buttons = new List<int>(_buttons.Length);
                int val;
                for (int i = 0; i < _buttons.Length; i++)
                {
                    int.TryParse(_buttons[i], out val);
                    Buttons.Add(val + 120);
                }
            }
            catch (Exception ex) { Err(ex.ToString()); }
        }

        public int VehicleId { get; private set; }
        public int QuestId { get; private set; }
        public float MinAngle { get; private set; }
        public float MaxAngle { get; private set; }
        public List<int> SpellIds { get; private set; }
        public List<int> Buttons { get; private set; }
        public int ExitButton { get; private set; }

        public WoWObject Vehicle
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWObject>(true).Where(o => o.Entry == VehicleId).
                    OrderBy(o => o.Distance).FirstOrDefault();
            }
        }

        #region Overrides of CustomForcedBehavior
        private Composite _root;

        private static bool IsInVehicle
        {
            get { return Lua.GetReturnVal<bool>("return UnitInVehicle('player')", 0); }
        }

        readonly Stopwatch _throttle = new Stopwatch();
        private bool _aimed;

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = 
                new PrioritySelector(
                    new Decorator(c => Vehicle == null,
                            new Action(c => Err("No cannons found\n Stoping HB"))
                        ),

                    new Decorator(c => Vehicle != null && !IsInVehicle,
                        new Action(c =>
                        {
                            if (!Vehicle.WithinInteractRange)
                            {
                                Navigator.MoveTo(Vehicle.Location);
                                Log("Moving to Cannon");
                            }
                            else
                                Vehicle.Interact();
                        })
                    ),
                    new Decorator(c => IsInVehicle && Vehicle != null,
                        new Action(c =>
                        {
                            // looping since current versions of HB seem to be unresponsive for periods of time
                            while (true)
                            {
                                var quest = ObjectManager.Me.QuestLog.GetQuestById((uint)QuestId);
                                if (quest.IsCompleted)
                                {
                                    if (ExitButton > 0)
                                        Lua.DoString("local _,s,_ = GetActionInfo({0}) CastSpellByID(s)", ExitButton);
                                    else
                                        Lua.DoString("VehicleExit()");
                                    _isDone = true;
                                    return RunStatus.Success;
                                }
                                
                                if (!_aimed)
                                {
                                    Lua.DoString("VehicleAimRequestNormAngle({0})", MinAngle);
                                    _aimed = true;
                                }
                                else
                                {
                                    Lua.DoString("VehicleAimRequestNormAngle({0})", MaxAngle);
                                    _aimed = false;
                                }
                                foreach (int s in Buttons)
                                {
                                    Lua.DoString("local _,s,_ = GetActionInfo({0}) CastSpellByID(s) ", s);
                                }
                                   
                                Thread.Sleep(100);
                                
                                _throttle.Reset();
                                _throttle.Start();
                            }
                        }))
                ));
        }

        private static void Err(string format, params object[] args)
        {
            Logging.Write(System.Drawing.Color.Red, "CannonControl: " + format, args);
            TreeRoot.Stop();
        }

        private static void Log(string format, params object[] args)
        {
            Logging.Write("CannonControl: " + format, args);
        }

        private bool _isDone;
        public override bool IsDone { get { return _isDone; } }

        public override void OnStart()
        {
            TreeRoot.GoalText = string.Format("Using big cannon {0} to kill loads of baddies", VehicleId);
        }

        #endregion
    }
}
