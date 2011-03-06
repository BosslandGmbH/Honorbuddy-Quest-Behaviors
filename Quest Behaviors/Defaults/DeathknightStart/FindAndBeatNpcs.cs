using System.Collections.Generic;
using System.Linq;
using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using System.Diagnostics;
using Styx.Logic.Combat;
using Styx.Logic;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// FindAndBeatNpcs by HighVoltz
    /// Finds Npc's that match MobId,MobId2 or MobId3 and beats them up using AutoAttack. useful for a dk quest
    /// ##Syntax##
    /// MobId: Id of the NPC
    /// MobId2:(Optional) Id of a NPC
    /// MobId3:(Optional) Id of a NPC
    /// QuestId:Id of the quest to perform this Behavior on. This behavior will finish is the quest is complete. 
    /// HealthPercent(Optional) Health Present to stop autoattack at and let CC take over: Default: 25
    /// X,Y,Z: The location where you want to move to
    /// </summary>
    /// 
    public class FindAndBeatNpcs : CustomForcedBehavior
    {
        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {
            {"MobId",null},
            {"MobId2",null},
            {"MobId3",null},
            {"HealthPercent",null},
            {"QuestId",null},
            {"X",null},
            {"Y",null},
            {"Z",null},
        };
        bool success = true;
        public FindAndBeatNpcs(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);
            int mobID = 0;
            int mobID2 = 0;
            int mobID3 = 0;
            int health = 0;
            int questId = 0;
            WoWPoint point = WoWPoint.Empty;

            success = success && GetAttributeAsInteger("MobId", true, "0", 0, int.MaxValue, out mobID);
            success = success && GetAttributeAsInteger("MobId2", false, "0", 0, int.MaxValue, out mobID2);
            success = success && GetAttributeAsInteger("MobId3", false, "0", 0, int.MaxValue, out mobID3);
            success = success && GetAttributeAsInteger("QuestId", true, "0", 0, int.MaxValue, out questId);
            success = success && GetAttributeAsInteger("HealthPercent", false, "25", 0, int.MaxValue, out health);
            success = success && GetXYZAttributeAsWoWPoint("X", "Y", "Z", true, WoWPoint.Empty, out point);

            MobId = mobID;
            MobId2 = mobID2;
            MobId3 = mobID3;
            QuestId = questId;
            HealthPercent = health;
            Location = point;
        }

        public int MobId { get; private set; }
        public int MobId2 { get; private set; }
        public int MobId3 { get; private set; }
        public int QuestId { get; private set; }
        public int HealthPercent { get; private set; }
        public WoWPoint Location { get; private set; }
        LocalPlayer me = ObjectManager.Me;
        public WoWUnit Npc
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>(true).OrderBy(o => o.Distance)
                    .FirstOrDefault(o => !o.Dead && !Blacklist.Contains(o.Guid) && 
                        (!o.GotTarget|| o.IsTargetingMeOrPet) && ((o.Entry == MobId) ||
                    (MobId2 > 0 && o.Entry == MobId2) || (MobId3 > 0 && o.Entry == MobId3)));
            }
        }

        #region Overrides of CustomForcedBehavior
        private Composite root;
        protected override Composite CreateBehavior()
        {
            return root ??
                (root = new PrioritySelector(
                    new Decorator(c => Npc != null,
                        new Action(c =>
                        {
                            if (!Npc.Attackable)
                                Styx.Logic.Blacklist.Add(Npc.Guid,new System.TimeSpan(0,5,0));
                            if ((me.Combat && (me.GotTarget && me.CurrentTarget != Npc && 
                                (me.CurrentTarget.Entry != MobId || me.CurrentTarget.Entry != MobId2 || me.CurrentTarget.Entry != MobId3 ))
                                || me.HealthPercent < HealthPercent) || IsDone)
                            {
                                return RunStatus.Success;
                            }
                            WoWUnit lootable = ObjectManager.GetObjectsOfType<WoWUnit>().OrderBy(u => u.Distance).
                                FirstOrDefault(u => u.Lootable);
                            if (lootable != null)
                            {
                                if (!lootable.WithinInteractRange)
                                {
                                    if (Npc.Distance < 30 && SpellManager.CanCast("Death Grip"))
                                    {
                                        SpellManager.Cast("Death Grip");
                                        WoWMovement.MoveStop();
                                    }
                                    else
                                        Navigator.MoveTo(lootable.Location);
                                }
                                else
                                    lootable.Interact();
                                if (Styx.Logic.Inventory.Frames.LootFrame.LootFrame.Instance != null &&
                                    Styx.Logic.Inventory.Frames.LootFrame.LootFrame.Instance.IsVisible)
                                {
                                    Styx.Logic.Inventory.Frames.LootFrame.LootFrame.Instance.LootAll();
                                    if (me.GotTarget)
                                        Blacklist.Add(me.CurrentTarget,new System.TimeSpan(1,0,0));
                                    me.ClearTarget();
                                }
                                return RunStatus.Running;
                            }
                            if (!me.GotTarget || me.CurrentTarget != Npc)
                                Npc.Target();
                            if (!Npc.WithinInteractRange)
                            {
                                TreeRoot.GoalText = string.Format("Moving to {0}", Npc.Name);
                                Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(me.Location, Npc.Location, 3));
                            }
                            else
                            {
                                if (me.IsMoving)
                                    WoWMovement.MoveStop();
                                if (!me.IsAutoAttacking || !me.IsSafelyFacing(ObjectManager.Me.CurrentTarget))
                                {
                                    TreeRoot.GoalText = string.Format("Bullying {0}", Npc.Name);
                                    Npc.Interact();
                                }
                            }
                            return RunStatus.Running;
                        })),
                    new Action(c => { Navigator.MoveTo(Location); })
                ));
        }

        void Err(string format, params object[] args)
        {
            Logging.Write(System.Drawing.Color.Red, "FindAndBeatNpcs: " + format, args);
            TreeRoot.Stop();
        }

        void Log(string format, params object[] args)
        {
            Logging.Write("FindAndBeatNpcs: " + format, args);
        }

        private bool isDone = false;
        public override bool IsDone
        {
            get
            {
                var quest = ObjectManager.Me.QuestLog.GetQuestById((uint)QuestId);
                return isDone || (QuestId > 0 && ((quest != null && quest.IsCompleted) || quest == null));
            }
        }
        public override void OnStart()
        {
            TreeRoot.GoalText = string.Format("Moving to {0} to beatup NPC's that match the following IDs {1}, {2}, {3}",
                Location,MobId,MobId2,MobId3);
        }
        
         #endregion
    }
}
