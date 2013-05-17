// Behavior originally contributed by mastahg.
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
// QUICK DOX:
// TOGGLECOMBAT allows the profile writer to completely disable the Combat Routine
// during moments in which it interferes with questing.  
// Once the profile no longer needs the Combat Routine disable, it calls this behavior
// a second time to re-enable it.
//
// Basic Attributes:
//      CombatEnabled [required]
//          If 'true' the Combat Routine is allowed to run.  With 'false', the Combat
//          Routine is completely disabled.
//
// THINGS TO KNOW:
// * If you disable the Combat Routine with this behavior, it is *completely* disabled.
//      No buffing, no healing, no pet summoning, no combat.
//
// * You should NOT attempt to disable the Combat Routine more than once, without an intervening
//      enable.  To do such, will cause unexpected results--requiring an Honorbuddy restart
//      to repair.
//
// * The behavior blindly toggles to the state specified in CombatEnabled.  (I.e., there
//      is no 'nesting' of calls to this behavior.)
//
#endregion


#region Usings
using System;
using System.Collections.Generic;

using CommonBehaviors.Actions;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
#endregion


namespace Styx.Bot.Quest_Behaviors
{
    [CustomBehaviorFileName(@"ToggleCombat")]
    public class ToggleCombat : CustomForcedBehavior
    {
        ~ToggleCombat()
        {
            Dispose(false);
        }


        public ToggleCombat(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.

                _isCombatEnabled = GetAttributeAsNullable<bool>("CombatEnabled", true, null, null) ?? false;
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                LogMessage("error",
                           "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message + "\nFROM HERE:\n" + except.StackTrace +
                           "\n");
                IsAttributeProblem = true;
            }
        }


        private readonly bool _isCombatEnabled;
        private bool _isDisposed;


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
                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }


        #region Overrides of CustomForcedBehavior
        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        public override bool IsDone
        {
            get { return true; }
        }


        static Composite _thoseFeet;
        private static Composite ThoseFeet { get { return _thoseFeet ?? (_thoseFeet = new ActionAlwaysSucceed()); } }


        public override void OnStart()
        {
            // This reports problems, and stops BT processing if there was a problem with attributes...
            // We had to defer this action, as the 'profile line number' is not available during the element's
            // constructor call.
            OnStart_HandleAttributeProblem();

            Logging.Write("Quest Behavior made by mastahg.");
            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            


            if (_isCombatEnabled)
            {
                TreeHooks.Instance.RemoveHook("Combat_Main", ThoseFeet);
            }
            else
            {
                TreeHooks.Instance.InsertHook("Combat_Main",0,ThoseFeet);
            }
        }

        #endregion
    }
}