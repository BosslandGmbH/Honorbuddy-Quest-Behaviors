// Behavior originally contributed by Raphus.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

#region Summary and Documentation
//
// QUICK DOX:
// Uses the hearthstone, if available.
// * Can optionally wait for the cooldown to complete.
// * If you don't elect to wait for the cooldown, the profile will continue.
//   In this situation, Honorbuddy will use whatever other transport options
//   are available to it to move to the next destination.  (E.g., flying mount,
//   ground mount, or in worse case 'on foot').
//   ==> Honorbuddy cannot do inter-continental travel on its on.  If you planned
//   ==> an inter-continental hearth, then you *must* wait for the cooldown to complete.
//   ==> Or, you can write the profile to help Honorbuddy get to the other continent
//   ==> if the hearthstone is on cooldown, and you don't want to wait.
//
// Basic Attributes:
//      WaitForCD [optional;  Default: false]
//          If true and the Hearthstone is on cooldown, Honorbuddy will wait for
//          the Hearthstone cooldown to complete, then use it.
//          If false and the Hearthstone is on cooldown, the behavior terminates
//          immediately, and the profile continues.  If travel is on the same continent,
//          Honorbuddy will provide the "plan B" travel.  However, if inter-continental
//          travel is involved, the profile is expected to provide profile directives
//          for "plan B" tranportation.
//
#endregion


