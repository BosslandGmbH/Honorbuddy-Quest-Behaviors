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
//      VariantQuestIds [REQUIRED if EscortCompleteWhen=QuestComplete; Default:empty]:
//          [QuestId and VariantQuestIds cannot be provided at the same time]
//          A comma separated list of quest Ids that are variants of QuestId.
//          The variants have the same objectives but only have a different quest ID depending on race, class, or faction.
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
//      IgnoreMobsInBlackspots [optional; Default: true]
//          When true, any mobs within (or too near) a blackspot will be ignored
//          in the list of viable targets that are considered for item use.
//      MovementBy [optional; Default: FlightorPreferred]
//          [allowed values: ClickToMoveOnly/FlightorPreferred/NavigatorOnly/NavigatorPreferred/None]
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
//      TerminateWhen [optional; Default: "false"]
//          Defines a string that will be evaluated as an alternative termination condition
//          for the behavior.  The string represents a boolean expression.  When the expression
//          evaluates to 'true', the behavior will be terminated.
//          NB: the behavior may also terminate due to other reasons.  For instance, a required
//          item is not in inventory, the quest is already complete, etc.
//		TerminateAtMaxRunTimeSecs [optional; Default: int.MaxValue]
//			This option defines how long the Quest behavior should run in Seconds before terminating
//      TerminationChecksQuestProgress [optional; Default: true]
//          This option is only considered when the QuestId attribute is also provided.
//          If true (the default), then this behavior considers itself 'done' when the
//          quest completes.
//          If false, the behavior will continue to run after the Quest completes.  This
//          implies that you will need to supply a TerminateWhen attribute, or some other
//          technique (e.g., WaitForNpcs="False") that allows the behavior to terminate.
//          Even if this attribute is false, the behavior will not run if the quest
//          is already complete.  I.e., the TerminationChecksQuestProgress attribute
//          is only consulted if it is determined that the behavior needs to run in the
//          first place.
//
// BEHAVIOR EXTENSION ELEMENTS (goes between <CustomBehavior ...> and </CustomBehavior> tags)
// See the "Examples" section for typical usage.
//      AvoidMobs [optional; Default: none]
//          Specifies a set of 'avoid mobs' that will be temporarily installed while the behavior
//          is running.  When the behavior completes, or the Honorbuddy is stopped, these temporary
//          'avoid mobs' will be removed.
//          This element expects a list of <Mob> sub-elements.
//
//          Mob
//              Specifies a single blackspot with the following attributes:
//                  Name [optional; ""]
//                      The name of mob to be avoided.
//                  Entry [required]
//                      The ID of the mob to be avoided.
//
//      Blackspots [optional; Default: none]
//          Specifies a set of blackspots that will be temporarily installed while the behavior
//          is running.  When the behavior completes, or the Honorbuddy is stopped, these temporary
//          blackspots will be removed.
//          This element expects a list of <Blackspot> sub-elements.
//
//          Blackspot
//              Specifies a single blackspot with the following attributes:
//                  Name [optional; Default: X/Y/Z location of the blackspot]
//                      The name of the waypoint is presented to the user as it is visited.
//                      This can be useful for debugging purposes, and for making minor adjustments
//                      (you know which waypoint to be fiddling with).
//                  X/Y/Z [REQUIRED; Default: none]
//                      The world coordinates of the blackspot.
//                  Radius [optional; Default: 10.0]
//                      The radius of the blackspot.
//                  Height [optional; Default 1.0]
//                      The height of the blackspot.
//
// These elements are provided by QuestBehaviorBase, but are used on individual behaviors
// on a case-by-case basis.
//
//      PursuitList [optional; Default: none]
//          Specifies a set of mobs or objects to pursue to fulfill the goal.
//          This element expects a list of one or more of the following sub-elements:
//
//          PursueGameObject
//              Id [REQUIRED, if PursueWhen is not specified; Default: none]
//                  The Id of the GameObject you wish to pursue.  The Id is
//                  optional, because the PursueWhen expression is capable of specifying
//                  whether or not a particular mob should be pursued.
//              PursueWhen [REQUIRED, if Id is not specified; Default: none]
//                  defines a boolean expression that indicates the object under
//                  consideration should be pursued.  If 'true' is returned, then
//                  the object under consideration is a candidate for pursuit.
//                  The candidate object to be considered for pursuit is made available
//                  in a variable called "GAMEOBJECT".  This variable is of type
//                  WoWGameObject (http://docs.honorbuddy.com/html/09b5630c-a2fe-0519-14e6-96be1babf9fb.htm),
//                  and may use any of the methods or properties associated with this type.
//              Priority [optional; Range: [-10000..10000]; Default: 0]
//                  Alters the importance of the GAMEOBJECT in the pursuit list.  Higher values are more important.
//                  Each behavior may implement honoring the priority differently.
//                  This may result in an immediate target switch, or it may be consulted only
//                  when a new target is selected
//
//          PursueSelf
//              PursueWhen [REQUIRED, if Id is not specified; Default: none]
//                  defines a boolean expression that indicates when 'self' should be pursued.
//                  If 'true' is returned, then the toon is a candidate for pursuit.
//                  The candidate object to be considered for pursuit is made available
//                  in a variable called "ME".  This variable is of type
//                  LocalPlayer (http://docs.honorbuddy.com/html/f911a610-93d8-289e-16d7-5cc1fc7ed512.htm),
//                  and may use any of the methods or properties associated with this type.
//              Priority [optional; Range: [-10000..10000]; Default: 0]
//                  Alters the importance of the toon in the pursuit list.  Higher values are more important.
//                  Each behavior may implement honoring the priority differently.
//                  This may result in an immediate target switch, or it may be consulted only
//                  when a new target is selected
//
//          PursueUnit
//              Id [REQUIRED, if PursueWhen is not specified; Default: none]
//                  The Id of the Unit you wish to pursue.  The Id is
//                  optional, because the PursueWhen expression is capable of specifying
//                  whether or not a particular mob should be pursued.
//              PursueWhen [REQUIRED, if Id is not specified; Default: none]
//                  defines a boolean expression that indicates the object under
//                  consideration should be pursued.  If 'true' is returned, then
//                  the object under consideration is a candidate for pursuit.
//                  The candidate unit to be considered for pursuit is made available
//                  in a variable called "UNIT".  This variable is of type
//                  WoWGameObject (http://docs.honorbuddy.com/html/6a12b75b-11cf-59c9-6b9f-d1528403d326.htm),
//                  and may use any of the methods or properties associated with this type.
//              Priority [optional; Range: [-10000..10000]; Default: 0]
//                  Alters the importance of the UNIT in the pursuit list.  Higher values are more important.
//                  Each behavior may implement honoring the priority differently.
//                  This may result in an immediate target switch, or it may be consulted only
//                  when a new target is selected
//              ConvertWhen [optional; Default: false]
//                  defines a boolean expression that indicates the object under
//                  consideration can fulfill the needed goal, if we 'convert' it.
//                  The mob will be converted using the technique specified in the "ConvertBy"
//                  attribute.
//              ConvertBy [optional; ONE OF: Killing Default: Killing]
//                  Currently, it is only possible to 'convert' alive mobs into dead mobs.
//                  This is only useful when dead mobs are needed to fulfill the goal.
//
// THINGS TO KNOW:
// * n/a
//
// EXAMPLES:
// Usage of an arbitrary TerminateWhen terminating condition:
//      <CustomBehavior File="InteractWith" MobId="12345" TerminateWhen="GetItemCount(39328) &gt; 12" >
//
// Usage of AvoidMobs and Blackspots:
//      <CustomBehavior File="InteractWith" MobId="12345" >
//          <AvoidMobs>
//              <Mob Name="Stable Master Kitrik" Id="28683" />
//              <Mob Name="Initiate's Training Dummy" Id="32541" />
//              <Mob Name="Scarlet Lord Jesseriah McCree" Id="28964" />
//          </AvoidMobs>
//          <Blackspots>
//              <Blackspot X="2053.501" Y="-5783.61" Z="101.3919" Radius="15.69214" />
//              <Blackspot X="1639.395" Y="-5847.581" Z="116.1873" Radius="18.69113" />
//              <Blackspot X="1758.225" Y="-5873.512" Z="116.1236" Radius="30.69964" />
//              <Blackspot X="1777.866" Y="-5923.742" Z="116.1065" Radius="5.556122" />
//          </Blackspots>
//      </CustomBehavior>
//
// Usage of PursuitList:
//  Not all QBcore-based behaviors support the Pursuit list.  You will need to consult
//  the individual behavior to discover if <PursuitList> is supported.
//
//  The following three invocations are equivalent:
//      <CustomBehavior File="XXXXX" MobId="12345" MobId2="23456" MobId3="34567" />
//
//      <CustomBehavior File="XXXXX">
//          <PursuitList>
//              <PursueUnit Id="12345" />
//              <PursueUnit Id="23456" />
//              <PursueUnit Id="34567" />
//          </PursuitList>
//      </CustomBehavior>
//
//      <CustomBehavior File="XXXXX">
//          <PursuitList>
//              <PursueUnit PursueWhen="UNIT.Entry == 12345" />
//              <PursueUnit PursueWhen="UNIT.Entry == 23456" />
//              <PursueUnit PursueWhen="UNIT.Entry == 34567" />
//          </PursuitList>
//      </CustomBehavior>
//
//  If a certain mob needs to be the higher priority, you can alter that with "Priority" attribute.
//  The "Priority" attribute may cause an 'immediate' switch to a higher-priority target when it becomes
//  available, or the behavior may defer the priority consideration until a new target needs to be selected.
//  Each behavior using the <PursuitList> will utilize priority differently.
//  You can also pursue yourself:
//      <CustomBehavior File="XXXXX">
//          <PursuitList>
//              <PursueSelf PursueWhen="ME.HasAura(54321)" />
//              <PursueUnit Id="23456" PursueWhen="!IsObjectiveComplete(1,98765)" Priority="100" />
//              <PursueUnit Id="34567" PursueWhen="!IsObjectiveComplete(2,98765)" />
//          </PursuitList>
//      </CustomBehavior>
//
//  Some goals need pursuing by faction:
//      <CustomBehavior File="XXXXX">
//          <PursuitList>
//              <PursueUnit PursueWhen="UNIT.FactionId == 876" />
//          </PursuitList>
//      </CustomBehavior>
//
//  Some quests require pursuing "dead" mobs for their goals.  Sometimes, "Alive" mobs can be converted
//  to "Dead" mobs to accelerate the process:
//      <CustomBehavior File="XXXXX">
//          <PursuitList>
//              <PursueUnit Id="23456" PursueWhen="UNIT.IsDead &amp;&amp; !UNIT.TaggedByOther"
//                                     ConvertWhen="UNIT.IsAlive" ConvertBy="Killing" />
//          </PursuitList>
//      </CustomBehavior>
//
#endregion

