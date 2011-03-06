using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PrinceOfDarkness
{
    //this class computes a few stuff about combat handling
    public class Behavior
    {
        private static List<double> manaPercentsAfterCombats;
        private static List<double> healthPercentsAfterCombats;

        private static int WINDOW = 4;

        public static void Reset()
        {
            PrinceOfDarkness.Debug("Resetting oracle data...");
            PrinceOfDarkness.maxAdds = 1;
            manaPercentsAfterCombats = new List<double>();
            healthPercentsAfterCombats = new List<double>();
        }

        public static void AddCombatStats()
        {
            if (PrinceOfDarkness.Me.Dead || PrinceOfDarkness.Me.IsGhost)
            {
                PrinceOfDarkness.Debug("I died. resetting oracle.");
                Reset();
                return;
            }

            if (manaPercentsAfterCombats.Count >= WINDOW)
                manaPercentsAfterCombats.RemoveAt(0);
            if (healthPercentsAfterCombats.Count >= WINDOW)
                healthPercentsAfterCombats.RemoveAt(0);

            healthPercentsAfterCombats.Add(PrinceOfDarkness.Me.HealthPercent);
            manaPercentsAfterCombats.Add(PrinceOfDarkness.Me.HealthPercent);

            SetMaxAdds();
        }

        public static void SetMaxAdds()
        {
            if (manaPercentsAfterCombats.Count == WINDOW)
            {
                double worstDeltaMana = 0;
                double worstDeltaHealth = 0;

                double averageMana = 0, averageHealth = 0;

                foreach (var p in manaPercentsAfterCombats)
                {
                    averageMana += p / WINDOW;
                    foreach (var p2 in manaPercentsAfterCombats)
                        if (Math.Abs(p - p2) > worstDeltaMana)
                            worstDeltaMana = Math.Abs(p - p2);
                }

                foreach (var p in healthPercentsAfterCombats)
                {
                    averageHealth += p / WINDOW;
                    foreach (var p2 in healthPercentsAfterCombats)
                        if (Math.Abs(p - p2) > worstDeltaHealth)
                            worstDeltaHealth = Math.Abs(p - p2);
                }

                PrinceOfDarkness.Debug("Oracle: deltaMana={0}, deltaLife={1}", worstDeltaMana, worstDeltaHealth);
                PrinceOfDarkness.Debug("Oracle: averageMana={0}, averageLife={1}", averageMana, averageHealth);

                //if stats are acceptable, consider pulling more mobs

                var worstDelta = Math.Max(worstDeltaHealth, worstDeltaMana);
                var worstAverage = Math.Max(averageMana, averageHealth);

                if (worstAverage > 75 && worstDelta < 10)
                {
                    PrinceOfDarkness.Debug("Nice stats! Trying to pull more mobs from now on.");
                    PrinceOfDarkness.maxAdds = Math.Min(3, PrinceOfDarkness.maxAdds + 1); ;
                }
                else
                {
                    PrinceOfDarkness.Debug("Dangerous combat behavior. Lowering max Adds!");
                    PrinceOfDarkness.maxAdds = Math.Max(1, PrinceOfDarkness.maxAdds-1);
                }
            }
            else
            {
                PrinceOfDarkness.Debug("Oracle needs {0} more combats", WINDOW - manaPercentsAfterCombats.Count);
                PrinceOfDarkness.maxAdds = 1;
            }

            PrinceOfDarkness.Debug("Oracle set maxAdds to " + PrinceOfDarkness.maxAdds);
        }

    }
}
