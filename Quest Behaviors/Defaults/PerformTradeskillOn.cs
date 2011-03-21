using System;
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
                int castOnItemId;

                CheckForUnrecognizedAttributes(new Dictionary<string, object>()
                                                {
                                                    { "CastOnItemId",       null },
                                                    { "NumTimes",           null },
                                                    { "QuestId",            null },
                                                    { "TradeSkillId",       null },
                                                    { "TradeSkillItemId",   null },
                                                });

                _isAttributesOkay = true;
                _isAttributesOkay &= GetAttributeAsInteger("QuestId", true, "0", 0, int.MaxValue, out QuestId);
                _isAttributesOkay &= GetAttributeAsInteger("TradeSkillId", true, "0", 0, int.MaxValue, out TradeSkillId);
                _isAttributesOkay &= GetAttributeAsInteger("TradeSkillItemId", true, "0", 0, int.MaxValue, out TradeSkillItemId);
                _isAttributesOkay &= GetAttributeAsInteger("NumTimes", false, "1", 1, int.MaxValue, out NumTimes);
                _isAttributesOkay &= GetAttributeAsInteger("CastOnItemId", false, "0", 0, int.MaxValue, out castOnItemId);

                // Semantic coherency --
                if (_isAttributesOkay)
                {
                    if (QuestId == 0)
                    {
                        UtilLogMessage("error", "\"QuestId\" may not be zero");
                        _isAttributesOkay = false;
                    }

                    if (TradeSkillId == 0)
                    {
                        UtilLogMessage("error", "\"TradeSkillId\" may not be zero");
                        _isAttributesOkay = false;
                    }

                    if (TradeSkillItemId == 0)
                    {
                        UtilLogMessage("error", "\"TradeSkillItemId\" may not be zero");
                        _isAttributesOkay = false;
                    }
                }


                if (_isAttributesOkay)
                {
                    if (castOnItemId > 0)
                        { CastOnItemId = castOnItemId; }
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

        public int      QuestId;

        /// <summary> Identifier for the trade skill </summary>
        public int      TradeSkillId;

        /// <summary> Identifier for the trade skill item. E.g; the actual 'item' we use from the tradeskill window. </summary>
        public int      TradeSkillItemId;

        /// <summary> If set, an item ID to cast the trade skill on. </summary>
        public int?     CastOnItemId;
        
        /// <summary> Number of times </summary>
        public int      NumTimes;


        private bool        _isAttributesOkay;
        private bool        _isBehaviorDone;


        private void PerformTradeSkill()
        {
            Lua.DoString("DoTradeSkill(" + GetTradeSkillIndex() + ", " + (NumTimes == 0 ? 1 : NumTimes) + ")");
            Thread.Sleep(500);

            if (CastOnItemId.HasValue)
            {
                var item = StyxWoW.Me.CarriedItems.FirstOrDefault(i => i.Entry == CastOnItemId.Value);
                if (item == null)
                {
                    UtilLogMessage("error", "COULD NOT FIND ITEM FOR " + GetType().Name 
                                            + "! Aborting and stopping bot! [Item: " + CastOnItemId.Value + "]");
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

                    Logging.WriteDebug("ID: " + id + " at " + i + " - " + WoWSpell.FromId(id).Name);
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
                return (_isBehaviorDone    // normal completion
                        ||  !UtilIsProgressRequirementsMet((int)QuestId, 
                                                           QuestInLogRequirement.InLog, 
                                                           QuestCompleteRequirement.NotComplete));
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

            else if (!IsDone)
            {
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = string.Format("{0}: {1}",
                                                  this.GetType().Name,
                                                  (quest == null) ? "Running" : ("\"" + quest.Name + "\""));
            }
		}

        #endregion
    }
}
