using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Styx.WoWInternals.WoWObjects;

namespace PrinceOfDarkness
{
    //that one handles mount/dismount events from HB API
    public static class MountHandler
    {
        private static LocalPlayer Me { get { return PrinceOfDarkness.Me; } }
        private static bool IsMounted { get { return Me.Mounted; } }

        private static bool wasMounted;

        public delegate void OnMountDelegate();
        public delegate void OnDismountDelegate();

        public static OnDismountDelegate OnDismount;
        public static OnMountDelegate OnMount;

        static MountHandler()
        {
            OnMount += delegate { PrinceOfDarkness.Debug("Player mounted fired"); };
            OnDismount += delegate { PrinceOfDarkness.Debug("Player dismounted fired"); };
        }

        public static void Pulse()
        {
            if (IsMounted && !wasMounted)
                OnMount();
            else if (!IsMounted && wasMounted)
                OnDismount();
            wasMounted = IsMounted;
        }
    }
}
