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
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.FortuneAndGlory
{
    [CustomBehaviorFileName(@"SpecificQuests\27748-Uldum-FortuneAndGlory")]
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
                    TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior());
                    Targeting.Instance.IncludeTargetsFilter -= Instance_IncludeTargetsFilter;
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
                return new Decorator(ret => IsQuestComplete(), new Action(delegate
                {
                    TreeRoot.StatusText =
                        "Finished!";
                    _isBehaviorDone = true;
                    return RunStatus.Success;
                }));
            }
        }

        private const uint ObsidianColossusId = 46646;

        public WoWUnit Enemey
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.IsAlive && u.Entry == ObsidianColossusId);
            }
        }

        public Composite TargetHim
        {
            get
            {
                return new PrioritySelector(ctx => Enemey, 
                    new Decorator(ctx => Me.CurrentTarget != (WoWUnit)ctx, 
                        new Action(ctx => ((WoWUnit)ctx).Target())));
            }
        }


        public Composite WaitAround
        {
            get
            {
                return new PrioritySelector(ctx => Enemey,
                    new Decorator(ctx => ctx != null && ((WoWUnit)ctx).HealthPercent > 26, new ActionAlwaysSucceed()));
            }
        }

        public Composite Kick
        {
            get
            {
                return new PrioritySelector( ctx => Me.CurrentTarget,
                    new Decorator(ctx => ctx != null && ((WoWUnit)ctx).IsCasting && ((WoWUnit)ctx).CastingSpellId == 87990 && SpellManager.CanCast(InteruptSpellName),
                    new Action(ctx => SpellManager.Cast(InteruptSpellName))));
            }
        }

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, WaitAround,TargetHim, Kick)));
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


        public string InteruptSpellName
        {
            get
            {
                switch (Me.Class)
                {
                    case WoWClass.Mage:
                        return "Counterspell";
                    case WoWClass.Paladin:
                        return "Rebuke";
                    case WoWClass.Shaman:
                        return "Wind Shear";
                    case WoWClass.DeathKnight:
                        return "Mind Freeze";
                    case WoWClass.Hunter:
                        return "Silencing Shot";
                    case WoWClass.Warrior:
                        return "Pummel";
                    case WoWClass.Rogue:
                        return "Kick";
                }
                return String.Empty;
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
                TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior());
                Targeting.Instance.IncludeTargetsFilter += Instance_IncludeTargetsFilter;
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = this.GetType().Name + ": " +
                                    ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
            }
        }

        void Instance_IncludeTargetsFilter(List<WoWObject> incomingUnits, HashSet<WoWObject> outgoingUnits)
        {
            foreach (var unit in incomingUnits.OfType<WoWUnit>())
            {
                if (unit.Entry == ObsidianColossusId && unit.HealthPercent <= 26)
                    outgoingUnits.Add(unit);
            }
        }

        #endregion
    }
}