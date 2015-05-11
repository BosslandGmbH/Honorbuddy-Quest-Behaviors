// Behavior originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Summary and Documentation
// QUICK DOX:
// This behavior pursues the identified Candy Bucket to complete "Tricks or Treats of..." achievement.
// This behavior will:
//  * Land at one of the identified "landing area"
//  * Dismount and walk to the identified Candy Bucket
//  * Complete the quest offered by the Candy Bucket
//  * Move back out to one of the identified the "landing areas"
//    This 'take off' area may not be the same area used to land, if multiple landing
//    areas are provided.
//  * Try to complete some achievements, and do some basic inventory management
//  * The behavior will wait for the "Trick" aura to expire
//  * The behavior will remove all of the unwanted costumes
//
// As it travels, the behavior will also pursue these acheivements:
//  * Check Your Head (http://wowhead.com/achievement=291)
//  * Out With It (http://wowhead.com/achievement=288)
//  * That Sparkling Smile (http://wowhead.com/achievement=981)
//      After the achievement is completed, excess Tooth Picks will be destroyed to keep
//      the backpack as free as possible.
//
// These achievements are not pursued:
//  * The Masquerade (http://wowhead.com/achievement=283)
//      You cannot apply the Wand on yourself, so this achievement cannot be completed solo.
//
// BEHAVIOR ATTRIBUTES:
// Basic Attributes:
//      QuestId [REQUIRED]:
//          This is the expected Quest offered by the pursued Candy Bucket
//
// THINGS TO KNOW:
//
#endregion


#region Examples
// EXAMPLE:
//     <CustomBehavior File="Holiday\HallowsEnd-CandyBuckets" QuestId="54321" />
//
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Bots.Grind;

using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Honorbuddy.QuestBehaviorCore.XmlElements;
using Styx;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Profiles.Quest.Order;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

#endregion


namespace Honorbuddy.Quest_Behaviors
{
    [CustomBehaviorFileName(@"Holiday\HallowsEnd-CandyBuckets")]
	public class HallowsEnd_CandyBuckets : QuestBehaviorBase
	{
		#region Constructor and Argument Processing
        public HallowsEnd_CandyBuckets(Dictionary<string, string> args)
			: base(args)
		{
			try
			{
				// NB: Core attributes are parsed by QuestBehaviorBase parent (e.g., QuestId, NonCompeteDistance, etc)
                // Although the QuestId is harvested by our QuestBehaviorBase parent, it is REQUIRED for this behavior.
                // So we enforce that constraint here.
			    GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null);

			    TerminationChecksQuestProgress = false;
			}

			catch (Exception except)
			{
				// Maintenance problems occur for a number of reasons.  The primary two are...
				// * Changes were made to the behavior, and boundary conditions weren't properly tested.
				// * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
				// In any case, we pinpoint the source of the problem area here, and hopefully it can be quickly
				// resolved.
				QBCLog.Exception(except);
				IsAttributeProblem = true;
			}
		}

		// Variables for Attributes provided by caller
        // none, atm.

		protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
		{
            // For an example, see TEMPLATE_QB.cs
		}

		protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
		{
            // For an example, see TEMPLATE_QB.cs
		}
		#endregion


		#region Private and Convenience variables

        private const int AchievementId_CheckYourHead = 291; // http://wowhead.com/achievement=291
        private const int AchievementId_OutWithIt = 288; // http://wowhead.com/achievement=288
        private const int AchievementId_ThatSparklingSmile = 981; // http://wowhead.com/achievement=981

        private const int AuraId_JackOLanterned = 44212; // http://wowhead.com/spell=44212
        private const int AuraId_MagicBroom = 47977; // http://wowhead.com/spell=47977
        private const int AuraId_TrickyTreat = 42919; // http://wowhead.com/spell=42919
        private const int AuraId_UpsetTummy = 42966; // http://wowhead.com/spell=42966

        private const int ItemId_MagicBroom = 37011; // http://wowhead.com/item=37011
        private const int ItemId_ToothPick = 37604; // http://wowhead.com/item=37604
        private const int ItemId_TrickyTreat = 33226; // http://wowhead.com/item=33226
        private const int ItemId_WeightedJackOLantern = 34068; // http://wowhead.com/item=34068

        private bool IsPostQuestWrapUpNeeded { get; set; }
        private CandyBucketType CandyBucketInfo { get; set; }
        private WoWGameObject SelectedCandyBucket { get; set; }
        private WaypointType SelectedLandingArea { get; set; }
        private string SelectedLandingAreaName { get; set; }
        private WaypointType SelectedTakeOffArea { get; set; }
        private static TimeSpan VariantDelay2Secs
        {
            get
            {
				return (StyxWoW.Random.Next(1, 100) < 70)
						? TimeSpan.FromMilliseconds(StyxWoW.Random.Next(1000, 2500))
						: TimeSpan.FromMilliseconds(StyxWoW.Random.Next(1500, 5000));
            }
        }
        private static TimeSpan VariantDelay5Secs
        {
            get
            {
				return (StyxWoW.Random.Next(1, 100) < 70)
						? TimeSpan.FromMilliseconds(StyxWoW.Random.Next(2500, 5500))
						: TimeSpan.FromMilliseconds(StyxWoW.Random.Next(3000, 8000));
            }
        }

        private readonly TimeSpan ThrottleTime_CheckYourHead = TimeSpan.FromMilliseconds(3300);

        private const string TrickAuraName = "Trick";

        private static readonly BehaviorDatabase _behaviorDatabase = new BehaviorDatabase();
        private static readonly ProfileHelperFunctionsBase _helpers = new ProfileHelperFunctionsBase();
        private readonly Stopwatch _throttleTimer_CheckYourHead = new Stopwatch();
        #endregion


