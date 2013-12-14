// Behavior originally contributed by Unknown.
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
using System.Xml.Linq;
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


namespace Honorbuddy.Quest_Behaviors.PerformTradeskillOn
{
    [CustomBehaviorFileName(@"PerformTradeskillOn")]
    class PerformTradeskillOn : CustomForcedBehavior
	{
		#region Constructor and Argument Processing
		public PerformTradeskillOn(Dictionary<string, string> args)
            : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;

                CastOnItemId = GetAttributeAsNullable<int>("CastOnItemId", false, ConstrainAs.ItemId, null) ;
                NumOfTimes = GetAttributeAsNullable<int>("NumOfTimes", false, ConstrainAs.RepeatCount, new[] { "NumTimes" }) ?? 1;
                TradeSkillId = GetAttributeAsNullable<int>("TradeSkillId", true, ConstrainAs.SpellId, null) ?? 0;
                TradeSkillItemId = GetAttributeAsNullable<int>("TradeSkillItemId", true, ConstrainAs.ItemId, null) ?? 0;
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
        public int? CastOnItemId { get; private set; }  /// If set, an item ID to cast the trade skill on.
        public int NumOfTimes { get; private set; }
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }
        public int TradeSkillId { get; private set; }
        public int TradeSkillItemId { get; private set; }  // Identifier for the trade skill item. E.g; the actual 'item' we use from the tradeskill window.
		 #endregion

        // Private variables for internal state
        private bool _isBehaviorDone;
	    private WoWSpell _tradeskillSpell;
	    private bool _isTradeskillOpen;
	    private int _numOfCasts;
        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id$"); } }
        public override string SubversionRevision { get { return ("$Revision$"); } }
   
	    public override void OnFinished()
	    {
			Lua.Events.DetachEvent("TRADE_SKILL_SHOW", OnTradeSkillOpen);
			Lua.Events.DetachEvent("TRADE_SKILL_CLOSE", OnTradeSkillClose);
			TreeRoot.GoalText = string.Empty;
			TreeRoot.StatusText = string.Empty;
	    }

        private Composite CreateTradeSkillCast()
        {
            return
                new PrioritySelector(
					new Decorator(ctx => _numOfCasts >= NumOfTimes, new Action(ctx => _isBehaviorDone = true)),

					new Decorator(ret => !IsTradeskillFrameShown,
						new Sequence(
							new Action(ctx => _tradeskillSpell.Cast()),
							new Sleep(1000))),

                    new Decorator(ret => StyxWoW.Me.IsCasting,
                        new ActionAlwaysSucceed()),

					new Sequence( ctx => GetTradeSkillIndex(),

						// check we have the material to craft recipe
						new DecoratorContinue(ctx => GetMaxRepeat((int)ctx) == 0,
							new Action(
								ctx =>
								{
									_isBehaviorDone = true;
									return RunStatus.Failure;
								})),

						new Action(ctx => Lua.DoString("DoTradeSkill({0}, {1})", (int)ctx, 1)),
						new WaitContinue(TimeSpan.FromMilliseconds(500), ctx => StyxWoW.Me.IsCasting, new ActionAlwaysSucceed()),

						new DecoratorContinue(ctx => CastOnItemId.HasValue,
							new Sequence( ctx =>  StyxWoW.Me.CarriedItems.FirstOrDefault(i => i.Entry == CastOnItemId.Value),
								new DecoratorContinue(ctx => ctx == null, 
									new Action(ctx => QBCLog.Fatal("Could not find ItemId({0}).", CastOnItemId.Value))),
								new Action(ctx => ((WoWItem)ctx ).Use()),
								new WaitContinue(TimeSpan.FromMilliseconds(500), ctx => StyxWoW.Me.IsCasting, new ActionAlwaysSucceed()))),

						new DecoratorContinue(ctx => Lua.GetReturnVal<bool>("return StaticPopup1:IsVisible()", 0),
							new Sequence(
								new Action(ctx => Lua.DoString("StaticPopup1Button1:Click()")),
								new WaitContinue(TimeSpan.FromMilliseconds(500), ctx => StyxWoW.Me.IsCasting, new ActionAlwaysSucceed()))),

						new Action(ctx => _numOfCasts++),
							// wait for cast to finish.
						new WaitContinue(TimeSpan.FromMilliseconds(3000), ctx => !StyxWoW.Me.IsCasting, new ActionAlwaysSucceed())));
        }

