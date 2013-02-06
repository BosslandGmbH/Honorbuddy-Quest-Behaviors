// Behavior originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Summary and Documentation
// QUICK DOX:
// ESCORTGROUP has the following characteristics:
//      * Escorts and defends a single NPC or a group of NPCs
//      * Can be used to defend a stationary object
//      * Can gossip with an NPC to initiate the quest (outside of a quest pickup)
//      * Will wait for the NPCs to be escorted to arrive, or can search for them
//        via a supplied SearchPath.
//      * Can 'lead' an NPC (rather than follow it) via a supplied EscortPath.
//
//  BEHAVIOR ATTRIBUTES:
//  Interaction to start:
//      StartNpcIdN [REQUIRED if StartEscortGossipOptions specified]
//          N may be omitted, or any numeric value--multiple mobs are supported.
//          If NPC interaction is required to start the escort, the StartNpcIdN identifieds one
//          or more NPCs that may be interacted with to initiate the escort.
//          If the quest fails for some reason, the behavior will return to these NPCs
//          to try again.
//      StartEscortGossipOptions [optional; Default: a dialog-less chat interaction]
//          This argument is only used of a StartNpcIdN is specified.
//          Specifies the set of interaction choices on each gossip panel to make, in order to
//          start the escort.
//          The value of this attribute is a comma-separated list of gossip options (e.g., "1,3,1").
//
// NPCs to escort:
//      EscortNpcIdN [REQUIRED]
//          N may be omitted, or any numeric value--multiple mobs are supported.
//          Identifies one or more NPCs to be escorted.
//
// Completion Criteria:
//      EscortCompleteWhen [optional; Default: QuestComplete]
//          [allowed values: DestinationReached/QuestComplete/QuestCompleteOrFails]
//          Defines the completion criteria for the behavior.
//      EscortCompleteX/EscortCompleteY/EscortCompleteZ [REQUIRED if EscortCompleteWhen=DestinationReached]
//          Defines the location at which the behavior is considered complete.  This value is only used
//          if EscortComplete is DestinationReached.
//
// Quest binding:
//      QuestId [REQUIRED if EscortCompleteWhen=QuestComplete; Default:none]:
//      QuestCompleteRequirement [Default:NotComplete]:
//      QuestInLogRequirement [Default:InLog]:
//              A full discussion of how the Quest* attributes operate is described in
//              http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
//      QuestObjectiveIndex [REQUIRED if EventCompleteWhen=QuestObjectiveComplete]
//          [on the closed interval: [1..5]]
//          This argument is only consulted if EventCompleteWhen is QuestObjectveComplete.
//          The argument specifies the index of the sub-goal of a quest.
//
// Tunables (ideally, the profile would _never_ provide these arguments):
//      EscortCompleteMaxRange [optional; Default: 10.0]
//          Defines the tolerance from the EscortCompleteX/Y/Z for which the behavior is considered
//          complete.  This value is only used if EscortCompelte is DestinationReached.
//      EscortMaxFightDistance [optional; Default: 27.0]
//          [on the close range: [5.0..100.0]]
//          The maximum range from the _nearest_ escorted NPC at which the toon will fight.
//          If for some reason, the toon is pulled outside of this range, the behavior
//          moves the toon back into this maximum range.
//      EscortMaxFollowDistance [optional; Default: 15.0]
//          [on the closed range: [3.0..100.0]]
//          The maximum range at which the toon will follow the escorted NPCs while not
//          in combat.  If a group of NPCs is being escorted, this distance is measured
//          from the group center point.
//      SearchForNpcsRadius [optional; Default: 10.0]
//          [on the closed range: [3.0..100.0]
//          This is the distance from the current position that the toon will look for
//          StartNpcIds or EscortNpcIds.
//
// BEHAVIOR EXTENSION ELEMENTS (goes between <CustomBehavior ...> and </CustomBehavior> tags)
// See the "Examples" section for typical usage.
//      EscortPath [optional]
//          Comprised of a set of Hotspots, this element defines a _unidirectional_ path
//          which the toon will lead the EscortNpcIdN.
//      SearchPath [optional; Default: Toon's current position when the behavior is started]
//          Comprised of a set of Hotspots, this element defines a _circular_ path
//          that the toon will traverse to locate the StartNpcIdN or EscortNpcIdN.
//          If no SearchPath is specified, the toon will wait at the current position
//          for the StartNpcIdN or EscortNpcIdN to arrive.
//
// THINGS TO KNOW:
// * All looting and harvesting is turned off while the escort is in progress.
// * Provide an EscortPath--it makes the toon look _incredibly_ human.
//
// POSSIBLE FUTURE IMPROVEMENTS:
// * Add QuestObjectiveIndex argument and EscortCompleteWhen.QuestObjectiveComplete
// * Do 'fan out' as we travel, so multiple toons don't stack up in the exact same spots
// * Timeouts: 1) While searching for NPCs, 2) for overall quest completion
#endregion

