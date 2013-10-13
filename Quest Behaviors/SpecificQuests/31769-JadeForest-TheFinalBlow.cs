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


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.TheFinalBlow
{
    [CustomBehaviorFileName(@"SpecificQuests\31769-JadeForest-TheFinalBlow")]
    public class TheFinalBlow : CustomForcedBehavior
    {
        private bool _isBehaviorDone;

        //<Vendor Name="Thunder Hold Soldier" Entry="66200" Type="Repair" X="3150.509" Y="-853.8038" Z="268.2579" />
        //	66203	Thunder Hold Cannon	135.46882629394531	<3124.805, -880.6007, 278.7155>	<3124.805, -880.6007, 278.7155>	1.26960659	2	0		False	False	False	False	False	False	False	Position: <3124.805, -880.6007, 278.7155>, Heading: 1.269607, TransportGuid: 0, DirectionCosX: 0.2966566, DirectionSinX: 0.9549842, DirectionCosY: 1, DirectionSinY: 0, FallTime: 0, FallStartHeight: 278.7155, LastFallHeight: 0, CurrentSpeed: 0, BackwardSpeed: 2.5, ForwardSpeed: 9.5, RunSpeed: 4.5, SwimmingForwardSpeed: 2.5, SwimmingBackwardSpeed: 2.5, FlyingForwardSpeed: 7, FlyingBackwardSpeed: 4.5, HoverHeight: 1, TimeMoved: 0, MovementFlags: 1536, IsFalling: False, MovingForward: False, MovingBackward: False, MovingStrafeLeft: False, MovingStrafeRight: False, MovingTurnLeft: False, MovingTurnRight: False, IsMoving: False, IsStrafing: False, IsSwimming: False, IsFlying: False, CanFly: False, IsAscending: False, IsDescending: False, JumpingOrShortFalling: False, FallingFar: False		0		0		0		0		0		0		0		0				False	0	0	NaN	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	False	128992	128992	0	128992	128992	13	12899	1289	128992	128992	128992	128992	0	128992	128992	128992	12899	1289	128992	128992	100	100	100	100	100	100	100	NaN	100	100	100	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	Styx.WoWInternals.WoWObjects.WoWUnit+PowerInfo	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	2000	3489660935	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0		0	0	0	0	0		False	False	False	False	False	0	0	0		False	84	84	Quilboar, Razormane, Parent: 	Styx.WoWInternals.WoWFactionTemplate	0.465	45101	45101	0	False	1	1	0	33280	104892416	0	0		0	Warrior	0	Rage				Normal	False	False	False	False	True	False	False	False	False	False	True	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False	False		Leather	NotSpecified	False	False	False	False	False	False	False	False	False	False	False	False	Normal	False	False	False	TargetGuid: 028000000537929E, RawPercent: 0, ThreatStatus: UnitNotInThreatTable, ThreatValue: 0	False	False	Hostile	False	True	False	False	False	True	True	False	19		False	False	False	False	False	692421404	True	Unit	Object, Unit	17379675301744864288	0	4	False	False	17379675301744864288	3124.80469	-880.6007	278.7155	72.7431	18351.803667366505	133.47869873046875	17816.563694059849	False	<3124.805, -880.6007, 278.7155>	None	TaxiNotEligible	False	False	True	False	True	False

        private Composite _root;
        private WoWPoint bounce = new WoWPoint(3158.702, -934.0057, 324.6955);
        private WoWPoint spot = new WoWPoint(3157.633, -894.3948, 324.696);
        private int stage = 0;

        public TheFinalBlow(Dictionary<string, string> args) : base(args)
        {
            try
            {
                QuestId = 31769; //GetAttributeAsQuestId("QuestId", true, null) ?? 0;
                //Destination = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
            }
            catch
            {
                Logging.Write("Problem parsing a QuestId in behavior: Rampage Against The Machine");
            }
        }

        public int QuestId { get; set; }


        public override bool IsDone
        {
            get { return _isBehaviorDone; }
        }

        private LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
        }


        public WoWGameObject Barricade
        {
            get { return ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(u => u.Entry == 215650 && u.Distance < 10); }
        }

        public WoWItem Gun
        {
            get { return Me.BagItems.FirstOrDefault(r => r.Entry == 89769); }
        }


        //	215650	Thunder Hold Explosives		4.75	<3189.123, -924.9722, 280.3177>	<3189.123, -924.9722, 280.3177>	472		0		0		0	0	0		Ready	Generic	Styx.WoWInternals.WoWObjects.WoWAnimatedSubObject	None	0	255	False	False	False	False	False	None	False	False	World\Goober\G_BarrelExplode.mdx		False	226674480	True	GameObject	Object, GameObject	17371309972465392432	0	22.5625	True	False	17371309972465392432	3189.12329	-924.9722	280.3177	0	0	3.4139407994225621	1.8465468883514404	3.4097352959215641	False	<3189.123, -924.9722, 280.3177>	None	TaxiNotEligible	False	True	False	True	True	False


        public Composite DoneYet
        {
            get
            {
                return new Decorator(
                    ret => IsQuestComplete(),
                    new Action(
                        delegate
                        {
                            TreeRoot.StatusText = "Finished!";
                            _isBehaviorDone = true;
                            return RunStatus.Success;
                        }));
            }
        }


        private Composite HandleCombat
        {
            get
            {
                return new PrioritySelector(
                    new Decorator(ret => !StyxWoW.Me.Combat, RoutineManager.Current.PreCombatBuffBehavior),
                    new Decorator(
                        ret => StyxWoW.Me.Combat,
                        new PrioritySelector(
                            RoutineManager.Current.HealBehavior,
                            new Decorator(
                                ret => StyxWoW.Me.GotTarget && !StyxWoW.Me.CurrentTarget.IsFriendly && !StyxWoW.Me.CurrentTarget.IsDead,
                                new PrioritySelector(RoutineManager.Current.CombatBuffBehavior, RoutineManager.Current.CombatBehavior)))));
            }
        }


        public Composite BlowUp
        {
            get
            {
                return new Decorator(
                    r => Barricade != null,
                    new Action(
                        delegate
                        {
                            Gun.Use();
                            return RunStatus.Failure;
                        }));
            }
        }


        //<Vendor Name="dddsd" Entry="0" Type="Repair" X="" />
        public Composite Move
        {
            get
            {
                return new PrioritySelector(
                    new Decorator(r => spot.Distance(Me.Location) > 10 && stage <= 0, new Action(r => Navigator.MoveTo(spot))),
                    new Decorator(r => spot.Distance(Me.Location) < 10 && stage == 0, new Action(r => stage = 1)));
            }
        }


        public Composite bouncez
        {
            get
            {
                return new PrioritySelector(
                    new Decorator(r => bounce.Distance(Me.Location) > 3 && stage == 1, new Action(r => Navigator.MoveTo(bounce))),
                    new Decorator(r => bounce.Distance(Me.Location) <= 3 && stage == 1, new Action(r => stage = -1)));
            }
        }

        private bool _useMount;
        public override void OnStart()
        {
            OnStart_HandleAttributeProblem();
            if (!IsDone)
            {
                TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_MainCombat());
                _useMount = CharacterSettings.Instance.UseMount;
                CharacterSettings.Instance.UseMount = false;

                PlayerQuest Quest = StyxWoW.Me.QuestLog.GetQuestById((uint) QuestId);
                TreeRoot.GoalText = ((Quest != null) ? ("\"" + Quest.Name + "\"") : "In Progress");
            }
        }

        public bool IsQuestComplete()
        {
            var quest = StyxWoW.Me.QuestLog.GetQuestById((uint) QuestId);
            return quest == null || quest.IsCompleted;
        }


        protected Composite CreateBehavior_MainCombat()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, BlowUp, HandleCombat, bouncez, Move)));
        }

        #region Cleanup

        private bool _isDisposed;

        ~TheFinalBlow()
        {
            Dispose(false);
        }

        public void Dispose(bool isExplicitlyInitiatedDispose)
        {
            if (!_isDisposed)
            {
                // NOTE: we should call any Dispose() method for any managed or unmanaged
                // resource, if that resource provides a Dispose() method.

                // Clean up managed resources, if explicit disposal...
                if (isExplicitlyInitiatedDispose)
                {
                    CharacterSettings.Instance.UseMount = _useMount;
                    TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_MainCombat());
                }

                // Clean up unmanaged resources (if any) here...
                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}