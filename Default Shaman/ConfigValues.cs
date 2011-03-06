
namespace ShamWOW
{
    using System.Linq;
    using System.Xml.Linq;

    /*************************************************************************
         * private class that manages defaults and access to configurable values.
         * supports loading values from XML file
         *************************************************************************/
    public class ConfigValues
    {
        public enum TypeOfPull { Fast, Ranged, Body, Auto }
        public enum PveCombatStyle { Normal, DisableTotemsCDs }
        public enum PvpCombatStyle { CombatOnly, HealingOverCombat, HealingOnly }
        public enum RafCombatStyle { CombatOnly, HealingOverCombat, HealingOnly }

        public const int PullTimeout = 15000;
        public const int waitWaitAfterRangedPull = 250;
        public const bool NearbyPlayerWarnings = false;
        public const bool BeepIfPlayerTargetsMe = false;

        public bool Debug = true;
        public bool UseGhostWolfForm = true;
        public int DistanceForGhostWolf = 35;
        public int RestHealthPercent = 60;
        public int RestManaPercent = 49;
        public int NeedHealHealthPercent = 50;
        public int EmergencyHealthPercent = 35;
        public int EmergencyManaPercent = 15;
        public int LifebloodPercent = 70;
        public int ShamanisticRagePercent = 65;
        public int ThunderstormPercent = 65;
        public bool UseBandages = true;
        public int DistanceForTotemRecall = 30;
        public int TwistManaPercent = 65;
        public int TwistDamagePercent = 85;
        public bool DisableMovement = false;
        public bool MeleeCombatBeforeLevel10 = false;

        public PveCombatStyle PVE_CombatStyle = PveCombatStyle.Normal;
        public TypeOfPull PVE_PullType = TypeOfPull.Fast;
        public bool PVE_SaveForStress_Bloodlust = true;
        public bool PVE_SaveForStress_DPS_Racials = false;
        public bool PVE_SaveForStress_ElementalTotems = true;
        public bool PVE_SaveForStress_FeralSpirit = true;
        public bool PVE_SaveForStress_TotemsSelected = false;
        public int PVE_LevelsAboveAsElite = 3;
        public int PVE_StressfulMobCount = 2;
        public string PVE_TotemEarth = "Auto";
        public string PVE_TotemFire = "Auto";
        public string PVE_TotemWater = "Auto";
        public string PVE_TotemAir = "Auto";
        public string PVE_MainhandEnchant = "Auto";
        public string PVE_OffhandEnchant = "Auto";

        public PvpCombatStyle PVP_CombatStyle = PvpCombatStyle.CombatOnly;
        public int PVP_GroupNeedHeal = 85;
        public string PVP_TotemEarth = "Auto";
        public string PVP_TotemFire = "Auto";
        public string PVP_TotemWater = "Auto";
        public string PVP_TotemAir = "Auto";
        public string PVP_MainhandEnchant = "Auto";
        public string PVP_OffhandEnchant = "Auto";

        public RafCombatStyle RAF_CombatStyle = RafCombatStyle.CombatOnly;
        public int RAF_GroupNeedHeal = 85;
        public string RAF_TotemEarth = "Auto";
        public string RAF_TotemFire = "Auto";
        public string RAF_TotemWater = "Auto";
        public string RAF_TotemAir = "Auto";
        public bool RAF_UseBloodlustOnBosses = false;
        public bool RAF_SaveFeralSpiritForBosses = true;
        public bool RAF_UseThunderstorm = true;
        public bool RAF_SaveElementalTotemsForBosses = true;

        //**** Calculated values for convenience
        public bool TotemsDisabled
        { get { return PVE_CombatStyle == PveCombatStyle.DisableTotemsCDs && !Styx.Logic.Battlegrounds.IsInsideBattleground; } }

        public bool PVP_Healer()
        {
            return PVP_CombatStyle != PvpCombatStyle.CombatOnly;
        }

