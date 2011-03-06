using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Styx.Helpers;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    public class VehicleBehavior : CustomForcedBehavior
    {
        #region Overrides of CustomForcedBehavior

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"NpcMountID",null},
            {"VehicleID",null},
            {"SpellID",null},
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
            int spellID = 0;
            int fireheight = 0;
            int firetillFinish = 0;
            int questId = 0;
            WoWPoint fireCoords = new WoWPoint(0, 0, 0);
            WoWPoint targetCoords = new WoWPoint(0, 0, 0);
            WoWPoint previousCoords = new WoWPoint(0, 0, 0);

            Logging.Write("Activating Emergency Gnome Bombing Tech!");

            
            success = success && GetAttributeAsInteger("NpcMountID", true, "1", 1, int.MaxValue, out npcmountid);
            success = success && GetAttributeAsInteger("VehicleID", true, "1", 1, int.MaxValue, out vehicleID);
            success = success && GetAttributeAsInteger("SpellID", true, "1", 1, int.MaxValue, out spellID);
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
            SpellCastID = spellID;
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
        public int Counter { get; set; }
        public int SpellType { get; set; }
        public int SpellCastID { get; set; }
        public int VehicleID { get; set; }
        public int NpcMountID { get; set; }
        public int FireHeight { get; set; }
        public int RotationFace { get; set; }
        public int FireTillFinish { get; set; }
        public uint QuestId { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        public List<WoWUnit> npcaroundlist;
        public List<WoWUnit> vehicleList;
        public List<WoWUnit> npcvehicleList;

        static public bool inVehicle { get { return Lua.GetReturnVal<bool>("return  UnitUsingVehicle(\"player\")", 0); } }

        /// <summary>
        /// A Queue for npc's we need to talk to
        /// </summary>
        //private WoWUnit CurrentUnit { get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(unit => unit.Entry == VehicleId); } }

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                    new Decorator(ret => (QuestId != 0 && me.QuestLog.GetQuestById(QuestId) != null &&
                         me.QuestLog.GetQuestById(QuestId).IsCompleted),
                        new Action(ret => _isDone = true)),

                    new Decorator(ret => Counter >= 1,
                        new Action(ret => _isDone = true)),

                        new PrioritySelector(

                            new Decorator(ret => !inVehicle,
                                new Action(delegate
                                {
                                    UpdateNpcList();

                                    if (npcvehicleList.Count >= 1 && npcvehicleList[0].WithinInteractRange)
                                    {
                                        npcaroundlist = ObjectManager.GetObjectsOfType<WoWUnit>()
                                         .Where(ret => (ret.Entry != VehicleID) && !ret.Dead).OrderBy(u => u.Location.Distance(me.Location)).ToList();

                                        npcaroundlist[0].Face();

                                        Styx.Logic.Combat.LegacySpellManager.CastSpellById(6603);

                                        MountedPoint = me.Location;
                                        npcvehicleList[0].Interact();
                                        return RunStatus.Success;
                                    }
                                    else
                                    {
                                        Navigator.MoveTo(npcvehicleList[0].Location);
                                        Thread.Sleep(300);
                                        return RunStatus.Running;
                                    }

                                })
                                ),

                            new Decorator(ret => inVehicle,
                                new Action(delegate
                                {
                                    vehicleList = ObjectManager.GetObjectsOfType<WoWUnit>()
                                         .Where(ret => (ret.Entry == VehicleID) && !ret.Dead).OrderBy(u => u.Location.Distance(MountedPoint)).ToList();

                                    if (vehicleList[0].Location.Distance(FirePoint) > 10)
                                    {

                                        WoWPoint destination1 = new WoWPoint(FirePoint.X, FirePoint.Y, FirePoint.Z);
                                        WoWPoint[] pathtoDest1 = Styx.Logic.Pathing.Navigator.GeneratePath(MountedPoint, destination1);

                                        foreach (WoWPoint p in pathtoDest1)
                                        {
                                            while (!vehicleList[0].Dead && p.Distance(vehicleList[0].Location) > 7)
                                            {
                                                Thread.Sleep(100);
                                                WoWMovement.ClickToMove(p);
                                            }
                                        }

                                        Thread.Sleep(400);
                                    }

                                    WoWMovement.ClickToMove(TargetPoint);
                                    Thread.Sleep(500);
                                    WoWMovement.MoveStop();

                                    Lua.DoString("VehicleAimRequestNormAngle(0." + FireHeight + ")");
                                    Thread.Sleep(400);

                                    // Logging.Write("8 " + (Environment.TickCount - timer));

                                    if (FireTillFinish == 1)
                                    {
                                        while (!me.QuestLog.GetQuestById(QuestId).IsCompleted)
                                        {
                                            Lua.DoString("CastSpellByID(" + SpellCastID + ")");
                                            Thread.Sleep(700);
                                        }

                                        if (me.QuestLog.GetQuestById(QuestId).IsCompleted)
                                        {
                                            Counter++;
                                        }
                                    }
                                    else
                                    {
                                        Lua.DoString("CastSpellByID(" + SpellCastID + ")");
                                        Thread.Sleep(700);
                                        Counter++;
                                    }


                                    return RunStatus.Success;


                                })
                                ),

                            new Action(ret => Logging.Write(""))
                        )
                    ));
        }

        public void UpdateNpcList()
        {
            ObjectManager.Update();

            vehicleList = ObjectManager.GetObjectsOfType<WoWUnit>()
              .Where(ret => (ret.Entry == VehicleID) && !ret.Dead).OrderBy(u => u.Distance).ToList();

            npcvehicleList = ObjectManager.GetObjectsOfType<WoWUnit>()
              .Where(ret => (ret.Entry == NpcMountID) && !ret.Dead).OrderBy(u => u.Distance).ToList();
        }

        private bool _isDone;
        public override bool IsDone
        {
            get { return _isDone; }
        }

        #endregion
    }
}
