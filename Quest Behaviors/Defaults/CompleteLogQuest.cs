using System.Collections.Generic;
using System.Threading;
using Styx.Helpers;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    public class CompleteLogQuest : CustomForcedBehavior
    {

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

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                    new Decorator(ret => Counter >= 1,
                        new Action(ret => _isDone = true)),

                        new PrioritySelector(

                            new Decorator(ret => Counter == 0,
                                new Action(delegate
                                {

                                   // Lua.DoString("ShowQuestComplete(" + QuestID + "); CompleteQuest(); GetQuestReward(1); ");

                                    //uint bob = me.QuestLog.GetQuestById(QuestID).

                                    Lua.DoString("ShowQuestComplete(\"" + questIndexID + "\")");
                                    Thread.Sleep(300);
                                    Lua.DoString("CompleteQuest()");
                                    Thread.Sleep(300);
                                    Lua.DoString("GetQuestReward(1)");
                                    Thread.Sleep(300);
                                    Counter++;
                                })
                                ),

                            new Action(ret => Logging.Write(""))
                        )
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

