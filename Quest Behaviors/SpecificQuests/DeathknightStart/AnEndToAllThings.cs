// Behavior originally contributed by Nesox / aiming & other rework by Chinajade
//
// DOCUMENTATION:
//     
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

using Bots.Grind;

using CommonBehaviors.Actions;

using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.DeathknightStart.AnEndToAllThings
{
    /// <summary>
    /// Moves along a path in a vehicle using spells to inflict damage and to heals itself until the quest is completed.
    /// ##Syntax##
    /// VehicleId: Id of the vehicle
    /// ItemId: Id of the item that summons the vehicle.
    /// AttackSpell: Id of the attackspell, can be enumerated using, 'GetPetActionInfo(index)'
    /// HealSpell: Id of the healspell, can be enumerated using, 'GetPetActionInfo(index)'
    /// NpcIds: a comma separated list with id's of npc's to kill for this quest. example. NpcIds="143,2,643,1337" 
    /// </summary>
    [CustomBehaviorFileName(@"SpecificQuests\DeathknightStart\AnEndToAllThings")]
    public class AnEndToAllThings : CustomForcedBehavior
    {
        public AnEndToAllThings(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                ActionBarIndex_Attack = GetAttributeAsNullable<int>("AttackSpellIndex", true, ConstrainAs.SpellId, new[] { "AttackSpell" }) ?? 0;
                ActionBarIndex_Heal = GetAttributeAsNullable<int>("HealSpellIndex", false, ConstrainAs.SpellId, new[] { "HealSpell" }) ?? 0;
                HealNpcId = GetAttributeAsNullable<int>("HealNpcId", true, ConstrainAs.MobId, new[] { "HealNpc" }) ?? 0;
                ItemId = GetAttributeAsNullable<int>("ItemId", false, ConstrainAs.ItemId, null) ?? 0;
                KillNpcId = GetAttributeAsNullable<int>("KillNpcId", true, ConstrainAs.MobId, new[] { "KillNpc" }) ?? 0;
                VehicleId = GetAttributeAsNullable<int>("VehicleId", true, ConstrainAs.VehicleId, null) ?? 0;

                DevourHuman = new VehicleAbility(ActionBarIndex_Heal);
                FrozenDeathbolt = new VehicleWeapon(ActionBarIndex_Attack, null, 130.0);
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
        public int ActionBarIndex_Attack { get; private set; }
        public int HealNpcId { get; private set; }
        public int ActionBarIndex_Heal { get; private set; }
        public int ItemId { get; private set; }
        public int KillNpcId { get; private set; }
        public int VehicleId { get; private set; }

        // Private variables for internal state
        private VehicleAbility DevourHuman { get; set; }
        private VehicleWeapon FrozenDeathbolt { get; set; }
        private ConfigMemento _configMemento;
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private bool _isInitialized;
        private PlayerQuest _quest;
        private readonly Stopwatch _remountTimer = new Stopwatch();

        // Private properties
        public WoWUnit FindNpcForHeal()
        {
            using (StyxWoW.Memory.AcquireFrame())
            {
                return 
                   (from wowUnit in ObjectManager.GetObjectsOfType<WoWUnit>()
                    where 
                        (wowUnit.Entry == HealNpcId)
                        && (wowUnit.HealthPercent > 1)
                        && Vehicle.IsSafelyFacing(wowUnit)
                        && wowUnit.InLineOfSight
                    orderby wowUnit.DistanceSqr
                    select wowUnit)
                    .FirstOrDefault();
            }
        }
        private CircularQueue<WoWPoint> EndPath { get; set; }       // End Path
        private WoWUnit FindNpcToKill()
        {
            using (StyxWoW.Memory.AcquireFrame())
            {
                return
                    (from wowUnit in ObjectManager.GetObjectsOfType<WoWUnit>()
                    where
                        (wowUnit.Entry == KillNpcId)
                        && (wowUnit.HealthPercent > 1)
                        && (wowUnit.Distance2DSqr > (40 * 40))
                        && Vehicle.IsSafelyFacing(wowUnit)
                        && wowUnit.InLineOfSight
                    orderby wowUnit.DistanceSqr
                    select wowUnit)
                    .FirstOrDefault();
            }
        }
        private LocalPlayer Me { get { return (StyxWoW.Me); } }
        private CircularQueue<WoWPoint> Path { get; set; }
        private WoWPoint StartPoint { get; set; }    // Start path
        public WoWUnit Vehicle
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>()
                                    .FirstOrDefault(ret => ret.Entry == VehicleId && ret.CreatedByUnitGuid == Me.Guid);
            }
        }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id: AnEndToAllThings.cs 569 2013-06-26 02:37:28Z chinajade $"); } }
        public override string SubversionRevision { get { return ("$Revision: 569 $"); } }


        ~AnEndToAllThings()
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
                if (_configMemento != null)
                { _configMemento.Dispose(); }

                _configMemento = null;

                BotEvents.OnBotStop -= BotEvents_OnBotStop;
                BotEvents.Player.OnPlayerDied -= Player_OnPlayerDied;
                Targeting.Instance.RemoveTargetsFilter -= Instance_RemoveTargetsFilter;

                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }


        public void BotEvents_OnBotStop(EventArgs args)
        {
            Dispose();
        }


        public IEnumerable<WoWPoint> ParseWoWPoints(IEnumerable<XElement> elements)
        {
            var temp = new List<WoWPoint>();

            foreach (XElement element in elements)
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

        /// <summary> Returns a quest object, 'An end to all things...' </summary>
        private PlayerQuest Quest
        {
            get
            {
                return _quest ?? (_quest = Me.QuestLog.GetQuestById(12779));
            }
        }

        private static void Player_OnPlayerDied()
        {
            LevelBot.ShouldUseSpiritHealer = true;
        }

        private static void Instance_RemoveTargetsFilter(List<WoWObject> units)
        {
            units.Clear();
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return
                new PrioritySelector(

                    new Decorator(ret => !_isInitialized,
                        new Action(ret => ParsePaths())),

                    // Go home.
                    new Decorator(ret => Quest != null && Quest.IsCompleted || _remountTimer.Elapsed.TotalMinutes >= 20,
                        new PrioritySelector(
                            new Decorator(ret => ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(r => r.Entry == 29107 && Vehicle.Location.Distance(r.Location) <= 10) != null,
                                new Sequence(
                                    new Action(ret => Lua.DoString("VehicleExit()")),
                                    new Action(ret => _isBehaviorDone = Quest.IsCompleted),
                                    new Action(ret => _remountTimer.Reset())
                                    )),

                            new Decorator(ret => Vehicle == null,
                                new Sequence(ret => Me.CarriedItems.FirstOrDefault(i => i.Entry == ItemId),
                                    new DecoratorContinue(ret => ret == null,
                                        new Sequence(
                                            new Action(ret => LogMessage("fatal", "Unable to find ItemId({0}) in inventory.", ItemId))
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
                                new Action(ret => WoWMovement.ClickToMove(EndPath.Peek())),
                                new DecoratorContinue(ret => Me.MovementInfo.IsAscending,
                                    new Action(ret => { WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend); })))
                                )),

                    new Decorator(ret => Vehicle == null,
                        new Sequence(ret => Me.CarriedItems.FirstOrDefault(i => i.Entry == ItemId),
                            new DecoratorContinue(ret => ret == null,
                                new Sequence(
                                    new Action(ret => LogMessage("fatal", "Unable to locate ItemId({0}) in inventory.", ItemId))
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

                            new Decorator(context => ((Vehicle.HealthPercent <= 70) || (Vehicle.ManaPercent <= 35))
                                                    && DevourHuman.IsAbilityReady(),
                                new Action(context =>
                                {
                                    var selectedTarget = FindNpcForHeal();
                                    if (selectedTarget == null)
                                        { return RunStatus.Failure; }

                                    if (selectedTarget.Location.Distance(Vehicle.Location) > 15)
                                    {
                                        WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
                                        WoWMovement.ClickToMove(selectedTarget.Location.Add(0, 0, 10));
                                        return RunStatus.Success;
                                    }

                                    DevourHuman.UseAbility();
                                    WoWMovement.ClickToMove(Path.Peek());
                                    return RunStatus.Success;
                                })),

                            new ActionFail(context =>
                            {
                                var heightDelta = Me.Location.Z - Path.Peek().Z;
                                if (heightDelta < -10)
                                    { WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend); }
                                else if (heightDelta > 10)
                                    { WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend); }
                            }),

                            new Sequence(
                                new Action(context =>
                                {
                                    if (!FrozenDeathbolt.IsWeaponReady())
                                        { return RunStatus.Failure; }

                                    var selectedTarget = FindNpcToKill();
                                    if (selectedTarget == null)
                                        { return RunStatus.Failure; }

                                    var projectileFlightTime = FrozenDeathbolt.CalculateTimeOfProjectileFlight(selectedTarget.Location);
                                    var anticipatedLocation = selectedTarget.AnticipatedLocation(projectileFlightTime);
                                    var isAimed = FrozenDeathbolt.WeaponAim(anticipatedLocation);

                                    if (!isAimed)
                                        { return RunStatus.Failure; }

                                    FrozenDeathbolt.WeaponFire(anticipatedLocation);
                                    return RunStatus.Success;
                                }),
                                // Need to delay a bit for the weapon to actually launch.  Otherwise
                                // it screws the aim up if we move again before projectile is fired.
                                // NB: Wait, not WaitContinue, because we want to fall through
                                new Wait(TimeSpan.FromMilliseconds(400), context => false, new ActionAlwaysFail())
                            ),
                            new Action(context => { WoWMovement.ClickToMove(Path.Peek()); })
                        )));
        }


        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        public override bool IsDone
        {
            get { return (_isBehaviorDone); }
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
                // The ConfigMemento() class captures the user's existing configuration.
                // After its captured, we can change the configuration however needed.
                // When the memento is dispose'd, the user's original configuration is restored.
                // More info about how the ConfigMemento applies to saving and restoring user configuration
                // can be found here...
                //     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_Saving_and_Restoring_User_Configuration
                _configMemento = new ConfigMemento();

                BotEvents.Player.OnPlayerDied += Player_OnPlayerDied;
                BotEvents.OnBotStop += BotEvents_OnBotStop;
                Targeting.Instance.RemoveTargetsFilter += Instance_RemoveTargetsFilter;

                // Disable any settings that may cause distractions --
                // When we do this quest, we don't want to be distracted by other things.
                // NOTE: these settings are restored to their normal values when the behavior completes
                // or the bot is stopped.
                CharacterSettings.Instance.HarvestHerbs = false;
                CharacterSettings.Instance.HarvestMinerals = false;
                CharacterSettings.Instance.LootChests = false;
                CharacterSettings.Instance.LootMobs = false;
                CharacterSettings.Instance.NinjaSkin = false;
                CharacterSettings.Instance.SkinMobs = false;
                CharacterSettings.Instance.RessAtSpiritHealers = true;

                TreeRoot.GoalText = this.GetType().Name + ": In Progress";
            }
        }

        #endregion
    }
}
