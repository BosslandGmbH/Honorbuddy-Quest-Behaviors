using System;
using System.Diagnostics;
using System.IO;
using System.Xml;
using Hera.Helpers;
using Styx.Helpers;

namespace Hera.Config
{
    public static class Settings
    {
        private const string FileName = @"CustomClasses\Default Druid\Class Specific\Config\Settings.xml";
        private static string _fullFileNameAndPath = "";

        // Common settings template
        public static string Cleanse { get; set; }
        public static bool DirtyData { get; set; }
        public static int RestMana { get; set; }
        public static int RestHealth { get; set; }
        public static int PotionMana { get; set; }
        public static string Debug { get; set; }
        public static string RAFTarget { get; set; }
        public static string HealPartyMembers { get; set; }
        public static string ShowUI { get; set; }
        public static string SmartEatDrink { get; set; }
        public static int CombatTimeout { get; set; }

        // class specific
        public static string Regrowth { get; set; }
        public static int RegrowthHealth { get; set; }
        public static string HealingTouch { get; set; }
        public static int HealingTouchHealth { get; set; }
        public static int InnervateMana { get; set; }

        // Balance
        public static string PullBalance { get; set; }
        public static string FaerieFireBalance { get; set; }
        public static string InsectSwarm { get; set; }
        public static string Moonfire { get; set; }
        public static string Starsurge { get; set; }
        public static string ForceOfNature { get; set; }
        public static string PrimaryDPSSpell { get; set; }

        // Feral
        public static string PullFeral { get; set; }
        public static int AttackEnergy { get; set; }
        public static string FaerieFireFeral { get; set; }
        public static string Rake { get; set; }
        public static string SkullBash { get; set; }
        public static string TigersFury { get; set; }
        public static string SavageRoar { get; set; }
        public static string Swipe { get; set; }
        public static string Thrash { get; set; }
        public static string Rip { get; set; }
        public static string Maim { get; set; }
        public static string FerociousBite { get; set; }
        public static string BearForm { get; set; }
        
        // Travel Form
        public static string TravelForm { get; set; }
        public static int TravelFormMinDistance { get; set; }
        public static int TravelFormHostileRange { get; set; }




