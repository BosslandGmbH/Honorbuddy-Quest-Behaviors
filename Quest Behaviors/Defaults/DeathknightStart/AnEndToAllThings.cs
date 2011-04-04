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
using Styx.Logic.Combat;
using Tripper.Tools.Math;
using System.Globalization;

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
            {"KillNpc",null},
            {"HealNpc",null}
        };

        private readonly bool _success = true;

        public AnEndToAllThings(Dictionary<string, string> args) : base(args)
        {
            CheckForUnrecognizedAttributes(_recognizedAttributes);
            int vehicleId = 0, attackSpell = 0, healSpell = 0, itemId = 0, killNpc = 0, healNpc = 0;

            _success = _success && GetAttributeAsInteger("VehicleId", true, "0", 0, int.MaxValue, out vehicleId);
            _success = _success && GetAttributeAsInteger("ItemId", false, "0", 0, int.MaxValue, out itemId);
            _success = _success && GetAttributeAsInteger("AttackSpell", true, "0", 0, int.MaxValue, out attackSpell);
            _success = _success && GetAttributeAsInteger("HealSpell", false, "0", 0, int.MaxValue, out healSpell);
            _success = _success && GetAttributeAsInteger("KillNpc", true, "0", 0, int.MaxValue, out killNpc);
            _success = _success && GetAttributeAsInteger("HealNpc", true, "0", 0, int.MaxValue, out healNpc);

            if(!_success)
            {
                Logging.Write(Color.Red, "Error parsing tag for AnEndToAllThings. {0}", Element);
                TreeRoot.Stop();
            }

            VehicleId = vehicleId;
            ItemId = itemId;
            AttackSpellId = attackSpell;
            HealSpellId = healSpell;
            KillNpc = (uint)killNpc;
            HealNpc = (uint)healNpc;

        }

        /// <summary>Id of the quest. </summary>
        public int VehicleId { get; private set; }

        /// <summary>Item that summons the big bad dragon! </summary>
        public int ItemId { get; private set; }

        public int AttackSpellId { get;  private set; }

        public int HealSpellId { get; private set; }

        /// <summary>Id of the attack spell. </summary>
        public WoWPetSpell AttackSpell
        {
            get
            {
                return Me.PetSpells.FirstOrDefault(s => s.Spell != null && s.Spell.Id == AttackSpellId);
            } 
        }

        /// <summary>Id of the heal spell. </summary>
        public WoWPetSpell HealSpell 
        { 
            get
            {
                return Me.PetSpells.FirstOrDefault(s => s.Spell != null && s.Spell.Id == HealSpellId);
            } 
        }

        /// <summary>Ids of npc's to kill. </summary>
        public uint KillNpc { get; private set; }

        public uint HealNpc { get; private set; }

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
        public WoWPoint StartPoint { get; private set; }

        /// <summary> The end path. </summary>
        public CircularQueue<WoWPoint> EndPath { get; private set; }

        /// <summary> The path. </summary>
        public CircularQueue<WoWPoint> Path { get; private set; }

        private bool _isInitialized;
        private void ParsePaths()
        {
            var endPath = new CircularQueue<WoWPoint>();
            var startPoint = WoWPoint.Empty;
            var path = new CircularQueue<WoWPoint>();

            foreach (WoWPoint point in ParseWoWPoints(Element.Elements().Where(elem => elem.Name == "Start")))
                startPoint = point;

            foreach (WoWPoint point in ParseWoWPoints(Element.Elements().Where(elem => elem.Name == "End")))
                endPath.Enqueue(point);

            foreach (WoWPoint point in ParseWoWPoints(Element.Elements().Where(elem => elem.Name == "Hop")))
                path.Enqueue(point);

            StartPoint = startPoint;
            EndPath = endPath;
            Path = path;
            _isInitialized = true;
        }

        #region Overrides of CustomForcedBehavior

        private static LocalPlayer Me { get { return StyxWoW.Me; } }

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
        public WoWUnit Vehicle { get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(ret => ret.Entry == VehicleId && ret.CreatedByUnitGuid == Me.Guid); } }

        public IEnumerable<WoWUnit> KillNpcs { get { return ObjectManager.GetObjectsOfType<WoWUnit>().Where(ret => ret.HealthPercent > 1 && ret.Entry == KillNpc); } }

        public IEnumerable<WoWUnit> HealNpcs { get { return ObjectManager.GetObjectsOfType<WoWUnit>().Where(ret => ret.HealthPercent > 1 && ret.Entry == HealNpc); } }

        private bool _isDone;
        /// <summary>Gets a value indicating whether this object is done.</summary>
        /// <value>true if this object is done, false if not.</value>
        public override bool IsDone { get { return _isDone; } }

        private readonly Stopwatch _remountTimer = new Stopwatch();

        public static void CastPetAction(WoWPetSpell spell)
        {
            Lua.DoString("CastPetAction({0})", spell.ActionBarIndex + 1);
        }

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

                            new Decorator(ret => Vehicle == null,
                                new Sequence(ret => Me.CarriedItems.FirstOrDefault(i => i.Entry == ItemId),
                                    new DecoratorContinue(ret => ret == null,
                                        new Sequence(
                                            new Action(ret => Logging.Write("Couldn't find item with id: {0}", ItemId)),
                                            new Action(ret => Logging.Write(Color.Red, "Honorbuddy stopped.")),
                                            new Action(ret => TreeRoot.Stop())
                                            )),

                                    new WaitContinue(60, ret => ((WoWItem)ret).Cooldown == 0,
                                        // Use the item
                                        new Sequence(
                                            new Action(ret => ((WoWItem)ret).UseContainerItem()),
                                            new Action(ret => ParsePaths()))
                                        ),

                                    // Wait until we are in the vehicle
                                    new WaitContinue(5, ret => Vehicle != null,
                                        new Sequence(
                                            new Action(ret => _remountTimer.Reset()),
                                            new Action(ret => _remountTimer.Start()),
                                            new Action(ret => WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend, TimeSpan.FromMilliseconds(500)))
                                            )
                                        ))),

                            new Decorator(ret => EndPath.Peek().Distance(Vehicle.Location) <= 6,
                                new Action(ret => EndPath.Dequeue())),

                            new Sequence(
                                new Action(ret => TreeRoot.StatusText = "Flying back to turn in the quest"),
                                new Action(ret => WoWMovement.ClickToMove(EndPath.Peek())))
                                )),

                    new Decorator(ret => Vehicle == null,
                        new Sequence(ret => Me.CarriedItems.FirstOrDefault(i => i.Entry == ItemId),
                            new DecoratorContinue(ret => ret == null,
                                new Sequence( 
                                    new Action(ret => Logging.Write("Couldn't find item with id: {0}", ItemId)),
                                    new Action(ret => Logging.Write(Color.Red, "Honorbuddy stopped.")),
                                    new Action(ret => TreeRoot.Stop())
                                    )),

                            new WaitContinue(60, ret => ((WoWItem)ret).Cooldown == 0,
                                // Use the item
                                new Sequence(
                                    new Action(ret => ParsePaths()),
                                    new Action(ret => ((WoWItem)ret).UseContainerItem()))
                                ),

                            // Wait until we are in the vehicle
                            new WaitContinue(5, ret => Vehicle != null, 
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

                            new Decorator(
                                ret => StartPoint != WoWPoint.Empty,
                                new PrioritySelector(
                                    new Decorator(
                                        ret => Vehicle.Location.Distance2D(StartPoint) < 15,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Pathing through"),
                                            new Action(ret => StartPoint = WoWPoint.Empty))),
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Moving towards start point"),
                                        new Action(ret => Navigator.PlayerMover.MoveTowards(StartPoint))))),

                            new Decorator(ret => Path.Peek().Distance2DSqr(Vehicle.Location) <= 30 * 30,
                                new Action(ret => Path.Dequeue())),

                            new Decorator(ret => (Vehicle.HealthPercent <= 70 || Vehicle.ManaPercent <= 35) &&
                                                 HealNpcs != null && HealSpell != null && !HealSpell.Spell.Cooldown,
                                 new PrioritySelector(
                                    ret => HealNpcs.Where(n => Vehicle.IsSafelyFacing(n)).OrderBy(n => n.DistanceSqr).FirstOrDefault(),
                                    new Decorator(
                                        ret => ret != null && ((WoWUnit)ret).InLineOfSightOCD,
                                        new PrioritySelector(
                                            new Decorator(
                                                ret => ((WoWUnit)ret).Location.Distance(Vehicle.Location) > 15,
                                                new Action(ret => WoWMovement.ClickToMove(((WoWUnit)ret).Location.Add(0,0,10)))),
                                            new Action(ret =>
                                            {
                                                WoWMovement.MoveStop();
                                                CastPetAction(HealSpell);
                                            }))))),

                            new Sequence(
                                ret => KillNpcs.Where(n => n.Distance2DSqr > 20 * 20 && Vehicle.IsSafelyFacing(n)).OrderBy(n => n.DistanceSqr).FirstOrDefault(),
                                new DecoratorContinue(
                                    ret => ret != null && ((WoWUnit)ret).InLineOfSightOCD && AttackSpell != null && !AttackSpell.Spell.Cooldown && !StyxWoW.GlobalCooldown,
                                    new Sequence(
                                        new Action(ret =>
                                            {
                                                Vector3 v = ((WoWUnit)ret).Location - StyxWoW.Me.Location;
                                                v.Normalize();
                                                Lua.DoString(string.Format(
                                                    "local pitch = {0}; local delta = pitch - VehicleAimGetAngle() + 0.1; VehicleAimIncrement(delta);", 
                                                    Math.Asin(v.Z).ToString(CultureInfo.InvariantCulture)));
                                            }),
                                        new Action(ret => CastPetAction(AttackSpell)),
                                        new Action(ret => StyxWoW.SleepForLagDuration()))),
                                        new Action(ret => StyxWoW.SleepForLagDuration()),
                                new Action(ret => WoWMovement.ClickToMove(Path.Peek()))
                                )
                        )));
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
            TreeRoot.GoalText = "An end to all things";
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