        public bool RAF_Healer()
        {
            return RAF_CombatStyle != RafCombatStyle.CombatOnly;
        }

        //**** Dump current settings to debug output
        public void DebugDump()
        {
            // General
            ShamWOW.Dlog("  #-- GENERAL SETTINGS -----#");
            ShamWOW.Dlog("  # Debug:                  '{0}'", this.Debug);
            ShamWOW.Dlog("  # UseGhostWolfForm:       '{0}'", this.UseGhostWolfForm);
            ShamWOW.Dlog("  # DistForGhostWolf:       '{0}'", this.DistanceForGhostWolf);
            ShamWOW.Dlog("  # UseBandages:            '{0}'", this.UseBandages);
            ShamWOW.Dlog("  # DistanceForTotemRecall: '{0}'", this.DistanceForTotemRecall);
            ShamWOW.Dlog("  # RestHealthPercent:      '{0}'", this.RestHealthPercent);
            ShamWOW.Dlog("  # RestManaPercent:        '{0}'", this.RestManaPercent);
            ShamWOW.Dlog("  # TwistDamagePercent:     '{0}'", this.TwistDamagePercent);
            ShamWOW.Dlog("  # TwistManaPercent:       '{0}'", this.TwistManaPercent);
            ShamWOW.Dlog("  # NeedHealHealthPercent:  '{0}'", this.NeedHealHealthPercent);
            ShamWOW.Dlog("  # EmergencyHealthPercent: '{0}'", this.EmergencyHealthPercent);
            ShamWOW.Dlog("  # EmergencyManaPercent:   '{0}'", this.EmergencyManaPercent);
            ShamWOW.Dlog("  # LifebloodPercent:       '{0}'", this.LifebloodPercent);
            ShamWOW.Dlog("  # ShamanisticRagePercent: '{0}'", this.ShamanisticRagePercent);
            ShamWOW.Dlog("  # DisableMovement:        '{0}'", this.DisableMovement);
            ShamWOW.Dlog("  # MeleeCombatBeforeLvl10: '{0}'", this.MeleeCombatBeforeLevel10);

            // PVE Grinding
            ShamWOW.Dlog("  #-- PVE SETTINGS ---------#");
            ShamWOW.Dlog("  # PVE_CombatStyle:        '{0}'", this.PVE_CombatStyle);
            ShamWOW.Dlog("  # PVE_LevelsAboveAsElite: '{0}'", this.PVE_LevelsAboveAsElite);
            ShamWOW.Dlog("  # PVE_StressfulMobCount:  '{0}'", this.PVE_StressfulMobCount);
            ShamWOW.Dlog("  # PVE_PullType:           '{0}'", this.PVE_PullType);
            ShamWOW.Dlog("  # PVE_MainhandEnchant:    '{0}'", this.PVE_MainhandEnchant);
            ShamWOW.Dlog("  # PVE_OffhandEnchant:     '{0}'", this.PVE_OffhandEnchant);
            ShamWOW.Dlog("  # PVE_TotemEarth:         '{0}'", this.PVE_TotemEarth);
            ShamWOW.Dlog("  # PVE_TotemFire:          '{0}'", this.PVE_TotemFire);
            ShamWOW.Dlog("  # PVE_TotemWater:         '{0}'", this.PVE_TotemWater);
            ShamWOW.Dlog("  # PVE_TotemAir:           '{0}'", this.PVE_TotemAir);
            ShamWOW.Dlog("  # PVE_Save_Totems:        '{0}'", this.PVE_SaveForStress_TotemsSelected);
            ShamWOW.Dlog("  # PVE_Save_FeralSpirit:   '{0}'", this.PVE_SaveForStress_FeralSpirit);
            ShamWOW.Dlog("  # PVE_Save_Elementals:    '{0}'", this.PVE_SaveForStress_ElementalTotems);
            ShamWOW.Dlog("  # PVE_Save_DPS_Racials:   '{0}'", this.PVE_SaveForStress_DPS_Racials);
            ShamWOW.Dlog("  # PVE_Save_Bloodlust:     '{0}'", this.PVE_SaveForStress_Bloodlust);

            // PVP
            ShamWOW.Dlog("  #-- PVP SETTINGS ---------#");
            ShamWOW.Dlog("  # PVP_CombatStyle:        '{0}'", this.PVP_CombatStyle);
            ShamWOW.Dlog("  # PVP_Healer:             '{0}'", this.PVP_Healer());
            ShamWOW.Dlog("  # PVP_GroupNeedHeal:      '{0}'", this.PVP_GroupNeedHeal);
            ShamWOW.Dlog("  # PVP_MainhandEnchant:    '{0}'", this.PVP_MainhandEnchant);
            ShamWOW.Dlog("  # PVP_OffhandEnchant:     '{0}'", this.PVP_OffhandEnchant);
            ShamWOW.Dlog("  # PVP_TotemEarth:         '{0}'", this.PVP_TotemEarth);
            ShamWOW.Dlog("  # PVP_TotemFire:          '{0}'", this.PVP_TotemFire);
            ShamWOW.Dlog("  # PVP_TotemWater:         '{0}'", this.PVP_TotemWater);
            ShamWOW.Dlog("  # PVP_TotemAir:           '{0}'", this.PVP_TotemAir);

            // RAF
            ShamWOW.Dlog("  #-- RAF SETTINGS ---------#");
            ShamWOW.Dlog("  # RAF_CombatStyle:        '{0}'", this.RAF_CombatStyle);
            ShamWOW.Dlog("  # RAF_Healer:             '{0}'", this.RAF_Healer());
            ShamWOW.Dlog("  # RAF_GroupNeedHeal:      '{0}'", this.RAF_GroupNeedHeal);
            ShamWOW.Dlog("  # RAF_TotemEarth:         '{0}'", this.RAF_TotemEarth);
            ShamWOW.Dlog("  # RAF_TotemFire:          '{0}'", this.RAF_TotemFire);
            ShamWOW.Dlog("  # RAF_TotemWater:         '{0}'", this.RAF_TotemWater);
            ShamWOW.Dlog("  # RAF_TotemAir:           '{0}'", this.RAF_TotemAir);
            ShamWOW.Dlog("  # RAF_UseBloodlust:       '{0}'", this.RAF_UseBloodlustOnBosses);
            ShamWOW.Dlog("  # RAF_SaveFeralSpirit:    '{0}'", this.RAF_SaveFeralSpiritForBosses);
            ShamWOW.Dlog("  # RAF_UseThunderstorm:    '{0}'", this.RAF_UseThunderstorm);

            ShamWOW.Dlog("  #-- SETTINGS END --------#");
        }



