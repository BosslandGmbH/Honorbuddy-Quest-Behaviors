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
using Styx.Logic.Combat;
using Styx.Logic.BehaviorTree;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    public class CastSpellOn : CustomForcedBehavior
    {

        /// <summary>
        /// CastSpellOn by Natfoth
        /// Allows you to use a Specific Spell on a Target, useful for Dummies and Starting Quests.
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// SpellId: Spell you wish to cast on the Target
        /// NumOfTimes: How many times before the script finishes
        /// HpLeftAmount: How low the HP should be before casting a spell on it. Such as wounded targets
        /// MinRange: If the spell has a minRange to it
        /// X,Y,Z: The general location where these objects can be found
        /// </summary>
        /// 

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"SpellId",null},
            {"NpcId",null},
            {"NumOfTimes",null},
            {"HpLeftAmount",null},
            {"MinRange",null},
            {"X",null},
            {"Y",null},
            {"Z",null},
            {"QuestId",null},

        };

        bool success = true;

        public CastSpellOn(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);

            int spellId = 0;
            int mobid = 0;
            int numberoftimes = 0;
            int hpleftamount = 0;
            int minRange = 0;
            int questId = 0;
            WoWPoint location = new WoWPoint(0, 0, 0);

            success = success && GetAttributeAsInteger("SpellId", true, "1", 0, int.MaxValue, out spellId);
            success = success && GetAttributeAsInteger("NpcId", true, "1", 0, int.MaxValue, out mobid);
            success = success && GetAttributeAsInteger("NumOfTimes", false, "1", 1, int.MaxValue, out numberoftimes);
            success = success && GetAttributeAsInteger("HpLeftAmount", false, "110", 0, int.MaxValue, out hpleftamount); ;
            success = success && GetAttributeAsInteger("MinRange", false, "3", 0, int.MaxValue, out minRange);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
            success = success && GetXYZAttributeAsWoWPoint("X", "Y", "Z", true, new WoWPoint(0, 0, 0), out location);

            QuestId = (uint)questId;
            SpellID = spellId;
            MobId = mobid;
            Counter = 1;
            HPLeftAmount = hpleftamount;
            MovedToTarget = false;
            MinRange = minRange;
            NumberOfTimes = numberoftimes;
            Location = location;

        }

        public WoWPoint Location { get; private set; }
        public int Counter { get; set; }
        public int SpellID { get; set; }
        public int MobId { get; set; }
        public bool MovedToTarget;
        public int NumberOfTimes { get; set; }
        public int HPLeftAmount { get; set; }
        public int MinRange { get; set; }
        public uint QuestId { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        public List<WoWUnit> mobList
        {
            get
            {
                if (HPLeftAmount > 0)
                {
                    return ObjectManager.GetObjectsOfType<WoWUnit>()
                                                                .Where(u => u.Entry == MobId && !u.Dead && u.HealthPercent <= HPLeftAmount)
                                                                .OrderBy(u => u.Distance).ToList();
                }
                else
                {
                    return ObjectManager.GetObjectsOfType<WoWUnit>()
                                            .Where(u => u.Entry == MobId && !u.Dead)
                                            .OrderBy(u => u.Distance).ToList();
                }
            }
        }

        #region Overrides of CustomForcedBehavior

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

            if (quest != null)
            {
                TreeRoot.GoalText = "CombatUseItem - " + quest.Name;
            }
            else
            {
                TreeRoot.GoalText = "CastSpellOn: Running";
            }
        }

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                            new Decorator(ret => (Counter > NumberOfTimes && QuestId == 0) || (me.QuestLog.GetQuestById(QuestId) != null && me.QuestLog.GetQuestById(QuestId).IsCompleted),
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new WaitContinue(120,
                                        new Action(delegate
                                        {
                                            _isDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )),

                           new Decorator(ret => mobList.Count == 0,
                                new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Moving To Location - X: " + Location.X + " Y: " + Location.Y),
                                        new Action(ret => Navigator.MoveTo(Location)),
                                        new Action(ret => Thread.Sleep(300))
                                    )
                                ),

                           new Decorator(ret => mobList.Count > 0 && !me.IsCasting,
                                new Sequence(
                                    new DecoratorContinue(ret => mobList[0].Location.Distance(me.Location) >= MinRange && mobList[0].Location.Distance(me.Location) <= 25,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Casting Spell - " + SpellID + " On Mob: " + mobList[0].Name + " Yards Away "+ mobList[0].Location.Distance(me.Location)),
                                            new Action(ret => WoWMovement.MoveStop()),
                                            new Action(ret => Thread.Sleep(300)),
                                            new Decorator(c => !me.IsCasting, CreateSpellBehavior)
                                            )
                                    ),
                                    new DecoratorContinue(ret => mobList[0].Location.Distance(me.Location) > 25,
                                        new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Moving To Mob - " + mobList[0].Name + " Yards Away: " + mobList[0].Location.Distance(me.Location)),
                                        new Action(ret => Navigator.MoveTo(mobList[0].Location)),
                                        new Action(ret => Thread.Sleep(300))
                                            )
                                    ),

                                   new DecoratorContinue(ret => mobList[0].Location.Distance(me.Location) < MinRange,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Too Close, Backing Up"),
                                            new Action(ret => mobList[0].Face()),
                                            new Action(ret => Thread.Sleep(100)),
                                            new Action(ret => WoWMovement.Move(WoWMovement.MovementDirection.Backwards)),
                                            new Action(ret => Thread.Sleep(100))
                                          ))
                                    ))

                                    

                            
                        )
                    );
        }

        Composite CreateSpellBehavior
        {
            get
            {
                return new Action(c =>
                {
                    if (SpellID > 0 && !me.IsCasting)
                    {
                        mobList[0].Target();
                        mobList[0].Face();
                        Thread.Sleep(300);
                        SpellManager.Cast(SpellID);

                        if (me.QuestLog.GetQuestById(QuestId) == null || QuestId == 0)
                        {
                            Counter++;
                        }
                        Thread.Sleep(300);
                        return RunStatus.Success;
                    }
                    else
                    {
                        _isDone = true;
                        return RunStatus.Success;
                    }

                });
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
