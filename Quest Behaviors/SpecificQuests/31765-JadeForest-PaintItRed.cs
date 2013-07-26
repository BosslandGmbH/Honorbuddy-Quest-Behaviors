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


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.PaintItRed
{
    [CustomBehaviorFileName(@"SpecificQuests\31765-JadeForest-PaintItRed")]
    public class PaintItRed : CustomForcedBehavior
    {
        public PaintItRed(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                QuestId = 31765;//GetAttributeAsQuestId("QuestId", true, null) ?? 0;
            }
            catch
            {
                Logging.Write("Problem parsing a QuestId in behavior: Rampage Against The Machine");
            }
        }
        public int QuestId { get; set; }
        private bool _isBehaviorDone;


        //<Vendor Name="Thunder Hold Soldier" Entry="66200" Type="Repair" X="3150.509" Y="-853.8038" Z="268.2579" />
        //	66203	Thunder Hold Cannon	135.46882629394531	<3124.805, -880.6007, 278.7155>	<3124.805, -880.6007, 278.7155>	1.26960659	2	0		False	False	False	False	False	False	False	Position: <3124.805, -880.6007, 278.7155>, Heading: 1.269607, TransportGuid: 0, DirectionCosX: 0.2966566, DirectionSinX: 0.9549842, DirectionCosY: 1, DirectionSinY: 0, FallTime: 0, FallStartHeight: 278.7155, LastFallHeight: 0, CurrentSpeed: 0, BackwardSpeed: 2.5, ForwardSpeed: 9.5, RunSpeed: 4.5, SwimmingForwardSpeed: 2.5, SwimmingBackwardSpeed: 2.5, FlyingForwardSpeed: 7, FlyingBackwardSpeed: 4.5, HoverHeight: 1, TimeMoved: 0, MovementFlags: 1536, IsFalling: False, MovingForward: False, MovingBackward: False, MovingStrafeLeft: False, MovingStrafeRight: False, MovingTurnLeft: False, MovingTurnRight: False, IsMoving: False, IsStrafing: False, IsSwimming: False, IsFlying: False, CanFly: False, IsAscending: False, IsDescending: False, JumpingOrShortFalling: False, FallingFar: False		0		0		0		0		0		0		0		0				False	0	0	NaN	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	False	128992	128992	0	128992	128992	13	12899	1289	128992	128992	128992	128992	0	128992	128992	128992	12899	1289	128992	128992	100	100	100	100	100	100	100	NaN	100	100	100	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	2000	3489660935	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0		0	0	0	0	0		False	False	False	False	False	0	0	0		False	84	84	Quilboar, Razormane, Parent: 	Styx.WoWInternals.WoWFactionTemplate	0.465	45101	45101	0	False	1	1	0	33280	104892416	0	0		0	Warrior	0	Rage				Normal	False	False	False	False	True	False	False	False	False	False	True	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False		Leather	NotSpecified	False	False	False	False	False	False	False	False	False	False	False	False	Normal	False	False	False	TargetGuid: 028000000537929E, RawPercent: 0, ThreatStatus: UnitNotInThreatTable, ThreatValue: 0	False	False	Hostile	False	True	False	False	False	True	True	False	19		False	False	False	False	False	692421404	True	Unit	Object, Unit	17379675301744864288	0	4	False	False	17379675301744864288	3124.80469	-880.6007	278.7155	72.7431	18351.803667366505	133.47869873046875	17816.563694059849	False	<3124.805, -880.6007, 278.7155>	None	TaxiNotEligible	False	False	True	False	True	False




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



        public WoWUnit Solider
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 66200 && u.IsAlive).OrderBy(u => u.Distance).FirstOrDefault();
            }
        }

        public WoWUnit Cannon
        {
            get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(r => r.IsAlive && r.Entry == 66203); }
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


                        Lua.DoString("VehicleExit()");
                        _isBehaviorDone = true;
                        return RunStatus.Success;

                    }));

            }
        }


        private void shoot(WoWUnit who)
        {
            var v = who.Location - StyxWoW.Me.Transport.Location;
            v.Normalize();
            Lua.DoString(string.Format("local pitch = {0}; local delta = pitch - VehicleAimGetAngle(); VehicleAimIncrement(delta);", Math.Asin(v.Z)));


            //If the target is moving, the projectile is not instant
            if (who.IsMoving)
            {
                WoWMovement.ClickToMove(Me.CurrentTarget.Location.RayCast(who.Rotation, 10f));
            }
            else
            {
                WoWMovement.ClickToMove(who.Location);
            }
            //Fire pew pew
            Lua.DoString("CastPetAction({0})", 1);
        }


       

        public Composite KillSoldier
        {
            get
            {
                return


                    new Decorator(r => !IsObjectiveComplete(1, (uint) QuestId) && Solider != null,
                                  new Action(r => shoot(Solider)));


            }

        }

        public Composite KillCannon
        {
            get
            {
                return


                    new Decorator(r => !IsObjectiveComplete(2, (uint) QuestId) && Cannon != null,
                                  new Action(r => shoot(Cannon)));


            }

        }

        public Composite EnsureTarget
        {
            get
            {
                return new Decorator(r=> Me.GotTarget && !Me.CurrentTarget.IsHostile, new Action(r=>Me.ClearTarget()));
            }
        }

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet,EnsureTarget, KillSoldier, KillCannon)));
        }
    }
}



