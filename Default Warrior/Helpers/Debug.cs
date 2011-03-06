using Styx.Helpers;
using System;

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

        // Exception logging
        [DefaultValue("no module name")]
        public static string ModuleName { get; set; }

        [DefaultValue("")]
        public static string ModuleMessage { get; set; }
        public static void Catch(Exception e)
        {
            Logging.WriteDebug("=================== [Exception Caught] ===================");
            Logging.WriteDebug(" ");
            Logging.WriteDebug(String.Format("Module: {0}", ModuleName));
            Logging.WriteDebug(String.Format("Module message: {0}",  ModuleMessage));
            Logging.WriteDebug(String.Format("Exception message: {0}", e.Message));
            Logging.WriteDebug(" ");
            Logging.WriteDebug("==========================================================");
        }

        public static void Catch(string moduleName, string moduleMessage, Exception e)
        {
            Logging.WriteDebug("=================== [Exception Caught] ===================");
            Logging.WriteDebug(" ");
            Logging.WriteDebug(String.Format("Module: {0}", moduleName));
            Logging.WriteDebug(String.Format("Module message: {0}", moduleMessage));
            Logging.WriteDebug(String.Format("Exception message: {0}", e.Message));
            Logging.WriteDebug(" ");
            Logging.WriteDebug("==========================================================");
        }


    }
}