/**
 * Behavior originally contributed by LastCoder
 **/
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Action = Styx.TreeSharp.Action;

namespace Behaviors
{
    class KunLaiTheFallofShaiHu : CustomForcedBehavior
    {

        #region Construction
        public KunLaiTheFallofShaiHu(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                QuestId = GetAttributeAsNullable("QuestId", false, ConstrainAs.QuestId(this), null) ?? 30855;
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
        #endregion

        #region Variables

        // Variables filled by construction
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }

        // Internal variables
        private bool _isBehaviorDone = false;
        private bool _isDisposed;
        private Composite _root;
        private Composite _behaviorTreeCombatHook;
        private LocalPlayer Me { get { return (StyxWoW.Me); } }

        // Static constant variables
        public static List<uint> ExplosiveHatredIds = new List<uint> { 61070 };
        public static uint ShaiHuId = 61069;
        public static WoWPoint Waypoint = new WoWPoint(2104.215, 314.8302, 475.4525);

        public List<WoWUnit> Enemys
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => u.FactionId == 2550 && !u.Elite && !u.IsDead && u.Distance < 199)
                                     .OrderBy(u => u.Distance).ToList());
            }
        }

        public List<WoWUnit> ExplosiveHatredEnemies
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => ExplosiveHatredIds.Contains(u.Entry) && !u.IsDead)
                                     .OrderBy(u => u.Distance).ToList());
            }
        }

        public WoWUnit ExplosiveHatredEnemy
        {
            get
            {
                return ExplosiveHatredEnemies.FirstOrDefault();
            }
        }

        public WoWUnit ShaiHuNPC
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => ShaiHuId == u.Entry && !u.IsDead).OrderBy(u => u.Distance)).FirstOrDefault();
            }
        }

        public Composite CreateCombatBehavior()
        {
            return new PrioritySelector(
                  new Decorator(cond => ShaiHuNPC != null && !ShaiHuNPC.HasAura(118633) && Me.CurrentTarget != ShaiHuNPC,
                    new Action(r =>
                    {
                        BotPoi.Current = new BotPoi(ShaiHuNPC, PoiType.Kill);
                        ShaiHuNPC.Target();
                        return RunStatus.Failure;
                    })),
                  new Decorator(cond => ShaiHuNPC != null && ShaiHuNPC.HasAura(118633),
                    new PrioritySelector(
                                new Decorator(cond => !ExplosiveHatredEnemy.IsTargetingMeOrPet,
                                    new Action(r =>
                                    {
                                        TreeRoot.StatusText = "Pulling explosive hatred using no combat move...";
                                        WoWPoint[] path = Navigator.GeneratePath(Me.Location, ExplosiveHatredEnemy.Location);

                                        foreach (WoWPoint p in path)
                                        {
                                            Thread.Sleep(500);
                                            WoWMovement.ClickToMove(p);
                                        }
                                        return RunStatus.Failure;
                                    })),
                                new Decorator(cond => ExplosiveHatredEnemy.IsTargetingMeOrPet && ExplosiveHatredEnemy.Location.Distance(ShaiHuNPC.Location) > 10,
                                    new Action(r =>
                                    {

                                        WoWPoint[] path = Navigator.GeneratePath(Me.Location, ShaiHuNPC.Location);

                                        foreach (WoWPoint p in path)
                                        {
                                            Thread.Sleep(500);
                                            WoWMovement.ClickToMove(p);
                                        }
                                        return RunStatus.Failure;
                                    })),
                              new Decorator(cond => ExplosiveHatredEnemy.Location.Distance(ShaiHuNPC.Location) < 10,
                                  new Action(r =>
                                  {
                                      BotPoi.Current = new BotPoi(ExplosiveHatredEnemy, PoiType.Kill);
                                      ExplosiveHatredEnemy.Target();
                                      return RunStatus.Failure;
                                  })))),

                        new Decorator(cond => ShaiHuNPC != null && !ShaiHuNPC.HasAura(118633),
                            new Action(r =>
                            {
                                if (BotPoi.Current == null ? true : BotPoi.Current.Entry == ShaiHuId)
                                {
                                    BotPoi.Current = new BotPoi(ShaiHuNPC, PoiType.Kill);
                                }
                                Thread.Sleep(500);
                                Navigator.MoveTo(ShaiHuNPC.Location);
                                return RunStatus.Failure;
                            })));
        }

        #endregion

        #region Disposal
        ~KunLaiTheFallofShaiHu()
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

        #endregion

        #region CustomForcedBehavior Override

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = CreateMainBehavior());
        }

        public Composite CreateMainBehavior()
        {
            return new PrioritySelector(
                new PrioritySelector(new Decorator(
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
                                    )))))
 
                                     
                );
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
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = this.GetType().Name + ": " + ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");

                _behaviorTreeCombatHook = CreateCombatBehavior();

                TreeHooks.Instance.InsertHook("Combat_Only", 0, _behaviorTreeCombatHook);

                WoWMovement.ClickToMove(new WoWPoint(2175.019, 380.8854, 476.0461));
            }

        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
