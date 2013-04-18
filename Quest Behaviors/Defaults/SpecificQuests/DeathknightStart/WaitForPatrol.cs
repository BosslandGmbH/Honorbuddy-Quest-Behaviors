// Behavior originally contributed by HighVoltz / revamp by Chinajade
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.


#region Summary and Documentation
// WAITFORPATROL waits at a safe location until an NPC is a specified distance away.
// The behavior has the following characteristics:
//  * The 'safe location' can either be an NPC, or an X/Y/Z coordinates
//  * The mob we're avoiding must be both a minimum distance away, _and_ facing away from us.
//      This assures the mob is not pathing towards us.
//
// BEHAVIOR ATTRIBUTES:
// Basic Attributes:
//      AvoidDistance [REQUIRED]
//          This is the minimum distance the AvoidMobId must be from our safe spot.
//      AvoidMobId [REQUIRED]
//          This is the Id of the NPC on which we should wait to clear the area.
//      MoveToMobId [REQUIRED, if X/Y/Z is omitted]
//          This is the Id of the NPC to which we should move for a 'safe spot'.
//      X/Y/Z [REQUIRED, if MoveToMobId is omitted]
//          This specifies the location of a 'safe spot' where the toon should loiter
//          while waiting for the AvoidMobId to clear the area.
//
// Quest binding:
//      QuestId [REQUIRED if EventCompleteWhen=QuestComplete; Default:none]:
//      QuestCompleteRequirement [Default:NotComplete]:
//      QuestInLogRequirement [Default:InLog]:
//          A full discussion of how the Quest* attributes operate is described in
//          http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
//      QuestObjectiveIndex [REQUIRED if EventCompleteWhen=QuestObjectiveComplete]
//          [on the closed interval: [1..5]]
//          This argument is only consulted if EventCompleteWhen is QuestObjectveComplete.
//          The argument specifies the index of the sub-goal of a quest.
//
// Tunables (ideally, the profile would _never_ provide these arguments):
//      MovementBy [optional; Default: NavigatorPreferred]
//          [allowed values: ClickToMoveOnly/NavigatorOnly/NavigatorPreferred]
//          Allows alternative navigation techniques.  You should provide this argument
//          only when an area is unmeshed, or not meshed well.  If ClickToMoveOnly
//          is specified, the area must be free of obstacles; otherwise, the toon
//          will get hung up.
//
// BEHAVIOR EXTENSION ELEMENTS (goes between <CustomBehavior ...> and </CustomBehavior> tags)
// See the "Examples" section for typical usage.
//      FollowPath [optional]
//          The FollowPath will be used to ingress into an area once a mob has been
//          determined to be 'clear'.  This allows for following a very precise path
//          into an unsafe area.  An additional benefit is if the AvoidMob turns around
//          and starts coming back, the FollowPath is used to return the toon back to the
//          safe spot until the AvoidMob is clear again.
//          There are several attributes that may be supplied to the FollowPath
//          extension element:
//              EgressDistance [optional, Default: one-third of AvoidDistance]
//                  Any time the AvoidMob gets within EgressDistance of the toon,
//                  the toon will return to the safe spot and re-evaluate the situation.
//              Strategy [optional, Default: StalkMobAtAvoidDistance]
//                  Allowed Values: StalkMobAtAvoidDistance / WaitForAvoidDistance
//                  This value determines how AvoidDistance triggers the use of FollowPath.
//                  With StalkMobAtAvoidDistance, the AvoidMob will be followed at
//                  AvoidDistance.  If the AvoidMob stops, the toon will stop until the
//                  AvoidMob continues moving away.  If the AvoidMob starts moving toward
//                  the toon, the toon will move back to the safespot when AvoidMob
//                  gets within EgressDistance.
//                  With WaitForAvoidDistance, the toon waits until AvoidMob is AvoidDistance
//                  away before it starts using the FollowPath.
//              DismissPet [optional, Default: false]
//                  A pet usually follows its owner two yards to the left of the owner
//                  as it travels.  This is sometimes enough to cause trouble in tight
//                  areas.  This attribute arranges to dismiss any pet before traversing
//                  the FollowPath.
//
// THINGS TO KNOW:
// * The AvoidMobId must meet both of the following criteria before the behavior terminates:
//      + AvoidMobId is greater than AvoidDistance away from our safe spot.
//      + AvoidMobId must be facing away from our safe spot.
//      This means you should choose the safe spot such that the mob is facing away
//      from you when you want to declare 'area clear'.
// * All looting and harvesting is turned off while the event is in progress.
// * This behavior does _not_ alter the PullDistance.  As such, the area in which we're
//      waiting will be kept clear of all mobs within PullDistance while we're waiting.
//      For classes that may move around a lot while fighting (i.e., Mages with blink),
//      you will want to choose your wait spot and pull distance judiciously.
// * If both MoveToMobId _and_ X/Y/Z are specified, the MoveToMobId takes precedence.
//      I.e., the behavior will move to the MoveToMobId as a safe spot _instead_
//      of the specified X/Y/Z location.
// * The behavior will mount to travel to our safe spot, if appropriate.
// * The behavior will automatically dismount once its arrives at our safe spot.
#endregion


