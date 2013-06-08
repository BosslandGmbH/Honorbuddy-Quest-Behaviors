// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Usings
using System;

using CommonBehaviors.Actions;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public abstract partial class QuestBehaviorBase
    {
        /// <summary>
        /// <para>Uses item defined by WOWITEMDELEGATE on target defined by SELECTEDTARGETDELEGATE.</para>
        /// <para>Notes:<list type="bullet">
        /// <item><description><para>* It is up to the caller to assure that all preconditions have been met for
        /// using the item (i.e., the target is in range, the item is off cooldown, etc).</para></description></item>
        /// <item><description><para> * If item use was successful, BT is provided with RunStatus.Success;
        /// otherwise, RunStatus.Failure is returned (e.g., item is not ready for use,
        /// item use was interrupted by combat, etc).</para></description></item>
        /// <item><description><para>* It is up to the caller to blacklist the target, or select a new target
        /// after successful item use.</para></description></item>
        /// </list></para>
        /// </summary>
        /// <param name="selectedTargetDelegate">may NOT be null.  The target provided by the delegate should be viable.</param>
        /// <param name="wowItemDelegate">may NOT be null.  The item provided by the delegate should be viable, and ready for use.</param>
        /// <returns></returns>
        public Composite UtilityBehaviorSeq_UseItemOn(ProvideWoWItemDelegate wowItemDelegate,
                                                     ProvideWoWObjectDelegate selectedTargetDelegate)
        {
            Contract.Requires(selectedTargetDelegate != null, context => "selectedTargetDelegate != null");
            Contract.Requires(wowItemDelegate != null, context => "wowItemDelegate != null");

            return new Sequence(
                new DecoratorContinue(context => !IsViable(_ubseqUseItemOn_SelectedTarget = selectedTargetDelegate(context)),
                    new Action(context =>
                    {
                        QBCLog.Warning("Target is not viable!");
                        return RunStatus.Failure;                        
                    })),

                new DecoratorContinue(context => !IsViable(_ubseqUseItemOn_ItemToUse = wowItemDelegate(context)),
                    new Action(context =>
                    {
                        QBCLog.Warning("We do not possess the item to use on {0}!", _ubseqUseItemOn_SelectedTarget.Name);
                        return RunStatus.Failure;
                    })),

                new DecoratorContinue(context => !_ubseqUseItemOn_ItemToUse.Usable,
                    new Action(context =>
                    {
                        QBCLog.Warning("{0} is not usable (yet).", _ubseqUseItemOn_ItemToUse.Name);
                        return RunStatus.Failure;
                    })),

                // Use the item...
                new Action(context =>
                {
                    // Set up 'interrupted use' detection...
                    // MAINTAINER'S NOTE: Once these handlers are installed, make sure all possible exit paths from the outer
                    // Sequence unhook these handlers.  I.e., if you plan on returning RunStatus.Failure, be sure to call
                    // UtilityBehaviorSeq_UseItemOn_HandlersUnhook() first.
                    UtilityBehaviorSeq_UseItemOn_HandlersHook();

                    // Notify user of intent...
                    var message = string.Format("Using '{0}' on '{1}'",
                                                _ubseqUseItemOn_ItemToUse.Name,
                                                _ubseqUseItemOn_SelectedTarget.Name);

                    var selectedTargetAsWoWUnit = _ubseqUseItemOn_SelectedTarget as WoWUnit;
                    if (selectedTargetAsWoWUnit != null)
                        { message += string.Format(" (health: {0:F1})", selectedTargetAsWoWUnit.HealthPercent); }

                    QBCLog.Info(message);

                    // Do it...
                    _ubseqUseItemOn_IsUseItemInterrupted = false;    
                    _ubseqUseItemOn_ItemToUse.Use(_ubseqUseItemOn_SelectedTarget.Guid);
                }),
                new WaitContinue(Delay.AfterItemUse, context => false, new ActionAlwaysSucceed()),

                // If item use requires a second click on the target (e.g., item has a 'ground target' mechanic)...
                new DecoratorContinue(context => StyxWoW.Me.CurrentPendingCursorSpell != null,
                    new Sequence(
                        new Action(context => { SpellManager.ClickRemoteLocation(_ubseqUseItemOn_SelectedTarget.Location); }),
                        new WaitContinue(Delay.AfterItemUse,
                            context => StyxWoW.Me.CurrentPendingCursorSpell == null,
                            new ActionAlwaysSucceed()),
                        // If we've leftover spell cursor dangling, clear it...
                        // NB: This can happen for "use item on location" type activites where you get interrupted
                        // (e.g., a walk-in mob).
                        new DecoratorContinue(context => StyxWoW.Me.CurrentPendingCursorSpell != null,
                            new Action(context => { Lua.DoString("SpellStopTargeting()"); }))       
                    )),

                // Wait for any casting to complete...
                // NB: Some interactions or item usages take time, and the WoWclient models this as spellcasting.
                new WaitContinue(TimeSpan.FromSeconds(15),
                    context => !(Me.IsCasting || Me.IsChanneling),
                    new ActionAlwaysSucceed()),

                // Were we interrupted in item use?
                new Action(context => { UtilityBehaviorSeq_UseItemOn_HandlersUnhook(); }),
                new DecoratorContinue(context => _ubseqUseItemOn_IsUseItemInterrupted,
                    new Sequence(
                        new Action(context => { QBCLog.DeveloperInfo("Use of {0} interrupted.", _ubseqUseItemOn_ItemToUse.Name); }),
                        // Give whatever issue encountered a chance to settle...
                        // NB: Wait, not WaitContinue--we want the Sequence to fail when delay completes.
                        new Wait(TimeSpan.FromMilliseconds(1500), context => false, new ActionAlwaysFail())
                    ))
            );
        }
        private bool _ubseqUseItemOn_IsUseItemInterrupted;
        private WoWItem _ubseqUseItemOn_ItemToUse;
        private WoWObject _ubseqUseItemOn_SelectedTarget;

        private void UtilityBehaviorSeq_UseItemOn_HandleUseItemInterrupted(object sender, LuaEventArgs args)
        {
            if (args.Args[0].ToString() == "player")
            {
                QBCLog.DeveloperInfo("Interrupted via {0} Event.", args.EventName);
                _ubseqUseItemOn_IsUseItemInterrupted = true;
            }
        }

        private void UtilityBehaviorSeq_UseItemOn_HandlersHook()
        {
            Lua.Events.AttachEvent("UNIT_SPELLCAST_CHANNEL_UPDATE", UtilityBehaviorSeq_UseItemOn_HandleUseItemInterrupted);
            Lua.Events.AttachEvent("UNIT_SPELLCAST_FAILED", UtilityBehaviorSeq_UseItemOn_HandleUseItemInterrupted);
            Lua.Events.AttachEvent("UNIT_SPELLCAST_INTERRUPTED", UtilityBehaviorSeq_UseItemOn_HandleUseItemInterrupted);
        }

        private void UtilityBehaviorSeq_UseItemOn_HandlersUnhook()
        {
            Lua.Events.DetachEvent("UNIT_SPELLCAST_CHANNEL_UPDATE", UtilityBehaviorSeq_UseItemOn_HandleUseItemInterrupted);
            Lua.Events.DetachEvent("UNIT_SPELLCAST_FAILED", UtilityBehaviorSeq_UseItemOn_HandleUseItemInterrupted);
            Lua.Events.DetachEvent("UNIT_SPELLCAST_INTERRUPTED", UtilityBehaviorSeq_UseItemOn_HandleUseItemInterrupted);    
        }
    }
}