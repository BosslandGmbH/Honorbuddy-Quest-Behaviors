using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using CommonBehaviors.Actions;
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
    class PerformTradeskillOn : CustomForcedBehavior
    {
        public uint QuestId;

        /// <summary> Identifier for the trade skill </summary>
        public uint TradeSkillId;

        /// <summary> Identifier for the trade skill item. E.g; the actual 'item' we use from the tradeskill window. </summary>
        public uint TradeSkillItemId;

        /// <summary> If set, an item ID to cast the trade skill on. </summary>
        public uint? CastOnItemId;
        
        /// <summary> Number of times </summary>
        public uint NumTimes;

        public PerformTradeskillOn(Dictionary<string, string> args) : base(args)
        {
            StringBuilder errors = new StringBuilder();
            if (!args.ContainsKey("QuestId"))
            {
                errors.AppendLine(GetType().Name + " - QuestId is missing!");
            }
            else if (!uint.TryParse(args["QuestId"], out QuestId))
            {
                errors.AppendLine(GetType().Name + " - Malformed QuestId. It must be an integer! [1, 2, 3, etc] Got: " + args["QuestId"]);
            }

            if (!args.ContainsKey("TradeSkillId"))
            {
                errors.AppendLine(GetType().Name + " - TradeSkillId is missing!");
            }
            else if (!uint.TryParse(args["TradeSkillId"], out TradeSkillId))
            {
                errors.AppendLine(GetType().Name + " - Malformed TradeSkillId. It must be an integer! [1, 2, 3, etc] Got: " + args["TradeSkillId"]);
            }

            if (!args.ContainsKey("TradeSkillItemId"))
            {
                errors.AppendLine(GetType().Name + " - TradeSkillItemId is missing!");
            }
            else if (!uint.TryParse(args["TradeSkillItemId"], out TradeSkillItemId))
            {
                errors.AppendLine(GetType().Name + " - Malformed TradeSkillItemId. It must be an integer! [1, 2, 3, etc] Got: " + args["TradeSkillItemId"]);
            }

            if (!args.ContainsKey("NumTimes"))
            {
                errors.AppendLine(GetType().Name + " - NumTimes is missing!");
            }
            else if (!uint.TryParse(args["NumTimes"], out NumTimes))
            {
                errors.AppendLine(GetType().Name + " - Malformed NumTimes. It must be an integer! [1, 2, 3, etc] Got: " + args["NumTimes"]);
            }

            // OPTIONAL
            if (args.ContainsKey("CastOnItemId"))
            {
                uint tmp;
                if (!uint.TryParse(args["CastOnItemId"], out tmp))
                {
                    errors.AppendLine(
                        GetType().Name + " - Malformed CastOnItemId. It must be an integer! [1, 2, 3, etc] Got: " + args["CastOnItemId"]);
                }
                else
                {
                    CastOnItemId = tmp;
                }
            }

            if (!string.IsNullOrEmpty(errors.ToString()))
            {
                Logging.Write(errors.ToString());
                throw new Exception(errors.ToString());
            }
        }

        private bool _done;
        public override bool IsDone
        {
            get
            {
                // Don't allow this behavior to run on null quest IDs. We MUST have an ID for this.
                if (QuestId == 0)
                    return true;

                var q = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
                return (q != null && q.IsCompleted) || _done;
            }
        }

        private void PerformTradeSkill()
        {
            Lua.DoString("DoTradeSkill(" + GetTradeSkillIndex() + ", " + (NumTimes == 0 ? 1 : NumTimes) + ")");
            Thread.Sleep(500);

            if (CastOnItemId.HasValue)
            {
                var item = StyxWoW.Me.CarriedItems.FirstOrDefault(i => i.Entry == CastOnItemId.Value);
                if (item == null)
                {
                    Logging.Write("COULD NOT FIND ITEM FOR " + GetType().Name + "! Aborting and stopping bot! [Item: " + CastOnItemId.Value + "]");
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

            _done = true;
        }

        protected override Composite CreateBehavior()
        {
            return new PrioritySelector(
                new Decorator(ret=>StyxWoW.Me.IsMoving,
                    new Action(ret=>Navigator.PlayerMover.MoveStop())),

                CreateTradeSkillCast());
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
    }
}
