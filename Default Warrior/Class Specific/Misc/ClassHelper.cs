using Hera.SpellsMan;
using Styx;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Hera.Helpers
{
    public enum ClassType
    {
        None = 0,
        Arms,
        Fury,
        Protection
    }

    public static class ClassHelper
    {
        private static LocalPlayer Me { get { return ObjectManager.Me; } }
        public static ClassType ClassSpec { get; set; }

        // Maximum pull distance for this class
        private static double _maximumDistance = 5;
        /// <summary>
        /// Maximum distance for casting spells or melee. If you are a caster set this to the maximum distance of your spell
        /// If you are melee set this to 5.5
        /// </summary>
        public static double MaximumDistance
        {
            get { return _maximumDistance; }
            set { _maximumDistance = value; }
        }

        // Move to distance for this class
        // If the target is more than MaxDistance away, the character will move to this (minimumDistance) distance
        // Useful for pulling thats at max range that are moving away from you, or fleeing targets
        private static double _minimumDistance = 3;
        /// <summary>
        /// If your distance from the target is greater than MaxDistance, it will move to this distance
        /// Set this to a few yards less than MaxDistance
        /// </summary>
        public static double MinimumDistance
        {
            get { return _minimumDistance; }
            set { _minimumDistance = value; }
        }

        public class Stance
        {

            public static bool IsBattleStance { get { return (Me.Shapeshift == ShapeshiftForm.BattleStance); } }

            public static bool IsBerserkerStance { get { return (Me.Shapeshift == ShapeshiftForm.BerserkerStance); } }

            public static bool IsDefensiveStance { get { return (Me.Shapeshift == ShapeshiftForm.DefensiveStance); } }


            public static void BattleStance()
            {
                if (Spell.IsKnown("Battle Stance") && Spell.CanCast("Battle Stance"))
                    Spell.Cast("Battle Stance");
            }

            public static void BerserkerStance()
            {
                if (Spell.IsKnown("Berserker Stance") && Spell.CanCast("Berserker Stance"))
                    Spell.Cast("Berserker Stance");
            }

            public static void DefensiveStance()
            {
                if (Spell.IsKnown("Defensive Stance") && Spell.CanCast("Defensive Stance"))
                    Spell.Cast("Defensive Stance");
            }


            public static bool NeedToStanceDance
            {
                get
                {
                    return false;
                }
            }

        }


    }
}
