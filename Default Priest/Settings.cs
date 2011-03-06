using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Styx;
using Styx.Helpers;
using Styx.WoWInternals;

namespace DefaultPriest
{
    public class DefaultPriestSettings : Settings
    {
        public DefaultPriestSettings()
            : base(Logging.ApplicationPath + "\\Settings\\DefaultPriestSettings_" + StyxWoW.Me.Name + ".xml")
        {
            Load();
        }

        ~DefaultPriestSettings()
        {
            Save();
        }

        [Setting, DefaultValue(true)]
        public bool UsePullPWS { get; set; }        

        [Setting, DefaultValue(true)]
        public bool DotAdds { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseWotF { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseWand { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseEMfHs { get; set; }

        [Setting, DefaultValue(true)]
        public bool DispelMagic { get; set; }

        [Setting, DefaultValue(true)]
        public bool RemoveDisease { get; set; }

        [Setting, DefaultValue(30.0)]
        public double BuffMana { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseCombatPWF { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseCombatInnerFire { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseCombatDivineSpirit { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseCombatFearWard { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseCombatShadowProtection { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseCombatShadowform { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseCombatVampiricEmbrace { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseCombatInnerFocus { get; set; }

        [Setting, DefaultValue(true)]
        public bool UsePreCombatPWF { get; set; }

        [Setting, DefaultValue(true)]
        public bool UsePreCombatInnerFire { get; set; }

        [Setting, DefaultValue(true)]
        public bool UsePreCombatDivineSpirit { get; set; }

        [Setting, DefaultValue(false)]
        public bool UsePreCombatFearWard { get; set; }

        [Setting, DefaultValue(false)]
        public bool UsePreCombatShadowProtection { get; set; }

        [Setting, DefaultValue(true)]
        public bool UsePreCombatShadowform { get; set; }

        [Setting, DefaultValue(true)]
        public bool UsePreCombatVampiricEmbrace { get; set; }

        [Setting, DefaultValue(true)]
        public bool UsePartyPWF { get; set; }

        [Setting, DefaultValue(true)]
        public bool UsePartyDivineSpirit { get; set; }

        [Setting, DefaultValue(false)]
        public bool UsePartyShadowProtection { get; set; }

        [Setting, DefaultValue(40.0)]
        public double RestHealth { get; set; }

        [Setting, DefaultValue(40.0)]
        public double RestMana { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseDispersionWhileResting { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseShadowPvPPsychicHorror { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseShadowPvPPsychicScream { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseShadowPvPDispersion { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseShadowPvPMindFlay { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseShadowPvPMindBlast { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseShadowPvPShadowfiend { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseShadowPvPSWD { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseShadowPvPDevouringPlague { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseShadowPvPSWP { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseShadowPvPVampiricTouch { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseShadowPvPHolyFire { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseShadowPvPSmite { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseShadowPsychicHorror { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseShadowPsychicScream { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseShadowDispersion { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseShadowMindFlay { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseShadowMindBlast { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseShadowShadowfiend { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseShadowSWD { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseShadowMindSear { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseShadowDevouringPlague { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseShadowSWP { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseShadowVampiricTouch { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseShadowHolyFire { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseShadowSmite { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseRaFPsychicHorror { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseRaFPsychicScream { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseRaFDispersion { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseRaFMindFlay { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseRaFMindBlast { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseRaFShadowfiend { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseRaFSWD { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseRaFMindSear { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseRaFDevouringPlague { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseRaFSWP { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseRaFVampiricTouch { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseRaFHolyFire { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseRaFSmite { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseShadowDispersionWhenStunned { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseShadowSilence { get; set; }

        [Setting, DefaultValue(2.0)]
        public double ShadowMindSearAddCount { get; set; }

        [Setting, DefaultValue(30.0)]
        public double ShadowDotHealth { get; set; }

        [Setting, DefaultValue(30.0)]
        public double ShadowDPSHealth { get; set; }

        [Setting, DefaultValue(60.0)]
        public double ShadowShadowfiendMana { get; set; }

        [Setting, DefaultValue(80.0)]
        public double ShadowShadowfiendHealth { get; set; }

        [Setting, DefaultValue(50.0)]
        public double ShadowDispersionMana { get; set; }

        [Setting, DefaultValue(50.0)]
        public double RaFDPSMana { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseShadowPvPPWS { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseShadowPVPRenew { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseShadowPVPFlashHeal { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseShadowPVPHeal { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseShadowPWS { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseShadowRenew { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseShadowFlashHeal { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseShadowHeal { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseRaFPWS { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseRaFRenew { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseRaFFlashHeal { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseRaFHeal { get; set; }

        [Setting, DefaultValue(30.0)]
        public double ShadowPvPPWSHealth { get; set; }

        [Setting, DefaultValue(0.0)]
        public double ShadowPvPRenewHealth { get; set; }

        [Setting, DefaultValue(0.0)]
        public double ShadowPvPFlashHealHealth { get; set; }

        [Setting, DefaultValue(0.0)]
        public double ShadowPvPHealHealth { get; set; }

        [Setting, DefaultValue(70.0)]
        public double ShadowPWSHealth { get; set; }

        [Setting, DefaultValue(0.0)]
        public double ShadowRenewHealth { get; set; }

        [Setting, DefaultValue(0.0)]
        public double ShadowFlashHealHealth { get; set; }

        [Setting, DefaultValue(45.0)]
        public double ShadowHealHealth { get; set; }

        [Setting, DefaultValue(40.0)]
        public double RaFPWSHealth { get; set; }

        [Setting, DefaultValue(80.0)]
        public double RaFRenewHealth { get; set; }

        [Setting, DefaultValue(70.0)]
        public double RaFFlashHealHealth { get; set; }

        [Setting, DefaultValue(45.0)]
        public double RaFHealHealth { get; set; }

        [Setting, DefaultValue(30.0)]
        public double ShadowSWDHealth { get; set; }

        [Setting, DefaultValue(70.0)]
        public double DiscPWSHealth { get; set; }

        [Setting, DefaultValue(90.0)]
        public double DiscRenewHealth { get; set; }

        [Setting, DefaultValue(75.0)]
        public double DiscFlashHealHealth { get; set; }

        [Setting, DefaultValue(45.0)]
        public double DiscHealHealth { get; set; }

        [Setting, DefaultValue(70.0)]
        public double HolyPWSHealth { get; set; }

        [Setting, DefaultValue(90.0)]
        public double HolyRenewHealth { get; set; }

        [Setting, DefaultValue(75.0)]
        public double HolyFlashHealHealth { get; set; }

        [Setting, DefaultValue(45.0)]
        public double HolyHealHealth { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseDiscPWS { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseDiscRenew { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseDiscFlashHeal { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseDiscHeal { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseHolyPWS { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseHolyRenew { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseHolyFlashHeal { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseHolyHeal { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseDiscPenance { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseDiscPrayerOfHealing { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseHolyPrayerOfHealing { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseDiscPainSuppression { get; set; }

        [Setting, DefaultValue(60.0)]
        public double DiscPenanceHealth { get; set; }

        [Setting, DefaultValue(50.0)]
        public double DiscPainSuppressionHealth { get; set; }

        [Setting, DefaultValue(3.0)]
        public double DiscPrayerOfHealingCount { get; set; }

        [Setting, DefaultValue(3.0)]
        public double HolyPrayerOfHealingCount { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseDiscPrayerOfMending { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseHolyPrayerOfMending { get; set; }

        [Setting, DefaultValue(85.0)]
        public double DiscPrayerOfHealingHealth { get; set; }

        [Setting, DefaultValue(85.0)]
        public double HolyPrayerOfHealingHealth { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseDiscPvPPWS { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseDiscPvPRenew { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseDiscPvPFlashHeal { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseDiscPvPHeal { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseDiscPvPPenance { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseDiscPvPPainSuppression { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseDiscPvPPrayerOfHealing { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseDiscPvPPrayerOfMending { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseHolyPvPPWS { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseHolyPvPRenew { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseHolyPvPFlashHeal { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseHolyPvPHeal { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseHolyPvPPrayerOfHealing { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseHolyPvPPrayerOfMending { get; set; }

        [Setting, DefaultValue(45.0)]
        public double DiscPvPHealHealth { get; set; }

        [Setting, DefaultValue(70.0)]
        public double DiscPvPPWSHealth { get; set; }

        [Setting, DefaultValue(90.0)]
        public double DiscPvPRenewHealth { get; set; }

        [Setting, DefaultValue(75.0)]
        public double DiscPvPFlashHealHealth { get; set; }

        [Setting, DefaultValue(60.0)]
        public double DiscPvPPenanceHealth { get; set; }

        [Setting, DefaultValue(50.0)]
        public double DiscPvPPainSuppressionHealth { get; set; }

        [Setting, DefaultValue(3.0)]
        public double DiscPvPPrayerOfHealingCount { get; set; }

        [Setting, DefaultValue(80.0)]
        public double DiscPvPPrayerOfHealingHealth { get; set; }

        [Setting, DefaultValue(45.0)]
        public double HolyPvPHealHealth { get; set; }

        [Setting, DefaultValue(70.0)]
        public double HolyPvPPWSHealth { get; set; }

        [Setting, DefaultValue(90.0)]
        public double HolyPvPRenewHealth { get; set; }

        [Setting, DefaultValue(75.0)]
        public double HolyPvPFlashHealHealth { get; set; }

        [Setting, DefaultValue(3.0)]
        public double HolyPvPPrayerOfHealingCount { get; set; }

        [Setting, DefaultValue(80.0)]
        public double HolyPvPPrayerOfHealingHealth { get; set; }

        [Setting, DefaultValue(true)]
        public bool UsePrayers { get; set; }

        [Setting, DefaultValue(0.0)]
        public double ShadowDPSMana { get; set; }

        [Setting, DefaultValue(75)]
        public int GrindingPullDistance { get; set; }

        [Setting, DefaultValue(30)]
        public int PvPPullDistance { get; set; }

        [Setting, DefaultValue(1)]
        public int HealPullDistance { get; set; }

        [Setting, DefaultValue(10.0)]
        public double ManaPotMana { get; set; }

        [Setting, DefaultValue(10.0)]
        public double HealthPotHealth { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseManaPot { get; set; }

        [Setting, DefaultValue(false)]
        public bool UseHealthPot { get; set; }

        [Setting, DefaultValue(false)]
        public bool DispelOnlyOOC { get; set; }

        [Setting, DefaultValue(true)]
        public bool PullWithMB { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseDiscSmite { get; set; }

        [Setting, DefaultValue(20.0)]
        public double DiscSmiteMana { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseDiscPvPSmite { get; set; }

        [Setting, DefaultValue(20.0)]
        public double DiscPvPSmiteMana { get; set; }

        [Setting, DefaultValue(20.0)]
        public double DiscGreaterHealHealth { get; set; }

        [Setting, DefaultValue(20.0)]
        public double DiscPvPGreaterHealHealth { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseDiscGreaterHeal { get; set; }

        [Setting, DefaultValue(true)]
        public bool UseDiscPvPGreaterHeal { get; set; }

        [Setting, DefaultValue(true)]
        public bool DruidUseInnervate { get; set; }

        [Setting, DefaultValue(true)]
        public bool DruidUseBarkskin { get; set; }

        [Setting, DefaultValue(true)]
        public bool DruidUseTreeOfLife { get; set; }

        [Setting, DefaultValue(true)]
        public bool DruidUsePvPTreeOfLife { get; set; }

        [Setting, DefaultValue(40)]
        public double DruidInnervateMana { get; set; }

        [Setting, DefaultValue(70)]
        public double DruidBarkskinHealth { get; set; }

        [Setting, DefaultValue(3)]
        public double DruidTreeOfLifeCount { get; set; }

        [Setting, DefaultValue(80)]
        public double DruidTreeOfLifeHealth { get; set; }

        [Setting, DefaultValue(3)]
        public double DruidPvPTreeOfLifeCount { get; set; }

        [Setting, DefaultValue(80)]
        public double DruidPvPTreeOfLifeHealth { get; set; }

        [Setting, DefaultValue(true)]
        public bool DruidUseSwiftmend { get; set; }

        [Setting, DefaultValue(true)]
        public bool DruidUseRegrowth { get; set; }

        [Setting, DefaultValue(true)]
        public bool DruidUseHealingTouch { get; set; }

        [Setting, DefaultValue(true)]
        public bool DruidUseNourish { get; set; }

        [Setting, DefaultValue(true)]
        public bool DruidUseRejuvenation { get; set; }

        [Setting, DefaultValue(true)]
        public bool DruidUseTranquility { get; set; }

        [Setting, DefaultValue(true)]
        public bool DruidUseWildGrowth { get; set; }

        [Setting, DefaultValue(true)]
        public bool DruidUseThorns { get; set; }

        [Setting, DefaultValue(true)]
        public bool DruidUseFF { get; set; }

        [Setting, DefaultValue(true)]
        public bool DruidUseInnervateOnPlayer { get; set; }

        [Setting, DefaultValue(true)]
        public bool DruidUsePvPSwiftmend { get; set; }

        [Setting, DefaultValue(true)]
        public bool DruidUsePvPRegrowth { get; set; }

        [Setting, DefaultValue(true)]
        public bool DruidUsePvPHealingTouch { get; set; }

        [Setting, DefaultValue(true)]
        public bool DruidUsePvPNourish { get; set; }

        [Setting, DefaultValue(true)]
        public bool DruidUsePvPRejuvenation { get; set; }

        [Setting, DefaultValue(true)]
        public bool DruidUsePvPTranquility { get; set; }

        [Setting, DefaultValue(true)]
        public bool DruidUsePvPWildGrowth { get; set; }

        [Setting, DefaultValue(true)]
        public bool DruidUsePvPThorns { get; set; }

        [Setting, DefaultValue(true)]
        public bool DruidUsePvPFF { get; set; }

        [Setting, DefaultValue(true)]
        public bool DruidUsePvPInnervateOnPlayer { get; set; }

        [Setting, DefaultValue(65)]
        public double DruidSwiftmendHealth { get; set; }

        [Setting, DefaultValue(70)]
        public double DruidRegrowthHealth { get; set; }

        [Setting, DefaultValue(60)]
        public double DruidHealingTouchHealth { get; set; }

        [Setting, DefaultValue(75)]
        public double DruidNourishHealth { get; set; }

        [Setting, DefaultValue(90)]
        public double DruidRejuvenationHealth { get; set; }

        [Setting, DefaultValue(3)]
        public double DruidTranquilityCount { get; set; }

        [Setting, DefaultValue(60)]
        public double DruidTranquilityHealth { get; set; }

        [Setting, DefaultValue(2)]
        public double DruidWildGrowthCount { get; set; }

        [Setting, DefaultValue(80)]
        public double DruidWildGrowthHealth { get; set; }

        [Setting, DefaultValue(65)]
        public double DruidPvPSwiftmendHealth { get; set; }

        [Setting, DefaultValue(70)]
        public double DruidPvPRegrowthHealth { get; set; }

        [Setting, DefaultValue(60)]
        public double DruidPvPHealingTouchHealth { get; set; }

        [Setting, DefaultValue(75)]
        public double DruidPvPNourishHealth { get; set; }

        [Setting, DefaultValue(90)]
        public double DruidPvPRejuvenationHealth { get; set; }

        [Setting, DefaultValue(3)]
        public double DruidPvPTranquilityCount { get; set; }

        [Setting, DefaultValue(60)]
        public double DruidPvPTranquilityHealth { get; set; }

        [Setting, DefaultValue(2)]
        public double DruidPvPWildGrowthCount { get; set; }

        [Setting, DefaultValue(80)]
        public double DruidPvPWildGrowthHealth { get; set; }

        [Setting, DefaultValue(false)]
        public bool DruidTankMode { get; set; }
    }
}