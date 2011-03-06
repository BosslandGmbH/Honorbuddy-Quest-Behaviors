using TreeSharp;

namespace DefaultMage
{
    public partial class DefaultMage
    {
        private Composite _combatBuffBehavior;
        public override Composite CombatBuffBehavior
        {
            get
            {
                if (_combatBuffBehavior == null)
                {
                    Log("Creating 'CombatBuff' behavior");
                    _combatBuffBehavior = CreateCombatBuffBehavior();
                }

                return _combatBuffBehavior;
            }
        }

        private Composite CreateCombatBuffBehavior()
        {
            return new PrioritySelector();
        }
    }
}
