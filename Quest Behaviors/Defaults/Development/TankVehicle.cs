// TODO:
// * Target selection

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
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

using Bots.Quest;
using Bots.Quest.Objectives;
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


namespace Honorbuddy.QuestBehaviors.TankVehicle
{
    public class TankVehicle : CustomForcedBehavior
    {
        #region Consructor and Argument Processing
        public TankVehicle(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                MobIds_UnoccupiedVehicle = GetNumberedAttributesAsArray<int>("UnoccupiedVehicleId", 1, ConstrainAs.MobId, null);
                VehicleAcquisitionArea = GetAttributeAsNullable<WoWPoint>("VehicleAcquisitionArea", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;

                // Quest handling...
                QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;

                // Tunables...
                CombatMaxEngagementRangeDistance = GetAttributeAsNullable<double>("CombatMaxEngagementRangeDistance", false, new ConstrainTo.Domain<double>(1.0, 40.0), null) ?? 23.0;
                NonCompeteDistance = GetAttributeAsNullable<double>("NonCompeteDistance", false, new ConstrainTo.Domain<double>(1.0, 40.0), null) ?? 25.0;

                MobIds = new int[]
                {
                    37916,  // Orc Raider
                    37921,  // Orc War machine
                    37938,  // Orc Outrider
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
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }

        public double CombatMaxEngagementRangeDistance { get; private set; }
        public Queue<WoWPoint> HuntingGrounds { get; private set; }
        public int[] MobIds { get; private set; }
        public IEnumerable<int> MobIds_UnoccupiedVehicle { get; private set; }
        public double NonCompeteDistance { get; private set; }
        public WoWPoint VehicleAcquisitionArea { get; private set; }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return "$Id$"; } }
        public override string SubversionRevision { get { return "$Rev$"; } }
        #endregion


        #region Private and Convenience variables
        private enum StateType_MainBehavior
        {
            AcquiringVehicle,       // initial state
            Hunting,
        }

        public delegate WoWPoint LocationDelegate(object context);
        public delegate string StringDelegate(object context);
        public delegate double RangeDelegate(object context);
        public delegate WoWUnit WoWUnitDelegate(object context);

        private ArticulationLimitsType ArticulationLimits { get; set; }
        private IEnumerable<int> AuraIds_OccupiedVehicle = null;
        private readonly TimeSpan Delay_WoWClientMovementThrottle = TimeSpan.FromMilliseconds(100);
        private LocalPlayer Me { get { return StyxWoW.Me; } }
        private IEnumerable<QuestGoalType> QuestGoals { get; set; }
        private WoWUnit SelectedTarget { get; set; }
        private StateType_MainBehavior State_MainBehavior
        {
            get { return _state_MainBehavior; }
            set
            {
                // For DEBUGGING...
                if (_state_MainBehavior != value)
                    { LogInfo("State_MainBehavior: {0}", value); }
                _state_MainBehavior = value;
            }
        }
        private WoWUnit UnoccupiedVehicle { get; set; }
        private IEnumerable<VehicleAbility> VehicleAbilities { get; set; }
        private VehicleAbility VehicleSpeedBurstAbility { get; set; }

        private Composite _behaviorTreeHook_CombatMain = null;
        private Composite _behaviorTreeHook_CombatOnly = null;
        private Composite _behaviorTreeHook_DeathMain = null;
        private Composite _behaviorTreeHook_Main = null;
        private ConfigMemento _configMemento = null;
        private bool _isBehaviorDone = false;
        private bool _isDisposed = false;
        private StateType_MainBehavior _state_MainBehavior;
        #endregion


        #region Destructor, Dispose, and cleanup
        ~TankVehicle()
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
                LogError("This behavior has been associated with QuestId({0}), but the quest is not in our log", QuestId);
                IsAttributeProblem = true;
            }

            HuntingGrounds = new Queue<WoWPoint>(
                XmlUtil_ParseSubtree<HotspotType>(HotspotType.Create, "HuntingGrounds", "Hotspot")
                .Select(h => h.ToWoWPoint));
            QuestGoals = XmlUtil_ParseSubtree<QuestGoalType>(QuestGoalType.Create, "Objectives", "QuestObjective");

