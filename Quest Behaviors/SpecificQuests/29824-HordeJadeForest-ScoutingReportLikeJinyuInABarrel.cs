// Behavior originally contributed by mastahg.
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Bots.Grind;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.ScoutingReportLikeJinyuInABarrel
{
	[CustomBehaviorFileName(@"SpecificQuests\29824-HordeJadeForest-ScoutingReportLikeJinyuInABarrel")]
	public class LikeJinyuinaBarrel : QuestBehaviorBase
	{
		public LikeJinyuinaBarrel(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = 29824;
				QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = QuestInLogRequirement.InLog;
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
			// Acquisition and checking of any sub-elements go here.
			// A common example:
			//     HuntingGrounds = HuntingGroundsType.GetOrCreate(Element, "HuntingGrounds", HuntingGroundCenter);
			//     IsAttributeProblem |= HuntingGrounds.IsAttributeProblem;

			// Let QuestBehaviorBase do basic initialization of the behavior, deal with bad or deprecated attributes,
			// capture configuration state, install BT hooks, etc.  This will also update the goal text.
			var isBehaviorShouldRun = OnStart_QuestBehaviorCore();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (isBehaviorShouldRun)
			{
				// Setup settings to prevent interference with your behavior --
				// These settings will be automatically restored by QuestBehaviorBase when Dispose is called
				// by Honorbuddy, or the bot is stopped.
				CharacterSettings.Instance.UseMount = false;

				// Setup the BehaviorFlags as needed --
				// These settings will be automatically restored by QuestBehaviorBase when Dispose is called
				// by Honorbuddy, or the bot is stopped.
				// Turns off anything that can interfer.
				LevelBot.BehaviorFlags &=
					~(BehaviorFlags.Combat | BehaviorFlags.FlightPath | BehaviorFlags.Rest | BehaviorFlags.Vendor | BehaviorFlags.Loot);
			}
		}

		protected override Composite CreateBehavior_CombatMain()
		{
			return new ActionRunCoroutine(ctx => MainCoroutine());
		}

		#endregion

		#region Behavior
		private bool _mount;
		private uint[] jinyu = new uint[] { 55793, 56701, 55791, 55711, 55709, 55710 };
		WoWPoint _phase2RelativePos = new WoWPoint(0, 0, -30);


		//<Vendor Name="Pearlfin Poolwatcher" Entry="55709" Type="Repair" X="-100.9809" Y="-2631.66" Z="2.150823" />
		//<Vendor Name="Pearlfin Poolwatcher" Entry="55711" Type="Repair" X="-130.8297" Y="-2636.422" Z="1.639656" />

		//209691 - sniper rifle
		public WoWGameObject Rifle
		{
			get { return ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(r => r.Entry == 209691); }
		}


		public List<WoWUnit> Enemies
		{
			get { return ObjectManager.GetObjectsOfType<WoWUnit>().Where(r => jinyu.Contains(r.Entry)).ToList(); }
		}


		public WoWUnit Barrel
		{
			get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(r => r.Entry == 55784); }
		}


		public async Task<bool> PhaseOneLogic()
		{
			if (Me.RelativeLocation == _phase2RelativePos)
				return false;
			WoWGameObject rifle = Rifle;
			if (!rifle.WithinInteractRange)
				return (await CommonCoroutines.MoveTo(rifle.Location)).IsSuccessful();
			await CommonCoroutines.StopMoving();
			rifle.Interact();
			await CommonCoroutines.SleepForRandomReactionTime();
			return true;
		}

		public async Task<bool> PhaseTwoLogic()
		{
			var barrel = Barrel;
			if (barrel != null)
			{
				barrel.Interact();
				await CommonCoroutines.SleepForLagDuration();
				return true;
			}

			var enemies = Enemies;
			if (enemies.Any())
			{
				foreach (var enemy in enemies)
				{
					if (!Query.IsViable(enemy))
						continue;
					enemy.Interact(true);
					await CommonCoroutines.SleepForRandomReactionTime();
				}
				return true;
			}
			return false;
		}


		private async Task<bool> MainCoroutine()
		{
			if (IsDone)
				return false;

			if (Me.IsQuestComplete(QuestId) || !Query.IsInVehicle())
			{
				QBCLog.Info("Finished!");
				CharacterSettings.Instance.UseMount = true;
				BehaviorDone();
				return true;
			}

			// We're always returning 'true'
			return await PhaseOneLogic() || await PhaseTwoLogic();
		}		

		#endregion
	}
}