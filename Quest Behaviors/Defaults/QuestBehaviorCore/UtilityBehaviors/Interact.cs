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
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion

// TODO: Need 'bind at inn' variant
// TODO: Need 'buy from merchant' variant
// TODO: Need 'gossip' variant

namespace Honorbuddy.QuestBehaviorCore
{
    public partial class UtilityBehaviorSeq
    {
        // TODO: Convert this to PrioritySelector--its more common and versatile.
        public class Interact : Sequence
        {
            public Interact(ProvideWoWObjectDelegate selectedTargetDelegate)
            {
                Contract.Requires(selectedTargetDelegate != null, context => "selectedTargetDelegate != null");

                SelectedTargetDelegate = selectedTargetDelegate;

                Children = CreateChildren();
            }


            // BT contruction-time properties...
            private ProvideWoWObjectDelegate SelectedTargetDelegate { get; set; }

            // BT visit-time properties...
            private bool IsInterrupted { get; set; }
            private WoWObject CachedTarget { get; set; }

            // Convenience properties...


            private List<Composite> CreateChildren()
            {
                return new List<Composite>()
                {
                    new Action(context =>
                    {
                        CachedTarget = SelectedTargetDelegate(context);
                        if (!Query.IsViable(CachedTarget))
                        {
                            QBCLog.Warning("Target is not viable!");
                            return RunStatus.Failure;                        
                        }

                        return RunStatus.Success;
                    }),

                    // Interact with the mob...
                    new Action(context =>
                    {
                        // Set up 'interrupted use' detection...
                        // MAINTAINER'S NOTE: Once these handlers are installed, make sure all possible exit paths from the outer
                        // Sequence unhook these handlers.  I.e., if you plan on returning RunStatus.Failure, be sure to call
                        // UtilityBehaviorSeq_UseItemOn_HandlersUnhook() first.
                        InterruptHandlersHook();

                        // Notify user of intent...
                        QBCLog.DeveloperInfo("Interacting with '{0}'", CachedTarget.SafeName());

                        // Do it...
                        IsInterrupted = false;    
                        CachedTarget.Interact();
                    }),
                    new WaitContinue(Delay.AfterInteraction, context => false, new ActionAlwaysSucceed()),

                    // Wait for any casting to complete...
                    // NB: Some interactions or item usages take time, and the WoWclient models this as spellcasting.
                    // NB: We can't test for IsCasting or IsChanneling--we must instead look for a valid spell being cast.
                    //      There are some quests that require actions where the WoWclient returns 'true' for IsCasting,
                    //      but there is no valid spell being cast.  We want the behavior to move on immediately in these
                    //      conditions.  An example of such an interaction is removing 'tangler' vines in the Tillers
                    //      daily quest area.
                    new WaitContinue(TimeSpan.FromSeconds(15),
                        context => (Me.CastingSpell == null) && (Me.ChanneledSpell == null),
                        new ActionAlwaysSucceed()),

                    // Were we interrupted in item use?
                    new Action(context => { InterruptHandlersUnhook(); }),
                    new DecoratorContinue(context => IsInterrupted,
                        new Sequence(
                            new Action(context => { QBCLog.DeveloperInfo("Interaction with {0} interrupted.", CachedTarget.SafeName()); }),
                            // Give whatever issue encountered a chance to settle...
                            // NB: Wait, not WaitContinue--we want the Sequence to fail when delay completes.
                            new Wait(TimeSpan.FromMilliseconds(1500), context => false, new ActionAlwaysFail())
                        )),
                    new Action(context =>
                    {
                        QBCLog.DeveloperInfo("Interact with '{0}' succeeded.", CachedTarget.SafeName());
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

            private void InterruptHandlersHook()
            {
                Lua.Events.AttachEvent("UNIT_SPELLCAST_CHANNEL_UPDATE", HandleInterrupted);
                Lua.Events.AttachEvent("UNIT_SPELLCAST_FAILED", HandleInterrupted);
                Lua.Events.AttachEvent("UNIT_SPELLCAST_INTERRUPTED", HandleInterrupted);
            }

            private void InterruptHandlersUnhook()
            {
                Lua.Events.DetachEvent("UNIT_SPELLCAST_CHANNEL_UPDATE", HandleInterrupted);
                Lua.Events.DetachEvent("UNIT_SPELLCAST_FAILED", HandleInterrupted);
                Lua.Events.DetachEvent("UNIT_SPELLCAST_INTERRUPTED", HandleInterrupted);    
            }
        }
    }
}