using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CommonBehaviors.Actions;
using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;

using Action = TreeSharp.Action;
using Styx.Logic.Inventory.Frames.Gossip;
using System;
using Styx.Logic;

namespace Styx.Bot.Quest_Behaviors
{
	public class RemoveUnitsFromTargeting : CustomForcedBehavior
	{
		readonly Dictionary<string, object> _recognizedAttributes = new Dictionary<string, object>()
        {
            {"QuestId",null},
        };

		private readonly bool _success = true;

		public RemoveUnitsFromTargeting(Dictionary<string, string> args)
			: base(args)
		{
			CheckForUnrecognizedAttributes(_recognizedAttributes);

			int questId = 0;
			_success = _success && GetAttributeAsInteger("QuestId", false, "0", int.MinValue, int.MaxValue, out questId);

			if (!_success)
			{
				Logging.Write(Color.Red, "Error parsing tag for AnEndToAllThings. {0}", Element);
				TreeRoot.Stop();
			}

			QuestId = (uint)questId;
		}

		public uint QuestId { get; private set; }
		private static LocalPlayer Me { get { return StyxWoW.Me; } }
		
		#region Overrides of CustomForcedBehavior

		private Composite _root;
		protected override Composite CreateBehavior()
		{
			return _root ?? (_root =
				new PrioritySelector(

					));
		}

		public override bool IsDone
		{
			get
			{
				var quest = Me.QuestLog.GetQuestById(QuestId);
				return quest == null || quest.IsCompleted;
			}
		}

		public override void Dispose()
		{
			Targeting.Instance.RemoveTargetsFilter -= Instance_RemoveTargetsFilter;
		}

		public override void OnStart()
		{
			Targeting.Instance.RemoveTargetsFilter += Instance_RemoveTargetsFilter;
		}

		void Instance_RemoveTargetsFilter(List<WoWObject> units)
		{
			units.Clear();
		}

		#endregion
	}
}
