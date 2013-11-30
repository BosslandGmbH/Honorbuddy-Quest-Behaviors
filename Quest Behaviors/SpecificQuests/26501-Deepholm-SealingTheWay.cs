//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

#region Summary and Documentation
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;

using Bots.Grind;
using Honorbuddy.QuestBehaviorCore;
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
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.SealingTheWay
{
    [CustomBehaviorFileName(@"SpecificQuests\26501-Deepholm-SealingTheWay")]
    public class SealingTheWay : CustomForcedBehavior
    {
        ~SealingTheWay()
        {
            Dispose(false);
        }

        public SealingTheWay(Dictionary<string, string> args)
            : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                QuestId = 26501;//GetAttributeAsNullable<int>("QuestId",false, ConstrainAs.QuestId(this), null) ?? 0;
                QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = QuestInLogRequirement.InLog;
                MobIds = new uint[] { 50635, 50638, 50643, 50636 };
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                QBCLog.Exception(except);
                IsAttributeProblem = true;
            }
        }


        // Attributes provided by caller
        public uint[] MobIds { get; private set; }
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
                    TreeHooks.Instance.RemoveHook("Questbot_Main", CreateBehavior_QuestbotMain());
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
                return new Decorator(ret => Me.IsQuestComplete(QuestId),
                    new Action(delegate
                    {
                        TreeRoot.StatusText = "Finished!";
                        _isBehaviorDone = true;
                        return RunStatus.Success;
                    }));
            }
        }

        public WoWUnit Geomancer(WoWPoint loc)
        {
            return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 43170 && u.IsAlive && u.Location.Distance(loc) <= 5).OrderBy(u => u.Distance).FirstOrDefault();
        }

        public WoWUnit Bad(WoWPoint loc)
        {
            return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.IsAlive && !u.IsPlayer && u.CurrentTarget != null && (u.CurrentTarget == Geomancer(loc) || u.CurrentTarget == Me)).OrderBy(u => u.Distance).FirstOrDefault();
        }


        public Composite DoDps
        {
            get
            {
                return
                    new PrioritySelector(
                        new Decorator(ret => RoutineManager.Current.CombatBehavior != null, RoutineManager.Current.CombatBehavior),
                        new Action(c => RoutineManager.Current.Combat()));
            }
        }

        public Composite DoPull
        {
            get
            {
                return
                    new PrioritySelector(
                        new Decorator(ret => RoutineManager.Current.PullBehavior != null, RoutineManager.Current.PullBehavior),
                        new Action(c => RoutineManager.Current.Pull()));
            }
        }
        public WoWPoint[] Spots = new WoWPoint[]
        {
            new WoWPoint(411.33,1659.2,348.8838),
            new WoWPoint(420.792,1718.1,349.4922),
            new WoWPoint(457.47,1727.42,348.5146),
            new WoWPoint(491.014,1659.59,348.2862)
        };


        public Composite Part(int i)
        {
            return new Decorator(r => !Me.IsQuestObjectiveComplete(QuestId, i),
                new PrioritySelector(
                    new Decorator(r => Geomancer(Spots[i - 1]) != null && Geomancer(Spots[i - 1]).Distance > 10,
                        new Action(r => Flightor.MoveTo(Geomancer(Spots[i - 1]).Location))),
                    new Decorator(r => (Me.CurrentTarget == null || (Me.CurrentTarget != null && Me.CurrentTarget.IsFriendly)) && Bad(Spots[i - 1]) != null,
                        new Action(r => Bad(Spots[i - 1]).Target())),
                    new Decorator(r => (Me.CurrentTarget == null || (Me.CurrentTarget != null && Me.CurrentTarget.IsFriendly)) && (Geomancer(Spots[i - 1]).CurrentTarget != null),
                        new Action(r => Geomancer(Spots[i - 1]).CurrentTarget.Target())),
                    new Decorator(r => !Me.Combat && Bad(Spots[i - 1]) != null, DoPull),
                        new Decorator(r => Bad(Spots[i - 1]) == null,
                            UseItem(i - 1))));

        }

        public WoWItem Rock
        {
            get { return Me.BagItems.FirstOrDefault(x => x.Entry == 58885); }
        }

        public Composite UseItem(int x)
        {

            return new Action(delegate
            {
                var g = Geomancer(Spots[x]);
                if (g.Distance > 5)
                    Navigator.MoveTo(g.Location);
                g.Target();
                Rock.Use();
            });

        }

        protected Composite CreateBehavior_QuestbotMain()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, LevelBot.CreateCombatBehavior(), Part(1), Part(2), Part(3), Part(4))));
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
                TreeHooks.Instance.InsertHook("Questbot_Main", 0, CreateBehavior_QuestbotMain());

                this.UpdateGoalText(QuestId);
            }
        }

        #endregion
    }
}