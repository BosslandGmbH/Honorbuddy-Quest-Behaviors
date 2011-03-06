using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Styx.Database;
using Styx.Logic.Combat;
using Styx.Helpers;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Pathing;
using Styx.Logic.Profiles.Quest;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Styx.Logic.BehaviorTree;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    public class VehicleBehavior : CustomForcedBehavior
    {
        /// <summary>
        /// VehicleBehavior by Natfoth
        /// Will control a vehicle and fire on locations/Mobs
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// NpcMountID: MobId of the vehicle before it is mounted.
        /// VehicleID: Mob of the actual Vehicle, sometimes it will be the some but sometimes it will not be.
        /// SpellIndex: Button bar Number starting from 1
        /// FireHeight: Between 0 - 99 The lower the number the closer to the ground it will be
        /// FireTillFinish: This is used for a few quests that the mob is flying but respawns fast, So the bot can fire in the same spot over and over.
        /// FireLocation Coords: Where you want to be at when you fire.
        /// TargetLocation Coords: Where you want to aim.
        /// PreviousFireLocation Coords: This should only be used if you are already inside of the vehicle when you call the behaviors again, and
        ///                                 should be the same coords as FireLocation on the call before it, Check the Wiki for more info or examples.
        /// </summary>
        /// 

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"NpcMountID",null},
            {"VehicleID",null},
            {"SpellIndex",null},
            {"FireHeight",null},
            {"FireTillFinish",null},
            {"QuestId",null},
            {"FireLocationX",null},
            {"FireLocationY",null},
            {"FireLocationZ",null},
            {"TargetLocationX",null},
            {"TargetLocationY",null},
            {"TargetLocationZ",null},
            {"PreviousFireLocationX",null},
            {"PreviousFireLocationY",null},
            {"PreviousFireLocationZ",null},

        };

        bool success = true;

        public VehicleBehavior(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);

            int npcmountid = 0;
            int vehicleID = 0;
            int spellIndex = 0;
            int fireheight = 0;
            int firetillFinish = 0;
            int questId = 0;
            WoWPoint fireCoords = new WoWPoint(0, 0, 0);
            WoWPoint targetCoords = new WoWPoint(0, 0, 0);
            WoWPoint previousCoords = new WoWPoint(0, 0, 0);

            TreeRoot.GoalText = "VehicleBehavior: Running";
            
            success = success && GetAttributeAsInteger("NpcMountID", true, "1", 1, int.MaxValue, out npcmountid);
            success = success && GetAttributeAsInteger("VehicleID", true, "1", 1, int.MaxValue, out vehicleID);
            success = success && GetAttributeAsInteger("SpellIndex", true, "1", 1, int.MaxValue, out spellIndex);
            success = success && GetAttributeAsInteger("FireHeight", true, "1", 1, int.MaxValue, out fireheight);
            success = success && GetAttributeAsInteger("FireTillFinish", false, "0", 0, 1, out firetillFinish);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
            success = success && GetXYZAttributeAsWoWPoint("FireLocationX", "FireLocationY", "FireLocationZ", true, new WoWPoint(0, 0, 0), out fireCoords);
            success = success && GetXYZAttributeAsWoWPoint("TargetLocationX", "TargetLocationY", "TargetLocationZ", true, new WoWPoint(0, 0, 0), out targetCoords);
            success = success && GetXYZAttributeAsWoWPoint("PreviousFireLocationX", "PreviousFireLocationY", "PreviousFireLocationZ", true, new WoWPoint(0, 0, 0), out previousCoords);

            QuestId = (uint)questId;
            NpcMountID = npcmountid;
            SpellType = 2;
            FireHeight = fireheight;
            SpellIndex = spellIndex;
            VehicleID = vehicleID;
            FirePoint = fireCoords;
            TargetPoint = targetCoords;
            MountedPoint = previousCoords;
            FireTillFinish = firetillFinish;


            Counter = 0;
            SpellType = 0;


        }

        public WoWPoint FirePoint { get; private set; }
        public WoWPoint MountedPoint { get; private set; }
        public WoWPoint TargetPoint { get; private set; }
        public WoWPoint LatestLocation { get; private set; }
        public int Counter { get; set; }
        public bool gotVehicle = false;
        public int SpellType { get; set; }
        public int SpellIndex { get; set; }
        public int VehicleID { get; set; }
        public int NpcMountID { get; set; }
        public int FireHeight { get; set; }
        public int RotationFace { get; set; }
        public int FireTillFinish { get; set; }
        public uint QuestId { get; set; }

        public WoWUnit Vehicle;

        public WoWPoint[] Path { get; private set; }
        int pathIndex = 0;

        public static LocalPlayer me = ObjectManager.Me;

        public List<WoWUnit> vehicleList
        {
            get
            {
                if (MountedPoint.X != 0)
                {
                    gotVehicle = true;
                    return ObjectManager.GetObjectsOfType<WoWUnit>()
                                              .Where(ret => (ret.Entry == VehicleID) && !ret.Dead).OrderBy(u => u.Location.Distance(MountedPoint)).ToList();
                }
                else
                {
                    gotVehicle = true;
                    return ObjectManager.GetObjectsOfType<WoWUnit>()
                                        .Where(ret => (ret.Entry == VehicleID) && !ret.Dead).OrderBy(u => u.Distance).ToList();
                }
            }
        }

        public List<WoWUnit> npcvehicleList
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>()
                                    .Where(ret => (ret.Entry == NpcMountID) && !ret.Dead).OrderBy(u => u.Distance).ToList();
            }
        }

        static public bool inVehicle { get { return Lua.GetReturnVal<bool>("return  UnitUsingVehicle(\"player\")", 0); } }

        #region Overrides of CustomForcedBehavior

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

            if (quest != null)
            {
                TreeRoot.GoalText = "VehicleBehavior - " + quest.Name;
            }
            else
            {
                TreeRoot.GoalText = "VehicleBehavior: Running";
            }
        }

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                           new Decorator(ret => (Counter > 0 && FireTillFinish == 0) || (me.QuestLog.GetQuestById(QuestId) != null && me.QuestLog.GetQuestById(QuestId).IsCompleted),
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new WaitContinue(120,
                                        new Action(delegate
                                        {
                                            _isDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )),

                           new Decorator(c => !inVehicle,
                            new Action(c =>
                            {
                                if (!npcvehicleList[0].WithinInteractRange)
                                {
                                    Navigator.MoveTo(npcvehicleList[0].Location);
                                    TreeRoot.StatusText = "Moving To Vehicle - " + npcvehicleList[0].Name + " Yards Away: " + npcvehicleList[0].Location.Distance(me.Location);
                                }
                                else
                                {
                                    npcvehicleList[0].Interact();
                                    MountedPoint = me.Location;
                                    
                                }

                            })
                        ),
                        new Decorator(c => inVehicle,
                            new Action(c =>
                            {
                                if (Vehicle == null)
                                {
                                    Vehicle = vehicleList[0];
                                }

                                if (Vehicle.Location.Distance(FirePoint) <= 5)
                                {
                                    TreeRoot.StatusText = "Firing Vehicle - " + Vehicle.Name + " Using Spell Index: " + SpellIndex + " Height: " + FireHeight;
                                    WoWMovement.ClickToMove(TargetPoint);
                                    Thread.Sleep(500);
                                    WoWMovement.MoveStop();

                                    using (new FrameLock())
                                    {
                                        Lua.DoString("VehicleAimRequestNormAngle(0.{0})", FireHeight);
                                        Lua.DoString("CastPetAction({0})", SpellIndex);
                                        Counter++;
                                        return RunStatus.Success;
                                    }
                                }
                                else if (Vehicle.Location.Distance(FirePoint) > 5)
                                {
                                    TreeRoot.StatusText = "Moving To FireLocation - Yards Away: " + FirePoint.Distance(Vehicle.Location);
                                    WoWMovement.ClickToMove(moveToLocation);
                                    Vehicle.Target();
                                }
                                return RunStatus.Running;
                            }))
                                   
                    ));
        }

        WoWPoint moveToLocation
        {
            get
            {

                Path = Navigator.GeneratePath(Vehicle.Location, FirePoint);
                pathIndex = 0;

                while (Path[pathIndex].Distance(Vehicle.Location) <= 3 && pathIndex < Path.Length - 1)
                    pathIndex++;
                return Path[pathIndex];

            }
        }

            

        private bool _isDone;
        public override bool IsDone
        {
            get { return _isDone; }
        }

        #endregion
    }
}
