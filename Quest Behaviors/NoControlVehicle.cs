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
    public class NoControlVehicle : CustomForcedBehavior
    {
        #region Overrides of CustomForcedBehavior

        public NoControlVehicle(Dictionary<string, string> args)
            : base(args)
        {
                uint npcmountid;
                if (!uint.TryParse(Args["NpcMountId"], out npcmountid))
                    Logging.Write("Parsing NpcMountId in NoControlVehicle behavior failed! please check your profile!");

                uint targetID;
                if (!uint.TryParse(Args["TargetId"], out targetID))
                    Logging.Write("Parsing TargetId in NoControlVehicle behavior failed! please check your profile!");

                uint spellID;
                if (!uint.TryParse(Args["SpellId"], out spellID))
                    Logging.Write("Parsing SpellId in NoControlVehicle behavior failed! please check your profile!");

                uint vehicleID;
                if (!uint.TryParse(Args["VehicleId"], out vehicleID))
                    Logging.Write("Parsing VehicleId in NoControlVehicle behavior failed! please check your profile!");

                int oftentouse;
                if (!int.TryParse(Args["OftenToUse"], out oftentouse))
                    Logging.Write("Parsing OftenToUse in NoControlVehicle behavior failed! please check your profile!");

                uint timestouse;
                if (!uint.TryParse(Args["TimesToUse"], out timestouse))
                    Logging.Write("Parsing TimesToUse in NoControlVehicle behavior failed! please check your profile!");

                uint maxdist;
                if (!uint.TryParse(Args["MaxDist"], out maxdist))
                    Logging.Write("Parsing MaxDist in NoControlVehicle behavior failed! please check your profile!");

                uint TypeID;
                if (!uint.TryParse(Args["TypeId"], out TypeID))
                    Logging.Write("Parsing TypeId in NoControlVehicle behavior failed! please check your profile!");
					
				uint questId;
				if (!uint.TryParse(Args["QuestId"], out questId))
					Logging.Write("Parsing questid in NoControlVehicle behavior failed! please check your profile!");

				QuestId = questId;	
                NpcMountID = npcmountid;
                SpellType = TypeID;
                TargetID = targetID;
                SpellCastID = spellID;
                VehicleID = vehicleID;
                TimesToUse = timestouse;
                OftenToUse = oftentouse;
                MaxDist = maxdist;


            Counter = 0;
            TimesUsedCounter = 0;
            moveAim = false;
            MountedPoint = new WoWPoint(0, 0, 0);

        }

        public WoWPoint LocationDest { get; private set; }
        public WoWPoint MountedPoint { get; private set; }
        public WoWPoint LocationMount { get; private set; }
        public int Counter { get; set; }
        public bool moveAim { get; set; }
        public int TimesUsedCounter { get; set; }
        public uint SpellType { get; set; }
        public uint TargetID { get; set; }
        public uint SpellCastID { get; set; }
        public uint VehicleID { get; set; }
        public uint NpcMountID { get; set; }
        public int OftenToUse { get; set; }
        public uint TimesToUse { get; set; }
        public uint MaxDist { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        public List<WoWUnit> npcList;
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

                                    if (SpellType == 1)
                                    {

                                        UpdateNpcList();

                                        if (npcList.Count == 1 && npcList[0].Location.Distance(vehicleList[0].Location) < 15)
                                        {
                                            UpdateNpcList();

                                            npcList[0].Target();
                                            Lua.DoString("CastSpellByID(" + SpellCastID + ")");
                                            Counter++;
                                            Thread.Sleep(1000);
                                            return RunStatus.Success;
                                        }

                                        return RunStatus.Running;

                                    }
                                    else if (SpellType == 2)
                                    {
                                        Logging.Write("Activating Emergency Gnome Bombing Tech!");
                                        UpdateNpcList();
                                        if (vehicleList.Count >= 1)
                                        {
                                            npcList = ObjectManager.GetObjectsOfType<WoWUnit>()
                                                .Where(u => u.Entry == TargetID).OrderBy(u => u.Location.Distance(vehicleList[0].Location)).ToList();
                                        }

                                        if (npcList.Count >= 1 && npcList[0].Location.Distance(vehicleList[0].Location) < MaxDist)
                                        {

                                            Logging.Write("Casting Spell Number " + TimesUsedCounter);

                                            Lua.DoString("CastSpellByID(" + SpellCastID + ")");
                                            LegacySpellManager.ClickRemoteLocation(npcList[0].Location);

                                            TimesUsedCounter++;
                                            Thread.Sleep(OftenToUse);

                                        }

                                        if (TimesUsedCounter > TimesToUse)
                                        {
                                            Counter++;
                                            return RunStatus.Success;
                                        }

                                        return RunStatus.Running;
                                    }
                                    else if (SpellType == 3)
                                    {
                                        Logging.Write("Pulling out the Anti-Gnome Shooter!");
                                        UpdateNpcList();

                                        if (vehicleList.Count >= 1)
                                        {
                                            npcList = ObjectManager.GetObjectsOfType<WoWUnit>()
                                                .Where(u => u.Entry == TargetID).OrderBy(u => u.Location.Distance(MountedPoint)).ToList();
                                        }

                                        if (npcList.Count >= 1 && npcList[0].Location.Distance(MountedPoint) < MaxDist)
                                        {

                                            Logging.Write("Casting Spell Number " + TimesUsedCounter);

                                            npcList[0].Target();
                                            WoWMovement.ClickToMove(npcList[0].Location);
                                            Thread.Sleep(500);


                                            if (npcList[0].Location.Distance(MountedPoint) < 10)
                                            {
                                                Lua.DoString("VehicleAimRequestNormAngle(0)");
                                            }
                                            else if (npcList[0].Location.Distance(MountedPoint) >= 10 && npcList[0].Location.Distance(MountedPoint) < 20)
                                            {
                                                Lua.DoString("VehicleAimRequestNormAngle(0.2)");
                                            }
                                            else if (npcList[0].Location.Distance(MountedPoint) >= 20 && npcList[0].Location.Distance(MountedPoint) < 30)
                                            {
                                                Lua.DoString("VehicleAimRequestNormAngle(0.4)");
                                            }
                                            else if (npcList[0].Location.Distance(MountedPoint) >= 40 && npcList[0].Location.Distance(MountedPoint) < 60)
                                            {
                                                Lua.DoString("VehicleAimRequestNormAngle(0.5)");
                                            }

                                            // Logging.Write("8 " + (Environment.TickCount - timer));

                                            Lua.DoString("CastSpellByID(" + SpellCastID + ")");
                                            TimesUsedCounter++;
                                            Thread.Sleep(OftenToUse);

                                        }

                                        if (TimesUsedCounter > TimesToUse)
                                        {
                                            Lua.DoString("VehicleExit()");
                                            Counter++;
                                            return RunStatus.Success;
                                        }

                                        return RunStatus.Running;
                                    }

                                    return RunStatus.Running;
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
              .Where(ret => (ret.Entry == VehicleID) && !ret.Dead).ToList();

            npcList = ObjectManager.GetObjectsOfType<WoWUnit>()
               .Where(u => u.Entry == TargetID).OrderBy(u => u.Distance).ToList();

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
