using System.Collections.Generic;
using System.Linq;
using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Common;
using Styx.Logic.Inventory;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Styx.Logic.Combat;

using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// IntotheRealmofShadows by HighVoltz
    /// Into the Realm of Shadows]
    /// ##Syntax##
    /// X,Y,Z: The location where you want to move to
    /// </summary>
    public class IntotheRealmofShadows : CustomForcedBehavior
    {
        readonly Dictionary<string, object> _recognizedAttributes = new Dictionary<string, object>()
        {
            {"X",null},
            {"Y",null},
            {"Z",null},
        };

        bool success = true;
        public IntotheRealmofShadows(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(_recognizedAttributes);
            WoWPoint point = WoWPoint.Empty;

            success = success && GetXYZAttributeAsWoWPoint("X", "Y", "Z", true, WoWPoint.Empty, out point);

            Location = point;
        }

        private bool IsInVehicle
        {
            get { return Lua.GetReturnVal<int>("return UnitIsControlling('player')", 0) == 1; }
        }

        public WoWPoint Location { get; private set; }
        LocalPlayer Me { get { return StyxWoW.Me; } }

        #region Overrides of CustomForcedBehavior

        private Composite _root;

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(
                    new Action(c =>
                    {
                        if (Me.HealthPercent < 2)
                        {
                            return RunStatus.Failure;
                        }
                        if (Me.HealthPercent < 60 && !Me.IsActuallyInCombat)
                        {
                            WoWItem food = Consumable.GetBestFood(true);
                            LevelbotSettings.Instance.FoodName = food != null ? food.Name : "";
                            Rest.Feed();
                            return RunStatus.Running;
                        }
                        if (IsInVehicle)
                        {
                            _isDone = true;
                            return RunStatus.Success;
                        }
                        WoWUnit Horse = ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 28782).OrderBy(u => u.Distance).FirstOrDefault();
                        if (Horse != null)
                        {
                            if (!Me.IsActuallyInCombat)
                            {
                                if (Horse.Distance > 4)
                                    Navigator.MoveTo(Horse.Location);
                                else
                                    Horse.Interact();
                            }
                            if (Me.IsActuallyInCombat)
                            {
                                if (Me.CurrentTarget != null)
                                {
                                    if (Me.CurrentTarget.Dead)
                                    {
                                        Me.ClearTarget();
                                    }
                                    else if (Me.CurrentTarget.Entry == 28768)
                                    {
                                        if (!Me.IsSafelyFacing(Horse))
                                            Horse.Face();
                                    }
                                    else if (!Me.IsSafelyFacing(Me.CurrentTarget))
                                        Me.CurrentTarget.Face();
                                    if (Me.IsMoving)
                                    {
                                        WoWMovement.MoveStop();
                                    }
                                    if (!Me.IsSafelyFacing(Me.CurrentTarget))
                                        Me.CurrentTarget.Face();
                                    if (SpellManager.CanCast("Icy Touch"))
                                        SpellManager.Cast("Icy Touch");
                                    if (SpellManager.CanCast("Plague Strike"))
                                        SpellManager.Cast("Plague Strike");
                                    if (SpellManager.CanCast("Blood Strike"))
                                        SpellManager.Cast("Blood Strike");
                                    if (SpellManager.CanCast("Death Coil"))
                                        SpellManager.Cast("Death Coil");
                                }
                            }
                        }
                        else
                            Navigator.MoveTo(Location);

                        return RunStatus.Running;
                    }

                )));
        }

        private bool _isDone;
        public override bool IsDone { get { return _isDone; } }

        public override void OnStart()
        {
            TreeRoot.GoalText = "Completing quest 'Realm of the Shadow'";
        }

        public override void Dispose()
        {
        }

        #endregion
    }
}
