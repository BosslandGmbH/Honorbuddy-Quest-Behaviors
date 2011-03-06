using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Styx.Logic.Combat;
using Styx.Database;
using Styx.Helpers;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    public class UseItemTargetLocation : CustomForcedBehavior
    {

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"ItemId",null},
            {"WaitTime",null},
            {"UseGO",null},
            {"UseNPC",null},
            {"NPCID",null},
            {"UseNPCLocation",null},
            {"GameObjectID",null},
            {"DoUntilFinished",null},
            {"QuestId",null},
            {"X",null},
            {"Y",null},
            {"Z",null},
            {"MoveX",null},
            {"MoveY",null},
            {"MoveZ",null},
            {"TargetX",null},
            {"TargetY",null},
            {"TargetZ",null},
         
        };

        bool success = true;


        public UseItemTargetLocation(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);

            int itemId = 0;
            int waittime = 0;
            int useGO = 0;
            int useNPC = 0;
            int npcID = 0;
            int useNpcLocation = 0;
            int goID = 0;
            int douUtilFinished = 0;
            int questId = 0;
            WoWPoint movelocation = new WoWPoint(0, 0, 0);
            WoWPoint targetlocation = new WoWPoint(0, 0, 0);
            
            success = success && GetAttributeAsInteger("ItemId", true, "1", 0, int.MaxValue, out itemId);
            success = success && GetAttributeAsInteger("WaitTime", true, "1", 0, int.MaxValue, out waittime);
            success = success && GetAttributeAsInteger("UseGO", false, "0", 0, int.MaxValue, out useGO);
            success = success && GetAttributeAsInteger("UseNPC", false, "0", 0, int.MaxValue, out useNPC);
            success = success && GetAttributeAsInteger("NPCID", false, "1", 0, int.MaxValue, out npcID);
            success = success && GetAttributeAsInteger("UseNPCLocation", false, "0", 0, int.MaxValue, out useNpcLocation);
            success = success && GetAttributeAsInteger("GameObjectID", false, "1", 0, int.MaxValue, out goID);
            success = success && GetAttributeAsInteger("DoUntilFinished", false, "0", 0, int.MaxValue, out douUtilFinished);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);

            if (useNPC == 1 || useGO == 1)
            {

                
                success = success && GetXYZAttributeAsWoWPoint("X", "Y", "Z", true, new WoWPoint(0, 0, 0), out movelocation);

                MoveLocation = movelocation;
            }
            else
            {
                success = success && GetXYZAttributeAsWoWPoint("MoveX", "MoveY", "MoveZ", true, new WoWPoint(0, 0, 0), out movelocation);
                success = success && GetXYZAttributeAsWoWPoint("TargetX", "TargetY", "TargetZ", true, new WoWPoint(0, 0, 0), out targetlocation);

                MoveLocation = movelocation;
                TargetLocation = targetlocation;
            }


            QuestId = (uint)questId;
            WaitTime = waittime;
            ItemID = itemId;
            UseGO = useGO;
            DoUntilFinished = douUtilFinished;
            UseNPC = useNPC;
            NpcID = npcID;
            ObjectID = goID;
            Counter = 1;
            UseNPCLocation = useNpcLocation;
            MovedToTarget = false;
            
        }

        public WoWPoint MoveLocation { get; private set; }
        public WoWPoint TargetLocation { get; private set; }
        public int Counter { get; set; }
        public int DoUntilFinished { get; set; }
        public int UseNPCLocation { get; set; }
        public int ItemID { get; set; }
        public int WaitTime { get; set; }
        public int UseGO { get; set; }
        public int UseNPC { get; set; }
        public int NpcID { get; set; }
        public int ObjectID { get; set; }
        public bool MovedToTarget;
        public uint QuestId { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        public List<WoWGameObject> objectList;
        public List<WoWUnit> npcList;


        #region Overrides of CustomForcedBehavior

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                    new Decorator(ret => (QuestId != 0 && me.QuestLog.GetQuestById(QuestId) != null &&
                         me.QuestLog.GetQuestById(QuestId).IsCompleted),
                        new Action(ret => _isDone = true)),

                    new Decorator(ret => Counter > 1,
                        new Action(ret => _isDone = true)),

                        new PrioritySelector(

                           new Decorator(ret => !MovedToTarget,
                                new Action(delegate
                                {

                                    Navigator.MoveTo(MoveLocation);

                                    if (MoveLocation.Distance(me.Location) < 3)
                                    {
                                        MovedToTarget = true;
                                        return RunStatus.Success;
                                    }

                                    if (me.Combat)
                                    {
                                        return RunStatus.Success;
                                    }

                                    return RunStatus.Running;

                                })
                                ),

                            new Decorator(ret => StyxWoW.Me.IsMoving,
                                new Action(delegate
                                {
                                    WoWMovement.MoveStop();
                                    StyxWoW.SleepForLagDuration();
                                })
                                ),

                            new Decorator(ret => MovedToTarget,
                                new Action(delegate
                                {
                                    // CurrentUnit.Interact();
									
									PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
                                    if (quest.IsCompleted)
                                    {
                                        Counter++;
                                        return RunStatus.Success;
                                    }

                                    if (UseNPCLocation >= 1)
                                    {
                                        ObjectManager.Update();

                                        npcList = ObjectManager.GetObjectsOfType<WoWUnit>()
                                            .Where(u => u.Entry == NpcID && !u.Dead)
                                            .OrderBy(u => u.Distance).ToList();

                                        if (npcList != null)
                                        {

                                            if (npcList[0].Location.Distance(me.Location) > 6 || !npcList[0].InLineOfSight)
                                            {
                                                WoWPoint destination = new WoWPoint(npcList[0].Location.X, npcList[0].Location.Y, npcList[0].Location.Z);

                                                Navigator.MoveTo(destination);
                                                Thread.Sleep(300);
                                            }
                                            else
                                            {
                                                if (me.Combat)
                                                {
                                                    return RunStatus.Success;
                                                }

                                                CastSpell();
                                            }
                                        }
                                        else
                                        {
                                            if (me.Combat)
                                            {
                                                return RunStatus.Success;
                                            }

                                            CastSpell();
                                        }

                                    }



                                    if (me.Combat)
                                    {
                                        return RunStatus.Success;
                                    }

                                    if (Counter > 1)
                                    {
                                        return RunStatus.Success;
                                    }
                                    return RunStatus.Running;
                                })
                                ),

                            new Action(ret => Navigator.MoveTo(MoveLocation))
                        )
                    ));
        }

        public void CastSpell()
        {

            ObjectManager.Update();

            Logging.Write("Using Item");

            Lua.DoString("UseItemByName(\"" + ItemID + "\")");
            if (DoUntilFinished == 0)
            {
                Counter++;
            }
            
            if (UseGO == 0 && UseNPC == 0)
            {
                LegacySpellManager.ClickRemoteLocation(TargetLocation);
            }
            else if (UseGO == 1)
            {
                objectList = ObjectManager.GetObjectsOfType<WoWGameObject>()
                                        .Where(u => u.Entry == ObjectID && !u.InUse && !u.IsDisabled)
                                        .OrderBy(u => u.Distance).ToList();


                LegacySpellManager.ClickRemoteLocation(objectList[0].Location);
            }
            else if (UseNPC == 1)
            {
                npcList = ObjectManager.GetObjectsOfType<WoWUnit>()
                                        .Where(u => u.Entry == NpcID)
                                        .OrderBy(u => u.Distance).ToList();


                LegacySpellManager.ClickRemoteLocation(npcList[0].Location);
            }

            Thread.Sleep(WaitTime);
        }



        private bool _isDone;
        public override bool IsDone
        {
            get { return _isDone; }
        }

        #endregion
    }
}
