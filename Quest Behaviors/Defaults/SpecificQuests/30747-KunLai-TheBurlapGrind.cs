using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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



namespace TheBurlapGrind
{
    public class TheBurlapGrind : CustomForcedBehavior
    {
        public TheBurlapGrind(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                QuestId = 30747;//GetAttributeAsQuestId("QuestId", true, null) ?? 0;
            }
            catch
            {
                Logging.Write("Problem parsing a QuestId in behavior: Rampage Against The Machine");
            }
        }
        public int QuestId { get; set; }
        private bool _isBehaviorDone;

        public uint[] MobIds = new uint[] { 60749, 60746, 60752, 60753, 60743, }; 


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
                CharacterSettings.Instance.UseMount = false;

                PlayerQuest Quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
                TreeRoot.GoalText = ((Quest != null) ? ("\"" + Quest.Name + "\"") : "In Progress");
            }
        }

        //<Vendor Name="Broketooth Ravager" Entry="60743" Type="Repair" X="2593.242" Y="1785.711" Z="665.8227" />

        public List<WoWUnit> Monkies
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => MobIds.Contains(u.Entry) && !u.IsDead && u.Distance < 350 && (point1.Distance(u.Location) < 80 || point2.Distance(u.Location) < 60)).OrderBy(u => u.Distance).ToList();
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


                        if(Me.Location.Distance(StartPoint) < 25)
                        {
                            CharacterSettings.Instance.UseMount = true;
                            Lua.DoString("VehicleExit()");
                            _isBehaviorDone = true;
                            return RunStatus.Success;
                        }
                        else
                        {
                            Logging.Write("Moving to start location");
                            Navigator.MoveTo(StartPoint);
                            return RunStatus.Failure;
                        }

                        
                        
                    }));

            }
        }


        public WoWUnit Yeti
        {
            get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(r => r.NpcFlags == 1 && r.Entry == 60587 && r.Location.Distance(StartPoint) < 30); }
        }
        //Point 1
        //<Vendor Name="Kota Kon" Entry="60754" Type="Repair" X="2794.707" Y="1695.803" Z="642.4144" />
        private WoWPoint point1 = new WoWPoint(2794.707,1695.803,642.4144);
        //Point 2
        //<Vendor Name="Kota Kon" Entry="60754" Type="Repair" X="2738.181" Y="1908.858" Z="622.8558" />
        private WoWPoint point2 = new WoWPoint(2738.181,1908.858,622.8558);
        //Wait point
        //<Vendor Name="Kota Kon" Entry="60754" Type="Repair" X="2793.078" Y="1773.688" Z="638.9326" />
        private WoWPoint WaitPoint = new WoWPoint(2793.078,1773.688,638.9326);

        //Start point
        //<Vendor Name="Kota Kon" Entry="60754" Type="Repair" X="2726.94" Y="2179.04" Z="590.6934" />
        private WoWPoint StartPoint = new WoWPoint(2726.94,2179.04,590.6934);

        public Composite MantidKill
        {
            get
            {
                return new Decorator(r => !IsQuestComplete() && Me.InVehicle, new Action(c =>
                    {
                        TreeRoot.StatusText = "Moving to Attack";
                        //<Vendor Name="Klaxxi Kunchong Destroyer" Entry="64834" Type="Repair" X="-58.25938" Y="3466.082" Z="113.1098" />
                        var hostile =
                            ObjectManager.GetObjectsOfType<WoWUnit>().Where(r => r.Entry != 60754 && r.GotTarget && r.CurrentTarget == Me.CharmedUnit && (point1.Distance(r.Location) < 80 || point2.Distance(r.Location) < 60)).OrderBy(r => r.Distance).FirstOrDefault();


                        //<Vendor Name="Dread Behemoth" Entry="67039" Type="Repair" X="-153.9916" Y="3373.808" Z="103.9902" />
                        //<Vendor Name="Ik'thik Kunchong" Entry="67036" Type="Repair" X="-142.2761" Y="3578.237" Z="118.6222" />
                        WoWUnit tar;



                        if (Me.GotTarget && Me.CurrentTarget.Distance < 20)
                        {
                            tar = Me.CurrentTarget;
                        }
                        else if (hostile != null)
                        {
                            tar = hostile;
                        }
                        else if (Monkies.Count > 0)
                        {
                            tar = Monkies.FirstOrDefault();
                        }
                        else
                        {

                            if (Me.Location.Distance(WaitPoint) > 20)
                            {
                                Navigator.MoveTo(WaitPoint);
                            }
                            else
                            {
                                Logging.Write("No viable targets, waiting.");
                            }
                            
                            return RunStatus.Failure;
                        }
                        /*else
                        {
                            var xtra =
                                ObjectManager.GetObjectsOfType<WoWUnit>().Where(
                                    r => (r.Entry == 67039 || r.Entry == 67036) && r.IsAlive).OrderBy(
                                        r => r.Distance).FirstOrDefault();

                            if (xtra != null)
                            {
                                tar = xtra;
                            }
                            else
                            {
                                Logging.Write("No viable targets, waiting.");
                                return RunStatus.Failure;
                            }
                        }*/

                        if (tar.Location.Distance(Me.CharmedUnit.Location) > 15)
                        {
                            //WoWMovement.ClickToMove(tar.Location);
                            Navigator.MoveTo(tar.Location);
                            tar.Target();
                            tar.Face();

                            Lua.DoString("CastPetAction(1)");
                            if (StyxWoW.Me.CurrentPendingCursorSpell != null)
                            {
                                SpellManager.ClickRemoteLocation(tar.Location);
                            }

                        }
                        else
                        {
                            //WoWMovement.ClickToMove(tar.Location);
                            tar.Target();
                            //WoWMovement.MoveStop();
                            //Thread.Sleep(400);
                            tar.Face();
                            Lua.DoString("CastPetAction(2)");
                            Lua.DoString("CastPetAction(4)");
                            //WoWMovement.MoveStop();
                            if (Me.IsMoving || Me.CharmedUnit.IsMoving)
                                WoWMovement.ClickToMove(Me.CharmedUnit.Location);
                        }






                        return RunStatus.Failure;
                    }));

            }
        }

        public Composite DoDps
        {
            get
            {
                return
                    new PrioritySelector(RoutineManager.Current.CombatBuffBehavior, RoutineManager.Current.CombatBehavior);
            }

        }


        public Composite GetIn
        {
            get
            {
                return new Decorator(r=>!Me.InVehicle, new PrioritySelector(
                    new Decorator(r=>Me.Combat,DoDps),
                    new Decorator(r => Yeti != null, new Action(r => { Yeti.Interact();Lua.DoString("SelectGossipOption(1,\"gossip\", true)");})),
                    new Decorator(r=> Yeti == null,new Action(r=>Navigator.MoveTo(StartPoint)))
                    
                    
                    
                    ));
            }
        }



        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet,GetIn, MantidKill)));
        }
    }
}



