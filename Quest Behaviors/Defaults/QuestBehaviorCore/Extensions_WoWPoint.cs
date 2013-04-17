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
using System.Linq;

using Styx;
using Styx.Pathing;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public static class Extensions_WoWPoint
    {
        public static Random _random = new Random((int)DateTime.Now.Ticks);

        private static LocalPlayer Me { get { return (StyxWoW.Me); } }
        public const double TAU = (2 * Math.PI);    // See http://tauday.com/


        /// <summary>
        /// <para>Adds the provided X, Y, and Z offsets to WOWPOINT yielding a new WoWPoint.</para>
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
        /// <param name="wowPoint"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <returns>new WoWPoint with adjusted coordinates</returns>
        /// <remarks>17Apr2011-12:16UTC chinajade</remarks> 
        public static WoWPoint Add(this WoWPoint wowPoint,
                                    double x,
                                    double y,
                                    double z)
        {
            return (new WoWPoint((wowPoint.X + x), (wowPoint.Y + y), (wowPoint.Z + z)));
        }


        public static WoWPoint AddPolarXY(this WoWPoint wowPoint,
                                           double xyHeadingInRadians,
                                           double distance,
                                           double zModifier)
        {
            return (wowPoint.Add((Math.Cos(xyHeadingInRadians) * distance),
                                 (Math.Sin(xyHeadingInRadians) * distance),
                                 zModifier));
        }



        /// <summary>
        /// <para>Finds another point near the destination.  Useful when toon is 'waiting' for something
        /// (e.g., boat, mob repops, etc). This allows multiple people running
        /// the same profile to not stand on top of each other while waiting for
        /// something.</para>
        /// <para>Notes:<list type="bullet">
        /// <item><description><para> * The returned WoWPoint is carefully chosen.  The returned WoWPoint
        /// will not cause you to fall off a boat dock or Zeppelin landing.</para></description></item>
        /// </list></para>
        /// </summary>
        /// <param name="location"></param>
        /// <param name="maxRadius"></param>
        /// <returns></returns>
        /// <remarks>17Apr2011-12:16UTC chinajade</remarks> 
        public static WoWPoint FanOutRandom(this WoWPoint location, double maxRadius)
        {
            QuestBehaviorBase.ContractRequires(maxRadius >= 0.0, context => "maxRadius >= 0.0");

            const int CYLINDER_LINE_COUNT = 12;
            const int MAX_TRIES = 50;
            const double SAFE_DISTANCE_BUFFER = 1.75;

            WoWPoint candidateDestination = location;
            int tryCount;

            // Most of the time we'll find a viable spot in less than 2 tries...
            // However, if you're standing on a pier, or small platform a
            // viable alternative may take 10-15 tries--its all up to the
            // random number generator.
            for (tryCount = MAX_TRIES; tryCount > 0; --tryCount)
            {
                WoWPoint circlePoint;
                bool[] hitResults;
                WoWPoint[] hitPoints;
                int index;
                WorldLine[] traceLines = new WorldLine[CYLINDER_LINE_COUNT + 1];

                candidateDestination = location.AddPolarXY((TAU * _random.NextDouble()), (maxRadius * _random.NextDouble()), 0.0);

                // Build set of tracelines that can evaluate the candidate destination --
                // We build a cone of lines with the cone's base at the destination's 'feet',
                // and the cone's point at maxRadius over the destination's 'head'.  We also
                // include the cone 'normal' as the first entry.

                // 'Normal' vector
                index = 0;
                traceLines[index].Start = candidateDestination.Add(0.0, 0.0, maxRadius);
                traceLines[index].End = candidateDestination.Add(0.0, 0.0, -maxRadius);

                // Cylinder vectors
                for (double turnFraction = 0.0; turnFraction < TAU; turnFraction += (TAU / CYLINDER_LINE_COUNT))
                {
                    ++index;
                    circlePoint = candidateDestination.AddPolarXY(turnFraction, SAFE_DISTANCE_BUFFER, 0.0);
                    traceLines[index].Start = circlePoint.Add(0.0, 0.0, maxRadius);
                    traceLines[index].End = circlePoint.Add(0.0, 0.0, -maxRadius);
                }


                // Evaluate the cylinder...
                // The result for the 'normal' vector (first one) will be the location where the
                // destination meets the ground.  Before this MassTrace, only the candidateDestination's
                // X/Y values were valid.
                GameWorld.MassTraceLine(traceLines.ToArray(),
                                        GameWorld.CGWorldFrameHitFlags.HitTestGroundAndStructures,
                                        out hitResults,
                                        out hitPoints);

                candidateDestination = hitPoints[0];    // From 'normal', Destination with valid Z coordinate


                // Sanity check...
                // We don't want to be standing right on the edge of a drop-off (say we'e on
                // a plaform or pier).  If there is not solid ground all around us, we reject
                // the candidate.  Our test for validity is that the walking distance must
                // not be more than 20% greater than the straight-line distance to the point.
                int viableVectorCount = hitPoints.Sum(point => ((Me.Location.SurfacePathDistance(point) < (Me.Location.Distance(point) * 1.20))
                                                                      ? 1
                                                                      : 0));

                if (viableVectorCount < (CYLINDER_LINE_COUNT + 1))
                    { continue; }

                // If new destination is 'too close' to our current position, try again...
                if (Me.Location.Distance(candidateDestination) <= SAFE_DISTANCE_BUFFER)
                    { continue; }

                break;
            }

            // If we exhausted our tries, just go with simple destination --
            if (tryCount <= 0)
                { candidateDestination = location; }

            return (candidateDestination);
        }


        /// <summary>
        /// Returns true, if ground is within DISTANCE _below_ you.
        /// </summary>
        /// <param name="location"></param>
        /// <param name="distance"></param>
        /// <returns>true, if ground is within DISTANCE _below_ you.</returns>
        /// <remarks>17Apr2011-12:16UTC chinajade</remarks> 
        public static bool IsOverGround(this WoWPoint location, double distance)
        {
            QuestBehaviorBase.ContractRequires(distance >= 0.0, context => "distance >= 0.0");

            return (GameWorld.TraceLine(location.Add(0.0, 0.0, 1.0),
                                        location.Add(0.0, 0.0, -distance),
                                        GameWorld.CGWorldFrameHitFlags.HitTestGroundAndStructures));
        }


        /// <summary>
        /// Returns true, if ground or water is withing DISTANCE _below_ you.
        /// </summary>
        /// <param name="location">TracelinePos should be passed as location</param>
        /// <param name="distance"></param>
        /// <returns>true, if ground or water is withing DISTANCE _below_ you.</returns>
        /// <remarks>raphus 18/12/2012</remarks>
        public static bool IsOverGroundOrWater(this WoWPoint location, double distance)
        {
            QuestBehaviorBase.ContractRequires(distance >= 0.0, context => "distance >= 0.0");

            return GameWorld.TraceLine(location.Add(0.0, 0.0, 1.0),
                                       location.Add(0.0, 0.0, -distance),
                                       GameWorld.CGWorldFrameHitFlags.HitTestGroundAndStructures
                                       | GameWorld.CGWorldFrameHitFlags.HitTestLiquid
                                       | GameWorld.CGWorldFrameHitFlags.HitTestLiquid2);
        }
        
        
        /// <summary>
        /// Returns true, if water is within DISTANCE _below_ you.
        /// </summary>
        /// <param name="location"></param>
        /// <param name="distance"></param>
        /// <returns>true, if water is within DISTANCE _below_ you.</returns>
        /// <remarks>17Apr2011-12:16UTC chinajade</remarks> 
        public static bool IsOverWater(this WoWPoint location, double distance)
        {
            QuestBehaviorBase.ContractRequires(distance >= 0.0, context => "distance >= 0.0");

            return GameWorld.TraceLine(location.Add(0.0, 0.0, 1.0),
                                       location.Add(0.0, 0.0, -distance),
                                       GameWorld.CGWorldFrameHitFlags.HitTestLiquid
                                       | GameWorld.CGWorldFrameHitFlags.HitTestLiquid2);
        }


        /// <summary>
        /// Calculates the distance between START and DESTINATION if the travel must be conducted
        /// over a surface (i.e., instead of flying).  This is most helpful in tunnels where a mob
        /// can be within X feet of you, but above or below you.  For such mobs, the direct distance
        /// is X feet, but the path you must take through the tunnels may be much much longer.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="destination"></param>
        /// <returns></returns>
        /// <remarks>17Apr2011-12:16UTC chinajade</remarks> 
        public static float SurfacePathDistance(this WoWPoint start, WoWPoint destination)
        {
            WoWPoint[] groundPath = Navigator.GeneratePath(start, destination) ?? new WoWPoint[0];

            // We define an invalid path to be of 'infinite' length
            if (groundPath.Length <= 0)
            {
                // For targets in the air, we will be unable to calculate the
                // surface path to them.  If we're flying, we still want
                // a gauging of the distance, so we use half the max float range,
                // and tack on the line-of-site distance to the unit.
                // This allows sane ordering evaluations in LINQ queries, yet
                // still returns something close enough to 'infinite' to make
                // using the path highly undesirable.
                return (float.MaxValue / 2) + start.Distance(destination);
            }


            float pathDistance = start.Distance(groundPath[0]);

            for (int i = 0; i < (groundPath.Length - 1); ++i)
                { pathDistance += groundPath[i].Distance(groundPath[i + 1]); }

            return (pathDistance);
        }


        // Returns WoWPoint.Empty if unable to locate water's surface
        public static WoWPoint WaterSurface(this WoWPoint location)
        {
            WoWPoint hitLocation;
            bool hitResult;
            WoWPoint locationUpper = location.Add(0.0, 0.0, 2000.0);
            WoWPoint locationLower = location.Add(0.0, 0.0, -2000.0);

            hitResult = (GameWorld.TraceLine(locationUpper,
                                             locationLower,
                                             (GameWorld.CGWorldFrameHitFlags.HitTestLiquid
                                              | GameWorld.CGWorldFrameHitFlags.HitTestLiquid2),
                                             out hitLocation));

            return (hitResult ? hitLocation : WoWPoint.Empty);
        }
    }
}