		#region Overrides of CustomForcedBehavior
		// DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return "$Id: HallowsEnd_CandyBuckets.cs 1728 2014-10-13 23:25:24Z chinajade $"; } }
		public override string SubversionRevision { get { return "$Rev: 1728 $"; } }


		// CreateBehavior supplied by QuestBehaviorBase.
		// Instead, provide CreateMainBehavior definition.

		// Dispose provided by QuestBehaviorBase.

		// IsDone provided by QuestBehaviorBase.
		// Call the QuestBehaviorBase.BehaviorDone() method when you want to indicate your behavior is complete.

		public override void OnStart()
		{
            _behaviorDatabase.RereadDatabaseIfFileChanged();

		    IsAttributeProblem |= _behaviorDatabase.CandyBuckets.IsAttributeProblem;

            CandyBucketInfo = _behaviorDatabase.CandyBuckets.CandyBuckets.FirstOrDefault(qd => qd.QuestId == QuestId);
            if (CandyBucketInfo == null)
            {
                CandyBucketInfo = CandyBucketType.Empty;

                var message = string.Format("Unable to find details for Candy Bucket providing QuestId({0})", QuestId);
                QBCLog.Fatal(message);
                BehaviorDone(message);
                return;
            }

            // Let QuestBehaviorBase do basic initialization of the behavior, deal with bad or deprecated attributes,
            // capture configuration state, install BT hooks, etc.  This will also update the goal text.

            // These quests will never make it to our log--they complete immediately...
            QuestRequirementInLog = QuestInLogRequirement.NotInLog;
		    QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
		    TerminationChecksQuestProgress = false;
			var isBehaviorShouldRun = OnStart_QuestBehaviorCore();
		    if (!isBehaviorShouldRun)
		        return;


            // If we're on the wrong map, we're done...
            if (Me.MapId != CandyBucketInfo.MapId)
            {
                var message = string.Format("You are on the wrong continent for Candy Bucket providing QuestId({0})."
                                            + "  Please move the toon to MapId({1}) and try again.",
                                            QuestId,
                                            CandyBucketInfo.MapId);
                QBCLog.Fatal(message);
                BehaviorDone(message);
                return;
            }

            // If we're the wrong faction, we're done...
            if ((CandyBucketInfo.FactionGroup != Me.FactionGroup) && (CandyBucketInfo.FactionGroup != WoWFactionGroup.Neutral))
            {
                var message = string.Format("QuestId({0}) is for faction group {1}, your FactionGroup is {2}.",
                                            QuestId,
                                            CandyBucketInfo.FactionGroup,
                                            Me.FactionGroup);
                QBCLog.Fatal(message);
                BehaviorDone(message);
                return;                
            }

			// Setup settings to prevent interference with your behavior --
			// These settings will be automatically restored by QuestBehaviorBase when Dispose is called
			// by Honorbuddy, or the bot is stopped.
			CharacterSettings.Instance.HarvestHerbs = false;
			CharacterSettings.Instance.HarvestMinerals = false;
			CharacterSettings.Instance.LootChests = false;
			CharacterSettings.Instance.SkinMobs = false;

            // Disable the learning of flightpaths until after we get quest from Candy Bucket...
            // We need to be able to 'see' CandyBucket in ObjectManager.  We position the landing area
            // near enough to the CandyBucket that this will always happen.  However, harvesting and
            // Flightpath learning can take us far enough away that the Candy Bucket is no longer visible
            // to the ObjectManager, and this will cause exceptions to be thrown.
		    LevelBot.BehaviorFlags &= ~BehaviorFlags.FlightPath;

            QBCLog.Info("Pursuing Candy Bucket: \"{0}\"", CandyBucketInfo.CandyBucketLocationName);
		}
		#endregion


        #region Profile Helpers
        // Used by profiles...
        public static bool HaveAnyQuestsOnMap(int mapId)
        {
            _behaviorDatabase.RereadDatabaseIfFileChanged();

            return
                _behaviorDatabase.CandyBuckets.CandyBuckets
                    .Any(q => (mapId == q.MapId)
                              && (Me.FactionGroup == q.FactionGroup)
                              && (Me.Level >= q.LevelRequirement)
                              && !Me.IsQuestComplete(q.QuestId));            
        }
        #endregion


        #region Main Behaviors
        protected override Composite CreateMainBehavior()
		{
            return new ActionRunCoroutine(ctx => MainCoroutine());
		}


        private async Task<bool> MainCoroutine()
        {
            if (IsDone)
                return false;

            var isMounted = Me.Mounted;
            var isQuestComplete = Me.IsQuestComplete(QuestId);

            // If we do the quest, we'll must do the post-quest cleanup...
            if (!isQuestComplete)
                IsPostQuestWrapUpNeeded = true;

            // If both the quest and the wrap up is complete, we're done...
            if (isQuestComplete && !IsPostQuestWrapUpNeeded)
            {
                SelectedCandyBucket = null;
                SelectedLandingArea = null;
                SelectedTakeOffArea = null;
                BehaviorDone(string.Format("\"{0}\" Candy Bucket complete", CandyBucketInfo.CandyBucketLocationName));
                return false;
            }

            // Establish entry area...
            if (SelectedLandingArea == null)
            {
                CandyBucketInfo.LandingAreas.ResetWaypoints();  // selects a new "CurrentWaypoint" from those available
                SelectedLandingArea = CandyBucketInfo.LandingAreas.CurrentWaypoint();
                SelectedLandingAreaName = string.Format("{0} of \"{1}\" Candy Bucket",
                                                        SelectedLandingArea.Name,
                                                        CandyBucketInfo.CandyBucketLocationName);
            }

            if (!isMounted && await PursueAchievement_CheckYourHead())
                return true;

            // Go fetch and complete Candy Bucket quest...
            if (!isQuestComplete)
            {
                await MoveToCandyBucketAndCompleteQuest(isMounted);
                return true;
            }

            await PostQuestWrapUp();
            return true;
        }
		#endregion


