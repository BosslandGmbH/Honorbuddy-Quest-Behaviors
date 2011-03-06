using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Styx.Database;
using Styx.Logic.Combat;
using Styx.Helpers;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Pathing;
using Styx.Logic.Profiles.Quest.Order;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    public class BasicInteractWith : CustomForcedBehavior
    {
        #region Overrides of CustomForcedBehavior

        public BasicInteractWith(Dictionary<string, string> args)
            : base(args)
        {
            uint npcID;
            if (!uint.TryParse(Args["NpcID"], out npcID))
                Logging.Write("Parsing mobid in BasicInteractWith behavior failed! please check your profile!");

            uint moveto;
            if (!uint.TryParse(Args["MoveTo"], out moveto))
                Logging.Write("Parsing mobid in BasicInteractWith behavior failed! please check your profile!");

            int numberoftimes;
            if (!int.TryParse(Args["NumOfTimes"], out numberoftimes))
                Logging.Write("Parsing NumOfTimes in BasicInteractWith behavior failed! please check your profile!");

            int waittime;
            if (!int.TryParse(Args["WaitTime"], out waittime))
                Logging.Write("Parsing WaitTime in BasicInteractWith behavior failed! please check your profile!");

            uint questId;
            if (!uint.TryParse(Args["QuestId"], out questId))
                Logging.Write("Parsing questid in BasicInteractWith behavior failed! please check your profile!");

            NPCID = npcID;
            QuestId = questId;
            MoveToNPC = moveto;
            NumberOfTimes = numberoftimes;
            WaitTime = waittime;

            Counter = 1;


        }

        public WoWPoint MovePoint { get; private set; }
        public int Counter { get; set; }
        public uint NPCID { get; set; }
        public uint MoveToNPC { get; set; }
        public uint QuestId { get; set; }
        public int NumberOfTimes { get; set; }
        public int WaitTime { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        public List<WoWUnit> npcList;

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                new Decorator(ret => (QuestId != 0 && me.QuestLog.GetQuestById(QuestId) != null &&
                         me.QuestLog.GetQuestById(QuestId).IsCompleted),
                        new Action(ret => _isDone = true)),

                    new Decorator(ret => Counter > NumberOfTimes,
                        new Action(ret => _isDone = true)),

                        new PrioritySelector(

                            new Decorator(ret => Counter <= NumberOfTimes,
                                new Action(delegate
                                {
                                    npcList = ObjectManager.GetObjectsOfType<WoWUnit>()
                                        .Where(u => u.Entry == NPCID)
                                        .OrderBy(u => u.Distance).ToList();
                                    if (npcList.Count >= 1)
                                    {
                                        if (MoveToNPC == 1)
                                        {
                                            if (npcList[0].Location.Distance(me.Location) < 3)
                                            {
                                                npcList[0].Interact();
                                                Thread.Sleep(WaitTime);
                                                Counter++;
                                                return RunStatus.Success;
                                            }
                                            else
                                            {
                                                Navigator.MoveTo(npcList[0].Location);
                                                Thread.Sleep(300);
                                                return RunStatus.Running;
                                            }
                                        }
                                        else
                                        {
                                            npcList[0].Interact();
                                            Thread.Sleep(300);
                                            Counter++;
                                            return RunStatus.Success;
                                        }
                                    }
                                    return RunStatus.Running;
                                })
                                ),

                            new Action(ret => Logging.Write(""))
                        )
                    ));
        }

        private bool _isDone;
        public override bool IsDone
        {
            get { return _isDone; }
        }

        #endregion
    }
}

