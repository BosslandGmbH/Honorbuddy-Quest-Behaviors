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

// TODO:
// * Need 'bind at inn' behavior
// * Need 'buy from merchant' behavior

namespace Honorbuddy.QuestBehaviorCore
{
    public partial class UtilityBehaviorSeq
    {
        public class InteractWith : Sequence
        {
            public InteractWith(ProvideWoWObjectDelegate selectedTargetDelegate,
                                ProvideMovementByDelgate movementByDelegate = null)
            {
                Contract.Requires(selectedTargetDelegate != null, context => "selectedTargetDelegate != null");

                SelectedTargetDelegate = selectedTargetDelegate;
                MovementByDelegate = movementByDelegate ?? (context => MovementByType.None);

                Children = CreateChildren();
            }


            // BT contruction-time properties...
            private ProvideWoWObjectDelegate SelectedTargetDelegate { get; set; }
            private ProvideMovementByDelgate MovementByDelegate { get; set; }

            // BT visit-time properties...
            private MovementByType CachedMovementBy { get; set; }
            private WoWObject CachedTarget { get; set; }
            private bool IsInteractInterrupted { get; set; }


            private List<Composite> CreateChildren()
            {
                return new List<Composite>()
                {
                    new Action(context =>
                    {
                        CachedMovementBy = MovementByDelegate(context);
                        CachedTarget = SelectedTargetDelegate(context);

                        if (!Query.IsViable(CachedTarget))
                        {
                            QBCLog.Warning("Target is not viable!");
                            return RunStatus.Failure;
                        }

                        return RunStatus.Success;
                    }),

                    new DecoratorContinue(context => CachedMovementBy != MovementByType.None,
                        new PrioritySelector(
                            // Show user which unit we're going after...
                            new UtilityBehaviorPS.Target(context => CachedTarget),

                            // If not within interact range, move closer...
                            new Decorator(context => !CachedTarget.WithinInteractRange,
                                new UtilityBehaviorPS.MoveTo(
                                    context => CachedTarget.Location,
                                    context => string.Format("interact with {0}", CachedTarget.Name),
                                    context => CachedMovementBy)),

                            new UtilityBehaviorPS.MoveStop(),
                            new UtilityBehaviorPS.FaceMob(context => CachedTarget),
                            new ActionAlwaysSucceed()
                        )),

                    // Interact with the mob...
                    new Action(context =>
                    {
                        // Set up 'interrupted use' detection...
                        // MAINTAINER'S NOTE: Once these handlers are installed, make sure all possible exit paths from the outer
                        // Sequence unhook these handlers.  I.e., if you plan on returning RunStatus.Failure, be sure to call
                        // UtilityBehaviorSeq_UseItemOn_HandlersUnhook() first.
                        InterruptDetection_Hook();

                        // Notify user of intent...
                        QBCLog.Info("Interacting with '{0}'", CachedTarget.Name);

                        // Do it...
                        IsInteractInterrupted = false;
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
                    new Action(context => { InterruptDetection_Unhook(); }),
                    new DecoratorContinue(context => IsInteractInterrupted,
                        new Sequence(
                            new Action(context => { QBCLog.DeveloperInfo("Interaction with {0} interrupted.", CachedTarget.Name); }),
                            // Give whatever issue encountered a chance to settle...
                            // NB: Wait, not WaitContinue--we want the Sequence to fail when delay completes,
                            // since we were interrupted.
                            new Wait(TimeSpan.FromMilliseconds(1500), context => false, new ActionAlwaysFail())
                        ))
                };
            }


            private void HandleInteractInterrupted(object sender, LuaEventArgs args)
            {
                if (args.Args[0].ToString() == "player")
                {
                    QBCLog.DeveloperInfo("Interrupted via {0} Event.", args.EventName);
                    IsInteractInterrupted = true;
                }
            }


            private void InterruptDetection_Hook()
            {
                Lua.Events.AttachEvent("UNIT_SPELLCAST_CHANNEL_UPDATE", HandleInteractInterrupted);
                Lua.Events.AttachEvent("UNIT_SPELLCAST_FAILED", HandleInteractInterrupted);
                Lua.Events.AttachEvent("UNIT_SPELLCAST_INTERRUPTED", HandleInteractInterrupted);
            }


            private void InterruptDetection_Unhook()
            {
                Lua.Events.DetachEvent("UNIT_SPELLCAST_CHANNEL_UPDATE", HandleInteractInterrupted);
                Lua.Events.DetachEvent("UNIT_SPELLCAST_FAILED", HandleInteractInterrupted);
                Lua.Events.DetachEvent("UNIT_SPELLCAST_INTERRUPTED", HandleInteractInterrupted);
            }
        }
    }
}