﻿// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Usings
using System.Linq;

using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public static class Extensions_LocalPlayer
    {
        // 28May2013-08:11UTC chinajade
        public static int CarriedItemCount(this LocalPlayer localPlayer, int itemId)
        {
            return (int)
                localPlayer.CarriedItems
                .Where(i => i.Entry == itemId)
                .Sum(i => i.StackCount);
        }


        // 28May2013-08:11UTC chinajade
        public static bool IsQuestComplete(this LocalPlayer localPlayer, int questId)
        {
            PlayerQuest quest = localPlayer.QuestLog.GetQuestById((uint)questId);

            return (quest != null)
                ? quest.IsCompleted                                                     // Immediately complete?
                : localPlayer.QuestLog.GetCompletedQuests().Contains((uint)questId);    // Historically complete?
        }


        // 24Feb2013-08:11UTC chinajade
        public static bool IsQuestObjectiveComplete(this LocalPlayer localPlayer, int questId, int objectiveIndex)
        {
            // If quest and objective was not specified, obviously its not complete...
            if ((questId <= 0) || (objectiveIndex <= 0))
                { return false; }

            // If quest is not in our log, obviously its not complete...
            if (localPlayer.QuestLog.GetQuestById((uint)questId) == null)
                { return false; }

            var questLogIndex = Lua.GetReturnVal<int>(string.Format("return GetQuestLogIndexByID({0})", questId), 0);

            return
                Lua.GetReturnVal<bool>(string.Format("return GetQuestLogLeaderBoard({0},{1})", objectiveIndex, questLogIndex), 2);
        }
    }
}
