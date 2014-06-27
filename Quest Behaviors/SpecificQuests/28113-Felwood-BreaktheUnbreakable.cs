﻿//
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

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.BreaktheUnbreakable
{
	[CustomBehaviorFileName(@"SpecificQuests\28113-Felwood-BreaktheUnbreakable")]
	public class _28113 : CustomForcedBehavior
	{
		public _28113(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			QuestId = 28113;
			touchdown = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
		}

		public int QuestId { get; set; }

		private Composite _root;
		// Private variables for internal state
		private bool _isBehaviorDone;
		private bool _isDisposed;


		private WoWItem ClawThing
		{
			get { return StyxWoW.Me.BagItems.FirstOrDefault(r => r.Entry == 63031); }
		}

		private bool touched;
		private WoWPoint touchdown;

		protected Composite CreateBehavior_QuestbotMain()
		{

			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet,
				new Decorator(r => StyxWoW.Me.IsCasting, new ActionAlwaysSucceed()),
				new Decorator(r => Firewall != null && Firewall.Distance < 10, new Action(r => { Navigator.PlayerMover.MoveStop();ClawThing.Use(); })),
				new Decorator(r => !touched && touchdown.Distance(StyxWoW.Me.Location) < 5, new Action(r => touched = true)),
				new Decorator(r => !touched, new Action(r => WoWMovement.ClickToMove(touchdown)))
				)));
		}


		private WoWGameObject Firewall
		{
			get { return ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(r => r.Entry == 206625 && r.FlagsUint == 32); }
		}


		public void Dispose(bool isExplicitlyInitiatedDispose)
		{

			if (!_isDisposed)
			{
				// NOTE: we should call any Dispose() method for any managed or unmanaged
				// resource, if that resource provides a Dispose() method.

				// Clean up managed resources, if explicit disposal...
				if (isExplicitlyInitiatedDispose)
				{
					TreeHooks.Instance.RemoveHook("Questbot_Main", CreateBehavior_QuestbotMain());
				}

				// Clean up unmanaged resources (if any) here...
				TreeRoot.GoalText = string.Empty;
				TreeRoot.StatusText = string.Empty;

				// Call parent Dispose() (if it exists) here ...
				base.Dispose();
			}

			_isDisposed = true;
		}


		public Composite DoneYet
		{
			get
			{
				return
					new Decorator(ret => touched, new Action(delegate
					{
						TreeRoot.StatusText = "Finished!";
						_isBehaviorDone = true;
						return RunStatus.Success;
					}));

			}
		}


		public override bool IsDone
		{
			get
			{
				return _isBehaviorDone;
			}
		}


		public override void OnStart()
		{
			// This reports problems, and stops BT processing if there was a problem with attributes...
			// We had to defer this action, as the 'profile line number' is not available during the element's
			// constructor call.
			OnStart_HandleAttributeProblem();

			//TreeHooks.Instance.InsertHook("Combat_OOC", 0, Hook);
			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (!IsDone)
			{
				TreeHooks.Instance.InsertHook("Questbot_Main", 0, CreateBehavior_QuestbotMain());

				this.UpdateGoalText(QuestId);
			}
		}
	}
}
