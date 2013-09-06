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
            public DescendForDismount(ProvideStringDelegate reasonDelegate = null)
            {
                MaxDismountHeightDelegate = (context => 8.0);
                ReasonDelegate = reasonDelegate ?? (context => string.Empty);

                Children = CreateChildren();
            }


            // BT contruction-time properties...
            private ProvideDoubleDelegate MaxDismountHeightDelegate { get; set; }
            private ProvideStringDelegate ReasonDelegate { get; set; }


            private List<Composite> CreateChildren()
            {
                return new List<Composite>()
                {
                    // Descend, if needed...
                    new Decorator(context => !IsReadyToDismount(context),
                        new PrioritySelector(
                            new Decorator(context => !WoWMovement.ActiveMover.MovementInfo.IsDescending,
                                new Action(context =>
                                {
                                    var reason = ReasonDelegate(context);

                                    TreeRoot.StatusText = "Descending before dismount"
                                        + (!string.IsNullOrEmpty(reason) ? (": " + reason) : string.Empty);
                                    WoWMovement.Move(WoWMovement.MovementDirection.Descend);
                                })),
                            new CompositeThrottle(TimeSpan.FromMilliseconds(1000),
                                new Action(context =>
                                {
                                    const double probeHeight = 400.0;
                                    var height = WoWMovement.ActiveMover.GetTraceLinePos().HeightOverGroundOrWater(probeHeight);

                                    QBCLog.DeveloperInfo("Descending from {0}",
                                        ((height > probeHeight) ? "unknown height" : string.Format("{0:F1}", height)));
                                }))
                        )),

                    // Stop descending...
                    new Decorator(context => WoWMovement.ActiveMover.MovementInfo.IsDescending,
                        new Action(context =>
                        {
                            QBCLog.DeveloperInfo("Descent Stopped");
                            WoWMovement.MoveStop(WoWMovement.MovementDirection.Descend);
                        }))
                };
            }

            private bool IsReadyToDismount(object context)
            {
                return !WoWMovement.ActiveMover.IsFlying
                        || (WoWMovement.ActiveMover.GetTraceLinePos().HeightOverGroundOrWater() < MaxDismountHeightDelegate(context));
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
                                        ProvideNavTypeDelegate navTypeDelegate = null)
            {
                Contract.Requires(mountStrategyDelegate != null, context => "mountStrategyDelegate != null");

                MountStrategyDelegate = mountStrategyDelegate;
                NavTypeDelegate = navTypeDelegate ?? (context => NavType.Fly);

                Children = CreateChildren();
            }


            // BT contruction-time properties...
            private Func<object, MountStrategyType> MountStrategyDelegate { get; set; }
            private ProvideNavTypeDelegate NavTypeDelegate { get; set; }

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
                            // Cancel Shapeshift needed?
                            new Decorator(context => ((CachedMountStrategy == MountStrategyType.CancelShapeshift)
                                                        || (CachedMountStrategy == MountStrategyType.DismountOrCancelShapeshift))
                                                      && Me.IsShapeshifted(),
                                new PrioritySelector(
                                    // Need to land, if flying...
                                    new UtilityBehaviorPS.DescendForDismount(),

                                    // NB: Some quest behaviors use this to cancel _any_ shapeshifted form, even when not flying.
                                    // So please keep that in mind while maintaining code.
                                    new Action(context =>
                                    {
                                        TreeRoot.StatusText = "Canceling shapeshift form.";
                                        Lua.DoString("CancelShapeshiftForm()");
                                    })
                                )),

                            // Dismount needed?
                            new Decorator(context => ((CachedMountStrategy == MountStrategyType.Dismount)
                                                        || (CachedMountStrategy == MountStrategyType.DismountOrCancelShapeshift))
                                                      && Me.IsMounted(),
                                new PrioritySelector(
                                    new UtilityBehaviorPS.DescendForDismount(),
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

                            // Mount needed?
                            new Decorator(context => CachedMountStrategy == MountStrategyType.Mount,
                                new PrioritySelector(
                                    // Flying and Ground mounts...
                                    new Decorator(context => !WoWMovement.ActiveMover.IsSwimming,
                                        new PrioritySelector(
                                            // If flying mount is wanted, and we're not on flying mount...
                                            new Decorator(context => NavTypeDelegate(context) == NavType.Fly
                                                                    && !Flightor.MountHelper.Mounted
                                                                    && Flightor.MountHelper.CanMount,
                                                new Action(context => Flightor.MountHelper.MountUp())),

                                            // Try ground mount...
                                            // NB: Force mounting by specifying a large distance to destination...
                                            new Decorator(context => !Me.Mounted && Mount.CanMount(),
                                                new Action(context =>
                                                {
                                                    return Mount.MountUp(() => true, () => WoWMovement.ActiveMover.Location.Add(1000.0, 1000.0, 1000.0))
                                                        ? RunStatus.Success
                                                        : RunStatus.Failure;
                                                }))
                                        )),

                                    // Swimming mounts...
                                    new Decorator(context => WoWMovement.ActiveMover.IsSwimming,
                                        // TODO: Future enhancement--deal with swimming mounts
                                        new ActionFail())
                                ))
                        ))
                };
            }
        }
    }
}