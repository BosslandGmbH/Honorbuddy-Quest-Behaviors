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
using System.Diagnostics;
using System.Linq;

using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using System.Xml.Linq;

using Action = Styx.TreeSharp.Action;
#endregion


namespace QuestBehaviors.WaitForPatrol
{
    public class WaitForPatrol : CustomForcedBehavior
    {
        #region Consructor and Argument Processing

        public enum FollowPathStrategyType
        {
            WaitForAvoidDistance,
            StalkMobAtAvoidDistance
        }

        public enum MovementByType
        {
            ClickToMoveOnly,
            NavigatorOnly,
            NavigatorPreferred,
        }


        public WaitForPatrol(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                AvoidDistance = GetAttributeAsNullable<double>("AvoidDistance", true, ConstrainAs.Range, new[] { "Distance" }) ?? 20.0;
                MobId_ToAvoid = GetAttributeAsNullable<int>("AvoidMobId", true, ConstrainAs.MobId, new[] { "MobId" }) ?? 0;
                MobId_ToMoveNear = GetAttributeAsNullable<int>("MoveToMobId", false, ConstrainAs.MobId, new[] { "MoveToMobID" }) ?? 0;
                SafespotLocation = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;

                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;

                // Tunables...
                MovementBy = GetAttributeAsNullable<MovementByType>("MovementBy", false, null, null) ?? MovementByType.NavigatorPreferred;

                // Semantic coherency / covariant dependency checks --
                if ((MobId_ToMoveNear == 0) && (SafespotLocation == WoWPoint.Empty))
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
        public int MobId_ToAvoid { get; private set; }
        public int MobId_ToMoveNear { get; private set; }
        private MovementByType MovementBy { get; set; }
        public WoWPoint SafespotLocation { get; private set; }

        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }
        #endregion


        #region Private and Convenience variables
        public delegate WoWPoint LocationDelegate(object context);
        public delegate string MessageDelegate(object context);
        public delegate double RangeDelegate(object context);

        private enum StateType_MainBehavior
        {
            MovingToSafespot,  // Initial state
            WaitingForMobToClear,
            FollowingPathToDestination,
            BehaviorDone,
        };

        public bool FollowPathDismissPet { get; set; }
        private double FollowPathEgressDistance { get; set; }
        public FollowPathStrategyType FollowPathStrategy { get; set; }
        private readonly TimeSpan Delay_WoWClientMovementThrottle = TimeSpan.FromMilliseconds(100);
        private LocalPlayer Me { get { return (StyxWoW.Me); } }
        private WoWUnit Mob_ToAvoid { get; set; }
        private WoWUnit Mob_ToMoveNear { get; set; }
        private Queue<WoWPoint> Path_Egress { get; set; }
        private Queue<WoWPoint> Path_Ingress { get; set; }
        private StateType_MainBehavior State_MainBehavior
        {
            get { return _state_MainBehavior; }
            set
            {
                // For DEBUGGING...
                //if (_state_MainBehavior != value)
                //    { LogMessage("info", "State_MainBehavior: {0}", value); }

                _state_MainBehavior = value;
            }
        }


        private Composite _behaviorTreeHook_CombatMain = null;
        private Composite _behaviorTreeHook_CombatOnly = null;
        private Composite _behaviorTreeHook_DeathMain = null;
        private Composite _behaviorTreeHook_Main = null;
        private ConfigMemento _configMemento = null;
        private IEnumerable<WoWPoint> _followPath = null;
        private bool _isBehaviorDone;
        private bool _isDisposed;
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

                // NB: we don't unhook _behaviorTreeHook_Main
                // This was installed when HB created the behavior, and its up to HB to unhook it

                if (_behaviorTreeHook_CombatMain != null)
                {
                    TreeHooks.Instance.RemoveHook("Combat_Main", _behaviorTreeHook_CombatMain);
                    _behaviorTreeHook_CombatMain = null;
                }

                if (_behaviorTreeHook_CombatOnly != null)
                {
                    TreeHooks.Instance.RemoveHook("Combat_Only", _behaviorTreeHook_CombatOnly);
                    _behaviorTreeHook_CombatOnly = null;
                }

