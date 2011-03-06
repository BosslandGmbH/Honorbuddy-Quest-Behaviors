using System.Collections.Generic;
using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using TreeSharp;
using System.Diagnostics;

using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// RunLua by HighVoltz
    /// Runs the lua script x amount of times waiting x milliseconds inbetween
    /// ##Syntax##           
    /// Lua: the lua script to run
    /// NumOfTimes: (Optional) - The number of times to execute this script. default:1
    /// QuestId: (Optional) - the quest to perform this action on
    /// WaitTime: (Optional) - The time in milliseconds to wait before executing the next. default:0
    /// </summary>
    public class RunLua : CustomForcedBehavior
    {
        Dictionary<string, object> _recognizedAttributes = new Dictionary<string, object>()
        {
            {"Lua",null},
            {"QuestId",null},
            {"NumOfTimes",null},
            {"WaitTime",null},
        };
        bool success = true;
        public RunLua(Dictionary<string, string> args)
            : base(args)
        {
            // tba. dictionary format is not documented.
            // CheckForUnrecognizedAttributes(recognizedAttributes);
            int numOfTimes = 0;
            int waitTime = 0;
            string lua = "";
            int questId = 0;
            success = success && GetAttributeAsString("Lua", true, "", out lua);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
            success = success && GetAttributeAsInteger("NumOfTimes", false, "1", 1, int.MaxValue, out numOfTimes);
            success = success && GetAttributeAsInteger("WaitTime", false, "0", 0, int.MaxValue, out waitTime);

            Lua = lua;
            NumOfTimes = numOfTimes;
            QuestId = questId;
            WaitTime = waitTime;
        }

        public string Lua { get; private set; }
        public int NumOfTimes { get; private set; }
        public int QuestId { get; private set; }
        public int WaitTime { get; private set; }

        int _counter;
        readonly Stopwatch _waitSw = new Stopwatch();

        #region Overrides of CustomForcedBehavior

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ??
                (_root = new PrioritySelector(

                    new Decorator(c => !success,
                        new Action(c => Err("Invalid or missing Attributes, Stopping HB"))),

                    new Action(c =>
                    {
                        if (!_waitSw.IsRunning && WaitTime >0)
                            _waitSw.Start();

                        if (_waitSw.ElapsedMilliseconds < WaitTime)
                            return;

                        WoWInternals.Lua.DoString(Lua);
                        _counter++;
                        _waitSw.Reset();
                    })
                ));
        }

        private static void Err(string format, params object[] args)
        {
            Logging.Write(System.Drawing.Color.Red, "RunLua: " + format, args);
            TreeRoot.Stop();
        }

        public override bool IsDone
        {
            get
            {
                var quest = ObjectManager.Me.QuestLog.GetQuestById((uint)QuestId);
                return 
                    _counter >= NumOfTimes || 
                    (QuestId > 0 && ((quest != null && quest.IsCompleted) || quest == null));
            }
        }
        public override void OnStart()
        {
            TreeRoot.GoalText = string.Format("Executing Lua: {0} {1} number of times while waiting {2} inbetween", Lua, NumOfTimes, WaitTime);
        }

        #endregion
    }
}
