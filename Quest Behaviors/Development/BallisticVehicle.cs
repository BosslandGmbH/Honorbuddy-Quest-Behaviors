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
using System.Linq;
using System.Xml.Linq;

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Honorbuddy.QuestBehaviorCore.XmlElements;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using System.Text;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.QuestBehaviors.BallisticVehicle
{
    [CustomBehaviorFileName(@"Vehicles\BallisticVehicle")]
    public class BallisticVehicle : QuestBehaviorBase
    {
        #region Consructor and Argument Processing
        public BallisticVehicle(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                MobIds_UnoccupiedVehicle = GetNumberedAttributesAsArray<int>("UnoccupiedVehicleId", 1, ConstrainAs.MobId, null);
                VehicleAcquisitionArea = GetAttributeAsNullable<WoWPoint>("VehicleAcquisitionArea", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;

                // Tunables...

                MobIds = new int[]
                {
                    37916,  // Orc Raider
                    37921,  // Orc War machine
                    37938,  // Orc Outrider
                };
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it can be quickly
                // resolved.
                QBCLog.Error("[MAINTENANCE PROBLEM]: " + except.Message
                            + "\nFROM HERE:\n"
                            + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }


        // Variables for Attributes provided by caller

        public int[] MobIds { get; private set; }
        public IEnumerable<int> MobIds_UnoccupiedVehicle { get; private set; }
        public WoWPoint VehicleAcquisitionArea { get; private set; }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return "$Id: BallisticVehicle.cs 601 2013-07-09 17:34:22Z chinajade $"; } }
        public override string SubversionRevision { get { return "$Rev: 601 $"; } }


        protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
        {
            // empty, for now
        }


        protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
        {
            // empty, for now
        }
        #endregion


        #region Private and Convenience variables
        private enum StateType_MainBehavior
        {
            AcquiringVehicle,       // initial state
            Hunting,
        }

        private IEnumerable<int> AuraIds_OccupiedVehicle { get; set; }
        private HuntingGroundsType HuntingGrounds { get; set; }
        private WoWPoint HuntingGroundCenter { get; set; }
        private IEnumerable<QuestGoalType> QuestGoals { get; set; }
        private WoWUnit SelectedTarget { get; set; }
        private StateType_MainBehavior State_MainBehavior
        {
            get { return _state_MainBehavior; }
            set
            {
                // For DEBUGGING...
                if (_state_MainBehavior != value)
                    { QBCLog.Info("State_MainBehavior: {0}", value); }
                _state_MainBehavior = value;
            }
        }
        private WoWUnit UnoccupiedVehicle { get; set; }
        private IEnumerable<VehicleAbility> VehicleAbilities { get; set; }
        private VehicleAbility VehicleSpeedBurstAbility { get; set; }
        private VehicleWeaponMoverType VehicleWeaponMover { get; set; }
        
        private StateType_MainBehavior _state_MainBehavior;
        #endregion


        #region Destructor, Dispose, and cleanup
        // Empty, for now...
        #endregion


        #region Overrides of CustomForcedBehavior

        public override void OnStart()
        {
            // Hunting ground processing...
            // NB: We had to defer this processing from the constructor, because XElement isn't available
            // to parse child XML nodes until OnStart() is called.
            HuntingGrounds = HuntingGroundsType.GetOrCreate(Element, "HuntingGrounds", HuntingGroundCenter);
            IsAttributeProblem |= HuntingGrounds.IsAttributeProblem;

            // TODO:
            // QuestGoals = XmlUtil_ParseSubtree<QuestGoalType>(QuestGoalType.Create, "Objectives", "QuestObjective");
            
            // Let QuestBehaviorBase do basic initializaion of the behavior, deal with bad or deprecated attributes,
            // capture configuration state, install BT hooks, etc.  This will also update the goal text.
            OnStart_QuestBehaviorCore(string.Empty);

            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (!IsDone)
            {
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

                AuraIds_OccupiedVehicle = GetOccupiedVehicleAuraIds();

                if (Me.InVehicle)
                {
                    QBCLog.Info("VEHICLE SPELLS--");
                    foreach (var wowPetSpell in Me.PetSpells.Where(s => s.Spell != null))
                        { QBCLog.Info(wowPetSpell.ToString_FullInfo()); }
                }

                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                if (quest != null)
                {
                    foreach (var objective in quest.GetObjectives())
                    {
                        QBCLog.Info(objective.ToString_FullInfo());
                    }
                }
            }
        }
        #endregion


        #region Main Behaviors
        protected override Composite CreateBehavior_CombatMain()
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
                            QBCLog.MaintenanceError("BehaviorState({0}) is unhandled", State_MainBehavior);
                            TreeRoot.Stop();
                            BehaviorDone();
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

                                        new Decorator(context => VehicleWeaponMover == null,
                                            new Action(context =>
                                            {
                                                VehicleWeaponMover = new VehicleWeaponMoverType();
                                                QBCLog.Info("{0}", VehicleWeaponMover.ToString());
                                            })),

                                        new Action(context =>
                                        {
                                            State_MainBehavior = StateType_MainBehavior.Hunting;
                                        })
                                    )),

                                // Move to and mount any free vehicle we've found...
                                new UtilityBehaviorSeq.Interact(context => UnoccupiedVehicle),

                                // If no vehicle to be found, move to the Vehicle acquisition area...
                                new Decorator(context => UnoccupiedVehicle == null,
                                    new UtilityBehaviorPS.MoveTo(
                                        context => VehicleAcquisitionArea,
                                        context => "Vehicle acquisition area",
                                        context => MovementBy))
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
                                        VehicleWeaponMover = null;
                                        VehicleAbilities = null;
                                        State_MainBehavior = StateType_MainBehavior.AcquiringVehicle;
                                    })),

                                // If we're done with objectives, leave vehicle and we're done...
                                new Decorator(context => UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete),
                                    new Action(context =>
                                    {
                                        Lua.DoString("VehicleExit()");
                                        BehaviorDone();
                                    })),

                                // If targets in the area, have at them...
                                new Decorator(context => Query.IsViable(SelectedTarget),
                                    new PrioritySelector(
                                        // Make certain were within the prescribed range for engagement...
                                        new Decorator(context => Me.Location.Distance(SelectedTarget.Location) > CharacterSettings.Instance.PullDistance,
                                            new UtilityBehaviorPS.MoveTo(
                                                context => SelectedTarget.Location,
                                                context => string.Format("within range of '{0}'", SelectedTarget.Name),
                                                context => MovementBy)) //, // TODO

                                        //new Action(context => { AimAndFire(SelectedMob, TODO, TODO); }) // TODO
                                    )),

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
                                new UtilityBehaviorPS.MoveTo(context => HuntingGrounds, context => MovementBy)
                            ))
                        #endregion
                    )
            ));
        }


        protected override Composite CreateBehavior_CombatOnly()
        {
            return new PrioritySelector(
                // Prevent the Combat routine from running while we're in the vehicle...
                new Decorator(context => Me.InVehicle,
                    new ActionAlwaysSucceed())
                );
        }


        protected override Composite CreateBehavior_DeathMain()
        {
            return new PrioritySelector(
                // empty, for now
                );
        }


        protected override Composite CreateMainBehavior()
        {
            return new PrioritySelector(

                // If quest is done, behavior is done...
                new Decorator(context => IsDone,
                    new Action(context => { BehaviorDone();}))
            );
        }
        #endregion


        #region Helpers
        public class VehicleAbility
        {
            public VehicleAbility(WoWPetSpell wowPetSpell)
            {
                _wowPetSpell = wowPetSpell;

                ActionBarIndex = wowPetSpell.ActionBarIndex +1;
                Spell = wowPetSpell.Spell;
QBCLog.Info("{0}", Spell.ToString_FullInfo());
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
QBCLog.Error("\nSPELL CD RESULTS({0}): \"{1}\"\nPETACTION CD RESULTS({2}): \"{3}\"",
    LuaCommand_Spell,
    string.Join("\", \"", tmpResultSpell),
    LuaCommand_PetAction,
    string.Join("\", \"", tmpResultPetAction));
                
                bool isOnCooldown1 = Lua.GetReturnVal<float>(LuaCommand_Cooldown, 0) <= 0.0f;
                bool isOnCooldown2 = _wowPetSpell.Cooldown;
                bool isOnCooldown3 = (Spell == null) ? false : (Spell.CooldownTimeLeft > TimeSpan.Zero);
                bool isOnCooldown4 = (Spell == null) ? false : Spell.Cooldown;
QBCLog.Info("COOLDOWN: {0}/{1}/{2}/{3}", isOnCooldown1, isOnCooldown2, isOnCooldown3, isOnCooldown4);
return tmpStopwatch.ElapsedMilliseconds > 2000;
                // return isOnCooldown3;
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
QBCLog.Info("COOLDOWNS BEFORE CAST");
IsAbilityReady();
tmpStopwatch.Reset();
                Lua.DoString(LuaCommand_Cast);
QBCLog.Info("COOLDOWNS AFTER CAST");
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
                        QBCLog.Warning("Muzzle Velocity not calculated--"
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

            private WoWPetSpell _wowPetSpell;

            private static readonly IEnumerable<WoWApplyAuraType> _speedBoostAuras = new List<WoWApplyAuraType>()
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
        private void AimAndFire(WoWUnit target, double muzzleVelocity, VehicleWeaponMoverType vehicleWeaponMover)
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


        private WoWUnit FindBestTarget()
        {
            IEnumerable<int> targetMobIds =
                from questGoal in QuestGoals
                where !questGoal.IsGoalComplete(QuestId)
                from mobId in questGoal.MobIds
                select mobId;

            return
               (from wowObject in Query.FindMobsAndFactions(targetMobIds.ToArray())
                let wowUnit = wowObject as WoWUnit
                orderby Me.Location.Distance(wowUnit.Location)
                select wowUnit)
                .FirstOrDefault();
        }


        private WoWUnit FindUnoccupiedVehicle(IEnumerable<int> mobIds_UnoccupiedVehicle)
        {
            Contract.Requires(mobIds_UnoccupiedVehicle != null,
                context => "mobIds_UnoccupiedVehicle argument may not be null");

            return
                (from wowObject in Query.FindMobsAndFactions(mobIds_UnoccupiedVehicle)
                 let wowUnit = wowObject as WoWUnit
                 where
                    Query.IsViable(wowUnit)
                    && !wowUnit.Auras.Values.Any(aura => AuraIds_OccupiedVehicle.Contains(aura.SpellId))
                    && !Query.FindPlayersNearby(wowUnit.Location, NonCompeteDistance).Any()
                 orderby wowUnit.Distance
                 select wowUnit)
                 .FirstOrDefault();
        }
        #endregion


        #region Vehicle Behaviors

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
                    var ability = new VehicleAbility(wowPetSpell);
                    abilities.Add(ability);
QBCLog.Info("ABILITY: {0}", ability.ToString());
//LogInfo("SPELL: {0}", wowPetSpell.Spell.ToString_FullInfo());
                }
            }

            return abilities;
        }


        public class VehicleWeaponMoverType
        {
            public VehicleWeaponMoverType()
            {
                Contract.Requires(Me.InVehicle, context => "Me.InVehicle");

                AzimuthBaseAbsolute = AzimuthCurrentAbsolute();
                HeadingBaseAbsolute = HeadingCurrentAbsolute();

                DiscoverArticulationLimits();
            }

            public double AzimuthCurrentAbsolute()
            {
                return NormalizeAngleToPi(Lua.GetReturnVal<double>("return VehicleAimGetAngle();", 0));
            }


            public void AzimuthRequestAbsolute(double absoluteAzimuth)
            {
                Lua.DoString("VehicleAimRequestAngle({0})", absoluteAzimuth);
            }


            public void DiscoverArticulationLimits()
            {
                AzimuthRequestAbsolute(TAU / 4);
                AzimuthLimitUpper = AzimuthCurrentAbsolute();

                AzimuthRequestAbsolute(-TAU / 4);
                AzimuthLimitLower = AzimuthCurrentAbsolute();
         
                const double headingIncrement = TAU/16;
                double headingOffset;
                for (headingOffset = 0.0f; headingOffset <= (Math.PI + headingIncrement/2); headingOffset += headingIncrement)
                {
                    HeadingRequestRelative(headingOffset);
                    if (Math.Abs(HeadingCurrentRelative() - headingOffset) > EpsilonHeading)
                        { break; }
                }
                HeadingLimitRelativeCcw = HeadingCurrentRelative();

                for (headingOffset = 0.0f; headingOffset <= (Math.PI + headingIncrement/2); headingOffset -= headingIncrement)
                {
                    HeadingRequestRelative(headingOffset);
                    if (Math.Abs(HeadingCurrentRelative() - headingOffset) > EpsilonHeading)
                        { break; }
                }
                HeadingLimitRelativeCw = HeadingCurrentRelative();
            }


            // Returns Milliseconds we expect to make the heading move...
            private int EstimatedMaxReactionTime(double headingRadiansDesired, double headingRadiansCurrent)
            {
                // Assume slowest unit can turn a full circle in 8 seconds...
                const double expectedWorseCaseTurnRate = TAU / 8000;

                // We want the smaller of the angle or its complement...
                double movementAngle = Math.Abs(headingRadiansDesired - headingRadiansCurrent);
                double maxRadianMovement = Math.Min(movementAngle, (TAU - movementAngle));

                return (int)(maxRadianMovement / expectedWorseCaseTurnRate);
            }

            public double HeadingCurrentAbsolute()
            {
                return _headingObserver.Rotation;
            }


            private double HeadingCurrentRelative()
            {
                return HeadingCurrentAbsolute() - HeadingBaseAbsolute;
            }


            public void HeadingRequestAbsolute(double absoluteRadianHeading)
            {
                int estimatedMaxReactionTime = EstimatedMaxReactionTime(absoluteRadianHeading, HeadingCurrentAbsolute());

                absoluteRadianHeading = WoWMathHelper.NormalizeRadian((float)absoluteRadianHeading);
                _maxReactionTimer.Restart();

                while ((_maxReactionTimer.ElapsedMilliseconds < estimatedMaxReactionTime)
                        && (Math.Abs(HeadingCurrentAbsolute() - absoluteRadianHeading) > EpsilonHeading))
                {
                    StyxWoW.Me.SetFacing((float)absoluteRadianHeading);   
                }
            }


            private void HeadingRequestRelative(double relativeRadianHeading)
            {
                HeadingRequestAbsolute((float)(relativeRadianHeading + HeadingBaseAbsolute));
            }


            public bool IsWithinAzimuthArticulation_Absolute(double desiredAzimuth)
            {
                // In WoW, larger Azimuths are "up"...
                return (desiredAzimuth >= AzimuthLimitLower) && (desiredAzimuth <= AzimuthLimitUpper);
            }

            public override string ToString()
            {
                return string.Format("<ArticulationLimitsType"
                    + " AzimuthLimitUpper=\"{0}\" AzimuthLimitLower=\"{1}\""
                    + " HeadingLimitRelativeCcw=\"{2}\" HeadingLimitRelativeCw=\"{3}\" />",
                    AzimuthLimitUpper, AzimuthLimitLower, HeadingLimitRelativeCcw, HeadingLimitRelativeCw);
            }
            
            private double AzimuthLimitLower { get; set; }       // azimuth in Radians
            private double AzimuthLimitUpper { get; set; }       // azimuth in Radians
            private double AzimuthBaseAbsolute { get; set; }
            private double HeadingBaseAbsolute { get; set; }
            private double HeadingLimitRelativeCcw { get; set; } // relative heading in Radians
            private double HeadingLimitRelativeCw { get; set; }  // relative heading in Radians
            private readonly WoWObject _headingObserver = (StyxWoW.Me.Transport ?? StyxWoW.Me);
            private readonly Stopwatch _maxReactionTimer = new Stopwatch();
            private const double EpsilonAzimuth = 0.05;   // radians
            private const double EpsilonHeading = 0.05;   // radians

        }
        #endregion


        #region Math helpers
        public double? CalculateBallisticLaunchAngle(
            WoWUnit target,
            double muzzleVelocityInFps,
            VehicleWeaponMoverType vehicleWeaponMover)
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
            if (vehicleWeaponMover.IsWithinAzimuthArticulation_Absolute(root))
                { return root; }

            // First root provides no solution, try second root...
            root = Math.Atan((v0Sqr + radicalTerm) / (g * distance2D));
            if (vehicleWeaponMover.IsWithinAzimuthArticulation_Absolute(root))
                { return root; }

            // Both solutions are out of the vehicle's articulation capabilities, return "no solution"...
            return null;
        }
        #endregion


        #region XML parsing
        private class QuestGoalType : QuestBehaviorXmlBase
        {
            public static QuestGoalType Create(XElement xElement)
            {
                return new QuestGoalType(xElement);
            }

            private QuestGoalType(XElement xElement)
                : base(xElement)
            {
                try
                {
                    QuestObjectiveIndex = GetAttributeAsNullable<int>("QuestObjectiveIndex", true, new ConstrainTo.Domain<int>(1, 5), null) ?? 0;
                    MobIds = GetNumberedAttributesAsArray<int>("MobId", 1, ConstrainAs.MobId, null);
                    ActionBarIndex_Attack = GetNumberedAttributesAsArray<int>("ActionBarIndex_Attack", 1, ConstrainAs.HotbarButton, null);
                }

                catch (Exception except)
                {
                    QBCLog.Error("[PROFILE PROBLEM with \"{0}\"]: {1}\nFROM HERE:\n{2}\n",
                        xElement.ToString(), except.Message, except.StackTrace);
                    IsAttributeProblem = true;
                }
            }

            public bool IsGoalComplete(int questId)
            {
                return Me.IsQuestObjectiveComplete(questId, QuestObjectiveIndex);
            }


            public int QuestObjectiveIndex { get; private set; }
            public int[] MobIds { get; private set; }
            public int[] ActionBarIndex_Attack { get; private set; }
        }
        #endregion
    }
}