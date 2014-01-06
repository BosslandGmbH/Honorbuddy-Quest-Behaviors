using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Buddy.Coroutines;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Action = System.Action;

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
		///                         * A "CancelShapeshift" will cancel _any_ shapeshift form whether or not
		///                         that form represents a 'mounted' form or not.
		///                     </para>
		///                 </description>
		///             </item>
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
		public static IEnumerator ExecuteMountStrategy(MountStrategyType mountStrategy, NavType navType = NavType.Fly)
		{
			if (mountStrategy == MountStrategyType.None)
			{
				yield return false;
				yield break;
			}

			// Cancel Shapeshift needed?
			if (((mountStrategy == MountStrategyType.CancelShapeshift)
				|| (mountStrategy == MountStrategyType.DismountOrCancelShapeshift))
				&& Me.IsShapeshifted())
			{
				if (WoWMovement.ActiveMover.IsFlying)
				{
					yield return LandAndDismount();
					yield break;
				}
				TreeRoot.StatusText = "Canceling shapeshift form.";
				Lua.DoString("CancelShapeshiftForm()");
				yield return true;
				yield break;
			}

			// Dismount needed?
			if (((mountStrategy == MountStrategyType.Dismount)
				|| (mountStrategy == MountStrategyType.DismountOrCancelShapeshift))
				&& Me.IsMounted())
			{
				if (WoWMovement.ActiveMover.IsFlying)
				{
					yield return LandAndDismount();
					yield break;
				}
				if (Me.IsShapeshifted())
				{
					TreeRoot.StatusText = "Canceling 'mounted' shapeshift form.";
					Lua.DoString("CancelShapeshiftForm()");
					yield return true;
					yield break;
				}
				if (Me.IsMounted())
				{
					TreeRoot.StatusText = "Dismounting";

					// Mount.Dismount() uses the Flightor landing system, which sometimes get stuck
					// a yard or two above the landing zone...
					// So, we opt to dismount via LUA since we've controlled the landing ourselves.
					Lua.DoString("Dismount()");
					yield return true;
					yield break;
				}
			}

			if ((mountStrategy == MountStrategyType.Mount) && Mount.UseMount)
			{
				// Flying and Ground mounts...
				if (!WoWMovement.ActiveMover.IsSwimming)
				{
					// If flying mount is wanted, and we're not on flying mount...
					if (navType == NavType.Fly && !Flightor.MountHelper.Mounted && Flightor.MountHelper.CanMount)
					{
						Flightor.MountHelper.MountUp();
						yield return true;
						yield break;
					}
					// Try ground mount...
					// NB: Force mounting by specifying a large distance to destination...
					if (!Me.Mounted && Mount.CanMount())
					{
						if (Mount.MountUp(() => true, () => WoWMovement.ActiveMover.Location.Add(1000.0, 1000.0, 1000.0)))
						{
							yield return true;
							yield break;
						}
					}
				}
				else
				{
					// Swimming mounts...
					yield return false;
				}
			}

			yield return false;
		}

		/// <summary> Finds a proper location to land and dismounts. </summary>
		/// <remarks> raphus, 12/10/2013. </remarks>
		public static IEnumerator LandAndDismount(string reason = "[QB] LandAndDismount")
		{
			var landAndDismountAction = new Mount.ActionLandAndDismount(reason);
			yield return StyxCoroutine.CompositeExecutor(landAndDismountAction);
		}

	}
}