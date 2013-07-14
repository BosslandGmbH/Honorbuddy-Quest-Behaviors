using System.Collections.Generic;
using System.Linq;

using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.BlackrockMaskHook
{
    [CustomBehaviorFileName(@"BlackrockMaskHook")]
    public class BlackrockMaskHook : CustomForcedBehavior
    {
        public BlackrockMaskHook(Dictionary<string, string> args)
            : base(args)
        {

                QuestId = 0;//GetAttributeAsQuestId("QuestId", true, null) ?? 0;
 
        }
        public int QuestId { get; set; }
        private bool _isBehaviorDone;


        public int spout = 60488;

        public int Xaril = 62151;
        private Composite _root;
        
        public QuestCompleteRequirement questCompleteRequirement = QuestCompleteRequirement.NotComplete;
        public QuestInLogRequirement questInLogRequirement = QuestInLogRequirement.InLog;
        
        public override bool IsDone
        {
            get
            {
                return inserted;
            }
        }

        private bool inserted = false;
        private LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
        }



        public static WoWItem Disguise
        {
            get
            {
                return StyxWoW.Me.BagItems.FirstOrDefault(r => r.Entry == 63357);
            }
        }

        public int[] Auras = new int[] { 89259, 89260, 89254, 89253, 89256, 89255, 89258, 89257 };


        public bool Disguised
        {
            get { return Me.HasAura(89261); }
        }

                public static Composite _myHook;
        public static Composite myHook
        {
            get
            {
                if (_myHook == null)
                {
                    _myHook = new Decorator(r => Disguise != null && Me.IsAlive && !Me.Combat && Me.ZoneId == 46 && !Disguised, new Action(r =>
                    {
                        Navigator.PlayerMover.MoveStop();
                        Disguise.Use();
                    }));                    return _myHook;
                }
                else
                {
                    return _myHook;
                }
            }
        }
        public override void OnStart()
        {
            OnStart_HandleAttributeProblem();
            
            if (myHook == null)
            {

                TreeHooks.Instance.InsertHook("Questbot_Main", 0, myHook);
            }
            else
            {
                TreeHooks.Instance.RemoveHook("Questbot_Main", myHook);
            }



            inserted = true;
        }




        public bool IsQuestComplete()
        {
            var quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
            return quest == null || quest.IsCompleted;
        }
        private bool IsObjectiveComplete(int objectiveId, uint questId)
        {
            if (Me.QuestLog.GetQuestById(questId) == null)
            {
                return false;
            }
            int returnVal = Lua.GetReturnVal<int>("return GetQuestLogIndexByID(" + questId + ")", 0);
            return
                Lua.GetReturnVal<bool>(
                    string.Concat(new object[] { "return GetQuestLogLeaderBoard(", objectiveId, ",", returnVal, ")" }), 2);
        }

        public Composite DoneYet
        {
            get
            {
                return
                    new Decorator(ret => IsQuestComplete(), new Action(delegate
                    {
                        TreeRoot.StatusText = "Finished!";
                        _isBehaviorDone = true;
                        return RunStatus.Success;
                    }));

            }
        }


        





    }
}
