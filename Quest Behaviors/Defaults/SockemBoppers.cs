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


namespace Honorbuddy.Quest_Behaviors.SockemBoppers
{
    [CustomBehaviorFileName(@"SockemBoppers")]
    public class SockemBoppers : CustomForcedBehavior
    {
        public SockemBoppers(Dictionary<string, string> args)
            : base(args)
        {

                QuestId = GetAttributeAsNullable<uint>("QuestId", true, null,null) ?? 0;
                MobIds = GetAttributeAsArray<uint>("MobIds", false, null, new[] { "NpcIds" }, null);
 
        }
        public uint QuestId { get; set; }
        private bool _isBehaviorDone;


        public int spout = 60488;

        public int Xaril = 62151;
        private Composite _root;

        private uint[] MobIds { get; set; }
        
        public QuestCompleteRequirement questCompleteRequirement = QuestCompleteRequirement.NotComplete;
        public QuestInLogRequirement questInLogRequirement = QuestInLogRequirement.InLog;
        
        public override bool IsDone
        {
            get
            {
                return _isBehaviorDone;
            }
        }

       
        private LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
        }



        public static WoWItem Bopper
        {
            get
            {
                return StyxWoW.Me.BagItems.FirstOrDefault(r => r.Entry == 63390);
            }
        }




        private Composite myHook;
        public override void OnStart()
        {
            OnStart_HandleAttributeProblem();

        }

        private WoWUnit Gooby
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>()
                                 .Where(r => MobIds.Contains(r.Entry) && r.IsAlive).OrderBy(r=>r.Distance2D)
                                 .FirstOrDefault();
            }
        }


        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = new PrioritySelector(
                
                DoneYet,
                new Decorator(r=> (!Me.GotTarget || !Me.CurrentTarget.IsAlive) && Gooby != null, new Action(r=>Gooby.Target())),
                new Decorator(r=> Me.GotTarget &&  Me.CurrentTarget.Distance > 3, new Action(r=>Navigator.MoveTo(Me.CurrentTarget.Location))),
                new Decorator(r => Me.GotTarget && Me.CurrentTarget.Distance <= 3, new Action(r=>Bopper.Use(true)))
                
                ));
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
