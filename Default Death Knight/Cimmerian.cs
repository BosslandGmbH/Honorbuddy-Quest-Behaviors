/**************************/
/* Cimmerian 2.2 by Mord /*
/**************************/
/*This version requires Honor Buddy 1.9 somthing*/
/*If you need help please check the thread where you got this download*/
/*I do not rip code from others. Code I use from others is given to me and I add credit*/
/*Please return the favor.*/

//Notes to self

//Code Lichborne 10 Frost
//Code Blood Tap
//Code Logic for Perma Pet
//Code Death and Decay
//Code Anti Magic Zone (Multi casters? Leave it up to the user)
//Code Ghoul Frenzy (when doing pets)
//Save abilities like DRW for adds, if enabled by user

using System;
using System.Diagnostics;
using System.Linq;
using System.Media;
using System.Threading;
using System.Windows.Forms;
//advanced code
//using Cimmerian.Gui;
//
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Cimmerian
{
    public class Deathknight : CombatRoutine
    {
        #region System Stuff

        public override string Name
        {
            get { return "Default Unholy DK 1.0 by Mord"; } 
        }

        public override WoWClass Class
        {
            get { return WoWClass.DeathKnight; }
        }

        public override bool NeedCombatBuffs
        {
            get { return false; }
        }

        public override bool NeedPullBuffs
        {
            get { return false; }
        }

        public override bool NeedPreCombatBuffs
        {
            get { return false; }
        }

        public override bool NeedHeal
        {
            get { return false; }
        }

        //advanced Code
        //public override bool WantButton
        //{
        //get { return true; }
        //}

        //public Form ConfigForm
        //{
        //get { return new CimmerianForm(); }
        //}

        public override void CombatBuff()
        {
        }

        public override void PullBuff()
        {
        }

        //public override void HandleFalling() { }

        public override void PreCombatBuff()
        {
        }

        public override void Heal()
        {
        }

        //Yes I do!
        //Advanced Code
        //public override void OnButtonPress() //Do on Config Button Press
        //{
        //if (_cachedForm != null)
        //_cachedForm.ShowDialog();

        //_cachedForm = new CimmerianForm();
        //_cachedForm.ShowDialog();
        //}

        private void Slog(string msg)
        {
            if (msg == _logspam)
            {
                return;
            }

            Logging.Write(msg);
            _logspam = msg;
        }

        #region Global Variables DONE

        private string _logspam;

        private static LocalPlayer Me
        {
            get { return ObjectManager.Me; }
        }

        #endregion

        #region Class Specific Variables

        public static int StuckCount;
        public static int LoSCount;
        private bool _addSpam;
        private bool _bloodBoilCast;
        private bool _bloodStrikeCast;
        private bool _bloodStrikeCastTwo;
        private bool _drwSpam;

        /// 
        /// ////////////////////////////////////////        
        /// ///Do not change anything in this box///
        /// ////////////////////////////////////////
        /// 
        //bool UseBloodTap = false;
        private bool _drwToggle;
        private bool _deathStrikeCast;
        private bool _deathStrikeCastTwo;
        private bool _gotAdds;
        private bool _hcCast;
        private bool _hcToggle;
        private bool _heartStrikeCast;
        private bool _heartStrikeCastTwo;
        private bool _icyTouchCast;
        private bool _moveWithMelee;
        private bool _obliterateCast;
        private bool _pestilenceCast;
        private bool _plagueStrikeCast;
        private bool _pullSpam;
        private bool _pullTypeSpam;
        private bool _pvPPullSpam;
        private bool _pvPPullTypeSpam;
        private bool _rimeProc;
        private bool _sgSpam;
        private bool _sgToggle;
        private bool _scourgeStrikeCast;
        private bool _openerComplete;
        private bool _pulseSlog;

        #region Basic Settings

        private const int RestHealth = 55;


        private const bool UseBoneShield = false;


        private const bool CaLevelOne = false;


        private const bool CaLevelTwo = false;


        private const bool CaLevelThree = false;


        private const bool UseHorn = true;


        private const bool PvPOpenWithDeathGrip = true;


        private const bool PvPOpenWithIcyTouch = false;


        private const bool OpenWithDeathGrip = true;


        private const bool OpenWithIcyTouch = false;


        private const bool OpenWithDarkCommand = false;


        private const int PvPIcyTouchRange = 20;


        private const bool PvPOpenWithIcyTouchBackup = true;


        private const int IcyTouchRange = 20;


        private const bool OpenWithIcyTouchBackup = true;


        private const bool PvPUseAt = false;


        private const bool PvPUseNaaru = false;


        private const int PvPNaaruHealth = 0;


        private const bool PvPUseStoneForm = false;


        private const int PvPStoneFormHealth = 0;


        private const bool PvPUseEm = false;

        private const bool PvPUseRuneTap = false;


        private const int PvPRuneTapHealth = 0;


        private const bool PvPUseMoB = false;


        private const int PvPmoBHealth = 0;


        private const bool PvPUseVampiricBlood = false;


        private const int PvPVampiricBloodHealth = 0;


        private const bool PvPUseUnbreakableArmor = false;


        private const int PvPUnbreakableArmorHealth = 0;


        private const bool PvPUseIceboundFortitude = true;


        private const int PvPIceboundFortitudeHealth = 50;


        private const bool PvPUseStrangulate = true;


        private const bool PvPUseStrangulateMelee = true;


        private const bool PvPUseMindFreeze = true;


        private const bool PvPUseDeathGripInterupt = false;


        private const int PvPRestHealth = 55;

        private const bool PvPUseStandardRotation = false;


        private const bool PvPUseDrw = false;


        private const bool UsePoF = false;


        private const bool UseStandardRotation = false;


        private const bool UseAt = false;


        private const bool UseNaaru = false;


        private const int NaaruHealth = 0;


        private const bool UseStoneForm = false;


        private const int StoneFormHealth = 0;


        private const bool UseEm = false;


        private const bool UseStrangulate = true;


        private const bool UseStrangulateMelee = true;

        private const bool UseMindFreeze = true;


        private const bool UseDeathGripInterupt = false;


        private const bool UseRuneTap = false;


        private const int RuneTapHealth = 0;


        private const bool UseMoB = false;


        private const int MoBHealth = 0;


        private const int MinMoBHealth = 0;


        private const bool UseVampiricBlood = false;


        private const bool VampiricBloodAdds = false;


        private const int VampiricBloodHealth = 0;


        private const int MinVampiricBloodHealth = 0;


        private const bool UseUnbreakableArmor = false;


        private const int UnbreakableArmorHealth = 0;


        private const int MinUnbreakableArmorHealth = 0;


        private const bool UseIceboundFortitude = false;


        private const int IceboundFortitudeHealth = 50;


        private const int MinIceboundFortitudeHealth = 0;


        private const bool IgnoreAdds = false;


        private const bool UseDrw = false;


        private const bool DrwAdds = false;


        private const int DrwThreshold = 0;


        private const bool UseMordBloodRotation = false;


        private const bool NaaruAdds = false;


        private const bool SfAdds = false;


        private const bool PlayerAlert = false;


        private const bool PvPUseMordBloodRotation = false;


        private const bool PvPUseHysteria = false;


        private const int HysteriaThreshold = 0;


        private const bool UseHysteria = false;


        private const bool PvPUseAntiMagicShell = true;


        private const bool UseAntiMagicShell = true;


        private const bool UseDeathChill = false;


        private const bool UseHowlingBlast = false;


        private const bool UseMordFrostRotation = false;


        private const bool PvPUseMordFrostRotation = false;


        private const bool PvPUseDeathChill = false;


        private const int PdRange = 0;


        private const bool UseRaiseDead = false;


        private const bool UseSummonGargoyle = true;


        private const bool PvPUseSummonGargoyle = true;


        private const bool PvPUseMordUnholyRotation = true;


        private const bool PvPUseHorn = true;


        private const bool PvPUseBoneShield = false;


        private const bool PvPUseRaiseDead = false;


        private const bool UseMordUnholyRotation = true;


        private const bool UseWarStomp = false;


        private const int WarStompHealth = 0;


        private const bool WarStompAdds = false;


        private const bool WarStompCasters = false;


        private const bool UseChainsOfIce = true;


        private const bool UseDeathGripRunners = false;


        private const bool UseDarkCommandRunners = true;


        private const bool PvPUseWarStomp = false;


        private const int PvPWarStompHealth = 0;


        private const bool UseRime = false;


        private const bool PvPUseRime = false;


        private const bool UseLifeBlood = false;


        private const bool LifeBloodAdds = false;


        private const int LifeBloodHealth = 0;


        private const bool UseBloodFury = false;


        private const bool BloodFuryAdds = false;


        private const int BloodFuryHealth = 0;


        private const bool PvPUseLifeBlood = false;


        private const int PvPLifeBloodHealth = 0;


        private const bool PvPUseBloodFury = false;


        private const int PvPBloodFuryHealth = 0;


        private const bool PvPcoI = true;


        private const bool UnbreakableArmorAdds = false;


        private const bool IceboundFortitudeAdds = true;


        private const int UseDsssHealth = 30;


        private const int DeathPactHealth = 0;


        private const bool UseDeathPact = false;


        private const int PvPDeathPactHealth = 0;


        private const bool PvPUseDeathPact = false;


        private const int PvPUseDsssHealth = 30;


        private const int FollowerDistance = 5;


        #endregion

        #region Advanced Settings

        /*
        private static int RestHealth
        {
            get { return (int) Settings.Default.RestHealth; }
        }

        private static bool UseBoneShield
        {
            get { return Settings.Default.UseBoneShield; }
        }

        private static bool CaLevelOne
        {
            get { return Settings.Default.CALevelOne; }
        }

        private static bool CaLevelTwo
        {
            get { return Settings.Default.CALevelTwo; }
        }

        private static bool CaLevelThree
        {
            get { return Settings.Default.CALevelThree; }
        }

        private static bool UseHorn
        {
            get { return Settings.Default.UseHorn; }
        }

        private static bool PvPOpenWithDeathGrip
        {
            get { return Settings.Default.PvPOpenWithDeathGrip; }
        }

        private static bool PvPOpenWithIcyTouch
        {
            get { return Settings.Default.PvPOpenWithIcyTouch; }
        }

        private static bool OpenWithDeathGrip
        {
            get { return Settings.Default.OpenWithDeathGrip; }
        }

        private static bool OpenWithIcyTouch
        {
            get { return Settings.Default.OpenWithIcyTouch; }
        }

        private static bool OpenWithDarkCommand
        {
            get { return Settings.Default.OpenWithDarkCommand; }
        }

        private static int PvPIcyTouchRange
        {
            get { return (int) Settings.Default.PvPIcyTouchRange; }
        }

        private static bool PvPOpenWithIcyTouchBackup
        {
            get { return Settings.Default.PvPOpenWithIcyTouchBackup; }
        }

        private static int IcyTouchRange
        {
            get { return (int) Settings.Default.IcyTouchRange; }
        }

        private static bool OpenWithIcyTouchBackup
        {
            get { return Settings.Default.OpenWithIcyTouchBackup; }
        }

        private static bool PvPUseAt
        {
            get { return Settings.Default.PvPUseAT; }
        }

        private static bool PvPUseNaaru
        {
            get { return Settings.Default.PvPUseNaaru; }
        }

        private static int PvPNaaruHealth
        {
            get { return (int) Settings.Default.PvPNaaruHealth; }
        }

        private static bool PvPUseStoneForm
        {
            get { return Settings.Default.PvPUseStoneForm; }
        }

        private static int PvPStoneFormHealth
        {
            get { return (int) Settings.Default.PvPStoneFormHealth; }
        }

        private static bool PvPUseEm
        {
            get { return Settings.Default.PvPUseEM; }
        }

        private static bool PvPUseRuneTap
        {
            get { return Settings.Default.PvPUseRuneTap; }
        }

        private static int PvPRuneTapHealth
        {
            get { return (int) Settings.Default.PvPRuneTapHealth; }
        }

        private static bool PvPUseMoB
        {
            get { return Settings.Default.PvPUseMoB; }
        }

        private static int PvPmoBHealth
        {
            get { return (int) Settings.Default.PvPMoBHealth; }
        }

        private static bool PvPUseVampiricBlood
        {
            get { return Settings.Default.PvPUseVampiricBlood; }
        }

        private static int PvPVampiricBloodHealth
        {
            get { return (int) Settings.Default.PvPVampiricBloodHealth; }
        }

        private static bool PvPUseUnbreakableArmor
        {
            get { return Settings.Default.PvPUseUnbreakableArmor; }
        }

        private static int PvPUnbreakableArmorHealth
        {
            get { return (int) Settings.Default.PvPUnbreakableArmorHealth; }
        }

        private static bool PvPUseIceboundFortitude
        {
            get { return Settings.Default.PvPUseIceboundFortitude; }
        }

        private static int PvPIceboundFortitudeHealth
        {
            get { return (int) Settings.Default.PvPIceboundFortitudeHealth; }
        }

        private static bool PvPUseStrangulate
        {
            get { return Settings.Default.PvPUseStrangulate; }
        }

        private static bool PvPUseStrangulateMelee
        {
            get { return Settings.Default.PvPUseStrangulateMelee; }
        }

        private static bool PvPUseMindFreeze
        {
            get { return Settings.Default.PvPUseMindFreeze; }
        }

        private static bool PvPUseDeathGripInterupt
        {
            get { return Settings.Default.PvPUseDeathGripInterupt; }
        }

        private static int PvPRestHealth
        {
            get { return (int) Settings.Default.PvPRestHealth; }
        }

        private static bool PvPUseStandardRotation
        {
            get { return Settings.Default.PvPUseStandardRotation; }
        }

        private static bool PvPUseDrw
        {
            get { return Settings.Default.PvPUseDRW; }}

        private static bool UsePoF
        {
            get { return Settings.Default.UsePoF; }
        }

        private static bool UseStandardRotation
        {
            get { return Settings.Default.UseStandardRotation; }
        }

        private static bool UseAt
        {
            get { return Settings.Default.UseAT; }
        }

        private static bool UseNaaru
        {
            get { return Settings.Default.UseNaaru; }
        }

        private static int NaaruHealth
        {
            get { return (int) Settings.Default.NaaruHealth; }
        }

        private static bool UseStoneForm
        {
            get { return Settings.Default.UseStoneForm; }
        }

        private static int StoneFormHealth
        {
            get { return (int) Settings.Default.StoneFormHealth; }
        }

        private static bool UseEm
        {
            get { return Settings.Default.UseEM; }
        }

        private static bool UseStrangulate
        {
            get { return Settings.Default.UseStrangulate; }
        }

        private static bool UseStrangulateMelee
        {
            get { return Settings.Default.UseStrangulateMelee; }
        }

        private static bool UseMindFreeze
        {
            get { return Settings.Default.UseMindFreeze; }
        }

        private static bool UseDeathGripInterupt
        {
            get { return Settings.Default.UseDeathGripInterupt; }
        }

        private static bool UseRuneTap
        {
            get { return Settings.Default.UseRuneTap; }
        }

        private static int RuneTapHealth
        {
            get { return (int) Settings.Default.RuneTapHealth; }
        }

        private static bool UseMoB
        {
            get { return Settings.Default.UseMoB; }
        }

        private static int MoBHealth
        {
            get { return (int) Settings.Default.MoBHealth; }
        }

        private static int MinMoBHealth
        {
            get { return (int) Settings.Default.MinMoBHealth; }
        }

        private static bool UseVampiricBlood
        {
            get { return Settings.Default.UseVampiricBlood; }
        }

        private static bool VampiricBloodAdds
        {
            get { return Settings.Default.VampiricBloodAdds; }
        }

        private static int VampiricBloodHealth
        {
            get { return (int) Settings.Default.VampiricBloodHealth; }
        }

        private static int MinVampiricBloodHealth
        {
            get { return (int) Settings.Default.MinVampiricBloodHealth; }
        }

        private static bool UseUnbreakableArmor
        {
            get { return Settings.Default.UseUnbreakableArmor; }
        }

        private static int UnbreakableArmorHealth
        {
            get { return (int) Settings.Default.UnbreakableArmorHealth; }
        }

        private static int MinUnbreakableArmorHealth
        {
            get { return (int) Settings.Default.MinUnbreakableArmorHealth; }
        }

        private static bool UseIceboundFortitude
        {
            get { return Settings.Default.UseIceboundFortitude; }
        }

        private static int IceboundFortitudeHealth
        {
            get { return (int) Settings.Default.IceboundFortitudeHealth; }
        }

        private static int MinIceboundFortitudeHealth
        {
            get { return (int) Settings.Default.MinIceboundFortitudeHealth; }
        }

        private static bool IgnoreAdds
        {
            get { return Settings.Default.IgnoreAdds; }
        }

        private static bool UseDrw
        {
            get { return Settings.Default.UseDRW; }
        }

        private static bool DrwAdds
        {
            get { return Settings.Default.DRWAdds; }
        }

        private static int DrwThreshold
        {
            get { return (int) Settings.Default.DRWThreshold; }
        }

        private static bool UseMordBloodRotation
        {
            get { return Settings.Default.UseMordBloodRotation; }
        }

        private static bool NaaruAdds
        {
            get { return Settings.Default.NaaruAdds; }
        }

        private static bool SfAdds
        {
            get { return Settings.Default.SFAdds; }
        }

        private static bool PlayerAlert
        {
            get { return Settings.Default.AlertPlayers; }
        }

        private static bool PvPUseMordBloodRotation
        {
            get { return Settings.Default.PvPUseMordBloodRotation; }
        }

        private static bool PvPUseHysteria
        {
            get { return Settings.Default.PvPUseHysteria; }
        }

        private static int HysteriaThreshold
        {
            get { return (int) Settings.Default.HysteriaThreshold; }
        }

        private static bool UseHysteria
        {
            get { return Settings.Default.UseHysteria; }
        }

        private static bool PvPUseAntiMagicShell
        {
            get { return Settings.Default.PvPUseAntiMagicShell; }
        }

        private static bool UseAntiMagicShell
        {
            get { return Settings.Default.UseAntiMagicShell; }
        }

        private static bool UseDeathChill
        {
            get { return Settings.Default.UseDeathChill; }
        }

        private static bool UseHowlingBlast
        {
            get { return Settings.Default.UseHowlingBlast; }
        }

        private static bool UseMordFrostRotation
        {
            get { return Settings.Default.UseMordFrostRotation; }
        }

        private static bool PvPUseMordFrostRotation
        {
            get { return Settings.Default.PvPUseMordFrostRotation; }
        }

        private static bool PvPUseDeathChill
        {
            get { return Settings.Default.PvPUseDeathChill; }
        }

        private static int PdRange
        {
            get { return (int) Settings.Default.PDRange; }
        }

        private static bool UseRaiseDead
        {
            get { return Settings.Default.UseRaiseDead; }
        }

        private static bool UseSummonGargoyle
        {
            get { return Settings.Default.UseSummonGargoyle; }
        }

        private static bool PvPUseSummonGargoyle
        {
            get { return Settings.Default.PvPUseSummonGargoyole; }
        }

        private static bool PvPUseMordUnholyRotation
        {
            get { return Settings.Default.PvPUseMordUnholyRotation; }
        }

        private static bool PvPUseHorn
        {
            get { return Settings.Default.PvPUseHorn; }
        }

        private static bool PvPUseBoneShield
        {
            get { return Settings.Default.PvPUseBoneShield; }
        }

        private static bool PvPUseRaiseDead
        {
            get { return Settings.Default.PvPUseRaiseDead; }
        }

        private static bool UseMordUnholyRotation
        {
            get { return Settings.Default.UseMordUnholyRotation; }
        }

        private static bool UseWarStomp
        {
            get { return Settings.Default.UseWarStomp; }
        }

        private static int WarStompHealth
        {
            get { return (int) Settings.Default.WarStompHealth; }
        }

        private static bool WarStompAdds
        {
            get { return Settings.Default.WarStompAdds; }
        }

        private static bool WarStompCasters
        {
            get { return Settings.Default.WarStompCasters; }
        }

        private static bool UseChainsOfIce
        {
            get { return Settings.Default.UseChainsOfIce; }
        }

        private static bool UseDeathGripRunners
        {
            get { return Settings.Default.UseDeathGripRunners; }
        }

        private static bool UseDarkCommandRunners
        {
            get { return Settings.Default.UseDarkCommandRunners; }
        }

        private static bool PvPUseWarStomp
        {
            get { return Settings.Default.PvPUseWarStomp; }
        }

        private static int PvPWarStompHealth
        {
            get { return (int) Settings.Default.PvPWarStompHealth; }
        }

        private static bool UseRime
        {
            get { return Settings.Default.UseRime; }
        }

        private static bool PvPUseRime
        {
            get { return Settings.Default.PvPUseRime; }
        }

        private static bool UseLifeBlood
        {
            get { return Settings.Default.UseLifeBlood; }
        }

        private static bool LifeBloodAdds
        {
            get { return Settings.Default.LifeBloodAdds; }
        }

        private static int LifeBloodHealth
        {
            get { return (int) Settings.Default.LifebloodHealth; }
        }

        private static bool UseBloodFury
        {
            get { return Settings.Default.UseBloodFury; }
        }

        private static bool BloodFuryAdds
        {
            get { return Settings.Default.BloodFuryAdds; }
        }

        private static int BloodFuryHealth
        {
            get { return (int) Settings.Default.BloodFuryHealth; }
        }

        private static bool PvPUseLifeBlood
        {
            get { return Settings.Default.PvPUseLifeBlood; }
        }

        private static int PvPLifeBloodHealth
        {
            get { return (int) Settings.Default.PvPLifebloodHealth; }
        }

        private static bool PvPUseBloodFury
        {
            get { return Settings.Default.PvPUseBloodFury; }
        }

        private static int PvPBloodFuryHealth
        {
            get { return (int) Settings.Default.PvPBloodFuryHealth; }
        }

        private static bool PvPcoI
        {
            get { return Settings.Default.PvPCoI; }
        }

        private static bool UnbreakableArmorAdds
        {
            get { return Settings.Default.UnbreakableArmorAdds; }
        }

        private static bool IceboundFortitudeAdds
        {
            get { return Settings.Default.IceboundFortitudeAdds; }
        }

        private static int UseDsssHealth
        {
            get { return (int)Settings.Default.UseDSSSHealth; }
        }

        private static int DeathPactHealth
        {
            get { return (int)Settings.Default.DeathPactHealth; }
        }

        private static bool UseDeathPact
        {
            get { return Settings.Default.UseDeathPact; }
        }

        private static int PvPDeathPactHealth
        {
            get { return (int)Settings.Default.PvPDeathPactHealth; }
        }

        private static bool PvPUseDeathPact
        {
            get { return Settings.Default.PvPUseDeathPact; }
        }

        private static int PvPUseDsssHealth
        {
            get { return (int)Settings.Default.PvPUseDSSSHealth; }
        }

        private static int FollowerDistance
        {
            get { return (int)Settings.Default.FollowerDistance; }
        }
         * 
         * */

        #endregion

        #endregion

        #endregion

        private bool NeedPull(object context)
        {
            return Me.CurrentTarget.IsAlive && Me.CurrentTarget.Attackable && !Me.CurrentTarget.IsFriendly;
        }


        #region Pulse Override

        /////////////////////////////////
        //Override HB Movement (CodenameG Inspired)
        /////////////////////////////////

        public override void Pulse()
        {
            if (CaLevelTwo) // Level 2 Assist
            {


                while (Styx.Logic.BehaviorTree.TreeRoot.IsRunning && !Me.Combat)
                {
                    if (!_pulseSlog)
                    {

                        Slog("#Out of Combat. Waiting for your next Pull#");
                        Slog("#and for Nesox to fix RaF!#");
                        _pulseSlog = true;
                    }

                    Thread.Sleep(25);
                }




            }

            //if (Me.GotTarget && !Me.Combat)
            //{

            //if(Me.CurrentTarget.IsAlive && Me.CurrentTarget.Attackable && !Me.CurrentTarget.IsFriendly)
            //{
            //Slog("Not Sleep");
            //RoutineManager.Current.Pull();
            //return;  
            //}

            //}
        }

        #endregion

        #region Pull DONE

        /////////////////////////////////
        //A global state run before combat when in range of target
        /////////////////////////////////

        private readonly Stopwatch _pullTimer = new Stopwatch(); //A Pull Timer        

        public override void Pull()
        {
            var num4 = FollowerDistance + 3; //Need a 4 yard threshold to prevent constant moving

            if (((InParty()) && (IsLeader())) || ((!InParty())) || ((InBg()))) //Solo , Pary Leader, BG
            {

                if (Me.GotTarget && !Me.Combat) //We need to make sure our target is valid
                {
                    _icyTouchCast = false;
                    _plagueStrikeCast = false;
                    _bloodStrikeCast = false;
                    _bloodStrikeCastTwo = false;
                    _heartStrikeCast = false;
                    _heartStrikeCastTwo = false;
                    _obliterateCast = false;
                    _pestilenceCast = false;
                    _bloodBoilCast = false;
                    _deathStrikeCast = false;
                    _deathStrikeCastTwo = false;
                    _scourgeStrikeCast = false;
                    _drwToggle = false;
                    _sgToggle = false;
                    _hcToggle = false;

                    if (InBg())
                    {
                        if (PvPUseBoneShield) //Cast Boneshield now!
                        {
                            if (!Me.Auras.ContainsKey("Bone Shield"))
                            {
                                if (SpellManager.CanCast("Bone Shield"))
                                    BoneShield();
                            }
                        }

                        if (PvPUseHorn) //Cast Horn now!
                        {
                            if (!Me.Auras.ContainsKey("Horn of Winter"))
                            {
                                if (SpellManager.CanCast("Horn of Winter"))
                                {
                                    WoWMovement.MoveStop();
                                    Thread.Sleep(125);
                                    HornOfWinter();
                                    return;
                                }
                            }
                        }

                        if (PvPUseRaiseDead) //Cast Raise Dead now!
                        {
                            if (SpellManager.CanCast("Raise Dead"))
                                RaiseDead();
                        }

                        if (!_pullTimer.IsRunning) //reset timer if it is still running from previous pull
                            _pullTimer.Start();

                        if ((Me.GotTarget) && (Me.CurrentTarget.Distance > 50)) //Also make sure we are sorta close
                        {
                            Blacklist.Add(Me.CurrentTarget, TimeSpan.FromSeconds(2.00));
                            Me.ClearTarget();
                            _pvPPullSpam = false;
                            _pvPPullTypeSpam = false;
                            return;
                        }

                        /*if (!_me.CurrentTarget.InLineOfSight) //Dont track if the target is not LoS
                        {
                            Slog("#Player at distance " + System.Math.Floor(_me.CurrentTarget.Distance) + " , Is not in Line of Sight.#");
                            Logic.Blacklist.Add(_me.CurrentTarget, TimeSpan.FromSeconds(4.00));
                            _me.ClearTarget();
                            PvPPullSpam = false;
                            PvPPullTypeSpam = false;
                            return;

                        }*/

                        if (_pullTimer.Elapsed.Seconds > 12)
                        {
                            Slog("Error pulling! Took too long! Clear Target.");
                            Me.ClearTarget();
                            _pullTimer.Reset();
                            _pullSpam = false;
                            return;
                        }

                        if (!_pvPPullSpam) //Avoid that annoying spam
                            Slog("#Killing Player , Distance : " + Math.Floor(Me.CurrentTarget.Distance) + " yards.#");

                        //
                        /////////////////////
                        //Opener is Deathgrip
                        /////////////////////
                        //

                        if (PvPOpenWithDeathGrip)
                        {
                            if ((Me.GotTarget) && (Me.CurrentTarget.Distance < 3)) //Too Close for Deathgrip, just start attacking
                            {
                                if (AutoAttack()) //Start Auto Attack
                                    return;
                            }

                            if (!SpellManager.CanCast("Death Grip")) //Death grip is on CD
                            {
                                if (PvPOpenWithIcyTouchBackup) //Use Icy Touch as a backup
                                {
                                    if (!SpellManager.CanCast("Icy Touch")) //Can not cast Icy Touch
                                    {
                                        if (!_pvPPullTypeSpam)
                                            Slog(
                                                "#Opener(s) is on Cooldown. Moving to within 3 yards of currently targeted player for melee attack#");

                                        Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                                          ObjectManager.Me.CurrentTarget.
                                                                                              Location, 2.5f));
                                        _pvPPullSpam = true;
                                        _pvPPullTypeSpam = true;
                                        _moveWithMelee = true;
                                    }

                                    else
                                    {
                                        if (Me.CurrentTarget.Distance >= PvPIcyTouchRange) //Not in range of Icy Touch 
                                        {
                                            if (!_pvPPullTypeSpam)
                                            {
                                                Slog("#Death Grip is on Cooldown, using Icy Touch#");
                                                Slog("#Moving to within " + PvPIcyTouchRange +
                                                     " yards of currently targeted player for Icy Touch#");
                                            }

                                            Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                                              ObjectManager.Me.CurrentTarget
                                                                                                  .Location, 2.5f));
                                            _pvPPullSpam = true;
                                            _pvPPullTypeSpam = true;
                                            _moveWithMelee = false;
                                            return;
                                        }

                                        if (!_pvPPullTypeSpam)
                                            Slog("#Within range of player for Icy Touch#");

                                        _pvPPullTypeSpam = true;
                                        _pvPPullSpam = true;
                                        _moveWithMelee = false;
                                    }
                                }

                                else //Can not Cast Death Grip. No Backup
                                {
                                    if (!_pvPPullTypeSpam)
                                        Slog(
                                            "#Opener(s) is on Cooldown. Moving to within 3 yards of currently targeted player for melee attack#");

                                    Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                                      ObjectManager.Me.CurrentTarget.
                                                                                          Location, 2.5f));
                                    _pvPPullSpam = true;
                                    _pvPPullTypeSpam = true;
                                    _moveWithMelee = true;
                                }
                            }

                            else //Death Grip is ready
                            {
                                if (Me.CurrentTarget.Distance >= 29)
                                {
                                    if (!_pvPPullTypeSpam)
                                        Slog("#Moving to within 29 yards of currently targeted player for Death Grip#");

                                    Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                                      ObjectManager.Me.CurrentTarget.
                                                                                          Location, 2.5f));
                                    _pvPPullSpam = true;
                                    _pvPPullTypeSpam = true;
                                    _moveWithMelee = false;
                                    return;
                                }

                                if (!_pvPPullTypeSpam)
                                    Slog("#Within range of player for Death Grip#");

                                _pvPPullTypeSpam = true;
                                _pvPPullSpam = true;
                                _moveWithMelee = false;
                            }
                        }

                            //
                        /////////////////////
                        //Opener is Icy Touch
                        /////////////////////
                        //

                        else if (PvPOpenWithIcyTouch)
                        {
                            if (Me.CurrentTarget.Distance < 3) //Too Close for Icy Touch, just start attacking
                            {
                                if (AutoAttack()) //Start Auto Attack
                                    return;
                            }

                            if (SpellManager.CanCast("Icy Touch"))
                            {
                                if (Me.CurrentTarget.Distance >= PvPIcyTouchRange)
                                {
                                    if (!_pvPPullTypeSpam)
                                        Slog("#Moving to within " + PvPIcyTouchRange +
                                             " yards of currently targeted player for Icy Touch#");

                                    Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                                      ObjectManager.Me.CurrentTarget.
                                                                                          Location, 2.5f));
                                    _pvPPullSpam = true;
                                    _pvPPullTypeSpam = true;
                                    _moveWithMelee = false;
                                    return;
                                }

                                if (!_pvPPullTypeSpam)
                                    Slog("#Within range of player for Icy Touch#");

                                _pvPPullTypeSpam = true;
                                _pvPPullSpam = true;
                                _moveWithMelee = false;
                            }

                            else
                            {
                                if (!_pvPPullTypeSpam)
                                    Slog(
                                        "#Opener(s) is on Cooldown. Moving to within 3 yards of currently targeted player for melee attack#");

                                Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                                  ObjectManager.Me.CurrentTarget.Location,
                                                                                  2.5f));
                                _pvPPullSpam = true;
                                _pvPPullTypeSpam = true;
                                _moveWithMelee = true;
                            }
                        }

                        else //No opener selected. Move to melee
                        {
                            if (!_pvPPullTypeSpam)
                                Slog(
                                    "#Opener(s) is on Cooldown. Moving to within 3 yards of currently targeted player for melee attack#");

                            _moveWithMelee = true;

                            Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                              ObjectManager.Me.CurrentTarget.Location, 2.5f));
                            _pvPPullSpam = true;
                            _pvPPullTypeSpam = true;
                            _moveWithMelee = true;
                        }


                        ///////////Pull////////////
                        if (!_moveWithMelee)
                        {
                            if (PvPopener()) //Do Opener
                            {
                                Slog("#Opener was successfull!#");
                                _pvPPullSpam = false;
                                _pvPPullTypeSpam = false;
                            }

                            else
                            {
                                Slog("#Opener failed! Try again#");
                                _moveWithMelee = false;
                                WoWMovement.MoveStop();
                                Thread.Sleep(100);
                                WoWMovement.Face();
                                return;
                            }
                        }

                        else //Move to Melee
                        {
                            if (Me.CurrentTarget.Distance < 10) //Dont start auto Attack till we are closer
                            {
                                if (AutoAttack()) //Start Auto Attack
                                    return;
                            }
                        }

                        if (Me.CurrentTarget.Distance < 1)
                        {
                            Slog("#Overshot Player... Stop & Face.#");
                            WoWMovement.MoveStop();
                            Thread.Sleep(125);
                            WoWMovement.Face();
                            Thread.Sleep(125);
                        }
                    }

                    ///////////////////////////////////////////////////////////////////////
                    //Not in BG
                    ///////////////////////////////////////////////////////////////////////

                    else
                    {
                        if (Me.CurrentTarget.TaggedByOther) //HB is too fuckin slow to see the player tagged.
                        {
                            Blacklist.Add(Me.CurrentTarget, TimeSpan.FromSeconds(5.0));
                            Me.ClearTarget();
                            WoWMovement.MoveStop();
                            _pvPPullSpam = false;
                            _pvPPullTypeSpam = false;
                            Slog("#Target is Tagged, Duh#");
                        }

                        if (UseBoneShield) //Cast Boneshield now!
                        {
                            if (!Me.Auras.ContainsKey("Bone Shield"))
                            {
                                if (SpellManager.CanCast("Bone Shield"))
                                {
                                    BoneShield();
                                    return;
                                }
                            }
                        }

                        if (UseHorn) //Cast Horn now!
                        {
                            if (!Me.Auras.ContainsKey("Horn of Winter"))
                            {
                                if (SpellManager.CanCast("Horn of Winter"))
                                {
                                    WoWMovement.MoveStop();
                                    Thread.Sleep(125);
                                    HornOfWinter();
                                    return;
                                }
                            }
                        }

                        if (UseRaiseDead) //Cast Raise Dead now!
                        {
                            if (SpellManager.CanCast("Raise Dead"))
                            {
                                RaiseDead();
                                return;
                            }
                        }

                        if (!_pullTimer.IsRunning) //reset timer if it is still running from previous pull
                            _pullTimer.Start();

                        if (Me.CurrentTarget.Distance > 50) //Also make sure we are sorta close
                        {
                            Blacklist.Add(Me.CurrentTarget, TimeSpan.FromSeconds(2.00));
                            Me.ClearTarget();
                            _pvPPullSpam = false;
                            _pvPPullTypeSpam = false;
                            return;
                        }

                        if (_pullTimer.Elapsed.Seconds > 12)
                        {
                            Slog("Error pulling! Took too long! Clear Target.");
                            Blacklist.Add(Me.CurrentTarget, TimeSpan.FromSeconds(60.00));
                            Me.ClearTarget();
                            _pullTimer.Reset();
                            _pullSpam = false;
                            return;
                        }

                        if (!_pullSpam) //Avoid that annoying spam
                            Slog("#Killing " + Me.CurrentTarget.Name + " Distance : " +
                                 Math.Floor(Me.CurrentTarget.Distance) + " yards.#");

                        //
                        //////////////////////
                        //Opener is Deathgrip 
                        //////////////////////
                        //

                        if (OpenWithDeathGrip)
                        {
                            if (Me.CurrentTarget.Distance < 3) //Too Close for Deathgrip, just start attacking
                            {
                                if (AutoAttack()) //Start Auto Attack
                                    return;
                            }

                            if (!SpellManager.CanCast("Death Grip")) //Death grip is on CD
                            {
                                if (OpenWithIcyTouchBackup) //Use Icy Touch as a backup
                                {
                                    if (!SpellManager.CanCast("Icy Touch")) //Can not cast Icy Touch
                                    {
                                        if (!_pullTypeSpam)
                                            Slog(
                                                "#Opener(s) is on Cooldown. Moving to within 3 yards of current target for melee attack#");

                                        Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                                          ObjectManager.Me.CurrentTarget.
                                                                                              Location, 2.5f));
                                        _pullSpam = true;
                                        _pullTypeSpam = true;
                                        _moveWithMelee = true;
                                    }

                                    else
                                    {
                                        if (Me.CurrentTarget.Distance >= IcyTouchRange) //Not in range of Icy Touch 
                                        {
                                            if (!_pullTypeSpam)
                                            {
                                                Slog("#Death Grip is on Cooldown, using Icy Touch#");
                                                Slog("#Moving to within " + IcyTouchRange +
                                                     " yards of current target for Icy Touch#");
                                            }

                                            Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                                              ObjectManager.Me.CurrentTarget
                                                                                                  .Location, 2.5f));
                                            _pullSpam = true;
                                            _pullTypeSpam = true;
                                            _moveWithMelee = false;
                                            return;
                                        }

                                        if (!_pullTypeSpam)
                                            Slog("#Within range of target for Icy Touch#");

                                        if (!Me.CurrentTarget.InLineOfSight) //Dont track if the target is not LoS
                                        {
                                            Slog("#But not in Line of Sight. Move to LoS#");
                                            Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(
                                                ObjectManager.Me.Location, ObjectManager.Me.CurrentTarget.Location, 2.5f));
                                            _pullSpam = true;
                                            _pullTypeSpam = true;
                                            _moveWithMelee = false;
                                            return;
                                        }

                                        _pullTypeSpam = true;
                                        _pullSpam = true;
                                        _moveWithMelee = false;
                                    }
                                }

                                else //Can not Cast Death Grip. No Backup
                                {
                                    if (!_pullTypeSpam)
                                        Slog(
                                            "#Opener(s) is on Cooldown. Moving to within 3 yards of current target for melee attack#");

                                    Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                                      ObjectManager.Me.CurrentTarget.
                                                                                          Location, 2.5f));
                                    _pullSpam = true;
                                    _pullTypeSpam = true;
                                    _moveWithMelee = true;
                                }
                            }

                            else //Death Grip is ready
                            {
                                if (Me.CurrentTarget.Distance >= 29)
                                {
                                    if (!_pullTypeSpam)
                                        Slog("#Moving to within 29 yards of current target for Death Grip#");

                                    Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                                      ObjectManager.Me.CurrentTarget.
                                                                                          Location, 2.5f));
                                    _pullSpam = true;
                                    _pullTypeSpam = true;
                                    _moveWithMelee = false;
                                    return;
                                }

                                if (!_pullTypeSpam)
                                    Slog("#Within range of target for Death Grip#");

                                if (!Me.CurrentTarget.InLineOfSight) //Dont track if the target is not LoS
                                {
                                    Slog("#But not in Line of Sight. Move to LoS#");
                                    Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                                      ObjectManager.Me.CurrentTarget.
                                                                                          Location, 2.5f));
                                    _pullSpam = true;
                                    _pullTypeSpam = true;
                                    _moveWithMelee = false;
                                    return;
                                }

                                _pullTypeSpam = true;
                                _pullSpam = true;
                                _moveWithMelee = false;
                            }
                        }

                            //
                        //////////////////////
                        //Opener is Dark Command 
                        //////////////////////
                        //

                        else if (OpenWithDarkCommand)
                        {
                            if (Me.CurrentTarget.Distance < 3) //Too Close for Dark Command, just start attacking
                            {
                                if (AutoAttack()) //Start Auto Attack
                                    return;
                            }

                            if (!SpellManager.CanCast("Dark Command")) //Dark Command is on CD
                            {
                                if (OpenWithIcyTouchBackup) //Use Icy Touch as a backup
                                {
                                    if (!SpellManager.CanCast("Icy Touch")) //Can not cast Icy Touch
                                    {
                                        if (!_pullTypeSpam)
                                            Slog(
                                                "#Opener(s) is on Cooldown. Moving to within 3 yards of current target for melee attack#");

                                        Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                                          ObjectManager.Me.CurrentTarget.
                                                                                              Location, 2.5f));
                                        _pullSpam = true;
                                        _pullTypeSpam = true;
                                        _moveWithMelee = true;
                                    }

                                    else
                                    {
                                        if (Me.CurrentTarget.Distance >= IcyTouchRange) //Not in range of Icy Touch 
                                        {
                                            if (!_pullTypeSpam)
                                            {
                                                Slog("#Dark Command is on Cooldown, using Icy Touch#");
                                                Slog("#Moving to within " + IcyTouchRange +
                                                     " yards of current target for Icy Touch#");
                                            }

                                            Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                                              ObjectManager.Me.CurrentTarget
                                                                                                  .Location, 2.5f));
                                            _pullSpam = true;
                                            _pullTypeSpam = true;
                                            _moveWithMelee = false;
                                            return;
                                        }

                                        if (!_pullTypeSpam)
                                            Slog("#Within range of target for Icy Touch#");

                                        if (!Me.CurrentTarget.InLineOfSight) //Dont track if the target is not LoS
                                        {
                                            Slog("#But not in Line of Sight. Move to LoS#");
                                            Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(
                                                ObjectManager.Me.Location, ObjectManager.Me.CurrentTarget.Location, 2.5f));
                                            _pullSpam = true;
                                            _pullTypeSpam = true;
                                            _moveWithMelee = false;
                                            return;
                                        }

                                        _pullTypeSpam = true;
                                        _pullSpam = true;
                                        _moveWithMelee = false;
                                    }
                                }

                                else //Can not Cast Death Grip. No Backup
                                {
                                    if (!_pullTypeSpam)
                                        Slog(
                                            "#Opener(s) is on Cooldown. Moving to within 3 yards of current target for melee attack#");

                                    Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                                      ObjectManager.Me.CurrentTarget.
                                                                                          Location, 2.5f));
                                    _pullSpam = true;
                                    _pullTypeSpam = true;
                                    _moveWithMelee = true;
                                    return;
                                }
                            }

                            else //Dark Command is ready
                            {
                                if (Me.CurrentTarget.Distance >= 29)
                                {
                                    if (!_pullTypeSpam)
                                        Slog("#Moving to within 29 yards of current target for Dark Command#");

                                    Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                                      ObjectManager.Me.CurrentTarget.
                                                                                          Location, 2.5f));
                                    _pullSpam = true;
                                    _pullTypeSpam = true;
                                    _moveWithMelee = false;
                                    return;
                                }

                                if (!_pullTypeSpam)
                                    Slog("#Within range of target for Dark Command#");

                                if (!Me.CurrentTarget.InLineOfSight) //Dont track if the target is not LoS
                                {
                                    Slog("#But not in Line of Sight. Move to LoS#");
                                    Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                                      ObjectManager.Me.CurrentTarget.
                                                                                          Location, 2.5f));
                                    _pullSpam = true;
                                    _pullTypeSpam = true;
                                    _moveWithMelee = false;
                                    return;
                                }

                                _pullTypeSpam = true;
                                _pullSpam = true;
                                _moveWithMelee = false;
                            }
                        }

                            //
                        /////////////////////
                        //Opener is Icy Touch
                        /////////////////////
                        //

                        else if (OpenWithIcyTouch)
                        {
                            if (Me.CurrentTarget.Distance < 3) //Too Close for Icy Touch, just start attacking
                            {
                                if (AutoAttack()) //Start Auto Attack
                                    return;
                            }

                            if (SpellManager.CanCast("Icy Touch"))
                            {
                                if (Me.CurrentTarget.Distance >= IcyTouchRange)
                                {
                                    if (!_pullTypeSpam)
                                        Slog("#Moving to within " + IcyTouchRange +
                                             " yards of current target for Icy Touch#");

                                    Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                                      ObjectManager.Me.CurrentTarget.
                                                                                          Location, 2.5f));
                                    _pullSpam = true;
                                    _pullTypeSpam = true;
                                    _moveWithMelee = false;
                                    return;
                                }

                                if (!_pullTypeSpam)
                                    Slog("#Within range of target for Icy Touch#");

                                if (!Me.CurrentTarget.InLineOfSight) //Dont track if the target is not LoS
                                {
                                    Slog("#But not in Line of Sight. Move to LoS#");
                                    Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                                      ObjectManager.Me.CurrentTarget.
                                                                                          Location, 2.5f));
                                    _pullSpam = true;
                                    _pullTypeSpam = true;
                                    _moveWithMelee = false;
                                    return;
                                }

                                _pullTypeSpam = true;
                                _pullSpam = true;
                                _moveWithMelee = false;
                            }

                            else
                            {
                                if (!_pullTypeSpam)
                                    Slog(
                                        "#Opener(s) is on Cooldown. Moving to within 3 yards of current target for melee attack#");

                                Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                                  ObjectManager.Me.CurrentTarget.Location,
                                                                                  2.5f));
                                _pullSpam = true;
                                _pullTypeSpam = true;
                                _moveWithMelee = true;
                            }
                        }

                        else //No opener selected. Move to melee
                        {
                            if (!_pullTypeSpam)
                                Slog("#Moving to within 3 yards of current target for melee attack#");

                            _moveWithMelee = true;

                            Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                              ObjectManager.Me.CurrentTarget.Location, 2.5f));
                            _pullSpam = true;
                            _pullTypeSpam = true;
                            _moveWithMelee = true;
                        }

                        ///////////Pull////////////
                        if (!_moveWithMelee)
                        {
                            if (Opener()) //Do Opener
                            {
                                Slog("#Opener was successfull!#");
                                _pullSpam = false;
                                _pullTypeSpam = false;
                            }

                            else
                            {
                                Slog("#Opener failed! Try again#");
                                _moveWithMelee = false;
                                WoWMovement.MoveStop();
                                Thread.Sleep(100);
                                WoWMovement.Face();
                                return;
                            }
                        }

                        else //Move to Melee
                        {
                            if (Me.CurrentTarget.Distance < 10) //Dont start auto Attack till we are closer
                            {
                                if (AutoAttack()) //Start Auto Attack
                                    return;
                            }
                        }

                        if (Me.CurrentTarget.Distance < 1)
                        {
                            Slog("#Overshot Target... Stop & Face.#");
                            WoWMovement.MoveStop();
                            Thread.Sleep(125);
                            WoWMovement.Face();
                            Thread.Sleep(125);
                        }
                    }
                }

            }

            ///////////////////////////////////////////////////////////////////////
            //Party Follower
            ///////////////////////////////////////////////////////////////////////

            if ((InParty()) && (!IsLeader()) && ((!InBg()))) //Party Follower, Not BG
            {
                if (Me.GotTarget && !Me.Combat) //We need to make sure our target is valid
                {

                    if (!_pullSpam)
                        Slog("#RaF : Leader is pulling#");

                    _pullSpam = true;
                    _icyTouchCast = false;
                    _plagueStrikeCast = false;
                    _bloodStrikeCast = false;
                    _bloodStrikeCastTwo = false;
                    _heartStrikeCast = false;
                    _heartStrikeCastTwo = false;
                    _obliterateCast = false;
                    _pestilenceCast = false;
                    _bloodBoilCast = false;
                    _deathStrikeCast = false;
                    _deathStrikeCastTwo = false;
                    _scourgeStrikeCast = false;
                    _drwToggle = false;
                    _sgToggle = false;
                    _hcToggle = false;
                    _openerComplete = true;

                    if (UseHorn) //Cast Horn now!
                    {
                        if (!Me.Auras.ContainsKey("Horn of Winter"))
                        {
                            if (SpellManager.CanCast("Horn of Winter"))
                            {
                                HornOfWinter();
                                Thread.Sleep(125);
                                return;
                            }
                        }
                    }

                    if (UseRaiseDead) //Cast Raise Dead now!
                    {
                        if (SpellManager.CanCast("Raise Dead"))
                        {
                            RaiseDead();
                            Thread.Sleep(125);
                            return;
                        }
                    }

                    if (UseBoneShield) //Cast Boneshield now!
                    {
                        if (!Me.Auras.ContainsKey("Bone Shield"))
                        {
                            if (SpellManager.CanCast("Bone Shield"))
                            {
                                BoneShield();
                                return;
                            }
                        }
                    }

                    if (Me.PartyMember1.Distance > num4)
                    {

                        Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                          ObjectManager.Me.PartyMember1.Location,
                                                                          FollowerDistance));
                        return;
                    }

                    if (AutoAttack()) //Start Auto Attack
                        return;

                }
            }
        }

        #endregion

        #region Combat DONE

        public override void Combat()
        {
            _pullSpam = false;

            if (_pulseSlog)
                _pulseSlog = false;

            if (((InParty()) && (IsLeader())) || ((!InParty())) || ((InBg()))) //Solo , Pary Leader, BG
            {

                if (_pullTimer.IsRunning)
                    _pullTimer.Reset(); //If timer is still running, stop it. 

                if (!_openerComplete)
                {
                    Slog("#Opener incomplete - Reset Rotations#");

                    _icyTouchCast = false;
                    _plagueStrikeCast = false;
                    _bloodStrikeCast = false;
                    _bloodStrikeCastTwo = false;
                    _heartStrikeCast = false;
                    _heartStrikeCastTwo = false;
                    _obliterateCast = false;
                    _pestilenceCast = false;
                    _bloodBoilCast = false;
                    _deathStrikeCast = false;
                    _deathStrikeCastTwo = false;
                    _scourgeStrikeCast = false;
                    _drwToggle = false;
                    _sgToggle = false;
                    _hcToggle = false;
                    _openerComplete = true;
                }

                if (!InBg()) //Not in a BG
                {

                    if (TargetErrorCheck())
                    {

                        if (AutoAttack()) //Start Auto Attack
                            return;

                        if (RunCheck())
                            return; //Prevent stupid movment

                        if (Rotation()) //Decide a spell to use
                            return;

                    }

                    if (NullCheck())
                    {
                        if (Me.CurrentTarget.IsPlayer)
                        {
                            if (AutoAttack()) //Start Auto Attack
                                return;

                            WoWMovement.Face();

                            if (PvPRotation()) //Decide a spell to use
                                return;
                        }
                    }

                }

                else //In a battleground
                {
                    if (AutoAttack()) //Start Auto Attack
                        return;

                    WoWMovement.Face();

                    if (PvPRotation()) //Decide a spell to use
                        return;

                }

            }

            if ((InParty()) && (!IsLeader()) && ((!InBg()))) //Party Follower, Not BG
            {

                if (RaFNullCheck()) //Make sure we have a valid target
                {

                    if (AutoAttack()) //Start Auto Attack
                        return;

                    if (RunCheck())
                        return; //Prevent stupid movment

                    if (Rotation()) //Decide a spell to use
                        return;

                }
            }

        }

        #endregion

        #region PvE Logic

        #region PvE Rotation DONE

        /////////////////////////////////
        //PvE In Combat rotation planner
        /////////////////////////////////

        public bool Rotation()
        {
            if (UseStandardRotation) //Level 55 Rotation
            {
                if (StartingRotation())
                    return true;
            }

            if (UseMordBloodRotation) //Level 58 Rotation
            {
                if (MordBlood())
                    return true;
            }

            if (UseMordFrostRotation) //Level 58 Rotation
            {

                if (MordFrost())
                    return true;
            }

            if (UseMordUnholyRotation) //Level 58 Rotation
            {

                if (MordUnholy())
                    return true;
            }

            return false;
        }

        #endregion

        #region PVE Move to Melee DONE

        /////////////////////////////////
        //Checks range to target and moves accordingly
        /////////////////////////////////

        public bool MoveToMelee()
        {
            if (NullCheck())
            {
                if (!Me.CurrentTarget.Fleeing) //We must stay closer if target is Fleeing
                {



                    if ((NullCheck()) && (Me.CurrentTarget.Distance > 4.75) && (!Me.CurrentTarget.Auras.ContainsKey("Death Grip"))) //We need to get in meele range first
                    {
                        //Ranged Instant Spells here



                        if ((ApproachDetector()))
                            return true;

                        Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                          ObjectManager.Me.CurrentTarget.Location, 2.5f));


                        return true;

                    }
                    if (NullCheck())
                    {

                        if (Me.CurrentTarget.Distance < 2.00)
                        {

                            WoWMovement.MoveStop(); //Dont Move!
                            WoWMovement.Face(); //Just in case we arent facing the right direction                
                            return false;
                        }
                    }

                    return false;
                }
            }
            if (NullCheck())
            {
                if ((Me.CurrentTarget.Fleeing) && (Me.CurrentTarget.Distance > 1.75)) //We need to get in meele range first
                {
                    if ((Me.CurrentTarget.Fleeing) && (Me.CurrentTarget.Distance > 1.75)) //We need to get in meele range first
                    {


                        //Ranged Instant Spells here

                        if ((ApproachDetector()))
                            return true;



                        Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                          ObjectManager.Me.CurrentTarget.Location, 1.00f));


                        return true;
                    }

                }
            }

            return false;
        }

        #endregion

        #region PvE Rotation : Level 55 DONE

        /////////////////////////////////
        //Starting Rotation.
        //Assumes no talents
        //Only Checks are Death Coil, Range and facing
        /////////////////////////////////

        public bool StartingRotation()
        {
            if (_icyTouchCast && _plagueStrikeCast && _bloodStrikeCast && _bloodStrikeCastTwo)
            {
                _icyTouchCast = false;
                _plagueStrikeCast = false;
                _bloodStrikeCast = false;
                _bloodStrikeCastTwo = false;
            }

            if ((!_icyTouchCast) && (SpellManager.CanCast("Icy Touch")))
            {
                if (SrChecks())
                    return true;

                IcyTouch();
                _icyTouchCast = true;
                return true;
            }

            if ((!_plagueStrikeCast) && (SpellManager.CanCast("Plague Strike")))
            {
                if (SrChecks())
                    return true;

                PlagueStrike();
                _plagueStrikeCast = true;
                return true;
            }

            if ((!_bloodStrikeCast) && (SpellManager.CanCast("Blood Strike")))
            {
                if (SrChecks())
                    return true;

                BloodStrike();
                _bloodStrikeCast = true;
                return true;
            }

            if ((!_bloodStrikeCastTwo) && (SpellManager.CanCast("Blood Strike")))
            {
                if (SrChecks())
                    return true;

                BloodStrike();
                _bloodStrikeCastTwo = true;
                return true;
            }

            return false;
        }

        public bool SrChecks()
        {
            if (Me.CurrentRunicPower > 40)
            {
                if (SpellManager.CanCast("Death Coil")) //Need 40 RP to cast
                {
                    DeathCoil();
                    return true;
                }
            }

            if (HealReactor()) //Check a heal first
                return true;

            if (MoveToMelee()) //Range Checks, and faces
                return true;

            if (CastReactor())
                return true; //Deal with Casters            

            return false;
        }

        #endregion

        #region PvE Rotation : Mord Blood

        public bool MordBlood()
        {
            if (_icyTouchCast && _plagueStrikeCast && _heartStrikeCast && _heartStrikeCastTwo && _deathStrikeCast &&
                _deathStrikeCastTwo)
            {
                _icyTouchCast = false;
                _plagueStrikeCast = false;
                _heartStrikeCast = false;
                _heartStrikeCastTwo = false;
                _obliterateCast = false;
                _deathStrikeCast = false;
                _deathStrikeCastTwo = false;
            }

            if (_pestilenceCast && _bloodBoilCast)
            {
                _pestilenceCast = false;
                _bloodBoilCast = false;
            }

            if (!_pestilenceCast && _bloodBoilCast)
            {
                _bloodBoilCast = false;
            }

            if (!IgnoreAdds)
                GetAdds(); //Check for adds while in combat            

            if (!_gotAdds)
            {
                _pestilenceCast = false;

                if ((!_icyTouchCast) && (SpellManager.CanCast("Icy Touch")))
                {


                    if (MbChecks())
                        return true;

                    IcyTouch();
                    _icyTouchCast = true;
                    return true;
                }


                if ((!_plagueStrikeCast) && (SpellManager.CanCast("Plague Strike")))
                {


                    if (MbChecks())
                        return true;

                    PlagueStrike();
                    _plagueStrikeCast = true;
                    return true;
                }

                if ((!_deathStrikeCast) && (SpellManager.CanCast("Death Strike")))
                {


                    if (MbChecks())
                        return true;

                    DeathStrike();
                    _deathStrikeCast = true;
                    return true;
                }

                if ((!_heartStrikeCast) && (SpellManager.CanCast("Heart Strike")))
                {


                    if (MbChecks())
                        return true;

                    HeartStrike();
                    _heartStrikeCast = true;
                    return true;
                }


                if ((!_heartStrikeCastTwo) && (SpellManager.CanCast("Heart Strike")))
                {


                    if (MbChecks())
                        return true;

                    HeartStrike();
                    _heartStrikeCastTwo = true;
                    return true;
                }

                if ((!_deathStrikeCastTwo) && (SpellManager.CanCast("Death Strike")))
                {


                    if (MbChecks())
                        return true;

                    DeathStrike();
                    _deathStrikeCastTwo = true;
                    return true;
                }

                return true;
            }

            if (NullCheck())
            {

                if ((SpellManager.CanCast("Icy Touch")) && (!Me.CurrentTarget.Auras.ContainsKey("Frost Fever")))
                {
                    if (MbChecks())
                        return true;

                    IcyTouch();
                    _icyTouchCast = true;
                    return true;
                }
            }

            if (NullCheck())
            {

                if ((SpellManager.CanCast("Plague Strike")) && (!Me.CurrentTarget.Auras.ContainsKey("Blood Plague")))
                {
                    if (MbChecks())
                        return true;

                    PlagueStrike();
                    _plagueStrikeCast = true;
                    return true;
                }
            }

            if ((!_pestilenceCast) && (SpellManager.CanCast("Pestilence")))
            {
                if (MbChecks())
                    return true;

                Pestilence();
                _pestilenceCast = true;
                _heartStrikeCast = true;
                return true;
            }

            if ((_pestilenceCast) && (!_bloodBoilCast) && (SpellManager.CanCast("Blood Boil")))
            {
                if (MbChecks())
                    return true;

                BloodBoil();
                _heartStrikeCastTwo = true;
                _bloodBoilCast = true;
                return true;
            }

            if ((!_deathStrikeCast) && (SpellManager.CanCast("Death Strike")))
            {
                if (MbChecks())
                    return true;

                DeathStrike();
                _deathStrikeCast = true;
                return true;
            }

            if ((!_deathStrikeCastTwo) && (SpellManager.CanCast("Death Strike")))
            {
                if (MbChecks())
                    return true;

                DeathStrike();
                _deathStrikeCastTwo = true;
                return true;
            }

            return true;
        }

        public bool MbChecks()
        {


            if (FleeReactor()) //Is target running?
                return true;

            if (HealReactor()) //Check a heal first
                return true;

            if (MoveToMelee()) //Range Checks, and faces
                return true;

            if (CastReactor())
                return true; //Deal with Casters

            if (RunicPowerReactor()) //Dump RP if we have it
                return true;

            if (CdReactor()) //Check instant abilities and use if needed
                return true;

            //EvadeCheck();

            return false;
        }

        #endregion

        #region PvE Rotation : Mord Frost

        /////////////////////////////////
        //General Frost Spec based Rotation. Requires level 58
        //http://talent.mmo-champion.com/?deathknight#B6_nlOT93KK8yYm,FQp,10482
        //        
        /////////////////////////////////

        public bool MordFrost()
        {
            if (_icyTouchCast && _plagueStrikeCast && _bloodStrikeCast && _bloodStrikeCastTwo && _obliterateCast)
            {
                _icyTouchCast = false;
                _plagueStrikeCast = false;
                _bloodStrikeCast = false;
                _bloodStrikeCastTwo = false;
                _obliterateCast = false;
            }

            if (_hcCast)
            {
                _hcCast = false;
            }

            if (!IgnoreAdds)
                GetAdds(); //Check for adds while in combat            

            if (!_gotAdds)
            {
                _pestilenceCast = false;
                _hcToggle = false;

                if ((!_icyTouchCast) && (SpellManager.CanCast("Icy Touch")))
                {


                    if (MfChecks())
                        return true;

                    if ((UseDeathChill) && (SpellManager.CanCast("Death Chill")))
                        DeathChill();

                    IcyTouch();
                    _icyTouchCast = true;
                    return true;
                }


                if ((!_plagueStrikeCast) && (SpellManager.CanCast("Plague Strike")))
                {


                    if (MfChecks())
                        return true;

                    PlagueStrike();
                    _plagueStrikeCast = true;
                    return true;
                }

                if ((!_obliterateCast) && (SpellManager.CanCast("Obliterate")))
                {


                    if (MfChecks())
                        return true;

                    Obliterate();
                    _obliterateCast = true;
                    return true;
                }

                if ((!_bloodStrikeCast) && (SpellManager.CanCast("Blood Strike")))
                {


                    if (MfChecks())
                        return true;

                    BloodStrike();
                    _bloodStrikeCast = true;
                    return true;
                }

                if ((!_bloodStrikeCastTwo) && (SpellManager.CanCast("Blood Strike")))
                {


                    if (MfChecks())
                        return true;

                    BloodStrike();
                    _bloodStrikeCastTwo = true;
                    return true;
                }

                return true;
            }

            _hcToggle = SpellManager.CanCast("Hungering Cold");

            if (NullCheck())
            {

                if ((SpellManager.CanCast("Icy Touch")) && (!Me.CurrentTarget.Auras.ContainsKey("Frost Fever")))
                {
                    if (MfChecks())
                        return true;

                    if ((UseDeathChill) && (SpellManager.CanCast("Death Chill")))
                        DeathChill();

                    IcyTouch();
                    _icyTouchCast = true;
                    return true;
                }
            }

            if (NullCheck())
            {

                if ((SpellManager.CanCast("Plague Strike")) && (!Me.CurrentTarget.Auras.ContainsKey("Blood Plague")))
                {
                    if (MfChecks())
                        return true;

                    PlagueStrike();
                    _plagueStrikeCast = true;
                    return true;
                }

            }

            if ((!_pestilenceCast) && (SpellManager.CanCast("Pestilence")))
            {
                if (MfChecks())
                    return true;

                Pestilence();
                _pestilenceCast = true;
                _bloodStrikeCast = true;
                return true;
            }

            if ((!UseHowlingBlast) || (!SpellManager.Spells.ContainsKey("Howling Blast")))
            {
                if ((!_obliterateCast) && (SpellManager.CanCast("Obliterate")))
                {
                    if (MfChecks())
                        return true;

                    Obliterate();
                    _obliterateCast = true;
                    return true;
                }
            }

            else if ((UseHowlingBlast) && (SpellManager.CanCast("Howling Blast")))
            {
                if (MfChecks())
                    return true;

                HowlingBlast();
                _obliterateCast = true;
                return true;
            }

            if ((!_bloodStrikeCastTwo) && (SpellManager.CanCast("Blood Strike")))
            {
                if (MfChecks())
                    return true;

                BloodStrike();
                _bloodStrikeCastTwo = true;
                return true;
            }

            return true;
        }

        public bool MfChecks()
        {


            if (FleeReactor()) //Is target running?
                return true;



            if (HealReactor()) //Check a heal first
                return true;

            if (MoveToMelee()) //Range Checks, and faces
                return true;



            if (CastReactor())
                return true; //Deal with Casters



            if (RunicPowerReactor()) //Dump RP if we have it
                return true;



            if (CdReactor()) //Check instant abilities and use if needed
                return true;



            //EvadeCheck();

            return false;
        }

        #endregion

        #region PvE Rotation : Mord Unholy

        /////////////////////////////////
        //General Unholy Spec based Rotation. Requires level 58
        //http://talent.mmo-champion.com/?deathknight#_CJTpDsu3dpyiy,FQp,10482
        //Will not cast Gargoyle till 61
        //
        /////////////////////////////////

        public bool MordUnholy()
        {
            if (_icyTouchCast && _plagueStrikeCast && _bloodStrikeCast && _bloodStrikeCastTwo && _scourgeStrikeCast)
            {
                _icyTouchCast = false;
                _plagueStrikeCast = false;
                _bloodStrikeCast = false;
                _scourgeStrikeCast = false;
                _bloodStrikeCastTwo = false;
            }

            if (!IgnoreAdds)
                GetAdds(); //Check for adds while in combat 

            if (!_gotAdds)
            {
                _pestilenceCast = false;

                if ((!_icyTouchCast) && (SpellManager.CanCast("Icy Touch")))
                {


                    if (MuChecks())
                        return true;

                    IcyTouch();
                    _icyTouchCast = true;
                    return true;
                }


                if ((!_plagueStrikeCast) && (SpellManager.CanCast("Plague Strike")))
                {


                    if (MuChecks())
                        return true;

                    PlagueStrike();
                    _plagueStrikeCast = true;
                    return true;
                }


                if ((!_bloodStrikeCast) && (SpellManager.CanCast("Blood Strike")))
                {


                    if (MuChecks())
                        return true;

                    BloodStrike();
                    _bloodStrikeCast = true;
                    return true;
                }

                if ((!_scourgeStrikeCast) && (SpellManager.CanCast("Scourge Strike")) && (Me.HealthPercent > UseDsssHealth))
                {


                    if (MuChecks())
                        return true;

                    ScourgeStrike();
                    _scourgeStrikeCast = true;
                    return true;
                }

                if ((!_scourgeStrikeCast) && (SpellManager.CanCast("Scourge Strike")) && (Me.HealthPercent <= UseDsssHealth))
                {


                    if (MuChecks())
                        return true;

                    DeathStrike();
                    _scourgeStrikeCast = true;
                    return true;
                }

                if ((!_bloodStrikeCastTwo) && (SpellManager.CanCast("Blood Strike")))
                {


                    if (MuChecks())
                        return true;

                    BloodStrike();
                    _bloodStrikeCastTwo = true;
                    return true;
                }

                return true;
            }

            if (NullCheck())
            {

                if ((SpellManager.CanCast("Icy Touch")) && (!Me.CurrentTarget.Auras.ContainsKey("Frost Fever")))
                {
                    if (MuChecks())
                        return true;

                    IcyTouch();
                    _icyTouchCast = true;
                    return true;
                }
            }

            if (NullCheck())
            {

                if ((SpellManager.CanCast("Plague Strike")) && (!Me.CurrentTarget.Auras.ContainsKey("Blood Plague")))
                {
                    if (MuChecks())
                        return true;

                    PlagueStrike();
                    _plagueStrikeCast = true;
                    return true;
                }
            }

            if ((!_pestilenceCast) && (SpellManager.CanCast("Pestilence")))
            {
                if (MuChecks())
                    return true;

                Pestilence();
                _pestilenceCast = true;
                _bloodStrikeCast = true;
                return true;
            }

            if ((!_scourgeStrikeCast) && (SpellManager.CanCast("Scourge Strike")) && (Me.HealthPercent > UseDsssHealth))
            {
                if (MuChecks())
                    return true;

                ScourgeStrike();
                _scourgeStrikeCast = true;
                return true;
            }

            if ((!_scourgeStrikeCast) && (SpellManager.CanCast("Scourge Strike")) && (Me.HealthPercent <= UseDsssHealth))
            {
                if (MuChecks())
                    return true;

                DeathStrike();
                _scourgeStrikeCast = true;
                return true;
            }

            if ((_pestilenceCast) && (!_bloodBoilCast) && (SpellManager.CanCast("Blood Boil")))
            {
                if (MuChecks())
                    return true;

                BloodBoil();
                _bloodStrikeCastTwo = true;
                _bloodBoilCast = true;
                return true;
            }

            return true;
        }

        public bool MuChecks()
        {

            if (FleeReactor()) //Is target running?
                return true;



            if (HealReactor()) //Check a heal first
                return true;

            if (MoveToMelee()) //Range Checks, and faces
                return true;



            if (CastReactor())
                return true; //Deal with Casters



            if (RunicPowerReactor()) //Dump RP if we have it
                return true;



            if (CdReactor()) //Check instant abilities and use if needed
                return true;




            //EvadeCheck();

            return false;
        }

        #endregion

        #region PvE Cast Reactor DONE

        /////////////////////////////////
        //This will detect casters and deal with them
        /////////////////////////////////

        public bool CastReactor()
        {
            if (NullCheck())
            {
                if (Me.CurrentTarget.IsCasting) //If target starts to cast, move in.
                {
                    Slog("#Target is casting#");

                    if (NullCheck())
                    {

                        if ((Me.CurrentTarget.Distance <= 4) && (Me.CurrentRunicPower > 20))
                        {
                            if ((UseMindFreeze) && (SpellManager.CanCast("Mind Freeze")))
                            //Interupt the cast if MindFreeze is available
                            {
                                MindFreeze();
                                return true;
                            }
                        }
                    }

                    if (NullCheck())
                    {

                        if ((UseStrangulate) && (UseStrangulateMelee) && (SpellManager.CanCast("Strangulate")) && (Me.CurrentTarget.Distance <= 29)) //Strangulate to Interupt Caster
                        {
                            Slog("#Strangulate#");
                            Strangulate();
                            return true;
                        }
                    }

                    if (NullCheck())
                    {

                        if ((UseStrangulate) && (SpellManager.CanCast("Strangulate")) && (Me.CurrentTarget.Distance <= 29) &&
                            (Me.CurrentTarget.Distance > 4)) //Strangulate to Interupt Caster
                        {
                            Slog("#Too far away! Interupt using Strangulate#");
                            Strangulate();
                            return true;
                        }
                    }

                    if (NullCheck())
                    {

                        if ((UseStrangulate) && (UseDeathGripInterupt) && (!SpellManager.CanCast("Strangulate")) &&
                            (SpellManager.CanCast("Death Grip")) && (Me.CurrentTarget.Distance <= 29) &&
                            (Me.CurrentTarget.Distance > 4)) //If St on CD cast DeathGrip
                        {
                            Slog("Too far away! Strangulate not ready! Use Deathgrip.");
                            DeathGrip();
                            return true;
                        }
                    }
                    if (NullCheck())
                    {

                        if ((UseWarStomp) && (WarStompCasters) && (SpellManager.CanCast("War Stomp")) &&
                            (Me.CurrentTarget.Distance <= 8))
                        {
                            WarStomp();
                            return true;
                        }
                    }

                    if (NullCheck())
                    {
                        if ((UseAt) && (SpellManager.CanCast("Arcane Torrent")) && (Me.CurrentTarget.Distance <= 8))
                        {
                            At();
                            return true;
                        }
                    }

                    if ((UseAntiMagicShell) && (SpellManager.CanCast("Anti-Magic Shell")))
                    {
                        Slog("#Interupt is on cooldown, Cast Shell#");
                        AntiMagicShell();
                        return true;
                    }

                    return false;
                }
            }

            return false;
        }

        #endregion

        #region PvE Flee Reactor DONE

        /////////////////////////////////
        //This will detect casters and deal with them
        /////////////////////////////////

        public bool FleeReactor()
        {
            if (NullCheck())
            {
                if (Me.CurrentTarget.Fleeing) //If target starts to cast, move in.
                {
                    Slog("#Target is running#");

                    if (NullCheck())
                    {

                        if (Me.CurrentTarget.Distance <= 29) //If we are not in range, dont bother
                        {
                            if (UseChainsOfIce)
                            {
                                if (NullCheck())
                                {
                                    if (!Me.CurrentTarget.Auras.ContainsKey("Chains of Ice"))
                                    {
                                        if (SpellManager.CanCast("Chains of Ice"))
                                        {
                                            Slog("#Freeze!#");
                                            Chains();
                                            return true;
                                        }
                                    }
                                }
                            }

                            if (UseDeathGripRunners)
                            {
                                if (SpellManager.CanCast("Death Grip"))
                                {
                                    Slog("#Get over here!#");
                                    DeathGrip();
                                    return true;
                                }
                            }

                            if (UseDarkCommandRunners)
                            {
                                if (SpellManager.CanCast("Dark Command")) //Try Dark Command
                                {
                                    Slog("#I, Vigo, the Scourge of Carpathia, the Sorrow of Moldavia, Command You!#");
                                    DarkCommand();
                                    return true;
                                }
                            }
                        }
                    }

                    return false;
                }
            }

            return false;
        }

        #endregion

        #region PvE Heal Reactor DONE

        /////////////////////////////////
        //This will decide if we need a heal
        /////////////////////////////////

        public bool HealReactor()
        {
            if (_gotAdds)
            {
                if (Me.Stunned)
                {
                    if (UseEm)
                    {
                        if (SpellManager.CanCast("Every Man for Himself")) //Get Unstunned
                        {
                            Em();
                            return true;
                        }
                    }
                }

                if (Me.HealthPercent <= RuneTapHealth) //Rune Tap
                {
                    if (UseRuneTap)
                    {
                        if (SpellManager.CanCast("Rune Tap"))
                        {
                            RuneTap();
                            return true;
                        }
                    }
                }

                if (Me.HealthPercent <= DeathPactHealth) //Death Pact
                {
                    if (UseDeathPact)
                    {
                        if (Me.GotAlivePet)
                        {
                            if (SpellManager.CanCast("Death Pact"))
                            {
                                DeathPact();
                                return true;
                            }

                        }
                    }
                }


                if (Me.HealthPercent <= MoBHealth) //Mark of Blood
                {
                    if (UseMoB)
                    {
                        if (NullCheck())
                        {
                            if ((Me.CurrentTarget.CurrentHealth > MinMoBHealth)) //Dont waste it if target is near death
                            {
                                if (SpellManager.CanCast("Mark of Blood"))
                                {
                                    MarkOfBlood();
                                    return true;
                                }
                            }
                        }
                    }
                }

                if (VampiricBloodAdds) //Cast VB when we get adds
                {
                    if (UseVampiricBlood)
                    {
                        if (SpellManager.CanCast("Vampiric Blood"))
                        {
                            VampiricBlood();
                            return true;
                        }
                    }
                }

                if (Me.HealthPercent <= VampiricBloodHealth) //Vampiric Blood 
                {
                    if (UseVampiricBlood)
                    {
                        if (NullCheck())
                        {
                            if (Me.CurrentTarget.CurrentHealth > MinVampiricBloodHealth)
                            //Dont waste it if target is near death
                            {
                                if (SpellManager.CanCast("Vampiric Blood"))
                                {
                                    VampiricBlood();
                                    return true;
                                }
                            }
                        }
                    }
                }

                if (UnbreakableArmorAdds) //Cast VB when we get adds
                {
                    if (UseUnbreakableArmor)
                    {
                        if (SpellManager.CanCast("Unbreakable Armor"))
                        {
                            UnbreakableArmor();
                            return true;
                        }
                    }
                }

                if (Me.HealthPercent <= UnbreakableArmorHealth) //Unbreakable Armor
                {
                    if (UseUnbreakableArmor)
                    {
                        if (NullCheck())
                        {
                            if (Me.CurrentTarget.CurrentHealth > MinUnbreakableArmorHealth)
                            //Dont waste it if target is near death
                            {
                                if (SpellManager.CanCast("Unbreakable Armor"))
                                {
                                    UnbreakableArmor();
                                    return true;
                                }
                            }
                        }
                    }
                }

                if (IceboundFortitudeAdds) //Cast VB when we get adds
                {
                    if (UseIceboundFortitude)
                    {
                        if (SpellManager.CanCast("Icebound Fortitude"))
                        {
                            IceboundFortitude();
                            return true;
                        }
                    }
                }

                if (Me.HealthPercent <= IceboundFortitudeHealth) //Icebound Fortitude
                {
                    if (UseIceboundFortitude)
                    {
                        if (NullCheck())
                        {
                            if (Me.CurrentTarget.CurrentHealth > MinIceboundFortitudeHealth)
                            //Dont waste it if target is near death
                            {
                                if (SpellManager.CanCast("Icebound Fortitude"))
                                {
                                    IceboundFortitude();
                                    return true;
                                }
                            }
                        }
                    }
                }

                if (LifeBloodAdds)
                {
                    if (UseLifeBlood)
                    {
                        if ((SpellManager.CanCast("Lifeblood")))
                        {
                            LifeBlood();
                            return true;
                        }
                    }
                }

                else
                {
                    if (Me.HealthPercent <= LifeBloodHealth)
                    {
                        if (UseLifeBlood)
                        {
                            if (SpellManager.CanCast("Lifeblood")) //Use Lifeblood when low on health
                            {
                                LifeBlood();
                                return true;
                            }
                        }
                    }
                }

                if (BloodFuryAdds)
                {
                    if (UseBloodFury)
                    {
                        if ((SpellManager.CanCast("Blood Fury")))
                        {
                            BloodFury();
                            return true;
                        }
                    }
                }

                else
                {
                    if (Me.HealthPercent <= BloodFuryHealth)
                    {
                        if (UseBloodFury)
                        {
                            if (SpellManager.CanCast("Blood Fury")) //Use Blood Fury when low on health
                            {
                                BloodFury();
                                return true;
                            }
                        }
                    }
                }

                if (NaaruAdds)
                {
                    if (UseNaaru)
                    {
                        if ((SpellManager.CanCast("Gift of the Naaru")))
                        {
                            Naaru();
                            return true;
                        }
                    }
                }

                else
                {
                    if (Me.HealthPercent <= NaaruHealth)
                    {
                        if (UseNaaru)
                        {
                            if (SpellManager.CanCast("Gift of the Naaru")) //Use Naaru when low on health
                            {
                                Naaru();
                                return true;
                            }
                        }
                    }
                }

                if (WarStompAdds)
                {
                    if (UseWarStomp)
                    {
                        if ((SpellManager.CanCast("Warstomp")))
                        {
                            WarStomp();
                            return true;
                        }
                    }
                }

                else
                {
                    if (Me.HealthPercent <= WarStompHealth)
                    {
                        if (UseWarStomp)
                        {
                            if (SpellManager.CanCast("War Stomp"))
                            {
                                if (NullCheck())
                                {
                                    if (Me.CurrentTarget.Distance <= 8) //Use WarStomp when low on health
                                    {
                                        WarStomp();
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }

                if (SfAdds)
                {
                    if (UseStoneForm)
                    {
                        if ((SpellManager.CanCast("Stoneform")))
                        {
                            Sf();
                            return true;
                        }
                    }
                }

                else
                {
                    if (Me.HealthPercent <= StoneFormHealth)
                    {
                        if (UseStoneForm)
                        {
                            if (SpellManager.CanCast("Stoneform")) //Use StoneForm when low on health
                            {
                                Sf();
                                return true;
                            }
                        }
                    }
                }
            }

            else //No adds
            {
                if (Me.Stunned)
                {
                    if (UseEm)
                    {
                        if (SpellManager.CanCast("Every Man for Himself")) //Get Unstunned
                        {
                            Em();
                            return true;
                        }
                    }
                }

                if (Me.HealthPercent <= RuneTapHealth) //Rune Tap
                {
                    if (UseRuneTap)
                    {
                        if (SpellManager.CanCast("Rune Tap"))
                        {
                            RuneTap();
                            return true;
                        }
                    }
                }

                if (Me.HealthPercent <= DeathPactHealth) //Death Pact
                {
                    if (UseDeathPact)
                    {
                        if (Me.GotAlivePet)
                        {
                            if (SpellManager.CanCast("Death Pact"))
                            {
                                DeathPact();
                                return true;
                            }

                        }
                    }
                }

                if (Me.HealthPercent <= MoBHealth) //Mark of Blood
                {
                    if (UseMoB)
                    {
                        if (NullCheck())
                        {
                            if (Me.CurrentTarget.CurrentHealth > MinMoBHealth) //Dont waste it if target is near death
                            {
                                if (SpellManager.CanCast("Mark of Blood"))
                                {
                                    MarkOfBlood();
                                    return true;
                                }
                            }
                        }
                    }
                }

                if (Me.HealthPercent <= VampiricBloodHealth) //Vampiric Blood 
                {
                    if (UseVampiricBlood)
                    {
                        if (NullCheck())
                        {
                            if ((Me.CurrentTarget.CurrentHealth > MinVampiricBloodHealth))
                            //Dont waste it if target is near death
                            {
                                if (SpellManager.CanCast("Vampiric Blood"))
                                {
                                    VampiricBlood();
                                    return true;
                                }
                            }
                        }
                    }
                }

                if (Me.HealthPercent <= UnbreakableArmorHealth) //Unbreakable Armor
                {
                    if (UseUnbreakableArmor)
                    {
                        if (NullCheck())
                        {
                            if (Me.CurrentTarget.CurrentHealth > MinUnbreakableArmorHealth)
                            //Dont waste it if target is near death
                            {
                                if (SpellManager.CanCast("Unbreakable Armor"))
                                {
                                    UnbreakableArmor();
                                    return true;
                                }
                            }

                        }
                    }
                }

                if (Me.HealthPercent <= IceboundFortitudeHealth) //Icebound Fortitude
                {
                    if (UseIceboundFortitude)
                    {
                        if (NullCheck())
                        {
                            if (Me.CurrentTarget.CurrentHealth > MinIceboundFortitudeHealth)
                            //Dont waste it if target is near death
                            {
                                if (SpellManager.CanCast("Icebound Fortitude"))
                                {
                                    IceboundFortitude();
                                    return true;
                                }
                            }
                        }
                    }
                }


                if (Me.HealthPercent <= BloodFuryHealth)
                {
                    if (UseBloodFury)
                    {
                        if (SpellManager.CanCast("Blood Fury")) //Use Blood Fury when low on health
                        {
                            BloodFury();
                            return true;
                        }
                    }
                }


                if (Me.HealthPercent <= LifeBloodHealth)
                {
                    if (UseLifeBlood)
                    {
                        if (SpellManager.CanCast("Lifeblood")) //Use Lifeblood when low on health
                        {
                            LifeBlood();
                            return true;
                        }
                    }
                }

                if (Me.HealthPercent <= NaaruHealth)
                {
                    if (UseNaaru)
                    {
                        if (SpellManager.CanCast("Gift of the Naaru")) //Use Naaru when low on health
                        {
                            Naaru();
                            return true;
                        }
                    }
                }

                if (Me.HealthPercent <= WarStompHealth)
                {
                    if (UseWarStomp)
                    {
                        if (SpellManager.CanCast("War Stomp"))
                        {
                            if (NullCheck())
                            {
                                if (Me.CurrentTarget.Distance <= 8) //Use WarStomp when low on health
                                {
                                    WarStomp();
                                    return true;
                                }
                            }
                        }
                    }
                }


                if (Me.HealthPercent <= StoneFormHealth)
                {
                    if (UseStoneForm)
                    {
                        if (SpellManager.CanCast("Stoneform")) //Use StoneForm when low on health
                        {
                            Sf();
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        #endregion

        #region PvE ApproachDetector DONE

        /////////////////////////////////
        //This will detect if a mob is approaching
        /////////////////////////////////

        public bool ApproachDetector()
        {
            if (NullCheck())
            {
                double aDone = Me.CurrentTarget.Distance;
                Thread.Sleep(125);
                double aDtwo = Me.CurrentTarget.Distance;

                if (aDone > aDtwo) //Difference between the two
                {
                    return true;
                }

                return false;
            }
            return false;
        }

        #endregion

        #region PvE Adds Reactor DONE

        //Credit to Hawker for getAdds()

        private void GetAdds()
        {
            var longList = ObjectManager.ObjectList;

            var mobList = (from thing in longList where (int)thing.Type == 3 select thing.ToUnit()).ToList();

            var enemyMobList = mobList.Where(thing => (thing.Guid != Me.Guid) && (thing.IsTargetingMeOrPet) && (!thing.Name.ToLower().Contains("rotting w"))).ToList();








            if (enemyMobList.Count > 1)
            {
                if (!_addSpam)
                    Slog("#Warning: We have " + enemyMobList.Count + " attackers.#");

                _gotAdds = true;

                _addSpam = true;
            }

            else
            {
                _gotAdds = false;

                _addSpam = false;
            }

            return;
        }
        #endregion

        #region PvE PlayerDetector DONE

        //Credit to Hawker for ScanForPlayers()

        private void ScanForPlayers()
        {

            foreach (var newPlayer in from player in ObjectManager.ObjectList
                                      where player.Type == WoWObjectType.Player && player.Guid != Me.Guid
                                      select player.ToPlayer())
            {
                if (newPlayer.Distance >= PdRange) continue;
                var d = Math.Round(newPlayer.Distance, 1);
                Slog("#Player Named: " + newPlayer.Name + " Is " + d + " yards away.#");
                SystemSounds.Exclamation.Play();
            }
            return;
        }

        #endregion

        #region PvE Opener DONE

        /////////////////////////////////
        //Decide an opener
        /////////////////////////////////

        public bool Opener()
        {
            if ((Me.GotTarget) && (OpenWithDeathGrip) && (SpellManager.CanCast("Death Grip")) && (Me.CurrentTarget.Distance <= 29))
            {
                Slog("#Casting Opener : Death Grip#");
                WoWMovement.MoveStop();
                Thread.Sleep(125);
                WoWMovement.Face();
                Thread.Sleep(125);

                DeathGrip(); //Do Opener

                if (PlayerAlert)
                    ScanForPlayers(); //Find players near

                _openerComplete = true;

                return true;
            }

            if ((Me.GotTarget) && (OpenWithDarkCommand) && (SpellManager.CanCast("Dark Command")) &&
                (Me.CurrentTarget.Distance <= 29))
            {
                Slog("#Casting Opener : Dark Command#");
                WoWMovement.MoveStop();
                Thread.Sleep(125);
                WoWMovement.Face();
                Thread.Sleep(125);

                DarkCommand(); //Do Opener 

                if (PlayerAlert)
                    ScanForPlayers(); //Find players near

                _openerComplete = true;

                return true;
            }


            if ((Me.GotTarget) && (OpenWithIcyTouch) && (SpellManager.CanCast("Icy Touch")) &&
                (Me.CurrentTarget.Distance <= IcyTouchRange))
            {
                Slog("#Casting Opener : Icy Touch#");
                WoWMovement.MoveStop();
                Thread.Sleep(125);
                WoWMovement.Face();
                Thread.Sleep(125);

                IcyTouch(); //Do Opener

                _icyTouchCast = true; //Tell HB not to cast it twice

                if (PlayerAlert)
                    ScanForPlayers(); //Find players near

                _openerComplete = true;

                return true;
            }


            if (((Me.GotTarget) && (OpenWithDeathGrip) && (!SpellManager.CanCast("Death Grip"))) ||
                ((Me.GotTarget) && (OpenWithDarkCommand) && (!SpellManager.CanCast("Death Grip"))) && (OpenWithIcyTouchBackup) &&
                (SpellManager.CanCast("IcyTouch")) && (Me.CurrentTarget.Distance <= IcyTouchRange))
            {
                Slog("#Casting Opener : Icy Touch#");
                WoWMovement.MoveStop();
                Thread.Sleep(125);
                WoWMovement.Face();
                Thread.Sleep(125);

                IcyTouch(); //Do Opener

                _icyTouchCast = true; //Tell HB not to cast it twice

                if (PlayerAlert)
                    ScanForPlayers(); //Find players near

                _openerComplete = true;

                return true;
            }

            return false;
        }

        #endregion

        #region PVE Run Check DONE

        /////////////////////////////////
        //Try to fix HB Movement and targeting Bugs
        /////////////////////////////////

        public bool RunCheck()
        {


            WoWMovement.Face(); //We want to face our target by any means.            

            if (Me.IsMoving) //Make sure we arent running all over the place
            {
                if (NullCheck())
                {
                    if (!Me.CurrentTarget.Fleeing)
                    {
                        if (NullCheck())
                        {
                            if (!Me.CurrentTarget.IsCasting)
                            {
                                if (NullCheck())
                                {
                                    if (Me.CurrentTarget.Distance < 2.0)
                                    {
                                        WoWMovement.MoveStop();

                                        return true;
                                    }
                                }
                            }
                        }
                    }

                }
            }

            return false;

        }

        #endregion

        #region PvE Runic Power Reactor DONE

        /////////////////////////////////
        //This will keep track of runic power and use it
        /////////////////////////////////

        public bool RunicPowerReactor()
        {
            //Dancing Rune Weapon workaround provided by TIA

            if (UseDrw)
            {
                if (SpellManager.Spells.ContainsKey("Dancing Rune Weapon"))
                {
                    if (NullCheck())
                    {
                        if (Me.CurrentTarget.CurrentHealth > DrwThreshold)
                        {
                            WoWSpell spell = SpellManager.Spells["Dancing Rune Weapon"];

                            if (!spell.Cooldown) //We may not have enoph RP to cast. So check if its on CD
                            {
                                if (((DrwAdds) && (_gotAdds)) || (!DrwAdds))
                                {
                                    if (!_drwSpam)
                                        Slog("#Dancing Rune Weapon Ready.... Disable RP Dump#");

                                    _drwToggle = true;

                                    _drwSpam = true;
                                }
                            }
                        }
                    }
                }
            }

            if (UseSummonGargoyle)
            {
                if (SpellManager.Spells.ContainsKey("Summon Gargoyle"))
                {
                    WoWSpell spell = SpellManager.Spells["Summon Gargoyle"];

                    if (!spell.Cooldown) //We may not have enoph RP to cast. So check if its on CD
                    {
                        if (!_sgSpam)
                            Slog("#Summon Gargoyle Ready.... Disable RP Dump#");

                        _sgToggle = true;

                        _sgSpam = true;
                    }
                }
            }

            if (SpellManager.CanCast("Frost Strike"))
            {
                if ((!_hcToggle) && (UseMordFrostRotation)) //Need 40 RP to cast 
                {
                    FrostStrike();
                    return true;
                }
            }

            if (SpellManager.CanCast("Death Coil"))
            {
                if ((!_sgToggle) && (!_drwToggle) && (!UseMordFrostRotation) && (!_hcToggle))
                {
                    DeathCoil();
                    return true;
                }
            }

            if (NullCheck())
            {

                if ((_drwToggle) && (Me.CurrentRunicPower > 60) && (Me.CurrentTarget.CurrentHealth > DrwThreshold))
                //Need 60 RP to cast
                {
                    Drw();
                    return true;
                }
            }
            if (NullCheck())
            {

                if ((_drwToggle) && (Me.CurrentRunicPower > 60) && (Me.CurrentTarget.CurrentHealth < DrwThreshold))
                //Need 60 RP to cast
                {
                    Slog("#Target health is now below threshold for Dancing Rune Weapon. Save it#");

                    _drwToggle = false;

                    return false;
                }
            }

            if ((_hcToggle) && (Me.CurrentRunicPower > 40) && (SpellManager.CanCast("Hungering Cold")))
            //Need 40 RP to cast 
            {
                HungeringCold();
                _hcToggle = false;
                return true;
            }


            if ((_sgToggle) && (Me.CurrentRunicPower > 60) && (SpellManager.CanCast("Summon Gargoyle")))
            //Need 60 RP to cast
            {
                SummonGargoyle();
                _sgToggle = false;
                return true;
            }

            return false;
        }

        #endregion

        #region PvE Cooldown Reactor DONE

        /////////////////////////////////
        //This will detect cooldown abilities and use them
        /////////////////////////////////

        public bool CdReactor() //Change to buff reactor when casting works
        {
            if (UseRime)
            {
                RimeCheck();

                if (_rimeProc) //If Rime is detected, unload Howling Blast
                {
                    HowlingBlast();
                    return true;
                }
            }

            //if ((UseBloodTap) && (_me.GetPowerPercent(WoWPowerType.Health) > BloodTapThreshold))
            //BloodTap();

            if (UseHysteria)
            {
                if (SpellManager.CanCast("Hysteria"))
                {
                    if (Me.HealthPercent >= HysteriaThreshold)
                    {
                        Hysteria();
                        return true;
                    }
                }
            }

            return false;
        }

        #endregion

        #region PvE EvadeCheck

        /////////////////////////////////////
        //Check for evades and blacklist those evades. Credit?
        /////////////////////////////////////

        #endregion

        #region PvE Target Error Check DONE

        public bool TargetErrorCheck()
        {
            if (NullCheck())
            {
                if ((!Me.CurrentTarget.IsPlayer)) //Got a target, now lets check its properties. Skip this if its a player.
                {
                    //If we have a target, but our target is not attacking us or pet. Clear, stop, retarget.

                    if (NullCheck())
                    {
                        if ((!Me.CurrentTarget.Dead) && (Me.CurrentTarget.CurrentTargetGuid != Me.Guid))
                        {
                            if (Me.GotAlivePet) //Have a pet?
                            {
                                if (NullCheck())
                                {
                                    if (Me.CurrentTarget.CurrentTargetGuid != Me.Pet.Guid) //Target isnt targeting our pet
                                    {
                                        if (NullCheck())
                                        {
                                            if (!Me.CurrentTarget.Fleeing)//The target isnt running either, so lets blacklist
                                            {
                                                WoWMovement.MoveStop();
                                                Thread.Sleep(125);
                                                Blacklist.Add(Me.CurrentTarget, TimeSpan.FromSeconds(5.00));
                                                Me.ClearTarget();
                                                return false;
                                            }
                                        }
                                    }
                                }
                            }
                            else //Dont have a pet
                            {
                                if (NullCheck())
                                {
                                    if (!Me.CurrentTarget.Fleeing)//The target isnt running either, so lets blacklist
                                    {
                                        WoWMovement.MoveStop();
                                        Thread.Sleep(125);
                                        Blacklist.Add(Me.CurrentTarget, TimeSpan.FromSeconds(5.00));
                                        Me.ClearTarget();
                                        return false;
                                    }
                                }
                            }
                        }
                    }

                }

            }

            if ((Me.GotTarget) && (Me.CurrentTarget.Dead))
            {
                Me.ClearTarget();
                return false;
            }

            return true;

        }

        #endregion

        #endregion

        #region PvP Logic

        #region PvP Rotation DONE

        /////////////////////////////////
        //PvP In Combat rotation planner
        /////////////////////////////////

        public bool PvPRotation()
        {
            if (PvPUseStandardRotation) //Level 55 Rotation
            {
                if (PvPStartingRotation())
                    return true;
            }

            if (PvPUseMordBloodRotation) //Blood 58 - 61 Rotation
            {
                if (PvPMordBlood())
                    return true;
            }

            if (PvPUseMordFrostRotation) //Frost 58 - 61 Rotation
            {
                if (PvPMordFrost())
                    return true;
            }

            if (PvPUseMordUnholyRotation) //Unholy 58 - 61 Rotation
            {
                if (PvPMordUnholy())
                    return true;
            }

            return false;
        }

        #endregion

        #region PvP Move to Melee DONE

        /////////////////////////////////
        //Checks range to target and moves accordingly
        /////////////////////////////////        

        public bool PvPMoveToMelee()
        {
            if (!Me.CurrentTarget.InLineOfSight)
            {
                LoSCount++;

                if (LoSCount == 2)
                {
                    Slog("#Player went out of LoS for more than 5 seconds. Ignore them for now#");
                    Blacklist.Add(Me.CurrentTarget, TimeSpan.FromSeconds(5.00));
                    Me.ClearTarget();
                    LoSCount = 0;
                    return true;
                }
            }

            else
                LoSCount = 0;

            if (Me.CurrentTarget.Distance > 1.50) //We need to get in meele range first
            {
                WoWMovement.Face();
                //Thread.Sleep(25);
                Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                  ObjectManager.Me.CurrentTarget.Location, 1.25f));
                //WoWMovement.ClickToMove(_me.CurrentTarget.Location, 1.00f);
                //Thread.Sleep(50);

                PvPInstantSpells();

                return true;
            }

            if (Me.CurrentTarget.Distance < 1.00)
            {
                WoWMovement.MoveStop();
                WoWMovement.Face(); //Just in case we arent facing the right direction                
                return false;
            }

            return false;
        }

        #endregion

        #region PvP Rotation : Level 55 DONE

        /////////////////////////////////
        //Starting Rotation.
        //Assumes no talents
        //Only Checks are Death Coil, Range and facing
        /////////////////////////////////

        public bool PvPStartingRotation()
        {
            if (_icyTouchCast && _plagueStrikeCast && _bloodStrikeCast && _bloodStrikeCastTwo)
            {
                _icyTouchCast = false;
                _plagueStrikeCast = false;
                _bloodStrikeCast = false;
                _bloodStrikeCastTwo = false;
            }

            if (!_icyTouchCast)
            {
                if (PvPsrChecks())
                    return true;

                IcyTouch();
                _icyTouchCast = true;
                return true;
            }

            if (!_plagueStrikeCast)
            {
                if (PvPsrChecks())
                    return true;

                PlagueStrike();
                _plagueStrikeCast = true;
                return true;
            }

            if (!_bloodStrikeCast)
            {
                if (PvPsrChecks())
                    return true;

                BloodStrike();
                _bloodStrikeCast = true;
                return true;
            }

            if (!_bloodStrikeCastTwo)
            {
                if (PvPsrChecks())
                    return true;

                BloodStrike();
                _bloodStrikeCastTwo = true;
                return true;
            }

            return false;
        }

        public bool PvPsrChecks()
        {
            if (PvPHealReactor()) //Check a heal first
                return true;

            if (PvPMoveToMelee()) //Range Checks, and faces
                return true;

            if (PvPCastReactor())
                return true; //Deal with Casters

            if (PvPRunicPowerReactor()) //Dump RP if we have it
                return true;

            return false;
        }

        #endregion

        #region PvP Rotation : Mord Blood

        /////////////////////////////////
        //Mord Blood Spec based Rotation. Requires level 58 (Will use Obliterate at 61)
        //http://talent.mmo-champion.com/?deathknight#B6_nlQtnrjLKLc,aQO3eO,10192                
        /////////////////////////////////

        public bool PvPMordBlood()
        {
            if (_icyTouchCast && _plagueStrikeCast && _heartStrikeCast && _heartStrikeCastTwo && _deathStrikeCast &&
                _deathStrikeCastTwo)
            {
                _icyTouchCast = false;
                _plagueStrikeCast = false;
                _heartStrikeCast = false;
                _heartStrikeCastTwo = false;
                _deathStrikeCast = false;
                _deathStrikeCastTwo = false;
            }

            if ((PvPcoI) && (!Me.CurrentTarget.Auras.ContainsKey("Chains of Ice")))//Use Chains of Ice Instead of Icy Touch
            {
                if ((!_icyTouchCast) && (SpellManager.CanCast("Chains of Ice")))
                {
                    if (PvPmbChecks())
                        return true;

                    Chains();
                    _icyTouchCast = true;
                    return true;
                }
            }

            else
            {
                if ((!_icyTouchCast) && (SpellManager.CanCast("Icy Touch")))
                {
                    if (PvPmbChecks())
                        return true;

                    IcyTouch();
                    _icyTouchCast = true;
                    return true;

                }
            }

            if ((!_plagueStrikeCast) && (SpellManager.CanCast("Plague Strike")))
            {
                if (PvPmbChecks())
                    return true;

                PlagueStrike();
                _plagueStrikeCast = true;
                return true;
            }

            if ((!_deathStrikeCast) && (SpellManager.CanCast("Death Strike")))
            {
                if (PvPmbChecks())
                    return true;

                DeathStrike();
                _deathStrikeCast = true;
                return true;
            }

            if ((!_heartStrikeCast) && (SpellManager.CanCast("Heart Strike")))
            {
                if (PvPmbChecks())
                    return true;

                HeartStrike();
                _heartStrikeCast = true;
                return true;
            }

            if ((!_heartStrikeCastTwo) && (SpellManager.CanCast("Heart Strike")))
            {
                if (PvPmbChecks())
                    return true;

                HeartStrike();
                _heartStrikeCastTwo = true;
                return true;
            }

            if ((!_deathStrikeCastTwo) && (SpellManager.CanCast("Death Strike")))
            {
                if (PvPmbChecks())
                    return true;

                DeathStrike();
                _deathStrikeCastTwo = true;
                return true;
            }

            return true;
        }

        public bool PvPmbChecks()
        {
            //WoWMovement.Face();            

            if (PvPHealReactor()) //Check a heal first
                return true;

            if (PvPMoveToMelee()) //Range Checks, and faces
                return true;

            if (PvPCastReactor())
                return true; //Deal with Casters

            if (PvPRunicPowerReactor()) //Dump RP if we have it
                return true;

            if (PvPcdReactor()) //Check instant abilities and use if needed
                return true;

            return false;
        }

        #endregion

        #region PvP Rotation : Mord Frost

        /////////////////////////////////
        //General Frost Spec based Rotation. Requires level 58
        //http://talent.mmo-champion.com/?deathknight#B6_nlOT93KK8yYm,FQp,10482
        //        
        /////////////////////////////////

        public bool PvPMordFrost()
        {
            if (_icyTouchCast && _plagueStrikeCast && _bloodStrikeCast && _bloodStrikeCastTwo && _obliterateCast)
            {
                _icyTouchCast = false;
                _plagueStrikeCast = false;
                _bloodStrikeCast = false;
                _bloodStrikeCastTwo = false;
                _obliterateCast = false;
            }

            if ((PvPcoI) && (!Me.CurrentTarget.Auras.ContainsKey("Chains of Ice")))//Use Chains of Ice Instead of Icy Touch
            {
                if ((!_icyTouchCast) && (SpellManager.CanCast("Chains of Ice")))
                {
                    if (PvPmfChecks())
                        return true;

                    Chains();
                    _icyTouchCast = true;
                    return true;
                }
            }

            else
            {
                if ((!_icyTouchCast) && (SpellManager.CanCast("Icy Touch")))
                {
                    if (PvPmfChecks())
                        return true;

                    if ((PvPUseDeathChill) && (SpellManager.CanCast("Death Chill")))
                        DeathChill();

                    IcyTouch();
                    _icyTouchCast = true;
                    return true;

                }
            }

            if ((!_plagueStrikeCast) && (SpellManager.CanCast("Plague Strike")))
            {
                if (PvPmfChecks())
                    return true;

                PlagueStrike();
                _plagueStrikeCast = true;
                return true;
            }

            if ((!_obliterateCast) && (SpellManager.CanCast("Obliterate")))
            {
                if (PvPmfChecks())
                    return true;

                Obliterate();
                _obliterateCast = true;
                return true;
            }

            if ((!_bloodStrikeCast) && (SpellManager.CanCast("Blood Strike")))
            {
                if (PvPmfChecks())
                    return true;

                BloodStrike();
                _bloodStrikeCast = true;
                return true;
            }

            if ((!_bloodStrikeCastTwo) && (SpellManager.CanCast("Blood Strike")))
            {
                if (PvPmfChecks())
                    return true;

                BloodStrike();
                _bloodStrikeCastTwo = true;
                return true;
            }

            return true;
        }

        public bool PvPmfChecks()
        {
            //WoWMovement.Face();            

            if (PvPHealReactor()) //Check a heal first
                return true;

            if (PvPMoveToMelee()) //Range Checks, and faces
                return true;

            if (PvPCastReactor())
                return true; //Deal with Casters

            if (PvPRunicPowerReactor()) //Dump RP if we have it
                return true;

            if (PvPcdReactor()) //Check instant abilities and use if needed
                return true;

            return false;
        }

        #endregion

        #region PvP Rotation : Mord Unholy

        /////////////////////////////////
        //General Unholy Spec based Rotation. Requires level 58
        //http://talent.mmo-champion.com/?deathknight#_CJTpDsu3dpyiy,FQp,10482
        //Will not cast Gargoyle till 61
        //
        /////////////////////////////////

        public bool PvPMordUnholy()
        {
            if (_icyTouchCast && _plagueStrikeCast && _bloodStrikeCast && _bloodStrikeCastTwo && _scourgeStrikeCast)
            {
                _icyTouchCast = false;
                _plagueStrikeCast = false;
                _bloodStrikeCast = false;
                _scourgeStrikeCast = false;
                _bloodStrikeCastTwo = false;
            }

            if ((PvPcoI) && (!Me.CurrentTarget.Auras.ContainsKey("Chains of Ice")))//Use Chains of Ice Instead of Icy Touch
            {
                if ((!_icyTouchCast) && (SpellManager.CanCast("Chains of Ice")))
                {
                    if (PvPmuChecks())
                        return true;

                    Chains();
                    _icyTouchCast = true;
                    return true;
                }
            }

            else
            {
                if ((!_icyTouchCast) && (SpellManager.CanCast("Icy Touch")))
                {
                    if (PvPmuChecks())
                        return true;

                    IcyTouch();
                    _icyTouchCast = true;
                    return true;

                }
            }


            if ((!_plagueStrikeCast) && (SpellManager.CanCast("Plague Strike")))
            {
                if (PvPmuChecks())
                    return true;

                PlagueStrike();
                _plagueStrikeCast = true;
                return true;
            }

            if ((!_bloodStrikeCast) && (SpellManager.CanCast("Blood Strike")))
            {
                if (PvPmuChecks())
                    return true;

                BloodStrike();
                _bloodStrikeCast = true;
                return true;
            }

            if ((!_scourgeStrikeCast) && (SpellManager.CanCast("Scourge Strike")) && (Me.HealthPercent > PvPUseDsssHealth))
            {
                if (PvPmuChecks())
                    return true;

                ScourgeStrike();
                _scourgeStrikeCast = true;
                return true;
            }

            if ((!_scourgeStrikeCast) && (SpellManager.CanCast("Scourge Strike")) && (Me.HealthPercent <= PvPUseDsssHealth))
            {


                if (PvPmuChecks())
                    return true;

                DeathStrike();
                _scourgeStrikeCast = true;
                return true;
            }

            if ((!_bloodStrikeCastTwo) && (SpellManager.CanCast("Blood Strike")))
            {
                if (PvPmuChecks())
                    return true;

                BloodStrike();
                _bloodStrikeCastTwo = true;
                return true;
            }

            return true;
        }

        public bool PvPmuChecks()
        {
            WoWMovement.Face();

            if ((Me.Stunned) && (UseEm) && (SpellManager.CanCast("Every Man for Himself"))) //Get Unstunned
            {
                Em();
                return true;
            }

            if (PvPHealReactor()) //Check a heal first
                return true;

            if (PvPMoveToMelee()) //Range Checks, and faces
                return true;

            if (PvPCastReactor())
                return true; //Deal with Casters

            if (PvPRunicPowerReactor()) //Dump RP if we have it
                return true;

            if (PvPcdReactor()) //Check instant abilities and use if needed
                return true;

            //EvadeCheck();

            return false;
        }

        #endregion

        #region PvP Cast Reactor DONE

        /////////////////////////////////
        //This will detect casters and deal with them
        /////////////////////////////////

        public bool PvPCastReactor()
        {
            if (Me.CurrentTarget.IsCasting) //If target starts to cast, move in.
            {
                Slog("#Target is casting#");

                if ((Me.CurrentTarget.Distance <= 4) && (Me.CurrentRunicPower > 20))
                {
                    if ((PvPUseMindFreeze) && (SpellManager.CanCast("Mind Freeze")))
                    //Interupt the cast if MindFreeze is available
                    {
                        MindFreeze();
                        return true;
                    }
                }

                if ((PvPUseStrangulate) && (PvPUseStrangulateMelee) && (SpellManager.CanCast("Strangulate")) &&
                    (Me.CurrentTarget.Distance <= 29))
                //Strangulate to Interupt Caster
                {
                    Slog("#Strangulate#");
                    Strangulate();
                    return true;
                }

                if ((PvPUseStrangulate) && (!PvPUseStrangulateMelee) && (SpellManager.CanCast("Strangulate")) &&
                    (Me.CurrentTarget.Distance <= 29) && (Me.CurrentTarget.Distance > 4))
                //Strangulate to Interupt Caster
                {
                    Slog("#Too far away! Interupt using Strangulate#");
                    Strangulate();
                    return true;
                }

                if ((PvPUseStrangulate) && (PvPUseDeathGripInterupt) && (!SpellManager.CanCast("Strangulate")) &&
                    (SpellManager.CanCast("Death Grip")) && (Me.CurrentTarget.Distance <= 29) &&
                    (Me.CurrentTarget.Distance > 4)) //If St on CD cast DeathGrip
                {
                    Slog("Too far away! Strangulate not ready! Use Deathgrip.");
                    DeathGrip();
                    return true;
                }

                if ((PvPUseWarStomp) && (WarStompCasters) && (SpellManager.CanCast("War Stomp")) &&
                    (Me.CurrentTarget.Distance <= 8))
                {
                    WarStomp();
                    return true;
                }

                if ((PvPUseAt) && (SpellManager.CanCast("Arcane Torrent")) && (Me.CurrentTarget.Distance <= 8))
                {
                    At();
                    return true;
                }

                if ((PvPUseAntiMagicShell) && (SpellManager.CanCast("Anti-Magic Shell")))
                {
                    Slog("#Interupt is on cooldown, Cast Shell#");
                    AntiMagicShell();
                    return true;
                }

                return false;
            }

            return false;
        }

        #endregion

        #region PvP Heal Reactor DONE

        /////////////////////////////////
        //This will decide if we need a heal
        /////////////////////////////////

        public bool PvPHealReactor()
        {
            if (Me.Stunned)
            {
                if (PvPUseEm)
                {
                    if (SpellManager.CanCast("Every Man for Himself")) //Get Unstunned
                    {
                        Em();
                        return true;
                    }
                }
            }

            if (Me.HealthPercent <= PvPRuneTapHealth) //Rune Tap
            {
                if (PvPUseRuneTap)
                {
                    if (SpellManager.CanCast("Rune Tap"))
                    {
                        RuneTap();
                        return true;
                    }
                }
            }

            if (Me.HealthPercent <= PvPDeathPactHealth) //Death Pact
            {
                if (PvPUseDeathPact)
                {
                    if (Me.GotAlivePet)
                    {
                        if (SpellManager.CanCast("Death Pact"))
                        {
                            DeathPact();
                            return true;
                        }

                    }
                }
            }

            if (Me.HealthPercent <= PvPmoBHealth) //Mark of Blood
            {
                if (PvPUseMoB)
                {
                    if (SpellManager.CanCast("Mark of Blood"))
                    {
                        MarkOfBlood();
                        return true;
                    }
                }
            }

            if (Me.HealthPercent <= PvPVampiricBloodHealth) //Vampiric Blood 
            {
                if (PvPUseVampiricBlood)
                {
                    if (SpellManager.CanCast("Vampiric Blood"))
                    {
                        VampiricBlood();
                        return true;
                    }
                }
            }

            if (Me.HealthPercent <= PvPUnbreakableArmorHealth) //Unbreakable Armor
            {
                if (PvPUseUnbreakableArmor)
                {
                    if (SpellManager.CanCast("Unbreakable Armor"))
                    {
                        UnbreakableArmor();
                        return true;
                    }
                }
            }

            if (Me.HealthPercent <= PvPIceboundFortitudeHealth) //Icebound Fortitude
            {
                if (PvPUseIceboundFortitude)
                {
                    if (SpellManager.CanCast("Icebound Fortitude"))
                    {
                        IceboundFortitude();
                        return true;
                    }
                }
            }

            if (Me.HealthPercent <= PvPBloodFuryHealth)
            {
                if (PvPUseBloodFury)
                {
                    if (SpellManager.CanCast("Blood Fury")) //Use Blood Fury when low on health
                    {
                        BloodFury();
                        return true;
                    }
                }
            }


            if (Me.HealthPercent <= PvPLifeBloodHealth)
            {
                if (PvPUseLifeBlood)
                {
                    if (SpellManager.CanCast("Lifeblood")) //Use Lifeblood when low on health
                    {
                        LifeBlood();
                        return true;
                    }
                }
            }

            if (Me.HealthPercent <= PvPNaaruHealth)
            {
                if (PvPUseNaaru)
                {
                    if (SpellManager.CanCast("Gift of the Naaru")) //Use Naaru when low on health
                    {
                        Naaru();
                        return true;
                    }
                }
            }

            if (Me.HealthPercent <= PvPWarStompHealth)
            {
                if (PvPUseWarStomp)
                {
                    if (SpellManager.CanCast("War Stomp"))
                    {
                        if (Me.CurrentTarget.Distance <= 8) //Use WarStomp when low on health
                        {
                            WarStomp();
                            return true;
                        }
                    }
                }
            }


            if (Me.HealthPercent <= PvPStoneFormHealth)
            {
                if (PvPUseStoneForm)
                {
                    if (SpellManager.CanCast("Stoneform")) //Use StoneForm when low on health
                    {
                        Sf();
                        return true;
                    }
                }
            }

            return false;
        }

        #endregion

        #region PvP Opener DONE

        /////////////////////////////////
        //Decide an opener
        /////////////////////////////////

        public bool PvPopener()
        {
            if ((PvPOpenWithDeathGrip) && (SpellManager.Spells.ContainsKey("Death Grip")) &&
                (SpellManager.CanCast("Death Grip")) && (Me.CurrentTarget.Distance <= 29))
            {
                Slog("#Casting Opener : Death Grip#");
                WoWMovement.MoveStop();
                Thread.Sleep(125);
                WoWMovement.Face();
                Thread.Sleep(125);

                DeathGrip(); //Do Opener               

                return true;
            }


            if ((PvPOpenWithIcyTouch) && (SpellManager.Spells.ContainsKey("Icy Touch")) &&
                (SpellManager.CanCast("Icy Touch")) && (Me.CurrentTarget.Distance <= PvPIcyTouchRange))
            {
                Slog("#Casting Opener : Icy Touch#");
                WoWMovement.MoveStop();
                Thread.Sleep(125);
                WoWMovement.Face();
                Thread.Sleep(125);

                IcyTouch(); //Do Opener
                _icyTouchCast = true;

                return true;
            }


            if ((PvPOpenWithDeathGrip) && (!SpellManager.CanCast("Death Grip")) && (PvPOpenWithIcyTouchBackup) &&
                (SpellManager.CanCast("IcyTouch")) && (Me.CurrentTarget.Distance <= PvPIcyTouchRange))
            {
                Slog("#Casting Opener : Icy Touch#");
                WoWMovement.MoveStop();
                Thread.Sleep(125);
                WoWMovement.Face();
                Thread.Sleep(125);

                IcyTouch(); //Do Opener 
                _icyTouchCast = true;

                return true;
            }

            return false;
        }

        #endregion

        #region PvP Runic Power Reactor

        /////////////////////////////////
        //This will keep track of runic power and use it
        /////////////////////////////////

        public bool PvPRunicPowerReactor()
        {
            //Dancing Rune Weapon workaround provided by TIA

            if ((PvPUseDrw) && (SpellManager.Spells.ContainsKey("Dancing Rune Weapon")))
            {
                WoWSpell spell = SpellManager.Spells["Dancing Rune Weapon"];

                if (!spell.Cooldown) //We may not have enoph RP to cast. So check if its on CD
                {
                    if (!_drwSpam)
                        Slog("#Dancing Rune Weapon Ready.... Disable RP Dump#");

                    _drwToggle = true;

                    _drwSpam = true;
                }
            }

            if (SpellManager.Spells.ContainsKey("Summon Gargoyle") && PvPUseSummonGargoyle)
            {
                WoWSpell spell = SpellManager.Spells["Summon Gargoyle"];

                if (!spell.Cooldown) //We may not have enoph RP to cast. So check if its on CD
                {
                    if (!_sgSpam)
                        Slog("#Summon Gargoyle Ready.... Disable RP Dump#");

                    _sgToggle = true;

                    _sgSpam = true;
                }
            }

            if ((!_sgToggle) && (!_drwToggle) && (!PvPUseMordFrostRotation) && (!_hcToggle) && (Me.CurrentRunicPower > 40) &&
                (SpellManager.CanCast("Death Coil"))) //Need 40 RP to cast
            {
                DeathCoil();
                return true;
            }

            if ((Me.CurrentRunicPower > 40) && (SpellManager.CanCast("Frost Strike")) && (PvPUseMordFrostRotation))
            //Need 40 RP to cast 
            {
                FrostStrike();
                return true;
            }

            if ((_drwToggle) && (Me.CurrentRunicPower > 60)) //Need 60 RP to cast
            {
                Drw();
                return true;
            }

            if ((_sgToggle) && (Me.CurrentRunicPower > 60) && (SpellManager.CanCast("Summon Gargoyole")))
            //Need 60 RP to cast
            {
                SummonGargoyle();
                _sgToggle = false;
                return true;
            }

            return false;
        }

        #endregion

        #region PvP Cooldown Reactor

        /////////////////////////////////
        //This will detect cooldown abilities and use them
        /////////////////////////////////

        public bool PvPcdReactor() //Change to buff reactor when casting works
        {
            if (PvPUseRime)
            {
                RimeCheck();

                if (_rimeProc) //If Rime is detected, unload Howling Blast
                {
                    HowlingBlast();
                    return true;
                }
            }

            //if ((UseBloodTap) && (_me.GetPowerPercent(WoWPowerType.Health) > BloodTapThreshold))
            //BloodTap();

            if (PvPUseHysteria)
            {
                if (SpellManager.CanCast("Hysteria"))
                {
                    Hysteria();
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region PvP Instant Spells DONE

        /////////////////////////////////
        //This will cast instant spells while moving
        /////////////////////////////////

        public void PvPInstantSpells()
        {
            if ((PvPUseNaaru) && (Me.HealthPercent <= PvPNaaruHealth) &&
                (SpellManager.CanCast("Gift of the Naaru"))) //Use Naaru when low on health
            {
                Naaru();
            }

            if ((PvPUseStoneForm) && (Me.HealthPercent <= PvPStoneFormHealth) &&
                (SpellManager.CanCast("Stoneform"))) //Use Stoneform when low on health
            {
                Sf();
            }

            if ((Me.CurrentRunicPower > 40) && (!_drwToggle)) //Need 40 RP to cast
                DeathCoil();

            if (PvPUseStandardRotation)
            {
                if (_icyTouchCast && _plagueStrikeCast && _bloodStrikeCast && _bloodStrikeCastTwo)
                {
                    _icyTouchCast = false;
                    _plagueStrikeCast = false;
                    _bloodStrikeCast = false;
                    _bloodStrikeCastTwo = false;
                }

                if (!_icyTouchCast)
                {
                    IcyTouch();
                    _icyTouchCast = true;
                }

                if (!_plagueStrikeCast)
                {
                    PlagueStrike();
                    _plagueStrikeCast = true;
                }

                if (!_bloodStrikeCast)
                {
                    BloodStrike();
                    _bloodStrikeCast = true;
                }

                if (!_bloodStrikeCastTwo)
                {
                    BloodStrike();
                    _bloodStrikeCastTwo = true;
                }
            }

            if (PvPUseMordBloodRotation)
            {
                if (_icyTouchCast && _plagueStrikeCast && _heartStrikeCast && _heartStrikeCastTwo && _deathStrikeCast &&
                    _deathStrikeCastTwo)
                {
                    _icyTouchCast = false;
                    _plagueStrikeCast = false;
                    _heartStrikeCast = false;
                    _heartStrikeCastTwo = false;
                    _deathStrikeCast = false;
                    _deathStrikeCastTwo = false;
                }

                if ((!_icyTouchCast) && (SpellManager.CanCast("Icy Touch")) &&
                    (Me.CurrentTarget.Distance < PvPIcyTouchRange))
                {
                    IcyTouch();
                    _icyTouchCast = true;
                }


                if ((!_plagueStrikeCast) && (SpellManager.CanCast("Plague Strike")) &&
                    (Me.CurrentTarget.Distance < 4))
                {
                    PlagueStrike();
                    _plagueStrikeCast = true;
                }

                else if ((!_deathStrikeCast) && (SpellManager.CanCast("Death Strike")) &&
                         (Me.CurrentTarget.Distance < 4))
                {
                    DeathStrike();
                    _deathStrikeCast = true;
                }


                if ((!_heartStrikeCast) && (SpellManager.CanCast("Heart Strike")) &&
                    (Me.CurrentTarget.Distance < 4))
                {
                    HeartStrike();
                    _heartStrikeCast = true;
                }


                if ((!_heartStrikeCastTwo) && (SpellManager.CanCast("Heart Strike")) &&
                    (Me.CurrentTarget.Distance < 4))
                {
                    HeartStrike();
                    _heartStrikeCastTwo = true;
                }

                else if ((!_deathStrikeCastTwo) && (SpellManager.CanCast("Death Strike")) &&
                         (Me.CurrentTarget.Distance < 4))
                {
                    DeathStrike();
                    _deathStrikeCastTwo = true;
                }
            }

            if (PvPUseMordFrostRotation)
            {
                if ((!_icyTouchCast) && (SpellManager.CanCast("Icy Touch")) &&
                    (Me.CurrentTarget.Distance < PvPIcyTouchRange))
                {
                    if ((UseDeathChill) && (SpellManager.CanCast("Death Chill")))
                        DeathChill();

                    IcyTouch();
                    _icyTouchCast = true;
                }

                if ((!_plagueStrikeCast) && (SpellManager.CanCast("Plague Strike")) &&
                    (Me.CurrentTarget.Distance < 4))
                {
                    PlagueStrike();
                    _plagueStrikeCast = true;
                }

                if ((!_obliterateCast) && (SpellManager.CanCast("Obliterate")) && (Me.CurrentTarget.Distance < 4))
                {
                    Obliterate();
                    _obliterateCast = true;
                }

                if ((!_bloodStrikeCast) && (SpellManager.CanCast("Blood Strike")) &&
                    (Me.CurrentTarget.Distance < 4))
                {
                    BloodStrike();
                    _bloodStrikeCast = true;
                }

                if ((!_bloodStrikeCastTwo) && (SpellManager.CanCast("Blood Strike")) &&
                    (Me.CurrentTarget.Distance < 4))
                {
                    BloodStrike();
                    _bloodStrikeCastTwo = true;
                }
            }

            if (PvPUseMordUnholyRotation)
            {
                if ((!_icyTouchCast) && (SpellManager.CanCast("Icy Touch")) &&
                    (Me.CurrentTarget.Distance < PvPIcyTouchRange))
                {
                    IcyTouch();
                    _icyTouchCast = true;
                }

                if ((!_plagueStrikeCast) && (SpellManager.CanCast("Plague Strike")) &&
                    (Me.CurrentTarget.Distance < 4))
                {
                    PlagueStrike();
                    _plagueStrikeCast = true;
                }

                if ((!_bloodStrikeCast) && (SpellManager.CanCast("Blood Strike")) &&
                    (Me.CurrentTarget.Distance < 4))
                {
                    BloodStrike();
                    _bloodStrikeCast = true;
                }

                if ((!_scourgeStrikeCast) && (SpellManager.CanCast("Scourge Strike")) &&
                    (Me.CurrentTarget.Distance < 4))
                {
                    BloodStrike();
                    _bloodStrikeCast = true;
                }

                if ((!_bloodStrikeCastTwo) && (SpellManager.CanCast("Blood Strike")) &&
                    (Me.CurrentTarget.Distance < 4))
                {
                    BloodStrike();
                    _bloodStrikeCastTwo = true;
                }
            }
        }

        #endregion

        #endregion

        #region RaF Logic

        #region RaF Null Check DONE

        public bool RaFNullCheck()
        {
            if (!Me.GotTarget) //Ok we dont have a target so let check a few things
            {
                if (RaFHelper.Leader.GotTarget) //Does the leader have a target?
                {
                    RaFHelper.Leader.CurrentTarget.Target();

                    return true;
                }

                return false;

            }

            if ((Me.GotTarget) && (Me.CurrentTarget.Dead))
            {
                Me.ClearTarget();
                return false;
            }

            if ((Me.CurrentTarget.Guid != RaFHelper.Leader.CurrentTarget.Guid) && (!Me.CurrentTarget.Fleeing) && (!Me.CurrentTarget.Dead))
            {

                RaFHelper.Leader.CurrentTarget.Target();
                return true;

            }
            return true;

        }

        #endregion

        #endregion

        #region Global Logic

        #region Global Need Rest DONE

        /////////////////////////////////
        //This pulses 8 times per second while out of combat
        /////////////////////////////////

        public override bool NeedRest
        {
            get
            {
                if (_pullTimer.IsRunning)
                    _pullTimer.Reset(); //If timer is still running, stop it.

                if ((!Me.Mounted) && (InBg()))
                {
                    if (PvPUseHorn) //Cast Horn now!
                    {
                        if (!Me.Auras.ContainsKey("Horn of Winter"))
                        {
                            if (SpellManager.CanCast("Horn of Winter"))
                            {
                                HornOfWinter();
                                Thread.Sleep(125);

                            }
                        }
                    }

                    if (PvPUseBoneShield) //Cast Boneshield now!
                    {
                        if (!Me.Auras.ContainsKey("Bone Shield"))
                        {
                            if (SpellManager.CanCast("Bone Shield"))
                            {
                                BoneShield();
                                Thread.Sleep(125);
                            }
                        }
                    }

                }

                if ((!InBg()) && (UsePoF) && (!Me.Auras.ContainsKey("Path of Frost")))
                {
                    if (SpellManager.CanCast("Path of Frost"))
                        PathofFrost();
                }

                if (InBg())
                    return Me.GetPowerPercent(WoWPowerType.Health) <= PvPRestHealth;
                return Me.GetPowerPercent(WoWPowerType.Health) <= RestHealth;
            }
        }

        #endregion

        #region Global Rest DONE

        public override void Rest()
        {
            if ((InBg()) && (Me.HealthPercent <= PvPRestHealth))
            {
                Slog("#Health is at " + Me.HealthPercent + "%, Eat.#");

                Styx.Logic.Common.Rest.Feed();
            }

            if ((!InBg()) && (Me.HealthPercent <= RestHealth))
            {
                Slog("Health is at " + Me.HealthPercent + "%, Eat.");

                Styx.Logic.Common.Rest.Feed();
            }
        }

        #endregion

        #region Global Null Check DONE

        public bool NullCheck()
        {
            if (!Me.GotTarget) //Ok we dont have a target so let check a few things
            {
                if (Me.GotAlivePet)
                {
                    if (Me.Pet.GotTarget) //Got pet, and pet has target
                    {

                        Me.Pet.CurrentTarget.Target(); //Target pets target all is good.
                        return true;
                    }

                }

                else //No pet, lets assume somthing is wrong and have HB correct itself
                    return false;

            }

            return true;

        }

        #endregion

        #region Global Party Check DONE

        public bool InParty()
        {
            if (Me.PartyMembers.Count >= 1)
                return true;

            return false;
        }

        #endregion

        #region Global Leader Check DONE

        public bool IsLeader()
        {
            if (Me.IsGroupLeader)
                return true;

            return false;
        }

        #endregion

        #region Global BGCheck DONE

        /////////////////////////////////
        //Check to see if we are in a BG (Credit to Bobby for BG Check)
        /////////////////////////////////

        public bool InBg()
        {
            return Battlegrounds.IsInsideBattleground;
        }

        #endregion

        #region Global Auto Attack DONE

        /////////////////////////////////
        //Simply makes sure we are attacking
        /////////////////////////////////

        public bool AutoAttack()
        {
            if (NullCheck())
            {
                if (!Me.IsAutoAttacking)
                {
                    if (!Me.CurrentTarget.Dead)
                    {
                        Slog("#Combat Stance Enabled#");
                        Lua.DoString("StartAttack()");
                        return true;
                    }

                    return false;
                }

                return false;

            }

            return false;
        }

        #endregion

        #region Global Rime Check

        /////////////////////////////////
        //Detect Rime (Credit to Bobby for code)
        /////////////////////////////////

        public void RimeCheck()
        {
            const string cRime = "Rime";
            Lua.DoString("buffName,_,_,stackCount,_,_,_,_,_=UnitBuff(\"player\",\"" + cRime + "\")");
            string buffName = Lua.GetLocalizedText("buffName", Me.BaseAddress);


            if (buffName == cRime)
            {
                _rimeProc = true;
                Slog("#Rime Detected#");
            }

            else
                _rimeProc = false;
        }

        #endregion

        #region Global Spells

        #region Blood Spells

        /////////////////////////////////////
        //Blood Strike (Req Level 55) Blood
        /////////////////////////////////////

        private void BloodStrike()
        {
            SpellManager.Cast("Blood Strike"); //Do It!                       


            Slog("**Blood Strike**");
        }

        /////////////////////////////////////
        //Rune Tap (Req 10 Point Blood Talent)
        /////////////////////////////////////

        private void RuneTap()
        {
            SpellManager.Cast("Rune Tap"); //Do It!                       


            Slog("**Rune Tap**");
        }

        /////////////////////////////////////
        //Pestilence (Req Level 56) Blood
        /////////////////////////////////////

        private void Pestilence()
        {
            SpellManager.Cast("Pestilence"); //Do It!                       


            Slog("**Pestilence**");
        }

        /////////////////////////////////////
        //Heart Strike (Req 40 Blood Talen) Blood
        /////////////////////////////////////

        private void HeartStrike()
        {
            SpellManager.Cast("Heart Strike"); //Do It!                       


            Slog("**Heart Strike**");
        }

        /////////////////////////////////////
        //Blood Boil (Req Level 58) Blood
        /////////////////////////////////////

        private void BloodBoil()
        {
            SpellManager.Cast("Blood Boil"); //Do It!                       


            Slog("**Blood Boil**");
        }

        /////////////////////////////////////
        //Strangulate (Req Level 59) Blood
        /////////////////////////////////////

        private void Strangulate()
        {
            SpellManager.Cast("Strangulate"); //Do It!


            Slog("**Strangulate**");
        }

        /////////////////////////////////////
        //Mark of Blood (Req 20 Blood Talent) Blood
        /////////////////////////////////////

        private void MarkOfBlood()
        {
            SpellManager.Cast("Mark of Blood"); //Do It!                       


            Slog("**Mark of Blood**");
        }

        /////////////////////////////////////
        //Vampiric Blood (Req 35 Blood Talent) Blood
        /////////////////////////////////////

        private void VampiricBlood()
        {
            SpellManager.Cast("Vampiric Blood"); //Do It!


            Slog("#Heath is low... **Vampiric Blood** #");
        }

        /////////////////////////////////////
        //Hysteria (Req 30 Blood Talents) Blood
        /////////////////////////////////////

        private void Hysteria()
        {
            //Me.Target(); //Target Self

            SpellManager.Cast("Hysteria"); //Do It!


            Slog("**Hysteria**");

            //Me.TargetLastTarget(); //Target our last target
        }

        #endregion

        #region Frost Spells

        /////////////////////////////////
        //Icy Touch (Req Level 55) Frost
        /////////////////////////////////

        private void IcyTouch()
        {
            SpellManager.Cast("Icy Touch"); //Do It!                                                


            Slog("**Icy Touch**");
        }

        /////////////////////////////////
        //Chains of Ice (Req Level 58) Frost
        /////////////////////////////////

        private void Chains()
        {
            SpellManager.Cast("Chains of Ice"); //Do It!                                                


            Slog("**Chains of Ice**");
        }

        /////////////////////////////////////
        //Icebound Fortitude (Req Level 62) Frost
        /////////////////////////////////////

        private void IceboundFortitude()
        {
            SpellManager.Cast("Icebound Fortitude"); //Do It!


            Slog("Icebound Fortitude");
        }

        /////////////////////////////////////
        //Unbreakable Armor (Req 30 Frost Talent) Frost
        /////////////////////////////////////

        private void UnbreakableArmor()
        {
            SpellManager.Cast("Unbreakable Armor"); //Do It!


            Slog("Heath is low... **Unbreakable Armor**");
        }

        /////////////////////////////////////
        //Hungering Cold (Req 30 Frost Talent) Frost
        /////////////////////////////////////

        private void HungeringCold()
        {
            SpellManager.Cast("Hungering Cold"); //Do It!


            Slog("**Hungering Cold**");
        }

        /////////////////////////////////////
        //Howling Blast (Req 50 Frost Talent) Frost
        /////////////////////////////////////

        private void HowlingBlast()
        {
            SpellManager.Cast("Howling Blast"); //Do It!                         


            Slog("**Howling Blast**");
        }

        /////////////////////////////////////
        //Frost Strike (Req 40 Frost Talent) Frost
        /////////////////////////////////////

        private void FrostStrike()
        {
            SpellManager.Cast("Frost Strike"); //Do It!                       


            Slog("**Frost Strike**");
        }

        /////////////////////////////////////
        //Deathchill (Req 20 Frost Talent) Frost
        /////////////////////////////////////

        private void DeathChill()
        {
            SpellManager.Cast("Deathchill"); //Do It!


            Slog("**Deathchill**");
        }

        /////////////////////////////////////
        //Path of Frost (Req Level 68) Frost
        /////////////////////////////////////

        private void PathofFrost()
        {
            SpellManager.Cast("Path of Frost"); //Do It!


            Slog("**Path of Frost**");
        }

        #endregion

        #region Unholy Spells

        /////////////////////////////////////
        //Plague Strike (Req Level 55) Unholy
        /////////////////////////////////////

        private void PlagueStrike()
        {
            SpellManager.Cast("Plague Strike"); //Do It!                        


            Slog("**Plague Strike**");
        }

        /////////////////////////////////////
        //Bone Shield (Requires 35 Unholy Talent)
        /////////////////////////////////////

        public void BoneShield()
        {
            WoWMovement.MoveStop();
            Thread.Sleep(125);
            SpellManager.Cast("Bone Shield"); //Do It!                        


            Slog("**Bone Shield**");

            Thread.Sleep(2500); //Try to save refresh time
        }

        /////////////////////////////////////
        //Anti-Magic Shell (Req Level 68) Unholy
        /////////////////////////////////////

        private void AntiMagicShell()
        {
            SpellManager.Cast("Anti-Magic Shell"); //Do It!                        


            Slog("**Anti-Magic Shell**");
        }

        #endregion

        #region Runic Power Spells

        /////////////////////////////////////
        //Death Coil (Req Level 55) Runic Power
        /////////////////////////////////////

        private void DeathCoil()
        {
            SpellManager.Cast("Death Coil"); //Do It!                       


            Slog("**Death Coil**");
        }

        /////////////////////////////////////
        //Mind Freeze (Req Level 57) Runic Power
        /////////////////////////////////////

        private void MindFreeze()
        {
            SpellManager.Cast("Mind Freeze"); //Do It!


            Slog("**Mind Freeze**");
        }

        /////////////////////////////////////
        //Dancing Rune Weapon (Req 51 Blood Talent)
        /////////////////////////////////////

        private void Drw()
        {
            SpellManager.Cast("Dancing Rune Weapon"); //Do It!


            Slog("**Dancing Rune Weapon**");


            _drwToggle = false; //Go back to RP Dump

            _drwSpam = false; //Reset Spam
        }

        /////////////////////////////////////
        //Summon Gargoyle (Req 51 Unholy Talent) Unholy
        /////////////////////////////////////

        private void SummonGargoyle()
        {
            SpellManager.Cast("Summon Gargoyle"); //Do It!


            Slog("**Summon Gargoyle**");

            _sgToggle = false; //Go back to RP Dump

            _sgSpam = false; //Reset Spam
        }

        #endregion

        #region Hybrid Spells

        /////////////////////////////////////
        //Obliterate (Req Level 61) Frost/Unholy
        /////////////////////////////////////

        private void Obliterate()
        {
            SpellManager.Cast("Obliterate"); //Do It!                       


            Slog("**Obliterate**");
        }

        /////////////////////////////////////
        //Scourge Strike (Req 40 Unholy Talents) Unholy/Frost
        /////////////////////////////////////

        private void ScourgeStrike()
        {
            SpellManager.Cast("Scourge Strike"); //Do It!                       


            Slog("**Scourge Strike**");
        }

        /////////////////////////////////////
        //Death Strike Level 56 Unholy/Frost
        /////////////////////////////////////

        private void DeathStrike()
        {
            SpellManager.Cast("Death Strike"); //Do It!                       


            Slog("**Death Strike**");
        }

        #endregion

        #region Other Spells

        /////////////////////////////////////
        //Death Grip (Req Level 55)
        /////////////////////////////////////

        private void DeathGrip()
        {
            SpellManager.Cast("Death Grip"); //Do It!                


            Slog("**Death Grip**");

            Thread.Sleep(500);
        }

        /////////////////////////////////////
        //Horn of Winter (Level 65)
        /////////////////////////////////////

        public void HornOfWinter()
        {
            WoWMovement.MoveStop();
            Thread.Sleep(125);
            SpellManager.Cast("Horn of Winter"); //Do It!        


            Slog("**Horn of Winter**");
        }

        /////////////////////////////////////
        //Dark Command (Req Level 65)
        /////////////////////////////////////

        private void DarkCommand()
        {
            SpellManager.Cast("Dark Command"); //Do It!                   


            Slog("**Dark Command**");
        }

        /////////////////////////////////////
        //Raise Dead (Req Level 56)
        /////////////////////////////////////

        private void RaiseDead()
        {
            SpellManager.Cast("Raise Dead"); //Do It!

            Slog("**Raise Dead**");
        }

        /////////////////////////////////////
        //Death Pact (Req Level ??)
        /////////////////////////////////////

        private void DeathPact()
        {
            SpellManager.Cast("Death Pact"); //Do It!

            Slog("**Death Pact**");

        }

        #endregion

        #region Misc Spells

        /////////////////////////////////
        //Every Man for Himself (Req Human)
        /////////////////////////////////

        private void Em()
        {
            SpellManager.Cast("Every Man for Himself"); //Do It!                


            Slog("**Every Man for Himself**");
        }

        /////////////////////////////////
        //Stoneform (Req Dwarf)
        /////////////////////////////////

        private void Sf()
        {
            SpellManager.Cast("Stoneform"); //Do It!                


            Slog("**Stoneform**");
        }

        /////////////////////////////////
        //Gift of the Naaru (Req Draenie)
        /////////////////////////////////

        private void Naaru()
        {
            SpellManager.Cast("Gift of the Naaru"); //Do It!                


            Slog("**Gift of the Naaru**");
        }

        /////////////////////////////////
        //Arcane Torrent (Req Blood Elf)
        /////////////////////////////////

        private void At()
        {
            SpellManager.Cast("Arcane Torrent"); //Do It!                


            Slog("**Arcane Torrent**");
        }

        /////////////////////////////////
        //WarStomp (Req Tauren)
        /////////////////////////////////

        private void WarStomp()
        {
            SpellManager.Cast("War Stomp"); //Do It!                


            Slog("**War Stomp**");
        }

        /////////////////////////////////
        //Blood Fury (Req Orc)
        /////////////////////////////////

        private void BloodFury()
        {
            SpellManager.Cast("Blood Fury"); //Do It!                


            Slog("**Blood Fury**");
        }

        /////////////////////////////////
        //LifeBlood (Req Hearbalist)
        /////////////////////////////////

        private void LifeBlood()
        {
            SpellManager.Cast("Lifeblood"); //Do It!                


            Slog("**Lifeblood**");
        }

        #endregion

        #endregion

        #endregion
    }
}