                if (_behaviorTreeHook_DeathMain != null)
                {
                    TreeHooks.Instance.RemoveHook("Death_Main", _behaviorTreeHook_DeathMain);
                    _behaviorTreeHook_DeathMain = null;
                }

                // Restore configuration...
                if (_configMemento != null)
                {
                    _configMemento.Dispose();
                    _configMemento = null;
                }

                BotEvents.OnBotStop -= BotEvents_OnBotStop;

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
        #endregion


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _behaviorTreeHook_Main ?? (_behaviorTreeHook_Main = CreateMainBehavior());
        }


        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
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
            ParseFollowPath("FollowPath");
            _followPath = ParsePath("FollowPath");

            if (FollowPathEgressDistance >= AvoidDistance)
            {
                LogError("EgressDistance({0:F1}) must be less than AvoidDistance({1:F1})",
                    FollowPathEgressDistance, AvoidDistance);
                IsAttributeProblem = true;
            }

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

                BotEvents.OnBotStop += BotEvents_OnBotStop;

                // Disable any settings that may interfere with the escort --
                // When we escort, we don't want to be distracted by other things.
                // NOTE: these settings are restored to their normal values when the behavior completes
                // or the bot is stopped.
                CharacterSettings.Instance.HarvestHerbs = false;
                CharacterSettings.Instance.HarvestMinerals = false;
                CharacterSettings.Instance.LootChests = false;
                CharacterSettings.Instance.NinjaSkin = false;
                CharacterSettings.Instance.SkinMobs = false;
                
                PlayerQuest quest = Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = GetType().Name + ": " + ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");

                LogMessage("info", "Moving to safe spot {0} until '{1}' moves {2:F1} yards away.",
                            SafespotLocation,
                            GetMobNameFromId(MobId_ToAvoid),
                            AvoidDistance);

                State_MainBehavior = StateType_MainBehavior.MovingToSafespot;

