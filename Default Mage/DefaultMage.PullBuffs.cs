using TreeSharp;

namespace DefaultMage
{
    public partial class DefaultMage
    {
        private Composite _pullBuffBehavior;
        public override Composite PullBuffBehavior
        {
            get
            {
                if (_pullBuffBehavior == null)
                {
                    Log("Creating 'PullBuff' behavior");
                    _pullBuffBehavior = CreatePullBuffBehavior();
                }

                return _pullBuffBehavior;
            }
        }

        /// <summary>
        /// Creates the behavior used for pulling mobs
        /// </summary>
        /// <returns></returns>
        private Composite CreatePullBuffBehavior()
        {
            return new PrioritySelector();
        }
    }
}
