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
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class JadeForestTakeNoPrisoners : CustomForcedBehavior
    {
        public JadeForestTakeNoPrisoners(Dictionary<string, string> args)
            : base(args)
        {

            try
            {
                QuestId = GetAttributeAsNullable("QuestId", false, ConstrainAs.QuestId(this), null) ?? 29727;
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

        private bool _usedTurret;

        public static int[] MobIds = new[] { 55411, 55484, 55410, 55473, 55505, 55485 };
        public static int[] OrcIds = new[] { 55498, 55501, 55499};
        public static int DwarfID = 55286;

        public static WoWPoint TurretLocation = new WoWPoint(1116.968f, -544.0963f, 413.5516f);
        public static WoWPoint UsingTurretLocation = new WoWPoint(1296.96f, -430.156f, 314.718f);

        public WoWUnit HozenEnemy
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => MobIds.Contains((int)u.Entry) && !u.IsDead)
                                     .OrderBy(u => u.Distance).FirstOrDefault());
            }
        }

        public WoWUnit OrcEnemy
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => OrcIds.Contains((int)u.Entry) && !u.IsDead)
                                     .OrderBy(u => u.Distance).FirstOrDefault());
            }
        }

        public WoWGameObject Turret
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWGameObject>()
                                     .Where(u => u.Entry == 209621)
                                     .OrderBy(u => u.Distance).FirstOrDefault());
            }
        }

        public WoWUnit Amber
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => u.Entry == 55283 && !u.IsDead)
                                     .OrderBy(u => u.Distance).FirstOrDefault());
            }
        }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id: CombatUseItemOn.cs 249 2012-09-19 01:31:37Z natfoth $"); } }
        public override string SubversionRevision { get { return ("$Revision: 249 $"); } }


        ~JadeForestTakeNoPrisoners()
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
                                    ret => !Me.InVehicle,
                                    new PrioritySelector(
                                        new Decorator(ret => Amber == null,
                                            new Sequence(
                                                new Action(ret => TreeRoot.StatusText = "Moving to Start Amber(Human) Story"),
                                                new Action(ret => Navigator.MoveTo(new WoWPoint(-157.5062f, -2659.278f, 1.069468f)))
                                             )),

                                        new Decorator(ret => Amber != null && !Amber.WithinInteractRange,
                                                new Action(ret => Navigator.MoveTo(Amber.Location))
                                             ),

                                        new Decorator(ret => Amber != null && Amber.WithinInteractRange,
                                            new Sequence(
                                                new Action(ret => WoWMovement.MoveStop()),
                                                new Action(ret => Amber.Interact()),
                                                new Action(ret => Thread.Sleep(400)),
                                                new Action(ret => Lua.DoString("SelectGossipOption(1,\"gossip\", true)"))
                                             )))),


                                    new Decorator(
                                        ret => Me.InVehicle,
                                            new PrioritySelector(
                                                 new Decorator(ret => HozenEnemy != null,
                                                    new Sequence(
                                                        new Action(ret => HozenEnemy.Target()),
                                                        new Action(ret => Thread.Sleep(400)),
                                                        new Action(ret => HozenEnemy.Interact()))),

                                                new Decorator(ret => OrcEnemy != null, // Orc Has to be Seperate or we will Die
                                                    new Sequence(
                                                        new Action(ret => OrcEnemy.Target()),
                                                        new Action(ret => Thread.Sleep(400)),
                                                        new Action(ret => OrcEnemy.Interact()))), 

                                                new Decorator(ret => UsingTurretLocation.Distance(StyxWoW.Me.Location) > 30 && !_usedTurret,
                                                    new PrioritySelector(
                                                        new Decorator(ret => TurretLocation.Distance(Me.Location) > 3,
                                                            new Action(ret => Navigator.MoveTo(TurretLocation))),
                                                        new Decorator(ret => TurretLocation.Distance(Me.Location) <= 3,
                                                            new Sequence(
                                                            new Action(ret => Turret.Interact()),
                                                            new Action(ret => _usedTurret = true)))))
                                                            

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
