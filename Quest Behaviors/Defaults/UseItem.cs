using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// Original code by Nesox, modified by KaZ
    /// Allows you to use items on nearby gameobjects/npc's
    /// ##Syntax##
    /// QuestId: The id of the quest.
    /// ItemId: The id of the item to use.
    /// NumOfTimes: Number of times to use said item.
    /// [Optional]WaitTime: Time to wait after using an item. DefaultValue: 1500 ms
    /// </summary>
    public class UseItem: CustomForcedBehavior
    {
        public UseItem(Dictionary<string, string> args)
            : base(args)
        {
            bool error = false;

            uint questId;
            if (!uint.TryParse(Args["QuestId"], out questId))
            {
                Logging.Write("Parsing attribute 'QuestId' in UseItem behavior failed! please check your profile!");
                error = true;
            }

            uint itemId;
            if(!uint.TryParse(Args["ItemId"], out itemId))
            {
                Logging.Write("Parsing attribute 'ItemId' in UseItem behavior failed! please check your profile!");
                error = true;
            }

            int numOfTimes;
            if (!int.TryParse(Args["NumOfTimes"], out numOfTimes))
            {
                Logging.Write("Parsing attribute 'NumOfTimes' in UseItem behavior failed! please check your profile!");
                error = true;
            }

            if(Args.ContainsKey("WaitTime"))
            {
                int waitTime;
                int.TryParse(Args["WaitTime"], out waitTime);
                WaitTime = waitTime != 0 ? waitTime : 1500;
            }

            if (error)
                TreeRoot.Stop();

            QuestId = questId;
            NumOfTimes = numOfTimes;
            ItemId = itemId;
        }

        public int WaitTime { get; private set; }
        public int Counter { get; private set; }
        public uint ItemId { get; private set; }
        public int NumOfTimes { get; private set; }
        public uint QuestId { get; private set; }

        #region Overrides of CustomForcedBehavior
        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
            new PrioritySelector(

                new Decorator(ret => Counter >= NumOfTimes,
                    new Action(ret => _isDone = true)),

                new Decorator(
                    ret => StyxWoW.Me.IsMoving,
                    new Action(delegate
                    {
                        Navigator.PlayerMover.MoveStop();
                        StyxWoW.SleepForLagDuration();
                    })),

                new Decorator(
                    ret => Item != null,
                    new Action(delegate
                    {
                        TreeRoot.StatusText = "Using item - Count: " + Counter;

                        Item.UseContainerItem();

                        StyxWoW.SleepForLagDuration();
                        Counter++;
                        Thread.Sleep(WaitTime);
                    }))));
        }

        private WoWItem Item
        {
            get
            {
                return StyxWoW.Me.CarriedItems.FirstOrDefault(i => i.Entry == ItemId && i.Usable && i.Cooldown != 0);
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

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
            if (quest != null)
            {
                var item = StyxWoW.Me.CarriedItems.FirstOrDefault(ret => ret.Entry == ItemId);
                if(item != null)
                    TreeRoot.GoalText = string.Format("Using item {0} for {1}",
                    item.Name,
                    quest.Name);
                
                else
                {
                    TreeRoot.GoalText = string.Format("Use item {0} times for quest:{1}",
                        NumOfTimes,
                        quest.Name);    
                }
            }
        }

        #endregion
    }

}
