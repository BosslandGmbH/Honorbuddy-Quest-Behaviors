// Behavior originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

//==================================================
// COPY WHAT YOU NEED FROM THIS FILE INTO YOUR QUEST BEHAVIOR:
// These are functional code snippets to be used for Quest Behavior (or other) development.
// Please copy whatever snippets you need from this file into TEMPLATE.cs.  We did not place them
// in TEMPLATE.cs to prevent unnecessarily bloating the base file.
// BE CERTAIN, that your namespace is unique, as other behaviors may be using the same snippets. (i.e.,
// Honorbuddy does not provide a location for common methods that are highly reusable in Quest Behaviors).
//==================================================


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Styx;
using Styx.Pathing;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.Quest_Behaviors.TEMPLATE_QB
{
    public static class Extensions_WoWObject
    {
        // Stolen from Singular
        public static string SafeName(this WoWObject obj)
        {
            if (obj.IsMe)
                { return "Me"; }

            string name;
            if (obj is WoWPlayer)
                { name = ((WoWPlayer)obj).Class.ToString(); }

            else if ((obj is WoWUnit) && obj.ToUnit().IsPet)
                { name =  obj.ToUnit().OwnedByRoot.SafeName()  + ":Pet"; }

            else
                { name = obj.Name + "." + AbbreviatedGuid(obj.Guid); }

            return name;
        }

        // Stolen from Singular...
        public static string AbbreviatedGuid(ulong guid)
        {
            const int AbbreviatedGuidLength = 4;
            string tmpString = string.Format("{0:X4}", guid);

            int startPos = (AbbreviatedGuidLength > tmpString.Length)
                            ? 0
                            : (tmpString.Length - AbbreviatedGuidLength);
                
            return tmpString.Substring(startPos, AbbreviatedGuidLength);
        }
    }
}