#region FAQs
// * Why the distinction between StartNpcIdN and EscortNpcIdN?
//      StartNpcIdN must be interacted via a gossip dialog to initiate the escort.
//      EscortNpcIdN represents a toon who's presence is sufficient to conduct
//      an escort.
//      Game mechanics are usually such that after the gossip completes with the
//      StartNpcIdN, an second instance of the gossip NPC is produced, and this
//      instanced-version is the NPC that should be followed (via EscortNpcIdN).
//
// * When does the behavior 'lead' versus 'follow' NPCs to be escorted?
//      If an EscortPath is specified, the toon will 'lead' the NPC while escorting.
//      Otherwise, it will 'follow' the NPC.
//
// * What happens if the toon gets in a fight but the NPCs keep going?
//      The toon will try to drag the mob to the NPC and pull the NPC into combat
//      also. This usually makes the NPC stop moving, and assist with the battle.
//
// * Can I specify both an EscortPath and a SearchPath?
//      Absolutely
//
// * What happens if the NPCs won't follow the EscortPath?
//      The behavior detects this situation and dequeues the current waypoint from
//      this escort path.  Such a dequeue will continue to happen until the toon
//      is again in a position to 'lead' the escorted NPCs.
//      If the toon never gets into a position to 'lead' the NPCs (i.e., the EscortPath
//      is poorly defined), then the toon just resorts to 'following' the NPCs.
//
// * What happens when an Escorted NPC dies?
//      If there is more than one NPC being escorted, the behavior will continue
//      until all NPCs are dead.  Once the last NPC dies, the behavior will return
//      to its starting point and try again, unless EscortCompleteWhen is set to
//      QuestCompleteOrFails.
//
// * What happens if the Quest fails?
//      Normally, the behavior returns to its starting point to try again.  However,
//      if EscortCompleteWhen is set to QuestCompleteOrFails, the behavior will simply
//      terminate.
//
// * The quest failed, how do I arrange to abandon it, and go pick it up again?
//      Set the EscortCompleteWhen attribute to QuestCompleteOrFails.  In the profile,
//      check for quest failure, and abandon and pick the quest up again, before
//      re-conducting the behavior.
#endregion

#region Examples
// "Reunited" (http://www.wowhead.com/quest=31091).
// A simple follow-and-defend quest.  The EscortPath is optional, but makes the toon look
// exceptionally human, because it will 'lead' the Npc rather than follow it.  The path is
// designed to avoid an initial encounter, just like a human would.
// The quest requires interacting with an NPC (StartNpcId) via a gossip, then the gossip NPC
// is immediately replaced with an instanced-version which we need to escort (EscortNpcId).
// 		<CustomBehavior File="EscortGroup" QuestId="31091" StartNpcId="63876" EscortNpcId="64013">
//          <EscortPath>
//              <Hotspot X="-874.6547" Y="3809.671" Z="-0.3560973" />
//              <Hotspot X="-895.4308" Y="3819.729" Z="-0.2184875" />
//              <Hotspot X="-927.9977" Y="3821.674" Z="-0.3616131" />
//              <Hotspot X="-955.2557" Y="3891.079" Z="-0.02633452" />
//              <Hotspot X="-1027.789" Y="3926.881" Z="0.591239" />
//              <Hotspot X="-1094.124" Y="3928.721" Z="-0.2553162" />
//              <Hotspot X="-1154.453" Y="3901.083" Z="1.835292" />
//          </EscortPath>
//      </CustomBehavior>
//
// "Students No More" (http://www.wowhead.com/quest=30625)
// Searchs for the students (EscortNpcIdN), then follows them to kill 4 elite mobs and some trash.
// The SearchPath is used to initially locate the students.
//      <CustomBehavior File="EscortGroup" QuestId="30625"
//          EscortNpcId1="59839" EscortNpcId2="59840" EscortNpcId3="59841" EscortNpcId4="59842" EscortNpcId5="59843">
//          <SearchPath>
//              <Hotspot X="-244.1373" Y="2327.563" Z="137.9225" />
//              <Hotspot X="-271.8149" Y="2286.35" 	Z="119.61" />
//              <Hotspot X="-278.60"   Y="2280.82"  Z="116.73" />
//              <Hotspot X="-377.7986" Y="2311.363" Z="117.677" />
//              <Hotspot X="-422.3317" Y="2303.213" Z="133.0315" />
//              <Hotspot X="-273.74"   Y="2351.56"  Z="126.98" />
//          </SearchPath>
//      </CustomBehavior>
// 
#endregion

