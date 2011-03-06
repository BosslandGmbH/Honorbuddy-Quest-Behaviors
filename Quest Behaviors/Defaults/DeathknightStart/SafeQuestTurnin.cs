using System.Collections.Generic;
using System.Drawing;
using QuestBot.QuestOrder;
using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;

namespace Styx.Bot.Quest_Behaviors.DeathknightStart
{
    public class SafeQuestTurnin : CustomForcedBehavior
    {
        readonly Dictionary<string, object> _recognizedAttributes = new Dictionary<string, object>()
        {
            {"QuestId",null},
            {"QuestName",null},
            {"TurnInName",null},
            {"TurnInId",null},
            {"X",null},
            {"Y",null},
            {"Z",null},
        };

        private readonly bool _success = true;

        public SafeQuestTurnin(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(_recognizedAttributes);
            int questId = 0, turninId = 0;
            string questName = "", turninName = "";
            float x = 0, y = 0, z = 0;

            _success = _success && GetAttributeAsInteger("QuestId", true, "0", 0, int.MaxValue, out questId);
            _success = _success && GetAttributeAsInteger("TurnInId", true, "0", 0, int.MaxValue, out turninId);
            
            _success = _success && GetAttributeAsString("QuestName", true, "", out questName);
            _success = _success && GetAttributeAsString("TurnInName", true, "", out turninName);

            _success = _success && GetAttributeAsFloat("X", true, "0", float.MinValue, float.MaxValue, out x);
            _success = _success && GetAttributeAsFloat("Y", true, "0", float.MinValue, float.MaxValue, out y);
            _success = _success && GetAttributeAsFloat("Z", true, "0", float.MinValue, float.MaxValue, out z);

            QuestTurnIn = new ForcedQuestTurnIn((uint)questId, questName, (uint)turninId, new WoWPoint(x, y, z));

            if (!_success || QuestTurnIn == null)
            {
                Logging.Write(Color.Red, "Error parsing tag for SafeQuestTurnin. {0}", Element);
                TreeRoot.Stop();
            }
        }

        public ForcedQuestTurnIn QuestTurnIn { get; private set; }

        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior() { return QuestTurnIn.Branch; }

        /// <summary>Gets a value indicating whether this object is done.</summary>
        /// <value>true if this object is done, false if not.</value>
        public override bool IsDone { get { return QuestTurnIn.IsDone; } }

        public override void Dispose()
        {
            Targeting.Instance.RemoveTargetsFilter -= Instance_RemoveTargetsFilter;
            QuestTurnIn.Dispose();
        }

        public override void OnStart()
        {
            Targeting.Instance.RemoveTargetsFilter += Instance_RemoveTargetsFilter;
            QuestTurnIn.OnStart();
        }

        private static void Instance_RemoveTargetsFilter(List<WoWObject> units)
        {
            units.Clear();
        }

        #endregion
    }
}
