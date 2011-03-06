using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic;
using Styx.Logic.POI;
using Styx.Helpers;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;

namespace DefaultPriest
{
    public partial class Priest : CombatRoutine
    {
        #region Dps

        private Composite PriestShadowDpsRotation
        {
            get
            {
                return new PrioritySelector(
                            CreateSpellCheckAndCast("Arcane Torrent",
                                    ret => CT.IsCasting &&
                                           CT.Distance < 10 &&
                                           Instance.Settings.UseShadowSilence),
                            CreateSpellCheckAndCast("Silence",
                                    ret => CT.IsCasting &&
                                           Instance.Settings.UseShadowSilence),
                            CreateSpellCheckAndCast("Mind Blast",
                                    ret => !Me.Combat && Instance.Settings.PullWithMB),
                            CreateSpellWithAuraCheckAndCast("Vampiric Touch",
                                    ret => ((CT.HealthPercent >= (double)Instance.Settings.ShadowDotHealth &&
                                           Me.ManaPercent >= (double)Instance.Settings.ShadowDPSMana) ||
                                           Me.IsInInstance) &&
                                           Instance.Settings.UseShadowVampiricTouch),
                            CreateSpellCheckAndCast("Shadowfiend",
                                    ret => CT.HealthPercent > (double)Instance.Settings.ShadowShadowfiendHealth &&
                                           Me.ManaPercent <= (double)Instance.Settings.ShadowShadowfiendMana &&
                                           Instance.Settings.UseShadowShadowfiend),
                            CreateSpellWithAuraCheckAndCast("Devouring Plague",
                                    ret => ((CT.HealthPercent >= (double)Instance.Settings.ShadowDotHealth &&
                                           Me.ManaPercent >= (double)Instance.Settings.ShadowDPSMana) ||
                                           Me.IsInInstance) &&
                                           Instance.Settings.UseShadowDevouringPlague),
                            CreateSpellWithAuraCheckAndCast("Shadow Word: Pain",
                                    ret => ((CT.HealthPercent >= (double)Instance.Settings.ShadowDotHealth &&
                                           Me.ManaPercent >= (double)Instance.Settings.ShadowDPSMana) ||
                                           Me.IsInInstance) &&
                                           Instance.Settings.UseShadowSWP),
                            CreateSpellCheckAndCast("Dispersion",
                                    ret => Me.ManaPercent <= (double)Instance.Settings.ShadowDispersionMana &&
                                           Instance.Settings.UseShadowDispersion),
                            CreateSpellCheckAndCast("Psychic Scream",
                                    ret => Adds.Count > 0 &&
                                           Instance.Settings.UseShadowPsychicScream),
                            CreateSpellWithAuraCheckAndCast("Psychic Horror",
                                    ret => Instance.Settings.UseShadowPsychicHorror),
                            CreateSpellCheckAndCast("Shadow Word: Death",
                                    ret => CT.HealthPercent <= (double)Instance.Settings.ShadowSWDHealth &&
                                           Instance.Settings.UseShadowSWD),
                            CreateSpellCheckAndCast("Mind Sear",
                                    ret => Adds.Count >= Instance.Settings.ShadowMindSearAddCount &&
                                           (Me.ManaPercent >= (double)Instance.Settings.ShadowDPSMana ||
                                           Me.IsInInstance) &&
                                           Instance.Settings.UseShadowMindSear),
                            CreateSpellCheckAndCast("Mind Blast",
                                    ret => ((CT.HealthPercent >= (double)Instance.Settings.ShadowDPSHealth &&
                                           Me.ManaPercent >= (double)Instance.Settings.ShadowDPSMana) ||
                                           Me.IsInInstance ) &&
                                           Instance.Settings.UseShadowMindBlast),
                            CreateSpellCheckAndCast("Holy Fire",
                                    ret => Me.Shapeshift != ShapeshiftForm.Shadow &&
                                           ((CT.HealthPercent >= (double)Instance.Settings.ShadowDPSHealth &&
                                           Me.ManaPercent >= (double)Instance.Settings.ShadowDPSMana) ||
                                           Me.IsInInstance) &&
                                           Instance.Settings.UseShadowHolyFire),
                            CreateSpellCheckAndCast("Smite",
                                    ret => Me.Shapeshift != ShapeshiftForm.Shadow &&
                                           ((CT.HealthPercent >= (double)Instance.Settings.ShadowDPSHealth &&
                                           Me.ManaPercent >= (double)Instance.Settings.ShadowDPSMana) ||
                                           Me.IsInInstance) &&
                                           Instance.Settings.UseShadowSmite),
                            CreateSpellCheckAndCast("Mind Flay",
                                    ret => ((CT.HealthPercent >= (double)Instance.Settings.ShadowDPSHealth &&
                                           Me.ManaPercent >= (double)Instance.Settings.ShadowDPSMana) ||
                                           Me.IsInInstance) &&
                                           Instance.Settings.UseShadowMindFlay),
                            CheckAndUseWand());
            }
        }

