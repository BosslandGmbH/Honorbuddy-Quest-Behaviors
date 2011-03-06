using Hera.Helpers;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using System;

namespace Hera.SpellsMan
{

    public static class Spell
    {

        private static LocalPlayer Me { get { return ObjectManager.Me; } }

        /// <summary>
        /// TRUE if a spell is known by HB
        /// </summary>
        /// <param name="spellName">Name of the spell to check</param>
        /// <returns>TRUE if the spell is know</returns>
        public static bool IsKnown(string spellName)
        {
            Debug.Log("IsKnown " + spellName, 1);
            bool result = SpellManager.HasSpell(spellName);
            Debug.Log("... result " + result, 1);

            return result;
        }


        /// <summary>
        /// Stop casting, goes without saying really
        /// </summary>
        public static void StopCasting()
        {
            Debug.Log("StopCasting", 1);
            SpellManager.StopCasting();
        }

        /// <summary>
        /// TRUE if on global cooldown
        /// </summary>
        public static bool IsGCD
        {
            get
            {
                bool result = LegacySpellManager.GlobalCooldown;
                return result;
            }
        }

        /// <summary>
        /// TRUE if HB can cast the spell
        /// </summary>
        /// <param name="spellName"></param>
        /// <returns>TRUE if they spell can be cast</returns>
        public static bool CanCast(string spellName)
        {
            Debug.Log("CanCast " + spellName, 1);
            return SpellManager.CanCast(spellName);
        }

        /// <summary>
        /// TRUE if HB can cast the spell. This performs a LUA check on the spell
        /// </summary>
        /// <param name="spellName"></param>
        /// <returns></returns>
        public static bool CanCastLUA(string spellName)
        {
            Debug.Log(String.Format("CanCast {0}",spellName), 1);
            var isUsable = Lua.GetReturnValues("return IsUsableSpell('" + spellName + "')", "stuffnthings.lua");

            if (isUsable != null) { if (isUsable[0] == "1") { return true; } }

            return false;
        }

        /// <summary>
        /// TRUE if HB can cast the spell. But first check our mana levels. 
        /// This way we can leave some mana for healing spells
        /// </summary>
        /// <param name="spellName">Spell name to check</param>
        /// <param name="minimumMana">If mana is below minimumMana percent then return FALSE</param>
        /// <returns>TRUE if the spell can be cast</returns>
        public static bool CanCast(string spellName, double minimumMana)
        {
            Debug.Log(String.Format("CanCast {0} minimumMana {1}", spellName, minimumMana), 1);
            bool result = Me.ManaPercent > minimumMana;

            Debug.Log("... result " + result, 1);
            return result && SpellManager.CanCast(spellName);
        }

        /// <summary>
        /// Cast a specific spell
        /// </summary>
        /// <param name="spellName"></param>
        /// <returns>TRUE if the spell was cast successfully</returns>
        public static bool Cast(string spellName)
        {
            Debug.Log("Cast " + spellName,1);
            bool result = SpellManager.Cast(spellName);

            Debug.Log("... result " + result, 1);
            if (result) Utils.Log("-" + spellName, Utils.Colour("Blue"));
            return result;
        }

        /// <summary>
        /// Cast a spell on a specific target. This does not deselect your current target if it differs from targetUnit
        /// </summary>
        /// <param name="spellName">Name of the spell to cast</param>
        /// <param name="targetUnit">WoWUnit to cast the spell on</param>
        /// <returns>TRUE if the spell was cast successfully</returns>
        public static bool Cast(string spellName, WoWUnit targetUnit)
        {
            Debug.Log(String.Format("Cast {0} targetUnit", spellName), 1);
            bool result = SpellManager.Cast(spellName, targetUnit);
            string targetName = "target";

            if (targetUnit.Guid == Me.Guid) targetName = "ME";
            if (RaFHelper.Leader != null && targetUnit.Guid == RaFHelper.Leader.Guid) targetName = "TANK";

            Debug.Log("... result " + result, 1);
            if (result) Utils.Log(String.Format("-{0} on {1}", spellName, targetName), Utils.Colour("Blue"));
            return result;
        }

        /// <summary>
        /// Cast a given spell using click-to-cast spells
        /// </summary>
        /// <param name="spellName">Spell name to cast</param>
        /// <param name="clickCastLocation">WoWPoint to cast the spell</param>
        /// <returns></returns>
        public static bool Cast(string spellName, WoWPoint clickCastLocation)
        {
            Debug.Log(String.Format("Click Cast {0}", spellName), 1);
            
            bool result = SpellManager.Cast(spellName);
            LegacySpellManager.ClickRemoteLocation(clickCastLocation);

            Debug.Log("... result " + result, 1);
            Utils.Log("-" + spellName, Utils.Colour("Blue"));
            return result;
            
        }

        /// <summary>
        /// TRUE if the spell is on cooldown and can not be cast
        /// </summary>
        /// <param name="spellName"></param>
        /// <returns>Name of the spell to check</returns>
        public static bool IsOnCooldown(string spellName)
        {
            Debug.Log("IsOnCooldown",1);
            if (!IsKnown(spellName)) return true;

            bool result = SpellManager.Spells[spellName].Cooldown;
            Debug.Log("... result " + result, 1);
            
            return result;
        }

