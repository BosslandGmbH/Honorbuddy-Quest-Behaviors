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
    public class MountVehOnly : CustomForcedBehavior
    {
        #region Overrides of CustomForcedBehavior

        public MountVehOnly(Dictionary<string, string> args)
            : base(args)
        {

            int npcmountid;
            if (!GetAttributeAsInteger("NpcMountId", true, "1", 0, int.MaxValue, out npcmountid))
                Logging.Write("Parsing mobid in MountVehOnly behavior failed! please check your profile!");

            WoWPoint location;
            if (!GetXYZAttributeAsWoWPoint("X", "Y", "Z", true, new WoWPoint(0, 0, 0), out location))
                Logging.Write("Parsing location in MountVehOnly behavior failed! please check your profile!");

            NpcMountID = npcmountid;
            Location = location;

            Counter = 0;
        }

        public WoWPoint Location { get; private set; }
        public int Counter { get; set; }
        public int NpcMountID { get; set; }

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

                    new Decorator(ret => Counter >= 1,
                        new Action(ret => _isDone = true)),

                        new PrioritySelector(

                            new Decorator(ret => !inVehicle,
                                new Action(delegate
                                {
                                    UpdateNpcList();

                                    if (npcvehicleList.Count >= 1 && Location.Distance(me.Location) < 3)
                                    {
                                        npcvehicleList[0].Interact();
                                        Logging.Write("Mounting Vehicle");
                                        return RunStatus.Success;
                                    }
                                    else
                                    {
                                        Navigator.MoveTo(Location);
                                        Thread.Sleep(300);
                                        return RunStatus.Running;
                                    }

                                })
                                ),

                            new Decorator(ret => inVehicle,
                                new Action(delegate
                                {

                                    Logging.Write("Mounted Finished");
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
