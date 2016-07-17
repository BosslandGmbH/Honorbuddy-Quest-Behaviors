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
using System.Threading.Tasks;
using Bots.DungeonBuddy.Helpers;
using Buddy.Coroutines;
using Styx;
using Styx.CommonBot;
using Styx.Pathing;
using Styx.WoWInternals.WoWObjects;
using Styx.CommonBot.Coroutines;
using Styx.WoWInternals;

#endregion

namespace Honorbuddy.QuestBehaviorCore
{
    public static partial class UtilityCoroutine
    {
        /// <summary>Turns in a quest at object </summary>
        /// <param name="wowObjectId"> The turnin object. </param>
        /// <param name="questId"> The quest Id. If 0 (default) then first completed quest is turned in. </param>
        /// <param name="searchLocation">The search location of <paramref name="wowObjectId" />.</param>
        /// <param name="movementBy">The movement type to use.</param>
        /// <param name="navigationFailedAction">
        ///     The action to take if <paramref name="wowObjectId" /> or <paramref name="searchLocation"/> cant be navigated to
        /// </param>
        /// <param name="notFoundAction">
        ///     The action to take if <paramref name="wowObjectId" /> is not found at
        ///     <paramref name="searchLocation" />.
        /// </param>
        /// <returns><c>true</c> if an action was taken; <c>false</c> otherwise</returns>
        /// <exception cref="Exception">A delegate callback throws an exception.</exception>
        public static async Task<bool> TurninQuest(
            int wowObjectId,
            WoWPoint searchLocation,
            uint questId = 0,
            MovementByType movementBy = MovementByType.FlightorPreferred,
            Action navigationFailedAction = null,
            Action notFoundAction = null)
        {
            return await TurninQuest(
                        ObjectManager.ObjectList.Where(o => o.Entry == wowObjectId).OrderBy(o => o.DistanceSqr).FirstOrDefault(),
                        searchLocation,
                        questId,
                        movementBy,
                        navigationFailedAction,
                        notFoundAction);
        }

        /// <summary>Turns in a quest at object </summary>
        /// <param name="wowObject"> The turnin object. </param>
        /// <param name="questId"> The quest Id. If 0 (default) then first completed quest is turned in. </param>
        /// <param name="searchLocation">The search location of <paramref name="wowObject" />.</param>
        /// <param name="movementBy">The movement type to use.</param>
        /// <param name="navigationFailedAction">
        ///     The action to take if <paramref name="wowObject" /> or <paramref name="searchLocation"/> cant be navigated to
        /// </param>
        /// <param name="notFoundAction">
        ///     The action to take if <paramref name="wowObject" /> is not found at
        ///     <paramref name="searchLocation" />.
        /// </param>
        /// <returns><c>true</c> if an action was taken; <c>false</c> otherwise</returns>
        /// <exception cref="Exception">A delegate callback throws an exception.</exception>
        public static async Task<bool> TurninQuest(
            WoWObject wowObject,
            WoWPoint searchLocation,
            uint questId = 0,
            MovementByType movementBy = MovementByType.FlightorPreferred,
            Action navigationFailedAction = null,
            Action notFoundAction = null)
        {
            if (wowObject == null)
            {
                if (!Navigator.AtLocation(searchLocation))
                {
                    if (await MoveTo(searchLocation, "Quest turnin search area", movementBy))
                        return true;

                    if (navigationFailedAction != null)
                        navigationFailedAction();
                    return false;
                }

                if (notFoundAction != null)
                    notFoundAction();
                else
                    TreeRoot.StatusText = "Waiting for the WoW object selected for quest turnin to spawn";
                return true;
            }

            if (!wowObject.WithinInteractRange)
            {
                if (await MoveTo(wowObject.Location, wowObject.SafeName, movementBy))
                    return true;

                if (navigationFailedAction != null)
                    navigationFailedAction();
                return false;
            }

            return await ScriptHelpers.TurninQuest(wowObject, questId);
        }
    }
}