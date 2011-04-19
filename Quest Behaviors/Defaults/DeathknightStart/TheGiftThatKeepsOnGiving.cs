using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// TheGiftThatKeepsOnGiving/UseItemOn by Nesox, rewriten by Highvoltz/re-rewritten by Nesox
    /// Allows you to use items on nearby gameobjects/npc's
    /// ##Syntax##
    /// [Optional]QuestId: The id of the quest.
    /// MobId The id of the object.
    /// [Optional]MobId2: The id of a 2nd object.
    /// [Optional]MobId3: The id of a 3rd object.
    /// ItemId: The id of the item to use.
    /// [Optional]NumOfTimes: Number of times to use said item.
    /// [Optional]CollectionDistance: The distance it will use to collect objects. DefaultValue:10000 yards( some NPCs can be view further then 100 yards)
    /// [Optional]HasAura: If a unit has a certian aura to check before using item. (By: j0achim)
    /// [Optional]StopMovingOnUse: (true/false) stops moving when using item. Default:true (By:HighVoltz)
    /// [Optional]HasGroundTarget: (true/false) true if you need to click the ground to cast spell in that area(Default: false)(By:HighVoltz)
    /// [Optional]IsDead: (true/false) true item is to be used on dead targets (Default: false)(By:HighVoltz)
    /// [Optional]InteractRange: The distance from the Object/NPC to use the item. Default: 4.5(By:HighVoltz)
    /// [Optional]MinionCount: Number of minions to gather. Used for the quest "The Gift That Keeps On Giving" Default: 0(By:HighVoltz)
    /// [Optional] X,Y,Z: The general location where theese objects can be found
    /// </summary>
    public class TheGiftThatKeepsOnGiving : CustomForcedBehavior
    {
        public TheGiftThatKeepsOnGiving(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                int?    tmpHasAuraId   = GetAttributeAsSpellId("HasAuraId", false, new [] { "HasAura" });

                AuraName    = "";       // populated below
                CollectionDistance  = GetAttributeAsInteger("CollectionDistance", false, 1, 100000, null) ?? 0;
                HasGroundTarget = GetAttributeAsBoolean("HasGroundTarget", false, null) ?? false;
                InteractRange   = GetAttributeAsDouble("InteractRange", false, 1.0, 100.0, null) ?? 4.5;
                IsDead      = GetAttributeAsBoolean("IsDead", false, null) ?? false;
                ItemId      = GetAttributeAsItemId("ItemId", true, null) ?? 0;
                Location    = GetXYZAttributeAsWoWPoint("", true, null) ?? WoWPoint.Empty;
                MinionCount = GetAttributeAsInteger("MinionCount", false, 0, int.MaxValue, null) ?? 0;
                MobId       = GetAttributeAsMobId("MobId", true, null) ?? 0;
                MobId2      = GetAttributeAsMobId("MobId2", false, null) ?? 0;
                MobId3      = GetAttributeAsMobId("MobId3", false, null) ?? 0;
                NumOfTimes  = GetAttributeAsInteger("NumOfTimes", false, 1, 1000, null) ?? 1;
                QuestId     = GetAttributeAsQuestId("QuestId", false, null) ?? 0;
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;
                StopMovingOnUse = GetAttributeAsBoolean("StopMovingOnUse", false, null) ?? true;

                if (tmpHasAuraId.HasValue)
                {
                    WoWSpell    spell = WoWSpell.FromId(tmpHasAuraId.Value);

                    if (spell != null)
                        { AuraName = spell.Name; }
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
        public string                   AuraName { get; private set; }
        public int                      CollectionDistance { get; private set; }
        public bool                     HasGroundTarget { get; private set; }
        public double                   InteractRange { get; private set; }
        public bool                     IsDead { get; private set; }
        public int                      ItemId { get; private set; }
        public WoWPoint                 Location { get; private set; }
        public int                      MinionCount { get; private set; }
        public int                      MobId { get; private set; }
        public int                      MobId2 { get; private set; }
        public int                      MobId3 { get; private set; }
        public int                      NumOfTimes { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }
        public bool                     StopMovingOnUse { get; private set; }

        // Private variables for internal state
        private readonly List<ulong>    _npcBlacklist = new List<ulong>();
        private Composite               _root;
        private readonly Stopwatch      _waitTimer = new Stopwatch();

        // Private properties
        private int                     Counter { get; set; }
        private LocalPlayer             Me { get { return (ObjectManager.Me); } }


        public WoWObject Object
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWObject>(true).Where(
                    o => ObjCheck(o, MobId) || (MobId2 > 0 && ObjCheck(o, MobId2)) || (MobId3 > 0 && ObjCheck(o, MobId3))).OrderBy(o => 
                        o.Distance).FirstOrDefault();
            }
        }


        bool ObjCheck(WoWObject obj, int id)
        {
            bool ret = false;
            if (obj.Entry == id && obj.Distance <= CollectionDistance &&
                !_npcBlacklist.Contains(obj.Guid) && AuraCheck(obj))
            {
                ret = (!IsDead || !(obj is WoWUnit) || ((WoWUnit) obj).Dead) &&
                      (IsDead || !(obj is WoWUnit) || !((WoWUnit) obj).Dead);
            }
            // temp fix to HB killing targets without letting us using item...
            if (ret && obj is WoWUnit)
                Blacklist.Add(obj, new System.TimeSpan(0, 10, 0));
            return ret;
        }


        bool AuraCheck(WoWObject obj)
        {
            if (string.IsNullOrEmpty(AuraName) || !(obj is WoWUnit))
                return true;
            if (((WoWUnit)obj).HasAura(AuraName))
                return true;
            return false;
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = 

                new PrioritySelector(ctx => Object,

                    new Decorator(ctx => ctx != null && (((WoWObject)ctx).Distance > InteractRange || !((WoWObject)ctx).InLineOfSightOCD),
                        new Sequence(
                            new Action(ctx => TreeRoot.StatusText = "Moving to use item on - " + ((WoWObject)ctx).Name),
                            new Action(ctx => Navigator.MoveTo(((WoWObject)ctx).Location)))),

                    new Decorator(ctx => ctx != null && ((WoWObject)ctx).Distance <= InteractRange,
                        new Sequence(
                            new DecoratorContinue(c => StopMovingOnUse && Me.IsMoving,
                                new Sequence(
                                    new Action(ctx => WoWMovement.MoveStop()),
                                    new WaitContinue(5, ctx => !Me.IsMoving,
                                        new Action(ctx => StyxWoW.SleepForLagDuration()))
                                    )),

                            new Sequence(ctx => StyxWoW.Me.CarriedItems.FirstOrDefault(ret => ret.Entry == ItemId),
                                // Set the status text.
                                new Action(ctx => TreeRoot.StatusText = "Using item on " + Object.Name),
                                
                                // If we don't have the item stop!
                                new DecoratorContinue(ctx => ctx == null,
                                    new Action(ctx => UtilLogMessage("fatal", string.Format("Could not find ItemId({0}) in inventory.", ItemId)))),

                                new DecoratorContinue(ctx => Object.Type == WoWObjectType.Unit,
                                    new Action(ctx => Object.ToUnit().Target())),
                                
                                // Face the object.
                                new Action(ctx => WoWMovement.Face(Object.Guid)),

                                // Use the item.
                                new Action(ctx => ((WoWItem)ctx).UseContainerItem()),

                                new DecoratorContinue(ctx => HasGroundTarget,
                                    new Action(ctx => LegacySpellManager.ClickRemoteLocation(Object.Location))),

                                new WaitContinue(6, ctx => false, 
                                    new Sequence(
                                        new Action(ctx => StyxWoW.SleepForLagDuration()),
                                        new Action(ctx => _npcBlacklist.Add(Object.Guid)),
                                        new Action(ctx => _waitTimer.Reset()),

                                        new DecoratorContinue(ctx => !_waitTimer.IsRunning,
                                            new Action(ctx => _waitTimer.Start())),

                                        new Action(ctx => Counter++)
                                        )
                                    )
                                )
                        )),

                    new Sequence(
                        new Action(ctx => UtilLogMessage("info", string.Format("Moving to {0}", Location))),
                        new Action(ctx => Navigator.MoveTo(Location))
                        )
                 ));
        }


        public override bool IsDone
        {
            get
            {
                return ((Counter >= NumOfTimes)     // normal completion
                        || (MinionCount > 0  &&  (MinionCount <= Me.Minions.Count))
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


    static class WoWUnitExtentions
    {
        public static bool IsTargetingMinion(this WoWUnit unit)
        {
            if (unit.GotTarget)
            {
                foreach (var minion in ObjectManager.Me.Minions)
                {
                    if (unit.CurrentTarget == minion)
                        return true;
                }
            }
            return false;
        }
    }
}
