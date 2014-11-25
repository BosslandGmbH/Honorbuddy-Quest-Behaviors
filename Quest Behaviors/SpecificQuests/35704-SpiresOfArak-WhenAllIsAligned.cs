// Originally contributed by MaxMuster
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

#region Summary and Documentation

#endregion

#region Examples

#endregion

#region Usings

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

#endregion

namespace Honorbuddy.Quest_Behaviors.SpecificQuests.WhenAllIsAligned
{
    [CustomBehaviorFileName(@"SpecificQuests\35704-SpiresOfArak-WhenAllIsAligned")]
    public class WhenAllIsAligned : CustomForcedBehavior
    {
        public WhenAllIsAligned(Dictionary<string, string> args)
            : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            try
            {
                QuestId = 35704;
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

        public int QuestId { get; set; }
        private bool _isBehaviorDone;

        public uint[] Mobs = {82803, 82806, 83487, 86287, 82804, 82817, 88648, 88250};

        private Composite _root;

        public override bool IsDone { get { return _isBehaviorDone; } }
        private LocalPlayer Me { get { return (StyxWoW.Me); } }

        public override void OnStart()
        {
            OnStart_HandleAttributeProblem();
            if (!IsDone)
            {
                TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_MainCombat());

                this.UpdateGoalText(QuestId);
            }
        }

        protected Composite CreateBehavior_MainCombat()
        {
            return _root ?? (_root = new ActionRunCoroutine(ctx => Coroutine_MainCombat()));
        }

        protected async Task<bool> Coroutine_MainCombat()
        {
            if (IsDone)
                return false;

            if (await HandleQuestCompletion())
                return true;

            if (await EnsureTarget())
                return true;

            return await Shoot(Adherent);
        }

        private async Task<bool> EnsureTarget()
        {
            if (!Me.GotTarget || Me.CurrentTarget.IsHostile)
                return false;
            Me.ClearTarget();
            return true;
        }

        private async Task<bool> HandleQuestCompletion()
        {
            if (!Me.IsQuestComplete(QuestId)) 
                return false;
            // Exit vehicle after quest is completed
            QBCLog.Info("Finished!");
            Lua.DoString("VehicleExit()");
            await CommonCoroutines.SleepForRandomUiInteractionTime();
            _isBehaviorDone = true;
            return true;
        }

        private async Task<bool> Shoot(WoWUnit target)
        {
            if (!Query.IsViable(target))
                return false;
            var v = target.Location - StyxWoW.Me.Transport.Location;
            v.Normalize();
            Lua.DoString(
                string.Format(
                    "local pitch = {0}; local delta = pitch - VehicleAimGetAngle(); VehicleAimIncrement(delta);",
                    Math.Asin(v.Z)));

            //If the target is moving, the projectile is not instant
            WoWMovement.ClickToMove(target.IsMoving ? target.Location.RayCast(target.Rotation, 10f) : target.Location);
            //Fire pew pew
            Lua.DoString("CastPetAction({0})", 1);
            await CommonCoroutines.SleepForRandomReactionTime();
            return true;
        }

        public WoWUnit Adherent
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>()
                        .Where(u => Mobs.Contains(u.Entry) && u.IsAlive)
                        .OrderBy(u => u.DistanceSqr)
                        .FirstOrDefault();
            }
        }

        #region Cleanup

        public override void OnFinished()
        {
            TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_MainCombat());
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }

        #endregion
    }
}



