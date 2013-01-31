using System.Collections.Generic;
using System.Linq;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

namespace AnAncientEvil
{
    public class AnAncientEvil : CustomForcedBehavior
    {
        public AnAncientEvil(Dictionary<string, string> args)
            : base(args)
        {

                QuestId = 29798;//GetAttributeAsQuestId("QuestId", true, null) ?? 0;
 
        }
        public int QuestId { get; set; }
        private bool _isBehaviorDone;

        //<Vendor Name="Zhao-Ren" Entry="55786" Type="Repair" X="713.9167" Y="4168.126" Z="213.846" />
        

        private Composite _root;
        
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

        public override void OnStart()
        {
            OnStart_HandleAttributeProblem();
            if (!IsDone)
            {

                if (TreeRoot.Current != null && TreeRoot.Current.Root != null && TreeRoot.Current.Root.LastStatus != RunStatus.Running)
                {
                    var currentRoot = TreeRoot.Current.Root;
                    if (currentRoot is GroupComposite)
                    {
                        var root = (GroupComposite)currentRoot;
                        root.InsertChild(0, CreateBehavior());
                    }
                }

                PlayerQuest Quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
                TreeRoot.GoalText = ((Quest != null) ? ("\"" + Quest.Name + "\"") : "In Progress");
            }
        }

        //<Vendor Name="Vordraka, the Deep Sea Nightmare" Entry="56009" Type="Repair" X="267.6218" Y="4036.052" Z="68.99686" />
        public WoWUnit Vord
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>(true).FirstOrDefault(u => u.Entry == 56009);
            }
        }

        //<Vendor Name="Deepscale Aggressor" Entry="60685" Type="Repair" X="287.0461" Y="4015.281" Z="75.54617" />


        public WoWUnit Add
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>(true).FirstOrDefault(u => u.Entry == 60685 && u.IsAlive);
            }
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

        private static WoWPoint CalculatePointBehindTarget()
        {
            return
                StyxWoW.Me.CurrentTarget.Location.RayCast(
                    StyxWoW.Me.CurrentTarget.Rotation + WoWMathHelper.DegreesToRadians(150),10f);
        }
        
        public Composite DpsHim
        {
            get
            {
                return new PrioritySelector(
                    new Decorator(r => Me.CurrentTarget == null && Vord != null && Me.CurrentTarget != Vord && Add == null, new Action(r => Vord.Target())),
                    new Decorator(r => Me.CurrentTarget == null || Me.CurrentTarget == Vord && Add != null, new Action(r=>Add.Target())),

                    new Decorator(r => Vord != null && Vord.IsCasting && !Vord.MeIsSafelyBehind, new Action(r=>Navigator.MoveTo(CalculatePointBehindTarget()))),
                    
                    RoutineManager.Current.CombatBehavior
                    
                    );
            }
        }





        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, DpsHim, new ActionAlwaysSucceed())));
        }
    }
}
