using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Media;
using System.Threading;
using System.Windows.Forms;
//Advanced
//using Convalesce.Gui;
//end advanced
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Convalesce
{
    /**************************/
    /* Convalesce 4.0 Cata by Mord /*
    /**************************/
    /*This version requires Honor Buddy 1.9.somthing
    /*If you need help please check the thread where you got this download*/
    /*I do not rip code from others. Code I use from others is given to me and I add credit*/
    /*Please return the favor.*/
    /*This core is mine, I wrote it, do not rip it. I dont mind people using this for a reference
     * but blatent rip offs will result in you being banned from the forum. Im not kidding.*/
    /*You can ofcourse edit this CC to your hearts content, but do not post it. Send me a PM on
     * the forum if you have ideas for additions.*/

    /* HB Logic
        PerformCombatAciton?
        Heal
        Combat Buffs
        Combat

        OutOfCombat?
        Rest
        PreCombatBuffs
        NeedPull?
        PullBuffs
        Pull
     */


    public class Paladin : CombatRoutine
    {
        #region System Stuff *HB2*

        /////////////////////////////////
        //Load Variables, GUI Settings, other core items
        /////////////////////////////////

        private readonly Stopwatch _pullTimer = new Stopwatch(); //A Pull Timer
        private readonly Stopwatch _buffTimer = new Stopwatch(); //A Pull Timer
        private Form _cachedForm; //Best damn form around.

        #region Potions *HB2*

        // Credit to Bobby53 for this code
        private readonly List<uint> _potionEid = new List<uint>
                                                     {
                                                         //=== RESTORATION POTIONS (HEALTH AND MANA)
                                                         40077,
                                                         // Crazy Alchemist's Potion 3500 (Alchemist)
                                                         34440,
                                                         // Mad Alchemist's Potion 2750   (Alchemist)
                                                         40087,
                                                         // Powerful Rejuvenation Potion 2475
                                                         22850,
                                                         // Super Rejuvenation Potion 2100
                                                         18253,
                                                         // Major Rejuvenation Potion 1760
                                                         9144,
                                                         // Wildvine Potion 1500
                                                         2456,
                                                         // Minor Rejuvenation Potion 150

                                                         //=== HEALTH POTIONS 
                                                         33447,
                                                         // Runic Healing Potion 4500

                                                         43569,
                                                         // Endless Healing Potion  2500
                                                         43531,
                                                         // Argent Healing Potion  2500
                                                         32947,
                                                         // Auchenai Healing Potion  2500
                                                         39671,
                                                         // Resurgent Healing Potion 2500
                                                         22829,
                                                         // Super Healing Potion 2500
                                                         33934,
                                                         // Crystal Healing Potion 2500
                                                         23822,
                                                         // Healing Potion Injector 2500
                                                         33092,
                                                         // Healing Potion Injector 2500

                                                         31852,
                                                         // Major Combat Healing Potion 1750
                                                         31853,
                                                         // Major Combat Healing Potion 1750
                                                         31839,
                                                         // Major Combat Healing Potion 1750
                                                         31838,
                                                         // Major Combat Healing Potion 1750
                                                         13446,
                                                         // Major Healing Potion 1750
                                                         28100,
                                                         // Volatile Healing Potion 1750 

                                                         18839,
                                                         // Combat Healing Potion 900 
                                                         3928,
                                                         // Superior Healing Potion 900

                                                         1710,
                                                         // Greater Healing Potion  585

                                                         929,
                                                         // Healing Potion  360

                                                         4596,
                                                         // Discolored Healing Potion 180
                                                         858,
                                                         // Lesser Healing Potion 180

                                                         118 // Minor Healing Potion 90
                                                     };

        /////////////////////////////////
        //Use Potion 
        /////////////////////////////////
        private void UsePotionIfAvailable()
        {
            WoWItem potion = CheckForItem(_potionEid);
            if (potion != null)
            {
                Slog("#POTION: *" + potion.Name + "*");
                Lua.DoString("UseItemByName(\"" + potion.Name + "\")");
            }
        }

        /////////////////////////////////
        //Check Inventory for Potion 
        /////////////////////////////////
        private static WoWItem CheckForItem(List<uint> listId)
        {
            List<WoWItem> itemList = ObjectManager.GetObjectsOfType<WoWItem>(false);

            return itemList.FirstOrDefault(item => listId.Contains(item.Entry));
        }

        #endregion

        #region Global Variables *HB2*

        /////////////////////////////////
        //Globals and shit
        /////////////////////////////////

        private static string _logspam;

        private static LocalPlayer Me
        {
            get { return ObjectManager.Me; }
        }

        #endregion


        public override string Name
        {
            get { return "Convalesce Basic 4.0 by Mord"; }
        }

        //Advanced Function
        /*
        public override string Name
        {
            get { return "Convalesce Advanced 4.0 by Mord"; }
        }

        public override bool WantButton
        {
            get { return true; }
        }

        Advanced Function
        public Form ConfigForm
        {
            //get { return new ConfigForm(); }
        }

        public override void OnButtonPress() //Do on Config Button Press
        {
            if (_cachedForm != null)
                _cachedForm.ShowDialog();

            _cachedForm = new ConfigForm();
            _cachedForm.ShowDialog();
        }
        //End Advanced Function
        */

        public override WoWClass Class
        {
            get { return WoWClass.Paladin; }
        }

        public bool NeedCombatAuras
        {
            get { return false; }
        }

        public bool NeedPullAuras
        {
            //SealCheck(); //Make sure seals are good before pulling
            //AuraCheck(); //Make sure Auras are set
            //BlessingCheck(); //Make sure Blessings are set
            //RfCheck(); //Righteous Fury
            //SelfHeal(); //Heal Self (Rest)

            //if (InBg())
            //{

            //if (PvPHealbot)
            //BgHeal(); //Scan for targets to heal
            //}
            get
            {
                //SealCheck(); //Make sure seals are good before pulling

                return false;
            }
        }

        public bool NeedPreCombatAuras
        {

            get { return false; }
        }

        public override bool NeedHeal
        {
            get { return false; }
        }

        public override void CombatBuff()
        {
        }

        public override void PullBuff()
        {

        }

        public override void Heal()
        {
        }

        private static void Slog(string msg)
        {
            if (msg == _logspam)
            {
                return;
            }

            Logging.Write(msg);
            _logspam = msg;
        }

        public static string SafeLogException(string msg)
        {
            msg = msg.Replace("{", "(");
            msg = msg.Replace("}", ")");
            return msg;
        }

        #region Class Specific Variables *HB2*

        /////////////////////////////////
        //User Settings
        /////////////////////////////////

        public static int StuckCount;
        public static int LoSCount;
        private bool _addSpam;
        private bool _aoWSpam;
        private bool _casterSpam;
        private bool _runnerSpam;
        private static bool _aoWProc;
        private bool _denounceSpam;
        private static bool _denounceProc;
        private bool _approachSpam;
        private bool _gotAdds; //Check for adds        
        private bool _movingToMelee;
        private bool _pullSpam;
        private bool _pullTypeSpam;


        #region Basic Settings (Non - GUI)



        private static bool HolySpec = false;


        private static bool ProtSpec = false;


        private static bool RetSpec = false;



        private const bool UseSoR = true;


        private const bool UseSoI = false;


        private const bool UseSoJ = false;


        private const bool UseSoT = false;


        private const bool PvPUseSoR = true;


        private const bool PvPUseSoJ = false;


        private const bool PvPUseSoI = false;


        private const bool PvPUseSoT = false;

        private const bool UseBoM = true;

        private const bool UseBoK = false;

        private const bool PvPUseBoM = true;

        private const bool PvPUseBoK = false;

        private const bool BuffOthers = false;

        private const bool PvPBuffOthers = false;



        private const bool UseDa = true;


        private const bool UseRa = false;


        private const bool UseCa = false;


        private const bool UseCruA = false;


        private const bool UseReA = false;


        private const bool PvPUseDa = true;


        private const bool PvPUseRa = false;


        private const bool PvPUseCa = false;


        private const bool PvPUseCruA = false;


        private const bool PvPUseReA = false;


        private const bool MountCrusader = false;


        private const bool PvPMountCrusader = false;


        private const bool UseRf = false;


        private const bool PvPUseRf = false;


        private const int RestHealth = 15;


        private const int RestMana = 55;


        private const int PvPRestHealth = 15;


        private const int PvPRestMana = 55;




        private const bool DivinePleaRest = false;


        private const bool PvPDivinePleaRest = false;




        private const int RestDl = 0;

        private const int RestHr = 0;

        private const int RestWoGHealth = 50;

        private const int RestWoGhp = 1;

        private const int RestLoDHealth = 0;

        private const int RestLoDhp = 1;

        private const int RestHl = 60;

        private const int PvPRestDl = 0;

        private const int PvPRestHr = 0;

        private const int PvPRestWoGhp = 1;

        private const int PvPRestWoGHealth = 50;

        private const int PvPRestLoDhp = 1;

        private const int PvPRestLoDHealth = 0;

        private const int PvPRestHl = 60;


        private const int CombatHl = 60;

        private const int CombatFoL = 0;

        private const int WoGHealth = 50;

        private const int WoGhp = 1;

        private const int LoDHealth = 0;

        private const int LoDhp = 1;

        private const int DlHealth = 0;

        private const int HrHealth = 0;

        private const int HsHealth = 0;

        private const int PvPwoGHealth = 50;

        private const int PvPwoGhp = 1;

        private const int PvPloDHealth = 0;

        private const int PvPloDhp = 1;

        private const int PvPdlHealth = 0;

        private const int PvPhrHealth = 0;

        private const int PvPhsHealth = 0;

        private const int TargetMinHeal = 0;


        private const bool UsePotion = true;

        private const int PotionHealth = 20;

        private const bool UseLoH = true;

        private const int LayHealth = 15;


        private const int PvPCombatHl = 60;


        private const int PvPCombatFoL = 0;


        private const bool UseFoLAdds = false;

        private const int PvPTargetMinHeal = 0;

        private const bool PvPUsePotion = true;

        private const int PvPLayHealth = 15;

        private const int PvPPotionHealth = 20;

        private const bool PvPUseLoH = true;

        private const bool UseCleanse = true;

        private const bool CleanseMagic = false;

        private const bool PvPCleanseMagic = false;

        private const bool PvPUseCleanse = false;


        private const bool UseLifeBlood = true;

        private const bool LifeBloodAdds = true;

        private const int LifeBloodHealth = 60;

        private const bool PvPUseLifeBlood = true;

        private const int PvPLifeBloodHealth = 60;

        private const bool HoJHeal = true;

        private const bool PvPHoJHeal = true;

        private const bool RepentanceHeal = false;

        private const bool PvPRepentanceHeal = false;

        private const int HealbotCombatHl = 0;

        private const int HealbotCombatFoL = 0;

        private const int HealbotWoGHealth = 0;

        private const int HealbotWoGhp = 0;

        private const int HealbotLoDHealth = 0;

        private const int HealbotLoDhp = 0;

        private const int HealbotDlHealth = 0;

        private const int HealbotHrHealth = 0;

        private const int HealbotHsHealth = 0;

        private const int HealbotLoHHealth = 0;

        private const bool HealbotUseCleanse = false;

        private const bool HealbotCleanseMagic = false;

        private const bool Healbot = false;

        private const bool HealbotDps = false;

        private const int HealbotDpsMana = 0;

        private const int HealbotDistance = 0;

        private const int PvPHealbotCombatHl = 0;

        private const int PvPHealbotCombatFoL = 0;

        private const int PvPHealbotWoGHealth = 0;

        private const int PvPHealbotWoGhp = 0;

        private const int PvPHealbotLoDHealth = 0;

        private const int PvPHealbotLoDhp = 0;

        private const int PvPHealbotDlHealth = 0;

        private const int PvPHealbotHrHealth = 0;

        private const int PvPHealbotHsHealth = 0;

        private const int PvPHealbotLoHHealth = 0;

        private const bool PvPHealbot = false;

        private const bool PvPHealbotDps = false;

        private const bool OpenWithMelee = false;

        private const bool OpenWithHs = false;

        private const bool OpenWithJudgement = false;

        private const bool OpenWithHoR = false;

        private const bool OpenWithAs = false;

        private const bool OpenWithEx = true;

        private const bool PvPOpenWithHs = false;

        private const bool PvPOpenWithJudgement = false;

        private const bool PvPOpenWithMelee = false;

        private const bool PvPOpenWithAs = false;

        private const bool PvPOpenWithEx = true;

        private const bool PlayerAlert = false;

        private const int PlayerDetectorRange = 0;

        private const bool WaitForApproach = true;

        private const bool ApproachNone = false;

        private const bool ApproachJudgement = false;

        private const bool ApproachAs = false;

        private const bool ApproachEx = true;

        private const bool ApproachHs = false;

        private const int DpsDistance = 0;

        private const bool UseCs = true;

        private const bool PvPUseCs = true;

        private const bool UseZealotry = false;

        private const bool UseZealotryAdds = false;

        private const bool PvPUseZealotry = true;

        private const bool UseJudgement = true;

        private const int JudgementRange = 9;

        private const int PvPJudgementRange = 9;

        private const bool PvPUseJudgement = true;

        private const bool UseExorcism = false;

        private const bool PvPUseExorcism = true;

        private const bool UseDenounce = false;

        private const bool UseAoW = true;

        private const bool PvPUseAoW = false;

        private const bool PvPUseDenounce = false;

        private const int TargetMinEx = 20;

        private const int PvPTargetMinEx = 20;

        private const bool UseConsecration = true;

        private const bool UseConsecrationAdds = true;

        private const bool PvPUseConsecration = true;

        private const bool UseHoW = true;

        private const bool PvPUseHoW = true;

        private const bool UseShieldR = false;

        private const bool PvPUseShieldR = false;

        private const int SoRhp = 0;

        private const int PvPSoRhp = 0;

        private const bool UseHotR = false;

        private const bool PvPUseHotR = false;

        private const bool UseDs = true;

        private const int Dshp = 3;

        private const bool UseDsAdds = true;

        private const bool PvPUseDs = true;

        private const int PvPDshp = 3;

        private const bool UseInquisition = false;

        private const bool PvPUseInquisition = false;

        private const int InquisitionHp = 0;

        private const int PvPUseInquisitionHp = 0;

        private const bool InquisitionAdds = false;

        private const bool UseHw = true;

        private const bool PvPUseHw = true;

        private const bool UseHwAdds = true;

        private const bool UseAs = false;

        private const bool UseAsAdds = false;

        private const bool PvPUseAs = false;

        private const bool HolyShockDPS = false;

        private const bool PvPHolyShockDPS = false;

        private const bool UseAvengingWrath = true;

        private const bool UseAvengingWrathAdds = true;

        private const bool PvPUseAvengingWrath = true;

        private const bool UseTV = true;

        private const int TVHP = 3;

        private const bool PvPUseTV = true;

        private const int PvPTVHP = 3;

        private const bool UseDf = false;

        private const bool UseDfAdds = false;

        private const bool PvPUseDf = false;

        private const bool HoJCasters = true;

        private const bool PvPHoJCasters = true;

        private const bool ASCasters = false;

        private const bool PvPASCasters = false;

        private const bool UseRebuke = true;

        private const bool UseRepentance = false;

        private const bool PvPUseRepentance = true;

        private const bool PvPUseRebuke = true;

        private const bool CasterRanged = true;

        private const bool PvPCasterRanged = true;

        private const bool HoJRunners = true;

        private const bool RepentanceRunners = true;

        private const bool CasterRunners = true;

        private const bool UseHoRRunners = true;

        private const bool UseAm = false;

        private const bool UseDivinePlea = true;

        private const bool UseDivinePleaAdds = true;

        private const bool PvPUseHoF = true;

        private const bool PvPUseAm = false;

        private const bool PvPUseDivinePlea = true;

        private const bool UseDp = true;

        private const bool UseDpAdds = true;

        private const int DpHealth = 40;

        private const int DpTargetHealth = 0;

        private const int PvPdpHealth = 50;

        private const int PvPdpTargetHealth = 0;

        private const int PvPDivineShieldHealth = 15;

        private const int DivineShieldHealth = 15;

        private const bool DetectAdds = true;

        private const int AddsCount = 2;

        private const bool UseAt = false;

        private const bool UseNaaru = false;

        private const bool NaaruAdds = false;

        private const int NaaruHealth = 0;

        private const bool UseSf = false;

        private const bool SfAdds = false;

        private const int SfHealth = 0;

        private const bool UseEm = false;

        private const bool PvPUseAt = false;

        private const bool PvPUseNaaru = false;

        private const int PvPNaaruHealth = 0;

        private const bool PvPUseSf = false;

        private const int PvPsfHealth = 0;

        private const bool PvPUseEm = false;


        #endregion

        #region Advanced Settings (GUI)

        /*

        private static bool HolySpec
        {
            get { return Settings.Default.HolySpec; }
        }

        private static bool ProtSpec
        {
            get { return Settings.Default.ProtSpec; }
        }

        private static bool RetSpec
        {
            get { return Settings.Default.RetSpec; }
        }


        private static bool UseSoR
        {
            get { return Settings.Default.UseSoR; }
        }

        private static bool UseSoI
        {
            get { return Settings.Default.UseSoI; }
        }

        private static bool UseSoJ
        {
            get { return Settings.Default.UseSoJ; }
        }        

        private static bool UseSoT
        {
            get { return Settings.Default.UseSoT; }
        }

        private static bool PvPUseSoR
        {
            get { return Settings.Default.PvPUseSoR; }
        }

        private static bool PvPUseSoJ
        {
            get { return Settings.Default.PvPUseSoJ; }
        }

        private static bool PvPUseSoI
        {
            get { return Settings.Default.PvPUseSoI; }
        }

        private static bool PvPUseSoT
        {
            get { return Settings.Default.PvPUseSoT; }
        }


        private static bool UseBoM
        {
            get { return Settings.Default.UseBoM; }
        }

        private static bool UseBoK
        {
            get { return Settings.Default.UseBoK; }
        }

        private static bool PvPUseBoM
        {
            get { return Settings.Default.PvPUseBoM; }
        }

        private static bool PvPUseBoK
        {
            get { return Settings.Default.PvPUseBoK; }
        }

        private static bool BuffOthers
        {
            get { return Settings.Default.BuffOthers; }
        }

        private static bool PvPBuffOthers
        {
            get { return Settings.Default.PvPBuffOthers; }
        }


        private static bool UseDa
        {
            get { return Settings.Default.UseDA; }
        }

        private static bool UseRa
        {
            get { return Settings.Default.UseRA; }
        }

        private static bool UseCa
        {
            get { return Settings.Default.UseCA; }
        }

        private static bool UseCruA
        {
            get { return Settings.Default.UseCruA; }
        }

        private static bool UseReA
        {
            get { return Settings.Default.UseReA; }
        }

        private static bool PvPUseDa
        {
            get { return Settings.Default.PvPUseDA; }
        }

        private static bool PvPUseRa
        {
            get { return Settings.Default.PvPUseRA; }
        }

        private static bool PvPUseCa
        {
            get { return Settings.Default.PvPUseCA; }
        }

        private static bool PvPUseCruA
        {
            get { return Settings.Default.PvPUseCruA; }
        }

        private static bool PvPUseReA
        {
            get { return Settings.Default.PvPUseReA; }
        }


        private static bool MountCrusader
        {
            get { return Settings.Default.MountCrusader; }
        }        

        private static bool PvPMountCrusader
        {
            get { return Settings.Default.PvPMountCrusader; }
        }


        private static bool UseRf
        {
            get { return Settings.Default.UseRF; }
        }

        private static bool PvPUseRf
        {
            get { return Settings.Default.PvPUseRF; }
        }


        private static int RestHealth
        {
            get { return (int) Settings.Default.RestHealth; }
        }

        private static int RestMana
        {
            get { return (int) Settings.Default.RestMana; }
        }

        private static int PvPRestHealth
        {
            get { return (int)Settings.Default.PvPRestHealth; }
        }

        private static int PvPRestMana
        {
            get { return (int)Settings.Default.PvPRestMana; }
        }


        private static bool DivinePleaRest
        {
            get { return Settings.Default.DivinePleaRest; }
        }

        private static bool PvPDivinePleaRest
        {
            get { return Settings.Default.PvPDivinePleaRest; }
        }


        private static int RestDl
        {
            get { return (int) Settings.Default.RestDL; }
        }

        private static int RestHr
        {
            get { return (int)Settings.Default.RestHR; }
        }

        private static int RestWoGHealth
        {
            get { return (int)Settings.Default.RestWoGHealth; }
        }

        private static int RestWoGhp
        {
            get { return (int)Settings.Default.RestWoGHP; }
        }

        private static int RestLoDHealth
        {
            get { return (int)Settings.Default.RestLoDHealth; }
        }

        private static int RestLoDhp
        {
            get { return (int)Settings.Default.RestLoDHP; }
        }

        private static int RestHl
        {
            get { return (int)Settings.Default.RestHL; }
        }

        private static int PvPRestDl
        {
            get { return (int)Settings.Default.PvPRestDL; }
        }

        private static int PvPRestHr
        {
            get { return (int)Settings.Default.PvPRestHR; }
        }

        private static int PvPRestWoGhp
        {
            get { return (int)Settings.Default.PvPRestWoGHP; }
        }

        private static int PvPRestWoGHealth
        {
            get { return (int) Settings.Default.PvPRestWoGHealth; }
        }

        private static int PvPRestLoDhp
        {
            get { return (int)Settings.Default.PvPRestLoDHP; }
        }

        private static int PvPRestLoDHealth
        {
            get { return (int)Settings.Default.PvPRestLoDHealth; }
        }

        private static int PvPRestHl
        {
            get { return (int)Settings.Default.PvPRestHL; }
        }


        private static int CombatHl
        {
            get { return (int) Settings.Default.CombatHL; }
        }

        private static int CombatFoL
        {
            get { return (int) Settings.Default.CombatFoL; }
        }

        private static int WoGHealth
        {
            get { return (int)Settings.Default.WoGHealth; }
        }

        private static int WoGhp
        {
            get { return (int)Settings.Default.WoGHP; }
        }

        private static int LoDHealth
        {
            get { return (int)Settings.Default.LoDHealth; }
        }

        private static int LoDhp
        {
            get { return (int)Settings.Default.LoDHP; }
        }

        private static int DlHealth
        {
            get { return (int)Settings.Default.DL; }
        }

        private static int HrHealth
        {
            get { return (int)Settings.Default.HR; }
        }

        private static int HsHealth
        {
            get { return (int)Settings.Default.CombatHS; }
        }

        private static int PvPwoGHealth
        {
            get { return (int)Settings.Default.PvPWoGHealth; }
        }

        private static int PvPwoGhp
        {
            get { return (int)Settings.Default.PvPWoGHP; }
        }

        private static int PvPloDHealth
        {
            get { return (int)Settings.Default.PvPLoDHealth; }
        }

        private static int PvPloDhp
        {
            get { return (int)Settings.Default.PvPLoDHP; }
        }

        private static int PvPdlHealth
        {
            get { return (int)Settings.Default.PvPDL; }
        }

        private static int PvPhrHealth
        {
            get { return (int)Settings.Default.PvPHR; }
        }

        private static int PvPhsHealth
        {
            get { return (int)Settings.Default.PvPCombatHS; }
        }

        private static int TargetMinHeal
        {
            get { return (int) Settings.Default.TargetMinHeal; }
        }

        private static bool UsePotion
        {
            get { return Settings.Default.UsePotion; }
        }

        private static int PotionHealth
        {
            get { return (int)Settings.Default.PotionHealth; }
        }

        private static bool UseLoH
        {
            get { return Settings.Default.UseLoH; }
        }

        private static int LayHealth
        {
            get { return (int)Settings.Default.LayHealth; }
        }

        private static int PvPCombatHl
        {
            get { return (int)Settings.Default.PvPCombatHL; }
        }

        private static int PvPCombatFoL
        {
            get { return (int)Settings.Default.PvPCombatFoL; }
        }

        private static bool UseFoLAdds
        {
            get { return Settings.Default.UseFoLAdds; }
        }

        private static int PvPTargetMinHeal
        {
            get { return (int)Settings.Default.PvPTargetMinHeal; }
        }

        private static bool PvPUsePotion
        {
            get { return Settings.Default.PvPUsePotion; }
        }

        private static int PvPLayHealth
        {
            get { return (int)Settings.Default.PvPLayHealth; }
        }

        private static int PvPPotionHealth
        {
            get { return (int)Settings.Default.PvPPotionHealth; }
        }

        private static bool PvPUseLoH
        {
            get { return Settings.Default.PvPUseLoH; }
        }

        private static bool UseCleanse
        {
            get { return Settings.Default.UseCleanse; }
        }

        private static bool CleanseMagic
        {
            get { return Settings.Default.CleanseMagic; }
        }

        private static bool PvPCleanseMagic
        {
            get { return Settings.Default.PvPCleanseMagic; }
        }

        private static bool PvPUseCleanse
        {
            get { return Settings.Default.PvPUseCleanse; }
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
            get { return (int)Settings.Default.LifeBloodHealth; }
        }

        private static bool PvPUseLifeBlood
        {
            get { return Settings.Default.PvPUseLifeBlood; }
        }

        private static int PvPLifeBloodHealth
        {
            get { return (int)Settings.Default.PvPLifeBloodHealth; }
        }

        private static bool HoJHeal
        {
            get { return Settings.Default.HoJHeal; }
        }

        private static bool PvPHoJHeal
        {
            get { return Settings.Default.PvPHoJHeal; }
        }

        private static bool RepentanceHeal
        {
            get { return Settings.Default.RepentanceHeal; }
        }

        private static bool PvPRepentanceHeal
        {
            get { return Settings.Default.PvPRepentanceHeal; }
        }


        private static int HealbotCombatHl
        {
            get { return (int)Settings.Default.HealbotCombatHL; }
        }

        private static int HealbotCombatFoL
        {
            get { return (int)Settings.Default.HealbotCombatFoL; }
        }

        private static int HealbotWoGHealth
        {
            get { return (int)Settings.Default.HealbotWoGHealth; }
        }

        private static int HealbotWoGhp
        {
            get { return (int)Settings.Default.HealbotWoGHP; }
        }

        private static int HealbotLoDHealth
        {
            get { return (int)Settings.Default.HealbotLoDHealth; }
        }

        private static int HealbotLoDhp
        {
            get { return (int)Settings.Default.HealbotLoDHP; }
        }

        private static int HealbotDlHealth
        {
            get { return (int)Settings.Default.HealbotDL; }
        }

        private static int HealbotHrHealth
        {
            get { return (int)Settings.Default.HealbotHR; }
        }

        private static int HealbotHsHealth
        {
            get { return (int)Settings.Default.HealbotCombatHS; }
        }

        private static int HealbotLoHHealth
        {
            get { return (int)Settings.Default.HealbotLoHHealth; }
        }

        private static bool HealbotUseCleanse
        {
            get { return Settings.Default.UseCleanse; }
        }

        private static bool HealbotCleanseMagic
        {
            get { return Settings.Default.HealbotCleanseMagic; }
        }

        private static bool Healbot
        {
            get { return Settings.Default.Healbot; }
        }

        private static bool HealbotDps
        {
            get { return Settings.Default.HealbotDPS; }
        }

        private static int HealbotDpsMana
        {
            get { return (int)Settings.Default.HealbotDPSMana; }
        }

        private static int HealbotDistance
        {
            get { return (int)Settings.Default.HealbotDistance; }
        }


        private static int PvPHealbotCombatHl
        {
            get { return (int)Settings.Default.PvPHealbotCombatHL; }
        }

        private static int PvPHealbotCombatFoL
        {
            get { return (int)Settings.Default.PvPHealbotCombatFoL; }
        }

        private static int PvPHealbotWoGHealth
        {
            get { return (int)Settings.Default.PvPHealbotWoGHealth; }
        }

        private static int PvPHealbotWoGhp
        {
            get { return (int)Settings.Default.PvPHealbotWoGHP; }
        }

        private static int PvPHealbotLoDHealth
        {
            get { return (int)Settings.Default.PvPHealbotLoDHealth; }
        }

        private static int PvPHealbotLoDhp
        {
            get { return (int)Settings.Default.PvPHealbotLoDHP; }
        }

        private static int PvPHealbotDlHealth
        {
            get { return (int)Settings.Default.PvPHealbotDL; }
        }

        private static int PvPHealbotHrHealth
        {
            get { return (int)Settings.Default.PvPHealbotHR; }
        }

        private static int PvPHealbotHsHealth
        {
            get { return (int)Settings.Default.PvPHealbotCombatHS; }
        }

        private static int PvPHealbotLoHHealth
        {
            get { return (int)Settings.Default.PvPHealbotLoHHealth; }
        }

        private static bool PvPHealbot
        {
            get { return Settings.Default.PvPHealbot; }
        }

        private static bool PvPHealbotDps
        {
            get { return Settings.Default.PvPHealbotDPS; }
        }


        private static bool OpenWithMelee
        {
            get { return Settings.Default.OpenWithMelee; }
        }

        private static bool OpenWithHs
        {
            get { return Settings.Default.OpenWithHS; }
        }

        private static bool OpenWithJudgement
        {
            get { return Settings.Default.OpenWithJudge; }
        }

        private static bool OpenWithHoR
        {
            get { return Settings.Default.OpenWithHoR; }
        }

        private static bool OpenWithAs
        {
            get { return Settings.Default.OpenWithAS; }
        }

        private static bool OpenWithEx
        {
            get { return Settings.Default.OpenWithEx; }
        }

        private static bool PvPOpenWithHs
        {
            get { return Settings.Default.PvPOpenWithHS; }
        }

        private static bool PvPOpenWithJudgement
        {
            get { return Settings.Default.PvPOpenWithJudge; }
        }

        private static bool PvPOpenWithMelee
        {
            get { return Settings.Default.PvPPullWithMelee; }
        }

        private static bool PvPOpenWithAs
        {
            get { return Settings.Default.PvPOpenWithAS; }
        }

        private static bool PvPOpenWithEx
        {
            get { return Settings.Default.PvPOpenWithEx; }
        }

        private static bool PlayerAlert
        {
            get { return Settings.Default.PlayerAlert; }
        }

        private static int PlayerDetectorRange
        {
            get { return (int)Settings.Default.PlayerDetectorRange; }
        }

        private static bool WaitForApproach
        {
            get { return Settings.Default.WaitForApproach; }
        }

        private static bool ApproachNone
        {
            get { return Settings.Default.ApproachNone; }
        }

        private static bool ApproachJudgement
        {
            get { return Settings.Default.ApproachJudgement; }
        }

        private static bool ApproachAs
        {
            get { return Settings.Default.ApproachAS; }
        }

        private static bool ApproachEx
        {
            get { return Settings.Default.ApproachEx; }
        }

        private static bool ApproachHs
        {
            get { return Settings.Default.ApproachHS; }
        }

        private static int DpsDistance
        {
            get { return (int)Settings.Default.DPSDistance; }
        }


        private static bool UseCs
        {
            get { return Settings.Default.UseCS; }
        }

        private static bool PvPUseCs
        {
            get { return Settings.Default.PvPUseCS; }
        }

        private static bool UseZealotry
        {
            get { return Settings.Default.UseZealotry; }
        }

        private static bool UseZealotryAdds
        {
            get { return Settings.Default.UseZealotryAdds; }
        }

        private static bool PvPUseZealotry
        {
            get { return Settings.Default.PvPUseZealotry; }
        }

        private static bool UseJudgement
        {
            get { return Settings.Default.UseJudgement; }
        }

        private static int JudgementRange
        {
            get { return (int)Settings.Default.JudgementRange; }
        }

        private static int PvPJudgementRange
        {
            get { return (int)Settings.Default.PvPJudgementRange; }
        }

        private static bool PvPUseJudgement
        {
            get { return Settings.Default.PvPUseJudgement; }
        }

        private static bool UseExorcism
        {
            get { return Settings.Default.UseExorcism; }
        }

        private static bool PvPUseExorcism
        {
            get { return Settings.Default.PvPUseExorcism; }
        }

        private static bool UseDenounce
        {
            get { return Settings.Default.UseDenounce; }
        }

        private static bool UseAoW
        {
            get { return Settings.Default.UseAoW; }
        }

        private static bool PvPUseAoW
        {
            get { return Settings.Default.PvPUseAoW; }
        }

        private static bool PvPUseDenounce
        {
            get { return Settings.Default.PvPUseDenounce; }
        }

        private static int TargetMinEx
        {
            get { return (int)Settings.Default.TargetMinEx; }
        }

        private static int PvPTargetMinEx
        {
            get { return (int)Settings.Default.PvPTargetMinEx; }
        }

        private static bool UseConsecration
        {
            get { return Settings.Default.UseConsecration; }
        }

        private static bool UseConsecrationAdds
        {
            get { return Settings.Default.UseConsecrationAdds; }
        }

        private static bool PvPUseConsecration
        {
            get { return Settings.Default.PvPUseConsecration; }
        }

        private static bool UseHoW
        {
            get { return Settings.Default.UseHoW; }
        }

        private static bool PvPUseHoW
        {
            get { return Settings.Default.PvPUseHoW; }
        }

        private static bool UseShieldR
        {
            get { return Settings.Default.UseShieldR; }
        }

        private static bool PvPUseShieldR
        {
            get { return Settings.Default.PvPUseShieldR; }
        }

        private static int SoRhp
        {
            get { return (int)Settings.Default.SoRHP; }
        }

        private static int PvPSoRhp
        {
            get { return (int)Settings.Default.PvPSoRHP; }
        }

        private static bool UseHotR
        {
            get { return Settings.Default.UseHotR; }
        }

        private static bool PvPUseHotR
        {
            get { return Settings.Default.PvPUseHotR; }
        }

        private static bool UseDs
        {
            get { return Settings.Default.UseDS; }
        }

        private static int Dshp
        {
            get { return (int)Settings.Default.DSHP; }
        }

        private static bool UseDsAdds
        {
            get { return Settings.Default.UseDSAdds; }
        }

        private static bool PvPUseDs
        {
            get { return Settings.Default.PvPUseDS; }
        }

        private static int PvPDshp
        {
            get { return (int)Settings.Default.PvPDSHP; }
        }

        private static bool UseInquisition
        {
            get { return Settings.Default.Inquisition; }
        }

        private static bool PvPUseInquisition
        {
            get { return Settings.Default.PvPInquisition; }
        }

        private static int InquisitionHp
        {
            get { return (int)Settings.Default.InquisitionHP; }
        }

        private static int PvPUseInquisitionHp
        {
            get { return (int)Settings.Default.PvPInquisitionHP; }
        }

        private static bool InquisitionAdds
        {
            get { return Settings.Default.InquisitionAdds; }
        }

        private static bool UseHw
        {
            get { return Settings.Default.UseHW; }
        }

        private static bool PvPUseHw
        {
            get { return Settings.Default.PvPUseHW; }
        }

        private static bool UseHwAdds
        {
            get { return Settings.Default.UseHWAdds; }
        }

        private static bool UseAs
        {
            get { return Settings.Default.UseAS; }
        }

        private static bool UseAsAdds
        {
            get { return Settings.Default.UseASAdds; }
        }

        private static bool PvPUseAs
        {
            get { return Settings.Default.PvPUseAS; }
        }

        private static bool HolyShockDPS
        {
            get { return Settings.Default.HolyShockDPS; }
        }

        private static bool PvPHolyShockDPS
        {
            get { return Settings.Default.HolyShockDPS; }
        }

        private static bool UseAvengingWrath
        {
            get { return Settings.Default.UseAvengingWrath; }
        }

        private static bool UseAvengingWrathAdds
        {
            get { return Settings.Default.UseAvengingWrathAdds; }
        }

        private static bool PvPUseAvengingWrath
        {
            get { return Settings.Default.PvPUseAvengingWrath; }
        }

        private static bool UseTV
        {
            get { return Settings.Default.UseTV; }
        }

        private static int TVHP
        {
            get { return (int)Settings.Default.TVHP; }
        }      

        private static bool PvPUseTV
        {
            get { return Settings.Default.PvPUseTV; }
        }

        private static int PvPTVHP
        {
            get { return (int)Settings.Default.PvPTVHP; }
        }

        private static bool UseDf
        {
            get { return Settings.Default.UseDF; }
        }

        private static bool UseDfAdds
        {
            get { return Settings.Default.UseDFAdds; }
        }

        private static bool PvPUseDf
        {
            get { return Settings.Default.PvPUseDF; }
        }


        private static bool HoJCasters
        {
            get { return Settings.Default.HoJCasters; }
        }

        private static bool PvPHoJCasters
        {
            get { return Settings.Default.PvPHoJCasters; }
        }

        private static bool ASCasters
        {
            get { return Settings.Default.ASCasters; }
        }

        private static bool PvPASCasters
        {
            get { return Settings.Default.PvPASCasters; }
        }

        private static bool UseRebuke
        {
            get { return Settings.Default.UseRebuke; }
        }

        private static bool UseRepentance
        {
            get { return Settings.Default.UseRepentance; }
        }

        private static bool PvPUseRepentance
        {
            get { return Settings.Default.PvPUseRepentance; }
        }

        private static bool PvPUseRebuke
        {
            get { return Settings.Default.PvPUseRebuke; }
        }

        private static bool CasterRanged
        {
            get { return Settings.Default.CasterRanged; }
        }

        private static bool PvPCasterRanged
        {
            get { return Settings.Default.PvPCasterRanged; }
        }



        private static bool HoJRunners
        {
            get { return Settings.Default.HoJRunners; }
        }

        private static bool RepentanceRunners
        {
            get { return Settings.Default.RepentanceRunners; }
        }

        private static bool CasterRunners
        {
            get { return Settings.Default.CasterRunners; }
        }

        private static bool UseHoRRunners
        {
            get { return Settings.Default.UseHoRRunners; }
        }


        private static bool UseAm
        {
            get { return Settings.Default.UseAM; }
        }

        private static bool UseDivinePlea
        {
            get { return Settings.Default.UseDivinePlea; }
        }

        private static bool UseDivinePleaAdds
        {
            get { return Settings.Default.UseDivinePleaAdds; }
        }

        private static bool PvPUseHoF
        {
            get { return Settings.Default.PvPUseHoF; }
        }

        private static bool PvPUseAm
        {
            get { return Settings.Default.PvPUseAM; }
        }

        private static bool PvPUseDivinePlea
        {
            get { return Settings.Default.PvPUseDivinePlea; }
        }


        private static bool UseDp
        {
            get { return Settings.Default.UseDP; }
        }

        private static bool UseDpAdds
        {
            get { return Settings.Default.UseDPAdds; }
        }

        private static int DpHealth
        {
            get { return (int)Settings.Default.DPHealth; }
        }

        private static int DpTargetHealth
        {
            get { return (int)Settings.Default.DPTargetHealth; }
        }

        private static int PvPdpHealth
        {
            get { return (int)Settings.Default.PvPDPHealth; }
        }

        private static int PvPdpTargetHealth
        {
            get { return (int)Settings.Default.PvPDPTargetHealth; }
        }


        private static int PvPDivineShieldHealth
        {
            get { return (int)Settings.Default.PvPDivineShieldHealth; }
        }

        private static int DivineShieldHealth
        {
            get { return (int)Settings.Default.DivineShieldHealth; }
        }


        private static bool DetectAdds
        {
            get { return Settings.Default.DetectAdds; }
        }

        private static int AddsCount
        {
            get { return (int) Settings.Default.AddsCount; }
        }


        private static bool UseAt
        {
            get { return Settings.Default.UseAT; }
        }

        private static bool UseNaaru
        {
            get { return Settings.Default.UseNaaru; }
        }

        private static bool NaaruAdds
        {
            get { return Settings.Default.NaaruAdds; }
        }

        private static int NaaruHealth
        {
            get { return (int) Settings.Default.NaaruHealth; }
        }

        private static bool UseSf
        {
            get { return Settings.Default.UseSF; }
        }

        private static bool SfAdds
        {
            get { return Settings.Default.SFAdds; }
        }

        private static int SfHealth
        {
            get { return (int) Settings.Default.SFHealth; }
        }

        private static bool UseEm
        {
            get { return Settings.Default.UseEM; }
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

        private static bool PvPUseSf
        {
            get { return Settings.Default.PvPUseSF; }
        }

        private static int PvPsfHealth
        {
            get { return (int) Settings.Default.PvPSFHealth; }
        }

        private static bool PvPUseEm
        {
            get { return Settings.Default.PvPUseEM; }
        }
        
         */

        #endregion


        #endregion

        #endregion

        #region Pull

        public override void Pull()
        {
            int num = HealbotDistance - 3;
            int num2 = HealbotDistance + 3; //Need a 4 yard threshold to prevent constant moving

            //int num3 = DpsDistance - 3;
            int num4 = DpsDistance + 3; //Need a 4 yard threshold to prevent constant moving 

            //
            /////////////////////
            //Solo, Leader, In BG (Not healing)
            /////////////////////
            //

            if ((!InParty() || (InParty() && IsLeader())) || ((InBg())))
            {
                if ((Me.CurrentTarget != null) && (!Me.Combat))
                {
                    /******Not in a BG******/

                    if (!InBg())
                    {
                        if (!_pullTimer.IsRunning)
                        {
                            _pullTimer.Start();
                        }

                        /******Target is out of Range******/
                        if (Me.CurrentTarget.Distance > 45.0)
                        {
                            Blacklist.Add(Me.CurrentTarget, TimeSpan.FromSeconds(2.0));
                            Me.ClearTarget();
                            WoWMovement.MoveStop();
                            _pullSpam = false;
                            _pullTypeSpam = false;
                            _pullTimer.Reset();
                            return;
                        }

                        if (!_pullSpam)
                        {
                            Slog(
                                string.Concat(new object[]
                                                  {
                                                      "#Killing Target *", Me.CurrentTarget.Name, "* , Distance : "
                                                      ,
                                                      Math.Floor(Me.CurrentTarget.Distance), " yards.#"
                                                  }));
                        }

                        if (_pullTimer.Elapsed.Seconds >= 14)
                        {
                            Slog("#Pull Timeout! Blacklisting target for 1 minute.#");
                            Blacklist.Add(Me.CurrentTarget, TimeSpan.FromMinutes(1.0));
                            Me.ClearTarget();
                            _pullTimer.Reset();
                            _pullSpam = false;
                            return;
                        }

                        /******Opener on CD*******/
                        if ((Me.CurrentTarget.Distance > 4.0) &&
                            ((((OpenWithJudgement && !SpellManager.CanCast("Judgement"))) ||
                              ((OpenWithAs && !SpellManager.CanCast("Avenger's Shield"))) ||
                              ((OpenWithHs && !SpellManager.CanCast("Holy Shock"))) ||
                               (OpenWithEx && !SpellManager.CanCast("Exorcism")))) ||
                             ((OpenWithHoR && !SpellManager.CanCast("Hand of Reckoning"))))
                        {
                            if (!_pullTypeSpam)
                            {
                                Slog("#Opener is on CD. Switch to Melee#");
                            }
                            _pullTypeSpam = true;

                            Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                              ObjectManager.Me.CurrentTarget.
                                                                                  Location, 2.5f));

                            _pullSpam = true;
                            if (!AutoAttack())
                            {
                                Thread.Sleep(125);
                            }

                            return;
                        }

                        /******No opener selected******/
                        if (OpenWithMelee)
                        {
                            if (Me.CurrentTarget.Distance > 4.0)
                            {
                                if (!_pullTypeSpam)
                                {
                                    Slog("#Moving to within 3 yards of current target for melee attack#");
                                }
                                _pullTypeSpam = true;

                                Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                                  ObjectManager.Me.CurrentTarget
                                                                                      .
                                                                                      Location, 2.5f));

                                _pullSpam = true;
                                if (!AutoAttack())
                                {
                                    Thread.Sleep(125);
                                }

                                return;
                            }
                        }

                        /******Opener is Hand of Reckoning******/
                        if (OpenWithHoR)
                        {
                            if ((Me.CurrentTarget.Distance >= 28.0) && (SpellManager.CanCast("Hand of Reckoning")))
                            {
                                if (!_pullTypeSpam)
                                {
                                    Slog(
                                        "#Moving to within 30 yards of current target for Hand of Reckoning#");
                                }
                                _pullTypeSpam = true;
                                Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                                  ObjectManager.Me.CurrentTarget
                                                                                      .Location, 2.5f));
                                _pullSpam = true;
                                return;
                            }

                            if ((Me.CurrentTarget.Distance < 28.0) && (SpellManager.CanCast("Hand of Reckoning")))
                            {
                                if (!_pullTypeSpam)
                                {
                                    Slog("#In Range for Hand of Reckoning#");
                                }

                                if (!Me.CurrentTarget.InLineOfSight) //Move to LoS
                                {
                                    Slog("#But not in Line of Sight. Move to LoS#");

                                    Navigator.MoveTo(
                                        WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                         ObjectManager.Me.CurrentTarget.
                                                                             Location, 2.5f));
                                    _pullSpam = true;
                                    _pullTypeSpam = true;
                                    return;
                                }

                                _pullTypeSpam = true;
                                _pullSpam = true;
                            }
                        }

                        /******Opener is Exorcism******/
                        if (OpenWithEx)
                        {
                            if ((Me.CurrentTarget.Distance >= 28.0) && (SpellManager.CanCast("Exorcism")))
                            {
                                if (!_pullTypeSpam)
                                {
                                    Slog("#Moving to within 30 yards of current target for Exorcism#");
                                }

                                _pullTypeSpam = true;
                                Navigator.MoveTo(
                                    WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                     ObjectManager.Me.CurrentTarget.
                                                                         Location, 2.5f));
                                _pullSpam = true;
                                return;
                            }

                            if ((Me.CurrentTarget.Distance < 28.0) && (SpellManager.CanCast("Exorcism")))
                            {
                                if (!_pullTypeSpam)
                                {
                                    Slog("#In range for Exorcism#");
                                }

                                if (!Me.CurrentTarget.InLineOfSight) //Move to LoS
                                {
                                    Slog("#But not in Line of Sight. Move to LoS#");

                                    Navigator.MoveTo(
                                        WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                         ObjectManager.Me.CurrentTarget.
                                                                             Location, 2.5f));
                                    _pullSpam = true;
                                    _pullTypeSpam = true;
                                    return;
                                }

                                _pullTypeSpam = true;
                                _pullSpam = true;
                            }
                        }

                        /******Opener is Holy Shock******/
                        if (OpenWithHs)
                        {
                            if ((Me.CurrentTarget.Distance >= 19.0) && (SpellManager.CanCast("Holy Shock")))
                            {
                                if (!_pullTypeSpam)
                                {
                                    Slog(
                                        "#Moving to within 30 yards of current target for Holy Shock#");
                                }
                                _pullTypeSpam = true;
                                Navigator.MoveTo(
                                    WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                     ObjectManager.Me.CurrentTarget.
                                                                         Location, 2.5f));
                                _pullSpam = true;
                                return;
                            }

                            if ((Me.CurrentTarget.Distance < 19.0) && (SpellManager.CanCast("Holy Shock")))
                            {
                                if (!_pullTypeSpam)
                                {
                                    Slog("#In Range for Holy Shock#");
                                }

                                if (!Me.CurrentTarget.InLineOfSight) //Move to LoS
                                {
                                    Slog("#But not in Line of Sight. Move to LoS#");
                                    Navigator.MoveTo(
                                        WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                         ObjectManager.Me.
                                                                             CurrentTarget.Location,
                                                                         2.5f));
                                    _pullSpam = true;
                                    _pullTypeSpam = true;
                                    return;
                                }

                                _pullTypeSpam = true;
                                _pullSpam = true;
                            }
                        }

                        /******Opener is Avenger's Shield******/
                        if (OpenWithAs)
                        {
                            if ((Me.CurrentTarget.Distance >= 28.0) && (SpellManager.CanCast("Avenger's Shield")))
                            {
                                if (!_pullTypeSpam)
                                {
                                    Slog(
                                        "#Moving to within 30 yards of current target for Avenger's Shield#");
                                }
                                _pullTypeSpam = true;
                                Navigator.MoveTo(
                                    WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                     ObjectManager.Me.CurrentTarget.
                                                                         Location, 2.5f));
                                _pullSpam = true;
                                return;
                            }

                            if ((Me.CurrentTarget.Distance < 28.0) && (SpellManager.CanCast("Avenger's Shield")))
                            {
                                if (!_pullTypeSpam)
                                {
                                    Slog("#In Range for Avenger's Shield#");
                                }

                                if (!Me.CurrentTarget.InLineOfSight) //Move to LoS
                                {
                                    Slog("#But not in Line of Sight. Move to LoS#");
                                    Navigator.MoveTo(
                                        WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                         ObjectManager.Me.
                                                                             CurrentTarget.Location,
                                                                         2.5f));
                                    _pullSpam = true;
                                    _pullTypeSpam = true;
                                    return;
                                }

                                _pullTypeSpam = true;
                                _pullSpam = true;
                            }
                        }

                        /******Opener is Judgement******/
                        if (OpenWithJudgement)
                        {
                            if ((Me.CurrentTarget.Distance >= 9.0) && (SpellManager.CanCast("Judgement")))
                            {
                                if (!_pullTypeSpam)
                                {
                                    Slog(
                                        "#Moving to within 9 yards of current target for Judgement#");
                                }
                                _pullTypeSpam = true;
                                Navigator.MoveTo(
                                    WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                     ObjectManager.Me.
                                                                         CurrentTarget.Location,
                                                                     2.5f));
                                _pullSpam = true;
                                return;
                            }

                            if ((Me.CurrentTarget.Distance < 9.0) && (SpellManager.CanCast("Judgement")))
                            {
                                if (!_pullTypeSpam)
                                {
                                    Slog("#In Range for Judgement#");
                                }

                                if (!Me.CurrentTarget.InLineOfSight) //Move to LoS
                                {
                                    Slog("#But not in Line of Sight. Move to LoS#");

                                    Navigator.MoveTo(
                                        WoWMathHelper.CalculatePointFrom(
                                            ObjectManager.Me.Location,
                                            ObjectManager.Me.CurrentTarget.Location, 2.5f));

                                    _pullSpam = true;
                                    _pullTypeSpam = true;
                                    return;
                                }

                                _pullTypeSpam = true;
                                _pullSpam = true;
                            }
                        }

                        /******In Range for Melee Attack******/
                        if (Me.CurrentTarget.Distance < 4.0)
                        {
                            Slog("#In Melee Range for Attack#");
                            WoWMovement.MoveStop();
                            WoWMovement.Face();
                            AutoAttack();
                            return;
                        }

                        /******In Range for Opener******/
                        if (Opener())
                        {
                            Slog("#Opener was successfull!#");
                            _pullSpam = false;

                            //Thread.Sleep(250);//HB may not detect combat right away
                        }

                        else
                        {
                            Slog("#Opener failed! Try again#");
                            WoWMovement.MoveStop();
                            WoWMovement.Face();
                        }
                    }

                    /******In a BG******/
                    if (InBg())
                    {
                        if (!_pullTimer.IsRunning)
                        {
                            _pullTimer.Start();
                        }

                        /******Dedicated Healer******/
                        if ((PvPHealbot) && (!PvPHealbotDps)) // DPS and Heal
                        {

                            if (BgHeal()) //Scan for targets to heal
                                return;

                            if (!_pullSpam)
                                Slog("#Pull Override - Waiting for targets to heal#");

                            _pullSpam = true;

                            return;
                        }

                        /******Heal + DPS******/
                        if ((PvPHealbot) && (PvPHealbotDps)) // DPS and Heal
                        {

                            if (BgHeal()) //Scan for targets to heal
                            {
                                if (!_pullSpam)
                                    Slog("#Healbot - Pull Override - Player(s) in need of healz#");

                                _pullSpam = true;

                                return;

                            }

                            return;

                        }

                        /******Target is out of Range******/
                        if (Me.CurrentTarget.Distance > 45.0)
                        {
                            Blacklist.Add(Me.CurrentTarget, TimeSpan.FromSeconds(2.0));
                            Me.ClearTarget();
                            WoWMovement.MoveStop();
                            _pullSpam = false;
                            _pullTypeSpam = false;
                            _pullTimer.Reset();
                            return;
                        }

                        if (!_pullSpam)
                        {
                            Slog(
                                string.Concat(new object[]
                                                  {
                                                      "#Killing Player at Distance : "
                                                      ,
                                                      Math.Floor(Me.CurrentTarget.Distance), " yards.#"
                                                  }));
                        }

                        if (_pullTimer.Elapsed.Seconds >= 14)
                        {
                            Slog("#Pull Timeout! Blacklisting target for 1 minute.#");
                            Blacklist.Add(Me.CurrentTarget, TimeSpan.FromMinutes(1.0));
                            Me.ClearTarget();
                            _pullTimer.Reset();
                            _pullSpam = false;
                            return;
                        }

                        /******Opener on CD*******/
                        if ((Me.CurrentTarget.Distance > 4.0) &&
                            ((((PvPOpenWithJudgement && !SpellManager.CanCast("Judgement"))) ||
                              ((PvPOpenWithAs && !SpellManager.CanCast("Avenger's Shield")) ||
                              ((OpenWithHs && !SpellManager.CanCast("Holy Shock"))) ||
                               (PvPOpenWithEx && !SpellManager.CanCast("Exorcism"))))))
                        {
                            if (!_pullTypeSpam)
                            {
                                Slog("#Opener is on CD. Switch to Melee#");
                            }
                            _pullTypeSpam = true;

                            Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                              ObjectManager.Me.CurrentTarget.
                                                                                  Location, 2.5f));

                            _pullSpam = true;
                            if (!AutoAttack())
                            {
                                Thread.Sleep(125);
                            }

                            return;
                        }

                        /******No opener selected******/
                        if (PvPOpenWithMelee)
                        {
                            if (Me.CurrentTarget.Distance > 4.0)
                            {
                                if (!_pullTypeSpam)
                                {
                                    Slog("#Moving to within 3 yards of current target for melee attack#");
                                }
                                _pullTypeSpam = true;

                                Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                                  ObjectManager.Me.CurrentTarget
                                                                                      .
                                                                                      Location, 2.5f));

                                _pullSpam = true;
                                if (!AutoAttack())
                                {
                                    Thread.Sleep(125);
                                }

                                return;
                            }
                        }

                        /******Opener is Exorcism******/
                        if (PvPOpenWithEx)
                        {
                            if ((Me.CurrentTarget.Distance >= 28.0) && (SpellManager.CanCast("Exorcism")))
                            {
                                if (!_pullTypeSpam)
                                {
                                    Slog("#Moving to within 30 yards of current target for Exorcism#");
                                }

                                _pullTypeSpam = true;
                                Navigator.MoveTo(
                                    WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                     ObjectManager.Me.CurrentTarget.
                                                                         Location, 2.5f));
                                _pullSpam = true;
                                return;
                            }

                            if ((Me.CurrentTarget.Distance < 28.0) && (SpellManager.CanCast("Exorcism")))
                            {
                                if (!_pullTypeSpam)
                                {
                                    Slog("#In range for Exorcism#");
                                }

                                if (!Me.CurrentTarget.InLineOfSight) //Move to LoS
                                {
                                    Slog("#But not in Line of Sight. Move to LoS#");

                                    Navigator.MoveTo(
                                        WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                         ObjectManager.Me.CurrentTarget.
                                                                             Location, 2.5f));
                                    _pullSpam = true;
                                    _pullTypeSpam = true;
                                    return;
                                }

                                _pullTypeSpam = true;
                                _pullSpam = true;
                            }
                        }

                        /******Opener is Holy Shock******/
                        if (PvPOpenWithHs)
                        {
                            if ((Me.CurrentTarget.Distance >= 19.0) && (SpellManager.CanCast("Holy Shock")))
                            {
                                if (!_pullTypeSpam)
                                {
                                    Slog(
                                        "#Moving to within 30 yards of current target for Holy Shock#");
                                }
                                _pullTypeSpam = true;
                                Navigator.MoveTo(
                                    WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                     ObjectManager.Me.CurrentTarget.
                                                                         Location, 2.5f));
                                _pullSpam = true;
                                return;
                            }

                            if ((Me.CurrentTarget.Distance < 19.0) && (SpellManager.CanCast("Holy Shock")))
                            {
                                if (!_pullTypeSpam)
                                {
                                    Slog("#In Range for Holy Shock#");
                                }

                                if (!Me.CurrentTarget.InLineOfSight) //Move to LoS
                                {
                                    Slog("#But not in Line of Sight. Move to LoS#");
                                    Navigator.MoveTo(
                                        WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                         ObjectManager.Me.
                                                                             CurrentTarget.Location,
                                                                         2.5f));
                                    _pullSpam = true;
                                    _pullTypeSpam = true;
                                    return;
                                }

                                _pullTypeSpam = true;
                                _pullSpam = true;
                            }
                        }

                        /******Opener is Avenger's Shield******/
                        if (PvPOpenWithAs)
                        {
                            if ((Me.CurrentTarget.Distance >= 28.0) && (SpellManager.CanCast("Avenger's Shield")))
                            {
                                if (!_pullTypeSpam)
                                {
                                    Slog(
                                        "#Moving to within 30 yards of current target for Avenger's Shield#");
                                }
                                _pullTypeSpam = true;
                                Navigator.MoveTo(
                                    WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                     ObjectManager.Me.CurrentTarget.
                                                                         Location, 2.5f));
                                _pullSpam = true;
                                return;
                            }

                            if ((Me.CurrentTarget.Distance < 28.0) && (SpellManager.CanCast("Avenger's Shield")))
                            {
                                if (!_pullTypeSpam)
                                {
                                    Slog("#In Range for Avenger's Shield#");
                                }

                                if (!Me.CurrentTarget.InLineOfSight) //Move to LoS
                                {
                                    Slog("#But not in Line of Sight. Move to LoS#");
                                    Navigator.MoveTo(
                                        WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                         ObjectManager.Me.
                                                                             CurrentTarget.Location,
                                                                         2.5f));
                                    _pullSpam = true;
                                    _pullTypeSpam = true;
                                    return;
                                }

                                _pullTypeSpam = true;
                                _pullSpam = true;
                            }
                        }

                        /******Opener is Judgement******/
                        if (PvPOpenWithJudgement)
                        {
                            if ((Me.CurrentTarget.Distance >= 9.0) && (SpellManager.CanCast("Judgement")))
                            {
                                if (!_pullTypeSpam)
                                {
                                    Slog(
                                        "#Moving to within 9 yards of current target for Judgement#");
                                }
                                _pullTypeSpam = true;
                                Navigator.MoveTo(
                                    WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                     ObjectManager.Me.
                                                                         CurrentTarget.Location,
                                                                     2.5f));
                                _pullSpam = true;
                                return;
                            }

                            if ((Me.CurrentTarget.Distance < 9.0) && (SpellManager.CanCast("Judgement")))
                            {
                                if (!_pullTypeSpam)
                                {
                                    Slog("#In Range for Judgement#");
                                }

                                if (!Me.CurrentTarget.InLineOfSight) //Move to LoS
                                {
                                    Slog("#But not in Line of Sight. Move to LoS#");

                                    Navigator.MoveTo(
                                        WoWMathHelper.CalculatePointFrom(
                                            ObjectManager.Me.Location,
                                            ObjectManager.Me.CurrentTarget.Location, 2.5f));

                                    _pullSpam = true;
                                    _pullTypeSpam = true;
                                    return;
                                }

                                _pullTypeSpam = true;
                                _pullSpam = true;
                            }
                        }

                        /******In Range for Melee Attack******/
                        if (Me.CurrentTarget.Distance < 4.0)
                        {
                            Slog("#In Melee Range for Attack#");
                            WoWMovement.MoveStop();
                            WoWMovement.Face();
                            AutoAttack();
                            return;
                        }

                        /******In Range for Opener******/
                        if (Opener())
                        {
                            Slog("#Opener was successfull!#");
                            _pullSpam = false;
                            //Thread.Sleep(250);
                        }

                        else
                        {
                            Slog("#Opener failed! Try again#");
                            WoWMovement.MoveStop();
                            WoWMovement.Face();
                        }
                    }
                }
            }

            //
            /////////////////////
            //In a party, Not Leader, Not in BG
            /////////////////////
            //

            if ((InParty()) && (!IsLeader()) && (!InBg()))
            {
                /******DPS Only or Heal + DPS******/
                if ((!Healbot) || (HealbotDps))
                {
                    if (!_pullSpam)
                        Slog("#RaF : Leader is pulling#");

                    if (Me.PartyMember1.Distance > num4)
                    {
                        Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                          ObjectManager.Me.PartyMember1.Location,
                                                                          DpsDistance));
                        return;
                    }
                }

                /******Dedicated Healer******/
                if ((Healbot) && (!HealbotDps))
                {
                    if (!_pullSpam)
                        Slog("#RaF : Leader is pulling#");

                    if ((((Healbot && HealbotDps) && (Me.ManaPercent < HealbotDpsMana)) || (Healbot && !HealbotDps)) &&
                        ((Me.PartyMember1.Distance < num) || (Me.PartyMember1.Distance > num2)))
                    {
                        if (Me.PartyMember1.Distance > num2)
                        {
                            Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                              ObjectManager.Me.PartyMember1.Location,
                                                                              HealbotDistance));
                            return;
                        }
                    }
                }
            }

        }

        #endregion

        #region Combat *HB2*

        /////////////////////////////////
        //Combat core 
        /////////////////////////////////

        public override void Combat()
        {
            _pullTimer.Reset();

            _pullSpam = false;

            /******Battleground******/

            if (InBg())
            {
                if (!PvPHealbot) //DPS only
                {
                    if (AutoAttack()) //Start Auto Attack
                        return;

                    if (SealCheck()) //If we got into battle after a rez
                        return;

                    if (PvPChecks()) //Combat Checks
                        return;

                    if (PvPRotation()) //Decide a spell to use
                        return;
                }

                if ((PvPHealbot) && (PvPHealbotDps)) //Heal + DPS
                {
                    if (AutoAttack()) //Start Auto Attack
                        return;

                    if (SealCheck()) //If we got into battle after a rez
                        return;

                    if (PvPChecks()) //Combat Checks
                        return;

                    if (PvPRotation()) //Decide a spell to use
                        return;
                }

                if ((PvPHealbot) && (!PvPHealbotDps)) //Dedicated Healer
                {

                    if (SealCheck()) //If we got into battle after a rez
                        return;

                    if (PvPHealbotChecks()) //Combat Checks
                        return;

                    if (BgHeal()) //Scan for targets to heal
                        return;
                }

            }

            else
            {
                /******Solo******/

                if (!InParty()) //Not in a party
                {
                    if (Me.GotTarget && Me.CurrentTarget != null)
                    {
                        //Slog("HP Count: " + Me.CurrentHolyPower + ".");

                        if (AutoAttack()) //Start Auto Attack
                            return;

                        if (Me.CurrentTarget.IsPlayer)
                        {
                            if (SealCheck()) //If we got into battle after a rez
                                return;

                            if (RunCheck()) //Prevent stupid movment                                        
                                return;

                            if (PvPChecks()) //Combat Checks
                                return;

                            if (PvPRotation()) //Decide a spell to use
                                return;
                        }

                        else
                        {
                            if (TargetCheck()) //Confirm our target is attacking us
                                return;

                            if (SealCheck()) //If we got into battle after a rez
                                return;

                            if (RunCheck()) //Prevent stupid movment                                        
                                return;

                            if (Checks()) //Combat Checks
                                return;

                            if (Rotation()) //Decide a spell to use
                                return;
                        }
                    }
                }

                /******Party******/

                if (InParty()) //Logic for when we are in a party (Not Raid)
                {
                    if (!IsLeader())
                    {
                        if (Healbot)
                        {
                            if (SealCheck()) //If we got into battle after a rez
                                return;

                            if (HealbotDps)
                            {
                                if (AutoAttack()) //Start Auto Attack
                                    return;

                                RaFTargetCheck();
                            }

                            if (HealReactor())
                                return; //Self Healz always come first

                            if (RaFRange()) //Range Checks
                                return;

                            if (RaFHeal()) //Heal Checks
                                return;

                            if (HealbotDps)
                            {
                                RaFRotation();
                                return;
                            }
                        }

                        else //Not a party healer
                        {
                            if (Me.GotTarget && Me.CurrentTarget != null)
                            {
                                if (AutoAttack()) //Start Auto Attack
                                    return;

                                if (Me.CurrentTarget.IsPlayer)
                                {
                                    if (SealCheck()) //If we got into battle after a rez
                                        return;

                                    if (RunCheck()) //Prevent stupid movment                                        
                                        return;

                                    if (PvPChecks()) //Combat Checks
                                        return;

                                    if (PvPRotation()) //Decide a spell to use
                                        return;
                                }

                                else
                                {
                                    if (SealCheck()) //If we got into battle after a rez
                                        return;

                                    if (RunCheck()) //Prevent stupid movment                                        
                                        return;

                                    if (Checks()) //Combat Checks
                                        return;

                                    if (Rotation()) //Decide a spell to use
                                        return;
                                }
                            }
                        }
                    }

                    else //We are the leader
                    {
                        if (Me.GotTarget && Me.CurrentTarget != null)
                        {
                            if (AutoAttack()) //Start Auto Attack
                                return;

                            if (Me.CurrentTarget.IsPlayer)
                            {
                                if (SealCheck()) //If we got into battle after a rez
                                    return;

                                if (RunCheck()) //Prevent stupid movment                                        
                                    return;

                                if (PvPChecks()) //Combat Checks
                                    return;

                                if (PvPRotation()) //Decide a spell to use
                                    return;
                            }

                            else
                            {
                                if (SealCheck()) //If we got into battle after a rez
                                    return;

                                if (RunCheck()) //Prevent stupid movment                                        
                                    return;

                                if (Checks()) //Combat Checks
                                    return;

                                if (Healbot)//Only
                                {
                                    if (RaFHeal()) //Heal Checks
                                        return;
                                }

                                if (Rotation()) //Decide a spell to use
                                    return;
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region PVE Logic *HB2*

        #region PVE Opener *4.0 Cata*

        /////////////////////////////////
        //Decide an opener
        /////////////////////////////////

        public bool Opener()
        {

            if (OpenWithHoR)
            {
                if (SpellManager.CanCast("Hand of Reckoning"))
                {
                    Slog("#Casting Opener : Hand of Reckoning#");
                    WoWMovement.MoveStop();
                    WoWMovement.Face();
                    Thread.Sleep(125);

                    HoR(); //Do Opener               

                    if (PlayerAlert)
                        ScanForPlayers(); //Find players near



                    return true;
                }
            }


            if (OpenWithEx)
            {
                if (SpellManager.CanCast("Exorcism"))
                {
                    Slog("#Casting Opener : Exorcism#");
                    WoWMovement.MoveStop();
                    WoWMovement.Face();
                    Thread.Sleep(125);

                    Exorcism(); //Do Opener 

                    if (PlayerAlert)
                        ScanForPlayers(); //Find players near



                    return true;
                }
            }

            if (OpenWithHs)
            {
                if (SpellManager.CanCast("Holy Shock"))
                {
                    Slog("#Casting Opener : Holy Shock#");
                    WoWMovement.MoveStop();
                    WoWMovement.Face();
                    Thread.Sleep(125);

                    HolyShockDps(); //Do Opener               

                    if (PlayerAlert)
                        ScanForPlayers(); //Find players near



                    return true;
                }
            }

            if (OpenWithAs)
            {
                if (SpellManager.CanCast("Avenger's Shield"))
                {
                    Slog("#Casting Opener : Avenger's Shield#");
                    WoWMovement.MoveStop();
                    WoWMovement.Face();
                    Thread.Sleep(125);

                    As(); //Do Opener               

                    if (PlayerAlert)
                        ScanForPlayers(); //Find players near



                    return true;
                }
            }

            if (OpenWithJudgement)
            {
                if (SpellManager.CanCast("Judgement"))
                {
                    Slog("#Casting Opener : Judgement#");

                    Judgement(); //Do Opener

                    //PullAdds();

                    if (PlayerAlert)
                        ScanForPlayers(); //Find players near



                    return true;
                }
            }

            return false;
        }

        #endregion

        #region PVE PlayerDetector *4.0 Cata*

        //Credit to Hawker for ScanForPlayers()

        private static void ScanForPlayers()
        {
            new List<WoWUnit>();
            foreach (WoWObject player in ObjectManager.ObjectList)
            {
                if ((int)player.Type != 4 || player.Guid == Me.Guid) continue;
                WoWPlayer newPlayer = player.ToPlayer();

                if (newPlayer.Distance >= PlayerDetectorRange) continue;
                double d = Math.Round(newPlayer.Distance, 1);
                Slog("#Player Named: " + newPlayer.Name + ", is " + d + " yards away.#");
                SystemSounds.Exclamation.Play();
            }
            return;
        }

        #endregion

        #region PvE Flee Reactor *4.0 Cata*

        /////////////////////////////////
        //This will detect runners and deal with them
        /////////////////////////////////

        public bool FleeReactor()
        {
            if (Me.CurrentTarget.Fleeing) //If target starts to cast, move in.
            {
                if (!_runnerSpam)
                {
                    Slog("#Target is running#");
                    _runnerSpam = true;

                }

                /******Interupts******/

                if (Me.CurrentTarget.Distance <= 29)
                {
                    if (UseHoRRunners)
                    {
                        if (SpellManager.CanCast("Hand of Reckoning"))
                        {
                            WoWMovement.Face();
                            HoR();
                            return true;
                        }
                    }

                }

                if (Me.CurrentTarget.Distance <= 19)
                {
                    if (RepentanceRunners)
                    {
                        if (SpellManager.CanCast("Repentance"))
                        {
                            WoWMovement.Face();
                            Repentance();
                            return true;
                        }
                    }

                }

                if (Me.CurrentTarget.Distance <= 4)
                {
                    if (HoJRunners)
                    {
                        if (SpellManager.CanCast("Hammer of Justice"))
                        {
                            WoWMovement.Face();
                            HoJ();
                            return true;
                        }
                    }

                }

                /******Ranged Spells******/

                if (CasterRunners)
                {

                    if (Me.CurrentTarget.HealthPercent < 20)
                    {
                        if (UseHoW)
                        {
                            if (SpellManager.CanCast("Hammer of Wrath")) //Hammer of Wrath. Finishing move
                            {
                                HoW();
                                return true;
                            }
                        }
                    }

                    if (Me.CurrentTarget.Distance <= JudgementRange)
                    {
                        if (UseJudgement)
                        {
                            if (SpellManager.CanCast("Judgement"))
                            {
                                Judgement();
                                return true;
                            }
                        }
                    }

                    if (Me.CurrentTarget.Distance <= 19)
                    {
                        if (HolyShockDPS)
                        {
                            if (SpellManager.CanCast("Holy Shock"))
                            {
                                HolyShockDps();
                                return true;
                            }
                        }
                    }

                    if (Me.CurrentTarget.Distance <= 29)
                    {
                        if (UseAs)
                        {
                            if (SpellManager.CanCast("Avenger's Shield"))
                            //Interupt the cast if Avenger's Shield is available
                            {
                                As();
                                return true;
                            }
                        }
                    }

                    if (Me.CurrentTarget.Distance <= 29)
                    {
                        if (UseExorcism)
                        {
                            if (SpellManager.CanCast("Exorcism"))
                            {
                                Exorcism();
                                return true;
                            }
                        }
                    }

                }

                /******Follow Target******/

                if (Me.CurrentTarget.Distance > 1.50) //We need to get in meele range first
                {
                    Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                      ObjectManager.Me.CurrentTarget.Location, 1.0f));
                    return true;
                }
            }

            _runnerSpam = false;

            return false;

        }

        #endregion

        #region PVE Checks *HB2*

        /////////////////////////////////
        //Various Combat Checks
        /////////////////////////////////

        public bool Checks()
        {
            WoWMovement.Face();

            if (Me.Stunned) //Get Unstunned
            {
                if (UseEm)
                {
                    if (SpellManager.CanCast("Every Man for Himself"))
                    {
                        Em();
                        return true;
                    }
                }
            }

            if (UseAoW)
                AoWCheck(); //Detect Art of War  

            if (UseDenounce)
                DenounceCheck(); //Detect Denounce

            if (HealReactor()) //Before doing anything. Do we need a heal?
                return true;

            if (FleeReactor()) //Target is running, stay in range.
                return true;

            if (CastReactor()) //If target is casting
                return true;

            if (MoveToMelee()) //Stay in Melee at all times
                return true;

            if (DetectAdds)
                GetAdds(); //Check for adds while in combat             

            if (_gotAdds) //Check for adds before casting a spell
            {
                if (AddsReactor())
                    return true;
            }

            if (PoisonReactor()) //Cure Poisons
                return true;

            return false;
        }

        #endregion

        #region PVE Run Check *4.0 Cata*

        /////////////////////////////////
        //Try to fix HB Movement Bugs
        /////////////////////////////////

        public bool RunCheck()
        {
            if (!_movingToMelee)
            {
                if (((Me.IsMoving) && (!Me.CurrentTarget.Fleeing) && (!Me.CurrentTarget.IsCasting)) ||
                    (Me.IsMoving) && (!Me.GotTarget)) //Make sure we arent running all over the place
                {
                    WoWMovement.MoveStop();
                    Thread.Sleep(125);

                    if (Me.GotTarget)
                        WoWMovement.Face();

                    return true;
                }
            }

            return false;
        }

        #endregion

        #region PVE Heal Reactor *4.0 Cata*

        /////////////////////////////////
        //Decide a Heal
        /////////////////////////////////

        public bool HealReactor()
        {
            if (_gotAdds) //Do we have adds?
            {
                if (Me.HealthPercent <= DivineShieldHealth) //Divine Shield 
                {

                    if (SpellManager.CanCast("Divine Shield"))
                    {
                        Slog("#Time to hit the OH SH*T Button!#");
                        DivineShield();
                        return true;

                    }
                }

                if (Me.HealthPercent <= LayHealth) //Cast Lay on Hands
                {
                    if (UseLoH)
                    {
                        if (SpellManager.CanCast("Lay on Hands"))
                        {
                            Slog("#Health is Low! Lay on Hands#");
                            LoH();
                            return true;
                        }
                    }
                }

                if (Me.HealthPercent <= PotionHealth) //Potion
                {
                    if (UsePotion)
                    {
                        UsePotionIfAvailable();
                        return true;
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

                if (UseDpAdds) //Use Divine Protection when we have adds
                {
                    if (SpellManager.CanCast("Divine Protection"))
                    {
                        Dp();
                        return true;
                    }
                }

                else
                {
                    if (Me.HealthPercent <= DpHealth)
                    {
                        if (UseDp)
                        {
                            if (SpellManager.CanCast("Divine Protection")) //Use Divine Protection when low on health
                            {
                                Dp();
                                return true;
                            }
                        }
                    }
                }

                if (NaaruAdds) //Use Naaru when we have adds
                {
                    if (UseNaaru)
                    {
                        if (SpellManager.CanCast("Gift of the Naaru"))
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
                            if (SpellManager.CanCast("Gift of the Naaru")) //Use Divine Protection when low on health
                            {
                                Naaru();
                                return true;
                            }
                        }
                    }
                }

                if (SfAdds) //Use Naaru when we have adds
                {
                    if (UseSf)
                    {
                        if (SpellManager.CanCast("Stoneform"))
                        {
                            Sf();
                            return true;
                        }
                    }
                }

                else
                {
                    if (Me.HealthPercent <= SfHealth)
                    {
                        if (UseSf)
                        {
                            if (SpellManager.CanCast("Stoneform")) //Use Stoneform when low on health
                            {
                                Sf();
                                return true;
                            }
                        }
                    }
                }

                if (UseFoLAdds) //Do we want to use FoL with adds?
                {
                    if (Me.CurrentTarget.HealthPercent > TargetMinHeal) //Make sure target has enoph health
                    {

                        if ((Me.HealthPercent <= CombatFoL) &&
                            ((Me.HealthPercent > CombatHl) || (Me.HealthPercent > WoGHealth) ||
                             (Me.HealthPercent > LoDHealth) || (Me.HealthPercent > DlHealth) ||
                             (Me.HealthPercent > HrHealth))) //Flash of Light
                        {
                            if (SpellManager.CanCast("Flash of Light"))
                            {
                                FoL();
                                return true;
                            }
                        }

                    }

                }

                if (Me.HealthPercent <= WoGHealth) //Word of Glory
                {

                    if ((SpellManager.CanCast("Word of Glory")) && (Me.CurrentHolyPower >= WoGhp))
                    {

                        WoG();
                        return true;
                    }

                }

                if (Me.HealthPercent <= LoDHealth) //Light of Dawn
                {

                    if ((SpellManager.CanCast("Light of Dawn")) && (Me.CurrentHolyPower >= LoDhp))
                    {

                        WoG();
                        return true;
                    }

                }

                if (Me.HealthPercent <= DlHealth) //Divine Light
                {
                    if (Me.CurrentTarget.HealthPercent > TargetMinHeal) //Make sure target has enoph health
                    {
                        if (SpellManager.CanCast("Divine Light"))
                        {
                            if (HoJHeal)
                            {
                                if (SpellManager.CanCast("Hammer of Justice"))
                                    HoJ(); //Stun them while we cast
                            }

                            DivineLight();
                            return true;
                        }
                    }
                }

                if (Me.HealthPercent <= HrHealth) //Divine Light
                {
                    if (Me.CurrentTarget.HealthPercent > TargetMinHeal) //Make sure target has enoph health
                    {
                        if (SpellManager.CanCast("Holy Radiance"))
                        {
                            if (HoJHeal)
                            {
                                if (SpellManager.CanCast("Hammer of Justice"))
                                    HoJ(); //Stun them while we cast
                            }

                            Hr();
                            return true;
                        }
                    }
                }


                if (Me.HealthPercent <= CombatHl) //Holy Light
                {
                    if (Me.CurrentTarget.HealthPercent > TargetMinHeal) //Make sure target has enoph health
                    {
                        if (SpellManager.CanCast("Holy Light")) //Holy Light
                        {
                            if (HoJHeal)
                            {
                                if (SpellManager.CanCast("Hammer of Justice"))
                                    HoJ(); //Stun them while we cast
                            }

                            HolyLight(); //Cast Holy Light
                            return true;
                        }
                    }
                }

                if (Me.HealthPercent <= HsHealth) //Holy Shock
                {
                    if (Me.CurrentTarget.HealthPercent > TargetMinHeal) //Make sure target has enoph health
                    {
                        if (SpellManager.CanCast("Holy Shock"))
                        {
                            if (HoJHeal)
                            {
                                if (SpellManager.CanCast("Hammer of Justice"))
                                    HoJ(); //Stun them while we cast
                            }

                            HolyShockHeal();
                            return true;
                        }
                    }
                }

                return false;
            }

            //No Adds

            if (Me.HealthPercent <= DivineShieldHealth) //Divine Shield 
            {

                if (SpellManager.CanCast("Divine Shield"))
                {
                    Slog("#Time to hit the OH SH*T Button!#");
                    DivineShield();
                    WoWMovement.MoveStop();
                    Thread.Sleep(100);
                    if (SpellManager.CanCast("Holy Light"))
                    {
                        HolyLight(); //Cast Holy Light 
                        return true;
                    }
                }

            }

            if (Me.HealthPercent <= LayHealth) //Cast Lay on Hands
            {
                if (UseLoH)
                {
                    if (SpellManager.CanCast("Lay on Hands"))
                    {
                        Slog("#Health is Low! Lay on Hands#");
                        LoH();
                        return true;
                    }
                }
            }

            if (Me.HealthPercent <= PotionHealth) //Potion
            {
                if (UsePotion)
                {
                    UsePotionIfAvailable();
                    return true;
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

            if (Me.HealthPercent <= DpHealth) //Divine Protection
            {
                if (UseDp)
                {
                    if (Me.CurrentTarget.HealthPercent <= DpTargetHealth)
                    {
                        if (SpellManager.CanCast("Divine Protection"))
                        {

                            Dp();
                            return true;
                        }
                    }
                }
            }

            if (Me.HealthPercent <= SfHealth)
            {
                if ((UseSf) && (!SfAdds))
                {
                    if (SpellManager.CanCast("Stoneform")) //Use Stoneform when low on health
                    {
                        Sf();
                        return true;
                    }
                }
            }

            if (Me.HealthPercent <= NaaruHealth) //Use Naaru when low on health
            {
                if (UseNaaru)
                {
                    if (SpellManager.CanCast("Gift of the Naaru"))
                    {
                        Naaru();
                        return true;
                    }
                }
            }

            if ((Me.HealthPercent <= CombatFoL) && ((Me.HealthPercent > CombatHl) || (Me.HealthPercent > WoGHealth) || (Me.HealthPercent > LoDHealth) || (Me.HealthPercent > DlHealth) || (Me.HealthPercent > HrHealth))) //Flash of Light
            {
                if (SpellManager.CanCast("Flash of Light"))
                {
                    FoL();
                    return true;
                }
            }

            if (Me.HealthPercent <= WoGHealth) //Word of Glory
            {

                if ((SpellManager.CanCast("Word of Glory")) && (Me.CurrentHolyPower >= WoGhp))
                {

                    WoG();
                    return true;
                }

            }

            if (Me.HealthPercent <= LoDHealth) //Light of Dawn
            {

                if ((SpellManager.CanCast("Light of Dawn")) && (Me.CurrentHolyPower >= LoDhp))
                {

                    LoD();
                    return true;
                }

            }

            if (Me.HealthPercent <= DlHealth) //Divine Light
            {
                if (Me.CurrentTarget.HealthPercent > TargetMinHeal) //Make sure target has enoph health
                {
                    if (SpellManager.CanCast("Divine Light"))
                    {
                        if (HoJHeal)
                        {
                            if (SpellManager.CanCast("Hammer of Justice"))
                                HoJ(); //Stun them while we cast
                        }

                        DivineLight();
                        return true;
                    }
                }
            }

            if (Me.HealthPercent <= HrHealth) //Holy Radiance
            {
                if (Me.CurrentTarget.HealthPercent > TargetMinHeal) //Make sure target has enoph health
                {
                    if (SpellManager.CanCast("Holy Radiance"))
                    {
                        if (HoJHeal)
                        {
                            if (SpellManager.CanCast("Hammer of Justice"))
                                HoJ(); //Stun them while we cast
                        }

                        Hr();
                        return true;
                    }
                }
            }


            if (Me.HealthPercent <= CombatHl) //Holy Light
            {
                if (Me.CurrentTarget.HealthPercent > TargetMinHeal) //Make sure target has enoph health
                {
                    if (SpellManager.CanCast("Holy Light")) //Holy Light
                    {
                        if (HoJHeal)
                        {
                            if (SpellManager.CanCast("Hammer of Justice"))
                                HoJ(); //Stun them while we cast
                        }

                        HolyLight(); //Cast Holy Light
                        return true;
                    }
                }
            }

            if (Me.HealthPercent <= HsHealth) //Divine Light
            {
                if (Me.CurrentTarget.HealthPercent > TargetMinHeal) //Make sure target has enoph health
                {
                    if (SpellManager.CanCast("Holy Shock"))
                    {
                        if (HoJHeal)
                        {
                            if (SpellManager.CanCast("Hammer of Justice"))
                                HoJ(); //Stun them while we cast
                        }

                        HolyShockHeal();
                        return true;
                    }
                }
            }

            return false;
        }

        #endregion

        #region PVE Cast Reactor *4.0 Cata*

        /////////////////////////////////
        //This will detect casters and deal with them
        /////////////////////////////////

        public bool CastReactor()
        {
            if (Me.CurrentTarget.IsCasting) //If target starts to cast, move in.
            {
                if (!_casterSpam)
                {
                    Slog("#Target is casting#");
                    _casterSpam = true;

                }

                /******Interupts******/

                if (Me.CurrentTarget.Distance <= 4)
                {
                    if (HoJCasters)
                    {
                        if (SpellManager.CanCast("Hammer of Justice")) //Interupt the cast if HoJ is available
                        {
                            HoJ();
                            return true;
                        }
                    }

                    if (UseRebuke)
                    {
                        if (SpellManager.CanCast("Rebuke")) //Interupt the cast if Rebuke is available
                        {
                            Rebuke();
                            return true;
                        }
                    }
                }

                if (Me.CurrentTarget.Distance <= 8)
                {
                    if (UseAt)
                    {

                        if (SpellManager.CanCast("Arcane Torrent"))
                        {
                            At();
                            return true;
                        }
                    }
                }

                if (Me.CurrentTarget.Distance <= 19)
                {
                    if (UseRepentance)
                    {
                        if (SpellManager.CanCast("Repentance")) //Interupt the cast if Repentance is available
                        {
                            Repentance();
                            return true;
                        }
                    }
                }

                if (Me.CurrentTarget.Distance <= 29)
                {
                    if (ASCasters)
                    {
                        if (SpellManager.CanCast("Avenger's Shield")) //Interupt the cast if Avenger's Shield is available
                        {
                            As();
                            return true;
                        }
                    }
                }

                /******Ranged Spells******/

                if (CasterRanged)
                {

                    if (Me.CurrentTarget.Distance <= JudgementRange)
                    {
                        if (UseJudgement)
                        {
                            if (SpellManager.CanCast("Judgement"))
                            {
                                Judgement();
                                return true;
                            }
                        }
                    }

                    if (Me.CurrentTarget.Distance <= 19)
                    {
                        if (HolyShockDPS)
                        {
                            if (SpellManager.CanCast("Holy Shock"))
                            {
                                HolyShockDps();
                                return true;
                            }
                        }
                    }

                    if (Me.CurrentTarget.Distance <= 29)
                    {
                        if (UseExorcism)
                        {
                            if (SpellManager.CanCast("Exorcism"))
                            {
                                Exorcism();
                                return true;
                            }
                        }
                    }

                }

            }

            _casterSpam = false;

            return false;
        }

        #endregion

        #region PVE Move to Melee *4.0 Cata*

        /////////////////////////////////
        //Checks range to target and moves accordingly
        /////////////////////////////////

        public bool MoveToMelee()
        {
            if (Me.CurrentTarget.Distance > 3.75) //We need to get in meele range first
            {

                if (Me.CurrentTarget.Distance > 5)
                {
                    if (UseJudgement)
                    {
                        if (Me.CurrentTarget.Distance < 9)
                        {
                            if (SpellManager.CanCast("Judgement"))
                            {
                                Judgement();
                                return true;
                            }
                        }
                    }

                    if (UseExorcism)
                    {
                        if (Me.CurrentTarget.Distance < 29)
                        {
                            if (SpellManager.CanCast("Exorcism"))
                            {
                                Exorcism();
                                return true;
                            }
                        }
                    }

                    if (UseAs)
                    {
                        if (Me.CurrentTarget.Distance < 29)
                        {
                            if (SpellManager.CanCast("Avenger's Shield"))
                            {
                                As();
                                return true;
                            }
                        }
                    }
                }

                if (WaitForApproach) //Stand still
                {
                    if (ApproachDetector())
                        return true;

                    _movingToMelee = true;
                    Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                      ObjectManager.Me.CurrentTarget.Location, 2.5f));
                    return true;
                }

                _movingToMelee = true;
                Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                  ObjectManager.Me.CurrentTarget.Location, 2.5f));
                return true;
            }

            if (Me.CurrentTarget.Distance < 2.75)
            {
                _movingToMelee = false;
                WoWMovement.MoveStop(); //Dont Move!
                WoWMovement.Face(); //Just in case we arent facing the right direction
                return false;
            }

            return false;
        }

        #endregion

        #region PVE Get Adds *4.0 Cata*

        //Credit to Hawker for getAdds()

        private void GetAdds()
        {
            List<WoWObject> longList = ObjectManager.ObjectList;
            //const double hp = 10000;

            List<WoWUnit> mobList = (from thing in longList where (int)thing.Type == 3 select thing.ToUnit()).ToList();

            List<WoWUnit> enemyMobList =
                mobList.Where(
                    thing =>
                    (thing.Guid != Me.Guid) && (thing.IsTargetingMeOrPet) &&
                    (!thing.Name.ToLower().Contains("rotting w"))).ToList();

            if (enemyMobList.Count >= AddsCount)
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

            /*List<WoWUnit> enemyMobList =
                mobList.Where(
                    thing =>
                    ((thing.Aggro || thing.PetAggro) && (thing.IsTargetingMeOrPet) &&
                    (!thing.Name.ToLower().Contains("rotting w"))).ToList();*/


        }

        #endregion

        #region PVE Adds Reactor *4.0 Cata*

        /////////////////////////////////
        //What to do when we have adds
        /////////////////////////////////

        public bool AddsReactor()
        {
            /******DPS Enhancer******/


            if (InquisitionAdds)
            {

                if (Me.CurrentHolyPower >= InquisitionHp)
                {
                    if (SpellManager.CanCast("Inquisition"))
                    {
                        Inquisition();
                        return true;
                    }
                }

            }

            if (UseAvengingWrathAdds)
            {
                if (SpellManager.CanCast("Avenging Wrath"))
                {
                    Aw();
                    return true;
                }
            }


            if (UseDivinePlea)
            {
                if (!UseDivinePleaAdds) // Divine Plea
                {
                    if (SpellManager.CanCast("Divine Plea"))
                    {
                        DivinePlea();
                        return true;
                    }
                }
            }

            if (UseDfAdds)
            {
                if (SpellManager.CanCast("Divine Favor")) //Divine Favor
                {
                    Df();
                    return true;
                }
            }

            if (UseZealotryAdds)
            {

                if (SpellManager.CanCast("Zealotry"))
                {
                    Zealotry();
                    return true;
                }

            }


            /******Holy Power Spells******/

            if (Me.CurrentTarget.Distance <= 4)
            {



                if ((UseDsAdds) && (Me.CurrentHolyPower >= Dshp))
                {

                    if (SpellManager.CanCast("Divine Storm"))
                    {
                        Ds();
                        return true;
                    }

                }

            }

            /******Other Spells******/


            if (UseAsAdds)
            {
                if (SpellManager.CanCast("Avenger's Shield")) //Avenger's Shield
                {
                    As();
                    return true;
                }
            }

            if (Me.CurrentTarget.Distance <= 10)
            {

                if (UseConsecrationAdds)
                {
                    if (SpellManager.CanCast("Consecration"))
                    {
                        Consecration();
                        return true;
                    }
                }


                if (UseHwAdds)
                {
                    if (SpellManager.CanCast("Holy Wrath")) //Holy Wrath
                    {
                        Hw();
                        return true;
                    }
                }
            }

            return false;
        }

        #endregion

        #region PVE Poison Reactor *4.0 Cata*

        /////////////////////////////////
        //Deal with Poisons & Disease
        /////////////////////////////////

        public bool PoisonReactor()
        {
            if ((UseCleanse) && (CleanseMagic))
            {
                foreach (var pair in Me.Auras)
                {
                    WoWDispelType checkDebuff = pair.Value.Spell.DispelType;
                    if (checkDebuff.Equals(WoWDispelType.Disease) || checkDebuff.Equals(WoWDispelType.Poison) ||
                        checkDebuff.Equals(WoWDispelType.Magic))
                    {
                        if (SpellManager.CanCast("Cleanse"))
                        {
                            Slog("Cleanse : " + pair.Key);
                            Cleanse();
                            return true;
                        }
                    }
                }
            }

            if ((UseCleanse) && (!CleanseMagic))
            {
                foreach (var pair in Me.Auras)
                {
                    WoWDispelType checkDebuff = pair.Value.Spell.DispelType;
                    if (checkDebuff.Equals(WoWDispelType.Disease) || checkDebuff.Equals(WoWDispelType.Poison))
                    {
                        if (SpellManager.CanCast("Cleanse"))
                        {
                            Slog("Cleanse : " + pair.Key);
                            Cleanse();
                            return true;
                        }
                    }
                }
            }

            return false;

        }

        #endregion

        #region PVE Approach Detector *4.0 Cata*

        /////////////////////////////////
        //This will detect if a mob is approaching
        //
        /////////////////////////////////

        public bool ApproachDetector()
        {
            if (Me.CurrentTarget.Distance > 4.5)
            {
                double aDone = Me.CurrentTarget.Distance;
                Thread.Sleep(125);
                double aDtwo = Me.CurrentTarget.Distance;

                if (aDone > aDtwo) //Difference between the two
                {
                    if (((IsLeader()) && (InParty())) || (!InParty())) //Not in a party, or we are the leader.
                    {
                        if (!_approachSpam)
                            Slog("#Target is Approaching#");

                        if (ApproachNone)
                            return true;

                        if (ApproachJudgement)
                        {
                            if (Me.CurrentTarget.Distance <= 9)
                            {

                                if (SpellManager.CanCast("Judgement"))
                                {
                                    Judgement();
                                    return true;
                                }

                            }
                        }

                        if (ApproachHs)
                        {
                            if (Me.CurrentTarget.Distance <= 19)
                            {

                                if (SpellManager.CanCast("Holy Shock"))
                                {
                                    HolyShockDps();
                                    return true;
                                }

                            }
                        }

                        if (ApproachEx)
                        {
                            if (Me.CurrentTarget.Distance <= 29)
                            {

                                if (SpellManager.CanCast("Exorcism"))
                                {
                                    Exorcism();
                                    return true;
                                }

                            }
                        }

                        if (ApproachAs)
                        {
                            if (Me.CurrentTarget.Distance <= 29)
                            {

                                if (SpellManager.CanCast("Avenger's Shield"))
                                {
                                    As();
                                    return true;
                                }

                            }
                        }

                    }

                    else
                    {
                        if (!_approachSpam)
                            Slog("#Target is Approaching Leader...#");
                    }
                }

                _approachSpam = false;
            }

            return false;
        }

        #endregion

        #region PVE Rotation *4.0 Cata*

        /////////////////////////////////
        //In Combat rotation planner
        /////////////////////////////////

        public bool Rotation()
        {

            /******Finishing Move******/

            if (Me.CurrentTarget.HealthPercent < 20)
            {
                if (UseHoW)
                {
                    if (SpellManager.CanCast("Hammer of Wrath")) //Hammer of Wrath. Finishing move
                    {
                        HoW();
                        return true;
                    }
                }
            }

            /******DPS Enhancer******/

            if (UseInquisition)
            {
                if (!InquisitionAdds)
                {

                    if (Me.CurrentHolyPower >= InquisitionHp)
                    {
                        if (SpellManager.CanCast("Inquisition"))
                        {
                            Inquisition();
                            return true;
                        }
                    }

                }
            }

            if (UseAvengingWrath) //Avenging Wrath
            {
                if (!UseAvengingWrathAdds)
                {
                    if (SpellManager.CanCast("Avenging Wrath"))
                    {
                        Aw();
                        return true;
                    }
                }
            }

            if (UseDivinePlea)
            {
                if (!UseDivinePleaAdds) // Divine Plea
                {
                    if (SpellManager.CanCast("Divine Plea"))
                    {
                        DivinePlea();
                        return true;
                    }
                }
            }

            if ((UseDf) && (!UseDfAdds))
            {
                if (SpellManager.CanCast("Divine Favor")) //Divine Favor
                {
                    Df();
                    return true;
                }
            }

            if ((UseZealotry) && (!UseZealotryAdds))
            {

                if (SpellManager.CanCast("Zealotry"))
                {
                    Zealotry();
                    return true;
                }

            }

            if (Me.CurrentTarget.Distance <= 9)
            {

                if (UseJudgement) //Judgement
                {
                    if (SpellManager.CanCast("Judgement"))
                    {
                        Judgement();
                        return true;
                    }
                }
            }

            /******Instant Exorcism******/

            if (Me.CurrentTarget.Distance <= 29)
            {

                if (_aoWProc) //Burn Exorcism on AoW?
                {

                    if (SpellManager.CanCast("Exorcism"))
                    {
                        Exorcism();
                        return true;
                    }
                }

                if (_denounceProc) //Burn Exorcism on Denounce?
                {

                    if (SpellManager.CanCast("Exorcism"))
                    {
                        Exorcism();
                        return true;
                    }
                }

            }

            /******Holy Power Spells******/

            if (Me.CurrentTarget.Distance <= 4)
            {

                if ((UseTV) && (Me.CurrentHolyPower >= TVHP)) //Templar's Verdict
                {

                    if (SpellManager.CanCast("Templar's Verdict"))
                    {
                        TV();
                        return true;
                    }

                }


                if ((UseShieldR) && (Me.CurrentHolyPower >= SoRhp)) //Shield of the Righteous
                {
                    if (Me.CurrentTarget.Distance <= 4)
                    {
                        if (SpellManager.CanCast("Shield of the Righteous"))
                        {
                            ShieldR();
                            return true;
                        }
                    }
                }

                if ((UseDs) && (Me.CurrentHolyPower >= Dshp))
                {
                    if (!UseDsAdds)
                    {
                        if (SpellManager.CanCast("Divine Storm"))
                        {
                            Ds();
                            return true;
                        }
                    }
                }



                /******Holy Power Generating Spells******/

                if ((UseCs)) // Crusader Strike
                {
                    if (SpellManager.CanCast("Crusader Strike"))
                    {
                        Cs();
                        return true;
                    }
                }

                if (UseHotR) //Hammer of the Righteous
                {
                    if (SpellManager.CanCast("Hammer of the Righteous"))
                    {
                        HotR();
                        return true;
                    }
                }

            }

            if (HolyShockDPS) //Holy Shock
            {
                if (SpellManager.CanCast("Holy Shock"))
                {
                    HolyShockDps();
                    return true;
                }
            }

            /******Other Spells******/

            if (UseAs)
            {
                if (!UseAsAdds)
                {
                    if (SpellManager.CanCast("Avenger's Shield")) //Avenger's Shield
                    {
                        As();
                        return true;
                    }
                }
            }

            if (Me.CurrentTarget.Distance <= 29)
            {
                if ((UseExorcism) && (!UseAoW) && (!UseDenounce))
                {

                    if (Me.CurrentTarget.HealthPercent > TargetMinEx)
                    //Dont cast if target is near death and not using AoW
                    {
                        if (SpellManager.CanCast("Exorcism"))
                        {
                            Exorcism();
                            return true;
                        }
                    }

                }
            }

            if (Me.CurrentTarget.Distance <= 10)
            {
                if (UseConsecration) //Consecration
                {
                    if (!UseConsecrationAdds)
                    {
                        if (SpellManager.CanCast("Consecration"))
                        {
                            Consecration();
                            return true;
                        }
                    }
                }

                if ((UseHw) && (!UseHwAdds))
                {
                    if (SpellManager.CanCast("Holy Wrath")) //Holy Wrath
                    {
                        Hw();
                        return true;
                    }
                }
            }

            return false;
        }

        #endregion

        #region PvE Target Check *4.0 Cata*

        /////////////////////////////////
        //If we have a target, but our target is not attacking us. Clear, stop, retarget.
        /////////////////////////////////        

        public bool TargetCheck()
        {
            if (Me.GotTarget)
            {
                if (Me.CurrentTarget.Dead)
                {
                    Me.ClearTarget();
                    return true;
                }

                if (!Me.CurrentTarget.Fleeing)
                {
                    if (Me.CurrentTarget.CurrentTargetGuid != Me.Guid)
                    {
                        Thread.Sleep(500); //HB is somtimes slow on pull

                        if (Me.CurrentTarget.CurrentTargetGuid != Me.Guid)
                        {
                            if ((!Me.CurrentTarget.Fleeing) || (!Me.CurrentTarget.IsCasting))
                            {
                                WoWMovement.MoveStop();
                                Thread.Sleep(125);
                                Blacklist.Add(Me.CurrentTarget, TimeSpan.FromSeconds(5.00));
                                //Prevent HB from targeting twice
                                Me.ClearTarget();
                                Thread.Sleep(125);
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        #endregion

        #endregion

        #region PVP Logic *HB2*

        #region PvP Opener *4.0 Cata*

        /////////////////////////////////
        //Decide an opener
        /////////////////////////////////

        public bool PvPopener()
        {
            if (PvPOpenWithEx)
            {
                if (SpellManager.CanCast("Exorcism"))
                {
                    Slog("#Casting Opener : Exorcism#");
                    WoWMovement.MoveStop();
                    WoWMovement.Face();
                    Thread.Sleep(125);

                    Exorcism(); //Do Opener

                    return true;
                }
            }

            if (PvPOpenWithHs)
            {
                if (SpellManager.CanCast("Holy Shock"))
                {
                    Slog("#Casting Opener : Holy Shock#");
                    WoWMovement.MoveStop();
                    WoWMovement.Face();
                    Thread.Sleep(125);

                    HolyShockDps(); //Do Opener

                    return true;
                }
            }

            if (PvPOpenWithAs)
            {
                if (SpellManager.CanCast("Avenger's Shield"))
                {
                    Slog("#Casting Opener : Avenger's Shield#");
                    WoWMovement.MoveStop();
                    WoWMovement.Face();
                    Thread.Sleep(125);

                    As(); //Do Opener

                    return true;
                }
            }

            if (PvPOpenWithJudgement)
            {
                if (SpellManager.CanCast("Judgement"))
                {
                    Slog("#Casting Opener : Judgement#");

                    Judgement(); //Do Opener 
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region PvP Checks *HB2*

        /////////////////////////////////
        //Various PvP Combat Checks
        /////////////////////////////////        

        public bool PvPChecks()
        {
            WoWMovement.Face(); //Just in case we arent facing the right direction

            if (Me.Stunned) //Get Unstunned
            {
                if (PvPUseEm)
                {
                    if (SpellManager.CanCast("Every Man for Himself"))
                    {
                        Em();
                        return true;
                    }
                }
            }

            if (PvPUseAoW)
                AoWCheck(); //Detect Art of War

            if (PvPUseDenounce)
                DenounceCheck(); //Detect Denounce  

            if (PvPHealReactor()) //Before doing anything. Do we need a heal?
                return true;

            if (PvPCastReactor()) //If target is casting
                return true;

            if (PvPMoveToMelee()) //Stay in Melee
                return true;

            if (PvPPoisonReactor()) //Cure Poisons
                return true;

            return false;
        }

        #endregion

        #region PvP HealBot Checks *HB2*

        /////////////////////////////////
        //Various PvP Healbot Checks
        /////////////////////////////////        

        public bool PvPHealbotChecks()
        {
            if (Me.Stunned) //Get Unstunned
            {
                if (PvPUseEm)
                {
                    if (SpellManager.CanCast("Every Man for Himself"))
                    {
                        Em();
                        return true;
                    }
                }
            }

            if (PvPUseDenounce)
                DenounceCheck(); //Detect Denounce            

            if (PvPHealReactor()) //Before doing anything. Do we need a heal?
                return true;

            if (PvPPoisonReactor()) //Cure Poisons
                return true;

            return false;
        }

        #endregion

        #region PvP HealReactor *4.0 Cata*

        /////////////////////////////////
        //Decide a PvP Self Heal
        /////////////////////////////////

        public bool PvPHealReactor()
        {
            if (Me.HealthPercent <= PvPDivineShieldHealth) //Divine Shield 
            {
                if (!Me.Auras.ContainsKey("Forbearance"))
                {

                    if (SpellManager.CanCast("Divine Shield"))
                    {

                        Slog("#Time to hit the OH SH*T Button!#");
                        DivineShield();
                        WoWMovement.MoveStop();
                        Thread.Sleep(100);
                        if (SpellManager.CanCast("Holy Light"))
                        {
                            HolyLight(); //Cast Holy Light 
                            return true;
                        }

                    }

                }

            }

            if (Me.HealthPercent <= PvPLayHealth) //Cast Lay on Hands
            {
                if (PvPUseLoH)
                {
                    if (SpellManager.CanCast("Lay on Hands"))
                    {

                        Slog("#Health is Low! Lay on Hands#");
                        LoH();
                        return true;

                    }
                }
            }

            if (Me.HealthPercent <= PvPPotionHealth) //Potion
            {
                if (PvPUsePotion)
                {
                    UsePotionIfAvailable();
                    return true;
                }
            }

            if (Me.HealthPercent <= PvPdpHealth) //Divine Protection
            {

                if (Me.CurrentTarget.HealthPercent <= PvPdpTargetHealth)
                {
                    if (SpellManager.CanCast("Divine Protection"))
                    {

                        Dp();
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

            if (Me.HealthPercent <= PvPsfHealth)
            {
                if ((PvPUseSf))
                {
                    if (SpellManager.CanCast("Stoneform")) //Use Stoneform when low on health
                    {
                        Sf();
                        return true;
                    }
                }
            }

            if (Me.HealthPercent <= PvPNaaruHealth) //Use Naaru when low on health
            {
                if (PvPUseNaaru)
                {
                    if (SpellManager.CanCast("Gift of the Naaru"))
                    {
                        Naaru();
                        return true;
                    }
                }
            }

            if ((Me.HealthPercent <= PvPCombatFoL) && ((Me.HealthPercent > PvPCombatHl) || (Me.HealthPercent > PvPwoGHealth) || (Me.HealthPercent > PvPloDHealth) || (Me.HealthPercent > PvPdlHealth) || (Me.HealthPercent > PvPhrHealth))) //Flash of Light
            {
                if (SpellManager.CanCast("Flash of Light"))
                {
                    FoL();
                    return true;
                }
            }

            if (Me.HealthPercent <= PvPwoGHealth) //Word of Glory
            {

                if ((SpellManager.CanCast("Word of Glory")) && (Me.CurrentHolyPower >= PvPwoGhp))
                {

                    WoG();
                    return true;
                }

            }

            if (Me.HealthPercent <= PvPloDHealth) //Light of Dawn
            {

                if ((SpellManager.CanCast("Light of Dawn")) && (Me.CurrentHolyPower >= PvPloDhp))
                {

                    LoD();
                    return true;
                }

            }

            if (Me.HealthPercent <= PvPdlHealth) //Divine Light
            {
                if (Me.CurrentTarget.HealthPercent > PvPTargetMinHeal) //Make sure target has enoph health
                {
                    if (SpellManager.CanCast("Divine Light"))
                    {
                        if (PvPHoJHeal)
                        {
                            if (SpellManager.CanCast("Hammer of Justice"))
                                HoJ(); //Stun them while we cast
                        }

                        DivineLight();
                        return true;
                    }
                }
            }

            if (Me.HealthPercent <= PvPhrHealth) //Holy Radiance
            {
                if (Me.CurrentTarget.HealthPercent > PvPTargetMinHeal) //Make sure target has enoph health
                {
                    if (SpellManager.CanCast("Holy Radiance"))
                    {
                        if (PvPHoJHeal)
                        {
                            if (SpellManager.CanCast("Hammer of Justice"))
                                HoJ(); //Stun them while we cast
                        }

                        Hr();
                        return true;
                    }
                }
            }


            if (Me.HealthPercent <= PvPCombatHl) //Holy Light
            {
                if (Me.CurrentTarget.HealthPercent > PvPTargetMinHeal) //Make sure target has enoph health
                {
                    if (SpellManager.CanCast("Holy Light")) //Holy Light
                    {
                        if (PvPHoJHeal)
                        {
                            if (SpellManager.CanCast("Hammer of Justice"))
                                HoJ(); //Stun them while we cast
                        }

                        HolyLight(); //Cast Holy Light
                        return true;
                    }
                }
            }

            if (Me.HealthPercent <= PvPhsHealth) //Holy Shock
            {
                if (Me.CurrentTarget.HealthPercent > PvPTargetMinHeal) //Make sure target has enoph health
                {
                    if (SpellManager.CanCast("Holy Shock"))
                    {
                        if (PvPHoJHeal)
                        {
                            if (SpellManager.CanCast("Hammer of Justice"))
                                HoJ(); //Stun them while we cast
                        }

                        HolyShockHeal();
                        return true;
                    }
                }
            }

            return false;
        }

        #endregion

        #region PvP Cast Reactor *4.0 Cata*

        /////////////////////////////////
        //This will detect casters and deal with them
        /////////////////////////////////

        public bool PvPCastReactor()
        {
            if (Me.CurrentTarget.IsCasting) //If target starts to cast, move in.
            {
                if (!_casterSpam)
                {
                    Slog("#Target is casting#");
                    _casterSpam = true;

                }

                /******Interupts******/

                if (Me.CurrentTarget.Distance <= 4)
                {
                    if (PvPHoJCasters)
                    {
                        if (SpellManager.CanCast("Hammer of Justice")) //Interupt the cast if HoJ is available
                        {
                            HoJ();
                            return true;
                        }
                    }

                    if (PvPUseRebuke)
                    {
                        if (SpellManager.CanCast("Rebuke")) //Interupt the cast if Rebuke is available
                        {
                            Rebuke();
                            return true;
                        }
                    }
                }

                if (Me.CurrentTarget.Distance <= 8)
                {
                    if (PvPUseAt)
                    {

                        if (SpellManager.CanCast("Arcane Torrent"))
                        {
                            At();
                            return true;
                        }
                    }
                }

                if (Me.CurrentTarget.Distance <= 19)
                {
                    if (PvPUseRepentance)
                    {
                        if (SpellManager.CanCast("Repentance")) //Interupt the cast if Repentance is available
                        {
                            Repentance();
                            return true;
                        }
                    }
                }

                if (Me.CurrentTarget.Distance <= 29)
                {
                    if (PvPASCasters)
                    {
                        if (SpellManager.CanCast("Avenger's Shield")) //Interupt the cast if Avenger's Shield is available
                        {
                            As();
                            return true;
                        }
                    }
                }

                /******Ranged Spells******/

                if (PvPCasterRanged)
                {

                    if (Me.CurrentTarget.Distance <= PvPJudgementRange)
                    {
                        if (PvPUseJudgement)
                        {
                            if (SpellManager.CanCast("Judgement"))
                            {
                                Judgement();
                                return true;
                            }
                        }
                    }

                    if (Me.CurrentTarget.Distance <= 19)
                    {
                        if (PvPHolyShockDPS)
                        {
                            if (SpellManager.CanCast("Holy Shock"))
                            {
                                HolyShockDps();
                                return true;
                            }
                        }
                    }

                    if (Me.CurrentTarget.Distance <= 29)
                    {
                        if (PvPUseExorcism)
                        {
                            if (SpellManager.CanCast("Exorcism"))
                            {
                                Exorcism();
                                return true;
                            }
                        }
                    }

                }

            }

            _casterSpam = false;

            return false;
        }

        #endregion

        #region PvP Move to Melee *4.0 Cata*

        /////////////////////////////////
        //Checks range to target and moves accordingly
        /////////////////////////////////

        //private readonly Stopwatch _CleanseTimer = new Stopwatch(); //Don't spam cleanse!

        public bool PvPMoveToMelee()
        {
            if (!Me.CurrentTarget.InLineOfSight)
            {
                LoSCount++;

                if (LoSCount == 8)
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

            if ((Me.Rooted) || (Me.Stunned) || (Me.Dazed))
            {
                if (PvPUseHoF)
                {
                    if (SpellManager.CanCast("Hand of Freedom"))
                    {
                        HoF(); //Cast Hand of Freedom 
                    }
                }

                /*if ((PvPUseCleanse) && (SpellManager.CanCast("Cleanse")))
                {
                    if ((_CleanseTimer.IsRunning) && (_CleanseTimer.Elapsed.Milliseconds > 7000)) //At 7 seconds we want to reset timer
                        _CleanseTimer.Reset();

                    if (!_CleanseTimer.IsRunning) //Get the timer running right before cast
                        _CleanseTimer.Start();

                    if (_CleanseTimer.Elapsed.Milliseconds < 1000) //Only cast if timer has just started                    
                        Cleanse(); //Cast Cleanse                        

                } */


            }

            if (Me.CurrentTarget.HealthPercent < 20)
            {
                if (Me.CurrentTarget.Distance <= 29)
                {
                    if (PvPUseHoW) //Hammer of Wrath. Finishing move
                    {
                        if (SpellManager.CanCast("Hammer of Wrath"))
                        {
                            HoW();
                            return true;
                        }
                    }
                }
            }

            if (Me.CurrentTarget.Distance <= PvPJudgementRange)
            {
                if (PvPUseJudgement)
                {
                    if (SpellManager.CanCast("Judgement"))
                    {
                        Judgement();
                        return true;
                    }
                }
            }

            if (Me.CurrentTarget.Distance <= 19)
            {
                if (PvPHolyShockDPS)
                {
                    if (SpellManager.CanCast("Holy Shock"))
                    {
                        if (Me.IsMoving)
                            WoWMovement.MoveStop();

                        HolyShockDps();
                        return true;
                    }
                }
            }

            if (Me.CurrentTarget.Distance <= 29)
            {
                if (PvPUseExorcism)
                {
                    if (SpellManager.CanCast("Exorcism"))
                    {
                        if (Me.IsMoving)
                            WoWMovement.MoveStop();

                        Exorcism();
                        return true;
                    }
                }

                if (PvPUseAs)
                {
                    if (SpellManager.CanCast("Avenger's Shield"))
                    {
                        As();
                        return true;
                    }
                }
            }

            if (Me.CurrentTarget.Distance > 2.00) //We need to get in meele range first
            {

                Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                  ObjectManager.Me.CurrentTarget.Location, 1.0f));
                if (PvPInstantSpells())
                    return true;

                return true;
            }

            return false;
        }

        #endregion

        #region PvP Instant Spells *4.0 Cata*

        /////////////////////////////////
        //This will cast instant spells while moving
        /////////////////////////////////

        public bool PvPInstantSpells()
        {

            /******Finishing Move******/

            if (Me.CurrentTarget.HealthPercent < 20)
            {
                if (PvPUseHoW)
                {
                    if (SpellManager.CanCast("Hammer of Wrath")) //Hammer of Wrath. Finishing move
                    {
                        HoW();
                        return true;
                    }
                }
            }

            /******DPS Enhancer******/

            if (PvPUseInquisition)
            {

                if (Me.CurrentHolyPower >= InquisitionHp)
                {
                    if (SpellManager.CanCast("Inquisition"))
                    {
                        Inquisition();
                        return true;
                    }
                }

            }

            if (PvPUseAvengingWrath) //Avenging Wrath
            {

                if (SpellManager.CanCast("Avenging Wrath"))
                {
                    Aw();
                    return true;
                }

            }

            if (PvPUseDivinePlea)
            {

                if (SpellManager.CanCast("Divine Plea"))
                {
                    DivinePlea();
                    return true;
                }

            }

            if (PvPUseDf)
            {
                if (SpellManager.CanCast("Divine Favor")) //Divine Favor
                {
                    Df();
                    return true;
                }
            }

            if (PvPUseZealotry)
            {

                if (SpellManager.CanCast("Zealotry"))
                {
                    Zealotry();
                    return true;
                }

            }

            if (Me.CurrentTarget.Distance <= 9)
            {

                if (PvPUseJudgement) //Judgement
                {
                    if (SpellManager.CanCast("Judgement"))
                    {
                        Judgement();
                        return true;
                    }
                }
            }

            /******Instant Exorcism******/

            if (Me.CurrentTarget.Distance <= 29)
            {

                if (_aoWProc) //Burn Exorcism on AoW?
                {

                    if (SpellManager.CanCast("Exorcism"))
                    {
                        Exorcism();
                        return true;
                    }
                }

                if (_denounceProc) //Burn Exorcism on Denounce?
                {

                    if (SpellManager.CanCast("Exorcism"))
                    {
                        Exorcism();
                        return true;
                    }
                }

            }

            /******Holy Power Spells******/

            if (Me.CurrentTarget.Distance <= 4)
            {

                if ((PvPUseTV) && (Me.CurrentHolyPower >= PvPTVHP)) //Templar's Verdict
                {

                    if (SpellManager.CanCast("Templar's Verdict"))
                    {
                        TV();
                        return true;
                    }

                }


                if ((PvPUseShieldR) && (Me.CurrentHolyPower >= PvPSoRhp)) //Shield of the Righteous
                {
                    if (Me.CurrentTarget.Distance <= 4)
                    {
                        if (SpellManager.CanCast("Shield of the Righteous"))
                        {
                            ShieldR();
                            return true;
                        }
                    }
                }

                if ((PvPUseDs) && (Me.CurrentHolyPower >= PvPDshp))
                {

                    if (SpellManager.CanCast("Divine Storm"))
                    {
                        Ds();
                        return true;
                    }

                }



                /******Holy Power Generating Spells******/

                if ((PvPUseCs)) // Crusader Strike
                {
                    if (SpellManager.CanCast("Crusader Strike"))
                    {
                        Cs();
                        return true;
                    }
                }

                if (PvPUseHotR) //Hammer of the Righteous
                {
                    if (SpellManager.CanCast("Hammer of the Righteous"))
                    {
                        HotR();
                        return true;
                    }
                }

            }

            if (PvPHolyShockDPS) //Holy Shock
            {
                if (SpellManager.CanCast("Holy Shock"))
                {
                    HolyShockDps();
                    return true;
                }
            }

            /******Other Spells******/

            if (Me.CurrentTarget.Distance <= 10)
            {
                if (PvPUseConsecration) //Consecration
                {

                    if (SpellManager.CanCast("Consecration"))
                    {
                        Consecration();
                        return true;
                    }

                }

                if (PvPUseHw)
                {
                    if (SpellManager.CanCast("Holy Wrath")) //Holy Wrath
                    {
                        Hw();
                        return true;
                    }
                }
            }
            return false;
        }

        #endregion

        #region PvP Rotation *4.0 Cata*

        /////////////////////////////////
        //PvP In Combat rotation planner
        /////////////////////////////////

        public bool PvPRotation()
        {
            /******Finishing Move******/

            if (Me.CurrentTarget.HealthPercent < 20)
            {
                if (PvPUseHoW)
                {
                    if (SpellManager.CanCast("Hammer of Wrath")) //Hammer of Wrath. Finishing move
                    {
                        HoW();
                        return true;
                    }
                }
            }

            /******DPS Enhancer******/

            if (PvPUseInquisition)
            {

                if (Me.CurrentHolyPower >= PvPUseInquisitionHp)
                {
                    if (SpellManager.CanCast("Inquisition"))
                    {
                        Inquisition();
                        return true;
                    }
                }

            }

            if (PvPUseAvengingWrath) //Avenging Wrath
            {

                if (SpellManager.CanCast("Avenging Wrath"))
                {
                    Aw();
                    return true;
                }

            }

            if (PvPUseDivinePlea)
            {

                if (SpellManager.CanCast("Divine Plea"))
                {
                    DivinePlea();
                    return true;
                }

            }

            if (PvPUseDf)
            {
                if (SpellManager.CanCast("Divine Favor")) //Divine Favor
                {
                    Df();
                    return true;
                }
            }

            if (PvPUseZealotry)
            {

                if (SpellManager.CanCast("Zealotry"))
                {
                    Zealotry();
                    return true;
                }

            }

            if (Me.CurrentTarget.Distance <= 9)
            {

                if (PvPUseJudgement) //Judgement
                {
                    if (SpellManager.CanCast("Judgement"))
                    {
                        Judgement();
                        return true;
                    }
                }
            }

            /******Instant Exorcism******/

            if (Me.CurrentTarget.Distance <= 29)
            {

                if (_aoWProc) //Burn Exorcism on AoW?
                {

                    if (SpellManager.CanCast("Exorcism"))
                    {
                        Exorcism();
                        return true;
                    }
                }

                if (_denounceProc) //Burn Exorcism on Denounce?
                {

                    if (SpellManager.CanCast("Exorcism"))
                    {
                        Exorcism();
                        return true;
                    }
                }

            }

            /******Holy Power Spells******/

            if (Me.CurrentTarget.Distance <= 4)
            {

                if ((PvPUseTV) && (Me.CurrentHolyPower >= PvPTVHP)) //Templar's Verdict
                {

                    if (SpellManager.CanCast("Templar's Verdict"))
                    {
                        TV();
                        return true;
                    }

                }


                if ((PvPUseShieldR) && (Me.CurrentHolyPower >= PvPSoRhp)) //Shield of the Righteous
                {
                    if (Me.CurrentTarget.Distance <= 4)
                    {
                        if (SpellManager.CanCast("Shield of the Righteous"))
                        {
                            ShieldR();
                            return true;
                        }
                    }
                }

                if ((PvPUseDs) && (Me.CurrentHolyPower >= PvPDshp))
                {

                    if (SpellManager.CanCast("Divine Storm"))
                    {
                        Ds();
                        return true;
                    }

                }



                /******Holy Power Generating Spells******/

                if ((PvPUseCs)) // Crusader Strike
                {
                    if (SpellManager.CanCast("Crusader Strike"))
                    {
                        Cs();
                        return true;
                    }
                }

                if (PvPUseHotR) //Hammer of the Righteous
                {
                    if (SpellManager.CanCast("Hammer of the Righteous"))
                    {
                        HotR();
                        return true;
                    }
                }

            }

            if (PvPHolyShockDPS) //Holy Shock
            {
                if (SpellManager.CanCast("Holy Shock"))
                {
                    HolyShockDps();
                    return true;
                }
            }

            /******Other Spells******/

            if (PvPUseAs)
            {

                if (SpellManager.CanCast("Avenger's Shield")) //Avenger's Shield
                {
                    As();
                    return true;
                }

            }

            if (Me.CurrentTarget.Distance <= 29)
            {
                if ((PvPUseExorcism) && (!PvPUseAoW) && (!PvPUseDenounce))
                {

                    if (Me.CurrentTarget.HealthPercent > PvPTargetMinEx)
                    //Dont cast if target is near death and not using AoW
                    {
                        if (SpellManager.CanCast("Exorcism"))
                        {
                            Exorcism();
                            return true;
                        }
                    }

                }
            }

            if (Me.CurrentTarget.Distance <= 10)
            {
                if (PvPUseConsecration) //Consecration
                {

                    if (SpellManager.CanCast("Consecration"))
                    {
                        Consecration();
                        return true;
                    }

                }

                if (PvPUseHw)
                {
                    if (SpellManager.CanCast("Holy Wrath")) //Holy Wrath
                    {
                        Hw();
                        return true;
                    }
                }
            }

            return false;
        }

        #endregion

        #region PVP Poison Reactor *4.0 Cata*

        /////////////////////////////////
        //Deal with Poisons & Disease
        /////////////////////////////////

        public bool PvPPoisonReactor()
        {
            if ((PvPUseCleanse) && (PvPCleanseMagic))
            {
                foreach (var pair in Me.Auras)
                {
                    WoWDispelType checkDebuff = pair.Value.Spell.DispelType;
                    if (checkDebuff.Equals(WoWDispelType.Disease) || checkDebuff.Equals(WoWDispelType.Poison) ||
                        checkDebuff.Equals(WoWDispelType.Magic))
                    {
                        if (SpellManager.CanCast("Cleanse"))
                        {
                            Slog("Cleanse : " + pair.Key);
                            Cleanse();
                            return true;
                        }
                    }
                }
            }

            if ((PvPUseCleanse) && (!PvPCleanseMagic))
            {
                foreach (var pair in Me.Auras)
                {
                    WoWDispelType checkDebuff = pair.Value.Spell.DispelType;
                    if (checkDebuff.Equals(WoWDispelType.Disease) || checkDebuff.Equals(WoWDispelType.Poison))
                    {
                        if (SpellManager.CanCast("Cleanse"))
                        {
                            Slog("Cleanse : " + pair.Key);
                            Cleanse();
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        #endregion

        #region PvP Heal Reactor *4.0 Cata*

        /////////////////////////////////
        //Decide a Heal
        /////////////////////////////////

        public bool BgHeal()
        {

            List<WoWPlayer> players = Me.RaidMembers;

            foreach (WoWPlayer player in players)
            {
                if (player.Distance > 30) continue;
                if (player.Dead) continue;
                if (player.IsGhost) continue;
                if (!player.InLineOfSight) continue;

                if (player.HealthPercent <= PvPHealbotLoHHealth)
                {

                    if (SpellManager.CanCast("Lay on Hands"))
                    {
                        player.Target();
                        Thread.Sleep(125);
                        LoH();

                        Me.ClearTarget(); //Always clear Raid Target

                        return true;
                    }

                }

                if ((player.HealthPercent <= PvPHealbotCombatFoL) && ((player.HealthPercent > PvPHealbotCombatHl) || (player.HealthPercent > PvPHealbotWoGHealth) || (player.HealthPercent > PvPHealbotLoDHealth) || (player.HealthPercent > PvPHealbotDlHealth) || (player.HealthPercent > PvPHealbotHrHealth))) //Flash of Light
                {

                    if (SpellManager.CanCast("Flash of Light"))
                    {
                        player.Target();
                        Thread.Sleep(125);
                        FoL();

                        Me.ClearTarget(); //Always clear Raid Target

                        return true;
                    }

                }

                if (player.HealthPercent <= PvPHealbotWoGHealth)
                {

                    if ((SpellManager.CanCast("Word of Glory")) && (Me.CurrentHolyPower >= PvPHealbotWoGhp))
                    {
                        player.Target();
                        Thread.Sleep(125);
                        WoG();

                        Me.ClearTarget(); //Always clear Raid Target

                        return true;
                    }

                }

                if (player.HealthPercent <= PvPHealbotLoDHealth)
                {

                    if ((SpellManager.CanCast("Light of Dawn")) && (Me.CurrentHolyPower >= PvPHealbotLoDhp))
                    {
                        player.Target();
                        Thread.Sleep(125);
                        LoD();

                        Me.ClearTarget(); //Always clear Raid Target

                        return true;
                    }

                }

                if (player.HealthPercent <= PvPHealbotDlHealth)
                {

                    if (SpellManager.CanCast("Divine Light"))
                    {
                        player.Target();
                        Thread.Sleep(125);
                        DivineLight();

                        Me.ClearTarget(); //Always clear Raid Target

                        return true;
                    }

                }

                if (player.HealthPercent <= PvPHealbotHrHealth)
                {

                    if (SpellManager.CanCast("Holy Radiance"))
                    {
                        player.Target();
                        Thread.Sleep(125);
                        Hr();

                        Me.ClearTarget(); //Always clear Raid Target

                        return true;
                    }

                }

                if (player.HealthPercent <= PvPHealbotCombatHl)
                {

                    if (SpellManager.CanCast("Holy Light"))
                    {
                        player.Target();
                        Thread.Sleep(125);
                        HolyLight();

                        Me.ClearTarget(); //Always clear Raid Target

                        return true;
                    }

                }

                if (player.HealthPercent <= PvPHealbotHsHealth)
                {

                    if (SpellManager.CanCast("Holy Shock"))
                    {
                        player.Target();
                        Thread.Sleep(125);
                        HolyShockDps(); //We are targeting a friendly player

                        Me.ClearTarget(); //Always clear Raid Target

                        return true;
                    }

                }

            }

            return false;
        }

        #endregion

        #endregion

        #region RaF Logic *HB2*

        #region RaF Target Check *4.0 Cata*

        /////////////////////////////////
        //Confirm we have our leaders target
        /////////////////////////////////

        private static void RaFTargetCheck()
        {
            var currentTarget =
                ObjectManager.Me.PartyMembers.Where(delegate(WoWPlayer unit) { return unit.IsGroupLeader; }).
                    FirstOrDefault().CurrentTarget;


            if ((Me.CurrentTarget == currentTarget) || (currentTarget == null)) return;
            Me.ClearTarget();
            Thread.Sleep(50);
            currentTarget.Target();
        }

        #endregion

        #region RaF Range Check *4.0 Cata*

        /////////////////////////////////
        //Checks for range of current target (DPS) and range of Heal Target (Healbot)
        //No range checks if the healer is the leader.
        /////////////////////////////////

        public bool RaFRange()
        {
            int num = HealbotDistance - 3;
            int num2 = HealbotDistance + 3; //Need a 4 yard threshold to prevent constant moving

            if (Healbot)
            {
                if (HealbotDps)
                {
                    if ((Me.ManaPercent > HealbotDpsMana))
                    {
                        if (Me.CurrentTarget.Distance > 3.75) //We need to get in meele range first
                        {

                            if (ApproachDetector())//Dont move if target is approaching the leader
                                return true;

                            Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                              ObjectManager.Me.CurrentTarget.
                                                                                  Location, 2.5f));

                            return true;
                        }

                        return false;
                    }

                    if (((Me.ManaPercent < HealbotDpsMana)) &&
                        ((Me.PartyMember1.Distance < num) || (Me.PartyMember1.Distance > num2)))
                    {
                        Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                          ObjectManager.Me.PartyMember1.Location,
                                                                          HealbotDistance));

                        return true;
                    }
                }

                else if (((!HealbotDps)) && ((Me.PartyMember1.Distance < num) || (Me.PartyMember1.Distance > num2)))
                {
                    Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,
                                                                      ObjectManager.Me.PartyMember1.Location,
                                                                      HealbotDistance));
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region RaF Heal Reactor *4.0 Cata*

        /////////////////////////////////
        //Decide a Heal
        /////////////////////////////////

        public bool RaFHeal()
        {
            List<WoWPlayer> playerlist = Me.PartyMembers;

            foreach (WoWPlayer player in playerlist)
            {

                if (player.HealthPercent <= HealbotLoHHealth)
                {

                    if (SpellManager.CanCast("Lay on Hands"))
                    {
                        player.Target();
                        Thread.Sleep(125);
                        LoH();

                        if (HealbotDps)
                        {
                            if (Me.ManaPercent > HealbotDpsMana)
                                Me.TargetLastTarget(); //Target our last target

                            else
                                Me.ClearTarget(); //Always clear Party Target
                        }

                        else
                            Me.ClearTarget(); //Always clear Party Target

                        return true;
                    }

                }

                if ((player.HealthPercent <= HealbotCombatFoL) && ((player.HealthPercent > HealbotCombatHl) || (player.HealthPercent > HealbotWoGHealth) || (player.HealthPercent > HealbotLoDHealth) || (player.HealthPercent > HealbotDlHealth) || (player.HealthPercent > HealbotHrHealth))) //Flash of Light
                {

                    if (SpellManager.CanCast("Flash of Light"))
                    {
                        player.Target();
                        Thread.Sleep(125);
                        FoL();

                        if (HealbotDps)
                        {
                            if (Me.ManaPercent > HealbotDpsMana)
                                Me.TargetLastTarget(); //Target our last target

                            else
                                Me.ClearTarget(); //Always clear Party Target
                        }

                        else
                            Me.ClearTarget(); //Always clear Party Target

                        return true;
                    }

                }

                if (player.HealthPercent <= HealbotWoGHealth)
                {

                    if ((SpellManager.CanCast("Word of Glory")) && (Me.CurrentHolyPower >= HealbotWoGhp))
                    {
                        player.Target();
                        Thread.Sleep(125);
                        WoG();

                        if (HealbotDps)
                        {
                            if (Me.ManaPercent > HealbotDpsMana)
                                Me.TargetLastTarget(); //Target our last target

                            else
                                Me.ClearTarget(); //Always clear Party Target
                        }

                        else
                            Me.ClearTarget(); //Always clear Party Target

                        return true;
                    }

                }

                if (player.HealthPercent <= HealbotLoDHealth)
                {

                    if ((SpellManager.CanCast("Light of Dawn")) && (Me.CurrentHolyPower >= HealbotLoDhp))
                    {
                        player.Target();
                        Thread.Sleep(125);
                        LoD();

                        if (HealbotDps)
                        {
                            if (Me.ManaPercent > HealbotDpsMana)
                                Me.TargetLastTarget(); //Target our last target

                            else
                                Me.ClearTarget(); //Always clear Party Target
                        }

                        else
                            Me.ClearTarget(); //Always clear Party Target

                        return true;
                    }

                }

                if (player.HealthPercent <= HealbotDlHealth)
                {

                    if (SpellManager.CanCast("Divine Light"))
                    {
                        player.Target();
                        Thread.Sleep(125);
                        DivineLight();

                        if (HealbotDps)
                        {
                            if (Me.ManaPercent > HealbotDpsMana)
                                Me.TargetLastTarget(); //Target our last target

                            else
                                Me.ClearTarget(); //Always clear Party Target
                        }

                        else
                            Me.ClearTarget(); //Always clear Party Target

                        return true;
                    }

                }

                if (player.HealthPercent <= HealbotHrHealth)
                {

                    if (SpellManager.CanCast("Holy Radiance"))
                    {
                        player.Target();
                        Thread.Sleep(125);
                        Hr();

                        if (HealbotDps)
                        {
                            if (Me.ManaPercent > HealbotDpsMana)
                                Me.TargetLastTarget(); //Target our last target

                            else
                                Me.ClearTarget(); //Always clear Party Target
                        }

                        else
                            Me.ClearTarget(); //Always clear Party Target

                        return true;
                    }

                }

                if (player.HealthPercent <= HealbotCombatHl)
                {

                    if (SpellManager.CanCast("Holy Light"))
                    {
                        player.Target();
                        Thread.Sleep(125);
                        HolyLight();

                        if (HealbotDps)
                        {
                            if (Me.ManaPercent > HealbotDpsMana)
                                Me.TargetLastTarget(); //Target our last target

                            else
                                Me.ClearTarget(); //Always clear Party Target
                        }

                        else
                            Me.ClearTarget(); //Always clear Party Target

                        return true;
                    }

                }

                if (player.HealthPercent <= HealbotHsHealth)
                {

                    if (SpellManager.CanCast("Holy Shock"))
                    {
                        player.Target();
                        Thread.Sleep(125);
                        HolyShockDps(); //We are targeting a friendly player

                        if (HealbotDps)
                        {
                            if (Me.ManaPercent > HealbotDpsMana)
                                Me.TargetLastTarget(); //Target our last target

                            else
                                Me.ClearTarget(); //Always clear Party Target
                        }

                        else
                            Me.ClearTarget(); //Always clear Party Target

                        return true;
                    }

                }

            }

            return false;
        }

        #endregion

        #region RaF Rotation *4.0 Cata*

        /////////////////////////////////
        //In Combat rotation planner
        /////////////////////////////////

        public bool RaFRotation()
        {

            /******Finishing Move******/

            if (Me.CurrentTarget.HealthPercent < 20)
            {
                if (UseHoW)
                {
                    if (SpellManager.CanCast("Hammer of Wrath")) //Hammer of Wrath. Finishing move
                    {
                        HoW();
                        return true;
                    }
                }
            }

            /******DPS Enhancer******/

            if (UseInquisition)
            {
                if (!InquisitionAdds)
                {

                    if (Me.CurrentHolyPower >= InquisitionHp)
                    {
                        if (SpellManager.CanCast("Inquisition"))
                        {
                            Inquisition();
                            return true;
                        }
                    }

                }
            }

            if (UseAvengingWrath) //Avenging Wrath
            {
                if (!UseAvengingWrathAdds)
                {
                    if (SpellManager.CanCast("Avenging Wrath"))
                    {
                        Aw();
                        return true;
                    }
                }
            }

            if (UseDivinePlea)
            {
                if (!UseDivinePleaAdds) // Divine Plea
                {
                    if (SpellManager.CanCast("Divine Plea"))
                    {
                        DivinePlea();
                        return true;
                    }
                }
            }

            if ((UseDf) && (!UseDfAdds))
            {
                if (SpellManager.CanCast("Divine Favor")) //Divine Favor
                {
                    Df();
                    return true;
                }
            }

            if ((UseZealotry) && (!UseZealotryAdds))
            {

                if (SpellManager.CanCast("Zealotry"))
                {
                    Zealotry();
                    return true;
                }

            }

            if (Me.CurrentTarget.Distance <= 9)
            {

                if (UseJudgement) //Judgement
                {
                    if (SpellManager.CanCast("Judgement"))
                    {
                        Judgement();
                        return true;
                    }
                }
            }

            /******Instant Exorcism******/

            if (Me.CurrentTarget.Distance <= 29)
            {

                if (_aoWProc) //Burn Exorcism on AoW?
                {

                    if (SpellManager.CanCast("Exorcism"))
                    {
                        Exorcism();
                        return true;
                    }
                }

                if (_denounceProc) //Burn Exorcism on Denounce?
                {

                    if (SpellManager.CanCast("Exorcism"))
                    {
                        Exorcism();
                        return true;
                    }
                }

            }

            /******Holy Power Spells******/

            if (Me.CurrentTarget.Distance <= 4)
            {

                if ((UseTV) && (Me.CurrentHolyPower >= TVHP)) //Templar's Verdict
                {

                    if (SpellManager.CanCast("Templar's Verdict"))
                    {
                        TV();
                        return true;
                    }

                }


                if ((UseShieldR) && (Me.CurrentHolyPower >= SoRhp)) //Shield of the Righteous
                {
                    if (Me.CurrentTarget.Distance <= 4)
                    {
                        if (SpellManager.CanCast("Shield of the Righteous"))
                        {
                            ShieldR();
                            return true;
                        }
                    }
                }

                if ((UseDs) && (Me.CurrentHolyPower >= Dshp))
                {
                    if (!UseDsAdds)
                    {
                        if (SpellManager.CanCast("Divine Storm"))
                        {
                            Ds();
                            return true;
                        }
                    }
                }

                /******Holy Power Generating Spells******/

                if ((UseCs)) // Crusader Strike
                {
                    if (SpellManager.CanCast("Crusader Strike"))
                    {
                        Cs();
                        return true;
                    }
                }

                if (UseHotR) //Hammer of the Righteous
                {
                    if (SpellManager.CanCast("Hammer of the Righteous"))
                    {
                        HotR();
                        return true;
                    }
                }

            }

            if (HolyShockDPS) //Holy Shock
            {
                if (SpellManager.CanCast("Holy Shock"))
                {
                    HolyShockDps();
                    return true;
                }
            }

            /******Other Spells******/

            if (UseAs)
            {
                if (!UseAsAdds)
                {
                    if (SpellManager.CanCast("Avenger's Shield")) //Avenger's Shield
                    {
                        As();
                        return true;
                    }
                }
            }

            if (Me.CurrentTarget.Distance <= 29)
            {
                if ((UseExorcism) && (!UseAoW) && (!UseDenounce))
                {

                    if (Me.CurrentTarget.HealthPercent > TargetMinEx)
                    //Dont cast if target is near death and not using AoW
                    {
                        if (SpellManager.CanCast("Exorcism"))
                        {
                            Exorcism();
                            return true;
                        }
                    }

                }
            }

            if (Me.CurrentTarget.Distance <= 10)
            {
                if (UseConsecration) //Consecration
                {
                    if (!UseConsecrationAdds)
                    {
                        if (SpellManager.CanCast("Consecration"))
                        {
                            Consecration();
                            return true;
                        }
                    }
                }

                if ((UseHw) && (!UseHwAdds))
                {
                    if (SpellManager.CanCast("Holy Wrath")) //Holy Wrath
                    {
                        Hw();
                        return true;
                    }
                }
            }


            return false;
        }

        #endregion

        #region RaF Poison Reactor *4.0 Cata*

        /////////////////////////////////
        //Deal with Poisons & Disease
        /////////////////////////////////

        public bool RaFPoisonReactor()
        {
            List<WoWPlayer> playerlist = Me.PartyMembers;

            foreach (WoWPlayer player in playerlist)
            {
                if ((HealbotUseCleanse) && (HealbotCleanseMagic))
                {
                    foreach (var pair in player.Auras)
                    {

                        WoWDispelType checkDebuff = pair.Value.Spell.DispelType;

                        if (checkDebuff.Equals(WoWDispelType.Disease) || checkDebuff.Equals(WoWDispelType.Poison) ||
                            checkDebuff.Equals(WoWDispelType.Magic))
                        {
                            if (SpellManager.CanCast("Cleanse"))
                            {
                                Slog("Cleanse : " + pair.Key);
                                player.Target();
                                Thread.Sleep(125);
                                Cleanse();

                                if (HealbotDps)
                                {
                                    if (Me.ManaPercent > HealbotDpsMana)
                                    {
                                        Me.TargetLastTarget(); //Target our last target
                                        return true;
                                    }

                                    Me.ClearTarget(); //Always clear Party Target
                                    return true;

                                }

                                Me.ClearTarget(); //Always clear Party Target
                            }
                        }

                    }

                }

                if ((HealbotUseCleanse) && (!HealbotCleanseMagic))
                {
                    foreach (var pair in player.Auras)
                    {

                        WoWDispelType checkDebuff = pair.Value.Spell.DispelType;

                        if (checkDebuff.Equals(WoWDispelType.Disease) || checkDebuff.Equals(WoWDispelType.Poison))
                        {
                            if (SpellManager.CanCast("Cleanse"))
                            {
                                Slog("Cleanse : " + pair.Key);
                                player.Target();
                                Thread.Sleep(125);
                                Cleanse();


                                if (HealbotDps)
                                {
                                    if (Me.ManaPercent > HealbotDpsMana)
                                    {
                                        Me.TargetLastTarget(); //Target our last target
                                        return true;
                                    }

                                    Me.ClearTarget(); //Always clear Party Target
                                    return true;

                                }

                                Me.ClearTarget(); //Always clear Party Target
                            }
                        }

                    }

                }

            }

            return false;
        }

        #endregion

        #endregion

        #region Global Logic *4.0 Cata*

        #region Global AoW Check *4.0 Cata*

        /////////////////////////////////
        //Detect Art of War (Credit to Bobby53 for this outline)
        /////////////////////////////////

        public void AoWCheck()
        {
            const string cArtOfWar = "The Art of War";
            Lua.DoString("buffName,_,_,stackCount,_,_,_,_,_=UnitBuff(\"player\",\"" + cArtOfWar + "\")");
            //string buffName = Lua.GetReturnValues("buffName", Me.BaseAddress);
            string buffName = Lua.GetLocalizedText("buffName", Me.BaseAddress);


            if (buffName == cArtOfWar)
            {
                _aoWProc = true;

                if (!_aoWSpam)
                {
                    Slog("#Art of War Detected#");
                    _aoWSpam = true;
                }
            }

            else
            {
                _aoWProc = false;

                _aoWSpam = true;
            }
        }

        #endregion

        #region Global Denounce Check *4.0 Cata*

        /////////////////////////////////
        //Detect Denounce (Credit to Bobby53 for this outline)
        /////////////////////////////////

        public void DenounceCheck()
        {
            const string cDenounce = "Denounce";
            Lua.DoString("buffName,_,_,stackCount,_,_,_,_,_=UnitBuff(\"player\",\"" + cDenounce + "\")");
            string buffName = Lua.GetLocalizedText("buffName", Me.BaseAddress);


            if (buffName == cDenounce)
            {
                _denounceProc = true;

                if (!_denounceSpam)
                {
                    Slog("#Denounce Detected#");
                    _denounceSpam = true;
                }
            }

            else
            {
                _denounceProc = false;

                _denounceSpam = true;
            }
        }

        #endregion

        #region Global Auto Attack *4.0 Cata*

        /////////////////////////////////
        //Simply makes sure we are attacking
        /////////////////////////////////

        public bool AutoAttack()
        {
            if ((!Me.IsAutoAttacking) && (Me.GotTarget))
            {
                if (!ObjectManager.Me.CurrentTarget.Dead)
                {
                    Slog("#Combat Stance Enabled#");
                    Lua.DoString("StartAttack()");
                    return true;
                }

                return false;
            }

            return false;
        }

        #endregion

        #region Global BGCheck *4.0 Cata*

        /////////////////////////////////
        //Check to see if we are in a BG
        /////////////////////////////////

        public bool InBg()
        {
            return Battlegrounds.IsInsideBattleground;
        }

        #endregion

        #region Global Auras *4.0 Cata*

        /////////////////////////////////
        //Check for Auras (Mounted or Not)
        /////////////////////////////////

        private void AuraCheck()
        {
            if (InBg()) //Lets find out if we are in a battle ground first
            {
                if (Me.Mounted) //Are we mounted? Aura Switch
                {
                    if (PvPMountCrusader)
                    {
                        if (!Me.Auras.ContainsKey("Crusader Aura"))
                        {
                            if (SpellManager.CanCast("Crusader Aura"))
                            {
                                CruAura();
                                //return;
                            }

                        }
                    }
                }

                else //Not Mounted, so either switch back or dont bother switching auras
                {
                    if (PvPUseDa)
                    {
                        if (!Me.Auras.ContainsKey("Devotion Aura"))
                        {
                            if (SpellManager.CanCast("Devotion Aura"))
                            {
                                DevAura();
                                //return;
                            }
                        }
                    }

                    if (PvPUseRa)
                    {
                        if (!Me.Auras.ContainsKey("Retribution Aura"))
                        {
                            if (SpellManager.CanCast("Retribution Aura"))
                            {
                                RetAura();
                                //return;

                            }
                        }
                    }

                    if (PvPUseCa)
                    {
                        if (!Me.Auras.ContainsKey("Concentration Aura"))
                        {
                            if (SpellManager.CanCast("Concentration Aura"))
                            {
                                ConAura();
                                //return;
                            }
                        }
                    }

                    if (PvPUseCruA)
                    {
                        if (!Me.Auras.ContainsKey("Crusader Aura"))
                        {
                            if (SpellManager.CanCast("Crusader Aura"))
                            {
                                CruAura();
                                //return;
                            }
                        }
                    }

                    if (PvPUseReA)
                    {
                        if (!Me.Auras.ContainsKey("Resistance Aura"))
                        {
                            if (SpellManager.CanCast("Resistance Aura"))
                            {
                                ReAAura();
                                //return;
                            }
                        }
                    }
                }
            }

            else //Not in Battleground
            {
                if (Me.Mounted) //Our we mounted? Aura Switch
                {
                    if (MountCrusader)
                    {
                        if (!Me.Auras.ContainsKey("Crusader Aura"))
                        {
                            if (SpellManager.CanCast("Crusader Aura"))
                            {
                                CruAura();
                                //return;
                            }
                        }
                    }
                }

                else //Not Mounted, so either switch back or dont bother switching auras
                {
                    if (UseDa)
                    {
                        if (!Me.Auras.ContainsKey("Devotion Aura"))
                        {
                            if (SpellManager.CanCast("Devotion Aura"))
                            {
                                DevAura();
                                //return;
                            }
                        }
                    }

                    if (UseRa)
                    {
                        if (!Me.Auras.ContainsKey("Retribution Aura"))
                        {
                            if (SpellManager.CanCast("Retribution Aura"))
                            {
                                RetAura();
                                //return;
                            }
                        }
                    }

                    if (UseCa)
                    {
                        if (!Me.Auras.ContainsKey("Concentration Aura"))
                        {
                            if (SpellManager.CanCast("Concentration Aura"))
                            {
                                ConAura();
                                // return;
                            }
                        }
                    }

                    if (UseCruA)
                    {
                        if (!Me.Auras.ContainsKey("Crusader Aura"))
                        {
                            if (SpellManager.CanCast("Crusader Aura"))
                            {
                                CruAura();
                                //return;
                            }
                        }
                    }

                    if (UseReA)
                    {
                        if (!Me.Auras.ContainsKey("Resistance Aura"))
                        {
                            if (SpellManager.CanCast("Resistance Aura"))
                            {
                                ReAAura();
                                //return;

                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Global Blessings *4.0 Cata*

        /////////////////////////////////
        //Check for Blessings before Combat
        //Buff other code thanks to FPSWare
        /////////////////////////////////

        public void BlessingCheck()
        {

            if (!Me.Mounted) //Are we mounted? Dont buff!!!
            {
                /******Solo******/
                if ((!InBg()) && ((!InParty()) && (!InRaid())))
                {

                    if (UseBoM)
                    {

                        if ((!Me.Auras.ContainsKey("Blessing of Might")))
                        {
                            if (SpellManager.CanCast("Blessing of Might"))
                            {
                                Me.Target();
                                BoM();
                                Me.ClearTarget();
                                //return;
                            }
                        }

                    }

                    if (UseBoK)
                    {
                        if ((!Me.Auras.ContainsKey("Blessing of Kings")))
                        {
                            if (SpellManager.CanCast("Blessing of Kings"))
                            {
                                Me.Target();
                                BoK();
                                Me.ClearTarget();
                                //return;
                            }
                        }
                    }

                    if (BuffOthers)
                    {

                        if (!_buffTimer.IsRunning)
                            _buffTimer.Start();

                        if (_buffTimer.ElapsedMilliseconds > 15000) //We only want to buff others every 15 seconds
                        {

                            if ((Me.ManaPercent > 10) && (!Me.Dead) && (!Me.IsGhost) && (!Me.IsResting))
                            {

                                List<WoWPlayer> plist =
                                    (from o in ObjectManager.ObjectList
                                     where o is WoWPlayer
                                     let p = o.ToPlayer()
                                     where p.Distance < 30
                                           && p.Guid != Me.Guid
                                           && (!Me.PvpFlagged && !p.PvpFlagged || Me.PvpFlagged && p.PvpFlagged)
                                           && (p.IsHorde && Me.IsHorde || p.IsAlliance && Me.IsAlliance)
                                           && p.Level <= Me.Level + 25
                                           && !p.Dead
                                           && p.InLineOfSight
                                           && !p.HasAura("Blessing of Kings")
                                           && !p.HasAura("Blessing of Might")
                                     select p).ToList();

                                foreach (WoWPlayer p in plist)
                                {
                                    if (UseBoK)
                                    {
                                        if (SpellManager.CanCast("Blessing of Kings"))
                                        {

                                            if (!p.HasAura("Blessing of Kings"))
                                            {
                                                p.Target();
                                                Thread.Sleep(250);
                                                Slog("#Friendly player is near - buff them#");
                                                BoK();
                                                Thread.Sleep(250);
                                                Me.ClearTarget();
                                                _buffTimer.Reset();
                                                // return;
                                            }

                                        }

                                    }

                                    if (UseBoM)
                                    {
                                        if (SpellManager.CanCast("Blessing of Might"))
                                        {

                                            if (!p.HasAura("Blessing of Might"))
                                            {
                                                p.Target();
                                                Thread.Sleep(250);
                                                Slog("#Friendly player is near - buff them#");
                                                BoK();
                                                Thread.Sleep(250);
                                                Me.ClearTarget();
                                                _buffTimer.Reset();
                                                // return;
                                            }

                                        }

                                    }

                                }
                            }
                        }

                    }

                }

                /******In Party, not Raid, Not BG******/

                if ((!InBg()) && ((InParty()) && (!InRaid())))
                {

                    List<WoWPlayer> playerlist = Me.PartyMembers;

                    foreach (WoWPlayer player in playerlist)
                    {

                        if (UseBoM)
                        {
                            if (!player.Auras.ContainsKey("Blessing of Might")) //Try with BoM first
                            {
                                if (SpellManager.CanCast("Blessing of Might"))
                                {
                                    Slog("#RaF : (Re)Buffing Party Members with Blessing of Might#");
                                    Me.Target();
                                    BoM();
                                    Me.ClearTarget();
                                    //return;
                                }
                            }

                        }


                        if (UseBoK)
                        {
                            if ((!player.Auras.ContainsKey("Blessing of Kings")))
                            {
                                if (SpellManager.CanCast("Blessing of Kings"))
                                {
                                    Slog("#RaF : (Re)Buffing Party Members with Blessing of Kings#");
                                    Me.Target();
                                    BoK();
                                    Me.ClearTarget();
                                    //return;
                                }
                            }
                        }

                    }

                    if (BuffOthers)
                    {

                        if (!_buffTimer.IsRunning)
                            _buffTimer.Start();

                        if (_buffTimer.ElapsedMilliseconds > 15000) //We only want to buff others every 15 seconds
                        {

                            if ((Me.ManaPercent > 10) && (!Me.Dead) && (!Me.IsGhost) && (!Me.IsResting))
                            {

                                List<WoWPlayer> plist =
                                    (from o in ObjectManager.ObjectList
                                     where o is WoWPlayer
                                     let p = o.ToPlayer()
                                     where p.Distance < 30
                                           && p.Guid != Me.Guid
                                           && (!Me.PvpFlagged && !p.PvpFlagged || Me.PvpFlagged && p.PvpFlagged)
                                           && (p.IsHorde && Me.IsHorde || p.IsAlliance && Me.IsAlliance)
                                           && p.Level <= Me.Level + 25
                                           && !p.Dead
                                           && p.InLineOfSight
                                           && !p.HasAura("Blessing of Kings")
                                           && !p.HasAura("Blessing of Might")
                                     select p).ToList();

                                foreach (WoWPlayer p in plist)
                                {
                                    if (UseBoK)
                                    {
                                        if (SpellManager.CanCast("Blessing of Kings"))
                                        {

                                            if (!p.HasAura("Blessing of Kings"))
                                            {
                                                p.Target();
                                                Thread.Sleep(250);
                                                Slog("#Friendly player is near - buff them#");
                                                BoK();
                                                Thread.Sleep(250);
                                                Me.ClearTarget();
                                                _buffTimer.Reset();
                                                //return;
                                            }

                                        }

                                    }

                                    if (UseBoM)
                                    {
                                        if (SpellManager.CanCast("Blessing of Might"))
                                        {

                                            if (!p.HasAura("Blessing of Might"))
                                            {
                                                p.Target();
                                                Thread.Sleep(250);
                                                Slog("#Friendly player is near - buff them#");
                                                BoK();
                                                Thread.Sleep(250);
                                                Me.ClearTarget();
                                                _buffTimer.Reset();
                                                //return;
                                            }

                                        }

                                    }

                                }
                            }
                        }

                    }

                }

                /******In BG******/

                if ((InBg()))
                {

                    if (PvPUseBoM)
                    {

                        if ((!Me.Auras.ContainsKey("Blessing of Might")))
                        {
                            if (SpellManager.CanCast("Blessing of Might"))
                            {
                                Me.Target();
                                BoM();
                                Me.ClearTarget();
                                // return;
                            }
                        }

                    }

                    if (PvPUseBoK)
                    {
                        if ((!Me.Auras.ContainsKey("Blessing of Kings")))
                        {
                            if (SpellManager.CanCast("Blessing of Kings"))
                            {
                                Me.Target();
                                BoK();
                                Me.ClearTarget();
                                //return;
                            }
                        }
                    }

                    if (PvPBuffOthers)
                    {

                        List<WoWPlayer> raidlist = Me.RaidMembers;

                        foreach (WoWPlayer player in raidlist)
                        {

                            if (PvPUseBoM)
                            {
                                if (!player.Auras.ContainsKey("Blessing of Might"))
                                {
                                    if (SpellManager.CanCast("Blessing of Might"))
                                    {
                                        Slog("#BG : (Re)Buffing BG with Blessing of Might#");
                                        player.Target();
                                        Thread.Sleep(250);
                                        BoM();
                                        Thread.Sleep(250);
                                        Me.ClearTarget();
                                        //return;
                                    }
                                }

                            }


                            if (PvPUseBoK)
                            {
                                if ((!player.Auras.ContainsKey("Blessing of Kings")))
                                {
                                    if (SpellManager.CanCast("Blessing of Kings"))
                                    {
                                        Slog("#BG : (Re)Buffing BG with Blessing of Kings#");
                                        player.Target();
                                        Thread.Sleep(250);
                                        BoK();
                                        Thread.Sleep(250);
                                        Me.ClearTarget();
                                        // return;
                                    }
                                }
                            }
                        }

                    }

                }

            }
        }

        #endregion

        #region Global Seal Check *4.0 Cata*

        /////////////////////////////////
        //Check for Seals before combat
        /////////////////////////////////

        public bool SealCheck()
        {
            /******In a BG******/
            if (InBg())
            {
                if (PvPUseSoR)
                {
                    if (!Me.Auras.ContainsKey("Seal of Righteousness"))
                    {
                        if (SpellManager.CanCast("Seal of Righteousness"))
                        {
                            SoR();
                            return true;
                        }
                    }
                }

                if (PvPUseSoJ)
                {
                    if (!Me.Auras.ContainsKey("Seal of Justice"))
                    {
                        if (SpellManager.CanCast("Seal of Justice"))
                        {
                            SoJ();
                            return true;
                        }
                    }
                }

                if (PvPUseSoI)
                {
                    if (!Me.Auras.ContainsKey("Seal of Insight"))
                    {
                        if (SpellManager.CanCast("Seal of Insight"))
                        {
                            SoI();
                            return true;
                        }
                    }
                }

                if (PvPUseSoT)
                {
                    if (!Me.Auras.ContainsKey("Seal of Truth"))
                    {
                        if (SpellManager.CanCast("Seal of Truth"))
                        {
                            SoT();
                            return true;
                        }
                    }
                }

                return false;

            }

            /******Not in a BG******/

            if (UseSoR)
            {
                if (!Me.Auras.ContainsKey("Seal of Righteousness"))
                {
                    if (SpellManager.CanCast("Seal of Righteousness"))
                    {
                        SoR();
                        return true;
                    }
                }
            }

            if (UseSoJ)
            {
                if (!Me.Auras.ContainsKey("Seal of Justice"))
                {
                    if (SpellManager.CanCast("Seal of Justice"))
                    {
                        SoJ();
                        return true;
                    }
                }
            }

            if (UseSoI)
            {
                if (!Me.Auras.ContainsKey("Seal of Insight"))
                {
                    if (SpellManager.CanCast("Seal of Insight"))
                    {
                        SoI();
                        return true;
                    }
                }
            }

            if (UseSoT)
            {
                if (!Me.Auras.ContainsKey("Seal of Truth"))
                {
                    if (SpellManager.CanCast("Seal of Truth"))
                    {
                        SoT();
                        return true;
                    }
                }
            }

            return false;
        }

        #endregion

        #region Global PreCombatBuff *4.0 Cata*

        /////////////////////////////////
        //This will run after rest (assuming Nesox is right)
        /////////////////////////////////

        public override void PreCombatBuff()
        {
            /*SealCheck(); //Make sure seals are good before pulling
            AuraCheck(); //Make sure Auras are set
            BlessingCheck(); //Make sure Blessings are set
            RfCheck(); //Righteous Fury
            SelfHeal(); //Heal Self (Rest)

            if (InBg())
            {

                if (PvPHealbot)
                    BgHeal(); //Scan for targets to heal
            }*/
        }

        #endregion

        #region Global NeedRest *4.0 Cata*

        /////////////////////////////////
        //This pulses 8 times per second while out of combat
        /////////////////////////////////

        public override bool NeedRest
        {

            get
            {

                SealCheck();
                AuraCheck(); //Make sure Auras are set
                BlessingCheck(); //Make sure Blessings are set
                RfCheck(); //Righteous Fury
                SelfHeal(); //Heal Self (Rest)

                //if (InBg())
                //{

                //if (PvPHealbot)
                //BgHeal(); //Scan for targets to heal
                //}

                if (InBg())
                {
                    return
                        Me.GetPowerPercent(WoWPowerType.Mana) <= PvPRestMana ||
                        Me.GetPowerPercent(WoWPowerType.Health) <= PvPRestHealth;
                }

                return
                    Me.GetPowerPercent(WoWPowerType.Mana) <= RestMana ||
                    Me.GetPowerPercent(WoWPowerType.Health) <= RestHealth;

            }
        }

        #endregion

        #region Global Righteous Fury *4.0 Cata*

        /////////////////////////////////
        //Check for Righteous Fury
        /////////////////////////////////

        private void RfCheck()
        {
            if (!InBg())
            {
                if (UseRf) //If its not checked, do not proceed
                {
                    if (IsLeader()) //Only leaders can use it
                    {
                        if (!Me.Auras.ContainsKey("Righteous Fury"))
                        {
                            if (SpellManager.CanCast("Righteous Fury"))
                                RighteousFury();
                        }
                    }
                }

                else
                {

                    if (Me.Auras.ContainsKey("Righteous Fury"))
                    {

                        Lua.DoString("CancelUnitBuff('player','Righteous Fury'");

                    }

                }

            }

            else
            {

                if (PvPUseRf) //If its not checked, do not proceed
                {

                    if (!Me.Auras.ContainsKey("Righteous Fury"))
                    {
                        if (SpellManager.CanCast("Righteous Fury"))
                            RighteousFury();
                    }

                }

                else
                {

                    if (Me.Auras.ContainsKey("Righteous Fury"))
                    {

                        Lua.DoString("CancelUnitBuff('player','Righteous Fury'");

                    }

                }

            }

        }

        #endregion

        #region Global Rest *4.0 Cata*

        /////////////////////////////////
        //If NeedRest() returns true, run Rest()
        /////////////////////////////////

        public override void Rest()
        {
            if (InBg()) //Are we in a Battle Ground?
            {

                if ((Me.HealthPercent <= PvPRestHealth) && (Me.ManaPercent <= 10))
                //Dont cast healz when low on mana. Drink/Eat first.
                {
                    Slog("#Out of Combat - Health is at " + Me.HealthPercent + "%, and we are out of mana. Eat.#");

                    Styx.Logic.Common.Rest.Feed();
                }

                if (Me.ManaPercent <= PvPRestMana)
                {
                    Slog("#Out of Combat - Mana is at " + Me.ManaPercent + "%. Time to drink.#");

                    Styx.Logic.Common.Rest.Feed();
                }
            }

            else
            {

                if ((Me.HealthPercent <= RestHealth) && (Me.ManaPercent <= 10))
                //Dont cast healz when low on mana. Drink/Eat first.
                {
                    Slog("#Out of Combat - Health is at " + Me.HealthPercent + "%, and we are out of mana. Eat.#");

                    Styx.Logic.Common.Rest.Feed();
                }

                if (Me.ManaPercent <= RestMana)
                {
                    Slog("#Out of Combat - Mana is at " + Me.ManaPercent + "%. Time to drink.#");

                    Styx.Logic.Common.Rest.Feed();
                }
            }
        }

        #endregion

        #region Global Rest Heal *4.0 Cata*

        /////////////////////////////////
        //Check if we need a heal out of combat
        /////////////////////////////////

        public void SelfHeal()
        {

            if (InBg()) //Are we in a Battle Ground?
            {
                if ((Me.GetPowerPercent(WoWPowerType.Health) <= PvPRestWoGHealth))
                {
                    if ((SpellManager.CanCast("Word of Glory")) && (Me.CurrentHolyPower >= PvPRestWoGhp))
                    {
                        Slog("#Out of Combat - Holy Power Count : " + Me.CurrentHolyPower + ", **Word of Glory**#");

                        WoG();

                        return;
                    }
                }

                if ((Me.GetPowerPercent(WoWPowerType.Health) <= PvPRestLoDHealth))
                {
                    if ((SpellManager.CanCast("Light of Dawn")) && (Me.CurrentHolyPower >= PvPRestLoDhp))
                    {
                        Slog("#Out of Combat - Holy Power Count : " + Me.CurrentHolyPower + ", **Light of Dawn**#");

                        LoD();

                        return;
                    }
                }

                if (Me.HealthPercent <= PvPRestDl)
                {

                    if (SpellManager.CanCast("Divine Light"))
                    {
                        Slog("#Out of Combat - Health is at " + Me.HealthPercent + "%, **Divine Light**#");

                        DivineLight();

                        return;
                    }

                }

                if (Me.HealthPercent <= PvPRestHr)
                {

                    if (SpellManager.CanCast("Holy Radiance"))
                    {
                        Slog("#Out of Combat - Health is at " + Me.HealthPercent + "%, **Holy Radiance**#");

                        Hr();

                        return;
                    }

                }

                if (Me.HealthPercent <= PvPRestHl)
                {

                    if (SpellManager.CanCast("Holy Light"))
                    {
                        Slog("#Out of Combat - Health is at " + Me.HealthPercent + "%, **Holy Light**#");

                        HolyLight();

                        return;
                    }

                }

                if (PvPDivinePleaRest)
                {
                    if (Me.ManaPercent >= PvPRestMana)
                    {
                        if (SpellManager.CanCast("Divine Plea"))
                        {
                            Slog("#Out of Combat - Mana is at " + Me.ManaPercent + "%. **Divine Plea**#");

                            DivinePlea();

                            return;
                        }
                    }
                }

            }

            else
            {
                if ((Me.GetPowerPercent(WoWPowerType.Health) <= RestWoGHealth))
                {
                    if ((SpellManager.CanCast("Word of Glory")) && (Me.CurrentHolyPower >= RestWoGhp))
                    {
                        Slog("#Out of Combat - Holy Power Count : " + Me.CurrentHolyPower + ", **Word of Glory**#");

                        WoG();

                        return;
                    }
                }

                if ((Me.GetPowerPercent(WoWPowerType.Health) <= RestLoDHealth))
                {
                    if ((SpellManager.CanCast("Light of Dawn")) && (Me.CurrentHolyPower >= RestLoDhp))
                    {
                        Slog("#Out of Combat - Holy Power Count : " + Me.CurrentHolyPower + ", **Light of Dawn**#");

                        LoD();

                        return;
                    }
                }

                if (Me.HealthPercent <= RestDl)
                {

                    if (SpellManager.CanCast("Divine Light"))
                    {
                        Slog("#Out of Combat - Health is at " + Me.HealthPercent + "%, **Divine Light**#");

                        DivineLight();

                        return;
                    }

                }

                if (Me.HealthPercent <= RestHr)
                {

                    if (SpellManager.CanCast("Holy Radiance"))
                    {
                        Slog("#Out of Combat - Health is at " + Me.HealthPercent + "%, **Holy Radiance**#");

                        Hr();

                        return;
                    }

                }

                if (Me.HealthPercent <= RestHl)
                {

                    if (SpellManager.CanCast("Holy Light"))
                    {
                        Slog("#Out of Combat - Health is at " + Me.HealthPercent + "%, **Holy Light**#");

                        HolyLight();

                        return;
                    }

                }

                if (DivinePleaRest)
                {
                    if (Me.ManaPercent >= RestMana)
                    {
                        if (SpellManager.CanCast("Divine Plea"))
                        {
                            Slog("#Out of Combat - Mana is at " + Me.ManaPercent + "%. **Divine Plea**#");

                            DivinePlea();

                            return;
                        }
                    }
                }

            }
        }

        #endregion

        #region Global Spells *4.0 Cata*

        #region Seals *4.0 Cata*

        /////////////////////////////////
        //Seal of Righteousness (Req Level 3)
        /////////////////////////////////

        private static void SoR()
        {
            SpellManager.Cast("Seal of Righteousness"); //Do It!                                          


            Slog("**Seal of Righteousness**");
        }

        /////////////////////////////////
        //Seal of Insight (Req Level 30)
        /////////////////////////////////

        private static void SoI()
        {
            SpellManager.Cast("Seal of Insight"); //Do It!                                          


            Slog("**Seal of Insight**");
        }

        /////////////////////////////////
        //Seal of Truth (Req Level 44)
        /////////////////////////////////

        private static void SoT()
        {
            SpellManager.Cast("Seal of Truth"); //Do It!                                          


            Slog("**Seal of Truth**");
        }

        /////////////////////////////////
        //Seal of Justice (Req Level 64)
        /////////////////////////////////

        private static void SoJ()
        {
            SpellManager.Cast("Seal of Justice"); //Do It!                                          


            Slog("**Seal of Justice**");
        }

        #endregion

        #region Auras *4.0 Cata*

        /////////////////////////////////
        //Devotion Aura (Req Level 5)
        /////////////////////////////////

        private static void DevAura()
        {
            SpellManager.Cast("Devotion Aura"); //Do It!                                               


            Slog("**Devotion Aura**");
        }

        /////////////////////////////////
        //Retribution Aura (Req Level 26)
        /////////////////////////////////

        private static void RetAura()
        {
            SpellManager.Cast("Retribution Aura"); //Do It!                                               


            Slog("**Retribution Aura**");
        }

        /////////////////////////////////
        //Concentration Aura (Req Level 42)
        /////////////////////////////////

        private static void ConAura()
        {
            SpellManager.Cast("Concentration Aura"); //Do It!                                               


            Slog("**Concentration Aura**");
        }

        /////////////////////////////////
        //Crusader Aura (Req Level 62)
        /////////////////////////////////

        private static void CruAura()
        {
            SpellManager.Cast("Crusader Aura"); //Do It!                                               


            Slog("**Crusader Aura**");
        }

        /////////////////////////////////
        //Resistance Aura (Req Level 76)
        /////////////////////////////////

        private static void ReAAura()
        {
            SpellManager.Cast("Resistance Aura"); //Do It!                                               


            Slog("**Resistance Aura**");
        }


        #endregion

        #region Blessings *4.0 Cata*

        /////////////////////////////////
        //Blessing of Might (Req Level 56)
        /////////////////////////////////

        private static void BoM()
        {
            SpellManager.Cast("Blessing of Might"); //Do It!                                               


            Slog("**Blessing of Might**");
        }

        /////////////////////////////////
        //Blessing of Kings (Req Level 20)
        /////////////////////////////////

        private static void BoK()
        {
            SpellManager.Cast("Blessing of Kings"); //Do It!                                               


            Slog("**Blessing of Kings**");
        }

        #endregion

        #region Heals *4.0 Cata*

        /////////////////////////////////
        //Word of Glory (Req Level 9)
        /////////////////////////////////

        private static void WoG()
        {
            SpellManager.Cast("Word of Glory"); //Do It!                                               


            Slog("**Word of Glory**");
        }

        /////////////////////////////////
        //Holy Light (Req Level 14)
        /////////////////////////////////

        private static void HolyLight()
        {
            if (Me.IsMoving)
                WoWMovement.MoveStop();

            SpellManager.Cast("Holy Light"); //Do It! 

            Thread.Sleep(3125);

            Slog("**Holy Light**");
        }

        /////////////////////////////////
        //Lay on Hands (Req Level 16)
        /////////////////////////////////

        private static void LoH()
        {
            SpellManager.Cast("Lay on Hands"); //Do It!                                               


            Slog("**Lay on Hands**");
        }

        /////////////////////////////////
        //Flash of Light (Req Level 20)
        /////////////////////////////////

        private static void FoL()
        {
            if (Me.IsMoving)
                WoWMovement.MoveStop();

            SpellManager.Cast("Flash of Light"); //Do It! 

            Slog("**Flash of Light**");

            Thread.Sleep(1625);
        }

        /////////////////////////////////
        //Divine Favor (Req 20 Holy Points)
        /////////////////////////////////

        private static void Df()
        {
            SpellManager.Cast("Divine Favor"); //Do It!                                               


            Slog("**Divine Favor**");
        }


        /////////////////////////////////
        //Light of Dawn (Req Level 40)
        /////////////////////////////////

        private static void LoD()
        {
            SpellManager.Cast("Light of Dawn"); //Do It!                                               


            Slog("**Light of Dawn**");
        }

        /////////////////////////////////
        //Divine Light (Req Level 62)
        /////////////////////////////////

        private static void DivineLight()
        {
            if (Me.IsMoving)
                WoWMovement.MoveStop();

            SpellManager.Cast("Divine Light"); //Do It!

            Slog("**Divine Light**");

            Thread.Sleep(3125);
        }

        /////////////////////////////////
        //Holy Radiance (Req Level 83)
        /////////////////////////////////

        private static void Hr()
        {
            SpellManager.Cast("Holy Radiance"); //Do It!                                               


            Slog("**Holy Radiance**");
        }

        /////////////////////////////////
        //Cleanse (Req Level 42)
        /////////////////////////////////

        private static void Cleanse()
        {
            SpellManager.Cast("Cleanse"); //Do It!                                               


            Slog("**Cleanse**");
        }

        /////////////////////////////////
        //Divine Plea (Req level 71)
        /////////////////////////////////

        private static void DivinePlea()
        {
            SpellManager.Cast("Divine Plea"); //Do It!                                               


            Slog("**Divine Plea**");
        }

        #endregion

        #region Protection *4.0 Cata*

        /////////////////////////////////
        //Divine Protection (Req Level 30)
        /////////////////////////////////

        private static void Dp()
        {
            SpellManager.Cast("Divine Protection"); //Do It!                                               


            Slog("**Divine Protection**");
        }

        /////////////////////////////////
        //Aura Mastery (Req Level 30)
        /////////////////////////////////

        private static void Am()
        {
            SpellManager.Cast("Aura Mastery"); //Do It!                                               


            Slog("**Aura Mastery**");
        }

        /////////////////////////////////
        //Divine Shield (Req Level 34)
        /////////////////////////////////

        private static void DivineShield()
        {
            SpellManager.Cast("Divine Shield"); //Do It!                                               


            Slog("**Divine Shield**");
        }

        #endregion

        #region DPS *4.0 Cata*

        /////////////////////////////////
        //Templar's Verdict (Req Level ??)
        /////////////////////////////////

        private static void Tv()
        {
            SpellManager.Cast("Templar's Verdict"); //Do It!                


            Slog("**Templar's Verdict**");
        }

        /////////////////////////////////
        //Crusader Strike (Req Level 1)
        /////////////////////////////////

        private static void Cs()
        {
            SpellManager.Cast("Crusader Strike"); //Do It!                


            Slog("**Crusader Strike**");
        }

        /////////////////////////////////
        //Judgement (Req Level 3)
        /////////////////////////////////

        private static void Judgement()
        {
            SpellManager.Cast("Judgement"); //Do It!                


            Slog("**Judgement**");
        }

        /////////////////////////////////
        //Consecration (Req Level 20)
        /////////////////////////////////

        private static void Consecration()
        {
            SpellManager.Cast("Consecration"); //Do It!                                               


            Slog("**Consecration**");
        }

        /////////////////////////////////
        //Exorcism (Req Level 20)
        /////////////////////////////////

        private static void Exorcism()
        {
            if ((Me.IsMoving) && (!_aoWProc) && (!_denounceProc))
                WoWMovement.MoveStop();

            SpellManager.Cast("Exorcism"); //Do It!

            Slog(_aoWProc ? "## Art of War - **Exorcism** - ##" : "**Exorcism**");

            if ((!_aoWProc) && (!_denounceProc))
                Thread.Sleep(1625);

        }

        /////////////////////////////////
        //Shield of the Righteousness (Req Level 25)
        /////////////////////////////////

        private static void ShieldR()
        {
            SpellManager.Cast("Shield of the Righteous"); //Do It!


            Slog("**Shield of the Righteous**");
        }

        /////////////////////////////////
        //Hammer of Wrath (Req Level 44)
        /////////////////////////////////

        private static void HoW()
        {
            SpellManager.Cast("Hammer of Wrath"); //Do It!                


            Slog("**Hammer of Wrath**");
        }

        /////////////////////////////////
        //Holy Wrath (Req Level 52)
        /////////////////////////////////

        private static void Hw()
        {
            SpellManager.Cast("Holy Wrath"); //Do It!                                               


            Slog("**Holy Wrath**");
        }

        /////////////////////////////////
        //Divine Storm (Req Level 60)
        /////////////////////////////////

        private static void Ds()
        {
            SpellManager.Cast("Divine Storm"); //Do It!                                               


            Slog("**Divine Storm**");
        }

        /////////////////////////////////
        //Hammer of the Righteous (Req Level 61)
        /////////////////////////////////

        private static void HotR()
        {
            SpellManager.Cast("Hammer of the Righteous"); //Do It!


            Slog("**Hammer of the Righteous**");
        }

        /////////////////////////////////
        //Avenging Wrath (Req Level 70)
        /////////////////////////////////

        private static void Aw()
        {
            SpellManager.Cast("Avenging Wrath"); //Do It!


            Slog("**Avenging Wrath**");
        }

        /////////////////////////////////
        //Zealotry (Req Level 81)
        /////////////////////////////////

        private static void Zealotry()
        {
            SpellManager.Cast("Zealotry"); //Do It!


            Slog("**Zealotry**");
        }

        /////////////////////////////////
        //Templar's Verdict (Req Level 81)
        /////////////////////////////////

        private static void TV()
        {
            SpellManager.Cast("Templar's Verdict"); //Do It!


            Slog("**Templar's Verdict**");
        }

        #endregion

        #region Misc *4.0 Cata*

        /////////////////////////////////
        //Every Man for Himself (Req Human)
        /////////////////////////////////

        private static void Em()
        {
            SpellManager.Cast("Every Man for Himself"); //Do It!                


            Slog("**Every Man for Himself**");
        }

        /////////////////////////////////
        //Stoneform (Req Dwarf)
        /////////////////////////////////

        private static void Sf()
        {
            SpellManager.Cast("Stoneform"); //Do It!                


            Slog("**Stoneform**");
        }

        /////////////////////////////////
        //Gift of the Naaru (Req Draenie)
        /////////////////////////////////

        private static void Naaru()
        {
            SpellManager.Cast("Gift of the Naaru"); //Do It!                


            Slog("**Gift of the Naaru**");
        }

        /////////////////////////////////
        //Arcane Torrent (Req Blood Elf)
        /////////////////////////////////

        private static void At()
        {
            SpellManager.Cast("Arcane Torrent"); //Do It!                


            Slog("**Arcane Torrent**");
        }

        /////////////////////////////////
        //Hammer of Justice (Req Level 8)
        /////////////////////////////////

        private static void HoJ()
        {
            SpellManager.Cast("Hammer of Justice"); //Do It!


            Slog("**Hammer of Justice**");
        }

        /////////////////////////////////
        //Aura Mastery (Req 10 Holy Points)
        /////////////////////////////////

        private static void AuraMastery()
        {
            SpellManager.Cast("Aura Mastery"); //Do It!                                               


            Slog("**Aura Mastery**");
        }

        /////////////////////////////////
        //Repentance (Req Level 30)
        /////////////////////////////////

        private static void Repentance()
        {
            SpellManager.Cast("Repentance"); //Do It!                                               


            Slog("**Repentance**");
        }

        /////////////////////////////////
        //Hand of Reckoning (Req Level 16)
        /////////////////////////////////

        private static void HoR()
        {
            SpellManager.Cast("Hand of Reckoning"); //Do It!                                               


            Slog("**Hand of Reckoning**");
        }

        /////////////////////////////////
        //Rebuke (Req Level 25)
        /////////////////////////////////

        private static void Rebuke()
        {
            SpellManager.Cast("Rebuke"); //Do It!                                               


            Slog("**Rebuke**");
        }

        /////////////////////////////////
        //Hand of Freedom (Req Level 52)
        /////////////////////////////////

        private static void HoF()
        {
            SpellManager.Cast("Hand of Freedom"); //Do It! 

            Slog("**Hand of Freedom**");
        }

        /////////////////////////////////////
        //Holy Shock Heal(Req 30 Holy Talents)
        /////////////////////////////////////

        private static void HolyShockHeal()
        {
            Me.Target(); //Target Self

            SpellManager.Cast("Holy Shock"); //Do It!

            Slog("**Holy Shock**");

            Me.TargetLastTarget(); //Target our last target
        }

        /////////////////////////////////////
        //Holy Shock DPS(Req 30 Holy Talents)
        /////////////////////////////////////

        private static void HolyShockDps()
        {
            SpellManager.Cast("Holy Shock"); //Do It!


            Slog("**Holy Shock**");
        }

        /////////////////////////////////
        //Avenger's Shield (Req 40 Prot Points)
        /////////////////////////////////

        private static void As()
        {
            SpellManager.Cast("Avenger's Shield"); //Do It!                                               


            Slog("**Avenger's Shield**");
        }

        /////////////////////////////////
        //Righteous Fury
        /////////////////////////////////

        private static void RighteousFury()
        {
            SpellManager.Cast("Righteous Fury"); //Do It!                


            Slog("**Righteous Fury**");
        }

        /////////////////////////////////
        //LifeBlood (Req Hearbalist)
        /////////////////////////////////

        private static void LifeBlood()
        {
            SpellManager.Cast("Lifeblood"); //Do It!                


            Slog("**Lifeblood**");
        }

        /////////////////////////////////
        //Inquisition
        /////////////////////////////////

        private static void Inquisition()
        {
            SpellManager.Cast("Inquisition"); //Do It!                


            Slog("**Inquisition**");
        }

        #endregion

        #endregion

        #region Global Raid Check *4.0 Cata*

        /////////////////////////////////
        //Simply Check if we are in a raid
        /////////////////////////////////

        public bool InRaid()
        {
            return Me.RaidMembers.Count > 0;
        }

        #endregion

        #region Global Party Check *4.0 Cata*

        /////////////////////////////////
        //Simply Check if we are in a party
        /////////////////////////////////

        public bool InParty()
        {
            return Me.PartyMember1 != null;
        }

        #endregion

        #region Global Leader Check *4.0 Cata*

        /////////////////////////////////
        //Simply Check if we are group leader
        /////////////////////////////////

        public bool IsLeader()
        {
            return Me.IsGroupLeader;
        }

        #endregion

        #endregion
    }
}