        #endregion

        #region Dps PvP

        private Composite PriestShadowPvPDpsRotation
        {
            get
            {
                return new PrioritySelector(
                            CreateSpellCheckAndCast("Arcane Torrent",
                                    ret => CT.IsCasting &&
                                           CT.Distance < 10 &&
                                           Instance.Settings.UseShadowSilence),
                            CreateSpellCheckAndCast("Silence",
                                    ret => CT.IsCasting &&
                                           Instance.Settings.UseShadowSilence),
                            CreateSpellCheckAndCast("Psychic Scream",
                                    ret => EnemyNearby &&
                                           Instance.Settings.UseShadowPvPPsychicScream),
                            CreateSpellCheckAndCast("Shadow Word: Death",
                                    ret => CT.HealthPercent <= (double)Instance.Settings.ShadowSWDHealth &&
                                           Instance.Settings.UseShadowPvPSWD),
                            CreateSpellWithAuraCheckAndCast("Devouring Plague",
                                    ret => CT.HealthPercent >= (double)Instance.Settings.ShadowDotHealth &&
                                           Instance.Settings.UseShadowPvPDevouringPlague),
                            CreateSpellWithAuraCheckAndCast("Shadow Word: Pain",
                                    ret => CT.HealthPercent >= (double)Instance.Settings.ShadowDotHealth &&
                                           Instance.Settings.UseShadowPvPSWP),
                            CreateSpellWithAuraCheckAndCast("Vampiric Touch",
                                    ret => CT.HealthPercent >= (double)Instance.Settings.ShadowDotHealth &&
                                           Instance.Settings.UseShadowPvPVampiricTouch),
                            CreateSpellWithAuraCheckAndCast("Psychic Horror",
                                    ret => Instance.Settings.UseShadowPsychicHorror),
                            CreateSpellCheckAndCast("Shadowfiend",
                                    ret => CT.HealthPercent > (double)Instance.Settings.ShadowShadowfiendHealth &&
                                           Me.ManaPercent <= (double)Instance.Settings.ShadowShadowfiendMana &&
                                           Instance.Settings.UseShadowPvPShadowfiend),
                            CreateSpellCheckAndCast("Mind Blast",
                                    ret => Instance.Settings.UseShadowPvPMindBlast),
                            CreateSpellCheckAndCast("Holy Fire",
                                    ret => Me.Shapeshift != ShapeshiftForm.Shadow &&
                                           Instance.Settings.UseShadowPvPHolyFire),
                            CreateSpellCheckAndCast("Smite",
                                    ret => Me.Shapeshift != ShapeshiftForm.Shadow &&
                                           Instance.Settings.UseShadowPvPSmite),
                            CreateSpellCheckAndCast("Mind Flay",
                                    ret => Instance.Settings.UseShadowPvPMindFlay),
                            CheckAndUseWand());
            }
        }

        #endregion

        #region Heal

        private static readonly Dictionary<SpellPriority, CastRequirements> PriestShadowHeal = new Dictionary<SpellPriority, CastRequirements> 
		{
			{new SpellPriority("Power Word: Shield", 100), (unit => Priest.Instance.Settings.UseShadowPWS && unit.HealthPercent <= (double)Priest.Instance.Settings.ShadowPWSHealth && CanCast("Power Word: Shield") && !unit.Dead && !unit.Auras.ContainsKey("Weakened Soul") && !unit.Auras.ContainsKey("Power Word: Shield"))},
			{new SpellPriority("Renew", 95), (unit => Priest.Instance.Settings.UseShadowRenew && unit.HealthPercent > (double)Priest.Instance.Settings.ShadowFlashHealHealth && unit.HealthPercent <= (double)Priest.Instance.Settings.ShadowRenewHealth && !unit.Dead && CanCast("Renew") && !unit.Auras.ContainsKey("Renew"))},
			{new SpellPriority("Flash Heal", 90), (unit => Priest.Instance.Settings.UseShadowFlashHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.ShadowFlashHealHealth && !unit.Dead && CanCast("Flash Heal"))},
			{new SpellPriority("Greater Heal", 85), (unit => Priest.Instance.Settings.UseShadowHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.ShadowHealHealth && !unit.Dead && CanCast("Greater Heal"))},
			{new SpellPriority("Heal", 80), (unit => Priest.Instance.Settings.UseShadowHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.ShadowHealHealth && CanCast("Heal") && !unit.Dead && !CanCast("Greater Heal"))},
			{new SpellPriority("Lesser Heal", 75), (unit => Priest.Instance.Settings.UseShadowHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.ShadowHealHealth && !unit.Dead && CanCast("Lesser Heal") && !CanCast("Heal") && !CanCast("Greater Heal"))}
		};

