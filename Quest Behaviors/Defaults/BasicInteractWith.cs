using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Styx.Database;
using Styx.Logic.Combat;
using Styx.Helpers;
using Styx.Logic;
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
    public class BasicInteractWith : CustomForcedBehavior
    {

        /// <summary>
        /// BasicInteractWith by Natfoth
        /// Allows you to Interact with Mobs that are Nearby.
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// NpcID: Id of the Mob to interact with.
        /// UseCTM, MoveTo(Optional): Will move to the Npc Location
        /// LUATarget: Should be used for those Mobs that are inside vehicles and return a location of 0,0,0
        /// Faction: The faction the mobs needs to be before interacting
        /// X,Y,Z: The general location where theese objects can be found
        /// </summary>
        

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"NpcID",null},
            {"NpcId",null},
            {"UseCTM",null},
            {"MoveTo",null},
            {"LUATarget",null},
            {"Faction",null},
            {"QuestId",null},

        };

        bool success = true;

        public BasicInteractWith(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);

            int mobID = 0;
            int useCTM = 0;
            int luatarget = 0;
            int usefaction = 0;
            int questId = 0;

            success = success && GetAttributeAsInteger("NpcID", false, "0", 0, int.MaxValue, out mobID);
            success = success && GetAttributeAsInteger("UseCTM", false, "0", 0, int.MaxValue, out useCTM);
            success = success && GetAttributeAsInteger("LUATarget", false, "0", 0, int.MaxValue, out luatarget);
            success = success && GetAttributeAsInteger("Faction", false, "0", 0, int.MaxValue, out usefaction);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);

            if (mobID == 0)
                success = success && GetAttributeAsInteger("NpcId", false, "0", 0, int.MaxValue, out mobID);

            if (useCTM == 0)
            {
                success = success && GetAttributeAsInteger("MoveTo", false, "0", 0, int.MaxValue, out useCTM);
            }

            MobId = mobID;
            LUATarget = luatarget;
            UseCTM = useCTM;
            FactionID = usefaction;
            QuestId = (uint)questId;
            Counter = 0;
        }

        public WoWPoint MovePoint { get; private set; }
        public int Counter { get; set; }
        public int MobId { get; set; }
        public int LUATarget { get; set; }
        public int UseCTM { get; set; }
        public int FactionID { get; set; }
        public uint QuestId { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        public List<WoWUnit> mobList
        {
            get
            {
                if (FactionID > 1)
                {
                    return ObjectManager.GetObjectsOfType<WoWUnit>()
                                    .Where(u => u.Entry == MobId && !u.Dead && u.FactionId == FactionID)
                                    .OrderBy(u => u.Distance).ToList(); 
                }
                else
                {
                    return ObjectManager.GetObjectsOfType<WoWUnit>()
                                            .Where(u => u.Entry == MobId && !u.Dead)
                                            .OrderBy(u => u.Distance).ToList();
                }
            }
        }

        #region Overrides of CustomForcedBehavior.

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

            if (quest != null)
            {
                TreeRoot.GoalText = "BasicInteractWith - " + quest.Name;
            }
            else
            {
                TreeRoot.GoalText = "BasicInteractWith: Running";
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

                             new Decorator(ret => mobList.Count > 0 && !mobList[0].WithinInteractRange && UseCTM > 0,
                                new Sequence(
                                    new DecoratorContinue(ret => UseCTM > 0,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Moving To Mob MyCTM - " + mobList[0].Name + " X: " + mobList[0].X + " Y: " + mobList[0].Y + " Z: " + mobList[0].Z),
                                            new Action(ret => WoWMovement.ClickToMove(mobList[0].Location))
                                            )),

                                      new DecoratorContinue(ret => UseCTM == 0,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Moving To Mob MyCTM - " + mobList[0].Name + " X: " + mobList[0].X + " Y: " + mobList[0].Y + " Z: " + mobList[0].Z),
                                            new Action(ret => Navigator.MoveTo(mobList[0].Location))
                                            ))


                                    )),

                            new Decorator(ret => mobList.Count > 0 && mobList[0].WithinInteractRange,
                                new Sequence(
                                    new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                        new Action(ret =>
                                        {
                                            WoWMovement.MoveStop();
                                            StyxWoW.SleepForLagDuration();
                                        })),
                                        new Action(ret => mobList[0].Interact()),
                                        new Action(ret => Counter++)
                                    )
                            ),

                            new Decorator(ret => mobList.Count > 0 && UseCTM == 0 && LUATarget == 0,
                                new Sequence(
                                    new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                        new Action(ret =>
                                        {
                                            WoWMovement.MoveStop();
                                            StyxWoW.SleepForLagDuration();
                                        })),
                                        new Action(ret => mobList[0].Interact()),
                                        new Action(ret => Counter++)
                                    )
                            ),

                            new Decorator(ret => mobList.Count > 0 && LUATarget > 0,
                                new Sequence(
                                    new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                        new Action(ret =>
                                        {
                                            WoWMovement.MoveStop();
                                            StyxWoW.SleepForLagDuration();
                                        })),
                                        new Action(ret => Lua.DoString("TargetNearest()")),
                                        new Action(ret => me.CurrentTarget.Interact()),
                                        new Action(ret => Counter++)
                                    )
                            )
                    )));
        }

        private bool _isDone;
        public override bool IsDone
        {
            get { return _isDone; }
        }

        #endregion
    }
}

