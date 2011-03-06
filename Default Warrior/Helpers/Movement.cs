using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using Styx.Helpers;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;

namespace Hera.Helpers
{
    public static class Movement
    {
        private static LocalPlayer Me { get { return ObjectManager.Me; } }

        /// <summary>
        /// Stop moving. Like da!
        /// </summary>
        public static void StopMoving() { while (Me.IsMoving) { WoWMovement.MoveStop(); Thread.Sleep(50); } }

        /// <summary>
        /// Move to within X yards of the target
        /// </summary>
        /// <param name="distanceFromTarget">Distance to move to the target</param>
        public static void MoveTo(float distanceFromTarget)
        {
            // Let HB do the math and find a WoWPoint X yards away from the target
            WoWPoint moveToHere = WoWMathHelper.CalculatePointFrom(Me.Location, Me.CurrentTarget.Location, distanceFromTarget);

            // Use HB navigation to move to a WoWPoint. WoWPoint has been calculated in the above code
            Navigator.MoveTo(moveToHere);
        }

        public static void MoveTo(WoWPoint location)
        {
            Navigator.MoveTo(location);
        }

        /// <summary>
        /// TRUE if we need to perform a distance check
        /// </summary>
        public static bool NeedToCheck
        {
            get
            {
                // 
                float interactRange = Me.CurrentTarget.InteractRange - 2.0f;
                if (!Target.IsFleeing && Me.CurrentTarget.Distance <= interactRange ) return false;

                // If distance is less than ClassHelper.MinimumDistance and the target is NOT running away and we are moving then we should stop moving
                if (Target.IsDistanceLessThan(ClassHelper.MinimumDistance) && !Target.IsFleeing && Me.IsMoving) WoWMovement.MoveStop();

                // TRUE if we are out of range. TRUE means we need to move closer
                bool result = Target.IsDistanceMoreThan(ClassHelper.MaximumDistance);

                return result;
            }
        }

        /// <summary>
        /// Move to melee distance if we need to
        /// </summary>
        public static void DistanceCheck()
        {
            // No target, nothing to do
            if (!Me.GotTarget) return;

            // If we're too close stop moving
            if (Target.IsDistanceLessThan(ClassHelper.MinimumDistance)) StopMoving();

            // If we're more than X yards away from the current target then move to X yards from the target
            DistanceCheck(ClassHelper.MaximumDistance, ClassHelper.MinimumDistance);
        }

        /// <summary>
        /// Move to range and stop moving if we are too close
        /// </summary>
        /// <param name="maxDistance">Maximum distance away from the target you want to be. This should be max spell range or melee range</param>
        /// <param name="moveToDistance">If your distance is greater than maxDistance you will move to this distance from the target</param>
        public static void DistanceCheck(double maxDistance, double moveToDistance)
        {
            // No target, nothing to do
            if (!Me.GotTarget) return;

            // If target (NPC) is running away move as close a possible to the target
            if (Me.GotTarget && Me.CurrentTarget.Fleeing)
            {
                Utils.Log("Runner!");
                moveToDistance = -0.5;
            }

            float interactRange = Me.CurrentTarget.InteractRange - 1;
            if (!Target.IsFleeing && Target.IsDistanceLessThan(interactRange)) return;

            // We're too far from the target so move closer
            if (Me.CurrentTarget.Fleeing && Target.Distance > moveToDistance || Target.Distance > maxDistance)
            {
                Utils.Log(String.Format("Moving closer to {0}", Me.CurrentTarget.Name), Color.FromName("DarkGreen"));
                MoveTo((float) moveToDistance);
            }
          
            // We're too close to the target we need to stop moving
            if (Target.Distance <= moveToDistance && Me.IsMoving)
            {
                Utils.Log("We are too close, stop moving.", Color.FromName("DarkGreen"));
                WoWMovement.MoveStop();
                return;
            }

            // We don't need to do anything so just exit
            if (Target.Distance > moveToDistance || !Me.IsMoving || Me.CurrentTarget.Fleeing)
                return;

            

            // When all else fails just stop moving
            WoWMovement.MoveStop();

        }







        #region Find Safe Location
        //************************************************************************************
        // Snazzy code Apoc gave me (from HB corpse rez)
        // It will find the best location furtherest away from mobs
        // I use this for kiting



        private static List<WoWPoint> AllMobsAroundUs
        {
            get
            {
                List<WoWUnit> mobs = (from o in ObjectManager.ObjectList
                                      where o is WoWUnit && o.Distance < 80
                                      let u = o.ToUnit()
                                      where u.Attackable
                                            && u.IsAlive
                                            //&& u.Guid != Me.CurrentTarget.Guid
                                            && u.IsHostile
                                      select u).ToList();


                return mobs.Select(u => u.Location).ToList();
            }
        }

        private static WoWPoint NearestMobLoc(WoWPoint p, IEnumerable<WoWPoint> mobLocs)
        {
            var lst = (mobLocs.OrderBy(u => u.Distance(p)));
            return mobLocs.OrderBy(u => u.Distance(p)).Count() > 0 ? lst.First() : new WoWPoint(float.MaxValue, float.MaxValue, float.MaxValue);
        }

        public static WoWPoint FindSafeLocation(double distanceFrom)
        {
            WoWPoint myLocation = Me.Location;
            WoWPoint destinationLocation;
            List<WoWPoint> mobLocations = new List<WoWPoint>();
            mobLocations = AllMobsAroundUs;
            double bestSafetyMargin = distanceFrom;

            mobLocations.Add(Me.CurrentTarget.Location);

            // Rotate 10 degrees each itteration
            for (float degrees = 0f; degrees < 360f; degrees += 10f)
            {
                // Search 5 yards further away each itteration
                for (float distanceFromMob = 0f; distanceFromMob <= 35f; distanceFromMob += 5f)
                {
                    destinationLocation = myLocation.RayCast((float)(degrees * Math.PI / 180f), distanceFromMob);
                    double mobDistance = destinationLocation.Distance2D(NearestMobLoc(destinationLocation, mobLocations));

                    // Mob(s) too close to our current base-safe location, not a suitable location
                    if (mobDistance <= bestSafetyMargin) continue;

                    // Found a mob-free location, lets do further testing.
                    // * Check if we can generate a path
                    // * Check if we have LOS 

                    // Can we generate a path to this location?
                    if (Navigator.GeneratePath(Me.Location, destinationLocation).Length <= 0)
                    {
                        Utils.Log("Mob-free location failed path generation check");
                        continue;
                    }

                    // Is the destination in line of sight?
                    if (!GameWorld.IsInLineOfSight(Me.Location, destinationLocation))
                    {
                        Utils.Log("Mob-free location failed line of sight check");
                        continue;
                    }

                    // We pass all checks. This is a good location 
                    // Make it so 'Number 1', "Engage"
                    return destinationLocation;

                }
            }

            return null;

        }
        #endregion




    }
}
