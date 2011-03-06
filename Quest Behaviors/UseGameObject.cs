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
    public class UseGameObject : CustomForcedBehavior
    {
        public UseGameObject(Dictionary<string, string> args)
            : base(args)
        {
            uint objectId;
            if (!uint.TryParse(Args["ObjectId"], out objectId))
                Logging.Write("Parsing ObjectId in UseGameObject behavior failed! please check your profile!");

            int numberoftimes;
            if (!int.TryParse(Args["NumOfTimes"], out numberoftimes))
                Logging.Write("Parsing NumOfTimes in UseGameObject behavior failed! please check your profile!");

            uint questId;
            if (!uint.TryParse(Args["QuestId"], out questId))
                Logging.Write("Parsing questid in UseGameObject behavior failed! please check your profile!");

            float xcoord;
            if (!float.TryParse(Args["X"], out xcoord))
                Logging.Write("Parsing X in UseGameObject behavior failed! please check your profile!");

            float ycoord;
            if (!float.TryParse(Args["Y"], out ycoord))
                Logging.Write("Parsing Y in UseGameObject behavior failed! please check your profile!");

            float zcoord;
            if (!float.TryParse(Args["Z"], out zcoord))
                Logging.Write("Parsing Z in UseGameObject behavior failed! please check your profile!");

            ObjectID = objectId;
            QuestId = questId;
            Counter = 1;
            MovedToTarget = false;
            NumberOfTimes = numberoftimes;
            Location = new WoWPoint(xcoord, ycoord, zcoord);
        }

        public WoWPoint Location { get; private set; }
        public int Counter { get; set; }
        public uint ObjectID { get; set; }
        public bool MovedToTarget;
        public int NumberOfTimes { get; set; }
        public uint QuestId { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        public List<WoWGameObject> objectList;

        /// <summary>
        /// A Queue for npc's we need to talk to
        /// </summary>
        //private WoWUnit CurrentUnit { get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(unit => unit.Distance < 100 && unit.Entry == MobId); } }

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

                                    ObjectManager.Update();

                                    objectList = ObjectManager.GetObjectsOfType<WoWGameObject>()
                                        .Where(u => u.Entry == ObjectID && !u.InUse && !u.IsDisabled)
                                        .OrderBy(u => u.Distance).ToList();

                                    if (objectList.Count >= 1)
                                    {

                                        WoWPoint destination1 = new WoWPoint(objectList[0].Location.X, objectList[0].Location.Y, objectList[0].Location.Z);
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
                                            Thread.Sleep(1000);
                                            UseGameObjectFunc();
                                        }
                                    }

                                    if (me.Combat)
                                    {
                                        return RunStatus.Success;
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

        public void UseGameObjectFunc()
        {
            Logging.Write("Using Object " + Counter + " Times out of " + NumberOfTimes);
            objectList[0].Interact();
            StyxWoW.SleepForLagDuration();
            Counter++;
            Thread.Sleep(6000);

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
