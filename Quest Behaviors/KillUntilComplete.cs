// Behavior originally contributed by Natfoth.
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
// QUICK DOX:
// KILLUNTILCOMPLETE kills the mobs defined as the goal.  This behavior complements
// Honorbuddy's Kill-form of <Objective> in the following ways:
//  * You can hunt multiple mobs simultaneously.
//  * Each mob can be qualified, and pursuit stops when the mob is no longer needed
//      for the goal.
//  * The behavior can prioritize the importance of which targets should be pursued
//      first when many targets are available.
//  * Priority targets can be switched to immediately, if necessary, rather than waiting
//      for the current target to die.
//
// BEHAVIOR ATTRIBUTES:
// *** ALSO see the documentation in QuestBehaviorCore/QuestBehaviorBase.cs.  All of the attributes it provides
// *** are available here, also.  The documentation on the attributes QuestBehaviorBase provides
// *** is _not_ repeated here, to prevent documentation inconsistencies.
//
// Basic Attributes:
//      MobIdN [at least one is REQUIRED]
//          Identifies the mobs on which the kill task should take place.
//          The MobIdN can represent either an NPC (WoWUnit) or an Object (WoWGameObject).
//          The two types can freely be mixed.
//
// Tunables:
//      ImmediatelySwitchToHighestPriorityTarget [optional; default: true]
//          If a higher-priority target becomes available, the behavior will immediately switch
//          to killing it, rather than waiting for the current mob to die.
//      WaitForNpcs [optional; Default: true]
//          This value affects what happens if there are no MobIds in the immediate area.
//          If true, the behavior will move to the next hunting ground waypoint, or if there
//          is only one waypoint, the behavior will stand and wait for MobIdN to respawn.
//          If false, and the behavior cannot locate MobIdN in the immediate area, the behavior
//          considers itself complete.
//          Please see "Things to know", below.
//      X/Y/Z [optional; Default: toon's current location when behavior is started]
//          This specifies the location where the toon should loiter
//          while waiting to kill MobIdN.  If you need a large hunting ground
//          you should prefer using the <HuntingGrounds> sub-element, as it allows for
//          multiple locations (waypoints) to visit.
//          This value is automatically converted to a <HuntingGrounds> waypoint.
// 
// BEHAVIOR EXTENSION ELEMENTS (goes between <CustomBehavior ...> and </CustomBehavior> tags)
// See the "Examples" section for typical usage.
//      <AvoidMobs> [optional; Default: none]
//          See documentation in QuestBehaviorCore/QuestBehaviorBase.cs for full explanation.
//
//      <Blackspots> [optional; Default: none]
//          See documentation in QuestBehaviorCore/QuestBehaviorBase.cs for full explanation.
//
//      <HuntingGrounds> [optional; Default: toon's position when behavior is started]
//          See documentation in QuestBehaviorCore/QuestBehaviorBase.cs for full explanation.
//
//      <PursuitList> [optional; None]
//          See documentation in QuestBehaviorCore/QuestBehaviorBase.cs for full explanation.
#endregion


