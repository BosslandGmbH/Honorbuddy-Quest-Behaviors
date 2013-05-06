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
using System.Windows.Media;

using Styx;
using Styx.Common;
using Styx.Pathing;
using Styx.WoWInternals;
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


        // 29Apr2013-09:51UTC chinajade
        public static double HeightOverGroundOrWater(this WoWPoint location, double probeDistance = 300.0)
        {
            WoWPoint hitLocation;
            WoWPoint destination = location.Add(0.0, 0.0, -probeDistance);

            var isObstructed = GameWorld.TraceLine(location, destination,
                                                   GameWorld.CGWorldFrameHitFlags.HitTestGroundAndStructures
                                                   | GameWorld.CGWorldFrameHitFlags.HitTestLiquid
                                                   | GameWorld.CGWorldFrameHitFlags.HitTestLiquid2,
                                                   out hitLocation);

            return isObstructed
                ? location.Distance(hitLocation)
                : double.MaxValue;          
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


            // Include distance it takes us to get from start point to first point in path...
            float pathDistance = start.Distance(groundPath[0]);

            // Include distance it takes us to get from last point in path to destination...
            pathDistance += groundPath[groundPath.Length - 1].Distance(destination);

            // Include distance for each point in path...
            for (int i = 0; i < (groundPath.Length - 1); ++i)
                { pathDistance += groundPath[i].Distance(groundPath[i + 1]); }

            // Sanity check...
            QuestBehaviorBase.ContractProvides(
                pathDistance >= start.Distance(destination),
                context => "Surface path distance must be equal to or greater than straight-line distance.");

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


        // Returns default(WoWPoint) if not usable Flightor location can be found...
        //  2May2013-12:25UTC chinajade
        public static WoWPoint FindFlightorUsableLocation(this WoWPoint destination)
        {
            // If we're not using flight, or not close enough to destination, then no massaging needed...
            if (!Me.IsFlying || (Me.Location.Distance(destination) > 100) || destination.IsFlightorUsable() )
                { return destination; }

            // If the last value we returned is still good, use it...
            if (_lastFlightorDestination == destination)
                { return _lastFlightorUsableLocation; }

            // Find a new outdoor solution...
            using (StyxWoW.Memory.AcquireFrame())
            {
                // Try to find landing spot looking at terrain...
                WoWPoint landingSpot =
                   (from viableSpot in destination.ViableLandingSpotGenerator()
                    orderby viableSpot.SurfacePathDistance(destination)
                    select viableSpot)
                    .FirstOrDefault();

                if (landingSpot != default(WoWPoint))
                {
                    _lastFlightorDestination = destination;
                    _lastFlightorUsableLocation = landingSpot;
                    return _lastFlightorUsableLocation;
                }

                // Try to find landing spot looking for a nearby outdoor mob...
                var outdoorObject =
                    (from wowObject in ObjectManager.GetObjectsOfType<WoWObject>(true, false)
                    let wowUnit = wowObject.ToUnit()
                    orderby
                        wowObject.Location.SurfacePathDistance(destination)
                        + (((wowUnit != null) && wowUnit.IsHostile) ? 50 : 0)
                    where
                        wowObject.IsValid
                        && wowObject.IsOutdoors
                        && ((wowUnit == null)
                            || ((wowUnit != null) && !wowUnit.PlayerControlled))
                        && wowObject.Location.IsFlightorUsable()
                        && Navigator.CanNavigateFully(destination, wowObject.Location)
                    select wowObject)
                    .FirstOrDefault();

                if (outdoorObject != null)
                {
                    Logging.Write(Colors.HotPink, "OUTDOOR MOB: {0} ({1}, dist:{2});  Dest: {3} (dist: {4})",
                        outdoorObject.Name,
                        outdoorObject.Location,
                        outdoorObject.Location.Distance(destination),
                        destination,
                        Me.Location.Distance(destination));

                    _lastFlightorDestination = destination;
                    _lastFlightorUsableLocation = outdoorObject.Location;
                    return _lastFlightorUsableLocation;
                }
            }

            // Unable to find suitable landing spot...
            return default(WoWPoint);
        }
        private static WoWPoint _lastFlightorDestination;
        private static WoWPoint _lastFlightorUsableLocation;


        //  2May2013-12:25UTC chinajade
        public static bool IsFlightorUsable(this WoWPoint location, double? height = null)
        {
            height = height ?? 35.0;
            QuestBehaviorBase.ContractRequires(height > 0.0, context => "height > 0.0");

            WoWPoint[]      hitLocations;
            bool[]          hitResults;
            WorldLine[]     invertedCone = location.CreateCone_Vertical(height.Value, height.Value, 8, 0.0, true, true, true);

            GameWorld.MassTraceLine(invertedCone,
                                    GameWorld.CGWorldFrameHitFlags.HitTestGroundAndStructures,
                                    out hitResults,
                                    out hitLocations);

            return (hitResults.Sum(hitResult => (hitResult ? 1 : 0))  <  4);
        }


        /// <summary>
        /// <para>Creates a vertically-oriented cone of LINECOUNT rays based on WOWPOINT.
        /// The cone has a base of CONEBASERADIUS and a height of CONEHEIGHT.
        /// The caller may offset the created cone using ZOFFSET.</para>
        /// <para>The cone is normally created upright with the base in the same X-Y plane
        /// as the WOWPOINT, and the apex at CONEHEIGHT above the WOWPOINT.  If INVERTCONE is true, the
        /// geometry is rearranged such that the cone's apex rests on the WOWPOINT,
        /// and the base is CONEHEIGHT above the WOWPOINT.</para>
        /// <para>If INCLUDENORMALVECTOR is true, the number of lines returned is incremented
        /// by one, and the cone's 'normal' vector will be included as the first item
        /// in the returned set of lines.</para>
        /// <para>Normally, the rays of the returned cone will be built from the base to
        /// the apex--regardless of whether the cone is upright or inverted.
        /// If RAYSFROMAPEXTOBASE is true, the direction of the rays will be
        /// reversed, and are built from the apex to the base.</para>
        /// </summary>
        /// <param name="wowPoint"></param>
        /// <param name="coneBaseRadius">the radius to use for the base of the cone.
        /// Must be on the partially-closed interval (0..double.MaxValue]</param>
        /// <param name="coneHeight">the height to use for the cone.
        /// Must be on the partially-closed interval (0..double.MaxValue]</param>
        /// <param name="lineCount">the number of lines used to construct the cone.</param>
        /// <param name="zOffset">vertical offset which is applied to the constructed cone.</param>
        /// <param name="invertCone">if true, the cone is built with its apex resting on
        /// the provided WoWPoint and the base is at <paramref name="coneHeight"/>.  If false,
        /// the cone is built with its apex at <paramref name="coneHeight"/>, and the base
        /// resides in the same X-Y plane as the provided WoWPoint.</param>
        /// <param name="includeNormalVector">if true, the 'normal' vector of the cone is
        /// added to the returned set of lines.  The normal vector will be the first element
        /// in the returned set.</param>
        /// <param name="raysFromApexToBase">if true, the rays comprising the cone start at the
        /// apex, and end at the cone's base.  If false, the rays start at the base, and terminate
        /// at the apex.</param>
        /// <returns>a set of lines representing the sides of a vertically-oriented cone.
        /// The normal vector may
        /// also be included as the first element in the returned set of lines if the
        /// <paramref name="includeNormalVector"/> value requires such.</returns>
        /// <remarks>
        /// <para>* Shapes are useful for collision and 'drop-off' detection when coupled
        /// with MassTraceLine().</para>
        /// <para>* About Rays (or Vectors)</para>
        /// <para>Rays are more than a simple set of lines--they also specify a
        /// direction.  For instance, knowing the direction in which the rays are built is important if
        /// the caller intends to use the returned set of lines as an argument to MassTraceLine().
        /// The answer MassTraceLine() provides can change based on the direction in which
        /// the rays were built.</para>
        /// <para>Consider the example of a toon standing in a tower.  Above the toon lies a
        /// platform, and at a further distance above the platform lies the tower's roof.
        /// If we built a ray from the toon's location out through the roof, then used
        /// it in a MassTraceLine(), the MassTraceLine() would return the 'hit point' as the
        /// the platform's location.  If we built the same ray in the opposite direction--
        /// from above the roof down to the toon, the MassTraceLine() would return the
        /// 'hit point' as the roof's location.</para>
        /// </remarks>
        //  2May2013-12:25UTC chinajade
        public static WorldLine[]       CreateCone_Vertical(this WoWPoint       wowPoint,
                                                            double              coneBaseRadius,
                                                            double              coneHeight,
                                                            int                 lineCount,
                                                            double              zOffset,
                                                            bool                invertCone,
                                                            bool                includeNormalVector,
                                                            bool                raysFromApexToBase)
        {
            QuestBehaviorBase.ContractRequires(coneBaseRadius > 0.0, context => "coneBaseRadius > 0.0");
            QuestBehaviorBase.ContractRequires(coneHeight > 0.0, context => "coneHeight > 0.0");
            QuestBehaviorBase.ContractRequires(lineCount > 0, context => "lineCount > 0");

            var     cone                = new WorldLine[lineCount + (includeNormalVector  ? 1  : 0)];
            double  deltaZOfConeApex    = coneHeight + zOffset;
            double  deltaZOfConeBase    = 0.0 + zOffset;
            int     perimeterIndex      = -1;
            double  turnIncrement       = QuestBehaviorBase.TAU / lineCount;

            // If user wants the cone inverted, swap the z contibution for the Apex and Base...
            if (invertCone)
            {
                double  tmp     = deltaZOfConeApex;

                deltaZOfConeApex = deltaZOfConeBase;
                deltaZOfConeBase = tmp;
            }

            WoWPoint        apex    = wowPoint.Add(0.0, 0.0, deltaZOfConeApex);

            // The 'normal vector' will be the first vector in the returned array, if it was requested...
            if (includeNormalVector)
            {
                WoWPoint    basePoint = wowPoint.Add(0.0, 0.0, deltaZOfConeBase);

                ++perimeterIndex;
                if (raysFromApexToBase)
                {
                    cone[perimeterIndex].Start = apex;
                    cone[perimeterIndex].End   = basePoint;
                }
                else
                {
                    cone[perimeterIndex].Start = basePoint;
                    cone[perimeterIndex].End   = apex;
                }
            }

            // Create the other vectors in the cylinder...
            for (double turnFraction = 0.0;   turnFraction < QuestBehaviorBase.TAU;  turnFraction += turnIncrement )
            {
                WoWPoint    basePoint = wowPoint.AddPolarXY(turnFraction, coneBaseRadius, deltaZOfConeBase);

                ++perimeterIndex;
                if (raysFromApexToBase)
                {
                    cone[perimeterIndex].Start = apex;
                    cone[perimeterIndex].End   = basePoint;
                }
                else
                {
                    cone[perimeterIndex].Start = basePoint;
                    cone[perimeterIndex].End   = apex;
                }
            }
         
            return (cone);
        }


        // Generates potential landing points around destination by:
        // * Creating points on a circle of N points around the destination.
        // * The circle radius keeps increasing
        // * As the circle gets larger, the number of points, N, on the circle increase.
        //
        //  2May2013-12:25UTC chinajade                
        private static IEnumerable<WoWPoint>  ViableLandingSpotGenerator(this WoWPoint destination)
        {
            var potentialLandingSpots = new List<WoWPoint>();

            potentialLandingSpots.AddRange(destination.CreateCircleXY_OnSurface(17.5, 15.0));
            potentialLandingSpots.AddRange(destination.CreateCircleXY_OnSurface(22.5, 15.0));
            potentialLandingSpots.AddRange(destination.CreateCircleXY_OnSurface(27.5, 15.0));
            potentialLandingSpots.AddRange(destination.CreateCircleXY_OnSurface(32.5, 15.0));
            potentialLandingSpots.AddRange(destination.CreateCircleXY_OnSurface(40.0, 15.0));
            potentialLandingSpots.AddRange(destination.CreateCircleXY_OnSurface(50.0, 15.0));
            potentialLandingSpots.AddRange(destination.CreateCircleXY_OnSurface(60.0, 25.0));
            potentialLandingSpots.AddRange(destination.CreateCircleXY_OnSurface(75.0, 25.0));
            potentialLandingSpots.AddRange(destination.CreateCircleXY_OnSurface(100.0, 25.0));

            var viableLandingSpots =
                from landingSpot in potentialLandingSpots
                where 
                    Navigator.CanNavigateFully(landingSpot, destination)
                    && landingSpot.IsFlightorUsable()
                select landingSpot;

            return viableLandingSpots;
        }


        //  2May2013-12:25UTC chinajade
        public static IList<WoWPoint> CreateCircleXY_OnSurface(this WoWPoint circleCenter,
                                                               double radius,
                                                               double arcLength)
        {
            QuestBehaviorBase.ContractRequires(radius > 0.0, context => "radius > 0.0");
            QuestBehaviorBase.ContractRequires(arcLength > 0.0, context => "arcLength > 0.0");
            QuestBehaviorBase.ContractRequires(arcLength <= radius, context => "arcLength <= radius");

            var circle = new List<WoWPoint>();
            double turnIncrement = arcLength / radius;

            for (double turnFactor = 0.0;    turnFactor < QuestBehaviorBase.TAU;    turnFactor += turnIncrement)
            {
                WoWPoint newPoint = circleCenter.AddPolarXY(turnFactor, radius, 0.0);

                Navigator.FindHeight(newPoint.X, newPoint.Y, out newPoint.Z);
                circle.Add(newPoint);
            }      

            return (circle);
        }
    }
}
