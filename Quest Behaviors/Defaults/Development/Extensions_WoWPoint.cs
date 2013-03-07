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


namespace Honorbuddy.QuestBehaviors.TEMPLATE_QB
{
    public static class Extensions_WoWPoint
    {
        public static Random _random = new Random((int)DateTime.Now.Ticks);

        private static LocalPlayer Me { get { return (StyxWoW.Me); } }
        public const double TAU = (2 * Math.PI);    // See http://tauday.com/


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


        // Finds another point near the destination.  Useful when toon is 'waiting' for something
        // (e.g., boat, mob repops, etc). This allows multiple people running
        // the same profile to not stand on top of each other while waiting for
        // something.
        public static WoWPoint FanOutRandom(this WoWPoint location,
                                                double maxRadius)
        {
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


        public static double SurfacePathDistance(this WoWPoint start,
                                                    WoWPoint destination)
        {
            WoWPoint[] groundPath = Navigator.GeneratePath(start, destination) ?? new WoWPoint[0];

            // We define an invalid path to be of 'infinite' length
            if (groundPath.Length <= 0)
                { return (double.MaxValue); }


            double pathDistance = start.Distance(groundPath[0]);

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
                                             out hitLocation);

            return (hitResult ? hitLocation : WoWPoint.Empty);
        }
    }
}
