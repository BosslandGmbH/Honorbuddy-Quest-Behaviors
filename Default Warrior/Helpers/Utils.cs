using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using Hera.SpellsMan;
using Styx;
using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;

namespace Hera.Helpers
{
    public static class Utils
    {
        public enum CastingBreak
        {
            None = 0,
            HealthIsAbove,
            HealthIsBelow,
            PowerIsAbove,
            PowerIsBelow
        }

        private static string _logSpam;
        private static LocalPlayer Me { get { return ObjectManager.Me; } }
        private static WoWUnit CT { get { return Me.CurrentTarget; } }

        public static void Log(string msg, Color colour) { if (msg == _logSpam) return; Logging.Write(colour, msg); _logSpam = msg; }
        public static void Log(string msg){ if (msg == _logSpam) return;  Logging.Write(msg); _logSpam = msg; }
        
        public static Color Colour(string nameOfColour) { return Color.FromName(nameOfColour); }

        /// <summary>
        /// Thread sleep for the duration of your current in-game lag
        /// </summary>
        public static void LagSleep() { StyxWoW.SleepForLagDuration(); }

        
        public static int CountOfAddsInRange(double distance, WoWPoint location)
        {
            List<WoWUnit> hlist =
                (from o in ObjectManager.ObjectList
                 where o is WoWUnit
                 let p = o.ToUnit()
                 where p.Distance2D < 40
                       && !p.Dead
                       && p.Combat
                       && (p.IsTargetingMyPartyMember || p.IsTargetingMeOrPet)
                       && p.IsHostile
                       && p.Attackable
                 select p).ToList();

            return hlist.Count(u => location.Distance(u.Location) <= distance);
        }


        /// <summary>
        /// TRUE if you have adds
        /// </summary>
        public static bool Adds
        {
            get
            {
                // I'm No longer using HB's TargetList count to do the add check as this is not producing the desired result
                // Instead using my own add check. Basically get all alive mobs attacking me or my pet
                if (!Me.IsInParty)
                {
                    List<WoWUnit> hlist =
                        (from o in ObjectManager.ObjectList
                         where o is WoWUnit
                         let p = o.ToUnit()
                         where p.Distance2D < 50
                               && !p.Dead
                               && p.IsTargetingMeOrPet // || Me.IsInInstance && p.IsTargetingMyPartyMember)
                               && p.Attackable
                         select p).ToList();


                    return hlist.Count > 1;
                }


                List<WoWUnit> hplist =
                       (from o in ObjectManager.ObjectList
                        where o is WoWUnit
                        let p = o.ToUnit()
                        where p.Distance2D < 50
                              && !p.Dead
                              && p.Combat
                              && (p.IsTargetingMyPartyMember || p.IsTargetingMeOrPet)
                              && p.Attackable
                        select p).ToList();


                return hplist.Count > 1;
            }
        }

        public static bool AddsInstance
        {
            get
            {
                List<WoWUnit> hlist =
                    (from o in ObjectManager.ObjectList
                     where o is WoWUnit
                     let p = o.ToUnit()
                     where p.Distance2D < 60
                           && !p.Dead
                           && p.Combat
                           && (p.IsTargetingMyPartyMember || p.IsTargetingMeOrPet)
                           && p.IsHostile
                           && p.Attackable
                     select p).ToList();

                return hlist.Count > 1;
            }
        }

        public static bool CanAoEInstance
        {
            get
            {
                if (RaFHelper.Leader == null) return false;

                List<WoWUnit> hlist =
                    (from o in ObjectManager.ObjectList
                     where o is WoWUnit
                     let p = o.ToUnit()
                     where p.Distance2D < 60
                           && !p.Dead
                           && p.Combat
                           && (p.IsTargetingMyPartyMember || p.IsTargetingMeOrPet)
                           && p.IsHostile
                           && p.Attackable
                     select p).ToList();

                int countNearTank = hlist.Where(u => u.HealthPercent >= 25).Count(u => RaFHelper.Leader.Location.Distance(u.Location) < 15);

                // If you have 3+ adds then AoE
                return countNearTank > 2;
            }
        }

       
        /// <summary>
        /// The nunmber of adds in combat with you.
        /// </summary>
        public static int AddsCount
        {
            get
            {
                List<WoWUnit> hlist =
               (from o in ObjectManager.ObjectList
                where o is WoWUnit
                let p = o.ToUnit()
                where p.Distance2D < 45
                      && !p.Dead
                    //&& p.IsTargetingMeOrPet
                      && (p.Aggro || p.PetAggro)
                      && p.Attackable
                select p).ToList();


                return hlist.Count;
                //return Targeting.Instance.TargetList.Count;
            }
        }

        /// <summary>
        /// Perform a LUA query and return the result as a string
        /// </summary>
        /// <param name="lua"></param>
        /// <returns></returns>
        public static string LuaGetReturnValueString(string lua)
        {
            return Lua.GetReturnValues(lua, "stuff.lua")[0];
        }
    