        /// <summary>
        /// Conditionally cast a debuff on your current target
        ///   * Check if the debuff is on the target
        ///   * Check if HB can cast the spell
        /// </summary>
        /// <param name="spellName">Name of the debuff to cast</param>
        /// <returns>TRUE if the debuff was cast successfully</returns>
        public static bool CastDebuff(string spellName)
        {
            Debug.Log("CastDebuff " + spellName, 1);
            if (Target.IsDebuffOnTarget(spellName)) return false;
            if (!CanCast(spellName)) return false;

            bool result = SpellManager.Cast(spellName);
            Debug.Log("... result " + result, 1);
            if (!result) return false;

            Utils.Log(String.Format("{0} on {1}", spellName, Me.CurrentTarget.Name), Utils.Colour("Blue"));
            return true;
        }

        /// <summary>
        /// Cast a debuff on a specific target
        /// </summary>
        /// <param name="spellName">Name of the debuff to cast</param>
        /// <param name="targetUnit">WoWUnit to cast the debuff on</param>
        /// <returns>TRUE if the debuff was cast successfully</returns>
        public static bool CastDebuff(string spellName, WoWUnit targetUnit)
        {
            Debug.Log(String.Format("CastDebuff {0} targetUnit {1}", spellName, targetUnit.Name), 1);
            if (targetUnit.HasAura(spellName)) return false;
            if (!CanCast(spellName)) return false;

            bool result = SpellManager.Cast(spellName, targetUnit);
            Debug.Log("... result " + result, 1);
            if (!result) return false;

            Utils.Log(String.Format("{0} on {1}", spellName, Me.CurrentTarget.Name), Utils.Colour("Blue"));
            return true;
        }

        /// <summary>
        /// Cast a buff on a specific target
        /// </summary>
        /// <param name="spellName">Name of the buff to cast</param>
        /// <param name="targetUnit">WoWUnit to cast the buff on</param>
        /// <returns>TRUE if the buff was cast successfully</returns>
        public static bool CastBuff(string spellName, WoWUnit targetUnit)
        {
            Debug.Log(String.Format("CastBuff {0} targetUnit {1}", spellName, targetUnit.Name), 1);
            if (targetUnit.HasAura(spellName)) return false;
            if (!CanCast(spellName)) return false;

            bool result = SpellManager.Cast(spellName, targetUnit);
            Debug.Log("... result " + result, 1);
            if (!result) return false;

            Utils.Log(spellName);
            return true;
        }

        
        /// <summary>
        /// TRUE if you have enough mana to cast the spell
        /// </summary>
        /// <param name="spellName">Spell name to check</param>
        /// <returns></returns>
        public static bool IsEnoughMana(string spellName)
        {
            Debug.Log("IsEnoughMana " + spellName, 1);
            if (!IsKnown(spellName)) return false;

            bool result = (Me.CurrentMana > SpellManager.Spells[spellName].PowerCost);
            Debug.Log("... result " + result, 1);

            return true;
        }

        
        /// <summary>
        /// TRUE if you have enough focus to cast the spell
        /// </summary>
        /// <param name="spellName">Spell name to check</param>
        /// <returns></returns>
        public static bool IsEnoughFocus(string spellName)
        {
            Debug.Log("IsEnoughFocus " + spellName, 1);
            if (!IsKnown(spellName)) return false;

            bool result = (Me.CurrentFocus > SpellManager.Spells[spellName].PowerCost);
            Debug.Log("... result " + result, 1);

            return true;
        }


        public static int PowerCost(string spellName)
        {
            Debug.Log("PowerCost " + spellName, 1);
            if (!IsKnown(spellName)) return 9999999;

            int result = SpellManager.Spells[spellName].PowerCost;
            Debug.Log("... result " + result, 1);

            return result;
        }


        /// <summary>
        /// Returns the maximum distance of the spell
        /// </summary>
        /// <param name="spellName">Spell name to check</param>
        /// <returns>The maximum distance the spell can be cast</returns>
        public static double MaxDistance(string spellName)
        {
            Debug.Log("MaxDistance " + spellName, 1);
            double spellDistance = 0.0;

            if (IsKnown(spellName))
            {
                //if (SpellManager.Spells[spellName].HasRange)
                    spellDistance = SpellManager.Spells[spellName].MaxRange;
            }

            Debug.Log("... result " + spellDistance, 1);
            return spellDistance;
        }

        public static double MinDistance(string spellName)
        {
            Debug.Log("MinDistance " + spellName, 1);
            double spellDistance = 0.0;

            if (IsKnown(spellName))
            {
                //For some reason .HasRange does not work in HB. Disable this check for now
                //if (SpellManager.Spells[spellName].HasRange)
                    spellDistance = SpellManager.Spells[spellName].MinRange;
            }
            Debug.Log("... result " + spellDistance, 1);
            return spellDistance;
        }


    }
}