                _behaviorTreeHook_CombatMain = CreateBehavior_CombatMain();
                TreeHooks.Instance.InsertHook("Combat_Main", 0, _behaviorTreeHook_CombatMain);
                _behaviorTreeHook_CombatOnly = CreateBehavior_CombatOnly();
                TreeHooks.Instance.InsertHook("Combat_Only", 0, _behaviorTreeHook_CombatOnly);
                _behaviorTreeHook_DeathMain = CreateBehavior_DeathMain();
                TreeHooks.Instance.InsertHook("Death_Main", 0, _behaviorTreeHook_DeathMain);
            }
        }
        #endregion


        #region Main Behaviors
        private Composite CreateBehavior_CombatMain()
        {
            return new PrioritySelector(
                // If we are following the path to the destination...
                new Decorator(context => State_MainBehavior == StateType_MainBehavior.FollowingPathToDestination,
                    new PrioritySelector(
                        // If no path specified, we're done...
                        new Decorator(context => _followPath.Count() <= 0,
                            new Action(context => { State_MainBehavior = StateType_MainBehavior.BehaviorDone; })),

                        // If Mob_ToAvoid is too close, abandon current ingress, and find egress path back to safespot...
                        new Decorator(context => Mob_ToAvoid != null,
                            new Decorator(context => ((Mob_ToAvoid.Distance < FollowPathEgressDistance) || Me.Combat)
                                                    && (Path_Egress == null),
                                new Action(context =>
                                {
                                    LogMessage("info", "Moving back to safespot due to {0}.",
                                        Me.Combat
                                        ? "combat"
                                        : string.Format("{0} too close (dist: {1:F1})", Mob_ToAvoid.Name, Mob_ToAvoid.Distance));
                                    Path_Ingress = null;
                                    Path_Egress = FindEgressPath();
                                }))
                            ),

                        // If we are egressing, follow the Yellow Brick Road...
                        new Decorator(context => Path_Egress != null,
                            new PrioritySelector(
                                // If we've come to the end of our egress path, move back to safe spot...
                                new Decorator(context => Path_Egress.Count() == 0,
                                    new Action(context => { State_MainBehavior = StateType_MainBehavior.MovingToSafespot; })),

                                // If we've arriaved at the current waypoint, dequeue it...
                                new Decorator(context => Me.Location.Distance(Path_Egress.Peek()) <= Navigator.PathPrecision,
                                    new Action(context => { Path_Egress.Dequeue(); })),

                                UtilityBehavior_MoveTo(context => Path_Egress.Peek(), context => "back to safe spot")
                            )),

                        // If we don't have a current ingress path to follow, build it...
                        new Decorator(context => (Mob_ToAvoid.Distance > AvoidDistance)
                                                && (Path_Ingress == null),
                            new Action(context =>
                            {
                                Path_Egress = null;
                                Path_Ingress = FindIngressPath();
                                DismissPet();
                            })),

                        // If we've consumed our ingress path, we're done...
                        new Decorator(context => Path_Ingress.Count() <= 0,
                            new Action(context => { State_MainBehavior = StateType_MainBehavior.BehaviorDone; })),

                        new Switch<FollowPathStrategyType>(context => FollowPathStrategy,
                            #region State: DEFAULT
                            new Action(context =>   // default case
                            {
                                LogMaintenanceError("FollowPathStrategyType({0}) is unhandled", FollowPathStrategy);
                                TreeRoot.Stop();
                                State_MainBehavior = StateType_MainBehavior.BehaviorDone;
                            }),
                            #endregion


                            #region Strategy: Stalk Mob at Avoid Distance Strategy
                            new SwitchArgument<FollowPathStrategyType>(FollowPathStrategyType.StalkMobAtAvoidDistance,
                                new Decorator(context => Mob_ToAvoid.Distance < AvoidDistance,
                                    new PrioritySelector(
                                        new Decorator(context => Me.IsMoving,
                                            new Action(context => { WoWMovement.MoveStop(); })),
                                        new ActionAlwaysSucceed()
                                    ))),
                            #endregion


                            #region Strategy: Wait for Avoid Distance
                            new SwitchArgument<FollowPathStrategyType>(FollowPathStrategyType.WaitForAvoidDistance,
                                new PrioritySelector(
                                    // No addition action needed to implement strategy for now
                                ))
                            #endregion
                        ),

                        // If we've arrived at the current ingress waypoint, dequeue it...
                        new Decorator(context => Me.Location.Distance(Path_Ingress.Peek()) <= Navigator.PathPrecision,
                            new Action(context => { Path_Ingress.Dequeue(); })),

                        // Follow the prescribed ingress path...
                        UtilityBehavior_MoveTo(context => Path_Ingress.Peek(), context => "to follow ingress path")
                    ))
            );
        }


        private Composite CreateBehavior_CombatOnly()
        {
            return new PrioritySelector(
                // If we get in combat while waiting for Mob to clear, move back to safespot when combat complete...
                new Decorator(context => State_MainBehavior == StateType_MainBehavior.WaitingForMobToClear,
                    new Action(context =>
                    {
                        State_MainBehavior = StateType_MainBehavior.MovingToSafespot;
                        return RunStatus.Failure;
                    }))
                );
        }


        private Composite CreateBehavior_DeathMain()
        {
            return new PrioritySelector(
                // empty, for now
                );
        }


        private Composite CreateMainBehavior()
        {
            return new PrioritySelector(

                // Update information for this BT visit...
                new Action(context =>
                {
                    Mob_ToAvoid = FindUnitsFromIds(MobId_ToAvoid).FirstOrDefault();
                    Mob_ToMoveNear = FindUnitsFromIds(MobId_ToMoveNear).FirstOrDefault();
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
                            new Decorator(context => MobId_ToMoveNear != 0,
                                new PrioritySelector(
                                    new Decorator(context => Mob_ToMoveNear != null,
                                        new PrioritySelector(
                                            // Target the MoveToNpc, as feedback to the user...
                                            new Decorator(context => Me.CurrentTarget != Mob_ToMoveNear,
                                                new Action(context => { Mob_ToMoveNear.Target(); })),

                                            // Move to mob...
                                            UtilityBehavior_MoveTo(context => Mob_ToMoveNear.Location, context => Mob_ToMoveNear.Name)
                                        )),

                                    // Need to wait for Mob to respawn...
                                    new Decorator(context => Mob_ToMoveNear == null,
                                        new Action(context =>
                                        {
                                            TreeRoot.StatusText = string.Format("Waiting for {0} to respawn", GetMobNameFromId(MobId_ToMoveNear));
                                        }))
                                )),

                            // No "Move Near" mob, so use the provided Safe spot coordinates...
                            new Decorator(context => MobId_ToMoveNear == 0,
                                UtilityBehavior_MoveTo(context => SafespotLocation, context => "to safespot")),

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
                            // If AvoidNpc is not around, we're done...
                            new Decorator(context => Mob_ToAvoid == null,
                                new Action(context =>
                                {
                                    Me.ClearTarget();
                                    Path_Ingress = null;
                                    Path_Egress = null;
                                    State_MainBehavior = StateType_MainBehavior.FollowingPathToDestination;
                                })),

                            // If AvoidNpc is prescribed distance away, and facing away from us, we're done...
                            new Decorator(context => (Mob_ToAvoid.Distance > AvoidDistance) && !Mob_ToAvoid.IsFacing(Me),
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
                                LogMessage("info", "Finished");
                                Me.ClearTarget();
                                _isBehaviorDone = true;
                            })
                        ))
                    #endregion
 
            ));
        }
        #endregion


        #region Helpers
        private void DismissPet()
        {
            if (FollowPathDismissPet && Me.GotAlivePet)
                { Lua.DoString("DismissPet()"); }
        }


        private Queue<WoWPoint> FindEgressPath()
        {
            List<WoWPoint> theWayOut = new List<WoWPoint>(_followPath);

            theWayOut.Reverse();

            WoWPoint egressStartPoint =
                (from point in theWayOut
                    let mobDistanceToPoint = point.Distance(Mob_ToAvoid.Location)
                    let myDistanceToPoint = point.Distance(Me.Location)
                where
                    myDistanceToPoint < mobDistanceToPoint
                orderby
                    myDistanceToPoint
                select point)
                .FirstOrDefault();

            while (theWayOut[0] != egressStartPoint)
                { theWayOut.RemoveAt(0); }

            return new Queue<WoWPoint>(theWayOut);
        }


        private Queue<WoWPoint> FindIngressPath()
        {
            List<WoWPoint> theWayIn = new List<WoWPoint>(_followPath);

            theWayIn.Reverse();

            WoWPoint ingressStartPoint =
                (from point in theWayIn
                    let myDistanceToPoint = point.Distance(Me.Location)
                orderby
                    myDistanceToPoint
                select point)
                .FirstOrDefault();

            while (theWayIn[0] != ingressStartPoint)
                { theWayIn.RemoveAt(0); }

            return new Queue<WoWPoint>(_followPath);
        }


        private IEnumerable<WoWUnit> FindUnitsFromIds(params int[] unitIds)
        {
            ContractRequires(unitIds != null, () => "unitIds argument may not be null");

            return
                from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
                where
                    unit.IsValid
                    && unit.IsAlive
                    && unitIds.Contains((int)unit.Entry)
                    && (unit.TappedByAllThreatLists || !unit.TaggedByOther)
                select unit;
        }


        private string GetMobNameFromId(int wowUnitId)
        {
            WoWUnit wowUnit = FindUnitsFromIds(wowUnitId).FirstOrDefault();

            return (wowUnit != null)
                ? wowUnit.Name
                : string.Format("MobId({0})", wowUnitId);
        }


        /// <returns>RunStatus.Success while movement is in progress; othwerise, RunStatus.Failure if no movement necessary</returns>
        private Composite UtilityBehavior_MoveTo(LocationDelegate locationDelegate,
                                                MessageDelegate locationNameDelegate,
                                                RangeDelegate precisionDelegate = null)
        {
            ContractRequires(locationDelegate != null, () => "locationRetriever may not be null");
            ContractRequires(locationNameDelegate != null, () => "locationNameDelegate may not be null");
            precisionDelegate = precisionDelegate ?? (context => Navigator.PathPrecision);

            return new PrioritySelector(locationContext => locationDelegate(locationContext),
                new Decorator(locationContext => !Me.Mounted
                                                    && Mount.CanMount()
                                                    && Mount.ShouldMount((WoWPoint)locationContext),
                    new Action(locationContext => { Mount.MountUp(() => (WoWPoint)locationContext); })),

                new Decorator(locationContext => (Me.Location.Distance((WoWPoint)locationContext) > precisionDelegate(locationContext)),
                    new Sequence(
                        new Action(locationContext =>
                        {
                            WoWPoint destination = (WoWPoint)locationContext;
                            string locationName = locationNameDelegate(locationContext) ?? destination.ToString();
                            MoveResult moveResult = MoveResult.Failed;

                            TreeRoot.StatusText = "Moving " + locationName;

                            // Use Navigator to get there, if allowed...
                            if ((MovementBy == MovementByType.NavigatorPreferred) || (MovementBy == MovementByType.NavigatorOnly))
                            {
                                if (!Me.IsSwimming)
                                    { moveResult = Navigator.MoveTo(destination); }
                            }

                            // If Navigator fails, fall back to click-to-move...
                            if ((moveResult == MoveResult.Failed) || (moveResult == MoveResult.PathGenerationFailed))
                            {
                                if (MovementBy == MovementByType.NavigatorOnly)
                                {
                                    LogMessage("warning", "Failed to move--is area unmeshed?");
                                    return RunStatus.Failure;
                                }

                                WoWMovement.ClickToMove(destination);
                            }

                            return RunStatus.Success;
                        }),

                        new WaitContinue(Delay_WoWClientMovementThrottle, ret => false, new ActionAlwaysSucceed())
                    ))
                );
        }
        #endregion


        #region XML parsing
        public void ParseFollowPath(string xmlElementName)
        {
            XElement element = Element.Elements(xmlElementName).FirstOrDefault();

            if (element != null)
            {
                XAttribute xDismissPet = element.Attribute("DismissPet");
                XAttribute xEgressDistance = element.Attribute("EgressDistance");
                XAttribute xStrategy = element.Attribute("Strategy");

                try
                {
                    FollowPathDismissPet = 
                        (xDismissPet != null)
                        ? UtilTo<bool>(xDismissPet.Name.ToString(), xDismissPet.Value)
                        : false;
                }
                catch(Exception) { IsAttributeProblem = true; }

                try
                {
                    FollowPathEgressDistance =
                        (xEgressDistance != null)
                        ? UtilTo<double>(xEgressDistance.Name.ToString(), xEgressDistance.Value)
                        : (AvoidDistance / 3.0);
                }
                catch(Exception) { IsAttributeProblem = true; }

                try
                {
                    FollowPathStrategy =
                        (xStrategy != null)
                        ? UtilTo<FollowPathStrategyType>(xStrategy.Name.ToString(), xStrategy.Value)
                        : FollowPathStrategyType.StalkMobAtAvoidDistance;
                }
                catch(Exception) { IsAttributeProblem = true; }
            }
        }

        // never returns null, but the returned Queue may be empty
        public Queue<WoWPoint> ParsePath(string pathElementName)
        {
            var descendants = Element.Descendants(pathElementName).Elements();
            Queue<WoWPoint> path = new Queue<WoWPoint>();

            if (descendants.Count() > 0)
            {
                foreach (XElement element in descendants.Where(elem => elem.Name == "Hotspot"))
                {
                    string elementAsString = element.ToString();
                    bool isAttributeMissing = false;

                    XAttribute xAttribute = element.Attribute("X");
                    if (xAttribute == null)
                    {
                        LogMessage("error", "Unable to locate X attribute for {0}", elementAsString);
                        isAttributeMissing = true;
                    }

                    XAttribute yAttribute = element.Attribute("Y");
                    if (yAttribute == null)
                    {
                        LogMessage("error", "Unable to locate Y attribute for {0}", elementAsString);
                        isAttributeMissing = true;
                    }

                    XAttribute zAttribute = element.Attribute("Z");
                    if (zAttribute == null)
                    {
                        LogMessage("error", "Unable to locate Z attribute for {0}", elementAsString);
                        isAttributeMissing = true;
                    }

                    if (isAttributeMissing)
                    {
                        IsAttributeProblem = true;
                        continue;
                    }

                    bool isParseProblem = false;

                    double x = 0.0;
                    if (!double.TryParse(xAttribute.Value, out x))
                    {
                        LogMessage("error", "Unable to parse X attribute for {0}", elementAsString);
                        isParseProblem = true;
                    }

                    double y = 0.0;
                    if (!double.TryParse(yAttribute.Value, out y))
                    {
                        LogMessage("error", "Unable to parse Y attribute for {0}", elementAsString);
                        isParseProblem = true;
                    }

                    double z = 0.0;
                    if (!double.TryParse(zAttribute.Value, out z))
                    {
                        LogMessage("error", "Unable to parse Z attribute for {0}", elementAsString);
                        isParseProblem = true;
                    }

                    if (isParseProblem)
                    {
                        IsAttributeProblem = true;
                        continue;
                    }

                    path.Enqueue(new WoWPoint(x, y, z));
                }
            }

            return path;
        }


        /// <summary>
        /// Converts the provide TEXT into the requested type.  If the conversion fails,
        /// an exception is thrown.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="attributeValueAsString"></param>
        /// <returns>the type-specific representation of the provided string, or
        /// 'null' if the string was malformed.</returns>
        /// <throw>InvalidCastException</throw>
        /// <throw>FormatException</throw>
        /// <throw>OverflowException</throw>
        /// <throw>ArgumentNullException</throw>
        private T         UtilTo<T>(string  attributeName,
                                    string  attributeValueAsString)
        {
            Type    concreteType    = typeof(T);

            // Booleans require special handling...
            if (concreteType == typeof(bool))
            {
                int tmpInt;

                if (int.TryParse(attributeValueAsString, out tmpInt))
                {
                    attributeValueAsString = (tmpInt != 0) ? "true" : "false";

                    LogWarning("Attribute's '{0}' value was provided as an integer (saw '{1}')--a boolean was expected.\n"
                                + "The integral value '{1}' was converted to Boolean({2}).\n"
                                + "Please update the profile to provide '{2}' for this value.",
                                attributeName,
                                tmpInt,
                                attributeValueAsString);
                }

                // Fall through for normal boolean conversion
            }


            // Enums require special handling...
            else if (concreteType.IsEnum)
            {
                // Enums require special handling...
                bool    isConversionSuccess = true;
                T       tmpValue            = default(T);

                try                 { tmpValue = (T)Enum.Parse(concreteType, attributeValueAsString, true); }
                catch (Exception)   { isConversionSuccess = false; }
                
                if (isConversionSuccess && Enum.IsDefined(concreteType, tmpValue))
                {
                    int tmpInt;

                    // If the provided value was a number instead of Enum name, ask the profile writer to fix it...
                    // This is not fatal, so we let it go without flagging IsAttributeProblem.
                    if (int.TryParse(attributeValueAsString, out tmpInt))
                    {
                        LogWarning("The '{0}' attribute's value '{1}' has been implicitly converted"
                                    + " to the corresponding enumeration '{2}'.\n"
                                    + "Please use the enumeration name '{2}' instead of a number.",
                                    attributeName,
                                    tmpInt,
                                    tmpValue.ToString());
                    }
                    
                    return (tmpValue);
                }

                LogError("The value '{0}' is not a member of the {1} enumeration.  Allowed values: {2}",
                    attributeValueAsString, concreteType.Name,
                    string.Join(", ", Enum.GetValues(typeof(T)).Cast<T>().Select(v => v.ToString())));
                return (default(T));
            }


            try
                { return ((T)Convert.ChangeType(attributeValueAsString, concreteType)); }
            catch (Exception except)
            {
                LogError("The '{0}' attribute's value (saw '{1}') is malformed. ({2})",
                    attributeName, attributeValueAsString, except.GetType().Name);
                throw;
            }
        }
        #endregion


        #region Diagnostic Methods
        // These are needed by a number of the pre-supplied methods...
        public delegate bool    ContractPredicateDelegate();
        public delegate string  StringProviderDelegate();

        /// <summary>
        /// <para>This is an efficent poor man's mechanism for reporting contract violations in methods.</para>
        /// <para>If the provided ISCONTRACTOKAY evaluates to true, no action is taken.
        /// If ISCONTRACTOKAY is false, a diagnostic message--given by the STRINGPROVIDERDELEGATE--is emitted to the log, along with a stack trace.</para>
        /// <para>This emitted information can then be used to locate and repair the code misusing the interface.</para>
        /// <para>For convenience, this method returns the evaluation if ISCONTRACTOKAY.</para>
        /// <para>Notes:<list type="bullet">
        /// <item><description><para> * The interface is built in terms of a StringProviderDelegate,
        /// so we don't pay a performance penalty to build an error message that is not used
        /// when ISCONTRACTOKAY is true.</para></description></item>
        /// <item><description><para> * The .NET 4.0 Contract support is insufficient due to the way Buddy products
        /// dynamically compile parts of the project at run time.</para></description></item>
        /// </list></para>
        /// </summary>
        /// <param name="isContractOkay"></param>
        /// <param name="stringProviderDelegate"></param>
        /// <returns>the evaluation of the provided ISCONTRACTOKAY predicate delegate</returns>
        ///  30Jun2012-15:58UTC chinajade
        ///  NB: We could provide a second interface to ContractRequires() that is slightly more convenient for static string use.
        ///  But *please* don't!  If helps maintainers to not make mistakes if they see the use of this interface consistently
        ///  throughout the code.
        public bool ContractRequires(bool isContractOkay, StringProviderDelegate stringProviderDelegate)
        {
            if (!isContractOkay)
            {
                // TODO: (Future enhancement) Build a string representation of isContractOkay if stringProviderDelegate is null
                string      message = stringProviderDelegate() ?? "NO MESSAGE PROVIDED";
                StackTrace  trace   = new StackTrace(1);

                LogMessage("error", "[CONTRACT VIOLATION] {0}\nLocation:\n{1}",
                                        message, trace.ToString());
            }

            return isContractOkay;
        }


        /// <summary>
        /// <para>Returns the name of the method that calls this function. If SHOWDECLARINGTYPE is true,
        /// the scoped method name is returned; otherwise, the undecorated name is returned.</para>
        /// <para>This is useful when emitting log messages.</para>
        /// </summary>
        /// <para>Notes:<list type="bullet">
        /// <item><description><para> * This method uses reflection--making it relatively 'expensive' to call.
        /// Use it with caution.</para></description></item>
        /// </list></para>
        /// <returns></returns>
        ///  7Jul2012-20:26UTC chinajade
        public static string    GetMyMethodName(bool  showDeclaringType   = false)
        {
            var method  = (new StackTrace(1)).GetFrame(0).GetMethod();

            if (showDeclaringType)
                { return (method.DeclaringType + "." + method.Name); }

            return (method.Name);
        }


        /// <summary>
        /// <para>For DEBUG USE ONLY--don't use in production code! (Almost exclusively used by DebuggingTools methods.)</para>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public void LogDeveloperInfo(string message, params object[] args)
        {
            LogMessage("debug", message, args);
        }
        
        
        /// <summary>
        /// <para>Error situations occur when bad data/input is provided, and no corrective actions can be taken.</para>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public void LogError(string message, params object[] args)
        {
            LogMessage("error", message, args);
        }
        
        
        /// <summary>
        /// MaintenanceErrors occur as a result of incorrect code maintenance.  There is usually no corrective
        /// action a user can perform in the field for these types of errors.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        ///  30Jun2012-15:58UTC chinajade
        public void LogMaintenanceError(string message, params object[] args)
        {
            string          formattedMessage    = string.Format(message, args);
            StackTrace      trace               = new StackTrace(1);

            LogMessage("error", "[MAINTENANCE ERROR] {0}\nLocation:\n{1}", formattedMessage, trace.ToString());
        }


        /// <summary>
        /// <para>Used to notify of problems where corrective (fallback) actions are possible.</para>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public void LogWarning(string message, params object[] args)
        {
            LogMessage("warning", message, args);
        }
        #endregion
    }


    public class CompositeThrottle : DecoratorContinue
    {
        public CompositeThrottle(TimeSpan throttleTime,
                                 Composite composite)
            : base(composite)
        {
            _hasRunOnce = false;
            _throttle = new Stopwatch();
            _throttleTime = throttleTime;

            _throttle.Reset();
            _throttle.Start();
        }


        protected override bool CanRun(object context)
        {
            if (_hasRunOnce && (_throttle.Elapsed < _throttleTime))
            { return (false); }

            _hasRunOnce = true;
            _throttle.Reset();
            _throttle.Start();

            return (true);
        }

        private bool _hasRunOnce;
        private readonly Stopwatch _throttle;
        private readonly TimeSpan _throttleTime;
    }
}
