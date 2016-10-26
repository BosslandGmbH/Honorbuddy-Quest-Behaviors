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
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Xml.Linq;
using Bots.Grind;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
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


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.UnmaskingTheYaungol
{
    [CustomBehaviorFileName(@"SpecificQuests\30690-KunLai-UnmaskingTheYaungol")]
    public class UnmaskingTheYaungol : QuestBehaviorBase
    {
        #region Consructor and Argument Processing
        public UnmaskingTheYaungol(Dictionary<string, string> args)
            : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            try
            {
                QuestId = 30690; // http://wowhead.com/quest=30690
                QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = QuestInLogRequirement.InLog;

                WaitPoint = new Vector3(2086.154f, 2019.164f, 453.4554f).FanOutRandom(4.0);
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
                KobaiSafePullAreaAnchor = new Vector3(2062.548f, 2019.029f, 452.4345f);
                KobaiSafePullAreaRadius = 20.0;
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


        // Variables for Attributes provided by caller

        public int AuraId_StealMask { get; private set; }
        public int GameObjectId_BlindingRageTrap { get; private set; }
        public int ItemId_BlindingRageTrap { get; private set; }
        public Vector3 KobaiSafePullAreaAnchor { get; private set; }
        public double KobaiSafePullAreaRadius { get; private set; }
        public int MobId_Kobai { get; private set; }
        public int MobId_MalevolentFury { get; private set; }
        public double ToonHealthPercentSafetyLevel { get; private set; }
        public Vector3 WaitPoint { get; private set; }

        // DON'T EDIT THIS--it is auto-populated by Git
        protected override string GitId => "$Id$";
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
                            && unit.IsUntagged()
                    select unit;
            }
        }

        private BattlefieldContext _combatContext = null;
        #endregion

        #region Overrides of CustomForcedBehavior

        protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
        {
        }

        protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
        {
        }

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

            if ((QuestId != 0) && (quest == null))
            {
                QBCLog.Error("This behavior has been associated with QuestId({0}), but the quest is not in our log", QuestId);
                IsAttributeProblem = true;
            }

            // If the needed item is not in my inventory, report problem...
            if (!Me.BagItems.Any(i => ItemId_BlindingRageTrap == (int)i.Entry))
            {
                QBCLog.Error("The behavior requires \"Blind Rage Trap\"(ItemId: {0}) to be in our bags; however, it cannot be located)",
                    ItemId_BlindingRageTrap);
                IsAttributeProblem = true;
            }


            // Let QuestBehaviorBase do basic initialization of the behavior, deal with bad or deprecated attributes,
            // capture configuration state, install BT hooks, etc.  This will also update the goal text.
            var isBehaviorShouldRun = OnStart_QuestBehaviorCore();

            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (isBehaviorShouldRun)
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
                // don't pull anything unless we absolutely must
                LevelBot.BehaviorFlags &= ~BehaviorFlags.Pull;

                _combatContext = new BattlefieldContext(ItemId_BlindingRageTrap, GameObjectId_BlindingRageTrap);
                this.UpdateGoalText(QuestId, "Looting and Harvesting are disabled while behavior in progress");
            }
        }

        #endregion


        #region Main Behavior

        protected override Composite CreateBehavior_CombatMain()
        {
            return new ActionRunCoroutine(ctx => CombatMainLogic());
        }

        protected async Task<bool> CombatMainLogic()
        {
            if (IsDone)
                return false;
            _combatContext.Update(MobId_Kobai, MobId_MalevolentFury);
            if (Me.Combat)
            {
                // If the Blind Rage Trap is not on cooldown, move right next to Kobai and use it...
                // NB: We don't want to drop the trap unless we're pounding on Kobai
                if ((_combatContext.Kobai != null)
                    && (Me.CurrentTarget == _combatContext.Kobai)
                    && (_combatContext.BlindingRageTrap != null)
                    && (_combatContext.BlindingRageTrap.CooldownTimeLeft <= TimeSpan.Zero))
                {
                    QBCLog.Info("Using Blinding Rage Trap");

                    if (!_combatContext.Kobai.IsWithinMeleeRange)
                        return await UtilityCoroutine.MoveTo(_combatContext.Kobai.Location, "Kobai", MovementByType.NavigatorOnly);

                    if (Me.IsMoving)
                        await CommonCoroutines.StopMoving();

                    if (!Me.IsSafelyFacing(_combatContext.Kobai))
                    {
                        _combatContext.Kobai.Face();
                        await Coroutine.Sleep(Delay.LagDuration.Milliseconds);
                    }
                    await Coroutine.Sleep(Delay.BeforeButtonClick.Milliseconds);
                    _combatContext.BlindingRageTrap.Use();
                    await Coroutine.Sleep(Delay.AfterItemUse.Milliseconds);
                    return true;
                }
                // "Steal Mask" aura...
                // If Kobai is blinded by rage, and the Malevolent Fury is not on the battlefield,
                // move right next to Kobai, and steal the mask...
                // NB: We only want to cause one Malevolet Fury to spawn.  If we click multiple times
                // then we get more.  So, only click if Fury is not already up.
                if ((_combatContext.Kobai != null)
                    && Me.HasAura(AuraId_StealMask)
                    && (_combatContext.MalevolentFury == null))
                {
                    QBCLog.Info("Pilfering Mask");

                    if (!_combatContext.Kobai.IsWithinMeleeRange)
                        return await UtilityCoroutine.MoveTo(_combatContext.Kobai.Location, "Kobai", MovementByType.NavigatorOnly);

                    if (Me.CurrentTargetGuid != _combatContext.Kobai.Guid)
                    {
                        _combatContext.Kobai.Target();
                        if (!await Coroutine.Wait(
                            2000,
                            () => _combatContext.Kobai.IsValid
                                && Me.CurrentTarget == _combatContext.Kobai))
                        {
                            return false;
                        }
                    }
                    Lua.DoString("ExtraActionButton1:Click()");
                    await Coroutine.Sleep(Delay.AfterItemUse.Milliseconds);
                    return true;
                }
            }
            // If we're not in combat, but have found Kobai, move to engage him...
            else
            {
                if (_combatContext.Kobai != null)
                {
                    // If Kobai is not in kill zone...
                    if (_combatContext.Kobai.Location.Distance(KobaiSafePullAreaAnchor) > KobaiSafePullAreaRadius)
                    {
                        if (await UtilityCoroutine_MoveToStartPosition())
                            return true;

                        // Wait for Kobai to arrive...
                        QBCLog.Info("Waiting for Kobai to move into kill zone (dist: {0:F1})",
                                Math.Max(_combatContext.Kobai.Location.Distance(KobaiSafePullAreaAnchor) - KobaiSafePullAreaRadius, 0.0));
                        await Coroutine.Wait(5000, () => Me.Combat);
                        return true;
                    }

                    // Kobai in kill zone, pull him...
                    if (_combatContext.Kobai.Location.Distance(KobaiSafePullAreaAnchor) <= KobaiSafePullAreaRadius)
                    {
                        if (BotPoi.Current.Type != PoiType.Kill)
                        {
                            QBCLog.Info("Engaging Kobai");
                            BotPoi.Current = new BotPoi(_combatContext.Kobai, PoiType.Kill);
                            if (Me.CurrentTarget != _combatContext.Kobai)
                            {
                                _combatContext.Kobai.Target();
                                await Coroutine.Sleep(Delay.LagDuration.Milliseconds);
                                return true;
                            }
                        }
                        return false;
                    }

                    // Can't find Kobai--must've just been killed--wait for repop...
                    if (_combatContext.Kobai == null && Navigator.AtLocation(WaitPoint))
                    {
                        QBCLog.Info("Waiting for Kobai to respawn");
                        await Coroutine.Wait(5000, () => Me.Combat);
                        return true;
                    }
                }

                if (!UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete))
                {
                    BehaviorDone("Finished");
                    return true;
                }

                // Move to start position, if needed...
                return await UtilityCoroutine_MoveToStartPosition();
            }

            return false;
        }

        #endregion


        #region Helpers

        private async Task<bool> UtilityCoroutine_MoveToStartPosition()
        {
            if (Navigator.AtLocation(WaitPoint))
                return false;

            return await UtilityCoroutine.MoveTo(WaitPoint, "Moving to start position", MovementByType.NavigatorOnly);
        }

        #endregion // Coroutine helpers
    }
}