        /// <summary>
        /// Checks for GCD, if you are casting a spell, got a target.
        /// Optionally if you know the spell and its cooldown status
        /// </summary>
        /// <param name="spellName">Spell you want to check if it exists and cooldown status</param>
        /// /// <param name="skipTargetCheck">If TRUE will not check for a valid target</param>
        /// <returns>TRUE is everything is ok and you should continue</returns>
        public static bool IsCommonChecksOk(string spellName, bool skipTargetCheck)
        {
            if (Spell.IsGCD) return false;
            if (Me.IsCasting) return false;
            if (Me.Dead) return false;

            // You want to skip the target check if you are healing or casting a spell that does not require a target
            if (!skipTargetCheck)
            {
                if (!Me.GotTarget) return false;
                if (CT.Dead) return false;
            }

            // if we're not checking a spell then all is good
            if (spellName == "") return true;

            // check if we know the spell and its not on cooldown
            if (!Spell.IsKnown(spellName)) return false;
            if (Spell.IsOnCooldown(spellName)) return false;

            // all is good lets continue
            return true;
        }


        // Auto attack, kind of useful for melee classes :)
        public static void AutoAttack(bool autoAttackOn)
        {
            if (autoAttackOn) { if (Me.IsAutoAttacking) return; Lua.DoString("StartAttack()"); return; }
            Lua.DoString("StopAttack()");
        }

        // Do a simple loop while casting a spell. 
        // Required so you don't double heal 
        public static void WaitWhileCasting()
        {
            Thread.Sleep(150);
            while (Me.IsCasting) { Thread.Sleep(100); }
        }

        public static void WaitWhileCasting(CastingBreak statCheck, double breakValue, WoWUnit targetCheck)
        {
            Thread.Sleep(150);
            while (Me.IsCasting)
            {
                Thread.Sleep(100);
                switch (statCheck)
                {
                    case CastingBreak.None:
                        Thread.Sleep(1);
                        break;

                    case CastingBreak.HealthIsAbove:
                        if (targetCheck.HealthPercent > breakValue)
                        {
                            Spell.StopCasting();
                            return;
                        }
                        break;

                    case CastingBreak.HealthIsBelow:
                        if (targetCheck.HealthPercent < breakValue)
                        {
                            Spell.StopCasting();
                            return;
                        }
                        break;

                    case CastingBreak.PowerIsAbove:
                        if (targetCheck.PowerPercent > breakValue)
                        {
                            Spell.StopCasting();
                            return;
                        }
                        break;

                    case CastingBreak.PowerIsBelow:
                        if (targetCheck.PowerPercent < breakValue)
                        {
                            Spell.StopCasting();
                            return;
                        }
                        break;

                }
            }
        }

        // Are you in a battleground
        public static bool IsBattleground { get { return Battlegrounds.IsInsideBattleground; } }

        /// <summary>
        /// Return a WoWUnit type of a player in your party/raid in need of healing. Null if noone is in need of healing
        /// </summary>
        /// <param name="minimumHealth">The health a player must be to be considered for healing</param>
        /// <returns>WoWUnit the player most in need of healing</returns>
        public static WoWUnit PlayerNeedsHealing(double minimumHealth)
        {
            // If you're not in a party then just leave
            if (!Me.IsInParty) return null;

            // MyGroup is populated with raid members or party members, whichever you are in
            List<WoWPlayer> myGroup = Me.IsInRaid ? Me.RaidMembers : Me.PartyMembers;

            // Enumerate all players in myGroup and find the person with the lowest health %
            List<WoWPlayer> playersToHeal = (from o in myGroup
                                            let p = o.ToPlayer()
                                            where p.Distance < 40
                                                  && !p.Dead
                                                  && !p.IsGhost
                                                  && p.InLineOfSight
                                                  && p.HealthPercent < minimumHealth
                                            orderby p.HealthPercent ascending
                                            select p).ToList();

            // If playersToHeal is more than 0 then we have someone to heal
            // So return the first person in the list, they will be the most in need
            return playersToHeal.Count > 0 ? playersToHeal[0] : null;
        }

        /// <summary>
        /// The best target to attack, the one with the lowest health % and the closest
        /// </summary>
        public static WoWUnit BestTarget
        {
            get
            {
                List<WoWUnit> mobsHealth = (from o in ObjectManager.ObjectList
                                            where o is WoWUnit && o.Distance < 40
                                            let u = o.ToUnit()
                                            where u.Attackable && u.IsAlive && Me.Aggro && !Blacklist.Contains(u.Guid)
                                            //where u.Attackable && u.IsAlive && u.IsTargetingMeOrPet && Me.CurrentTargetGuid != u.Guid
                                            orderby u.HealthPercent, u.Distance2D ascending
                                            select u).ToList();

                return mobsHealth.Count > 0 ? mobsHealth[0] : null;
            }
        }

