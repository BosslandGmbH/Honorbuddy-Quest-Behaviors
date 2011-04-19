﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using CommonBehaviors.Actions;

using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    class PerformTradeskillOn : CustomForcedBehavior
    {
        public PerformTradeskillOn(Dictionary<string, string> args) : base(args)
        {
			try
			{
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                CastOnItemId    = GetAttributeAsItemId("CastOnItemId", false, null) ?? 0;
                NumOfTimes      = GetAttributeAsInteger("NumOfTimes", false, 1, 1000, new [] { "NumTimes" }) ?? 1;
                QuestId         = GetAttributeAsQuestId("QuestId", true, null) ?? 0;
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;
                TradeSkillId    = GetAttributeAsSpellId("TradeSkillId", true, null) ?? 0;
                TradeSkillItemId = GetAttributeAsItemId("TradeSkillItemId", true, null) ?? 0;
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
				IsAttributeProblem = true;
			}
        }


        public int?                     CastOnItemId { get; private set; }  /// If set, an item ID to cast the trade skill on.
        public int                      NumOfTimes { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }
        public int                      TradeSkillId { get; private set; }
        public int                      TradeSkillItemId { get; private set; }  // Identifier for the trade skill item. E.g; the actual 'item' we use from the tradeskill window.


        private bool        _isBehaviorDone;


        private void PerformTradeSkill()
        {
            Lua.DoString("DoTradeSkill(" + GetTradeSkillIndex() + ", " + (NumOfTimes == 0 ? 1 : NumOfTimes) + ")");
            Thread.Sleep(500);

            if (CastOnItemId.HasValue)
            {
                var item = StyxWoW.Me.CarriedItems.FirstOrDefault(i => i.Entry == CastOnItemId.Value);
                if (item == null)
                {
                    UtilLogMessage("fatal", string.Format("Could not find ItemId({0}) for {1}.", CastOnItemId.Value, GetType().Name));
                    TreeRoot.Stop();
                    return;
                }
                item.Use();
                Thread.Sleep(500);
            }

            if (Lua.GetReturnVal<bool>("return StaticPopup1:IsVisible()", 0))
                Lua.DoString("StaticPopup1Button1:Click()");

            Thread.Sleep(500);

            while(StyxWoW.Me.IsCasting)
            {
                Thread.Sleep(100);
            }

            _isBehaviorDone = true;
        }



        private Composite CreateTradeSkillCast()
        {
            return 
                new PrioritySelector(
                    new Decorator(ret => Lua.GetReturnVal<bool>("return StaticPopup1:IsVisible()", 0),
                        new Action(ret => Lua.DoString("StaticPopup1Button1:Click()"))
                    ),
                
                    new Decorator(ret=>!Lua.GetReturnVal<bool>("return TradeSkillFrame:IsVisible()", 0),
                        new Action(ret=>WoWSpell.FromId((int)TradeSkillId).Cast())),

                    new Decorator(ret=>StyxWoW.Me.IsCasting,
                        new ActionAlwaysSucceed()),

                new Action(ret=>PerformTradeSkill()));
        }


        private int GetTradeSkillIndex()
        {
            using (new FrameLock())
            {
                int count = Lua.GetReturnVal<int>("return GetNumTradeSkills()", 0);
                for (int i = 1; i <= count; i++)
                {
                    string link = Lua.GetReturnVal<string>("return GetTradeSkillItemLink(" + i + ")", 0);

                    // Make sure it's not a category!
                    if (string.IsNullOrEmpty(link))
                    {
                        continue;
                    }

                    link = link.Remove(0, link.IndexOf(':') + 1);
                    if (link.IndexOf(':') != -1)
                        link = link.Remove(link.IndexOf(':'));
                    else
                        link = link.Remove(link.IndexOf('|'));

                    int id = int.Parse(link);

                    UtilLogMessage("debug", string.Format("ID: " + id + " at " + i + " - " + WoWSpell.FromId(id).Name));

                    if (id == TradeSkillItemId)
                        return i;
                }
            }
            return 0;
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return new PrioritySelector(
                new Decorator(ret=>StyxWoW.Me.IsMoving,
                    new Action(ret=>Navigator.PlayerMover.MoveStop())),

                CreateTradeSkillCast());
        }


        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone     // normal completion
                        || !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
            }
        }


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
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = this.GetType().Name + ": " + ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
            }
		}

        #endregion
    }
}