        public ConfigValues()
        {
        }

        public bool FileLoad(string sFilename)
        {
            XElement toplvl = XElement.Load(sFilename);
            XElement[] elements = toplvl.Elements().ToArray();
            foreach (XElement elem in elements)
            {
                switch (elem.Name.ToString())
                {
                    case "debug":
                        LoadBool(elem, ref Debug); break;

                    case "useghostwolf":
                        LoadBool(elem, ref UseGhostWolfForm); break;
                    case "safedistanceforghostwolf":
                        LoadInt(elem, ref DistanceForGhostWolf); break;
                    case "restminmana":
                        LoadInt(elem, ref RestManaPercent); break;
                    case "restminhealth":
                        LoadInt(elem, ref RestHealthPercent); break;
                    case "needheal":
                        LoadInt(elem, ref NeedHealHealthPercent); break;
                    case "emergencyhealth":
                        LoadInt(elem, ref EmergencyHealthPercent); break;
                    case "emergencymana":
                        LoadInt(elem, ref EmergencyManaPercent); break;
                    case "needlifeblood":
                        LoadInt(elem, ref LifebloodPercent); break;
                    case "needshamanisticrage":
                        LoadInt(elem, ref ShamanisticRagePercent); break;
                    case "needthunderstorm":
                        LoadInt(elem, ref ThunderstormPercent); break;
                    case "usebandages":
                        LoadBool(elem, ref UseBandages); break;
                    case "totemrecalldistance":
                        LoadInt(elem, ref DistanceForTotemRecall); break;
                    case "twistwatershield":
                        LoadInt(elem, ref TwistManaPercent); break;
                    case "twistlightningshield":
                        LoadInt(elem, ref TwistDamagePercent); break;
                    case "disablemovement":
                        LoadBool(elem, ref DisableMovement); break;
                    case "meleecombatbeforelevel10":
                        LoadBool(elem, ref MeleeCombatBeforeLevel10); break;

                    case "pve_combatstyle":
                        LoadPveCombatStyle(elem, ref PVE_CombatStyle); break;
                    case "pve_typeofpull":
                        LoadTypeOfPull(elem, ref PVE_PullType); break;
                    case "pve_stressonly_feralspirit":
                        LoadBool(elem, ref PVE_SaveForStress_FeralSpirit); break;
                    case "pve_stressonly_elementaltotems":
                        LoadBool(elem, ref PVE_SaveForStress_ElementalTotems); break;
                    case "pve_stressonly_dps_racials":
                        LoadBool(elem, ref PVE_SaveForStress_DPS_Racials); break;
                    case "pve_stressonly_bloodlust":
                        LoadBool(elem, ref PVE_SaveForStress_Bloodlust); break;
                    case "pve_stressonly_totembar":
                        LoadBool(elem, ref PVE_SaveForStress_TotemsSelected); break;
                    case "pve_stresslevelsabove":
                        LoadInt(elem, ref PVE_LevelsAboveAsElite); break;
                    case "pve_stressfulmobcount":
                        LoadInt(elem, ref PVE_StressfulMobCount); break;
                    case "pve_totemearth":
                        LoadStr(elem, ref PVE_TotemEarth); break;
                    case "pve_totemfire":
                        LoadStr(elem, ref PVE_TotemFire); break;
                    case "pve_totemwater":
                        LoadStr(elem, ref PVE_TotemWater); break;
                    case "pve_totemair":
                        LoadStr(elem, ref PVE_TotemAir); break;
                    case "pve_mainhand":
                        LoadStr(elem, ref PVE_MainhandEnchant); break;
                    case "pve_offhand":
                        LoadStr(elem, ref PVE_OffhandEnchant); break;

                    case "pvp_combatstyle":
                        LoadPvpCombatStyle(elem, ref PVP_CombatStyle); break;
                    case "pvp_groupneedheal":
                        LoadInt(elem, ref PVP_GroupNeedHeal); break;
                    case "pvp_totemearth":
                        LoadStr(elem, ref PVP_TotemEarth); break;
                    case "pvp_totemfire":
                        LoadStr(elem, ref PVP_TotemFire); break;
                    case "pvp_totemwater":
                        LoadStr(elem, ref PVP_TotemWater); break;
                    case "pvp_totemair":
                        LoadStr(elem, ref PVP_TotemAir); break;
                    case "pvp_mainhand":
                        LoadStr(elem, ref PVP_MainhandEnchant); break;
                    case "pvp_offhand":
                        LoadStr(elem, ref PVP_OffhandEnchant); break;

                    case "raf_combatstyle":
                        LoadRafCombatStyle(elem, ref RAF_CombatStyle); break;
                    case "raf_groupneedheal":
                        LoadInt(elem, ref RAF_GroupNeedHeal); break;
                    case "raf_usethunderstorm":
                        LoadBool(elem, ref RAF_UseThunderstorm); break;
                    case "raf_usebloodlustonbosses":
                        LoadBool(elem, ref RAF_UseBloodlustOnBosses); break;
                    case "raf_saveferalspiritforbosses":
                        LoadBool(elem, ref RAF_SaveFeralSpiritForBosses); break;
                    case "raf_totemearth":
                        LoadStr(elem, ref RAF_TotemEarth); break;
                    case "raf_totemfire":
                        LoadStr(elem, ref RAF_TotemFire); break;
                    case "raf_totemwater":
                        LoadStr(elem, ref RAF_TotemWater); break;
                    case "raf_totemair":
                        LoadStr(elem, ref RAF_TotemAir); break;
                    case "raf_saveelementaltotemsforbosses":
                        LoadBool(elem, ref RAF_SaveElementalTotemsForBosses); break;

                    default:
                        ShamWOW.Dlog("error: unknown config setting: {0}={1}", elem.Name, elem.Value.ToString());
                        break;
                }
            }

            return true;
        }

