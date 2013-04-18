// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Summary and Documentation
// QUICK DOX:
// TEMPLATE.cs is a skeleton for creating new quest behaviors.
//
// Quest binding:
//      QuestId [REQUIRED if EscortCompleteWhen=QuestComplete; Default:none]:
//      QuestCompleteRequirement [Default:NotComplete]:
//      QuestInLogRequirement [Default:InLog]:
//              A full discussion of how the Quest* attributes operate is described in
//              http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
//      QuestObjectiveIndex [REQUIRED if EventCompleteWhen=QuestObjectiveComplete]
//          [on the closed interval: [1..5]]
//          This argument is only consulted if EventCompleteWhen is QuestObjectveComplete.
//          The argument specifies the index of the sub-goal of a quest.
//
// Tunables (ideally, the profile would _never_ provide these arguments):
//      CombatMaxEngagementDistance [optional; Default: 23.0]
//          This is a work around for some buggy Combat Routines.  If a targetted mob is
//          "too far away", some Combat Routines refuse to engage it for killing.  This
//          value moves the toon within an appropriate distance to the requested target
//          so the Combat Routine will perform as expected.
//      IgnoreMobsInBlackspots [optional; Default: true]
//          When true, any mobs within (or too near) a blackspot will be ignored
//          in the list of viable targets that are considered for item use.
//      MovementBy [optional; Default: NavigatorPreferred]
//          [allowed values: ClickToMoveOnly/NavigatorOnly/NavigatorPreferred/None]
//          Allows alternative navigation techniques.  You should provide this argument
//          only when an area is unmeshed, or not meshed well.  If ClickToMoveOnly
//          is specified, the area must be free of obstacles; otherwise, the toon
//          will get hung up.
//      NonCompeteDistance [optional; Default: 20]
//          If a player is within this distance of a target that looks
//          interesting to us, we'll ignore the target.  The assumption is that the player may
//          be going for the same target, and we don't want to draw attention.
//          Shared resources, such as Vendors, Innkeepers, Trainers, etc. are never considered
//          to be "in competition".
//
// THINGS TO KNOW:
//
// EXAMPLE:
//     <CustomBehavior File="TEMPLATE" />
#endregion

#region Usings
using System;
using System.Collections.Generic;