        #region Helper Coroutines
        private async Task<bool> DeleteItem(WoWItem wowItem)
        {
            Contract.Requires(Query.IsViable(wowItem), (context) => "wowItem is viable");

            QBCLog.Info("Deleting {0}", wowItem.Name);
            wowItem.PickUp();
            await CommonCoroutines.SleepForLagDuration();
            Lua.DoString("DeleteCursorItem();");
            await Coroutine.Sleep(Delay.AfterInteraction);
            return true;
        }


        // We expect the candy bucket to be visible to the ObjectManager, before this method is called.
        // This means the upper logic must have already done the needed gross move.
        private async Task<bool> MoveToCandyBucketAndCompleteQuest(bool isMounted)
        {
            var candyBucketId = CandyBucketInfo.CandyBucketId;
            var candyBucketLocationName = CandyBucketInfo.CandyBucketLocationName;

            // Find the destination Candy Bucket, if we can "see" it...
            if (SelectedCandyBucket == null)
            {
                SelectedCandyBucket =
                    ObjectManager.GetObjectsOfType<WoWGameObject>()
                                 .FirstOrDefault(u => Query.IsViable(u) && (u.Entry == candyBucketId));
            }

            // If we can't "see" the Candy Bucket, move to the landing area...
            if ((SelectedCandyBucket == null)  &&  !Navigator.AtLocation(SelectedLandingArea.Location))
            {
                if (await PreferMount_MagicBroom())
                    return true;

                await UtilityCoroutine.MoveTo(SelectedLandingArea.Location, SelectedLandingAreaName, CandyBucketInfo.MovementBy);
                return true;
            }

            // If we're at the landing area, and can't see the Candy Bucket, something is horribly wrong...
            // (Probably wrong QuestId or wrong CandyBucketId).
            if (Navigator.AtLocation(SelectedLandingArea.Location) && (SelectedCandyBucket == null))
            {
                var maxWaitForCandyBucketTime = TimeSpan.FromSeconds(20);

                QBCLog.Warning("Waiting for {0} seconds for CandyBucket({1}) to appear at \"{2}\"",
                    maxWaitForCandyBucketTime.TotalSeconds, candyBucketId, candyBucketLocationName);

                // Since it can take the WoWclient several seconds to 'wink in' objects, we must allow for this...
                // The WoWclient can be *very* slow at times--especially if the area is busy.
                // For testing: If you've a character that hasn't done any of the Twilight Highlands quests,
                // the phasing in Twilight Highlands is a good test bed.  Goldshire is another place where
                // the WoWserver is slow (when its full of people).
                await Coroutine.Sleep(VariantDelay2Secs);
                await Coroutine.Wait(maxWaitForCandyBucketTime,
                                     delegate
                                        {
                                            SelectedCandyBucket =
                                                ObjectManager.GetObjectsOfType<WoWGameObject>()
                                                            .FirstOrDefault(
                                                                u => Query.IsViable(u) && (u.Entry == candyBucketId));
                                            return SelectedCandyBucket != null;
                                        });
                if (SelectedCandyBucket != null)
                    return true;

                QBCLog.Fatal("Unable to locate CandyBucket({0}) at \"{1}\"", candyBucketId, candyBucketLocationName);
                return false;
            }

            // Make certain SelectedCandyBucket is still viable (phasing areas et al)...
            if (!Query.IsViable(SelectedCandyBucket))
                return true;

            // We "see" the Candy Bucket, move to the landing area and dismount, if still mounted...
            if (isMounted)
            {
                if (!Navigator.AtLocation(SelectedLandingArea.Location))
                {
                    await UtilityCoroutine.MoveTo(SelectedLandingArea.Location, "Candy Bucket", CandyBucketInfo.MovementBy);
                    return true;
                }

                await CommonCoroutines.LandAndDismount();

                // Make it look like we're getting our bearings...
                // await Coroutine.Sleep(VariantDelay5Secs);
            }

            // Move toward candy bucket on foot...
            // The Candy Buckets have a very large interact range.  The bot will interact with them through
            // walls if we let it.  Thus, the LoS requirement.
            // NB: Some CandyBuckets--like the one in Wildhammer Stronghold, Shadowmoon Valley--are behind a bar that will
            // not pass a LoS check, so we need to selectively disable this.
            ProfileManager.CurrentProfile.UseMount = false;
            if (!SelectedCandyBucket.WithinInteractRange || (CandyBucketInfo.NeedLos && !SelectedCandyBucket.InLineOfSight))
            {
                await UtilityCoroutine.MoveTo(SelectedCandyBucket.Location, SelectedCandyBucket.Name, MovementByType.NavigatorPreferred);
                return true;
            }

            // Handle opening inventory, before we move on...
            // Some of the Hallows End inventory items are unique ("Handful of Treats", "Crudely Wrapped Gift", etc).
            // We must make certain these items are not in our inventory by opening them, before we do the quest
            // that can possibly result in more.  If we don't do this, then the quest acquisition will fail in an
            // endless loop.
            if (await HandleItems_ToOpen())
                return true;

            // Get quest from candy bucket...
			await CommonCoroutines.StopMoving();
            if (!QuestFrame.Instance.IsVisible)
            {
                await UtilityCoroutine.Interact(SelectedCandyBucket);
                return true;
            }

            await Coroutine.Sleep(Delay.AfterInteraction);
            // At the time of this writing, "CompeteQuest()" is correct...
			// QuestFrame.Instance.AcceptQuest();
			QuestFrame.Instance.CompleteQuest();
			//QuestFrame.Instance.ClickContinue();

            await CommonCoroutines.SleepForLagDuration();

            // Quest complete. We can now safely learn flightpaths again...
            LevelBot.BehaviorFlags |= BehaviorFlags.FlightPath;
            return true;
        }