        private static void LoadBool(XElement elem, ref bool value)
        {
            bool localVal;
            if (!bool.TryParse(elem.Value, out localVal))
            {
                localVal = value;
                ShamWOW.Slog(
                    "config:  setting '{0}' invalid - expected True/False but read '{1}' - defaulting to '{2}'",
                    elem.Name,
                    elem.Value,
                    localVal
                    );
            }
            value = localVal;
        }
        private static void LoadStr(XElement elem, ref string value)
        {
            value = elem.Value;
        }
        private static void LoadInt(XElement elem, ref int value)
        {
            int localVal;
            if (!int.TryParse(elem.Value, out localVal))
            {
                localVal = value;
                ShamWOW.Slog(
                    "config:  setting '{0}' invalid - expected integer but read '{1}' - defaulting to '{2}'",
                    elem.Name,
                    elem.Value,
                    localVal
                    );
            }
            value = localVal;
        }


        private static void LoadTypeOfPull(XElement elem, ref TypeOfPull value)
        {
            switch (elem.Value.ToUpper()[0])
            {
                case 'A':
                    value = TypeOfPull.Auto;
                    break;
                case 'B':
                    value = TypeOfPull.Body;
                    break;
                case 'R':
                    value = TypeOfPull.Ranged;
                    break;
                case 'F':
                    value = TypeOfPull.Fast;
                    break;
                default:
                    ShamWOW.Slog(
                        "config:  setting '{0}' invalid - expected integer but read '{1}' - defaulting to '{2}'",
                        elem.Name,
                        elem.Value,
                        value
                        );
                    break;
            }
        }


