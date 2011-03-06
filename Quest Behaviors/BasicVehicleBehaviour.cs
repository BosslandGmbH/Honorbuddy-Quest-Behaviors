using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Styx.Database;
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
    public class BasicVehicleBehaviour : CustomForcedBehavior
    {
        #region Overrides of CustomForcedBehavior

        public BasicVehicleBehaviour(Dictionary<string, string> args)
            : base(args)
        {
            uint destXcoord;
            if (!uint.TryParse(Args["DestX"], out destXcoord))
                Logging.Write("Parsing DestX in BasicVehicleBehaviour behavior failed! please check your profile!");

            uint destYcoord;
            if (!uint.TryParse(Args["DestY"], out destYcoord))
                Logging.Write("Parsing DestY in BasicVehicleBehaviour behavior failed! please check your profile!");

            uint destZcoord;
            if (!uint.TryParse(Args["DestZ"], out destZcoord))
                Logging.Write("Parsing DestZ in BasicVehicleBehaviour behavior failed! please check your profile!");

            uint npcID;
            if (!uint.TryParse(Args["NpcId"], out npcID))
                Logging.Write("Parsing NpcId in BasicVehicleBehaviour behavior failed! please check your profile!");

            uint vehicleID;
            if (!uint.TryParse(Args["VehicleId"], out vehicleID))
                Logging.Write("Parsing VehicleId in BasicVehicleBehaviour behavior failed! please check your profile!");

            uint spellID;
            if (!uint.TryParse(Args["SpellId"], out spellID))
                Logging.Write("Parsing SpellId in BasicVehicleBehaviour behavior failed! please check your profile!");

            float mountXcoord;
            if (!float.TryParse(Args["MountX"], out mountXcoord))
                Logging.Write("Parsing MountX in BasicVehicleBehaviour behavior failed! please check your profile!");

            float mountYcoord;
            if (!float.TryParse(Args["MountY"], out mountYcoord))
                Logging.Write("Parsing MountY in BasicVehicleBehaviour behavior failed! please check your profile!");

            float mountZcoord;
            if (!float.TryParse(Args["MountZ"], out mountZcoord))
                Logging.Write("Parsing MountZ in BasicVehicleBehaviour behavior failed! please check your profile!");

            uint questId;
            if (!uint.TryParse(Args["QuestId"], out questId))
                Logging.Write("Parsing QuestId in BasicVehicleBehaviour behavior failed! please check your profile!");


            VehicleId = vehicleID;
            IsMounted = false;
            QuestId = questId;
            NPCID = npcID;
            Counter = 0;
            SpellCastID = spellID;
            LocationDest = new WoWPoint(destXcoord, destYcoord, destZcoord);
            LocationMount = new WoWPoint(mountXcoord, mountYcoord, mountZcoord);
            MountedPoint = new WoWPoint(0, 0, 0);
        }

        public WoWPoint LocationDest { get; private set; }
        public WoWPoint LocationMount { get; private set; }
        public WoWPoint MountedPoint { get; private set; }
        public int Counter { get; set; }
        public bool IsMounted { get; set; }
        public uint VehicleId { get; set; }
        public uint NPCID { get; set; }
        public uint SpellCastID { get; set; }
        public uint QuestId { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        public List<WoWUnit> vehicleList;

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

                            new Decorator(ret => IsMounted != true && vehicleList == null,
                                new Action(delegate
                                {
                                    WoWPoint destination1 = new WoWPoint(LocationMount.X, LocationMount.Y, LocationMount.Z);
                                    WoWPoint[] pathtoDest1 = Styx.Logic.Pathing.Navigator.GeneratePath(me.Location, destination1);

                                    foreach (WoWPoint p1 in pathtoDest1)
                                    {
                                        while (!me.Dead && p1.Distance(me.Location) > 3)
                                        {
                                            Thread.Sleep(100);
                                            WoWMovement.ClickToMove(p1);
                                        }
                                    }

                                    ObjectManager.Update();
                                    vehicleList = ObjectManager.GetObjectsOfType<WoWUnit>()
                                      .Where(ret => (ret.Entry == VehicleId) && !ret.Dead).OrderBy(ret => ret.Location.Distance(me.Location)).ToList();

                                })
                                ),

                            new Decorator(ret => vehicleList[0] != null && !vehicleList[0].WithinInteractRange && IsMounted != true,
                                new Action(ret => Navigator.MoveTo(vehicleList[0].Location))
                                ),

                            new Decorator(ret => StyxWoW.Me.IsMoving,
                                new Action(delegate
                                {
                                    WoWMovement.MoveStop();
                                    StyxWoW.SleepForLagDuration();
                                })
                                ),

                            new Decorator(ret => IsMounted != true,
                                new Action(delegate
                                {

                                    MountedPoint = me.Location;
                                    vehicleList[0].Interact();
                                    StyxWoW.SleepForLagDuration();
                                    IsMounted = true;

                                    ObjectManager.Update();
                                    vehicleList = ObjectManager.GetObjectsOfType<WoWUnit>()
                                      .Where(ret => (ret.Entry == VehicleId) && !ret.Dead).OrderBy(ret => ret.Location.Distance(MountedPoint)).ToList();
                                    Thread.Sleep(3000);
                                })
                                ),

                            new Decorator(ret => IsMounted = true,
                                new Action(delegate
                                {
                                    WoWPoint destination = new WoWPoint(LocationDest.X, LocationDest.Y, LocationDest.Z);
                                    WoWPoint[] pathtoDest = Styx.Logic.Pathing.Navigator.GeneratePath(vehicleList[0].Location, destination);

                                    foreach (WoWPoint p in pathtoDest)
                                    {
                                        while (!vehicleList[0].Dead && p.Distance(vehicleList[0].Location) > 3)
                                        {
                                            Thread.Sleep(100);
                                            WoWMovement.ClickToMove(p);
                                        }

                                    }

                                    Lua.DoString("CastSpellByID(" + SpellCastID + ")");

                                    Counter++;

                                })
                                ),

                            new Action(ret => Logging.Write(""))
                        )
                    ));
        }

        private bool _isDone;
        public override bool IsDone
        {
            get { return _isDone; }
        }

        #endregion
    }
}
