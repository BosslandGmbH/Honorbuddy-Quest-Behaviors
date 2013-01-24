// WORK IN PROGRESS -- WON'T COMPILE, UNTESTED, DON'T USE YET


// Behavior originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//
// DOCUMENTATION:
//      http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_CollectThings
//     
// QUICK DOX:
//      Collects items from mobs or objects when (right-click) 'interaction' is required.
//      Most useful for those type of quests where you blow something up,
//      then you have to collect the pieces.
//
//  Parameters (required, then optional--both listed alphabetically):
//      (***One or more of the following two attributes must be specified***)
//      MobIdN [REQUIRED if ObjectId is omitted]: Defines the mobs that drop the Items we're after.
//              N may be omitted, or any numeric value--multiple mobs are supported.
//      ObjectIdN [REQUIRED if MobId is omitted]: Defines the objects that drop the Items we're after.
//              N may be omitted, or any numeric value--mulitple objects are supported.
//
//      (This attribute is optional, but governs what other attributes are optional)
//      CollectUntil [Default: RequiredCountReached]: Defines the terminating condition for
//              this behavior.  Available options include:  NoTargetsInArea, RequiredCountReached, QuestComplete.
//              "Targets" means mobs or objects--whatever is dropping the items we're after.
//
//      (***These attributes may/may not be optional based on value of CollectUntil attribute***)
//      CollectItemCount [REQUIRED if CollectUntil=RequiredCountReached; Default: 1]:
//              represents the number of items we must collect for the behavior to terminate.
//      CollectItemId [REQUIRED if CollectUntil=NoTargetsInArea or RequiredCountReached; Default:none]:
//              Identifies the item we are collecting.  The only time this attribute may be omitted
//              is when we're collecting intangibles such as 'attitudes' or 'liberations' that
//              will complete the quest.
//      QuestId [REQUIRED if CollectUntil=QuestComplete; Default:none]:
//
//      (***These attibutes are completely optional***)
//      HuntingGroundRadius [Default: 120]: The range from the anchor location (i.e., X/Y/Z) location at which
//              targets (mobs or objects) will be sought.
//      IgnoreMobsInBlackspots [Default: false]: If true, mobs sitting in blackspotted areas will not be
//              considered as targets.
//      MobState [Default: DontCare]: Identifies the state in which the Mob must be to be considered
//              as a target.  The MobState only applies if the target is some form of NPC.  The MobState
//              Valid values are Alive/Dead/DontCare.
//      NonCompeteDistance [Default: 25]: If a player is within this distance of a target that looks
//              interesting to us, we'll ignore the target.  The assumption is that the player may
//              be going for the same target, and we don't want to draw attention.
//      PostInteractDelay [Default: 1500ms]: The number of milliseconds to wait after each interaction.
//              This is useful if the target requires time for the interaction to complete.
//              This value must be on the closed interval [0..61000].
//      QuestCompleteRequirement [Default:NotComplete]:
//      QuestInLogRequirement [Default:InLog]:
//              A full discussion of how the Quest* attributes operate is described in
//              http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
//      X/Y/Z [Default: Toon's initial position]: Defines the anchor of a search area for
//              which targets (mobs or objects) will be sought.  The hunting ground is defined by
//              this value coupled with the CollectionDistance.
// 
// Exmaples:
// <CustomBehavior File="CollectThings" ...other args... >
//     <Hotspot Name="Cathedral Square fishing dock" X="4554.003" Y="-4718.743" Z="883.0464" StartPoint="true" />
//     <Hotspot Name="The Shady Lady" X="4578.725" Y="-4721.257" Z="882.8724" />
//     <Hotspot Name="The Blue Recluse" X="4584.166" Y="-4693.487" Z="882.7331" StartPoint="true" />
// </CustomBehavior>
// 
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Styx;

