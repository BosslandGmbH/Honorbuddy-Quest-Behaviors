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

using Styx;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public partial class UtilityBehaviorPS
    {
        /// <summary>
        /// Descends to a height safe for dismounting.  "Safe" is defined as 'not flying, or no more than
        /// MAXDISMOUNTHEIGHTDELEGATE above the ground.
        /// <para>Notes:<list type="bullet">
        /// <item><description><para> * If MAXDISMOUNTHEIGHTDELEGATE is not provided, a suitable value
        /// is used.</para></description></item>
        /// </list></para>
        /// </summary>
        /// <returns></returns>
        /// <remarks>17Apr2013-01:44UTC chinajade</remarks>
        public class DescendForDismount : PrioritySelector
        {
            public DescendForDismount(ProvideDoubleDelegate maxDismountHeightDelegate)
            {
                Contract.Requires(maxDismountHeightDelegate != null, context => "maxDismountHeightDelegate != null");

                MaxDismountHeightDelegate = maxDismountHeightDelegate ?? (context => 8.0);

                Children = CreateChildren();
            }


            // BT contruction-time properties...
            private ProvideDoubleDelegate MaxDismountHeightDelegate { get; set; }


            private List<Composite> CreateChildren()
            {
                return new List<Composite>()
                {
                    // Descend, if needed...
                    new Decorator(context => !IsReadyToDismount(context),
                        new PrioritySelector(
                            new Decorator(context => !Me.MovementInfo.IsDescending,
                                new Action(context =>
                                {
                                    TreeRoot.StatusText = "Descending before dismount";
                                    WoWMovement.Move(WoWMovement.MovementDirection.Descend);
                                })),
                            new Action(context =>
                            {
                                const double probeHeight = 400.0;
                                var height = Me.GetTraceLinePos().HeightOverGroundOrWater(probeHeight);

                                TreeRoot.StatusText = string.Format("Descending from {0}",
                                    ((height > probeHeight) ? "unknown height" : string.Format("{0:F1}", height)));
                            })
                        )),

                    // Stop descending...
                    new Decorator(context => Me.MovementInfo.IsDescending,
                        new Action(context =>
                        {
                            TreeRoot.StatusText = "Descent Stopped";
                            WoWMovement.MoveStop(WoWMovement.MovementDirection.Descend);
                        }))
                };
            }

            private bool IsReadyToDismount(object context)
            {
                return !Me.IsFlying
                        || (Me.GetTraceLinePos().HeightOverGroundOrWater() < MaxDismountHeightDelegate(context));
            }
        }
    }


    public partial class UtilityBehaviorPS
    {
        /// <summary>
        /// Mounts or Dismounts according to the provided MOUNTSTRATEGYDELEGATE.
        /// <para>Notes:<list type="bullet">
        /// <item><description><para> * A "CancelShapeshift" will cancel _any_ shapeshift form whether or not
        /// that form represents a 'mounted' form or not.</para></description></item>
        /// <item><description><para> * A "Dismount" will unmount, or cancel a 'mounted' shapeshift form.
        /// Examples of the latter include: Druid Flight Form, Druid Travel Form, Shaman Ghost Wolf, Worgen Running Wild.
        /// </para></description></item>
        /// <item><description><para> * Requests to "Mount" will only be honored if the area allows it.</para></description></item>
        /// </list></para>
        /// </summary>
        /// <param name="mountStrategyDelegate"></param>
        /// <returns></returns>
        /// <remarks>17Apr2013-03:11UTC chinajade</remarks>
        public class ExecuteMountStrategy : PrioritySelector
        {
            public ExecuteMountStrategy(Func<object, MountStrategyType> mountStrategyDelegate,
                                        ProvideDoubleDelegate maxDismountHeightDelegate)
            {
                Contract.Requires(mountStrategyDelegate != null, context => "mountStrategyDelegate != null");
                Contract.Requires(maxDismountHeightDelegate != null, context => "maxDismountHeightDelegate != null");

                MountStrategyDelegate = mountStrategyDelegate;
                MaxDismountHeightDelegate = maxDismountHeightDelegate ?? (context => 8.0);

                Children = CreateChildren();
            }


            // BT contruction-time properties...
            private ProvideDoubleDelegate MaxDismountHeightDelegate { get; set; }
            private Func<object, MountStrategyType> MountStrategyDelegate { get; set; }

            // BT visit-time properties...
            private MountStrategyType CachedMountStrategy { get; set; }


            private List<Composite> CreateChildren()
            {
                return new List<Composite>()
                {
                    new ActionFail(context =>
                    {
                        CachedMountStrategy = MountStrategyDelegate(context);
                    }),

                    new Decorator(context => CachedMountStrategy != MountStrategyType.None,
                        new PrioritySelector(
                            new Decorator(context => Me.IsShapeshifted()
                                                    && ((CachedMountStrategy == MountStrategyType.CancelShapeshift)
                                                        || (CachedMountStrategy == MountStrategyType.DismountOrCancelShapeshift)),
                                new PrioritySelector(
                                    new UtilityBehaviorPS.DescendForDismount(MaxDismountHeightDelegate),
                                    new Action(context =>
                                    {
                                        TreeRoot.StatusText = "Canceling shapeshift form.";
                                        Lua.DoString("CancelShapeshiftForm()");
                                    })
                                )),

                            new Decorator(context => Me.IsMounted()
                                                    && ((CachedMountStrategy == MountStrategyType.Dismount)
                                                        || (CachedMountStrategy == MountStrategyType.DismountOrCancelShapeshift)),
                                new PrioritySelector(
                                    new UtilityBehaviorPS.DescendForDismount(MaxDismountHeightDelegate),
                                    new Decorator(context => Me.IsShapeshifted(),
                                        new Action(context =>
                                        {
                                            TreeRoot.StatusText = "Canceling 'mounted' shapeshift form.";
                                            Lua.DoString("CancelShapeshiftForm()");
                                        })),
                                    new Decorator(context => Me.IsMounted(),
                                        new Action(context =>
                                        {
                                            TreeRoot.StatusText = "Dismounting";

                                            // Mount.Dismount() uses the Flightor landing system, which sometimes get stuck
                                            // a yard or two above the landing zone...
                                            // So, we opt to dismount via LUA since we've controlled the landing ourselves.
                                            Lua.DoString("Dismount()");
                                        }))
                                    )),

                            new Decorator(context => !Me.IsMounted()
                                                    && (CachedMountStrategy == MountStrategyType.Mount)
                                                    && Mount.CanMount(),
                                new Action(context =>
                                {
                                    TreeRoot.StatusText = "Mounting";
                                    // We make up a destination for MountUp() that is far enough away, it will always choose to mount...
                                    Mount.MountUp(() => Me.Location.Add(1000.0, 1000.0, 1000.0));
                                }))
                    ))
                };
            }
        }
    }


    public partial class UtilityBehaviorPS
    {
        public class MountAsNeeded : PrioritySelector
        {
            public MountAsNeeded(ProvideWoWPointDelegate destinationDelegate,
                                CanRunDecoratorDelegate suppressMountUse = null)
            {
                Contract.Requires(destinationDelegate != null, context => "locationRetriever may not be null");

                DestinationDelegate = destinationDelegate;
                SuppressMountUse = suppressMountUse ?? (context => false);

                Children = CreateChildren();
            }

            // BT contruction-time properties...
            private ProvideWoWPointDelegate DestinationDelegate { get; set; }
            private CanRunDecoratorDelegate SuppressMountUse { get; set; }

            // BT visit-time properties...
            private WoWPoint CachedDestination { get; set; }

            // Convenience properties...
            private const int AuraId_AquaticForm = 1066;
            private const int AuraId_WarlockUnendingBreath = 5697;
            private const int SpellId_DruidAquaticForm = 1066;
            private const int SpellId_WarlockUnendingBreath = 5697;


            // 22Apr2013-09:02UTC chinajade
            private List<Composite> CreateChildren()
            {
                return new List<Composite>()
                {
                    new ActionFail(context =>
                    {
                        CachedDestination = DestinationDelegate(context);
                    }),

                    new CompositeThrottle(TimeSpan.FromMilliseconds(3000),
                        new PrioritySelector(
                            new Decorator(context => Me.IsSwimming,
                                new Action(context =>
                                {
                                    if (SpellManager.CanCast(SpellId_DruidAquaticForm) && !Me.HasAura(AuraId_AquaticForm))
                                        { SpellManager.Cast(SpellId_DruidAquaticForm); }

                                    else if (SpellManager.CanCast(SpellId_WarlockUnendingBreath) && !Me.HasAura(AuraId_WarlockUnendingBreath))
                                        { SpellManager.Cast(SpellId_WarlockUnendingBreath); }

                                    return RunStatus.Failure;
                                })
                            ),

                            new Decorator(context => !Me.IsSwimming,
                                new Decorator(context => !SuppressMountUse(context)
                                                        && !Me.InVehicle
                                                        && !Me.Mounted
                                                        && Mount.CanMount()
                                                        && (Mount.ShouldMount(CachedDestination)
                                                            || (Me.Location.SurfacePathDistance(CachedDestination) > CharacterSettings.Instance.MountDistance)
                                                            // This allows Me.MovementInfo.CanFly to have a chance at evaluating 'true' in rough terrain
                                                            || !Navigator.CanNavigateFully(Me.Location, CachedDestination)),
                                    new Action(context =>
                                    {
                                        Mount.MountUp(() => CachedDestination);
                                        return RunStatus.Failure;
                                    })
                                ))
                        ))
                };
            }
        }
    }
}