#region Examples
// SIMPLEST FORM:
// Kill a set of mobs in the area.  The hunting ground is centered around the point where
// the toon is standing when the behavior starts:
//      <CustomBehavior File="KillUntilComplete" MobId="12345" MobId2="23456" MobId3="34567" />
//
// A FULL FEATURED EXAMPLE:
// In Recon Essentials (http://wowhead.com/quest=27977), Only Dark Iron Steamsmiths can drop the
// Smithing Tuyere, but Dark Iron Steamsmiths and Dark Iron Lookouts can both drop the Lookout's Spyglass.
// We prioritize Dark Iron Steamsmiths any time we find them, because they are hard to find, and
// the bottleneck for this quest.
//      <CustomBehavior File="KillUntilComplete" QuestId="27977" >
//          <Blackspots>
//              <Blackspot Name="low-hanging rock" X="9876.54" Y="8765.43" Z="125.2" />
//          </Blackspot>
//          <HuntingGrounds>
//              <Hotspot Name="North-central tent area" X="1234.56" Y="2345.67" Z="131.05" />
//              <Hotspot Name="NE tent area" X="2345.56" Y="3456.67" Z="128.05" />
//              <Hotspot Name="Tower 1" X="3456.56" Y="4567.67" Z="121.05" />
//              <Hotspot Name="Tower 2" X="4567.56" Y="5678.67" Z="135.05" />
//              <Hotspot Name="Tower 3" X="5678.56" Y="6789.67" Z="127.05" />
//          </HuntingGrounds>
//          <PursuitList>
//              <PursueUnit Id="5840" PursueWhen="!IsObjectiveComplete(1,27977)" />
//              <PursueUnit Id="5840" PursueWhen="!IsObjectiveComplete(2,27977)" Priority="10" />
//              <PursueUnit Id="8566" PursueWhen="!IsObjectiveComplete(2,27977)" />
//          </PursuitList>
//      </CustomBehavior>
//
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Bots.DungeonBuddy.Helpers;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Honorbuddy.QuestBehaviorCore.XmlElements;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using WaitTimer = Styx.Common.Helpers.WaitTimer;

#endregion


namespace Honorbuddy.Quest_Behaviors.KillUntilComplete
{
	[CustomBehaviorFileName(@"KillUntilComplete")]
	public class KillUntilComplete : QuestBehaviorBase
	{
		public KillUntilComplete(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				var mobIds = GetNumberedAttributesAsArray<int>("MobId", 0, ConstrainAs.MobId, new[] { "NpcID" }) ?? new int[0];
                HuntingGroundCenter = GetAttributeAsNullable<WoWPoint>("", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;
                AddMobIdsToPersueList(mobIds, PursuitList);

                // Tunables...
                WaitForNpcs = GetAttributeAsNullable<bool>("WaitForNpcs", false, null, null) ?? true;
                ImmediatelySwitchToHighestPriorityTarget = GetAttributeAsNullable<bool>("ImmediatelySwitchToHighestPriorityTarget", false, null, null) ?? true;
            }
			catch (Exception except)
			{
				// Maintenance problems occur for a number of reasons.  The primary two are...
				// * Changes were made to the behavior, and boundary conditions weren't properly tested.
				// * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
				// In any case, we pinpoint the source of the problem area here, and hopefully it
				// can be quickly resolved.
				QBCLog.Exception(except);
				IsAttributeProblem = true;
			}
		}


		// Attributes provided by caller
        private HuntingGroundsType HuntingGrounds { get; set; }
        private WoWPoint HuntingGroundCenter { get; set; }
        private bool WaitForNpcs { get; set; }
        private bool ImmediatelySwitchToHighestPriorityTarget { get; set; }
		// Private variables for internal state
        private Composite _combatMain;
        private Composite _mainBehavior;
        private readonly WaitTimer _targetSwitchTimer = new WaitTimer(TimeSpan.FromSeconds(3));
		// Private properties
        private UtilityCoroutine.NoMobsAtCurrentWaypoint _noMobsAtCurrentWaypoint;

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }

        #region Overrides of QuestBehaviorBase

        protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
        {
            //UsageCheck_DeprecatedAttribute(xElement,
            //    Args.Keys.Any(k => CaseInsensitiveContains(k, "MobId") || CaseInsensitiveContains(k, "NpcID")),
            //    "MobId",
            //    context => string.Format("Automatically converted MobId attributes into PursuitList."
            //                              + "  Please update profile to use PursuitList, instead."));
        }

        protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
        {
            // PursuitList isn't Initialized at this point.

            //UsageCheck_SemanticCoherency(xElement,
            //    (!PursuitList.PursueObjects.Any()),
            //    context => "You must specify one or more PursueObject.");
        }

