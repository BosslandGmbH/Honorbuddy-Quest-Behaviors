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

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.PompfruitPickup
{
	[CustomBehaviorFileName(@"SpecificQuests\30231-VOEB-PompfruitPickup")]
	public class Blastranaar : CustomForcedBehavior
	{
		public Blastranaar(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = 30231;//GetAttributeAsQuestId("QuestId", true, null) ?? 0;
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
		public int QuestId { get; set; }
		private bool _isBehaviorDone;
		public int MobIdPomfruit = 58767;
		public int PomharvestFireworkId = 79344;
		private Composite _root;

		public override bool IsDone
		{
			get
			{
				return _isBehaviorDone;
			}
		}
		private LocalPlayer Me
		{
			get { return (StyxWoW.Me); }
		}

		public override void OnStart()
		{
			OnStart_HandleAttributeProblem();
			if (!IsDone)
			{
				this.UpdateGoalText(QuestId);
			}
		}

		public List<WoWUnit> Pomfruit
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == MobIdPomfruit && !u.IsDead && u.Distance < 10000).OrderBy(u => u.Distance).ToList();
			}
		}

		public WoWItem PomharvestFirework { get { return (StyxWoW.Me.CarriedItems.FirstOrDefault(i => i.Entry == PomharvestFireworkId)); } }


		public Composite DoneYet
		{
			get
			{
				return new Decorator(ret => Me.IsQuestObjectiveComplete(QuestId, 1),
					new Action(delegate
					{
						TreeRoot.StatusText = "Finished!";
						_isBehaviorDone = true;
						return RunStatus.Success;
					}));
			}
		}


		public Composite PomfruitFlyTo
		{
			get
			{
				return new Decorator(ret => !Me.IsQuestObjectiveComplete(QuestId, 1),
					new Action(c =>
					{
						if (Pomfruit[0].Location.Distance(Me.Location) > 0)
						{
							if (PomharvestFirework.Cooldown == 0)
							{
								PomharvestFirework.UseContainerItem();
								StyxWoW.Sleep(200);
							}

							TreeRoot.StatusText = "Moving to Pomfruit";
							Flightor.MoveTo(Pomfruit[0].Location);
											StyxWoW.Sleep(200);
							Pomfruit[0].Interact();

							if (Me.IsQuestObjectiveComplete(QuestId, 1))
							{
								TreeRoot.StatusText = "Finished!";
								_isBehaviorDone = true;
								return RunStatus.Success;
							}
						}
					return RunStatus.Running;
					}));
			}
		}

		
		protected override Composite CreateBehavior()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, PomfruitFlyTo, new ActionAlwaysSucceed())));
		}
	}
}
