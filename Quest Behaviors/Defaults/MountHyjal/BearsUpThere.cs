using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Styx.Database;
using Styx.Logic.Combat;
using Styx.Helpers;
using System.Drawing;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Pathing;
using Styx.Logic.Profiles.Quest;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors.MountHyjal
{
    public class BearsUpThere : CustomForcedBehavior
    {
        /// <summary>
        /// BearsUpThere by Bobby53 
        /// 
        /// Completes the vehicle quest http://www.wowhead.com/quest=25462
        /// 
        /// To use, you must use the Ladder at <RunTo  X="5254.562" Y="-1536.917" Z="1361.341" />
        /// Due to how the coordinate system is relative to the vehicle once you enter, it
        /// is setup to only support this specific ladder.  
        /// 
        /// ##Syntax##
        /// QuestId: Id of the quest (default is 0)
        /// [Optional] QuestName: optional quest name (documentation only)
        /// 
        /// </summary>
        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {
            // {"X",null},
            // {"Y",null},
            // {"Z",null},
            {"QuestId",null},
            {"QuestName",null}
        };

        public uint QuestId { get; set; }

        public LocalPlayer Me { get { return ObjectManager.Me; } }
        bool error = false;

        //  LEVEL: -1=unknown, 0=tree top, 1=highest, 2=middle, 3=lowest
        const int LEVEL_BOTTOM = 1;
        const int LEVEL_TOP = 5;
        const int LEVEL_UNKNOWN = 0;
        int lvlCurrent = LEVEL_UNKNOWN;

        const int AURA_CLIMBING_TREE = 74920;
        const int AURA_IN_TREE = 46598;
        const int CLIMB_UP = 74922;
        const int CLIMB_DOWN_AT_TOP = 75070;
        const int CLIMB_DOWN = 74974;
        const int CHUCK_A_BEAR = 75139;

            /*
RIGHT SIDE:  isontransport:True, rotation:1.356836,  degrees:77.741
LEFT SIDE:  isontransport:True, rotation:1.612091,  degrees:92.366
ENTRY:  isontransport:True, rotation:0.1570796,  degrees:9

             */
        // these are values recorded from tree @ 14:33
        //  ..  when taking the right ladder (while facing tree)
        //  ..  angle while on tree level other than top is always 9
        //  ..  if you are on correct tree and correct side

        const double AIM_ANGLE = -0.97389394044876;
        const double TRAMP_RIGHT_SIDE = 77.741;
        const double TRAMP_LEFT_SIDE = 92.366;


        public BearsUpThere(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);

            // WoWPoint location = new WoWPoint(0, 0, 0);
            int questId = 0;

            // error = error || !GetXYZAttributeAsWoWPoint("X", "Y", "Z", true, new WoWPoint(0, 0, 0), out location);
            error = error || !GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);

            QuestId = (uint)questId;
            if (DoWeHaveQuest() && !IsQuestComplete() && !InTree())
            {
                error = true;
                Log(  "==================================================================");
                Elog( "NOT IN TREE!!!  ENTER TREE TO USE CUSTOM BEHAVIOR");
                Log(  "==================================================================");
            }
            
            if (error)
                TreeRoot.Stop();
        }
        

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                    // check if we left tree/vehicle
                    new Decorator(ret => !InTree(), new Action(ret => _isDone = true)),

                    // is quest abandoned or complete?
                    //  ..  move down until we auto-exit vehicle
                    new Decorator(ret => !DoWeHaveQuest() || IsQuestComplete(), new Action( ret => ClimbDown())),

                    // level unknown and already at top?  set to top then
                    new Decorator(ret => lvlCurrent == LEVEL_UNKNOWN && !IsClimbingTheTree(), 
                                    new Action( delegate
                                        {
                                            lvlCurrent = LEVEL_TOP;
                                            return RunStatus.Success;
                                        })),

                    // level unknown?
                    //  ..  move to top and establish known level
                    new Decorator(ret => lvlCurrent == LEVEL_UNKNOWN, new Action(ret => ClimbUp())),

                    // have a bear in inventory?
                    new Decorator(ret => IsBearCubInBags(), 
                        new PrioritySelector(
                            //  ..  below top?  move up
                            new Decorator(ret => lvlCurrent != LEVEL_TOP, new Action(ret => ClimbUp())),
                            //  ..  aim trajectory angle
                            new Decorator(ret => NeedAimAngle(), new Action(ret => AimAngle())),
                            //  ..  aim direction (left/right)
                            new Decorator(ret => NeedAimDirection(), new Action(ret => AimDirection())),
                            //  ..  throw                           
                            new Action( ret => ChuckBear() )
                            )
                        ),

                    // at top with no bear?
                    //  ..  move down
                    new Decorator(ret => lvlCurrent == LEVEL_TOP, new Action(ret => ClimbDown())),

                    // lootable bears here?
                    //  ..  loot a bear
                    new Decorator(ret => !IsBearCubInBags(), new Action(ret => LootClosestBear())),

                    // can we move down without leaving vehicle?
                    new Decorator(ret => lvlCurrent > LEVEL_BOTTOM, new Action(ret => ClimbDown())),

                    // move up
                    new Decorator(ret => lvlCurrent < LEVEL_TOP, new Action(ret => ClimbUp()))
                    )
                );
        }

        public static void Log( Color clr, string msg, params object[] args)
        {
            Logging.Write(clr, "[BearsUpThere] " + msg, args);
        }

        public static void Log(string msg, params object[] args)
        {
            Log(Color.Blue, msg, args);
        }

        public static void Dlog(string msg, params object[] args)
        {
            msg = String.Format(msg, args);
            Logging.WriteDebug(Color.Blue, "/BearsUpThere\\ " + msg, args);
        }

        public static void Elog(string msg, params object[] args)
        {
            msg = String.Format(msg, args);
            Logging.Write(Color.Red, "[BearsUpThere] ERROR: " + msg, args);
        }

        private void WaitForCurrentSpell()
        {
            while (StyxWoW.GlobalCooldown)
                Thread.Sleep( 100);
            while (StyxWoW.Me.IsCasting)
                Thread.Sleep(100);
        }

        private bool CanCastNow( int spellId )
        {
#if  FIGUERED_OUT_VEHICLE_SPELLS
            if (!SpellManager.HasSpell(spellId))
            {
                Elog("spell manager does not know spellid: {0}", spellId);
                TreeRoot.Stop();
            }

            int stopWaiting = System.Environment.TickCount + 5000;
            while ( !SpellManager.CanCast( spellId) && stopWaiting > Environment.TickCount )
                Thread.Sleep(100);

            return SpellManager.CanCast( spellId );
#else
            WaitForCurrentSpell();
            return true;
#endif
        }

        private RunStatus ClimbUp()
        {
            bool canCast = CanCastNow(CLIMB_UP);
            WoWPoint lastPos = Me.Location;
            Lua.DoString("CastSpellByID({0})", CLIMB_UP);
            WaitForCurrentSpell();
            Thread.Sleep(2000);

            if (Me.Location.Distance(lastPos) != 0)
            {
                Dlog("(Climb Up) moved +{0:F1} yds, pos: {1}", Me.Location.Distance(lastPos), Me.Location);
                if (!IsClimbingTheTree())
                    lvlCurrent = LEVEL_TOP;
                else
                    lvlCurrent++;
            }
            else
                Dlog("(Climb Up) no movement UP occurred");

            return RunStatus.Success;
        }

        private RunStatus ClimbDown()
        {
            int spellId;

            // spell id to move down is different if you are at top of tree
            if (IsClimbingTheTree())
                spellId = CLIMB_DOWN;
            else
                spellId = CLIMB_DOWN_AT_TOP;

            WoWPoint lastPos = Me.Location;
            CanCastNow(spellId);
            Lua.DoString("CastSpellByID({0})", spellId);
            WaitForCurrentSpell();

            // wait longer if at top due to UI skin change
            Thread.Sleep( spellId == CLIMB_DOWN_AT_TOP ? 3000 : 2000);

            if (Me.Location.Distance(lastPos) != 0)
            {
                lvlCurrent--;
                Dlog("(Climb Down) moved -{0:F1} yds, pos: {1}", Me.Location.Distance(lastPos), Me.Location);
            }
            else
                Dlog("(Climb Down) no movement DOWN occurred");
            
            return RunStatus.Success;
        }

        private double GetAimAngle()
        {
            return Lua.GetReturnVal<double>("return VehicleAimGetAngle()", 0);
        }

        private double GetAimAdjustment()
        {
            return GetAimAngle() - AIM_ANGLE;
        }

        private bool NeedAimAngle()
        {
            return Math.Abs(GetAimAdjustment()) > 0.0001;
        }

        private RunStatus AimAngle()
        {
            double angleAdjust = GetAimAdjustment();
            Dlog("(Aim Angle) adjusting current angle {0} by {1} to {2}", GetAimAngle(), angleAdjust, AIM_ANGLE);

            Lua.DoString("VehicleAimDecrement({0})", angleAdjust);

            StyxWoW.SleepForLagDuration();
            return RunStatus.Success;
        }

        private bool NeedAimDirection()
        {
            double normRotation = TRAMP_LEFT_SIDE > TRAMP_RIGHT_SIDE ? 0 : 360;
            if (Me.Transport.RotationDegrees < TRAMP_RIGHT_SIDE)
                return true;

            if ((Me.Transport.RotationDegrees + normRotation) > (TRAMP_LEFT_SIDE + normRotation))
                return true;

            return false;
        }

        private RunStatus AimDirection()
        {
            double normRotation = TRAMP_LEFT_SIDE > TRAMP_RIGHT_SIDE ? 0 : 360;
            double currRotation = Me.Transport.RotationDegrees;
            Dlog("(AimRotation) Trampoline Boundary - Left Edge: {0}  Right Edge: {1}", TRAMP_LEFT_SIDE, TRAMP_RIGHT_SIDE);

            WoWMovement.MovementDirection whichWay = WoWMovement.MovementDirection.None;
            string dirCmd;

            // left/right - get current direction and turn until on trampoline
            if (Me.Transport.RotationDegrees < TRAMP_RIGHT_SIDE)
            {
                whichWay = WoWMovement.MovementDirection.TurnLeft;
                dirCmd = "TurnLeft";
            }
            else if ((Me.Transport.RotationDegrees + normRotation) > (TRAMP_LEFT_SIDE + normRotation))
            {
                whichWay = WoWMovement.MovementDirection.TurnRight;
                dirCmd = "TurnRight";
            }
            else // if (whichWay == WoWMovement.MovementDirection.None)
            {
                Dlog("(AimRotation) Done, Ending Rotation: {0}", Me.Transport.RotationDegrees);
                return RunStatus.Failure;
            }

            Dlog("(AimRotation) Current Rotation: {0} - {1}", Me.Transport.RotationDegrees, whichWay.ToString().ToUpper());
#if WOWMOVEMENT_TIMED_TURNS_STOPFAILING
            WoWMovement.Move(whichWay, TimeSpan.FromMilliseconds( 10));
            WoWMovement.MoveStop(whichWay);
            // loop until we actually move
            while ( 0.001 > (currRotation - Me.Transport.RotationDegrees ))
               StyxWoW.SleepForLagDuration();
#elif WOWMOVEMENT_TURNS_STOPFAILING
            WoWMovement.Move(whichWay);
            Thread.Sleep(10);
            WoWMovement.MoveStop(whichWay);
            // loop until we actually move
            while ( 0.001 > (currRotation - Me.Transport.RotationDegrees ))
               StyxWoW.SleepForLagDuration();
#else
            // doing LUA calls these because WoWMovement API doesn't stop turning quickly enough
            Lua.DoString( dirCmd + "Start()");
            Thread.Sleep(10);
            Lua.DoString( dirCmd + "Stop()");
#endif
            return RunStatus.Success;
        }

        private RunStatus ChuckBear()
        {
            Dlog("(Chuck-A-Bear) threw bear at trampoline");
            bool canCast = CanCastNow(CHUCK_A_BEAR );
            Lua.DoString("CastSpellByID({0})", CHUCK_A_BEAR);
            WaitForCurrentSpell();
            Thread.Sleep(4000);
            return RunStatus.Success;
        }

        bool IsBearCubInBags()
        {
#if USE_OM
            WoWItem item = ObjectManager.GetObjectsOfType<WoWItem>().Find(unit => unit.Entry == 54439);
#else
            WoWItem item = Me.BagItems.Find(unit => unit.Entry == 54439);
#endif
            return item != null;
        }

        RunStatus LootClosestBear()
        {
            List<WoWUnit> bears =
                   (from o in ObjectManager.ObjectList
                    where o is WoWUnit
                    let unit = o.ToUnit()
                    where
                        unit.Entry == 40240
                        && 15 < unit.WorldLocation.Distance(Me.Transport.WorldLocation)
                    orderby
                        unit.WorldLocation.Distance(Me.Transport.WorldLocation) ascending
                    select unit
                        ).ToList();

            foreach (WoWUnit bear in bears)
            {
                StyxWoW.SleepForLagDuration();

                bear.Target();  // target so we can use LUA func
                bool bChkLua = Lua.GetReturnVal<bool>("return CheckInteractDistance(\"target\", 1)", 0);

                bool bChkInt = bear.WithinInteractRange;
                if (!bChkLua && !bChkInt)
                    continue;

                bear.Interact();
                WaitForCurrentSpell();
                StyxWoW.SleepForLagDuration();

                ObjectManager.Update();
                StyxWoW.SleepForLagDuration();

                if (IsBearCubInBags())
                {
                    Log("(Loot Bear) grabbed a bear to throw");
                    return RunStatus.Success;
                }
            }

            Dlog("(Loot Bear) no bear at level {0}", lvlCurrent );
            return RunStatus.Failure;
        }

        public bool InTree()
        {
            return HasAura(AURA_IN_TREE) || IsClimbingTheTree();
        }

        public bool IsClimbingTheTree()
        {
            return HasAura( AURA_CLIMBING_TREE );
        }

        public bool DoWeHaveQuest()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
            return quest != null;
        }

        public bool IsQuestComplete()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
            return quest == null || quest.IsCompleted ;
        }

        public bool HasAura(int auraId)
        {
            WoWAura aura = (from a in Me.Auras
                            where a.Value.SpellId == auraId 
                            select a.Value).FirstOrDefault();
            return aura != null;
        }

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
            if (quest != null)
            {
                TreeRoot.GoalText = string.Format("Doing quest: {0}", quest.Name);
            }
        }

        private bool _isDone;
        public override bool IsDone
        {
            get 
            {
                return _isDone;
            }
        }
    }
}