	    protected override bool IncludeUnitInTargeting(WoWUnit wowUnit)
	    {
	        return !Me.IsActuallyInCombat && PursuitList.ShouldPursue(wowUnit);
	    }

	    protected override float WeightUnitForTargeting(WoWUnit wowUnit)
	    {
	        float priority;
	        return PursuitList.ShouldPursue(wowUnit, out priority) ? priority : 0;
	    }

	    public override void OnStart()
	    {
	        // Hunting ground processing...
	        // NB: We had to defer this processing from the constructor, because XElement isn't available
	        // to parse child XML nodes until OnStart() is called.
	        HuntingGrounds = HuntingGroundsType.GetOrCreate(
	            Element,
	            "HuntingGrounds",
	            new WaypointType(HuntingGroundCenter, "hunting ground center"));
	        IsAttributeProblem |= HuntingGrounds.IsAttributeProblem;
	        // Let QuestBehaviorBase do basic initializaion of the behavior, deal with bad or deprecated attributes,
	        // capture configuration state, install BT hooks, etc.  This will also update the goal text.
	        var isBehaviorShouldRun = OnStart_QuestBehaviorCore();

	        // If the quest is complete, this behavior is already done...
	        // So we don't want to falsely inform the user of things that will be skipped.
	        if (isBehaviorShouldRun)
	        {                
                UsageCheck_SemanticCoherency(Element,
                    (!PursuitList.PursueObjects.Any()),
                    context => "You must specify one or more MobId or PursueObject.");	           
	        }
	    }

        protected override Composite CreateBehavior_CombatMain()
        {
            return _combatMain ?? (_combatMain = new ActionRunCoroutine(ctx => Coroutine_CombatMain()));
        }

        private async Task<bool> Coroutine_CombatMain()
        {
            if (!_targetSwitchTimer.IsFinished || BotPoi.Current.Type != PoiType.Kill || !ImmediatelySwitchToHighestPriorityTarget)
                return false;

            var firstUnit = Targeting.Instance.FirstUnit;
            if (!Query.IsViable(firstUnit))
                return false;

            if (firstUnit != BotPoi.Current.AsObject)
            {
                BotPoi.Current = new BotPoi(firstUnit, PoiType.Kill);
                QBCLog.Info("Current POI is not the best target. Changing.");
                _targetSwitchTimer.Reset();
            }
            return false;
        }

        protected override Composite CreateMainBehavior()
        {
            return _mainBehavior ?? (_mainBehavior = new ActionRunCoroutine(ctx => MainCoroutine()));
        }

	    private async Task<bool> MainCoroutine()
	    {
	        if (BotPoi.Current.Type != PoiType.None || !Targeting.Instance.IsEmpty())
	            return false;

	        if (_noMobsAtCurrentWaypoint == null)
	        {
	            _noMobsAtCurrentWaypoint =
	                new UtilityCoroutine.NoMobsAtCurrentWaypoint(
	                    () => HuntingGrounds,
	                    () => MovementBy,
	                    () => { if (!WaitForNpcs) BehaviorDone("Terminating--\"WaitForNpcs\" is false."); },
	                    () =>
	                        TargetExclusionAnalysis.Analyze(
	                            Element,
	                            () => PursuitList.GetPursuitedObjects(),
	                            TargetExclusionChecks));
	        }

            // move to waypoints when there's nothing else to do.
	        return await _noMobsAtCurrentWaypoint;
	    }

	    #endregion


        private List<string> TargetExclusionChecks(WoWObject wowObject)
        {
            var exclusionReasons = TargetExclusionAnalysis.CheckCore(wowObject, this);
            return exclusionReasons;
        }

	    private void AddMobIdsToPersueList(IEnumerable<int> mobIds, PursuitListType pursuitList)
	    {
	        foreach (var mobId in mobIds)
	        {
	            var pursueObject = new PursueObjectType<WoWUnit>(mobId);
                pursuitList.PursueObjects.Add(pursueObject);
	        }
	    }
    }
}

