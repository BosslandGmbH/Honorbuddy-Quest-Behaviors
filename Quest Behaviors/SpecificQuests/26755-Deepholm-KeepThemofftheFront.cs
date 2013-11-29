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
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;

using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Honorbuddy.QuestBehaviorCore;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.KeepThemofftheFront
{
    [CustomBehaviorFileName(@"SpecificQuests\26755-Deepholm-KeepThemofftheFront")]
    public class KeepThemofftheFront : CustomForcedBehavior
    {
        private const uint StoneTroggReinforcementId = 43960;
        private const uint FungalTerrorId = 43954;

        private const double WeaponAzimuthMax = 0.7853999;
        private const double WeaponAzimuthMin = -0.1745;
        private const double WeaponMuzzleVelocity = 100;
        private const double WeaponProjectileGravity = 30;

        private readonly VehicleWeapon _catapult;
        private readonly uint[] _mobIds = {StoneTroggReinforcementId, FungalTerrorId};

        private bool _isBehaviorDone;

        private Composite _root;

        public KeepThemofftheFront(Dictionary<string, string> args) : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            QuestId = 26755;
            var weaponArticulation = new WeaponArticulation(WeaponAzimuthMin, WeaponAzimuthMax);
            _catapult = new VehicleWeapon(1, weaponArticulation, WeaponMuzzleVelocity, WeaponProjectileGravity);
        }

        public int QuestId { get; set; }


        public override bool IsDone
        {
            get { return _isBehaviorDone; }
        }

        private static LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
        }


        public WoWUnit BestTarget
        {
            get
            {
                var activeMover = WoWMovement.ActiveMover;
                var myLoc = activeMover.Location;

                var myTarget = activeMover.CurrentTarget;

                if (myTarget != null && myTarget.IsAlive && _mobIds.Contains(myTarget.Entry) && myTarget.DistanceSqr > 25*25)
                    return myTarget;

                return (from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
                    where _mobIds.Contains(unit.Entry) && unit.IsAlive
                    let distanceSqr = myLoc.DistanceSqr(unit.Location)
                    where distanceSqr > 25*25
                    orderby distanceSqr
                    select unit).FirstOrDefault();
            }
        }


        public override void OnStart()
        {
            OnStart_HandleAttributeProblem();
            if (!IsDone)
            {
                TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_CombatMain());

                this.UpdateGoalText(QuestId);
            }
        }


        protected Composite CreateBehavior_CombatMain()
        {
            return _root ??
                   (_root =
                       new Decorator(
                           ret => !_isBehaviorDone,
                           new PrioritySelector(CreateBehavior_CheckQuestCompletion(), CreateBehavior_ShootCatapult())));
        }


        private Composite CreateBehavior_CheckQuestCompletion()
        {
            return new Decorator(ret => Me.IsQuestComplete(QuestId),
                new Action(ctx =>
                    {
                        Lua.DoString("VehicleExit()");
                        _isBehaviorDone = true;
                    }));
        }


        private Composite CreateBehavior_ShootCatapult()
        {
            WoWUnit target = null;
            return new PrioritySelector(
                ctx => target = BestTarget,
                new Decorator(
                    ctx => target != null && _catapult.WeaponAim(target),
                    new Action(ctx => _catapult.WeaponFire())));
        }

           #region Cleanup

        private bool _isDisposed;

        ~KeepThemofftheFront()
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
                    TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_CombatMain());
                }

                // Clean up unmanaged resources (if any) here...
                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}