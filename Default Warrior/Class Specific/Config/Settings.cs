using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml;
using Hera.Helpers;
using Styx.Helpers;

namespace Hera.Config
{
    public static class Settings
    {
        private const string FileName = @"CustomClasses\Default Warrior\Class Specific\Config\";
        private static string _fullFileNameAndPath = "";
        private static string _settingsPathAndFile = "";

        public static string LowLevelCheck { get; set; }

        // Common settings template
        public static string Environment { get; set; }
        public static string EnvironmentLoading { get; set; }


        // Common settings template
        public static bool DirtyData { get; set; }

        public static int RestHealth { get; set; }
        public static int RestMana { get; set; }
        public static string Debug { get; set; }
        public static string RAFTarget { get; set; }
        public static string ShowUI { get; set; }
        public static string SmartEatDrink { get; set; }
        public static int CombatTimeout { get; set; }
        public static int HealthPotion { get; set; }
        public static int ManaPotion { get; set; }
        public static int LifebloodHealth{ get; set; }

        // class specific
        public static string Rend { get; set; }
        public static string ThunderClap { get; set; }
        public static string Shout { get; set; }
        public static string DeathWish { get; set; }
        public static string BerserkerRage { get; set; }
        public static int AttackRage { get; set; }
        public static string Execute { get; set; }
        public static string DeadlyCalm { get; set; }
        public static string Cleave { get; set; }
        public static string Whirlwind { get; set; }
        public static string Retaliation { get; set; }

        public static string Hamstring { get; set; }
        public static string SweepingStrikes { get; set; }
        public static string SunderArmor { get; set; }
        public static int EnragedRegenerationHealth { get; set; }
        public static string Recklessness { get; set; }




        public static void SaveEnvironment()
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(_settingsPathAndFile);

            xmlDoc.SelectSingleNode("//Settings/Environment").InnerText = Environment;
            xmlDoc.SelectSingleNode("//Settings/Loading").InnerText = EnvironmentLoading;

            xmlDoc.Save(_settingsPathAndFile);
        }

        public static void LoadEnvironment()
        {
            _settingsPathAndFile = "";
            _settingsPathAndFile = Process.GetCurrentProcess().MainModule.FileName;
            _settingsPathAndFile = Path.GetDirectoryName(_settingsPathAndFile);
            _settingsPathAndFile = Path.Combine(_settingsPathAndFile, FileName);
            _settingsPathAndFile = Path.Combine(_settingsPathAndFile, "Settings.xml");

            XmlTextReader reader = new XmlTextReader(_settingsPathAndFile);
            XmlDocument xml = new XmlDocument();
            xml.Load(reader);

            XmlNode xvar = xml.SelectSingleNode("//Settings/Environment"); Environment = Convert.ToString(xvar.InnerText);
            xvar = xml.SelectSingleNode("//Settings/Loading"); EnvironmentLoading = Convert.ToString(xvar.InnerText);

            reader.Close();
        }


        public static void Save()
        {
            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(_fullFileNameAndPath);
                string currentXMLKey = "NO KEY READ YET";

                Type type = typeof(Settings); PropertyInfo[] props = type.GetProperties();
                foreach (var p in props)
                {
                    if (p.Name == "DirtyData") continue;
                    if (p.Name == "Environment") continue;
                    if (p.Name == "EnvironmentLoading") continue;

                    PropertyInfo pInfo = type.GetProperty(p.Name);
                    object propValue = pInfo.GetValue(p.Name, null);

                    //Utils.Log("*** " + p.Name + " value: " + propValue);
                    Helpers.Debug.ModuleMessage = p.Name;
                    Helpers.Debug.ModuleName = propValue.ToString();

                    currentXMLKey = "//" + Fpsware.CCClass + "/" + p.Name;
                    xmlDoc.SelectSingleNode(currentXMLKey).InnerText = propValue.ToString();
                }

                xmlDoc.Save(_fullFileNameAndPath);
                Utils.Log("Settings have been updated");
            }
            catch (Exception e)
            {
                Logging.WriteDebug("**********************************************************************");
                Logging.WriteDebug("**********************************************************************");
                Logging.WriteDebug(" ");
                Logging.WriteDebug(" ");
                Logging.WriteDebug(String.Format("Exception in XML Save {0}", e.Message));
                Logging.WriteDebug(String.Format("Node {0}", Helpers.Debug.ModuleMessage));
                Logging.WriteDebug(String.Format("Value {0}", Helpers.Debug.ModuleName));
                Logging.WriteDebug(" ");
                Logging.WriteDebug(" ");
                Logging.WriteDebug("**********************************************************************");
                Logging.WriteDebug("**********************************************************************");
            }
        }

        public static void Load()
        {
            string currentXMLKey = "NO KEY READ YET";

            try
            {
                if (string.IsNullOrEmpty(Environment)) Environment = "PVE";

                XmlTextReader reader; XmlNode xvar;

                string sPath = Process.GetCurrentProcess().MainModule.FileName;
                sPath = Path.GetDirectoryName(sPath);
                sPath = Path.Combine(sPath, FileName);
                sPath = Path.Combine(sPath, Environment);
                sPath = sPath + ".xml";
                _fullFileNameAndPath = sPath;

                Utils.Log(string.Format("Loading settings from the {0} config file...", Environment));

                try { reader = new XmlTextReader(sPath); }
                catch (Exception e) { Logging.WriteDebug(String.Format("Error loading configuration file: {0}", e.Message)); return; }
                XmlDocument xml = new XmlDocument();
                xml.Load(reader);

                Type type = typeof(Settings); PropertyInfo[] props = type.GetProperties();
                foreach (var p in props)
                {
                    if (p.Name == "DirtyData") continue;
                    if (p.Name == "Environment") continue;
                    if (p.Name == "EnvironmentLoading") continue;

                    PropertyInfo pInfo = type.GetProperty(p.Name);
                    //object propValue = pInfo.GetValue(p.Name, null);

                    currentXMLKey = "//" + Fpsware.CCClass + "/" + p.Name;
                    xvar = xml.SelectSingleNode(currentXMLKey);

                    switch (pInfo.PropertyType.Name)
                    {
                        case "Boolean": if (xvar != null) { bool tmp = Convert.ToBoolean(xvar.InnerText); p.SetValue(type, tmp, null); } break;
                        case "String": if (xvar != null) { string tmp = Convert.ToString(xvar.InnerText); p.SetValue(type, tmp, null); } break;
                        case "Int32": if (xvar != null) { int tmp = Convert.ToInt16(xvar.InnerText); p.SetValue(type, tmp, null); } break;
                    }
                }

                reader.Close();
                Utils.Log("Finished loading settings");
            }

            catch (Exception e)
            {
                Logging.WriteDebug("**********************************************************************");
                Logging.WriteDebug("**********************************************************************");
                Logging.WriteDebug(" ");
                Logging.WriteDebug(" ");
                Logging.WriteDebug(String.Format("Exception in XML Load: {0}", e.Message)); Logging.WriteDebug("-- Attempted to read: " + currentXMLKey);
                Logging.WriteDebug(" ");
                Logging.WriteDebug(" ");
                Logging.WriteDebug("**********************************************************************");
                Logging.WriteDebug("**********************************************************************");
            }
        }


    }
}