#region Usings
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.QuestBehaviors.EscortGroup1
{
    public class EscortGroup : CustomForcedBehavior
    {
        public delegate WoWPoint LocationDelegate(object context);
        public delegate string MessageDelegate(object context);
        public delegate double RangeDelegate(object context);

        #region Consructor and Argument Processing
        public enum EscortCompleteWhenType
        {
            DestinationReached,
            QuestComplete,
            QuestCompleteOrFails,
            QuestObjectiveComplete,
        }

        public EscortGroup(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // Parameters dealing with 'starting' the behavior...
                StartEscortGossipOptions = GetAttributeAsArray<int>("StartEscortGossipOptions", false, new ConstrainTo.Domain<int>(1, 10), null, null);
                StartNpcIds = GetNumberedAttributesAsArray<int>("StartNpcId", 0, ConstrainAs.MobId, null);

                // Parameters dealing with the Escorted Npcs...
                EscortNpcIds = GetNumberedAttributesAsArray<int>("EscortNpcId", 1, ConstrainAs.MobId, null); ;

                // Parameters dealing with when the task is 'done'...
                EscortCompleteWhen = GetAttributeAsNullable<EscortCompleteWhenType>("EscortCompleteWhen", false, null, null) ?? EscortCompleteWhenType.QuestComplete;
                EscortCompleteLocation = GetAttributeAsNullable<WoWPoint>("EscortComplete", false, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;

                // Quest handling...
                QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
                QuestObjectiveIndex = GetAttributeAsNullable<int>("QuestObjectiveIndex", false, new ConstrainTo.Domain<int>(1, 5), null) ?? 0;

                // Tunables...
                EscortCompleteMaxRange = GetAttributeAsNullable<double>("EscortCompleteMaxRange", false, new ConstrainTo.Domain<double>(1.0, 100.0), null) ?? 10.0;
                EscortMaxFightDistance = GetAttributeAsNullable<double>("EscortMaxFightDistance", false, new ConstrainTo.Domain<double>(5.0, 100.0), null) ?? 27.0;
                EscortMaxFollowDistance = GetAttributeAsNullable<double>("EscortMaxFollowDistance", false, new ConstrainTo.Domain<double>(3.0, 100.0), null) ?? 15.0;
                SearchForNpcsRadius = GetAttributeAsNullable<double>("SearchForNpcsRadius", false, new ConstrainTo.Domain<double>(1.0, 100.0), null) ?? 10.0;

                // Semantic Coherency checks --
                if ((StartEscortGossipOptions.Count() != 0) && (StartNpcIds.Count() == 0))
                {
                    LogMessage("error", "If StartEscortGossipOptions are specified, you must also specify one or more StartNpcIdN");
                    IsAttributeProblem = true;
                }

                if (EscortMaxFightDistance < EscortMaxFollowDistance)
                {
                    LogMessage("error", "EscortedNpcsMaxCombatDistance({0}) must be greater than or equal to EscortedNpcsMaxNoCombatDistance({1})",
                        EscortMaxFightDistance, EscortMaxFollowDistance);
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

                if ((QuestId == 0) && (EscortCompleteWhen != EscortCompleteWhenType.DestinationReached))
                {
                    LogMessage("error", "When no QuestId is specified, EscortCompleteWhen must be DestinationReached");
                    IsAttributeProblem = true;
                }

                if ((EscortCompleteWhen == EscortCompleteWhenType.QuestObjectiveComplete)
                    && ((QuestId == 0) || (QuestObjectiveIndex == 0)))
                {
                    LogMessage("error", "With an EscortCompleteWhen argument of QuestObjectiveComplete, you must specify both QuestId and QuestObjectiveIndex arguments");
                    IsAttributeProblem = true;
                }

                if ((QuestObjectiveIndex != 0) && (EscortCompleteWhen != EscortCompleteWhenType.QuestObjectiveComplete))
                {
                    LogMessage("error", "The QuestObjectiveIndex argument should not be specified unless EscortCompleteWhen is QuestObjectiveComplete");
                    IsAttributeProblem = true;
                }

                if (StartEscortGossipOptions.Count() == 0)
                    { StartEscortGossipOptions = new int[] { 1 }; }

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
        private EscortCompleteWhenType EscortCompleteWhen { get; set; }
        private WoWPoint EscortCompleteLocation { get; set; }
        private double EscortCompleteMaxRange { get; set; }

        private int[] EscortNpcIds { get; set; }
        private double EscortMaxFightDistance { get; set; }
        private double EscortMaxFollowDistance { get; set; }

        private double SearchForNpcsRadius { get; set; }
        public int[] StartEscortGossipOptions { get; set; }
        private int[] StartNpcIds { get; set; }

        private int QuestId { get; set; }
        private int QuestObjectiveIndex { get; set; }
        private QuestCompleteRequirement QuestRequirementComplete { get; set; }
        private QuestInLogRequirement QuestRequirementInLog { get; set; }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return "$Id$"; } }
        public override string SubversionRevision { get { return "$Rev$"; } }
        #endregion


        #region Private and Convenience variables
        private enum BehaviorStateType
        {
            InitialState,
            SearchingForEscortUnits,
            InteractingToStart,
            InitializeEscortPath,
            Escorting,
            CheckDone,
        }

        private struct MovementState
        {
            public bool IsMoveInProgress { get; set; }
        }

        private readonly TimeSpan Delay_GossipDialogThrottle = TimeSpan.FromMilliseconds(1000);
        private readonly TimeSpan Delay_WoWClientMovementThrottle = TimeSpan.FromMilliseconds(100);
        private double EscortMaxLeadDistance { get; set; }
        private readonly TimeSpan LagDuration = TimeSpan.FromMilliseconds((StyxWoW.WoWClient.Latency * 2) + 150);
        private LocalPlayer Me { get { return StyxWoW.Me; } }
        private IEnumerable<WoWUnit> MeAsGroup  = new List<WoWUnit>() { StyxWoW.Me };

        private BehaviorStateType _behaviorState = BehaviorStateType.InitialState;
        private Composite _behaviorTreeHook_Combat = null;
        private Composite _behaviorTreeHook_Death = null;
        private Composite _behaviorTreeHook_Main = null;
        private ConfigMemento _configMemento = null;
        private Queue<WoWPoint> _escortPath = null;
        private IEnumerable<WoWPoint> _escortPathSpecified = null;
        private int _gossipOptionIndex;
        private bool _isBehaviorDone = false;
        private bool _isDisposed = false;
        private MovementState _movementState    = new MovementState();
        private WoWPoint _movementDestination = WoWPoint.Empty;
        private BotPoi _poiStartLocation = null;
        private int _pullDistanceOriginal = 0;
        private Queue<WoWPoint> _searchPath = null;
        private WoWUnit _targetPoiUnit = null;
        private WoWPoint _toonStartingPosition = WoWPoint.Empty;
        private Stopwatch _waitForStartTimer = new Stopwatch();
        #endregion


        #region Destructor, Dispose, and cleanup
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
                if (_behaviorTreeHook_Combat != null)
                {
                    TreeHooks.Instance.RemoveHook("Combat_Only", _behaviorTreeHook_Combat);
                    _behaviorTreeHook_Combat = null;
                }

                if (_behaviorTreeHook_Death != null)
                {
                    TreeHooks.Instance.RemoveHook("Death_Main", _behaviorTreeHook_Death);
                    _behaviorTreeHook_Death = null;
                }

                // NB: we don't unhook _behaviorTreeHook_Main
                // This was installed when HB created the behavior, and its up to HB to unhook it

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
                return _isBehaviorDone     // normal completion
                        || !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete);
            }
        }


        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

            if ((QuestId != 0) && (quest == null))
            {
                LogMessage("error", "This behavior has been associated with QuestId({0}), but the quest is not in our log", QuestId);
                IsAttributeProblem = true;
            }

            _escortPathSpecified = ParsePath("EscortPath");
            _searchPath = ParsePath("SearchPath");

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
                CharacterSettings.Instance.LootMobs = false;
                CharacterSettings.Instance.NinjaSkin = false;
                CharacterSettings.Instance.SkinMobs = false;
                _pullDistanceOriginal = CharacterSettings.Instance.PullDistance;
                CharacterSettings.Instance.PullDistance = 10;
                
                TreeRoot.GoalText = string.Format(
                    "{0}: \"{1}\"\nLooting and Harvesting are disabled while Escort in progress",
                    this.GetType().Name,
                    ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress (no associated quest)"));

                // If we're leading the escort instead of following,
                // allow some hysteresis in that 'move back to escort' distance...
                // This prevents the toon from vascillating while moving.
                if (_escortPathSpecified.Count() > 0)
                {
                    EscortMaxLeadDistance = EscortMaxFollowDistance;
                    EscortMaxFollowDistance = EscortMaxFollowDistance + Math.Max(0.2 * EscortMaxFollowDistance, 5.0);
                }

                // If search path not provided, use our current location...
                if (_searchPath.Count() <= 0)
                    { _searchPath.Enqueue(Me.Location); }

                _toonStartingPosition = Me.Location;

                _behaviorState = BehaviorStateType.InitialState;
                _behaviorTreeHook_Combat = CreateCombatBehavior();
                TreeHooks.Instance.InsertHook("Combat_Only", 0, _behaviorTreeHook_Combat);
                _behaviorTreeHook_Death = CreateDeathBehavior();
                TreeHooks.Instance.InsertHook("Death_Main", 0, _behaviorTreeHook_Death);
            }
        }
        #endregion


        #region Main Behavior
        private Composite CreateCombatBehavior()
        {
            // NB: This behavior is designed to run at a 'higher priority' than Combat_Only.
            // This is necessary because movement is frequently more important than combat when conducting
            // escorts (e.g., the escort doesn't wait for us to kill the mob).

            return new Decorator(context => _behaviorState == BehaviorStateType.Escorting,
                new PrioritySelector(escortedUnitsContext => FindUnitsFromIds(EscortNpcIds),

                    // If Escort has not failed, then "keep up" with escorted units as needed...
                    new Decorator(escortedUnitsContext => !IsEscortFailed((IEnumerable<WoWUnit>)escortedUnitsContext),
                        new PrioritySelector(
                            // Toon should *never* drift more than EscortMaxFightDistance from nearest escorted unit
                            UtilityBehavior_MoveTo(
                                escortedUnitsContext => !((IEnumerable<WoWUnit>)escortedUnitsContext).Any(u => Me.Location.Distance(u.Location) <= EscortMaxFightDistance),
                                escortedUnitsContext => false,
                                escortedUnitsContext => FindGroupCenterPoint((IEnumerable<WoWUnit>)escortedUnitsContext),
                                escortedUnitsContext => "nearest unit in combat"),

                            // If the escorted units are not in combat with us and moving away,
                            // move to escorted center point in an attempt to put the escorted units in combat also.
                            UtilityBehavior_MoveTo(
                                escortedUnitsContext => !IsAnyInCombat((IEnumerable<WoWUnit>)escortedUnitsContext)
                                                            && ((IEnumerable<WoWUnit>)escortedUnitsContext).All(u => u.IsMoving),
                                escortedUnitsContext => IsAnyInCombat((IEnumerable<WoWUnit>)escortedUnitsContext)
                                                            || ((IEnumerable<WoWUnit>)escortedUnitsContext).Any(u => !u.IsMoving),
                                escortedUnitsContext => FindGroupCenterPoint((IEnumerable<WoWUnit>)escortedUnitsContext),
                                escortedUnitsContext => "closer to escorted units")
                    )),
                       
                    // ---Target Selection---
                    new Action(escortedUnitsContext =>
                    {
                        ChooseBestTarget((IEnumerable<WoWUnit>)escortedUnitsContext);
                        return RunStatus.Failure;
                    })
                ));
        }


        protected Composite CreateDeathBehavior()
        {
            // If toon dies, we need to restart behavior
            return new Decorator(context => (Me.IsDead || Me.IsGhost) && (_behaviorState != BehaviorStateType.CheckDone),
                new Action(context => { _behaviorState = BehaviorStateType.CheckDone; }));
        }


        // NB: Due to the complexity, this behavior is 'state' based.  All necessary actions are
        // conducted in the current state.  If the current state is no longer valid, then a state change
        // is effected.  Ths entry state is "InitialState".
        private Composite CreateMainBehavior()
        {
            // NB: This behavior is hooked in at a 'higher priority' than Combat_Only.  We need this
            // because while escorting, it is sometimes more important to move than fight.

            // Let other behaviors deal with toon death and path back to corpse...
            return new PrioritySelector(escortedUnitsContext => FindUnitsFromIds(EscortNpcIds),
                    //FOR DEBUG:
                    //new Action(escortedUnitsContext => { LogMessage("info", "Current State: {0}", _behaviorState); return RunStatus.Failure; }),

                    new Decorator(escortedUnitsContext => _isBehaviorDone,
                        new Action(escortedUnitsContext => { LogMessage("info", "Finished"); })),

                    new Switch<BehaviorStateType>(escortedUnitsContext => _behaviorState,
                        new Action(escortedUnitsContext =>   // default case
                        {
                            LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: BehaviorState({0}) is unhandled", _behaviorState);
                            TreeRoot.Stop();
                            _isBehaviorDone = true;
                        }),

                        #region State: InitialState
                        new SwitchArgument<BehaviorStateType>(BehaviorStateType.InitialState,
                            new PrioritySelector(
                                UtilityBehavior_MoveTo(
                                    escortedUnitsContext => true,
                                    escortedUnitsContext => false,
                                    escortedUnitsContext => _toonStartingPosition,
                                    escortedUnitsContext => "start location"),

                                // Start at nearest point in the search path...
                                new Action(escortedUnitsContext =>
                                {
                                    WoWPoint nearestPoint = _searchPath.OrderBy(p => Me.Location.Distance(p)).FirstOrDefault();
                                    while (_searchPath.Peek() != nearestPoint)
                                        { Utility_RotatePath(_searchPath); }

                                    _behaviorState = BehaviorStateType.SearchingForEscortUnits;
                                })
                            )),
                        #endregion

                        #region State: SearchingForEscortUnits
                        new SwitchArgument<BehaviorStateType>(BehaviorStateType.SearchingForEscortUnits,
                            new PrioritySelector(
                                // If Start NPCs specified, move to them when found...
                                new Decorator(escortedUnitsContext => StartNpcIds.Count() > 0,
                                    new PrioritySelector(startUnitsContext => FindUnitsFromIds(StartNpcIds).Where(u => u.Distance < SearchForNpcsRadius),
                                        new Decorator(startUnitsContext => ((IEnumerable<WoWUnit>)startUnitsContext).Count() > 0,
                                            new PrioritySelector(
                                                UtilityBehavior_MoveTo(
                                                    startUnitsContext => true,
                                                    startUnitsContext => FindAllTargets((IEnumerable<WoWUnit>)startUnitsContext).Count() > 0,
                                                    startUnitsContext => FindGroupCenterPoint((IEnumerable<WoWUnit>)startUnitsContext),
                                                    startUnitsContext => "group center"),
                                                new Action(startUnitsContext => _behaviorState = BehaviorStateType.InteractingToStart)
                                            )))),

                                // If no StartNpcs specified, move to the EscortedNpcs when found...
                                new Decorator(escortedUnitsContext => (StartNpcIds.Count() <= 0),
                                    new PrioritySelector(escortedUnitsContext => FindUnitsFromIds(EscortNpcIds).Where(u => u.Distance < SearchForNpcsRadius),
                                        new Decorator(escortedUnitsContext => ((IEnumerable<WoWUnit>)escortedUnitsContext).Count() > 0,
                                            new PrioritySelector(
                                                UtilityBehavior_MoveTo(
                                                    escortedUnitsContext => true,
                                                    escortedUnitsContext => FindAllTargets((IEnumerable<WoWUnit>)escortedUnitsContext).Count() > 0,
                                                    escortedUnitsContext => FindGroupCenterPoint((IEnumerable<WoWUnit>)escortedUnitsContext),
                                                    escortedUnitsContext => "group center"),
                                                new Action(escortedUnitsContext => { _behaviorState = BehaviorStateType.InitializeEscortPath; })
                                            )))),

                                new Decorator(escortedUnitsContext => !IsAnyInCombat(MeAsGroup),
                                    new PrioritySelector(
                                        // HBAPI BUG: HB returns false for CanMount() in valid mounting areas
                                        new Decorator(escortedUnitsContext => !Me.Mounted && Mount.CanMount(),
                                            new Action(escortedUnitsContext => { Mount.MountUp(() => WoWPoint.Empty); })),

                                        // If we've reached the next point in the search path, and there is more than one, update path...
                                        new Decorator(escortedUnitsContext => Me.Location.Distance(_searchPath.Peek()) <= Navigator.PathPrecision
                                                                                && _searchPath.Count() > 1,
                                            new Action(escortedUnitsContext => Utility_RotatePath(_searchPath))),

                                        // Move to next search waypoint as needed...
                                        UtilityBehavior_MoveTo(
                                            escortedUnitsContext => true,
                                            escortedUnitsContext => false,
                                            escortedUnitsContext => _searchPath.Peek(),
                                            escortedUnitsContext => "next search waypoint",
                                            escortedUnitsContext => 5.0),

                                        // If no search path, or only one point, just sit at current position and await
                                        // for NPCs to arrive...
                                        new Decorator(escortedUnitsContext => _searchPath.Count() <= 1,
                                            new CompositeThrottle(TimeSpan.FromSeconds(60),
                                                new Action(escortedUnitsContext => { LogMessage("info", "Waiting for NPCs to arrive"); })))
                                    ))
                                )),
                        #endregion

                        #region State: InteractingToStart
                        // NB:some escorts depop the interaction NPC and immediately replace with the escort-instance version
                        // after selecting the appropriate gossip options.  Do NOT be tempted to check for presence of
                        // correct NPC while in this state--it will hang the behavior tree if it is immediately replaced on gossip.
                        new SwitchArgument<BehaviorStateType>(BehaviorStateType.InteractingToStart,
                            new PrioritySelector(
                                // If no interaction required to start escort, then proceed excorting
                                new Decorator(escortedUnitsContext => StartNpcIds.Count() <= 0,
                                    new Action(escortedUnitsContext => _behaviorState = BehaviorStateType.InitializeEscortPath)),

                                // If in combat while interacting, restart the conversation
                                // TODO: May need to select targets et all here, but no test identified for such yet
                                new Decorator(escortedUnitsContext => IsAnyInCombat(MeAsGroup),
                                    new Action(escortedUnitsContext =>
                                    {
                                        if (GossipFrame.Instance != null)
                                            { GossipFrame.Instance.Close(); }
                                        _behaviorState = BehaviorStateType.SearchingForEscortUnits;
                                    })),
                 
                                // Continue with interaction
                                UtilityBehavior_GossipToStartEscort(),

                                new Action(escortedUnitsContext =>
                                {
                                    if (GossipFrame.Instance != null)
                                        { GossipFrame.Instance.Close(); }
                                    _behaviorState = BehaviorStateType.InitializeEscortPath;
                                })
                            )),
                        #endregion

                        #region State: InitializeEscortPath
                        new SwitchArgument<BehaviorStateType>(BehaviorStateType.InitializeEscortPath,
                            new Action(delegate
                            {
                                // Since the _escortPath is 'consumed' while we travel,
                                // we need to have a full copy of it when starting...
                                // (Behavior must be Honorbuddy stop/start friendly).
                                _escortPath = new Queue<WoWPoint>(_escortPathSpecified);

                                // TODO: Find nearest point, and remove all preceding it
                                WoWPoint nearestEscortPathPoint = _escortPath.OrderBy(p => Me.Location.Distance(p)).FirstOrDefault();
                                while (_escortPath.Peek() != nearestEscortPathPoint)
                                    { _escortPath.Dequeue(); }

                                _behaviorState = BehaviorStateType.Escorting;
                            })
                            ),
                        #endregion

                        #region State: Escorting
                        new SwitchArgument<BehaviorStateType>(BehaviorStateType.Escorting,
                            new PrioritySelector(
                                // Escort complete or failed?
                                new Decorator(escortedUnitsContext => (IsEscortComplete() || IsEscortFailed((IEnumerable<WoWUnit>)escortedUnitsContext)),
                                    new Action(escortedUnitsContext => { _behaviorState = BehaviorStateType.CheckDone; })),
                       
                                // If we are too far behind the Escorted Units, then close distance...
                                // NB: Not falling behind the escort has higher priority than combat
                                UtilityBehavior_MoveTo(
                                    escortedUnitsContext => ((IEnumerable<WoWUnit>)escortedUnitsContext).All(u => u.Distance > EscortMaxFollowDistance),
                                    escortedUnitsContext => false,
                                    escortedUnitsContext => FindGroupCenterPoint((IEnumerable<WoWUnit>)escortedUnitsContext),
                                    escortedUnitsContext => "to keep up with escort",
                                    escortedUnitsContext => 5.0),

                                // If escort units or myself are being attacked, then defend...
                                // NB: We don't use a 'combat' check here.  Instead we look for hostles targeting us or the escort.
                                // This allows us additional time to close distance to the hostiles before they actually hit us.
                                new Decorator(escortedUnitsContext => FindAllTargets((IEnumerable<WoWUnit>)escortedUnitsContext).Count() > 0,
                                    new Sequence(
                                        new DecoratorContinue(escortedUnitsContext => Me.Mounted,
                                            new Action(escortedUnitsContext => { Mount.Dismount(); })),
                                        new Action(escortedUnitsContext => { ChooseBestTarget((IEnumerable<WoWUnit>)escortedUnitsContext); }),
                                        new Decorator(escortedUnitsContext => RoutineManager.Current.CombatBehavior != null,
                                            RoutineManager.Current.CombatBehavior),
                                        new Action(escortedUnitsContext => RoutineManager.Current.Combat())
                                    )),

                                // If escort path was specified (we're leading the escort), process it...
                                new Decorator(escortedUnitsContext => _escortPath.Count() > 0,
                                    new PrioritySelector(
                                        // If we've arrived at the next point, the dequeue it...
                                        new Decorator(escortedUnitsContext => IsWaypointDropNeeded(_escortPath, (IEnumerable<WoWUnit>)escortedUnitsContext, "EscortPath"),
                                            new Action(escortedUnitsContext => _escortPath.Dequeue())),

                                        // Move towards next point...
                                        UtilityBehavior_MoveTo(
                                            escortedUnitsContext => Me.Location.Distance(FindGroupCenterPoint((IEnumerable<WoWUnit>)escortedUnitsContext)) < EscortMaxLeadDistance,
                                            escortedUnitsContext => ((IEnumerable<WoWUnit>)escortedUnitsContext).All(u => u.IsMoving && !u.IsFacing(Me))
                                                                    || ((IEnumerable<WoWUnit>)escortedUnitsContext).All(u => u.Distance > EscortMaxLeadDistance),
                                            escortedUnitsContext => _escortPath.Peek(),
                                            escortedUnitsContext => "escort path waypoint")
                                    ))
                            )),
                        #endregion

                        #region State: CheckDone
                        new SwitchArgument<BehaviorStateType>(BehaviorStateType.CheckDone,
                            new PrioritySelector(
                                new Decorator(escortedUnitsContext => IsEscortComplete(),
                                    new Action(delegate { _isBehaviorDone = true; })),
                                new Action(delegate
                                {
                                    LogMessage("warning", "Looks like we've failed the escort, returning to start to re-do");
                                    _behaviorState = BehaviorStateType.InitialState;
                                })
                            ))
                        #endregion
                    ));
        }


        // Get the weakest mob attacking our weakest escorted unit...
        private void ChooseBestTarget(IEnumerable<WoWUnit> escortedUnits)
        {
            if ((_targetPoiUnit == null) || !_targetPoiUnit.IsValid || _targetPoiUnit.IsDead)
            {
                IEnumerable<WoWUnit>    hostiles = FindAllTargets(escortedUnits).ToList();

                _targetPoiUnit =
                   (from unit in hostiles
                    let attackedEscortUnit = unit.CurrentTarget
                    // The +1 term doesn't change relative weighting, and prevents division by zero in evaluation equation
                    let unitCountAttackingEscortUnit = hostiles.Count(u => unit.CurrentTarget == u.CurrentTarget) +1
                    orderby // evaluation equation:
                        attackedEscortUnit.HealthPercent / unitCountAttackingEscortUnit // prefer low health escorted that are surrounded
                        + unit.HealthPercent                                    // prefer weaker enemies
                        + unit.Location.Distance(attackedEscortUnit.Location)   // prefer nearby mobs
                        + (unit.Elite ? 1000 : 1)                               // prefer non-elite mobs
                        + (unit.IsTargetingMeOrPet ? 100 : 1)                   // prefer targets attacking escorted units (instead of myself/pet)
                    select unit
                    ).FirstOrDefault();
            }

            // If target has strayed, reset to what we want...
            if ((_targetPoiUnit != null) && (Me.CurrentTarget != _targetPoiUnit))
            {
                BotPoi.Current = new BotPoi(_targetPoiUnit, PoiType.Kill);
                _targetPoiUnit.Target();
            }
        }


        // Finds all enemies attacking escorted units, or the myself or pet
        private IEnumerable<WoWUnit> FindAllTargets(IEnumerable<WoWUnit> escortedUnits)
        {
            // NB: Some AoE effect will snag 'neutral' targets, so the test is intentionally
            // for !IsFriendly, instead of IsHostile.
            return
                from unit in ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                where
                    !unit.IsFriendly && !unit.IsPlayer
                    && (unit.IsTargetingMeOrPet || unit.IsTargetingAnyMinion || IsTargettingGroupMember(unit, escortedUnits))
                    && !Blacklist.Contains(unit, BlacklistFlags.Combat)
                select unit;
        }


        private IEnumerable<WoWUnit> FindUnitsFromIds(IEnumerable<int> unitIds)
        {
            return
                from unit in ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                where unitIds.Contains((int)unit.Entry) && unit.IsAlive
                select unit;
        }


        // Returns group center point or, WoWPoint.Empty if group is empty
        private WoWPoint FindGroupCenterPoint(IEnumerable<WoWUnit> groupMembers)
        {
            int groupMemberCount = 0;
            double centerPointX = 0.0;
            double centerPointY = 0.0;
            double centerPointZ = 0.0;

            foreach (WoWUnit wowUnit in groupMembers)
            {
                centerPointX += wowUnit.Location.X;
                centerPointY += wowUnit.Location.Y;
                centerPointZ += wowUnit.Location.Z;
                ++groupMemberCount;
            }

            return (groupMemberCount <= 0)
                    ? Me.Location
                    : (new WoWPoint((centerPointX / groupMemberCount),
                                    (centerPointY / groupMemberCount),
                                    (centerPointZ / groupMemberCount)));
        }


        private bool IsEscortComplete()
        {
            switch (EscortCompleteWhen)
            {
                case EscortCompleteWhenType.DestinationReached:
                    return Me.Location.Distance(EscortCompleteLocation) <= EscortCompleteMaxRange;

                case EscortCompleteWhenType.QuestComplete:
                {
                    PlayerQuest quest = Me.QuestLog.GetQuestById((uint)QuestId);
                    return (quest == null) || quest.IsCompleted;
                }

                case EscortCompleteWhenType.QuestCompleteOrFails:
                {
                    PlayerQuest quest = Me.QuestLog.GetQuestById((uint)QuestId);
                    return (quest == null) || quest.IsCompleted || quest.IsFailed;
                }

                case EscortCompleteWhenType.QuestObjectiveComplete:
                {
                    return (IsQuestObjectiveComplete(QuestId, QuestObjectiveIndex));
                }
            }

            LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: EscortCompleteWhen({0}) state is unhandled", EscortCompleteWhen);
            TreeRoot.Stop();
            return true;
        }


        // Escort fails when 1) quest says so, or 2) there are no more units to escort
        private bool IsEscortFailed(IEnumerable<WoWUnit> escortedUnits)
        {
            bool isFailed = !escortedUnits.Any(u => u.IsValid && u.IsAlive);

            if (QuestId != 0)
            {
                PlayerQuest quest = Me.QuestLog.GetQuestById((uint)QuestId);
                isFailed |= quest.IsFailed;
            }

            return isFailed;
        }


        // returns true, if any member of GROUP (or their pets) is in combat
        private bool IsAnyInCombat(IEnumerable<WoWUnit> group)
        {
            return group.Any(u => u.Combat || ((u.Pet != null) && u.Pet.Combat));
        }


        private bool IsQuestObjectiveComplete(int questId, int objectiveId)
        {
            if (Me.QuestLog.GetQuestById((uint)questId) == null)
                { return false; }

            int questLogIndex = Lua.GetReturnVal<int>(string.Format("return GetQuestLogIndexByID({0})", questId), 0);

            return
                Lua.GetReturnVal<bool>(string.Format("return GetQuestLogLeaderBoard({0},{1})", objectiveId, questLogIndex), 2);
        }
        
        
        // returns true, if WOWUNIT is targeting any member of GROUP (or a group member's pet)
        private bool IsTargettingGroupMember(WoWUnit wowUnit, IEnumerable<WoWUnit> group)
        {
            WoWUnit currentTarget = wowUnit.CurrentTarget;

            return (currentTarget == null)
                ? false
                : group.Any(m => (currentTarget == m) || ((m.Pet != null) && (currentTarget == m.Pet)));
        }


        private bool IsWaypointDropNeeded(Queue<WoWPoint> path, IEnumerable<WoWUnit> escortedUnits, string pathName)
        {
            int waypointCount = path.Count();

             // No more waypoints?
            if (waypointCount == 0)
                { return false; }

            WoWPoint waypointCurrent = path.Peek();
            
            // If toon or any of the escorted crosses the waypoint, then the waypoint is no longer needed...
            if ((Me.Location.Distance(waypointCurrent) < Navigator.PathPrecision)
                || (escortedUnits.Any(u => u.Location.Distance(waypointCurrent) < Navigator.PathPrecision)))
                { return true; }

            // If escortedUnits are walking away from waypoint toward next one, then we can drop the current waypoint...
            //bool isImplicitDropNeeded = (waypointCount <= 1)
            //    ? escortedUnits.All(u => u.IsMoving && !(u.IsFacing(Me) || u.IsFacing(waypointCurrent)))
            //    : escortedUnits.All(u => u.IsMoving && !(u.IsFacing(Me) || u.IsFacing(waypointCurrent)) && u.IsFacing(path.ElementAt(1)));
            bool isImplicitDropNeeded = escortedUnits.All(u => u.IsMoving && !(u.IsFacing(Me) || u.IsFacing(waypointCurrent)));

            if (isImplicitDropNeeded)
            {
                if (string.IsNullOrEmpty(pathName))
                    { pathName = "Unknown path"; }

                LogMessage("warning", "Dropping waypoint {0}--NPCs doesn't want to go that way", path.Peek().ToString());
            }

            return (isImplicitDropNeeded);
        }


        /// <summary>
        /// When STARTMOVINGWHEN is true, this behavior moves to LOCATIONDELEGATE.  When STOPIMMEDIATELYWHEN is true,
        /// or the toon is within PRECISIONDELEGATE of LOCATIONDELEGATE, the behavior stops moving.
        /// If PRECISIONDELEGATE is not supplied, Navigation.PathPrecision is used.
        /// </summary>
        /// <param name="startMovingWhen"></param>
        /// <param name="stopImmediatelyWhen"></param>
        /// <param name="locationDelegate"></param>
        /// <param name="locationNameDelegate"></param>
        /// <param name="precisionDelegate"></param>
        /// <returns>RunStatus.Success while movement is in progress; othwerise, RunStatus.Failure if no movement necessary</returns>
        private Composite UtilityBehavior_MoveTo(CanRunDecoratorDelegate startMovingWhen,
                                                 CanRunDecoratorDelegate stopImmediatelyWhen,
                                                 LocationDelegate locationDelegate,
                                                 MessageDelegate locationNameDelegate,
                                                 RangeDelegate precisionDelegate = null)
        {
            precisionDelegate = precisionDelegate ?? (context => Navigator.PathPrecision);

            return new PrioritySelector(
                // Need to start moving?
                new Decorator(context => !_movementState.IsMoveInProgress && startMovingWhen(context) && !stopImmediatelyWhen(context)
                                            && (Me.Location.Distance(locationDelegate(context)) > precisionDelegate(context)),
                    new Action(context => { _movementState.IsMoveInProgress = true; })),

                // Headed somewhere?
                new Decorator(context => _movementState.IsMoveInProgress,
                    new PrioritySelector(
                        // Did we arrive at destination?
                        new Decorator(context => Me.Location.Distance(locationDelegate(context)) <= precisionDelegate(context),
                            new Action(context => { _movementState.IsMoveInProgress = false; })),

                        // Did stop trigger activate?
                        new Decorator(context => stopImmediatelyWhen(context),
                            new Action(context =>
                            {
                                if (Me.IsMoving)
                                    { WoWMovement.MoveStop(); }
                                _movementState.IsMoveInProgress = false;
                            })),

                        // Notify user of progress...
                        new CompositeThrottle(TimeSpan.FromSeconds(1),
                            new Action(context =>
                            {
                                double destinationDistance = Me.Location.Distance(locationDelegate(context));
                                string locationName = locationNameDelegate(context) ?? locationDelegate(context).ToString();
                                LogMessage("info", "Moving to {0}", locationName, /*{1:F1}*/ destinationDistance);
                                return RunStatus.Failure; // fall through after notifying user
                            })),

                        // Conduct movement...
                        new Action(context =>
                        {
                            WoWPoint destination = locationDelegate(context);

                            // Try to use Navigator to get there...
                            MoveResult moveResult = Navigator.MoveTo(destination);

                            // If Navigator fails, fall back to click-to-move...
                            if ((moveResult == MoveResult.Failed) || (moveResult == MoveResult.PathGenerationFailed))
                                { WoWMovement.ClickToMove(destination); }

                            return RunStatus.Failure; // fall through for delay
                        }),

                        new WaitContinue(Delay_WoWClientMovementThrottle, ret => false, new ActionAlwaysSucceed())
                    ))
            );
        }
        
        // Wait for group to arrive at starting location, when present interact to start escort --
        // When the group is within "StartNpcMaxRange" of our current position, we will interact
        // with the nearest NPC to start the escort.
        private Composite UtilityBehavior_GossipToStartEscort()
        {
            return new PrioritySelector(gossipUnitContext => FindUnitsFromIds(StartNpcIds).OrderBy(u => u.Distance).FirstOrDefault(),
                new Decorator(gossipUnitContext => gossipUnitContext != null,
                    new PrioritySelector(
                        // Move to closest unit...
                        UtilityBehavior_MoveTo(
                            gossipUnitContext => true,
                            gossipUnitContext => false,
                            gossipUnitContext => ((WoWUnit)gossipUnitContext).Location,
                            gossipUnitContext => ((WoWUnit)gossipUnitContext).Name,
                            gossipUnitContext => ((WoWUnit)gossipUnitContext).InteractRange),

                        // Interact with unit to open the Gossip dialog...
                        new Decorator(gossipUnitContext => (GossipFrame.Instance == null) || !GossipFrame.Instance.IsVisible,
                            new Sequence(
                                new Action(gossipUnitContext => ((WoWUnit)gossipUnitContext).Target()),
                                new Action(gossipUnitContext => LogMessage("info", "Interacting with \"{0}\" to start escort.", ((WoWUnit)gossipUnitContext).Name)),
                                new Action(gossipUnitContext => ((WoWUnit)gossipUnitContext).Interact()),
                                new WaitContinue(LagDuration, ret => GossipFrame.Instance.IsVisible, new ActionAlwaysSucceed()),
                                new Action(gossipUnitContext => _gossipOptionIndex = 0),
                                new WaitContinue(Delay_GossipDialogThrottle, ret => GossipFrame.Instance.IsVisible, new ActionAlwaysSucceed())
                            )),

                        // Choose appropriate gossip options...
                        // NB: If we get attacked while gossiping, and the dialog closes, then it will automatically be retried.
                        new Decorator(gossipUnitContext => (_gossipOptionIndex < StartEscortGossipOptions.Length)
                                                            && (GossipFrame.Instance != null) && GossipFrame.Instance.IsVisible,
                                new Sequence(
                                    new Action(gossipUnitContext => GossipFrame.Instance.SelectGossipOption(StartEscortGossipOptions[_gossipOptionIndex])),
                                    new Action(gossipUnitContext => ++_gossipOptionIndex),
                                    new WaitContinue(Delay_GossipDialogThrottle, ret => false, new ActionAlwaysSucceed())
                                ))
                    )));
        }


        private string Utility_GetNamesOfUnits(IEnumerable<WoWUnit> wowUnits)
        {
            return (wowUnits.Count() > 0) ?
                string.Join(", ", wowUnits.Select(u => u.Name))
                : "NPCs";
        }


        private void Utility_RotatePath(Queue<WoWPoint> path)
        {
            WoWPoint frontPoint = path.Dequeue();
            path.Enqueue(frontPoint);
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
                    { return false; }

                _throttle.Restart();
                return true;
            }

            private readonly Stopwatch _throttle;
            private readonly TimeSpan _throttleTime;
        }
        #endregion


        #region Path parsing
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
        #endregion
    }
}

