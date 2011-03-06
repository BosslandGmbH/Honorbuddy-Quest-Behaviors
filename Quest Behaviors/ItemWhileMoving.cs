using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Styx.Database;
using Styx.Helpers;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    public class ItemWhileMoving : CustomForcedBehavior
    {
        public ItemWhileMoving(Dictionary<string, string> args)
            : base(args)
        {

            uint itemId;
            if (!uint.TryParse(Args["ItemId"], out itemId))
                Logging.Write("Parsing ItemId in ItemWhileMoving behavior failed! please check your profile!");

            uint questId;
            if (!uint.TryParse(Args["QuestId"], out questId))
                Logging.Write("Parsing questid in ItemWhileMoving behavior failed! please check your profile!");

            float xcoord;
            if (!float.TryParse(Args["X"], out xcoord))
                Logging.Write("Parsing X in ItemWhileMoving behavior failed! please check your profile!");

            float ycoord;
            if (!float.TryParse(Args["Y"], out ycoord))
                Logging.Write("Parsing Y in ItemWhileMoving behavior failed! please check your profile!");

            float zcoord;
            if (!float.TryParse(Args["Z"], out zcoord))
                Logging.Write("Parsing Z in ItemWhileMoving behavior failed! please check your profile!");

            QuestId = questId;
            ItemID = itemId;
            Counter = 0;
            Counter123 = 1;
            MovedToTarget = false;
            Location = new WoWPoint(xcoord, ycoord, zcoord);
        }

        public WoWPoint Location { get; private set; }
        public int Counter { get; set; }
        public int Counter123 { get; set; }
        public uint ItemID { get; set; }
        public int NPCID { get; set; }
        public bool MovedToTarget;
        public int NumberOfTimes { get; set; }
        public uint QuestId { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        public List<WoWUnit> npcList;


        #region Overrides of CustomForcedBehavior

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(
                    new Decorator(ret => (QuestId != 0 && me.QuestLog.GetQuestById(QuestId) != null &&
                         me.QuestLog.GetQuestById(QuestId).IsCompleted),
                        new Action(ret => _isDone = true)),

                    new Decorator(ret => Counter > 0,
                        new Action(ret => _isDone = true)),

                        new PrioritySelector(

                           new Decorator(ret => !MovedToTarget,
                                new Action(delegate
                                {
                                    if (Location.Distance(me.Location) > 10)
                                    {
                                        WoWMovement.ClickToMove(Location);
                                        Logging.Write("2");
                                        CastSpell();
                                        Counter123++;
                                    }
                                    else
                                    {
                                        Logging.Write("1");
                                        Counter++;
                                        return RunStatus.Success;
                                    }

                                    Thread.Sleep(1000);
                                    return RunStatus.Running;

                                })
                                ),

                            new Decorator(ret => StyxWoW.Me.IsMoving,
                                new Action(delegate
                                {
                                    WoWMovement.MoveStop();
                                    StyxWoW.SleepForLagDuration();
                                })
                                ),

                            new Action(ret => Navigator.MoveTo(Location))
                        )
                    ));
        }

        public void CastSpell()
        {
            Logging.Write("Casted Spell " + Counter + " Times out of " + NumberOfTimes);
            WoWItem ItemUseage = me.CarriedItems.Where(item => item.Entry.Equals(ItemID)).FirstOrDefault();
            Logging.Write("3");

            ItemUseage.Interact();
            Thread.Sleep(3000);
        }



        private bool _isDone;
        public override bool IsDone
        {
            get { return _isDone; }
        }

        #endregion
    }
}
