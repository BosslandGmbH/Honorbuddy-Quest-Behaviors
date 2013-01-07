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
    public class FourWindsCleaningHouse : CustomForcedBehavior
    {
        public FourWindsCleaningHouse(Dictionary<string, string> args)
            : base(args)
        {

            try
            {
                QuestId = GetAttributeAsNullable("QuestId", false, ConstrainAs.QuestId(this), null) ?? 30078;
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

        public static int[] MobIds = new[] { 58014, 58015, 58017 };
        public static int ChenOutsideID = 56133;

        public static WoWPoint ChenOutsideLocation = new WoWPoint(-706.8047f, 1265.453f, 136.0237f);

        //public static Common.Helpers.WaitTimer AimingTimer = new Common.Helpers.WaitTimer(TimeSpan.FromSeconds(2));

        private bool _started;
        private bool _eddySlain;
        private bool _joogaSlain;
        private bool _fizzySlain;

        public WoWUnit Enemy
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => MobIds.Contains((int)u.Entry) && !u.IsDead)
                                     .OrderBy(u => u.Distance).FirstOrDefault());
            }
        }

        public WoWUnit EddyDead
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => u.Entry == 58014 && u.IsDead)
                                     .OrderBy(u => u.Distance).FirstOrDefault());
            }
        }

        public WoWUnit JoogaDead
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => u.Entry == 58015 && u.IsDead)
                                     .OrderBy(u => u.Distance).FirstOrDefault());
            }
        }

        public WoWUnit FizzyDead
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => u.Entry == 58017 && u.IsDead)
                                     .OrderBy(u => u.Distance).FirstOrDefault());
            }
        }

        public WoWUnit FizzySprite
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => u.Entry == 58253 && !u.IsDead)
                                     .OrderBy(u => u.Distance).FirstOrDefault());
            }
        }

        public WoWUnit ChenOutside
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => u.Entry == ChenOutsideID && !u.IsDead)
                                     .OrderBy(u => u.Distance).FirstOrDefault());
            }
        }


        public WoWGameObject TinyBubbles
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWGameObject>()
                                     .Where(u => u.Entry == 210231)
                                     .OrderBy(u => u.Distance).FirstOrDefault());
            }
        }

        public WoWGameObject EmptyKeg
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWGameObject>()
                                     .Where(u => u.Entry == 210230)
                                     .OrderBy(u => u.Distance).FirstOrDefault());
            }
        }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id: CombatUseItemOn.cs 249 2012-09-19 01:31:37Z natfoth $"); } }
        public override string SubversionRevision { get { return ("$Revision: 249 $"); } }


        ~FourWindsCleaningHouse()
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
                                    ret => Me.IsOutdoors && !_started,
                                    new PrioritySelector(
                                        new Decorator(ret => ChenOutside == null,
                                            new Sequence(
                                                new Action(ret => TreeRoot.StatusText = "Moving to Start Chen Escort"),
                                                new Action(ret => Navigator.MoveTo(new WoWPoint(-157.5062f, -2659.278f, 1.069468f)))
                                             )),

                                        new Decorator(ret => ChenOutside != null && !ChenOutside.WithinInteractRange,
                                                new Action(ret => Navigator.MoveTo(ChenOutside.Location))
                                             ),

                                        new Decorator(ret => ChenOutside != null && ChenOutside.WithinInteractRange,
                                            new Sequence(
                                                new Action(ret => WoWMovement.MoveStop()),
                                                new Action(ret => Flightor.MountHelper.Dismount()),
                                                new Action(ret => ChenOutside.Interact()),
                                                new Action(ret => Thread.Sleep(400)),
                                                new Action(ret => Lua.DoString("SelectGossipOption(1,\"gossip\", true)")),
                                                new Action(ret => _started = true)
                                             )))),


                                    new Decorator(
                                        ret => Me.MapId == 870,
                                            new PrioritySelector(

                                                new Decorator(ret => FizzySprite != null,
                                                    new Sequence(
                                                       new Decorator(ret => RoutineManager.Current.CombatBehavior != null,
                                                                RoutineManager.Current.CombatBehavior),
                                                         new Action(ret => FizzySprite.Target()),
                                                         new Action(ret => RoutineManager.Current.Pull()),
                                                         new Decorator(ret => FizzySprite.Distance > 20,
                                                          new Action(ret => WoWMovement.ClickToMove(FizzySprite.Location))))),

                                                new Decorator(ret => Enemy != null && (!_eddySlain || Enemy.Entry == 58014),
                                                    new PrioritySelector(
                                                        new Decorator(ret => EddyDead != null,
                                                            new Action(ret => _eddySlain = true)),

                                                        new Decorator(ret => TinyBubbles != null,
                                                            new Action(ret => WoWMovement.ClickToMove(TinyBubbles.Location))),
                                                        new Decorator(ret => Me.Combat && TinyBubbles == null && (Enemy.HasAura(110189) || Enemy.HasAura(110198)),
                                                            new PrioritySelector(
                                                                new Decorator(ret => Enemy.Distance > 3,
                                                                    new Action(ret => WoWMovement.ClickToMove(Enemy.Location))),
                                                                new Decorator(ret => Enemy.Distance <= 3,
                                                                    new Action(ret => Enemy.Interact())))),

                                                        new Decorator(ret => !Me.Combat || Enemy.Combat,
                                                            new Sequence(
                                                                new Decorator(ret => RoutineManager.Current.CombatBehavior != null,
                                                                         RoutineManager.Current.CombatBehavior),
                                                                new Action(ret => Enemy.Target()),
                                                                new Action(ret => RoutineManager.Current.Pull()),
                                                                new Decorator(ret => Enemy.Distance > 20,
                                                                     new Action(ret => WoWMovement.ClickToMove(Enemy.Location)))))
                                                        )),

                                               new Decorator(ret => Enemy != null && ((_eddySlain && !_joogaSlain) || Enemy.Entry == 58015),
                                                    new PrioritySelector(
                                                        new Decorator(ret => JoogaDead != null,
                                                            new Action(ret => _joogaSlain = true)),
                                                        new Decorator(ret => Me.Combat && EmptyKeg != null && !Enemy.HasAura(110183),
                                                            new PrioritySelector(
                                                                new Decorator(ret => EmptyKeg.Distance > 3,
                                                                    new Action(ret => WoWMovement.ClickToMove(EmptyKeg.Location))),
                                                                new Decorator(ret => EmptyKeg.Distance <= 3,
                                                                    new Action(ret => EmptyKeg.Interact())))),
                                                        new Decorator(ret => !Me.Combat || Enemy.Combat,
                                                            new Sequence(
                                                                new Decorator(ret => RoutineManager.Current.CombatBehavior != null,
                                                                         RoutineManager.Current.CombatBehavior),
                                                                new Action(ret => Enemy.Target()),
                                                                new Action(ret => RoutineManager.Current.Pull()),
                                                                new Decorator(ret => Enemy.Distance > 20,
                                                                     new Action(ret => WoWMovement.ClickToMove(Enemy.Location)))))
                                                        
                                                        )),





                                          new Decorator(ret => Enemy != null && ((_joogaSlain && !_fizzySlain) || Enemy.Entry == 58017),
                                                    new PrioritySelector(
                                                        new Decorator(ret => FizzyDead != null,
                                                            new Action(ret => _fizzySlain = true)),
                                                        new Decorator(ret => !Me.Combat || Enemy.Combat,
                                                            new Sequence(
                                                                new Decorator(ret => RoutineManager.Current.CombatBehavior != null,
                                                                         RoutineManager.Current.CombatBehavior),
                                                                new Action(ret => Enemy.Target()),
                                                                new Action(ret => RoutineManager.Current.Pull()),
                                                                new Decorator(ret => Enemy.Distance > 20,
                                                                     new Action(ret => WoWMovement.ClickToMove(Enemy.Location)))))

                                                        ))

                                         


                                                            

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
