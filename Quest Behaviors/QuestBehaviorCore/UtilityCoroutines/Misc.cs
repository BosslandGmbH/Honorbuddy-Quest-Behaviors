#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.WoWInternals.WoWObjects;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Frames;
using Action = System.Action;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
	public partial class UtilityCoroutine
	{
		/// <summary>
		///     <para>
		///         This coroutine waits MAXWAITTIMEINMILLISECONDSDELEGATE for item ITEMIDDELEGATE to arrive in our bags.
		///         If the item doesn't arrived within the specified time, the behavior is terminated.
		///     </para>
		/// </summary>
		public class WaitForInventoryItem : CoroutineTask<bool>
		{
			// BT contruction-time properties...
			public WaitForInventoryItem(
				Func<int> itemIdDelegate,
				Action actionOnExpiredTimerDelegate,
				Func<int> maxWaitTimeInMillisecondsDelegate = null)
			{
				Contract.Requires(itemIdDelegate != null, context => "itemIdDelegate != null");
				Contract.Requires(actionOnExpiredTimerDelegate != null, context => "actionOnExpiredTimerDelegate != null");

				ItemIdDelegate = itemIdDelegate;
				ActionOnExpiredTimerDelegate = actionOnExpiredTimerDelegate;
				MaxWaitTimeInMillisecondsDelegate = maxWaitTimeInMillisecondsDelegate ?? (() => 5000);
			}

			private Action ActionOnExpiredTimerDelegate { get; set; }
			private Func<int> ItemIdDelegate { get; set; }
			private Func<int> MaxWaitTimeInMillisecondsDelegate { get; set; }

			// BT visit-time properties...
			private WoWItem CachedWoWItem { get; set; }
			private WaitTimer WatchdogTimer_WaitForItemArrival { get; set; }

			public override async Task<bool> Run()
			{
				var itemId = ItemIdDelegate();
				CachedWoWItem = Me.CarriedItems.FirstOrDefault(i => (i.Entry == itemId));

				if (Query.IsViable(CachedWoWItem))
				{
					// Squelch the timer, so it will be available if the item disappears from our bags...
					WatchdogTimer_WaitForItemArrival = null;
					return false;
				}

				// If timer is not spinning, create & start it...
				if (WatchdogTimer_WaitForItemArrival == null)
				{
					WatchdogTimer_WaitForItemArrival = new WaitTimer(TimeSpan.FromMilliseconds(MaxWaitTimeInMillisecondsDelegate()));
					WatchdogTimer_WaitForItemArrival.Reset();
				}

				// If timer completes, time to call it quits...
				if (WatchdogTimer_WaitForItemArrival.IsFinished)
				{
					QBCLog.ProfileError(
						"{0} has not arrived in our bag within {1}.",
						Utility.GetItemNameFromId(itemId),
						Utility.PrettyTime(WatchdogTimer_WaitForItemArrival.WaitTime));
					ActionOnExpiredTimerDelegate();
				}
				else
				{
					TreeRoot.StatusText = String.Format(
						"Waiting {0} for {1} to arrive in our bags.",
						Utility.PrettyTime(WatchdogTimer_WaitForItemArrival.WaitTime),
						Utility.GetItemNameFromId(itemId));
				}

				return true;
			}
		}

		private const int ItemId_HearthStoneId = 6948;
		private const int ItemId_TheInnkeepersDaughter = 64488;
		private const int ItemId_GarrisonHearthStoneId = 110560;

		/// <summary>
		/// <para>Uses the hearthstone.</para>
		/// <para>Dismounts if mounted and stops moving before attempting to cast hearthstone. </para>
		/// <para>Does not yield until hearthstone is casted unless it can't be casted, already in hearthstone area or cast failed. </para>
		/// </summary>
		/// <param name="useGarrisonHearthstone">Use garrison hearthstone if set to <c>true</c>.</param>
		/// <param name="inHearthAreaAction">The action to take if already in hearthstone area.</param>
		/// <param name="noHearthStoneInBagsAction">The action to take if no hearthstone is in bags.</param>
		/// <param name="hearthNotSetAction">The action to take if hearth is not set.</param>
		/// <param name="hearthOnCooldownAction">The action to take if hearth is on cooldown.</param>
		/// <param name="hearthCastedAction">The action to take if hearth is successfully casted.</param>
		/// <param name="hearthCastFailedAction">The action to take if hearth failed to cast. The reason string is passed in argument.</param>
		/// <returns>Returns <c>true</c> if hearth was casted</returns>
		/// <exception cref="Exception">A delegate callback throws an exception.</exception>
		public static async Task<bool> UseHearthStone(
			bool useGarrisonHearthstone = false,
			Action inHearthAreaAction = null,
			Action noHearthStoneInBagsAction = null,
			Action hearthNotSetAction = null,
			Action hearthOnCooldownAction = null,
			Action hearthCastedAction = null,
			Action<string> hearthCastFailedAction = null)
		{
			if (IsInHearthStoneArea(useGarrisonHearthstone))
			{
				if (inHearthAreaAction != null)
					inHearthAreaAction();
				else
					QBCLog.DeveloperInfo("Already at hearthstone area");

				return false;
			}

			var hearthStones = useGarrisonHearthstone
				? GetHearthStonesByIds(ItemId_GarrisonHearthStoneId)
				: GetHearthStonesByIds(ItemId_HearthStoneId, ItemId_TheInnkeepersDaughter);

			if (!hearthStones.Any())
			{
				if (noHearthStoneInBagsAction != null)
					noHearthStoneInBagsAction();
				else
					QBCLog.DeveloperInfo("No hearthstone found in bag");
				return false;
			}

			if (!useGarrisonHearthstone && Me.HearthstoneAreaId == 0)
			{
				// I can only see this occurring if using the Innkeeper's Daughter hearthtone since the normal hearthstone
				// only shows up in bags if hearth has been set. 
				if (hearthNotSetAction != null)
					hearthNotSetAction();
				else
					QBCLog.DeveloperInfo("Hearth has not been set");
				return false;
			}

			var usableHearthstone = hearthStones.FirstOrDefault(i => !i.Effects.First().Spell.Cooldown);
			if (usableHearthstone == null)
			{
				if (hearthOnCooldownAction != null)
					hearthOnCooldownAction();
				else
					QBCLog.DeveloperInfo("Hearth is on cooldown");

				return false;
			}

			// the following coroutines execute sequentially, they do not return until dismounted or movement has stopped.
			await CommonCoroutines.LandAndDismount();
			await CommonCoroutines.StopMoving();

			// Close any frame that can prevent hearthstone use... 
			// For example WoW will try to sell to hearthstone if merchant frame is open when hearthstone is used
			await CloseFrames();

			var hearthstoneSpell = usableHearthstone.Effects.First().Spell;
			using (var castMonitor = SpellCastMonitor.Start(hearthstoneSpell.Id))
			{
				QBCLog.DeveloperInfo("Using hearthstone: {0}", hearthstoneSpell.Name);

				usableHearthstone.UseContainerItem();
				var castResult = await castMonitor.GetResult(12000);
				if (castResult == SpellCastResult.Succeeded)
				{
					await Coroutine.Wait(2000, () => IsInHearthStoneArea(useGarrisonHearthstone));
					if (hearthCastedAction != null)
						hearthCastedAction();
					else
						QBCLog.DeveloperInfo("Successfully used hearthstone");
					return true;
				}

				string reason = castResult == SpellCastResult.UnknownFail ? castMonitor.FailReason : castResult.ToString();

				if (hearthCastFailedAction != null)
					hearthCastFailedAction(reason);
				else
					QBCLog.Warning("Cast of {0} failed. Reason: {1}", hearthstoneSpell.Name, reason);
				return false;
			}
		}

		private static async Task CloseFrames()
		{
			if (MerchantFrame.Instance.IsVisible)
			{
				MerchantFrame.Instance.Close();
				await Coroutine.Wait(2000, () => !MerchantFrame.Instance.IsVisible);
			}

			if (MailFrame.Instance.IsVisible)
			{
				MailFrame.Instance.Close();
				await Coroutine.Wait(2000, () => !MailFrame.Instance.IsVisible);
			}

			using (var bankFrame = new GuildBankFrame())
			{
				if (bankFrame.IsVisible)
				{
					bankFrame.Close();
					await Coroutine.Wait(2000, () => !bankFrame.IsVisible);
				}	
			}

		}

		private static bool IsInHearthStoneArea (bool useGarrisonHearthstone = false)
		{
			if (useGarrisonHearthstone)
				return Me.CurrentMap.IsGarrison;

			return Me.HearthstoneAreaId == Me.SubZoneId;
		}

		private static List<WoWItem> GetHearthStonesByIds(params uint[] hearthstoneIds)
		{
			return Me.BagItems.Where(i => i != null && i.ItemInfo != null
				&& hearthstoneIds.Contains(i.Entry)).ToList();
		}

	}
}