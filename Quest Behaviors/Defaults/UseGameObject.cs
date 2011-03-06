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
using Timer = Styx.Helpers.WaitTimer;

namespace Styx.Bot.Quest_Behaviors
{
    public class UseGameObject : CustomForcedBehavior
    {

        /// <summary>
        /// UseGameObject by Natfoth
        /// Allows you to use a Specific Spell on a Target, useful for Dummies and Starting Quests.
        /// ##Syntax##
        /// QuestId: If you have a questID and it is > 0 then it will do the behavior over and over until the quest is complete, otherwise it will use NumOfTimes.
        /// ObjectId: The Object to Use.
        /// NumOfTimes: How many times to use the objects.
        /// X,Y,Z: The general location where these objects can be found
        /// </summary>
        /// 

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"ObjectId",null},
            {"NumOfTimes",null},
            {"QuestId",null},
            {"WaitTime",null},
            {"X",null},
            {"Y",null},
            {"Z",null},

        };

        bool success = true;
        public Timer _timer;


        public UseGameObject(Dictionary<string, string> args)
            : base(args)
        {

            CheckForUnrecognizedAttributes(recognizedAttributes);

            int objectId = 0;
            int numberoftimes = 0;
            int questId = 0;
            int waittime = 0;
            int usequestId = 0;
            WoWPoint location = new WoWPoint(0, 0, 0);

            success = success && GetAttributeAsInteger("ObjectId", true, "1", 0, int.MaxValue, out objectId);
            success = success && GetAttributeAsInteger("NumOfTimes", false, "1", 0, int.MaxValue, out numberoftimes);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
            success = success && GetAttributeAsInteger("WaitTime", false, "0", 0, int.MaxValue, out waittime);
            success = success && GetXYZAttributeAsWoWPoint("X", "Y", "Z", false, new WoWPoint(0, 0, 0), out location);

            ObjectID = objectId;
            QuestId = (uint)questId;
            UseQuestId = usequestId;
            WaitTime = waittime;
            Counter = 1;
            NumberOfTimes = numberoftimes;
            Location = location;

            _timer = new Timer(new TimeSpan(0, 0, 0, 0, WaitTime));
        }

        public WoWPoint Location { get; private set; }
        public int Counter { get; set; }
        public int ObjectID { get; set; }
        public int WaitTime { get; set; }
        public int NumberOfTimes { get; set; }
        public uint QuestId { get; set; }
        public int UseQuestId { get; set; }
        

        public static LocalPlayer me = ObjectManager.Me;

        public List<WoWGameObject> objectList
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWGameObject>()
                                       .Where(u => u.Entry == ObjectID && !u.InUse && !u.IsDisabled)
                                       .OrderBy(u => u.Distance).ToList();
            }
        }

        #region Overrides of CustomForcedBehavior

        public override void OnStart()
        {
            _timer = null;
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

            if (quest != null)
            {
                TreeRoot.GoalText = "UseGameObject - " + quest.Name;
            }
            else
            {
                TreeRoot.GoalText = "UseGameObject: Running";
            }
        }

        private Composite _root;
        protected override Composite CreateBehavior()
        {

            return _root ?? (_root =
                new PrioritySelector(

                           new Decorator(ret => (Counter > NumberOfTimes && QuestId == 0) || (me.QuestLog.GetQuestById(QuestId) != null && me.QuestLog.GetQuestById(QuestId).IsCompleted),
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new WaitContinue(120,
                                        new Action(delegate
                                        {
                                            _isDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )),

                           new Decorator(ret => _timer != null && !_timer.IsFinished,
                                new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "UseItemOn - WaitTimer: " + _timer.TimeLeft.Seconds + " Seconds Left"),
                                        new Action(delegate { return RunStatus.Success; })
                                    )
                                ),

                           new Decorator(ret => _timer != null && _timer.IsFinished,
                                new Sequence(
                                        new Action(ret => _timer.Reset()),
                                        new Action(ret => _timer = null),
                                        new Action(delegate { return RunStatus.Success; })
                                    )
                                ),

                            
                           new Decorator(ret => objectList.Count == 0,
                                new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Moving To Location - X: " + Location.X + " Y: " + Location.Y + " Z: " + Location.Z),
                                        new Action(ret => Navigator.MoveTo(Location)),
                                        new Action(ret => Thread.Sleep(300))
                                    )
                                ),

                           new Decorator(ret => objectList.Count > 0,
                                new Sequence(
                                    new DecoratorContinue(ret => objectList[0].WithinInteractRange,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Using Object - " + objectList[0].Name),
                                            new Action(ret => WoWMovement.MoveStop()),
                                            new Action(ret => objectList[0].Interact()),
                                            new Action(ret => Counter++),
                                            new Action(ret => _timer = new Timer(new TimeSpan(0, 0, 0, 0, WaitTime))),
                                            new Action(ret => _timer.Reset())
                                            )
                                    ),
                                    new DecoratorContinue(ret => !objectList[0].WithinInteractRange,
                                        new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Moving To Object - " + objectList[0].Name + " Yards Away: " + objectList[0].Location.Distance(me.Location)),
                                        new Action(ret => Navigator.MoveTo(objectList[0].Location)),
                                        new Action(ret => Thread.Sleep(100))
                                            ))
                                    ))
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
