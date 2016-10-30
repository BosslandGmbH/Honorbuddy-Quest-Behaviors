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

using System.Linq;

using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public static class Extensions_WoWUnit
    {
        // 11Apr2013-07:48UTC chinajade
        public static bool IsUntagged(this WoWUnit wowUnit)
        {
            return (wowUnit != null)
                && (wowUnit.TaggedByMe || !wowUnit.TaggedByOther);
        }
    }
}