        private async Task<bool> PostQuestWrapUp()
        {
            // We may have the "Trick" aura at this point, but we should be able to
            // do some basic things while waiting for it to expire.
            if (!Me.Mounted)
            {
                // Pursue any missing Achievements...
                // We go after achievements first, in case user decides to delete items we need
                // to pursue them.w
                if (await PursueAchievement_OutWithIt())
                    return true;

                if (await PursueAchievement_ThatSparklingSmile())
                    return true;

                // Handle inventory, before we move on...
                if (await HandleItems_ToOpen())
                    return true;

                if (await HandleItems_ToDiscard())
                    return true;

            }

            // Establish take off area, and move to it...
            if (SelectedTakeOffArea == null)
            {
                CandyBucketInfo.LandingAreas.ResetWaypoints();  // selects a new "CurrentWaypoint" from those available
                SelectedTakeOffArea = CandyBucketInfo.LandingAreas.CurrentWaypoint();
            }

            if (!Navigator.AtLocation(SelectedTakeOffArea.Location))
            {
                await UtilityCoroutine.MoveTo(SelectedTakeOffArea.Location, "Take off area", MovementByType.NavigatorPreferred);
                return true;
            }

            ProfileManager.CurrentProfile.UseMount = null;
            if (await PreferMount_MagicBroom())
                return true;

            IsPostQuestWrapUpNeeded = false;
            return false;
        }


        private async Task<bool> Handle_UnwantedAuras()
        {
            var unwantedAura =
                (from aura in Me.GetAllAuras()
                 where
                     aura.Cancellable
                     && (aura.Name.EndsWith(" Costume")
                         || _behaviorDatabase.DispelAuras.NameAndIds.Any(d => d.Id == aura.SpellId)
                         // Jack-o'-Lanterned explicitly included since part of "Check Your Head" achievement...
                         || (aura.SpellId == AuraId_JackOLanterned))
                 select aura)
                .FirstOrDefault();

            if (unwantedAura == null)
                return false;

            // Cancel any unwanted costume aura...
            QBCLog.Info("Dispelling unwanted \"{0}\"", unwantedAura.Name);
            unwantedAura.TryCancelAura();
            await Coroutine.Wait(TimeSpan.FromMilliseconds(3000), () => !Me.HasAura(unwantedAura.Name));
            await Coroutine.Sleep(VariantDelay2Secs);
            return true;
        }


        private async Task<bool> HandleAura_Trick()
        {
            if (!Me.HasAura(TrickAuraName))
                return false;

            // Since we can't cancel the "Trick" aura, we just have to wait for it to expire...
            QBCLog.Info("Waiting for \"{0}\" to expire", TrickAuraName);
            await Coroutine.Wait(TimeSpan.FromMilliseconds(120000), () => !Me.HasAura(TrickAuraName));
            await Coroutine.Sleep(VariantDelay2Secs);
            return true;
        }


        private async Task<bool> HandleItems_ToOpen()
        {
            var itemToOpen = Me.CarriedItems.FirstOrDefault(i => _behaviorDatabase.ItemsToOpen.NameAndIds.Any(o => i.Entry == o.Id));

            if (itemToOpen == null)
                return false;

            QBCLog.Info("Opening {0}", itemToOpen.Name);
            itemToOpen.Use();
            await Coroutine.Sleep(Delay.AfterItemUse);
            await Coroutine.Sleep(VariantDelay2Secs);
            return true;
        }


        private async Task<bool> HandleItems_ToDiscard()
        {
            var unwantedItem = Me.CarriedItems.FirstOrDefault(i => _behaviorDatabase.ItemsToDiscard.NameAndIds.Any(o => i.Entry == o.Id));

            if (unwantedItem == null)
                return false;

            await DeleteItem(unwantedItem);
            return true;
        }


        private async Task<bool> PreferMount_MagicBroom()
        {
            // We can't mount if we have certain auras, so nuke them...
            if (await Handle_UnwantedAuras())
                return true;

            // "Trick" aura must be gone before we try to mount...
            if (await HandleAura_Trick())
                return true;

            if (!Flightor.MountHelper.CanMount || Me.HasAura(AuraId_MagicBroom))
                return false;

            // Prefer the Magic Broom mount, if we've acquired one...
            var magicBroom = Me.CarriedItems.FirstOrDefault(i => i.Entry == ItemId_MagicBroom);
            if (magicBroom == null)
                return false;

            magicBroom.Use();
            await CommonCoroutines.SleepForLagDuration();
            return true;
        }


