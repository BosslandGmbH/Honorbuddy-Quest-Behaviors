using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Hera.SpellsMan;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Hera.Helpers
{
    public static class Self
    {
        private static LocalPlayer Me { get { return ObjectManager.Me; } }

        /// <summary>
        /// Simple buff check
        /// </summary>
        /// <param name="buffName">Name of the buff to check</param>
        /// <returns>TRUE if the buff is present on you</returns>
        public static bool IsBuffOnMe(string buffName)
        {
            return (Me.Auras.ContainsKey(buffName));
            //return (Me.HasAura(buffName));
        }

        public static bool IsBuffOnMeLUA(string buffName)
        {
            Lua.DoString(string.Format(@"buffName,_,_,stackCount,_,_,_,_,_=UnitBuff(""player"",""{0}"")", buffName));
            string buff = Lua.GetLocalizedText("buffName", Me.BaseAddress);
            
            return buff == buffName;
        }


        /// <summary>
        /// Checks Spell.CanCast and Me.HasAura
        /// </summary>
        /// <param name="buffName">Name of the buff you want to cast</param>
        /// <returns></returns>
        public static bool CanBuffMe(string buffName)
        {
            if (Me.HasAura(buffName)) return false;
            if (!Spell.CanCast(buffName)) return false;
            

            return true;
        }

        /// <summary>
        /// TRUE is the given buff is present on you
        /// </summary>
        /// <param name="buffName">Buff name you want to check for</param>
        /// <returns>TRUE if the buff is present</returns>
        public static bool BuffMe(string buffName)
        {
            Spell.Cast(buffName, Me);
            Utils.LagSleep();

            bool result = IsBuffOnMe(buffName);
            return result;
        }

        /// <summary>
        /// The number of stacks on a given buff
        /// </summary>
        /// <param name="buffName">Buff name you want to check for</param>
        /// <returns>The number (int) of stacks of a buff</returns>
        public static int StackCount(string buffName)
        {
            if (!IsBuffOnMe(buffName)) return 0;
            uint stackCount = Me.Auras[buffName].StackCount;

            return (int)stackCount;
        }

        /// <summary>
        /// Scan the area (30 yards) for players of the same faction to buff
        /// </summary>
        /// <param name="spellName">Name of the buff you want to cast</param>
        /// <param name="excludeIfBuffPresent">Do not cast the buff if this spell is present on the target</param>
        /// <param name="minimumMana">Do not cast the buff if your mana is below this percent</param>
        /// <param name="buffInCombat">TRUE if you want to cast buffs on players while you are in combat</param>
        public static void BuffRandomPlayers(string spellName, string excludeIfBuffPresent, double minimumMana, bool buffInCombat)
        {
            if (Me.IsResting) return;
            if (IsBuffOnMe("Drink")) return;
            if (IsBuffOnMe("Food")) return;
            if (!Spell.CanCast(spellName)) return;
            if (Me.IsGhost) return;
            if (Me.Dead) return;
            if (Me.Mounted) return;
            if (!buffInCombat && Me.Combat) return;
            if (Me.ManaPercent < minimumMana) return;

            List<WoWPlayer> plist =
                (from o in ObjectManager.ObjectList
                 where o is WoWPlayer
                 let p = o.ToPlayer()
                 where p.Distance < 30
                       && p.Guid != Me.Guid
                       && (p.IsHorde && Me.IsHorde || p.IsAlliance && Me.IsAlliance)
                     //&& p.Level <= Me.Level + 25
                       && !p.Dead
                       && p.InLineOfSight
                       && !p.HasAura(spellName)
                       && !p.HasAura(excludeIfBuffPresent)
                 select p).ToList();


            foreach (WoWPlayer p in plist)
            {
                if (!Spell.CanCast(spellName))
                    return;
                if (!buffInCombat && p.Combat)
                    return;
                if (!Me.PvpFlagged && p.PvpFlagged)
                    return;
                Utils.Log(string.Format("Being friendly and casting {0} on a player", spellName), Utils.Colour("Green"));
                Spell.Cast(spellName, p);
            }

        }

        /// <summary>
        /// Scan the area (40 yards) for players of the same faction to heal
        /// </summary>
        /// <param name="spellName">Name of the sepll you want to cast</param>
        /// <param name="excludeIfBuffPresent">Do not cast the buff if this spell is present on the target</param>
        /// <param name="buffInCombat">Do not cast the buff if your mana is below this percent</param>
        /// <param name="minimumHealth">The minimum health a player must be before healing them</param>
        /// <param name="minimumMana">TRUE if you want to cast buffs on players while you are in combat</param>
        public static void HealRandomPlayers(string spellName, string excludeIfBuffPresent, bool buffInCombat, double minimumHealth, double minimumMana)
        {
            if (Me.IsResting) return;
            if (IsBuffOnMe("Drink")) return;
            if (IsBuffOnMe("Food")) return;
            if (!Spell.CanCast(spellName)) return;
            if (Me.IsGhost) return;
            if (Me.Dead) return;
            if (Me.Mounted) return;
            if (Me.ManaPercent < minimumMana) return;

            List<WoWPlayer> plist =
                (from o in ObjectManager.ObjectList
                 where o is WoWPlayer
                 let p = o.ToPlayer()
                 where p.Distance < 40
                       && p.Guid != Me.Guid
                       && (p.IsHorde && Me.IsHorde || p.IsAlliance && Me.IsAlliance)
                       && !p.Dead
                       && p.InLineOfSight
                       && p.HealthPercent > 10
                       && p.HealthPercent < minimumHealth
                       && !p.HasAura(spellName)
                       && !p.HasAura(excludeIfBuffPresent)
                 select p).ToList();



            foreach (WoWPlayer p in plist)
            {
                if (!Spell.CanCast(spellName))
                    return;
                if (!buffInCombat && p.Combat)
                    return;
                if (p.HasAura(excludeIfBuffPresent))
                    return;
                if (p.HasAura(spellName))
                    return;
                if (!Me.PvpFlagged && p.PvpFlagged)
                    return;
                if (Me.IsMoving)

                    WoWMovement.MoveStop();
                Utils.Log("Being friendly and healing a player", Utils.Colour("Green"));
                Spell.Cast(spellName, p);
                Thread.Sleep(500);
                while (Me.IsCasting)
                    Thread.Sleep(250);
            }

        }

        public static bool IsHealthAbove(int healthLevel) { return Me.HealthPercent > healthLevel; }

        public static bool IsManaAbove(int manaPercentLevel) { return Me.ManaPercent > manaPercentLevel; }

        public static bool IsEnergyAbove(int energyLevel)
        {
            // If you're not a Rogue or a Druid then return false
            if (Me.Class != WoWClass.Warrior && Me.Class != WoWClass.Druid) return false;
            if (Me.Class == WoWClass.Druid && Me.Shapeshift != ShapeshiftForm.Cat) return false;
            return Me.CurrentEnergy > energyLevel;
        }

        public static bool IsRageAbove(int rageLevel)
        {
            // If you're not a Rogue or a Druid then return false
            if (Me.Class != WoWClass.Warrior && Me.Class != WoWClass.Druid) return false;
            if (Me.Class == WoWClass.Druid && Me.Shapeshift != ShapeshiftForm.Bear) return false;
            return Me.CurrentRage > rageLevel;
        }

        public static bool IsFocusAbove(double focusLevel)
        {
            return Me.Class == WoWClass.Hunter && Me.CurrentFocus > focusLevel;
        }
    }
}
