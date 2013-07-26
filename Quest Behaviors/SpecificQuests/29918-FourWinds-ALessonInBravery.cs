// Behavior originally contributed by Natfoth.
//
// WIKI DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_FireFromTheSky
//
// QUICK DOX:
//      Used for the Dwarf Quest SI7: Fire From The Sky
//
//  Notes:
//      * Make sure to Save Gizmo.
//

using System;
using System.Collections.Generic;
using System.Linq;

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.ALessonInBravery
{
    [CustomBehaviorFileName(@"SpecificQuests\29918-FourWinds-ALessonInBravery")]
    public class FourWindsLessonInBravery : CustomForcedBehavior
    {
        public FourWindsLessonInBravery(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                QuestId = GetAttributeAsNullable("QuestId", false, ConstrainAs.QuestId(this), null) ?? 29918;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
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
        private int QuestId { get; set; }
        private QuestCompleteRequirement QuestRequirementComplete { get; set; }
        private QuestInLogRequirement QuestRequirementInLog { get; set; }


        // Private variables for internal state
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private Composite _root;

        // Private properties
        private const int AuraId_Mangle = 105373;
        private IEnumerable<int> AuraIds_OccupiedVehicle; 
        private LocalPlayer Me { get { return (StyxWoW.Me); } }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id: 29918-FourWinds-ALessonInBravery.cs 574 2013-06-28 08:54:59Z chinajade $"); } }
        public override string SubversionRevision { get { return ("$Revision: 574 $"); } }


        ~FourWindsLessonInBravery()
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
                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }

        
        #region Overrides of CustomForcedBehavior
        public Composite DoneYet
        {
            get
            {
                return new Decorator(ret => Me.IsQuestObjectiveComplete(QuestId, 2),
                    new Action(delegate
                    {
                        TreeRoot.StatusText = "Finished!";
                        _isBehaviorDone = true;
                        return RunStatus.Success;
                    }));
            }
        }


        public WoWItem Rope
        {
            get { return Me.BagItems.FirstOrDefault(r => r.Entry == 75208); }
        }


        public WoWUnit GiantAssBird
        {         
            get
            {
                if (!Query.IsViable(_giantAssBird))
                {
                    _giantAssBird =
                       (from wowObject in Query.FindMobsAndFactions(Utility.ToEnumerable<int>(56171))
                        let wowUnit = wowObject as WoWUnit
                        where
                            Query.IsViable(wowUnit)
                            && wowUnit.IsAlive
                            // Eliminate bird vehicles occupied by other players...
                            && !wowUnit.Auras.Values.Any(aura => AuraIds_OccupiedVehicle.Contains(aura.SpellId))
                        orderby wowUnit.Distance
                        select wowUnit)
                        .FirstOrDefault();
                }

                return _giantAssBird;
            }
        }
        private WoWUnit _giantAssBird;


        public Composite GetOnBird
        {
            get
            {
                return
                    new Decorator(ret => !Me.InVehicle,
                        new PrioritySelector(
                            new Decorator(r => Query.IsViable(GiantAssBird),
                                new Action(r =>
                                {
                                    Utility.Target(GiantAssBird);
                                    Navigator.MoveTo(GiantAssBird.Location);
                                    Rope.Use();
                                }))
                            ));
            }
        }


        public Composite KillBird
        {
            get
            {
                return new Decorator(ret => Me.InVehicle,
                    new PrioritySelector(
                        // Get back on bid when tossed off...
                        new Decorator(context => Me.HasAura(AuraId_Mangle),
                            new Sequence(
                                // Small variant delay to prevent looking like a bot...
                                new WaitContinue(
                                    Delay.BeforeButtonClick,
                                    context => false,
                                    new ActionAlwaysSucceed()),
                                new Action(context => { Lua.DoString("RunMacroText('/click ExtraActionButton1')");  })
                            )),

                        // Make certain bird stays targeted...
                        new ActionFail(context => { Utility.Target(GiantAssBird, false, PoiType.Kill); }),

                        // Spank bird (use backup MiniCombatRoutine if main CR doesn't attack in vehicles...
                        RoutineManager.Current.CombatBuffBehavior,
                        RoutineManager.Current.CombatBehavior,
                        new UtilityBehaviorPS.MiniCombatRoutine()
                    ));
            }
        }

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, GetOnBird,KillBird, new ActionAlwaysSucceed())));
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
                if (TreeRoot.Current != null && TreeRoot.Current.Root != null && TreeRoot.Current.Root.LastStatus != RunStatus.Running)
                {
                    var currentRoot = TreeRoot.Current.Root;
                    if (currentRoot is GroupComposite)
                    {
                        var root = (GroupComposite)currentRoot;
                        root.InsertChild(0, CreateBehavior());
                    }
                }

                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = GetType().Name + ": " + ((quest != null) ? quest.Name : "In Progress");

                AuraIds_OccupiedVehicle = QuestBehaviorBase.GetOccupiedVehicleAuraIds();
            }
        }

        #endregion
    }
}
