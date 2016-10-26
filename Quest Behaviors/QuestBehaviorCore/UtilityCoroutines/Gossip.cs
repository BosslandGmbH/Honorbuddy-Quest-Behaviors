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
        /// <summary>
        ///     <para>Gossips with the specified wow object. </para>
        ///     <para>Hearthstone bind popups are automatically accepted</para>
        /// </summary>
        /// <param name="wowObjectId">The wow object identifier.</param>
        /// <param name="searchLocation">The search location of <paramref name="wowObjectId" />.</param>
        /// <param name="movementBy">The movement type to use.</param>
        /// <param name="navigationFailedAction">
        ///     The action to take if <paramref name="wowObjectId" /> or <paramref name="searchLocation"/> cant be navigated to
        /// </param>
        /// <param name="notFoundAction">The action to take if
        ///     <paramref name="wowObjectId" /> is not found at
        ///     <paramref name="searchLocation" />.</param>
        /// <param name="noGossipFrameAction">The action to take if interaction with
        ///     <paramref name="wowObjectId" /> didn't open a gossip frame.</param>
        /// <param name="noMatchingGossipOptionAction">
        ///     <para>The action to take if the passed in gossip type and/or gossip indices </para>
        ///     <para>doesn't match what was offered by <paramref name="wowObjectId" />.</para>
        /// </param>
        /// <param name="gossipEntryType">
        ///     <para>Type gossip entry type to select. If none of this type are found on current page then</para>
        ///     <para> normal gossip types are clicked through in hopes of ending on a page with this gossip type</para>
        /// </param>
        /// <param name="gossipIndexes">The gossip indexes to follow through. Has precedence over
        ///     <paramref name="gossipEntryType" />.</param>
        /// <returns></returns>
        /// <exception cref="Exception">A delegate callback throws an exception.</exception>
        public static async Task<bool> Gossip(
            int wowObjectId,
            Vector3 searchLocation,
            MovementByType movementBy = MovementByType.FlightorPreferred,
            Action navigationFailedAction = null,
            Action notFoundAction = null,
            Action noGossipFrameAction = null,
            Action noMatchingGossipOptionAction = null,
            GossipEntry.GossipEntryType gossipEntryType = GossipEntry.GossipEntryType.Unknown,
            params int[] gossipIndexes)
        {
            return await Gossip(
                        ObjectManager.ObjectList
                            .Where(o => o.Entry == wowObjectId)
                            .OrderBy(o => o.DistanceSqr).FirstOrDefault(),
                        searchLocation,
                        movementBy,
                        navigationFailedAction,
                        notFoundAction,
                        noGossipFrameAction,
                        noMatchingGossipOptionAction,
                        gossipEntryType,
                        gossipIndexes);
        }

        /// <summary>Gossips with the specified wow object. Hearthstone bind popups are automatically accepted</summary>
        /// <param name="wowObject">The wow object. Navigates to <paramref name="searchLocation" /> null </param>
        /// <param name="searchLocation">The search location of <paramref name="wowObject" />.</param>
        /// <param name="movementBy">The movement type to use.</param>
        /// <param name="navigationFailedAction">
        ///     The action to take if <paramref name="wowObject" /> or <paramref name="searchLocation"/> cant be navigated to
        /// </param>
        /// <param name="notFoundAction">
        ///     The action to take if <paramref name="wowObject" /> is not found at
        ///     <paramref name="searchLocation" />.
        /// </param>
        /// <param name="noGossipFrameAction">
        ///     The action to take if interaction with <paramref name="wowObject" /> didn't open a
        ///     gossip frame.
        /// </param>
        /// <param name="noMatchingGossipOptionAction">
        ///     <para>The action to take if the passed in gossip type and/or gossip indices </para>
        ///     <para>doesn't match what was offered by <paramref name="wowObject" />.</para>
        /// </param>
        /// <param name="gossipEntryType">
        ///     <para>Type gossip entry type to select. Ignored if set to Unknown.</para>
        ///		<para>If none of this type are found on current page then</para>
        ///     <para> normal gossip types are clicked through in hopes of ending on a page with this gossip type</para>
        /// </param>
        /// <param name="gossipIndexes">
        ///     The gossip indexes to follow through. Has precedence over
        ///     <paramref name="gossipEntryType" />.
        /// </param>
        /// <exception cref="Exception">A delegate callback throws an exception.</exception>
        public static async Task<bool> Gossip(
            WoWObject wowObject,
            Vector3 searchLocation,
            MovementByType movementBy = MovementByType.FlightorPreferred,
            Action navigationFailedAction = null,
            Action notFoundAction = null,
            Action noGossipFrameAction = null,
            Action noMatchingGossipOptionAction = null,
            GossipEntry.GossipEntryType gossipEntryType = GossipEntry.GossipEntryType.Unknown,
            params int[] gossipIndexes)
        {
            if (wowObject == null)
            {
                if (!Navigator.AtLocation(searchLocation))
                {
                    if (await MoveTo(searchLocation, "Gossip object search area", movementBy))
                        return true;

                    navigationFailedAction?.Invoke();
                    return false;
                }

                if (notFoundAction != null)
                    notFoundAction();
                else
                    TreeRoot.StatusText = "Waiting for the WoW object selected for gossip to spawn";
                return true;
            }

            if (!wowObject.WithinInteractRange)
            {
                if (await MoveTo(wowObject.Location, wowObject.SafeName, movementBy, wowObject.InteractRange))
                    return true;

                navigationFailedAction?.Invoke();
                return false;
            }

            if (await CommonCoroutines.Dismount("Gossiping with " + wowObject.SafeName))
                await Coroutine.Sleep(Delay.BeforeButtonClick);

            // If gossip frame is open then we must assume that it doesn't belong to the selected gossip object at this point
            if (GossipFrame.Instance.IsVisible)
            {
                GossipFrame.Instance.Close();
                return true;
            }

            Func<bool> isFrameReadyForInput =
                () =>
                    GossipFrame.Instance.IsVisible &&
                    (GossipFrame.Instance.GossipOptionEntries != null ||
                     (!gossipIndexes.Any() && gossipEntryType == GossipEntry.GossipEntryType.Unknown));

            wowObject.Interact();
            var openedGossipFrame = await Coroutine.Wait(3000, isFrameReadyForInput);

            if (!openedGossipFrame)
            {
                QBCLog.Warning("No gossip frame was opened after interacting with {0}", wowObject.SafeName);
                noGossipFrameAction?.Invoke();

                return false;
            }

            int gossipPage = 1;
            // Click through all the gossip indices
            for (var i = 0; i < gossipIndexes.Length; i++)
            {
                var index = gossipIndexes[i] - 1;

                var gossipEntry =
                    GossipFrame.Instance.GossipOptionEntries.Where(g => g.Index == index)
                               .Select(g => (GossipEntry?)g)
                               .FirstOrDefault();

                if (!gossipEntry.HasValue || gossipEntry.Value.Type == GossipEntry.GossipEntryType.Unknown)
                {
                    QBCLog.Warning("{0} does not provide a gossip at index {1} on page {2}", wowObject.SafeName, index + 1, gossipPage);
                    noMatchingGossipOptionAction?.Invoke();
                    return false;
                }

                await ClickGossipOption(gossipEntry.Value, gossipPage);

                // make sure frame didn't close before we're done.
                if (!isFrameReadyForInput() && (i < gossipIndexes.Length - 1 || gossipEntryType != GossipEntry.GossipEntryType.Unknown))
                {
                    // This can happen if some external event causes object to stop offering gossip frame, such as NPC getting into combat.
                    // Usually this can be fixed by interacting with object again at a later time. We let the caller handle this.
                    QBCLog.Warning("Gossip frame for {0} closed unexpectedly.", wowObject.SafeName);
                    return true;
                }
                gossipPage++;
            }

            if (gossipEntryType != GossipEntry.GossipEntryType.Unknown)
            {
                if (!gossipIndexes.Any())
                {
                    while (true)
                    {
                        var gossipEntry = GossipFrame.Instance.GossipOptionEntries.FirstOrDefault(g => g.Type == gossipEntryType);
                        // If no gossip indices were specified then we just click through more gossip,
                        // hopefully it leads to the final gossip type
                        if (gossipEntry.Type != gossipEntryType)
                        {
                            gossipEntry =
                                GossipFrame.Instance.GossipOptionEntries.FirstOrDefault(g => g.Type == GossipEntry.GossipEntryType.Gossip);
                        }

                        if (gossipEntry.Type == GossipEntry.GossipEntryType.Unknown)
                        {
                            QBCLog.Warning("{0} does not provide a {0} gossip type", wowObject.SafeName, gossipEntryType);
                            noMatchingGossipOptionAction?.Invoke();
                            return false;
                        }

                        await ClickGossipOption(gossipEntry, gossipPage);

                        if (!isFrameReadyForInput() && gossipEntry.Type != gossipEntryType)
                        {
                            // This can happen if some external event causes object to stop offering gossip frame, such as NPC getting into combat.
                            // Usually this can be fixed by interacting with object again at a later time. We let the caller handle this.
                            QBCLog.Warning("Gossip frame for {0} closed unexpectedly.", wowObject.SafeName);
                            return true;
                        }

                        if (gossipEntry.Type == gossipEntryType)
                            break;

                        gossipPage++;
                    }
                }
            }

            // Set hearthstone automatically
            const string setHsPopupName = "CONFIRM_BINDER";
            if (Lua.GetReturnVal<bool>($"return StaticPopup_Visible('{setHsPopupName}')", 0))
            {
                uint hsId = StyxWoW.Me.HearthstoneAreaId;
                Lua.DoString(
                    $"local _,frame = StaticPopup_Visible('{setHsPopupName}') if frame then StaticPopup_OnClick(frame, 1) end");

                if (await Coroutine.Wait(5000, () => StyxWoW.Me.HearthstoneAreaId != hsId))
                {
                    await CommonCoroutines.SleepForRandomReactionTime();
                    var boundLocation = Lua.GetReturnVal<string>("return GetBindLocation()", 0);

                    QBCLog.Info(
                        "You are now bound at {0} Inn in {1}({2})",
                        (Query.IsViable(wowObject) ? wowObject.SafeName : "the"),
                        boundLocation,
                        Me.HearthstoneAreaId);
                }
            }
            return true;
        }

        private static async Task ClickGossipOption(GossipEntry gossipEntry, int gossipPage)
        {
            // Log the gossip option we're about to take...
            QBCLog.DeveloperInfo(
                "Selecting Gossip Option({0}) on page {1}: \"{2}\"",
                gossipEntry.Index + 1,
                gossipPage,
                gossipEntry.Text);

            GossipFrame.Instance.SelectGossipOption(gossipEntry.Index);
            await CommonCoroutines.SleepForLagDuration();
            await CommonCoroutines.SleepForRandomUiInteractionTime();
        }
    }
}
