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
using System.Linq;
using System.Threading.Tasks;
using Bots.Grind;
using Buddy.Coroutines;

using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
	public static partial class UtilityCoroutine
	{
		/// <summary>
		/// Uses the transport.
		/// </summary>
		/// <param name="transportId">The transport identifier.</param>
		/// <param name="startLocation">The start location.</param>
		/// <param name="endLocation">The end location.</param>
		/// <param name="waitAtLocation">The wait at location.</param>
		/// <param name="standAtLocation">The stand at location.</param>
		/// <param name="getOffLocation">The get off location.</param>
		/// <param name="movement">The movement.</param>
		/// <param name="destination">The destination.</param>
		/// <param name="navigationFailedAction">
		///     The action to take if <paramref name="waitAtLocation" /> cant be navigated to
		/// </param>
		/// <returns>returns <c>true</c> until done</returns>
		/// <exception cref="Exception">A delegate callback throws an exception. </exception>
		public static async Task<bool> UseTransport(
			int transportId,
			WoWPoint startLocation,
			WoWPoint endLocation,
			WoWPoint waitAtLocation,
			WoWPoint standAtLocation,
			WoWPoint getOffLocation,
			MovementByType movement = MovementByType.FlightorPreferred,
			string destination = null,
			Action navigationFailedAction = null)
		{
			if (getOffLocation != WoWPoint.Empty && Me.Location.DistanceSqr(getOffLocation) < 2 * 2)
			{
				return false;
			}

			var transportLocation = GetTransportLocation(transportId);
			if (transportLocation != WoWPoint.Empty
				&& transportLocation.DistanceSqr(startLocation) < 1.5 * 1.5
				&& waitAtLocation.DistanceSqr(Me.Location) < 2 * 2)
			{
				TreeRoot.StatusText = "Moving inside transport";
				Navigator.PlayerMover.MoveTowards(standAtLocation);
				await CommonCoroutines.SleepForLagDuration();
				// wait for bot to get on boat.
				await Coroutine.Wait(12000, () => !Me.IsMoving || Navigator.AtLocation(standAtLocation));
			}

			// loop while on transport to prevent bot from doing anything else
			while (Me.Transport != null && Me.Transport.Entry == transportId)
			{
				if (transportLocation != WoWPoint.Empty && transportLocation.DistanceSqr(endLocation) < 1.5 * 1.5)
				{
					TreeRoot.StatusText = "Moving out of transport";
					Navigator.PlayerMover.MoveTowards(getOffLocation);
					await CommonCoroutines.SleepForLagDuration();
					// Sleep until we stop moving.
					await Coroutine.Wait(12000, () => !Me.IsMoving || Navigator.AtLocation(getOffLocation));
					return true;
				}

				// Exit loop if in combat or dead.
				if (Me.Combat || !Me.IsAlive)
					return false;

				TreeRoot.StatusText = "Waiting for the end location";
				await Coroutine.Yield();
				// update transport location.
				transportLocation = GetTransportLocation(transportId);
			}

			if (waitAtLocation.DistanceSqr(Me.Location) > 2 * 2)
			{
				if (!await MoveTo(waitAtLocation, destination ?? waitAtLocation.ToString(), movement))
				{
					if (navigationFailedAction != null)
						navigationFailedAction();
				}
				return true;
			}
			await CommonCoroutines.LandAndDismount();
			TreeRoot.StatusText = "Waiting for transport";
			return true;
		}

		private static WoWPoint GetTransportLocation(int transportId)
		{
			var transport = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(o => o.Entry == transportId);
			return transport != null ? transport.WorldLocation : WoWPoint.Zero;
		}
	}
}