using CommonBehaviors.Actions;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.QuestBehaviors.EscortGroup
{
    public class EscortGroup : CustomForcedBehavior
    {
        public delegate WoWPoint LocationDelegate(object context);
        public delegate string MessageDelegate(object context);

        private enum BehaviorStateType
        {
            CheckDone,
            Escorting_Defending,
            Escorting_Loitering,
            Escorting_MoveToAnchorPoint,
            MovingToStartLocation,
            WaitingForComplete,
            WaitingForStart_ByNpcPresence,
            WaitingForStart_ByNpcInteraction,
            WaitingForStart_DontCare,
        }

        public enum StartByType
        {
            NpcInteraction,
            NpcPresence,
            StartTypeDontCare,
        }

        public enum EscortCompleteWhenType
        {
            DestinationReached,
            QuestComplete,
            QuestCompleteOrFails,
        }

        public EscortGroup(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // Parameters dealing with 'starting' the behavior...
                StartBy = GetAttributeAsNullable<StartByType>("StartBy", false, null, null) ?? StartByType.NpcPresence;
                StartEscortGossipOptions = GetAttributeAsArray<int>("StartEscortGossipOptions", false, new ConstrainTo.Domain<int>(-1, 10), null, null) ?? new int[] { 1 };
                StartLocation = GetAttributeAsNullable<WoWPoint>("Start", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;
                StartNpcs = GetNumberedAttributesAsArray<int>("StartNpcId", 0, ConstrainAs.MobId, null);
                StartNpcMaxRange = GetAttributeAsNullable<double>("StartNpcMaxRange", false, new ConstrainTo.Domain<double>(1.0, 100.0), null) ?? 25.0; ;

                // Parameters dealing with the Escorted Npcs...
                EscortedNpcs = GetNumberedAttributesAsArray<int>("EscortedNpcId", 1, ConstrainAs.MobId, null); ;
                EscortedNpcsMaxFollowDistance = GetAttributeAsNullable<double>("EscortedMobsMaxFollowDistance", false, new ConstrainTo.Domain<double>(1.0, 100.0), null) ?? 25.0;
                EscortTimeMaxInSeconds = GetAttributeAsNullable<double>("EscortTimeMaxInSeconds", false, new ConstrainTo.Domain<double>(1.0, 600.0), null) ?? 420.0;

                // Parameters dealing with when the task is 'done'...
                EscortCompleteWhen = GetAttributeAsNullable<EscortCompleteWhenType>("EscortCompleteWhen", false, null, null) ?? EscortCompleteWhenType.QuestComplete;
                EscortCompleteLocation = GetAttributeAsNullable<WoWPoint>("EscortComplete", false, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
                EscortCompleteMaxRange = GetAttributeAsNullable<double>("EscortCompleteMaxRange", false, new ConstrainTo.Domain<double>(1.0, 100.0), null) ?? 25.0;

                // Quest handling...
                QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;


                // Semantic Coherency checks --
                if ((StartBy != StartByType.StartTypeDontCare) && (StartNpcs.Count() == 0))
                {
                    LogMessage("error", "With a StartBy of NpcPresence or NpcInteraction, one or more StartNpcId argument(s) must be specified");
                    IsAttributeProblem = true;
                }

                if ((EscortCompleteWhen == EscortCompleteWhenType.DestinationReached) && (EscortCompleteLocation == WoWPoint.Empty))
                {
                    LogMessage("error", "With a EscortCompleteWhen argument of DestinationReached, you must specify EscortCompleteX/EscortCompleteY/EscortCompleteZ arguments");
                    IsAttributeProblem = true;
                }

                if ((EscortCompleteWhen == EscortCompleteWhenType.QuestComplete) && (QuestId == 0))
                {
                    LogMessage("error", "With a EscortCompleteWhen argument of QuestComplete, you must specify a QuestId argument");
                    IsAttributeProblem = true;
                }

                for (int i = 0; i < StartEscortGossipOptions.Length; ++i)
                    { StartEscortGossipOptions[i] -= 1; }
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it can be quickly
                // resolved.
                LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
                                    + "\nFROM HERE:\n"
                                    + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }


        // Variables for Attributes provided by caller
        private EscortCompleteWhenType EscortCompleteWhen { get; private set; }
        private WoWPoint EscortCompleteLocation { get; private set; }
        private double EscortCompleteMaxRange { get; private set; }

        private int[] EscortedNpcs { get; private set; }
        private double EscortedNpcsMaxFollowDistance { get; private set; }
        private double EscortTimeMaxInSeconds { get; private set; }

        private StartByType StartBy { get; private set; }
        public int[] StartEscortGossipOptions { get; private set; }
        private WoWPoint StartLocation { get; private set; }
        private int[] StartNpcs { get; private set; }
        private double StartNpcMaxRange { get; private set; }

        private int QuestId { get; private set; }
        private QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        private QuestInLogRequirement QuestRequirementInLog { get; private set; }

        // Private and Convenience variables
        private readonly TimeSpan Delay_GossipDialogThrottle = TimeSpan.FromMilliseconds(1000);
        private readonly TimeSpan Delay_WoWClientMovementThrottle = TimeSpan.FromMilliseconds(100);
        private static LocalPlayer Me { get { return (StyxWoW.Me); } }

        private WoWPoint _anchorPoint = WoWPoint.Empty;
        private BehaviorStateType _behaviorState = BehaviorStateType.WaitingForStart_DontCare;
        private WoWUnit _escortedUnit = null;
        private int _gossipOptionIndex;
        private bool _isBehaviorDone = false;
        private bool _isDisposed = false;
        private Stopwatch _waitForStartTimer = new Stopwatch();

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id$"); } }
        public override string SubversionRevision { get { return ("$Revision$"); } }


        ~EscortGroup()
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
                // None, atm

                Utility_NotifyUser(string.Empty);

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }


        #region Overrides of CustomForcedBehavior
        protected override Composite CreateBehavior()
        {
            return (
                new Switch<BehaviorStateType>(ctx => _behaviorState,
                    new Action(ctx => { // default case
                        LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: BehaviorState({0}) is unhandled", _behaviorState);
                        TreeRoot.Stop();
                        _isBehaviorDone = true;
                        }),

                    new SwitchArgument<BehaviorStateType>(BehaviorStateType.MovingToStartLocation,
                        new PrioritySelector(
                            UtilityBehavior_MoveTo(context => StartLocation, context => "Start Location"),

                            new Action(delegate
                            {
                                switch (StartBy)
                                {
                                    case StartByType.NpcInteraction:
                                        _behaviorState = BehaviorStateType.WaitingForStart_ByNpcInteraction;
                                        break;
                                    case StartByType.NpcPresence:
                                        _behaviorState = BehaviorStateType.WaitingForStart_ByNpcPresence;
                                        break;
                                    case StartByType.StartTypeDontCare:
                                        _behaviorState = BehaviorStateType.WaitingForStart_DontCare;
                                        break;
                                    default:
                                        LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: StartBy({0}) state is unhandled", StartBy);
                                        TreeRoot.Stop();
                                        _isBehaviorDone = true;
                                        break;
                                }
                                return (RunStatus.Failure);
                            })
                            )),

                    new SwitchArgument<BehaviorStateType>(BehaviorStateType.WaitingForStart_ByNpcInteraction,
                        BehaviorState_WaitingForStart_ByInteract()),

                    new SwitchArgument<BehaviorStateType>(BehaviorStateType.WaitingForStart_ByNpcPresence,
                        BehaviorState_WaitingForStart_ByPresence()),

                    new SwitchArgument<BehaviorStateType>(BehaviorStateType.WaitingForStart_DontCare,
                        BehaviorState_WaitingForStart_DontCare()),

                    new SwitchArgument<BehaviorStateType>(BehaviorStateType.Escorting,
                        BehaviorState_Escorting()),

                    new SwitchArgument<BehaviorStateType>(BehaviorStateType.WaitingForComplete,
                        BehaviorState_WaitingForComplete())
                ));
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
            // This reports problems, and stops BT processing if there was a problem with attributes...
            // We had to defer this action, as the 'profile line number' is not available during the element's
            // constructor call.
            OnStart_HandleAttributeProblem();

            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (!IsDone)
            {
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = this.GetType().Name 
                    + ": "
                    + ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress (no associated quest)");

                _behaviorState = BehaviorStateType.MovingToStartLocation;
            }
        }
        #endregion


        #region Behavior helpers
        private Composite BehaviorState_CheckDone()
        {
            return (new ActionAlwaysFail());
        }


        private Composite BehaviorState_Escorting()
        {
            return (new PrioritySelector(
                // If we are too far from the Escorted Unit, then close distance...
                
                // If escort units in combat, then defend...

                // Escort done?

                // Escort failed?

                new Action(delegate { _waitForStartTimer.Stop(); return (RunStatus.Failure); }),
                new ActionAlwaysFail()
                ));
        }


        private Composite BehaviorState_Escorting_Defending()
        {
            return (new PrioritySelector(
                // If we are too far from the Escorted Unit, then close distance...

                // If escort units in combat, then defend...

                // Escort done?

                // Escort failed?

                new Action(delegate { _waitForStartTimer.Stop(); return (RunStatus.Failure); }),
                new ActionAlwaysFail()
                ));
        }


        private Composite BehaviorState_Escorting_Loitering()
        {
            return (new PrioritySelector(
                // If we are too far from the Escorted Unit, then close distance...
                new Decorator(context => (Me.Location.Distance(FindGroupCenterPoint()) >= EscortedNpcsMaxFollowDistance),
                    new Action(delegate
                    {
                        _behaviorState = BehaviorStateType.Escorting_MoveToAnchorPoint;
                        return (RunStatus.Success);
                    })),

                // If escort units in combat, then defend...
                new Decorator(context => (FindNextTarget() != null),
                    new Action(delegate
                    {
                        _behaviorState = BehaviorStateType.Escorting_Defending;
                        return (RunStatus.Success);
                    })),

                // Escort complete or failed?
                new Decorator(context => IsEscortComplete() || IsEscortFailed(),
                    new Action(delegate
                    {
                        _behaviorState = BehaviorStateType.CheckDone;
                        return (RunStatus.Success);
                    }))
                ));
        }


        // Move to anchor point, even if in combat, to prevent failing quest...
        private Composite BehaviorState_Escorting_MoveToAnchorPoint()
        {
            return (new PrioritySelector(
                // If Anchor point is empty, find new anchor point...
                new Decorator(context => (_anchorPoint == WoWPoint.Empty),
                    new Action(delegate
                    {
                        _anchorPoint = FindGroupCenterPoint();

                        // If we were unable to calculate an anchorpoint, see if we're done...
                        if (_anchorPoint == WoWPoint.Empty)
                        {
                            _behaviorState = BehaviorStateType.CheckDone;
                        }
                    })),

                // If we are too far from the group center, then close distance...
                // NB: The 'valid' check makes sure we're not chasing units that have de-popped.
                new Decorator(context => (Me.Location.Distance(_anchorPoint) > Navigator.PathPrecision),
                    UtilityBehavior_MoveTo(context => _escortedUnit.Location, context => _escortedUnit.Name)),

                // Done with movement...
                new Action(delegate
                {
                    _anchorPoint = WoWPoint.Empty;
                    _behaviorState = BehaviorStateType.Escorting_Loitering;
                })
                ));
        }


        private Composite BehaviorState_WaitingForComplete()
        {
            return (new ActionAlwaysFail());
        }


        // Wait for group to arrive at starting location, when present interact to start escort --
        // When the group is within "StartNpcMaxRange" of our current position, we will interact
        // with the nearest NPC to start the escort.
        private Composite BehaviorState_WaitingForStart_ByInteract()
        {
            return (new PrioritySelector(
                nearestUnitContext => FindStartUnits().OrderBy(u => u.Distance).FirstOrDefault(),

                // Wait for units to arrive...
                UtilityBehavior_WaitForStartUnitsToArrive(),

                // Move to closest unit...
                UtilityBehavior_MoveTo(nearestUnitContext => ((WoWUnit)nearestUnitContext).Location,
                                        nearestUnitContext => ((WoWUnit)nearestUnitContext).Name),

                // Interact with unit to open the Gossip dialog...
                new Decorator(nearestUnitContext => (StartEscortGossipOptions.Length > 0) && (GossipFrame.Instance == null),
                    new Sequence(
                        new Action(nearestUnitContext =>
                        {
                            WoWUnit nearestUnit = ((WoWUnit)nearestUnitContext);

                            nearestUnit.Target();
                            Utility_NotifyUser(string.Format("Interacting with \"{0}\" to start escort.", nearestUnit.Name));
                            nearestUnit.Interact();
                            _gossipOptionIndex = 0;
                        }),

                        new WaitContinue(Delay_GossipDialogThrottle, ret => (GossipFrame.Instance != null), new ActionAlwaysSucceed())
                    )),

                // Choose appropriate gossip options...
                new Decorator(nearestUnitContext => (GossipFrame.Instance != null) && (_gossipOptionIndex < StartEscortGossipOptions.Length),
                    new Sequence(
                        new Action(nearestUnitContext =>
                        {
                            // Select next Gossip option...
                            // NB: If we get attacked while gossiping, and the dialog closes,
                            // then it will automatically be retried.
                            GossipFrame.Instance.SelectGossipOption(StartEscortGossipOptions[_gossipOptionIndex]);
                            ++_gossipOptionIndex;
                        }),
                        new WaitContinue(Delay_GossipDialogThrottle, ret => (_gossipOptionIndex >= StartEscortGossipOptions.Length), new ActionAlwaysSucceed())
                        )),

                // All done, start the escort...
                new Action(nearestUnitContext =>
                {
                    GossipFrame.Instance.Close();
                    _waitForStartTimer.Stop();
                    _behaviorState = BehaviorStateType.Escorting;
                    return (RunStatus.Failure);
                })
            ));
        }


        // Wait for group to arrive at start location, then automatically start escort --
        // When the group is with "StartNpcMaxRange" of our current position,
        // we start the escort.
        private Composite BehaviorState_WaitingForStart_ByPresence()
        {
            return (new PrioritySelector(
                // Wait for units to arrive...
                UtilityBehavior_WaitForStartUnitsToArrive(),
                        
                // Start escorting...
                new Action(nearestUnitContext =>
                {
                    _waitForStartTimer.Stop();
                    _behaviorState = BehaviorStateType.Escorting;
                    return (RunStatus.Failure);
                })
            ));
        }


        // No pre-conditions for starting --
        // We simply find the group, and start escorting them.
        private Composite BehaviorState_WaitingForStart_DontCare()
        {
            return (
                // Start escorting...
                new Action(ctx =>
                {
                    _waitForStartTimer.Stop();
                    _behaviorState = BehaviorStateType.Escorting;
                    return (RunStatus.Failure);
                })
            );
        }


        // May return 'null'
        private IEnumerable<WoWUnit> FindEscortedUnits()
        {
            return (ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                .Where(u => EscortedNpcs.Contains((int)u.Entry) && u.IsAlive)
                .ToList());
        }


        private WoWPoint FindGroupCenterPoint()
        {
            IEnumerable<WoWUnit> escortedUnits = FindEscortedUnits();
            int escortedUnitCount = 0;
            double centerPointX = 0.0;
            double centerPointY = 0.0;
            double centerPointZ = 0.0;

            foreach (WoWUnit wowUnit in FindEscortedUnits())
            {
                centerPointX += wowUnit.Location.X;
                centerPointY += wowUnit.Location.Y;
                centerPointZ += wowUnit.Location.Z;
                ++escortedUnitCount;
            }

            return ((escortedUnitCount <= 0)
                    ? WoWPoint.Empty
                    : (new WoWPoint((centerPointX / escortedUnitCount),
                                    (centerPointY / escortedUnitCount),
                                    (centerPointZ / escortedUnitCount))));
        }


        // May return 'null'
        private IEnumerable<WoWUnit> FindStartUnits()
        {
            // If "StartNpcs" were specified, prefer them...
            if (StartNpcs.Count() > 0)
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                    .Where(u => StartNpcs.Contains((int)u.Entry))
                    .ToList());
            }

            // No "StartNpcs' specified, use the Npcs to be Escorted...
            return (ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                .Where(u => EscortedNpcs.Contains((int)u.Entry))
                .ToList());
        }


        // Find the weakest mob attacking our weakest escorted unit...
        private WoWUnit FindNextTarget()
        {
            IEnumerable<WoWUnit>  escortedUnits = FindEscortedUnits();

            if (escortedUnits.Any(u => u.Combat))
            {
                WoWUnit weakestEscortedUnit = escortedUnits.OrderBy(u => u.HealthPercent).FirstOrDefault();

                if (weakestEscortedUnit != null)
                {
                    return (ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                        .Where(u => u.CurrentTargetGuid == weakestEscortedUnit.Guid)
                        .OrderBy(u => u.HealthPercent)
                        .FirstOrDefault());
                }
            }

            return (null);
        }


        // We prefer to Escort the unit in the group with the lowest health...
        private WoWUnit FindUnitToEscort()
        {
            return (FindEscortedUnits().OrderBy(u => u.HealthPercent).FirstOrDefault());
        }


        private bool IsEscortComplete()
        {
            switch (EscortCompleteWhen)
            {
                case EscortCompleteWhenType.DestinationReached:
                    return (Me.Location.Distance(EscortCompleteLocation) <= EscortCompleteMaxRange);

                case EscortCompleteWhenType.QuestComplete:
                    {
                        PlayerQuest quest = Me.QuestLog.GetQuestById((uint)QuestId);
                        return ((quest == null) || quest.IsCompleted);
                    }

                case EscortCompleteWhenType.QuestCompleteOrFails:
                    {
                        PlayerQuest quest = Me.QuestLog.GetQuestById((uint)QuestId);
                        return ((quest == null) || quest.IsCompleted || quest.IsFailed);
                    }
            }

            LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: EscortCompleteWhen({0}) state is unhandled", EscortCompleteWhen);
            TreeRoot.Stop();
            return (true);
        }


        // Escort fails when 1) quest says so, or 2) there are no more units to escort
        private bool IsEscortFailed()
        {
            bool isFailed = (FindEscortedUnits().Count() <= 0);

            if (QuestId != 0)
            {
                PlayerQuest quest = Me.QuestLog.GetQuestById((uint)QuestId);
                isFailed |= quest.IsFailed;
            }

            return (isFailed);
        }


        // Returns: RunStatus.Success while movement is in progress; othwerise, RunStatus.Failure if no movement necessary
        private Composite UtilityBehavior_MoveTo(LocationDelegate locationDelegate, MessageDelegate locationNameDelegate)
        {
            return (
                new Sequence(
                    // Done, if we're already at destination...
                    new DecoratorContinue(context => (Me.Location.Distance(locationDelegate(context)) <= Navigator.PathPrecision),
                        new ActionAlwaysFail()),

                    // Notify user of progress...
                    new CompositeThrottle(TimeSpan.FromSeconds(1),
                        new Action(context =>
                        {
                            double destinationDistance = Me.Location.Distance(locationDelegate(context));
                            string locationName = locationNameDelegate(context);
                            Utility_NotifyUser(string.Format("Moving to {0} (distance: {1})", locationName, destinationDistance));
                        })
                        ),

                    new Action(context =>
                    {
                        WoWPoint destination = locationDelegate(context);

                        // Try to use Navigator to get there...
                        MoveResult moveResult = Navigator.MoveTo(destination);

                        // If Navigator fails, fall back to click-to-move...
                        if ((moveResult == MoveResult.Failed) || (moveResult == MoveResult.PathGenerationFailed))
                        {
                            WoWMovement.ClickToMove(destination);
                        }

                        return (RunStatus.Success); // fall through
                    }),

                    new WaitContinue(Delay_WoWClientMovementThrottle, ret => false, new ActionAlwaysSucceed())
                    ));
        }


        // Returns RunStatus.Success while waiting for units to arrive; RunStatus.Failure, if no need to wait
        private Composite UtilityBehavior_WaitForStartUnitsToArrive()
        {
            return (new PrioritySelector(
                nearestUnitContext => FindStartUnits().OrderBy(u => u.Distance).FirstOrDefault(),

                // Wait for units to arrive...
                new Decorator(nearestUnitContext => (nearestUnitContext == null),
                    new Sequence(
                        new Action(delegate { if (!_waitForStartTimer.IsRunning) { _waitForStartTimer.Restart(); } }),

                        new CompositeThrottle(TimeSpan.FromSeconds(10),
                            new Action(delegate
                            {
                                Utility_NotifyUser(string.Format("Waiting {0} for NPCs to arrive within {1} feet.",
                                                                Utility_BuildTimeAsString(_waitForStartTimer.Elapsed),
                                                                StartNpcMaxRange));
                            })),

                        new ActionAlwaysSucceed()
                        )),

                new Action(delegate { _waitForStartTimer.Stop(); return (RunStatus.Failure); })
                ));
        }

        
        private string Utility_BuildTimeAsString(TimeSpan timeSpan)
        {
            string formatString = string.Empty;

            if (timeSpan.Hours > 0)         { formatString = "{0:D2}h:{1:D2}m:{2:D2}s"; }
            else if (timeSpan.Minutes > 0)  { formatString = "{1:D2}m:{2:D2}s"; }
            else                            { formatString = "{2:D2}s"; }

            return (string.Format(formatString, timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds));
        }


        private string Utility_GetNamesOfUnits(IEnumerable<WoWUnit> wowUnits)
        {
            return ((wowUnits.Count() > 0)
                ? string.Join(", ", wowUnits.Select(u => u.Name))
                : "No NPCs");
        }


        private void Utility_NotifyUser(string statusText)
        {
            if (statusText != null) { TreeRoot.StatusText = statusText; }
            if (!string.IsNullOrEmpty(statusText)) { LogMessage("info", statusText); }
        }
        #endregion // Behavior helpers


        #region TreeSharp Extensions
        public class CompositeThrottle : DecoratorContinue
        {
            public CompositeThrottle(TimeSpan throttleTime,
                                     Composite composite)
                : base(composite)
            {
                _throttle = new Stopwatch();
                _throttleTime = throttleTime;
            }


            protected override bool CanRun(object context)
            {
                if (_throttle.IsRunning && (_throttle.Elapsed < _throttleTime))
                    { return (false); }

                _throttle.Restart();
                return (true);
            }

            private readonly Stopwatch _throttle;
            private readonly TimeSpan _throttleTime;
        }
        #endregion
    }
}

