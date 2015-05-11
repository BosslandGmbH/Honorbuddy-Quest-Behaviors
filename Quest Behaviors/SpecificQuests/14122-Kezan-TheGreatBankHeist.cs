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
#endregion


#region Examples
#endregion


#region Usings

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Bots.Grind;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
using Extensions = Styx.Common.Extensions;

#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.TheGreatBankHeist
{
	[CustomBehaviorFileName(@"SpecificQuests\14122-Kezan-TheGreatBankHeist")]
	public class _14122 : QuestBehaviorBase
	{
		public _14122(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			QuestId = 14122;
		}

		private WoWPoint wp = new WoWPoint(-8373.504, 1725.106, 39.94993);
		private int _petAbilityIndex;

		private readonly string[] _bossWhisperIcons =
		{
			// Amazing G-Ray
			"INV_Misc_EngGizmos_20.blp",
			// Blastcrackers
			"INV_Misc_Bomb_07.blp",
			// Ear-O-Scope!
			"INV_Misc_Ear_NightElf_02.blp",
			// Infinifold Lockpick
			"INV_Misc_EngGizmos_swissArmy.blp",
			// Kaja'mite Drill
			"INV_Weapon_ShortBlade_21.blp"
		};

		#region Overrides of QuestBehaviorBase

		protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
		{
			//// EXAMPLE: 
			//UsageCheck_DeprecatedAttribute(xElement,
			//    Args.Keys.Contains("Nav"),
			//    "Nav",
			//    context => string.Format("Automatically converted Nav=\"{0}\" attribute into MovementBy=\"{1}\"."
			//                              + "  Please update profile to use MovementBy, instead.",
			//                              Args["Nav"], MovementBy));
		}

		protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
		{
			//// EXAMPLE:
			//UsageCheck_SemanticCoherency(xElement,
			//    (!MobIds.Any() && !FactionIds.Any()),
			//    context => "You must specify one or more MobIdN, one or more FactionIdN, or both.");
			//
			//const double rangeEpsilon = 3.0;
			//UsageCheck_SemanticCoherency(xElement,
			//    ((RangeMax - RangeMin) < rangeEpsilon),
			//    context => string.Format("Range({0}) must be at least {1} greater than MinRange({2}).",
			//                  RangeMax, rangeEpsilon, RangeMin)); 
		}

		public override void OnStart()
		{
			// Let QuestBehaviorBase do basic initialization of the behavior, deal with bad or deprecated attributes,
			// capture configuration state, install BT hooks, etc.  This will also update the goal text.
			var isBehaviorShouldRun = OnStart_QuestBehaviorCore();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (isBehaviorShouldRun)
			{
				Lua.Events.AttachEvent("CHAT_MSG_RAID_BOSS_WHISPER", BossWhisperHandler);

				// Disable anything that can interfer with behavior.
				// These settings will be automatically restored by QuestBehaviorBase when Dispose is called
				// by Honorbuddy, or the bot is stopped.
				LevelBot.BehaviorFlags &= ~(BehaviorFlags.Combat | BehaviorFlags.Loot | BehaviorFlags.FlightPath | BehaviorFlags.Vendor);
			}
		}


		public override void OnFinished()
		{
			Lua.Events.DetachEvent("CHAT_MSG_RAID_BOSS_WHISPER", BossWhisperHandler);

			if (StyxWoW.Me.HasAura("Vault Cracking Toolset"))
				Lua.DoString("VehicleExit()");

			base.OnFinished();
		}

		protected override Composite CreateMainBehavior()
		{
			return new ActionRunCoroutine(ctx => MainCoroutine());
		}

		#endregion

		public WoWGameObject Bank
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWGameObject>()
					.Where(ctx => ctx.Entry == 195449)
					.OrderBy(ctx => ctx.DistanceSqr).FirstOrDefault();
			}
		}

		private const int AuraId_VaultCrackingToolset = 67476;

		async Task<bool> MainCoroutine()
		{
			if (IsDone)
				return false;

			if (StyxWoW.Me.Location.DistanceSqr(wp) > 5 * 5)
			{
				TreeRoot.StatusText = "Moving to location";
				Navigator.MoveTo(wp);
				return true;
			}

			if (!StyxWoW.Me.HasAura(AuraId_VaultCrackingToolset))
			{
				Bank.Interact();
				await Coroutine.Sleep((int)Delay.LagDuration.TotalMilliseconds);
				return true;
			}

			if (_petAbilityIndex > 0)
			{
				await Coroutine.Sleep((int)Delay.BeforeButtonClick.TotalMilliseconds);
				Lua.DoString("CastPetAction({0})", _petAbilityIndex);
				_petAbilityIndex = 0;
				return true;
			}

			return false;
		}

		public void BossWhisperHandler(object sender, LuaEventArgs arg)
		{
			var msg = arg.Args[0].ToString();
			var match = _bossWhisperIcons.FirstOrDefault(msg.Contains);
			_petAbilityIndex = match != null ? (Extensions.IndexOf(_bossWhisperIcons, match) + 1) : 0;
		}
	}
}
