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

using Styx.CommonBot;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public abstract partial class QuestBehaviorBase
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
        public Composite UtilityBehaviorPS_DescendForDismount(ProvideDoubleDelegate maxDismountHeightDelegate = null)
        {
            maxDismountHeightDelegate = maxDismountHeightDelegate ?? (context => 8.0);

            Func<object, bool> isReadyToDismount = (context =>
            {
                return !Me.IsFlying
                        || (Me.GetTraceLinePos().HeightOverGroundOrWater() < maxDismountHeightDelegate(context));
            });

            return new PrioritySelector(
                // Descend, if needed...
                new Decorator(context => !isReadyToDismount(context),
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
            );
        }


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
        public Composite UtilityBehaviorPS_ExecuteMountStrategy(Func<object, MountStrategyType> mountStrategyDelegate)
        {
            Contract.Requires(mountStrategyDelegate != null, context => "mountStrategyDelegate != null");

            return new Decorator(context => mountStrategyDelegate(context) != MountStrategyType.None,
                new PrioritySelector(
                    new Decorator(context => Me.IsShapeshifted()
                                            && ((mountStrategyDelegate(context) == MountStrategyType.CancelShapeshift)
                                                || (mountStrategyDelegate(context) == MountStrategyType.DismountOrCancelShapeshift)), 
                        new PrioritySelector(
                            UtilityBehaviorPS_DescendForDismount(context => MaxDismountHeight),
                            new Action(context =>
                            {
                                TreeRoot.StatusText = "Canceling shapeshift form.";
                                Lua.DoString("CancelShapeshiftForm()");
                            })
                        )),

                    new Decorator(context => Me.IsMounted()
                                            && ((mountStrategyDelegate(context) == MountStrategyType.Dismount)
                                                || (mountStrategyDelegate(context) == MountStrategyType.DismountOrCancelShapeshift)),
                        new PrioritySelector(
                            UtilityBehaviorPS_DescendForDismount(context => MaxDismountHeight),
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
                                            && (mountStrategyDelegate(context) == MountStrategyType.Mount)
                                            && Mount.CanMount(),
                        // We make up a destination for MountUp() that is far enough away, it will always choose to mount...
                        new Action(context =>
                        {
                            TreeRoot.StatusText = "Mounting";
                            Mount.MountUp(() => Me.Location.Add(1000.0, 1000.0, 1000.0));
                        }))
            ));
        }


        // 22Apr2013-09:02UTC chinajade
        public Composite UtilityBehaviorPS_MountAsNeeded(ProvideWoWPointDelegate destinationDelegate,
                                                            CanRunDecoratorDelegate suppressMountUse = null)
        {
            Contract.Requires(destinationDelegate != null, context => "locationRetriever may not be null");
            suppressMountUse = suppressMountUse ?? (context => false);

            const int AuraId_AquaticForm = 1066;
            const int AuraId_WarlockUnendingBreath = 5697;
            const int SpellId_DruidAquaticForm = 1066;
            const int SpellId_WarlockUnendingBreath = 5697;

            return
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
                            new Decorator(context => !suppressMountUse(context)
                                                    && !Me.InVehicle
                                                    && !Me.Mounted
                                                    && Mount.CanMount()
                                                    && (Mount.ShouldMount(_ubpsMoveTo_Location)
                                                        || (Me.Location.SurfacePathDistance(_ubpsMoveTo_Location) > CharacterSettings.Instance.MountDistance)
                                                        // This allows Me.MovementInfo.CanFly to have a chance at evaluating 'true' in rough terrain
                                                        || !Navigator.CanNavigateFully(Me.Location, _ubpsMoveTo_Location)),
                                new Action(context =>
                                {
                                    Mount.MountUp(() => destinationDelegate(context));
                                    return RunStatus.Failure;
                                })
                            ))
                    ));
        }
    }
}