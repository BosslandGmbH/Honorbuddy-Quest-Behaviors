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
using System.Linq;
using System.Numerics;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.GaspingForBreath
{
    [CustomBehaviorFileName(@"SpecificQuests\14395-Gilneas-GaspingForBreath")]
    public partial class GaspingForBreath : CustomForcedBehavior
    {
        #region Consructor and Argument Processing
        public GaspingForBreath(Dictionary<string, string> args)
            : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            try
            {
                // Quest handling...
                QuestId = 14395; // http://wowhead.com/quest=14395
                QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = QuestInLogRequirement.InLog;

                AuraId_Drowning = 68730; // http://wowhead.com/spell=68730
                AuraId_RescueDrowningWatchman = 68735; // http://wowhead.com/spell=68735
                MobId_DrowningWatchman = 36440; // http://wowhead.com/npc=36440

                // Tunables...
                CombatMaxEngagementRangeDistance = 23.0;
                NonCompeteDistance = 25.0;
                PositionToMakeLandfall = new Vector3(-1897.101f, 2520.762f, 1.735498f).FanOutRandom(5.0);

                Blackspots = new List<Blackspot>()
                {
                    // empty, for now
                };

                IgnoreDrowningWatchmenInTheseAreas = new List<Blackspot>()
                {
                    new Blackspot(new Vector3(-1916.865f, 2559.767f, -14.21182f), 5.0f, 1.0f),
                    new Blackspot(new Vector3(-1931.899f, 2562.926f, -19.15102f), 5.0f, 1.0f)
                };

                // A collection of paths that will get us from shore near a Watchman in the water...
                // We need these paths because there are mesh issues in this area/phase, and there is
                // much flotsam and jetsam in the water.
                // NB: Paths are directional--the first point should be on shore, and the last point
                // should be somewhere in the water.  Our algorithm will reverse the path when necessary.
                Paths = new List<List<Vector3>>()
                {
					// Path straight out
					new List<Vector3>()
                    {
                        new Vector3(-1898.565f, 2526.652f, 1.035018f),
                        new Vector3(-1905.135f, 2532.711f, -1.765509f),
                        new Vector3(-1920.694f, 2534.667f, -1.693366f),
                        new Vector3(-1936.677f, 2536.928f, -1.693366f),
                        new Vector3(-1951.854f, 2541.426f, -1.693366f),
                        new Vector3(-1974.511f, 2548.637f, -1.693366f),
                        new Vector3(-2001.689f, 2551.984f, -1.655865f),
                        new Vector3(-2022.086f, 2549.928f, -1.655865f),
                        new Vector3(-2042.27f, 2546.034f, -1.655865f),
                        new Vector3(-2059.579f, 2542.695f, -1.655865f),
                        new Vector3(-2074.083f, 2539.897f, -1.655865f),
                        new Vector3(-2086.656f, 2535.151f, -1.655865f),
                        new Vector3(-2100.172f, 2529.053f, -1.655865f),
                        new Vector3(-2120.214f, 2520.011f, -1.655865f)
                    },

					// Path to left
					new List<Vector3>()
                    {
                        new Vector3(-1917.676f, 2512.804f, 0.9624463f),
                        new Vector3(-1921.708f, 2518.946f, -1.56824f),
                        new Vector3(-1933.636f, 2519.966f, -1.708278f),
                        new Vector3(-1950.844f, 2535.389f, -1.633051f),
                        new Vector3(-1976.363f, 2520.421f, -1.633051f),
                        new Vector3(-1983.294f, 2512.472f, -1.633051f),
                        new Vector3(-1967.469f, 2497.509f, -1.633051f)
                    },

					// Path to right
					new List<Vector3>()
                    {
                        new Vector3(-1889.763f, 2523.739f, 1.452872f),
                        new Vector3(-1886.251f, 2533.122f, 1.710165f),
                        new Vector3(-1894.029f, 2546.086f, 1.341531f),
                        new Vector3(-1887.969f, 2560.070f, 1.207472f),
                        new Vector3(-1885.753f, 2571.156f, 1.337856f),
                        new Vector3(-1891.537f, 2574.381f, 1.792468f),
                        new Vector3(-1905.131f, 2587.235f, -1.833358f),
                        new Vector3(-1918.267f, 2604.448f, -1.675449f),
                        new Vector3(-1931.461f, 2614.436f, -1.675449f),
                        new Vector3(-1943.776f, 2623.013f, -1.675449f),
                        new Vector3(-1955.846f, 2631.421f, -1.675449f),
                        new Vector3(-1968.597f, 2638.606f, -1.675449f),
                        new Vector3(-1980.798f, 2646.535f, -1.675449f),
                        new Vector3(-1992.115f, 2650.767f, -1.675449f),
                        new Vector3(-2003.118f, 2665.267f, -1.675449f),
                        new Vector3(-2008.434f, 2681.922f, -1.675449f),
                    }
                };

                // Semantic coherency / covariant dependency checks --
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

        // DON'T EDIT THIS--it is auto-populated by Git
        public override string VersionId => QuestBehaviorBase.GitIdToVersionId("$Id$");


        // Variables for Attributes provided by caller
        public double CombatMaxEngagementRangeDistance { get; private set; }
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }

        public IEnumerable<Blackspot> IgnoreDrowningWatchmenInTheseAreas { get; private set; }
        public int AuraId_Drowning { get; private set; }
        public int AuraId_RescueDrowningWatchman { get; private set; }
        public IEnumerable<Blackspot> Blackspots { get; private set; }
        public int MobId_DrowningWatchman { get; private set; }
        public double NonCompeteDistance { get; private set; }
        public List<List<Vector3>> Paths { get; private set; }
        public Vector3 PositionToMakeLandfall { get; private set; }


        #endregion


        #region Private and Convenience variables
        public delegate Vector3 LocationDelegate(object context);
        public delegate string MessageDelegate(object context);
        public delegate double RangeDelegate(object context);
        public delegate string StringDelegate(object context);
        public delegate WoWUnit WoWUnitDelegate(object context);


        private enum StateType_MainBehavior
        {
            DroppingOffVictim,  // Initial state
            PathingOutToVictim,
            Rescuing,
            PathingIntoShore,
        };


        private Queue<Vector3> _currentPath = null;
        private readonly TimeSpan _delay_WoWClientMovementThrottle = TimeSpan.FromMilliseconds(100);
        private LocalPlayer Me { get { return StyxWoW.Me; } }
        private WoWUnit SelectedTarget { get; set; }
        private StateType_MainBehavior State_MainBehavior
        {
            get { return _state_MainBehavior; }
            set
            {
                // For DEBUGGING...
                //if (_state_MainBehavior != value)
                //    { QBCLog.Info("State_MainBehavior: {0}", value); }

                _state_MainBehavior = value;
            }
        }

        private Composite _behaviorTreeHook_CombatMain = null;
        private Composite _behaviorTreeHook_CombatOnly = null;
        private Composite _behaviorTreeHook_DeathMain = null;
        private Composite _behaviorTreeHook_Main = null;
        private ConfigMemento _configMemento = null;
        private bool _isBehaviorDone = false;
        private StateType_MainBehavior _state_MainBehavior;
        #endregion


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _behaviorTreeHook_Main ?? (_behaviorTreeHook_Main = CreateMainBehavior());
        }


        public override void OnFinished()
        {
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

            if (_configMemento != null)
            {
                _configMemento.Dispose();
                _configMemento = null;
            }

            BlackspotManager.RemoveBlackspots(Blackspots);
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
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
                QBCLog.Error("This behavior has been associated with QuestId({0}), but the quest is not in our log", QuestId);
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
                _configMemento = new ConfigMemento();

                // Disable any settings that may interfere with the escort --
                // When we escort, we don't want to be distracted by other things.
                // NOTE: these settings are restored to their normal values when the behavior completes
                // or the bot is stopped.
                CharacterSettings.Instance.HarvestHerbs = false;
                CharacterSettings.Instance.HarvestMinerals = false;
                CharacterSettings.Instance.LootChests = false;
                CharacterSettings.Instance.NinjaSkin = false;
                CharacterSettings.Instance.SkinMobs = false;
                // CharacterSettings.Instance.PullDistance = 1;    // don't pull anything unless we absolutely must

                BlackspotManager.AddBlackspots(Blackspots);

                State_MainBehavior = StateType_MainBehavior.DroppingOffVictim;

                _behaviorTreeHook_CombatMain = CreateBehavior_CombatMain();
                TreeHooks.Instance.InsertHook("Combat_Main", 0, _behaviorTreeHook_CombatMain);
                _behaviorTreeHook_CombatOnly = CreateBehavior_CombatOnly();
                TreeHooks.Instance.InsertHook("Combat_Only", 0, _behaviorTreeHook_CombatOnly);
                _behaviorTreeHook_DeathMain = CreateBehavior_DeathMain();
                TreeHooks.Instance.InsertHook("Death_Main", 0, _behaviorTreeHook_DeathMain);

                this.UpdateGoalText(QuestId);
            }
        }
        #endregion


        #region Main Behavior
        private Composite CreateBehavior_CombatMain()
        {
            return new PrioritySelector(
                );
        }


        private Composite CreateBehavior_CombatOnly()
        {
            return new PrioritySelector(
                // If we got in Combat, force a path recalculation when done...
                new Action(context =>
                {
                    _currentPath = null;
                    return RunStatus.Failure;
                })
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
                        QBCLog.Info("Finished");
                    })),


                // Stateful Operation:
                new Switch<StateType_MainBehavior>(context => State_MainBehavior,
            #region State: DEFAULT
                    new Action(context =>   // default case
                    {
                        QBCLog.MaintenanceError("StateType_MainBehavior({0}) is unhandled", State_MainBehavior);
                        TreeRoot.Stop();
                        _isBehaviorDone = true;
                    }),
            #endregion


            #region State: Dropping Off Victim
                    new SwitchArgument<StateType_MainBehavior>(StateType_MainBehavior.DroppingOffVictim,
                        new PrioritySelector(
                            // If Watchman dropped off, go get another...
                            new Decorator(context => !Me.HasAura(AuraId_RescueDrowningWatchman),
                                new Action(context =>
                                {
                                    WoWMovement.MoveStop();
                                    _currentPath = null;
                                    SelectedTarget = null;
                                    State_MainBehavior = StateType_MainBehavior.PathingOutToVictim;
                                })),

                            // Move to drop off spot...
                            new Decorator(context => !Navigator.AtLocation(PositionToMakeLandfall),
                                new ActionRunCoroutine(
                                    context => UtilityCoroutine.MoveTo(
                                        PositionToMakeLandfall,
                                        "back to shore")))
                        )),
            #endregion


            #region State: Pathing Out to Victim
                    new SwitchArgument<StateType_MainBehavior>(StateType_MainBehavior.PathingOutToVictim,
                        new PrioritySelector(
                            // If our selected target is no good, find another...
                            new Decorator(context => !IsViableDrowningWatchman(SelectedTarget),
                                new Action(context =>
                                {
                                    QBCLog.Info("Finding new Drowning Watchman to save");
                                    _currentPath = null;
                                    SelectedTarget = FindDrowningWatchman();
                                })),

                            // Show user which target we're after...
                            new Decorator(context => Me.CurrentTarget != SelectedTarget,
                                new Action(context => { SelectedTarget.Target(); })),

                            // If we don't have a path to victim, find one...
                            new Decorator(context => _currentPath == null,
                                new Action(context => { _currentPath = FindPath(Me.Location, SelectedTarget.Location); })),

                            // If path completely consumed, we're done...
                            new Decorator(context => _currentPath.Count() <= 0,
                                new Action(context => { State_MainBehavior = StateType_MainBehavior.Rescuing; })),

                            // If we've arrived at the current waypoint, dequeue it...
                            new Decorator(context => Navigator.AtLocation(_currentPath.Peek()),
                                new Action(context => { _currentPath.Dequeue(); })),

                            // Follow the prescribed path...
                            new ActionRunCoroutine(
                                context => UtilityCoroutine.MoveTo(
                                    _currentPath.Peek(),
                                    "out to Drowned Watcman"))
                        )),
            #endregion


            #region State: Rescuing
                    new SwitchArgument<StateType_MainBehavior>(StateType_MainBehavior.Rescuing,
                        new PrioritySelector(
                            // If we've got the Watchman, start heading in...
                            new Decorator(context => Me.HasAura(AuraId_RescueDrowningWatchman),
                                new Action(context =>
                                {
                                    _currentPath = null;
                                    State_MainBehavior = StateType_MainBehavior.PathingIntoShore;
                                })),

                            // If our selected target is no good, find another...
                            new Decorator(context => !IsViableDrowningWatchman(SelectedTarget),
                                new Action(context =>
                                {
                                    _currentPath = null;
                                    SelectedTarget = null;
                                    State_MainBehavior = StateType_MainBehavior.PathingOutToVictim;
                                })),

                            // Go get a fresh Drowning Watchman...
                            UtilityBehavior_InteractWithMob(context => SelectedTarget)
                        )),
            #endregion


            #region State: Pathing Into Shore
                    new SwitchArgument<StateType_MainBehavior>(StateType_MainBehavior.PathingIntoShore,
                        new PrioritySelector(
                            // If we don't have a path, find the correct one...
                            new Decorator(context => _currentPath == null,
                                new Action(context => { _currentPath = FindPath(Me.Location, PositionToMakeLandfall); })),

                            // If path completely consumed, we're done...
                            new Decorator(context => _currentPath.Count() <= 0,
                                new Action(context => { State_MainBehavior = StateType_MainBehavior.DroppingOffVictim; })),

                            // If we've lost the Watchman we rescued, go fetch another...
                            new Decorator(context => !Me.HasAura(AuraId_RescueDrowningWatchman),
                                new Action(context =>
                                {
                                    _currentPath = null;
                                    SelectedTarget = null;
                                    State_MainBehavior = StateType_MainBehavior.PathingOutToVictim;
                                })),

                            // If we've arrived at the current waypoint, dequeue it...
                            new Decorator(context => Navigator.AtLocation(_currentPath.Peek()),
                                new Action(context => { _currentPath.Dequeue(); })),

                            // Follow the prescribed path...
                            new ActionRunCoroutine(
                                context => UtilityCoroutine.MoveTo(
                                    _currentPath.Peek(),
                                    "in to drop off Drowned Watchman"))
                        ))
            #endregion
                ));
        }
        #endregion


        #region Helpers

        private double CalculatePathCost(Vector3 source, Vector3 destination, IList<Vector3> path)
        {
            double pathCost = 0.0;
            int pointCount = path.Count();

            for (int i = 0; i < pointCount - 1; ++i)
            { pathCost = path[i].Distance(path[i + 1]); }

            return source.Distance(path[0]) + pathCost + destination.Distance(path[pointCount - 1]);
        }


        private List<Vector3> ExtractPathSegment(Vector3 source, Vector3 destination, IList<Vector3> path)
        {
            List<Vector3> pathSegment = new List<Vector3>(path);

            Vector3 pointNearestSource = pathSegment.OrderBy(p => p.Distance(source)).FirstOrDefault();
            Vector3 pointNearestDestination = pathSegment.OrderBy(p => p.Distance(destination)).FirstOrDefault();

            if (pathSegment.IndexOf(pointNearestSource) > pathSegment.IndexOf(pointNearestDestination))
            { pathSegment.Reverse(); }

            while (pathSegment[0] != pointNearestSource)
            { pathSegment.RemoveAt(0); }

            while (pathSegment[pathSegment.Count() - 1] != pointNearestDestination)
            { pathSegment.RemoveAt(pathSegment.Count() - 1); }

            return pathSegment;
        }


        private WoWUnit FindDrowningWatchman()
        {
            // Blacklist any watchmen in troubling area...
            IEnumerable<WoWUnit> drowningWatchmenInNeedOfBlacklisting =
                from unit in FindUnitsFromIds(MobId_DrowningWatchman)
                where
                    IgnoreDrowningWatchmenInTheseAreas.Any(b => b.Location.Distance(unit.Location) <= b.Radius)
                    && !Blacklist.Contains(unit.Guid, BlacklistFlags.Combat)
                select unit;

            foreach (var drowningWatchman in drowningWatchmenInNeedOfBlacklisting)
            { Blacklist.Add(drowningWatchman.Guid, BlacklistFlags.Combat, TimeSpan.FromMinutes(5)); }

            // Choose the nearest viable watchman...
            return
               (from unit in FindUnitsFromIds(MobId_DrowningWatchman)
                where
                    IsViableDrowningWatchman(unit)
                    && unit.HasAura(AuraId_Drowning)
                orderby
                   (from path in Paths
                    let pathSegment = ExtractPathSegment(Me.Location, unit.Location, path)
                    let pathCost = CalculatePathCost(Me.Location, unit.Location, pathSegment)
                    orderby pathCost
                    select pathCost)
                    .FirstOrDefault()
                select unit)
                .FirstOrDefault();
        }


        private Queue<Vector3> FindPath(Vector3 source, Vector3 destination)
        {
            return new Queue<Vector3>(
               (from path in Paths
                let pathSegment = ExtractPathSegment(source, destination, path)
                orderby CalculatePathCost(source, destination, pathSegment)
                select pathSegment)
                .FirstOrDefault()
            );
        }


        // 25Feb2013-12:50UTC chinajade
        private IEnumerable<WoWPlayer> FindPlayersNearby(Vector3 location, double radius)
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
            Contract.Requires(unitIds != null, context => "unitIds argument may not be null");

            return
                from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
                where
                    unit.IsValid
                    && unit.IsAlive
                    && unitIds.Contains((int)unit.Entry)
                    && !unit.TaggedByOther
                select unit;
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


        private bool IsViableDrowningWatchman(WoWUnit wowUnit)
        {
            return IsViable(wowUnit)
                && (FindPlayersNearby(wowUnit.Location, NonCompeteDistance).Count() <= 0);
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
                            new ActionRunCoroutine(
                                interactUnitContext => UtilityCoroutine.MoveTo(
                                    ((WoWUnit)interactUnitContext).Location,
                                    "to interact with " + ((WoWUnit)interactUnitContext).SafeName))),

                        new Decorator(interactUnitContext => Me.IsMoving,
                            new Action(interactUnitContext => { WoWMovement.MoveStop(); })),
                        new Decorator(interactUnitContext => !Me.IsFacing((WoWUnit)interactUnitContext),
                            new Action(interactUnitContext => { Me.SetFacing((WoWUnit)interactUnitContext); })),

                        // Blindly interact...
                        // Ideally, we would blacklist the unit if the interact failed.  However, the HB API
                        // provides no CanInteract() method (or equivalent) to make this determination.
                        new Action(interactUnitContext =>
                        {
                            QBCLog.DeveloperInfo("Interacting with {0}", ((WoWUnit)interactUnitContext).SafeName);
                            ((WoWUnit)interactUnitContext).Interact();
                            return RunStatus.Failure;
                        }),
                        new Wait(TimeSpan.FromMilliseconds(1000), context => false, new ActionAlwaysSucceed())
                    )));
        }
        #endregion // Behavior helpers


        #region Pet Helpers
        // Cut-n-paste any PetControl helper methods you need, here...
        #endregion
    }
}