        private async Task<bool> PursueAchievement_CheckYourHead()
        {
            const double MaxDistanceSqrToSelectedTarget = (40 * 40);
            const double MaxRangeSqrForWeightedJackOLantern = (10 * 10);
            if (Query.IsAchievementPersonallyCompleted(AchievementId_CheckYourHead))
                return false;

            // If we're still in throttle, nothing to do...
            // If timer is not running, we'll start it below, once we've tried to advance the achievement.
            if (_throttleTimer_CheckYourHead.IsRunning && (_throttleTimer_CheckYourHead.Elapsed < ThrottleTime_CheckYourHead))
                return false;

            // If no Weighted Jack-o'-Lanterns, then nothing to do...
            var weightedJackOLantern = Me.CarriedItems.FirstOrDefault(i => i.Entry == ItemId_WeightedJackOLantern);
            if (weightedJackOLantern == null)
                return false;

            // See if we can find a viable target nearby...
            var neededRaces =
                _behaviorDatabase.AchievementGoals_CheckYourHead.SubGoals
                    .Where(s => !_helpers.IsAchievementCompleted(AchievementId_CheckYourHead, s.Index))
                    .Select(s => s.Race)
                    .ToList();

            var selectedPlayer =
                (from player in ObjectManager.GetObjectsOfType<WoWUnit>(true, true)
                 where
                    neededRaces.Contains(player.Race)
                    && !player.IsBlacklistedForInteraction()
                    && !player.HasAura(AuraId_JackOLanterned)
                    && (player.DistanceSqr < MaxDistanceSqrToSelectedTarget)
                    && !player.IsMoving
                 orderby player.DistanceSqr
                 select player)
                .FirstOrDefault();

            // If no matching players found, we're done...
            if (selectedPlayer == null)
            {
                _throttleTimer_CheckYourHead.Restart();
                return false;
            }

            // Move close enough to player to use Weighted Jack-o'-Lantern
            Utility.Target(selectedPlayer);
            if ((Me.Location.DistanceSqr(selectedPlayer.Location) > MaxRangeSqrForWeightedJackOLantern)
                || !selectedPlayer.InLineOfSpellSight)
            {
                // This is a bit ugly...
                // we are re-evaluating the selectedPlayer constantly as we're moving to him.
                // Although, this is expensive, its the right thing to do.  The player may mount up
                // and fly off, and other degenerate problems.  We don't want to chase him if this happens,
                // because we'll look like a bot.
                if (await UtilityCoroutine.MoveTo(selectedPlayer.Location, selectedPlayer.SafeName, MovementByType.NavigatorPreferred))
                    return true;
            }

            // We blacklist the player after attempting use of Weighted Jack-o'-Lantern.
            // Sometimes it doesn't work, and we don't want to get gummed up retrying.
            // It may be because the player is wearing a costume, or is cross-realm.
            QBCLog.Info("Advancing \"Check Your Head\" achievement with a {0} target.", selectedPlayer.Race);
			await CommonCoroutines.StopMoving();
            weightedJackOLantern.Use();
            selectedPlayer.BlacklistForInteracting(TimeSpan.FromMinutes(5));
            await Coroutine.Sleep(Delay.AfterItemUse);
            _throttleTimer_CheckYourHead.Restart();

            // Simulate "looking for next target"...
            await Coroutine.Sleep(VariantDelay5Secs);
            return true;
        }


        private async Task<bool> PursueAchievement_OutWithIt()
        {
            if (Query.IsAchievementPersonallyCompleted(AchievementId_OutWithIt))
                return false;

            // If we can't eat any more candy at the moment, don't try...
            if (Me.HasAura(AuraId_UpsetTummy))
                return false;

            var trickyTreatCount = _helpers.GetItemCount(ItemId_TrickyTreat);
            if ((trickyTreatCount < 20) && !Me.HasAura(AuraId_TrickyTreat))
                return false;

            var trickyTreat = Me.CarriedItems.FirstOrDefault(i => i.Entry == ItemId_TrickyTreat);
            if (trickyTreat == null)
                return false;

            QBCLog.Info("Pursuing Achievement: \"Out With It\" (http://wowhead.com/achievement={0})", AchievementId_OutWithIt);
            QBCLog.Info("    Eating \"{0}\"", trickyTreat.Name);
            trickyTreat.Use();
            await Coroutine.Sleep(Delay.AfterItemUse);
            return true;
        }


        private async Task<bool> PursueAchievement_ThatSparklingSmile()
        {
            var toothPick = Me.CarriedItems.FirstOrDefault(i => i.Entry == ItemId_ToothPick);
            if (toothPick == null)
                return false;

            if (Query.IsAchievementPersonallyCompleted(AchievementId_ThatSparklingSmile))
            {
                // Achievement is done, delete spare toothpicks to free up inventory space...
                return await DeleteItem(toothPick);
            }

            QBCLog.Info("Pursuing Achievement: \"That Sparkling Smile\" (http://wowhead.com/achievement={0})",
                        AchievementId_ThatSparklingSmile);
            toothPick.Use();
            await Coroutine.Sleep(Delay.AfterItemUse);
            return true;
        }
        #endregion


        #region Helper classes: Database
        public class BehaviorDatabase
        {
            public BehaviorDatabase()
            {
                _databaseName = "HallowsEnd.xml";
                _lastDatabaseModifiedTime = new DateTime(0);
                RereadDatabaseIfFileChanged();
            }

            public CandyBucketList CandyBuckets;
            public AchievementGoals_CheckYourHead AchievementGoals_CheckYourHead;
            public NameAndIdList DispelAuras;
            public NameAndIdList ItemsToDiscard;
            public NameAndIdList ItemsToOpen;

            private readonly string _databaseName;
            private DateTime _lastDatabaseModifiedTime;


