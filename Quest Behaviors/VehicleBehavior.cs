using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Styx.Database;
using Styx.Logic.Combat;
using Styx.Helpers;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Pathing;
using Styx.Logic.Profiles.Quest.Order;
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

        public VehicleBehavior(Dictionary<string, string> args)
            : base(args)
        {
                Logging.Write("Activating Emergency Gnome Bombing Tech!");

                uint npcmountid;
                if (!uint.TryParse(Args["NpcMountID"], out npcmountid))
                    Logging.Write("Parsing NpcMountID in VehicleBehavior behavior failed! please check your profile!");

                uint vehicleID;
                if (!uint.TryParse(Args["VehicleID"], out vehicleID))
                    Logging.Write("Parsing VehicleID in VehicleBehavior behavior failed! please check your profile!");

                uint spellID;
                if (!uint.TryParse(Args["SpellID"], out spellID))
                    Logging.Write("Parsing SpellID in VehicleBehavior behavior failed! please check your profile!");

                uint fireheight;
                if (!uint.TryParse(Args["FireHeight"], out fireheight))
                    Logging.Write("Parsing FireHeight in VehicleBehavior behavior failed! please check your profile!");

                uint questId;
                if (!uint.TryParse(Args["QuestId"], out questId))
                    Logging.Write("Parsing questid in VehicleBehavior behavior failed! please check your profile!");

                float fireXcoord;
                if (!float.TryParse(Args["FireLocationX"], out fireXcoord))
                    Logging.Write("Parsing FireLocationX in VehicleBehavior behavior failed! please check your profile!");

                float fireYcoord;
                if (!float.TryParse(Args["FireLocationY"], out fireYcoord))
                    Logging.Write("Parsing FireLocationY in VehicleBehavior behavior failed! please check your profile!");

                float fireZcoord;
                if (!float.TryParse(Args["FireLocationZ"], out fireZcoord))
                    Logging.Write("Parsing FireLocationZ in VehicleBehavior behavior failed! please check your profile!");

                float targetXcoord;
                if (!float.TryParse(Args["TargetLocationX"], out targetXcoord))
                    Logging.Write("Parsing TargetLocationX in VehicleBehavior behavior failed! please check your profile!");

                float targetYcoord;
                if (!float.TryParse(Args["TargetLocationY"], out targetYcoord))
                    Logging.Write("Parsing TargetLocationY in VehicleBehavior behavior failed! please check your profile!");

                float targetZcoord;
                if (!float.TryParse(Args["TargetLocationZ"], out targetZcoord))
                    Logging.Write("Parsing TargetLocationZ in VehicleBehavior behavior failed! please check your profile!");

                float previousXcoord;
                if (!float.TryParse(Args["PreviousFireLocationX"], out previousXcoord))
                    Logging.Write("Parsing PreviousFireLocationX in VehicleBehavior behavior failed! please check your profile!");

                float previousYcoord;
                if (!float.TryParse(Args["PreviousFireLocationY"], out previousYcoord))
                    Logging.Write("Parsing questid in VehicleBehavior behavior failed! please check your profile!");

                float previousZcoord;
                if (!float.TryParse(Args["PreviousFireLocationZ"], out previousZcoord))
					Logging.Write("Parsing questid in VehicleBehavior behavior failed! please check your profile!");

                QuestId = questId;
                NpcMountID = npcmountid;
                SpellType = 2;
                FireHeight = fireheight;
                SpellCastID = spellID;
                VehicleID = vehicleID;
                FirePoint = new WoWPoint(fireXcoord, fireYcoord, fireZcoord);
                TargetPoint = new WoWPoint(targetXcoord, targetYcoord, targetZcoord);
                MountedPoint = new WoWPoint(previousXcoord, previousYcoord, previousZcoord);


            Counter = 0;
            SpellType = 0;


        }

        public WoWPoint FirePoint { get; private set; }
        public WoWPoint MountedPoint { get; private set; }
        public WoWPoint TargetPoint { get; private set; }
        public int Counter { get; set; }
        public int SpellType { get; set; }
        public uint SpellCastID { get; set; }
        public uint VehicleID { get; set; }
        public uint NpcMountID { get; set; }
        public uint FireHeight { get; set; }
        public uint RotationFace { get; set; }
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

                                    Lua.DoString("CastSpellByID(" + SpellCastID + ")");
                                    // TimesUsedCounter++;
                                    //Thread.Sleep(OftenToUse);
                                    Thread.Sleep(700);
                                    Counter++;


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
