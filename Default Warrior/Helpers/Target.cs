using System;
using System.Diagnostics;
using Hera.SpellsMan;
using Styx.Helpers;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Settings = Hera.Config.Settings;


namespace Hera.Helpers
{
    public static class Target
    {

        private static LocalPlayer Me { get { return ObjectManager.Me; } }
        private static WoWUnit CT { get { return Me.CurrentTarget; } }
        private static Stopwatch pullTimer = new Stopwatch();
        private static Stopwatch combatTimer = new Stopwatch();
        private static ulong _pullGuid;
        private static ulong _combatGuid;



        /// <summary>
        /// The current health percent of the target
        /// </summary>
        public static double HealthPercent { get { return CT.HealthPercent; } }

        /// <summary>
        /// TRUE if the current target is more than 4 levels lower than you
        /// </summary>
        public static bool IsLowLevel
        {
            get
            {
                if (!Me.GotTarget) return false;

                // Temp fix, see if we can resolve crashing issue.
                if (CT.Level < 6) return false;
                return CT.Level <= Me.Level - 4;
            }
        }

        /// <summary>
        /// TRUE if the current target is more than 3 levels higher than you
        /// </summary>
        public static bool IsHighLevel
        {
            get
            {
                return Me.GotTarget && CT.Level >= Me.Level - 3;

                // Temp fix, see if we can resolve crashing issue.
                //if (CT.Level < 6) return false;
            }
        }

        public static bool IsFleeing { get { return Me.GotTarget && CT.Fleeing; } }

        public static bool IsDistanceLessThan(double distanceCheck) { return Me.GotTarget && CT.Distance < distanceCheck; }

        public static bool IsDistanceMoreThan(double distanceCheck) { return Me.GotTarget && CT.Distance > distanceCheck; }

        public static bool IsTargetingMe { get { return Me.GotTarget && CT.CurrentTargetGuid == Me.Guid; } }

        public static bool IsHealthAbove(double targetHealth) { return Me.GotTarget && CT.HealthPercent > targetHealth; }

        public static double Distance { get { return !Me.GotTarget ? 0 : CT.Distance; } }

        public static bool IsCasting { get { return Me.GotTarget && CT.IsCasting; } }

        /// <summary>
        /// This is not actually true, this checks if the target's health is > 10X your own health
        /// </summary>
        public static bool IsInstanceBoss
        {
            get
            {
                if (!Me.GotTarget) return false;

                uint myHp = Me.MaxHealth;
                uint ctHp = CT.MaxHealth;
                bool result = (ctHp > myHp * 11);

                return result;
            }
        }

        public static bool IsWithinInteractRange
        {
            get
            {
                return Me.GotTarget && CT.WithinInteractRange;
            }
        }

        public static float InteractRange
        {
            get
            {
                return !Me.GotTarget ? 0 : CT.InteractRange;
            }
        }

        /// <summary>
        /// TRUE is the debuff is on the target.
        /// Similar to CanDebuffTarget except this does not check if you can cast the spell
        /// </summary>
        /// <param name="debuffName"></param>
        /// <returns></returns>
        public static bool IsDebuffOnTarget(string debuffName) { return Me.GotTarget && CT.HasAura(debuffName); }  //.HasAura(DebuffName)); }

        /// <summary>
        /// TRUE if the target has been tagged and you are not in Party/Raid
        /// </summary>
        public static bool IsTaggedByOther { get { return (Me.GotTarget && !(Me.IsInParty || Me.IsInRaid) && CT.TaggedByOther); } }

        /// <summary>
        /// TRUE if the target is considered a caster
        /// A caster is a NPC that has MANA
        /// </summary>
        public static bool IsCaster { get { return Me.GotTarget && CT.ManaPercent > 1; } }

        /// <summary>
        /// TRUE if HB can generate a path to the target. 
        /// </summary>
        //public static bool CanGenerateNavPath { get { return Me.GotTarget && Navigator.GeneratePath(Me.Location, CT.Location).Length > 0; } }
        public static bool CanGenerateNavPath
        {
            get
            {
                return Me.GotTarget && Navigator.CanNavigateFully(Me.Location, CT.Location, 60);
                // Navigator.GeneratePath(Me.Location, CT.Location).Length > 0;
            }
        }

