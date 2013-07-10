using System.Collections.Generic;
using System.Linq;
using System.Threading;
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


namespace Honorbuddy.Quest_Behaviors.TheEndlessFlowHook
{
    [CustomBehaviorFileName(@"TheEndlessFlowHook")]
    public class TheEndlessFlowHook : CustomForcedBehavior
    {
        public TheEndlessFlowHook(Dictionary<string, string> args)
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


        //	44360	Scourge Bone Animus Bunny	34.640281677246094	<1479.48, -1413.77, 67.12534>	<1479.48, -1413.77, 67.12534>	2.443461	6.5	0		False	False	False	False	False	False	False	Position: <1479.48, -1413.77, 67.12534>, Heading: 2.443461, TransportGuid: 0, DirectionCosX: -0.7660444, DirectionSinX: 0.6427876, DirectionCosY: 1, DirectionSinY: 0, FallTime: 0, FallStartHeight: 67.12534, LastFallHeight: 0, CurrentSpeed: 0, BackwardSpeed: 2.5, ForwardSpeed: 8, RunSpeed: 4.5, SwimmingForwardSpeed: 2.5, SwimmingBackwardSpeed: 2.5, FlyingForwardSpeed: 7, FlyingBackwardSpeed: 4.5, HoverHeight: 1, TimeMoved: 0, MovementFlags: 512, IsFalling: False, MovingForward: False, MovingBackward: False, MovingStrafeLeft: False, MovingStrafeRight: False, MovingTurnLeft: False, MovingTurnRight: False, IsMoving: False, IsStrafing: False, IsSwimming: False, IsFlying: False, CanFly: False, IsAscending: False, IsDescending: False, JumpingOrShortFalling: False, FallingFar: False		0		0		0		0		0		0		0		0				False	0	0	NaN	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	False	6104	6104	0	6104	6104	1	610	61	6104	6104	6104	6104	0	6104	6104	6104	610	61	6104	6104	100	100	100	100	100	100	100	NaN	100	100	100	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo		0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	2000	2000	0	0	0	0	0	2.5	0	0	0	0	0	0	0	0	0	0	0		0	0	0	0	0		False	False	False	False	False	0	0	0		False	60	35	Villian, Parent: 	Styx.WoWInternals.WoWFactionTemplate	1.25	20024	20024	0	False	1	1	0	33554688	2048	0	0		0	Warrior	0	Rage	Unknown			Normal	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False		Leather	NotSpecified	False	False	False	False	False	False	False	False	False	False	False	False	Normal	False	False	False	TargetGuid: 04000000065F3998, RawPercent: 0, ThreatStatus: UnitNotInThreatTable, ThreatValue: 0	False	False	Friendly	True	False	False	False	False	True	True	False	0		False	False	False	False	False	530789612	True	Unit	Object, Unit	17667811862933261906	0	42.25	False	False	17667811862933261906	1479.48	-1413.77	67.1253357	140	1199.9490548591712	13.283207893371582	176.44362403452396	False	<1479.48, -1413.77, 67.12534>	None	None	False	False	True	False	False	False

        public static WoWUnit Bunny
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(r => r.Entry == 44360).OrderBy(r=>r.Distance2D).FirstOrDefault();
            }
        }
        //44329/
        public static WoWUnit Stickbone
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(r => r.Entry == 44329 && r.IsAlive).OrderBy(r => r.Distance2D).FirstOrDefault();
            }
        }

        public static WoWItem Bomb
        {
            get
            {
                return StyxWoW.Me.BagItems.FirstOrDefault(r => r.Entry == 60849);
            }
        }

    

        private Composite myHook;
        public override void OnStart()
        {
            OnStart_HandleAttributeProblem();
            
            if (myHook == null)
            {
                myHook = new PrioritySelector(
                    //Remove hook once were done with the quest
                    new Decorator(r=> Bomb == null, new Action(r=>TreeHooks.Instance.RemoveHook("Questbot_Main", myHook))),
                    new Decorator(r=> Bunny != null && Bunny.Distance < 35 && Bunny.Distance > 5, new Action(r=>Navigator.MoveTo(Bunny.Location))),
                    
                   
                    new Decorator(r=> Bunny != null && Bunny.Distance <=5, new Sequence(
							new Action(ret => Bomb.Use()),
							new WaitContinue(1, ret => Me.CurrentPendingCursorSpell != null, new ActionAlwaysSucceed()),
							new Action(ret => SpellManager.ClickRemoteLocation(Bunny.Location)))),
							
							
                  new Decorator(r => Stickbone != null && Stickbone.Distance <= 5, new Sequence(
							new Action(ret => Bomb.Use()),
							new WaitContinue(1, ret => Me.CurrentPendingCursorSpell != null, new ActionAlwaysSucceed()),
							new Action(ret => SpellManager.ClickRemoteLocation(Stickbone.Location))))

                        
                    
                    );
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
