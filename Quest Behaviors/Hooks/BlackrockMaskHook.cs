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


namespace Honorbuddy.Quest_Behaviors.Hooks
{
	[CustomBehaviorFileName(@"Hooks\BlackrockMaskHook")]
    public class BlackrockMaskHook : CustomForcedBehavior
    {
        public BlackrockMaskHook(Dictionary<string, string> args)
            : base(args)
        {

                QuestId = 0;//GetAttributeAsQuestId("QuestId", true, null) ?? 0;
                //True = hook running, false = hook stopped
                state = GetAttributeAsNullable<bool>("state", false, null, null) ?? false;
 
        }
        public int QuestId { get; set; }
        private bool _isBehaviorDone;


        private bool state;

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


        public static bool Disguised
        {
            get { return StyxWoW.Me.HasAura(89261); }
        }

        public static Composite _myHook;
        public static Composite myHook
        {
            get
            {
                if (_myHook == null)
                {
                    _myHook = new Decorator(r => Disguise != null && StyxWoW.Me.IsAlive && !StyxWoW.Me.Combat && StyxWoW.Me.ZoneId == 46 && !Disguised, new Action(r =>
                    {
                        Navigator.PlayerMover.MoveStop();
                        Disguise.Use();
                    }));                    
					return _myHook;
                }
                else
                {
                    return _myHook;
                }
            }
            set
            {
                _myHook = value;
            }
        }

        public override void OnStart()
        {
            OnStart_HandleAttributeProblem();

            Logging.Write("BlackrockMaskHook:{0}  - gfrsa",state);
            if (state == true)
            {
                if (_myHook == null)
                {
                    Logging.Write("BlackrockMaskHook:Inserting hook - gfrsa");
                    TreeHooks.Instance.InsertHook("Questbot_Main", 0, myHook);
                }
                else
                {
                    Logging.Write("BlackrockMaskHook:Insert was requested, but was already present - gfrsa");
                }


            }
            else
            {
                if (_myHook != null)
                {
                    Logging.Write("BlackrockMaskHook:Removing hook - gfrsa");
                    TreeHooks.Instance.RemoveHook("Questbot_Main", myHook);
                    myHook = null;
                }
                else
                {
                    Logging.Write("BlackrockMaskHook:Remove was requested, but hook was not present - gfrsa");
                }

            }


            /*if (_myHook == null)
            {
                Logging.Write("BlackrockMaskHook:Inserting hook - gfrsa");
                TreeHooks.Instance.InsertHook("Questbot_Main", 0, myHook);
            }
            else
            {
                Logging.Write("BlackrockMaskHook:Removing hook - gfrsa");
                TreeHooks.Instance.RemoveHook("Questbot_Main", myHook);
                myHook = null;
            }*/
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
