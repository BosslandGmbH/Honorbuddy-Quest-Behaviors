using System;
using System.Collections.Generic;

using Styx.Logic.BehaviorTree;
using Styx.Logic.Questing;


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
                AbandonType     abandonType;
                int             questId;

                CheckForUnrecognizedAttributes(new Dictionary<string, object>()
                                                {
                                                    { "QuestId",    null},
                                                    { "Type",       null},
                                                });

                _isAttributesOkay = true;
                _isAttributesOkay &= GetAttributeAsInteger("QuestId", true, "0", 0, int.MaxValue, out questId);
                _isAttributesOkay &= GetAttributeAsEnum<AbandonType>("Type", true, AbandonType.Incomplete, out abandonType);
                
                if (_isAttributesOkay)
                {
                    QuestId = (uint)questId;
                    Type = abandonType;
                }
            }

			catch (Exception except)
			{
				// Maintenance problems occur for a number of reasons.  The primary two are...
				// * Changes were made to the behavior, and boundary conditions weren't properly tested.
				// * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
				// In any case, we pinpoint the source of the problem area here, and hopefully it
				// can be quickly resolved.
				UtilLogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
										+ "\nFROM HERE:\n"
										+ except.StackTrace + "\n");
				_isAttributesOkay = false;
			}
        }

        public uint         QuestId { get; private set; }
        public AbandonType  Type { get; private set; }

        private bool    _isAttributesOkay;
        private bool    _isBehaviorDone;

        
        #region Overrides of CustomForcedBehavior

        public override bool IsDone
        {
            get
            {
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
                return (_isBehaviorDone || (quest == null));
            }
        }


        public override void OnStart()
        {
            if (!_isAttributesOkay)
			{
				UtilLogMessage("error", "Stopping Honorbuddy.  Please repair the profile!");

                // *Never* want to stop Honorbuddy (e.g., TreeRoot.Stop()) in the constructor --
                // This would defeat the "ProfileDebuggingMode" configurable that builds an instance of each
                // used behavior when the profile is loaded.
				TreeRoot.Stop();
			}

            else
            {
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

                if (quest == null)
                    { UtilLogMessage("error", string.Format("Cannot find quest {0}.", QuestId)); }

                else if ((quest != null)  &&  quest.IsCompleted  &&  (Type != AbandonType.All))
                    { UtilLogMessage("warning", string.Format("Quest \"{0}\"(id: {1}) is Complete!  Skipping abandon.", quest.Name, QuestId)); }

                else if ((quest != null)  &&  !quest.IsFailed  &&  (Type == AbandonType.Failed))
                    { UtilLogMessage("error", string.Format("Quest \"{0}\"(id: {1}) has not Failed!  Skipping abandon.", quest.Name, QuestId)); }

                else
                {
                    TreeRoot.GoalText = string.Format("Abandoning quest: \"{0}\"", quest.Name);
                    QuestLog ql = new QuestLog();
                    ql.AbandonQuestById(QuestId);
                    UtilLogMessage("info", string.Format("Quest \"{0}\"(id: {1}) successfully abandoned", quest.Name, QuestId));
                }

                _isBehaviorDone = true;
            }
        }

        #endregion
    }
}