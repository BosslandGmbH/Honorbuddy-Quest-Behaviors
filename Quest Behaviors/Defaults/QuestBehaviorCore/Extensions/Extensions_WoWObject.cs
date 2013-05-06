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
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public static class Extensions_WoWObject
    {
        // Mostly stolen from Singular
        // 12Apr2013-06:29UTC chinajade
        public static string SafeName(this WoWObject obj)
        {
            const ulong GuidMask = 0x0ffff;

            if (obj.IsMe)
                { return "Me"; }

            string name;
            if (obj is WoWPlayer)
                { name = ((WoWPlayer)obj).Class.ToString(); }

            else if ((obj is WoWUnit) && obj.ToUnit().IsPet)
                { name =  obj.ToUnit().OwnedByRoot.SafeName()  + ":Pet"; }

            else
                { name = string.Format("{0}.{1:X4}", obj.Name, (obj.Guid & GuidMask)); }

            return name;
        }
    }
}
