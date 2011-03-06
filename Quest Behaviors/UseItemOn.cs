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
    public class UseItemOn : CustomForcedBehavior
    {
        public UseItemOn(Dictionary<string, string> args)
            : base(args)
        {
            uint itemId;
            if (!uint.TryParse(Args["ItemId"], out itemId))
                Logging.Write("Parsing ItemId in UseItemOn behavior failed! please check your profile!");

            int npcID;
            if (!int.TryParse((string)args["NpcId"], out npcID))
                Logging.Write("Parsing NpcId in UseItemOn behavior failed! please check your profile!");

            int numberoftimes;
            if (!int.TryParse(Args["NumOfTimes"], out numberoftimes))
                Logging.Write("Parsing NumOfTimes in UseItemOn behavior failed! please check your profile!");

            int waittime;
            if (!int.TryParse(Args["WaitTime"], out waittime))
                Logging.Write("Parsing HpLeftAmount in UseItemOn behavior failed! please check your profile!");

            uint questId;
            if (!uint.TryParse(Args["QuestId"], out questId))
                Logging.Write("Parsing questid in UseItemOn behavior failed! please check your profile!");

            float xcoord;
            if (!float.TryParse(Args["X"], out xcoord))
                Logging.Write("Parsing X in UseItemOn behavior failed! please check your profile!");

            float ycoord;
            if (!float.TryParse(Args["Y"], out ycoord))
                Logging.Write("Parsing Y in UseItemOn behavior failed! please check your profile!");

            float zcoord;
            if (!float.TryParse(Args["Z"], out zcoord))
                Logging.Write("Parsing Z in UseItemOn behavior failed! please check your profile!");


            QuestId = questId;
            ItemID = itemId;
            NPCID = npcID;
            Counter = 1;
            WaitTime = waittime;
            MovedToTarget = false;
            NumberOfTimes = numberoftimes;
            Location = new WoWPoint(xcoord, ycoord, zcoord);
        }

        public WoWPoint Location { get; private set; }
        public int Counter { get; set; }
        public uint ItemID { get; set; }
        public int NPCID { get; set; }
        public bool MovedToTarget;
        public int NumberOfTimes { get; set; }
        public int WaitTime { get; set; }
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

                    new Decorator(ret => Counter > NumberOfTimes,
                        new Action(ret => _isDone = true)),

                        new PrioritySelector(

                           new Decorator(ret => !MovedToTarget,
                                new Action(delegate
                                {

                                    WoWPoint destination1 = new WoWPoint(Location.X, Location.Y, Location.Z);
                                    WoWPoint[] pathtoDest1 = Styx.Logic.Pathing.Navigator.GeneratePath(me.Location, destination1);

                                    foreach (WoWPoint p in pathtoDest1)
                                    {
                                        while (!me.Dead && p.Distance(me.Location) > 3)
                                        {
                                            if (me.Combat)
                                            {
                                                break;
                                            }
                                            Thread.Sleep(100);
                                            WoWMovement.ClickToMove(p);
                                        }

                                        if (me.Combat)
                                        {
                                            break;
                                        }
                                    }

                                    if (me.Combat)
                                    {
                                        return RunStatus.Success;
                                    }
                                    else if (!me.Combat)
                                    {
                                        MovedToTarget = true;
                                        return RunStatus.Success;
                                    }

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

                            new Decorator(ret => MovedToTarget,
                                new Action(delegate
                                {
                                    // CurrentUnit.Interact();
									
									PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
                                    if (quest.IsCompleted)
                                    {
                                        Counter++;
                                        return RunStatus.Success;
                                    }



                                    ObjectManager.Update();

                                    npcList = ObjectManager.GetObjectsOfType<WoWUnit>()
                                        .Where(u => u.Entry == NPCID)
                                        .OrderBy(u => u.Distance).ToList();

                                    if (npcList != null)
                                    {

                                        if (npcList[0].Location.Distance(me.Location) > 20 || !npcList[0].InLineOfSight)
                                        {
                                            WoWPoint destination = new WoWPoint(npcList[0].Location.X, npcList[0].Location.Y, npcList[0].Location.Z);
                                            WoWPoint[] pathtoDest = Styx.Logic.Pathing.Navigator.GeneratePath(me.Location, destination);

                                            foreach (WoWPoint p in pathtoDest)
                                            {
                                                while (!me.Dead && p.Distance(me.Location) > 3)
                                                {
                                                    if (me.Combat)
                                                    {
                                                        break;
                                                    }
                                                    Thread.Sleep(100);
                                                    WoWMovement.ClickToMove(p);
                                                }

                                                if (me.Combat)
                                                {
                                                    break;
                                                }
                                            }
                                        }

                                        npcList[0].Target();
                                        Thread.Sleep(1000);
                                        CastSpell();

                                        if (me.Combat)
                                        {
                                            return RunStatus.Success;
                                        }
                                    }
                                    else
                                    {
                                        Logging.Write("NPC List is Empty");
                                    }




                                    if (Counter > NumberOfTimes)
                                    {
                                        return RunStatus.Success;
                                    }
                                    return RunStatus.Running;
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

            ItemUseage.Interact();
            Counter++;
            Thread.Sleep(WaitTime);
        }



        private bool _isDone;
        public override bool IsDone
        {
            get
            {
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
                return _isDone && quest != null && quest.IsCompleted || quest == null;
            }
        }

        #endregion
    }
}
