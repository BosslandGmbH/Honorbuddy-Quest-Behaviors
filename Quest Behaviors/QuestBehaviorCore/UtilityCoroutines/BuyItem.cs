// Originally contributed by HighVoltz.
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
using System.Numerics;
using System.Threading.Tasks;

using Buddy.Coroutines;
using Styx;
using Styx.CommonBot;
using Styx.Pathing;
using Styx.WoWInternals.WoWObjects;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Frames;
using Styx.WoWInternals;

#endregion

namespace Honorbuddy.QuestBehaviorCore
{
    public static partial class UtilityCoroutine
    {
        /// <summary>Gossips with the specified wow object. Hearthstone bind popups are automatically accepted</summary>
        /// <param name="wowObjectId">The wow object identifier.</param>
        /// <param name="searchLocation">The search location of <paramref name="wowObjectId" />.</param>
        /// <param name="itemId">The item identifier.</param>
        /// <param name="quantity">The quantity to buy.</param>
        /// <param name="movementBy">The movement type to use.</param>
        /// <param name="navigationFailedAction">
        ///     The action to take if <paramref name="wowObjectId" /> or <paramref name="searchLocation"/> cant be navigated to
        /// </param>
        /// <param name="notFoundAction">The action to take if
        ///     <paramref name="wowObjectId" /> is not found at
        ///     <paramref name="searchLocation" />.</param>
        /// <param name="noVendorFrameAction">The action to take if interaction with
        ///     <paramref name="wowObjectId" /> didn't open a vendor frame.
        /// </param>
        /// <param name="itemNotFoundAction">The action to take if <paramref name="wowObjectId"/> does not offer <paramref name="itemId"/> </param>
        /// <param name="insufficientFundsAction">The action to take if toon doesn't have enough funds to buy <paramref name="itemId"/> </param>
        /// <returns></returns>
        /// <exception cref="Exception">A delegate callback throws an exception.</exception>
        public static async Task<bool> BuyItem(
            int wowObjectId,
            Vector3 searchLocation,
            int itemId,
            int quantity,
            MovementByType movementBy = MovementByType.FlightorPreferred,
            Action navigationFailedAction = null,
            Action notFoundAction = null,
            Action noVendorFrameAction = null,
            Action itemNotFoundAction = null,
            Action insufficientFundsAction = null)
        {
            return await BuyItem(
                        ObjectManager.ObjectList.FirstOrDefault(o => o.Entry == wowObjectId),
                        searchLocation,
                        itemId,
                        quantity,
                        movementBy,
                        navigationFailedAction,
                        notFoundAction,
                        noVendorFrameAction,
                        itemNotFoundAction,
                        insufficientFundsAction);
        }

        /// <summary>Buys an item from the specified wow object.</summary>
        /// <param name="wowObject">The wow object. Navigates to <paramref name="searchLocation" /> null</param>
        /// <param name="searchLocation">The search location of <paramref name="wowObject" />.</param>
        /// <param name="itemId">The item identifier.</param>
        /// <param name="quantity">The quantity to buy.</param>
        /// <param name="movementBy">The movement type to use.</param>
        /// <param name="navigationFailedAction">
        ///     The action to take if <paramref name="wowObject" /> or
        ///     <paramref name="searchLocation" /> cant be navigated to
        /// </param>
        /// <param name="notFoundAction">
        ///     The action to take if
        ///     <paramref name="wowObject" /> is not found at
        ///     <paramref name="searchLocation" />.
        /// </param>
        /// <param name="noVendorFrameAction">
        ///     The action to take if interaction with
        ///     <paramref name="wowObject" /> didn't open a vendor frame.
        /// </param>
        /// <param name="itemNotFoundAction">The action to take if <paramref name="wowObject"/> does not offer <paramref name="itemId"/> </param>
        /// <param name="insufficientFundsAction">The action to take if toon doesn't have enough funds to buy <paramref name="itemId"/> </param>
        /// <returns></returns>
        /// <exception cref="Exception">A delegate callback throws an exception.</exception>
        public static async Task<bool> BuyItem(
            WoWObject wowObject,
            Vector3 searchLocation,
            int itemId,
            int quantity,
            MovementByType movementBy = MovementByType.FlightorPreferred,
            Action navigationFailedAction = null,
            Action notFoundAction = null,
            Action noVendorFrameAction = null,
            Action itemNotFoundAction = null,
            Action insufficientFundsAction = null)
        {
            if (!MerchantFrame.Instance.IsVisible)
            {
                return await Gossip(
                            wowObject,
                            searchLocation,
                            movementBy,
                            navigationFailedAction,
                            notFoundAction,
                            null,
                            noVendorFrameAction,
                            GossipEntry.GossipEntryType.Vendor);
            }

            var item = MerchantFrame.Instance.GetAllMerchantItems().FirstOrDefault(i => i.ItemId == itemId);

            if (item == null)
            {
                if (itemNotFoundAction != null)
                    itemNotFoundAction();
                return false;
            }

            if (!MerchantFrame.Instance.BuyItem(item.Index, quantity))
            {
                if (insufficientFundsAction != null)
                    insufficientFundsAction();
                return false;
            }

            await CommonCoroutines.SleepForRandomUiInteractionTime();
            MerchantFrame.Instance.Close();
            await CommonCoroutines.SleepForLagDuration();
            return true;
        }
    }
}