        private static void LoadPveCombatStyle(XElement elem, ref PveCombatStyle value)
        {
            switch (elem.Value.ToUpper()[0])
            {
                case 'N':
                    value = PveCombatStyle.Normal;
                    break;
                case 'D':
                    value = PveCombatStyle.DisableTotemsCDs;
                    break;
                default:
                    ShamWOW.Slog(
                        "config:  setting '{0}' invalid - expected pve combat style but read '{1}' - defaulting to '{2}'",
                        elem.Name,
                        elem.Value,
                        value
                        );
                    break;
            }
        }


        private static void LoadPvpCombatStyle(XElement elem, ref PvpCombatStyle value)
        {
            switch (elem.Value.ToLower())
            {
                case "combatonly":
                    value = PvpCombatStyle.CombatOnly;
                    break;
                case "healingovercombat":
                    value = PvpCombatStyle.HealingOverCombat;
                    break;
                case "healingonly":
                    value = PvpCombatStyle.HealingOnly;
                    break;
                default:
                    ShamWOW.Slog(
                        "config:  setting '{0}' invalid - expected pvp combat style but read '{1}' - defaulting to '{2}'",
                        elem.Name,
                        elem.Value,
                        value
                        );
                    break;
            }
        }


        private static void LoadRafCombatStyle(XElement elem, ref RafCombatStyle value)
        {
            switch (elem.Value.ToLower())
            {
                case "combatonly":
                    value = RafCombatStyle.CombatOnly;
                    break;
                case "healingovercombat":
                    value = RafCombatStyle.HealingOverCombat;
                    break;
                case "healingonly":
                    value = RafCombatStyle.HealingOnly;
                    break;
                default:
                    ShamWOW.Slog(
                        "config:  setting '{0}' invalid - expected Raf combat style but read '{1}' - defaulting to '{2}'",
                        elem.Name,
                        elem.Value,
                        value
                        );
                    break;
            }
        }


