// Behavior originally contributed by mastahg.
//
// DOCUMENTATION:
//     
//				<CustomBehavior File="StandAndKill" QuestId="25553" MobId="40974" X="3772.889" Y="-3233.83" Z="975.3411" /> // originally made for hyjal behavior


using System;
using System.Collections.Generic;
using System.Linq;
using CommonBehaviors.Actions;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class shaofhatred : CustomForcedBehavior
    {
        ~shaofhatred()
        {
            Dispose(false);
        }

        public shaofhatred(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.

                QuestId = 30968;
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
                LogMessage("error",
                           "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message + "\nFROM HERE:\n" + except.StackTrace +
                           "\n");
                IsAttributeProblem = true;
            }
        }


        // Attributes provided by caller
        public int MobIds { get; private set; }
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }
        public WoWPoint Location { get; private set; }



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

        public bool IsQuestComplete()
        {
            if (QuestId == 0)
                return false;

            var quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
            return quest == null || quest.IsCompleted;
        }

  

        public bool InDanger
        {
            get
            {
                return
                    WoWMissile.AllMissiles.Where(r => r.SpellId == 127335).Select(r => r.ImpactPosition).Count(r => r.Distance(Me.Location) <= 8.5) > 0;
            }
        }



        public WoWUnit Friendly
        {
            get { return ObjectManager.GetObjectsOfType<WoWUnit>().Where(r => !r.IsPlayer && r.IsFriendly && r.Distance < 80).OrderByDescending(r=>r.Distance2D).FirstOrDefault(); }
        }


        public WoWUnit Hostile
        {
            get { return ObjectManager.GetObjectsOfType<WoWUnit>().Where(r => !r.IsPlayer && !r.IsFriendly && r.GotTarget && r.CurrentTarget == Me && r.Distance < 10).OrderBy(r=>r.Distance).FirstOrDefault(); }
        }

        public Composite DoDps
        {
            get
            {
                return
                    new PrioritySelector(RoutineManager.Current.HealBehavior,RoutineManager.Current.CombatBuffBehavior, RoutineManager.Current.CombatBehavior);
            }
        }

        public WoWUnit Sha
        {
            get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(r => r.Entry == 62541); }
        }

        //<Vendor Name="Ban Bearheart" Entry="62806" Type="Repair" X="1400.784" Y="4426.749" Z="86.55973" />
        public WoWUnit healer
        {
            get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(r => r.Entry == 62806); }
        }

        
        public Composite Stuff
        {
            get
            {
                return new PrioritySelector(

                    new Decorator(r=> (!Me.GotTarget || (Sha != null && Me.CurrentTarget == Sha)) && Hostile != null, new Action(r=>Hostile.Target())),
                    new Decorator(r => !Me.GotTarget && Sha != null , new Action(r => Sha.Target())), 
                    new Decorator(r=>InDanger, new Action(r=>Navigator.MoveTo(Friendly.Location))),
                    new Decorator(r=> Me.HealthPercent < 70, new Action(r=>Navigator.MoveTo(healer.Location))),
                    DoDps

                    );
            }
        }




        protected override Composite CreateBehavior()
        {

            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, Stuff,new ActionAlwaysSucceed())));
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