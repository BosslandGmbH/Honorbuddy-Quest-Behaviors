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
    public class MountVehOnly : CustomForcedBehavior
    {
        #region Overrides of CustomForcedBehavior

        public MountVehOnly(Dictionary<string, string> args)
            : base(args)
        {
            Logging.Write("Mounting Vehicle!");

            uint npcmountid;
            if (!uint.TryParse(Args["NpcMountId"], out npcmountid))
                Logging.Write("Parsing mobid in MountVehOnly behavior failed! please check your profile!");

            float xcoord;
            if (!float.TryParse(Args["X"], out xcoord))
                Logging.Write("Parsing X in MountVehOnly behavior failed! please check your profile!");

            float ycoord;
            if (!float.TryParse(Args["Y"], out ycoord))
                Logging.Write("Parsing Y in MountVehOnly behavior failed! please check your profile!");

            float zcoord;
            if (!float.TryParse(Args["Z"], out zcoord))
                Logging.Write("Parsing Z in MountVehOnly behavior failed! please check your profile!");

            NpcMountID = npcmountid;
            FirePoint = new WoWPoint(xcoord, ycoord, zcoord);

            Counter = 0;
        }

        public WoWPoint FirePoint { get; private set; }
        public int Counter { get; set; }
        public uint NpcMountID { get; set; }

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

                                    if (npcvehicleList.Count >= 1 && npcvehicleList[0].WithinInteractRange)
                                    {
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
