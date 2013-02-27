// Template originally contributed by Chinajade.
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
//      CombatMaxEngagementRangeDistance [optional; Default: 23.0]
//          This is a work around for some buggy Combat Routines.  If a targetted mob is
//          "too far away", some Combat Routines refuse to engage it for killing.  This
//          value moves the toon within an appropriate distance to the requested target
//          so the Combat Routine will perform as expected.
//
// THINGS TO KNOW:
//
// EXAMPLE:
//     <CustomBehavior File="TEMPLATE" />
#endregion

#region Usings
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using System.Text;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.QuestBehaviors.TEMPLATE_QB
{
    public partial class TEMPLATE_QB : CustomForcedBehavior
    {
        #region Consructor and Argument Processing
        public TEMPLATE_QB(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // Quest handling...
                QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
                QuestObjectiveIndex = GetAttributeAsNullable<int>("QuestObjectiveIndex", false, new ConstrainTo.Domain<int>(1, 5), null) ?? 0;

                // Tunables...
                CombatMaxEngagementRangeDistance = GetAttributeAsNullable<double>("CombatMaxEngagementRangeDistance", false, new ConstrainTo.Domain<double>(1.0, 40.0), null) ?? 23.0;
                NonCompeteDistance = 25.0;
                Blackspots = new List<Blackspot>()
                {
                    // empty for now
                };

                // Semantic coherency / covariant dependency checks --
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it can be quickly
                // resolved.
                LogError("[MAINTENANCE PROBLEM]: " + except.Message
                        + "\nFROM HERE:\n"
                        + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }


        // Variables for Attributes provided by caller
        public double CombatMaxEngagementRangeDistance { get; private set; }
        public int QuestId { get; private set; }
        public int QuestObjectiveIndex { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }

        public IEnumerable<Blackspot> Blackspots { get; private set; }
        public double NonCompeteDistance { get; private set; }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return "$Id$"; } }
        public override string SubversionRevision { get { return "$Rev$"; } }
        #endregion


        #region Private and Convenience variables
        public delegate string MessageDelegate(object context);
        public delegate double RangeDelegate(object context);
        public delegate WoWUnit WoWUnitDelegate(object context);

        private readonly TimeSpan Delay_WoWClientMovementThrottle = TimeSpan.FromMilliseconds(100);
        private LocalPlayer Me { get { return StyxWoW.Me; } }

        private Composite _behaviorTreeHook_CombatMain = null;
        private Composite _behaviorTreeHook_CombatOnly = null;
        private Composite _behaviorTreeHook_DeathMain = null;
        private Composite _behaviorTreeHook_Main = null;
        private ConfigMemento _configMemento = null;
        private bool _isBehaviorDone = false;
        private bool _isDisposed = false;
        #endregion


        #region Destructor, Dispose, and cleanup
        ~TEMPLATE_QB()
        {
            Dispose(false);
        }


        // 24Feb2013-08:10UTC chinajade
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

                // Restore configuration...
                if (_configMemento != null)
                {
                    _configMemento.Dispose();
                    _configMemento = null;
                }

                BlackspotManager.RemoveBlackspots(Blackspots);

                BotEvents.OnBotStop -= BotEvents_OnBotStop;

                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }


        public void BotEvents_OnBotStop(EventArgs args)
        {
            Dispose();
        }
        #endregion


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
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
                return _isBehaviorDone     // normal completion
                        || !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete);
            }
        }


        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

            if ((QuestId != 0) && (quest == null))
            {
                LogMessage("error", "This behavior has been associated with QuestId({0}), but the quest is not in our log", QuestId);
                IsAttributeProblem = true;
            }

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

                // Disable any settings that may interfere with the escort --
                // When we escort, we don't want to be distracted by other things.
                // NOTE: these settings are restored to their normal values when the behavior completes
                // or the bot is stopped.
                CharacterSettings.Instance.HarvestHerbs = false;
                CharacterSettings.Instance.HarvestMinerals = false;
                CharacterSettings.Instance.LootChests = false;
                CharacterSettings.Instance.NinjaSkin = false;
                CharacterSettings.Instance.SkinMobs = false;
                CharacterSettings.Instance.PullDistance = 1;    // don't pull anything unless we absolutely must

                BlackspotManager.AddBlackspots(Blackspots);

                TreeRoot.GoalText = string.Format(
                    "{0}: \"{1}\"",
                    this.GetType().Name,
                    ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress (no associated quest)"));

                _behaviorTreeHook_CombatMain = CreateBehavior_CombatMain();
                TreeHooks.Instance.InsertHook("Combat_Main", 0, _behaviorTreeHook_CombatMain);
                _behaviorTreeHook_CombatOnly = CreateBehavior_CombatOnly();
                TreeHooks.Instance.InsertHook("Combat_Only", 0, _behaviorTreeHook_CombatOnly);
                _behaviorTreeHook_DeathMain = CreateBehavior_DeathMain();
                TreeHooks.Instance.InsertHook("Death_Main", 0, _behaviorTreeHook_DeathMain);
            }
        }
        #endregion


        #region Main Behaviors
        private Composite CreateBehavior_CombatMain()
        {
            return new PrioritySelector(
                );
        }


        private Composite CreateBehavior_CombatOnly()
        {
            return new PrioritySelector(
                );
        }


        private Composite CreateBehavior_DeathMain()
        {
            return new PrioritySelector(
                );
        }


        private Composite CreateMainBehavior()
        {
            return new PrioritySelector(

                // If quest is done, behavior is done...
                new Decorator(context => IsDone,
                    new Action(context =>
                    {
                        _isBehaviorDone = true;
                        LogMessage("info", "Finished");
                    }))
                );
        }
        #endregion


        #region Helpers

        // 25Feb2013-12:50UTC chinajade
        private IEnumerable<WoWUnit> FindMobsTargetingMeOrPet()
        {
            return
                from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
                where
                    IsViable(unit)
                    && !unit.IsFriendly
                    && ((unit.CurrentTarget == Me)
                        || (Me.GotAlivePet && unit.CurrentTarget == Me.Pet))
                select unit;
        }


        // 25Feb2013-12:50UTC chinajade
        private IEnumerable<WoWPlayer> FindPlayersNearby(WoWPoint location, double radius)
        {
            return
                from player in ObjectManager.GetObjectsOfType<WoWPlayer>()
                where
                    player.IsAlive
                    && player.Location.Distance(location) < radius
                select player;
        }


        // 24Feb2013-08:11UTC chinajade
        private IEnumerable<WoWUnit> FindUnitsFromIds(params int[] unitIds)
        {
            ContractRequires(unitIds != null, () => "unitIds argument may not be null");

            return
                from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
                where
                    unit.IsValid
                    && unit.IsAlive
                    && unitIds.Contains((int)unit.Entry)
                    && (unit.TappedByAllThreatLists || !unit.TaggedByOther)
                select unit;
        }


        private string GetMobNameFromId(int wowUnitId)
        {
            WoWUnit wowUnit = FindUnitsFromIds(wowUnitId).FirstOrDefault();

            return (wowUnit != null)
                ? wowUnit.Name
                : string.Format("MobId({0})", wowUnitId);
        }


        // 24Feb2013-08:11UTC chinajade
        private bool IsQuestObjectiveComplete(int questId, int objectiveId)
        {
            if (Me.QuestLog.GetQuestById((uint)questId) == null)
                { return false; }

            int questLogIndex = Lua.GetReturnVal<int>(string.Format("return GetQuestLogIndexByID({0})", questId), 0);

            return
                Lua.GetReturnVal<bool>(string.Format("return GetQuestLogLeaderBoard({0},{1})", objectiveId, questLogIndex), 2);
        }


        // 24Feb2013-08:11UTC chinajade
        private bool IsViable(WoWUnit wowUnit)
        {
            return
                (wowUnit != null)
                && wowUnit.IsValid
                && wowUnit.IsAlive
                && !Blacklist.Contains(wowUnit, BlacklistFlags.Combat);
        }


        /// <summary>
        /// This behavior quits attacking the mob, once the mob is targeting us.
        /// </summary>
        // 24Feb2013-08:11UTC chinajade
        private Composite UtilityBehavior_GetMobsAttention(WoWUnitDelegate selectedTargetDelegate)
        {

            return new PrioritySelector(targetContext => selectedTargetDelegate(targetContext),
                new Decorator(targetContext => IsViable((WoWUnit)targetContext),
                    new PrioritySelector(
                        new Decorator(targetContext => !((((WoWUnit)targetContext).CurrentTarget == Me)
                                                        || (Me.GotAlivePet && ((WoWUnit)targetContext).CurrentTarget == Me.Pet)),
                            new PrioritySelector(
                                new Action(targetContext =>
                                {
                                    LogMessage("info", "Getting attention of {0}", ((WoWUnit)targetContext).Name);
                                    return RunStatus.Failure;
                                }),
                                UtilityBehavior_SpankMob(selectedTargetDelegate)))
                    )));
        }


        private Composite UtilityBehavior_InteractWithMob(WoWUnitDelegate unitToInteract)
        {
            return new PrioritySelector(interactUnitContext => unitToInteract(interactUnitContext),
                new Decorator(interactUnitContext => IsViable((WoWUnit)interactUnitContext),
                    new PrioritySelector(
                        // Show user which unit we're going after...
                        new Decorator(interactUnitContext => Me.CurrentTarget != (WoWUnit)interactUnitContext,
                            new Action(interactUnitContext => { ((WoWUnit)interactUnitContext).Target(); })),

                        // If not within interact range, move closer...
                        new Decorator(interactUnitContext => !((WoWUnit)interactUnitContext).WithinInteractRange,
                            new Action(interactUnitContext =>
                            {
                                LogDeveloperInfo("Moving to interact with {0}", ((WoWUnit)interactUnitContext).Name);
                                Navigator.MoveTo(((WoWUnit)interactUnitContext).Location);
                            })),

                        new Decorator(interactUnitContext => Me.IsMoving,
                            new Action(interactUnitContext => { WoWMovement.MoveStop(); })),
                        new Decorator(interactUnitContext => !Me.IsFacing((WoWUnit)interactUnitContext),
                            new Action(interactUnitContext => { Me.SetFacing((WoWUnit)interactUnitContext); })),

                        // Blindly interact...
                        // Ideally, we would blacklist the unit if the interact failed.  However, the HB API
                        // provides no CanInteract() method (or equivalent) to make this determination.
                        new Action(interactUnitContext =>
                        {
                            LogDeveloperInfo("Interacting with {0}", ((WoWUnit)interactUnitContext).Name);
                            ((WoWUnit)interactUnitContext).Interact();
                            return RunStatus.Failure;
                        }),
                        new Wait(TimeSpan.FromMilliseconds(1000), context => false, new ActionAlwaysSucceed())
                    )));
        }


        private Composite UtilityBehavior_MoveTo(LocationRetriever locationRetriever,
                                                    MessageDelegate locationNameDelegate,
                                                    RangeDelegate precisionDelegate = null)
        {
            ContractRequires(locationRetriever != null, () => "locationRetriever may not be null");
            ContractRequires(locationNameDelegate != null, () => "locationNameDelegate may not be null");
            precisionDelegate = precisionDelegate ?? (context => Navigator.PathPrecision);

            return new PrioritySelector(locationContext => locationRetriever(),
                new Decorator(locationContext => !Me.Mounted
                                                    && Mount.CanMount()
                                                    && Mount.ShouldMount((WoWPoint)locationContext),
                    new Action(locationContext => { Mount.MountUp(() => (WoWPoint)locationContext); })),

                new Decorator(locationContext => (Me.Location.Distance((WoWPoint)locationContext) > precisionDelegate(locationContext)),
                    new Sequence(
                        new Action(context =>
                        {
                            WoWPoint destination = locationRetriever();
                            string locationName = locationNameDelegate(context) ?? destination.ToString();

                            TreeRoot.StatusText = "Moving to " + locationName;

                            if (!Me.IsSwimming && Navigator.CanNavigateFully(Me.Location, destination))
                                { Navigator.MoveTo(destination); }
                            else
                                { WoWMovement.ClickToMove(destination); }
                        }),
                        new WaitContinue(Delay_WoWClientMovementThrottle, ret => false, new ActionAlwaysSucceed())
                    ))
                );
        }
        
        
        /// <summary>
        /// Unequivocally engages mob in combat.
        /// </summary>
        // 24Feb2013-08:11UTC chinajade
        private Composite UtilityBehavior_SpankMob(WoWUnitDelegate selectedTargetDelegate)
        {
            return new PrioritySelector(targetContext => selectedTargetDelegate(targetContext),
                new Decorator(targetContext => IsViable((WoWUnit)targetContext),
                    new PrioritySelector(               
                        new Decorator(targetContext => ((WoWUnit)targetContext).Distance > CombatMaxEngagementRangeDistance,
                            new Action(targetContext => { Navigator.MoveTo(((WoWUnit)targetContext).Location); })),
                        new Decorator(targetContext => Me.CurrentTarget != (WoWUnit)targetContext,
                            new Action(targetContext =>
                            {
                                BotPoi.Current = new BotPoi((WoWUnit)targetContext, PoiType.Kill);
                                ((WoWUnit)targetContext).Target();
                            })),
                        new Decorator(targetContext => !((WoWUnit)targetContext).IsTargetingMeOrPet,
                            new PrioritySelector(
                                new Decorator(targetContext => RoutineManager.Current.CombatBehavior != null,
                                    RoutineManager.Current.CombatBehavior),
                                new Action(targetContext => { RoutineManager.Current.Combat(); })
                            ))
                    )));
        }
        #endregion // Behavior helpers


        #region Pet Helpers
        // Cut-n-paste any PetControl helper methods you need, here...
        #endregion


        #region Diagnostic Methods
        // These are needed by a number of the pre-supplied methods...
        public delegate bool    ContractPredicateDelegate();
        public delegate string  StringProviderDelegate();

        /// <summary>
        /// <para>This is an efficent poor man's mechanism for reporting contract violations in methods.</para>
        /// <para>If the provided ISCONTRACTOKAY evaluates to true, no action is taken.
        /// If ISCONTRACTOKAY is false, a diagnostic message--given by the STRINGPROVIDERDELEGATE--is emitted to the log, along with a stack trace.</para>
        /// <para>This emitted information can then be used to locate and repair the code misusing the interface.</para>
        /// <para>For convenience, this method returns the evaluation if ISCONTRACTOKAY.</para>
        /// <para>Notes:<list type="bullet">
        /// <item><description><para> * The interface is built in terms of a StringProviderDelegate,
        /// so we don't pay a performance penalty to build an error message that is not used
        /// when ISCONTRACTOKAY is true.</para></description></item>
        /// <item><description><para> * The .NET 4.0 Contract support is insufficient due to the way Buddy products
        /// dynamically compile parts of the project at run time.</para></description></item>
        /// </list></para>
        /// </summary>
        /// <param name="isContractOkay"></param>
        /// <param name="stringProviderDelegate"></param>
        /// <returns>the evaluation of the provided ISCONTRACTOKAY predicate delegate</returns>
        ///  30Jun2012-15:58UTC chinajade
        ///  NB: We could provide a second interface to ContractRequires() that is slightly more convenient for static string use.
        ///  But *please* don't!  If helps maintainers to not make mistakes if they see the use of this interface consistently
        ///  throughout the code.
        public bool ContractRequires(bool isContractOkay, StringProviderDelegate stringProviderDelegate)
        {
            if (!isContractOkay)
            {
                // TODO: (Future enhancement) Build a string representation of isContractOkay if stringProviderDelegate is null
                string      message = stringProviderDelegate() ?? "NO MESSAGE PROVIDED";
                StackTrace  trace   = new StackTrace(1);

                LogMessage("error", "[CONTRACT VIOLATION] {0}\nLocation:\n{1}",
                                        message, trace.ToString());
            }

            return isContractOkay;
        }


        /// <summary>
        /// <para>Returns the name of the method that calls this function. If SHOWDECLARINGTYPE is true,
        /// the scoped method name is returned; otherwise, the undecorated name is returned.</para>
        /// <para>This is useful when emitting log messages.</para>
        /// </summary>
        /// <para>Notes:<list type="bullet">
        /// <item><description><para> * This method uses reflection--making it relatively 'expensive' to call.
        /// Use it with caution.</para></description></item>
        /// </list></para>
        /// <returns></returns>
        ///  7Jul2012-20:26UTC chinajade
        public static string    GetMyMethodName(bool  showDeclaringType   = false)
        {
            var method  = (new StackTrace(1)).GetFrame(0).GetMethod();

            if (showDeclaringType)
                { return (method.DeclaringType + "." + method.Name); }

            return (method.Name);
        }


        /// <summary>
        /// <para>For DEBUG USE ONLY--don't use in production code! (Almost exclusively used by DebuggingTools methods.)</para>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public void LogDeveloperInfo(string message, params object[] args)
        {
            LogMessage("debug", message, args);
        }
        
        
        /// <summary>
        /// <para>Error situations occur when bad data/input is provided, and no corrective actions can be taken.</para>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public void LogError(string message, params object[] args)
        {
            LogMessage("error", message, args);
        }
        
        
        /// <summary>
        /// MaintenanceErrors occur as a result of incorrect code maintenance.  There is usually no corrective
        /// action a user can perform in the field for these types of errors.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        ///  30Jun2012-15:58UTC chinajade
        public void LogMaintenanceError(string message, params object[] args)
        {
            string          formattedMessage    = string.Format(message, args);
            StackTrace      trace               = new StackTrace(1);

            LogMessage("error", "[MAINTENANCE ERROR] {0}\nLocation:\n{1}", formattedMessage, trace.ToString());
        }


        /// <summary>
        /// <para>Used to notify of problems where corrective (fallback) actions are possible.</para>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public void LogWarning(string message, params object[] args)
        {
            LogMessage("warning", message, args);
        }
        #endregion
    }


    #region WoWPoint_Extensions
    public static class WoWPoint_Extensions
    {
        public static Random _random = new Random((int)DateTime.Now.Ticks);

        private static LocalPlayer Me { get { return (StyxWoW.Me); } }
        public const double TAU = (2 * Math.PI);    // See http://tauday.com/


        public static WoWPoint Add(this WoWPoint wowPoint,
                                    double x,
                                    double y,
                                    double z)
        {
            return (new WoWPoint((wowPoint.X + x), (wowPoint.Y + y), (wowPoint.Z + z)));
        }


        public static WoWPoint AddPolarXY(this WoWPoint wowPoint,
                                           double xyHeadingInRadians,
                                           double distance,
                                           double zModifier)
        {
            return (wowPoint.Add((Math.Cos(xyHeadingInRadians) * distance),
                                 (Math.Sin(xyHeadingInRadians) * distance),
                                 zModifier));
        }


        // Finds another point near the destination.  Useful when toon is 'waiting' for something
        // (e.g., boat, mob repops, etc). This allows multiple people running
        // the same profile to not stand on top of each other while waiting for
        // something.
        public static WoWPoint FanOutRandom(this WoWPoint location,
                                                double maxRadius)
        {
            const int CYLINDER_LINE_COUNT = 12;
            const int MAX_TRIES = 50;
            const double SAFE_DISTANCE_BUFFER = 1.75;

            WoWPoint candidateDestination = location;
            int tryCount;

            // Most of the time we'll find a viable spot in less than 2 tries...
            // However, if you're standing on a pier, or small platform a
            // viable alternative may take 10-15 tries--its all up to the
            // random number generator.
            for (tryCount = MAX_TRIES; tryCount > 0; --tryCount)
            {
                WoWPoint circlePoint;
                bool[] hitResults;
                WoWPoint[] hitPoints;
                int index;
                WorldLine[] traceLines = new WorldLine[CYLINDER_LINE_COUNT + 1];

                candidateDestination = location.AddPolarXY((TAU * _random.NextDouble()), (maxRadius * _random.NextDouble()), 0.0);

                // Build set of tracelines that can evaluate the candidate destination --
                // We build a cone of lines with the cone's base at the destination's 'feet',
                // and the cone's point at maxRadius over the destination's 'head'.  We also
                // include the cone 'normal' as the first entry.

                // 'Normal' vector
                index = 0;
                traceLines[index].Start = candidateDestination.Add(0.0, 0.0, maxRadius);
                traceLines[index].End = candidateDestination.Add(0.0, 0.0, -maxRadius);

                // Cylinder vectors
                for (double turnFraction = 0.0; turnFraction < TAU; turnFraction += (TAU / CYLINDER_LINE_COUNT))
                {
                    ++index;
                    circlePoint = candidateDestination.AddPolarXY(turnFraction, SAFE_DISTANCE_BUFFER, 0.0);
                    traceLines[index].Start = circlePoint.Add(0.0, 0.0, maxRadius);
                    traceLines[index].End = circlePoint.Add(0.0, 0.0, -maxRadius);
                }


                // Evaluate the cylinder...
                // The result for the 'normal' vector (first one) will be the location where the
                // destination meets the ground.  Before this MassTrace, only the candidateDestination's
                // X/Y values were valid.
                GameWorld.MassTraceLine(traceLines.ToArray(),
                                        GameWorld.CGWorldFrameHitFlags.HitTestGroundAndStructures,
                                        out hitResults,
                                        out hitPoints);

                candidateDestination = hitPoints[0];    // From 'normal', Destination with valid Z coordinate


                // Sanity check...
                // We don't want to be standing right on the edge of a drop-off (say we'e on
                // a plaform or pier).  If there is not solid ground all around us, we reject
                // the candidate.  Our test for validity is that the walking distance must
                // not be more than 20% greater than the straight-line distance to the point.
                int viableVectorCount = hitPoints.Sum(point => ((Me.Location.SurfacePathDistance(point) < (Me.Location.Distance(point) * 1.20))
                                                                      ? 1
                                                                      : 0));

                if (viableVectorCount < (CYLINDER_LINE_COUNT + 1))
                { continue; }

                // If new destination is 'too close' to our current position, try again...
                if (Me.Location.Distance(candidateDestination) <= SAFE_DISTANCE_BUFFER)
                { continue; }

                break;
            }

            // If we exhausted our tries, just go with simple destination --
            if (tryCount <= 0)
            { candidateDestination = location; }

            return (candidateDestination);
        }


        public static double SurfacePathDistance(this WoWPoint start,
                                                    WoWPoint destination)
        {
            WoWPoint[] groundPath = Navigator.GeneratePath(start, destination) ?? new WoWPoint[0];

            // We define an invalid path to be of 'infinite' length
            if (groundPath.Length <= 0)
            { return (double.MaxValue); }


            double pathDistance = start.Distance(groundPath[0]);

            for (int i = 0; i < (groundPath.Length - 1); ++i)
            { pathDistance += groundPath[i].Distance(groundPath[i + 1]); }

            return (pathDistance);
        }


        // Returns WoWPoint.Empty if unable to locate water's surface
        public static WoWPoint WaterSurface(this WoWPoint location)
        {
            WoWPoint hitLocation;
            bool hitResult;
            WoWPoint locationUpper = location.Add(0.0, 0.0, 2000.0);
            WoWPoint locationLower = location.Add(0.0, 0.0, -2000.0);

            hitResult = (GameWorld.TraceLine(locationUpper,
                                             locationLower,
                                             GameWorld.CGWorldFrameHitFlags.HitTestLiquid,
                                             out hitLocation)
                         || GameWorld.TraceLine(locationUpper,
                                                locationLower,
                                                GameWorld.CGWorldFrameHitFlags.HitTestLiquid2,
                                                out hitLocation));

            return (hitResult ? hitLocation : WoWPoint.Empty);
        }
    }
    #endregion
}