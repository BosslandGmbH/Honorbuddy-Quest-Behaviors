using System;
using System.Collections.Generic;
using System.Diagnostics;
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


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.DriveByPiracy
{
    [CustomBehaviorFileName(@"SpecificQuests\26649-Stranglethorn-DriveByPiracy")]
    public class q26649 : CustomForcedBehavior
    {
        private const double WeaponAzimuthMax = 1.134464;
        private const double WeaponAzimuthMin = -0.348367;
        private const double CannonballMuzzleVelocity = 80;
        private const double CannonballGravity = 19.29;
        private const uint VentureCoOilWorkerId = 43596;
        public static LocalPlayer me = StyxWoW.Me;

        private readonly WeaponArticulation _weaponArticulation = new WeaponArticulation(WeaponAzimuthMin, WeaponAzimuthMax);

        private uint QuestId = 26649;
        private bool _isBehaviorDone;
        private Composite _root;
        public double angle = 0;
        public q26649(Dictionary<string, string> args) : base(args) {}


        public override bool IsDone
        {
            get { return _isBehaviorDone; }
        }

        private LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
        }

        private WoWUnit BestTarget
        {
            get
            {
                var myLoc = Me.Location;
                return
                    ObjectManager.GetObjectsOfTypeFast<WoWUnit>()
                        .Where(u => u.Entry == VentureCoOilWorkerId && u.IsAlive)
                        .OrderBy(u => u.Location.DistanceSqr(myLoc))
                        .FirstOrDefault();
            }
        }

        public bool IsQuestComplete()
        {
            var quest = StyxWoW.Me.QuestLog.GetQuestById((uint) QuestId);
            return quest == null || quest.IsCompleted;
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

        protected Composite CreateBehavior_CombatMain()
        {
            return _root ??
                   (_root =
                       new Decorator(
                           ctx => !IsDone,
                           new PrioritySelector(CreateBehavior_CheckCompletion(), CreateBehavior_ShootPirates())));
        }

        public Composite CreateBehavior_ShootPirates()
        {
            var cannon = new VehicleWeapon(3, _weaponArticulation, CannonballMuzzleVelocity, CannonballGravity);
            const int readyAuraId = 81513;
            const int aimAuraId = 81514;
            WoWUnit charmedUnit = null;

            WoWUnit selectedTarget = null;
            return new PrioritySelector(
                ctx => selectedTarget = BestTarget,
                new Decorator(
                    r => selectedTarget != null,
                    new PrioritySelector(
                        ctx => charmedUnit = Me.CharmedUnit,
                        new Decorator(
                            ctx => !charmedUnit.HasAura(readyAuraId),
                            new Action(ctx => Lua.DoString("CastPetAction({0})", 1))),
                        // aim weapon and fire.
                        new Decorator(
                            ctx => cannon.WeaponAim(selectedTarget),
                            new Sequence(
                                new Action(ctx => Lua.DoString("CastPetAction({0})", 2)),
                                new WaitContinue(2, ctx => charmedUnit.HasAura(aimAuraId), new ActionAlwaysSucceed()),
                                new Action(ctx => cannon.WeaponFire()))))));
        }

        public Composite CreateBehavior_CheckCompletion()
        {
            return new Decorator(
                ret => !_isBehaviorDone && IsQuestComplete(),
                new Sequence(
                    new ActionSetActivity("Finished!"),
                    new Action(ctx => Lua.DoString("CastPetAction({0})", 5)),
                    new Action(ctx => _isBehaviorDone = true)));
        }

        #region Cleanup

        private bool _isDisposed;

        ~q26649()
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