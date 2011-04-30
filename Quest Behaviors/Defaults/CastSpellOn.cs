// Behavior originally contributed by Natfoth.
//
// WIKI DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_CastSpellOn
//
// QUICK DOX:
//      Allows you to cast a specific spell on a target.  Useful for training dummies and starting quests.
//
//  Parameters (required, then optional--both listed alphabetically):
//      MobId: Id of the target (NPC or Object) on which the spell should be cast.
//      SpellId: Spell that should be used to cast on the target
//      X,Y,Z: The general location where the targets can be found.
//
//      HpLeftAmount [Default:110 hitpoints]: How low the hitpoints on the target should be before attempting
//              to cast a spell on the target.   Note this is an absolute value, and not a percentage.
//      MinRange [Default:3 yards]: minimum distance from the target at which a cast attempt shoudl be made.
//      NumOfTimes [Default:1]: The number of times to perform th casting action on viable targets in the area.
//      QuestId [Default:none]:
//      QuestCompleteRequirement [Default:NotComplete]:
//      QuestInLogRequirement [Default:InLog]:
//              A full discussion of how the Quest* attributes operate is described in
//              http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
//      Range [Default:25 yards]: maximum distance from the target at which a cast attempt should be made.
//
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
    public class CastSpellOn : CustomForcedBehavior
    {
        public CastSpellOn(Dictionary<string, string> args)
            : base(args)
        {
			try
			{
                HpLeftAmount = GetAttributeAsInteger("HpLeftAmount", false, 0, int.MaxValue, null) ?? 110;
                Location    = GetXYZAttributeAsWoWPoint("", true, null) ?? WoWPoint.Empty;
                MinRange    = GetAttributeAsRange("MinRange", false, null) ?? 3;
                MobId       = GetAttributeAsMobId("MobId", true, new [] { "NpcId" }) ?? 0;
                NumOfTimes  = GetAttributeAsNumOfTimes("NumOfTimes", false, null) ?? 1;
                QuestId     = GetAttributeAsQuestId("QuestId", false, null) ?? 0;
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;
                Range       = GetAttributeAsRange("Range", false, null) ?? 25;
                SpellId     = GetAttributeAsSpellId("SpellId", true, null) ?? 0;

                Counter     = 1;
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
        public int                      HpLeftAmount { get; private set; }
        public WoWPoint                 Location { get; private set; }
        public int                      MinRange { get; private set; }
        public int                      MobId { get; private set; }
        public int                      NumOfTimes { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }
        public int                      Range { get; private set; }
        public int                      SpellId { get; private set; }

        // Private variables for internal state
        private bool                _isBehaviorDone;
        private Composite           _root;

        // Private properties
        private int                 Counter { get; set; }
        private LocalPlayer         Me { get { return (ObjectManager.Me); } }
        public List<WoWUnit>        MobList { get { if (HpLeftAmount > 0)
                                                    {
                                                        return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                                                             .Where(u => u.Entry == MobId && !u.Dead && u.HealthPercent <= HpLeftAmount)
                                                                             .OrderBy(u => u.Distance).ToList());
                                                    }
                                                    else
                                                    {
                                                        return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                                                             .Where(u => u.Entry == MobId && !u.Dead)
                                                                             .OrderBy(u => u.Distance).ToList());
                                                    }
                                                }}


        #region Overrides of CustomForcedBehavior

        private Composite CreateRootBehavior()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => !IsDone && StyxWoW.Me.IsAlive,
                    new PrioritySelector(
                        new Decorator(ret => Counter > NumOfTimes && QuestId == 0,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Finished!"),
                                            new WaitContinue(120,
                                                new Action(delegate
                                                {
                                                    _isBehaviorDone = true;
                                                    return RunStatus.Success;
                                                }))
                                            )),

                        new Decorator(ret => MobList.Count > 0 && !Me.IsCasting && SpellManager.CanCast(SpellId),
                            new Sequence(
                                new DecoratorContinue(ret => MobList[0].Location.Distance(Me.Location) >= MinRange && MobList[0].Location.Distance(Me.Location) <= Range && MobList[0].InLineOfSightOCD,
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Casting Spell - " + SpellId + " On Mob: " + MobList[0].Name + " Yards Away " + MobList[0].Location.Distance(Me.Location)),
                                        new Action(ret => WoWMovement.MoveStop()),
                                        new Action(ret => Thread.Sleep(300)),
                                        new Decorator(c => !Me.IsCasting, CreateSpellBehavior)
                                        )
                                ),
                                new DecoratorContinue(ret => MobList[0].Location.Distance(Me.Location) > Range || !MobList[0].InLineOfSightOCD,
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Moving To Mob - " + MobList[0].Name + " Yards Away: " + MobList[0].Location.Distance(Me.Location)),
                                        new Action(ret => Navigator.MoveTo(MobList[0].Location))
                                        )
                                ),

                                new DecoratorContinue(ret => MobList[0].Location.Distance(Me.Location) < MinRange,
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Too Close, Backing Up"),
                                        new Action(ret => MobList[0].Face()),
                                        new Action(ret => Thread.Sleep(100)),
                                        new Action(ret => WoWMovement.Move(WoWMovement.MovementDirection.Backwards)),
                                        new Action(ret => Thread.Sleep(2000)),
                                        new Action(ret => WoWMovement.MoveStop(WoWMovement.MovementDirection.Backwards))
                                        ))
                                ))
                )));
        }

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(
                    
                        new Decorator(ret => MobList.Count == 0,
                            new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Moving To Location - X: " + Location.X + " Y: " + Location.Y),
                                    new Action(ret => Navigator.MoveTo(Location)),
                                    new Action(ret => Thread.Sleep(300))
                                )
                            )
                            
                        )
                    );
        }


        Composite CreateSpellBehavior
        {
            get
            {
                return new Action(c =>
                {
                    if (SpellId > 0)
                    {
                        MobList[0].Target();
                        MobList[0].Face();
                        Thread.Sleep(300);
                        SpellManager.Cast(SpellId);

                        if (Me.QuestLog.GetQuestById((uint)QuestId) == null || QuestId == 0)
                        {
                            Counter++;
                        }
                        Thread.Sleep(300);
                        return RunStatus.Success;
                    }
                    else
                    {
                        _isBehaviorDone = true;
                        return RunStatus.Success;
                    }
                });
            }
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
                if (TreeRoot.Current != null && TreeRoot.Current.Root != null && TreeRoot.Current.Root.LastStatus != RunStatus.Running)
                {
                    var currentRoot = TreeRoot.Current.Root;
                    if (currentRoot is GroupComposite)
                    {
                        var root = (GroupComposite)currentRoot;
                        root.InsertChild(0, CreateRootBehavior());
                    }
                }


                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = this.GetType().Name + ": " + ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
            }
        }

        #endregion
    }
}