            public void RereadDatabaseIfFileChanged()
            {

                // NB: We use the absolute path here.  If we don't, then QBs get confused if there are additional
                // QBs supplied in the Honorbuddy/Default Profiles/. directory.
                var dataFileFullPath = Utility.GetDataFileFullPath(_databaseName);
                var lastReadTime = File.GetLastWriteTime(dataFileFullPath);

                if (lastReadTime <= _lastDatabaseModifiedTime)
                    return;

                QBCLog.DeveloperInfo("Database \"{0}\" has changed--re-reading.", _databaseName);

                var xDoc = XDocument.Load(dataFileFullPath, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo);
                var xHallowsEnd = xDoc.Elements("HallowsEnd").DefaultIfEmpty(new XElement("HallowsEnd")).ToList();

                // Achievement goals for "Check Your Head" ...
                {
                    var xAchievements = xHallowsEnd.Elements("Achievements").DefaultIfEmpty(new XElement("Achievements"));
                    var xCheckYourHead = xAchievements.Elements("CheckYourHead").DefaultIfEmpty(new XElement("CheckYourHead"));

                    AchievementGoals_CheckYourHead = new AchievementGoals_CheckYourHead(xCheckYourHead.FirstOrDefault());
                }

                // Candy Buckets...
                using (StyxWoW.Memory.AcquireFrame())
                {
                    var xCandyBuckets = xHallowsEnd.Elements("CandyBuckets").DefaultIfEmpty(new XElement("CandyBuckets"));

                    CandyBuckets = new CandyBucketList(xCandyBuckets.FirstOrDefault());
                }

                // Auras to dispel...
                {
                    var xDispelAuras = xHallowsEnd.Elements("DispelAuras").DefaultIfEmpty(new XElement("DispelAuras"));

                    DispelAuras = new NameAndIdList(xDispelAuras.FirstOrDefault());
                }

                // Items to Discard...
                {
                    var xItems = xHallowsEnd.Elements("ItemsToDiscard").DefaultIfEmpty(new XElement("ItemsToDiscard"));

                    ItemsToDiscard = new NameAndIdList(xItems.FirstOrDefault());
                }

                // Items to open...
                {
                    var xItems = xHallowsEnd.Elements("ItemsToOpen").DefaultIfEmpty(new XElement("ItemsToOpen"));

                    ItemsToOpen = new NameAndIdList(xItems.FirstOrDefault());
                }

                QBCLog.DeveloperInfo("Database \"{0}\" re-read complete.", _databaseName);
                _lastDatabaseModifiedTime = lastReadTime;
            }


            public XElement ToXml()
            {
                var root = new XElement("HallowsEnd");

                root.Add(CandyBuckets.ToXml("CandyBuckets"));
                root.Add(AchievementGoals_CheckYourHead.ToXml("CheckYourHead"));
                root.Add(DispelAuras.ToXml("DispelAuras"));
                root.Add(ItemsToDiscard.ToXml("ItemsToDiscard"));
                root.Add(ItemsToOpen.ToXml("ItemsToOpen"));

                return root;
            }
            
        }
        #endregion


        #region Helper classes: Database-GoalsCheckYourHead
        public class Achievement_CheckYourHead_SubGoal : QuestBehaviorXmlBase
        {
            public Achievement_CheckYourHead_SubGoal(XElement xElement)
                : base(xElement)
            {
                try
                {
                    Index = GetAttributeAsNullable<int>("Index", true, ConstrainAs.ItemId, null) ?? -1;
                    Race = GetAttributeAsNullable<WoWRace>("Race", true, null, null) ?? WoWRace.Broken;

                    HandleAttributeProblem();
                }

                catch (Exception except)
                {
                    if (Query.IsExceptionReportingNeeded(except))
                        QBCLog.Exception(except, "PROFILE PROBLEM with \"{0}\"", xElement.ToString());

                    IsAttributeProblem = true;
                }
            }

            public int Index { get; private set; }
            public WoWRace Race { get; private set; }

            private const string DefaultElementName = "SubGoal";


            public override XElement ToXml(string elementName = null)
            {
                elementName = string.IsNullOrEmpty(elementName) ? DefaultElementName : elementName;

                var root = new XElement(elementName,
                                        new XAttribute("Index", Index),
                                        new XAttribute("Race", Race));

                return root;
            }
        }

        public class AchievementGoals_CheckYourHead : QuestBehaviorXmlBase
        {
            public AchievementGoals_CheckYourHead(XElement xElement)
                : base(xElement)
            {
                try
                {
                    // Acquire the SubGoal info...
                    SubGoals = new List<Achievement_CheckYourHead_SubGoal>();
                    if (xElement != null)
                    {
                        foreach (XElement childElement in xElement.Elements("SubGoal"))
                        {
                            var subGoal = new Achievement_CheckYourHead_SubGoal(childElement);

                            if (!subGoal.IsAttributeProblem)
                                SubGoals.Add(subGoal);

                            IsAttributeProblem |= subGoal.IsAttributeProblem;
                        }
                    }

                    HandleAttributeProblem();
                }

                catch (Exception except)
                {
                    if (Query.IsExceptionReportingNeeded(except))
                        QBCLog.Exception(except, "PROFILE PROBLEM with \"{0}\"", xElement.ToString());
                    IsAttributeProblem = true;
                }
            }

            public readonly List<Achievement_CheckYourHead_SubGoal> SubGoals;

            private const string DefaultElementName = "GoalsCheckYourHead";

            public override XElement ToXml(string elementName = null)
            {
                elementName = string.IsNullOrEmpty(elementName) ? DefaultElementName : elementName;

                var root = new XElement(elementName);

                foreach (var subGoal in SubGoals)
                    root.Add(subGoal.ToXml("SubGoal"));

                return root;
            }
        }
        #endregion


