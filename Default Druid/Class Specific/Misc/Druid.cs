using System;
using System.Collections.Generic;
using Hera.Config;
using Hera.Helpers;
using Hera.SpellsMan;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;
using Styx.WoWInternals.WoWObjects;

namespace Hera
{
    public partial class Fpsware
    {
        // ************************************************************************************
        //
        public const string CCName = "Fpsware Druid";                                // Name of the CC displayed to the user
        public const string AuthorName = "Fpsware";                                 // Part of the string used in the CC name
        private readonly Version _versionNumber = new Version(0, 0, 9);             // Part of the string used in the CC name
        private const WoWClass CCClass = WoWClass.Druid;                            // The class this CC will support
        // ************************************************************************************

        #region HB Start Up
        // This event is fired each time you hit the Start button in HB
        // The only important thing in here is setting the pull distance. If you're Feral then set it to melee distance
        // If you're Balance or other then set it to the maximum range on the Wrath spell
        void BotEvents_OnBotStarted(EventArgs args)
        {
            IsBotRunning = true;

            if (ClassHelper.ClassSpec == ClassType.Feral)
            {
                ClassHelper.MaximumDistance = 5.0;
                ClassHelper.MinimumDistance = 3;
            }
            else
            {
                double spellCastDistance = Spell.MaxDistance("Wrath");
                ClassHelper.MaximumDistance = spellCastDistance;
                ClassHelper.MinimumDistance = spellCastDistance - 2;
            }

            Utils.Log("Pull Max Distance set to " + ClassHelper.MaximumDistance);
            Utils.Log("Pull Min Distance set to " + ClassHelper.MinimumDistance);
            
            /*
            Utils.Log("***********************[ Me.Auras]************************");
            foreach (KeyValuePair<string, WoWAura> keyValuePair in Me.Auras)
            {
                Utils.Log(" key = " + keyValuePair.Key + " value = " + keyValuePair.Value);
            }
            Utils.Log("***********************************************");
            Utils.Log(" ");
            Utils.Log("  ");

            Utils.Log("***********************[ Me.Active.Auras]************************");
            foreach (KeyValuePair<string, WoWAura> keyValuePair in Me.ActiveAuras)
            {
                Utils.Log(" key = " + keyValuePair.Key + " value = " + keyValuePair.Value);
            }
             */
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

            // So we don't overload HB the below code is only run once per second
            if (_pulseTimer.IsRunning && _pulseTimer.ElapsedMilliseconds <= 2000) return;
            _pulseTimer.Reset();
            _pulseTimer.Start();

            // Decurse
            // Remove all debuffs from yourself
            /*
            if (CLC.ResultOK(Settings.Cleanse) && ClassHelper.NeedToDecurse)
            {
                if (Spell.CanCast("Remove Corruption")) Spell.Cast("Remove Corruption", Me);
            }
             */

            // Travel Form, Transformers let move out!
            if (ClassHelper.CanUseTravelForm) ClassHelper.Shapeshift.TravelForm();


            // Quick and dirty fix for cat form range issue if you've not spent any talents);
            if (ClassHelper.ClassSpec == ClassType.None && (ClassHelper.Shapeshift.IsCatForm || ClassHelper.Shapeshift.IsBearForm))
            {
                ClassHelper.MaximumDistance = 5.5;
                ClassHelper.MinimumDistance = 3.0; 
            }
            // Decurse - You and all party members
            if (CLC.ResultOK(Settings.Cleanse) && Spell.CanCast("Remove Corruption"))
            {
                WoWUnit p = ClassHelper.NeedToDecursePlayer;
                if (p != null)
                {
                    if (Spell.CanCast("Remove Corruption")) Spell.Cast("Remove Corruption", p);
                }
            }
            // Buff players
            //if (Me.IsInParty && !Me.Combat)
            if (Me.IsInParty && Self.IsManaAbove(40))
            {
                WoWUnit p = RAF.PlayerNeedsBuffing("Mark of the Wild");
                if (p != null)
                {
                    if (Spell.CanCast("Mark of the Wild")) Spell.Cast("Mark of the Wild", p);
                }
            }




        }


        #endregion
    }
}
