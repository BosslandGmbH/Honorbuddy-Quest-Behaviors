// Behavior originally contributed by Natfoth.
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
// WIKI DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_FireFromTheSky
//
// QUICK DOX:
//      Used for the Dwarf Quest SI7: Fire From The Sky
//
//  Notes:
//      * Make sure to Save Gizmo.
//
#endregion


#region Examples
#endregion


#region Usings

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Bots.DungeonBuddy.Helpers;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Frames;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.TakeNoPrisoners
{
    [CustomBehaviorFileName(@"SpecificQuests\29727-JadeForest-TakeNoPrisoners")]
    public class JadeForestTakeNoPrisoners : QuestBehaviorBase
    {
        public JadeForestTakeNoPrisoners(Dictionary<string, string> args)
            : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            try
            {
                QuestId = GetAttributeAsNullable("QuestId", false, ConstrainAs.QuestId(this), null) ?? 29727;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
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

        // Private variables for internal state

        // Private properties
        private const int MobId_SullysBombBarrel = 55410;

        public static int[] MobIds = new[] { 55411, 55484, 55473, 55505, 55485, MobId_SullysBombBarrel };
        public static int[] OrcIds = new[] { 55498, 55501, 55499 };

        public static WoWPoint TurretLocation = new WoWPoint(1116.968f, -544.0963f, 413.5516f);
        public static WoWPoint UsingTurretLocation = new WoWPoint(1296.96f, -430.156f, 314.718f);

        public WoWUnit HozenEnemy
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => MobIds.Contains((int)u.Entry) && !u.IsDead)
                                     .OrderBy(u => u.Distance).FirstOrDefault());
            }
        }

        public WoWUnit OrcEnemy
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => OrcIds.Contains((int)u.Entry) && !u.IsDead)
                                     .OrderBy(u => u.DistanceSqr).FirstOrDefault());
            }
        }

        public WoWGameObject Turret
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWGameObject>()
                                     .Where(u => u.Entry == 209621)
                                     .OrderBy(u => u.DistanceSqr).FirstOrDefault());
            }
        }

        public WoWUnit Amber
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => u.Entry == 55283 && !u.IsDead)
                                     .OrderBy(u => u.Distance).FirstOrDefault());
            }
        }

        // DON'T EDIT THIS--it is auto-populated by Git
        protected override string GitId => "$Id$";


        #region Overrides of CustomForcedBehavior

        protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
        {
        }

        protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
        {
        }

        protected override Composite CreateBehavior_CombatMain()
        {
            return new ActionRunCoroutine(ctx => Coroutine_CombatMain());
        }

        private readonly WoWPoint _vehicleLoc = new WoWPoint(-157.5062f, -2659.278f, 1.069468f);

        private async Task<bool> Coroutine_CombatMain()
        {
            if (IsDone)
                return false;

            if (!Query.IsInVehicle())
            {
                // only move to vehicle if doing nothing else
                if (!Targeting.Instance.IsEmpty() || BotPoi.Current.Type != PoiType.None)
                    return false;

                WoWUnit amber = Amber;
                // Wait for Amber to load in the ObjectMananger.
                if (amber == null || !amber.WithinInteractRange)
                {
                    var moveTo = amber != null ? amber.Location : _vehicleLoc;
                    await UtilityCoroutine.MoveTo(moveTo, "Moving to Start Amber(Human) Story", MovementBy);
                    return true;
                }

                if (await CommonCoroutines.StopMoving())
                    return true;

                if (!GossipFrame.Instance.IsVisible)
                {
                    amber.Interact();
                    await CommonCoroutines.SleepForRandomUiInteractionTime();
                    return true;
                }

                if (GossipFrame.Instance.GossipOptionEntries != null)
                {
                    GossipFrame.Instance.SelectGossipOption(0);
                    await CommonCoroutines.SleepForRandomUiInteractionTime();
                    return true;
                }
                return true;
            }

            if (await InteractWithUnit(HozenEnemy))
                return true;

            if (await InteractWithUnit(OrcEnemy))
                return true;

            if (!Me.HasAura("See Quest Invis 5"))
            {
                var turret = Turret;
                if (TurretLocation.DistanceSqr(Me.Location) > 3 * 3)
                {
                    await UtilityCoroutine.MoveTo(TurretLocation, "Turret Location", MovementBy);
                    return true;
                }
                if (turret == null)
                {
                    TreeRoot.StatusText = "Waiting for turret to spawn";
                    return true;
                }

                if (!turret.WithinInteractRange)
                {
                    await UtilityCoroutine.MoveTo(TurretLocation, "interact range of turret", MovementBy);
                    return true;
                }

                if (await CommonCoroutines.StopMoving())
                    return true;

                QBCLog.Info("Using turret");
                Turret.Interact();
                return true;
            }
            return false;
        }


        private async Task<bool> InteractWithUnit(WoWUnit unit)
        {
            if (unit == null)
                return false;

            // Wait for sully to move away from the bombs so we don't blow him up.
            if (unit.Entry == MobId_SullysBombBarrel)
                await Coroutine.Sleep(4000);

            unit.Interact();
            await CommonCoroutines.SleepForRandomUiInteractionTime();
            return true;
        }

        #endregion
    }
}
