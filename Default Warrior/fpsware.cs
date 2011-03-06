using System;
using System.Diagnostics;
using System.Threading;
using Hera.Config;
using Hera.Helpers;
using Hera.UI;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Hera
{
    public partial class Fpsware : CombatRoutine
    {
        // ************************************************************************************
        // Miscelaneous variables used as part of the CC
        //
        public string Environment;                                          // Used for dynamic environment loading
        private bool _isCCLoaded;                                           // A variable I use to check if the CC is loaded
        public bool IsBotRunning;                                           // Currently doing nothing
        private readonly Stopwatch _pulseTimer = new Stopwatch();           // Used in the pulse check as it does not overload HB
        private static LocalPlayer Me { get { return ObjectManager.Me; } }  // A global property of ME
        private static WoWUnit CT { get { return Me.CurrentTarget; } }      // A global property of my current target
        // ************************************************************************************


        // ************************************************************************************
        // Required HB overrides
        //
        public override string Name { get { return String.Format("{0}({1}) by {2}", CCName, _versionNumber, AuthorName); } }
        public override WoWClass Class { get { return CCClass; } }
        // ************************************************************************************

        #region CC Constructor
        public Fpsware()
        {
            // If you are not this class don't continue past this point. 
            if (Me.Class != CCClass) return;

            // Register the events of the Stop / Start buttons in HB
            BotEvents.OnBotStarted += BotEvents_OnBotStarted;
            BotEvents.OnBotStopped += BotEvents_OnBotStopped;

            // Finds the spec of your class: 0,1,2,3 and uses an enum to return something more logical
            // 0 = no talent points spent yet
            ClassHelper.ClassSpec = (ClassType) Talents.Spec;
            Utils.Log(string.Format("You are a level {0} {1} {2}", Me.Level,ClassHelper.ClassSpec,Me.Class));

            // Load the settings from the XML file
            LoadSettings();
        }
        #endregion
        
        #region Configuration Form Stuff
        // All very straight forward here. The name of the UI form is called UIForm
        // It will open the UI in a seperate thread

        private static Thread Thread { get; set; }
        private UIForm _gui;

        // This overrides HB OnButtonPress event. This where we assign the form to a thread and start the thread
        public override void OnButtonPress() { Thread = new Thread(OpenGui) { IsBackground = true, Name = "GuiThread" }; Thread.Start(); }
        public void OpenGui() { _gui = new UIForm(); _gui.ShowDialog(); }

        // Yes you need to display the WantButton
        // As this is a 'HB default' CC the UI is turned off by default
        // To enable to UI edit the KarisSettings.xml file and change:
        // <ShowUI>... never</ShowUI>
        // to
        //<ShowUI>... always</ShowUI>
        public override bool WantButton
        {
            get { return Settings.ShowUI.Contains("always"); }
        }
        #endregion

        #region Load Settings
        private void LoadSettings()
        {
            Utils.Log("Loading settings ...");

            Settings.Load();
            Utils.Log(!_isCCLoaded ? "Settings loaded" : "Settings have been updated with your changes", Utils.Colour("DarkRed"));
            Settings.DirtyData = false;

        }
        #endregion
    }
}
