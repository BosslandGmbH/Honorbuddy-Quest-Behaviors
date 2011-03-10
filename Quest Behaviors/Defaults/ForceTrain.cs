using System.Collections.Generic;
using System.Drawing;
using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Questing;

namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// ForceTrain by Nesox
    /// Allows you to force HB to go train spells. 
    /// ForceSetVendor can also be used to achieve this.
    /// ##Syntax##
    /// QuestId: Id of the quest. If 0 is specified it will run anways.
    /// </summary>
    public class ForceTrain : CustomForcedBehavior
    {
        #region Overrides of CustomForcedBehavior

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"QuestId",null}
        };

        public ForceTrain(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);
            uint questId;
            if (!uint.TryParse(Args["QuestId"], out questId))
            {
                Logging.Write(Color.Red, "Parsing attribute 'QuestId' in ForceTrain behavior failed! please check your profile!");
                TreeRoot.Stop();
            }

            QuestId = questId;
        }

        /// <summary>
        /// The id of the quest associated with this behavior.
        /// </summary>
        public uint QuestId { get; set; }

        public override void OnStart()
        {
            if (!IsDone || QuestId == 0)
            {
                LevelbotSettings.Instance.FindVendorsAutomatically = true;
                Vendors.ForceTrainer = true;
                _isDone = true;
            }
        }

        private bool _isDone;
        public override bool IsDone
        {
            get
            {
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

                return
                    _isDone ||
                    (quest != null && quest.IsCompleted) ||
                    quest == null;
            }
        }

        #endregion
    }
}