            if (HuntingGrounds.Count() <= 0)
            {
                LogError("No <HuntingGrounds> sub-element has been specified, and it is required");
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
                    "{0}: \"{1}\"",
                    this.GetType().Name,
                    ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress (no associated quest)"));

                AuraIds_OccupiedVehicle = GetOccupiedVehicleAuraIds();

                if (Me.InVehicle)
                {
                    LogInfo("VEHICLE SPELLS--");
                    foreach (var wowPetSpell in Me.PetSpells.Where(s => s.Spell != null))
                        { LogInfo(wowPetSpell.ToString_FullInfo()); }
                }

                _behaviorTreeHook_CombatMain = CreateBehavior_CombatMain();
                TreeHooks.Instance.InsertHook("Combat_Main", 0, _behaviorTreeHook_CombatMain);
                _behaviorTreeHook_CombatOnly = CreateBehavior_CombatOnly();
                TreeHooks.Instance.InsertHook("Combat_Only", 0, _behaviorTreeHook_CombatOnly);
                _behaviorTreeHook_DeathMain = CreateBehavior_DeathMain();
                TreeHooks.Instance.InsertHook("Death_Main", 0, _behaviorTreeHook_DeathMain);


                PlayerQuest quest2 = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                if (quest2 != null)
                {
                    foreach (var objective in quest.GetObjectives())
                    {
                        LogInfo(objective.ToString_FullInfo());
                    }
                }
            }
        }
        #endregion


        #region Main Behaviors
        private Composite CreateBehavior_CombatMain()
        {
            return new Decorator(context => !IsDone,
                new PrioritySelector(
                    // Update information we need for this BT visit...
                    new Action(context =>
                    {
                        SelectedTarget = FindBestTarget();
                        UnoccupiedVehicle = FindUnoccupiedVehicle(MobIds_UnoccupiedVehicle);

                        return RunStatus.Failure;
                    }),

                    new Switch<StateType_MainBehavior>(context => State_MainBehavior,
                        #region State: DEFAULT
                        new Action(context =>   // default case
                        {
                            LogMaintenanceError("BehaviorState({0}) is unhandled", State_MainBehavior);
                            TreeRoot.Stop();
                            _isBehaviorDone = true;
                        }),
                        #endregion


                        #region State: Acquiring Vehicle
                        // Find a vehicle to use...
                        new SwitchArgument<StateType_MainBehavior>(StateType_MainBehavior.AcquiringVehicle,
                            new PrioritySelector(
                                // If we acquired a vehicle, move to hunting grounds...
                                new Decorator(context => Me.InVehicle,
                                    new PrioritySelector(
                                        // Discover vehicle's abilities...
                                        new Decorator(context => VehicleAbilities == null,
                                            new Action(context =>
                                            {
                                                VehicleAbilities = DiscoverVehicleAbilities();

                                                VehicleSpeedBurstAbility = 
                                                   (from ability in VehicleAbilities
                                                    where ability.IsSpeedBurst
                                                    select ability)
                                                    .FirstOrDefault();
                                            })),

                                        new Decorator(context => ArticulationLimits == null,
                                            new Action(context =>
                                            {
                                                ArticulationLimits = DiscoverArticulationLimitsOfVehicleWeapon();
                                                LogInfo("{0}", ArticulationLimits.ToString());
                                            })),

                                        new Action(context =>
                                        {
                                            // Locate nearest waypoint in hunting ground to start...
                                            WoWPoint nearestWaypoint =
                                               (from point in HuntingGrounds
                                                orderby Me.Location.Distance(point)
                                                select point)
                                                .FirstOrDefault();

                                            while (HuntingGrounds.Peek() != nearestWaypoint)
                                            {
                                                WoWPoint currentWaypoint = HuntingGrounds.Dequeue();
                                                HuntingGrounds.Enqueue(currentWaypoint);
                                            }

                                            State_MainBehavior = StateType_MainBehavior.Hunting;
                                        })
                                    )),

                                // Move to and mount any free vehicle we've found...
                                UtilityBehavior_InteractWithMob(context => UnoccupiedVehicle),

                                // If no vehicle to be found, move to the Vehicle acquisition area...
                                new Decorator(context => UnoccupiedVehicle == null,
                                    UtilityBehavior_MoveTo(context => VehicleAcquisitionArea, context => "Vehicle acquisition area"))
                            )),
                        #endregion


                        #region State: Hunting
                        // Killing things...
                        new SwitchArgument<StateType_MainBehavior>(StateType_MainBehavior.Hunting,
                            new PrioritySelector(
                                // If we're no longer in vehicle, go fetch another...
                                new Decorator(context => !Me.InVehicle,
                                    new Action(context =>
                                    {
                                        ArticulationLimits = null;
                                        VehicleAbilities = null;
                                        State_MainBehavior = StateType_MainBehavior.AcquiringVehicle;
                                    })),

                                // If we're done with objectives, leave vehicle and we're done...
                                new Decorator(context => UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete),
                                    new Action(context =>
                                    {
                                        Lua.DoString("VehicleExit()");
                                        _isBehaviorDone = true;
                                    })),

                                // If targets in the area, have at them...
                                new Decorator(context => IsViable(SelectedTarget),
                                    new PrioritySelector(
                                        // Make certain were within the prescribed range for engagement...
                                        new Decorator(context => Me.Location.Distance(SelectedTarget.Location) > CombatMaxEngagementRangeDistance,
                                            UtilityBehavior_MoveTo(
                                                context => SelectedTarget.Location,
                                                context => string.Format("within range of '{0}'", SelectedTarget.Name))) //, // TODO

                                        //new Action(context => { AimAndFire(SelectedMob, TODO, TODO); }) // TODO
                                    )),

                                // Dequeue current waypoint if we've arrived...
                                new Decorator(context => Me.Location.Distance(HuntingGrounds.Peek()) <= Navigator.PathPrecision,
                                    new Action(context =>
                                    {
                                        // Rotate to the next waypoint
                                        WoWPoint currentWaypoint = HuntingGrounds.Dequeue();
                                        HuntingGrounds.Enqueue(currentWaypoint);
                                    })),

                                // Apply speed burst if we have one...
                                new Decorator(context => Me.IsMoving && (VehicleSpeedBurstAbility != null),
                                    new Action(context =>
                                    {
                                        //TODO:
                                        //if (!VehicleSpeedBurstAbility.IsAbilityReady())
                                        //    { VehicleSpeedBurstAbility.UseAbility(); }

                                        return RunStatus.Failure; // fall through
                                    })),

                                // Otherwise, move to next hotspot...
                                UtilityBehavior_MoveTo(context => HuntingGrounds.Peek(), context => "to next hunting ground waypoint")
                            ))
                        #endregion
                    )
            ));
        }


        private Composite CreateBehavior_CombatOnly()
        {
            return new PrioritySelector(
                // Prevent the Combat routine from running while we're in the vehicle...
                new Decorator(context => Me.InVehicle,
                    new ActionAlwaysSucceed())
                );
        }


        private Composite CreateBehavior_DeathMain()
        {
            return new PrioritySelector(
                // empty, for now
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
                        LogInfo("Finished");
                    }))
            );
        }
        #endregion


        #region Helpers
        public class ArticulationLimitsType
        {
            public ArticulationLimitsType(double up, double down, double left, double right)
            {
                Down = down;
                Left = left;
                Right = right;
                Up = up;
            }

            public bool IsWithinAzimuthArticulation(double angle)
            {
                // In WoW, larger Azimuths are "up"...
                return (angle >= Down) && (angle <= Up);
            }

            public bool IsWithinHeadingArticulation(double angle)
            {
                // In WoW, larger headings are "Left"..
                return (angle >= Right) && (angle <= Left);
            }

            public override string ToString()
            {
                return string.Format("<ArticulationLimitsType"
                    + " Up=\"{0}\" Down=\"{1}\" Left=\"{2}\" Right=\"{3}\"",
                    Up, Down, Left, Right);
            }

            public double Down { get; private set; }     // azimuth in Radians
            public double Left { get; private set; }     // relative heading in Radians
            public double Right { get; private set; }    // relative heading in Radians
            public double Up { get; private set; }       // azimuth in Radians
        };


        public class VehicleAbility
        {
            public VehicleAbility(CustomForcedBehavior behavior, WoWPetSpell wowPetSpell)
            {
                _behavior = behavior;
                _wowPetSpell = wowPetSpell;

                ActionBarIndex = wowPetSpell.ActionBarIndex +1;
                Spell = wowPetSpell.Spell;
_behavior.LogMessage("info", "{0}", Spell.ToString_FullInfo());
                if (Spell != null)
                {
                    IsMissile = Spell.SpellMissileId != 0;
                    IsSpeedBurst = Spell.SpellEffects.Any(e => _speedBoostAuras.Contains(e.AuraType));
                }

                LuaCommand_Cast = string.Format("CastPetAction({0})", ActionBarIndex);
                LuaCommand_Cooldown = string.Format("return GetPetActionCooldown({0});", ActionBarIndex);

                // Force MuzzleVelocity to be updated...
                if (IsMissile)
                    { UseAbility(); }
            }

            public int ActionBarIndex { get; private set; }  // 1-based
            public bool IsMissile { get; private set; }
            public bool IsSpeedBurst { get; private set; }
            public WoWSpell Spell { get; private set; }
            public double MuzzleVelocity { get; private set; }


            public bool IsAbilityReady()
            {
string LuaCommand_Spell = string.Format("return GetSpellCooldown({0});", Spell.Id);
IEnumerable<string> tmpResultSpell = Lua.GetReturnValues(LuaCommand_Spell);
string LuaCommand_PetAction = string.Format("return GetPetActionCooldown({0});", ActionBarIndex);
IEnumerable<string> tmpResultPetAction = Lua.GetReturnValues(LuaCommand_PetAction);
_behavior.LogMessage("error", "\nSPELL CD RESULTS({0}): \"{1}\"\nPETACTION CD RESULTS({2}): \"{3}\"",
    LuaCommand_Spell,
    string.Join("\", \"", tmpResultSpell),
    LuaCommand_PetAction,
    string.Join("\", \"", tmpResultPetAction));
                
                bool isOnCooldown1 = Lua.GetReturnVal<float>(LuaCommand_Cooldown, 0) <= 0.0f;
                bool isOnCooldown2 = _wowPetSpell.Cooldown;
                bool isOnCooldown3 = (Spell == null) ? false : (Spell.CooldownTimeLeft > TimeSpan.Zero);
                bool isOnCooldown4 = (Spell == null) ? false : Spell.Cooldown;
_behavior.LogMessage("info", "COOLDOWN: {0}/{1}/{2}/{3}", isOnCooldown1, isOnCooldown2, isOnCooldown3, isOnCooldown4);
return tmpStopwatch.ElapsedMilliseconds > 2000;
                return isOnCooldown3;
            }


            public override string ToString()
            {
                StringBuilder tmp = new StringBuilder();

                tmp.Append("<VehicleAbility");
                tmp.AppendFormat(" ActionBarIndex=\"{0}\"", ActionBarIndex);
                if (Spell != null)
                {
                    tmp.AppendFormat(" SpellName=\"{0}\"", Spell.Name);
                    tmp.AppendFormat(" SpellId=\"{0}\"", Spell.Id);
                }
                if (IsMissile)
                {
                    tmp.AppendFormat(" IsMissile=\"{0}\"", IsMissile);
                    tmp.AppendFormat(" MuzzleVelocity=\"{0:F1}\"", MuzzleVelocity);
                }
                if (IsSpeedBurst)
                    { tmp.AppendFormat(" IsSpeedBurst=\"{0}\"", IsSpeedBurst); }
                tmp.Append(" />");

                return tmp.ToString();
            }

private static Stopwatch tmpStopwatch = new Stopwatch();
            public void UseAbility()
            {
_behavior.LogMessage("info", "COOLDOWNS BEFORE CAST");
IsAbilityReady();
tmpStopwatch.Reset();
                Lua.DoString(LuaCommand_Cast);
_behavior.LogMessage("info", "COOLDOWNS AFTER CAST");
IsAbilityReady();
                MuzzleVelocity = UpdateMuzzleVelocity();
            }


            private double UpdateMuzzleVelocity()
            {
                double launchAngle = WoWMathHelper.NormalizeRadian(Lua.GetReturnVal<float>("return VehicleAimGetAngle()", 0));
                double muzzleVelocity = 0.0;

                if ((StyxWoW.Me.InVehicle) && (Spell != null) && (Spell.SpellMissileId > 0))
                {
                    IEnumerable<WoWMissile> firedMissileQuery =
                        from missile in WoWMissile.InFlightMissiles
                        where
                            (missile.CasterGuid == StyxWoW.Me.TransportGuid)
                            && (missile.SpellId == Spell.Id)
                        select missile;
                    WoWMissile launchedMissile = null;
                    Stopwatch missileTimer = new Stopwatch();

                    // Launch missile, and wait until launch is observed;
                    missileTimer.Start();

                    do
                    {
                        ObjectManager.Update();
                        launchedMissile = firedMissileQuery.FirstOrDefault();
                    } while ((launchedMissile == null) && (missileTimer.ElapsedMilliseconds < 1000));

                    // If we failed to see the missile, report error and move on...
                    if (launchedMissile == null)
                    {
                        _behavior.LogMessage("warning", "Muzzle Velocity not calculated--"
                            + "Unable to locate projectile launched by Vehicle Ability button #{0}",
                            ActionBarIndex);
                        return muzzleVelocity;
                    }

                    muzzleVelocity = CalculateMuzzleVelocity(launchedMissile, launchAngle);
                }

                return muzzleVelocity;
            }


            // Calculates muzzle (initial) velocity of a ballistic missile...
            // * Accounts for uneven terrain
            private double CalculateMuzzleVelocity(WoWMissile wowMissile, double launchAngle)
            {
                double muzzleVelocity = 0.0;

                if (wowMissile == null)
                    { return muzzleVelocity; }

                // Initial velocity calculation...
                // v0 = sqrt((R^2 * g) / (R * sin(2*theta)  +  2 * h * cos^2(theta)))
                // where, R = range, g = grav const, h = drop height, theta = launch angle
                double g = 32.1740; // feet/sec^2
                double R = wowMissile.FirePosition.Distance2D(wowMissile.ImpactPosition);
                double h = wowMissile.FirePosition.Z - wowMissile.ImpactPosition.Z;
                double sinTwoTheta = Math.Sin(2 * launchAngle);
                double cosTheta = Math.Cos(launchAngle);

                muzzleVelocity = Math.Sqrt(((R * R) * g) / (R * sinTwoTheta + 2 * h * (cosTheta * cosTheta)));

                return muzzleVelocity;
            }

            private string LuaCommand_Cast { get; set; }
            private string LuaCommand_Cooldown { get; set; }

            private CustomForcedBehavior _behavior;
            private WoWPetSpell _wowPetSpell;

            private static IEnumerable<WoWApplyAuraType> _speedBoostAuras = new List<WoWApplyAuraType>()
            {
                WoWApplyAuraType.ModFlightSpeedAlways,
                WoWApplyAuraType.ModFlightSpeedNotStack,
                WoWApplyAuraType.ModIncreaseFlightSpeed,
                WoWApplyAuraType.ModIncreaseMountedSpeed,
                WoWApplyAuraType.ModIncreaseSpeed,
                WoWApplyAuraType.ModIncreaseSwimSpeed,
                WoWApplyAuraType.ModMountedSpeedAlways,
                WoWApplyAuraType.ModMountedSpeedNotStack,
                WoWApplyAuraType.ModSpeedAlways,
                WoWApplyAuraType.ModSpeedFlight,
                WoWApplyAuraType.ModSpeedNotStack
            };
        }


        // NB: In WoW, larger headings are to left, and larger Azimuths are up
        private void AimAndFire(WoWUnit target, double muzzleVelocity, ArticulationLimitsType articulation)
        {
            //if (Me.InVehicle && (target != null))
            //{
            //    // Handle heading...
            //    double traveltime = target.Distance / (Projectile_FeetPerSecond * 3.0f /*feet to yards*/);
            //    WoWPoint targetLeadPoint = target.Location.RayCast(target.RenderFacing, (float)(target.MovementInfo.CurrentSpeed * traveltime));
            //    float neededHeading = WoWMathHelper.CalculateNeededFacing(Me.Location, targetLeadPoint);
            //    neededHeading = WoWMathHelper.NormalizeRadian(neededHeading);
            //    Me.SetFacing(neededHeading);

            //    // Handle Azimuth...
            //    // "Location" is measured at the feet of the toon.  We want to aim for the 'middle' of the toon's
            //    // height.
            //    double currentAzimuth = NormalizeAngleToPi(Lua.GetReturnVal<double>("return VehicleAimGetAngle();", 0));
            //    double? neededAzimuth = CalculateBallisticLaunchAngle(target, muzzleVelocity, articulation);
            //    if (neededAzimuth.HasValue)
            //    {
            //        LogInfo("Firing at {0} (dist: {1:F1})", target.Name, target.Distance);

            //        neededAzimuth = NormalizeAngleToPi(neededAzimuth.Value);

            //        // Execute fire...
            //        // NB: VehicleAimIncrement() handles negative values of 'increment' correctly...
            //        Lua.DoString("VehicleAimIncrement({0}); {1}", (neededAzimuth - currentAzimuth), "CastPetAction(1)" /*TODO*/);
            //    }

            //    else
            //        { LogInfo("No firing solution to {0} (dist: {1})", target.Name, target.Location.Distance(Me.Location)); }
            //}
        }


        public ArticulationLimitsType DiscoverArticulationLimitsOfVehicleWeapon()
        {
            if (!Me.InVehicle)
                { return null; }

            WoWObject faceListener = (Me.Transport != null) ? Me.Transport : Me;
            bool hasAngleControl = Lua.GetReturnVal<bool>("return IsVehicleAimAngleAdjustable();", 0);
            Stopwatch movementTimeout = new Stopwatch();
            double originalAzimuth = NormalizeAngleToPi(Lua.GetReturnVal<double>("return VehicleAimGetAngle();", 0));
            float originalHeading = Me.RenderFacing;

            double down = 0.0;
            double up = 0.0;

            // Discover Azimuth limits...
            if (hasAngleControl)
            {
                double desiredAzimuth = (Math.PI / 2);
                movementTimeout.Restart();
                do
                {
                    Lua.DoString("VehicleAimRequestAngle({0})", (Math.PI / 2));
                    up = NormalizeAngleToPi(Lua.GetReturnVal<double>("return VehicleAimGetAngle();", 0));
                } while ((up < desiredAzimuth) && (movementTimeout.ElapsedMilliseconds < 1000));

                desiredAzimuth = (-Math.PI / 2);
                movementTimeout.Restart();
                do
                {
                    Lua.DoString("VehicleAimRequestAngle({0})", -(Math.PI / 2));
                    down = NormalizeAngleToPi(Lua.GetReturnVal<double>("return VehicleAimGetAngle();", 0));
                } while ((down > desiredAzimuth) && (movementTimeout.ElapsedMilliseconds < 1000));

                Lua.DoString("VehicleAimRequestAngle({0})", originalAzimuth);
            }

            // Discover Heading limits...

            // We want to force turning in a particular direction (CCW) to determine 'left' limits...
            const float headingIncrement = (float)Math.PI / 16;
            float headingOffset;
            double left = 0.0;
            double right = 0.0;


            Me.SetFacing(originalHeading);
            for (headingOffset = 0.0f; headingOffset <= (Math.PI + headingIncrement/2); headingOffset += headingIncrement)
            {
                Me.SetFacing(WoWMathHelper.NormalizeRadian(originalHeading + headingOffset));
                left = WoWMathHelper.NormalizeRadian(faceListener.Rotation);
LogInfo("Left: {0:F2} (offset: {1:F2})", left, headingOffset);
            }

            Me.SetFacing(originalHeading);
            for (headingOffset = 0.0f; headingOffset <= (Math.PI + headingIncrement/2); headingOffset += headingIncrement)
            {
                Me.SetFacing(WoWMathHelper.NormalizeRadian(originalHeading - headingOffset));
                right = WoWMathHelper.NormalizeRadian(faceListener.Rotation);
LogInfo("Right: {0:F2} (offset: {1:F2})", right, headingOffset);
            }

            // Return to original heading...
            Me.SetFacing((float)originalHeading);

            return new ArticulationLimitsType(up, down, left, right);
        }


        public IEnumerable<VehicleAbility> DiscoverVehicleAbilities()
        {
            // If not in vehicle, no way to discover abilities...
            if (!Me.InVehicle)
                { return null; }

            var abilities = new List<VehicleAbility>();

            foreach (var wowPetSpell in Me.PetSpells)
            {
                if (wowPetSpell.Spell != null)
                {
                    var ability = new VehicleAbility(this, wowPetSpell);
                    abilities.Add(ability);
LogInfo("ABILITY: {0}", ability.ToString());
//LogInfo("SPELL: {0}", wowPetSpell.Spell.ToString_FullInfo());
                }
            }

            return abilities;
        }


        private WoWUnit FindBestTarget()
        {
            IEnumerable<int> targetMobIds =
                from questGoal in QuestGoals
                where !questGoal.IsGoalComplete(QuestId)
                from mobId in questGoal.MobIds
                select mobId;

            return
               (from target in FindUnitsFromIds(targetMobIds.ToArray())
                orderby Me.Location.Distance(target.Location)
                select target)
                .FirstOrDefault();
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
        private IEnumerable<WoWUnit> FindUnitsFromIds(IEnumerable<int> unitIds)
        {
            ContractRequires(unitIds != null, () => "unitIds argument may not be null");

            return
                from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
                where
                    IsViable(unit)
                    && unitIds.Contains((int)unit.Entry)
                    && (unit.TappedByAllThreatLists || !unit.TaggedByOther)
                select unit;
        }


        private WoWUnit FindUnoccupiedVehicle(IEnumerable<int> mobIds_UnoccupiedVehicle)
        {
            ContractRequires(mobIds_UnoccupiedVehicle != null,
                () => "mobIds_UnoccupiedVehicle argument may not be null");

            return
                (from vehicle in FindUnitsFromIds(mobIds_UnoccupiedVehicle)
                 where
                    !vehicle.Auras.Values.Any(aura => AuraIds_OccupiedVehicle.Contains(aura.SpellId))
                    && (FindPlayersNearby(vehicle.Location, NonCompeteDistance).Count() <= 0)
                 orderby vehicle.Distance
                 select vehicle)
                 .FirstOrDefault();
        }


        /// <summary>
        /// <para>Reads the "Quest Behaviors/DATA/AuraIds_OccupiedVehicle.xml" file, and returns an IEnumerable
        /// of all the AuraIds that are represent Vehicles that are occupied.</para>
        /// <para>If the da file has malformed entries (which it should never be), error messages
        /// will be emitted.</para>
        /// </summary>
        /// <returns>the IEnumerable may be empty, but it will never be null.</returns>
        //  7Mar2013-02:28UTC chinajade
        public IEnumerable<int> GetOccupiedVehicleAuraIds()
        {
            List<int> occupiedVehicleAuraIds = new List<int>();
            string auraDataFileName = Path.Combine(GlobalSettings.Instance.QuestBehaviorsPath, "DATA", "AuraIds_OccupiedVehicle.xml");

            if (!File.Exists(auraDataFileName))
            {
                LogWarning("Unable to locate Occupied Vehicle Aura database (in {0}).  Vehicles will be unqualified"
                    + "--this may cause us to follow vehicles occupied by other players.",
                    auraDataFileName);
                return occupiedVehicleAuraIds;
            }

            XDocument xDoc = XDocument.Load(auraDataFileName);

            foreach (XElement aura in xDoc.Descendants("Auras").Elements())
            {
                string elementAsString = aura.ToString();

                XAttribute spellIdAttribute = aura.Attribute("SpellId");
                if (spellIdAttribute == null)
                {
                    LogError("Unable to locate SpellId attribute for {0}", elementAsString);
                    continue;
                }

                int auraSpellId;
                if (!int.TryParse(spellIdAttribute.Value, out auraSpellId))
                {
                    LogError("Unable to parse SpellId attribute for {0}", elementAsString);
                    continue;
                }

                occupiedVehicleAuraIds.Add(auraSpellId);
            }

            return occupiedVehicleAuraIds;
        }


        private string GetMobNameFromId(int wowUnitId)
        {
            WoWUnit wowUnit = FindUnitsFromIds(new int[] { wowUnitId }).FirstOrDefault();

            return (wowUnit != null)
                ? wowUnit.Name
                : string.Format("MobId({0})", wowUnitId);
        }


        // 24Feb2013-08:11UTC chinajade
        private bool IsQuestObjectiveComplete(int questId, int objectiveIndex)
        {
            if (Me.QuestLog.GetQuestById((uint)questId) == null)
                { return false; }

            int questLogIndex = Lua.GetReturnVal<int>(string.Format("return GetQuestLogIndexByID({0})", questId), 0);

            return
                Lua.GetReturnVal<bool>(string.Format("return GetQuestLogLeaderBoard({0},{1})", objectiveIndex, questLogIndex), 2);
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
        #endregion


        #region Utility Behaviors
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


        private Composite UtilityBehavior_MoveTo(LocationDelegate locationDelegate,
                                                    StringDelegate locationNameDelegate,
                                                    RangeDelegate precisionDelegate = null)
        {
            ContractRequires(locationDelegate != null, () => "locationRetriever may not be null");
            ContractRequires(locationNameDelegate != null, () => "locationNameDelegate may not be null");
            precisionDelegate = precisionDelegate ?? (context => Navigator.PathPrecision);

            return new PrioritySelector(locationContext => locationDelegate(locationContext),
                new Decorator(locationContext => !Me.InVehicle && !Me.Mounted
                                                    && Mount.CanMount()
                                                    && Mount.ShouldMount((WoWPoint)locationContext),
                    new Action(locationContext => { Mount.MountUp(() => (WoWPoint)locationContext); })),

                new Decorator(locationContext => (Me.Location.Distance((WoWPoint)locationContext) > precisionDelegate(locationContext)),
                    new Sequence(
                        new Action(context =>
                        {
                            WoWPoint destination = locationDelegate(context);
                            string locationName = locationNameDelegate(context) ?? destination.ToString();

                            TreeRoot.StatusText = "Moving to " + locationName;

                            MoveResult moveResult = Navigator.MoveTo(destination);

                            // If Navigator couldn't move us, resort to click-to-move...
                            if (!((moveResult == MoveResult.Moved)
                                    || (moveResult == MoveResult.ReachedDestination)
                                    || (moveResult == MoveResult.PathGenerated)))
                            {
                                WoWMovement.ClickToMove(destination);
                            }
                        }),
                        new WaitContinue(Delay_WoWClientMovementThrottle, ret => false, new ActionAlwaysSucceed())
                    ))
                );
        }
        #endregion


        #region Pet Helpers
        // Cut-n-paste any PetControl helper methods you need, here...
        #endregion


        #region Vehicle Behaviors
        private class ArticulationDiscoveryStateType
        {
            public ArticulationDiscoveryStateType()
            {
                OriginalAzimuth = CurrentAzimuth;
                OriginalHeading = CurrentHeading;

                if (!HasAngleControl)
                {
                    IsDiscovered_Up = true;
                    IsDiscovered_Down = true;
                    Limit_Up = OriginalAzimuth;
                    Limit_Down = OriginalAzimuth;
                }
            }

            public double CurrentAzimuth
            {
                get { return NormalizeAngleToPi(Lua.GetReturnVal<double>("return VehicleAimGetAngle();", 0)); }
            }

            public double CurrentHeading
            {
                get
                {
                    WoWObject facingListener = (StyxWoW.Me.Transport != null) ? StyxWoW.Me.Transport : StyxWoW.Me;
                    return WoWMathHelper.NormalizeRadian(facingListener.Rotation);
                }
            }

            public WoWObject FacingListener
            {
                get { return (StyxWoW.Me.Transport != null) ? StyxWoW.Me.Transport : StyxWoW.Me; }
            }

            public bool HasAngleControl
            {
                get { return Lua.GetReturnVal<bool>("return IsVehicleAimAngleAdjustable();", 0); }
            }

            public bool IsDiscovered_Down { get; set; }
            public bool IsDiscovered_Left { get; set; }
            public bool IsDiscovered_Right { get; set; }
            public bool IsDiscovered_Up { get; set; }
            public double Limit_Down { get; set; }
            public double Limit_Left { get; set; }
            public double Limit_Right { get; set; }
            public double Limit_Up { get; set; }
            public double OriginalAzimuth { get; private set; }
            public double OriginalHeading { get; private set; }


            private bool DiscoverDown()
            {
                RequestAzimuth(-Math.PI / 2);
                Limit_Down = CurrentAzimuth;
            }

            private bool DiscoverUp()
            {
                RequestAzimuth(Math.PI / 2);
                Limit_Up = CurrentAzimuth;
            }

            public void RequestAzimuth(double requestedAzimuth)
            {
                Lua.DoString("VehicleAimRequestAngle({0})", requestedAzimuth);
            }
        }

        private ArticulationDiscoveryStateType ArticulationDiscoveryState = null;
        private double HeadingOffset { get; set; }
        private double HeadingIncrement { get; set; }
        private double OriginalHeading { get; set; }
        public void UtilityBehaviorPS_DiscoverArticulationLimits()
        {
            const double Tolerance_Azimuth = 0.05;
            const double Tolerance_Heading = 0.05;

            return new Decorator(context => Me.InVehicle,
                new PrioritySelector(
                    new Decorator(context => ArticulationDiscoveryState == null,
                        new Action(context => { ArticulationDiscoveryState = new ArticulationDiscoveryStateType(); })),

                            
                    // Left...
                    new Decorator(context => !ArticulationDiscoveryState.IsDiscovered_Left,
                        new Sequence(
                            new Action(context =>
                            {
                                OriginalHeading = ArticulationDiscoveryState.FacingListener.Rotation;
                                HeadingOffset = 0.0;
                                HeadingIncrement = Math.PI / 16;
                            }),
                            new WhileLoop(RunStatus.Success, context => HeadingOffset <= (Math.PI + HeadingIncrement/2),
                                new Action(context => { Me.SetFacing(WoWMathHelper.NormalizeRadian((float)(OriginalHeading + HeadingOffset))); }),
                                new Wait(TimeSpan.FromMilliseconds(1000),
                                    context => Math.Abs(ArticulationDiscoveryState.FacingListener.Rotation - HeadingOffset) < Tolerance_Heading,
                                    new ActionAlwaysSucceed()),
                                new Action(context =>
                                {
                                    ArticulationDiscoveryState.Limit_Left = ArticulationDiscoveryState.CurrentHeading;
                                    HeadingOffset += HeadingIncrement;
                                })
                            ),
                            new Action(context => { ArticulationDiscoveryState.IsDiscovered_Left = true; })
                        )),

                    // Right...
                    new Decorator(context => !ArticulationDiscoveryState.IsDiscovered_Right,
                        new PrioritySelector(
                        )),

                    // Capture discoveries...
                    new Action(context =>
                    {
                        WoWObject facingListener = (Me.Transport != null) ? Me.Transport : Me;
                        Stopwatch maxReactionTimer = new Stopwatch();

                        Func<double> azimuthCurrent = () => { return NormalizeAngleToPi(Lua.GetReturnVal<double>("return VehicleAimGetAngle();", 0)); };
                        Action<double> azimuthRequest = (requestedAzimuth) => { Lua.DoString("VehicleAimRequestAngle({0})", requestedAzimuth); };

                        Func<double, double> acquireAzimuthLimit = (desiredAzimuth) =>
                            {
                                const double azimuthTolerance = 0.05;
                                const int maxReactionTimeInMilliseconds = 1000;

                                azimuthRequest(desiredAzimuth);

                                maxReactionTimer.Restart();
                                while ((maxReactionTimer.ElapsedMilliseconds < maxReactionTimeInMilliseconds)
                                        && (Math.Abs(azimuthCurrent() - desiredAzimuth) > azimuthTolerance))
                                {
                                    // empty
                                }

                                return azimuthCurrent();
                            };

                        Func<double, double> acquireHeadingLimit = (desiredHeading) =>
                        {
                            // Start from original heading...
                            while (Math.Abs(
                            Me.SetFacing(originalHeading);
                        for (headingOffset = 0.0f; headingOffset <= (Math.PI + headingIncrement/2); headingOffset += headingIncrement)
                        {
                            Me.SetFacing(WoWMathHelper.NormalizeRadian(originalHeading + headingOffset));
                            limitLeft = WoWMathHelper.NormalizeRadian(facingListener.Rotation);
            LogInfo("Left: {0:F2} (offset: {1:F2})", limitLeft, headingOffset);
                        }

                            return 0.0;
                        };

                        Func<double> headingCurrent = () => { return ((Me.Transport != null) ? Me.Transport : Me).Rotation; };
                        Action<double> headingRequest = (desiredHeading) =>
                        {
                            const double headingTolerance = 0.05;
                            const int maxReactionTimeInMilliseconds = 1000;

                            desiredHeading = WoWMathHelper.NormalizeRadian((float)desiredHeading);
                            while ((maxReactionTimer.ElapsedMilliseconds < maxReactionTimeInMilliseconds)
                                    && (Math.Abs(headingCurrent() - desiredHeading) > headingTolerance))
                            {
                                Me.SetFacing((float)desiredHeading);
                            }
                        };

                        // Discover Azimuth limits...
                        double originalAzimuth = azimuthCurrent(); // capture original azimuth
                        double limitUp = acquireAzimuthLimit(Math.PI / 2);
                        double limitDown = acquireAzimuthLimit(-Math.PI / 2);
                        azimuthRequest(originalAzimuth);    // restore original azimuth
LogWarning("UP Discovered as: {0:F1}", limitUp);
LogWarning("DOWN Discovered as: {0:F1}", limitDown);

                        // Discover Heading limits...

                        // We want to force turning in a particular direction (CCW) to determine 'left' limits...
                        const float headingIncrement = (float)Math.PI / 16;
                        float headingOffset;
                        double limitLeft = 0.0;
                        double limitRight = 0.0;
                        float originalHeading = facingListener.Rotation;


                        Me.SetFacing(originalHeading);
                        for (headingOffset = 0.0f; headingOffset <= (Math.PI + headingIncrement/2); headingOffset += headingIncrement)
                        {
                            Me.SetFacing(WoWMathHelper.NormalizeRadian(originalHeading + headingOffset));
                            limitLeft = WoWMathHelper.NormalizeRadian(facingListener.Rotation);
            LogInfo("Left: {0:F2} (offset: {1:F2})", limitLeft, headingOffset);
                        }

                        Me.SetFacing(originalHeading);
                        for (headingOffset = 0.0f; headingOffset <= (Math.PI + headingIncrement/2); headingOffset += headingIncrement)
                        {
                            Me.SetFacing(WoWMathHelper.NormalizeRadian(originalHeading - headingOffset));
                            limitRight = WoWMathHelper.NormalizeRadian(facingListener.Rotation);
            LogInfo("Right: {0:F2} (offset: {1:F2})", limitRight, headingOffset);
                        }

                        // Return to original heading...
                        Me.SetFacing((float)originalHeading);

                        //return new ArticulationLimitsType(up, down, left, right);
                    })
            ));
        }
        #endregion


        #region Math helpers
        public double? CalculateBallisticLaunchAngle(
            WoWUnit target,
            double muzzleVelocityInFps,
            ArticulationLimitsType articulationRange)
        {
            if (target == null)
                { return null; }

            // TODO: Express this equation straightforward
            double g = 32.174; // in feet per second^2
            double v0Sqr = muzzleVelocityInFps * muzzleVelocityInFps;
            double distance2D = Me.Location.Distance2D(target.Location);
            double heightDiff = target.Location.Z - Me.Location.Z;

            double tmp1 = g * (distance2D * distance2D);
            double tmp2 = 2 * heightDiff * (muzzleVelocityInFps * muzzleVelocityInFps);
            double radicalTerm = (v0Sqr * v0Sqr) - (g * (tmp1 + tmp2));

            // If radicalTerm is negative, then both roots are imaginary...
            // This means that the muzzleVelocity is insufficient to hit the target
            // at the target's current distance.
            if (radicalTerm < 0)
                { return null; }

            // Prefer the 'lower' angle, if its within the articulation range...
            double root = Math.Atan((v0Sqr - radicalTerm) / (g * distance2D));
            if (articulationRange.IsWithinAzimuthArticulation(root))
                { return root; }

            // First root provides no solution, try second root...
            root = Math.Atan((v0Sqr + radicalTerm) / (g * distance2D));
            if (articulationRange.IsWithinAzimuthArticulation(root))
                { return root; }

            // Both solutions are out of the vehicle's articulation capabilities, return "no solution"...
            return null;
        }



        /// <summary>
        /// Returns the normalized ANGLEINRADIANS to the closed interval [-PI..+PI]
        /// </summary>
        /// <param name="radians"></param>
        /// <returns></returns>
        public static double NormalizeAngleToPi(double angleInRadians)
        {
            while (angleInRadians > Math.PI)  { angleInRadians -= (2 * Math.PI); }
            while (angleInRadians < -Math.PI) { angleInRadians += (2 * Math.PI); }
            return (angleInRadians);
        }
        #endregion


        #region XML parsing

        private class HotspotType : XmlUtilClass_ElementParser
        {
            public static HotspotType Create(CustomForcedBehavior parent, XElement element)
            {
                return new HotspotType(parent, element);
            }

            private HotspotType(CustomForcedBehavior parent, XElement element)
                : base(parent, element)
            {
                try
                {
                    ToWoWPoint = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
                }

                catch (Exception except)
                {
                    parent.LogMessage("error", "[PROFILE PROBLEM with \"{0}\"]: {1}\nFROM HERE:\n{2}\n",
                        element.ToString(), except.Message, except.StackTrace);
                    IsAttributeProblem = true;
                }
            }

            public WoWPoint ToWoWPoint { get; private set; }
        }


        private class QuestGoalType : XmlUtilClass_ElementParser
        {
            public static QuestGoalType Create(CustomForcedBehavior parent, XElement element)
            {
                return new QuestGoalType(parent, element);
            }

            private QuestGoalType(CustomForcedBehavior parent, XElement element)
                : base(parent, element)
            {
                try
                {
                    ObjectiveIndex = GetAttributeAsNullable<int>("ObjectiveIndex", true, new ConstrainTo.Domain<int>(1, 5), null) ?? 0;
                    MobIds = GetNumberedAttributesAsArray<int>("MobId", 1, ConstrainAs.MobId, null);
                    ActionBarIndex_Attack = GetNumberedAttributesAsArray<int>("ActionBarIndex_Attack", 1, ConstrainAs.HotbarButton, null);
                }

                catch (Exception except)
                {
                    parent.LogMessage("error", "[PROFILE PROBLEM with \"{0}\"]: {1}\nFROM HERE:\n{2}\n",
                        element.ToString(), except.Message, except.StackTrace);
                    IsAttributeProblem = true;
                }
            }

            public bool IsGoalComplete(int questId)
            {
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)questId);

                if (quest == null)
                    { return false; }

                var objective = quest.GetObjectives().FirstOrDefault(o => o.Index == ObjectiveIndex);
//LogInfo(objective.AsXmlString());
                

                int questLogIndex = Lua.GetReturnVal<int>(string.Format("return GetQuestLogIndexByID({0})", questId), 0);

                return
                    Lua.GetReturnVal<bool>(string.Format("return GetQuestLogLeaderBoard({0},{1})", ObjectiveIndex, questLogIndex), 2);
            }


            public int ObjectiveIndex { get; private set; }
            public int[] MobIds { get; private set; }
            public int[] ActionBarIndex_Attack { get; private set; }
        }


        private IEnumerable<T> XmlUtil_ParseSubtree<T>(
            Func<CustomForcedBehavior, XElement, T> factory,
            string pathElementName,
            string subElementsName)
            where T: XmlUtilClass_ElementParser
        {
            var descendants = Element.Descendants(pathElementName).Elements();
            var returnValue = new List<T>();

            foreach (var element in descendants.Where(elem => elem.Name == subElementsName))
            {
                try
                {
                    T parser = factory(this, element);

                    this.IsAttributeProblem = parser.IsAttributeProblem;
                    returnValue.Add(parser);
                }

                catch(Exception ex)
                {
                    LogError("{0}: {1}", element.ToString(), ex.ToString());
                    IsAttributeProblem = true;
                }
            }

            return returnValue;
        }


        private class XmlUtilClass_ElementParser : CustomForcedBehavior
        {
            protected XmlUtilClass_ElementParser(CustomForcedBehavior parent, XElement element)
                : base(ParseElementAttributes(element))
            {
                Element = element;
                _parent = parent;
            }

            private static Dictionary<string, string> ParseElementAttributes(XElement element)
            {
                var arguments = new Dictionary<string, string>();

                foreach (var attribute in element.Attributes())
                    { arguments.Add(attribute.Name.ToString(), attribute.Value); }

                return arguments;
            }

            #region (No-op) Overrides for CustomForcedBehavior
            protected override Composite CreateBehavior() { return new PrioritySelector(); }

            public override bool IsDone { get { return false; } }

            public override void OnStart() { /*empty*/ }
            #endregion

            CustomForcedBehavior _parent;
        }
        #endregion


        #region Diagnostic Methods
        public delegate string StringProviderDelegate();

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

                LogError("[CONTRACT VIOLATION] {0}\nLocation:\n{1}", message, trace.ToString());
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
        /// <para>Normal information to keep user informed.</para>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public void LogInfo(string message, params object[] args)
        {
            LogMessage("info", message, args);
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


    #region DEBUG
    public static class DebugExtensions
    {
        // 9Mar2013-07:55UTC chinajade
        public static string ToString_FullInfo(this PlayerQuest playerQuest, bool useCompactForm = false, int indentLevel = 0)
        {
            var tmp = new StringBuilder();

            if (playerQuest != null)
            {
                var indent = string.Empty.PadLeft(indentLevel);
                var fieldSeparator = useCompactForm ? " " : string.Format("\n  {0}", indent);

                tmp.AppendFormat("<PlayerQuest Key_Id=\"{0}\" Key_Name=\"{1}\"", playerQuest.Id, playerQuest.Name);
                tmp.AppendFormat("{0}CompletionText=\"{1}\"", fieldSeparator, playerQuest.CompletionText);
                tmp.AppendFormat("{0}Description=\"{1}\"", fieldSeparator, playerQuest.Description);
                tmp.AppendFormat("{0}FlagsPvP=\"{1}\"", fieldSeparator, playerQuest.FlagsPVP);
                tmp.AppendFormat("{0}Id=\"{1}\"", fieldSeparator, playerQuest.Id);
                tmp.AppendFormat("{0}InternalInfo=\"{1}\"", fieldSeparator, playerQuest.InternalInfo);
                tmp.AppendFormat("{0}IsAutoAccepted=\"{1}\"", fieldSeparator, playerQuest.IsAutoAccepted);
                tmp.AppendFormat("{0}IsCompleted=\"{1}\"", fieldSeparator, playerQuest.IsCompleted);
                tmp.AppendFormat("{0}IsDaily=\"{1}\"", fieldSeparator, playerQuest.IsDaily);
                tmp.AppendFormat("{0}IsFailed=\"{1}\"", fieldSeparator, playerQuest.IsFailed);
                tmp.AppendFormat("{0}IsPartyQuest=\"{1}\"", fieldSeparator, playerQuest.IsPartyQuest);
                tmp.AppendFormat("{0}IsSharable=\"{1}\"", fieldSeparator, playerQuest.IsShareable);
                tmp.AppendFormat("{0}IsStayAliveQuest=\"{1}\"", fieldSeparator, playerQuest.IsStayAliveQuest);
                tmp.AppendFormat("{0}IsWeekly=\"{1}\"", fieldSeparator, playerQuest.IsWeekly);
                tmp.AppendFormat("{0}Level=\"{1}\"", fieldSeparator, playerQuest.Level);
                tmp.AppendFormat("{0}Name=\"{1}\"", fieldSeparator, playerQuest.Name);
                tmp.AppendFormat("{0}NormalObjectiveRequiredCounts=\"{1}\"", fieldSeparator,
                    (playerQuest.NormalObjectiveRequiredCounts == null)
                    ? "NONE"
                    : string.Join(", ", playerQuest.NormalObjectiveRequiredCounts.Select(c => c.ToString())));
                tmp.AppendFormat("{0}Objectives=\"{1}\"", fieldSeparator,
                    (playerQuest.Objectives == null)
                    ? "NONE"
                    : string.Join(",", playerQuest.Objectives.Select(o => string.Format("{0}  \"{1}\"", fieldSeparator, o))));
                tmp.AppendFormat("{0}ObjectiveText=\"{1}\"", fieldSeparator, playerQuest.ObjectiveText);
                tmp.AppendFormat("{0}RequiredLevel=\"{1}\"", fieldSeparator, playerQuest.RequiredLevel);
                tmp.AppendFormat("{0}RewardMoney=\"{1}\"", fieldSeparator, playerQuest.RewardMoney);
                tmp.AppendFormat("{0}RewardMoneyAtMaxLevel=\"{1}\"", fieldSeparator, playerQuest.RewardMoneyAtMaxLevel);
                tmp.AppendFormat("{0}RewardNumTalentPoints=\"{1}\"", fieldSeparator, playerQuest.RewardNumTalentPoints);
                tmp.AppendFormat("{0}RewardSpell=\"{1}\"", fieldSeparator, 
                    (playerQuest.RewardSpell == null)
                    ? null
                    : ToString_FullInfo(playerQuest.RewardSpell, false, indentLevel +4));
                tmp.AppendFormat("{0}RewardSpellId=\"{1}\"", fieldSeparator, playerQuest.RewardSpellId);
                tmp.AppendFormat("{0}RewardTitleId=\"{1}\"", fieldSeparator, playerQuest.RewardTitleId);
                tmp.AppendFormat("{0}RewardXp=\"{1}\"", fieldSeparator, playerQuest.RewardXp);
                tmp.AppendFormat("{0}SubDescription=\"{1}\"", fieldSeparator, playerQuest.SubDescription);
                tmp.AppendFormat("{0}SuggestedPlayers=\"{1}\"", fieldSeparator, playerQuest.SuggestedPlayers);
                tmp.AppendFormat("{0}/>", fieldSeparator);
            }

            return tmp.ToString();
        }


        // 9Mar2013-07:55UTC chinajade
        public static string ToString_FullInfo(this Styx.WoWInternals.Quest.QuestObjective questObjective, bool useCompactForm = false, int indentLevel = 0)
        {
            var tmp = new StringBuilder();

            var indent = string.Empty.PadLeft(indentLevel);
            var fieldSeparator = useCompactForm ? " " : string.Format("\n  {0}", indent);

            tmp.AppendFormat("<QuestObjective Key_Index=\"{0}\"", questObjective.Index);
            tmp.AppendFormat("{0}Count=\"{1}\"", fieldSeparator, questObjective.Count);
            tmp.AppendFormat("{0}ID=\"{1}\"", fieldSeparator, questObjective.ID);
            tmp.AppendFormat("{0}Index=\"{1}\"", fieldSeparator, questObjective.Index);
            tmp.AppendFormat("{0}IsEmpty=\"{1}\"", fieldSeparator, questObjective.IsEmpty);
            tmp.AppendFormat("{0}Objective=\"{1}\"", fieldSeparator, questObjective.Objective);
            tmp.AppendFormat("{0}Type=\"{1}\"", fieldSeparator, questObjective.Type);
            tmp.AppendFormat("{0}/>", fieldSeparator);

            return tmp.ToString();
        }


        //  9Mar2013-07:55UTC chinajade
        public static string ToString_FullInfo(this SpellEffect spellEffect, bool useCompactForm = false, int indentLevel = 0)
        {
            var tmp = new StringBuilder();

            if (spellEffect != null)
            {
                var indent = string.Empty.PadLeft(indentLevel);
                var fieldSeparator = useCompactForm ? " " : string.Format("\n  {0}", indent);

                tmp.AppendFormat("<SpellEffect Key_TriggerSpell=\"{0}\"", spellEffect.TriggerSpell);
                tmp.AppendFormat("{0}Amplitude=\"{1}\"", fieldSeparator, spellEffect.Amplitude);
                tmp.AppendFormat("{0}AuraType=\"{1}\"", fieldSeparator, spellEffect.AuraType);
                tmp.AppendFormat("{0}BasePoints=\"{1}\"", fieldSeparator, spellEffect.BasePoints);
                tmp.AppendFormat("{0}ChainTarget=\"{1}\"", fieldSeparator, spellEffect.ChainTarget);
                tmp.AppendFormat("{0}EffectType=\"{1}\"", fieldSeparator, spellEffect.EffectType);
                tmp.AppendFormat("{0}ImplicitTargetA=\"{1}\"", fieldSeparator, spellEffect.ImplicitTargetA);
                tmp.AppendFormat("{0}ImplicitTargetB=\"{1}\"", fieldSeparator, spellEffect.ImplicitTargetB);
                tmp.AppendFormat("{0}ItemType=\"{1}\"", fieldSeparator, spellEffect.ItemType);
                tmp.AppendFormat("{0}Mechanic=\"{1}\"", fieldSeparator, spellEffect.Mechanic);
                tmp.AppendFormat("{0}MiscValueA=\"{1}\"", fieldSeparator, spellEffect.MiscValueA);
                tmp.AppendFormat("{0}MiscValueB=\"{1}\"", fieldSeparator, spellEffect.MiscValueB);
                tmp.AppendFormat("{0}MultipleValue=\"{1}\"", fieldSeparator, spellEffect.MultipleValue);
                tmp.AppendFormat("{0}PointsPerComboPoint=\"{1}\"", fieldSeparator, spellEffect.PointsPerComboPoint);
                tmp.AppendFormat("{0}RadiusIndex=\"{1}\"", fieldSeparator, spellEffect.RadiusIndex);
                tmp.AppendFormat("{0}RealPointsPerLevel=\"{1}\"", fieldSeparator, spellEffect.RadiusIndex);
                tmp.AppendFormat("{0}SpellClassMask=\"{1}\"", fieldSeparator, spellEffect.SpellClassMask);
                tmp.AppendFormat("{0}TriggerSpell=\"{1}\"", fieldSeparator, spellEffect.TriggerSpell);
                tmp.AppendFormat("{0}/>", fieldSeparator);
            }

            return tmp.ToString();
        }


        //  9Mar2013-07:55UTC chinajade
        public static string ToString_FullInfo(this WoWMissile wowMissile, bool useCompactForm = false, int indentLevel = 0)
        {
            var tmp = new StringBuilder();

            if (wowMissile != null)
            {
                var indent = string.Empty.PadLeft(indentLevel);
                var fieldSeparator = useCompactForm ? " " : string.Format("\n  {0}", indent);

                bool isInFlight = WoWMissile.InFlightMissiles.FirstOrDefault(m => m.BaseAddress == wowMissile.BaseAddress) != null;

                tmp.AppendFormat("<WoWMissile Key_Spell=\"{0}\" BaseAddress=\"0x{1:x}\"",
                    ((wowMissile.Spell == null) ? "UNKNOWN" : wowMissile.Spell.Name),
                    (wowMissile.BaseAddress));
                tmp.AppendFormat("{0}Caster=\"{1}\"", fieldSeparator,
                    (wowMissile.Caster == null) ? "UNKNOWN" : wowMissile.Caster.Name);
                tmp.AppendFormat("{0}CasterGuid=\"0x{1:x}\" <!--Me=\"0x{2:x}\" MyVehicle=\"0x{3:x}\" -->",
                    fieldSeparator, wowMissile.Caster.Guid, StyxWoW.Me.Guid, StyxWoW.Me.TransportGuid);
                tmp.AppendFormat("{0}FirePosition=\"{1}\"", fieldSeparator, wowMissile.FirePosition);
                tmp.AppendFormat("{0}Flags=\"0x{1:x}\"", fieldSeparator, wowMissile.Flags);
                tmp.AppendFormat("{0}ImpactPosition=\"{1}\" <!--dist: {2:F1}-->", fieldSeparator, wowMissile.ImpactPosition,
                    wowMissile.ImpactPosition.Distance(StyxWoW.Me.Location));
                tmp.AppendFormat("{0}IsInFlight=\"{1}\"", fieldSeparator, isInFlight);
                tmp.AppendFormat("{0}Position=\"{1}\" <!--dist: {2:F1}-->", fieldSeparator, wowMissile.Position,
                    wowMissile.Position.Distance(StyxWoW.Me.Location));
                tmp.AppendFormat("{0}Spell=\"{1}\"", fieldSeparator,
                    (wowMissile.Spell == null) ? "NONE" : wowMissile.Spell.Name);
                tmp.AppendFormat("{0}SpellId=\"{1}\"", fieldSeparator, wowMissile.SpellId);
                tmp.AppendFormat("{0}SpellVisualId=\"{1}\"", fieldSeparator, wowMissile.SpellVisualId);
                tmp.AppendFormat("{0}Target=\"{1}\"", fieldSeparator,
                    (wowMissile.Target == null) ? "NONE" : wowMissile.Target.Name);
                tmp.AppendFormat("{0}TargetGuid=\"0x{1:x}\"", fieldSeparator, wowMissile.TargetGuid);
                tmp.AppendFormat("{0}/>", fieldSeparator);
            }

            return tmp.ToString();
        }


        //  9Mar2013-07:55UTC chinajade
        public static string ToString_FullInfo(this WoWPetSpell wowPetSpell, bool useCompactForm = false, int indentLevel = 0)
        {
            StringBuilder tmp = new StringBuilder();

            if (wowPetSpell != null)
            {
                var indent = string.Empty.PadLeft(indentLevel);
                var fieldSeparator = useCompactForm ? " " : string.Format("\n  {0}", indent);

                tmp.AppendFormat("<WoWPetSpell Key_ActionBarIndex=\"{0}\"", wowPetSpell.ActionBarIndex);
                tmp.AppendFormat("{0}Action=\"{1}\"", fieldSeparator, wowPetSpell.Action);
                tmp.AppendFormat("{0}Cooldown=\"{1}\"", fieldSeparator, wowPetSpell.Cooldown);
                tmp.AppendFormat("{0}Spell=\"{1}\"", fieldSeparator,
                    (wowPetSpell.Spell == null)
                    ? "NONE"
                    : ToString_FullInfo(wowPetSpell.Spell, useCompactForm, indentLevel + 4));
                tmp.AppendFormat("{0}SpellType=\"{1}\"", fieldSeparator, wowPetSpell.SpellType);
                tmp.AppendFormat("{0}Stance=\"{1}\"", fieldSeparator, wowPetSpell.Stance);
                tmp.AppendFormat("{0}/>", fieldSeparator);
            }

            return tmp.ToString();
        }


        //  9Mar2013-07:55UTC chinajade
        public static string ToString_FullInfo(this WoWSpell wowSpell, bool useCompactForm = false, int indentLevel = 0)
        {
            StringBuilder tmp = new StringBuilder();

            if (wowSpell != null)
            {
                var indent = string.Empty.PadLeft(indentLevel);
                var fieldSeparator = useCompactForm ? " " : string.Format("\n  {0}", indent);

                tmp.AppendFormat("<WoWSpell Key_Id=\"{0}\" Key_Name=\"{1}\"", wowSpell.Id, wowSpell.Name);
                tmp.AppendFormat("{0}BaseCooldown=\"{1}\"", fieldSeparator, wowSpell.BaseCooldown);
                // tmp.AppendFormat("{0}BaseDuration=\"{1}\"", fieldSeparator, wowSpell.BaseDuration);
                tmp.AppendFormat("{0}BaseLevel=\"{1}\"", fieldSeparator, wowSpell.BaseLevel);
                tmp.AppendFormat("{0}CanCast=\"{1}\"", fieldSeparator, wowSpell.CanCast);
                tmp.AppendFormat("{0}CastTime=\"{1}\"", fieldSeparator, wowSpell.CastTime);
                tmp.AppendFormat("{0}Category=\"{1}\"", fieldSeparator, wowSpell.Category);
                tmp.AppendFormat("{0}CooldownTime=\"{1}\"", fieldSeparator, wowSpell.Cooldown);
                tmp.AppendFormat("{0}CooldownTimeLeft=\"{1}\"", fieldSeparator, wowSpell.CooldownTimeLeft);
                tmp.AppendFormat("{0}CreatesItemId=\"{1}\"", fieldSeparator, wowSpell.CreatesItemId);
                tmp.AppendFormat("{0}DispellType=\"{1}\"", fieldSeparator, wowSpell.DispelType);
                // tmp.AppendFormat("{0}DurationPerLevel=\"{1}\"", fieldSeparator, wowSpell.DurationPerLevel);
                tmp.AppendFormat("{0}HasRange=\"{1}\"", fieldSeparator, wowSpell.HasRange);
                tmp.AppendFormat("{0}Id=\"{1}\"", fieldSeparator, wowSpell.Id);
                tmp.AppendFormat("{0}IsFunnel=\"{1}\"", fieldSeparator, wowSpell.IsFunnel);
                tmp.AppendFormat("{0}IsMelee=\"{1}\"", fieldSeparator, wowSpell.IsMeleeSpell);
                tmp.AppendFormat("{0}IsSelfOnly=\"{1}\"", fieldSeparator, wowSpell.IsSelfOnlySpell);
                tmp.AppendFormat("{0}Level: {1}", fieldSeparator, wowSpell.Level);
                // tmp.AppendFormat("{0}MaxDuration=\"{1}\"", fieldSeparator, wowSpell.MaxDuration);
                tmp.AppendFormat("{0}MaxRange=\"{1}\"", fieldSeparator, wowSpell.MaxRange);
                tmp.AppendFormat("{0}MaxStackCount=\"{1}\"", fieldSeparator, wowSpell.MaxStackCount);
                tmp.AppendFormat("{0}MaxTargets=\"{1}\"", fieldSeparator, wowSpell.MaxTargets);
                tmp.AppendFormat("{0}Mechanic=\"{1}\"", fieldSeparator, wowSpell.Mechanic);
                tmp.AppendFormat("{0}MinRange=\"{1}\"", fieldSeparator, wowSpell.MinRange);
                tmp.AppendFormat("{0}Name=\"{1}\"", fieldSeparator, wowSpell.Name);
                tmp.AppendFormat("{0}PowerCost=\"{1}\"", fieldSeparator, wowSpell.PowerCost);
                tmp.AppendFormat("{0}ResearchProjectId=\"{1}\"", fieldSeparator, wowSpell.ResearchProjectId);
                tmp.AppendFormat("{0}School=\"{1}\"", fieldSeparator, wowSpell.School);
                tmp.AppendFormat("{0}SpellDescriptionVariableId=\"{1}\"", fieldSeparator, wowSpell.SpellDescriptionVariableId);

                tmp.AppendFormat("{0}SpellEffects=\"{1}\"", fieldSeparator, (wowSpell.SpellEffects.Count() == 0) ? " NONE" : "");
                foreach (var effect in wowSpell.SpellEffects)
                    { tmp.AppendFormat("{0}  {1}", fieldSeparator, ToString_FullInfo(effect, useCompactForm, indentLevel + 4)); }

                tmp.AppendFormat("{0}SpellMissileId=\"{1}\"", fieldSeparator, wowSpell.SpellMissileId);
                tmp.AppendFormat("{0}TargetType=\"0x{1:x}\"", fieldSeparator, wowSpell.TargetType);
                tmp.AppendFormat("{0}/>", fieldSeparator);
            }

            return tmp.ToString();
        }


        //  9Mar2013-07:55UTC chinajade
        public static ulong Util_AbbreviatedGuid(ulong guid)
        {
            return guid & 0x0ffffff;
        }
    }
    #endregion
}