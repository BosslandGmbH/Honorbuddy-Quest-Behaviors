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
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.AGoblininSharksClothing
{
	[CustomBehaviorFileName(@"SpecificQuests\24817-LostIsles-AGoblininSharksClothing")]
	public class _24817:QuestBehaviorBase
	{
		public _24817(Dictionary<string, string> Args)
			: base(Args)
		{
			QBCLog.BehaviorLoggingContext = this;

			QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
		}

		public int QuestId { get; set; }
		private bool IsBehaviorDone = false;
		private Composite _root;
		public WoWGameObject Controller
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWGameObject>()
						.Where(ret => (ret.Entry == 202108 ))
						.OrderBy(ret => ret.DistanceSqr)
						.FirstOrDefault();
			}
		}

		public WoWUnit Hammer
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>()
						.Where(ret => (ret.Entry == 36682 ))
						.OrderBy(ret => ret.DistanceSqr)
						.FirstOrDefault();
			}
		}

		protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement) {}

		protected override void EvaluateUsage_SemanticCoherency(XElement xElement) {}

		protected override Composite CreateMainBehavior()
		{
			return _root ?? (_root = new ActionRunCoroutine(ctx => MainCoroutine()));
		}

		private const int AuraId_MechasharkXSteam = 71661;

		protected async Task<bool> MainCoroutine()
		{
			if (IsDone)
				return false;
			if (!StyxWoW.Me.HasAura(AuraId_MechasharkXSteam))
			{
				var controler = Controller;
				if (controler == null)
				{
					QBCLog.Fatal("Controler could not be found in ObjectManager");
					return true;
				}
				if (!controler.WithinInteractRange)
					return (await CommonCoroutines.MoveTo(controler.Location)).IsSuccessful();

				if (await CommonCoroutines.StopMoving())
					return true;

				controler.Interact();
				await Coroutine.Sleep(5000);
				return true;
			}

			var hammer = Hammer;
			if (hammer == null)
			{
				QBCLog.Fatal("Hammer could not be found in ObjectManager");
				return true;
			}
			if (hammer.IsAlive && StyxWoW.Me.CurrentTarget != hammer)
			{
				await DoQuest(hammer);
				return true;
			}
			if (StyxWoW.Me.QuestLog.GetQuestById(24817).IsCompleted)
			{
				Lua.DoString("VehicleExit()");
				IsBehaviorDone = true;
				return true;
			}
			return false;
		}

	    private async Task DoQuest(WoWUnit hammer)
		{
			// make sure bot does not try to handle combat or anything else that can interrupt with quest behavior.
			LevelBot.BehaviorFlags &= ~(BehaviorFlags.Combat | BehaviorFlags.Loot | BehaviorFlags.FlightPath | BehaviorFlags.Vendor);

			if (hammer.DistanceSqr > 45 * 45)
            {
				Navigator.MoveTo(hammer.Location);
                await Coroutine.Sleep(100);
            }
            else
            {
				while (!StyxWoW.Me.QuestLog.GetQuestById(24817).IsCompleted && StyxWoW.Me.IsAlive && Query.IsViable(hammer))
                {
					if (StyxWoW.Me.CurrentTargetGuid != hammer.Guid)
                    {
						hammer.Target();
                        await CommonCoroutines.SleepForLagDuration();
                        continue;
                    }

					if (!StyxWoW.Me.IsSafelyFacing(hammer))
                    {
						hammer.Face();
	                    await Coroutine.Wait(2000, () =>!Query.IsViable(hammer) || StyxWoW.Me.IsSafelyFacing(hammer));
                    } 

                    try
                    {
                        WoWMovement.Move(WoWMovement.MovementDirection.Backwards);
                        await Coroutine.Sleep(200);
                    }
                    finally
                    {
                        WoWMovement.MoveStop(WoWMovement.MovementDirection.Backwards);
                    }

	                if (CastPetAction(3) || CastPetAction(2) || CastPetAction(1))
		                await CommonCoroutines.SleepForRandomReactionTime();

                    await Coroutine.Yield();
	                hammer = Hammer;
                }
            }
	    }

		private bool CastPetAction(int buttonSlot)
		{
			var lua = string.Format("if GetPetActionCooldown({0}) == 0 then CastPetAction({0}) return true end return false", buttonSlot);
			return Lua.GetReturnVal<bool>(lua, 0);
		}
	}
}
