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
    public class CombatUseItemOn : CustomForcedBehavior
    {

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"ItemId",null},
            {"NpcId",null},
            {"NumOfTimes",null},
            {"HpLeftAmount",null},
            {"X",null},
            {"Y",null},
            {"Z",null},
            {"QuestId",null},

        };

        bool success = true;


        public CombatUseItemOn(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);

            int itemId = 0;
            int npcID = 0;
            int numberoftimes = 0;
            int hpleftamount = 0;
            int questId = 0;
            WoWPoint location = new WoWPoint(0, 0, 0);

            success = success && GetAttributeAsInteger("ItemId", true, "1", 0, int.MaxValue, out itemId);            
            success = success && GetAttributeAsInteger("NpcId", true, "1", 0, int.MaxValue, out npcID);
            success = success && GetAttributeAsInteger("NumOfTimes", true, "1", 0, int.MaxValue, out numberoftimes);
            success = success && GetAttributeAsInteger("HpLeftAmount", false, "110", 0, int.MaxValue, out hpleftamount);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
            success = success && GetXYZAttributeAsWoWPoint("X", "Y", "Z", true, new WoWPoint(0, 0, 0), out location);

            QuestId = (uint)questId;
            ItemID = itemId;
            NPCID = npcID;
            Counter = 1;
            Counter123 = 0;
            HPLeftAmount = hpleftamount;
            MovedToTarget = false;
            NumberOfTimes = numberoftimes;
            Location = location;
        }

        public WoWPoint Location { get; private set; }
        public int Counter { get; set; }
        public int Counter123 { get; set; }
        public int ItemID { get; set; }
        public int NPCID { get; set; }
        public bool MovedToTarget;
        public int NumberOfTimes { get; set; }
        public int HPLeftAmount { get; set; }
        public uint QuestId { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

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

                    new Decorator(ret => Counter > NumberOfTimes,
                        new Action(ret => _isDone = true)),

                        new PrioritySelector(

                           new Decorator(ret => !MovedToTarget,
                                new Action(delegate
                                {
                                    Counter123++;
                                    if (me.Auras.ContainsKey("Freezing Trap Effect"))
                                    {
                                        Logging.Write("HasAura");
                                        CastSpell();
                                        Counter++;
                                        return RunStatus.Success;
                                    }

                                    WoWPoint destination1 = new WoWPoint(Location.X, Location.Y, Location.Z);
                                    WoWPoint[] pathtoDest1 = Styx.Logic.Pathing.Navigator.GeneratePath(me.Location, destination1);

                                    foreach (WoWPoint p in pathtoDest1)
                                    {
                                        while (!me.Dead && p.Distance(me.Location) > 3)
                                        {
                                            Thread.Sleep(100);
                                            WoWMovement.ClickToMove(p);
                                        }
                                    }
                                    //Styx.Logic.Combat.SpellManager.

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

                            new Action(ret => Navigator.MoveTo(Location))
                        )
                    ));
        }

        public void CastSpell()
        {
            Logging.Write("Casted Spell " + Counter + " Times out of " + NumberOfTimes);
            WoWItem ItemUseage = me.CarriedItems.Where(item => item.Entry.Equals(ItemID)).FirstOrDefault();

            ItemUseage.Interact();
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