        /// <summary>
        /// Blacklist the target for X seconds
        /// </summary>
        /// <param name="seconds">Seconds to blacklist the target</param>
        public static void BlackList(int seconds) { if (!Me.GotTarget) return; Styx.Logic.Blacklist.Add(CT, new TimeSpan(0, 0, seconds)); Me.ClearTarget(); }

        public static bool IsPlayer { get { return Me.GotTarget && CT.IsPlayer; } }

        public static void Face() { if (!Me.GotTarget) return; CT.Face(); }

        public static bool IsFacing
        {
            get
            {
                return Me.GotTarget && WoWMathHelper.IsFacing(Me.Location, Me.Rotation, Me.CurrentTarget.Location, WoWMathHelper.DegreesToRadians(120));
                //return Me.GotTarget && Me.IsSafelyFacing(CT.Location);
            }
        }

        public static bool IsElite { get { return Me.GotTarget && CT.Elite; } }

        /// <summary>
        /// TRUE if you can cast the debuff on the target
        ///   * Do you have a target
        ///   * Can you cast the spell
        ///   * Is the debuff already on the target
        /// </summary>
        /// <param name="spellName">Debuff spell to cast on the target</param>
        /// <returns></returns>
        public static bool CanDebuffTarget(string spellName)
        {
            if (!Me.GotTarget) return false;
            if (!Spell.CanCast(spellName)) return false;
            if (CT.HasAura(spellName)) return false;
            if (CT.Distance > Spell.MaxDistance(spellName)) return false;

            return true;
        }

        /// <summary>
        /// Return the stack count of a given debuff on the current target
        /// </summary>
        /// <param name="debuffName">Debuff name to check</param>
        /// <returns>int of debuff stacks</returns>
        public static int DebuffStackCount(string debuffName)
        {
            if (!Me.GotTarget) return 0;
            if (!IsDebuffOnTarget(debuffName)) return 0;
            uint stackCount = CT.Auras[debuffName].StackCount;

            return (int)stackCount;
        }


        /// <summary>
        /// A number of simple checks to determine if the currently selected target should be pulled
        /// Checks the following; GotTarget, IsUnderground, IsTotem, IsPet & Tagged
        /// </summary>
        public static bool IsValidPullTarget
        {
            get
            {
                if (!Me.GotTarget) return false;
                // The .IsUnderground property has been removed from HB :(
                //if (CT.IsUnderground && !Me.IsUnderground) return false;
                if (CT.IsSwimming && !Me.IsSwimming) return false;
                if (CT.Distance2D > 90) return false;
                if (!CT.InLineOfSight) return false;
                if (CT.IsTotem) return false;
                if (CT.IsPet) return false;
                if (Styx.Logic.RaFHelper.Leader == null && CT.TaggedByOther) return false;

                
                return true;
            }
        }


        /// <summary>
        /// Check how long we've been in comat, is it more than 30 seconds?
        /// </summary>
        public static bool CombatTimerExpired
        {
            get
            {
                if (!Me.GotTarget) return false;
                if (Me.CurrentTarget.Dead) return false;

                if (_combatGuid != Me.CurrentTarget.Guid)
                {
                    Utils.Log(string.Format("New combat target {0}, resetting combat timer.", Me.CurrentTarget.Name), System.Drawing.Color.FromName("DarkBlue"));
                    _combatGuid = Me.CurrentTarget.Guid;
                    combatTimer.Reset();
                    combatTimer.Start();
                }

                return combatTimer.Elapsed.Seconds >= Settings.CombatTimeout;
            }
        }


        /// <summary>
        /// Have you been trying to pull the target for more than 20 seconds?
        /// </summary>
        public static bool PullTimerExpired
        {
            get
            {
                if (Self.IsBuffOnMe("Drink") && _pullGuid != 0) _pullGuid = 0;
                if (Self.IsBuffOnMe("Eat") && _pullGuid != 0) _pullGuid = 0;

                if (_pullGuid != Me.CurrentTarget.Guid)
                {
                    Utils.Log(string.Format("New pull target {0} ({1}), resetting pull timer.", Me.CurrentTarget.Name, Me.CurrentTarget.Level), System.Drawing.Color.FromName("DarkBlue"));
                    _pullGuid = Me.CurrentTarget.Guid;
                    pullTimer.Reset();
                    pullTimer.Start();
                }

                return pullTimer.Elapsed.Seconds >= 20;
            }
        }
    }
}
