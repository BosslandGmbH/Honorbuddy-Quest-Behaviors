// Behavior originally contributed by Chinajade.
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
// 30690-KunLai-UnmaskingTheYaungol is a point-solution behavior.
// The behavior:
//  1) Moves to a safe start location
//  2) Wait for Kobai to respawn (if recently killed)
//  3) Wait for Kobai to move into "kill zone"
//      (We did it this way to make it safe for squishies/undergeared)
//  4) Pulls Kobai
//  5) Drops Blinding Rage Trap at Kobais feet and waits for Blinded By Rage aura
//  5) Steal Kobai's mask
//  6) Reprioritizes kill target to Malevolent Fury when it arrives
//  7) Profit!
// 
// THINGS TO KNOW:
//  * If the event fails for some reason, the event retries automatically.
//
//  * The toon will not defend itself while being attacked until the mask has
//      been pilfered (i.e., the Malevolent Fury is on the battlefield).
//      This is required to prevent certain attacks from interfering with
//      the trap placement and mask pilfering (i.e., Shaman's "Feral Spirit").
//      There is a safety measure if the toon's health gets below 60%
//      while waiting to pilfer the mask, it will start defending itself.
//      If this happens, the event is automatically retried. 
//      "Not defending" also prevents failures if the class max level
//      is ever increased above 90 by Blizzard, or the toon is uber-geared.
//
// EXAMPLE:
//     <CustomBehavior File="30690-KunLai-UnmaskingTheYaungol" />
#endregion