        public void Save(string sFilename)
        {
            XDocument doc = new XDocument();
            doc.Add(new XElement("ShamWOW",
                        new XElement("debug", Debug),

                        new XElement("useghostwolf", UseGhostWolfForm),
                        new XElement("safedistanceforghostwolf", DistanceForGhostWolf),
                        new XElement("restminmana", RestManaPercent),
                        new XElement("restminhealth", RestHealthPercent),
                        new XElement("needheal", NeedHealHealthPercent),
                        new XElement("emergencyhealth", EmergencyHealthPercent),
                        new XElement("emergencymana", EmergencyManaPercent),
                        new XElement("needlifeblood", LifebloodPercent),
                        new XElement("usebandages", UseBandages),
                        new XElement("totemrecalldistance", DistanceForTotemRecall),
                        new XElement("twistwatershield", TwistManaPercent),
                        new XElement("twistlightningshield", TwistDamagePercent),
                        new XElement("disablemovement", DisableMovement),
                        new XElement("meleecombatbeforelevel10", MeleeCombatBeforeLevel10),


                        new XElement("pve_combatstyle", PVE_CombatStyle),
                        new XElement("pve_typeofpull", PVE_PullType),
                        new XElement("pve_stressonly_feralspirit", PVE_SaveForStress_FeralSpirit),
                        new XElement("pve_stressonly_elementaltotems", PVE_SaveForStress_ElementalTotems),
                        new XElement("pve_stressonly_dps_racials", PVE_SaveForStress_DPS_Racials),
                        new XElement("pve_stressonly_bloodlust", PVE_SaveForStress_Bloodlust),
                        new XElement("pve_stressonly_totembar", PVE_SaveForStress_TotemsSelected),
                        new XElement("pve_stresslevelsabove", PVE_LevelsAboveAsElite),
                        new XElement("pve_stressfulmobcount", PVE_StressfulMobCount),
                        new XElement("pve_totemearth", PVE_TotemEarth),
                        new XElement("pve_totemfire", PVE_TotemFire),
                        new XElement("pve_totemwater", PVE_TotemWater),
                        new XElement("pve_totemair", PVE_TotemAir),
                        new XElement("pve_mainhand", PVE_MainhandEnchant),
                        new XElement("pve_offhand", PVE_OffhandEnchant),

                        new XElement("pvp_combatstyle", PVP_CombatStyle),
                        new XElement("pvp_groupneedheal", PVP_GroupNeedHeal),
                        new XElement("pvp_totemearth", PVP_TotemEarth),
                        new XElement("pvp_totemfire", PVP_TotemFire),
                        new XElement("pvp_totemwater", PVP_TotemWater),
                        new XElement("pvp_totemair", PVP_TotemAir),
                        new XElement("pvp_mainhand", PVP_MainhandEnchant),
                        new XElement("pvp_offhand", PVP_OffhandEnchant),

                        new XElement("raf_combatstyle", RAF_CombatStyle),
                        new XElement("raf_groupneedheal", RAF_GroupNeedHeal),
                        new XElement("raf_usethunderstorm", RAF_UseThunderstorm),
                        new XElement("raf_usebloodlustonbosses", RAF_UseBloodlustOnBosses),
                        new XElement("raf_saveferalspiritforbosses", RAF_SaveFeralSpiritForBosses),
                        new XElement("raf_saveelementaltotemsforbosses", RAF_SaveElementalTotemsForBosses),
                        new XElement("raf_totemearth", RAF_TotemEarth),
                        new XElement("raf_totemfire", RAF_TotemFire),
                        new XElement("raf_totemwater", RAF_TotemWater),
                        new XElement("raf_totemair", RAF_TotemAir)
                        )
                    );

            doc.Save(sFilename);
        }

    }

}
