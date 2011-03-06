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
using Styx.Logic.Combat;
using Styx.Logic.BehaviorTree;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    public class CompleteLogQuest : CustomForcedBehavior
    {

        /// <summary>
        /// CompleteLogQuest by Natfoth
        /// Will complete a "In-The-Field" Quest.
        /// ##Syntax##
        /// QuestId: The Entire purpose of this behavior, so this one is required.
        /// </summary>
        /// 

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"QuestID",null},

        };

        bool success = true;


        #region Overrides of CustomForcedBehavior

        public CompleteLogQuest(Dictionary<string, string> args)
            : base(args)
        {

            CheckForUnrecognizedAttributes(recognizedAttributes);

            int questId = 0;

            success = success && GetAttributeAsInteger("QuestID", true, "0", 0, int.MaxValue, out questId);

            Counter = 0;
            QuestID = (uint)questId;


        }

        public WoWPoint MovePoint { get; private set; }
        public int Counter { get; set; }
        public static uint QuestID { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        static public int questIndexID { get { return Lua.GetReturnVal<int>("return  GetQuestLogIndexByID(" + QuestID + ")", 0); } }

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestID);

            if (quest != null)
                TreeRoot.GoalText = "CompleteLogQuest - " + quest.Name;
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

                           new Decorator(ret => Counter == 0,
                                new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Completing Log Quest - " + QuestID),
                                        new Action(ret => Lua.DoString("ShowQuestComplete({0})", questIndexID)),
                                        new Action(ret => Thread.Sleep(300)),
                                        new Action(ret => Lua.DoString("CompleteQuest()")),
                                        new Action(ret => Thread.Sleep(300)),
                                        new Action(ret => Lua.DoString("GetQuestReward({0})", 1)),
                                        new Action(ret => Thread.Sleep(300)),
                                        new Action(ret => Lua.DoString("AcceptQuest()")),
                                        new Action(ret => Counter++)
                                    ))
                               
                        )
                    );
        }

        private bool _isDone;
        public override bool IsDone
        {
            get { return _isDone; }
        }

        #endregion
    }
}

