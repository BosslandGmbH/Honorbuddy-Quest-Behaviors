using System;
using System.Collections.Generic;
using System.Linq;

using Styx.Helpers;
using Styx.Logic;
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
        public FindAndBeatNpcs(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                HealthPercent   = GetAttributeAsInteger("HealthPercent", false, 0, 99, null) ?? 25;
                Location    = GetXYZAttributeAsWoWPoint("", true, null) ?? WoWPoint.Empty;
                MobId       = GetAttributeAsMobId("MobId", true, null) ?? 0;
                MobId2      = GetAttributeAsMobId("MobId2", false, null) ?? 0;
                MobId3      = GetAttributeAsMobId("MobId3", false, null) ?? 0;
                QuestId     = GetAttributeAsQuestId("QuestId", false, null) ?? 0;
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;
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
				IsAttributeProblem = true;
			}
        }


        // Attributes provided by caller
        public int                      HealthPercent { get; private set; }
        public WoWPoint                 Location { get; private set; }
        public int                      MobId { get; private set; }
        public int                      MobId2 { get; private set; }
        public int                      MobId3 { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }

        // Private variables for internal state
        private bool            _isBehaviorDone;
        private Composite       _root;

        // Private properties
        private LocalPlayer     Me { get { return (ObjectManager.Me); } }
        public WoWUnit          Npc
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>(true)
                                     .OrderBy(o => o.Distance)
                                     .FirstOrDefault(o => !o.Dead
                                                     && !Blacklist.Contains(o.Guid)
                                                     && (!o.GotTarget|| o.IsTargetingMeOrPet)
                                                     && ((o.Entry == MobId)
                                                         || (MobId2 > 0 && o.Entry == MobId2)
                                                         || (MobId3 > 0 && o.Entry == MobId3))));
            }
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ??
                (_root = new PrioritySelector(
                    new Decorator(c => Npc != null,
                        new Action(c =>
                        {
                            if (!Npc.Attackable)
                                Styx.Logic.Blacklist.Add(Npc.Guid,new System.TimeSpan(0,5,0));
                            if ((Me.Combat && (Me.GotTarget && Me.CurrentTarget != Npc && 
                                (Me.CurrentTarget.Entry != MobId || Me.CurrentTarget.Entry != MobId2 || Me.CurrentTarget.Entry != MobId3 ))
                                || Me.HealthPercent < HealthPercent) || IsDone)
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
                                    if (Me.GotTarget)
                                        Blacklist.Add(Me.CurrentTarget,new System.TimeSpan(1,0,0));
                                    Me.ClearTarget();
                                }
                                return RunStatus.Running;
                            }
                            if (!Me.GotTarget || Me.CurrentTarget != Npc)
                                Npc.Target();
                            if (!Npc.WithinInteractRange)
                            {
                                TreeRoot.GoalText = string.Format("Moving to {0}", Npc.Name);
                                Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(Me.Location, Npc.Location, 3));
                            }
                            else
                            {
                                if (Me.IsMoving)
                                    WoWMovement.MoveStop();
                                if (!Me.IsAutoAttacking || !Me.IsSafelyFacing(ObjectManager.Me.CurrentTarget))
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


        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone     // normal completion
                        || !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
            }
        }


        public override void OnStart()
        {
            // This reports problems, and stops BT processing if there was a problem with attributes...
            // We had to defer this action, as the 'profile line number' is not available during the element's
            // constructor call.
            OnStart_HandleAttributeProblem();

            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (!IsDone)
            {
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

               TreeRoot.GoalText = this.GetType().Name + ": " + ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
            }
        }
        
         #endregion
    }
}
