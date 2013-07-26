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

#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.Tanaris.RocketRescue_24910
{
    [CustomBehaviorFileName(@"SpecificQuests\24910-Tanaris-RocketRescue")]
    public class RocketRescue_24910 : QuestBehaviorBase
    {
        #region Constructor and Argument Processing
        public RocketRescue_24910(Dictionary<string, string> args)
            : base(new Dictionary<string, string>() { { "QuestId", "24910" } }, false)
        {
            try
            {   
                AuraId_EmergencyRocketPack = 75730;         // http://wowhead.com/spell=75730 (applies to us)
                AuraId_Parachute = 54649;                   // http://wowhead.com/spell=54649 (applies to us)
                AuraId_RocketPack = 72359;                  // http://wowhead.com/spell=72359 (applies to mob)
                BalloonLaunchPoint = new WoWPoint(-7092.513, -3906.368, 10.96168);
                MobId_Objective1_SteamwheedleSurvivor = 38571;  // http://wowhead.com/npc=38571
                MobId_Objective2_SouthseaBlockader = 40583;     // http://wowhead.com/npc=40583
                MobId_SteamwheedleRescueBalloon = 40604;    // http://wowhead.com/npc=40604

                // Weapon allows TAU (i.e., 2*PI) horizontal rotation
                WeaponAzimuthMax = 0.0;                    // Use: /script print(VehicleAimGetAngle())
                WeaponAzimuthMin = -1.18;                  // Use: /script print(VehicleAimGetAngle())

                var weaponArticulation = new WeaponArticulation();
                WeaponLifeRocket = new VehicleWeapon(1, weaponArticulation);            // (slot 1) http://wowhead.com/npc=75560
                WeaponPirateDestroyingBomb = new VehicleWeapon(2, weaponArticulation);  // (slot 2) http://wowhead.com/npc=73257
                WeaponEmergencyRocketPack = new VehicleAbility(6);                      // (slot 6) http://wowhead.com/npc=40603
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
        private int AuraId_EmergencyRocketPack { get; set; }
        private int AuraId_Parachute { get; set; }
        private int AuraId_RocketPack { get; set; }
        private WoWPoint BalloonLaunchPoint { get; set; }
        private int MobId_Objective1_SteamwheedleSurvivor { get; set; }
        private int MobId_SteamwheedleRescueBalloon { get; set; }
        private int MobId_Objective2_SouthseaBlockader { get; set; }
        private double WeaponAzimuthMax { get; set; }
        private double WeaponAzimuthMin { get; set; }

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
            MountingBalloon,        // initial state
            RidingOutToHuntingGrounds,
            CompletingObjectives,
            ReturningToBase,
        }

        private WoWUnit BalloonVehicle { get; set; }
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
        private VehicleWeapon WeaponChoice { get; set; }
        private VehicleAbility WeaponEmergencyRocketPack { get; set; }
        private VehicleWeapon WeaponLifeRocket { get; set; }
        private VehicleWeapon WeaponPirateDestroyingBomb { get; set; }

        private BehaviorStateType _behaviorState;
        private readonly LocalBlacklist _targetBlacklist = new LocalBlacklist(TimeSpan.FromSeconds(30));

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id: 24910-Tanaris-RocketRescue.cs 574 2013-06-28 08:54:59Z chinajade $"); } }
        public override string SubversionRevision { get { return ("$Rev: 574 $"); } }
        #endregion


        #region Destructor, Dispose, and cleanup
        ~RocketRescue_24910()
        {
            Dispose(false);
        }
        #endregion


        #region Overrides of CustomForcedBehavior
        public override void OnStart()
        {
            // Let QuestBehaviorBase do basic initializaion of the behavior, deal with bad or deprecated attributes,
            // capture configuration state, install BT hooks, etc.  This will also update the goal text.
            OnStart_QuestBehaviorCore();

            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (!IsDone)
            {
                BehaviorState = BehaviorStateType.MountingBalloon;
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
                new Decorator(context => Me.InVehicle,
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

                    new SwitchArgument<BehaviorStateType>(BehaviorStateType.MountingBalloon,
                        StateBehaviorPS_MountingBalloon()),
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
        private Composite StateBehaviorPS_MountingBalloon()
        {
            return new PrioritySelector(
                new Decorator(context => Me.IsQuestComplete(QuestId),
                    new Action(context => { BehaviorDone(); })),

                // If we're in the vehicle, wait for the ride out to hunting grounds to complete...
                new Decorator(context => IsInBalloon(),
                    new Action(context => { BehaviorState = BehaviorStateType.RidingOutToHuntingGrounds; })),

                // Locate the vehicle...
                new Decorator(context => !Query.IsViable(BalloonVehicle),
                    new ActionFail(context =>
                    {
                        BalloonVehicle =
                            Query.FindMobsAndFactions(Utility.ToEnumerable(MobId_SteamwheedleRescueBalloon))
                            .FirstOrDefault()
                            as WoWUnit;
                    })),

                // Move to vehicle and enter...
                new Decorator(context => Query.IsViable(BalloonVehicle),
                    new PrioritySelector(
                        new CompositeThrottle(Throttle.UserUpdate,
                            new Action(context =>
                            {
                                TreeRoot.StatusText =
                                    string.Format("Moving to {0}", Utility.GetObjectNameFromId(MobId_SteamwheedleRescueBalloon));
                            })),
                        new UtilityBehaviorPS.MoveTo(
                            context => BalloonVehicle.Location,
                            context => BalloonVehicle.Name,
                            context => MovementBy),
                        new UtilityBehaviorSeq.Interact(context => BalloonVehicle)
                    )),

                // Otherwise, move near balloon launch point...
                new Decorator(context => !Query.IsViable(BalloonVehicle),
                    new PrioritySelector(
                        new UtilityBehaviorPS.MoveTo(
                            context => BalloonLaunchPoint,
                            context => "Balloon Launch Point",
                            context => MovementBy),
                        new Action(context =>
                        {
                            TreeRoot.StatusText =
                                string.Format("Waiting for Steamwheedle Rescue Balloon({0}) to respawn.",
                                    Utility.GetObjectNameFromId(MobId_SteamwheedleRescueBalloon));
                        })
                    ))
            );
        }


        private Composite StateBehaviorPS_RidingOutToHuntingGrounds()
        {
            return new PrioritySelector(
                // If for some reason no longer in the vehicle, go fetch another...
                new Decorator(context => !IsInBalloon(),
                    new Action(context => { BehaviorState = BehaviorStateType.MountingBalloon; })),

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
                    new Action(context => { BehaviorState = BehaviorStateType.MountingBalloon; })),

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
                            var projectileFlightTime = WeaponChoice.CalculateTimeOfProjectileFlight(SelectedTarget.Location);
                            var anticipatedLocation = SelectedTarget.AnticipatedLocation(projectileFlightTime);
                            var isAimed = WeaponChoice.WeaponAim(anticipatedLocation);

                            if (isAimed)
                            {
                                WeaponChoice.WeaponFire(anticipatedLocation);
                                _targetBlacklist.Add(SelectedTarget, TimeSpan.FromSeconds(30));
                            }

                            else // Weapon cannot address selected target, blacklist target for a few seconds...
                            {
                                _targetBlacklist.Add(SelectedTarget, TimeSpan.FromSeconds(5));
                                return RunStatus.Failure;
                            }

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
                        wowUnit.Distance
                    select wowUnit)
                    .FirstOrDefault();
            }
        }


        private bool IsInBalloon()
        {
            return Me.InVehicle
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
