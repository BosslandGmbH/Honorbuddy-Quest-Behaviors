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
using Styx.CommonBot.Frames;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public static class Query
    {
        // Do NOT make this static!
        // We need a 'fresh list' each time the QB is started; otherwise, very bad things happen.
        private static LocalBlacklist _interactBlacklist = new LocalBlacklist();


        // 30May2013-03:56UTC chinajade
        public static void BlacklistForCombat(WoWObject wowObject, TimeSpan duration)
        {
            if (wowObject != null)
            {
                Blacklist.Add(wowObject.Guid, BlacklistFlags.Pull | BlacklistFlags.Combat, duration);
            }
        }


        // 11Apr2013-03:56UTC chinajade
        public static void BlacklistForInteracting(WoWObject wowObject, TimeSpan duration)
        {
            if (wowObject != null)
            { 
                _interactBlacklist.Add(wowObject.Guid, duration); 
            }
        }


        public static void BlacklistForPulling(WoWObject wowObject, TimeSpan duration)
        {
            if (wowObject != null)
            {
                Blacklist.Add(wowObject.Guid, BlacklistFlags.Pull, duration);
            }
        }


        public static void BlacklistsReset()
        {
            _interactBlacklist = new LocalBlacklist();
        }


        public static IEnumerable<WoWObject> FindMobsAndFactions(
            IEnumerable<int> mobIds,
            bool includeSelf = false,
            IEnumerable<int> factionIds = null,
            ProvideBoolDelegate extraQualifiers = null)
        {
            mobIds = mobIds ?? Enumerable.Empty<int>();
            factionIds = factionIds ?? Enumerable.Empty<int>();
            extraQualifiers = extraQualifiers ?? (context => true);

            return
                from wowObject in ObjectManager.GetObjectsOfType<WoWObject>(true, true)
                let wowUnit = wowObject as WoWUnit
                where
                    Query.IsViable(wowObject)
                    && (mobIds.Contains((int)wowObject.Entry)
                        || (includeSelf && wowObject.IsMe)
                        || ((wowUnit != null) && factionIds.Contains((int)wowUnit.FactionId)))
                    && extraQualifiers(wowObject)
                select wowObject;
        }


        public static List<WoWUnit> FindMobsTargetingUs(bool ignoreMobsInBlackspots, double nonCompeteDistance)
        {
            using (StyxWoW.Memory.AcquireFrame())
            {
                return
                   (from wowUnit in ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                    where
                        Query.IsViableForPulling(wowUnit, ignoreMobsInBlackspots, nonCompeteDistance)
                        && (wowUnit.IsTargetingMeOrPet
                            || wowUnit.IsTargetingAnyMinion
                            || wowUnit.IsTargetingMyPartyMember)
                        // exclude opposing faction: both players and their pets show up as "PlayerControlled"
                        && !wowUnit.PlayerControlled
                        // Do not pull mobs on the AvoidMobs list
                        && !ProfileManager.CurrentOuterProfile.AvoidMobs.Contains(wowUnit.Entry)
                        orderby wowUnit.Location.CollectionDistance()
                        select wowUnit)
                    .ToList();
            }
        }


        public static List<WoWUnit> FindMobsWithinAggroRange(
            WoWPoint destination,
            bool ignoreMobsInBlackspots,
            double nonCompeteDistance,
            IEnumerable<int> excludedUnitIds = null,
            double extraRangePadding = 0.0)
        {
            excludedUnitIds = excludedUnitIds ?? Enumerable.Empty<int>();

            using (StyxWoW.Memory.AcquireFrame())
            {
                return
                    (from wowUnit in ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                     let collectionDistance = destination.CollectionDistance(wowUnit.Location)
                     where
                        IsViableForPulling(wowUnit, ignoreMobsInBlackspots, nonCompeteDistance)
                        && wowUnit.IsHostile
                        // exclude opposing faction: both players and their pets show up as "PlayerControlled"
                        && !wowUnit.PlayerControlled
                        // exclude any units that are candidates for interacting
                        && !excludedUnitIds.Contains((int)wowUnit.Entry)
                        // Do not pull mobs on the AvoidMobs list
                        && !ProfileManager.CurrentOuterProfile.AvoidMobs.Contains(wowUnit.Entry)
                        && (collectionDistance <= (wowUnit.MyAggroRange + extraRangePadding))
                     orderby wowUnit.DistanceSqr
                     select wowUnit)
                    .ToList();
            }
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


        public static bool IsAnyNpcFrameVisible()
        {
            return
                AuctionFrame.Instance.IsVisible
                || GossipFrame.Instance.IsVisible
                || MailFrame.Instance.IsVisible
                || MerchantFrame.Instance.IsVisible
                || QuestFrame.Instance.IsVisible
                || TaxiFrame.Instance.IsVisible
                || TrainerFrame.Instance.IsVisible;
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
            return (wowObject != null) && Blacklist.Contains(wowObject.Guid, BlacklistFlags.Combat);
        }


        // 11Apr2013-04:41UTC chinajade
        public static bool IsBlacklistedForInteraction(WoWObject wowObject)
        {
            return (wowObject != null) && _interactBlacklist.Contains(wowObject.Guid);
        }


        // 4Jun2013-04:41UTC chinajade
        public static bool IsBlacklistedForPulling(WoWObject wowObject)
        {
            return (wowObject != null) && Blacklist.Contains(wowObject.Guid, BlacklistFlags.Pull);
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
            WoWUnit wowUnit = wowObject as WoWUnit;
            bool isTagged = ((wowUnit != null) && !wowUnit.IsUntagged());

            return !IsSharedWorldResource(wowObject)
                    && (FindPlayersNearby(wowObject.Location, nonCompeteDistance, excludeGroupMembers).Any()
                        || isTagged);
        }

        
        // 23Mar2013-05:38UTC chinajade
        public static bool IsInLineOfSight(WoWObject wowObject)
        {
            WoWUnit wowUnit = wowObject as WoWUnit;

            return (wowUnit == null)
                ? wowObject.InLineOfSight
                // NB: For WoWUnit, we do two checks.  This keeps us out of trouble when the
                // mobs are up a stairway and we're looking at them through a guardrail and
                // other boundary conditions.
                : (wowUnit.InLineOfSight && wowUnit.InLineOfSpellSight);
        }



        public static bool IsMobTargetingUs(WoWUnit wowUnit)
        {
            return
                Query.IsViable(wowUnit)
                && (wowUnit.IsTargetingMeOrPet
                    || wowUnit.IsTargetingAnyMinion
                    || wowUnit.IsTargetingMyPartyMember)
                // exclude opposing faction: both players and their pets show up as "PlayerControlled"
                && !wowUnit.PlayerControlled;
        }


        public static bool IsPoiMatch(WoWObject wowObject, PoiType poiType)
        {
            if (!IsViable(wowObject))
                { return false; }

            return (BotPoi.Current != null)
                    && (BotPoi.Current.Guid == wowObject.Guid)
                    && (BotPoi.Current.Type == poiType);
        }


        // 16Apr2013-10:11UTC chinajade
        public static bool IsSharedWorldResource(WoWObject wowObject)
        {
            bool isSharedResource = false;
            var wowGameObject = wowObject.ToGameObject();
            var wowUnit = wowObject as WoWUnit;

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
            Contract.Requires(wowObject != null, context => "wowObject != null");
            Contract.Requires(auraIdsWanted != null, context => "auraIdsWanted != null");

            var wowUnit = wowObject as WoWUnit;

            return
                (wowUnit == null)           // Unit has no auras to check
                || !auraIdsWanted.Any()     // No aura qualifiers provided
                || wowUnit.GetAllAuras().Any(a => auraIdsWanted.Contains(a.SpellId));
        }


        // 30May2013-08:11UTC chinajade
        public static bool IsStateMatch_AurasMissing(WoWObject wowObject, IEnumerable<int> auraIdsMissing)
        {
            Contract.Requires(wowObject != null, context => "wowObject != null");
            Contract.Requires(auraIdsMissing != null, context => "auraIdsMissing != null");

            var wowUnit = wowObject as WoWUnit;

            return
                (wowUnit == null)           // Unit has no auras to check
                || !auraIdsMissing.Any()    // No aura qualifiers provided
                || !wowUnit.GetAllAuras().Any(a => auraIdsMissing.Contains(a.SpellId));
        }


        // 30May2013-08:11UTC chinajade
        public static bool IsStateMatch_MeshNavigable(WoWObject wowObject, MovementByType movementBy)
        {
            Contract.Requires(wowObject != null, context => "wowObject != null");

            return
                (movementBy != MovementByType.NavigatorOnly)
                || Navigator.CanNavigateFully(StyxWoW.Me.Location, wowObject.Location);
        }


        // 27Jun2013-08:11UTC chinajade
        public static bool IsStateMatch_NotMoving(WoWUnit wowUnit, bool nonMovingTargetWanted)
        {
            return
                !nonMovingTargetWanted || !wowUnit.IsMoving;
        }
        
        
        // 30May2013-08:11UTC chinajade
        public static bool IsStateMatch_IgnoreMobsInBlackspots(WoWObject wowObject, bool ignoreMobsInBlackspots)
        {
            return
                ignoreMobsInBlackspots
                || !Targeting.IsTooNearBlackspot(ProfileManager.CurrentProfile.Blackspots, wowObject.Location);
        }


        // 28Apr2013-03:38UTC chinajade
        public static bool IsStateMatch_MobState(WoWObject wowObject, MobStateType requestedMobState, double mobHpPercentLeft = 100.0)
        {
            WoWUnit wowUnit = wowObject as WoWUnit;

            if (wowUnit != null)
            {
                bool isMobAlive = wowUnit.IsAlive;

                return
                    (wowUnit.HealthPercent <= mobHpPercentLeft)
                    && (requestedMobState == MobStateType.DontCare)
                        || ((requestedMobState == MobStateType.Alive) && isMobAlive)
                        || ((requestedMobState == MobStateType.AliveNotInCombat) && isMobAlive && !wowUnit.Combat)
                        || ((requestedMobState == MobStateType.BelowHp) && isMobAlive)
                        || ((requestedMobState == MobStateType.Dead) && wowUnit.IsDead && wowUnit.IsUntagged());
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
        public static bool IsViableForInteracting(WoWObject wowObject,
                                                 bool ignoreMobsInBlackspots,
                                                 double nonCompeteDistance)
        {
            if (wowObject == null)
                { return false; }

            bool isViableForInteracting =
                IsViable(wowObject)
                && !IsBlacklistedForInteraction(wowObject)
                && (ignoreMobsInBlackspots || !Targeting.IsTooNearBlackspot(ProfileManager.CurrentProfile.Blackspots, wowObject.Location))
                && !IsInCompetition(wowObject, nonCompeteDistance);

            return isViableForInteracting;
        }


        // 24Feb2013-08:11UTC chinajade
        public static bool IsViableForPulling(WoWUnit wowUnit,
                                              bool ignoreMobsInBlackspots,
                                              double nonCompeteDistance)
        {
            return IsViableForFighting(wowUnit)
                    && !IsBlacklistedForPulling(wowUnit)
                    && (ignoreMobsInBlackspots || !Targeting.IsTooNearBlackspot(ProfileManager.CurrentProfile.Blackspots, wowUnit.Location))
                    && !IsInCompetition(wowUnit, nonCompeteDistance);
        }
    }
}