#region Usings

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;


using Honorbuddy.QuestBehaviorCore.XmlElements;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Profiles.Quest.Order;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public abstract partial class QuestBehaviorBase : CustomForcedBehavior
    {
        #region Constructor and Argument Processing
        protected QuestBehaviorBase(Dictionary<string, string> args)
            : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            try
            {
                // Quest handling...
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
                VariantQuestIds = GetAttributeAsArray<int>("VariantQuestIds", false, ConstrainAs.QuestId(this), null, null) ?? new int[0];

                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
                QuestObjectiveIndex = GetAttributeAsNullable<int>("QuestObjectiveIndex", false, new ConstrainTo.Domain<int>(1, 10), null) ?? 0;

                // Tunables...
                IgnoreMobsInBlackspots = GetAttributeAsNullable<bool>("IgnoreMobsInBlackspots", false, null, null) ?? true;
                MovementBy = GetAttributeAsNullable<MovementByType>("MovementBy", false, null, null) ?? MovementByType.FlightorPreferred;
                NonCompeteDistance = GetAttributeAsNullable<double>("NonCompeteDistance", false, new ConstrainTo.Domain<double>(0.0, 50.0), null) ?? 20.0;

                TerminateAtMaxRunTimeSecs = GetAttributeAsNullable<int>("TerminateAtMaxRunTimeSecs", false, new ConstrainTo.Domain<int>(0, int.MaxValue), null) ?? int.MaxValue;

                // Go ahead and compile the "TerminateWhen" expression to look for problems...
                // Doing this in the constructor allows us to catch 'blind change'problems when ProfileDebuggingMode is turned on.
                // If there is a problem, an exception will be thrown (and handled here).
                var terminateWhenExpression = GetAttributeAs<string>("TerminateWhen", false, ConstrainAs.StringNonEmpty, null);
                TerminateWhenCompiledExpression = Utility.ProduceParameterlessCompiledExpression<bool>(terminateWhenExpression);
                TerminateWhen = Utility.ProduceCachedValueFromCompiledExpression(TerminateWhenCompiledExpression, false);

                TerminationChecksQuestProgress = GetAttributeAsNullable<bool>("TerminationChecksQuestProgress", false, null, null) ?? true;

                // Dummy attributes...
                // These attributes are accepted, but not used.  They are here to help the profile writer document without
                // causing "unknown attribute" warnings to be emitted.
                GetAttributeAs<string>("QuestName", false, ConstrainAs.StringNonEmpty, null);

                // XML types

                // Add temporary avoid mobs, if any were specified...
                // NB: ConfigMemento will restore the orginal list in OnFinished
                _temporaryAvoidMobs = AvoidMobsType.GetOrCreate(Element, "AvoidMobs");

                // Add temporary blackspots, if any were specified...
                // NB: Ideally, we'd save and restore the original blackspot list.  However,
                // BlackspotManager does not currently give us a way to "see" what is currently
                // on the list.
                _temporaryBlackspots = BlackspotsType.GetOrCreate(Element, "Blackspots");

                PursuitList = PursuitListType.GetOrCreate(Element, "PursuitList");
            }

            catch (Exception except)
            {
                if (Query.IsExceptionReportingNeeded(except))
                {
                    // Maintenance problems occur for a number of reasons.  The primary two are...
                    // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                    // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                    // In any case, we pinpoint the source of the problem area here, and hopefully it can be quickly
                    // resolved.
                    QBCLog.Exception(except);
                }
                IsAttributeProblem = true;
            }
        }


        // Variables for Attributes provided by caller...
        // NB: The 'setters' are 'protected' (instead of 'private') in case the concrete QB needs
        // to reparse some information.  It is _very_ bad form to use the setters outside of
        // the base-class' or concrete-class' constructor.
        public bool IgnoreMobsInBlackspots { get; protected set; }
        public MovementByType MovementBy { get; protected set; }
        public PursuitListType PursuitList { get; protected set; }


        public double NonCompeteDistance { get; protected set; }
        public int QuestId { get; protected set; }
        public int[] VariantQuestIds { get; protected set; }
        public int QuestObjectiveIndex { get; protected set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; protected set; }
        public QuestInLogRequirement QuestRequirementInLog { get; protected set; }
        private int TerminateAtMaxRunTimeSecs { get; set; }

        private PerFrameCachedValue<bool> TerminateWhen { get; set; }

        [CompileExpression]
        public DelayCompiledExpression<Func<bool>> TerminateWhenCompiledExpression { get; protected set; }


        public bool TerminationChecksQuestProgress { get; protected set; }

        public readonly Stopwatch _behaviorRunTimer = new Stopwatch();

        // For the default value we use unexpanded
        // Git ID which GitIdToVersionId handles to
        // return vUnk.
        protected virtual string GitId => "$" + "Id$";

        public override string VersionId => GitIdToVersionId(GitId);

        internal static string GitIdToVersionId(string str)
        {
            // Format is $ Id$, without the space, when unexpanded
            if (str == "$" + "Id$") // Unexpanded, probably by downloading directly or something
                return "vUnk";

            return str.Substring(5, 6);
        }

        #endregion


        #region Private and Convenience variables
        private Composite _behaviorTreeHook_CombatMain;
        private Composite _behaviorTreeHook_CombatOnly;
        private Composite _behaviorTreeHook_DeathMain;
        private Composite _behaviorTreeHook_QuestbotMain;
        private Composite _behaviorTreeHook_Main;
        private ConfigMemento _configMemento;
        private bool _isBehaviorDone;
        private BlackspotsType _temporaryBlackspots;
        private AvoidMobsType _temporaryAvoidMobs;

        protected bool IsOnFinishedRun { get; private set; }
        public static LocalPlayer Me { get { return StyxWoW.Me; } }

        #endregion


        #region Overrides of CustomForcedBehavior

        public override NavType? NavType
        {
            get
            {
                switch (MovementBy)
                {
                    case MovementByType.FlightorPreferred:
                        return Styx.NavType.Fly;
                    case MovementByType.NavigatorOnly:
                    case MovementByType.NavigatorPreferred:
                        return Styx.NavType.Run;
                    default:
                        return null;
                }
            }
        }

        protected sealed override Composite CreateBehavior()
        {
            return _behaviorTreeHook_Main
                ?? (_behaviorTreeHook_Main = new ExceptionCatchingWrapper(this, CreateMainBehavior()));
        }

        private bool CheckTermination()
        {
            if (TerminationChecksQuestProgress)
            {
                var questId = GetQuestOrVariantId();
                if (Me.IsQuestObjectiveComplete(questId, QuestObjectiveIndex))
                    return true;

                if (!UtilIsProgressRequirementsMet(questId, QuestRequirementInLog, QuestRequirementComplete))
                    return true;
            }

            if (_behaviorRunTimer.ElapsedMilliseconds / 1000 >= TerminateAtMaxRunTimeSecs)
            {
                QBCLog.Info("Terminating due to 'max run time' expiry.");
                return true;
            }

            return false;
        }

        public sealed override bool IsDone
        {
            get
            {
                return _isBehaviorDone // normal completion
                       || TerminateWhen // Specified condition in profile
                       || CheckTermination(); // Quest/objective ID
            }
        }


        // 24Feb2013-08:10UTC chinajade
        public override void OnFinished()
        {
            // Defend against being called multiple times (just in case)...
            if (!IsOnFinishedRun)
            {
                if (Targeting.Instance != null)
                {
                    Targeting.Instance.IncludeTargetsFilter -= TargetFilter_IncludeTargets;
                    Targeting.Instance.RemoveTargetsFilter -= TargetFilter_RemoveTargets;
                    Targeting.Instance.WeighTargetsFilter -= TargetFilter_WeighTargets;
                }


                // NB: we don't unhook _behaviorTreeHook_Main
                // This was installed when HB created the behavior, and its up to HB to unhook it

                BehaviorHookRemove("Combat_Main", ref _behaviorTreeHook_CombatMain);
                BehaviorHookRemove("Combat_Only", ref _behaviorTreeHook_CombatOnly);
                BehaviorHookRemove("Death_Main", ref _behaviorTreeHook_DeathMain);
                BehaviorHookRemove("Questbot_Main", ref _behaviorTreeHook_QuestbotMain);

                // Remove temporary blackspots...
                if (_temporaryBlackspots != null)
                {
                    BlackspotManager.RemoveBlackspots(_temporaryBlackspots.GetBlackspots());
                    _temporaryBlackspots = null;
                }

                // Restore configuration...
                if (_configMemento != null)
                {
                    _configMemento.Dispose();
                    _configMemento = null;
                }

                // Make sure we don't leave stale POIs set after finishing a QB.
                // This could happen if the user used a TerminateWhen to finish it
                // for example.
                BotPoi.Clear("Finished " + GetType().Name);

                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Report the behavior run time...
                if (_behaviorRunTimer.IsRunning)
                {
                    _behaviorRunTimer.Stop();
                    QBCLog.DeveloperInfo("Behavior completed in {0}", Utility.PrettyTime(_behaviorRunTimer.Elapsed));
                }

                base.OnFinished();
                QBCLog.BehaviorLoggingContext = null;
                IsOnFinishedRun = true;
            }
        }


        public override void OnStart()
        {
            var isBehaviorShouldRun = OnStart_QuestBehaviorCore();

            if (isBehaviorShouldRun)
            {
                // empty for now...
            }
        }
        #endregion


        #region Concrete class overrides
        // Most of the time, we want a ConfigMemento to be created...
        // However, behaviors occasionally do not want this to happen (i.e., UserSettings).
        // So, we allow concrete behaviors to override this factory.
        // 17Feb2014-06:41UTC chinajade
        protected virtual ConfigMemento CreateConfigMemento()
        {
            return new ConfigMemento();
        }
        #endregion


        #region Base class primitives
        /// <summary>
        /// <para>This reports problems, and stops BT processing if there was a problem with attributes...
        /// We had to defer this action, as the 'profile line number' is not available during the element's
        /// constructor call.</para>
        /// <para>It also captures the user's configuration, and installs Behavior Tree hooks.  The items will
        /// be restored when the behavior terminates, or Honorbuddy is stopped.</para>
        /// </summary>
        /// <return>true, if the behavior should run; false, if it should not.</return>
        /// <param name="extraGoalTextDescription"></param>
        protected bool OnStart_QuestBehaviorCore(string extraGoalTextDescription = null)
        {
            // Semantic coherency / covariant dependency checks...
            UsageCheck_SemanticCoherency(Element,
                ((QuestObjectiveIndex > 0) && (QuestId <= 0)),
                context => string.Format("QuestObjectiveIndex of '{0}' specified, but no corresponding QuestId provided",
                                        QuestObjectiveIndex));

            UsageCheck_SemanticCoherency(Element,
                QuestId > 0 && VariantQuestIds.Any(),
                context => "Cannot provide both a QuestId and VariantQuestIds at same time.");

            UsageCheck_SemanticCoherency(Element,
                VariantQuestIds.Length == 1,
                context => "VariantQuestIds must provide at least 2 quest IDs.");

            if (VariantQuestIds.Any())
            {
                var completedQuests = new HashSet<uint>(StyxWoW.Me.QuestLog.GetCompletedQuests());
                UsageCheck_SemanticCoherency(Element,
                    VariantQuestIds.Count(id => Me.QuestLog.ContainsQuest((uint)id) || completedQuests.Contains((uint)id)) > 1,
                    context => $"Multiple quests provided by VariantQuestIds: ({string.Join(", ", VariantQuestIds)}) " +
                               "were found in player's quest log or have been turned in. " +
                               "This indicates that some/all of the quests specified by VariantQuestIds are not variants.");
            }

            EvaluateUsage_SemanticCoherency(Element);

            // Deprecated attributes...
            EvaluateUsage_DeprecatedAttributes(Element);

            // This reports problems, and stops BT processing if there was a problem with attributes...
            // We had to defer this action, as the 'profile line number' is not available during the element's
            // constructor call.
            OnStart_HandleAttributeProblem();

            var questId = GetQuestOrVariantId();
            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            // NB: Since the IsDone property may skip checking the 'progress conditions', we need to explicltly
            // check them here to see if we even need to start the behavior.
            if (!(IsDone || !UtilIsProgressRequirementsMet(questId, QuestRequirementInLog, QuestRequirementComplete)))
            {
                this.UpdateGoalText(questId, extraGoalTextDescription);

                // Start the timer to measure the behavior run time...
                _behaviorRunTimer.Restart();

                // Monitored Behaviors...
                if (QuestBehaviorCoreSettings.Instance.MonitoredBehaviors.Contains(GetType().Name))
                {
                    QBCLog.Debug("MONITORED BEHAVIOR: {0}", GetType().Name);
                    AudibleNotifyOn(true);
                }

                _configMemento = CreateConfigMemento();

                if (Targeting.Instance != null)
                {
                    Targeting.Instance.IncludeTargetsFilter += TargetFilter_IncludeTargets;
                    Targeting.Instance.RemoveTargetsFilter += TargetFilter_RemoveTargets;
                    Targeting.Instance.WeighTargetsFilter += TargetFilter_WeighTargets;
                }

                Query.InCompetitionReset();
                Utility.BlacklistsReset();

                _behaviorTreeHook_CombatMain = BehaviorHookInstall("Combat_Main", CreateBehavior_CombatMain());
                _behaviorTreeHook_CombatOnly = BehaviorHookInstall("Combat_Only", CreateBehavior_CombatOnly());
                _behaviorTreeHook_DeathMain = BehaviorHookInstall("Death_Main", CreateBehavior_DeathMain());
                _behaviorTreeHook_QuestbotMain = BehaviorHookInstall("Questbot_Main", CreateBehavior_QuestbotMain());

                BlackspotManager.AddBlackspots(_temporaryBlackspots.GetBlackspots());

                if (_temporaryAvoidMobs != null)
                {
                    foreach (var avoidMobId in _temporaryAvoidMobs.GetAvoidMobIds())
                    {
                        // NB: ProfileManager.CurrentProfile.AvoidMobs will never be null
                        if (!ProfileManager.CurrentProfile.AvoidMobs.Contains(avoidMobId))
                        {
                            ProfileManager.CurrentProfile.AvoidMobs.Add(avoidMobId);
                        }
                    }
                }

                return true;    // behavior should run
            }

            return false;   // behavior should NOT run
        }
        #endregion


        protected void BehaviorDone(string extraMessage = null)
        {
            if (!_isBehaviorDone)
            {
                QBCLog.DeveloperInfo("{0} behavior complete.  {1}", GetType().Name, (extraMessage ?? string.Empty));
                _isBehaviorDone = true;
            }
        }


        // Only installs behaviors the concrete class has defined...
        protected Composite BehaviorHookInstall(string behaviorHookName, Composite behavior)
        {
            if (behavior != null)
            {
                behavior = new ExceptionCatchingWrapper(this, behavior);
                TreeHooks.Instance.InsertHook(behaviorHookName, 0, behavior);
            }

            return behavior;
        }


        // Remove a specific installed behavior...
        // NB: it nulls the behavior, after it has been detached.
        protected void BehaviorHookRemove(string behaviorHookName, ref Composite behavior)
        {
            if (behavior != null)
            {
                TreeHooks.Instance.RemoveHook(behaviorHookName, behavior);
                behavior = null;
            }
        }


        /// <summary>
        /// <para>This method should check for use of deprecated attributes by the profile.
        /// It should make calls to UsageCheck_DeprecatedAttribute() to accomplish the task.</para>
        /// </summary>
        /// <param name="xElement"></param>
        protected abstract void EvaluateUsage_DeprecatedAttributes(XElement xElement);
        //{
        //     // EXAMPLE:
        //    UsageCheck_DeprecatedAttribute(xElement,
        //        Args.Keys.Contains("Nav"),
        //        "Nav",
        //        context => string.Format("Automatically converted Nav=\"{0}\" attribute into MovementBy=\"{1}\"."
        //                                  + "  Please update profile to use MovementBy, instead.",
        //                                  Args["Nav"], MovementBy));
        // }


        /// <summary>
        /// <para>This method should perform any semantic coherency, or covariant dependency checks needed
        /// by the behavior.  It should make calls to UsageCheck_SemanticCoherency() to accomplish the task.</para>
        /// </summary>
        /// <param name="xElement"></param>
        protected abstract void EvaluateUsage_SemanticCoherency(XElement xElement);
        //{
        //    // EXAMPLE:
        //    UsageCheck_SemanticCoherency(xElement,
        //        (!MobIds.Any() && !FactionIds.Any()),
        //        context => "You must specify one or more MobIdN, one or more FactionIdN, or both.");
        //
        //    const double rangeEpsilon = 3.0;
        //    UsageCheck_SemanticCoherency(xElement,
        //        ((RangeMax - RangeMin) < rangeEpsilon),
        //        context => string.Format("Range({0}) must be at least {1} greater than MinRange({2}).",
        //                      RangeMax, rangeEpsilon, RangeMin));
        //}

        protected PlayerQuest GetQuestOrVariantInLog()
        {
            if (VariantQuestIds.Any())
                return VariantQuestIds.Select(id => StyxWoW.Me.QuestLog.GetQuestById((uint)id)).FirstOrDefault(q => q != null);

            return StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

        }

        /// <summary>
        /// Searches player's quest log and list of completed quests for a quest specified by
        /// <see cref="QuestId"/> and <see cref="VariantQuestIds"/>,
        /// and returns the id if found; otherwise returns <see cref="QuestId"/>
        /// </summary>
        /// <returns></returns>
        protected int GetQuestOrVariantId()
        {
            var questInLog = GetQuestOrVariantInLog();
            if (questInLog != null)
                return (int)questInLog.Id;

            if (!VariantQuestIds.Any())
                return QuestId;

            var completedQuests = new HashSet<uint>(StyxWoW.Me.QuestLog.GetCompletedQuests());
            return VariantQuestIds
                    .FirstOrDefault(id => completedQuests.Contains((uint)id));
        }

        #region TargetFilters

        /// <summary> Includes object in targeting list when returns true. This should be overridden in QBs to determine if the object should be in target list or not.
        ///           Keep in mind that critters, guards, players and tagged mobs are now passed in include filters rather then to be removed in default remove filter.
        ///           Have extra checks for those if you don't want them in target list.</summary>
        ///
        /// <remarks> raphus, 24/07/2013. </remarks>
        ///
        /// <param name="unit"> The WoWUnit. </param>
        ///
        /// <returns> true if it succeeds, false if it fails. </returns>
        protected virtual bool IncludeUnitInTargeting(WoWUnit wowUnit)
        {
            return false;
        }

        /// <summary> Removes the object from targeting list when returns true. This should be overridden in QBs to determine if the object should be removed from target list or not.
        ///           This should be used only for WoWObjects that we don't really want to be included. For example, default include filter includes all units  that are attacking us.
        ///           If we have a case where we don't want to attack to an attacker, it should be removed here. </summary>
        ///
        /// <remarks> raphus, 24/07/2013. </remarks>
        ///
        /// <param name="unit"> The WoWUnit. </param>
        ///
        /// <returns> true if it succeeds, false if it fails. </returns>
        protected virtual bool RemoveUnitFromTargeting(WoWUnit wowUnit)
        {
            return false;
        }

        /// <summary> Weight unit for targeting. </summary>
        ///
        /// <remarks> raphus, 24/07/2013. </remarks>
        ///
        /// <param name="wowUnit"> The unit. </param>
        ///
        /// <returns> . </returns>
        protected virtual float WeightUnitForTargeting(WoWUnit wowUnit)
        {
            // Prefer units closest to us...
            return (float)(-wowUnit.CollectionDistance());
        }


        /// <summary>
        /// <para>HBcore runs the TargetFilter_RemoveTargets before the TargetFilter_IncludeTargets.</para>
        /// </summary>
        /// <param name="units"></param>
        protected virtual void TargetFilter_IncludeTargets(List<WoWObject> incomingWowObjects, HashSet<WoWObject> outgoingWowObjects)
        {
            foreach (var wowObject in incomingWowObjects)
            {
                var wowUnit = wowObject.ToUnit();

                if (!Query.IsViable(wowUnit))
                    continue;

                if (IncludeUnitInTargeting(wowUnit))
                    outgoingWowObjects.Add(wowUnit);
            }
        }


        /// <summary>
        /// <para>HBcore runs the TargetFilter_RemoveTargets before the TargetFilter_IncludeTargets.</para>
        /// </summary>
        /// <param name="wowObjects"></param>
        protected virtual void TargetFilter_RemoveTargets(List<WoWObject> wowObjects)
        {
            wowObjects.RemoveAll(obj =>
                {
                    var wowUnit = obj.ToUnit();

                    // We are not interested with objects.
                    return !Query.IsViable(wowUnit) || RemoveUnitFromTargeting(wowUnit);
                });
        }


        /// <summary>
        /// <para>When scoring targets, a higher value of TargetPriority.Score makes the target more valuable.</para>
        /// </summary>
        /// <param name="units"></param>
        protected virtual void TargetFilter_WeighTargets(List<Targeting.TargetPriority> targetPriorities)
        {
            foreach (var targetPriority in targetPriorities)
            {
                var wowUnit = targetPriority.Object.ToUnit();

                if (!Query.IsViable(wowUnit))
                    continue;

                targetPriority.Score += WeightUnitForTargeting(wowUnit);
            }
        }
        #endregion


        #region Main Behaviors
        protected virtual Composite CreateBehavior_CombatMain()
        {
            return null;
        }


        protected virtual Composite CreateBehavior_CombatOnly()
        {
            return null;
        }


        protected virtual Composite CreateBehavior_DeathMain()
        {
            return null;
        }


        protected virtual Composite CreateBehavior_QuestbotMain()
        {
            return null;
        }


        protected virtual Composite CreateMainBehavior()
        {
            return new PrioritySelector(
                // empty, for now...
                );
        }
        #endregion
    }


    public static class CustomForcedBehavior_Extensions
    {
        public static void UpdateGoalText(this CustomForcedBehavior cfb, int questId, string extraGoalTextDescription = null)
        {
            TreeRoot.GoalText = string.Format(
                "{0}: {1}{2}    {3}",
                QBCLog.VersionedBehaviorName,
                (GetQuestReference(questId) + Environment.NewLine),
                ((extraGoalTextDescription != null)
                    ? (extraGoalTextDescription + Environment.NewLine)
                    : string.Empty),
                Utility.GetProfileReference(cfb.Element));
        }

        private static string GetQuestReference(int questId)
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)questId);

            return
                (quest != null)
                ? $"\"{quest.Name}\" (http://wowhead.com/quest={questId})"
                    : "In Progress (no associated quest)";
        }
    }
}
