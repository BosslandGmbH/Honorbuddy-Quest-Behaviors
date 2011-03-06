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

        private Composite PriestDiscDpsRotation
        {
            get
            {
                return
                    new PrioritySelector(
                        CreateSpellCheckAndCast("Shadowfiend",
                                    ret => CT.HealthPercent > (double)Instance.Settings.ShadowShadowfiendHealth &&
                                            Me.ManaPercent <= (double)Instance.Settings.ShadowShadowfiendMana &&
                                            Instance.Settings.UseRaFShadowfiend),
                        CreateSpellCheckAndCast("Shadow Word: Death",
                                    ret => CT.HealthPercent <= (double)Instance.Settings.ShadowSWDHealth &&
                                            Instance.Settings.UseShadowSWD),
                        new DecoratorEx(ret => !Me.IsInInstance,
                            new PrioritySelector(
                                CreateSpellCheckAndCast("Archangel",
                                    ret => Me.ManaPercent <= 80 &&
                                            Me.HasAura("Evangelism") &&
                                            Me.Auras["Evangelism"].StackCount == 5),
                                CreateSpellCheckAndCast("Holy Fire",
                                    ret => CT.HealthPercent > 30),
                                CreateSpellCheckAndCast("Penance"),
                                CreateSpellCheckAndCast("Smite"),
                                CheckAndUseWand())),

                        new DecoratorEx(ret => Me.IsInInstance,
                            new PrioritySelector(
                                CreateSpellCheckAndCast("Smite",
                                        ret => Instance.Settings.UseDiscSmite &&
                                               !Me.PartyMembers.Exists(p => p.IsAlive && p.HealthPercent < 70) &&
                                               Me.ManaPercent >= Instance.Settings.DiscSmiteMana &&
                                               HasTalent(1,4)),
                                CheckAndUseWand())));
            }
        }

        #endregion

        #region Dps PvP

        private Composite PriestDiscPvPDpsRotation
        {
            get
            {
                return new PrioritySelector(
                            CreateSpellCheckAndCast("Shadow Word: Death",
                                    ret => CT.HealthPercent <= (double)Instance.Settings.ShadowSWDHealth &&
                                           Instance.Settings.UseShadowPvPSWD),
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

        private static readonly Dictionary<SpellPriority, CastRequirements> PriestDiscHeal = new Dictionary<SpellPriority, CastRequirements> 
		{
			{new SpellPriority("Desperate Prayer", 100), (unit => Me.Combat && unit == Me && Priest.Instance.Settings.UseDiscPenance && unit.HealthPercent <= (double)Priest.Instance.Settings.DiscPenanceHealth && !unit.Dead && CanCast("Desperate Prayer"))},
			{new SpellPriority("Penance", 95), (unit => Priest.Instance.Settings.UseDiscPenance && unit.HealthPercent <= (double)Priest.Instance.Settings.DiscPenanceHealth && !unit.Dead && CanCast("Penance"))},
			{new SpellPriority("Power Word: Shield", 90), (unit => Me.Combat && Priest.Instance.Settings.UseDiscPWS && unit.HealthPercent <= (double)Priest.Instance.Settings.DiscPWSHealth && CanCast("Power Word: Shield") && !unit.Dead && !unit.Auras.ContainsKey("Weakened Soul") && !unit.Auras.ContainsKey("Power Word: Shield"))},
			{new SpellPriority("Pain Suppression", 85), (unit => Me.Combat && Priest.Instance.Settings.UseDiscPainSuppression && unit.HealthPercent <= (double)Priest.Instance.Settings.DiscPainSuppressionHealth && CanCast("Pain Suppression") && !unit.Dead)},
			{new SpellPriority("Prayer of Healing", 80), (unit => Priest.Instance.Settings.UseDiscPrayerOfHealing && unit.HealthPercent <= (double)Priest.Instance.Settings.DiscPrayerOfHealingHealth && !unit.Dead && CanCast("Prayer of Healing") && ShouldCastPrayerOfHealing)},
			{new SpellPriority("Renew", 75), (unit => Priest.Instance.Settings.UseDiscRenew && unit.HealthPercent <= (double)Priest.Instance.Settings.DiscRenewHealth && unit.HealthPercent > (double)Priest.Instance.Settings.DiscFlashHealHealth && !unit.Dead && CanCast("Renew") && !unit.Auras.ContainsKey("Renew"))},
			{new SpellPriority("Binding Heal", 70), (unit => Priest.Instance.Settings.UseDiscFlashHeal && unit != Me && Me.HealthPercent <= (double)Priest.Instance.Settings.DiscFlashHealHealth && unit.HealthPercent <= (double)Priest.Instance.Settings.DiscFlashHealHealth && !unit.Dead && CanCast("Binding Heal"))},
			{new SpellPriority("Greater Heal", 65), (unit => Priest.Instance.Settings.UseDiscGreaterHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.DiscGreaterHealHealth && !unit.Dead && CanCast("Greater Heal"))},
			{new SpellPriority("Heal", 60), (unit => Priest.Instance.Settings.UseDiscHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.DiscHealHealth && CanCast("Heal") && !unit.Dead)},
			{new SpellPriority("Lesser Heal", 55), (unit => Priest.Instance.Settings.UseDiscHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.DiscHealHealth && !unit.Dead && CanCast("Lesser Heal") && !CanCast("Heal"))},
			{new SpellPriority("Flash Heal", 50), (unit => Priest.Instance.Settings.UseDiscFlashHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.DiscFlashHealHealth && !unit.Dead && CanCast("Flash Heal"))},
			{new SpellPriority("Prayer of Mending", 45), (unit => Me.Combat && unit.HealthPercent <= 90 && Priest.Instance.Settings.UseDiscPrayerOfMending && !unit.Dead && CanCast("Prayer of Mending") && NoOneHasPrayerOfMending)}
		};

        #endregion

        #region Heal PvP

        private static readonly Dictionary<SpellPriority, CastRequirements> PriestDiscHealPvP = new Dictionary<SpellPriority, CastRequirements> 
		{
			{new SpellPriority("Pain Suppression", 100), (unit => Me.Combat && Priest.Instance.Settings.UseDiscPvPPainSuppression && unit.HealthPercent <= (double)Priest.Instance.Settings.DiscPvPPainSuppressionHealth && CanCast("Pain Suppression") && !unit.Dead)},
			{new SpellPriority("Desperate Prayer", 95), (unit => Me.Combat && unit == Me && Priest.Instance.Settings.UseDiscPvPPenance && unit.HealthPercent <= (double)Priest.Instance.Settings.DiscPvPPenanceHealth && !unit.Dead && CanCast("Desperate Prayer"))},
			{new SpellPriority("Penance", 90), (unit => Priest.Instance.Settings.UseDiscPvPPenance && unit.HealthPercent <= (double)Priest.Instance.Settings.DiscPvPPenanceHealth && !unit.Dead && CanCast("Penance"))},
			{new SpellPriority("Power Word: Shield", 85), (unit => Me.Combat && Priest.Instance.Settings.UseDiscPvPPWS && unit.HealthPercent <= (double)Priest.Instance.Settings.DiscPvPPWSHealth && CanCast("Power Word: Shield") && !unit.Dead && !unit.Auras.ContainsKey("Weakened Soul") && !unit.Auras.ContainsKey("Power Word: Shield"))},
			{new SpellPriority("Renew", 80), (unit => Priest.Instance.Settings.UseDiscPvPRenew && unit.HealthPercent > (double)Priest.Instance.Settings.DiscPvPFlashHealHealth && unit.HealthPercent <= (double)Priest.Instance.Settings.DiscPvPRenewHealth && !unit.Dead && CanCast("Renew") && !unit.Auras.ContainsKey("Renew"))},
			{new SpellPriority("Binding Heal", 75), (unit => Priest.Instance.Settings.UseDiscPvPFlashHeal && unit != Me && Me.HealthPercent <= (double)Priest.Instance.Settings.DiscPvPFlashHealHealth && unit.HealthPercent <= (double)Priest.Instance.Settings.DiscPvPFlashHealHealth && !unit.Dead && CanCast("Binding Heal"))},			
			{new SpellPriority("Greater Heal", 70), (unit => Priest.Instance.Settings.UseDiscPvPGreaterHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.DiscPvPGreaterHealHealth && !unit.Dead && CanCast("Greater Heal"))},
			{new SpellPriority("Heal", 65), (unit => Priest.Instance.Settings.UseDiscPvPHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.DiscPvPHealHealth && CanCast("Heal") && !unit.Dead)},
			{new SpellPriority("Lesser Heal", 60), (unit => Priest.Instance.Settings.UseDiscPvPHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.DiscPvPHealHealth && !unit.Dead && CanCast("Lesser Heal") && !CanCast("Heal"))},
			{new SpellPriority("Flash Heal", 55), (unit => Priest.Instance.Settings.UseDiscPvPFlashHeal && unit.HealthPercent <= (double)Priest.Instance.Settings.DiscPvPFlashHealHealth && !unit.Dead && CanCast("Flash Heal"))},
			{new SpellPriority("Prayer of Mending", 50), (unit => Me.Combat && unit.HealthPercent <= 90 && Priest.Instance.Settings.UseDiscPvPPrayerOfMending && !unit.Dead && CanCast("Prayer of Mending") && NoOneHasPrayerOfMending)}
			
		};

        #endregion

        #region Methods

        public static bool ShouldCastPrayerOfHealing
        {
            get
            {
                int count = 0;

                if (Me.PartyMembers.Count > 0)
                {
                    foreach (WoWPlayer p in Me.PartyMembers)
                    {
                        if (p.HealthPercent < (double)Priest.Instance.Settings.DiscPrayerOfHealingHealth &&
                            p.HealthPercent > 1 &&
                            p.Distance < 30)
                            count++;
                    }
                }

                if (count >= Priest.Instance.Settings.DiscPrayerOfHealingCount)
                    return true;

                return false;
            }
        }

        public static bool NoOneHasPrayerOfMending
        {
            get
            {
                if (Me.HasAura("Prayer of Mending"))
                    return false;

                List<WoWPlayer> playerList = Me.IsInRaid ? Me.RaidMembers : Me.PartyMembers;

                return !playerList.Exists(p => p.HasAura("Prayer of Mending") && p.Auras["Prayer of Mending"].CreatorGuid == Me.Guid);
            }
        }

        #endregion
    }
}