#region FAQs
// * I've set the AvoidDistance very low (say 20), but it takes a while before the behavior
//   decides the patrol is 'clear'.  Why?
//      A 'clear' patrol is determined by _two_ pieces of Battlefield state: the AvoidDistance
//      _and_ whether or the mob is facing you.  If the mob is facing you, it is assumed that
//      the mob is pathing toward you.  The behavior waits until the mob is pathing away
//      (i.e., no longer facing you) and beyond AvoidDistance before deciding
//      the mob is 'clear'.
//
// * I'm using a FollowPath, and the toon is running in after the patrol clears,
//   then running back to the safe spot.  What is wrong?
//      The behavior is configured incorrectly, and there are two solutions to this problem.
//      1) Incresae the value of AvoidDistance to the apex of the AvoidMobId's path (measured
//          from your safe spot.
//      2) Change the Strategy of the FollowPath to "StalkMobAtAvoidDistance".
//          This arranges to follow the AvoidMobId at a safe distance.
//
#endregion

#region Examples
// "Breaking Out is Hard to Do" (http://wowhead.com/quest=26587).
// The Blackrock Worg Captain (http://wowhead.com/43341) guards a key that is surrounded by 40 or so dogs.
// You must wait until the Captain clears, then thread your way through a very tight path
// to fetch the key.
// We use the "FollowPath" variant for this quest.  This allows us to safely move back to our 'safe spot'
// if the Captain gets too close while we're fetching the key.
//
//	    <CustomBehavior File="SpecificQuests\DeathknightStart\WaitForPatrol" AvoidMobId="43341" AvoidDistance="35"
//  		X="-9211.345" Y="-2775.24" Z="90.38371" >
//      	<FollowPath Strategy="StalkMobAtAvoidDistance" DismissPet="true" EgressDistance="25" >
//              <Hotspot X="-9202.807" Y="-2775.259" Z="90.38366" />
//              <Hotspot X="-9186.497" Y="-2770.601" Z="88.68089" />
//              <Hotspot X="-9165.448" Y="-2743.158" Z="88.74001" />
//          </FollowPath>
//      </CustomBehavior>
//
// "Grand Theft Palomino" (http://www.wowhead.com/quest=12680)
// This is one of the Death Knight 'starter' quests.  To complete, we must wait for 
// Stable Master Kitrik (http://wowhead.com/npc=28683) to be at a safe distance, before we
// walk in and steal the horse.
//
//      <CustomBehavior File="WaitForPatrol" AvoidMobId="28683" AvoidDistance="85"
//          X="2175.955" Y="-5769.175" Z="101.3312" />
// 
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Honorbuddy.QuestBehaviorCore.XmlElements;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.DeathknightStart.WaitForPatrol
{
    [CustomBehaviorFileName(@"SpecificQuests\DeathknightStart\WaitForPatrol")]
    public class WaitForPatrol : QuestBehaviorBase
    {
        #region Consructor and Argument Processing

        public WaitForPatrol(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                AvoidDistance = GetAttributeAsNullable<double>("AvoidDistance", true, ConstrainAs.Range, new[] { "Distance" }) ?? 20.0;
                MobIdToAvoid = GetAttributeAsNullable<int>("AvoidMobId", true, ConstrainAs.MobId, new[] { "MobId" }) ?? 0;
                MobIdToMoveNear = GetAttributeAsNullable<int>("MoveToMobId", false, ConstrainAs.MobId, new[] { "MoveToMobID" }) ?? 0;
                SafespotLocation = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;

                // Tunables...


                // Semantic coherency / covariant dependency checks --
                if ((MobIdToMoveNear == 0) && (SafespotLocation == WoWPoint.Empty))
                {
                    LogError("Either MoveToMobId or X/Y/Z (for the safe spot) must be specified.");
                    IsAttributeProblem = true;
                }
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                LogError("[MAINTENANCE PROBLEM]: " + except.Message
                        + "\nFROM HERE:\n"
                        + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }


        // Attributes provided by caller
        public double AvoidDistance { get; private set; }
        public int MobIdToAvoid { get; private set; }
        public int MobIdToMoveNear { get; private set; }
        public WoWPoint SafespotLocation { get; private set; }
        #endregion


        #region Private and Convenience variables

        private enum StateType_MainBehavior
        {
            MovingToSafespot,  // Initial state
            WaitingForMobToClear,
            FollowingPathToDestination,
            BehaviorDone,
        };

        private SafePathType FollowPath { get; set; }
        private WoWUnit Mob_ToAvoid { get; set; }
        private WoWUnit Mob_ToMoveNear { get; set; }
        private Queue<WaypointType> Path_Egress { get; set; }
        private Queue<WaypointType> Path_Ingress { get; set; }
        private StateType_MainBehavior State_MainBehavior
        {
            get { return _state_MainBehavior; }
            set
            {
                // For DEBUGGING...
                if (_state_MainBehavior != value)
                    { LogDeveloperInfo("State_MainBehavior: {0}", value); }

                _state_MainBehavior = value;
            }
        }

        private StateType_MainBehavior _state_MainBehavior;
        #endregion

        private static readonly TimeSpan ThrottleUserStatusUpdate = TimeSpan.FromSeconds(1);

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id: WaitForPatrol.cs 249 2012-09-19 01:31:37Z natfoth $"); } }
        public override string SubversionRevision { get { return ("$Revision: 249 $"); } }


        #region Destructor, Dispose, and cleanup
        ~WaitForPatrol()
        {
            Dispose(false);
        }
        #endregion


        #region Overrides of CustomForcedBehavior

        public override void OnStart()
        {
            // FollowPath processing...
            // NB: We had to defer this processing from the constructor, because XElement isn't available
            // to parse child XML nodes until OnStart() is called.
            FollowPath = SafePathType.GetOrCreate(Element, "FollowPath", AvoidDistance / 3.0, SafespotLocation);
            IsAttributeProblem |= FollowPath.IsAttributeProblem;

            // This reports problems, and stops BT processing if there was a problem with attributes...
            // We had to defer this action, as the 'profile line number' is not available during the element's
            // constructor call.
            OnStart_HandleAttributeProblem();

            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (!IsDone)
            {
                OnStart_BaseQuestBehavior();

                // Disable any settings that may interfere with the escort --
                // When we escort, we don't want to be distracted by other things.
                // NOTE: these settings are restored to their normal values when the behavior completes
                // or the bot is stopped.
                CharacterSettings.Instance.HarvestHerbs = false;
                CharacterSettings.Instance.HarvestMinerals = false;
                CharacterSettings.Instance.LootChests = false;
                CharacterSettings.Instance.NinjaSkin = false;
                CharacterSettings.Instance.SkinMobs = false;

                LogInfo("Moving to safe spot {0} until '{1}' moves {2:F1} yards away.",
                            SafespotLocation,
                            GetMobNameFromId(MobIdToAvoid),
                            AvoidDistance);

                State_MainBehavior = StateType_MainBehavior.MovingToSafespot;
            }
        }
        #endregion


        #region Main Behaviors
        protected override Composite CreateBehavior_CombatMain()
        {
            return new PrioritySelector(
                // If we are following the path to the destination...
                new Decorator(context => State_MainBehavior == StateType_MainBehavior.FollowingPathToDestination,
                    new PrioritySelector(
                        // If no path specified, we're done...
                        new Decorator(context => !FollowPath.Waypoints.Any(),
                            new Action(context => { State_MainBehavior = StateType_MainBehavior.BehaviorDone; })),

                        // If Mob_ToAvoid is too close, abandon current ingress, and find egress path back to safespot...
                        new Decorator(context => Mob_ToAvoid != null,
                            new Decorator(context => ((Mob_ToAvoid.Distance < FollowPath.EgressDistance) || Me.Combat)
                                                    && (Path_Egress == null),
                                new Action(context =>
                                {
                                    LogInfo("Moving back to safespot due to {0}.",
                                        Me.Combat
                                        ? "combat"
                                        : string.Format("{0} too close (dist: {1:F1})", Mob_ToAvoid.Name, Mob_ToAvoid.Distance));
                                    Path_Ingress = null;
                                    Path_Egress = FollowPath.FindPath_Egress(Mob_ToAvoid);
                                }))
                            ),

                        // If we are egressing, follow the Yellow Brick Road...
                        new Decorator(context => Path_Egress != null,
                            new PrioritySelector(
                                // If we've come to the end of our egress path, move back to safe spot...
                                new Decorator(context => !Path_Egress.Any(),
                                    new Action(context => { State_MainBehavior = StateType_MainBehavior.MovingToSafespot; })),

                                // If we've arriaved at the current waypoint, dequeue it...
                                new Decorator(context => Me.Location.Distance(Path_Egress.Peek().Location) <= Navigator.PathPrecision,
                                    new Action(context => { Path_Egress.Dequeue(); })),

                                UtilityBehaviorPS_MoveTo(context => Path_Egress.Peek().Location, context => "safe spot")
                            )),

                        // If we don't have a current ingress path to follow, build it...
                        new Decorator(context => ((Mob_ToAvoid == null) || (Mob_ToAvoid.Distance > AvoidDistance))
                                                && (Path_Ingress == null),
                            new Action(context =>
                            {
                                Path_Egress = null;
                                Path_Ingress = FollowPath.FindPath_Ingress();
                                FollowPath.DismissPetIfNeeded();
                            })),

                        // If we've an Ingress path to follow, use it...
                        new Decorator(context => Path_Ingress != null,
                            new PrioritySelector(
                                // If we've consumed our ingress path, we're done...
                                new Decorator(context => !Path_Ingress.Any(),
                                    new Action(context => { State_MainBehavior = StateType_MainBehavior.BehaviorDone; })),

                                new Switch<SafePathType.StrategyType>(context => FollowPath.Strategy,
                                    #region State: DEFAULT
                                    new Action(context =>   // default case
                                    {
                                        LogMaintenanceError("FollowPathStrategyType({0}) is unhandled", FollowPath.Strategy);
                                        TreeRoot.Stop();
                                        State_MainBehavior = StateType_MainBehavior.BehaviorDone;
                                    }),
                                    #endregion


                                    #region Strategy: Stalk Mob at Avoid Distance Strategy
                                    new SwitchArgument<SafePathType.StrategyType>(SafePathType.StrategyType.StalkMobAtAvoidDistance,
                                        new Decorator(context => (Mob_ToAvoid != null) && (Mob_ToAvoid.Distance < AvoidDistance),
                                            new PrioritySelector(
                                                new Decorator(context => Me.IsMoving,
                                                    new Action(context => { WoWMovement.MoveStop(); })),
                                                new ActionAlwaysSucceed()
                                            ))),
                                    #endregion


                                    #region Strategy: Wait for Avoid Distance
                                    new SwitchArgument<SafePathType.StrategyType>(SafePathType.StrategyType.WaitForAvoidDistance,
                                        new PrioritySelector(
                                            // No addition action needed to implement strategy for now
                                        ))
                                    #endregion
                                ),

                                // If we've arrived at the current ingress waypoint, dequeue it...
                                new Decorator(context => Me.Location.Distance(Path_Ingress.Peek().Location) <= Navigator.PathPrecision,
                                    new Action(context => { Path_Ingress.Dequeue(); })),

                                // Follow the prescribed ingress path...
                                UtilityBehaviorPS_MoveTo(context => Path_Ingress.Peek().Location, context => "follow ingress path")
                            ))
                    ))
            );
        }


        protected override Composite CreateBehavior_CombatOnly()
        {
            return new PrioritySelector(
                // NB: Since we highlight the target we're watching while we're waiting...
                // If we get into combat, we don't Honorbuddy running off to chase the currently highlighted
                // target (its usually an elite).  So, if the selected target not attacking us, we clear the target,
                // so Honorbuddy can make a proper target selection.
                new Decorator(context => (Me.CurrentTarget != null) && !Me.CurrentTarget.IsTargetingMeOrPet,
                    new Action(context => { Me.ClearTarget(); })),

                // If we get in combat while waiting for Mob to clear, move back to safespot when combat complete...
                new Decorator(context => State_MainBehavior == StateType_MainBehavior.WaitingForMobToClear,
                    new Action(context =>
                    {
                        State_MainBehavior = StateType_MainBehavior.MovingToSafespot;
                        return RunStatus.Failure;
                    }))
                );
        }


        protected override Composite CreateBehavior_DeathMain()
        {
            return new PrioritySelector(
                // empty, for now
                );
        }


        protected override Composite CreateMainBehavior()
        {
            return new PrioritySelector(

                // Update information for this BT visit...
                new Action(context =>
                {
                    Mob_ToAvoid = FindUnitsFromIds(ToEnumerable<int>(MobIdToAvoid)).FirstOrDefault();
                    Mob_ToMoveNear = FindUnitsFromIds(ToEnumerable<int>(MobIdToMoveNear)).FirstOrDefault();
                    return RunStatus.Failure;  // fall thru
                }),


                // If quest is done, behavior is done...
                new Decorator(context => IsDone,
                    new Action(context => { State_MainBehavior = StateType_MainBehavior.BehaviorDone; })),


                // Stateful Operation:
                new Switch<StateType_MainBehavior>(context => State_MainBehavior,
                    #region State: DEFAULT
                    new Action(context =>   // default case
                    {
                        LogMaintenanceError("StateType_MainBehavior({0}) is unhandled", State_MainBehavior);
                        TreeRoot.Stop();
                        State_MainBehavior = StateType_MainBehavior.BehaviorDone;
                    }),
                    #endregion


                    #region State: Moving to Safespot
                    new SwitchArgument<StateType_MainBehavior>(StateType_MainBehavior.MovingToSafespot,
                        new PrioritySelector(
                            // If a "Move Near" mob was specified, move to it...
                            new Decorator(context => MobIdToMoveNear != 0,
                                new PrioritySelector(
                                    new Decorator(context => Mob_ToMoveNear != null,
                                        new PrioritySelector(
                                            // Target the MoveToNpc, as feedback to the user...
                                            new Decorator(context => Me.CurrentTarget != Mob_ToMoveNear,
                                                new Action(context => { Mob_ToMoveNear.Target(); })),

                                            // Move to mob...
                                            UtilityBehaviorPS_MoveTo(context => Mob_ToMoveNear.Location, context => Mob_ToMoveNear.Name)
                                        )),

                                    // Need to wait for Mob to respawn...
                                    new Decorator(context => Mob_ToMoveNear == null,
                                        new Action(context =>
                                        {
                                            TreeRoot.StatusText = string.Format("Waiting for {0} to respawn", GetMobNameFromId(MobIdToMoveNear));
                                        }))
                                )),

                            // No "Move Near" mob, so use the provided Safe spot coordinates...
                            new Decorator(context => MobIdToMoveNear == 0,
                                UtilityBehaviorPS_MoveTo(context => SafespotLocation, context => "to safespot")),

                            // Dismount once we've arrived at mob...
                            new Decorator(context => Me.Mounted,
                                new Action(context => { Mount.Dismount(); })),

                            // At safe spot, now wait for mob...
                            new Action(context =>
                            {
                                TreeRoot.StatusText = string.Empty;
                                State_MainBehavior = StateType_MainBehavior.WaitingForMobToClear; 
                            })
                        )),
                    #endregion


                    #region State: Waiting for Mob to Clear
                    new SwitchArgument<StateType_MainBehavior>(StateType_MainBehavior.WaitingForMobToClear,
                        new PrioritySelector(
                            // If AvoidNpc is not around,
                            // or if AvoidNpc is prescribed distance away, and facing away from us,
                            // we're done...
                            new Decorator(context => IsSafeToMoveToDestination(Mob_ToAvoid),
                                new Action(context =>
                                {
                                    Me.ClearTarget();
                                    Path_Ingress = null;
                                    Path_Egress = null;
                                    State_MainBehavior = StateType_MainBehavior.FollowingPathToDestination;
                                })),

                            // Target and Face the AvoidNpc, as feedback to the user...
                            new Decorator(context => Me.CurrentTarget != Mob_ToAvoid,
                                new Action(context => { Mob_ToAvoid.Target(); })),
                            new Decorator(context => !Me.IsSafelyFacing(Mob_ToAvoid),
                                new Action(context => { Mob_ToAvoid.Face(); })),

                            // Tell user what we're up to...
                            new CompositeThrottle(ThrottleUserStatusUpdate,
                                new Action(context =>
                                {
                                    TreeRoot.StatusText = string.Format(
                                                            "Waiting for '{0}' (dist: {1:F1}) to move {2:F1} yards away, and pathing away from us.",
                                                            Mob_ToAvoid.Name,
                                                            Mob_ToAvoid.Distance,
                                                            AvoidDistance);
                                }))
                        )),
                    #endregion

                        
                    #region State: Following Path to Destination
                    new SwitchArgument<StateType_MainBehavior>(StateType_MainBehavior.FollowingPathToDestination,
                        new PrioritySelector(
                            // The "Follow Path to Destination" logic is handled in a Combat_Main hook.
                            // If we get into a fight, and need to back out, we want to be certain we drag the mobs
                            // back to the safespot before engaging them.  That cannot be done in the normal (non-combat)
                            // main behavior, here.
                        )),
                    #endregion

                        
                    #region State: Behavior Done
                    new SwitchArgument<StateType_MainBehavior>(StateType_MainBehavior.BehaviorDone,
                        new PrioritySelector(
                            new Action(context =>
                            {
                                Me.ClearTarget();
                                BehaviorDone();
                            })
                        ))
                    #endregion
 
            ));
        }
        #endregion


        #region Helpers
        private bool IsSafeToMoveToDestination(WoWUnit avoidNpc)
        {
            // Its safe to move when:
            // * avoidNpc is not around
            // * avoidNpc is not in combat, and its the prescribed distance away, and facing away from us.
            // NB: We use the 'combat' check because if the avoidNpc is in combat, he situation of 'safe to move'
            // can rapidly change.  This usually happens when the NPC is busy killing another player.
            return !IsViable(avoidNpc)
                    || (!avoidNpc.Combat
                        && (avoidNpc.Distance > AvoidDistance)
                        && !avoidNpc.IsSafelyFacing(Me));
        }
        #endregion
    }
}
