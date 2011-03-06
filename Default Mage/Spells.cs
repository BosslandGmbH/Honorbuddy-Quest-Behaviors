using System;
using System.Collections.Generic;
using System.Threading;
using Styx;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace DefaultMage
{
    public partial class DefaultMage
    {
        internal class Spells
        {
           
            private static Spells _instance;
            public static Spells Instance
            {
                get { return _instance ?? (_instance = new Spells()); }
            }

            public WoWSpell this[string spellName]
            {
                get { return LegacySpellManager.KnownSpells.ContainsKey(spellName) ? LegacySpellManager.KnownSpells[spellName] : null; }
            }

            public void StopCasting()
            {
                Lua.DoString("SpellStopCasting()");
            }

            public bool CanCast(string spellname)
            {
                WoWSpell spell = this[spellname];

                if (spell == null || spell.Cooldown || LegacySpellManager.GlobalCooldown || StyxWoW.Me.IsCasting)
                    return false;

                if (Lua.GetReturnVal<int>("return IsUsableSpell(\"" + spell.Name + "\")", 0) != 1)
                    return false;

                return true;
            }

            public void Cast(string spell)
            {
                Log("Casting: " + this[spell].Name);
                this[spell].Cast();

                var sleepTime = Lua.GetReturnVal<int>("return GetNetStats()", 2);
                Thread.Sleep(sleepTime * 2);
            }


       

            /// <summary>
            /// Buffs ourselves with the specified spell.
            /// </summary>
            /// <param name="spell"></param>
            public void Buff(string spell)
            {
                Buff(spell, StyxWoW.Me, true);
            }

            /// <summary>
            /// Buffs the specified unit with a spell. Optionally targeting the current target.
            /// </summary>
            /// <param name="spell">The name of the spell to buff.</param>
            /// <param name="target">The unit to buff.</param>
            /// <param name="targetLast">Whether or not we should re-target our old target after buffing the unit.</param>
            public void Buff(string spell, WoWUnit target, bool targetLast)
            {
                var autoSelfCast = Lua.GetReturnVal<bool>("return GetCVar('autoSelfCast')", 0);

                // don't target self if we got AutoSelfCast on, or if target isn't me
                if (target != Me || target == Me && !autoSelfCast)
                    target.Target();

                // Quick sleep to allow WoW to update us
                Thread.Sleep(100);
                Log("Buffing " + target.Name + " with " + spell);
                this[spell].Cast();

                if (targetLast && (target != Me && !autoSelfCast))
                {
                    // Again... wait for WoW to update/cast
                    Thread.Sleep(100);
                    StyxWoW.Me.TargetLastTarget();
                    // Sheesh... so much waiting...
                    Thread.Sleep(100);
                }
            }

            /// <summary>
            /// Determines whether or not we have a spell.
            /// </summary>
            /// <param name="spell"></param>
            /// <returns></returns>
            public bool HasSpell(string spell)
            {
                return this[spell] != null;
            }

            /// <summary>
            /// Determines if we can buff ourselves with the specified spell.
            /// </summary>
            /// <param name="spell"></param>
            /// <returns></returns>
            public bool CanBuff(string spell)
            {
                return CanBuff(spell, StyxWoW.Me);
            }

            /// <summary>
            /// Determines if we can buff the specified unit with the chosen spell.
            /// </summary>
            /// <param name="spell">The spell to check for</param>
            /// <param name="unit">The unit to check</param>
            /// <returns></returns>
            public bool CanBuff(string spell, WoWUnit unit)
            {
                return CanCast(spell) && !unit.Auras.ContainsKey(spell);
            }

            private readonly Random _rand = new Random();
            /// <summary>
            /// Casts a random spell at our current target (or ourselves)
            /// </summary>
            /// <param name="spells"></param>
            public void CastRandom(List<string> spells)
            {
                string curSpell = spells[_rand.Next(0, spells.Count)];
                while (spells.Count != 0 && !CanCast(curSpell))
                {
                    spells.Remove(curSpell);
                    curSpell = spells.Count != 0 ? spells[_rand.Next(0, spells.Count)] : null;
                }
                if (curSpell != null)
                {
                    this[curSpell].Cast();
                }
            }
        }

      
    }
}