        #region Helper classes: Database-CandyBuckets
        public class CandyBucketType : QuestBehaviorXmlBase
        {
            public CandyBucketType(XElement xElement)
            : base(xElement)
            {
                try
                {
                    MapId = GetAttributeAsNullable<int>("MapId", true, new ConstrainTo.Domain<int>(0, int.MaxValue), null) ?? -1;
                    FactionGroup = GetAttributeAsNullable<WoWFactionGroup>("FactionGroup", true, null, null) ?? WoWFactionGroup.None;
                    LevelRequirement = GetAttributeAsNullable<int>("LevelRequirement", true, new ConstrainTo.Domain<int>(1, 1001), null) ?? -1;
                    AchievementId = GetAttributeAsNullable<int>("AchievementId", true, ConstrainAs.QuestId, null) ?? -1;
                    AchievementSubIndex = GetAttributeAsNullable<int>("AchievementSubIndex", true, new ConstrainTo.Domain<int>(1, 50), null) ?? -1;
                    CandyBucketId = GetAttributeAsNullable<int>("CandyBucketId", true, ConstrainAs.MobId, null) ?? -1;
                    QuestId = GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId, null) ?? -1;

                    // Tunables
                    NeedLos = GetAttributeAsNullable<bool>("NeedLos", false, null, null) ?? true;
                    MovementBy = GetAttributeAsNullable<MovementByType>("MovementBy", false, null, null) ?? MovementByType.FlightorPreferred;

                    // LandingArea processing...
                    LandingAreas = HuntingGroundsType.GetOrCreate(Element, "LandingAreas", null);
                    if (LandingAreas.Waypoints.Count <= 0)
                        LandingAreas.Waypoints.Add(new WaypointType(Me.Location, "initial location"));
                    LandingAreas.WaypointVisitStrategy = HuntingGroundsType.WaypointVisitStrategyType.PickOneAtRandom;
                    IsAttributeProblem |= LandingAreas.IsAttributeProblem;

                    if (!IsAttributeProblem)
                    {
                        // Look up the location name from the achievement itself...
                        var luaCmd = string.Format("return GetAchievementCriteriaInfo({0},{1});", AchievementId, AchievementSubIndex);
                        CandyBucketLocationName = Lua.GetReturnVal<string>(luaCmd, 0);
                        if (string.IsNullOrEmpty(CandyBucketLocationName))
                            CandyBucketLocationName = "UNKNOWN";
                    }

                    HandleAttributeProblem();
                }

                catch (Exception except)
                {
                    if (Query.IsExceptionReportingNeeded(except))
                        QBCLog.Exception(except, "PROFILE PROBLEM with \"{0}\"", xElement.ToString());

                    IsAttributeProblem = true;
                }
            }


            private CandyBucketType()
            {
                AchievementId = -1;
                AchievementSubIndex = -1;
                CandyBucketId = -1;
                CandyBucketLocationName = "UNKNOWN";
                FactionGroup = WoWFactionGroup.None;
                NeedLos = true;
                LevelRequirement = -1;
                MapId = -1;
                MovementBy = MovementByType.None;
                QuestId = -1;
            }

            public int AchievementId { get; private set; }
            public int AchievementSubIndex { get; private set; }
            public int CandyBucketId { get; private set; }
            public string CandyBucketLocationName { get; private set; }
            public WoWFactionGroup FactionGroup { get; private set; }
            public bool NeedLos { get; private set; }
            public HuntingGroundsType LandingAreas { get; private set; }
            public int LevelRequirement { get; private set; }
            public int MapId { get; private set; }
            public MovementByType MovementBy { get; private set; }
            public int QuestId { get; private set; }

            private const string DefaultElementName = "CandyBucket";


            public static CandyBucketType Empty = new CandyBucketType();


