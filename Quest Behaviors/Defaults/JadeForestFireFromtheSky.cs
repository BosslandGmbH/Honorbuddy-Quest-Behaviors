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
using Styx.Common;
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
    public class JadeForestFireFromTheSky : CustomForcedBehavior
    {
        public JadeForestFireFromTheSky(Dictionary<string, string> args)
            : base(args)
        {

            try
            {
                QuestId = GetAttributeAsNullable("QuestId", false, ConstrainAs.QuestId(this), null) ?? 29725;
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
        private int Counter { get; set; }
        private LocalPlayer Me { get { return (StyxWoW.Me); } }

        public static int[] MobIds = new[] { 55550, 55589 };
        public static int DwarfID = 55286;

        public static WoWPoint Shrine1Location = new WoWPoint(789.3542f, -1988.882f, 54.2512f);
        public static WoWPoint Shrine2Location = new WoWPoint(963.9094, -1960.19, 67.762);
        public static WoWPoint Shrine3Location = new WoWPoint(776.9325, -1788.328, 56.5228);
        public static WoWPoint CampLocation = new WoWPoint(714.5405, -2103.443, 65.78586);

        public static Common.Helpers.WaitTimer AimingTimer = new Common.Helpers.WaitTimer(TimeSpan.FromSeconds(2));
        public static Common.Helpers.WaitTimer WaitAtThridTimer = new Common.Helpers.WaitTimer(TimeSpan.FromSeconds(20));

        private bool _firstExplored;
        private bool _secondExplored;
        private bool _thridExplored;
        private bool _waitForThrid;
        private bool _campExplored;

        public WoWUnit Enemy
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => MobIds.Contains((int)u.Entry) && !u.IsDead)
                                     .OrderBy(u => u.Distance).FirstOrDefault());
            }
        }

        public WoWUnit Sully
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => u.Entry == 55282 && !u.IsDead)
                                     .OrderBy(u => u.Distance).FirstOrDefault());
            }
        }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id: CombatUseItemOn.cs 249 2012-09-19 01:31:37Z natfoth $"); } }
        public override string SubversionRevision { get { return ("$Revision: 249 $"); } }


        ~JadeForestFireFromTheSky()
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
                            new Decorator(ret => _campExplored || Me.QuestLog.GetQuestById((uint)QuestId) != null && Me.QuestLog.GetQuestById((uint)QuestId).IsCompleted,
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
                                    ret => !Me.InVehicle,
                                    new PrioritySelector(
                                        new Decorator(ret => Sully == null,
                                            new Sequence(
                                                new Action(ret => TreeRoot.StatusText = "Moving to Start Sully(Dwarf) Story"),
                                                new Action(ret => Navigator.MoveTo(new WoWPoint(-157.5062f, -2659.278f, 1.069468f)))
                                             )),

                                        new Decorator(ret => Sully != null && !Sully.WithinInteractRange,
                                                new Action(ret => Navigator.MoveTo(Sully.Location))
                                             ),

                                        new Decorator(ret => Sully != null && Sully.WithinInteractRange,
                                            new Sequence(
                                                new Action(ret => WoWMovement.MoveStop()),
                                                new Action(ret => Flightor.MountHelper.Dismount()),
                                                new Action(ret => Sully.Interact()),
                                                new Action(ret => Thread.Sleep(400)),
                                                new Action(ret => Lua.DoString("SelectGossipOption(1,\"gossip\", true)"))
                                             )))),


                                    new Decorator(
                                        ret => Me.InVehicle,
                                            new PrioritySelector(
                                                new Decorator(ret => Enemy != null && AimingTimer.IsFinished,
                                                    new Sequence(
                                                        new Action(ret => Enemy.Target()),
                                                        new Action(ret => Thread.Sleep(400)),
                                                        new Action(ret => Lua.DoString("CastPetAction({0})", 1)),
                                                        new Action(ret => AimingTimer.Reset()))),

                                                new Decorator(ret => !_firstExplored,
                                                    new PrioritySelector(
                                                        new Decorator(ret => Shrine1Location.Distance(Me.Location) > 3,
                                                            new Action(ret => Navigator.MoveTo(Shrine1Location))),
                                                        new Decorator(ret => Shrine1Location.Distance(Me.Location) <= 3,
                                                            new Action(ret => _firstExplored = true)))),

                                                new Decorator(ret => !_secondExplored,
                                                    new PrioritySelector(
                                                        new Decorator(ret => Shrine2Location.Distance(Me.Location) > 3,
                                                            new Action(ret => Navigator.MoveTo(Shrine2Location))),
                                                        new Decorator(ret => Shrine2Location.Distance(Me.Location) <= 3,
                                                            new Action(ret => _secondExplored = true)))),

                                                new Decorator(ret => !_thridExplored,
                                                    new PrioritySelector(
                                                        new Decorator(ret => Shrine3Location.Distance(Me.Location) > 3,
                                                            new Action(ret => Navigator.MoveTo(Shrine3Location))),
                                                        new Decorator(ret => Shrine3Location.Distance(Me.Location) <= 3,
                                                            new Sequence(
                                                               new Action(ret => _thridExplored = true),
                                                               new Action(ret => WaitAtThridTimer.Reset()))))),

                                                new Decorator(ret => !_waitForThrid,
                                                    new PrioritySelector(
                                                        new Decorator(ret => WaitAtThridTimer.IsFinished,
                                                            new Action(ret => _waitForThrid = true)))),

                                                new Decorator(ret => !_campExplored && _waitForThrid,
                                                    new PrioritySelector(
                                                        new Decorator(ret => CampLocation.Distance(Me.Location) > 3,
                                                            new Action(ret => Navigator.MoveTo(CampLocation))),
                                                        new Decorator(ret => CampLocation.Distance(Me.Location) <= 3,
                                                               new Action(ret => _campExplored = true))))
                                                            

                                            ))
                                                            

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

            AimingTimer.Reset();

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
