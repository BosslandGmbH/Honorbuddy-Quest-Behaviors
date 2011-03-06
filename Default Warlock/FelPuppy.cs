using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Styx.WoWInternals.WoWObjects;
using Styx.WoWInternals;
using Styx.Logic.Combat;

namespace PrinceOfDarkness
{
    public class FelPuppy
    {
        //this one aims to help controlling warlock pets
        public static WoWUnit Pet { get { return PrinceOfDarkness.Me.Pet; } }

        public static bool IsValid { get { return Pet != null && !Pet.Dead;  } }

        private static Dictionary<WoWSpell, int> spells; //spell - book index

        //must call this periodicaly
        public static void Pulse()
        {
            //handle fake autocast spells
            CheckAutoCast();
        }

        public static bool Cast (WoWSpell spell)
        {
            if (spell == null)
                return false;
            PrinceOfDarkness.Debug("Trying to cast pet spell: " + spell.Name);
            if (!HasSpell(spell))
            {
                PrinceOfDarkness.Debug("FelPuppy.Cast error: pet don't have spell id=" + spell.Id);
                return false;
            }

            if (spell.Cooldown)
            {
                PrinceOfDarkness.Debug("Pet's spell is on cooldown.");
                return false;
            }

            if (Pet.IsCasting)
            {
                PrinceOfDarkness.Debug("Pet is already casting");
                return false;
            }

            PrinceOfDarkness.Debug("Pet casts spell: " + spell.Name);
            string lua = string.Format("CastSpell({0}, BOOKTYPE_PET)", 
                spells.First(keypair => keypair.Key.Id == spell.Id).Value);
            PrinceOfDarkness.Debug("Inject lua: /script " + lua);
            Lua.DoString(lua);
            return true;
        }

        //******************************************************************
        //enables auto-casting pet spells that arent in wow, like felguard's stun
        //XXX storing spell id instead of wowspell instances as they dont have comparators........
        private static Dictionary<int, AutoCastCondition> autoCastSpells = new Dictionary<int, AutoCastCondition>();
        public enum AutoCastCondition
        {
            PetNearTarget,
            Any
        }

        public static void AddAutoCast(WoWSpell spell, AutoCastCondition condition)
        {
            PrinceOfDarkness.Debug("Faking autocast pet spell: {0} IF {1}", spell.Name, condition.ToString());
            autoCastSpells.Add(spell.Id, condition);
        }

        public static void RemoveAutoCast(WoWSpell spell)
        {
            PrinceOfDarkness.Debug("Removed fake autocast pet spell: " + spell.Name);
            autoCastSpells.Remove(spell.Id);
        }

        public static void RemoveAllAutoCast()
        {
            foreach (var pair in autoCastSpells)
                RemoveAutoCast(WoWSpell.FromId(pair.Key));
        }

        private static void CheckAutoCast()
        {
            if (!IsValid)
                return;

            foreach (var pair in autoCastSpells)
            {
                WoWSpell spell = WoWSpell.FromId(pair.Key);
                if (CanCast(spell))
                {
                    //check autocast condition
                    switch (pair.Value)
                    {
                        case AutoCastCondition.PetNearTarget:
                            if (Pet.CurrentTarget != null &&
                                Pet.CurrentTarget.IsAlive &&
                                Pet.CurrentTarget.Location.DistanceSqr(Pet.Location) < 20)
                                break; //can cast
                            else
                                continue; //cannot cast, try next one

                        default:
                            //pass
                            break;
                    }

                    //yay.
                    Cast(spell);
                    return;
                }
            }
        }

        //******************************************************************

        public static bool CanCast(WoWSpell spell)
        {
            return IsValid && HasSpell(spell) && !spell.Cooldown && !Pet.IsCasting && !Pet.Stunned;
        }

        public static bool HasSpell(WoWSpell spell)
        {
            //awful.
            return spell != null && spells != null && spells.Any(keypair => keypair.Key.Id == spell.Id);
        }

        public static void HandlePetBarUpdated(object sender, LuaEventArgs args)
        {
            PrinceOfDarkness.Debug("FelPuppy: Detected pet bar update. Reloading pet skills...");
            UpdatePetSkills();
        }

        public static void UpdatePetSkills () {
            spells = new Dictionary<WoWSpell, int>();

            if (PrinceOfDarkness.Me.Pet == null)
                return;

            List<string> lua = Lua.GetReturnValues(@"
                local ids = ''
                for j = 1, 32 do
                    local _, spellID = GetSpellBookItemInfo(j, BOOKTYPE_PET)
                    if spellID ~= nil then
                        ids = ids .. j.. ':' .. spellID .. ','
                    else
                        break
                    end
                end
                return ids
            ");

            if (lua == null || lua.Count == 0)
            {
                PrinceOfDarkness.Debug("WARN: FelPuppy.UpdatePetSkills lua injection failed!!!");
                return;
            }

            foreach (var raw in lua[0].Split(','))
            {
                if (raw == null || raw.Length < 1)
                    continue;
                int id = 0;
                int spellid = 0;
                int.TryParse(raw.Substring(0, raw.IndexOf(':')), out id);
                int.TryParse(raw.Substring(raw.IndexOf(':')+1), out spellid);
                if (id < 1 || spellid < 1)
                    continue;
                WoWSpell spell = WoWSpell.FromId(spellid);
                if (spell == null)
                    continue;
                PrinceOfDarkness.Debug("Found pet spell: Key={0}, Name={1}", id, spell.Name);
                spells.Add(spell, id);
            }

            PrinceOfDarkness.Debug("This pet has {0} skills.", spells.Count);
        }

        public static void Attack()
        {
            if (IsValid)
            {
                PrinceOfDarkness.Debug("Pet attack on target");
                Lua.DoString("PetAttack()");
            }
        }

        public static void Attack(WoWUnit target)
        {
            if (IsValid && target != null)
            {
                PrinceOfDarkness.Debug("Pet attack on "+target.Name);
                WoWUnit lastTarget = PrinceOfDarkness.Me.CurrentTarget;
                PrinceOfDarkness.TargetUnit(target);
                FelPuppy.Attack();
                PrinceOfDarkness.TargetUnit(lastTarget);
            }
        }
    }
}
