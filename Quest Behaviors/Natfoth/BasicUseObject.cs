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
using Styx.Logic.BehaviorTree;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    public class BasicUseObject : CustomForcedBehavior
    {

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {
            {"ObjectId",null},
            {"QuestId",null},
        };

        bool success = true;


        public BasicUseObject(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);

            int objectId = 0;
            int questId = 0;

            success = success && GetAttributeAsInteger("ObjectId", true, "1", 0, int.MaxValue, out objectId);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);

            ObjectID = objectId;
            QuestId = (uint)questId;
            Counter = 1;
            MovedToTarget = false;
        }

        public int Counter { get; set; }
        public int ObjectID { get; set; }
        public bool MovedToTarget;
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

                    new Decorator(ret => Counter > 1,
                        new Action(ret => _isDone = true)),

                        new PrioritySelector(

                           new Decorator(ret => !MovedToTarget,
                                new Action(delegate
                                {
                                        MovedToTarget = true;
                                        return RunStatus.Success;

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

                                    TreeRoot.GoalText = "BasicUseObject Running";
                                    TreeRoot.StatusText = "Using Object";

                                    ObjectManager.Update();

                                    objectList = ObjectManager.GetObjectsOfType<WoWGameObject>()
                                        .Where(u => u.Entry == ObjectID && !u.InUse && !u.IsDisabled)
                                        .OrderBy(u => u.Distance).ToList();

                                    if (objectList.Count >= 1)
                                    {

                                         Thread.Sleep(1000);
                                         UseGameObjectFunc();
                                    }

                                    if (me.Combat)
                                    {
                                        return RunStatus.Success;
                                    }


                                    if (Counter > 1)
                                    {
                                        return RunStatus.Success;
                                    }
                                    return RunStatus.Running;
                                })
                                ),

                            new Action(ret => Thread.Sleep(1000))
                        )
                    ));
        }

        public void UseGameObjectFunc()
        {
            Logging.Write("Using Object");
            objectList[0].Interact();
            StyxWoW.SleepForLagDuration();
            Counter++;
            Thread.Sleep(6000);

        }

        private bool _isDone;
        public override bool IsDone
        {
            get { return _isDone; }
        }

        #endregion
    }
}