#region Examples
// Toon is in Eastern Plaguelands, and hearth is set in Stormwind.
// We choose to not wait for the cooldown.  If the hearthstone is on cooldown,
// the toon fill find other means to get to Stormwind.  In the best case, this
// will involve a Taxi ride.  In the worst case, this will involve walking.
//    <CustomBehavior File="UseHearthstone" />
//    <!-- continue with profile when hearthstone completes, or hearthstone on cooldown -->
//
// Toon is in Outland, and hearth is set in Stormwind.
// We are lazy and don't want to write the required "Plan B" code for inter-continental
// travel.  So we force a wait for the hearthstone cooldown, if the hearthstone is
// not ready yet.
//    <CustomBehavior File="UseHearthstone" WaitForCD="True" />
//    <!-- profile continues once the hearth has been used -->
//
// Toon is in Outland, and hearth is set in Stormwind.
// We choose not to wait for the hearth cooldown, if hearth is not available.
// As such, we must provide the "Plan B" code for inter-continental travel,
// if the hearth is not used.
//    <CustomBehavior File="UseHearthstone" />
//    <!-- "Plan B" for inter-continental travel, if hearth was on cooldown -->
//    <If Condition="Me.MapId != 723">
//        <CustomBehavior File="Message" Text="Flying or Shattrath to take portal to Stormwind" LogColor="Orange" />
//        <CustomBehavior File="FlyTo" DestName="SW portal" X="-1956.413" Y="5383.551" Z="-12.42774" />
//        <CustomBehavior File="ForcedDismount" />
//        <MoveTo X="-1893.108" Y="5393.317" Z="-12.4277" />
//        <!-- Use portal to Stormwind -->
//        <CustomBehavior File="InteractWith" MobId="183325" X="-1893.108" Y="5393.317" Z="-12.4277" />
//        <CustomBehavior File="WaitTimer" WaitTime="20000" GoalText="Waiting for zone {TimeRemaining}" />
//    </If>
//    <!-- profile continues once in Stormwind...
//         We may have gotten here by hearth, or by moving through portal
//      -->
//
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals.DBC;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.UseHearthstone
{
	[CustomBehaviorFileName(@"UseHearthstone")]
	public class UseHearthstone : QuestBehaviorBase
	{
		public UseHearthstone(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.

				WaitOnCd = GetAttributeAsNullable<bool>("WaitForCD", false, null, null) ?? false;
				UseGarrisonHearthstone = GetAttributeAsNullable<bool>("UseGarrisonHearthstone", false, null, null) ?? false;
			}

			catch (Exception except)
			{
				// Maintenance problems occur for a number of reasons.  The primary two are...
				// * Changes were made to the behavior, and boundary conditions weren't properly tested.
				// * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
				// In any case, we pinpoint the source of the problem area here, and hopefully it
				// can be quickly resolved.
				QBCLog.Exception(except);
				IsAttributeProblem = true;
			}
		}

		// Attributes provided by caller
		public bool WaitOnCd { get; private set; }
		public bool UseGarrisonHearthstone { get; private set; }

		// Private variables for internal state
		private bool _isBehaviorDone;
		private Composite _root;

		// Private properties
		private LocalPlayer Me { get { return (StyxWoW.Me); } }
		private int _retries;
		// DON'T EDIT THESE--they are auto-populated by Subversion


		#region Overrides of CustomForcedBehavior

		protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement) {}

		protected override void EvaluateUsage_SemanticCoherency(XElement xElement) {}

		public override void OnStart()
		{
			_retries = 0;
			base.OnStart();
		}

		protected override Composite CreateMainBehavior()
		{
			return new ActionRunCoroutine(ctx => MainBehavior());
		}
		#endregion

		private const int ItemId_HearthStoneId = 6948;
		private const int ItemId_TheInnkeepersDaughter = 64488;
		private const int ItemId_GarrisonHearthStoneId = 110560;
		private const int MaxRetries = 5;

		private async Task<bool> MainBehavior()
		{
			if (_retries >= MaxRetries)
			{
				BehaviorDone(string.Format("We have reached our max number of tries ({0}) without successfully hearthing", MaxRetries));
				return true;
			}

			if (IsInHearthStoneArea)
			{
				BehaviorDone("Toon is already in hearthstone area");
				return true;
			}

			var hearthStones = UseGarrisonHearthstone
				?  GetHearthStonesByIds(ItemId_GarrisonHearthStoneId)
				: GetHearthStonesByIds(ItemId_HearthStoneId, ItemId_TheInnkeepersDaughter);

			if (!hearthStones.Any())
			{
				BehaviorDone("No hearthstones found in bag.");
				return true;
			}

			if (!UseGarrisonHearthstone && Me.HearthstoneAreaId == 0)
			{
				// I can only see this occurring if using the Innkeeper's Daughter hearthtone since the normal hearthstone
				// only shows up in bags if hearth has been set. 
				QBCLog.DeveloperInfo("Hearth has not been set");
				return true;
			}

			var usableHearthstone = hearthStones.FirstOrDefault(i => !i.Effects.First().Spell.Cooldown);
			if (usableHearthstone == null)
			{
				if (WaitOnCd)
				{
					TreeRoot.StatusText = "Waiting for hearthstone cooldown";
					return true;
				}

				BehaviorDone("Hearthstone is on cooldown");
				return true;
			}

			if (await CommonCoroutines.LandAndDismount())
				return true;

			if (await CommonCoroutines.StopMoving())
				return true;

			var hearthstoneSpell = usableHearthstone.Effects.First().Spell;
			using (var castMonitor = SpellCastMonitor.Start(hearthstoneSpell.Id))
			{
				TreeRoot.StatusText = string.Format("Using {0} with {1} out of {2} tries left", hearthstoneSpell.Name, ++_retries, MaxRetries);
				usableHearthstone.UseContainerItem();
				var castResult = await castMonitor.GetResult(12000);
				if (castResult == SpellCastResult.Succeeded)
				{
					await Coroutine.Wait(2000, () => IsInHearthStoneArea);
					BehaviorDone("Successfully casted Hearthstone");
					return true;
				}

				string reason = castResult == SpellCastResult.UnknownFail ? castMonitor.FailReason : castResult.ToString();

				QBCLog.Warning("Cast of {0} failed. Reason: {1}", hearthstoneSpell.Name, reason);
			}
			return false;
		}

		private bool IsInHearthStoneArea
		{
			get
			{
				if (UseGarrisonHearthstone)
					return Me.CurrentMap.IsGarrison;

				return Me.HearthstoneAreaId == Me.SubZoneId;
			}
		}

		private List<WoWItem> GetHearthStonesByIds(params uint[] hearthstoneIds)
		{
			return Me.BagItems.Where(i => i != null && i.ItemInfo != null
				&& hearthstoneIds.Contains(i.Entry)).ToList();
		}
	}
}

