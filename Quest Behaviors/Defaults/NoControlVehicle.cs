using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Styx.Logic.BehaviorTree;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
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
        /// TimesToUse: Where you want to be at when you fire.
        /// TypeId: Where you want to aim.
        /// PreviousFireLocation Coords: This should only be used if you are already inside of the vehicle when you call the behaviors again, and
        ///                                 should be the same coords as FireLocation on the call before it, Check the Wiki for more info or examples.
        /// </summary>
        ///
        public NoControlVehicle(Dictionary<string, string> args)
            : base(args)
        {
			try
			{
                int     homeIndex;
                int     maxRange;
                int     npcMountId;
                int     oftenToUse;
                int     questId;
                int     spellIndex;
                int     targetId;
                int     targetId2;
                int     targetId3;
                int     timesToUse;
                int     typeId;
                int     vehicleId;
                int     waitTime;

                CheckForUnrecognizedAttributes(new Dictionary<string, object>()
                                                {
                                                    { "AttackIndex",    null },
                                                    { "HomeIndex",      null },
                                                    { "MaxRange",       null },
                                                    { "MobId",          null },
                                                    { "NpcId",          null },
                                                    { "NpcMountId",     null },
                                                    { "NumOfTimes",     null },
                                                    { "QuestId",        null },
                                                    { "OftenToUse",     null },
                                                    { "SpellIndex",     null },
                                                    { "TargetId",       null },
                                                    { "TargetId2",      null },
                                                    { "TargetId3",      null },
                                                    { "TimesToUse",     null },
                                                    { "TypeId",         null },
                                                    { "VehicleId",      null },
                                                    { "WaitTime",       null },
                                                });

                _isAttributesOkay = true;
                _isAttributesOkay &= GetAttributeAsInteger("HomeIndex", false, "1", 0, int.MaxValue, out homeIndex);
                _isAttributesOkay &= GetAttributeAsInteger("MaxRange", false, "1", 0, int.MaxValue, out maxRange);
                _isAttributesOkay &= GetAttributeAsInteger("NpcMountId", false, "1", 0, int.MaxValue, out npcMountId);
                _isAttributesOkay &= GetAttributeAsInteger("OftenToUse", false, "1000", 0, int.MaxValue, out oftenToUse);
                _isAttributesOkay &= GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);


                _isAttributesOkay &= GetAttributeAsInteger("AttackIndex", false, "0", 0, int.MaxValue, out spellIndex);
                if (spellIndex == 0)
                    { _isAttributesOkay &= GetAttributeAsInteger("SpellIndex", true, "0", 0, int.MaxValue, out spellIndex); }


                _isAttributesOkay &= GetAttributeAsInteger("NpcId", false, "0", 0, int.MaxValue, out targetId);
                if (targetId == 0)
                    { _isAttributesOkay &= GetAttributeAsInteger("MobId", false, "0", 0, int.MaxValue, out targetId); }
                if (targetId == 0)
                    { _isAttributesOkay &= GetAttributeAsInteger("TargetId", true, "0", 0, int.MaxValue, out targetId); }


                _isAttributesOkay &= GetAttributeAsInteger("TargetId2", false, "0", 0, int.MaxValue, out targetId2);
                _isAttributesOkay &= GetAttributeAsInteger("TargetId3", false, "0", 0, int.MaxValue, out targetId3);


                _isAttributesOkay &= GetAttributeAsInteger("NumOfTimes", false, "0", 0, int.MaxValue, out timesToUse);
                if (timesToUse == 0)
                    { _isAttributesOkay &= GetAttributeAsInteger("TimesToUse", false, "1", 0, int.MaxValue, out timesToUse); }


                _isAttributesOkay &= GetAttributeAsInteger("TypeId", false, "2", 0, 4, out typeId);
                _isAttributesOkay &= GetAttributeAsInteger("VehicleId", false, "0", 0, int.MaxValue, out vehicleId);
                _isAttributesOkay &= GetAttributeAsInteger("WaitTime", false, "0", 0, int.MaxValue, out waitTime);


                // Semantic coherency --
                if (_isAttributesOkay)
                {
                    if (((Args.ContainsKey("AttackIndex") ? 1 : 0)  + (Args.ContainsKey("SpellIndex") ? 1 : 0))  >  1)
                    {
                        UtilLogMessage("error", "\"AttackIndex\", and \"SpellIndex\" attributes are mutually exclusive."
                                                + "   Use \"AttackIndex\" (\"SpellIndex\" is deprecated).");
                        _isAttributesOkay = false;
                    }

                    if ((Args.ContainsKey("TargetId") ? 1 : 0) 
                        + (Args.ContainsKey("MobId") ? 1 : 0)
                        + (Args.ContainsKey("NpcId") ? 1 : 0)  > 1)
                    {
                        UtilLogMessage("error", "\"TargetId\", \"MobId\", and \"NpcId\" attributes are mutually exclusive."
                                                + "   Use \"TargetId\" (\"MobId\" and \"NpcId\" are deprecated).");
                        _isAttributesOkay = false;
                    }

                    if (spellIndex == 0)
                    {
                        UtilLogMessage("error", "\"SpellIndex\" may not be zero.");
                        _isAttributesOkay = false;
                    }

                    if (targetId == 0)
                    {
                        UtilLogMessage("error", "\"TargetId\" may not be zero.");
                        _isAttributesOkay = false;
                    }
                }


                if (_isAttributesOkay)
                {
                    QuestId = (uint)questId;
                    NpcMountID = npcMountId;
                    SpellType = typeId;
                    MaxRange = maxRange;
                    TargetID = targetId;
                    TargetID2 = targetId2;
                    TargetID3 = targetId3;
                    SpellIndex = spellIndex;
                    HomeIndex = homeIndex;
                    VehicleID = vehicleId;
                    TimesToUse = timesToUse;
                    OftenToUse = oftenToUse;
                    WaitTime = waitTime;

                    MountedPoint = WoWPoint.Empty;
                }
			}

			catch (Exception except)
			{
				// Maintenance problems occur for a number of reasons.  The primary two are...
				// * Changes were made to the behavior, and boundary conditions weren't properly tested.
				// * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
				// In any case, we pinpoint the source of the problem area here, and hopefully it
				// can be quickly resolved.
				UtilLogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
										+ "\nFROM HERE:\n"
										+ except.StackTrace + "\n");
				_isAttributesOkay = false;
			}
        }


        public int      Counter = 1;
        public int      HomeIndex { get; set; }
        public WoWPoint LocationDest { get; private set; }
        public WoWPoint LocationMount { get; private set; }
        public int      MaxRange = 1;
        public WoWPoint MountedPoint { get; private set; }
        public int      NpcMountID { get; set; }
        public int      OftenToUse { get; set; }
        public uint     QuestId { get; set; }
        public int      SpellIndex { get; set; }
        public int      SpellType { get; set; }
        public int      TargetID { get; set; }
        public int      TargetID2 { get; set; }
        public int      TargetID3 { get; set; }
        public int      TimesToUse { get; set; }
        public int      WaitTime { get; set; }
        public int      VehicleID { get; set; }

        private bool        _isAttributesOkay;
        private bool        _isBehaviorDone;
        private Composite   _root;

        public static LocalPlayer s_me = ObjectManager.Me;


        public List<WoWUnit> NpcList
        {
            get
            {
                if (VehicleList.Count > 0)
                {
                    return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                         .Where(u => (u.Entry == TargetID || u.Entry == TargetID2 || u.Entry == TargetID3) && VehicleList[0].Location.Distance(u.Location) <= MaxRange)
                                         .OrderBy(u => u.Distance)
                                         .ToList());
                }
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => (u.Entry == TargetID || u.Entry == TargetID2 || u.Entry == TargetID3) && !u.Dead).OrderBy(u => u.Distance)
                                     .ToList());
            }
        }


        public List<WoWUnit> VehicleList
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(ret => (ret.Entry == VehicleID) && !ret.Dead)
                                     .ToList());
            }
        }


        public List<WoWUnit> NpcVehicleList
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(ret => (ret.Entry == NpcMountID) && !ret.Dead)
                                     .OrderBy(u => u.Distance)
                                     .ToList());
            }
        }

        static public bool InVehicle { get { return Lua.GetReturnVal<int>("if IsPossessBarVisible() or UnitInVehicle('player') then return 1 else return 0 end", 0) == 1; } }


        double AimAngle
        {
            get
            {
                if (NpcList[0].Location.Distance(MountedPoint) < 10)
                    { return 0; }

                else if (NpcList[0].Location.Distance(MountedPoint) >= 10 && NpcList[0].Location.Distance(MountedPoint) < 20)
                    { return 0.2; }

                else if (NpcList[0].Location.Distance(MountedPoint) >= 20 && NpcList[0].Location.Distance(MountedPoint) < 30)
                    { return 0.4; }

                else if (NpcList[0].Location.Distance(MountedPoint) >= 40 && NpcList[0].Location.Distance(MountedPoint) < 60)
                    { return 0.5; }

                else
                    { return 0.2; }
            }
        }


       #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ??
                (_root = new PrioritySelector(
                    new Decorator(c => Counter > TimesToUse,
                        new Action(c =>
                        {
                            TreeRoot.StatusText = "Finished!";
                            if (HomeIndex > 0)
                            {
                                Lua.DoString("CastPetAction({0})", HomeIndex);
                            }
                            _isBehaviorDone = true;
                            return RunStatus.Success;
                        })
                    ),

                    new Decorator(c => NpcVehicleList.Count > 0 && !InVehicle,
                        new Action(c =>
                        {
                            if (!NpcVehicleList[0].WithinInteractRange)
                            {
                                Navigator.MoveTo(NpcVehicleList[0].Location);
                                TreeRoot.StatusText = "Moving To Vehicle - " + NpcVehicleList[0].Name + " Yards Away: " + NpcVehicleList[0].Location.Distance(s_me.Location);
                            }
                            else
                            {
                                NpcVehicleList[0].Interact();
                                MountedPoint = s_me.Location;
                            }

                        })
                    ),
                    new Decorator(c => InVehicle && SpellType == 1,
                        new Action(c =>
                        {
                            if (NpcList.Count == 0 || NpcList[0].Location.Distance(VehicleList[0].Location) > 15)
                            {
                                TreeRoot.StatusText = "Waiting for Mob to Come Into Range or Appear.";
                                return RunStatus.Running;
                            }
                            else if (NpcList.Count >= 1 && NpcList[0].Location.Distance(VehicleList[0].Location) <= 15)
                            {
                                TreeRoot.StatusText = "Using Spell Index On - " + NpcList[0].Name + " Spell Index: " + SpellIndex;
                                NpcList[0].Target();
                                Lua.DoString("CastPetAction({0})", SpellIndex);
                                Thread.Sleep(WaitTime);
                                Counter++;
                                return RunStatus.Success;
                            }
                            return RunStatus.Running;
                        })),

                    new Decorator(c => InVehicle && SpellType == 2,
                        new Action(c =>
                        {
                            if (NpcList.Count >= 1)
                            {
                                Thread.Sleep(OftenToUse);

                                TreeRoot.StatusText = "Using Spell Index On - " + NpcList[0].Name + " Spell Index: " + SpellIndex + " Times Used: " + Counter;

                                if (Counter > TimesToUse || (s_me.QuestLog.GetQuestById(QuestId) != null && s_me.QuestLog.GetQuestById(QuestId).IsCompleted && QuestId > 0))
                                {
                                    Lua.DoString("VehicleExit()");
                                    _isBehaviorDone = true;
                                    return RunStatus.Success;
                                }
                                NpcList[0].Target();
                                Lua.DoString("CastPetAction({0})", SpellIndex);
                                LegacySpellManager.ClickRemoteLocation(NpcList[0].Location);
                                Thread.Sleep(WaitTime);
                                Counter++;
                                return RunStatus.Running;                                
                            }
                            return RunStatus.Running;
                        })),

                   new Decorator(c => InVehicle && SpellType == 3,
                        new Action(c =>
                        {
                            if (NpcList.Count >= 1)
                            {
                                using (new FrameLock())
                                {
                                    TreeRoot.StatusText = "Using Spell Index On - " + NpcList[0].Name + " Spell Index: " + SpellIndex + " Times Used: " + Counter;
                                    if (Counter > TimesToUse || (s_me.QuestLog.GetQuestById(QuestId) != null && s_me.QuestLog.GetQuestById(QuestId).IsCompleted && QuestId > 0))
                                    {
                                        Lua.DoString("VehicleExit()");
                                        _isBehaviorDone = true;
                                        return RunStatus.Success;
                                    }
                                    Random rnd = new Random();
                                    int r = rnd.Next(NpcList.Count);
                                    NpcList[r].Target();
                                    WoWMovement.ClickToMove(NpcList[r].Location);

                                    Random rand = new Random();
                                    Lua.DoString("VehicleAimRequestNormAngle({0})", 0.1 + (rand.NextDouble() * (0.6 - 0.1)));
                                    Lua.DoString("CastPetAction({0})", SpellIndex);
                                    Thread.Sleep(WaitTime);
                                    Counter++;
                                    return RunStatus.Running;
                                }
                            }
                            return RunStatus.Running;
                        })),

                    new Decorator(c => InVehicle && SpellType == 4,
                        new Action(c =>
                        {
                            if (NpcList.Count >= 1)
                            {
                                using (new FrameLock())
                                {
                                    if (Counter > TimesToUse || (s_me.QuestLog.GetQuestById(QuestId) != null && s_me.QuestLog.GetQuestById(QuestId).IsCompleted && QuestId > 0))
                                    {
                                        Lua.DoString("VehicleExit()");
                                        _isBehaviorDone = true;
                                        return RunStatus.Success;
                                    }
                                    NpcList[0].Target();
                                    WoWMovement.ClickToMove(NpcList[0].Location);
                                    Lua.DoString("CastPetAction({0})", SpellIndex);
                                    LegacySpellManager.ClickRemoteLocation(NpcList[0].Location);
                                    Counter++;
                                    return RunStatus.Running;
                                }
                            }
                            return RunStatus.Running;
                        }))
                ));
        }


        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone    // normal completion
                        ||  !UtilIsProgressRequirementsMet((int)QuestId, 
                                                           QuestInLogRequirement.InLog, 
                                                           QuestCompleteRequirement.NotComplete));
            }
        }


        public override void OnStart()
        {
			if (!_isAttributesOkay)
			{
				UtilLogMessage("error", "Stopping Honorbuddy.  Please repair the profile!");

                // *Never* want to stop Honorbuddy (e.g., TreeRoot.Stop()) in the constructor --
                // This would defeat the "ProfileDebuggingMode" configurable that builds an instance of each
                // used behavior when the profile is loaded.
				TreeRoot.Stop();
			}

            else
            {
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

                TreeRoot.GoalText = string.Format("{0}: {1}",
                                                  this.GetType().Name,
                                                  (quest == null) ? "Running" : ("\"" + quest.Name + "\""));
            }
        }

        #endregion
    }
}