        public static void Save()
        {


            try
            {

                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(_fullFileNameAndPath);

                // Common settings template
                xmlDoc.SelectSingleNode("//Druid/ShowUI").InnerText = ShowUI;
                xmlDoc.SelectSingleNode("//Druid/SmartEatDrink").InnerText = SmartEatDrink;
                xmlDoc.SelectSingleNode("//Druid/HealPartyMembers").InnerText = HealPartyMembers;
                xmlDoc.SelectSingleNode("//Druid/RAFTarget").InnerText = RAFTarget;
                xmlDoc.SelectSingleNode("//Druid/Debug").InnerText = Debug;
                xmlDoc.SelectSingleNode("//Druid/PotionMana").InnerText = PotionMana.ToString();
                xmlDoc.SelectSingleNode("//Druid/RestMana").InnerText = RestMana.ToString();
                xmlDoc.SelectSingleNode("//Druid/RestHealth").InnerText = RestHealth.ToString();
                xmlDoc.SelectSingleNode("//Druid/Cleanse").InnerText = Cleanse;
                xmlDoc.SelectSingleNode("//Druid/CombatTimeout").InnerText = CombatTimeout.ToString();

                // Class specific
                // Healing
                xmlDoc.SelectSingleNode("//Druid/Regrowth").InnerText = Regrowth;
                xmlDoc.SelectSingleNode("//Druid/RegrowthHealth").InnerText = RegrowthHealth.ToString();
                xmlDoc.SelectSingleNode("//Druid/HealingTouch").InnerText = HealingTouch;
                xmlDoc.SelectSingleNode("//Druid/HealingTouchHealth").InnerText = HealingTouchHealth.ToString();
                xmlDoc.SelectSingleNode("//Druid/InnervateMana").InnerText = InnervateMana.ToString();

                // Balance
                xmlDoc.SelectSingleNode("//Druid/PullBalance").InnerText = PullBalance;
                xmlDoc.SelectSingleNode("//Druid/FaerieFireBalance").InnerText = FaerieFireBalance;
                xmlDoc.SelectSingleNode("//Druid/InsectSwarm").InnerText = InsectSwarm;
                xmlDoc.SelectSingleNode("//Druid/Moonfire").InnerText = Moonfire;
                xmlDoc.SelectSingleNode("//Druid/Starsurge").InnerText = Starsurge;
                xmlDoc.SelectSingleNode("//Druid/PrimaryDPSSpell").InnerText = PrimaryDPSSpell;
                xmlDoc.SelectSingleNode("//Druid/ForceOfNature").InnerText = ForceOfNature;

                // Feral
                xmlDoc.SelectSingleNode("//Druid/PullFeral").InnerText = PullFeral;
                xmlDoc.SelectSingleNode("//Druid/AttackEnergy").InnerText = AttackEnergy.ToString();
                xmlDoc.SelectSingleNode("//Druid/FaerieFireFeral").InnerText = FaerieFireFeral;
                xmlDoc.SelectSingleNode("//Druid/Rake").InnerText = Rake;
                xmlDoc.SelectSingleNode("//Druid/SkullBash").InnerText = SkullBash;
                xmlDoc.SelectSingleNode("//Druid/TigersFury").InnerText = TigersFury;
                xmlDoc.SelectSingleNode("//Druid/Swipe").InnerText = Swipe;
                xmlDoc.SelectSingleNode("//Druid/SavageRoar").InnerText = SavageRoar;
                xmlDoc.SelectSingleNode("//Druid/Thrash").InnerText = Thrash;
                xmlDoc.SelectSingleNode("//Druid/Rip").InnerText = Rip;
                xmlDoc.SelectSingleNode("//Druid/Maim").InnerText = Maim;
                xmlDoc.SelectSingleNode("//Druid/FerociousBite").InnerText = FerociousBite;
                xmlDoc.SelectSingleNode("//Druid/BearForm").InnerText = BearForm;

                // Travel Form
                xmlDoc.SelectSingleNode("//Druid/TravelForm").InnerText = TravelForm;
                xmlDoc.SelectSingleNode("//Druid/TravelFormMinDistance").InnerText = TravelFormMinDistance.ToString();
                xmlDoc.SelectSingleNode("//Druid/TravelFormHostileRange").InnerText = TravelFormHostileRange.ToString();




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
                
                XmlTextReader reader;
                XmlNode xvar;

                string sPath = Process.GetCurrentProcess().MainModule.FileName;
                sPath = Path.GetDirectoryName(sPath);
                sPath = Path.Combine(sPath, FileName);
                _fullFileNameAndPath = sPath;

                Utils.Log("Loading settings from the config file...");

                try { reader = new XmlTextReader(sPath); }
                catch (Exception e) { Logging.WriteDebug(String.Format("Error loading configuration file: {0}", e.Message)); return; }

                XmlDocument xml = new XmlDocument();


                xml.Load(reader);

                // Common settings template
                currentXMLKey = "//Druid/HealPartyMembers"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { HealPartyMembers = Convert.ToString(xvar.InnerText); }
                currentXMLKey = "//Druid/RAFTarget"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { RAFTarget = Convert.ToString(xvar.InnerText); }
                currentXMLKey = "//Druid/Debug"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { Debug = Convert.ToString(xvar.InnerText); }
                currentXMLKey = "//Druid/PotionMana"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { PotionMana = Convert.ToInt16(xvar.InnerText); }
                currentXMLKey = "//Druid/RestMana"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { RestMana = Convert.ToInt16(xvar.InnerText); }
                currentXMLKey = "//Druid/RestHealth"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { RestHealth = Convert.ToInt16(xvar.InnerText); }
                currentXMLKey = "//Druid/Cleanse"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { Cleanse = Convert.ToString(xvar.InnerText); }
                currentXMLKey = "//Druid/ShowUI"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { ShowUI = Convert.ToString(xvar.InnerText); }
                currentXMLKey = "//Druid/SmartEatDrink"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { SmartEatDrink = Convert.ToString(xvar.InnerText); }
                currentXMLKey = "//Druid/CombatTimeout"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { CombatTimeout = Convert.ToInt16(xvar.InnerText); }

                // Class specific

                // Healing and Mana Regen
                currentXMLKey = "//Druid/Regrowth"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { Regrowth = Convert.ToString(xvar.InnerText); }
                currentXMLKey = "//Druid/RegrowthHealth"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { RegrowthHealth = Convert.ToInt16(xvar.InnerText); }
                currentXMLKey = "//Druid/HealingTouch"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { HealingTouch = Convert.ToString(xvar.InnerText); }
                currentXMLKey = "//Druid/HealingTouchHealth"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { HealingTouchHealth = Convert.ToInt16(xvar.InnerText); }
                currentXMLKey = "//Druid/InnervateMana"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { InnervateMana = Convert.ToInt16(xvar.InnerText); }

                // Balance
                currentXMLKey = "//Druid/PullBalance"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { PullBalance = Convert.ToString(xvar.InnerText); }
                currentXMLKey = "//Druid/FaerieFireBalance"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { FaerieFireBalance = Convert.ToString(xvar.InnerText); }
                currentXMLKey = "//Druid/InsectSwarm"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { InsectSwarm = Convert.ToString(xvar.InnerText); }
                currentXMLKey = "//Druid/Moonfire"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { Moonfire = Convert.ToString(xvar.InnerText); }
                currentXMLKey = "//Druid/Starsurge"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { Starsurge = Convert.ToString(xvar.InnerText); }
                currentXMLKey = "//Druid/ForceOfNature"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { ForceOfNature = Convert.ToString(xvar.InnerText); }
                currentXMLKey = "//Druid/PrimaryDPSSpell"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { PrimaryDPSSpell = Convert.ToString(xvar.InnerText); }

                // Feral
                currentXMLKey = "//Druid/PullFeral"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { PullFeral = Convert.ToString(xvar.InnerText); }
                currentXMLKey = "//Druid/AttackEnergy"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { AttackEnergy = Convert.ToInt16(xvar.InnerText); }
                currentXMLKey = "//Druid/FaerieFireFeral"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { FaerieFireFeral = Convert.ToString(xvar.InnerText); }
                currentXMLKey = "//Druid/Rake"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { Rake = Convert.ToString(xvar.InnerText); }
                currentXMLKey = "//Druid/SkullBash"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { SkullBash = Convert.ToString(xvar.InnerText); }
                currentXMLKey = "//Druid/TigersFury"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { TigersFury = Convert.ToString(xvar.InnerText); }
                currentXMLKey = "//Druid/SavageRoar"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { SavageRoar = Convert.ToString(xvar.InnerText); }
                currentXMLKey = "//Druid/Swipe"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { Swipe = Convert.ToString(xvar.InnerText); }
                currentXMLKey = "//Druid/Thrash"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { Thrash = Convert.ToString(xvar.InnerText); }
                currentXMLKey = "//Druid/Rip"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { Rip = Convert.ToString(xvar.InnerText); }
                currentXMLKey = "//Druid/Maim"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { Maim = Convert.ToString(xvar.InnerText); }
                currentXMLKey = "//Druid/FerociousBite"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { FerociousBite = Convert.ToString(xvar.InnerText); }
                currentXMLKey = "//Druid/BearForm"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { BearForm = Convert.ToString(xvar.InnerText); }
                
                // Travel Form
                currentXMLKey = "//Druid/TravelForm"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { TravelForm = Convert.ToString(xvar.InnerText); }
                currentXMLKey = "//Druid/TravelFormMinDistance"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { TravelFormMinDistance = Convert.ToInt16(xvar.InnerText); }
                currentXMLKey = "//Druid/TravelFormHostileRange"; xvar = xml.SelectSingleNode(currentXMLKey); if (xvar != null) { TravelFormHostileRange = Convert.ToInt16(xvar.InnerText); }

                
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