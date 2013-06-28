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
using System;

using Styx;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public static class Extensions_WoWObject
    {
        public static WoWPoint AnticipatedLocation(this WoWObject wowObject, TimeSpan atTime)
        {
            var wowUnit = wowObject.ToUnit();

            if (wowUnit == null)
                { return wowObject.Location; }

            var anticipatedLocation =
                wowUnit.Location.RayCast(
                    wowUnit.RenderFacing, 
                    (float)(wowUnit.MovementInfo.CurrentSpeed * atTime.TotalSeconds));

            return (anticipatedLocation);
        }


        public static double CollectionDistance(this WoWObject wowObject)
        {
            var isMeSwimmingOrFlying = StyxWoW.Me.IsSwimming || StyxWoW.Me.IsFlying;
            WoWPoint myLocation = StyxWoW.Me.Location;

            // NB: we use the 'surface path' to calculate distance to mobs.
            // This is important in tunnels/caves where mobs may be within X feet of us,
            // but they are below or above us, and we have to traverse much tunnel to get to them.
            return isMeSwimmingOrFlying
                 ? myLocation.Distance(wowObject.Location)
                 : myLocation.SurfacePathDistance(wowObject.Location);
        }


        // Mostly stolen from Singular
        // 12Apr2013-06:29UTC chinajade
        public static string SafeName(this WoWObject wowObject, bool debug = false)
        {
            const ulong GuidMask = 0x0ffff;

            if (wowObject.IsMe)
                { return "Self"; }

            string name;
            if (wowObject is WoWPlayer)
                { name = ((WoWPlayer)wowObject).Class.ToString(); }

            else if ((wowObject is WoWUnit) && wowObject.ToUnit().IsPet)
                { name = wowObject.ToUnit().OwnedByRoot.SafeName()  + ":Pet"; }

            else
                { name = wowObject.Name; }

            if (debug)
                { name = string.Format("{0}.{1:X4}", name, (wowObject.Guid & GuidMask)); }

            return name;
        }


        public static double SurfacePathDistance(this WoWObject objectTo)
        {
            return StyxWoW.Me.SurfacePathDistance(objectTo);
        }


        public static double SurfacePathDistance(this WoWObject objectFrom, WoWObject objectTo)
        {
            return objectFrom.Location.SurfacePathDistance(objectTo.Location);
        }
    }
}
