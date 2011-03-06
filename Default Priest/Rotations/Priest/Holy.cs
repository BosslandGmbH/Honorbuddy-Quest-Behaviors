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

        private Composite PriestHolyDpsRotation
        {
            get
            {
                return new PrioritySelector(
                            CreateSpellCheckAndCast("Shadowfiend",
                                    ret => CT.HealthPercent > (double)Instance.Settings.ShadowShadowfiendHealth &&
                                           Me.ManaPercent <= (double)Instance.Settings.ShadowShadowfiendMana &&
                                           Instance.Settings.UseRaFShadowfiend),
                            CreateSpellCheckAndCast("Holy Fire",
                                    ret => !Me.IsInInstance &&
                                           CT.HealthPercent > 30),
                            CreateSpellCheckAndCast("Smite",
                                    ret => Instance.Settings.UseDiscSmite &&
                                           Me.ManaPercent >= Instance.Settings.DiscSmiteMana),
                            CheckAndUseWand());
            }
        }

        #endregion

        #region Dps PvP

        private Composite PriestHolyPvPDpsRotation
        {
            get
            {
                return new PrioritySelector(
                            CreateSpellCheckAndCast("Shadowfiend",
                                    ret => CT.HealthPercent > (double)Instance.Settings.ShadowShadowfiendHealth &&
                                           Me.ManaPercent <= (double)Instance.Settings.ShadowShadowfiendMana &&
                                           Instance.Settings.UseRaFShadowfiend),
                            CreateSpellCheckAndCast("Holy Fire",
                                    ret => CT.HealthPercent > 15),
                            CreateSpellCheckAndCast("Smite",
                                    ret => Instance.Settings.UseDiscPvPSmite &&
                                           Me.ManaPercent >= Instance.Settings.DiscPvPSmiteMana),
                            CheckAndUseWand());
            }
        }

        #endregion

        #region Heal

        private static readonly Dictionary<SpellPriority, CastRequirements> PriestHolyHeal = new Dictionary<SpellPriority, CastRequirements> 
        {
	        {new SpellPriority("Power Word: Shield", 90), (unit => Me.Combat && Priest.Instance.Settings.UseHolyPWS && unit.HealthPercent <= (double)Priest.Instance.Settings.HolyPWSHealth && CanCast("Power Word: Shield") && !unit.Dead && !unit.Auras.ContainsKey("Weakened Soul") && !unit.Auras.ContainsKey("Power Word: Shield"))},
	        {new SpellPriority("Prayer of Healing", 85), (unit => Priest.Instance.Settings.UseHolyPrayerOfHealing && unit.HealthPercent <= (double)Priest.Instance.Settings.HolyPrayerOfHealingHealth && !unit.Dead && CanCast("Prayer of Healing") && ShouldCastPrayerOfHealing)},
	        {new SpellPriority("Renew", 80), (unit => Priest.Instance.Settings.UseHolyRenew && unit.HealthPercent <= (double)Priest.Instance.Settings.HolyRenewHealth && unit.HealthPercent > (double)Priest.Instance.Settings.HolyFlashHealHealth && !unit.Dead && CanCast("Renew") && !unit.Auras.ContainsKey("Renew"))},
	        {new SpellPriority("Binding Heal", 75), (unit => Priest.Instance.Settings.UseHolyFlashHeal && unit != Me && Me.HealthPercent <= (double)Priest.Instance.Settings.HolyFlashHealHealth && unit.HealthPercent <= (double)Priest.Instance.Settings.HolyFlashHealHealth && !unit.Dead && CanCast("Binding Heal"))},
	        {new SpellPriority("Flash Heal", 70), (unit => Priest.Instance.Settings.UseHolyFlashHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.HolyFlashHealHealth && !unit.Dead && CanCast("Flash Heal"))},
	        {new SpellPriority("Greater Heal", 65), (unit => Priest.Instance.Settings.UseHolyHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.HolyHealHealth && !unit.Dead && CanCast("Greater Heal"))},
	        {new SpellPriority("Heal", 60), (unit => Priest.Instance.Settings.UseHolyHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.HolyHealHealth && CanCast("Heal") && !unit.Dead && !CanCast("Greater Heal"))},
	        {new SpellPriority("Lesser Heal", 55), (unit => Priest.Instance.Settings.UseHolyHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.HolyHealHealth && !unit.Dead && CanCast("Lesser Heal") && !CanCast("Heal") && !CanCast("Greater Heal"))},
	        {new SpellPriority("Prayer of Mending", 50), (unit => Me.Combat && unit.HealthPercent <= 90 && Priest.Instance.Settings.UseHolyPrayerOfMending && !unit.Dead && CanCast("Prayer of Mending") && NoOneHasPrayerOfMending)},
        };

        #endregion

        #region Heal PvP

        private static readonly Dictionary<SpellPriority, CastRequirements> PriestHolyHealPvP = new Dictionary<SpellPriority, CastRequirements> 
		{
			{new SpellPriority("Power Word: Shield", 90), (unit => Me.Combat && Priest.Instance.Settings.UseHolyPvPPWS && unit.HealthPercent <= (double)Priest.Instance.Settings.HolyPvPPWSHealth && CanCast("Power Word: Shield") && !unit.Dead && !unit.Auras.ContainsKey("Weakened Soul") && !unit.Auras.ContainsKey("Power Word: Shield"))},
			{new SpellPriority("Renew", 85), (unit => Priest.Instance.Settings.UseHolyPvPRenew && unit.HealthPercent > (double)Priest.Instance.Settings.HolyPvPFlashHealHealth && unit.HealthPercent <= (double)Priest.Instance.Settings.HolyPvPRenewHealth && !unit.Dead && CanCast("Renew") && !unit.Auras.ContainsKey("Renew"))},
			{new SpellPriority("Binding Heal", 80), (unit => Priest.Instance.Settings.UseHolyPvPFlashHeal && unit != Me && Me.HealthPercent <= (double)Priest.Instance.Settings.HolyPvPFlashHealHealth && unit.HealthPercent <= (double)Priest.Instance.Settings.HolyPvPFlashHealHealth && !unit.Dead && CanCast("Binding Heal"))},
			{new SpellPriority("Greater Heal", 75), (unit => Priest.Instance.Settings.UseHolyPvPHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.HolyPvPHealHealth && !unit.Dead && CanCast("Greater Heal"))},
			{new SpellPriority("Heal", 70), (unit => Priest.Instance.Settings.UseHolyPvPHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.HolyPvPHealHealth && CanCast("Heal") && !unit.Dead && !CanCast("Greater Heal"))},
			{new SpellPriority("Lesser Heal", 65), (unit => Priest.Instance.Settings.UseHolyPvPHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.HolyPvPHealHealth && !unit.Dead && CanCast("Lesser Heal") && !CanCast("Heal") && !CanCast("Greater Heal"))},
			{new SpellPriority("Flash Heal", 60), (unit => Priest.Instance.Settings.UseHolyPvPFlashHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.HolyPvPFlashHealHealth && !unit.Dead && CanCast("Flash Heal"))},
			{new SpellPriority("Prayer of Mending", 55), (unit => Me.Combat && unit.HealthPercent <= 90 && Priest.Instance.Settings.UseHolyPvPPrayerOfMending && !unit.Dead && CanCast("Prayer of Mending") && NoOneHasPrayerOfMending)}
		};

        #endregion
    }
}