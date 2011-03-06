using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Styx.Logic.Combat;
using Styx.WoWInternals.WoWObjects;

namespace PrinceOfDarkness
{
    public class UnitHelper
    {
        public static bool HasBuff(WoWUnit unit, WoWAura aura)
        {
            return HasBuff(unit, aura.Spell);
        }

        public static bool HasBuff(WoWUnit unit, WoWSpell spell)
        {
            if (unit == null)
                return false;
            
            var auras = unit.GetAllAuras();
            foreach (var a in auras)
            {
                //PrinceOfDarkness.Debug("hasbuff: name={0}, spellName={1}, spellID={2}", a.Name, a.Spell.Name, a.Spell.Id);
                if (a.SpellId == spell.Id && a.CreatorGuid == PrinceOfDarkness.Me.Guid)
                    return true;
            }
            return false;
        }

        public static bool MustRefreshBuff(WoWUnit unit, WoWSpell spell)
        {
            if (unit == null)
                return false;

            var auras = unit.GetAllAuras();
            foreach (var a in auras)
            {
                //PrinceOfDarkness.Debug("hasbuff: name={0}, spellName={1}, spellID={2}", a.Name, a.Spell.Name, a.Spell.Id);
                if (a.SpellId == spell.Id && a.CreatorGuid == PrinceOfDarkness.Me.Guid)
                    return a.TimeLeft.TotalSeconds <= 1.0;
            }
            //not found
            return true;
        }
    }
}
