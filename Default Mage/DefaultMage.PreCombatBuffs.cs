using Styx.Logic.Combat;
using TreeSharp;

namespace DefaultMage
{
    public partial class DefaultMage
    {
        private Composite _preCombatBuffBehavior;
        public override Composite PreCombatBuffBehavior
        {
            get
            {
                if (_preCombatBuffBehavior == null)
                {
                    Log("Creating 'PreCombatBuff' behavior");
                    _preCombatBuffBehavior = CreatePreCombatBuffsBehavior();
                }

                return _preCombatBuffBehavior;
            }
        }

        /// <summary>
        /// Creates the behavior used for buffing with regular buffs. eg; 'Power Word: Fortitude', 'Inner Fire' etc...
        /// </summary>
        /// <returns></returns>
        private Composite CreatePreCombatBuffsBehavior()
        {
            return new PrioritySelector(

                new Decorator(ret => !Me.Mounted && SpellManager.HasSpell("Arcane Brilliance") && !Me.Auras.ContainsKey("Fel Intelligence") && !Me.Auras.ContainsKey("Arcane Brilliance") && !Me.Auras.ContainsKey("Dalaran Brilliance") && !Me.Auras.ContainsKey("Wisdom of Agamaggan"),
                              new PrioritySelector(
                                  CreateBuffCheckAndCast("Arcane Brilliance")
                                  )),

                new Decorator(ret => !Me.Mounted && SpellManager.HasSpell("Summon Water Elemental") && !Me.GotAlivePet,
                              new PrioritySelector(  
                                  CreateBuffCheckAndCast("Summon Water Elemental")
                                  )),


                new Decorator(ret => !Me.Mounted && SpellManager.HasSpell("Molten Armor") && !Me.Mounted && !SpellManager.HasSpell("Mage Armor") && !SpellManager.HasSpell("Frost Armor"),
                              new PrioritySelector(
                                  CreateBuffCheckAndCast("Molten Armor", true)
                                  )),

                new Decorator(ret => !Me.Mounted && SpellManager.HasSpell("Molten Armor") && !Me.Mounted && !SpellManager.HasSpell("Mage Armor") && SpellManager.HasSpell("Frost Armor"),
                              new PrioritySelector(
                                  CreateBuffCheckAndCast("Frost Armor", true)
                                  )),

                new Decorator(ret => !Me.Mounted && SpellManager.HasSpell("Molten Armor") && !Me.Mounted && SpellManager.HasSpell("Mage Armor") && SpellManager.HasSpell("Frost Armor"),
                              new PrioritySelector(
                                  CreateBuffCheckAndCast("Mage Armor", true)
                                  ))
                );
        }
    }
}
