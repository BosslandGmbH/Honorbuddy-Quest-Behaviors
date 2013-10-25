using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
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
using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.Olblasty
{
    [CustomBehaviorFileName(@"SpecificQuests\26647-STV-Olblasty")]
    public class Olblasty : CustomForcedBehavior
    {
        private const double WeaponAzimuthMax = 1.134464;
        private const double WeaponAzimuthMin = -0.348367;
        private const double CannonballMuzzleVelocity = 80;
        private const double CannonballGravity = 19.29;

        private readonly WeaponArticulation _weaponArticulation = new WeaponArticulation(WeaponAzimuthMin, WeaponAzimuthMax);

        private bool _isBehaviorDone;

        private Composite _root;
        public QuestCompleteRequirement questCompleteRequirement = QuestCompleteRequirement.NotComplete;
        public QuestInLogRequirement questInLogRequirement = QuestInLogRequirement.InLog;

        public Olblasty(Dictionary<string, string> args) : base(args)
        {
            try
            {
                QuestId = 26647; //GetAttributeAsQuestId("QuestId", true, null) ?? 0;
            }
            catch
            {
                Logging.Write("Problem parsing a QuestId in behavior: Rampage Against The Machine");
            }
        }

        public int QuestId { get; set; }


        public override bool IsDone
        {
            get { return _isBehaviorDone; }
        }

        private LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
        }

        public Composite CreateBehavior_CheckCompletion()
        {
            return new Decorator(
                ret => !_isBehaviorDone && IsQuestComplete(),
                new Action(
                    delegate
                    {
                        TreeRoot.StatusText = "Finished!";

                        Lua.DoString("VehicleExit()");
                        _isBehaviorDone = true;
                        return RunStatus.Success;
                    }));
        }

        public Composite CreateBehavior_Shoot()
        {
            var cannon = new VehicleWeapon(3, _weaponArticulation, CannonballMuzzleVelocity, CannonballGravity);
            const int readyAuraId = 81513;
            const int aimAuraId = 81514;
            WoWUnit charmedUnit = null;

            const uint boatId = 43561;
            WoWUnit boat = null;
            return new PrioritySelector(
                ctx => boat = ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(u => u.Entry == boatId),
                new Decorator(
                    r => boat != null,
                    new PrioritySelector(
                        ctx => charmedUnit = Me.CharmedUnit,
                        new Decorator(ctx => !charmedUnit.HasAura(readyAuraId), new Action(ctx => Lua.DoString("CastPetAction({0})", 1))),
                        new Decorator(
                            ctx => cannon.WeaponAim(boat),
                            new Sequence(
                                new Action(ctx => Lua.DoString("CastPetAction({0})", 2)),
                                new WaitContinue(2, ctx => charmedUnit.HasAura(aimAuraId), new ActionAlwaysSucceed()),
                                new Action(ctx => cannon.WeaponFire()))))));
        }

        private Composite CreateBehavior_GetInVehicle()
        {
            const uint oldBlastyId = 43562;

            WoWUnit vehicle = null;

            return new Decorator(
                ctx => !Me.InVehicle,
                new PrioritySelector(
                    ctx => vehicle = ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(u => u.Entry == oldBlastyId),
                    new Decorator(ctx => vehicle != null && !vehicle.WithinInteractRange, new Action(ctx => Navigator.MoveTo(vehicle.Location))),
                    new Decorator(
                        ctx => vehicle != null && vehicle.WithinInteractRange,
                        new PrioritySelector(
                            new Decorator(ctx => Me.Mounted, new Action(ctx => Mount.Dismount("Getting in vehicle"))),
                            new Decorator(ctx => Me.IsShapeshifted(), new Action(ctx => Lua.DoString("CancelShapeshiftForm()"))),
                            new Action(ctx => vehicle.Interact())))));
        }

        public override void OnStart()
        {
            OnStart_HandleAttributeProblem();
            if (!IsDone)
            {
                TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_CombatMain());
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint) QuestId);
                TreeRoot.GoalText = ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
            }
        }


        public bool IsQuestComplete()
        {
            var quest = StyxWoW.Me.QuestLog.GetQuestById((uint) QuestId);
            return quest == null || quest.IsCompleted;
        }


        protected Composite CreateBehavior_CombatMain()
        {
            return _root ?? (_root = new Decorator(ret => !IsDone, new PrioritySelector(CreateBehavior_CheckCompletion(), CreateBehavior_GetInVehicle(), CreateBehavior_Shoot())));
        }

        #region Cleanup

        private bool _isDisposed;

        ~Olblasty()
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