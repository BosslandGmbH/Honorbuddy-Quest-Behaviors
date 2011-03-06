using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
    public class CastSpellOn : CustomForcedBehavior
    {
        public CastSpellOn(Dictionary<string, string> args)
            : base(args)
        {
            uint spellId;
            if (!uint.TryParse(Args["SpellId"], out spellId))
                Logging.Write("Parsing SpellId in CastSpellOn behavior failed! please check your profile!");

            int npcID;
            if (!int.TryParse(Args["NpcId"], out npcID))
                Logging.Write("Parsing NpcId in CastSpellOn behavior failed! please check your profile!");

            int numberoftimes;
            if (!int.TryParse(Args["NumOfTimes"], out numberoftimes))
                Logging.Write("Parsing NumOfTimes in CastSpellOn behavior failed! please check your profile!");

            int hpleftamount;
            if (!int.TryParse(Args["HpLeftAmount"], out hpleftamount))
                Logging.Write("Parsing HpLeftAmount in CastSpellOn behavior failed! please check your profile!");

            int minRange;
            if (!int.TryParse(Args["MinRange"], out minRange))
                Logging.Write("Parsing MinRange in CastSpellOn behavior failed! please check your profile!");

            uint questId;
            if (!uint.TryParse(Args["QuestId"], out questId))
                Logging.Write("Parsing questid in CastSpellOn behavior failed! please check your profile!");

            float xcoord;
            if (!float.TryParse(Args["X"], out xcoord))
                Logging.Write("Parsing X in CastSpellOn behavior failed! please check your profile!");

            float ycoord;
            if (!float.TryParse(Args["Y"], out ycoord))
                Logging.Write("Parsing Y in CastSpellOn behavior failed! please check your profile!");

            float zcoord;
            if (!float.TryParse(Args["Z"], out zcoord))
                Logging.Write("Parsing Z in CastSpellOn behavior failed! please check your profile!");

            QuestId = questId;
            SpellID = spellId;
            NPCID = npcID;
            Counter = 1;
            HPLeftAmount = hpleftamount;
            MovedToTarget = false;
            MovedToNpc = true;
            MinRange = minRange;
            NumberOfTimes = numberoftimes;
            Location = new WoWPoint(xcoord, ycoord, zcoord);

        }

        public WoWPoint Location { get; private set; }
        public int Counter { get; set; }
        public uint SpellID { get; set; }
        public int NPCID { get; set; }
        public bool MovedToTarget;
        public bool MovedToNpc;
        public int NumberOfTimes { get; set; }
        public int HPLeftAmount { get; set; }
        public int MinRange { get; set; }
        public uint QuestId { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        public List<WoWUnit> npcList;

        /// <summary>
        /// A Queue for npc's we need to talk to
        /// </summary>
        //private WoWUnit CurrentUnit { get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(unit => unit.Distance < 100 && unit.Entry == MobId); } }

        #region Overrides of CustomForcedBehavior

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                    new Decorator(ret => (QuestId != 0 && me.QuestLog.GetQuestById(QuestId) != null &&
                         me.QuestLog.GetQuestById(QuestId).IsCompleted),
                        new Action(ret => _isDone = true)),

                    new Decorator(ret => Counter > NumberOfTimes,
                        new Action(ret => _isDone = true)),

                        new PrioritySelector(

                           new Decorator(ret => !MovedToTarget,
                                new Action(delegate
                                {
                                    WoWPoint destination1 = new WoWPoint(Location.X, Location.Y, Location.Z);
                                    WoWPoint[] pathtoDest1 = Styx.Logic.Pathing.Navigator.GeneratePath(me.Location, destination1);

                                    foreach (WoWPoint p in pathtoDest1)
                                    {
                                        if (npcList == null)
                                        {
                                            ObjectManager.Update();

                                            npcList = ObjectManager.GetObjectsOfType<WoWUnit>()
                                                .Where(u => u.Entry == NPCID && u.HealthPercent <= HPLeftAmount && !u.Dead)
                                                .OrderBy(u => u.Distance).ToList();
                                        }
                                        while (!me.Dead && p.Distance(me.Location) > 3)
                                        {
                                            if (me.Combat)
                                            {
                                                break;
                                            }

                                            if (npcList[0].Location.Distance(me.Location) <= MinRange && npcList.Count >= 1 && npcList[0].InLineOfSight)
                                            {
                                                MovedToTarget = true;
                                                Thread.Sleep(100);
                                                WoWMovement.MoveStop();
                                                return RunStatus.Success;
                                            }
                                            else
                                            {
                                                Thread.Sleep(100);
                                                WoWMovement.ClickToMove(p);
                                            }
                                        }

                                        if (me.Combat)
                                        {
                                            break;
                                        }
                                    }

                                    if (me.Combat)
                                    {
                                        return RunStatus.Success;
                                    }
                                    else if (!me.Combat)
                                    {
                                        MovedToTarget = true;
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

                                    if (!MovedToNpc)
                                    {
                                        WoWPoint destination = new WoWPoint(npcList[0].Location.X, npcList[0].Location.Y, npcList[0].Location.Z);
                                        WoWPoint[] pathtoDest = Styx.Logic.Pathing.Navigator.GeneratePath(me.Location, destination);

                                        foreach (WoWPoint p in pathtoDest)
                                        {
                                            while (!me.Dead && p.Distance(me.Location) > 3)
                                            {
                                                if (me.Combat)
                                                {
                                                    break;
                                                }
                                                if (npcList[0].Location.Distance(me.Location) <= MinRange && npcList.Count >= 1 && npcList[0].InLineOfSight)
                                                {
                                                    MovedToNpc = true;
                                                    Thread.Sleep(100);
                                                    WoWMovement.MoveStop();
                                                    return RunStatus.Success;
                                                }
                                                else
                                                {
                                                    Thread.Sleep(100);
                                                    WoWMovement.ClickToMove(p);
                                                }
                                            }

                                            if (me.Combat)
                                            {
                                                break;
                                            }
                                        }
                                    }

                                    if (npcList != null)
                                    {
                                        if (me.Combat)
                                        {
                                            return RunStatus.Success;
                                        }

                                        if (npcList[0].Location.Distance(me.Location) < MinRange && MinRange > 3 && npcList[0].InLineOfSight)
                                        {
                                            npcList[0].Face();
                                            Thread.Sleep(300);
                                            
                                            WoWMovement.Move(WoWMovement.MovementDirection.Backwards);
                                            Thread.Sleep(1000);
                                            WoWMovement.MoveStop();
                                        }
                                        else
                                        {
                                            Thread.Sleep(500);
                                            CastSpell();
                                        }

                                        

                                        if (me.Combat)
                                        {
                                            return RunStatus.Success;
                                        }
                                    }
                                    else
                                    {
                                        Logging.Write("NPC List is Empty");
                                    }

                                    return RunStatus.Running;
                                })
                                ),

                            new Action(ret => Navigator.MoveTo(Location))
                        )
                    ));
        }

        public void CastSpell()
        {
            npcList[0].Target();
            Thread.Sleep(300);
            npcList[0].Face();
            Thread.Sleep(300);
            Lua.DoString("CastSpellByID(" + SpellID + ")");
            Counter++;
            Thread.Sleep(3000);
        }

        private bool _isDone;
        public override bool IsDone
        {
            get
            {
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
                return _isDone && quest != null && quest.IsCompleted || quest == null;
            }
        }

        #endregion
    }
}
