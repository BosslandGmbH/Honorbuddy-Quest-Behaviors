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

using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class FortuneAndGlory : CustomForcedBehavior
    {
        ~FortuneAndGlory()
        {
            Dispose(false);
        }

        public FortuneAndGlory(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                //Location = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ??WoWPoint.Empty;
                QuestId = 27748;//GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
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
                LogMessage("error",
                           "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message + "\nFROM HERE:\n" + except.StackTrace +
                           "\n");
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


        public Composite DoDps
        {
            get
            {
                return
                    new PrioritySelector(
                        new Decorator(ret => RoutineManager.Current.CombatBehavior != null,
                                      RoutineManager.Current.CombatBehavior),
                        new Action(c => RoutineManager.Current.Combat()));
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
                return new Decorator(ret => IsQuestComplete(), new Action(delegate
                                                                                           {
                                                                                               TreeRoot.StatusText =
                                                                                                   "Finished!";
                                                                                               _isBehaviorDone = true;
                                                                                               return RunStatus.Success;
                                                                                           }));
            }
        }



        public WoWUnit Enemey
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.IsAlive && u.Entry == 46646);
            }
        }


        public Composite ShootStuff
        {
            get
            {
                return new Decorator(ret => Me.CurrentTarget == Enemey, DoDps);
            }
        }


        public Composite TargetHim
        {
            get
            {
                return new Decorator(ret => Me.CurrentTarget != Enemey, new Action(r => Enemey.Target()));
            }
        }


        public Composite WaitAround
        {
            get
            {
                return new Decorator(ret => Enemey != null && Enemey.HealthPercent > 26, new ActionAlwaysSucceed());
            }
        }

        public Composite Kick
        {
            get
            {
                return new Decorator(ret => Me.CurrentTarget.IsCasting && Me.CurrentTarget.CastingSpellId == 87990 && SpellManager.CanCast(PullMob()),
                    new Action(delegate
                                   { var x = PullMob();
                                       SpellManager.Cast(x);
                                   }
                                   ));
            }
        }

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, WaitAround,TargetHim, Kick, ShootStuff)));
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


        public string PullMob()
        {
            string spell = "";

            switch (Me.Class)
            {
                case WoWClass.Mage:
                    spell = "Counterspell";
                    break;
                case WoWClass.Druid:
                    spell = "dsad";
                    break;
                case WoWClass.Paladin:
                    spell = "Rebuke";
                    break;
                case WoWClass.Priest:
                    spell = "dasd";
                    break;
                case WoWClass.Shaman:
                    spell = "Wind Shear";
                    break;
                case WoWClass.Warlock:
                    spell = "dsad";
                    break;
                case WoWClass.DeathKnight:
                    spell = "Mind Freeze";
                    break;
                case WoWClass.Hunter:
                    spell = "Silencing Shot";
                    break;
                case WoWClass.Warrior:
                    spell = "Pummel";
                    break;
                case WoWClass.Rogue:
                    spell = "Kick";
                    break;

            }

            return spell;


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