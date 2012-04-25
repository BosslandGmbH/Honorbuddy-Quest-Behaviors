// Behavior originally contributed by mastahg.
//
// DOCUMENTATION:
//     
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class Steping : CustomForcedBehavior
    {

        public Steping(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                //Location = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ??WoWPoint.Empty;
                QuestId = GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
                MobIds = GetAttributeAsNullable<int>("MobId", true, ConstrainAs.MobId, null) ?? 0;
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
        public int MobIds { get; private set; }
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }

        // Private variables for internal state
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private Composite _root;

        // Private properties
        private LocalPlayer Me
        {
            get { return (ObjectManager.Me); }
        }

        private List<WoWUnit> Stage1
        {
            get
            {//40794 40803 31146
                return
                    (ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 45191 && u.WithinInteractRange && u != CurrentStone
                        && u.Z > Me.Z).OrderByDescending(u => u.Distance).ToList());
            }
        }

        private WoWUnit CurrentStone
        {
            get { return (ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == 45191 && u.GetAllAuras().FirstOrDefault(x => x.CreatorGuid == Me.Guid && x.Name == "Ride Vehicle") != null)); }
        }



        private List<WoWUnit> Good
        {
            get
            {//40794 40803 31146
                return
                    /*(ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 45191 && u.WithinInteractRange && u != CurrentStone
                        && u.Guid != lastguid && u.Location.Distance(dragon.Location) <= 5).OrderBy(u => u.Distance).ToList());*/
                    (ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 45191 && u.WithinInteractRange && u != CurrentStone
                        && u.Guid != lastguid).OrderBy(u => u.Location.Distance(dragon.Location)).ToList());
            }
        }

        private List<WoWUnit> Bad
        {
            get
            {//40794 40803 31146
                return
                    (ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 45191 && u.WithinInteractRange && u != CurrentStone
                        && u.Guid != lastguid).OrderByDescending(u => u.Distance).ToList());
            }
        }


        private WoWUnit dragon
        {
            get { return (ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == MobIds)); }
        }


        ~Steping()
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


        private bool IsRanged
        {
            get
            {
                return (((Me.Class == WoWClass.Druid) &&
                         (SpellManager.HasSpell("BalanceSpell") || SpellManager.HasSpell("RestoSpell"))) ||
                        ((Me.Class == WoWClass.Shaman) &&
                         (SpellManager.HasSpell("ElementalSpell") || SpellManager.HasSpell("RestoSpell"))) ||
                        (Me.Class == WoWClass.Hunter) || (Me.Class == WoWClass.Mage) || (Me.Class == WoWClass.Priest) ||
                        (Me.Class == WoWClass.Warlock));
            }
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
                    new Decorator( ret =>
                        (IsQuestComplete()),
                        new Sequence(new Action(ret => TreeRoot.StatusText = "Finished!"),
                                     new Action(delegate
                                     {
                                         _isBehaviorDone = true;
                                         return RunStatus.Success;
                                     })));

            }
        }

        public Composite DoDps
        {
            get
            {
                return
                     new Sequence(new Action(ret => Navigator.PlayerMover.MoveStop()),
                    new PrioritySelector(
                        new Decorator(ret => RoutineManager.Current.CombatBehavior != null, RoutineManager.Current.CombatBehavior),
                        new Action(c => RoutineManager.Current.Combat())));
            }
        }

        private ulong lastguid;
        private WoWUnit LastStone;

        public Composite MoveCloser
        {
            get
            {
                return new Action(delegate
                {
                    if (Stage1.Count > 0)
                    {
                        var target = Stage1[0];

                        //target.Target();
                        target.Interact();
                        lastguid = CurrentStone.Guid;
                        //Navigator.MoveTo(target.Location);
                    }
                });
            }
        }
        

        public Composite NewStone
        {
            get
            {
                return new Action(delegate
                                      {
                                          
                                          if (Good.Count > 0)
                                          {
                                              Good[0].Interact();
                                              lastguid = CurrentStone.Guid;
                                          }
                                          /*else if (Bad.Count > 0)
                                          {
                                              Bad[0].Interact();
                                              lastguid = CurrentStone.Guid;
                                          }*/

                                      });
            }
        }

        public Composite DragonMethod
        {
            get
            {
                return new PrioritySelector(new Decorator(ret => !Me.HasAura(84448), DoDps),
                                            new WaitContinue(4, ret => Me.HasAura(84448), NewStone));
                //new Decorator(ret => dragon.HealthPercent < 10.0f, DoDps));
            }
        }
        public Composite Stepping
        {
            get
            {
                return new Decorator(ret => Me.Z < 254, MoveCloser);
            }
        }


        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, Stepping, DragonMethod)));
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

                TreeRoot.GoalText = this.GetType().Name + ": " +
                                    ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
            }



 
        }







        #endregion
    }
}