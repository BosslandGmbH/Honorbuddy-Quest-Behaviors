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
    public abstract partial class QuestBehaviorBase
    {
        public Composite UtilityBehaviorSeq_InteractWith(ProvideWoWObjectDelegate selectedTargetDelegate,
                                                        ProvideBoolDelegate doMovementDelegate = null)
        {
            ContractRequires(selectedTargetDelegate != null, context => "selectedTargetDelegate != null");
            doMovementDelegate = doMovementDelegate ?? (context => true);

            return new Sequence(
                new DecoratorContinue(context => !IsViable(_ubseqInteractWith_SelectedTarget = selectedTargetDelegate(context)),
                    new Action(context =>
                    {
                        LogWarning("Target is not viable!");
                        return RunStatus.Failure;                        
                    })),

                new DecoratorContinue(context => doMovementDelegate(context),
                    new PrioritySelector(
                        // Show user which unit we're going after...
                        new Decorator(context => (_ubseqInteractWith_SelectedTarget.ToUnit() != null)
                                                    && (Me.CurrentTarget != _ubseqInteractWith_SelectedTarget),
                            new Action(context => { _ubseqInteractWith_SelectedTarget.ToUnit().Target(); })),

                        // If not within interact range, move closer...
                        new Decorator(context => !_ubseqInteractWith_SelectedTarget.WithinInteractRange,
                            UtilityBehaviorPS_MoveTo(interactUnitContext => _ubseqInteractWith_SelectedTarget.Location,
                                                     interactUnitContext => string.Format("interact with {0}", _ubseqInteractWith_SelectedTarget.Name))),

                        UtilityBehaviorPS_MoveStop(),
                        UtilityBehaviorPS_FaceMob(context => _ubseqInteractWith_SelectedTarget),
                        new ActionAlwaysSucceed()
                    )),

                // Interact with the mob...
                new Action(context =>
                {
                    // Set up 'interrupted use' detection...
                    // MAINTAINER'S NOTE: Once these handlers are installed, make sure all possible exit paths from the outer
                    // Sequence unhook these handlers.  I.e., if you plan on returning RunStatus.Failure, be sure to call
                    // UtilityBehaviorSeq_UseItemOn_HandlersUnhook() first.
                    UtilityBehaviorSeq_InteractWith_HandlersHook();

                    // Notify user of intent...
                    LogInfo("Interacting with '{0}'", _ubseqInteractWith_SelectedTarget.Name);

                    // Do it...
                    _ubseqInteractWith_IsInteractInterrupted = false;    
                    _ubseqInteractWith_SelectedTarget.Interact();
                }),
                new WaitContinue(Delay_AfterInteraction, context => false, new ActionAlwaysSucceed()),

                // Wait for any casting to complete...
                // NB: Some interactions or item usages take time, and the WoWclient models this as spellcasting.
                new WaitContinue(TimeSpan.FromSeconds(15),
                    context => !(Me.IsCasting || Me.IsChanneling),
                    new ActionAlwaysSucceed()),

                // Were we interrupted in item use?
                new Action(context => { UtilityBehaviorSeq_InteractWith_HandlersUnhook(); }),
                new DecoratorContinue(context => _ubseqInteractWith_IsInteractInterrupted,
                    new Sequence(
                        new Action(context => { LogDeveloperInfo("Interaction with {0} interrupted.", _ubseqInteractWith_SelectedTarget.Name); }),
                        // Give whatever issue encountered a chance to settle...
                        // NB: Wait, not WaitContinue--we want the Sequence to fail when delay completes.
                        new Wait(TimeSpan.FromMilliseconds(1500), context => false, new ActionAlwaysFail())
                    ))  
            );
        }
        private bool _ubseqInteractWith_IsInteractInterrupted;
        private WoWObject _ubseqInteractWith_SelectedTarget;

        private void UtilityBehaviorSeq_InteractWith_HandleInteractInterrupted(object sender, LuaEventArgs args)
        {
            if (args.Args[0].ToString() == "player")
            {
                LogDeveloperInfo("Interrupted via {0} Event.", args.EventName);
                _ubseqInteractWith_IsInteractInterrupted = true;
            }
        }

        private void UtilityBehaviorSeq_InteractWith_HandlersHook()
        {
            Lua.Events.AttachEvent("UNIT_SPELLCAST_CHANNEL_UPDATE", UtilityBehaviorSeq_InteractWith_HandleInteractInterrupted);
            Lua.Events.AttachEvent("UNIT_SPELLCAST_FAILED", UtilityBehaviorSeq_InteractWith_HandleInteractInterrupted);
            Lua.Events.AttachEvent("UNIT_SPELLCAST_INTERRUPTED", UtilityBehaviorSeq_InteractWith_HandleInteractInterrupted);
        }

        private void UtilityBehaviorSeq_InteractWith_HandlersUnhook()
        {
            Lua.Events.DetachEvent("UNIT_SPELLCAST_CHANNEL_UPDATE", UtilityBehaviorSeq_InteractWith_HandleInteractInterrupted);
            Lua.Events.DetachEvent("UNIT_SPELLCAST_FAILED", UtilityBehaviorSeq_InteractWith_HandleInteractInterrupted);
            Lua.Events.DetachEvent("UNIT_SPELLCAST_INTERRUPTED", UtilityBehaviorSeq_InteractWith_HandleInteractInterrupted);    
        }
    }
}