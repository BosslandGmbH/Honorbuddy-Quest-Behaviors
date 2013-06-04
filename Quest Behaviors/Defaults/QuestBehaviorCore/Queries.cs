// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public abstract partial class QuestBehaviorBase
    {
        // Do NOT make this static!
        // We need a 'fresh list' each time the QB is started; otherwise, very bad things happen.
        private readonly LocalBlacklist _interactBlacklist = new LocalBlacklist();
        private readonly LocalBlacklist _pullingBlacklist = new LocalBlacklist();

        // 30May2013-03:56UTC chinajade
        public static void BlacklistForCombat(WoWObject wowObject, TimeSpan duration)
        {
            if (wowObject != null)
            {
                Blacklist.Add(wowObject.Guid, BlacklistFlags.Combat, duration);
            }
        }


        // 11Apr2013-03:56UTC chinajade
        public void BlacklistForInteracting(WoWObject wowObject, TimeSpan duration)
        {
            if (wowObject != null)
            { 
                _interactBlacklist.Add(wowObject.Guid, duration); 
            }
        }


        public void BlacklistForPulling(WoWObject wowObject, TimeSpan duration)
        {
            if (wowObject != null)
            {
                _pullingBlacklist.Add(wowObject.Guid, duration);
            }
        }
        
        
        // 24Feb2013-08:11UTC chinajade
        public static IEnumerable<WoWObject> FindObjectsFromIds(IEnumerable<int> objectIds, ProvideBoolDelegate extraQualifiers = null)
        {
            ContractRequires(objectIds != null, context => "objectIds argument may not be null");
            extraQualifiers = extraQualifiers ?? (wowObjectContext => true);

            return
                from wowObject in ObjectManager.GetObjectsOfType<WoWObject>(true, false)
                where
                    IsViable(wowObject)
                    && objectIds.Contains((int)wowObject.Entry)
                    && extraQualifiers(wowObject)
                select wowObject;
        }


        // 25Feb2013-12:50UTC chinajade
        public static IEnumerable<WoWPlayer> FindPlayersNearby(WoWPoint location, double radius, ProvideBoolDelegate extraQualifiers = null)
        {
            extraQualifiers = extraQualifiers ?? (wowPlayerContext => true);

            return
                from wowPlayer in ObjectManager.GetObjectsOfType<WoWPlayer>(true, false)
                where
                    IsViable(wowPlayer)
                    && wowPlayer.IsAlive
                    && (wowPlayer.Location.Distance(location) < radius)
                    && extraQualifiers(wowPlayer)
                select wowPlayer;
        }


        // 24Feb2013-08:11UTC chinajade
        public static IEnumerable<WoWUnit> FindUnitsFromIds(IEnumerable<int> unitIds, ProvideBoolDelegate extraQualifiers = null)
        {
            ContractRequires(unitIds != null, context => "unitIds argument may not be null");
            extraQualifiers = extraQualifiers ?? (wowUnitContext => true);

            return
                from wowUnit in ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                where
                    IsViable(wowUnit)
                    && unitIds.Contains((int)wowUnit.Entry)
                    && extraQualifiers(wowUnit)
                select wowUnit;
        }


        // returns true, if any member of GROUP (or their pets) is in combat
        // 24Feb2013-08:11UTC chinajade
        public static bool IsAnyInCombat(IEnumerable<WoWUnit> group)
        {
            return group.Any(u => u.Combat || (u.GotAlivePet && u.Pet.Combat));
        }


        // 11Apr2013-04:41UTC chinajade
        public static bool IsBlacklistedForCombat(WoWObject wowObject)
        {
            return (wowObject != null)
                ? Blacklist.Contains(wowObject.Guid, BlacklistFlags.Combat)
                : false;
        }


        // 11Apr2013-04:41UTC chinajade
        public bool IsBlacklistedForInteraction(WoWObject wowObject)
        {
            return (wowObject != null)
                ? _interactBlacklist.Contains(wowObject.Guid)
                : false;
        }


        // 4Jun2013-04:41UTC chinajade
        public bool IsBlacklistedForPulling(WoWObject wowObject)
        {
            return (wowObject != null)
                ? _pullingBlacklist.Contains(wowObject.Guid)
                : false;
        }
        
        
        public static bool IsExceptionReportingNeeded(Exception except)
        {
            var typeOfException = except.GetType();

            return !(typeOfException == typeof(ThreadAbortException));
        }

        
        // 11Apr2013-04:41UTC chinajade
        public static bool IsInCompetition(WoWObject wowObject, double nonCompeteDistance)
        {
            ProvideBoolDelegate excludeGroupMembers = (potentialGroupMember =>
            {
                var asWoWPlayer = potentialGroupMember as WoWPlayer;

                return (asWoWPlayer != null) && !asWoWPlayer.IsInMyParty;
            });

            // Is WoWUnit claimed by another player?
            WoWUnit wowUnit = wowObject.ToUnit();
            bool isTagged = ((wowUnit != null) && !wowUnit.IsUntagged());

            return !IsSharedWorldResource(wowObject)
                    && (FindPlayersNearby(wowObject.Location, nonCompeteDistance, excludeGroupMembers).Any()
                        || isTagged);
        }

        
        // 23Mar2013-05:38UTC chinajade
        public static bool IsInLineOfSight(WoWObject wowObject)
        {
            WoWUnit wowUnit = wowObject.ToUnit();

            return (wowUnit == null)
                ? wowObject.InLineOfSight
                // NB: For WoWUnit, we do two checks.  This keeps us out of trouble when the
                // mobs are up a stairway and we're looking at them through a guardrail and
                // other boundary conditions.
                : (wowUnit.InLineOfSight && wowUnit.InLineOfSpellSight);
        }


        // 16Apr2013-10:11UTC chinajade
        public static bool IsSharedWorldResource(WoWObject wowObject)
        {
            bool isSharedResource = false;
            var wowGameObject = wowObject.ToGameObject();
            var wowUnit = wowObject.ToUnit();

            isSharedResource |= (wowGameObject != null) && _sharedGameObjectTypes.Contains(wowGameObject.SubType);
            isSharedResource |= (wowUnit != null) && wowUnit.TappedByAllThreatLists;
            isSharedResource |= (wowUnit != null) && !wowUnit.IsHostile
                                && (wowUnit.IsAnyTrainer
                                    || wowUnit.IsAnyVendor
                                    || wowUnit.IsAuctioneer
                                    || wowUnit.IsBanker
                                    || wowUnit.IsFlightMaster
                                    || wowUnit.IsGuard
                                    || wowUnit.IsGuildBanker
                                    || wowUnit.IsInnkeeper
                                    || wowUnit.IsQuestGiver
                                    || wowUnit.IsStableMaster
                                   );
            
            return isSharedResource;
        }
        private static readonly WoWGameObjectType[] _sharedGameObjectTypes =
        {
            WoWGameObjectType.Binder,           // sets hearthstone
            // NOT: WoWGameObjectType.Door,
            // Although it would be nice to include doors, NPCs locked in cages that require a key to open
            // are "door" subtypes.  If we walked up and took one of these resources while it was in competition
            // that would not be good.  Thus, we exclude it from our list of 'shared resources'.
            WoWGameObjectType.GuildBank,
            WoWGameObjectType.Mailbox,
            WoWGameObjectType.MeetingStone,
            WoWGameObjectType.QuestGiver,
            WoWGameObjectType.SpellCaster,      // portals
            WoWGameObjectType.Transport
        };


        // 30May2013-08:11UTC chinajade
        public static bool IsStateMatch_AurasWanted(WoWObject wowObject, IEnumerable<int> auraIdsWanted)
        {
            ContractRequires(wowObject != null, context => "wowObject != null");
            ContractRequires(auraIdsWanted != null, context => "auraIdsWanted != null");

            var wowUnit = wowObject as WoWUnit;

            return
                (wowUnit == null)           // Unit has no auras to check
                || !auraIdsWanted.Any()     // No aura qualifiers provided
                || wowUnit.GetAllAuras().Any(a => auraIdsWanted.Contains(a.SpellId));
        }


        // 30May2013-08:11UTC chinajade
        public static bool IsStateMatch_AurasMissing(WoWObject wowObject, IEnumerable<int> auraIdsMissing)
        {
            ContractRequires(wowObject != null, context => "wowObject != null");
            ContractRequires(auraIdsMissing != null, context => "auraIdsMissing != null");

            var wowUnit = wowObject as WoWUnit;

            return
                (wowUnit == null)           // Unit has no auras to check
                || !auraIdsMissing.Any()    // No aura qualifiers provided
                || !wowUnit.GetAllAuras().Any(a => auraIdsMissing.Contains(a.SpellId));
        }


        // 30May2013-08:11UTC chinajade
        public static bool IsStateMatch_MeshNavigable(WoWObject wowObject, MovementByType movementBy)
        {
            ContractRequires(wowObject != null, context => "wowObject != null");

            return
                (movementBy != MovementByType.NavigatorOnly)
                || ((movementBy == MovementByType.NavigatorOnly)
                    && !Navigator.CanNavigateFully(Me.Location, wowObject.Location));
        }


        // 30May2013-08:11UTC chinajade
        public static bool IsStateMatch_IgnoreMobsInBlackspots(WoWObject wowObject, bool ignoreMobsInBlackspots)
        {
            return
                ignoreMobsInBlackspots
                || !Targeting.IsTooNearBlackspot(ProfileManager.CurrentProfile.Blackspots, wowObject.Location);
        }


        // 28Apr2013-03:38UTC chinajade
        public static bool IsStateMatch_MobState(WoWObject wowObject, MobStateType requestedMobState, double mobHpPercentLeft)
        {
            if (requestedMobState == MobStateType.DontCare)
            {
                return true;
            }

            WoWUnit wowUnit = wowObject.ToUnit();
            if (wowUnit != null)
            {
                return
                    ((requestedMobState == MobStateType.Alive) && wowUnit.IsAlive)
                    || ((requestedMobState == MobStateType.AliveNotInCombat) && wowUnit.IsAlive && !wowUnit.Combat)
                    || ((requestedMobState == MobStateType.Dead) && wowUnit.IsDead && wowUnit.IsUntagged())
                    || ((requestedMobState == MobStateType.BelowHp) && wowUnit.IsAlive && (wowUnit.HealthPercent < mobHpPercentLeft));
            }

            return false;
        }
        
        
        // 24Feb2013-08:11UTC chinajade
        public static bool IsViable(WoWObject wowObject)
        {
            return
                (wowObject != null)
                && wowObject.IsValid;
        }
        
        
        // 24Feb2013-08:11UTC chinajade
        public static bool IsViableForFighting(WoWUnit wowUnit)
        {
            return
                IsViable(wowUnit)
                && wowUnit.IsAlive
                && !wowUnit.IsFriendly
                && wowUnit.Attackable
                && !IsBlacklistedForCombat(wowUnit);
        }


        // 24Feb2013-08:11UTC chinajade
        public bool IsViableForInteracting(WoWObject wowObject)
        {
            if (wowObject == null)
                { return false; }

            bool isViableForInteracting =
                IsViable(wowObject)
                && !IsBlacklistedForInteraction(wowObject)
                && (IgnoreMobsInBlackspots || !Targeting.IsTooNearBlackspot(ProfileManager.CurrentProfile.Blackspots, wowObject.Location))
                && !IsInCompetition(wowObject, NonCompeteDistance);

            return isViableForInteracting;
        }


        // 24Feb2013-08:11UTC chinajade
        public bool IsViableForPulling(WoWUnit wowUnit)
        {
            return IsViableForFighting(wowUnit)
                    && !IsBlacklistedForPulling(wowUnit)
                    && !IsInCompetition(wowUnit, NonCompeteDistance);
        }
    }
}