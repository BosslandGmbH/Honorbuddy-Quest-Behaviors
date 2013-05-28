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
using System.Collections.Generic;
using System.Linq;

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
    }
}
