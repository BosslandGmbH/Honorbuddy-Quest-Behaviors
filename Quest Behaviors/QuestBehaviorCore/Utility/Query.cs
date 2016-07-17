﻿// Originally contributed by Chinajade.
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
using System.Globalization;
using System.Linq;
using System.Threading;
using Honorbuddy.QuestBehaviorCore.XmlElements;

using JetBrains.Annotations;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Bars;
using Styx.CommonBot.Frames;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public static class Query
    {
        /// <summary>Determines if toon is within a tolerable proximity of <paramref name="location" />.</summary>
        /// <param name="location">The location.</param>
        /// <param name="tolerance">The tolerance. Default: <see cref="Navigator.PathPrecision"/></param>
        /// <returns></returns>
        public static bool AtLocation(WoWPoint location, float? tolerance)
        {
            return AtLocation((WoWMovement.ActiveMover ?? StyxWoW.Me).Location, location, tolerance);
        }

        /// <summary>Determines whether <paramref name="myLoc" /> is within a tolerable proximity of <paramref name="location" />.</summary>
        /// <param name="myLoc">My loc.</param>
        /// <param name="location">The location.</param>
        /// <param name="tolerance">The tolerance. Default: <see cref="Navigator.PathPrecision"/></param>
        /// <returns></returns>
        public static bool AtLocation(WoWPoint myLoc, WoWPoint location, float? tolerance)
        {
            var tol = tolerance ?? Navigator.PathPrecision;
            // We are checking if point is in an upright cylinder whose center is positioned at 'location', 
            // radius set to 'tolerance' and height set to max(4.5, 'tolerance') x 2.
            // This is the most suitable method when using mesh navigation because the mesh is often above or below the 
            // actual ingame terrain so there's a need to clamp the tolerance along the Z axis to an amount that 
            // is greater then the maximum terrain/mesh Z coord difference
            return myLoc.Distance2DSqr(location) <= tol * tol && Math.Abs(myLoc.Z - location.Z) <= Math.Max(4.5f, tol);
        }

        public static IEnumerable<Blackspot> FindCoveringBlackspots(WoWPoint location)
        {
            return
                from blackspot in BlackspotManager.GetAllCurrentBlackspots()
                where
                    (blackspot.Location.Distance2D(location) <= blackspot.Radius)
                    && (Math.Abs(blackspot.Location.Z - location.Z) <= blackspot.Height)
                select blackspot;
        }


        public static IEnumerable<WoWObject> FindMobsAndFactions(
            IEnumerable<int> mobIds,
            bool includeSelf = false,
            IEnumerable<int> factionIds = null,
            ProvideBoolDelegate extraQualifiers = null)
        {
            return
                from wowObject in ObjectManager.GetObjectsOfType<WoWObject>(true, true)
                where IsMobOrFaction(wowObject, mobIds, includeSelf, factionIds, extraQualifiers)
                select wowObject;
        }

        public static IEnumerable<WoWUnit> FindMobsAttackingMe()
        {
            return
                from o in ObjectManager.ObjectList
                let unit = o as WoWUnit
                where
                    IsViable(unit)
                    && unit.Aggro
                select unit;
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
                     where
                        IsViableForPulling(wowUnit, ignoreMobsInBlackspots, nonCompeteDistance)
                        && wowUnit.IsHostile
                        // exclude opposing faction: both players and their pets show up as "PlayerControlled"
                        && !wowUnit.PlayerControlled
                        // exclude any units that are candidates for interacting
                        && !excludedUnitIds.Contains((int)wowUnit.Entry)
                        // Do not pull mobs on the AvoidMobs list
                        && !ProfileManager.CurrentOuterProfile.AvoidMobs.Contains(wowUnit.Entry)
                     let collectionDistance = destination.CollectionDistance(wowUnit.Location)
                     where collectionDistance <= (wowUnit.MyAggroRange + extraRangePadding)
                     orderby wowUnit.DistanceSqr
                     select wowUnit)
                    .ToList();
            }
        }


        /// <summary>
        /// <para>Locates the mount identified by MOUNTNAMEORID in the provided MOUNTLIST.</para>
        /// <para>MOUNTLIST is usually provided as Mount.Mounts, Mount.FlyingMounts, etc.</para>
        /// <para>The MOUNTNAMEORID can be the mount 'name' or SpellId.  If a name, the name
        /// is converted to lowercase to make the comparison.</para>
        /// </summary>
        /// <param name="mountList"></param>
        /// <param name="mountNameOrId"></param>
        /// <returns>null, if the mount could not be located in the MOUNTLIST.</returns>
        /// <remarks>5-Jan-2014 00:30 UTC chinajade</remarks>
        public static Mount.MountWrapper FindMount(IEnumerable<Mount.MountWrapper> mountList, string mountNameOrId)
        {
            if (string.IsNullOrEmpty(mountNameOrId))
            { return null; }

            mountNameOrId = mountNameOrId.Trim().ToLower();

            int mountId;
            IEnumerable<Mount.MountWrapper> query;

            if (Int32.TryParse(mountNameOrId, NumberStyles.Integer, CultureInfo.InvariantCulture, out mountId) && (mountId != 0))
            {
                query =
                    from mount in mountList
                    where
                        mount.CreatureSpellId == mountId
                    select mount;
            }

            else if (!string.IsNullOrEmpty(mountNameOrId))
            {
                query =
                    from mount in mountList
                    where
                        mount.CreatureSpell != null
                        && mount.CreatureSpell.Name.ToLower() == mountNameOrId
                    select mount;
            }

            else
            {
                return null;
            }

            return query.FirstOrDefault();
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

        /// <summary>Finds unocupied vehicles.</summary>
        /// <param name="ids">The vehicle ids.</param>
        /// <param name="extraQualifiers">The extra qualifiers.</param>
        /// <returns></returns>
        public static IEnumerable<WoWUnit> FindUnoccupiedVehicles(IEnumerable<int> ids, Func<WoWUnit, bool> extraQualifiers = null)
        {
            return from wowUnit in ObjectManager.GetObjectsOfType<WoWUnit>(false, false)
                   where ids.Contains((int)wowUnit.Entry)
                       && (extraQualifiers == null || extraQualifiers(wowUnit))
                       && wowUnit.CanInteractNow
                   orderby wowUnit.Location.CollectionDistance()
                   select wowUnit;
        }

        // 25Aug2013 chinajade
        public static void InCompetitionReset()
        {
            s_inCompetitionTimers.Clear();
        }


        // 25Aug2013 chinajade
        public static TimeSpan InCompetitionTimeRemaining(WoWObject wowObject)
        {
            DateTime waitStart;
            if (s_inCompetitionTimers.TryGetValue(wowObject.Guid, out waitStart))
            {
                var now = DateTime.Now;

                if (now <= (waitStart + s_inCompetitionMaxWaitTime))
                { return waitStart + s_inCompetitionMaxWaitTime - now; }
            }

            return TimeSpan.Zero;
        }


        /// <summary>
        /// This method returns whether ACHIEVEMENTID is personal compelete for the toon.
        /// This means the achievement is complete (account-wide), and the toon personally has the achievement.
        /// </summary>
        /// <param name="achievementId"></param>
        /// <returns></returns>
        public static bool IsAchievementPersonallyCompleted(int achievementId)
        {
            var luaCmd = string.Format("return GetAchievementInfo({0})", achievementId);

            var results = Lua.GetReturnValues(luaCmd);
            try
            {
                var isAchievementComplete = (results.Count < 4) ? false : Lua.ParseLuaValue<bool>(results[3]);
                var wasEarnedByMe = (results.Count < 13) ? false : Lua.ParseLuaValue<bool>(results[12]);

                return isAchievementComplete && wasEarnedByMe;
            }
            catch (Exception ex)
            {
                return false;
            }
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


        public static bool IsExceptionReportingNeeded(Exception except)
        {
            var typeOfException = except.GetType();

            return !(typeOfException == typeof(ThreadAbortException));
        }


        // 11Apr2013-04:41UTC chinajade
        public static bool IsInCompetition(WoWObject wowObject, double nonCompeteDistance)
        {
            if (!IsViable(wowObject))
            { return false; }

            // Shared world resources are never in competition...
            if (IsSharedWorldResource(wowObject))
            { return false; }

            // If unit is tagged by someone else and in combat, it is in competition...
            // N.B. There are some cases where NPCs are shown as tagged by - 
            // someone else yet nobody is actively engaged with said unit. 
            // If unit is not in combat with anyone and it is tagged and nobody else is around it can not be in competition. 
            // On the other hand if we choose to ignore the tagged unit and it's the only NPC that exists in the world then bot will be stuck
            var wowUnit = wowObject as WoWUnit;
            var isTaggedByOther = ((wowUnit != null) && !wowUnit.IsUntagged() && wowUnit.Combat);
            if (isTaggedByOther)
            { return true; }


            ProvideBoolDelegate excludeGroupMembers = (potentialGroupMember =>
            {
                var asWoWPlayer = potentialGroupMember as WoWPlayer;

                return (asWoWPlayer != null) && !asWoWPlayer.IsInMyParty;
            });

            var isPlayersNearby = FindPlayersNearby(wowObject.Location, nonCompeteDistance, excludeGroupMembers).Any();
            var isCompetitionTimerRunning = s_inCompetitionTimers.ContainsKey(wowObject.Guid);

            // If players are clear, and competition timer is running...
            // We no longer need the competition timer.
            if (!isPlayersNearby && isCompetitionTimerRunning)
            { s_inCompetitionTimers.Remove(wowObject.Guid); }

            // If players are nearby, and we haven't established competition timer...
            // We need to record time at which we start the wait.
            if (isPlayersNearby && !isCompetitionTimerRunning)
            {
                // Add new entry...
                s_inCompetitionTimers.Add(wowObject.Guid, DateTime.Now);

                // Time to sweep away old 'in competition' entries?
                if ((s_inCompetitionSweepTimer == null) || s_inCompetitionSweepTimer.IsFinished)
                {
                    // Remove expired 'in competition' entries...
                    var now = DateTime.Now;
                    var keysToRemove =
                       (from kvp in s_inCompetitionTimers
                        where now > (kvp.Value + s_inCompetitionSweepTime)
                        select kvp.Key)
                        .ToArray();

                    foreach (var key in keysToRemove)
                    { s_inCompetitionTimers.Remove(key); }

                    // Reset sweep timer (creating it, if necessary)...
                    if (s_inCompetitionSweepTimer == null)
                    { s_inCompetitionSweepTimer = new WaitTimer(s_inCompetitionSweepTime); }

                    s_inCompetitionSweepTimer.Reset();
                }
            }


            // If we've been waiting on object too long, it is no longer 'in competition'...
            // NB: Since group membership affects 'nearby players', we must make this test
            // after the competition timers have been updated due to nearby players.
            DateTime waitStart;
            if (s_inCompetitionTimers.TryGetValue(wowObject.Guid, out waitStart))
            {
                if (DateTime.Now > (waitStart + s_inCompetitionMaxWaitTime))
                { return false; }

                // Fall through, if we haven't been waiting too long...
            }

            return (isPlayersNearby);
        }
        private static readonly Dictionary<WoWGuid, DateTime> s_inCompetitionTimers = new Dictionary<WoWGuid, DateTime>();
        private static readonly TimeSpan s_inCompetitionMaxWaitTime = TimeSpan.FromSeconds(90);
        private static readonly TimeSpan s_inCompetitionSweepTime = TimeSpan.FromSeconds(10/*mins*/ * 60 /*secs*/);
        private static WaitTimer s_inCompetitionSweepTimer = null;
        private static PerFrameCachedValue<bool> s_isInVehicle;


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


        // 25Nov2013-09:30UTC chinajade
        public static bool IsInVehicle()
        {
            return s_isInVehicle ?? (s_isInVehicle =
                new PerFrameCachedValue<bool>(() => StyxWoW.Me.InVehicle
                    || Lua.GetReturnVal<bool>("return IsPossessBarVisible()", 0)));
        }


        // 25Nov2013 HighVoltz
        public static bool IsMeleeSpec(WoWSpec spec)
        {
            return !IsRangeSpec(spec);
        }

        public static bool IsMobOrFaction(
            WoWObject wowObject,
            IEnumerable<int> mobIds,
            bool includeSelf = false,
            IEnumerable<int> factionIds = null,
            ProvideBoolDelegate extraQualifiers = null)
        {
            if (!IsViable(wowObject))
                return false;

            var wowUnit = wowObject as WoWUnit;
            var isMob = mobIds != null && mobIds.Contains((int)wowObject.Entry);
            var isMe = includeSelf && wowObject.IsMe;
            var isFaction = wowUnit != null && factionIds != null && factionIds.Contains((int)wowUnit.FactionId);

            return (isMob || isMe || isFaction) && (extraQualifiers == null || extraQualifiers(wowObject));
        }


        public static bool IsMountAquatic(string mountNameOrId)
        {
            return FindMount(Mount.UnderwaterMounts, mountNameOrId) != null;
        }


        public static bool IsMountFlying(string mountNameOrId)
        {
            return FindMount(Mount.FlyingMounts, mountNameOrId) != null;
        }


        public static bool IsMountGround(string mountNameOrId)
        {
            return FindMount(Mount.GroundMounts, mountNameOrId) != null;
        }


        public static bool IsMountKnown(string mountNameOrId)
        {
            return FindMount(Mount.Mounts, mountNameOrId) != null;
        }


        /// <summary>
        /// Returns 'true' for items that start quests, or are quest objectives.
        /// </summary>
        /// <param name="wowItem"></param>
        /// <returns></returns>
        // 3Nov2013 chinajade
        public static bool IsQuestItem(WoWItem wowItem)
        {
            // If not valid, then not a quest item...
            if ((wowItem == null) || !wowItem.IsValid)
            { return false; }

            ItemInfo itemInfo = wowItem.ItemInfo;

            if (itemInfo == null)
                return false;

            return
                (itemInfo.BeginQuestId != 0)                    // Begins a quest?
                || (itemInfo.Bond == WoWItemBondType.Quest);    // Objective of quest?
        }


        // 25Nov2013 HighVoltz
        public static bool IsRangeSpec(WoWSpec spec)
        {
            switch (spec)
            {
                case WoWSpec.HunterBeastMastery:
                case WoWSpec.HunterMarksmanship:
                case WoWSpec.HunterSurvival:

                case WoWSpec.MageArcane:
                case WoWSpec.MageFire:
                case WoWSpec.MageFrost:

                case WoWSpec.PriestDiscipline:
                case WoWSpec.PriestHoly:
                case WoWSpec.PriestShadow:

                case WoWSpec.WarlockAffliction:
                case WoWSpec.WarlockDemonology:
                case WoWSpec.WarlockDestruction:

                case WoWSpec.ShamanElemental:
                case WoWSpec.ShamanRestoration:

                case WoWSpec.PaladinHoly:

                case WoWSpec.DruidBalance:
                case WoWSpec.DruidRestoration:

                case WoWSpec.MonkMistweaver:

                    return true;
                default:
                    return false;
            }
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


        //  2Sep2013 chinajade
        public static bool IsPoiIdle(BotPoi poi)
        {
            if (poi == null)
            { return false; }

            return s_idlePoiTypes.Contains(poi.Type);
        }
        private static readonly PoiType[] s_idlePoiTypes =
        {
            PoiType.None,
            PoiType.Quest,
            PoiType.QuestPickUp,
            PoiType.QuestTurnIn
        };


        public static bool IsPoiMatch(WoWObject wowObject, PoiType poiType, NavType? navType = null)
        {
            if (!IsViable(wowObject))
            { return false; }

            return (BotPoi.Current != null)
                    && (BotPoi.Current.Guid == wowObject.Guid)
                    && (BotPoi.Current.Type == poiType)
                    && ((navType == null) || (BotPoi.Current.NavType == navType));
        }


        // 16Apr2013-10:11UTC chinajade
        public static bool IsSharedWorldResource(WoWObject wowObject)
        {
            bool isSharedResource = false;
            var wowGameObject = wowObject.ToGameObject();
            var wowUnit = wowObject as WoWUnit;

            isSharedResource |= (wowGameObject != null) && s_sharedGameObjectTypes.Contains(wowGameObject.SubType);
            isSharedResource |= (wowUnit != null) && wowUnit.TappedByAllThreatLists;
            isSharedResource |= (wowUnit != null) && !wowUnit.IsHostile
                                && (wowUnit.IsAnyTrainer
                                    || wowUnit.IsAnyVendor
                                    || wowUnit.IsAuctioneer
                                    || wowUnit.IsBanker
                                    || wowUnit.IsFlightMaster
                                    || wowUnit.IsGuildBanker
                                    || wowUnit.IsInnkeeper
                                    || wowUnit.IsQuestGiver
                                    || wowUnit.IsStableMaster
                                    || wowUnit.CanGossip
                                   );

            return isSharedResource;
        }
        private static readonly WoWGameObjectType[] s_sharedGameObjectTypes =
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
        public static bool IsStateMatch_IgnoreMobsInBlackspots(WoWObject wowObject, bool ignoreMobsInBlackspots)
        {
            return !ignoreMobsInBlackspots || !IsTargetInBlackspot(wowObject);
        }


        // 30May2013-08:11UTC chinajade
        public static bool IsStateMatch_MeshNavigable(WoWObject wowObject, MovementByType movementBy)
        {
            Contract.Requires(wowObject != null, context => "wowObject != null");

            return
                (movementBy != MovementByType.NavigatorOnly)
                || Navigator.CanNavigateFully(StyxWoW.Me.Location, wowObject.Location);
        }


        // 28Apr2013-03:38UTC chinajade
        public static bool IsStateMatch_MobState(WoWObject wowObject, MobStateType requestedMobState, double mobHpPercentLeft = 100.0)
        {
            var wowUnit = wowObject as WoWUnit;

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


        // 27Jun2013-08:11UTC chinajade
        public static bool IsStateMatch_NotMoving(WoWUnit wowUnit, bool nonMovingTargetWanted)
        {
            return
                !nonMovingTargetWanted || !wowUnit.IsMoving;
        }


        /// <summary>
        /// <p>Returns 'true' if WOWOBJECT is located in a blackspot; otherwise, 'false'.</p>
        /// 
        /// <p>Not all blackspots are created equal.  For targeting purposes, we only want to consider
        /// blackspots defined by the profile (static), or the QBcore-based behaviors (a subset of dynamic
        /// blackspots).  Other blackspots, such as those dropped by the StuckHandler (also dynamic), should not
        /// be considered for targeting purposes.  If we did, then it would be possible for say InteractWith
        /// to cause Honorbuddy to hang forever, because the StuckHandler dropped a blackspot on a unique
        /// target.</p>
        /// <p>Blackspots dropped by the StuckHandler and other sources should not be considered for
        /// targeting decisions.</p>
        /// </summary>
        /// <param name="wowObject"></param>
        /// <returns></returns>
        public static bool IsTargetInBlackspot(WoWObject wowObject)
        {
            Func<WoWPoint, Blackspot, bool> isInBlackspot =
                (location, blackspot) =>
                {
                    return
                        (blackspot.Location.DistanceSqr(location) <= (blackspot.Radius * blackspot.Radius))
                        && (location.Z >= (blackspot.Location.Z - blackspot.Height))
                        && (location.Z <= (blackspot.Location.Z + blackspot.Height));
                };
            var wowObjectLocation = wowObject.Location;

            return
                // Consider profile-defined (static) blackspots...
                BlackspotManager.GetAllCurrentBlackspots(BlackspotQueryFlags.Static)
                                .Any(b => isInBlackspot(wowObjectLocation, b))
                ||
                // Consider QBcore-defined (dynamic) blackspots...
                BlackspotManager.GetAllCurrentBlackspots(BlackspotQueryFlags.Dynamic)
                                .Any(b => BlackspotType.IsQbcoreDefined(b) && isInBlackspot(wowObjectLocation, b));
        }


        // 30Sep2013 chinajade
        public static bool IsVehicleActionBarShowing()
        {
            return ActionBar.Active.Type == ActionBarType.Vehicle;
        }


        // 24Feb2013-08:11UTC chinajade
        [ContractAnnotation("null=>false")]
        public static bool IsViable(WoWObject wowObject)
        {
            return
                (wowObject != null)
                && wowObject.IsValid;
        }


        // 24Feb2013-08:11UTC chinajade
        [ContractAnnotation("null=>false")]
        public static bool IsViableForFighting(WoWUnit wowUnit)
        {
            return
                IsViable(wowUnit)
                && wowUnit.IsAlive
                && !wowUnit.IsFriendly
                && wowUnit.Attackable
                && wowUnit.CanSelect
                && !wowUnit.IsBlacklistedForCombat();
        }


        // 24Feb2013-08:11UTC chinajade
        [ContractAnnotation("wowObject:null=>false")]
        public static bool IsViableForInteracting(WoWObject wowObject,
                                                 bool ignoreMobsInBlackspots,
                                                 double nonCompeteDistance)
        {
            if (wowObject == null)
            { return false; }

            bool isViableForInteracting =
                IsViable(wowObject)
                && !wowObject.IsBlacklistedForInteraction()
                && IsStateMatch_IgnoreMobsInBlackspots(wowObject, ignoreMobsInBlackspots)
                && !IsInCompetition(wowObject, nonCompeteDistance);

            return isViableForInteracting;
        }


        // 24Feb2013-08:11UTC chinajade
        public static bool IsViableForPulling(WoWUnit wowUnit,
                                              bool ignoreMobsInBlackspots,
                                              double nonCompeteDistance)
        {
            return IsViableForFighting(wowUnit)
                    && !wowUnit.IsBlacklistedForPulling()
                    && IsStateMatch_IgnoreMobsInBlackspots(wowUnit, ignoreMobsInBlackspots)
                    && !IsInCompetition(wowUnit, nonCompeteDistance);
        }
    }
}