#region Usings

using System.Threading.Tasks;

using Styx;
using Styx.CommonBot;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.CommonBot.Coroutines;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public static partial class UtilityCoroutine
    {
        /// <summary>
        ///     Mounts or Dismounts according to the provided MOUNTSTRATEGYDELEGATE.
        ///     <para>
        ///         Notes:
        ///         <list type="bullet">
        ///             <item>
        ///                 <description>
        ///                     <para>
        ///                         * A "Dismount" will unmount, or cancel a 'mounted' shapeshift form.
        ///                         Examples of the latter include: Druid Flight Form, Druid Travel Form, Shaman Ghost Wolf, Worgen
        ///                         Running Wild.
        ///                     </para>
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <description>
        ///                     <para> * Requests to "Mount" will only be honored if the area allows it.</para>
        ///                 </description>
        ///             </item>
        ///         </list>
        ///     </para>
        /// </summary>
        /// <returns></returns>
        /// <remarks>17Apr2013-03:11UTC chinajade</remarks>
        public static async Task<bool> ExecuteMountStrategy(MountStrategyType mountStrategy, NavType navType = NavType.Fly)
        {
            if (mountStrategy == MountStrategyType.None)
                return false;

#pragma warning disable 618
            // Dismount needed?
            if (mountStrategy == MountStrategyType.Dismount ||
                mountStrategy == MountStrategyType.CancelShapeshift ||
                mountStrategy == MountStrategyType.DismountOrCancelShapeshift ||
                mountStrategy == MountStrategyType.Land)
            {
                if (!Me.Mounted)
                    return false;

                if (mountStrategy != MountStrategyType.Land)
                    return await CommonCoroutines.LandAndDismount("Requested by QB");

                if (!Me.IsFlying)
                    return false;

                return await CommonCoroutines.LandAndDismount("Land requested by QB", false);
            }
#pragma warning restore 618

            if (mountStrategy == MountStrategyType.Mount && Mount.UseMount && Mount.CanMount())
            {
                if (Me.Mounted)
                {
                    // Check if we should switch mounts
                    if (navType == NavType.Fly)
                    {
                        if (Mount.Current.IsFlyingMount || !Flightor.IsFlyableArea())
                            return false;

                        await CommonCoroutines.LandAndDismount("Switching to flying mount");
                    }
                    else
                    {
                        // If ground nav then any mount is fine.
                        return false;
                    }
                }

                if (navType == NavType.Fly)
                {
                    if (!Flightor.CanFly)
                        return false;

                    return await CommonCoroutines.SummonFlyingMount();
                }

                return await CommonCoroutines.SummonGroundMount();
            }

            return false;
        }

        /// <summary> Finds a proper location to land and dismounts. </summary>
        /// <remarks> raphus, 12/10/2013. </remarks>
        public static async Task<bool> LandAndDismount(string reason = "[QB] LandAndDismount")
        {
            return await CommonCoroutines.LandAndDismount(reason);
        }
    }
}
