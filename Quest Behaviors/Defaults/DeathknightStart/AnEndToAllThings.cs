using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Bots.Grind;
using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Profiles;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors.DeathknightStart
{
    /// <summary>
    /// AnEndToAllThings by Nesox
    /// Moves along a path in a vehicle using spells to inflict damage and to heals itself until the quest is completed.
    /// ##Syntax##
    /// VehicleId: Id of the vehicle
    /// ItemId: Id of the item that summons the vehicle.
    /// AttackSpell: Id of the attackspell, can be enumerated using, 'GetPetActionInfo(index)'
    /// HealSpell: Id of the healspell, can be enumerated using, 'GetPetActionInfo(index)'
    /// NpcIds: a comma separated list with id's of npc's to kill for this quest. example. NpcIds="143,2,643,1337" 
    /// </summary>
    public class AnEndToAllThings : CustomForcedBehavior
    {
        readonly Dictionary<string, object> _recognizedAttributes = new Dictionary<string, object>()
        {
            {"VehicleId",null},
            {"ItemId",null},
            {"AttackSpell",null},
            {"HealSpell",null},
            {"NpcIds",null},
        };

        private readonly bool _success = true;

        public AnEndToAllThings(Dictionary<string, string> args) : base(args)
        {
            CheckForUnrecognizedAttributes(_recognizedAttributes);
            int vehicleId = 0, attackSpell = 0, healSpell = 0, itemId = 0;
            string npcIds = "";

            _success = _success && GetAttributeAsInteger("VehicleId", true, "0", 0, int.MaxValue, out vehicleId);
            _success = _success && GetAttributeAsInteger("ItemId", false, "0", 0, int.MaxValue, out itemId);
            _success = _success && GetAttributeAsInteger("AttackSpell", true, "0", 0, int.MaxValue, out attackSpell);
            _success = _success && GetAttributeAsInteger("HealSpell", false, "0", 0, int.MaxValue, out healSpell);
            _success = _success && GetAttributeAsString("NpcIds", true, "", out npcIds);

            if(!_success)
            {
                Logging.Write(Color.Red, "Error parsing tag for AnEndToAllThings. {0}", Element);
                TreeRoot.Stop();
            }

            VehicleId = vehicleId;
            ItemId = itemId;
            AttackSpell = attackSpell;
            HealSpell = healSpell;

            string[] splitted = npcIds.Split(',');
            NpcIds = new List<uint>();

            foreach(string s in splitted)
            {
                uint temp;
                if(uint.TryParse(s, out temp))
                    NpcIds.Add(temp);

                else
                {
                    Logging.Write(Color.Red, "Error parsing attribute NpcIds");
                    TreeRoot.Stop();
                }
            }
        }

        /// <summary>Id of the quest. </summary>
        public int VehicleId { get; private set; }

        /// <summary>Item that summons the big bad dragon! </summary>
        public int ItemId { get; private set; }

        /// <summary>Id of the attack spell. </summary>
        public int AttackSpell { get; private set; }

        /// <summary>Id of the heal spell. </summary>
        public int HealSpell { get; private set; }

        /// <summary>Ids of npc's to kill. </summary>
        public List<uint> NpcIds { get; private set; }

        public IEnumerable<WoWPoint> ParseWoWPoints(IEnumerable<XElement> elements)
        {
            var temp = new List<WoWPoint>();

            foreach(XElement element in elements)
            {
                XAttribute xAttribute, yAttribute, zAttribute;
                xAttribute = element.Attribute("X");
                yAttribute = element.Attribute("Y");
                zAttribute = element.Attribute("Z");

                float x, y, z;
                float.TryParse(xAttribute.Value, out x);
                float.TryParse(yAttribute.Value, out y);
                float.TryParse(zAttribute.Value, out z);
                temp.Add(new WoWPoint(x, y, z));
            }

            return temp;
        }

        /// <summary> The start path. </summary>
        public CircularQueue<WoWPoint> StartPath { get; private set; }

        /// <summary> The end path. </summary>
        public CircularQueue<WoWPoint> EndPath { get; private set; }

        /// <summary> The path. </summary>
        public CircularQueue<WoWPoint> Path { get; private set; }

        private bool _isInitialized;
        private void ParsePaths()
        {
            var endPath = new CircularQueue<WoWPoint>();
            var startPath = new CircularQueue<WoWPoint>();
            var path = new CircularQueue<WoWPoint>();

            foreach (WoWPoint point in ParseWoWPoints(Element.Elements().Where(elem => elem.Name == "Start")))
                endPath.Enqueue(point);

            foreach (WoWPoint point in ParseWoWPoints(Element.Elements().Where(elem => elem.Name == "End")))
                startPath.Enqueue(point);

            foreach (WoWPoint point in ParseWoWPoints(Element.Elements().Where(elem => elem.Name == "Hop")))
                path.Enqueue(point);

            StartPath = startPath;
            EndPath = endPath;
            Path = path;
            _isInitialized = true;
        }

        #region Overrides of CustomForcedBehavior

        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        /// <summary> Returns true if the player is inside a vehicle. </summary>
        private static bool IsInVehicle
        {
            get { return Lua.GetReturnVal<bool>("return UnitInVehicle('player')", 0); }
        }

        private PlayerQuest _quest;
        /// <summary> Returns a quest object, 'An end to all things...' </summary>
        private PlayerQuest Quest
        {
            get
            {
                return _quest ?? (_quest = Me.QuestLog.GetQuestById(12779));
            }
        }

        /// <summary> The vehicle as a wowunit </summary>
        public WoWUnit Vehicle { get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(ret => ret.Entry == VehicleId); } }

        public IEnumerable<WoWUnit> Npcs { get { return ObjectManager.GetObjectsOfType<WoWUnit>().Where(ret => NpcIds.Contains(ret.Entry)); } }

        private bool _isDone;
        /// <summary>Gets a value indicating whether this object is done.</summary>
        /// <value>true if this object is done, false if not.</value>
        public override bool IsDone { get { return _isDone; } }

        private readonly Stopwatch _remountTimer = new Stopwatch();

        protected override Composite CreateBehavior()
        {
            return 
                new PrioritySelector(

                    new Decorator(ret => !_isInitialized,
                        new Action(ret => ParsePaths())),

                    // Go home.
                    new Decorator(ret => Quest != null && Quest.IsCompleted || _remountTimer.Elapsed.TotalMinutes >= 14,
                        new PrioritySelector(

                            new Decorator(ret => ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(r => r.Entry == 29107 && Vehicle.Location.Distance(r.Location) <= 10) != null,
                                new Sequence(
                                    new Action(ret => Lua.DoString("VehicleExit()")),
                                    new Action(ret => _isDone = Quest.IsCompleted),
                                    new Action(ret => _remountTimer.Reset())
                                    )),

                    new Decorator(ret => !IsInVehicle,
                        new Sequence(ret => Me.CarriedItems.FirstOrDefault(i => i.Entry == ItemId),
                            new DecoratorContinue(ret => ret == null,
                                new Sequence(
                                    new Action(ret => Logging.Write("Couldn't find item with id: {0}", ItemId)),
                                    new Action(ret => Logging.Write(Color.Red, "Honorbuddy stopped.")),
                                    new Action(ret => TreeRoot.Stop())
                                    )),

                            new WaitContinue(60, ret => ((WoWItem)ret).Cooldown == 0,
                                // Use the item
                                new Action(ret => ((WoWItem)ret).UseContainerItem())
                                ),

                            // Wait until we are in the vehicle
                            new WaitContinue(5, ret => IsInVehicle,
                                new Sequence(
                                    new Action(ret => _remountTimer.Reset()),
                                    new Action(ret => _remountTimer.Start()),
                                    new Action(ret => WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend, TimeSpan.FromMilliseconds(500)))
                                    )
                                ))),

                            new Decorator(ret => StartPath.Peek().Distance(Vehicle.Location) <= 6,
                                new Action(ret => StartPath.Dequeue())),

                            new Action(ret => WoWMovement.ClickToMove(StartPath.Peek()))
                        )),

                    new Decorator(ret => Vehicle != null && ((Vehicle.HealthPercent <= 35 || Vehicle.ManaPercent <= 35) && Npcs.ToList().Count > 0 && Npcs.First(u => u.Distance < 60) != null),
                        new Action(delegate 
                            {
                                Lua.DoString(
                                    @"  for i=1, NUM_PET_ACTION_SLOTS, 1 do 
                                            local c,_,_ = GetPetActionCooldown(i)
                                            local _,_,_,_,_,_,_,s = GetPetActionInfo(i)
                                            
                                            if c == 0 and s == " + HealSpell + " then " +
                                                "CastPetAction(i) " +
                                            "end " +
                                        "end");

                                return RunStatus.Failure;
                            })),

                    new Decorator(ret => !IsInVehicle,
                        new Sequence(ret => Me.CarriedItems.FirstOrDefault(i => i.Entry == ItemId),
                            new DecoratorContinue(ret => ret == null,
                                new Sequence( 
                                    new Action(ret => Logging.Write("Couldn't find item with id: {0}", ItemId)),
                                    new Action(ret => Logging.Write(Color.Red, "Honorbuddy stopped.")),
                                    new Action(ret => TreeRoot.Stop())
                                    )),

                            new WaitContinue(60, ret => ((WoWItem)ret).Cooldown == 0,
                                // Use the item
                                new Action(ret => ((WoWItem)ret).UseContainerItem())
                                ),

                            // Wait until we are in the vehicle
                            new WaitContinue(5, ret => IsInVehicle, 
                                new Sequence(
                                    new Action(ret => _remountTimer.Reset()),
                                    new Action(ret => _remountTimer.Start()),
                                    new Action(ret => WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend, TimeSpan.FromMilliseconds(500)))
                                    )
                                ))),

                    new Decorator(ret => Vehicle != null,
                        
                        new PrioritySelector(

                            new Decorator(ret => !_remountTimer.IsRunning,
                                new Action(ret => _remountTimer.Start())),

                            new Decorator(ret => Path.Peek().Distance(Vehicle.Location) <= 6,
                                new Action(ret => Path.Dequeue())
                                ),

                            new Sequence(
                                new Action(ret => Lua.DoString(
                                       @" for i=1, NUM_PET_ACTION_SLOTS, 1 do 
                                            local c,_,_ = GetPetActionCooldown(i)
                                            local _,_,_,_,_,_,_,s = GetPetActionInfo(i)
                                            if c == 0 and s == " + AttackSpell + " then " +
                                                "CastPetAction(i) " +
                                            "end " +
                                        "end")),

                                new Action(ret => WoWMovement.ClickToMove(Path.Peek()))
                                )
                        ))
                );
        }

        private static void Player_OnPlayerDied()
        {
            LevelBot.ShouldUseSpiritHealer = true;
        }

        private static void Instance_RemoveTargetsFilter(List<WoWObject> units)
        {
            units.Clear();
        }

        private bool _shouldLoot;
        private bool _ressAtSpiritHealers;
        public override void OnStart()
        {
            Targeting.Instance.RemoveTargetsFilter += Instance_RemoveTargetsFilter;
            _shouldLoot = LevelbotSettings.Instance.LootMobs;
            _ressAtSpiritHealers = LevelbotSettings.Instance.RessAtSpiritHealers;
            LevelbotSettings.Instance.LootMobs = false;
            LevelbotSettings.Instance.RessAtSpiritHealers = true;

            BotEvents.Player.OnPlayerDied += Player_OnPlayerDied;
        }

        public override void Dispose()
        {
            Targeting.Instance.RemoveTargetsFilter -= Instance_RemoveTargetsFilter;
            LevelbotSettings.Instance.LootMobs = _shouldLoot;
            LevelbotSettings.Instance.RessAtSpiritHealers = _ressAtSpiritHealers;

            BotEvents.Player.OnPlayerDied -= Player_OnPlayerDied;
        }

        #endregion
    }
}
