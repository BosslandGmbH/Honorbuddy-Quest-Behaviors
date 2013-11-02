using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

/* This behavior is for killing Thane noobface in Grizzly Hills (Horde 12259 and Alliance 12255) 
		This behavior was developed by Kickazz006
		Code was taken from Shak
		How I used it in this behavior was chop each in half and take the bits that I needed
		Feel free to re-use the code to your liking (anyone else)
	*/

namespace Honorbuddy.Quest_Behaviors.SpecificQuests.AllyTheThaneofVoldrune
{
    [CustomBehaviorFileName(@"SpecificQuests\12259-GrizzlyHills-HordeTheThaneofVoldrune")]
    public class q12259 : CustomForcedBehavior
    {
        private WoWPoint endloc = new WoWPoint(2805.055, -2488.745, 47.76864);

        private readonly WoWPoint _flamebringerLocation = new WoWPoint(2793.088, -2506.125, 47.61626);
        private bool _isDone;
        private Composite _root;

        private bool IsQuestComplete
        {
            get
            {
                var quest = Me.QuestLog.GetQuestById(QuestId);
                return quest != null && quest.IsCompleted;
            }
        }

        #region Overrides of CustomForcedBehavior

        public override bool IsDone
        {
            get { return _isDone; }
        }

        public override void OnStart()
        {
            OnStart_HandleAttributeProblem();
            if (!IsDone)
            {
                TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_CombatMain());
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
                TreeRoot.GoalText = ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
            }
        }

        #endregion

        protected Composite CreateBehavior_CombatMain()
        {
            return _root ??
                   (_root =new Decorator(ctx => !IsDone,
                       new PrioritySelector(
                           new Decorator(
                               ret => IsQuestComplete ,
                               new PrioritySelector(
                                   new Decorator(
                                       ctx => Me.InVehicle,
                                       new PrioritySelector(
                                           new Decorator(
                                               ctx => WoWMovement.ActiveMover.Location.DistanceSqr(endloc) > 10*10,
                                               new Action(ctx => Flightor.MoveTo(endloc))),
                                           new Action(ctx => Lua.DoString("VehicleExit()")))),
                                   new Sequence(
                                       new Action(ret => TreeRoot.StatusText = "Finished!"),
                                       new Action(ctx => _isDone = true)))),
                           new Decorator(ctx => !Me.IsActuallyInCombat && !Me.InVehicle, CreateBehavior_GetInVehicle()),
                           new Decorator(ctx => Me.InVehicle, CreateBehavior_Kill()))));
        }

        private Composite CreateBehavior_GetInVehicle()
        {
            const uint flamebringerId = 27292;

            WoWUnit flamebringer = null;

            return
                new PrioritySelector(
                    ctx =>
                        flamebringer =
                            ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(u => u.Entry == flamebringerId),
                    // move out to framebringers location
                    new Decorator(ctx => flamebringer == null,CreateBehavior_MoveTo(ctx =>_flamebringerLocation)),
                    new Decorator(ctx => flamebringer.Distance > 5, CreateBehavior_MoveTo(ctx => flamebringer.Location)),
                    // dismount and cancel shapeshift
                    new Decorator(ctx => Me.IsMounted(), new Action(ctx => Mount.Dismount("Getting on Flamebringger"))),
                    new Decorator(ctx => Me.IsShapeshifted(), new Action(ctx => Lua.DoString("CancelShapeshiftForm()"))),
                    new Decorator(ctx => Me.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
                    // interact with and talk to flamebringer
                    new Decorator(ctx => !GossipFrame.Instance.IsVisible, new Action(ctx => flamebringer.Interact())),
                    new Decorator(
                        ctx => GossipFrame.Instance.IsVisible,
                        new Action(ctx => GossipFrame.Instance.SelectGossipOption(0))));
        }

        private Composite CreateBehavior_MoveTo(Func<object, WoWPoint> locationSelector)
        {
            return
                new PrioritySelector(
                    new Decorator(
                        ctx => WoWMovement.ActiveMover.MovementInfo.CanFly,
                        new Action(ctx => Flightor.MoveTo(locationSelector(ctx)))),
                    new Action(ctx => Navigator.MoveTo(locationSelector(ctx))));
        }

        private Composite CreateBehavior_Kill()
        {
            const uint torvaldErikssonId = 27377;

            var movetoLocation = new WoWPoint(2939.321, -2536.72, 123.3394);
            WoWUnit torvaldEriksson = null;

            return
                new PrioritySelector(
                    ctx =>
                        torvaldEriksson =
                            ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(u => u.Entry == torvaldErikssonId && u.IsAlive),
                    // move in position
                    new Decorator(
                        ctx => WoWMovement.ActiveMover.Location.DistanceSqr(movetoLocation) > 5*5,
                        new Action(ctx => Flightor.MoveTo(movetoLocation))),
                    new Decorator(ctx => WoWMovement.ActiveMover.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
                    new Decorator(
                        ctx => torvaldEriksson != null ,
                        new PrioritySelector(
                            // target
                            new Decorator(
                                ctx => WoWMovement.ActiveMover.CurrentTargetGuid != torvaldEriksson.Guid,
                                new Action(ctx => torvaldEriksson.Target())),
                            // face 
                            new Decorator(
                                ctx => !WoWMovement.ActiveMover.IsSafelyFacing(torvaldEriksson, 30),
                                new Action(ctx => torvaldEriksson.Face())),
                            new Action(ctx => AimAndFire(torvaldEriksson)))));
        }

        private void AimAndFire(WoWUnit target)
        {
            var v = target.Location - StyxWoW.Me.Location;
            v.Normalize();
            Lua.DoString(
                string.Format(
                    "local pitch = {0}; local delta = pitch - VehicleAimGetAngle(); VehicleAimIncrement(delta);",
                    Math.Asin(v.Z).ToString(CultureInfo.InvariantCulture)));
            Lua.DoString("CastPetAction(3)");
            Lua.DoString("CastPetAction(2)");
            Lua.DoString("CastPetAction(1)");
        }

        public q12259(Dictionary<string, string> args) : base(args)
        {
            QuestId = 12259;
            Location = WoWPoint.Empty;
            Endloc = WoWPoint.Empty;
            QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
            QuestRequirementInLog = QuestInLogRequirement.InLog;
        }

        public WoWPoint Location { get; private set; }
        public WoWPoint Endloc { get; private set; }
        public uint QuestId { get; set; }

        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }

        private static LocalPlayer Me
        {
            get { return StyxWoW.Me; }
        }

        #region Cleanup

        private bool _isDisposed;

        ~q12259()
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