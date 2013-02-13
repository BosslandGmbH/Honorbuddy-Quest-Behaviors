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
// 30978-TownlongSteppes-HostileSkies.cs is a point-solution behavior.
// The behavior:
//  1) Moves to Nurong's Cannon & picks it up to "enter the vehicle"
//  2) Selects targets and start shooting them down.
//      Killing the Voress'thalik will be preferred over Korthik Swarmers
//      anytime it is up.
//  3) Profit!
// 
// THINGS TO KNOW:
// * We completely disable combat for this behavior
//      Combat is unnecessary while in this vehicle, and it allows
//      the behavior to run a little more responsively being
//      hooked 'higher up' in the behavior tree.
//
// EXAMPLE:
//     <CustomBehavior File="30978-TownlongSteppes-HostileSkies" />
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
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using System.Text;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.QuestBehaviors.HostileSkies
{
    public class HostileSkies : CustomForcedBehavior
    {
        #region Consructor and Argument Processing
        public HostileSkies(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                QuestId = 30978; // http://wowhead.com/quest=30978
                QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = QuestInLogRequirement.InLog;
                QuestObjectiveIndex_KorthikSwarmer = 1; // http://wowhead.com/quest=30991
                QuestObjectiveIndex_Voressthalik = 2; // http://wowhead.com/quest=30991

                MobId_KorthikSwarmer = 62300; // http://wowhead.com/npc=62300
                MobId_Voressthalik = 62269; // http://wowhead.com/npc=62269
                MobId_NurongsCannon = 62747; // http://wowhead.com/npc=62747

                VehicleId_NurongsCannon = 62302; // http://wowhead.com/npc=62302
                NurongsCannonShot_LuaCommand = "if GetPetActionCooldown(1) == 0 then CastPetAction(1) end"; // http://www.wowhead.com/npc=62302

                // Cannon Articulation (these were determined empirically)...
                CannonArticulation_AzimuthMax = 0.75;   // radians
                CannonArticulation_AzimuthMin = 0.10;   // radians
                CannonArticulation_HeadingMin = 3.8;    // radians
                CannonArticulation_HeadingMax = 5.5;    // radians
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
        public double CannonArticulation_AzimuthMin { get; private set; }
        public double CannonArticulation_AzimuthMax { get; private set; }
        public double CannonArticulation_HeadingMin { get; private set; }
        public double CannonArticulation_HeadingMax { get; private set; }
        public int MobId_KorthikSwarmer { get; private set; }
        public int MobId_NurongsCannon { get; private set; }
        public int MobId_Voressthalik { get; private set; }
        public string NurongsCannonShot_LuaCommand { get; private set; }
        public int QuestId { get; private set; }
        public int QuestObjectiveIndex_KorthikSwarmer { get; private set; }
        public int QuestObjectiveIndex_Voressthalik { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }
        public int VehicleId_NurongsCannon { get; private set; }


        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return "$Id$"; } }
        public override string SubversionRevision { get { return "$Rev$"; } }
        #endregion


        #region Private and Convenience variables
        private WoWPoint CannonVehicleLocation { get; set; }
        private LocalPlayer Me { get { return StyxWoW.Me; } }
        private WoWUnit SelectedTarget { get; set; }

        private Composite _behaviorTreeHook_Combat = null;
        private Composite _behaviorTreeHook_Main = null;
        private ConfigMemento _configMemento = null;
        private bool _isBehaviorDone = false;
        private bool _isDisposed = false;
        #endregion


        #region Destructor, Dispose, and cleanup
        ~HostileSkies()
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

                // NB: we don't unhook _behaviorTreeHook_Main
                // This was installed when HB created the behavior, and its up to HB to unhook it

                if (_behaviorTreeHook_Combat != null)
                {
                    TreeHooks.Instance.RemoveHook("Combat_Main", _behaviorTreeHook_Combat);
                    _behaviorTreeHook_Combat = null;
                }

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

                _behaviorTreeHook_Combat = CreateCombatBehavior();
                TreeHooks.Instance.InsertHook("Combat_Main", 0, _behaviorTreeHook_Combat);
            }
        }
        #endregion


        #region Main Behavior
        protected Composite CreateCombatBehavior()
        {
            // Nothing to do, for now...
            return new ActionAlwaysFail();
        }


        public Composite CreateMainBehavior()
        {
            // NB: We need to allow lower BT nodes to run when the behavior is finished; otherwise, HB will not
            // process the change of _isBehaviorDone state.
            return new PrioritySelector(

                // If quest is done, behavior is done...
                new Decorator(context => IsDone,
                    new Action(context =>
                    {
                        _isBehaviorDone = true;
                        LogMessage("info", "Finished");
                    })),

                // If not using cannon, get in cannon vehicle...
                new Decorator(context => !Me.InVehicle,
                    new PrioritySelector(cannonContext => FindUnitsFromId(MobId_NurongsCannon).FirstOrDefault(),
                        // If unable to locate cannon, warn user and stop...
                        new Decorator(cannonContext => cannonContext == null,
                            new Action(cannonContext =>
                            {
                                LogMessage("error", "PROFILE ERROR: Nurong's Canon is not in the area--please repair profile");
                                TreeRoot.Stop();
                                _isBehaviorDone = true;
                            })),

                        // Move close enough, and interact with cannon...
                        new Decorator(cannonContext => ((WoWUnit)cannonContext).Distance > ((WoWUnit)cannonContext).InteractRange,
                            new Action(cannonContext => { Navigator.MoveTo(((WoWUnit)cannonContext).Location); })),
                        new Decorator(cannonContext => !Me.IsFacing((WoWUnit)cannonContext),
                            new Action(cannonContext => { ((WoWUnit)cannonContext).Face(); })),
                        new Decorator(cannonContext => Me.IsMoving,
                            new Action(cannonContext => { WoWMovement.MoveStop(); })),
                        new Decorator(cannonContext => !Me.InVehicle,
                            new Action(cannonContext => { ((WoWUnit)cannonContext).Interact(); }))
                    )),

                new Decorator(context => CannonVehicleLocation == WoWPoint.Empty,
                    new Action(context => { CannonVehicleLocation = FindUnitsFromId(VehicleId_NurongsCannon).FirstOrDefault().Location; })),

                // Ready...
                new Decorator(context => (SelectedTarget == null) || !SelectedTarget.IsValid || SelectedTarget.IsDead
                                            || Blacklist.Contains(SelectedTarget, BlacklistFlags.Combat),
                    new Action(context =>
                    {   
                        if (SelectedTarget != null)
                            { WoWMovement.ConstantFaceStop(SelectedTarget.Guid); }
                        Me.ClearTarget();

                        SelectedTarget = ChooseTarget();
                    })),

                // Aim...
                // If we can't successfully fire at target, go find another...
                new Decorator(context => !IsWithinArticulationLimits(SelectedTarget),
                    new Action(context => { Blacklist.Add(SelectedTarget, BlacklistFlags.Combat, TimeSpan.FromSeconds(60)); })),
                    
                // Fire!
                new Action(context =>
                {
                    SelectedTarget.Target();
                    AimCannon();
                    Lua.DoString(NurongsCannonShot_LuaCommand);
                })
            );
        }
        #endregion


        #region Helpers

        // NB: In WoW, larger headings are to left, and larger Azimuths are up
        private void AimCannon()
        {
            if ((SelectedTarget == null) || !SelectedTarget.IsValid)
                { return; }

            // Handle heading...
            WoWMovement.ConstantFace(SelectedTarget.Guid);

            // Handle Azimuth...
            // "Location" is measured at the feet of the toon.  We want to aim for the 'middle' of the toon's
            // height.
            double currentAzimuth = NormalizeAngleToPi(Lua.GetReturnVal<double>("return VehicleAimGetAngle();", 0));
            double neededAzimuth = Math.Atan((SelectedTarget.Location.Z - CannonVehicleLocation.Z
                                             + (GetBoundingHeight(SelectedTarget) / 2))   // Middle of toon height
                                            / CannonVehicleLocation.Distance2D(SelectedTarget.Location));
            neededAzimuth = NormalizeAngleToPi(neededAzimuth);

            // VehicleAimIncrement() handles negative values of 'increment' correctly...
            Lua.DoString("VehicleAimIncrement({0});", (neededAzimuth - currentAzimuth));
        }


        private WoWUnit ChooseTarget()
        {
            WoWUnit target = null;

            // Prefer the Voress'thalik, if its up...
            if (!IsQuestObjectiveComplete(QuestId, QuestObjectiveIndex_Voressthalik))
            {
                target =
                   (from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
                    where
                        unit.IsValid
                        && (unit.Entry == MobId_Voressthalik)
                        && unit.IsAlive
                        && !Blacklist.Contains(unit, BlacklistFlags.Combat)
                        && IsWithinArticulationLimits(unit)
                    select unit)
                    .FirstOrDefault();
            }


            // If no Voress'thalik, check for Swarmers...
            if ((target == null) && !IsQuestObjectiveComplete(QuestId, QuestObjectiveIndex_KorthikSwarmer))
            {
                target =
                   (from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
                    where
                        unit.IsValid
                        && (unit.Entry == MobId_KorthikSwarmer)
                        && unit.IsAlive
                        && !Blacklist.Contains(unit, BlacklistFlags.Combat)
                        && IsWithinArticulationLimits(unit)
                    orderby // prefer units that are "clustered"
                        FindUnitsSurroundingTarget(unit, 25.0).Count() descending, unit.Distance
                    select unit)
                    .FirstOrDefault();
            }

            if (target != null)
                { target.Target(); }

            return target;
        }


        private IEnumerable<WoWUnit> FindUnitsFromId(int unitId)
        {
            return
                from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
                where (unit.Entry == unitId) && unit.IsAlive
                        && (unit.TaggedByMe || unit.TappedByAllThreatLists || !unit.TaggedByOther)
                select unit;
        }


        private IEnumerable<WoWUnit> FindUnitsSurroundingTarget(WoWUnit target, double surroundRadius)
        {
            return
                from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
                where
                    (target.Entry == unit.Entry)
                    && unit.IsAlive
                    && (target.Location.Distance(unit.Location) < surroundRadius)
                select unit;
        }


        private double GetBoundingHeight(WoWUnit unit)
        {
            // The WoWclient lies about the height of the Voress'thalik unit...
            // This causes us to aim too low and do little or no damage.
            // We correct the WoWclient lies here.
            if (unit.Entry == MobId_Voressthalik)
                { return 30.0; }

            return unit.BoundingHeight;
        }
        

        private bool IsQuestObjectiveComplete(int questId, int objectiveId)
        {
            if (Me.QuestLog.GetQuestById((uint)questId) == null)
                { return false; }

            int questLogIndex = Lua.GetReturnVal<int>(string.Format("return GetQuestLogIndexByID({0})", questId), 0);

            return
                Lua.GetReturnVal<bool>(string.Format("return GetQuestLogLeaderBoard({0},{1})", objectiveId, questLogIndex), 2);
        }


        private bool IsWithinArticulationLimits(WoWUnit potentialTarget)
        {
            if ((potentialTarget == null) || !potentialTarget.IsValid)
                { return false; }

            double neededAzimuth = Math.Atan((potentialTarget.Location.Z - CannonVehicleLocation.Z)
                                / CannonVehicleLocation.Distance2D(potentialTarget.Location));
            double neededFacing = WoWMathHelper.CalculateNeededFacing(CannonVehicleLocation, potentialTarget.Location);
            neededFacing = WoWMathHelper.NormalizeRadian((float)neededFacing);

            if ((neededFacing < CannonArticulation_HeadingMin) || (neededFacing > CannonArticulation_HeadingMax))
                { return false; }

            if ((neededAzimuth < CannonArticulation_AzimuthMin) || (neededAzimuth > CannonArticulation_AzimuthMax))
                { return false; }

            return true;
        }


        /// <summary>
        /// Returns the normalized ANGLEINRADIANS to the closed interval [-PI..+PI]
        /// </summary>
        /// <param name="radians"></param>
        /// <returns></returns>
        public double NormalizeAngleToPi(double angleInRadians)
        {
            while (angleInRadians > Math.PI)  { angleInRadians -= (2 * Math.PI); }
            while (angleInRadians < -Math.PI) { angleInRadians += (2 * Math.PI); }
            return (angleInRadians);
        }

        #endregion // Behavior helpers
    }
}

