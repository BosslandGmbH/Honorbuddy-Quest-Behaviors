// Behavior originally contributed by Chinajade
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

#region Summary and Documentation
// Documentation:
// * Moves to Siege Tank, dismounts, and enters tank
// * Fires at targets as it moves around range
// * Behavior is stop/start friendly
// * Accommodates getting thrown out of a vehicle for any reason
//
// Notes:
// * The only time this behavior misses is when weapons platform pitches or rolls.
//   We've looked at techniques to calculate the angle contributions induced by
//   vehicle pitch and roll, but we've yet to find any place with usable information.
//   The most obvious place to look is WoWUnit.GetWorldMatrix(); however, the matrix
//   returned is devoid of meaningful 'Z contributions' for each of the primary axis.
//   <sigh> The search will have to continue some other day.  For now, the misses
//   aren't significant enough to waste significant effort trying to find the buried
//   information--if its available at all.
//
#endregion


#region Examples
#endregion

#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using Bots.Grind;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.Tanaris.RocketRescue_24910
{
    [CustomBehaviorFileName(@"SpecificQuests\24910-Tanaris-RocketRescue")]
    public class RocketRescue_24910_25050 : QuestBehaviorBase
    {
        #region Constructor and Argument Processing
        public RocketRescue_24910_25050(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                QuestId = Me.IsAlliance ? 25050 : 24910;
                TerminationChecksQuestProgress = false;

                AuraId_EmergencyRocketPack = 75730;         // http://wowhead.com/spell=75730 (applies to us)
                AuraId_Parachute = 54649;                   // http://wowhead.com/spell=54649 (applies to us)
                AuraId_RocketPack = 72359;                  // http://wowhead.com/spell=72359 (applies to mob)
                MobId_Objective1_SteamwheedleSurvivor = 38571;  // http://wowhead.com/npc=38571
                MobId_Objective2_SouthseaBlockader = 40583;     // http://wowhead.com/npc=40583
                MobId_SteamwheedleRescueBalloon = 40604;    // http://wowhead.com/npc=40604
                VehicleStagingArea = new WoWPoint(-7092.513, -3906.368, 10.96168);

                // Weapon allows TAU (i.e., 2*PI) horizontal rotation
                WeaponAzimuthMax = 0.0;                    // Use: /script print(VehicleAimGetAngle())
                WeaponAzimuthMin = -1.18;                  // Use: /script print(VehicleAimGetAngle())
                WeaponLifeRocket_MuzzleVelocity = 80.0;
                WeaponPirateDestroyingBomb_MuzzleVelocity = 80.0;
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                QBCLog.Error("[MAINTENANCE PROBLEM]: " + except.Message
                        + "\nFROM HERE:\n"
                        + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }

        // Attributes provided by caller
        private int AuraId_EmergencyRocketPack { get; set; }
        private int AuraId_Parachute { get; set; }
        private int AuraId_RocketPack { get; set; }
        private WoWPoint VehicleStagingArea { get; set; }
        private int MobId_Objective1_SteamwheedleSurvivor { get; set; }
        private int MobId_SteamwheedleRescueBalloon { get; set; }
        private int MobId_Objective2_SouthseaBlockader { get; set; }
        private double WeaponAzimuthMax { get; set; }
        private double WeaponAzimuthMin { get; set; }
        private double WeaponLifeRocket_MuzzleVelocity { get; set; }
        private double WeaponPirateDestroyingBomb_MuzzleVelocity { get; set; }

        protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
        {
            // empty, for now
        }

        protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
        {
            // empty, for now
        }
        #endregion


        #region Private and Convenience variables
        private enum BehaviorStateType
        {
            MountingVehicle,        // initial state
            RidingOutToHuntingGrounds,
            CompletingObjectives,
            ReturningToBase,
        }

        private BehaviorStateType BehaviorState
        {
            get { return _behaviorState; }
            set
            {
                // For DEBUGGING...
                if (_behaviorState != value)
                    { QBCLog.DeveloperInfo("BehaviorStateType: {0}", value); }

                _behaviorState = value;
            }
        }
        private WoWUnit SelectedTarget { get; set; }
        private WoWUnit Vehicle { get; set; }
        private VehicleWeapon WeaponChoice { get; set; }
        private VehicleAbility WeaponEmergencyRocketPack { get; set; }
        private VehicleWeapon WeaponLifeRocket { get; set; }
        private VehicleWeapon WeaponPirateDestroyingBomb { get; set; }

        private BehaviorStateType _behaviorState;
        private readonly LocalBlacklist _targetBlacklist = new LocalBlacklist(TimeSpan.FromSeconds(30));

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id$"); } }
        public override string SubversionRevision { get { return ("$Rev$"); } }
        #endregion


        #region Destructor, Dispose, and cleanup
        ~RocketRescue_24910_25050()
        {
            Dispose(false);
        }
        #endregion


        #region Overrides of CustomForcedBehavior
        public override void OnStart()
        {
            // Let QuestBehaviorBase do basic initializaion of the behavior, deal with bad or deprecated attributes,
            // capture configuration state, install BT hooks, etc.  This will also update the goal text.
            var isBehaviorShouldRun = OnStart_QuestBehaviorCore();

            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (isBehaviorShouldRun)
            {               
                // Turn off LevelBot behaviors that will interfere...
                // NB: These will be restored by our parent class when we're done.
                // NB: We need to disable the Roam behavior to prevent the StuckHandler from kicking in.
                LevelBot.BehaviorFlags &=
                    ~(BehaviorFlags.Combat | BehaviorFlags.Loot | BehaviorFlags.Roam | BehaviorFlags.Vendor);

                BehaviorState = BehaviorStateType.MountingVehicle;
            }
        }
        #endregion


        #region Main Behaviors
        protected override Composite CreateBehavior_CombatMain()
        {
            return new PrioritySelector(
                // empty, for now...
                );
        }


        protected override Composite CreateBehavior_CombatOnly()
        {
            return new PrioritySelector(
                // Disable combat routine while we are in the vehicle...
                new Decorator(context => Query.IsInVehicle(),
                    new ActionAlwaysSucceed())
                );
        }


        protected override Composite CreateBehavior_DeathMain()
        {
            return new PrioritySelector(
                // empty, for now...
                );
        }


        protected override Composite CreateMainBehavior()
        {
            return new PrioritySelector(
                new Switch<BehaviorStateType>(context => BehaviorState,
                    new Action(context =>   // default case
                    {
                        var message = string.Format("BehaviorStateType({0}) is unhandled", BehaviorState);
                        QBCLog.MaintenanceError(message);
                        TreeRoot.Stop();
                        BehaviorDone(message);
                    }),

                    new SwitchArgument<BehaviorStateType>(BehaviorStateType.MountingVehicle,
                        StateBehaviorPS_MountingVehicle()),
                    new SwitchArgument<BehaviorStateType>(BehaviorStateType.RidingOutToHuntingGrounds,
                        StateBehaviorPS_RidingOutToHuntingGrounds()),
                    new SwitchArgument<BehaviorStateType>(BehaviorStateType.CompletingObjectives,
                        StateBehaviorPS_CompletingObjectives()),
                    new SwitchArgument<BehaviorStateType>(BehaviorStateType.ReturningToBase,
                        StateBehaviorPS_ReturningToBase())
                ));
        }
        #endregion


        #region Behavior States
        private Composite StateBehaviorPS_MountingVehicle()
        {
            return new PrioritySelector(
                new Decorator(context => Me.IsQuestComplete(QuestId),
                    new Action(context => { BehaviorDone(); })),

                new Decorator(context => IsInBalloon(),
                    new PrioritySelector(
                        SubBehaviorPS_InitializeVehicleAbilities(), 
                        new Action(context => { BehaviorState = BehaviorStateType.RidingOutToHuntingGrounds; })
                    )),
                    
                // Locate a vehicle to mount...
                new Decorator(context => !Query.IsViable(Vehicle),
                    new PrioritySelector(
                        new Action(context =>
                        {
                            Vehicle =
                                Query.FindMobsAndFactions(Utility.ToEnumerable(MobId_SteamwheedleRescueBalloon))
                                .FirstOrDefault()
                                as WoWUnit;

                            if (Query.IsViable(Vehicle))
                            {
                                Utility.Target(Vehicle);
                                return RunStatus.Success;
                            }

                            return RunStatus.Failure;   // fall through
                        }),

                        // No vehicle found, move to staging area...
                        new UtilityBehaviorPS.MoveTo(
                            context => VehicleStagingArea,
                            context => "Vehicle Staging Area",
                            context => MovementBy),

                        // Wait for vehicle to respawn...
                        new CompositeThrottle(Throttle.UserUpdate,
                            new Action(context =>
                            {
                                TreeRoot.StatusText =
                                    string.Format("Waiting for {0} to respawn.",
                                        Utility.GetObjectNameFromId(MobId_SteamwheedleRescueBalloon));
                            }))
                    )),

                // Move to vehicle and enter...
                new CompositeThrottle(Throttle.UserUpdate,
                    new Action(context => { TreeRoot.StatusText = string.Format("Moving to {0}", Vehicle.Name); })),
                new Decorator(context => !Vehicle.WithinInteractRange,
                    new UtilityBehaviorPS.MoveTo(
                        context => Vehicle.Location,
                        context => Vehicle.Name,
                        context => MovementBy)),
                new Decorator(context => Me.IsMoving,
                    new Action(context => { Navigator.PlayerMover.MoveStop(); })),
                new Decorator(context => Me.Mounted,
                    new UtilityBehaviorPS.ExecuteMountStrategy(context => MountStrategyType.DismountOrCancelShapeshift)),
                new ActionFail(context =>
                {
                    // If we got booted out of a vehicle for some reason, reset the weapons...
                    WeaponLifeRocket = null;
                    WeaponPirateDestroyingBomb = null;
                    WeaponEmergencyRocketPack = null;

                    Utility.Target(Vehicle);
                    Vehicle.Interact();
                }),
                new Wait(TimeSpan.FromMilliseconds(10000), context => IsInBalloon(), new ActionAlwaysSucceed()),
                new ActionAlwaysSucceed()
            );
        }


        private Composite StateBehaviorPS_RidingOutToHuntingGrounds()
        {
            return new PrioritySelector(
                // If for some reason no longer in the vehicle, go fetch another...
                new Decorator(context => !IsInBalloon(),
                    new Action(context =>
                    {
                        QBCLog.Warning("We've been jettisoned from vehicle unexpectedly--will try again.");
                        BehaviorState = BehaviorStateType.MountingVehicle;
                    })),

                // Ride to hunting grounds complete when spells are enabled...
                new Decorator(context => WeaponLifeRocket.IsWeaponUsable(),
                    new Action(context => { BehaviorState = BehaviorStateType.CompletingObjectives; })),

                new CompositeThrottle(Throttle.UserUpdate,
                    new Action(context => { TreeRoot.StatusText = "Riding out to hunting grounds"; }))
            );            
        }


        private Composite StateBehaviorPS_CompletingObjectives()
        {
            return new PrioritySelector(
                // If for some reason no longer in the vehicle, go fetch another...
                new Decorator(context => !IsInBalloon(),
                    new Action(context =>
                    {
                        QBCLog.Warning("We've been jettisoned from vehicle unexpectedly--will try again.");
                        BehaviorState = BehaviorStateType.MountingVehicle;
                    })),

                // If quest is complete, then head back...
                new Decorator(context => Me.IsQuestComplete(QuestId),
                    new Action(context => { BehaviorState = BehaviorStateType.ReturningToBase; })),

                new CompositeThrottle(Throttle.UserUpdate,
                    new Action(context => { TreeRoot.StatusText = "Completing Quest Objectives"; })),

                // Select new best target, if our current one is no longer useful...
                new Decorator(context => !IsViableForTargeting(SelectedTarget),
                    new Action(context =>
                    {
                        if (!IsViableForTargeting(SelectedTarget) && !Me.IsQuestObjectiveComplete(QuestId, 1))
                        {
                            SelectedTarget = FindBestTarget(MobId_Objective1_SteamwheedleSurvivor);
                            WeaponChoice = WeaponLifeRocket;
                        }

                        if (!IsViableForTargeting(SelectedTarget) && !Me.IsQuestObjectiveComplete(QuestId, 2))
                        {
                            SelectedTarget = FindBestTarget(MobId_Objective2_SouthseaBlockader);
                            WeaponChoice = WeaponPirateDestroyingBomb;
                        }
                    })),

                // Aim & Fire at the selected target...
                new Decorator(context => IsViableForTargeting(SelectedTarget),
                    new Sequence(
                        new Action(context =>
                        {
                            // If weapon aim cannot address selected target, blacklist target for a few seconds...
                            if (!WeaponChoice.WeaponAim(SelectedTarget))
                            {
                                _targetBlacklist.Add(SelectedTarget, TimeSpan.FromSeconds(5));
                                return RunStatus.Failure;
                            }

                            // If weapon could not be fired, wait for it to become ready...
                            if (!WeaponChoice.WeaponFire())
                                { return RunStatus.Failure; }

                            // Weapon was fired, blacklist target so we can choose another...
                            _targetBlacklist.Add(SelectedTarget, TimeSpan.FromSeconds(15));
                            return RunStatus.Success;
                        }),
                        new WaitContinue(Delay.AfterWeaponFire, context => false, new ActionAlwaysSucceed())
                    ))
            );            
        }


        private Composite StateBehaviorPS_ReturningToBase()
        {
            // If we are not returning home...
            return new Decorator(context => !(Me.HasAura(AuraId_EmergencyRocketPack)
                                                || Me.HasAura(AuraId_Parachute)),
                new PrioritySelector(
                    // If still in vehicle, then use spell to start journey home...
                    new Decorator(context => WeaponEmergencyRocketPack.IsAbilityReady(),
                        new Action(context => { WeaponEmergencyRocketPack.UseAbility(); })),

                    // If journey complete, behavior is done...
                    new Decorator(context => !Me.IsMoving,
                        new Action(context => { BehaviorDone(); })),

                    new CompositeThrottle(Throttle.UserUpdate,
                        new ActionFail(context => { TreeRoot.StatusText ="Returning to base"; }))
                ));
        }


        private Composite SubBehaviorPS_InitializeVehicleAbilities()
        {
            return
                new Decorator(context => (WeaponLifeRocket == null)
                                            || (WeaponPirateDestroyingBomb == null)
                                            || (WeaponEmergencyRocketPack == null),
                    // Give the WoWclient a few seconds to produce the vehicle action bar...
                    // NB: If we try to use the weapon too quickly after entering vehicle,
                    // then it will cause the WoWclient to d/c.
                    new WaitContinue(TimeSpan.FromSeconds(10),
                        context => Query.IsVehicleActionBarShowing(),
                        new Action(context =>
                        {
                            var weaponArticulation = new WeaponArticulation(WeaponAzimuthMin, WeaponAzimuthMax);

                            // (slot 1): http://wowhead.com/spell=75560
                            WeaponLifeRocket =
                                new VehicleWeapon(1, weaponArticulation, WeaponLifeRocket_MuzzleVelocity)
                                {
                                    LogAbilityUse = true,
                                    LogWeaponFiringDetails = false
                                };

                            // (slot 2): http://wowhead.com/spell=73257
                            WeaponPirateDestroyingBomb =
                                new VehicleWeapon(2, weaponArticulation, WeaponPirateDestroyingBomb_MuzzleVelocity)
                                {
                                    LogAbilityUse = true,
                                    LogWeaponFiringDetails = false
                                };

                            // (slot 6): http://wowhead.com/spell=40603
                            WeaponEmergencyRocketPack =
                                new VehicleAbility(6)
                                {
                                    LogAbilityUse = true
                                };
                        })
                    ));
        }
        #endregion


        #region Helpers
        private WoWUnit FindBestTarget(int targetId)
        {
            using (StyxWoW.Memory.AcquireFrame())
            {
                return
                   (from wowUnit in ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                    where
                        (wowUnit.Entry == targetId)
                        && IsViableForTargeting(wowUnit)
                    orderby
                        wowUnit.Distance2DSqr
                    select wowUnit)
                    .FirstOrDefault();
            }
        }


        private bool IsInBalloon()
        {
            return Query.IsInVehicle()
                && !(Me.HasAura(AuraId_EmergencyRocketPack)
                    || Me.HasAura(AuraId_Parachute));
        }


        private bool IsViableForTargeting(WoWUnit wowUnit)
        {
            return
                Query.IsViable(wowUnit)
                && !_targetBlacklist.Contains(wowUnit.Guid)
                && wowUnit.IsAlive
                && wowUnit.InLineOfSight
                && Query.IsStateMatch_AurasMissing(wowUnit, Utility.ToEnumerable<int>(AuraId_RocketPack));
        }
        #endregion
    }
}
