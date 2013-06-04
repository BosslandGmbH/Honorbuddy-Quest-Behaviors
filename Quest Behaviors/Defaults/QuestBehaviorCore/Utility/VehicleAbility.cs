// Behavior originally contributed by Nesox / completely reworked by Chinajade
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

#region Usings
using System.Linq;

using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    // 11Mar2013-04:41UTC chinajade
    public class VehicleAbility
    {
        public VehicleAbility(int abilityIndex /*[1..12]*/)
        {
            QuestBehaviorBase.ContractRequires((1 <= abilityIndex) && (abilityIndex <= 12),
                                                context => "1 <= abilityIndex) && (abilityIndex <= 12)");

            AbilityIndex = abilityIndex;
            _luaCommand_UseAbility = string.Format("CastPetAction({0})", abilityIndex);
            _luaQuery_ActionInfo = string.Format("return GetPetActionInfo({0})", abilityIndex);
            _luaQuery_IsUsable = string.Format("return GetPetActionSlotUsable({0})", abilityIndex);
            _vehicleAbilityNameDefault = string.Format("VehicleAbility({0})", abilityIndex);

            QuestBehaviorBase.LogWarning("NEW Weapon: {0}", Name);
        }

        public int AbilityIndex { get; private set; }


        #region Private and Convenience variables
        private LocalPlayer Me { get { return QuestBehaviorBase.Me; } }

        private readonly string _luaQuery_ActionInfo;
        private readonly string _luaQuery_IsUsable;
        private readonly string _luaCommand_UseAbility;
        private WoWPetSpell _vehicleAbility;
        private string _vehicleAbilityName;
        private readonly string _vehicleAbilityNameDefault;
        private readonly static WoWPetSpell.PetSpellType[] _vehicleAbilityTypes =
            {
                WoWPetSpell.PetSpellType.Action,
                WoWPetSpell.PetSpellType.Spell,
                WoWPetSpell.PetSpellType.Stance
            };
        #endregion


        // 11Mar2013-04:41UTC chinajade
        protected WoWPetSpell FindVehicleAbility()
        {
            if (!Me.InVehicle)
            {
                // assignment intentional, we want it to become null again when we exit vehicle...
                return _vehicleAbility = null;
            }

            return
                _vehicleAbility
                ?? (_vehicleAbility =
                        Me.PetSpells
                        .FirstOrDefault(petSpell => _vehicleAbilityTypes.Contains(petSpell.SpellType)
                                                        && ((petSpell.ActionBarIndex + 1) == AbilityIndex)));
        }


        // 11Mar2013-04:41UTC chinajade
        protected WoWSpell FindVehicleAbilitySpell()
        {
            var vehicleAbility = FindVehicleAbility();

            return
                (vehicleAbility == null)
                ? null
                : vehicleAbility.Spell;
        }


        // 11Mar2013-04:41UTC chinajade
        public bool IsAbilityReady()
        {
            var spell = FindVehicleAbilitySpell();

            return
                IsAbilityUsable()
                && (spell != null)
                && !spell.Cooldown;
        }


        // 11Mar2013-04:41UTC chinajade
        public bool IsAbilityUsable()
        {
            return Me.InVehicle
                && Lua.GetReturnVal<bool>(_luaQuery_IsUsable, 0);
        }


        // 11Mar2013-04:41UTC chinajade
        public string Name
        {
            get
            {
                if (!Me.InVehicle)
                {
                    // We want dynamic name to return to null when we exit vehicle...
                    _vehicleAbilityName = null;
                    return _vehicleAbilityNameDefault;
                }

                // If we've previously acquired the name, return what we know...
                if (!string.IsNullOrEmpty(_vehicleAbilityName))
                { return _vehicleAbilityName; }

                // We must resort to LUA, since the SpellManager doesn't keep up with 'pet spells'...
                // NB: The 'missile effect' (from the Spell, if any) usually does not correlate
                //  to the ability name.  So, look it up properly.
                _vehicleAbilityName = Lua.GetReturnVal<string>(_luaQuery_ActionInfo, 0);

                return
                    !string.IsNullOrEmpty(_vehicleAbilityName)
                        ? _vehicleAbilityName
                        : _vehicleAbilityNameDefault;
            }
        }


        public bool UseAbility()
        {
            if (!Me.InVehicle)
            {
                QuestBehaviorBase.LogWarning("Attempted to use {0} while not in Vehicle!", Name);
                return false;
            }

            if (IsAbilityReady())
            {
                Lua.DoString(_luaCommand_UseAbility);
                return true;
            }

            return false;
        }
    }
}
