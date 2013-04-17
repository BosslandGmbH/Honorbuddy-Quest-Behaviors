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
using System;
using System.Collections.Generic;
using System.Linq;

using Styx;
using Styx.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public partial class QuestBehaviorBase
    {
        // 11Apr2013-04:41UTC chinajade
        public string GetMobNameFromId(int wowUnitId)
        {
            WoWUnit wowUnit = FindUnitsFromIds(ToEnumerable<int>(wowUnitId)).FirstOrDefault();

            return (wowUnit != null)
                ? wowUnit.Name
                : string.Format("MobId({0})", wowUnitId);
        }


        public static WoWPoint GetPointToGainDistance(WoWObject target, double minDistanceNeeded)
        {
            var minDistance = (float)(minDistanceNeeded + /*epsilon*/(2 * Navigator.PathPrecision));

            Func<WoWObject, WoWPoint, bool> isPointViable = (selectedTarget, potentialDestination) =>
            {
                return
                    selectedTarget.Location.Distance(potentialDestination) > minDistance
                    && (StyxWoW.Me.Location.Distance(potentialDestination) < selectedTarget.Location.Distance(potentialDestination))
                    && GameWorld.IsInLineOfSight(potentialDestination, selectedTarget.Location);
            };

            // If the previously calculated point is still viable, use it...
            if (isPointViable(target, _gainDistancePoint))
            {
                return _gainDistancePoint;
            }

            // Otherwise, find a new point...
            WoWObject moveTowardsObject = null;

            if (!(StyxWoW.Me.IsFlying || StyxWoW.Me.IsSwimming))
            {
                using (StyxWoW.Memory.AcquireFrame())
                {
                    moveTowardsObject =
                       (from wowObject in ObjectManager.GetObjectsOfType<WoWObject>(true, false)
                        where
                            wowObject.IsValid
                            && isPointViable(target, wowObject.Location)
                        orderby
                            StyxWoW.Me.Location.SurfacePathDistance(wowObject.Location)
                        select wowObject)
                        .FirstOrDefault();
                }
            }

            _gainDistancePoint =
                (moveTowardsObject != null)
                ? moveTowardsObject.Location
                // Resort to brute force...
                : WoWMathHelper.CalculatePointFrom(StyxWoW.Me.Location, target.Location, minDistance);
  
            return _gainDistancePoint;
        }
        private static WoWPoint _gainDistancePoint;


        //  9Mar2013-12:34UTC chinajade
        public static string PrettyMoney(ulong totalCopper)
        {
            ulong moneyCopper = totalCopper % 100;
            totalCopper /= 100;

            ulong moneySilver = totalCopper % 100;
            totalCopper /= 100;

            ulong moneyGold = totalCopper;

            string formatString =
                (moneyGold > 0) ? "{0}g{1:D2}s{2:D2}c"
                : (moneySilver > 0) ? "{1}s{2:D2}c"
                : "{2}c";

            return string.Format(formatString, moneyGold, moneySilver, moneyCopper);
        }


        //  9Mar2013-12:34UTC chinajade
        public static string PrettyTime(TimeSpan duration)
        {
            double milliSeconds = duration.TotalMilliseconds;

            return
                (milliSeconds < 1000) ? string.Format("{0}ms", milliSeconds)
                : (((int)milliSeconds % 1000) == 0) ? string.Format("{0}s", milliSeconds / 1000)
                : string.Format("{0:F3}s", milliSeconds / 1000);
        }

        
        // 12Mar2013-08:27UTC chinajade
        public static IEnumerable<T> ToEnumerable<T>(T item)
        {
            yield return item;
        }
    }
}