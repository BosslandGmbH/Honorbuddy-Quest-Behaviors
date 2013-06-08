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
using System.Globalization;
using System.Threading;

using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public class WeaponArticulation
    {
        public WeaponArticulation(double? azimuthMin = null, double? azimuthMax = null)
        {
            AzimuthMin = azimuthMin ?? double.NaN;
            AzimuthMax = azimuthMax ?? double.NaN;

            AzimuthReference = double.NaN;
            ReferenceHeading = double.NaN;
        }


        public double AzimuthMin { get; private set; }
        public double AzimuthMax { get; private set; }


        #region Private and Convenience variables
        private LocalPlayer Me { get { return QuestBehaviorBase.Me; } }

        private double AzimuthReference { get; set; }
        private double ReferenceHeading { get; set; }
        #endregion


        // 11Mar2013-04:41UTC chinajade
        // NB: method instead of a property, because significant time may be involved in execution
        public double AzimuthGet()
        {
            return
                Me.InVehicle
                ? UtilAzimuthCurrentAbsolute()
                : double.NaN;
        }


        // 11Mar2013-04:41UTC chinajade
        // NB: method instead of a property, because significant time may be involved in execution
        public bool AzimuthSet(double azimuth)
        {
            if (!IsWithinAzimuthLimits(azimuth))
                { return false; }

            UtilAzimuthRequestAbsolute(azimuth);
            return true;
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
                Me.SetFacing((float)heading); 
                return true;
            }

            return false;
        }


        // 11Mar2013-04:41UTC chinajade
        public bool IsWithinAzimuthLimits(double azimuth)
        {
            if (!UtilAzimuthArticulationAcquire())
                { return false; }

            return (AzimuthMin < azimuth) && (azimuth < AzimuthMax);
        }


        private bool UtilAzimuthArticulationAcquire()
        {
            if (!Me.InVehicle)
                { return false; }

            if (double.IsNaN(AzimuthReference))
            {
                AzimuthReference = AzimuthGet();
                QBCLog.DeveloperInfo("Weapon AzimuthReference: {0:F2}", AzimuthReference);
            }

            if (double.IsNaN(AzimuthMin))
            {
                UtilAzimuthRequestAbsolute(-QuestBehaviorBase.TAU / 4);
                AzimuthMin = UtilAzimuthCurrentAbsolute();
                QBCLog.DeveloperInfo("Weapon AzimuthMin: {0:F2}", AzimuthMin);
            }

            if (double.IsNaN(AzimuthMax))
            {
                UtilAzimuthRequestAbsolute(QuestBehaviorBase.TAU / 4);
                AzimuthMax = UtilAzimuthCurrentAbsolute();
                QBCLog.DeveloperInfo("Weapon AzimuthMax: {0:F2}", AzimuthMax);
            }

            return true;
        }


        private double UtilAzimuthCurrentAbsolute()
        {
            return QuestBehaviorBase.NormalizeAngleToPi(Lua.GetReturnVal<double>("return VehicleAimGetAngle();", 0));
        }


        private void UtilAzimuthRequestAbsolute(double azimuth)
        {
            // NB: VehicleAimRequestAngle() doesn't appear to work on all vehicles, but
            // VehicleAimIncrement() does.  VehicleAimIncrement handles both postive and
            // negative increments correctly.
            Lua.DoString(string.Format(
                        "local azimuthDesired = {0}; local delta = azimuthDesired - VehicleAimGetAngle(); VehicleAimIncrement(delta);",
                        azimuth.ToString(CultureInfo.InvariantCulture)));
        }
    }
}
