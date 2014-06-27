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

using Honorbuddy.QuestBehaviorCore;
using Styx;
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
	public class _24817:CustomForcedBehavior
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
		public List<WoWGameObject> q24817controller
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWGameObject>().Where(ret => (ret.Entry == 202108 && !StyxWoW.Me.IsDead)).OrderBy(ret => ret.Distance).ToList();
			}
		}
		public List<WoWUnit> q24817_hammer
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>().Where(ret => (ret.Entry == 36682 && !StyxWoW.Me.IsDead)).OrderBy(ret => ret.Distance).ToList();
			}
		}
		public override bool IsDone
		{
			get
			{
				return (IsBehaviorDone);
			}
		}
		public override void OnStart()
		{
			OnStart_HandleAttributeProblem();
			if (!IsDone)
			{
				this.UpdateGoalText(QuestId);
			}
		}
		protected override Composite CreateBehavior()
		{
			return _root ?? (_root =
				new PrioritySelector(
					new Decorator(
						ret => !StyxWoW.Me.HasAura("Mechashark X-Steam"),
								new Sequence(
									new Action(ret => Navigator.MoveTo(q24817controller[0].Location)),
									new Action(ret => q24817controller[0].Interact()),
									new Sleep(5000)
									)),
					new Decorator(
						ret => q24817_hammer[0].IsAlive,
						new PrioritySelector(
							new Decorator(
								ret => StyxWoW.Me.CurrentTarget != q24817_hammer[0],
								new Sequence(
									new Action(ret => 
									{
										if (q24817_hammer.Count > 0 && q24817_hammer[0].Location.Distance(StyxWoW.Me.Location) > 45)
										{
											Navigator.MoveTo(q24817_hammer[0].Location);
											StyxWoW.Sleep(100);
										}
										if (q24817_hammer.Count > 0 && (q24817_hammer[0].Location.Distance(StyxWoW.Me.Location) <= 45))
										{
											while (!StyxWoW.Me.QuestLog.GetQuestById(24817).IsCompleted)
											{
												q24817_hammer[0].Face();
												q24817_hammer[0].Target();
												WoWMovement.Move(WoWMovement.MovementDirection.Backwards);
												StyxWoW.Sleep(200);
												WoWMovement.MoveStop(WoWMovement.MovementDirection.Backwards);
												Lua.DoString("CastPetAction(3)");
												Lua.DoString("CastPetAction(2)");
												Lua.DoString("CastPetAction(1)");
											}
										}
									}))))),
					 new Decorator(
						 ret => StyxWoW.Me.QuestLog.GetQuestById(24817).IsCompleted,
						 new Sequence(
							 new Action(ret => Lua.DoString("VehicleExit()")),
							 new Action(ret => IsBehaviorDone = true)))
					));

		}
	}
}
