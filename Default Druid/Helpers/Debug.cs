using Styx.Helpers;

namespace Hera.Helpers
{
    public static class Debug
    {
        private static string _logSpam;

        public static bool Level1
        {
            get { return Config.Settings.Debug.Contains("debug level 1"); }
        }

        public static bool Level2
        {
            get { return Config.Settings.Debug.Contains("debug level 2"); }
        }

        public static bool Level3
        {
            get { return Config.Settings.Debug.Contains("debug level 3"); }
        }


        public static void Log(string msg, int debugLevel)
        {
            if (debugLevel != 1 && debugLevel != 2 && debugLevel != 3) return;
            if (!Level3)                // Level 3 includes both level 1 and level 2 logging. 
            {
                if (debugLevel == 1 && !Level1) return;
                if (debugLevel == 2 && !Level2) return;
                
            }
            else { debugLevel = 3; }
            if (msg == _logSpam) return;

            Logging.Write(string.Format("DEBUG ({0}) {1}", debugLevel, msg)); _logSpam = msg;
        }

    }
}