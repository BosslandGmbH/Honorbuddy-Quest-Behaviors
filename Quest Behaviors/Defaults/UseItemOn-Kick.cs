// Behavior originally contributed by Nesox.
//
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_UseItemOn
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;

using CommonBehaviors.Actions;

using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors.UseItemOn2
{
    /// <summary>
    /// Allows you to use items on nearby gameobjects/npc's
    /// ##Syntax##
    /// QuestId: The id of the quest.
    /// MobId1, MobId2, ...MobIdN: The ids of the mobs.
    /// ItemId: The id of the item to use.
    /// [Optional]NumOfTimes: Number of times to use said item.
    /// [Optional]WaitTime: Time to wait after using an item. DefaultValue: 1500 ms
    /// [Optional]CollectionDistance: The distance it will use to collect objects. DefaultValue:100 yards
    /// [Optional]HasAura: If a unit has a certian aura to check before using item. (By: j0achim)
    /// [Optional]Range: The range to object that it will use the item
    /// [Optional]MobState: The state of the npc -> Dead, Alive, BelowHp. None is default
    /// [Optional]MobHpPercentLeft: Will only be used when NpcState is BelowHp
    /// ObjectType: the type of object to interact with, expected value: Npc/Gameobject
    /// [Optional]X,Y,Z: The general location where theese objects can be found
    /// </summary>
    public class UseItemOn : CustomForcedBehavior
    {
        public enum ObjectType
        {
            Npc,
            GameObject,
        }

        public enum NpcState
        {
            Alive,
            BelowHp,
            Dead,
            DontCare,
        }


        public UseItemOn(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                int     tmpMobHasAuraId;
                int     tmpMobHasAuraMissingId;

                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                CollectionDistance = GetAttributeAsInteger("CollectionDistance", false, 1, 10000, null) ?? 100;
                tmpMobHasAuraId   = GetAttributeAsSpellId("HasAuraId", false, new [] { "HasAura" }) ?? 0;
                tmpMobHasAuraMissingId = GetAttributeAsSpellId("IsMissingAuraId", false, null) ?? 0;
                MobHpLeftAmount = GetAttributeAsInteger("MobHpPercentLeft", false, 0, int.MaxValue, new [] { "HpLeftAmount" }) ?? 100;
                HuntingGroundAnchor = GetXYZAttributeAsWoWPoint("", false, null) ?? Me.Location;
                ItemId      = GetAttributeAsItemId("ItemId", true, null) ?? 0;
                MinRange    = GetAttributeAsDouble("MinRange", false, 1.0, 50.0, null) ?? 3.0;
                MobIds      = GetNumberedAttributesAsIntegerArray("MobId", 1, 1, int.MaxValue, new [] { "NpcId" }) ?? new int[0];
                MobType     = GetAttributeAsEnum<ObjectType>("MobType", false, new [] { "ObjectType" }) ?? ObjectType.Npc;
                NumOfTimes  = GetAttributeAsNumOfTimes("NumOfTimes", false, null) ?? 1;
                _NpcState   = GetAttributeAsEnum<NpcState>("MobState", false, new [] { "NpcState" }) ?? NpcState.DontCare;
                Range       = GetAttributeAsDouble("Range", false, 1.0, 50.0, null) ?? 5.0;
                QuestId     = GetAttributeAsQuestId("QuestId", false, null) ?? 0;
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;
                WaitTime    = TimeSpan.FromMilliseconds(GetAttributeAsWaitTime("WaitTime", false, null) ?? 1500);

                MobAuraName = (tmpMobHasAuraId != 0) ? AuraNameFromId("HasAuraId", tmpMobHasAuraId)  : null;
                MobAuraMissingName = (tmpMobHasAuraMissingId != 0) ? AuraNameFromId("HasAuraId", tmpMobHasAuraMissingId)  : null;


                // Semantic coherency...
                ItemInfo    itemInfo    = ItemInfo.FromId((uint)ItemId);

                ItemName = (itemInfo != null)  ? itemInfo.Name  : "unknown";

                if (Item == null)
                {
                    LogMessage("error", "Item ({0}, '{1}') is not in our inventory", ItemId, ItemName);
                    IsAttributeProblem = true;
                }

                if (Range < MinRange)
                {
                    LogMessage("error", "Problem with attribute coherency:  Range >= MinRange (saw '{0}' and '{1}', respectively",
                                            Range, MinRange);
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
				LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
									+ "\nFROM HERE:\n"
									+ except.StackTrace + "\n");
				IsAttributeProblem = true;
			}
        }


        // Attributes provided by caller
        public int                      CollectionDistance { get; private set; }
        public WoWPoint                 HuntingGroundAnchor { get; private set; }
        public int                      ItemId { get; private set; }
        public double                   MinRange { get; private set; }
        public string                   MobAuraName { get; private set; }
        public string                   MobAuraMissingName { get; private set; }
        public int                      MobHpLeftAmount { get; private set; }
        public int[]                    MobIds { get; private set; }
        public ObjectType               MobType { get; private set; }
        public NpcState                 _NpcState { get; private set; }
        public int                      NumOfTimes { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }
        public double                   Range { get; private set; }
        public TimeSpan                 WaitTime { get; private set; }

        // Private variables for internal state
        private WoWObject               _currentTarget;
        private bool                    _isBehaviorDone;
        private readonly List<ulong>    _npcAuraWait = new List<ulong>();
        private readonly List<ulong>    _npcBlacklist = new List<ulong>();
        private Composite               _root;

        // Private properties
        private int                     Counter { get; set; }
        private readonly TimeSpan       Delay_RepopWaitTimer                = TimeSpan.FromMilliseconds(3000);
        private readonly TimeSpan       Delay_TargetConsumed                = TimeSpan.FromMinutes(7);
        private TimeSpan                Delay_WowClientLagDuration { get { return (TimeSpan.FromMilliseconds(StyxWoW.WoWClient.Latency * 2 + 150)); } }
        private readonly TimeSpan       Delay_WowClientMoveCommandThrottle  = TimeSpan.FromMilliseconds(500);
        private string                  ItemName { get; set; }
        private LocalPlayer             Me { get { return (ObjectManager.Me); } }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string      SubversionId { get { return ("$Id: UseItemOn.cs 133 2011-05-24 18:13:37Z chinajade $"); } }
        public override string      SubversionRevision { get { return ("$Revision: 133 $"); } }


        // May return 'null' if auraId is not valid.
        private string      AuraNameFromId(string   attributeName,
                                           int      auraId)
        {
            string  tmpString   = null;

            try 
            { 
                tmpString = WoWSpell.FromId(auraId).Name; 
            }
            catch
            {
                LogMessage("fatal", "Could not find {0}({1}).", attributeName, auraId);
                IsAttributeProblem = true;
            }

            return (tmpString);
        }


        /// <summary> Current object we should interact with.</summary>
        /// <value> The object.</value>
        private IEnumerable<WoWObject>  ViableTargets
        {
            get
            {
                IEnumerable<WoWObject>  wowObjects = null;

                switch (MobType)
                {
                    case ObjectType.GameObject:
                        wowObjects = ObjectManager.GetObjectsOfType<WoWGameObject>()
                                                .OrderBy(ret => ret.Distance)
                                                .Where(obj => !_npcBlacklist.Contains(obj.Guid)
                                                                && obj.Distance < CollectionDistance
                                                                && MobIds.Contains((int)obj.Entry))
                                                .Cast<WoWObject>();
                        break;

                    case ObjectType.Npc:
                        var     baseTargets     = ObjectManager.GetObjectsOfType<WoWUnit>()
                                                               .OrderBy(target => target.Distance)
                                                               .Where(target => !_npcBlacklist.Contains(target.Guid)
                                                                                && (target.Distance < CollectionDistance)
                                                                                && MobIds.Contains((int)target.Entry));

                        var     auraQualifiedTargets    = baseTargets
                                                            .Where(target => (((MobAuraName == null) && (MobAuraMissingName == null))
                                                                              || ((MobAuraName != null) && target.HasAura(MobAuraName))
                                                                              || ((MobAuraMissingName != null) && !target.HasAura(MobAuraMissingName))));

                        var     npcStateQualifiedTargets = auraQualifiedTargets
                                                            .Where(target => ((_NpcState == NpcState.DontCare)
                                                                              || ((_NpcState == NpcState.Dead) && target.Dead)
                                                                              || ((_NpcState == NpcState.Alive) && target.IsAlive)
                                                                              || ((_NpcState == NpcState.BelowHp) && target.IsAlive && (target.HealthPercent < MobHpLeftAmount))));

                        wowObjects = npcStateQualifiedTargets.Cast<WoWObject>();
                        break;
                }

                return (wowObjects);
            }
        }

        public WoWItem Item
        {
            get { return (Me.CarriedItems.FirstOrDefault(ret => ret.Entry == ItemId)); }
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return (_root ?? (_root =
            new PrioritySelector(context => _currentTarget = ViableTargets.FirstOrDefault(target => !target.IsLocallyBlacklisted()),

                // If our goal achieved, bail...
                new Decorator(ret => (Counter >= NumOfTimes),
                    new Action(delegate { _isBehaviorDone = true; })),

                // Sanity check on item...
                new Decorator(ret => (Item == null),
                    new Action(delegate
                    {
                        LogMessage("fatal", "We've no more of Item({0}, '{1}'} to use", ItemId, ItemName);
                    })),

                // If no target available...
                new Decorator(ret => (_currentTarget == null),
                    new PrioritySelector(

                    // If we're not at the hunting ground anchor, move back there to wait...
                    new Decorator(ret => (Me.Location.Distance(HuntingGroundAnchor) > Navigator.PathPrecision),
                            new Sequence(
                                new Action(delegate
                                {
                                    TreeRoot.StatusText = "No more targets--moving to hunting ground anchor location";
                                    Navigator.MoveTo(HuntingGroundAnchor);
                                }),
                                new WaitContinue(Delay_WowClientMoveCommandThrottle, ret => false, new ActionAlwaysSucceed())
                                )),

                    // Otherwise, just wait for repops...
                    new Sequence(
                        new Action(delegate { LogMessage("info", "No targets available--awaiting repops"); }),
                        new WaitContinue(Delay_RepopWaitTimer, ret => false, new ActionAlwaysSucceed())
                        )
                    )),

                // If new target is out-of-range, move to within range...
                new Decorator(ret => (_currentTarget.DistanceSqr > (Range * Range)),
                    new Sequence(
                        new Action(delegate { TreeRoot.StatusText = "Moving to target \"" + _currentTarget.Name + "\""; }),
                        new Action(ret => Navigator.MoveTo(_currentTarget.Location)),
                        new WaitContinue(Delay_WowClientMoveCommandThrottle, ret => false, new ActionAlwaysSucceed())
                        )),

                // If we are too close to selected target, back up...
                new Decorator(ret => (Me.Location.Distance(_currentTarget.Location) < MinRange),
                    new PrioritySelector(
                        // If backing up, make sure we're facing the target...
                        new Decorator(ret => Me.MovementInfo.MovingBackward,
                            new Action(delegate { WoWMovement.Face(_currentTarget.Guid); })),

                        // Start backing up...
                        new Sequence(
                            new Action(delegate
                            {
                                TreeRoot.StatusText = "Too close to \"" + _currentTarget.Name + "\"--backing up";
                                WoWMovement.MoveStop();
                                WoWMovement.Face(_currentTarget.Guid);
                                WoWMovement.Move(WoWMovement.MovementDirection.Backwards);
                            }),

                            new WaitContinue(Delay_WowClientMoveCommandThrottle,
                                             ret => (Me.Location.Distance(_currentTarget.Location) > MinRange),
                                             new ActionAlwaysSucceed())
                            )
                        )),

                // We're between MinRange and Range, stop movement and face the target...
                new Sequence(
                    new Action(delegate
                    {
                        WoWMovement.MoveStop();
                        WoWMovement.Face(_currentTarget.Guid);
                    }),
                    new WaitContinue(Delay_WowClientLagDuration, ret => false, new ActionAlwaysSucceed()),
                    new ActionAlwaysFail()      // fall through to next element
                    ),

                // If Item on cooldown, we need to wait...
                new Decorator(ret => (Item.Cooldown > 0),
                    new Action(delegate { LogMessage("info", "Item({0}, '{1}') on cooldown--waiting to use", ItemId, ItemName); })
                    ),

                // Use the item...
                new Sequence(
                    new Action(delegate
                    {
                        TreeRoot.StatusText = "Using item on \"" + _currentTarget.Name + "\"";
                        if (_currentTarget is WoWUnit)
                            { (_currentTarget as WoWUnit).Target(); }
                    }),
                    new WaitContinue(Delay_WowClientLagDuration, ret => false, new ActionAlwaysSucceed()),
                    new Action(delegate
                    {
                        Item.UseContainerItem();
                        _currentTarget.LocallyBlacklist(Delay_TargetConsumed);
                        ++Counter;
                    }),
                    new WaitContinue(Delay_WowClientLagDuration, ret => false, new ActionAlwaysSucceed()),
                    new WaitContinue(WaitTime, ret => false, new ActionAlwaysSucceed()),
                    new Action(delegate { Me.ClearTarget(); })
                    )
                )));
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

    #region Extensions to HBcore

    // The HBcore 'global' blacklist will also prevent looting.  We don't want that.
    // Since the HBcore blacklist is not built to instantiate, we have to roll our
    // own.  <sigh>
    public class LocalBlackList
    {
        public LocalBlackList(TimeSpan  maxSweepTime)
        {
            _maxSweepTime = maxSweepTime;
            _stopWatchForSweeping.Start();
        }

        private Dictionary<ulong, DateTime>     _blackList              = new Dictionary<ulong, DateTime>();
        private TimeSpan                        _maxSweepTime;
        private Stopwatch                       _stopWatchForSweeping   = new Stopwatch();      


        public void     Add(ulong guid,   TimeSpan timeSpan)
        {
            if (_stopWatchForSweeping.Elapsed > _maxSweepTime)
                { RemoveExpired(); }

            _blackList[guid] = DateTime.Now.Add(timeSpan);
        }


        public bool     Contains(ulong  guid)
        {
            if (_stopWatchForSweeping.Elapsed > _maxSweepTime)
                { RemoveExpired(); }

            return (_blackList.ContainsKey(guid));
        }


        public void     RemoveExpired()
        {
            DateTime    now     = DateTime.Now;

            List<ulong> expiredEntries  = (from key in _blackList.Keys
                                            where (_blackList[key] < now)
                                            select key).ToList();

            foreach (ulong entry in expiredEntries)
                { _blackList.Remove(entry); }

            _stopWatchForSweeping.Reset();
            _stopWatchForSweeping.Start();
        }
    }


    public static class WoWObject_Extensions
    {
        private static LocalPlayer      Me { get { return (ObjectManager.Me); } }

        // We provide our own 'local' blacklist.  If we use the global one maintained by HBcore,
        // that will prevent us from looting also.
        private static LocalBlackList   _blackList  = new LocalBlackList(TimeSpan.FromSeconds(30));

        public static void      LocallyBlacklist(this WoWObject    wowObject,
                                                 TimeSpan          timeSpan)
        {
            _blackList.Add(wowObject.Guid, timeSpan);
        }


        public static bool      IsLocallyBlacklisted(this WoWObject    wowObject)
        {
            return (_blackList.Contains(wowObject.Guid));
        }
    }

    #endregion      // Extensions to HBcore
}