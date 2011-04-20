// Behavior originally contributed by Bobby53.
//
// DOCUMENTATION:
//     
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors.MountHyjal
{
    /// <summary>
    /// Allows safely completing the http://www.wowhead.com/quest=25464 .  Can also be used
    /// on similar quest if one discovered.
    /// 
    /// Moves to XYZ
    /// Locates MobId
    /// If MobId has AuraId, run to XYZ
    /// Otherwise run to MobId and use ItemId
    /// At end, waits for Living Bomb before continuing
    /// 
    /// Note: to minimize damage, it will cast ItemId for a max of 5 seconds 
    /// then run to xyz and wait even if no aura is present.  the duration betwen
    /// aoe casts (aura present) varies and waiting for it to appear before
    /// running out results in a very weak toon (and possible death from living bomb)
    /// 
    /// ##Syntax##
    /// QuestId: The id of the quest.
    /// [Optional] MobId: The id of the object.
    /// [Optional] ItemId: The id of the item to use.
    /// [Optional] AuraId: Spell id of the aura on MobId that signals we should run
    /// [Optional] CollectionDistance: distance at xyz to search for MobId
    /// [Optional] Range: Distance to use item at
    /// X,Y,Z: safe point (location we run to when target has auraid) must be in LoS of MobId
    /// </summary>
    public class BaronGeddon : CustomForcedBehavior
    {
        public BaronGeddon(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                AuraId      = GetAttributeAsSpellId("AuraId", false, null) ?? 74813;        // should be 74813 - Inferno - http://www.wowhead.com/spell=74813
                CollectionDistance  = GetAttributeAsRange("CollectionDistance", false, null) ?? 100;    // dist from point to search for mob
                ItemId      = GetAttributeAsItemId("ItemId", false, null) ?? 54463;         // should be 54463 - Flameseer's Staff
                Location    = GetXYZAttributeAsWoWPoint("", true, null) ?? WoWPoint.Empty;  // point to start at/run to when mob has AuraId
                                                                                            // ...also used as center point for mob search area
                MobId       = GetAttributeAsMobId("MobId", false, null) ?? 40147;           //  should be 40147 - Baron Geddon
                QuestId     = GetAttributeAsQuestId("QuestId", true, null) ?? 0;            // should be 25464 for http://www.wowhead.com/quest=25464
                /* */         GetAttributeAsString_NonEmpty("QuestName", false, null);      // (doc only - not used)
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;
                Range       = GetAttributeAsRange("Range", false, null) ?? 18;              // should be 18 or less (see http://www.wowhead.com/spell=75192)
                                                                                            // note: wowhead says 10, but actual testing shows 18+ which decreases damage taken

                _bombWait = new Stopwatch();
                _castTime = new Stopwatch();
                _isBehaviorDone = IsQuestComplete();
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
        public int                      AuraId { get; private set; }
        public int                      CollectionDistance { get; private set; }
        public int                      ItemId { get; private set; }
        public WoWPoint                 Location { get; private set; }
        public int                      MobId { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }
        public int                      Range { get; private set; }

        // Private variables for internal state
        private Stopwatch               _bombWait;
        private Stopwatch               _castTime;
        private bool                    _isBehaviorDone;
        private bool                    _moveToCoordYet;
        private readonly List<ulong>    _npcBlacklist = new List<ulong>();
        private Composite               _root;

        // Private properties
        private WoWItem                 Item { get { return (StyxWoW.Me.CarriedItems.FirstOrDefault(ret => ret.Entry == ItemId)); } }
        private LocalPlayer             Me { get { return (ObjectManager.Me); } }
        private WoWUnit                 Mob  { get { WoWUnit @object = (ObjectManager.GetObjectsOfType<WoWUnit>()
                                                                            .OrderBy(ret => ret.Distance)
                                                                            .FirstOrDefault(obj => !_npcBlacklist.Contains(obj.Guid)
                                                                                            && obj.Distance < CollectionDistance
                                                                                            && obj.Entry == MobId));
                                                        if (@object != null)
                                                            { UtilLogMessage("debug", @object.Name); }
                                                        return @object; } }


        public bool DoWeHaveQuest()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
            return quest != null;
        }

        public bool IsQuestComplete()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
            return quest == null || quest.IsCompleted;
        }

        public bool HasAura(WoWUnit unit, int auraId)
        {
            WoWAura aura = (from a in unit.Auras
                            where a.Value.SpellId == auraId
                            select a.Value).FirstOrDefault();
            return aura != null;
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(
                    // done with QB?
                    new Decorator(ret => IsQuestComplete(),
                        new PrioritySelector(
                            new Decorator(ret => Location.Distance(Me.Location) > 3,
                                new Action(ret => Navigator.MoveTo(Location))),
                            new Decorator(ret => !HasAura(Me, 82924) && 1000 < _bombWait.ElapsedMilliseconds && _bombWait.ElapsedMilliseconds > 12000,
                                new Action( ret => _isBehaviorDone = true )),
                            new Action( delegate
                                {
                                    TreeRoot.StatusText = "Waiting for Living Bomb - " + Location;
                                    if ( !_bombWait.IsRunning )
                                        _bombWait.Start();
                                })
                            )
                        ),
                    // move to safe spot initially
                    new Decorator(ret => !_moveToCoordYet,
                        new PrioritySelector(
                            new Decorator(ret => Location.Distance(Me.Location) < 3,
                                new Action(ret => _moveToCoordYet = true)),
                            new Sequence(
                                new Action(delegate { TreeRoot.StatusText = "Move to start - " + Location; }),
                                new Action(ret => Navigator.MoveTo(Location)))
                            )
                        ),
                    // have current mob
                    new Decorator( ret => Mob != null,
                        new PrioritySelector( 

                            // target quest mob
                            new Decorator( ret => Mob != null && Mob != Me.CurrentTarget,
                                new Action(ret => (Mob as WoWUnit).Target())),

                            // need to move ( timer or aura )
                            new Decorator( ret => _castTime.ElapsedMilliseconds > 5000 || HasAura(Mob as WoWUnit, AuraId),
                                new PrioritySelector(
                                    // if at safe spot then wait
                                    new Decorator(ret => Location.Distance(Me.Location) < 3,
                                        new Action(delegate
                                        {
                                            if (!HasAura(Mob as WoWUnit, AuraId))
                                                TreeRoot.StatusText = "Wait to see - " + Mob.Name;
                                            else
                                            {
                                                TreeRoot.StatusText = "Wait till clear - " + Mob.Name;
                                                _castTime.Reset();   // clear timer now that we see aura
                                            }
                                        })),
                                    new Action(delegate 
                                    { 
                                        TreeRoot.StatusText = "Move away to - " + Location; 
                                        Navigator.MoveTo(Location);
                                    }))
                                ),

                            // need to attack
                            new PrioritySelector(
                                new Decorator( ret => Mob.Distance > Range,
                                    new Action(delegate 
                                    { 
                                        TreeRoot.StatusText = "Moving in - " + Mob.Name;
                                        Navigator.MoveTo(WoWMovement.CalculatePointFrom(Mob.Location, Range - 1));
                                    })),
                                new Decorator(ret => Me.IsMoving,
                                    new Action(delegate
                                    {
                                        WoWMovement.MoveStop();
                                        StyxWoW.SleepForLagDuration();
                                    })),
                                new Decorator( ret=> _castTime.IsRunning,
                                    new Action( ret => 0 )),
                                new Action(delegate
                                {
                                    TreeRoot.StatusText = "Using item on - " + Mob.Name;
                                    (Mob as WoWUnit).Target();

                                    if (Item == null)
                                    {
                                        UtilLogMessage("fatal", "Could not locate ItemId({0}) in inventory.", ItemId);
                                        return;
                                    }

                                    WoWMovement.Face(Mob.Guid);

                                    Item.UseContainerItem();
                                    _castTime.Start();
                                    StyxWoW.SleepForLagDuration();
                                })
                                )
                            )
                        )                    
                    )
                );
        }

        public override bool IsDone
        {
            get
            {
                // different handling than most quests, becuase
                // .. when we reach the IsQuestCompleted state 
                // .. it has to run back out of danger before
                // .. leaving the QB.
                //
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
