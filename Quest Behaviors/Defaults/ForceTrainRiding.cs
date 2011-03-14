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
    public class ForceTrainRiding : CustomForcedBehavior
    {

        /// <summary>
        /// BasicInteractWith by Natfoth
        /// Allows you to Interact with Mobs that are Nearby.
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// NpcId: Id of the Mob to interact with.
        /// UseCTM, MoveTo(Optional): Will move to the Npc Location
        /// Faction: The faction the mobs needs to be before interacting
        /// X,Y,Z: The general location where theese objects can be found
        /// </summary>
        

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"NpcID",null},
            {"NpcId",null},
            {"X",null},
            {"Y",null},
            {"Z",null},
            {"QuestId",null},

        };

        bool success = true;

        public ForceTrainRiding(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);

            int mobID = 0;
            int questId = 0;
            WoWPoint location = new WoWPoint(0, 0, 0);

            success = success && GetAttributeAsInteger("NpcID", false, "0", 0, int.MaxValue, out mobID);
            success = success && GetXYZAttributeAsWoWPoint("X", "Y", "Z", false, new WoWPoint(0, 0, 0), out location);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);

            if (mobID == 0)
                success = success && GetAttributeAsInteger("NpcId", false, "0", 0, int.MaxValue, out mobID);

            Location = location;
            MobId = mobID;
            QuestId = (uint)questId;
            Counter = 0;
        }

        public WoWPoint Location { get; private set; }
        public WoWPoint MovePoint { get; private set; }
        public int Counter { get; set; }
        public int MobId { get; set; }
        public uint QuestId { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        public List<WoWUnit> mobList
        {
            get
            {
                    return ObjectManager.GetObjectsOfType<WoWUnit>()
                                            .Where(u => u.Entry == MobId && !u.Dead)
                                            .OrderBy(u => u.Distance).ToList();
                
            }
        }

        #region Overrides of CustomForcedBehavior.

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

            if (quest != null)
            {
                TreeRoot.GoalText = "ForceTrainRiding - " + quest.Name;
            }
            else
            {
                TreeRoot.GoalText = "ForceTrainRiding: Running";
            }
        }

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                    new Decorator(ret => Counter >= 1,
                        new Action(ret => _isDone = true)),

                        new PrioritySelector(

                            new Decorator(ret => Counter > 0,
                                new Sequence(

                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new Action(ret => _isDone = true),
                                    new WaitContinue(1,
                                        new Action(delegate
                                        {
                                            _isDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )
                                ),

                             new Decorator(ret => mobList.Count > 0 && !mobList[0].WithinInteractRange,
                                new Action(ret => Navigator.MoveTo(mobList[0].Location))),

                            new Decorator(ret => mobList.Count > 0 && mobList[0].WithinInteractRange,
                                new Sequence(
                                    new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                        new Action(ret =>
                                        {
                                            WoWMovement.MoveStop();
                                            StyxWoW.SleepForLagDuration();
                                        })),
                                    new Action(ret => TreeRoot.StatusText = "Opening Trainer - " + mobList[0].Name + " X: " + mobList[0].X + " Y: " + mobList[0].Y + " Z: " + mobList[0].Z),
                                    new Action(ret => mobList[0].Interact()),
                                    new Action(ret => Thread.Sleep(200)),
                                    new Action(ret => Lua.DoString("BuyTrainerService(0)")),
                                    new Action(ret => Counter++)
                                    )
                            ),

                            new Decorator(ret => ridingTrainer != null,
                                new Action(ret => Navigator.MoveTo(ridingTrainer.Location))
                                ),

                            new Action(ret => Counter++)
                    )));
        }

        NpcResult ridingTrainer
        {
            get
            {
                return NpcQueries.GetNpcById((uint)MobId);
            }

        }

        private bool _isDone;
        public override bool IsDone
        {
            get { return _isDone; }
        }

        #endregion
    }
}