using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public partial class QuestBehaviorBase : CustomForcedBehavior
    {
        #region Consructor and Argument Processing
        protected QuestBehaviorBase(Dictionary<string, string> args)
            : base(args)
        {
            BehaviorLoggingContext = this;

            try
            {
                // Quest handling...
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
                QuestObjectiveIndex = GetAttributeAsNullable<int>("QuestObjectiveIndex", false, new ConstrainTo.Domain<int>(1, 5), null) ?? 0;

                // Tunables...
                CombatMaxEngagementDistance = GetAttributeAsNullable<double>("CombatMaxEngagementDistance", false, new ConstrainTo.Domain<double>(1.0, 40.0), null) ?? 23.0;
                IgnoreMobsInBlackspots = GetAttributeAsNullable<bool>("IgnoreMobsInBlackspots", false, null, null) ?? true;
                MaxDismountHeight = GetAttributeAsNullable<double>("MaxDismountHeight", false, new ConstrainTo.Domain<double>(1.0, 75.0), null) ?? 8.0;
                MovementBy = GetAttributeAsNullable<MovementByType>("MovementBy", false, null, null) ?? MovementByType.NavigatorPreferred;
                NonCompeteDistance = GetAttributeAsNullable<double>("NonCompeteDistance", false, new ConstrainTo.Domain<double>(0.0, 50.0), null) ?? 20.0;

                // Semantic coherency / covariant dependency checks --
                if ((QuestObjectiveIndex > 0) && (QuestId <= 0))
                {
                    LogError("QuestObjectiveIndex of '{0}' specified, but no corresponding QuestId provided", QuestObjectiveIndex);
                    IsAttributeProblem = true;
                }
            }

            catch (Exception except)
            {
                if (IsExceptionReportingNeeded(except))
                {
                    // Maintenance problems occur for a number of reasons.  The primary two are...
                    // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                    // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                    // In any case, we pinpoint the source of the problem area here, and hopefully it can be quickly
                    // resolved.
                    LogError("[MAINTENANCE PROBLEM]: " + except.Message
                             + "\nFROM HERE:\n"
                             + except.StackTrace + "\n");
                }
                IsAttributeProblem = true;
            }
        }


        // Variables for Attributes provided by caller
        public double CombatMaxEngagementDistance { get; private set; }
        public bool IgnoreMobsInBlackspots { get; private set; }
        public double MaxDismountHeight { get; private set; }
        public MovementByType MovementBy { get; set; }
        public int QuestId { get; private set; }
        public int QuestObjectiveIndex { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }
        public double NonCompeteDistance { get; private set; }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return "$Id$"; } }
        public override string SubversionRevision { get { return "$Rev$"; } }
        #endregion


        #region Private and Convenience variables
        protected TimeSpan Delay_AfterItemUse { get { return TimeSpan.FromMilliseconds(_random.Next(400, 900)); } }
        protected TimeSpan Delay_Interaction { get { return TimeSpan.FromMilliseconds(_random.Next(600, 1700)); } }
        protected readonly TimeSpan Delay_LagDuration = TimeSpan.FromMilliseconds((StyxWoW.WoWClient.Latency * 2) + 150);
        protected readonly TimeSpan Delay_WoWClientMovementThrottle = TimeSpan.FromMilliseconds(250);
        protected LocalPlayer Me { get { return StyxWoW.Me; } }

        private Composite _behaviorTreeHook_CombatMain;
        private Composite _behaviorTreeHook_CombatOnly;
        private Composite _behaviorTreeHook_DeathMain;
        private Composite _behaviorTreeHook_Main;
        private ConfigMemento _configMemento;
        private bool _isBehaviorDone;
        private bool _isDisposed;

        // Statics
        public static Random _random = new Random((int)DateTime.Now.Ticks);
        #endregion


        #region Destructor, Dispose, and cleanup
        ~QuestBehaviorBase()
        {
            Dispose(false);
        }


        // 24Feb2013-08:10UTC chinajade
        protected void Dispose(bool isExplicitlyInitiatedDispose)
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

                // NB: we don't unhook _behaviorTreeHook_Main
                // This was installed when HB created the behavior, and its up to HB to unhook it

                if (_behaviorTreeHook_CombatMain != null)
                {
                    TreeHooks.Instance.RemoveHook("Combat_Main", _behaviorTreeHook_CombatMain);
                    _behaviorTreeHook_CombatMain = null;
                }

                if (_behaviorTreeHook_CombatOnly != null)
                {
                    TreeHooks.Instance.RemoveHook("Combat_Only", _behaviorTreeHook_CombatOnly);
                    _behaviorTreeHook_CombatOnly = null;
                }

                if (_behaviorTreeHook_DeathMain != null)
                {
                    TreeHooks.Instance.RemoveHook("Death_Main", _behaviorTreeHook_DeathMain);
                    _behaviorTreeHook_DeathMain = null;
                }

                BotEvents.OnBotStop -= BotEvents_OnBotStop;

                // Restore configuration...
                if (_configMemento != null)
                {
                    _configMemento.Dispose();
                    _configMemento = null;
                }

                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                BehaviorLoggingContext = null;
                base.Dispose();
            }

            _isDisposed = true;
        }


        private void BotEvents_OnBotStop(EventArgs args)
        {
            Dispose();
        }
        #endregion


        #region Overrides of CustomForcedBehavior

        protected sealed override Composite CreateBehavior()
        {
            return _behaviorTreeHook_Main ?? (_behaviorTreeHook_Main = CreateMainBehavior());
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
                return IsDone_BaseQuestBehavior();
            }
        }


        public override void OnStart()
        {
            OnStart_BaseQuestBehavior();
        }
        #endregion


        #region Base class primitives

        protected bool IsDone_BaseQuestBehavior()
        {
            return _isBehaviorDone     // normal completion
                    || IsQuestObjectiveComplete(QuestId, QuestObjectiveIndex)
                    || !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete);
        }


        protected void OnStart_BaseQuestBehavior(string extraDescription = null)
        {
            // This reports problems, and stops BT processing if there was a problem with attributes...
            // We had to defer this action, as the 'profile line number' is not available during the element's
            // constructor call.
            OnStart_HandleAttributeProblem();

            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (!IsDone)
            {
                // The ConfigMemento() class captures the user's existing configuration.
                // After its captured, we can change the configuration however needed.
                // When the memento is dispose'd, the user's original configuration is restored.
                // More info about how the ConfigMemento applies to saving and restoring user configuration
                // can be found here...
                //     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_Saving_and_Restoring_User_Configuration
                _configMemento = new ConfigMemento();

                BotEvents.OnBotStop += BotEvents_OnBotStop;

                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = string.Format(
                    "{0}: \"{1}\"\n{2}",
                    GetType().Name,
                    ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress (no associated quest)"),
                    (extraDescription ?? string.Empty));

                _behaviorTreeHook_CombatMain = CreateBehavior_CombatMain();
                TreeHooks.Instance.InsertHook("Combat_Main", 0, _behaviorTreeHook_CombatMain);
                _behaviorTreeHook_CombatOnly = CreateBehavior_CombatOnly();
                TreeHooks.Instance.InsertHook("Combat_Only", 0, _behaviorTreeHook_CombatOnly);
                _behaviorTreeHook_DeathMain = CreateBehavior_DeathMain();
                TreeHooks.Instance.InsertHook("Death_Main", 0, _behaviorTreeHook_DeathMain);
            }
        }
        #endregion


        protected void BehaviorDone()
        {
            if (!_isBehaviorDone)
            {
                LogDeveloperInfo("{0} behavior complete.", GetType().Name);
                _isBehaviorDone = true;
            }
        }

        #region Main Behaviors
        protected virtual Composite CreateBehavior_CombatMain()
        {
            return new PrioritySelector(
                // empty, for now...
                );
        }


        protected virtual Composite CreateBehavior_CombatOnly()
        {
            return new PrioritySelector(
                // empty, for now...
                );
        }


        protected virtual Composite CreateBehavior_DeathMain()
        {
            return new PrioritySelector(
                // empty, for now...
                );
        }


        protected virtual Composite CreateMainBehavior()
        {
            return new PrioritySelector(
                // empty, for now...
                );
        }
        #endregion
    }
}