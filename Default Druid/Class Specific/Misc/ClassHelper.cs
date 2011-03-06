using System.Collections.Generic;
using Hera.SpellsMan;
using Hera.Config;
using Styx;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.Logic.POI;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Hera.Helpers
{
    public enum ClassType
    {
        None = 0,
        Balance,
        Feral,
        Restoration
    }

    public static class ClassHelper
    {
        private static LocalPlayer Me { get { return ObjectManager.Me; } }
        private static ClassType _classSpec;
        public static ClassType ClassSpec
        {
            get { return _classSpec; }
            set
            {
                Utils.Log("Your spec has been detected as " + ClassSpec);
                _classSpec = value;
            }
        }

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
        private static double _minimumDistance = 3.0;
        /// <summary>
        /// If your distance from the target is greater than MaxDistance, it will move to this distance
        /// Set this to a few yards less than MaxDistance
        /// </summary>
        public static double MinimumDistance
        {
            get { return _minimumDistance; }
            set { _minimumDistance = value; }
        }

        /// <summary>
        /// TRUE if the there is a debuff on you that you can remove
        /// In this case (for Druid) its Poison and Curse
        /// </summary>
        public static bool NeedToDecurse
        {
            get 
            {
                foreach (KeyValuePair<string, WoWAura> aura in Me.Auras)
                {
                    if (!aura.Value.IsHarmful) continue;

                    if (aura.Value.Spell.DispelType == WoWDispelType.Curse || aura.Value.Spell.DispelType == WoWDispelType.Poison)
                        return true;
                }

                return false;
            }
        }


        public static WoWUnit NeedToDecursePlayer
        {
            get
            {
                foreach (KeyValuePair<string, WoWAura> aura in Me.Auras)
                {
                    if (!aura.Value.IsHarmful) continue;
                    if (aura.Value.Spell.DispelType == WoWDispelType.Curse) return Me;
                    if (aura.Value.Spell.DispelType == WoWDispelType.Poison) return Me;
                }

                if (Me.IsInParty)
                {
                    foreach (WoWPlayer p in Me.PartyMembers)
                    {
                        foreach (KeyValuePair<string, WoWAura> aura in p.Auras)
                        {
                            if (!aura.Value.IsHarmful) continue;
                            if (aura.Value.Spell.DispelType == WoWDispelType.Curse) return p;
                            if (aura.Value.Spell.DispelType == WoWDispelType.Poison) return p;
                        }
                    }
                }

                return null;
            }
        }


        /// <summary>
        /// Simply function to check for a minimum and maximum combo points
        /// If combo points are inside this range the result is TRUE
        /// </summary>
        /// <param name="minComboPoints">Minimum range</param>
        /// <param name="maxComboPoints">Maximum range</param>
        /// <returns></returns>
        public static bool ComboCheck(int minComboPoints, int maxComboPoints)
        {
            if (minComboPoints == 0) { return Me.ComboPoints <= maxComboPoints; }

            if (maxComboPoints == 0) { return Me.ComboPoints >= minComboPoints; }

            return Me.ComboPoints >= minComboPoints && Me.ComboPoints <= maxComboPoints;
        }

        /// <summary>
        /// Simply function to check for a minimum and maximum combo points and if you can cast the given spell
        /// If combo points are inside this range the result is TRUE
        /// </summary>
        /// <param name="minComboPoints">Minimum range</param>
        /// <param name="maxComboPoints">Maximum range</param>
        /// <param name="canCastSpellName">Spell name to check if it can be cast</param>
        /// <returns></returns>
        public static bool ComboCheck(int minComboPoints, int maxComboPoints, string canCastSpellName)
        {
            return (Spell.CanCast(canCastSpellName) && ComboCheck(minComboPoints, maxComboPoints));
        }
        
        /// <summary>
        /// Simply function to check for a minimum and maximum combo points and if you can cast the given spell
        /// If combo points are inside this range the result is TRUE
        /// </summary>
        /// <param name="minComboPoints">Minimum range</param>
        /// <param name="maxComboPoints">Maximum range</param>
        /// <param name="canCastSpellName">Spell name to check if it can be cast</param>
        /// <param name="onlyIfNotDebuffed">Check if this spell debuff is already on the target</param>
        /// <returns></returns>
        public static bool ComboCheck(int minComboPoints, int maxComboPoints, string canCastSpellName, bool onlyIfNotDebuffed)
        {
            if (Target.IsDebuffOnTarget(canCastSpellName) && onlyIfNotDebuffed) return false;
            return (Spell.CanCast(canCastSpellName) && ComboCheck(minComboPoints, maxComboPoints));
        }
        
        public class Shapeshift 
        {

        /// <summary>
        /// TRUE if you should be casting spells, checks if you are the following
        /// Moonkin, Tree of Life, Travel form and Human/caster form
        /// </summary>
        public static bool IsCasterCapable
        {
            get
            {
                switch (Me.Shapeshift)
                {
                    case ShapeshiftForm.Cat:
                    case ShapeshiftForm.Bear:
                    case ShapeshiftForm.DireBear:
                        return false;

                    case ShapeshiftForm.Moonkin:
                    case ShapeshiftForm.Travel:
                    case ShapeshiftForm.TreeOfLife:
                    case ShapeshiftForm.Normal:
                        return true;

                }

                return false;
            }
        }

        public static bool IsCatForm { get { return (Me.Shapeshift == ShapeshiftForm.Cat); } }

        public static bool IsBearForm { get { return (Me.Shapeshift == ShapeshiftForm.Bear || Me.Shapeshift == ShapeshiftForm.DireBear); } }

        public static bool IsMoonkinForm { get { return (Me.Shapeshift == ShapeshiftForm.Moonkin); } }

        public static bool IsHumanForm { get { return (Me.Shapeshift == ShapeshiftForm.Normal); } }

        public static bool IsTravelForm { get { return (Me.Shapeshift == ShapeshiftForm.Travel); } } 
        
        public static bool IsWaterForm { get { return (Me.Shapeshift == ShapeshiftForm.Aqua); } }

        public static bool IsTreeForm { get { return (Me.Shapeshift == ShapeshiftForm.TreeOfLife); } }

        /// <summary>
        /// Shapeshift to Cat form
        /// </summary>
        public static void CatForm()
        {
            if (Spell.IsKnown("Cat Form") && Spell.CanCast("Cat Form"))
                Spell.Cast("Cat Form");
        }

        /// <summary>
        /// Shapeshift to Moonkin form
        /// </summary>
        public static void MoonkinForm()
        {
            if (Spell.IsKnown("Moonkin Form") && Spell.CanCast("Moonkin Form"))
                Spell.Cast("Moonkin Form");
        }

        /// <summary>
        /// Shapeshift to Bear form
        /// </summary>
        public static void BearForm()
        {
            if (Spell.IsKnown("Bear Form") && Spell.CanCast("Bear Form"))
                Spell.Cast("Bear Form");

        }

        public static void TravelForm()
        {
            if (Spell.IsKnown("Travel Form") && Spell.CanCast("Travel Form") && !Me.HasAura("Travel Form"))
                Spell.Cast("Travel Form");

        }

        /// <summary>
        /// TRUE if you need to shapeshift
        /// </summary>
        public static bool NeedToShapeshift
        {
            get
            {
                // If you don't know cat form then you won't know anything else so just leave
                if (!Spell.IsKnown("Cat Form")) return false;
                bool result = false;
                
                switch (ClassSpec)
                {
                    case ClassType.Balance:
                        return Spell.CanCast("Moonkin Form") && !IsMoonkinForm;

                    case ClassType.Feral:
                    case ClassType.None:
                        if (!Utils.Adds && Me.Combat && IsCatForm) return false;
                        if (!Me.IsInParty && CLC.ResultOK(Settings.BearForm) && Spell.CanCast("Bear Form") && !IsBearForm) result = true;    // We need to be in bear form so make it so
                        if (!Me.IsInParty && CLC.ResultOK(Settings.BearForm) && IsBearForm) return false;                                    // Don't do anything we are in bear form and thats where we need to be
                        //if (CLC.ResultOK(Settings.BearForm) && IsBearForm) result = true;

                        if (result && Me.GotTarget && !Target.IsLowLevel) return true;
                        return Spell.CanCast("Cat Form") && !IsCatForm;
                }

                return false;
            }
        }


        /// <summary>
        /// Automatically shapeshift to the appropriate form
        /// </summary>
        /// <returns></returns>
        public static bool AutoShapeshift()
        {
            switch (ClassSpec)
                {
                    case ClassType.Balance:
                        if (Spell.CanCast("Moonkin Form") && !IsMoonkinForm) { MoonkinForm(); return true; }
                        return false;

                    case ClassType.Feral:
                    case ClassType.None:
                        if (CLC.ResultOK(Settings.BearForm) && Spell.CanCast("Bear Form") && !IsBearForm)
                        {
                            BearForm();
                            Utils.AutoAttack(true);
                            return true;
                        }
                        if (Spell.CanCast("Cat Form") && !IsCatForm)
                        {
                            if (CLC.ResultOK(Settings.BearForm) && IsBearForm) return false;
                            CatForm();
                            Utils.AutoAttack(true);
                            return true;
                        }
                        return false;

                        //case ClassType.None:
                        //return false;
                }

            return false;
        }

        }

        /// <summary>
        /// Can we safely use Travel form.
        /// Only if destination is more than 100 yards and there are no hostile mobs in 60 yards
        /// </summary>
        public static bool CanUseTravelForm
        {
            get
            {
                if (Me.Combat) return false;
                if (Me.Mounted) return false;
                if (!Spell.IsKnown("Travel Form")) return false;
                if (Shapeshift.IsTravelForm) return false;
                if (!CLC.ResultOK(Settings.TravelForm)) return false;
                if (!Spell.CanCast("Travel Form")) return false;
                if (Me.Dead || Me.IsGhost) return false;
                if (Me.IsInParty) return false;

                int poiDistance = (int) BotPoi.Current.Location.Distance(StyxWoW.Me.Location);

                if (poiDistance < Settings.TravelFormMinDistance) return false;
                bool result = Utils.HostileMobsInRange(Settings.TravelFormHostileRange);

                if (!result) { Utils.Log(string.Format("Destination is {0} yards away and no hostiles in range, using Travel Form", poiDistance)); }
                
                return !result;
            }
        }


        private static string _balanceDPSSpell = "Wrath";
        public static string BalanceDPSSpell
        {
            get { return _balanceDPSSpell; }
            set { _balanceDPSSpell = value; }
        }

        public static bool IsHealerOnly
        {
            get
            {
                if (ClassSpec == ClassType.Restoration)
                {
                    if (Utils.IsBattleground) return true;
                    if (Me.IsInParty) return true;
                    if (RaFHelper.Leader != null) return true;
                    if (Me.PartyMembers.Count > 1) return true;

                }
                return false;
            }
        }
    }

    
}
