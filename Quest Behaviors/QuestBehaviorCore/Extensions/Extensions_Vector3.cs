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
using System.Numerics;
using Styx;
using Styx.Common;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public static class Extensions_Vector3
    {
        private static LocalPlayer Me { get { return (StyxWoW.Me); } }
        public const double TAU = (2 * Math.PI);    // See http://tauday.com/


        /// <summary>
        /// <para>Adds the provided X, Y, and Z offsets to Vector3 yielding a new Vector3.</para>
        /// <para>---</para>
        /// <para>The HBcore only provides a version of this that accepts 'float' values.
        /// This version accepts 'doubles', because it is inefficient to keep truncating
        /// data types (to float) that are provided by the Math and other libraries.</para>
        /// <para>'Double' performance is just as fast as 'Float'.  Internally, modern
        /// computer architectures calculate using maximum precision (i.e., many bits
        /// bigger than double), then truncate the result to fit.  The only benefit
        /// 'float' has over 'double' is storage space, which is negligible unless
        /// you've a database using billions of them.</para>
        /// </summary>
        /// <returns>new Vector3 with adjusted coordinates</returns>
        /// <remarks>17Apr2011-12:16UTC chinajade</remarks> 
        public static Vector3 Add(this Vector3 v,
                                    double x,
                                    double y,
                                    double z)
        {
            return v + new Vector3((float)x, (float)y, (float)z);
        }


        public static Vector3 AddPolarXY(this Vector3 v,
                                         double xyHeadingInRadians,
                                         double distance,
                                         double zModifier)
        {
            return v.Add((Math.Cos(xyHeadingInRadians) * distance),
                         (Math.Sin(xyHeadingInRadians) * distance),
                         zModifier);
        }

        /// <summary>
        /// <para>Finds another point near the destination.  Useful when toon is 'waiting' for something
        /// (e.g., boat, mob repops, etc). This allows multiple people running
        /// the same profile to not stand on top of each other while waiting for
        /// something.</para>
        /// <para>Notes:<list type="bullet">
        /// <item><description><para> * The returned Vector3 is carefully chosen.  The returned Vector3
        /// will not cause you to fall off a boat dock or Zeppelin landing.</para></description></item>
        /// </list></para>
        /// </summary>
        /// <param name="location"></param>
        /// <param name="maxRadius"></param>
        /// <returns></returns>
        /// <remarks>17Apr2011-12:16UTC chinajade</remarks> 
        public static Vector3 FanOutRandom(this Vector3 location, double maxRadius)
        {
            Contract.Requires(maxRadius >= 0.0, context => "maxRadius >= 0.0");

            // Optimize situations where we want a very close-by point...
            if (maxRadius <= 1)
                return location;

            const int CYLINDER_LINE_COUNT = 12;
            const int MAX_TRIES = 50;
            const double SAFE_DISTANCE_BUFFER = 1.75;

            Vector3 candidateDestination = location;
            int tryCount;

            // ActiveMover is null in some cases where player is not in control of movement,
            // such as when on a taxi like the one for the 
            // 'Mission: The Murketh and Shaadraz Gateways' quest (http://www.wowhead.com/quest=10146)
            var me = WoWMovement.ActiveMover ?? StyxWoW.Me;
            Contract.Requires(me != null, context => "me != null");
            var myLoc = me.Location;

            // Most of the time we'll find a viable spot in less than 2 tries...
            // However, if you're standing on a pier, or small platform a
            // viable alternative may take 10-15 tries--its all up to the
            // random number generator.
            for (tryCount = MAX_TRIES; tryCount > 0; --tryCount)
            {
                bool[] hitResults;
                Vector3[] hitPoints;
                Func<double, double> weightedRandomRadius =
                    (radiusMaximum) =>
                        {
                            return
                                (StyxWoW.Random.Next(101) < 80)
                                    // We want a large number of the candidate magnitudes to be near the max range.
                                    // This encourages toons to 'spread out'.
                                    ? ((radiusMaximum * 0.70) + (radiusMaximum * 0.30 * StyxWoW.Random.NextDouble()))
                                    : (radiusMaximum * StyxWoW.Random.NextDouble());
                        };
                var traceLines = new WorldLine[CYLINDER_LINE_COUNT + 1];

                candidateDestination = location.AddPolarXY((TAU * StyxWoW.Random.NextDouble()), weightedRandomRadius(maxRadius), 0.0);

                // If destination is in the air...
                if (!IsOverGround(candidateDestination, 3.0))
                {
                    // If we don't have clear LoS between the specified and candidate destinations, the candidate is unsuitable...
                    if (GameWorld.TraceLine(location, candidateDestination, TraceLineHitFlags.Collision))
                        continue;

                    // Otherwise, we have our candidate destination...
                    break;
                }


                // Ground-based destinations...
                // Build set of tracelines that can evaluate the candidate destination --
                // We build a cone of lines with the cone's base at the destination's 'feet',
                // and the cone's point at maxRadius over the destination's 'head'.  We also
                // include the cone 'normal' as the first entry.

                // 'Normal' vector
                var index = 0;
                traceLines[index].Start = candidateDestination.Add(0.0, 0.0, maxRadius);
                traceLines[index].End = candidateDestination.Add(0.0, 0.0, -maxRadius);

                // Cylinder vectors
                for (double turnFraction = 0.0; turnFraction < TAU; turnFraction += (TAU / CYLINDER_LINE_COUNT))
                {
                    ++index;
                    var circlePoint = candidateDestination.AddPolarXY(turnFraction, SAFE_DISTANCE_BUFFER, 0.0);
                    traceLines[index].Start = circlePoint.Add(0.0, 0.0, maxRadius);
                    traceLines[index].End = circlePoint.Add(0.0, 0.0, -maxRadius);
                }


                // Evaluate the cylinder...
                // The result for the 'normal' vector (first one) will be the location where the
                // destination meets the ground.  Before this MassTrace, only the candidateDestination's
                // X/Y values were valid.
                GameWorld.MassTraceLine(traceLines.ToArray(),
                                    TraceLineHitFlags.Collision,
                                    out hitResults,
                                    out hitPoints);

                candidateDestination = hitPoints[0];    // From 'normal', Destination with valid Z coordinate


                // Sanity check...
                // We don't want to be standing right on the edge of a drop-off (say we'e on
                // a plaform or pier).  If there is not solid ground all around us, we reject
                // the candidate.  Our test for validity is that the walking distance must
                // not be more than 20% greater than the straight-line distance to the point.
                // TODO: FanOutRandom PathTraversalCost on point (replace with HB's SampleMesh method instead)
                int viableVectorCount = hitPoints.Sum(point => ((location./*PathTraversalCost*/Distance(point) < (location.Distance(point) * 1.20))
                                                                ? 1
                                                                : 0));

                if (viableVectorCount < (CYLINDER_LINE_COUNT * 0.8))
                    continue;

                // If new destination is 'too close' to our current position, try again...
                if (myLoc.Distance(candidateDestination) <= SAFE_DISTANCE_BUFFER)
                    continue;

                break;
            }

            // If we exhausted our tries, just go with simple destination --
            if (tryCount <= 0)
                candidateDestination = location;

            return (candidateDestination);
        }


        // 29Apr2013-09:51UTC chinajade
        public static double HeightOverGroundOrWater(this Vector3 location, double probeDistance = 300.0)
        {
            Vector3 hitLocation;
            Vector3 destination = location.Add(0.0, 0.0, -probeDistance);

            var isObstructed = GameWorld.TraceLine(location, destination,
                                                   TraceLineHitFlags.Collision
                                                   | TraceLineHitFlags.LiquidAll,
                                                   out hitLocation);

            return isObstructed
                ? location.Distance(hitLocation)
                : double.MaxValue;
        }


        /// <summary>
        /// Returns true, if ground is within DISTANCE _below_ you.
        /// </summary>
        /// <param name="location"></param>
        /// <param name="probeDistance"></param>
        /// <returns>true, if ground is within DISTANCE _below_ you.</returns>
        /// <remarks>17Apr2011-12:16UTC chinajade</remarks> 
        public static bool IsOverGround(this Vector3 location, double probeDistance)
        {
            Contract.Requires(probeDistance >= 0.0, context => "distance >= 0.0");

            return (GameWorld.TraceLine(location.Add(0.0, 0.0, 1.0),
                                        location.Add(0.0, 0.0, -probeDistance),
                                        TraceLineHitFlags.Collision));
        }


        /// <summary>
        /// Returns true, if ground or water is withing DISTANCE _below_ you.
        /// </summary>
        /// <param name="location">TracelinePos should be passed as location</param>
        /// <param name="probeDistance"></param>
        /// <returns>true, if ground or water is withing DISTANCE _below_ you.</returns>
        /// <remarks>raphus 18/12/2012</remarks>
        public static bool IsOverGroundOrWater(this Vector3 location, double probeDistance)
        {
            Contract.Requires(probeDistance >= 0.0, context => "distance >= 0.0");

            return GameWorld.TraceLine(location.Add(0.0, 0.0, 1.0),
                                       location.Add(0.0, 0.0, -probeDistance),
                                       TraceLineHitFlags.Collision | TraceLineHitFlags.LiquidAll);
        }


        /// <summary>
        /// Returns true, if water is within DISTANCE _below_ you.
        /// </summary>
        /// <param name="location"></param>
        /// <param name="probeDistance"></param>
        /// <returns>true, if water is within DISTANCE _below_ you.</returns>
        /// <remarks>17Apr2011-12:16UTC chinajade</remarks> 
        public static bool IsOverWater(this Vector3 location, double probeDistance)
        {
            Contract.Requires(probeDistance >= 0.0, context => "distance >= 0.0");

            return GameWorld.TraceLine(location.Add(0.0, 0.0, 1.0),
                                       location.Add(0.0, 0.0, -probeDistance),
                                       TraceLineHitFlags.LiquidAll);
        }


        // Returns Vector3.Zero if unable to locate water's surface
        public static Vector3 WaterSurface(this Vector3 location)
        {
            Vector3 hitLocation;
            Vector3 locationUpper = location.Add(0.0, 0.0, 2000.0);
            Vector3 locationLower = location.Add(0.0, 0.0, -2000.0);

            var hitResult = (GameWorld.TraceLine(locationUpper,
                                             locationLower,
                                             TraceLineHitFlags.LiquidAll,
                                             out hitLocation));

            return (hitResult ? hitLocation : Vector3.Zero);
        }
    }
}