            public override XElement ToXml(string elementName = null)
            {
                elementName = string.IsNullOrEmpty(elementName) ? DefaultElementName : elementName;

                var root = new XElement(elementName,
                                        new XAttribute("MapId", MapId),
                                        new XAttribute("FactionGroup", FactionGroup),
                                        new XAttribute("LevelRequirement", LevelRequirement),
                                        new XAttribute("AchievementId", AchievementId),
                                        new XAttribute("AchievementSubIndex", AchievementSubIndex),
                                        new XAttribute("CandyBucketId", CandyBucketId),
                                        new XAttribute("QuestId", QuestId),
                                        new XAttribute("MovementBy", MovementBy),
                                        new XAttribute("NeedLos", NeedLos));
                root.Add(LandingAreas.ToXml("LandingAreas"));

                return root;
            }
        }

        public class CandyBucketList : QuestBehaviorXmlBase
        {
            public CandyBucketList(XElement xElement)
                : base(xElement)
            {
                try
                {
                    // Acquire the candy bucket info...
                    CandyBuckets = new List<CandyBucketType>();
                    if (xElement != null)
                    {
                        foreach (XElement childElement in xElement.Elements("CandyBucket"))
                        {
                            var candyBucket = new CandyBucketType(childElement);

                            if (!candyBucket.IsAttributeProblem)
                                CandyBuckets.Add(candyBucket);

                            IsAttributeProblem |= candyBucket.IsAttributeProblem;
                        }
                    }

                    HandleAttributeProblem();
                }

                catch (Exception except)
                {
                    if (Query.IsExceptionReportingNeeded(except))
                        QBCLog.Exception(except, "PROFILE PROBLEM with \"{0}\"", xElement.ToString());
                    IsAttributeProblem = true;
                }
            }

            public readonly List<CandyBucketType> CandyBuckets;

            private const string DefaultElementName = "CandyBuckets";
 
            public override XElement ToXml(string elementName = null)
            {
                elementName = string.IsNullOrEmpty(elementName) ? DefaultElementName : elementName;

                var root = new XElement(elementName);

                foreach (var candyBucket in CandyBuckets)
                {
                    root.Add(new XComment(string.Format("Achievement({0}, {1}): \"{2}\"",
                                                        candyBucket.AchievementId,
                                                        candyBucket.AchievementSubIndex,
                                                        candyBucket.CandyBucketLocationName)));
                    root.Add(candyBucket.ToXml("CandyBucket"));
                }

                return root;
            }
        }
        #endregion


        #region Helper classes: Database-DispelAuras
        public class DispelAuraList : QuestBehaviorXmlBase
        {
            public DispelAuraList(XElement xElement)
                : base(xElement)
            {
                try
                {
                    // Acquire the candy bucket info...
                    Auras = new List<NameAndIdType>();
                    if (xElement != null)
                    {
                        foreach (var childElement in xElement.Elements("Aura"))
                        {
                            var aura = new NameAndIdType(childElement);

                            if (!aura.IsAttributeProblem)
                                Auras.Add(aura);

                            IsAttributeProblem |= aura.IsAttributeProblem;
                        }
                    }

                    HandleAttributeProblem();
                }

                catch (Exception except)
                {
                    if (Query.IsExceptionReportingNeeded(except))
                        QBCLog.Exception(except, "PROFILE PROBLEM with \"{0}\"", xElement.ToString());
                    IsAttributeProblem = true;
                }
            }

            public readonly List<NameAndIdType> Auras;

            private const string DefaultElementName = "DispelAuras";

            public override XElement ToXml(string elementName = null)
            {
                elementName = string.IsNullOrEmpty(elementName) ? DefaultElementName : elementName;

                var root = new XElement(elementName);

                foreach (var aura in Auras)
                    root.Add(aura.ToXml("Aura"));

                return root;
            }
        }
        #endregion


        #region Helper classes: Database-NameAndIdType
        public class NameAndIdType : QuestBehaviorXmlBase
        {
            public NameAndIdType(XElement xElement)
                : base(xElement)
            {
                try
                {
                    Name = GetAttributeAs<string>("Name", false, ConstrainAs.StringNonEmpty, null);
                    Id = GetAttributeAsNullable<int>("Id", true, ConstrainAs.ItemId, null) ?? -1;

                    if (string.IsNullOrEmpty(Name))
                        Name = string.Format("ItemId({0}", Id);

                    HandleAttributeProblem();
                }

                catch (Exception except)
                {
                    if (Query.IsExceptionReportingNeeded(except))
                        QBCLog.Exception(except, "PROFILE PROBLEM with \"{0}\"", xElement.ToString());

                    IsAttributeProblem = true;
                }
            }

            private const string DefaultElementName = "Item";

            public int Id { get; private set; }
            public string Name { get; private set; }


            public override XElement ToXml(string elementName = null)
            {
                elementName = string.IsNullOrEmpty(elementName) ? DefaultElementName : elementName;

                var root = new XElement(elementName,
                                        new XAttribute("Name", Name),
                                        new XAttribute("Id", Id));

                return root;
            }
        }

        public class NameAndIdList : QuestBehaviorXmlBase
        {
            public NameAndIdList(XElement xElement)
                : base(xElement)
            {
                try
                {
                    // Acquire the candy bucket info...
                    NameAndIds = new List<NameAndIdType>();
                    if (xElement != null)
                    {
                        foreach (XElement childElement in xElement.Elements("Item"))
                        {
                            var item = new NameAndIdType(childElement);

                            if (!item.IsAttributeProblem)
                                NameAndIds.Add(item);

                            IsAttributeProblem |= item.IsAttributeProblem;
                        }
                    }

                    HandleAttributeProblem();
                }

                catch (Exception except)
                {
                    if (Query.IsExceptionReportingNeeded(except))
                        QBCLog.Exception(except, "PROFILE PROBLEM with \"{0}\"", xElement.ToString());

                    IsAttributeProblem = true;
                }
            }

            public readonly List<NameAndIdType> NameAndIds;

            private const string DefaultElementName = "Items";

            public override XElement ToXml(string elementName = null)
            {
                elementName = string.IsNullOrEmpty(elementName) ? DefaultElementName : elementName;

                var root = new XElement(elementName);

                foreach (var item in NameAndIds)
                    root.Add(item.ToXml("Item"));

                return root;
            }
        }
        #endregion
    }
}


#region DevTools Snippets
//var achievementId = 965;
//var achievementSubIndex = 0;
//var candyBucket =
//    ObjectManager.GetObjectsOfType<WoWObject>(true,false)
//    .OrderBy(o => o.Distance)
//    .FirstOrDefault(o => o.Name == "Candy Bucket");
//if (candyBucket != null)
//{
//    candyBucket.Interact();
//    Thread.Sleep(2000);

//    var luaCmd = string.Format("return GetAchievementCriteriaInfo({0},{1});", achievementId, achievementSubIndex);
//     var candyBucketLocationName = Lua.GetReturnVal<string>(luaCmd, 0);

//    var questId = QuestFrame.Instance.CurrentShownQuestId;
//    QuestFrame.Instance.Close();
//   Logging.Write("{0}{0}{0}{0}{0}{0}"
//       + "      <CandyBucket MapId=\"{1}\" FactionGroup=\"{2}\" LevelRequirement=\"60\" {0}"
//       + "                   AchievementId=\"{3}\" AchievementSubIndex=\"{4}\"{0}"
//       + "                   CandyBucketId=\"{5}\" QuestId=\"{6}\" >{0}"
//       + "        <LandingAreas>{0}"
//       + "        </LandingAreas>{0}"
//       + "      </CandyBucket>{0}",
//      Environment.NewLine,
//      Me.MapId,
//      Me.FactionGroup.ToString(),
//      achievementId,
//      achievementSubIndex,
//      candyBucket.Entry,
//      questId);
//}
#endregion


#region Helpful LUA to use in WoWclient
/*
local id = 5838;
for i = 1,GetAchievementNumCriteria(id) do
    local desc = GetAchievementCriteriaInfo(id, i);
	local msg=string.format("(%d,%d) = %s", id, i, desc);
    print(msg); 
end
 */
#endregion