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
using System.Collections.Generic;

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
    public partial class UtilityBehaviorSeq
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
        /// <param name="selectedTargetDelegate"> may NOT be null.  The target provided by the delegate should be viable.</param>
        /// <param name="wowItemDelegate"> may NOT be null.  The item provided by the delegate should be viable, and ready for use.</param>
        /// <returns></returns>
        public class CastSpell : Sequence
        {
            public CastSpell(ProvideIntDelegate spellIdDelegate,
                             ProvideWoWObjectDelegate selectedTargetDelegate)
            {
                Contract.Requires(spellIdDelegate != null, context => "spellDelegate != null");
                Contract.Requires(selectedTargetDelegate != null, context => "selectedTargetDelegate != null");

                SpellIdDelegate = spellIdDelegate;
                SelectedTargetDelegate = selectedTargetDelegate;

                Children = CreateChildren();
            }


            // BT contruction-time properties...
            private ProvideIntDelegate SpellIdDelegate { get; set; }
            private ProvideWoWObjectDelegate SelectedTargetDelegate { get; set; }

            // BT visit-time properties...
            private WoWSpell CachedSpell { get; set; }
            private WoWUnit CachedTarget { get; set; }
            private bool IsInterrupted { get; set; }


            private List<Composite> CreateChildren()
            {
                return new List<Composite>()
                {
                    // Cache & qualify...
                    new Action(context =>
                    {
                        // If SelectedTargetDelegate is null, then its implicitly 'self', or a general effect...
                        var selectedTarget = SelectedTargetDelegate(context) ?? Me;
                        if (selectedTarget != null)
                        {
                            CachedTarget = selectedTarget as WoWUnit;
                            if (CachedTarget == null)
                            {
                                QBCLog.Warning("Target {0} is not a WoWUnit--cannot cast spell on it", selectedTarget.SafeName());
                                return RunStatus.Failure;
                            }
                        }

                        var spellId = SpellIdDelegate(context);
                        SpellFindResults findSpellResults;
                        var isSpellFound = SpellManager.FindSpell(spellId, out findSpellResults);
                        if (!isSpellFound)
                        {
                            QBCLog.Warning("Spell {0} is not known.", Utility.GetSpellNameFromId(spellId));
                            return RunStatus.Failure;
                        }

                        CachedSpell = findSpellResults.Override ?? findSpellResults.Original;
                        if (!SpellManager.CanCast(CachedSpell))
                        {
                            QBCLog.Warning("{0} is not usable (yet).", CachedSpell.Name);
                            return RunStatus.Failure;
                        }

                        return RunStatus.Success;
                    }),

                    // Use the item...
                    new Action(context =>
                    {
                        // Set up 'interrupted use' detection...
                        // MAINTAINER'S NOTE: Once these handlers are installed, make sure all possible exit paths from the outer
                        // Sequence unhook these handlers.  I.e., if you plan on returning RunStatus.Failure, be sure to call
                        // UtilityBehaviorSeq_UseItemOn_HandlersUnhook() first.
                        InterruptDetection_Hook();

                        // Notify user of intent...
                        var message = string.Format("Attempting cast of '{0}' on '{1}'", CachedSpell.Name, CachedTarget.SafeName());

                        message +=
                            (CachedTarget.IsDead)
                            ? " (dead)"
                            : string.Format(" (health: {0:F1})", CachedTarget.HealthPercent);

                        QBCLog.DeveloperInfo(message);

                        // Do it...
                        IsInterrupted = false;
                        SpellManager.Cast(CachedSpell, CachedTarget);
                    }),
                    new WaitContinue(Delay.AfterItemUse, context => false, new ActionAlwaysSucceed()),

                    // If item use requires a second click on the target (e.g., item has a 'ground target' mechanic)...
                    new DecoratorContinue(context => StyxWoW.Me.CurrentPendingCursorSpell != null,
                        new Sequence(
                            new Action(context => { SpellManager.ClickRemoteLocation(CachedTarget.Location); }),
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
                    new Action(context => { InterruptDectection_Unhook(); }),
                    new DecoratorContinue(context => IsInterrupted,
                        new Sequence(
                            new Action(context => { QBCLog.Warning("Cast of {0} interrupted.", CachedSpell.Name); }),
                            // Give whatever issue encountered a chance to settle...
                            // NB: Wait, not WaitContinue--we want the Sequence to fail when delay completes.
                            new Wait(TimeSpan.FromMilliseconds(1500), context => false, new ActionAlwaysFail())
                        )),
                    new Action(context =>
                    {
                        QBCLog.DeveloperInfo("Cast of '{0}' on '{1}' succeeded.", CachedSpell.Name, CachedTarget.SafeName());
                    })
                };
            }

            private void HandleInterrupted(object sender, LuaEventArgs args)
            {
                if (args.Args[0].ToString() == "player")
                {
                    QBCLog.DeveloperInfo("Interrupted via {0} Event.", args.EventName);
                    IsInterrupted = true;
                }
            }


            private void InterruptDetection_Hook()
            {
                Lua.Events.AttachEvent("UNIT_SPELLCAST_CHANNEL_UPDATE", HandleInterrupted);
                Lua.Events.AttachEvent("UNIT_SPELLCAST_FAILED", HandleInterrupted);
                Lua.Events.AttachEvent("UNIT_SPELLCAST_INTERRUPTED", HandleInterrupted);
            }


            private void InterruptDectection_Unhook()
            {
                Lua.Events.DetachEvent("UNIT_SPELLCAST_CHANNEL_UPDATE", HandleInterrupted);
                Lua.Events.DetachEvent("UNIT_SPELLCAST_FAILED", HandleInterrupted);
                Lua.Events.DetachEvent("UNIT_SPELLCAST_INTERRUPTED", HandleInterrupted);
            }
        }
    }
}