#region Usings
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.QuestBehaviors.UnmaskingTheYaungol
{
    public class UnmaskingTheYaungol : CustomForcedBehavior
    {
        #region Consructor and Argument Processing
        public UnmaskingTheYaungol(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                QuestId = 30690; // http://wowhead.com/quest=30690
                QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = QuestInLogRequirement.InLog;

                WaitPoint = new WoWPoint(2086.154, 2019.164, 453.4554).FanOutRandom(4.0);
                AuraId_StealMask = 118985; // http://wowhead.com/spell=118985
                ItemId_BlindingRageTrap = 81741; // http://wowhead.com/item=81741, item in bags
                GameObjectId_BlindingRageTrap = 209349; // http://wowhead.com/object-209349, object once deployed (created by Me)
                MobId_Kobai = 61303; // http://wowhead.com/npc=61303
                MobId_MalevolentFury = 61333; // http://wowhead.com/npc=61333
                ToonHealthPercentSafetyLevel = 60;

                // For streamlining...
                // We don't want a bunch of adds when we pull Kobai--not only can they interfere with our task,
                // but they can make life difficult for squishies.  This value makes certain that Kobai
                // is clear of surrounding mobs before we pull him.
                KobaiSafePullAreaAnchor = new WoWPoint(2062.548, 2019.029, 452.4345); 
                KobaiSafePullAreaRadius = 20.0;
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it can be quickly
                // resolved.
                LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
                                    + "\nFROM HERE:\n"
                                    + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }


        // Variables for Attributes provided by caller

        public int QuestId { get; set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; set; }
        public QuestInLogRequirement QuestRequirementInLog { get; set; }

        public int AuraId_StealMask { get; private set; }
        public int GameObjectId_BlindingRageTrap { get; private set; }
        public int ItemId_BlindingRageTrap { get; private set; }
        public WoWPoint KobaiSafePullAreaAnchor { get; private set; }
        public double KobaiSafePullAreaRadius { get; private set; }
        public int MobId_Kobai { get; private set; }
        public int MobId_MalevolentFury { get; private set; }
        public double ToonHealthPercentSafetyLevel { get; private set; }
        public WoWPoint WaitPoint { get; private set; }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return "$Id$"; } }
        public override string SubversionRevision { get { return "$Rev$"; } }
        #endregion


        #region Private and Convenience variables
        private class BattlefieldContext
        {
            public BattlefieldContext(int itemId_BlindingRageTrap, int objectId_BlindingRageTrap)
            {
                BlindingRageTrap = StyxWoW.Me.BagItems.FirstOrDefault(i => (int)i.Entry == itemId_BlindingRageTrap);
                _objectId_DeployedBlindingRageTrap = objectId_BlindingRageTrap;
            }

            public BattlefieldContext Update(int mobId_Kobai, int mobId_MalevolentFury)
            {
                Kobai = FindUnitsFromId(mobId_Kobai).FirstOrDefault();
                MalevolentFury = FindUnitsFromId(mobId_MalevolentFury).FirstOrDefault();
                DeployedBlindingRageTrap = ObjectManager.GetObjectsOfType<WoWGameObject>()
                                            .FirstOrDefault(o => (o.Entry == _objectId_DeployedBlindingRageTrap)
                                                                && (o.CreatedByGuid == StyxWoW.Me.Guid));
                return (this);
            }

            public WoWItem BlindingRageTrap { get; private set; }
            public WoWUnit Kobai { get; private set; }
            public WoWUnit MalevolentFury { get; private set; }
            public WoWGameObject DeployedBlindingRageTrap { get; private set; }

            private int _objectId_DeployedBlindingRageTrap;

            private IEnumerable<WoWUnit> FindUnitsFromId(int unitId)
            {
                return
                    from unit in ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                    where (unit.Entry == unitId) && unit.IsAlive
                            && (unit.TaggedByMe || unit.TappedByAllThreatLists || !unit.TaggedByOther)
                    select unit;
            }
        }

        private LocalPlayer Me { get { return StyxWoW.Me; } }

        private Composite _behaviorTreeHook_Combat = null;
        private Composite _behaviorTreeHook_Main = null;
        private BattlefieldContext _combatContext = null;
        private ConfigMemento _configMemento = null;
        private bool _isBehaviorDone = false;
        private bool _isDisposed = false;
        #endregion


        #region Destructor, Dispose, and cleanup
        ~UnmaskingTheYaungol()
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
                if (_behaviorTreeHook_Combat != null)
                {
                    TreeHooks.Instance.RemoveHook("Combat_Main", _behaviorTreeHook_Combat);
                    _behaviorTreeHook_Combat = null;
                }

                // NB: we don't unhook _behaviorTreeHook_Main
                // This was installed when HB created the behavior, and its up to HB to unhook it

                if (_configMemento != null)
                {
                    _configMemento.Dispose();
                    _configMemento = null;
                }

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

            // If the needed item is not in my inventory, report problem...
            if (!Me.BagItems.Any(i => ItemId_BlindingRageTrap == (int)i.Entry))
            {
                LogMessage("error", "The behavior requires \"Blind Rage Trap\"(ItemId: {0}) to be in our bags; however, it cannot be located)",
                    ItemId_BlindingRageTrap);
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
                
                TreeRoot.GoalText = string.Format(
                    "{0}: \"{1}\"\nLooting and Harvesting are disabled while behavior in progress",
                    this.GetType().Name,
                    ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress (no associated quest)"));

                _combatContext = new BattlefieldContext(ItemId_BlindingRageTrap, GameObjectId_BlindingRageTrap);

                _behaviorTreeHook_Combat = CreateCombatBehavior();
                TreeHooks.Instance.InsertHook("Combat_Main", 0, _behaviorTreeHook_Combat);
            }
        }
        #endregion


        #region Main Behavior
        private Composite CreateCombatBehavior()
        {
            return new PrioritySelector(context => _combatContext.Update(MobId_Kobai, MobId_MalevolentFury),
                new Decorator(context => Me.Combat,
                    new PrioritySelector(
                        // If a Malevolent Fury on battlefield, switch to kill it first...
                        new Decorator(context => _combatContext.MalevolentFury != null,
                            new PrioritySelector(
                                new Action(context => { LogMessage("info", "Fighting Malevolent Fury"); return RunStatus.Failure; }),
                                new Decorator(context => (Me.CurrentTarget != _combatContext.MalevolentFury),
                                    new Action(context =>
                                    {
                                        BotPoi.Current = new BotPoi(_combatContext.MalevolentFury, PoiType.Kill);
                                        _combatContext.MalevolentFury.Target();
                                        return RunStatus.Failure;
                                    })),
                                new Decorator(context => _combatContext.MalevolentFury.Distance > Me.CombatReach,
                                    new Action(context => { Navigator.MoveTo(_combatContext.MalevolentFury.Location); })),
                                new Decorator(context => RoutineManager.Current.CombatBehavior != null,
                                    RoutineManager.Current.CombatBehavior),
                                new Action(preferredTargetContext =>
                                {
                                    RoutineManager.Current.Combat();
                                    return RunStatus.Failure;
                                })
                            )),

                        // If the Blind Rage Trap is not on cooldown, move right next to Kobai and use it...
                        // NB: We don't want to drop the trap unless we're pounding on Kobai
                        new Decorator(context => (_combatContext.Kobai != null)
                                                    && (Me.CurrentTarget == _combatContext.Kobai)
                                                    && (_combatContext.BlindingRageTrap != null)
                                                    && (_combatContext.BlindingRageTrap.CooldownTimeLeft <= TimeSpan.Zero),
                            new PrioritySelector(
                                new Action(context => { LogMessage("info", "Using Blinding Rage Trap"); return RunStatus.Failure; }),
                                new Decorator(context => _combatContext.Kobai.Distance > Me.CombatReach,
                                    new Action(context => { Navigator.MoveTo(_combatContext.Kobai.Location); })),
                                new Decorator(context => Me.IsMoving,
                                    new Action(context => { WoWMovement.MoveStop(); })),
                                new Decorator(context => !Me.IsSafelyFacing(_combatContext.Kobai),
                                    new Action(context => { _combatContext.Kobai.Face(); })),
                                new Wait(TimeSpan.FromMilliseconds(250), context => false, new ActionAlwaysSucceed()),
                                new Action(context => { _combatContext.BlindingRageTrap.Use(); }),
                                new Wait(TimeSpan.FromMilliseconds(1000), context => false, new ActionAlwaysSucceed())
                            )),

                        // "Steal Mask" aura...
                        // If Kobai is blinded by rage, and the Malevolent Fury is not on the battlefield,
                        // move right next to Kobai, and steal the mask...
                        // NB: We only want to cause one Malevolet Fury to spawn.  If we click multiple times
                        // then we get more.  So, only click if Fury is not already up.
                        new Decorator(context => (_combatContext.Kobai != null)
                                                && Me.HasAura(AuraId_StealMask)
                                                && (_combatContext.MalevolentFury == null),
                            new PrioritySelector(
                                new Action(context => { LogMessage("info", "Pilfering Mask"); return RunStatus.Failure; }),
                                new Decorator(context => _combatContext.Kobai.Distance > Me.CombatReach,
                                    new Action(context => { Navigator.MoveTo(_combatContext.Kobai.Location); })),
                                new Decorator(context => (Me.CurrentTarget != _combatContext.Kobai),
                                    new Action(context => { _combatContext.Kobai.Target(); })),
                                new Decorator(context => _combatContext.MalevolentFury == null,
                                    new Action(context => { Lua.DoString("RunMacroText('/click ExtraActionButton1')"); })),
                                new Wait(TimeSpan.FromMilliseconds(1000), context => false, new ActionAlwaysSucceed())
                            )),

                        // Disallow combat until the trap Malevolent Fury shows up...
                        // NB: We *must* disable combat while the trap is being placed, and the mask pilfered.
                        // Otherwise, the attacks of certain classes will interfere with the trap placement
                        // and pilfering of the mask. A Shaman's "Feral Spirit" is one such example.
                        new Decorator(context => (_combatContext.MalevolentFury == null)
                                                    && /*safety measure*/(Me.HealthPercent > ToonHealthPercentSafetyLevel),
                            new ActionAlwaysSucceed())
                    )),

                // If we're not in combat, but have found Kobai, move to engage him...
                new Decorator(context => !Me.Combat && (_combatContext.Kobai != null),
                    new PrioritySelector(
                        // If Kobai is not in kill zone...
                        new Decorator(context => _combatContext.Kobai.Location.Distance(KobaiSafePullAreaAnchor) > KobaiSafePullAreaRadius,
                            new PrioritySelector(
                                UtilityBehavior_MoveToStartPosition(),

                                // Wait for Kobai to arrive...
                                new Action(context =>
                                {
                                    LogMessage("info", "Waiting for Kobai to move into kill zone (dist: {0:F1})",
                                        Math.Max(_combatContext.Kobai.Location.Distance(KobaiSafePullAreaAnchor) - KobaiSafePullAreaRadius, 0.0));
                                    return RunStatus.Failure;
                                }),
                                new Wait(TimeSpan.FromSeconds(5), context => false, new ActionAlwaysSucceed())
                            )),

                        // Kobai in kill zone, pull him...
                        new Decorator(context => _combatContext.Kobai.Location.Distance(KobaiSafePullAreaAnchor) <= KobaiSafePullAreaRadius,
                            new PrioritySelector(
                                new Action(context => { LogMessage("info", "Engaging Kobai"); return RunStatus.Failure; }),
                                new Decorator(context => Me.Mounted,
                                    new Action(context => { Mount.Dismount(); })),
                                new Decorator(context => (Me.CurrentTarget != _combatContext.Kobai),
                                    new Action(context =>
                                    {
                                        BotPoi.Current = new BotPoi(_combatContext.Kobai, PoiType.Kill);
                                        _combatContext.Kobai.Target();
                                        return RunStatus.Failure;
                                    })),
                                new Decorator(context => _combatContext.Kobai.Distance > CharacterSettings.Instance.PullDistance,
                                    new Action(context => { Navigator.MoveTo(_combatContext.Kobai.Location); }))
                            ))
                    )),

                // Can't find Kobai--must've just been killed--wait for repop...
                new Decorator(context => !Me.Combat && (_combatContext.Kobai == null)
                                        && (Me.Location.Distance(WaitPoint) <= Navigator.PathPrecision),
                    new Sequence(
                        new Action(context => { LogMessage("info", "Waiting for Kobai to respawn"); }),
                        new Wait(TimeSpan.FromSeconds(5), context => false, new ActionAlwaysSucceed())
                    ))

            );
        }


        private Composite CreateMainBehavior()
        {
            return new PrioritySelector(
                // If quest is done, behavior is done...
                new Decorator(context => !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete),
                    new Action(context => { LogMessage("info", "Finished"); _isBehaviorDone = true; })),

                // Move to start position, if needed...
                UtilityBehavior_MoveToStartPosition()
            );

        }
        #endregion


        #region Helpers
        private Composite UtilityBehavior_MoveToStartPosition()
        {
            // Move to start position, if needed...
            return new PrioritySelector(
                new Decorator(context => Me.Location.Distance(WaitPoint) > Navigator.PathPrecision,
                    new PrioritySelector(
                        new Decorator(context => !Me.Mounted && Mount.CanMount(),
                            new Action(context => { Mount.MountUp(() => WaitPoint); })),
                        new Action(context =>
                        {
                            LogMessage("info", "Moving to start position");
                            Navigator.MoveTo(WaitPoint);
                        })
                    )),

                // We're at start position, dismount...
                new Decorator(context => Me.Mounted,
                    new Action(context => { Mount.Dismount(); }))
            );
        }
        #endregion // Behavior helpers
    }


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
}