	    int GetMaxRepeat(int index)
	    {
		    var lua = string.Format("return GetTradeSkillInfo({0})", index);
		    return Lua.GetReturnVal<int>(lua, 2);
	    }

        private int GetTradeSkillIndex()
        {
                var count = Lua.GetReturnVal<int>("return GetNumTradeSkills()", 0);
                for (int i = 1; i <= count; i++)
                {
                    var link = Lua.GetReturnVal<string>("return GetTradeSkillItemLink(" + i + ")", 0);

                    // Make sure it's not a category!
                    if (string.IsNullOrEmpty(link))
                    {
                        continue;
                    }
					/* below are 2 examples of links. the 1st link has a spell ID and the second a item ID)
						|cffffd000|Henchant:7428|h[Enchant Bracer - Minor Dodge]|h|r
						|cffffffff|Hitem:6218:0:0:0:0:0:0:0:90:0:0|h[Runed Copper Rod]|h|r
					 */
	                bool createsItem = link.Contains("|Hitem:");
					link = link.Remove(0, link.IndexOf(':') + 1);
	                link = link.Remove(link.IndexOf(':') != -1 ? link.IndexOf(':') : link.IndexOf('|'));

	                int id = int.Parse(link);
					// 

					if (createsItem)
					{
						var item = ItemInfo.FromId((uint)id);
						if (item != null)
							QBCLog.DeveloperInfo(string.Format("ItemID: {0} at {1} - {2}", id, i, item.Name));
					}
					else
					{
						var spell = WoWSpell.FromId(id);
						if (spell != null)
  							QBCLog.DeveloperInfo(string.Format("SpellID: {0} at {1} - {2} ", id, i, spell.Name));	
					}
                    if (id == TradeSkillItemId)
                        return i;
                }
            
            return 0;
        }

	    bool IsTradeskillFrameShown
	    {
		    get
		    {
				return Lua.GetReturnVal<bool>("return TradeSkillFrame:IsVisible()", 0) || _isTradeskillOpen;
		    }
	    }

        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return new PrioritySelector(
                new Decorator(ret => StyxWoW.Me.IsMoving,
                    new Action(ret => Navigator.PlayerMover.MoveStop())),

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
	        _tradeskillSpell = WoWSpell.FromId(TradeSkillId);
	        if (_tradeskillSpell == null)
	        {
				QBCLog.ProfileError("TradeSkillId {0} is not a valid spell Id.", TradeSkillId);
	        }
            // This reports problems, and stops BT processing if there was a problem with attributes...
            // We had to defer this action, as the 'profile line number' is not available during the element's
            // constructor call.
            OnStart_HandleAttributeProblem();

            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (!IsDone)
            {
				Lua.Events.AttachEvent("TRADE_SKILL_SHOW", OnTradeSkillOpen);
				Lua.Events.AttachEvent("TRADE_SKILL_CLOSE", OnTradeSkillClose);
				// make sure we start with tradeskill frame closed to ensure the correct tradeskill window is opened.
				Lua.DoString("CloseTradeSkill()");
                this.UpdateGoalText(QuestId);
            }
        }

	    private void OnTradeSkillOpen(object sender, LuaEventArgs args)
	    {
		    _isTradeskillOpen = true;
	    }

		private void OnTradeSkillClose(object sender, LuaEventArgs args)
		{
			_isTradeskillOpen = false;
		}
	    #endregion
    }
}
