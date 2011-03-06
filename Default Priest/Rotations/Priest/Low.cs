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

        private Composite PriestLowDpsRotation
        {
            get
            {
                return new PrioritySelector(
                            CreateSpellCheckAndCast("Holy Fire", ret => !Me.Combat),
                            CreateSpellCheckAndCast("Smite", ret => !Me.Combat),
                            CreateSpellWithAuraCheckAndCast("Shadow Word: Pain"),
                            CreateSpellCheckAndCast("Holy Fire"),
                            CreateSpellCheckAndCast("Smite"),
                            CheckAndUseWand());
            }
        }

        #endregion

        #region Dps PvP

        private Composite PriestLowPvPDpsRotation
        {
            get
            {
                return new PrioritySelector(
                            CreateSpellWithAuraCheckAndCast("Shadow Word: Pain"),
                            CreateSpellCheckAndCast("Holy Fire"),
                            CreateSpellCheckAndCast("Smite"),
                            CheckAndUseWand());
            }
        }

        #endregion

        #region Heal

        private static readonly Dictionary<SpellPriority, CastRequirements> PriestLowHeal = new Dictionary<SpellPriority, CastRequirements> 
        {
            {new SpellPriority("Power Word: Shield", 100), (unit => unit.HealthPercent <= 100 && !unit.Dead && CanCast("Power Word: Shield", unit) && !unit.Auras.ContainsKey("Weakened Soul") && !unit.Auras.ContainsKey("Power Word: Shield"))},
            {new SpellPriority("Lesser Heal", 90), (unit => unit.HealthPercent <= 50 && !unit.Dead && CanCast("Lesser Heal", unit))},
			{new SpellPriority("Lesser Heal", 80), (unit => unit.HealthPercent <= 50 && !unit.Dead && CanCast("Lesser Heal", unit))}
        };	

        #endregion

        #region Heal RaF

        private static readonly Dictionary<SpellPriority, CastRequirements> PriestLowHealRaF = new Dictionary<SpellPriority, CastRequirements> 
        {
			{new SpellPriority("Lesser Heal", 100), (unit => unit.HealthPercent <= 60 && !unit.Dead && CanCast("Lesser Heal", unit))},
            {new SpellPriority("Power Word: Shield", 90), (unit => unit.HealthPercent <= 30 && !unit.Dead && CanCast("Power Word: Shield", unit) && !unit.Auras.ContainsKey("Weakened Soul"))},
			{new SpellPriority("Lesser Heal", 80), (unit => unit.HealthPercent <= 60 && !unit.Dead && CanCast("Lesser Heal", unit))}
            
        };

        #endregion
    }
}