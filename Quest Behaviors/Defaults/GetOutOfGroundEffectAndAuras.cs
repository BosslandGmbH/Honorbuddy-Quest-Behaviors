// TODO:
// - Allow preferred targets
// - Move to initial state if we die (need death behavior hook)

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
//      StartNpcIdN [REQUIRED if StartEscortGossipOptions specified]
//          N may be omitted, or any numeric value--multiple mobs are supported.
//          If NPC interaction is required to start the escort, the StartNpcIdN identifieds one
//          or more NPCs that may be interacted with to initiate the escort.
//          If the quest fails for some reason, the behavior will return to these NPCs
//          to try again.
//      StartEscortGossipOptions [optional; Default: first gossip option on first dialog panel]
//          Specifies the set of interaction choices on each gossip panel to make, in order to
//          start the escort.
//          The value of this attribute is a comma-separated list of gossip options (e.g., "1,3,1").
//
//      EscortNpcIdN [REQUIRED]
//          N may be omitted, or any numeric value--multiple mobs are supported.
//          Identifies one or more NPCs to be escorted.
//      EscortMaxFightDistance [optional; Default: 27.0]
//          [on the close ranged: [5.0..100.0]]
//          The maximum range from the _nearest_ escorted NPC at which the toon will fight.
//          If for some reason, the toon is pulled outside of this range, the behavior
//          moves the toon back into this maximum range.
//      EscortMaxFollowDistance [optional; Default: 15.0]
//          [on the closed ranged: [3.0..100.0]]
//          The maximum range at which the toon will follow the escorted NPCs while not
//          in combat.  If a group of NPCs is being escorted, this distance is measured
//          from the group center point.
//
//      EscortCompleteWhen [optional; Default: QuestComplete]
//          [allowed values: DestinationReached/QuestComplete/QuestCompleteOrFails]
//          Defines the completion criteria for the behavior.
//      EscortCompleteX/EscortCompleteY/EscortCompleteZ [REQUIRED if EscortCompleteWhen=DestinationReached]
//          Defines the location at which the behavior is considered complete.  This value is only used
//          if EscortComplete is DestinationReached.
//      EscortCompleteMaxRange [optional; Default: 10.0]
//          Defines the tolerance from the EscortCompleteX/Y/Z for which the behavior is considered
//          complete.  This value is only used if EscortCompelte is DestinationReached.
//
//      QuestId [REQUIRED if EscortCompleteWhen=QuestComplete; Default:none]:
//      QuestCompleteRequirement [Default:NotComplete]:
//      QuestInLogRequirement [Default:InLog]:
//              A full discussion of how the Quest* attributes operate is described in
//              http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
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
// * Do 'fan out' as we travel, so multiple toons don't stack up in the exact same spots
// * Timeouts: 1) While searching for NPCs, 2) for overall quest completion
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
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.QuestBehaviors.GetOutOfGroundEffectAndAuras
{
    public class GetOutOfGroundEffectAndAuras : CustomForcedBehavior
    {
        public delegate WoWPoint LocationDelegate(object context);
        public delegate string MessageDelegate(object context);
        public delegate double RangeDelegate(object context);

        #region Consructor and Argument Processing
        public enum EventCompleteWhenType
        {
            QuestComplete,
            QuestCompleteOrFails,
            SpecificMobsDead,
        }

        public enum MovementByType
        {
            ClickToMoveOnly,
            NavigatorOnly,
            NavigatorPreferred,
        }

        public GetOutOfGroundEffectAndAuras(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // Parameters dealing with 'starting' the behavior...
                StartEventGossipOptions = GetAttributeAsArray<int>("StartEventGossipOptions", false, new ConstrainTo.Domain<int>(1, 10), null, null);
                StartNpcIds = GetNumberedAttributesAsArray<int>("StartNpcId", 0, ConstrainAs.MobId, null);
                StartLocation = GetAttributeAsNullable<WoWPoint>("Start", false, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;


                // Parameters dealing with avoidance during battle...
                MoveAwayFromMobWithAuraIds = GetNumberedAttributesAsArray<int>("MoveAwayFromMobWithAuraId", 0, ConstrainAs.SpellId, null);
                MoveAwayFromMobCastingSpellIds = GetNumberedAttributesAsArray<int>("MoveAwayFromMobCastingSpellId", 0, ConstrainAs.SpellId, null);
                MoveBehindMobCastingSpellIds = GetNumberedAttributesAsArray<int>("MoveBehindMobCastingSpellId", 0, ConstrainAs.SpellId, null);
                AvoidMobMinRange = GetAttributeAsNullable<double>("AvoidMobMinRange", false, new ConstrainTo.Domain<double>(5.0, 100.0), null) ?? 25.0;
                MoveOutOfGroundEffectAuraIds = GetNumberedAttributesAsArray<int>("MoveOutOfGroundEffectAuraId", 0, ConstrainAs.SpellId, null);
                PreferKillingMobIds = GetNumberedAttributesAsArray<int>("PreferKillingMobId", 0, ConstrainAs.MobId, null);

                // Parameters dealing with when the task is 'done'...
                EventCompleteDeadMobIds = GetNumberedAttributesAsArray<int>("EventCompleteDeadMobId", 0, ConstrainAs.MobId, null);
                EventCompleteWhen = GetAttributeAsNullable<EventCompleteWhenType>("EventCompleteWhen", false, null, null) ?? EventCompleteWhenType.QuestComplete;

                // Quest handling...
                QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;

                // Behavior tunables (profiles shouldn't specify these unless working around bugs)...
                MovementBy = GetAttributeAsNullable<MovementByType>("MovementBy", false, null, null) ?? MovementByType.NavigatorPreferred;


                // Semantic Coherency checks --
                if ((StartEventGossipOptions.Count() > 0) && (StartNpcIds.Count() <= 0))
                {
                    LogMessage("error", "If StartEscortGossipOptions are specified, you must also specify one or more StartNpcIdN");
                    IsAttributeProblem = true;
                }

                if ((QuestId == 0)
                    && ((EventCompleteWhen == EventCompleteWhenType.QuestComplete)
                        || (EventCompleteWhen == EventCompleteWhenType.QuestCompleteOrFails)))
                {
                    LogMessage("error", "With a EventCompleteWhen argument of QuestComplete, you must specify a QuestId argument");
                    IsAttributeProblem = true;
                }

                if ((EventCompleteWhen == EventCompleteWhenType.SpecificMobsDead) && (EventCompleteDeadMobIds.Count() <= 0))
                {
                    LogMessage("error", "With a EventCompleteWhen argument of SpecificMobsDead, you must specify one or more EventCompleteDeadMobIdN argument");
                    IsAttributeProblem = true;
                }

                if ((MoveAwayFromMobWithAuraIds.Count() <= 0)
                    && (MoveAwayFromMobCastingSpellIds.Count() <= 0)
                    && (MoveBehindMobCastingSpellIds.Count() <= 0)
                    && (MoveOutOfGroundEffectAuraIds.Count() <= 0)
                    && (PreferKillingMobIds.Count() <= 0))
                {
                    LogMessage("error", "None of MoveAwayFromMobWithAuraIdN, MoveAwayFromMobCastingSpellIdN, MoveBehindMobCastingSpellIdN,"
                                        + " MoveOutOfGroundEffectAuraIdN, or PreferKillingMobIdN were specified");
                    IsAttributeProblem = true;
                }

                // If no gossip options specified, set up options for 'interaction without a dialog'...
                if (StartEventGossipOptions.Count() == 0)
                    { StartEventGossipOptions = new int[] { 0 }; }

                for (int i = 0; i < StartEventGossipOptions.Length; ++i)
                    { StartEventGossipOptions[i] -= 1; }
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
        private int[] StartEventGossipOptions { get; set; }
        private int[] StartNpcIds { get; set; }
        private WoWPoint StartLocation { get; set; }

        private double AvoidMobMinRange { get; set; }
        private int[] MoveAwayFromMobWithAuraIds { get; set; }
        private int[] MoveAwayFromMobCastingSpellIds { get; set; }
        private int[] MoveBehindMobCastingSpellIds { get; set; }
        private int[] MoveOutOfGroundEffectAuraIds { get; set; }
        private int[] PreferKillingMobIds { get; set; }
        
        private int[] EventCompleteDeadMobIds { get; set; }
        private EventCompleteWhenType EventCompleteWhen { get; set; }

        private int QuestId { get; set; }
        private QuestCompleteRequirement QuestRequirementComplete { get; set; }
        private QuestInLogRequirement QuestRequirementInLog { get; set; }

        // Tunables
        private MovementByType MovementBy { get; set; }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return "$Id$"; } }
        public override string SubversionRevision { get { return "$Rev$"; } }
        #endregion


        #region Private and Convenience variables
        private enum BehaviorStateType
        {
            InitialState,
            SearchingForStartUnits,
            InteractingToStart,
            MainEvent,
            CheckDone,
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
        private ConfigMemento _configMemento = null;
        private int _gossipOptionIndex;
        private bool _isBehaviorDone = false;
        private bool _isDisposed = false;
        private List<WoWPoint> _safespots = null;
        private WoWPoint _toonStartingPosition = StyxWoW.Me.Location;
        private Stopwatch _waitForStartTimer = new Stopwatch();
        #endregion


        #region Destructor, Dispose, and cleanup
        ~GetOutOfGroundEffectAndAuras()
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
                    TreeHooks.Instance.RemoveHook("Combat_Main", _behaviorTreeHook_Combat);
                    _behaviorTreeHook_Combat = null;
                }

                if (_behaviorTreeHook_Death != null)
                {
                    TreeHooks.Instance.RemoveHook("Death_Main", _behaviorTreeHook_Death);
                    _behaviorTreeHook_Death = null;
                }

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
        // NB: This behavior is designed to run at a 'higher priority' than Combat_Main.
        // This is necessary because movement is frequently more important than combat when conducting
        // escorts (e.g., the escort doesn't wait for us to kill the mob).  Be aware that this priority
        // inversion is not what you are used to seeing in most quest behaviors, and results in some
        // 'different' combinations of PrioritySelector and Sequence.
        // NB: Due to the complexity, this behavior is also 'state' based.  All necessary actions are
        // conducted in the current state.  If the current state is no longer valid, then a state change
        // is effected.  Ths entry state is "MovingToStartLocation".
        protected override Composite CreateBehavior()
        {
            return CreateMainBehavior();
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

            _safespots = ParsePath("Safespots");

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
                CharacterSettings.Instance.PullDistance = 25;
                
                TreeRoot.GoalText = string.Format(
                    "{0}: \"{1}\"\nLooting and Harvesting are disabled while event in progress",
                    this.GetType().Name,
                    ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress (no associated quest)"));

                // If search path not provided, use our current location...
                if (_safespots.Count() <= 0)
                    { _safespots.Add(Me.Location); }

                _toonStartingPosition = Me.Location;

                _behaviorState = BehaviorStateType.InitialState;
                _behaviorTreeHook_Death = CreateDeathBehavior();
                TreeHooks.Instance.InsertHook("Death_Main", 0, _behaviorTreeHook_Death);
                _behaviorTreeHook_Combat = CreateCombatBehavior();
                TreeHooks.Instance.InsertHook("Combat_Main", 0, _behaviorTreeHook_Combat);
            }
        }
        #endregion


        #region Main Behavior

        protected Composite CreateCombatBehavior()
        {
            // NB: This behavior is hooked in at a 'higher priority' than Combat_Main.  We need this
            // because it is sometimes more important to avoid things than fight.

            // NB: We might be in combat with nothing to fight immediately after initiating the event.
            // Be aware of this when altering this behavior.

            return new PrioritySelector(targetUnitsContext => FindAllTargets(),

                // Move away from mob with Aura...
                new PrioritySelector(targetUnitsContext => NearestMobWithAura((IEnumerable<WoWUnit>)targetUnitsContext, MoveAwayFromMobWithAuraIds),
                    new Decorator(nearestAvoidMobContext => (nearestAvoidMobContext != null)
                                                            && (((WoWUnit)nearestAvoidMobContext).Distance < AvoidMobMinRange),
                        UtilityBehavior_MoveWithinRange(nearestAvoidMobContext => PreferredSafespot((WoWUnit)nearestAvoidMobContext),
                            nearestAvoidMobContext => string.Format("away from mob with '{0}' aura",
                                UnitAuraFromAuraIds((WoWUnit)nearestAvoidMobContext, MoveAwayFromMobWithAuraIds).Name))
                    )),

                // Move away from mob casting particular AoE spells...
                new PrioritySelector(targetUnitsContext => NearestMobCastingSpell((IEnumerable<WoWUnit>)targetUnitsContext, MoveAwayFromMobCastingSpellIds),
                    new Decorator(nearestAuraMobContext => (nearestAuraMobContext != null)
                                                                && (((WoWUnit)nearestAuraMobContext).Distance < AvoidMobMinRange),
                        UtilityBehavior_MoveWithinRange(nearestAuraMobContext => PreferredSafespot((WoWUnit)nearestAuraMobContext),
                            nearestAuraMobContext => string.Format("away from mob casting '{0}'",
                                UnitSpellFromCastingIds((WoWUnit)nearestAuraMobContext, MoveAwayFromMobCastingSpellIds).Name))
                    )),

                // Move behind mobs casting particular AoE spells...
                new PrioritySelector(targetUnitsContext => NearestMobCastingSpell((IEnumerable<WoWUnit>)targetUnitsContext, MoveBehindMobCastingSpellIds),
                    new Decorator(nearestBehindMobContext => (nearestBehindMobContext != null)
                                                                && (!Me.IsSafelyBehind((WoWUnit)nearestBehindMobContext)
                                                                    || !Me.IsSafelyFacing((WoWUnit)nearestBehindMobContext)),
                        new PrioritySelector(
                            UtilityBehavior_MoveWithinRange(nearestBehindMobContext => SafespotBehindMob((WoWUnit)nearestBehindMobContext,
                                                                                            ((WoWUnit)nearestBehindMobContext).CombatReach),
                                nearestBehindMobContext => string.Format("behind from mob casting '{0}'",
                                    UnitSpellFromCastingIds((WoWUnit)nearestBehindMobContext, MoveBehindMobCastingSpellIds).Name),
                                nearestBehindMobContext => ((WoWUnit)nearestBehindMobContext).CombatReach),
                            new Decorator(nearestBehindMobContext => !Me.IsSafelyFacing((WoWUnit)nearestBehindMobContext),
                                new Action(nearestBehindMobContext => { ((WoWUnit)nearestBehindMobContext).Face(); }))
                        ))),

                // Move out of ground effect...
                new PrioritySelector(targetUnitsContext => ((IEnumerable<WoWUnit>)targetUnitsContext).FirstOrDefault(),
                    new Decorator(targetUnitContext => (targetUnitContext != null) && (GroundEffectFromIds(MoveOutOfGroundEffectAuraIds) != null),
                        UtilityBehavior_MoveWithinRange(targetUnitContext => PreferredSafespot((WoWUnit)targetUnitContext, true),
                            targetUnitContext => string.Format("out of '{0}' ground effect", GroundEffectFromIds(MoveOutOfGroundEffectAuraIds).Name))
                    )),

                // If a preferred target is available and not targeted, switch targets...
                new PrioritySelector(preferredUnitContext => FindUnitsFromIds(PreferKillingMobIds).FirstOrDefault(),
                    new Decorator(preferredUnitContext => (preferredUnitContext != null)
                                            && ((Me.CurrentTarget == null) || !PreferKillingMobIds.Contains((int)Me.CurrentTarget.Entry)),
                        new Action(preferredUnitContext =>
                        {
                            LogMessage("info", "Reprioritizing target to '{0}'", ((WoWUnit)preferredUnitContext).Name);
                            BotPoi.Current = new BotPoi((WoWUnit)preferredUnitContext, PoiType.Kill);
                        }))
                    )
                );
        }


        protected Composite CreateDeathBehavior()
        {
            // If toon dies, we need to restart behavior
            return new Decorator(context => (Me.IsDead || Me.IsGhost) && (_behaviorState != BehaviorStateType.CheckDone),
                new Action(context => { _behaviorState = BehaviorStateType.CheckDone; }));
        }


        protected Composite CreateMainBehavior()
        {
            // Let other behaviors deal with toon death and path back to corpse...
            return new PrioritySelector(
                    new Decorator(context => _isBehaviorDone,
                        new Action(context => { LogMessage("info", "Behavior Finished"); })),

                    new Switch<BehaviorStateType>(context => _behaviorState,
                        new Action(context =>   // default case
                        {
                            LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: BehaviorState({0}) is unhandled", _behaviorState);
                            TreeRoot.Stop();
                            _isBehaviorDone = true;
                        }),

                        new SwitchArgument<BehaviorStateType>(BehaviorStateType.InitialState,
                            new PrioritySelector(
                                UtilityBehavior_MoveWithinRange(context => _toonStartingPosition,
                                                                context => "to start location"),
                                new Action(context => { _behaviorState = BehaviorStateType.SearchingForStartUnits; })
                            )),

                        new SwitchArgument<BehaviorStateType>(BehaviorStateType.SearchingForStartUnits,
                            new PrioritySelector(
                                // If no StartNpcs specified, move on to main event...
                                new Decorator(context => (StartNpcIds.Count() <= 0),
                                    new Action(context => _behaviorState = BehaviorStateType.MainEvent)),

                                // If Start NPCs specified, move to them when found...
                                new PrioritySelector(startUnitsContext => FindUnitsFromIds(StartNpcIds),
                                    new Decorator(startUnitsContext => ((IEnumerable<WoWUnit>)startUnitsContext).Count() > 0,
                                        new PrioritySelector(nearestStartUnitContext => ((IEnumerable<WoWUnit>)nearestStartUnitContext).OrderBy(u => u.Distance).FirstOrDefault(),
                                            UtilityBehavior_MoveWithinRange(nearestStartUnitContext => ((WoWUnit)nearestStartUnitContext).Location,
                                                                            nearestStartUnitContext => string.Format("to {0}", ((WoWUnit)nearestStartUnitContext).Name),
                                                                            nearestStartUnitContext => ((WoWUnit)nearestStartUnitContext).InteractRange),
                                            new Action(startUnitsContext => _behaviorState = BehaviorStateType.InteractingToStart)
                                        ))),

                                new CompositeThrottle(TimeSpan.FromSeconds(10),
                                    new Action(escortedUnitsContext =>
                                    {
                                        LogMessage("warning", "Unable to locate start units: {0}",
                                                            Utility_GetNamesOfUnits(StartNpcIds));
                                    }))
                            )),

                        // NB:some events depop the interaction NPC and immediately replace with the instanced version
                        // after selecting the appropriate gossip options.  Do NOT be tempted to check for presence of
                        // correct NPC while in this state--it will hang the behavior tree if the NPC
                        // is immediately replaced on gossip.
                        new SwitchArgument<BehaviorStateType>(BehaviorStateType.InteractingToStart,
                            new PrioritySelector(
                                // If no interaction required to start escort, then proceed excorting
                                new Decorator(escortedUnitsContext => StartNpcIds.Count() <= 0,
                                    new Action(escortedUnitsContext => _behaviorState = BehaviorStateType.MainEvent)),

                                // If in combat while interacting, restart the conversation
                                new Decorator(escortedUnitsContext => IsInCombat(MeAsGroup),
                                    new Action(escortedUnitsContext =>
                                    {
                                        if (GossipFrame.Instance != null)
                                            { GossipFrame.Instance.Close(); }
                                        _behaviorState = BehaviorStateType.SearchingForStartUnits;
                                    })),

                                // Continue with interaction
                                UtilityBehavior_GossipToStartEvent(),

                                new Action(escortedUnitsContext =>
                                {
                                    if (GossipFrame.Instance != null)
                                        { GossipFrame.Instance.Close(); }
                                    _behaviorState = BehaviorStateType.MainEvent;
                                })
                            )),

                        new SwitchArgument<BehaviorStateType>(BehaviorStateType.MainEvent,
                            new PrioritySelector(
                                // Main event doesn't have a whole lot to do, just check for complete or failed...
                                new Decorator(context => IsEventComplete() || IsEventFailed(),
                                    new Action(context => { _behaviorState = BehaviorStateType.CheckDone; })),

                                new Decorator(context => (StartLocation != WoWPoint.Empty) && (Me.Location.Distance(StartLocation) > Navigator.PathPrecision),
                                    UtilityBehavior_MoveWithinRange(context => StartLocation, context => "to start location")),

                                new ActionAlwaysFail()
                            )),

                        new SwitchArgument<BehaviorStateType>(BehaviorStateType.CheckDone,
                            new PrioritySelector(
                                new Decorator(context => IsEventComplete(),
                                    new Action(delegate { _isBehaviorDone = true; })),
                                new Action(delegate
                                {
                                    LogMessage("info", "Looks like we've failed the event, returning to start to re-do");
                                    _behaviorState = BehaviorStateType.InitialState;
                                })
                            ))
                    ));
        }


        // Finds all enemies attacking escorted units, or the myself or pet
        private IEnumerable<WoWUnit> FindAllTargets()
        {
            IEnumerable<WoWUnit> targetsQuery = 
                from unit in ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                where unit.IsValid && IsAttackingMeOrPet(unit) && !Blacklist.Contains(unit, BlacklistFlags.Combat)
                select unit;

            return (targetsQuery.ToList());
        }


        private IEnumerable<WoWUnit> FindUnitsFromIds(IEnumerable<int> unitIds)
        {
            return ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                .Where(u => unitIds.Contains((int)u.Entry) && u.IsValid && u.IsAlive)
                .ToList();
        }


        /// <summary>
        /// Returns the WoWAura if we are standing in one of the ground effects
        /// identified by GROUNDEFFECTSPELLIDS.
        /// </summary>
        /// <param name="groundEffectSpellIds"></param>
        /// <returns>may return null</returns>
        private WoWAura GroundEffectFromIds(int[] groundEffectSpellIds)
        {
            return Me.Auras.Values.FirstOrDefault(a => groundEffectSpellIds.Contains(a.SpellId));
        }


        /// <summary>
        /// We rolled our own function here, since WoWUnit.Aggro isn't documented as considering the pet
        /// </summary>
        /// <param name="wowUnit"></param>
        /// <returns>Returns true if the WoWUnit is attacking the player or his pet</returns>
        private bool IsAttackingMeOrPet(WoWUnit wowUnit)
        {
            return (wowUnit.Combat
                    && ((wowUnit.CurrentTarget == Me) || ((Me.Pet != null) && (wowUnit.CurrentTarget == Me.Pet))));
        }


        private bool IsEventComplete()
        {
            switch (EventCompleteWhen)
            {
                case EventCompleteWhenType.QuestComplete:
                {
                    PlayerQuest quest = Me.QuestLog.GetQuestById((uint)QuestId);
                    if (quest.IsCompleted)
                        { LogMessage("Event done due to Quest(\"{0}\", {1}) complete", quest.Name, quest.Id); }
                    return (quest == null) || quest.IsCompleted;
                }

                case EventCompleteWhenType.QuestCompleteOrFails:
                {
                    PlayerQuest quest = Me.QuestLog.GetQuestById((uint)QuestId);
                    if (quest.IsCompleted)
                        { LogMessage("Event done due to Quest(\"{0}\", {1}) complete", quest.Name, quest.Id); }
                    if (quest.IsCompleted)
                        { LogMessage("Event done due to Quest(\"{0}\", {1}) failed", quest.Name, quest.Id); }
                    return (quest == null) || quest.IsCompleted || quest.IsFailed;
                }
                
                case EventCompleteWhenType.SpecificMobsDead:
                {
                    WoWUnit ourDeadTarget =
                        ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                        .Where(u => EventCompleteDeadMobIds.Contains((int)u.Entry) && u.IsDead && u.TaggedByMe)
                        .FirstOrDefault();

                    if (ourDeadTarget != null)
                        { LogMessage("Event done due to killing '{0}'({1})", ourDeadTarget.Name, ourDeadTarget.Entry); }
                    return (ourDeadTarget != null);
                }
            }

            LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: EventCompleteWhen({0}) state is unhandled", EventCompleteWhen);
            TreeRoot.Stop();
            return true;
        }


        /// <returns>returns true if the QUESTID exists in our log, and has failed</returns>
        private bool IsEventFailed()
        {
            bool isFailed = false;

            if (QuestId != 0)
            {
                PlayerQuest quest = Me.QuestLog.GetQuestById((uint)QuestId);
                isFailed |= quest.IsFailed;
            }

            return isFailed;
        }


        /// <returns>returns true, if any member of GROUP (or their pets) is in combat</returns>
        private bool IsInCombat(IEnumerable<WoWUnit> group)
        {
            return group.Any(u => u.Combat || ((u.Pet != null) && u.Pet.Combat));
        }


        /// <summary>
        /// Returns the nearest mob in TARGETS casting one of the SPELLIDs
        /// </summary>
        /// <param name="targets"></param>
        /// <param name="spellIds"></param>
        /// <returns>may return null</returns>
        private WoWUnit NearestMobCastingSpell(IEnumerable<WoWUnit> targets, int[] spellIds)
        {
            IEnumerable<WoWUnit> targetQuery =
                from target in targets
                where UnitSpellFromCastingIds(target, spellIds) != null
                orderby target.Distance
                select target;

            return (targetQuery.FirstOrDefault());
        }


        /// <summary>
        /// Returns the nearest mob in TARGETS that has an aura of one of the AURAIDS
        /// </summary>
        /// <param name="targets"></param>
        /// <param name="auraIds"></param>
        /// <returns>may return null</returns>
        private WoWUnit NearestMobWithAura(IEnumerable<WoWUnit> targets, int[] auraIds)
        {
            IEnumerable<WoWUnit> targetQuery =
                from target in targets
                where target.Auras.Values.Any(a => auraIds.Contains(a.SpellId))
                orderby target.Distance
                select target;

            return (targetQuery.FirstOrDefault());
        }


        /// <returns>returns WoWPoint away from the TARGETUNIT</returns>
        private WoWPoint PreferredSafespot(WoWUnit targetunit, bool isGroundEffectProblem = false)
        {
            IEnumerable<WoWPoint> preferredSafespotOrder;

            preferredSafespotOrder =
                from spot in _safespots
                // The +1 term guarantees distances are never less than 1.  This prevents
                // degenerate cases in preference evaluation for distances less than one, or zero.
                let myDistanceToSpot = Me.Location.Distance(spot) +1
                let mobDistanceToSpot = targetunit.Location.Distance(spot) +1
                orderby // preference ordering equation:
                    // prefer spots that are close to me, but not mob
                    (myDistanceToSpot / mobDistanceToSpot)
                    // If ground effect problem, avoid any nearby spots
                    + ((isGroundEffectProblem && (myDistanceToSpot < AvoidMobMinRange)) ? 100 : 0)
                    // prefer spots away from mob
                    + ((targetunit.Location.Distance(spot) < AvoidMobMinRange) ? 1000 : 0)
                    // prefer spots the toon doesn't have to run through the mob
                    + (WoWMathHelper.IsInPath(targetunit, Me.Location, spot) ? 10000 : 0)
                select spot;

            return preferredSafespotOrder.FirstOrDefault();
        }


        /// <returns>Returns a WoWPoint at DISTANCE behind UNIT</returns>
        private WoWPoint SafespotBehindMob(WoWUnit unit, double distance)
        {
            return unit.Location.RayCast(unit.Rotation + (float)Math.PI, (float)distance);
        }
   
   
        /// <returns>Returns the WoWAura if UNIT has one of the AURAIDS; otherwise, returns null</returns>
        private WoWAura UnitAuraFromAuraIds(WoWUnit unit, int[] auraIds)
        {
            return unit.Auras.Values.FirstOrDefault(a => auraIds.Contains(a.SpellId));
        }
        
        
        /// <returns>Returns the WoWSpell if UNIT is casting one of the SPELLIDS; otherwise, returns null</returns>
        private WoWSpell UnitSpellFromCastingIds(WoWUnit unit, int[] spellIds)
        {
            return (unit.IsCasting ? (spellIds.Contains(unit.CastingSpellId) ? unit.CastingSpell : null)
                : unit.IsChanneling ? (spellIds.Contains(unit.ChanneledCastingSpellId) ? unit.ChanneledSpell : null)
                : null);
        }


        /// <returns>Returns RunStatus.Success if gossip in progress; otherwise, RunStatus.Failure if gossip complete or unnecessary</returns>
        private Composite UtilityBehavior_GossipToStartEvent()
        {
            return new PrioritySelector(gossipUnitContext => FindUnitsFromIds(StartNpcIds).OrderBy(u => u.Distance).FirstOrDefault(),
                new Decorator(gossipUnitContext => gossipUnitContext != null,
                    new PrioritySelector(
                        // Move to closest unit...
                        UtilityBehavior_MoveWithinRange(gossipUnitContext => ((WoWUnit)gossipUnitContext).Location,
                                                gossipUnitContext => ((WoWUnit)gossipUnitContext).Name,
                                                gossipUnitContext => ((WoWUnit)gossipUnitContext).InteractRange),

                        // Interact with unit to open the Gossip dialog...
                        new Decorator(gossipUnitContext => (GossipFrame.Instance == null) || !GossipFrame.Instance.IsVisible,
                            new Sequence(
                                new Action(gossipUnitContext => ((WoWUnit)gossipUnitContext).Target()),
                                new Action(gossipUnitContext => LogMessage("info", "Interacting with \"{0}\" to start event.", ((WoWUnit)gossipUnitContext).Name)),
                                new Action(gossipUnitContext => ((WoWUnit)gossipUnitContext).Interact()),
                                new WaitContinue(LagDuration, ret => GossipFrame.Instance.IsVisible, new ActionAlwaysSucceed()),
                                new WaitContinue(Delay_GossipDialogThrottle, ret => GossipFrame.Instance.IsVisible, new ActionAlwaysSucceed()),
                                new Action(gossipUnitContext =>
                                {
                                    _gossipOptionIndex = 0;

                                    // If no dialog is expected, we're done...
                                    if (StartEventGossipOptions[_gossipOptionIndex] < 0)
                                        { return RunStatus.Failure; }
                                    return RunStatus.Success;
                                })
                            )),

                        // Choose appropriate gossip options...
                        // NB: If we get attacked while gossiping, and the dialog closes, then it will automatically be retried.
                        new Decorator(gossipUnitContext => (_gossipOptionIndex < StartEventGossipOptions.Length)
                                                            && (GossipFrame.Instance != null) && GossipFrame.Instance.IsVisible,
                                new Sequence(
                                    new Action(gossipUnitContext => GossipFrame.Instance.SelectGossipOption(StartEventGossipOptions[_gossipOptionIndex])),
                                    new Action(gossipUnitContext => ++_gossipOptionIndex),
                                    new WaitContinue(Delay_GossipDialogThrottle, ret => false, new ActionAlwaysSucceed())
                                ))
                    )));
        }


        /// <returns>RunStatus.Success while movement is in progress; othwerise, RunStatus.Failure if no movement necessary</returns>
        private Composite UtilityBehavior_MoveWithinRange(LocationDelegate locationDelegate,
                                                            MessageDelegate locationNameDelegate,
                                                            RangeDelegate precisionDelegate = null)
        {
            precisionDelegate = precisionDelegate ?? (context => Navigator.PathPrecision);

            return new Sequence(
                // Done, if we're already at destination...
                new DecoratorContinue(context => (Me.Location.Distance(locationDelegate(context)) <= precisionDelegate(context)),
                    new Decorator(context => Me.IsMoving,   // This decorator failing indicates the behavior is complete
                        new Action(delegate { WoWMovement.MoveStop(); }))),

                // Notify user of progress...
                new CompositeThrottle(TimeSpan.FromSeconds(1),
                    new Action(context =>
                    {
                        double destinationDistance = Me.Location.Distance(locationDelegate(context));
                        string locationName = locationNameDelegate(context) ?? locationDelegate(context).ToString();
                        LogMessage("info", string.Format("Moving {0}", locationName));
                    })
                    ),

                new Action(context =>
                {
                    WoWPoint destination = locationDelegate(context);
                    MoveResult moveResult = MoveResult.Failed;

                    // Try to use Navigator to get there...
                    if ((MovementBy == MovementByType.NavigatorOnly) || (MovementBy == MovementByType.NavigatorPreferred))
                        { moveResult = Navigator.MoveTo(destination); }

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

                    return RunStatus.Success; // fall through
                }),

                new WaitContinue(Delay_WoWClientMovementThrottle, ret => false, new ActionAlwaysSucceed())
                );
        }


        private string Utility_BuildTimeAsString(TimeSpan timeSpan)
        {
            string formatString = string.Empty;

            if (timeSpan.Hours > 0)         { formatString = "{0:D2}h:{1:D2}m:{2:D2}s"; }
            else if (timeSpan.Minutes > 0)  { formatString = "{1:D2}m:{2:D2}s"; }
            else                            { formatString = "{2:D}s"; }

            return string.Format(formatString, timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
        }


        private string Utility_GetNamesOfUnits(int[] unitIds)
        {
            if ((unitIds == null) || (unitIds.Count() <= 0))
                { return ("No units"); }

            List<string> unitNames = new List<string>();

            foreach (var unitId in unitIds)
            {
                WoWUnit unit = ObjectManager.GetObjectsOfType<WoWUnit>(true, false).FirstOrDefault(u => u.Entry == unitId);

                unitNames.Add((unit != null)
                            ? unit.Name
                            : string.Format("NpcId({0})", unitId));                    
            }

            return string.Join(", ", unitNames);
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
        public List<WoWPoint> ParsePath(string pathElementName)
        {
            var descendants = Element.Descendants(pathElementName).Elements();
            List<WoWPoint> path = new List<WoWPoint>();

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

                    path.Add(new WoWPoint(x, y, z));
                }
            }

            return path;
        }
        #endregion
    }
}

