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
    public class BasicVehicleBehaviour : CustomForcedBehavior
    {
        #region Overrides of CustomForcedBehavior

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"DestX",null},
            {"DestY",null},
            {"DestZ",null},
            {"NpcId",null},
            {"VehicleId",null},
            {"SpellId",null},
            {"MountX",null},
            {"MountY",null},
            {"MountZ",null},
            {"QuestId",null},

        };

        bool success = true;

        public BasicVehicleBehaviour(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);

            WoWPoint destcoords = new WoWPoint(0, 0, 0);
            WoWPoint mountcoords = new WoWPoint(0, 0, 0);
            int npcID = 0;
            int vehicleID = 0;
            int spellID = 0;
            int questId = 0;

            success = success && GetXYZAttributeAsWoWPoint("DestX", "DestY", "DestZ", true, new WoWPoint(0, 0, 0), out destcoords);
            success = success && GetAttributeAsInteger("NpcId", true, "1", 0, int.MaxValue, out npcID);
            success = success && GetAttributeAsInteger("VehicleId", true, "1", 0, int.MaxValue, out vehicleID);
            success = success && GetAttributeAsInteger("SpellId", false, "1", 0, int.MaxValue, out spellID);
            success = success && GetXYZAttributeAsWoWPoint("MountX", "MountY", "MountZ", true, new WoWPoint(0, 0, 0), out mountcoords);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);


            VehicleId = vehicleID;
            IsMounted = false;
            QuestId = (uint)questId;
            NPCID = npcID;
            Counter = 0;
            SpellCastID = spellID;
            LocationDest = destcoords;
            LocationMount = mountcoords;
            MountedPoint = new WoWPoint(0, 0, 0);
        }

        public WoWPoint LocationDest { get; private set; }
        public WoWPoint LocationMount { get; private set; }
        public WoWPoint MountedPoint { get; private set; }
        public int Counter { get; set; }
        public bool IsMounted { get; set; }
        public int VehicleId { get; set; }
        public int NPCID { get; set; }
        public int SpellCastID { get; set; }
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
