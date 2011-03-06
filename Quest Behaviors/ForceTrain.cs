using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Styx.Database;
using Styx.Helpers;
using Styx.Logic;
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
    public class ForceTrain : CustomForcedBehavior
    {
        #region Overrides of CustomForcedBehavior

        public ForceTrain(Dictionary<string, string> args)
            : base(args)
        {
            uint chattype;
            if (!uint.TryParse(Args["ForceTrain"], out chattype))
                Logging.Write("Parsing ForceTrain in ForceTrain behavior failed! please check your profile!");

            uint questId;
            if (!uint.TryParse(Args["QuestId"], out questId))
                Logging.Write("Parsing questid in UseItemOn behavior failed! please check your profile!");

            QuestId = questId;
            Counter = 0;
            AtTrainer = false;
            ChatType = chattype;
        }

        public int Counter { get; set; }
        public uint ChatType { get; set; }
        public bool AtTrainer;
        public uint QuestId { get; set; }

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

                    new Decorator(ret => Counter >= 1,
                        new Action(ret => _isDone = true)),

                        new PrioritySelector(

                            new Decorator(ret => Counter == 0,
                                new Action(delegate
                                {

                                    LevelbotSettings.Instance.FindVendorsAutomatically = true;
                                    Vendors.ForceTrainer = true;
                                    Counter++;
                                    return RunStatus.Success;


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
