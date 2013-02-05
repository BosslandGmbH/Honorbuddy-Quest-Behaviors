// Behavior originally contributed by mastahg
//
// DOCUMENTATION:
//     
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    public class Rag : CustomForcedBehavior
    {
        /// <summary>
        /// This is only used when you get a quest that Says, Kill anything x times. Or on the chance the wowhead ID is wrong
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// MobId, MobId2, ...MobIdN: Mob Values that it will kill.
        /// X,Y,Z: The general location where theese objects can be found
        /// </summary>
        /// 
        public Rag(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                Location = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ??
                           WoWPoint.Empty;
                //MobIds = GetNumberedAttributesAsArray<int>("MobId", 1, ConstrainAs.MobId, new[] {"NpcID"})
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
                LogMessage("error",
                           "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message + "\nFROM HERE:\n" + except.StackTrace +
                           "\n");
                IsAttributeProblem = true;
            }
        }


        // Attributes provided by caller
        public WoWPoint Location { get; private set; }
        public int QuestId = 25551;// 26581;//25551;
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }

        // Private variables for internal state
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private Composite _root;

        // Private properties
        private LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
        }

        //<Vendor Name="Malfurion Stormrage" Entry="41632" Type="Repair" X="3993.279" Y="-3036.587" Z="575.3904" />
        private WoWUnit Malfurion

        {
            get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(r => r.Entry == 41632); }
        }


        //<Vendor Name="Cenarius" Entry="41631" Type="Repair" X="3954.34" Y="-2826.02" Z="618.7476" />
        private WoWUnit Cenarius

        {
            get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(r => r.Entry == 41631); }
        }

        private WoWUnit Add
        {
            get
            {//40794 40803 31146
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 40794 && !u.IsDead).OrderBy(
                        u => u.Distance).FirstOrDefault();
            }
        }

        private WoWUnit Ragnaros
        {
            get { return (ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == 41634)); }
        }


        ~Rag()
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



        public bool IsQuestComplete()
        {
            var quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
            return quest == null || quest.IsCompleted;
        }

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

        public Composite DoDps
        {
            get
            {
                return new PrioritySelector(RoutineManager.Current.CombatBuffBehavior,
                                            RoutineManager.Current.CombatBehavior);
            }
        }

        public Composite Story
        {
            get
            {
                return new Decorator(r => Malfurion == null, new PrioritySelector(

                                                                 new Action(r=>
                                                                                {
                                                                                    WoWMovement.MoveStop();
                                                                                    Cenarius.Interact();
                                                                                    Thread.Sleep(400);
                                                                                    Lua.DoString("SelectGossipOption(1,\"gossip\", true)");
                                                                                }


                                                                     )));
            }
        }


        public Composite KillAdds
        {
            get
            {
                return new Decorator(r=> Add != null, new PrioritySelector(
                    
                    
                    new Decorator(r=> !Me.GotTarget || Me.CurrentTarget != Add, new Action(r=>Add.Target())),
                    DoDps

                    
                    
                    ));
            }
        }


        public Composite KillBoss
        {
            get
            {
                return new Decorator(r => Ragnaros != null, new PrioritySelector(


                    new Decorator(r => !Me.GotTarget || Me.CurrentTarget != Ragnaros, new Action(r => Ragnaros.Target())),
                    DoDps



                    ));
            }
        }


        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = new PrioritySelector(DoneYet,Story, KillAdds, KillBoss));
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