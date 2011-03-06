using System;
using Hera.Config;
using Hera.Helpers;
using Hera.SpellsMan;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.POI;
using Styx.WoWInternals.WoWObjects;

namespace Hera
{
    public partial class Fpsware
    {
        // ************************************************************************************
        //
        public const string CCName = "Fpsware Warrior";                                 // Name of the CC displayed to the user
        public const string AuthorName = "Fpsware";                                     // Part of the string used in the CC name
        private readonly Version _versionNumber = new Version(0, 0, 9);                 // Part of the string used in the CC name
        public const WoWClass CCClass = WoWClass.Warrior;                               // The class this CC will support
        // ************************************************************************************

        #region HB Start Up
        // This event is fired each time you hit the Start button in HB
        // The only important thing in here is setting the pull distance. If you're Feral then set it to melee distance
        // If you're Balance or other then set it to the maximum range on the Wrath spell
        void BotEvents_OnBotStarted(EventArgs args)
        {

            Settings.LoadEnvironment();

            Utils.Timers.Add("Pulse");          // Only do certain things in the Pulse check every 1 second
            Utils.Timers.Add("Environment");    // Check environment so we can dynamically load settings


            IsBotRunning = true;
            ClassHelper.MaximumDistance = 5.70;
            ClassHelper.MinimumDistance = 3.5;

            Utils.Log("Pull Max Distance set to " + ClassHelper.MaximumDistance);
            Utils.Log("Pull Min Distance set to " + ClassHelper.MinimumDistance);
        }

        // This event is fired each time you hit the Stop button in HB
        // Currently its only asigning FALSE to a variable, but you go do anything you want in here
        void BotEvents_OnBotStopped(EventArgs args)
        {
            IsBotRunning = false;
        }
        #endregion

        #region Pulse
        public override void Pulse()
        {
            // HB runs this as frequenty as possible. I don't know the exact frequency but its supposed to be 5-10 times per second
            // Anything you want checked on a regular basis you may want to add here. 
            // For example buffing / healing random players

            base.Pulse();

            // If Settings.DirtyData = true it will reload the settings from the XML file
            // This reads the XML file and re-populates the Settings class with any changed values
            if (!_isCCLoaded)
            {
                _isCCLoaded = true;
                Settings.DirtyData = true;
            }
            if (Settings.DirtyData) LoadSettings();

            // Dynamically load the config file
            if (Settings.EnvironmentLoading.Contains("Automatic") && Utils.Timers.Expired("Environment", 5000) && StyxWoW.IsInGame && !Me.Combat)
            {
                string currentEnvironment = "PVE";

                if (Utils.IsBattleground) currentEnvironment = "PVP";
                else if (Me.IsInInstance) currentEnvironment = "Instance";

                if (Environment != currentEnvironment)
                {
                    Environment = currentEnvironment;
                    Utils.Log("Environment has changed, automatically loading " + currentEnvironment + " settings");
                    Settings.Environment = currentEnvironment;
                    Settings.Load();
                }

                Utils.Timers.Recycle("Environment", 5000);
            }

            // So we don't overload HB the below code is only run once per second
            if (!Utils.Timers.Expired("Pulse", 1000)) return;
            Utils.Timers.Recycle("Pulse", 1000);

        }


        #endregion
    }
}