        /// <summary>
        /// Scan the area [searchRange] yard for hostile mobs
        /// </summary>
        /// <param name="searchRange"></param>
        /// <returns></returns>
        public static bool HostileMobsInRange(double searchRange)
        {
            List<WoWUnit> hlist =
               (from o in ObjectManager.ObjectList
                where o is WoWUnit
                let p = o.ToUnit()
                where p.Distance2D < searchRange
                      && !p.Dead
                      && p.IsHostile
                select p).ToList();


            return hlist.Count > 0;
        }


        public static bool IsInLineOfSight(WoWPoint location)
        {
            bool result = GameWorld.IsInLineOfSight(Me.Location, location);

            return result;
        }

        public static bool IsInLineOfSight()
        {
            if (Me.IsInParty)
            {
                if (RaFHelper.Leader != null && !IsInLineOfSight(RaFHelper.Leader.Location)) return false;
            }
            else
            {
                if (Me.GotTarget && !IsInLineOfSight(CT.Location)) return false;
            }

            return true;

        }

        public static void MoveToLineOfSight()
        {
            WoWPoint location = Me.Location;
            if (Me.IsInParty && RaFHelper.Leader != null) location = RaFHelper.Leader.Location;
            if (!Me.IsInParty && Me.GotTarget) location = CT.Location;

            Movement.MoveTo(location);
            while (!GameWorld.IsInLineOfSight(Me.Location, location))
            {
                Movement.MoveTo(location);
                Thread.Sleep(250);
            }

            if (Me.IsMoving) Movement.StopMoving();
        }

        public static void MoveToLineOfSight(WoWPoint location)
        {
            //Utils.Log(string.Format("We don't have LOS on {0} moving closer...", CT.Name),System.Drawing.Color.FromName("DarkRed"));
            Movement.MoveTo(location);
            while (!GameWorld.IsInLineOfSight(Me.Location, location))
            {
                Movement.MoveTo(location);
                Thread.Sleep(250);
            }

            if (Me.IsMoving) Movement.StopMoving();
        }

        /// <summary>
        /// A nice class to keep all my timers in a single place and not have to worry about creating and tracking all the Stopwatches.
        /// </summary>
        public static class Timers
        {
            private static Dictionary<string, Stopwatch> _timerCollection = new Dictionary<string, Stopwatch>();

            public static void Add(string timerName)
            {
                Stopwatch stw = new Stopwatch();
                stw.Start();

                _timerCollection.Add(timerName, stw);
            }

            public static void Remove(string timerName)
            {
                _timerCollection.Remove(timerName);
            }

            public static Stopwatch Timer(string timerName)
            {
                if (!timerExists(timerName)) return null;
                return _timerCollection[timerName];
            }

            public static bool Expired(string timerName, long maximumMilliseconds)
            {
                if (!timerExists(timerName)) return false; 
                return _timerCollection[timerName].ElapsedMilliseconds > maximumMilliseconds;
            }

            public static bool IsRunning(string timerName)
            {
                if (!timerExists(timerName)) return false;
                return _timerCollection[timerName].IsRunning;
            }

            public static void Start(string timerName)
            {
                if (!timerExists(timerName)) return;
                _timerCollection[timerName].Start();
            }

            public static void Stop(string timerName)
            {
                if (!timerExists(timerName)) return;
                _timerCollection[timerName].Stop();
            }

            public static long ElapsedMilliseconds(string timerName)
            {
                if (!timerExists(timerName)) return 0;
                return _timerCollection[timerName].ElapsedMilliseconds;
            }

            public static long ElapsedSeconds(string timerName)
            {
                if (!timerExists(timerName)) return 0;
                return _timerCollection[timerName].ElapsedMilliseconds / 1000;
            }

            public static void Reset(string timerName)
            {
                if (!timerExists(timerName)) return;
                _timerCollection[timerName].Reset();
                _timerCollection[timerName].Start();
            }

            public static void Recycle(string timerName,long elapsedMilliseconds)
            {
                if (_timerCollection[timerName].ElapsedMilliseconds < elapsedMilliseconds) return;

                _timerCollection[timerName].Reset();
                _timerCollection[timerName].Start();
            }

            public static bool Exists(string timerName)
            {
                return _timerCollection.ContainsKey(timerName);
            }

            private static bool timerExists(string timerName)
            {
                if (!_timerCollection.ContainsKey(timerName))
                {
                    Logging.WriteDebug("**********************************************************************");
                    Logging.WriteDebug(" ");
                    Logging.WriteDebug(String.Format(" Timer '{0}' does not exist", timerName));
                    Logging.WriteDebug(" ");
                    Logging.WriteDebug("**********************************************************************");
                    return false;
                }

                return true;
            }





        }
    }
}
