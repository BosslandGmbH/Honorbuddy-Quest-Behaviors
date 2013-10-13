// Behavior originally contributed by mastahg.
//
// DOCUMENTATION:
//     
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Tripper.Tools.Math;
using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.ScoutingReportOnTheRightTrack
{
    [CustomBehaviorFileName(@"SpecificQuests\29731-HordeJadeForest-ScoutingReportOnTheRightTrack")]
    public class OnTheRightTrack : CustomForcedBehavior
    {
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private Composite _root;

        public OnTheRightTrack(Dictionary<string, string> args) : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                //Location = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ??WoWPoint.Empty;
                QuestId = 29731; //GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
                //MobIds = GetAttributeAsNullable<int>("MobId", true, ConstrainAs.MobId, null) ?? 0;
                QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = QuestInLogRequirement.InLog;
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message + "\nFROM HERE:\n" + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }


        // Attributes provided by caller
        public uint[] MobIds { get; private set; }
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }
        public WoWPoint Location { get; private set; }

        // Private variables for internal state


        // Private properties
        private LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
        }

        ~OnTheRightTrack()
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
                    CharacterSettings.Instance.UseMount = _mount;
                    TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_MainCombat());
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

        private static readonly WoWPoint spot = new WoWPoint(853.4566, -1950.396, 61.24099);
        private static readonly WoWPoint bounce = new WoWPoint(884.9406, -1863.337, 62.95205);

        private CircularQueue<WoWPoint> _circularQueue = new CircularQueue<WoWPoint>() {spot, bounce};

        private bool _mount;

        public Composite DoneYet
        {
            get
            {            
                return new Decorator(
                    ret => IsQuestComplete() || !Me.InVehicle,
                    new Action(
                        delegate
                        {
                            TreeRoot.StatusText = "Finished!";
                            CharacterSettings.Instance.UseMount = true;
                            _isBehaviorDone = true;
                            return RunStatus.Success;
                        }));
            }
        }

        public WoWUnit Enemy
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>()
                        .Where(x => x.Entry == 55550 && !x.HasAura("Smoke Bomb") && x.GotTarget && x.CurrentTarget == Me.CharmedUnit)
                        .OrderBy(x => x.Distance)
                        .FirstOrDefault();
            }
        }

        public Composite Move
        {
            get
            {
                return new PrioritySelector(
                    new Decorator(r => _circularQueue.Peek().Distance(Me.Location) < 10, new Action(r => _circularQueue.Dequeue())),
                    new Action(r => Navigator.MoveTo(_circularQueue.Peek())));
            }
        }

        public Composite Obj1
        {
            get
            {
                return new Decorator(
                    r => Enemy != null,
                    new Action(
                        r =>

                        {
                            Enemy.Target();
                            CastSpell("Smoke Bomb");
                        }));
            }
        }

        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone // normal completion
                        || !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
            }
        }

        public bool IsQuestComplete()
        {
            var quest = StyxWoW.Me.QuestLog.GetQuestById((uint) QuestId);
            return quest == null || quest.IsCompleted;
        }

        public void CastSpell(string action)
        {
            var spell = StyxWoW.Me.PetSpells.FirstOrDefault(p => p.ToString() == action);
            if (spell == null)
                return;

            Logging.Write("[Pet] Casting {0}", action);
            Lua.DoString("CastPetAction({0})", spell.ActionBarIndex + 1);
        }

        private bool IsObjectiveComplete(int objectiveId, uint questId)
        {
            if (Me.QuestLog.GetQuestById(questId) == null)
            {
                return false;
            }
            int returnVal = Lua.GetReturnVal<int>("return GetQuestLogIndexByID(" + questId + ")", 0);
            return Lua.GetReturnVal<bool>(string.Concat(new object[] {"return GetQuestLogLeaderBoard(", objectiveId, ",", returnVal, ")"}), 2);
        }


        protected Composite CreateBehavior_MainCombat()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, Obj1, Move, new ActionAlwaysSucceed())));
        }


        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
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
                _mount = CharacterSettings.Instance.UseMount;
                CharacterSettings.Instance.UseMount = false;
                TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_MainCombat());
                Navigator.Clear();
                // Me.QuestLog.GetQuestById(27761).GetObjectives()[2].

                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint) QuestId);

                TreeRoot.GoalText = GetType().Name + ": " + ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
            }
        }

        #endregion
    }
}