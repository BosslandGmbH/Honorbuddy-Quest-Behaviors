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

using Styx.Helpers;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public class WeaponArticulation
    {
        public WeaponArticulation(double weaponAzimuthMin, double weaponAzimuthMax)
        {
            WeaponAzimuthMin = weaponAzimuthMin;
            WeaponAzimuthMax = weaponAzimuthMax;

            ReferenceAzimuth = double.NaN;
            ReferenceHeading = double.NaN;
        }


        public WeaponArticulation()
        {
            WeaponAzimuthMin = double.NaN;
            WeaponAzimuthMax = double.NaN;
        }


        public double WeaponAzimuthMin { get; private set; }
        public double WeaponAzimuthMax { get; private set; }


        #region Private and Convenience variables
        private LocalPlayer Me { get { return QuestBehaviorBase.Me; } }

        private double ReferenceAzimuth { get; set; }
        private double ReferenceHeading { get; set; }
        #endregion


        public void AcquireReferences()
        {
            if (double.IsNaN(ReferenceAzimuth))
                { ReferenceAzimuth = AzimuthGet(); }

            if (double.IsNaN(ReferenceHeading))
                { ReferenceHeading = HeadingGet(); }
        }

        // 11Mar2013-04:41UTC chinajade
        // NB: method instead of a property, because significant time may be involved in execution
        public double AzimuthGet()
        {
            return
                Me.InVehicle
                ? WoWMathHelper.NormalizeRadian(Lua.GetReturnVal<float>("return VehicleAimGetAngle()", 0))
                : double.NaN;
        }


        // 11Mar2013-04:41UTC chinajade
        // NB: method instead of a property, because significant time may be involved in execution
        public bool AzimuthSet(double azimuth)
        {
            if (Me.InVehicle)
            {
                Lua.DoString("VehicleAimRequestAngle({0})", azimuth);
                return true;
            }

            return false;
        }


        // 11Mar2013-04:41UTC chinajade
        // NB: method instead of a property, because significant time may be involved in execution
        public double HeadingGet()
        {
            return
                Me.InVehicle
                ? QuestBehaviorBase.MovementObserver.Rotation
                : double.NaN;
        }


        // 11Mar2013-04:41UTC chinajade
        // NB: method instead of a property, because significant time may be involved in execution
        public bool HeadingSet(double heading)
        {
            if (Me.InVehicle)
            {
                return true;
            }

            return false;
        }


        // 11Mar2013-04:41UTC chinajade
        public bool IsWithinAzimuthLimits(double azimuth)
        {
            QuestBehaviorBase.LogWarning("Azimuth desired: {0:F2}", azimuth);
            return (WeaponAzimuthMin < azimuth) && (azimuth < WeaponAzimuthMax);
        }
    }
}
