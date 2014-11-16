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
// This is only used when you get a quest that Says, Kill anything x times. Or on the chance the wowhead ID is wrong
// ##Syntax##
// QuestId: Id of the quest.
// MobId, MobId2, ...MobIdN: Mob Values that it will kill.
// X,Y,Z: The general location where theese objects can be found
// 
#endregion


#region Examples
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

