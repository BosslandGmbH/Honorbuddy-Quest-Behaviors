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
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }

        // Private variables for internal state
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private Composite _root;

        // Private properties
        private LocalPlayer Me { get { return (StyxWoW.Me); } }

        private bool _onBird;

        public static WoWPoint RopeGameObjectLocation = new WoWPoint(238.4929, -393.3196, 247.5843);

        private WoWItem Item { get { return (StyxWoW.Me.CarriedItems.FirstOrDefault(i => i.Entry == 75208)); } }

        public WoWUnit Enemy
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => u.Entry == 56171 && !u.IsDead && u.Distance < 199)
                                     .OrderBy(u => u.Distance).FirstOrDefault());
            }
        }

        public WoWGameObject RopeGameObject
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWGameObject>()
                                     .Where(u => u.Entry == 215319)
                                     .OrderBy(u => u.Distance).FirstOrDefault());
            }
        }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id: CombatUseItemOn.cs 249 2012-09-19 01:31:37Z natfoth $"); } }
        public override string SubversionRevision { get { return ("$Revision: 249 $"); } }

        public static Common.Helpers.WaitTimer GetOnTimer = new Common.Helpers.WaitTimer(TimeSpan.FromSeconds(1));


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

       

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(
                    new Decorator(
                        ret => !_isBehaviorDone,
                        new PrioritySelector(
                            new Decorator(ret => Me.QuestLog.GetQuestById((uint)QuestId) != null && Me.QuestLog.GetQuestById((uint)QuestId).IsCompleted,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new WaitContinue(120,
                                        new Action(delegate
                                        {
                                            _isBehaviorDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )),

                                new Decorator(
                                    ret => !Me.InVehicle && !_onBird,
                                    new PrioritySelector(
                                        new Decorator(ret => Item == null,
                                            new PrioritySelector(
                                                new Decorator(ret => RopeGameObject == null,
                                                    new Action(ret => Navigator.MoveTo(RopeGameObjectLocation))),
                                                new Decorator(ret => RopeGameObject != null,
                                                    new PrioritySelector(
                                                        new Decorator(ret => !RopeGameObject.WithinInteractRange,
                                                            new Action(ret => Navigator.MoveTo(RopeGameObject.Location))),
                                                        new Decorator(ret => RopeGameObject.WithinInteractRange,
                                                            new Sequence(
                                                                new Action(ret => WoWMovement.MoveStop()),
                                                                new Action(ret => RopeGameObject.Interact())))
                                                    ))
                                        )),

                                        new Decorator(ret => Enemy == null,
                                            new Action(ret => Navigator.MoveTo(RopeGameObject.Location))),

                                        new Decorator(ret => Enemy != null,
                                            new Sequence(
                                                new Action(ret => Enemy.Target()),
                                                new Action(ret => Item.UseContainerItem()),
                                                new Action(ret => Thread.Sleep(3000)),
                                                new Action(ret => GetOnTimer.Reset()),
                                                new Decorator(ret => Enemy.Distance < 20,
                                                    new Action(ret => _onBird = true))))
                                     )),


                                    new Decorator(
                                        ret => Me.InVehicle,
                                            new PrioritySelector(
                                                 new Sequence(
                                                     new Decorator(ret => GetOnTimer.IsFinished,
                                                         new Sequence(
                                                             new Action(ret => Lua.DoString("RunMacroText('/click VehicleMenuBarActionButton1','0')")),
                                                             new Action(ret => Lua.DoString("RunMacroText('/click ExtraActionButton1','0')")),
                                                             new Action(ret => GetOnTimer.Reset()))),
                                                     
                                                     new Decorator(ret => RoutineManager.Current.CombatBehavior != null,
                                                                RoutineManager.Current.CombatBehavior),
                                                     new Action(ret => Enemy.Target()),
                                                     new Action(c => RoutineManager.Current.Combat())))
                                                            

                                            )
                                                            

                    ))));
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
            }
        }

        #endregion
    }
}