        #endregion

        #region Heal RaF

        private static readonly Dictionary<SpellPriority, CastRequirements> PriestShadowHealRaF = new Dictionary<SpellPriority, CastRequirements> 
		{
            {new SpellPriority("Power Word: Shield", 100), (unit => Priest.Instance.Settings.UseRaFPWS && unit.HealthPercent <= (double)Priest.Instance.Settings.RaFPWSHealth && CanCast("Power Word: Shield") && !unit.Dead && !unit.Auras.ContainsKey("Weakened Soul") && !unit.Auras.ContainsKey("Power Word: Shield"))},
			{new SpellPriority("Renew", 95), (unit => Priest.Instance.Settings.UseRaFRenew && unit.HealthPercent > (double)Priest.Instance.Settings.RaFFlashHealHealth && unit.HealthPercent <= (double)Priest.Instance.Settings.RaFRenewHealth && !unit.Dead && CanCast("Renew") && !unit.Auras.ContainsKey("Renew"))},
            {new SpellPriority("Flash Heal", 90), (unit => Priest.Instance.Settings.UseRaFFlashHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.RaFFlashHealHealth && !unit.Dead && CanCast("Flash Heal"))},
            {new SpellPriority("Greater Heal", 85), (unit => Priest.Instance.Settings.UseRaFHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.RaFHealHealth && !unit.Dead && CanCast("Greater Heal"))},
            {new SpellPriority("Heal", 80), (unit => Priest.Instance.Settings.UseRaFHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.RaFHealHealth && !unit.Dead && CanCast("Heal") && !CanCast("Greater Heal"))},
            {new SpellPriority("Lesser Heal", 75), (unit => Priest.Instance.Settings.UseRaFHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.RaFHealHealth && !unit.Dead && CanCast("Lesser Heal") && !CanCast("Heal") && !CanCast("Greater Heal"))}
        };

        #endregion

        #region Heal PvP

        private static readonly Dictionary<SpellPriority, CastRequirements> PriestShadowHealPvP = new Dictionary<SpellPriority, CastRequirements> 
		{
            {new SpellPriority("Power Word: Shield", 100), (unit => Priest.Instance.Settings.UseShadowPvPPWS && unit.HealthPercent <= (double)Priest.Instance.Settings.ShadowPvPPWSHealth && CanCast("Power Word: Shield") && !unit.Dead && !unit.Auras.ContainsKey("Weakened Soul") && !unit.Auras.ContainsKey("Power Word: Shield"))},
			{new SpellPriority("Renew", 95), (unit => Priest.Instance.Settings.UseShadowPVPRenew && unit.HealthPercent > (double)Priest.Instance.Settings.ShadowPvPFlashHealHealth && unit.HealthPercent <= (double)Priest.Instance.Settings.ShadowPvPRenewHealth && !unit.Dead && CanCast("Renew") && !unit.Auras.ContainsKey("Renew"))},
            {new SpellPriority("Flash Heal", 90), (unit => Priest.Instance.Settings.UseShadowPVPFlashHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.ShadowPvPFlashHealHealth && !unit.Dead && CanCast("Flash Heal"))},
            {new SpellPriority("Greater Heal", 85), (unit => Priest.Instance.Settings.UseShadowPVPHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.ShadowPvPHealHealth && CanCast("Greater Heal") && !unit.Dead)},
            {new SpellPriority("Heal", 80), (unit => Priest.Instance.Settings.UseShadowPVPHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.ShadowPvPHealHealth && CanCast("Heal") && !CanCast("Greater Heal") && !unit.Dead)},
            {new SpellPriority("Lesser Heal", 75), (unit => Priest.Instance.Settings.UseShadowPVPHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.ShadowPvPHealHealth && CanCast("Lesser Heal") && !CanCast("Heal") && !unit.Dead && !CanCast("Greater Heal"))}
        };

        #endregion
    }
}