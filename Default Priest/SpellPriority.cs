using System;
using System.Collections.Generic;

namespace DefaultPriest
{
    internal class SpellPriority : IComparer<SpellPriority>, IComparable<SpellPriority>
    {
        public SpellPriority(string name, int priority)
        {
            Name = name;
            Priority = priority;
        }

        public string Name { get; set; }
        public int Priority { get; set; }

        #region Implementation of IComparer<SpellPriority>

        /// <summary>
        /// Compares two objects and returns a value indicating whether one is less than, equal to, or greater than the other.
        /// </summary>
        /// <returns>
        /// Value Condition Less than zero<paramref name="x"/> is less than <paramref name="y"/>.Zero<paramref name="x"/> equals <paramref name="y"/>.Greater than zero<paramref name="x"/> is greater than <paramref name="y"/>.
        /// </returns>
        /// <param name="x">The first object to compare.</param><param name="y">The second object to compare.</param>
        public int Compare(SpellPriority x, SpellPriority y)
        {
            if (x != null)
            {
                return x.CompareTo(y);
            }

            return 0;
        }

        #endregion

        #region Implementation of IComparable<SpellPriority>

        /// <summary>
        /// Compares the current object with another object of the same type.
        /// </summary>
        /// <returns>
        /// A 32-bit signed integer that indicates the relative order of the objects being compared. The return value has the following meanings: Value Meaning Less than zero This object is less than the <paramref name="other"/> parameter.Zero This object is equal to <paramref name="other"/>. Greater than zero This object is greater than <paramref name="other"/>. 
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public int CompareTo(SpellPriority other)
        {
            if (other == null)
                return 1;

            if (other.Priority < Priority)
                return -1;

            if (other.Priority == Priority)
                return 0;

            if (other.Priority > Priority)
                return 1;

            return -1;
        }

        #endregion
    }
}
