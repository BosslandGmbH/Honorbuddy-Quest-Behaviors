using System;
using System.Drawing;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;

using Styx;
using Styx.Logic;
using Styx.Helpers;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.Combat.CombatRoutine;
using Styx.WoWInternals.WoWObjects;

namespace Rogue
{
    public class Rogue : CombatRoutine
    {
        #region Globals
        public override WoWClass Class { get { return WoWClass.Rogue; } }
        public override string Name { get { return "Default Rogue Combat Class"; } }
        private bool HasPicked;
        private int _logspamMobCount;
        private uint deathCount = 0;
        private uint countBlind = 0;
        private uint countGouge = 0;
        private WoWPoint prevPrevSafePoint = new WoWPoint();
        private WoWPoint prevSafePoint = new WoWPoint();
        private WoWPoint safePoint = new WoWPoint();
        private static Stopwatch fightTimer = new Stopwatch();
        private static Stopwatch moveTimer = new Stopwatch();
        private static Stopwatch pullTimer = new Stopwatch();
        private static ulong lastGuid;
        private string _logspam;
        private WoWUnit oldAdd;
        private List<WoWUnit> addsList = new List<WoWUnit>();
        private List<uint> HealPotionIds = new List<uint>()
        {
            118, 858, 4596, 929, 1710, 3928, 13446, 17348, 28100, 33934, 22829, 17349, 32947, 33092, 18839, 31839, 31852, 31853, 31838, 39671, 33447, 41166, 43531
        };
        #endregion
        #region Private Functions
        private void slog(string msg)
        {
            if (msg == _logspam)
            {
                return;
            }
            Logging.Write(msg);
            _logspam = msg;
        }
        private double targetDistance
        {
            get
            {
                return StyxWoW.Me.GotTarget ? StyxWoW.Me.CurrentTarget.Distance : uint.MaxValue - 1;
            }
        }
        private void SafeMoveToPoint(WoWPoint point, int duration)
        {
            moveTimer.Reset();
            moveTimer.Start();
            while (StyxWoW.Me.HealthPercent > 1)
            {
                Thread.Sleep(500);
                Navigator.MoveTo(point);
                if (!StyxWoW.Me.Auras.ContainsKey("Stealth") || moveTimer.ElapsedMilliseconds >= duration)
                {
                    break;
                }
                WoWMovement.MoveStop();
            }
            return;
        }
        private int SortByDistance(WoWUnit x, WoWUnit y)
        {
            int retval = StyxWoW.Me.Location.Distance(x.Location).CompareTo(StyxWoW.Me.Location.Distance(y.Location));
            return retval;
        }
        WoWPoint attackPointBuffer;
        WoWPoint attackPoint
        {
            get
            {
                if (StyxWoW.Me.GotTarget)
                {
                    attackPointBuffer = WoWMovement.CalculatePointFrom(StyxWoW.Me.CurrentTarget.Location, 1.0f);
                    return attackPointBuffer;
                }
                else
                {
                    WoWPoint noSpot = new WoWPoint();
                    return noSpot;
                }
            }
        }
        private List<WoWUnit> getAdds()
        {
            // Variables
            List<WoWUnit> enemyMobList = new List<WoWUnit>();
            // Get Objects of WoWUnit
            List<WoWUnit> mobList = ObjectManager.GetObjectsOfType<WoWUnit>(false);
            // Iterate WoWUnits found
            foreach (WoWUnit thing in mobList)
            {
                // Mob validation
                if (
                    thing.Distance > 40 ||
                    !thing.IsAlive
                    )
                {
                    continue;
                }
                // Mob must be attacking me
                if (thing.IsTargetingMeOrPet)
                {
                    // Push into List
                    enemyMobList.Add(thing);
                }
            }
            // Warning about amount of Mobs - if debugging, then display as well
            if ((enemyMobList.Count > 1 && _logspamMobCount != enemyMobList.Count))
            {
                slog("Warning - there are " + enemyMobList.Count.ToString() + " attackers");
            }
            // Spam check variable update
            _logspamMobCount = enemyMobList.Count;
            enemyMobList.Sort(SortByDistance);
            // Return list
            return enemyMobList;
        }
        private WoWItem HaveItemCheck(List<uint> listId)
        {
            foreach (WoWItem item in ObjectManager.GetObjectsOfType<WoWItem>(false))
            {
                if (listId.Contains(item.Entry))
                {
                    return item;
                }
            }
            return null;
        }
        #endregion
        #region Pull
        public override void Pull()
        {
            try
            {
                bgTargetCheck();
                getAdds();
                slog("Starting Pull");
                if (!StyxWoW.Me.Combat && StyxWoW.Me.GotTarget)
                {
                    slog("Safe location is saved");
                    prevPrevSafePoint = prevSafePoint;
                    prevSafePoint = safePoint;
                    safePoint = StyxWoW.Me.Location;
                }
                slog("PVP Checked");
                if (Blacklist.Contains(StyxWoW.Me.CurrentTarget.Guid))
                {
                    slog("Target is blacklisted");
                    Styx.Logic.Blacklist.Add(StyxWoW.Me.CurrentTarget.Guid, System.TimeSpan.FromSeconds(30));
                    StyxWoW.Me.ClearTarget();
                    //pullGuid = 0;
                }
                if (StyxWoW.Me.CurrentTarget.Guid != lastGuid)
                {
                    slog("Pull starting. Target is new");
                    pullTimer.Reset();
                    pullTimer.Start();
                    lastGuid = StyxWoW.Me.CurrentTarget.Guid;
                    if (StyxWoW.Me.CurrentTarget.IsPlayer)
                    {
                        slog("Pull: Killing Player at distance " + Math.Round(StyxWoW.Me.CurrentTarget.Distance).ToString() + "");
                    }
                    slog("Pull: Killing " + StyxWoW.Me.CurrentTarget.Name + " at distance " + Math.Round(StyxWoW.Me.CurrentTarget.Distance).ToString() + "");
                    pullTimer.Reset();
                    pullTimer.Start();
                }
                if (!StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.CurrentHealth > 95 && 30 * 1000 < pullTimer.ElapsedMilliseconds)
                {
                    slog(" This " + StyxWoW.Me.CurrentTarget.Name + " is a bugged mob.  Blacklisting for 1 hour.");
                    Blacklist.Add(StyxWoW.Me.CurrentTarget.Guid, TimeSpan.FromHours(1.00));
                    StyxWoW.Me.ClearTarget();
                    //pullGuid = 0;
                    if (StyxWoW.Me.Location.Distance(safePoint) >= 30)
                    {
                        slog("Try to move to safePoint");
                        SafeMoveToPoint(safePoint, 10000);
                    }
                    else if (StyxWoW.Me.Location.Distance(prevSafePoint) >= 30)
                    {
                        slog("Try to move to prevSafePoint");
                        SafeMoveToPoint(prevSafePoint, 10000);
                    }
                    else if (StyxWoW.Me.Location.Distance(prevPrevSafePoint) >= 30)
                    {
                        slog("Try to move to prevPrevSafePoint");
                        SafeMoveToPoint(prevPrevSafePoint, 10000);
                    }
                    else
                    {
                        slog("Can't move to locations");
                    }
                }
                if (!StyxWoW.Me.Combat)
                {
                    if (SpellManager.CanCast("Stealth") && !StyxWoW.Me.Auras.ContainsKey("Stealth") && LegacySpellManager.KnownSpells.ContainsKey("Pick Pocket"))
                    {
                        SpellManager.Cast("Stealth");
                    }
                }
                if (SpellManager.CanCast("Sprint") && !StyxWoW.Me.Combat && !StyxWoW.Me.Auras.ContainsKey("Sprint") && targetDistance > 7)
                {
                    SpellManager.Cast("Sprint");
                }
                if (SpellManager.CanCast("Distract"))
                {
                    if (StyxWoW.Me.IsAlive && StyxWoW.Me.GotTarget && !Battlegrounds.IsInsideBattleground && !StyxWoW.Me.Combat)
                    {
                        if (StyxWoW.Me.CurrentTarget.Distance > 4 && StyxWoW.Me.CurrentTarget.Distance < 25)
                        {
                            Distract();
                        }
                    }
                }
                if (!StyxWoW.Me.Combat && targetDistance > 4 && targetDistance < Styx.Logic.Targeting.PullDistance + 10)
                {
                    slog("Move to target");
                    int a = 0;
                    while (a < 50 && ObjectManager.Me.IsAlive && ObjectManager.Me.GotTarget && ObjectManager.Me.CurrentTarget.Distance > 4)
                    {
                        if (ObjectManager.Me.Combat)
                        {
                            Logging.Write("Combat has started.  Abandon pull.");
                            break;
                        }
                        WoWMovement.Face();
                        Navigator.MoveTo(WoWMovement.CalculatePointFrom(ObjectManager.Me.CurrentTarget.Location, 2f));
                        StyxWoW.SleepForLagDuration();
                        ++a;
                    }
                }
                else
                {
                    WoWMovement.MoveStop();
                    WoWMovement.Face();
                }
                if (StyxWoW.Me.GotTarget && targetDistance <= 5 && !StyxWoW.Me.IsAutoAttacking)
                {
                    slog("Final state of pulling");
                    if (attackPoint != WoWPoint.Empty)
                    {
                        Navigator.MoveTo(attackPoint);
                    }
                    if (SpellManager.CanCast("Pick Pocket") && StyxWoW.Me.GotTarget && targetDistance <= 5 && !StyxWoW.Me.CurrentTarget.IsPlayer)
                    {
                        if (StyxWoW.Me.CurrentTarget.CreatureType == WoWCreatureType.Humanoid || StyxWoW.Me.CurrentTarget.CreatureType == WoWCreatureType.Undead)
                        {
                            if (!HasPicked)
                            {
                                slog("Try to pickpocket");
                                PickPocket();
                                Thread.Sleep(1000);
                                HasPicked = true;
                            }
                        }
                    }
                    if (SpellManager.CanCast("Ambush") && StyxWoW.Me.GotTarget && targetDistance <= 5)
                    {
                        Ambush();
                    }
                    else
                    {
                        Lua.DoString("StartAttack()");
                    }
                }
            }
            catch (Exception e)
            {
                Logging.Write("{0} Exception caught.", e);
            }
            finally
            {
                slog("Pull done.");
                fightTimer.Reset();
                fightTimer.Start();
                HasPicked = false;
            }
        }
        #endregion
        #region Basic Combat
        // Combat Routine
        public override void Combat()
        {
            // Fix for POI System Loop while not having target and still in combat
            if (StyxWoW.Me.CurrentTargetGuid == 0 || StyxWoW.Me.CurrentTarget == null) return;
            // Clear target if CurrentTarget is dead
            if (StyxWoW.Me.GotTarget && !StyxWoW.Me.CurrentTarget.IsAlive)
            {
                StyxWoW.Me.ClearTarget();
                StyxWoW.SleepForLagDuration();
            }
            if (StyxWoW.Me.GotTarget && (StyxWoW.Me.CurrentTarget.Guid != lastGuid || InfoPanel.Deaths > deathCount))
            {
                if (InfoPanel.Deaths > deathCount)
                {
                    deathCount = InfoPanel.Deaths;
                }
                if (StyxWoW.Me.CurrentTarget.IsPlayer)
                {
                    slog("Killing level " + StyxWoW.Me.CurrentTarget.Level.ToString() + " " + StyxWoW.Me.CurrentTarget.Race.ToString() + " " + StyxWoW.Me.CurrentTarget.Class.ToString() + " at range of " + System.Math.Round(StyxWoW.Me.CurrentTarget.Distance).ToString() + " yards.");
                }
                else
                {
                    slog("Killing " + StyxWoW.Me.CurrentTarget.Name + " at range of " + System.Math.Round(StyxWoW.Me.CurrentTarget.Distance).ToString() + " yards.");
                }
                fightTimer.Reset();
                fightTimer.Start();
                lastGuid = StyxWoW.Me.CurrentTarget.Guid;
            }
            // If in combat and have no target, find one!
            if (!StyxWoW.Me.GotTarget)
            {
                // Find mobs attacking us
                addsList = getAdds();
                // If we find an attacking mob
                if (addsList.Count > 0)
                {
                    // Log
                    slog("Combat: Finding target..");
                    // Find valid mob
                    int mobIndex = 0;
                    do
                    {
                        addsList[mobIndex++].Target();
                    } while (!StyxWoW.Me.GotTarget && (StyxWoW.Me.GotTarget && !StyxWoW.Me.CurrentTarget.IsAlive) && addsList.Count < mobIndex);
                    // Move to attack range
                    if (attackPoint != WoWPoint.Empty)
                    {
                        Navigator.MoveTo(attackPoint);
                    }
                }
                else
                // No attacking mobs
                {
                    // Log
                    slog("Combat: No target found, ending combat..");
                    // Return from Combat()
                    return;
                }
            }
            if (combatChecks)
            {
                addsList = getAdds();
                if (addsList.Count > 1)
                {
                    if (addsList[1].Distance < 7)
                    {
                        if (SpellManager.CanCast("Blade Flurry") && StyxWoW.Me.GotTarget && targetDistance <= 5)
                        {
                            BladeFlurry();
                        }
                        if (SpellManager.CanCast("Killing Spree") && StyxWoW.Me.GotTarget && targetDistance <= 5)
                        {
                            KillingSpree();
                        }
                        if (SpellManager.CanCast("Adrenaline Rush") && StyxWoW.Me.GotTarget && targetDistance <= 5)
                        {
                            AdrenalineRush();
                        }
                        if (!StyxWoW.Me.Auras.ContainsKey("Blade Flurry") && !SpellManager.CanCast("Fan of Knives") && addsList.Count < 3)
                        {
                            BlindAndGouge();
                        }
                        if (StyxWoW.Me.Auras.ContainsKey("Blade Flurry") && SpellManager.CanCast("Fan of Knives") && StyxWoW.Me.GotTarget && targetDistance <= 5)
                        {
                            FanofKnives();
                        }
                    }
                }
                if (StyxWoW.Me.Stunned || StyxWoW.Me.Dazed && StyxWoW.Me.Combat)
                {
                    WillOfTheForsaken();
                    EveryManForHimself();
                }
                if (StyxWoW.Me.ComboPoints > 5)
                {
                    slog(StyxWoW.Me.ComboPoints.ToString() + " combo points.  Need to restart WoW.");
                }
            }
            else
            {
                return;
            }
            if (combatChecks)
            {
                if (SpellManager.CanCast("Berserking"))
                {
                    Berserking();
                }
                if (SpellManager.CanCast("Bloodrage"))
                {
                    Bloodrage();
                }
                if (StyxWoW.Me.GotTarget && StyxWoW.Me.CurrentTarget.IsCasting)
                {
                    if (SpellManager.CanCast("Kick") && StyxWoW.Me.GotTarget && targetDistance <= 5)
                    {
                        Kick();
                    }
                    if (SpellManager.CanCast("Gouge") && !SpellManager.CanCast("Kick"))
                    {
                        Gouge();
                    }
                    if (!SpellManager.CanCast("Kick") && !SpellManager.CanCast("Gouge") && SpellManager.CanCast("Arcane Torrent"))
                    {
                        ArcaneTorrent();
                    }
                }
            }
            if (StyxWoW.Me.HealthPercent < 50 && StyxWoW.Me.CurrentTarget.HealthPercent > 20 && StyxWoW.Me.Combat)
            {
                if (!combatChecks)
                {
                    return;
                }
                if (SpellManager.CanCast("Killing Spree") && StyxWoW.Me.GotTarget && targetDistance <= 5)
                {
                    KillingSpree();
                }
                if (SpellManager.CanCast("Evasion") && StyxWoW.Me.GotTarget && targetDistance <= 5)
                {
                    Evasion();
                }
                if (SpellManager.CanCast("Adrenaline Rush") && StyxWoW.Me.GotTarget && targetDistance <= 5)
                {
                    AdrenalineRush();
                }
                if (SpellManager.CanCast("Stoneform"))
                {
                    Stoneform();
                }
            }
            if (StyxWoW.Me.Combat && StyxWoW.Me.HealthPercent < 20 && StyxWoW.Me.CurrentTarget.HealthPercent > 15)
            {
                HealPotionDrink();
            }
            if (SpellManager.CanCast("Lifeblood") && StyxWoW.Me.HealthPercent < 50)
            {
                Lifeblood();
            }
            if (!SpellManager.CanCast("Vanish") && SpellManager.CanCast("Cloak of Shadows") && 25 > StyxWoW.Me.HealthPercent && 10 < StyxWoW.Me.CurrentTarget.HealthPercent && StyxWoW.Me.GotTarget)
            {
                CloakofShadows();
                StyxWoW.Me.ClearTarget();
                if (StyxWoW.Me.Location.Distance(safePoint) >= 30)
                {
                    slog("Try to move to safePoint");
                    SafeMoveToPoint(safePoint, 7000);
                }
                else if (StyxWoW.Me.Location.Distance(prevSafePoint) >= 30)
                {
                    slog("Try to move to prevSafePoint");
                    SafeMoveToPoint(prevSafePoint, 7000);
                }
                else if (StyxWoW.Me.Location.Distance(prevPrevSafePoint) >= 30)
                {
                    slog("Try to move to prevPrevSafePoint");
                    SafeMoveToPoint(prevPrevSafePoint, 7000);
                }
                else
                {
                    slog("Can't move to locations");
                }
            }
            if (StyxWoW.Me.GotTarget && StyxWoW.Me.HealthPercent < 25 && StyxWoW.Me.CurrentTarget.HealthPercent > 10 && SpellManager.CanCast("Vanish"))
            {
                Vanish();
                //pullGuid = 0;
                if (StyxWoW.Me.Location.Distance(safePoint) >= 30)
                {
                    slog("Try to move to safePoint");
                    SafeMoveToPoint(safePoint, 7000);
                }
                else if (StyxWoW.Me.Location.Distance(prevSafePoint) >= 30)
                {
                    slog("Try to move to prevSafePoint");
                    SafeMoveToPoint(prevSafePoint, 7000);
                }
                else if (StyxWoW.Me.Location.Distance(prevPrevSafePoint) >= 30)
                {
                    slog("Try to move to prevPrevSafePoint");
                    SafeMoveToPoint(prevPrevSafePoint, 7000);
                }
                else
                {
                    slog("Can't move to locations");
                }
            }
            if (SpellManager.CanCast("Deadly Throw"))
            {
                if (StyxWoW.Me.GotTarget && 10 < StyxWoW.Me.CurrentTarget.Distance && 30 >= targetDistance && StyxWoW.Me.ComboPoints > 0)
                {
                    DeadlyThrow();
                }
            }
            if (!SpellManager.CanCast("Deadly Throw") && SpellManager.CanCast("Sprint"))
            {
                if (StyxWoW.Me.GotTarget && 10 < StyxWoW.Me.CurrentTarget.Distance && 30 >= targetDistance)
                {
                    Sprint();
                }
            }
            if (!SpellManager.CanCast("Deadly Throw") && !SpellManager.CanCast("Sprint") && SpellManager.CanCast("Throw"))
            {
                if (StyxWoW.Me.GotTarget && StyxWoW.Me.CurrentTarget.Distance > 7 && StyxWoW.Me.CurrentTarget.Distance < 25)
                {
                    Throw();
                }
            }
            if (30 * 1000 < fightTimer.ElapsedMilliseconds && StyxWoW.Me.CurrentTarget.HealthPercent > 95)
            {
                slog(" This " + StyxWoW.Me.CurrentTarget.Name + " is a bugged mob.  Combat blacklisting for 1 hour.");
                Blacklist.Add(StyxWoW.Me.CurrentTarget.Guid, TimeSpan.FromHours(1.00));
                StyxWoW.Me.ClearTarget();
                lastGuid = 0;
                if (StyxWoW.Me.Location.Distance(safePoint) >= 30)
                {
                    slog("Try to move to safePoint");
                    SafeMoveToPoint(safePoint, 7000);
                }
                else if (StyxWoW.Me.Location.Distance(prevSafePoint) >= 30)
                {
                    slog("Try to move to prevSafePoint");
                    SafeMoveToPoint(prevSafePoint, 7000);
                }
                else if (StyxWoW.Me.Location.Distance(prevPrevSafePoint) >= 30)
                {
                    slog("Try to move to prevPrevSafePoint");
                    SafeMoveToPoint(prevPrevSafePoint, 7000);
                }
                else
                {
                    slog("Can't move to locations");
                }
            }
            if (StyxWoW.Me.GotTarget)
            {
                FinishingMoves();
            }
        }
        #endregion
        #region Advanced Combat
        private void FinishingMoves()
        {
            if (!StyxWoW.Me.Combat)
            {
                return;
            }
// Is Behind Check Broken            
//            if (SpellManager.CanCast("Backstab"))
//            {
//                if (StyxWoW.Me.GotTarget && StyxWoW.Me.CurrentTarget.Distance <= 5)
//                {
//                    if (StyxWoW.Me.MeIsSafelyBehind)
//                    {
//                        Backstab();
//                    }
//                }
//            }
            if (SpellManager.CanCast("Recuperate"))
            {
                if (StyxWoW.Me.GotTarget && targetDistance <= 7 && StyxWoW.Me.CurrentHealth < 75 && StyxWoW.Me.ComboPoints > 2)
                {
                    Recuperate();
                }
            }
            if (SpellManager.CanCast("Recuperate") && Battlegrounds.IsInsideBattleground)
            {
                if (StyxWoW.Me.GotTarget && targetDistance <= 25 && StyxWoW.Me.CurrentHealth < 100 && StyxWoW.Me.ComboPoints >= 1)
                {
                    Recuperate();
                }
            }
            if (SpellManager.CanCast("Riposte") && StyxWoW.Me.GotTarget && targetDistance <= 5)
            {
                Riposte();
            }
            if (SpellManager.CanCast("Slice and Dice") && !StyxWoW.Me.Auras.ContainsKey("Slice and Dice") && StyxWoW.Me.ComboPoints > 1 && StyxWoW.Me.GotTarget && targetDistance <= 5)
            {
                SliceAndDice();
            }
            if (StyxWoW.Me.ComboPoints > 1 && StyxWoW.Me.CurrentTarget.HealthPercent < 20)
            {
                if (SpellManager.CanCast("Eviscerate") && StyxWoW.Me.GotTarget && targetDistance <= 5)
                {
                    Eviscerate();
                }
            }
            if (StyxWoW.Me.ComboPoints > 3 && StyxWoW.Me.CurrentTarget.HealthPercent < 40)
            {
                if (SpellManager.CanCast("Eviscerate") && StyxWoW.Me.GotTarget && targetDistance <= 5)
                {
                    Eviscerate();
                }
            }
            if (StyxWoW.Me.ComboPoints > 4 && StyxWoW.Me.CurrentTarget.HealthPercent < 100)
            {
                if (SpellManager.CanCast("Eviscerate") && StyxWoW.Me.GotTarget && targetDistance <= 5)
                {
                    Eviscerate();
                }
            }
            if (StyxWoW.Me.ComboPoints < 5)
            {
                if (LegacySpellManager.KnownSpells.ContainsKey("Revealing Strike"))
                {
                    if (SpellManager.CanCast("Revealing Strike") && StyxWoW.Me.GotTarget && targetDistance <= 5)
                    {
                        RevealingStrike();
                    }
                }
                if (!LegacySpellManager.KnownSpells.ContainsKey("Revealing Strike"))
                {
                    if (SpellManager.CanCast("Sinister Strike") && StyxWoW.Me.GotTarget && targetDistance <= 5)
                    {
                        SinisterStrike();
                    }
                    else
                    {
                        return;
                    }
                }
            }
        }
        #endregion
        #region Combat Sanity Checks
        private bool combatChecks
        {
            get
            {
                if (!ObjectManager.Me.GotTarget)
                {
                    Logging.Write("No target.");
                    return false;
                }
                if (ObjectManager.Me.Dead)
                {
                    Logging.Write("I died.");
                    return false;
                }
                if (ObjectManager.Me.GotTarget && !ObjectManager.Me.IsAutoAttacking)
                {
                    Lua.DoString("StartAttack()");
                }
                if (StyxWoW.Me.GotTarget && ObjectManager.Me.CurrentTarget.Distance > 50)
                {
                    if (ObjectManager.Me.CurrentTarget.IsPlayer)
                    {
                        Logging.Write("Out of range: Level " + ObjectManager.Me.CurrentTarget.Level.ToString() + " " + ObjectManager.Me.CurrentTarget.Race.ToString() + " " + System.Math.Round(ObjectManager.Me.CurrentTarget.Distance).ToString() + " yards away.");
                    }
                    else
                    {
                        Logging.Write("Out of range: Level " + ObjectManager.Me.CurrentTarget.Name + " is " + System.Math.Round(ObjectManager.Me.CurrentTarget.Distance).ToString() + " yards away.");
                    }
                    StyxWoW.Me.ClearTarget();
                    return false;
                }
                double meleeRange = 5;
                if (StyxWoW.Me.GotTarget && StyxWoW.Me.CurrentTarget.Distance > meleeRange)
                {
                    int a = 0;
                    while (a < 50 && ObjectManager.Me.IsAlive && ObjectManager.Me.GotTarget && ObjectManager.Me.CurrentTarget.Distance > meleeRange)
                    {
                        WoWMovement.Face();
                        Navigator.MoveTo(WoWMovement.CalculatePointFrom(ObjectManager.Me.CurrentTarget.Location, 2.5f));
                        StyxWoW.SleepForLagDuration();
                        ++a;
                    }
                }
                if (StyxWoW.Me.GotTarget)
                {
                    WoWMovement.Face();
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
        public void bgTargetCheck()
        {
            if (Battlegrounds.IsInsideBattleground)
            {
                if (StyxWoW.Me.GotTarget && StyxWoW.Me.CurrentTarget.IsPet)
                {
                    slog("Battleground: Blacklist Pet " + StyxWoW.Me.CurrentTarget.Name);
                    Blacklist.Add(StyxWoW.Me.CurrentTarget.Guid, TimeSpan.FromDays(1));
                    StyxWoW.Me.ClearTarget();
                    lastGuid = 0;
                }
                if (StyxWoW.Me.GotTarget && !StyxWoW.Me.CurrentTarget.InLineOfSight)
                {
                    slog("Battleground: Target out of Line of Sight, blacklisting for 3 seconds");
                    Blacklist.Add(StyxWoW.Me.CurrentTarget.Guid, TimeSpan.FromSeconds(3));
                    StyxWoW.Me.ClearTarget();
                    lastGuid = 0;
                }
                if (StyxWoW.Me.GotTarget && StyxWoW.Me.CurrentTarget.Distance > 29)
                {
                    slog("Battleground: Target out of Range (" + StyxWoW.Me.CurrentTarget.Distance + " yards), blacklisting for 3 seconds");
                    Blacklist.Add(StyxWoW.Me.CurrentTarget.Guid, TimeSpan.FromSeconds(3));
                    StyxWoW.Me.ClearTarget();
                    lastGuid = 0;
                }
                if (StyxWoW.Me.GotTarget && StyxWoW.Me.CurrentTarget.Auras.ContainsKey("Divine Shield"))
                {
                    slog("Battleground: Target has Divine Shield, blacklisting for 10 seconds");
                    Blacklist.Add(StyxWoW.Me.CurrentTarget.Guid, TimeSpan.FromSeconds(10));
                    StyxWoW.Me.ClearTarget();
                    lastGuid = 0;
                }
                if (StyxWoW.Me.GotTarget && StyxWoW.Me.CurrentTarget.Auras.ContainsKey("Ice Block"))
                {
                    slog("Battleground: Target has Ice Block, blacklisting for 10 seconds");
                    Blacklist.Add(StyxWoW.Me.CurrentTarget.Guid, TimeSpan.FromSeconds(10));
                    StyxWoW.Me.ClearTarget();
                    lastGuid = 0;
                }
            }
        }
        #endregion
        #region Add Management
        private void BlindAndGouge()
        {
            WoWUnit myAdd = addsList[1];
            if (myAdd != oldAdd)
            {
                countBlind = 0;
                countGouge = 0;
            }
            if (countBlind < 3 && SpellManager.CanCast("Blind"))
            {
                addsList[1].Target();
                WoWMovement.Face();
                Thread.Sleep(750);
                SpellManager.Cast("Blind");
                addsList[0].Target();
                WoWMovement.Face();
                Thread.Sleep(750);
                ++countBlind;
                Lua.DoString("StartAttack()");
                oldAdd = myAdd;
            }
            else if (SpellManager.CanCast("Gouge") && 3 > countGouge)
            {
                addsList[1].Target();
                WoWMovement.Face();
                Thread.Sleep(750);
                SpellManager.Cast("Gouge");
                addsList[0].Target();
                Thread.Sleep(750);
                WoWMovement.Face();
                Lua.DoString("StartAttack()");
                ++countGouge;
                oldAdd = myAdd;
            }
            else
                return;
        }
        #endregion
        #region Rogue Skills
        private void AdrenalineRush()
        {
            SpellManager.Cast("Adrenaline Rush");
            slog("Skill: Adrenaline Rush");
        }
        private void Ambush()
        {
            SpellManager.Cast("Ambush");
            slog("Skill: Ambush");
        }
        private void Backstab()
        {
            SpellManager.Cast("Backstab");
            slog("Skill: Backstab");
        }
        private void BladeFlurry()
        {
            SpellManager.Cast("Blade Flurry");
            slog("Skill: Blade Flurry");
        }
        private void CloakofShadows()
        {
            SpellManager.Cast("Cloak of Shadows");
            slog("Skill: Cloak of Shadows");
        }
        private void DeadlyThrow()
        {
            SpellManager.Cast("Deadly Throw");
            slog("Skill: Deadly Throw");
        }
        private void Distract()
        {
            WoWPoint distractPoint = WoWMovement.CalculatePointFrom(StyxWoW.Me.CurrentTarget.Location, -4.0f);
            SpellManager.Cast("Distract");
            LegacySpellManager.ClickRemoteLocation(distractPoint);
            slog("Skill: Distract");
        }
        private void Evasion()
        {
            SpellManager.Cast("Evasion");
            slog("Skill: Evasion");
        }
        private void FanofKnives()
        {
            SpellManager.Cast("Fan of Knives");
            slog("Skill: Fan of Knives");
        }
        private void Gouge()
        {
            SpellManager.Cast("Gouge");
            slog("Skill: Gouge");
        }
        private void Kick()
        {
            SpellManager.Cast("Kick");
            slog("Skill: Kick");
        }
        private void KillingSpree()
        {
            SpellManager.Cast("Killing Spree");
            slog("Skill: Killing Spree");
        }
        private void Eviscerate()
        {
            SpellManager.Cast("Eviscerate");
            slog("Skill: Eviscerate");
        }
        private void PickPocket()
        {
            SpellManager.Cast("Pick Pocket");
            slog("Skill: Pick Pocket");
        }
        private void Recuperate()
        {
            SpellManager.Cast("Recuperate");
            slog("Skill: Recuperate");
        }
        private void RevealingStrike()
        {
            SpellManager.Cast("Revealing Strike");
            slog("Skill: Revealing Strike");
        }
        private void Riposte()
        {
            SpellManager.Cast("Riposte");
            slog("Skill: Riposte");
        }
        private void SinisterStrike()
        {
            SpellManager.Cast("Sinister Strike");
            slog("Skill: Sinister Strike");
        }
        private void SliceAndDice()
        {
            SpellManager.Cast("Slice and Dice");
            slog("Skill: Slice and Dice");
        }
        private void Sprint()
        {
            SpellManager.Cast("Sprint");
            slog("Skill: Sprint");
        }
        private void Throw()
        {
            SpellManager.Cast("Throw");
            slog("Skill: Throw");
        }
        private void Vanish()
        {
            SpellManager.Cast("Vanish");
            slog("Skill: Vanish");
        }
        #endregion
        #region Racials
        #region Alliance Racials
        private void Stoneform()
        {
            SpellManager.Cast("Stoneform");
            slog("Racial: Stoneform");
        }
        private void GiftOfTheNaaru()
        {
            if (SpellManager.CanCast("Gift of the Naaru"))
            {
                SpellManager.Cast("Gift of the Naaru");
                slog("Racial: Gift of the Naaru");
            }
        }
        private void EveryManForHimself()
        {
            if (SpellManager.CanCast("Every Man for Himself"))
            {
                SpellManager.Cast("Every Man for Himself");
                slog("Racial: Every Man for Himself");
            }
        }
        private void Shadowmeld()
        {
            if (SpellManager.CanCast("Shadowmeld"))
            {
                SpellManager.Cast("Shadowmeld");
                slog("Racial: Shadowmeld");
            }
        }
        private void EscapeArtist()
        {
            if (SpellManager.CanCast("Escape Artist"))
            {
                SpellManager.Cast("Escape Artist");
                slog("Racial: Escape Artist");
            }
        }
        #endregion
        #region Horde Racials
        private void Berserking()
        {
            SpellManager.Cast("Berserking");
            slog("Racial: Berserking");
        }
        private void Bloodrage()
        {
            SpellManager.Cast("Bloodrage");
            slog("Racial: Bloodrage");
        }
        private void WarStomp()
        {
            if (SpellManager.CanCast("War Stomp"))
            {
                SpellManager.Cast("War Stomp");
                slog("Racial: War Stomp");
            }
        }
        private void ArcaneTorrent()
        {
            if (SpellManager.CanCast("Arcane Torrent"))
            {
                SpellManager.Cast("Arcane Torrent");
                slog("Racial: Arcane Torrent");
            }
        }
        private void WillOfTheForsaken()
        {
            if (SpellManager.CanCast("Will of the Forsaken"))
            {
                SpellManager.Cast("Will of the Forsaken");
                slog("Racial: Will of the Forsaken");
            }
        }
        #endregion
        #endregion
        #region Profession Skills
        private void Lifeblood()
        {
            SpellManager.Cast("Lifeblood");
            slog("Profession: Lifeblood");
        }
        #endregion
        #region Potion Management
        private void HealPotionDrink()
        {
            WoWItem healPotion = HaveItemCheck(HealPotionIds);
            if (!Equals(null, healPotion))
            {
                slog("Heal Potion drinking.");
                Lua.DoString("UseItemByName(\"" + healPotion.Entry + "\")");
                StyxWoW.SleepForLagDuration();
            }
            else
            {
                slog("No potions in backpack.");
            }
        }
        #endregion
        #region Rest Functions
        public override bool NeedRest
        {
            get
            {
                bool restNeeded = false;
                if (!StyxWoW.Me.Auras.ContainsKey("Food") && StyxWoW.Me.HealthPercent < 55 && !StyxWoW.Me.IsSwimming)
                {
                    slog("Rest: Need health");
                    restNeeded = true;
                    return restNeeded;
                }
                return restNeeded;
            }
        }
        public override void Rest()
        {
            if (StyxWoW.Me.Mounted)
            {
                Mount.Dismount();
            }
            // if we have food, eat it
            if (!Styx.Logic.Common.Rest.NoFood && StyxWoW.Me.HealthPercent < 55 && !StyxWoW.Me.IsSwimming)
            {
                slog("Rest: Eating Food");
                Lua.DoString("UseItemByName(\"" + Styx.Helpers.LevelbotSettings.Instance.FoodName + "\")");
                Thread.Sleep(500);
                SpellManager.Cast("Stealth");
                while (StyxWoW.Me.HealthPercent < 98 && StyxWoW.Me.HasAura("Food"))
                {
                    Thread.Sleep(10);
                }
            }
            fightTimer.Reset();
            pullTimer.Reset();
        }
        #endregion
        #region Required Overrides
        public override bool NeedPreCombatBuffs
        {
            get
            {
                return false;
            }
        }
        public override void PreCombatBuff()
        {
        }
        public override bool NeedCombatBuffs
        {
            get
            {
                return false;
            }
        }
        public override void CombatBuff()
        {
        }
        public override bool NeedHeal
        {
            get
            {
                return false;
            }
        }
        public override void Heal()
        {
        }
        public override bool NeedPullBuffs
        {
            get
            {
                return false;
            }
        }
        public override void PullBuff()
        {
        }
        #endregion
    }
}
