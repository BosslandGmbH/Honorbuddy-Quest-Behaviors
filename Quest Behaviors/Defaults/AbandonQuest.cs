using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// AbandonQuest by Bobby53
    /// Allows you to abandon a quest in your quest log
    /// ##Syntax##
    /// QuestId: The id of the quest.
    /// Type: 
    ///     All:        abandon quest if its in log regardless of status
    ///     Failed:     abandon quest only if failed
    ///     Incomplete: abandon incomplete quests (failed and any not complete)  
    ///    
    ///     <CustomBehavior File="AbandonQuest" QuestId="25499" />
    ///     <CustomBehavior File="AbandonQuest" QuestId="25499" Type="All" />
    ///     <CustomBehavior File="AbandonQuest" QuestId="25499" Type="Failed" />
    ///     <CustomBehavior File="AbandonQuest" QuestId="25499" Type="Incomplete" />
    /// </summary>
    public class AbandonQuest : CustomForcedBehavior
    {
        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {
            {"QuestId",null},
            {"Type", null}
        };

        public enum AbandonType
        {
            All,
            Failed,
            Incomplete
        };

        public AbandonQuest(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                CheckForUnrecognizedAttributes(recognizedAttributes);

                bool error = false;

                uint questId;
                if (!uint.TryParse(Args["QuestId"], out questId))
                {
                    Logging.Write("Parsing attribute 'QuestId' in AbandonQuest behavior failed! please check your profile!");
                    error = true;
                }

                AbandonType type = AbandonType.Incomplete;
                if (Args.ContainsKey("Type"))
                {
                    type = (AbandonType )Enum.Parse(typeof(AbandonType ), Args["Type"], true);
                }

                if (error)
                    TreeRoot.Stop();

                QuestId = questId;
                Type = type;
            }
            catch (Exception ex)
            {
                Logging.Write("AbandonQuest failed");
                Logging.WriteException(ex);
            }
        }

        public uint QuestId { get; private set; }
        public AbandonType  Type { get; private set; }

        private bool _isDone;
        public override bool IsDone
        {
            get
            {
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
                return _isDone || quest == null ;
            }
        }

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
            if (quest == null)
                Logging.WriteDebug("AbandonQuest: cannot find quest {0}", QuestId);
            else if (quest != null && quest.IsCompleted && Type != AbandonType.All)
                Logging.WriteDebug("AbandonQuest: quest {0} is Complete!  skipping abandon", QuestId);
            else if (quest != null && !quest.IsFailed && Type == AbandonType.Failed)
                Logging.WriteDebug("AbandonQuest: quest {0} has not Failed!  skipping abandon", QuestId);
            else
            {
                TreeRoot.GoalText = string.Format("Abandoning quest: {0}", quest.Name);
                QuestLog ql = new QuestLog();
                ql.AbandonQuestById(QuestId);
                Logging.WriteDebug("AbandonQuest: quest {0} successfully abandoned", QuestId);
            }

            _isDone = true;
        }

    }
}