using System;
using System.Collections.Generic;
using System.Linq;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;
using Styx.Logic.Combat;

namespace Styx.Bot.Quest_Behaviors
{
    public class CombatUseItemOn : CustomForcedBehavior
    {

        /// <summary>
        /// CombatUseItemOn by raphus
        /// Allows you to use an Item after you gain an Aura.
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// ItemId: Id of the Item you wish to use once you have an aura.
        /// HasAura: Aura ID of the aura on you when to use the item
        /// NpcHasAura: Aura ID of the aura on npc when to use the item
        /// NpcHPLeft: Hp of the npc when to use the item
        /// CastingSpellId: Spell ID of the spell that npc is casting when to use the item
        /// NpcId: NpcID of the mob that will attack you.
        /// NumOfTimes: How times needed to use the item.
        /// X,Y,Z: The general location where these objects can be found
        /// </summary>
        /// 

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"ItemId",null},
            {"NpcId",null},
            {"MobId",null},
            {"NumOfTimes",null},
            {"X",null},
            {"Y",null},
            {"Z",null},
            {"HasAura",null},
            {"NpcHPLeft",null},
            {"NpcHasAura",null},
            {"CastingSpellId",null},
            {"QuestId",null},

        };

        bool success = true;

        public CombatUseItemOn(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);

            int itemId = 0;
            int mobid = 0;
            int hasaura = 0;
            int npcHPLeft = 0;
            int npcHasAura = 0;
            int castingSpellId = 0;
            int numberoftimes = 0;
            int questId = 0;
            WoWPoint location = new WoWPoint(0, 0, 0);

            success = success && GetAttributeAsInteger("ItemId", true, "1", 0, int.MaxValue, out itemId);
            success = success && GetAttributeAsInteger("NpcId", true, "1", 0, int.MaxValue, out mobid);
            success = success && GetAttributeAsInteger("HasAura", false, "0", 0, int.MaxValue, out hasaura);
            success = success && GetAttributeAsInteger("NumOfTimes", false, "1", 0, int.MaxValue, out numberoftimes);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
            success = success && GetAttributeAsInteger("NpcHPLeft", false, "0", 0, int.MaxValue, out npcHPLeft);
            success = success && GetAttributeAsInteger("NpcHasAura", false, "0", 0, int.MaxValue, out npcHasAura);
            success = success && GetAttributeAsInteger("CastingSpellId", false, "0", 0, int.MaxValue, out castingSpellId);
            success = success && GetXYZAttributeAsWoWPoint("X", "Y", "Z", true, new WoWPoint(0, 0, 0), out location);

            if (mobid == 1)
                success = success && GetAttributeAsInteger("MobId", false, "1", 0, int.MaxValue, out mobid);

            QuestId = (uint)questId;
            ItemID = itemId;
            MobId = mobid;
            HasAura = hasaura;
            NpcHasAura = npcHasAura;
            NpcHPLeft = npcHPLeft;
            CastingSpellId = castingSpellId;
            Counter = 0;
            NumberOfTimes = numberoftimes;
            Location = location;
        }

        public WoWPoint Location { get; private set; }
        public int Counter { get; set; }
        public int ItemID { get; set; }
        public int MobId { get; set; }
        public int HasAura { get; set; }
        public int NpcHasAura { get; set; }
        public int NpcHPLeft { get; set; }
        public int CastingSpellId { get; set; }
        public bool MovedToTarget;
        public int NumberOfTimes { get; set; }
        public uint QuestId { get; set; }

        public static LocalPlayer Me { get { return StyxWoW.Me; } }

        

        public WoWUnit Mob
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>()
                                       .Where(u => u.Entry == MobId && !u.Dead)
                                       .OrderBy(u => u.Distance).FirstOrDefault();
            }
        }

        public WoWItem Item
        {
            get
            {
                return Me.CarriedItems.FirstOrDefault(i => i.Entry == ItemID && i.Usable && i.Cooldown == 0);
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
                TreeRoot.GoalText = "CombatUseItem";
            }

            if (TreeRoot.Current != null && TreeRoot.Current.Root != null && TreeRoot.Current.Root.LastStatus != RunStatus.Running)
            {
                var currentRoot = TreeRoot.Current.Root;
                if (currentRoot is GroupComposite)
                {
                    var root = (GroupComposite)currentRoot;
                    root.InsertChild(0, RootCompositeOverride());
                }
            }
        }

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(
                    new Decorator(
                        ret => !Me.Combat,
                            new PrioritySelector(
                                new Decorator(
                                    ret => Mob == null,
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Moving to location"),
                                        new Action(ret => Navigator.MoveTo(Location)))),
                                new Decorator(
                                    ret => Me.CurrentTarget == null,
                                    new Action(ret => Mob.Target())),
                                new Decorator(
                                    ret => RoutineManager.Current.PullBehavior != null,
                                    RoutineManager.Current.PullBehavior),
                                new Action(ret => RoutineManager.Current.Pull())))
                ));
        }

        private Composite RootCompositeOverride()
        {
            return
                new PrioritySelector(
                    new Decorator(
                        ret => !_isDone && Me.IsAlive,
                        new PrioritySelector(
                            new Decorator(ret => (Counter >= NumberOfTimes) || (Me.QuestLog.GetQuestById(QuestId) != null && Me.QuestLog.GetQuestById(QuestId).IsCompleted),
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new WaitContinue(120,
                                        new Action(delegate
                                        {
                                            _isDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )),

                            new Decorator(
                                ret => Me.CurrentTarget != null && Item != null && Me.Combat,
                                new PrioritySelector(
                                    new Decorator(
                                        ret => (CastingSpellId != 0 && Me.CurrentTarget.CastingSpellId == CastingSpellId) ||
                                               (NpcHasAura != 0 && Me.CurrentTarget.Auras.Values.Any(a => a.SpellId == NpcHasAura)) ||
                                               (NpcHPLeft != 0 && Me.CurrentTarget.HealthPercent <= NpcHPLeft) ||
                                               (HasAura != 0 && Me.Auras.Values.Any(a => a.SpellId == HasAura)),
                                        new PrioritySelector(
                                            new Decorator(
                                                ret => Me.CurrentTarget.Distance > 4,
                                                new Action(ret => Navigator.MoveTo(Me.CurrentTarget.Location))),
                                            new Sequence(
                                                new Action(ret => Navigator.PlayerMover.MoveStop()),
                                                new Action(ret => StyxWoW.SleepForLagDuration()),
                                                new Action(ret => TreeRoot.StatusText = "Using item"),
                                                new Action(ret => Item.UseContainerItem()),
                                                new DecoratorContinue(
                                                    ret => QuestId == 0,
                                                    new Action(ret => Counter++)))))))

                    )));
        }

        private bool _isDone;
        public override bool IsDone
        {
            get
            {
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
                var result = _isDone && quest != null && quest.IsCompleted || quest == null;
                if (result)
                    _isDone = true;
                return result;
            }
        }

        #endregion
    }
}
