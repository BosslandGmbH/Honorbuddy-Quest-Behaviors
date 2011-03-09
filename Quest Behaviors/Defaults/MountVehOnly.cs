using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Styx.Database;
using Styx.Logic.Combat;
using Styx.Helpers;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Pathing;
using Styx.Logic.Profiles.Quest;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Styx.Logic.BehaviorTree;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    public class MountVehOnly : CustomForcedBehavior
    {
        

        /// <summary>
        /// MountVehOnly by Natfoth
        /// Only use this when you need to mount a Vehicle but it will require nothing else, wow has to auto dismount you at the end or you use EjectVeh.
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// MobMountId: The ID of the Vehicle you want to mount.
        /// X,Y,Z: The general location where these objects can be found
        /// </summary>
        /// 

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"NpcMountId",null},
            {"MobMountId", null},
            {"X",null},
            {"Y",null},
            {"Z",null},
            {"QuestId",null},

        };

        bool success = true;

        public MountVehOnly(Dictionary<string, string> args)
            : base(args)
        {

            CheckForUnrecognizedAttributes(recognizedAttributes);

            int npcmountid = 0;
            int mobmountid = 0;
            int questId = 0;
            WoWPoint location = new WoWPoint(0, 0, 0);

            success = success && GetAttributeAsInteger("NpcMountId", false, "1", 0, int.MaxValue, out npcmountid);
            success = success && GetAttributeAsInteger("MobMountId", false, "1", 0, int.MaxValue, out mobmountid);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
            success = success && GetXYZAttributeAsWoWPoint("X", "Y", "Z", true, new WoWPoint(0, 0, 0), out location);

            MobMountId = mobmountid != 1 ? mobmountid : npcmountid;
            Location = location;
            QuestId = (uint)questId;

            Counter = 0;
        }

        public WoWPoint Location { get; private set; }
        public int Counter { get; set; }
        public int MobMountId { get; set; }
        public uint QuestId { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        public List<WoWUnit> npcvehicleList;

        static public bool inVehicle { get { return Lua.GetReturnVal<bool>("return  UnitUsingVehicle(\"player\")", 0); } }


        public List<WoWUnit> mobList
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>()
                                    .Where(u => u.Entry == MobMountId && !u.Dead)
                                    .OrderBy(u => u.Distance).ToList();
            }
        }

        #region Overrides of CustomForcedBehavior

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

            if (quest != null)
            {
                TreeRoot.GoalText = "MountVehOnly - " + quest.Name;
            }
            else
            {
                TreeRoot.GoalText = "MountVehOnly: Running";
            }
        }

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                            new Decorator(ret => Counter > 0,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new WaitContinue(120,
                                        new Action(delegate
                                        {
                                            _isDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )),

                           new Decorator(ret => inVehicle,
                                new Sequence(
                                        new Action(ret => Counter++)
                                    )
                                ),

                           new Decorator(ret => mobList.Count == 0,
                                new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Moving To Location - X: " + Location.X + " Y: " + Location.Y),
                                        new Action(ret => Navigator.MoveTo(Location)),
                                        new Action(ret => Thread.Sleep(300))
                                    )
                                ),

                           new Decorator(ret => mobList.Count > 0,
                                new Sequence(
                                    new DecoratorContinue(ret => mobList[0].WithinInteractRange,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Mounting Vehicle - " + mobList[0].Name),
                                            new Action(ret => WoWMovement.MoveStop()),
                                            new Action(ret => mobList[0].Interact())
                                            )
                                    ),
                                    new DecoratorContinue(ret => !mobList[0].WithinInteractRange,
                                        new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Moving To Vehicle - " + mobList[0].Name + " X: " + mobList[0].X + " Y: " + mobList[0].Y + " Z: " + mobList[0].Z + " Yards Away: " + mobList[0].Location.Distance(me.Location)),
                                        new Action(ret => Navigator.MoveTo(mobList[0].Location)),
                                        new Action(ret => Thread.Sleep(300))
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
