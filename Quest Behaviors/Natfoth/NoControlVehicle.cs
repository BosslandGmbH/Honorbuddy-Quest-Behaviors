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
    public class NoControlVehicle : CustomForcedBehavior
    {

        /// <summary>
        /// NoControlVehicle by Natfoth
        /// For Vehicles you do not have to move, such as Cannons, Horses, Bombings, and even ground targeting cannons.
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// NpcMountID: MobId of the vehicle before it is mounted.
        /// VehicleId: Between 0 - 99 The lower the number the closer to the ground it will be
        /// TargetId, TargetId2, TargetId3: Mob of the actual Vehicle, sometimes it will be the some but sometimes it will not be.
        /// SpellIndex: Button bar Number starting from 1 
        /// OftenToUse: This is used for a few quests that the mob is flying but respawns fast, So the bot can fire in the same spot over and over.
        /// NumOfTimes: Where you want to be at when you fire.
        /// TypeId: Where you want to aim.
        /// PreviousFireLocation Coords: This should only be used if you are already inside of the vehicle when you call the behaviors again, and
        ///                                 should be the same coords as FireLocation on the call before it, Check the Wiki for more info or examples.
        /// </summary>

        

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"NpcMountId",null},
            {"TargetId",null},
            {"TargetId2",null},
            {"TargetId3",null},
            {"MobId",null},
            {"SpellIndex",null},
            {"VehicleId",null},
            {"OftenToUse",null},
            {"NumOfTimes",null},
            {"TimesToUse",null},
            {"MaxRange",null},
            {"HomeIndex",null},
            {"TypeId",null},
            {"QuestId",null},

        };

        bool success = true;

        public NoControlVehicle(Dictionary<string, string> args)
            : base(args)
        {

            CheckForUnrecognizedAttributes(recognizedAttributes);

            int npcmountid = 0;
            int targetID = 0;
            int targetID2 = 0;
            int targetID3 = 0;
            int spellIndex = 0;
            int homeIndex = 0;
            int vehicleID = 0;
            int oftentouse = 0;
            int timestouse = 0;
            int TypeID = 0;
            int questId = 0;
            int maxrange = 0;
            
            success = success && GetAttributeAsInteger("NpcMountId", false, "1", 0, int.MaxValue, out npcmountid);
            success = success && GetAttributeAsInteger("TargetId", false, "0", 0, int.MaxValue, out targetID);
            success = success && GetAttributeAsInteger("TargetId2", false, "0", 0, int.MaxValue, out targetID2);
            success = success && GetAttributeAsInteger("TargetId3", false, "0", 0, int.MaxValue, out targetID3);
            success = success && GetAttributeAsInteger("SpellIndex", false, "1", 0, int.MaxValue, out spellIndex);
            success = success && GetAttributeAsInteger("VehicleId", false, "0", 0, int.MaxValue, out vehicleID);
            success = success && GetAttributeAsInteger("OftenToUse", false, "1000", 0, int.MaxValue, out oftentouse);
            success = success && GetAttributeAsInteger("TimesToUse", false, "1", 0, int.MaxValue, out timestouse);
            success = success && GetAttributeAsInteger("TypeId", false, "2", 0, 4, out TypeID);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);

            
            success = success && GetAttributeAsInteger("HomeIndex", false, "1", 0, int.MaxValue, out homeIndex);
            success = success && GetAttributeAsInteger("MaxRange", false, "1", 0, int.MaxValue, out maxrange);

            if (targetID == 0)
            {
                success = success && GetAttributeAsInteger("MobId", false, "1", 0, int.MaxValue, out targetID);
            }
            if (spellIndex == 0)
            {
                success = success && GetAttributeAsInteger("AttackIndex", false, "1", 0, int.MaxValue, out spellIndex);
            }
            if (timestouse == 0)
            {
                success = success && GetAttributeAsInteger("NumOfTimes", false, "1", 0, int.MaxValue, out timestouse);
            }

            QuestId = (uint)questId;
            NpcMountID = npcmountid;
            SpellType = TypeID;
            MaxRange = maxrange;
            TargetID = targetID;
            TargetID2 = targetID2;
            TargetID3 = targetID3;
            SpellIndex = spellIndex;
            HomeIndex = homeIndex;
            VehicleID = vehicleID;
            TimesToUse = timestouse;
            OftenToUse = oftentouse;

            MountedPoint = new WoWPoint(0, 0, 0);

        }

        public WoWPoint LocationDest { get; private set; }
        public WoWPoint MountedPoint { get; private set; }
        public WoWPoint LocationMount { get; private set; }
        public int Counter = 1;
        public int MaxRange = 1;
        public int SpellType { get; set; }
        public int TargetID { get; set; }
        public int TargetID2 { get; set; }
        public int TargetID3 { get; set; }
        public int SpellIndex { get; set; }
        public int HomeIndex { get; set; }
        public int VehicleID { get; set; }
        public int NpcMountID { get; set; }
        public int OftenToUse { get; set; }
        public int TimesToUse { get; set; }
        public uint QuestId { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        public List<WoWUnit> npcList
        {
            get
            {
                if (vehicleList.Count > 0)
                {
                    return ObjectManager.GetObjectsOfType<WoWUnit>()
                                                    .Where(u => (u.Entry == TargetID || u.Entry == TargetID2 || u.Entry == TargetID3) && vehicleList[0].Location.Distance(u.Location) <= MaxRange).OrderBy(u => u.Distance).ToList();
                }
                return ObjectManager.GetObjectsOfType<WoWUnit>()
                                .Where(u => (u.Entry == TargetID || u.Entry == TargetID2 || u.Entry == TargetID3) && !u.Dead).OrderBy(u => u.Distance).ToList();
            }
        }

        public List<WoWUnit> vehicleList
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>()
                                    .Where(ret => (ret.Entry == VehicleID) && !ret.Dead).ToList();
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

        static public bool InVehicle { get { return Lua.GetReturnVal<int>("if IsPossessBarVisible() or UnitInVehicle('player') then return 1 else return 0 end", 0) == 1; } }



        #region Overrides of CustomForcedBehavior

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

            if (quest != null)
            {
                TreeRoot.GoalText = "NoControlVehicle - " + quest.Name;
            }
            else
            {
                TreeRoot.GoalText = "NoControlVehicle: Running";
            }
        }

        private Composite root;
        protected override Composite CreateBehavior()
        {
            return root ??
                (root = new PrioritySelector(
                    new Decorator(c => Counter > TimesToUse,
                        new Action(c =>
                        {
                            TreeRoot.StatusText = "Finished!";
                            if (HomeIndex > 0)
                            {
                                Lua.DoString("CastPetAction({0})", HomeIndex);
                            }
                            _isDone = true;
                            return RunStatus.Success;
                        })
                    ),

                    new Decorator(c => npcvehicleList.Count > 0 && !InVehicle,
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
                    new Decorator(c => InVehicle && SpellType == 1,
                        new Action(c =>
                        {
                            if (npcList.Count == 0 || npcList[0].Location.Distance(vehicleList[0].Location) > 15)
                            {
                                TreeRoot.StatusText = "Waiting for Mob to Come Into Range or Appear.";
                                return RunStatus.Running;
                            }
                            else if (npcList.Count >= 1 && npcList[0].Location.Distance(vehicleList[0].Location) <= 15)
                            {
                                using (new FrameLock())
                                {
                                    TreeRoot.StatusText = "Using Spell Index On - " + npcList[0].Name + " Spell Index: " + SpellIndex;
                                    npcList[0].Target();
                                    Lua.DoString("CastPetAction({0})", SpellIndex);
                                    Counter++;
                                    return RunStatus.Success;
                                }
                            }
                            return RunStatus.Running;
                        })),

                    new Decorator(c => InVehicle && SpellType == 2,
                        new Action(c =>
                        {
                            if (npcList.Count >= 1)
                            {
                                Thread.Sleep(OftenToUse);
                                using (new FrameLock())
                                {
                                    TreeRoot.StatusText = "Using Spell Index On - " + npcList[0].Name + " Spell Index: " + SpellIndex + " Times Used: " + Counter;

                                    if (Counter > TimesToUse || (me.QuestLog.GetQuestById(QuestId) != null && me.QuestLog.GetQuestById(QuestId).IsCompleted && QuestId > 0))
                                    {
                                        Lua.DoString("VehicleExit()");
                                        _isDone = true;
                                        return RunStatus.Success;
                                    }
                                    npcList[0].Target();
                                    Lua.DoString("CastPetAction({0})", SpellIndex);
                                    LegacySpellManager.ClickRemoteLocation(npcList[0].Location);
                                    Counter++;
                                    return RunStatus.Running;
                                }
                                
                            }
                            return RunStatus.Running;
                        })),

                   new Decorator(c => InVehicle && SpellType == 3,
                        new Action(c =>
                        {
                            if (npcList.Count >= 1)
                            {
                                using (new FrameLock())
                                {
                                    
                                    TreeRoot.StatusText = "Using Spell Index On - " + npcList[0].Name + " Spell Index: " + SpellIndex + " Times Used: " + Counter;
                                    if (Counter > TimesToUse || (me.QuestLog.GetQuestById(QuestId) != null && me.QuestLog.GetQuestById(QuestId).IsCompleted && QuestId > 0))
                                    {
                                        Lua.DoString("VehicleExit()");
                                        _isDone = true;
                                        return RunStatus.Success;
                                    }
                                    Random rnd = new Random();
                                    int r = rnd.Next(npcList.Count);
                                    npcList[r].Target();
                                    WoWMovement.ClickToMove(npcList[r].Location);

                                    Random rand = new Random();
                                    Lua.DoString("VehicleAimRequestNormAngle({0})", 0.1 + (rand.NextDouble() * (0.6 - 0.1)));
                                    Lua.DoString("CastPetAction({0})", SpellIndex);
                                    Counter++;
                                    return RunStatus.Running;
                                }
                            }
                            return RunStatus.Running;
                        })),

                    new Decorator(c => InVehicle && SpellType == 4,
                        new Action(c =>
                        {
                            if (npcList.Count >= 1)
                            {
                                using (new FrameLock())
                                {
                                    if (Counter > TimesToUse || (me.QuestLog.GetQuestById(QuestId) != null && me.QuestLog.GetQuestById(QuestId).IsCompleted && QuestId > 0))
                                    {
                                        Lua.DoString("VehicleExit()");
                                        _isDone = true;
                                        return RunStatus.Success;
                                    }
                                    npcList[0].Target();
                                    WoWMovement.ClickToMove(npcList[0].Location);
                                    Lua.DoString("CastPetAction({0})", SpellIndex);
                                    LegacySpellManager.ClickRemoteLocation(npcList[0].Location);
                                    Counter++;
                                    return RunStatus.Running;
                                }
                            }
                            return RunStatus.Running;
                        }))
                ));
        }

        double AimAngle
        {
            get
            {
                if (npcList[0].Location.Distance(MountedPoint) < 10)
                {
                    return 0;
                }
                else if (npcList[0].Location.Distance(MountedPoint) >= 10 && npcList[0].Location.Distance(MountedPoint) < 20)
                {
                    return 0.2;
                }
                else if (npcList[0].Location.Distance(MountedPoint) >= 20 && npcList[0].Location.Distance(MountedPoint) < 30)
                {
                    return 0.4;
                }
                else if (npcList[0].Location.Distance(MountedPoint) >= 40 && npcList[0].Location.Distance(MountedPoint) < 60)
                {
                    return 0.5;
                }
                else
                {
                    return 0.2;
                }
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
