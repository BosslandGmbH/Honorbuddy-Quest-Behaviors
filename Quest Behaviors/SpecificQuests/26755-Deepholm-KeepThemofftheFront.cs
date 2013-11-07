using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Honorbuddy.QuestBehaviorCore;
using Action = Styx.TreeSharp.Action;


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
        public QuestCompleteRequirement questCompleteRequirement = QuestCompleteRequirement.NotComplete;
        public QuestInLogRequirement questInLogRequirement = QuestInLogRequirement.InLog;
        private WeaponArticulation weaponArticulation;

        public KeepThemofftheFront(Dictionary<string, string> args) : base(args)
        {
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
                PlayerQuest Quest = StyxWoW.Me.QuestLog.GetQuestById((uint) QuestId);
                TreeRoot.GoalText = ((Quest != null) ? ("\"" + Quest.Name + "\"") : "In Progress");
            }
        }

        public bool IsQuestComplete()
        {
            var quest = StyxWoW.Me.QuestLog.GetQuestById((uint) QuestId);
            return quest == null || quest.IsCompleted;
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
            return new Decorator(
                ret => IsQuestComplete(),
                new Sequence(new Action(ctx => Lua.DoString("VehicleExit()")), new Action(ctx => _isBehaviorDone = true)));
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