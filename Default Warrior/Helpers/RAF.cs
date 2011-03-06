using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using Hera.SpellsMan;
using Styx;
using Styx.Helpers;
using Styx.Logic;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Hera.Helpers
{
    public static class RAF
    {
        private static LocalPlayer Me { get { return ObjectManager.Me; } }
        private static WoWUnit CT { get { return Me.CurrentTarget; } }


        public static int CountOfAddsAttackingRAFLeader
        {
            get
            {
                if (RaFHelper.Leader == null) return 0;

                List<WoWUnit> hlist =
                    (from o in ObjectManager.ObjectList
                     where o is WoWUnit
                     let p = o.ToUnit()
                     where p.Distance2D < 80
                           && !p.Dead
                           && p.Combat
                           && (p.CurrentTargetGuid == RaFHelper.Leader.Guid)
                           && p.IsHostile
                           && p.Attackable
                     select p).ToList();

                return hlist.Count;
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
                     where p.Distance2D < 80
                           && !p.Dead
                           && p.Combat
                           && (p.IsTargetingMyPartyMember || p.IsTargetingMeOrPet)
                           && p.IsHostile
                           && p.Attackable
                     select p).ToList();

                int countNearTank = 0;
                foreach (WoWUnit u in hlist)
                {
                    if (u.HealthPercent < 15) continue;                                         // If the mobs health is below X% don't include it
                    if (RaFHelper.Leader.Location.Distance(u.Location) < 18) countNearTank++;
                }

                // If you have X+ adds then AoE
                return countNearTank > 2;
            }
        }

        public static WoWUnit PlayerNeedsHealing(double minimumHealth)
        {
            // If you're not in a party then just leave
            if (!Me.IsInParty) return null;

            // MyGroup is populated with raid members or party members, whichever you are in
            List<WoWPlayer> myGroup = Me.IsInRaid ? Me.RaidMembers : Me.PartyMembers;

            // Enumerate all players in myGroup and find the person with the lowest health %
            List<WoWPlayer> playersToHeal = (from o in myGroup
                                             let p = o.ToPlayer()
                                             where p.Distance < 60
                                                   && !p.Dead
                                                   && !p.IsGhost
                                                   //&& p.InLineOfSight
                                                   && p.HealthPercent < minimumHealth
                                             orderby p.HealthPercent ascending
                                             select p).ToList();

            // If playersToHeal is more than 0 then we have someone to heal
            // So return the first person in the list, they will be the most in need
            return playersToHeal.Count > 0 ? playersToHeal[0] : null;
        }

        public static WoWUnit PlayerNeedsHealingExcludeTank(double minimumHealth)
        {
            // If you're not in a party then just leave
            if (!Me.IsInParty) return null;

            // MyGroup is populated with raid members or party members, whichever you are in
            List<WoWPlayer> myGroup = Me.IsInRaid ? Me.RaidMembers : Me.PartyMembers;

            // Enumerate all players in myGroup and find the person with the lowest health %
            List<WoWPlayer> playersToHeal = (from o in myGroup
                                             let p = o.ToPlayer()
                                             where p.Distance < 60
                                                   && !p.Dead
                                                   && !p.IsGhost
                                                   && p.Guid != RaFHelper.Leader.Guid
                                                 //&& p.InLineOfSight
                                                   && p.HealthPercent < minimumHealth
                                             orderby p.HealthPercent ascending
                                             select p).ToList();

            // If playersToHeal is more than 0 then we have someone to heal
            // So return the first person in the list, they will be the most in need
            return playersToHeal.Count > 0 ? playersToHeal[0] : null;
        }

        public static int CountOfPlayersInNeed(double minimumHealth)
        {
            // If you're not in a party then just leave
            if (!Me.IsInParty) return 0;

            // MyGroup is populated with raid members or party members, whichever you are in
            List<WoWPlayer> myGroup = Me.IsInRaid ? Me.RaidMembers : Me.PartyMembers;

            List<WoWPlayer> playersToHeal = (from o in myGroup
                                             let p = o.ToPlayer()
                                             where p.Distance < 60
                                                   && !p.Dead
                                                   && !p.IsGhost
                                                   //&& p.InLineOfSight
                                                   && p.HealthPercent < minimumHealth
                                             //orderby p.HealthPercent ascending
                                             select p).ToList();

            return playersToHeal.Count;
        }

        public static WoWUnit PlayerNeedsBuffing(string buffName)
        {
            // If you're not in a party then just leave
            if (!Me.IsInParty) return null;

            // MyGroup is populated with raid members or party members, whichever you are in
            List<WoWPlayer> myGroup = Me.IsInRaid ? Me.RaidMembers : Me.PartyMembers;

            List<WoWPlayer> plist = (from o in myGroup
                                     let p = o.ToPlayer()
                                     where p.Distance < 30
                                           && !p.Dead
                                           && !p.IsGhost
                                           && p.InLineOfSight
                                           && !p.ActiveAuras.ContainsKey(buffName)
                                     select p).ToList();

            return plist.Count > 0 ? plist[0] : null;
        }

        public static WoWUnit RAFHealer
        {
            get
            {
                if (!Me.IsInParty) return null;

                int i = 1;
                foreach (WoWPlayer player in Me.PartyMembers)
                {
                    string memberRole = Lua.GetReturnVal<string>(string.Format(@"return UnitGroupRolesAssigned(""party{0}"")", i), 0);
                    
                    if (memberRole == "HEALER")
                    {
                        return player;
                    }
                    i++;
                }

                return null;
            }
        }

        public static WoWUnit RAFTank
        {
            get
            {
                if (!Me.IsInParty) return null;

                int i = 1;
                foreach (WoWPlayer p in Me.PartyMembers)
                {
                    string partyRole = Lua.GetReturnVal<string>(string.Format(@"return UnitGroupRolesAssigned(""party{0}"")", i), 0);

                    if (partyRole == "TANK")
                    {
                        return p;
                    }
                    i++;
                }

                return null;
            }
        }

    }
}
