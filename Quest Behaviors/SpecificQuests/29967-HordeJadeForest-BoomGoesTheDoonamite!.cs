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

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.BoomGoesTheDoonamite
{
	[CustomBehaviorFileName(@"SpecificQuests\29967-HordeJadeForest-BoomGoesTheDoonamite!")]
	public class BoomGoestheDoonamite : CustomForcedBehavior
	{
		~BoomGoestheDoonamite()
		{
			Dispose(false);
		}

		public BoomGoestheDoonamite(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = 29967;
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


		// Attributes provided by caller
		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }

		// Private variables for internal state
		private bool _isBehaviorDone;
		private bool _isDisposed;
		private Composite _root;


		// Private properties
		private LocalPlayer Me
		{
			get { return (StyxWoW.Me); }
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
					CharacterSettings.Instance.UseMount = _mount;
					TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_MainCombat());
				}

				// Clean up unmanaged resources (if any) here...
				TreeRoot.GoalText = string.Empty;
				TreeRoot.StatusText = string.Empty;

				// Call parent Dispose() (if it exists) here ...
				base.Dispose();
			}

			_isDisposed = true;
		}


		#region Overrides of CustomForcedBehavior

		public Composite DoneYet
		{
			get
			{
				return new Decorator(ret => Me.IsQuestComplete(QuestId),
					new Action(delegate
					{
						TreeRoot.StatusText = "Finished!";
						CharacterSettings.Instance.UseMount = true;
						_isBehaviorDone = true;
						return RunStatus.Success;
					}));
			}
		}


		public void CastSpell(string action)
		{

			var spell = StyxWoW.Me.PetSpells.FirstOrDefault(p => p.ToString() == action);
			if (spell == null)
				return;

			QBCLog.Info("[Pet] Casting {0}", action);
			Lua.DoString("CastPetAction({0})", spell.ActionBarIndex + 1);

		}


		//<Vendor Name="Pearlfin Poolwatcher" Entry="55709" Type="Repair" X="-100.9809" Y="-2631.66" Z="2.150823" />
		//<Vendor Name="Pearlfin Poolwatcher" Entry="55711" Type="Repair" X="-130.8297" Y="-2636.422" Z="1.639656" />

		//209691 - sniper rifle


		public WoWUnit Clutchpop
		{
			get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(r => r.Entry == 56525); }
		}


		public WoWUnit Enemy
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>().Where(r => r.Entry == 56603).OrderBy(r=>r.Distance).FirstOrDefault();
			}
		}


		WoWPoint spot = new WoWPoint(1368.113,-571.8212,339.3784);


		//<Vendor Name="Rivett Clutchpop" Entry="56525" Type="Repair" X="1368.113" Y="-571.8212" Z="339.3784" />
		public Composite PhaseOne
		{
			get
			{
				return new Decorator(r => !Query.IsInVehicle(),
					new PrioritySelector(
						new Decorator(r => Clutchpop == null || !Clutchpop.WithinInteractRange,
							new Action(r => Navigator.MoveTo(spot))),
						new Decorator(r => !GossipFrame.Instance.IsVisible,
							new Action(r => Clutchpop.Interact())),
						new Decorator(r => GossipFrame.Instance.IsVisible,
							new Action(r =>GossipFrame.Instance.SelectGossipOption(0)))
					));
			}
		}



		public Composite PhaseTwo
		{
			get
			{               
					return new Decorator(r => Enemy != null, new Action(r =>
																	  {

																		  CastSpell("Throw Methane Bomb");
																		  SpellManager.ClickRemoteLocation(
																			  Enemy.Location.RayCast(Enemy.MovementInfo.Heading,10));
																	  }
														   ));
			}

		}


		protected Composite CreateBehavior_MainCombat()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, PhaseOne,PhaseTwo, new ActionAlwaysSucceed())));
		}


		public override void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}


		public override bool IsDone
		{
			get
			{
				return (_isBehaviorDone     // normal completion
						|| !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
			}
		}

		private bool _mount;
		public override void OnStart()
		{
			// This reports problems, and stops BT processing if there was a problem with attributes...
			// We had to defer this action, as the 'profile line number' is not available during the element's
			// constructor call.
			OnStart_HandleAttributeProblem();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (!IsDone)
			{
				_mount = CharacterSettings.Instance.UseMount;
				CharacterSettings.Instance.UseMount = false;

				TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_MainCombat());

				this.UpdateGoalText(QuestId);
			}
		}
	   #endregion
	}
}