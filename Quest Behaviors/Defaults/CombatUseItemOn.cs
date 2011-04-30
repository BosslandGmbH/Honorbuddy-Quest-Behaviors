// Behavior originally contributed by Raphus.
//
// WIKI DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_CombatUseItemOn
//
// QUICK DOX:
//      Uses an item on a target while the toon is in combat.   The caller can determine at what point
//      in combat the item will be used:
//          * When the target's health drops below a certain percentage
//          * When the target is casting a particular spell
//          * When the target gains a particular aura
//          * When the toon gains a particular aura
//          * one or more of the above happens
//
//  Parameters (required, then optional--both listed alphabetically):
//      ItemId: Id of the item to use on the targets.
//      MobId: Id of the targets on which to use the item.
//      X, Y, Z: world-coordinates of the general location where the targets can be found.
//
//      CastingSpellId [Default:none]: waits for the target to be casting this spell before using the item.
//      HasAuraId [Default:none]: waits for the toon to acquire this aura before using the item.
//      MobHasAuraId [Default:none]: waits for the target to acquire this aura before using the item.
//      MobHpPercentLeft [Default:0 percent]: waits for the target's hitpoints to fall below this percentage
//              before using the item.
//
//      NumOfTimes [Default:1]: number of times to use the item on a viable target
//      QuestId [Default:none]:
//      QuestCompleteRequirement [Default:NotComplete]:
//      QuestInLogRequirement [Default:InLog]:
//              A full discussion of how the Quest* attributes operate is described in
//              http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
//
//  Notes:
//      * One or more of CastingSpellId, HasAuraId, MobHasAuraId, or MobHpPercentLeft must be specified.
//
using System;
using System.Collections.Generic;
using System.Linq;

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
    public class CombatUseItemOn : CustomForcedBehavior
    {
        public CombatUseItemOn(Dictionary<string, string> args)
            : base(args)
        {

			try
			{
                CastingSpellId = GetAttributeAsSpellId("CastingSpellId", false, null) ?? 0;
                HasAuraId   = GetAttributeAsSpellId("HasAuraId", false, new [] { "HasAura" }) ?? 0;
                ItemId      = GetAttributeAsItemId("ItemId", true, null) ?? 0;
                Location    = GetXYZAttributeAsWoWPoint("", true, null) ?? WoWPoint.Empty;
                MobId       = GetAttributeAsMobId("MobId", true, new [] { "NpcId" }) ?? 0;
                NpcHasAuraId = GetAttributeAsSpellId("MobHasAuraId", false, new [] { "NpcHasAuraId", "NpcHasAura" }) ?? 0;
                NpcHpPercentLeft = GetAttributeAsInteger("MobHpPercentLeft", false, 0, int.MaxValue, new [] { "NpcHpLeft", "NpcHPLeft" }) ?? 0;
                NumOfTimes  = GetAttributeAsNumOfTimes("NumOfTimes", false, null) ?? 1;
                QuestId     = GetAttributeAsQuestId("QuestId", false, null) ?? 0;
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;

                // semantic coherency checks --
                if ((CastingSpellId == 0) && (HasAuraId == 0) && (NpcHasAuraId == 0) && (NpcHpPercentLeft == 0))
                {
                    UtilLogMessage("error", "One or more of the following attributes must be specified:\n"
                                            + "CastingSpellId, HasAuraId, MobHasAuraId, MobHpPercentLeft");
                    IsAttributeProblem = true;
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
				IsAttributeProblem = true;
			}
        }


        // Attributes provided by caller
        public int                      CastingSpellId { get; private set; }
        public int                      HasAuraId { get; private set; }
        public int                      ItemId { get; private set; }
        public WoWPoint                 Location { get; private set; }
        public int                      MobId { get; private set; }
        public int                      NpcHasAuraId { get; private set; }
        public int                      NpcHpPercentLeft { get; private set; }
        public int                      NumOfTimes { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }

        // Private variables for internal state
        private bool                _isBehaviorDone;
        private Composite           _root;

        // Private properties
        private int                 Counter { get; set; }
        public WoWItem              Item { get { return Me.CarriedItems.FirstOrDefault(i => i.Entry == ItemId && i.Cooldown == 0); } }
        private LocalPlayer         Me { get { return (ObjectManager.Me); } }
        public WoWUnit              Mob { get { return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                                                     .Where(u => u.Entry == MobId && !u.Dead)
                                                                     .OrderBy(u => u.Distance).FirstOrDefault());
                                        }}


        #region Overrides of CustomForcedBehavior

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
                                new Action(ret => RoutineManager.Current.Pull()))),
                    RootCompositeOverride()
                ));
        }

        private Composite RootCompositeOverride()
        {
            return
                new PrioritySelector(
                    new Decorator(
                        ret => !_isBehaviorDone && Me.IsAlive,
                        new PrioritySelector(
                            new Decorator(ret => (Counter >= NumOfTimes) || (Me.QuestLog.GetQuestById((uint)QuestId) != null && Me.QuestLog.GetQuestById((uint)QuestId).IsCompleted),
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new WaitContinue(120,
                                        new Action(delegate
                                        {
                                            _isBehaviorDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )),

                            new Decorator(
                                ret => Me.CurrentTarget != null && Item != null && Me.Combat,
                                new PrioritySelector(
                                    new Decorator(
                                        ret => (CastingSpellId != 0 && Me.CurrentTarget.CastingSpellId == CastingSpellId) ||
                                               (NpcHasAuraId != 0 && Me.CurrentTarget.Auras.Values.Any(a => a.SpellId == NpcHasAuraId)) ||
                                               (NpcHpPercentLeft != 0 && Me.CurrentTarget.HealthPercent <= NpcHpPercentLeft) ||
                                               (HasAuraId != 0 && Me.Auras.Values.Any(a => a.SpellId == HasAuraId)),
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


        public override bool IsDone
        {
            get
            {
                bool    isDone  = (_isBehaviorDone     // normal completion
                                   || !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));

                if (isDone)
                    { _isBehaviorDone = true; }

                return (isDone);
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
                if (TreeRoot.Current != null && TreeRoot.Current.Root != null && TreeRoot.Current.Root.LastStatus != RunStatus.Running)
                {
                    var currentRoot = TreeRoot.Current.Root;
                    if (currentRoot is GroupComposite)
                    {
                        var root = (GroupComposite)currentRoot;
                        root.InsertChild(0, RootCompositeOverride());
                    }
                }


                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = this.GetType().Name + ": " + ((quest != null) ? quest.Name : "In Progress");
            }
        }

        #endregion
    }
}
