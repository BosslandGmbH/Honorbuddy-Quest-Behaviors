// Behavior originally contributed by mastahg.
//
// DOCUMENTATION:
// This behavior makes mastahg cry each time it is used.
//     
//

using System;
using System.Collections.Generic;
using System.Linq;

using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.SlayTheFools
{
    [CustomBehaviorFileName(@"SlayTheFools")]
    public class SlayTheFools : CustomForcedBehavior
    {
        ~SlayTheFools()
        {
            Dispose(false);
        }

        public SlayTheFools(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
				// 				<CustomBehavior File="SlayTheFools" QuestId="" MobId="" CollectionDistance="" 
				// 				<CustomBehavior File="SlayTheFools" QuestId="29749" MobId="55489" CollectionDistance="60" X="2930.426" Y="-1644.892" Z="252.8332" />
                Location = GetAttributeAsNullable<WoWPoint>("", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;
                QuestId = GetAttributeAsNullable<int>("QuestId",true, ConstrainAs.QuestId(this), null) ?? 0;
                ObjectiveId = GetAttributeAsNullable<int>("ObjectiveId", false, ConstrainAs.RepeatCount, null) ?? 0;
                MobId = GetAttributeAsNullable<int>("MobId", true, ConstrainAs.MobId, null) ?? 0;
                Distance = GetAttributeAsNullable<int>("CollectionDistance", false, null, null) ?? 1000;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;

                Logging.Write("!!!!!!Distance thingy is:" + Distance);
                
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                LogMessage("error",
                           "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message + "\nFROM HERE:\n" + except.StackTrace +
                           "\n");
                IsAttributeProblem = true;
            }
        }


        // Attributes provided by caller
        public int ObjectiveId { get; private set; }
        public int QuestId { get; private set; }
        public int MobId { get; private set; }
        public int Distance { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }
        public WoWPoint Location { get; private set; }
        public int FlightSpot;
        public int State = 4;


        // Private variables for internal state
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private Composite _root;





        // Private properties
        private LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
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
                return
                    new Decorator(ret => IsQuestComplete(), new Action(delegate
                    {

                        TreeRoot.StatusText = "Finished!";
                        _isBehaviorDone = true;
                        return RunStatus.Success;
                    }));

            }
        }

        private bool IsObjectiveComplete(int objectiveId, uint questId)
        {
            if (this.Me.QuestLog.GetQuestById(questId) == null)
            {
                return false;
            }
            int returnVal = Lua.GetReturnVal<int>("return GetQuestLogIndexByID(" + questId + ")", 0);
            return
                Lua.GetReturnVal<bool>(
                    string.Concat(new object[] { "return GetQuestLogLeaderBoard(", objectiveId, ",", returnVal, ")" }), 2);
        }

        public bool IsQuestComplete()
        {
            if (ObjectiveId == 0)
            {
                var quest = StyxWoW.Me.QuestLog.GetQuestById((uint) QuestId);
                return quest == null || quest.IsCompleted;
            }
            else
            {
                return IsObjectiveComplete(ObjectiveId, (uint) QuestId);
            }
        }


        public Composite DoDps
        {
            get
            {
                return
                    new PrioritySelector(RoutineManager.Current.CombatBuffBehavior,RoutineManager.Current.CombatBehavior);
            }

        }

        public WoWUnit TargetKind
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == MobId && u.IsAlive && u.Location.Distance(Location) <= Distance).OrderBy( u => u.Distance).FirstOrDefault();
            }
        }

        public Composite SelectNew
        {
            get
            {
                return new Decorator(r=> TargetKind != null, new Action(r=>TargetKind.Target()));
            }
        }


        public Composite FindOne
        {
            get
            {
                return
                    new Decorator(
                        r => Me.CurrentTarget == null || (Me.CurrentTarget.IsFriendly || Me.CurrentTarget.IsDead),
                        SelectNew);

            }
        }

        public Composite HandleCombat
        {
            get
            {
                return
                    new PrioritySelector(
					new Decorator(r=>Me.Mounted,new Action(r=>Flightor.MountHelper.Dismount())),
                        new Decorator(ret => Me.Combat, DoDps));
            }
        }


        public Composite StayInRange
        {
            get
            {
                return new Decorator(ret => Me.Location.Distance(Location) > Distance, new Action(r=>Navigator.MoveTo(Location)));
            }
        }

        protected override Composite CreateBehavior()
        {

            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, StayInRange,HandleCombat, FindOne,DoDps, new ActionAlwaysSucceed())));
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
                return (_isBehaviorDone // normal completion
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
                if (TreeRoot.Current != null && TreeRoot.Current.Root != null &&
                    TreeRoot.Current.Root.LastStatus != RunStatus.Running)
                {
                    var currentRoot = TreeRoot.Current.Root;
                    if (currentRoot is GroupComposite)
                    {
                        var root = (GroupComposite)currentRoot;
                        root.InsertChild(0, CreateBehavior());
                    }
                }


                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = this.GetType().Name + ": " +
                                    ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
            }
        }

        #endregion
    }
}