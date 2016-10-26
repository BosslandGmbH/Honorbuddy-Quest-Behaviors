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
using System.Numerics;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public static class Extensions_WoWObject
    {
        public static Vector3 AnticipatedLocation(this WoWObject wowObject, TimeSpan atTime)
        {
            var wowUnit = wowObject as WoWUnit;

            if (wowUnit == null)
            { return wowObject.Location; }

            var anticipatedLocation =
                wowUnit.Location.RayCast(
                    wowUnit.RenderFacing,
                    (float)(wowUnit.MovementInfo.CurrentSpeed * atTime.TotalSeconds));

            return (anticipatedLocation);
        }


        // 30May2013-03:56UTC chinajade
        public static void BlacklistForCombat(this WoWObject wowObject, TimeSpan duration)
        {
            Blacklist.Add(wowObject.Guid, BlacklistFlags.Pull | BlacklistFlags.Combat, duration);
        }


        // 11Apr2013-03:56UTC chinajade
        public static void BlacklistForInteracting(this WoWObject wowObject, TimeSpan duration)
        {
            Blacklist.Add(wowObject.Guid, BlacklistFlags.Interact, duration);
        }


        public static void BlacklistForPulling(this WoWObject wowObject, TimeSpan duration)
        {
            Blacklist.Add(wowObject.Guid, BlacklistFlags.Pull, duration);
        }


        // 11Apr2013-04:41UTC chinajade
        public static bool IsBlacklistedForCombat(this WoWObject wowObject)
        {
            return Blacklist.Contains(wowObject.Guid, BlacklistFlags.Combat);
        }


        // 11Apr2013-04:41UTC chinajade
        public static bool IsBlacklistedForInteraction(this WoWObject wowObject)
        {
            return Blacklist.Contains(wowObject.Guid, BlacklistFlags.Interact);
        }


        // 4Jun2013-04:41UTC chinajade
        public static bool IsBlacklistedForPulling(this WoWObject wowObject)
        {
            return Blacklist.Contains(wowObject.Guid, BlacklistFlags.Pull);
        }


        // 2Sep2013 chinajade
        public static int SafeGuid(this WoWObject wowObject)
        {
            return (int)wowObject.Guid.Lowest & 0x0ffffff;
        }


        public static double SurfacePathDistance(this WoWObject objectTo)
        {
            PathInformation info = Navigator.LookupPathInfo(objectTo);
            if (info.Navigability == PathNavigability.Navigable)
            {
                // Don't care about whether this is a min distance or exact one...
                return info.Distance;
            }

            return double.NaN;
        }


        public static double PathTraversalCost(this WoWObject objectTo)
        {
            double pathDist = objectTo.SurfacePathDistance();
            if (!double.IsNaN(pathDist))
                return pathDist;

            // For targets in the air, we will be unable to calculate the
            // surface path to them.  If we're flying, we still want
            // a gauging of the distance, so we use a large value
            // and tack on the line-of-site distance to the unit.
            // This allows sane ordering evaluations in LINQ queries, yet
            // still returns something large to make using the path highly
            // undesirable.
            return 50000 + objectTo.Distance;
        }

        public static double CollectionDistance(this WoWObject objectTo)
        {
            // NB: we use the 'surface path' to calculate distance to mobs.
            // This is important in tunnels/caves where mobs may be within X feet of us,
            // but they are below or above us, and we have to traverse much tunnel to get to them.
            // NB: If either the player or the mob is 'off the mesh', then a SurfacePath distance
            // calculation will be absurdly large.  In these situations, we resort to direct line-of-sight
            // distances.
            double pathDist = objectTo.SurfacePathDistance();
            return double.IsNaN(pathDist) ? objectTo.Distance : pathDist;
        }

        /// <summary>
        /// Returns the time it takes to traverse to the DESTINATION.  The caller
        /// can supply a FACTOROFSAFETY that acts as a multiplier on the calculated time.
        /// The caller can provide a LOWERLIMITOFMAXTIME to place a minimum bound on the
        /// traversal time returned.
        /// The caller can provide UPPERLIMITONMAXTIME to place an upper bound on the
        /// traversal time calculated.
        /// <para>Notes:<list type="bullet">
        /// <item><description><para> * If we are on the ground, the traversal time is calculated
        /// based on the ground path to the destination.  This may require navigating around obstacles,
        /// or via a particular path to the destination.  If we are swimming or flying, the the
        /// travesal time is calculated as straight line-of-sight to the destination.</para></description></item>
        /// <item><description><para> * The FACTOROFSAFETY defaults to 1.0.  The 1.0 value calculates
        /// the precise time needed to arrive at the destination if everything goes perfect.
        /// The factor of safety should be increased to accomodate 'stuck' situations, mounting
        /// time, and other factors.  In most situations, a good value for factor of safety
        /// is about 2.5.</para></description></item>
        /// <item><description><para> * The LOWERLIMITOFMAXTIME places a lower bound on the
        /// traversal time.  This lower limit is imposed after the factor of safety has
        /// been applied.</para></description></item>
        /// <item><description><para> * The UPPERLIMITONMAXTIME places an upper bound on the
        /// traversal time.  This upper limit is imposed after the factor of safety has
        /// been applied.  We can get times that are effectively 'infinite' in situations 
        /// where the Navigator was unable to calculate a path to the target.  This puts
        /// an upper limit on such bogus values.</para></description></item>
        /// </list></para>
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="factorOfSafety"></param>
        /// <param name="lowerLimitOnMaxTime"></param>
        /// <param name="upperLimitOnMaxTime"></param>
        /// <returns></returns>
        public static TimeSpan MaximumTraversalTime(this WoWObject destination,
                                                    double factorOfSafety = 1.0,
                                                    TimeSpan? lowerLimitOnMaxTime = null,
                                                    TimeSpan? upperLimitOnMaxTime = null)
        {
            var pathDistance = destination.SurfacePathDistance();

            double distanceToCover =
                !double.IsNaN(pathDistance)
                ? pathDistance
                : WoWMovement.ActiveMover.Location.Distance(destination.Location);

            return Utility.MaximumTraversalTime(distanceToCover, factorOfSafety, lowerLimitOnMaxTime,
                                                upperLimitOnMaxTime);
        }
    }
}
