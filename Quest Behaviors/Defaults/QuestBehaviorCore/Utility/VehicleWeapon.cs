// Behavior originally contributed by Nesox / completely reworked by Chinajade
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using Styx;
using Styx.Common.Helpers;
using Styx.Helpers;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    // 11Mar2013-04:41UTC chinajade
    public class VehicleWeapon : VehicleAbility
    {
        // 11Mar2013-04:41UTC chinajade
        public VehicleWeapon(int abilityIndex /*[1..12]*/, WeaponArticulation weaponArticulation = null)
            : base(abilityIndex)
        {
            MuzzleVelocityInFps = double.NaN;   // "we don't know, yet"
            WeaponArticulation = weaponArticulation;
        }

        public double MuzzleVelocityInFps { get; private set; }


        #region Private and Convenience variables
        private LocalPlayer Me { get { return QuestBehaviorBase.Me; } }
        private readonly WaitTimer MissileWatchingTimer = new WaitTimer(TimeSpan.FromMilliseconds(1000));
        private WoWUnit MovementObserver { get { return QuestBehaviorBase.MovementObserver; } }
        private bool NeedsTestFire { get { return double.IsNaN(MuzzleVelocityInFps); } }
        private WeaponArticulation WeaponArticulation { get; set; }
        #endregion


        // 11Mar2013-04:41UTC chinajade
        private double? CalculateBallisticLaunchAngle(WoWPoint targetLocation)
        {
            if (targetLocation == WoWPoint.Empty)
                { return null; }

            const double g = 32.174; // in feet per second^2
            double v0Sqr = MuzzleVelocityInFps * MuzzleVelocityInFps;
            double horizontalDistance = MovementObserver.Location.Distance2D(targetLocation);
            double heightDiff = targetLocation.Z - MovementObserver.Location.Z;

            double tmp1 = g * (horizontalDistance * horizontalDistance);
            double tmp2 = 2 * heightDiff * v0Sqr;
            double radicalTerm = (v0Sqr * v0Sqr) - (g * (tmp1 + tmp2));

            QuestBehaviorBase.LogInfo("Muzz: {0:F2}, Dist2D: {1:F2}, Height: {2:F2}",
                v0Sqr,
                horizontalDistance,
                heightDiff);

            // If radicalTerm is negative, then both roots are imaginary...
            // This means that the muzzleVelocity is insufficient to hit the target
            // at the target's current distance.
            if (radicalTerm < 0)
                { return null; }

            radicalTerm = Math.Sqrt(radicalTerm);

            QuestBehaviorBase.LogInfo("tmp1: {0:F2}, tmp2: {1:F2}, rad: {2:F2}",
                tmp1,
                tmp2,
                radicalTerm);
            QuestBehaviorBase.LogInfo("rootTan = {0:F2} / {1:F2} = {2:F2}",
                (v0Sqr - radicalTerm),
                (g * horizontalDistance),
                (v0Sqr - radicalTerm) / (g * horizontalDistance));

            // Prefer the 'lower' angle, if its within the articulation range...
            double root = Math.Atan((v0Sqr - radicalTerm) / (g * horizontalDistance));
            if (WeaponArticulation.IsWithinAzimuthLimits(root))
                { return root; }

            // First root provides no solution, try second root...
            root = Math.Atan((v0Sqr + radicalTerm) / (g * horizontalDistance));
            if (WeaponArticulation.IsWithinAzimuthLimits(root))
                { return root; }

            // Both solutions are out of the vehicle's articulation capabilities, return "no solution"...
            return null;
        }


        // 11Mar2013-04:41UTC chinajade
        private double CalculateMuzzleVelocity()
        {
            double launchAngle = WeaponArticulation.AzimuthGet();
            double muzzleVelocity = 0.0;
            var spell = FindVehicleAbilitySpell();

            if ((spell != null) && (spell.SpellMissileId > 0))
            {
                IEnumerable<WoWMissile> firedMissileQuery =
                    from missile in WoWMissile.InFlightMissiles
                    where
                        (missile.CasterGuid == Me.TransportGuid)
                        && (missile.SpellId == spell.Id)
                    select missile;


                // Launch missile, and wait until launch is observed;
                MissileWatchingTimer.Reset();

                WoWMissile launchedMissile = null;
                while ((launchedMissile == null) && !MissileWatchingTimer.IsFinished)
                {
                    ObjectManager.Update();
                    launchedMissile = firedMissileQuery.FirstOrDefault();
                }

                // If we failed to see the missile, report error and move on...
                if (launchedMissile == null)
                {
                    QuestBehaviorBase.LogWarning(
                        "Muzzle Velocity not calculated--"
                        + "Unable to locate projectile launched by Vehicle Ability button #{0}",
                        AbilityIndex);
                    return double.NaN;  // "we don't know"
                }

                // Initial velocity calculation...
                // * Accounts for uneven terrain
                //
                // v0 = sqrt((R^2 * g) / (R * sin(2*theta)  +  2 * h * cos^2(theta)))
                // where, R = range, g = grav const, h = drop height, theta = launch angle
                double g = 32.1740; // feet/sec^2
                double R = launchedMissile.FirePosition.Distance2D(launchedMissile.ImpactPosition);
                double h = launchedMissile.FirePosition.Z - launchedMissile.ImpactPosition.Z;
                double sinTwoTheta = Math.Sin(2 * launchAngle);
                double cosTheta = Math.Cos(launchAngle);

                muzzleVelocity = Math.Sqrt(((R * R) * g) / (R * sinTwoTheta + 2 * h * (cosTheta * cosTheta)));
            }

            return muzzleVelocity;
        }


        // 11Mar2013-04:41UTC chinajade
        public bool IsWeaponReady()
        {
            return IsAbilityReady();
        }


        // 11Mar2013-04:41UTC chinajade
        public bool IsWeaponUsable()
        {
            return IsAbilityUsable();
        }


        // 11Mar2013-04:41UTC chinajade
        public bool WeaponAim(WoWObject selectedTarget)
        {
            if (selectedTarget == null)
                { return false; }

            // Show user what we're targeting...
            var wowUnit = selectedTarget as WoWUnit;
            if ((wowUnit != null) && (Me.CurrentTarget != wowUnit))
                { wowUnit.Target(); }

            return WeaponAim(selectedTarget.Location);
        }


        // 11Mar2013-04:41UTC chinajade
        public bool WeaponAim(WoWPoint selectedLocation)
        {
            if (!Me.InVehicle)
            {
                QuestBehaviorBase.LogWarning("Attempted to aim weapon while not in Vehicle!");
                return false;
            }

            if (selectedLocation == WoWPoint.Empty)
            {
                QuestBehaviorBase.LogWarning("No target for WeaponAim!");
                return false;
            }

            // Test fire weapon, if needed.  This gets us the muzzle velocity...
            if (NeedsTestFire && IsWeaponReady())
            {
                WeaponFire();
                return false;
            }

            // For heading, we just face the location...
            var neededFacing = WoWMathHelper.CalculateNeededFacing(MovementObserver.Location, selectedLocation);
            Me.SetFacing(neededFacing);

            // Calculate the azimuth...
            var azimuth = CalculateBallisticLaunchAngle(selectedLocation);
            if (!azimuth.HasValue)
            { return false; }

            QuestBehaviorBase.LogInfo("Requested Azimuth: {0:F2}", azimuth.Value);
            Lua.DoString("VehicleAimRequestAngle({0})", azimuth.Value);
            return true;
        }


        // 11Mar2013-04:41UTC chinajade
        public bool WeaponFire()
        {
            var isWeaponUsed = UseAbility();

            if (isWeaponUsed)
            {
                // Maintain muzzle velocity using low-pass filter on the accumulated value...
                MuzzleVelocityInFps =
                    NeedsTestFire
                    ? CalculateMuzzleVelocity()
                    : (0.7 * MuzzleVelocityInFps) + (0.3 * CalculateMuzzleVelocity());

QuestBehaviorBase.LogWarning("CastingPetSpell({0})-{1}: {2:F2}fps", Name, AbilityIndex, MuzzleVelocityInFps);
            }

            return isWeaponUsed;
        }
    }
}
