using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Styx.Database;
using Styx.Helpers;
using Styx.Logic.BehaviorTree;
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
        readonly Dictionary<string, object> _recognizedAttributes = new Dictionary<string, object>()
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


        public UseGameObject(Dictionary<string, string> args)
            : base(args)
        {

            CheckForUnrecognizedAttributes(_recognizedAttributes);

            int objectId = 0;
            int numberoftimes = 0;
            int questId = 0;
            int waitTime = 0;
            WoWPoint location = new WoWPoint(0, 0, 0);

            success = success && GetAttributeAsInteger("ObjectId", true, "1", 0, int.MaxValue, out objectId);
            success = success && GetAttributeAsInteger("NumOfTimes", true, "1", 0, int.MaxValue, out numberoftimes);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
            success = success && GetAttributeAsInteger("WaitTime", false, "1500", 0, int.MaxValue, out waitTime);
            success = success && GetXYZAttributeAsWoWPoint("X", "Y", "Z", true, new WoWPoint(0, 0, 0), out location);

            ObjectId = objectId;
            QuestId = (uint)questId;
            NumberOfTimes = numberoftimes;
            Location = location;
            WaitTime = waitTime;
        }

        public WoWPoint Location { get; private set; }
        public int ObjectId { get; private set; }
        public int NumberOfTimes { private get; set; }
        public uint QuestId { get; private set; }
        public int WaitTime { get; private set; }

        public static LocalPlayer Me { get { return StyxWoW.Me; } } 

        public WoWGameObject GameObject
        {
            get
            {
                return 
                    ObjectManager.GetObjectsOfType<WoWGameObject>().Where(
                    u => u.Entry == ObjectId && 
                        !u.InUse && 
                        !u.IsDisabled).OrderBy(u => u.Distance).FirstOrDefault();
            }
        }

        #region Overrides of CustomForcedBehavior

        private Composite _root;
        private int _counter;

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                    // Move to the gameobject if it isn't null and we aren't withing interact range.
                    new Decorator(ret => GameObject != null && !GameObject.WithinInteractRange,
                        new Sequence(
                            new Action(ret => TreeRoot.StatusText = "Moving to interact with gameobject: " + GameObject.Name),
                            new Action(ret => TreeRoot.GoalText = "Use Gameobject: " + GameObject.Name),
                            new Action(ret => Navigator.MoveTo(GameObject.Location))
                            )
                        ),

                    // Interact etc. 
                    new Decorator(ret => GameObject != null && GameObject.WithinInteractRange,
                        // Set the context to the gameobject
                        new Sequence(ret => GameObject,

                            new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                new Sequence(
                                    new Action(ret => WoWMovement.MoveStop()),
                                    new WaitContinue(5, ret => !StyxWoW.Me.IsMoving,
                                        null)
                                    )),

                            new Action(ret => Logging.Write("Using Object [{0}] {1} Times out of {2}", ((WoWGameObject)ret).Name, _counter + 1, NumberOfTimes)),
                            new Action(ret => ((WoWGameObject)ret).Interact()),
                            new Action(ret => StyxWoW.SleepForLagDuration()),
                            new Action(ret => Thread.Sleep(WaitTime)),
                            new Action(delegate { _counter++; })
                        )),

                        new Decorator(ret => Location != WoWPoint.Empty,
                            new Sequence(
                                new Action(ret => TreeRoot.StatusText = "Moving to interact with gameobject"),
                                new Action(ret => TreeRoot.GoalText = "Use Gameobject"),
                                new Action(ret => Navigator.MoveTo(Location))
                                )
                            )
                        ));
        }

        public override bool IsDone
        {
            get
            {
                PlayerQuest quest = Me.QuestLog.GetQuestById(QuestId);

                return
                   _counter >= NumberOfTimes ||
                   (QuestId > 0 && quest == null) ||
                   (quest != null && quest.IsCompleted);
            }
        }

        #endregion
    }
}
