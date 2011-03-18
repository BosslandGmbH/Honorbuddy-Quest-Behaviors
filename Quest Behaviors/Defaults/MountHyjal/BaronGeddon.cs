using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using Styx;
using Styx.Logic.Combat;
using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;
using System.Diagnostics;

namespace Styx.Bot.Quest_Behaviors.MountHyjal
{
    /// <summary>
    /// BaronGeddon by Bobby53
    /// 
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
        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {
            {"QuestId",null},               //  should be 25464 for http://www.wowhead.com/quest=25464
            {"QuestName",null},             //  (doc only - not used)
            {"MobId",null},                 //  should be 40147 - Baron Geddon
            {"ItemId",null},                //  should be 54463 - Flameseer's Staff
            {"Range",null},                 //  should be 18 or less (see http://www.wowhead.com/spell=75192 )
                    // note: wowhead says 10, but actual testing shows 18+ which decreases damage taken
            {"AuraId",null},                //  should be 74813 - Inferno - http://www.wowhead.com/spell=74813
            {"CollectionDistance",null},    //  dist from point to search for mob
            {"X",null},                     //  point to start at/run to when mob has AuraId
            {"Y",null},                     //  ..  also used as center point for mob search area
            {"Z",null},
        };

        public LocalPlayer Me { get { return ObjectManager.Me; } }

        public BaronGeddon(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);

            bool error = false;
            
            uint questId;
            if (!uint.TryParse(Args["QuestId"], out questId))
            {
                Logging.Write("Parsing attribute 'QuestId' in BaronGeddon behavior failed! please check your profile!");
                error = true;
            }

            uint mobId = 40147;
            if (Args.ContainsKey("MobId") && !uint.TryParse(Args["MobId"], out mobId))
            {
                Logging.Write("Parsing attribute 'MobId' in BaronGeddon behavior failed! please check your profile!");
                error = true;
            }

            uint itemId = 54463;
            if (Args.ContainsKey("ItemId") && !uint.TryParse(Args["ItemId"], out itemId))
            {
                Logging.Write("Parsing attribute 'ItemId' in BaronGeddon behavior failed! please check your profile!");
                error = true;
            }

            int auraId = 74813;
            if (Args.ContainsKey("AuraId") && !int.TryParse(Args["AuraId"], out auraId))
            {
                Logging.Write("Parsing attribute 'AuraId' in BaronGeddon behavior failed! please check your profile!");
                error = true;
            }

            int distance = 100;
            if (Args.ContainsKey("CollectionDistance"))
            {
                int.TryParse(Args["CollectionDistance"], out distance);
            }

            int distance2 = 18;
            if (Args.ContainsKey("Range"))
            {
                int.TryParse(Args["Range"], out distance2);
            }

            float x, y, z;
            if (!float.TryParse(Args["X"], out x))
            {
                Logging.Write("Parsing attribute 'X' in BaronGeddon behavior failed! please check your profile!");
                error = true;
            }

            if (!float.TryParse(Args["Y"], out y))
            {
                Logging.Write("Parsing attribute 'Y' in BaronGeddon behavior failed! please check your profile!");
                error = true;
            }

            if (!float.TryParse(Args["Z"], out z))
            {
                Logging.Write("Parsing attribute 'Z' in BaronGeddon behavior failed! please check your profile!");
                error = true;
            }

            if (error)
                TreeRoot.Stop();

            QuestId = questId;
            MobId = mobId;
            ItemId = itemId;
            AuraId = auraId;
            CollectionDistance = distance != 0 ? distance : 100;
            Range = distance2 != 0 ? distance2 : 18;
            Location = new WoWPoint(x, y, z);

            castTime = new Stopwatch();
            bombWait = new Stopwatch();
            _isDone = IsQuestComplete();

        }

        public WoWPoint Location { get; private set; }
        public int Counter { get; private set; }
        public uint MobId { get; private set; }
        public uint ItemId { get; private set; }
        public int AuraId { get; private set; }
        public uint QuestId { get; private set; }
        public int CollectionDistance { get; private set; }
        public int Range { get; private set; }

        public bool moveToCoordYet = false;
        public Stopwatch castTime;
        public Stopwatch bombWait;

        private readonly List<ulong> _npcBlacklist = new List<ulong>();

        /// <summary> Current object we should interact with.</summary>
        /// <value> The object.</value>
        private WoWUnit Mob
        {
            get
            {
                WoWUnit @object = null;
                @object = ObjectManager.GetObjectsOfType<WoWUnit>().OrderBy(ret => ret.Distance).FirstOrDefault(obj =>
                    !_npcBlacklist.Contains(obj.Guid) &&
                    obj.Distance < CollectionDistance &&
                    obj.Entry == MobId);

                if (@object != null)
                {
                    Logging.Write(@object.Name);
                }
                return @object;
            }
        }

        private WoWItem Item
        {
            get
            {
                var item = StyxWoW.Me.CarriedItems.FirstOrDefault(ret => ret.Entry == ItemId);
                return item;
            }
        }

        #region Overrides of CustomForcedBehavior
        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(
                    // done with QB?
                    new Decorator(ret => IsQuestComplete(),
                        new PrioritySelector(
                            new Decorator(ret => Location.Distance(Me.Location) > 3,
                                new Action(ret => Navigator.MoveTo(Location))),
                            new Decorator(ret => !HasAura(Me, 82924) && 1000 < bombWait.ElapsedMilliseconds && bombWait.ElapsedMilliseconds > 12000,
                                new Action( ret => _isDone = true )),
                            new Action( delegate
                                {
                                    TreeRoot.StatusText = "Waiting for Living Bomb - " + Location;
                                    if ( !bombWait.IsRunning )
                                        bombWait.Start();
                                })
                            )
                        ),
                    // move to safe spot initially
                    new Decorator(ret => !moveToCoordYet,
                        new PrioritySelector(
                            new Decorator(ret => Location.Distance(Me.Location) < 3,
                                new Action(ret => moveToCoordYet = true)),
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
                            new Decorator( ret => castTime.ElapsedMilliseconds > 5000 || HasAura(Mob as WoWUnit, AuraId),
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
                                                castTime.Reset();   // clear timer now that we see aura
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
                                new Decorator( ret=> castTime.IsRunning,
                                    new Action( ret => 0 )),
                                new Action(delegate
                                {
                                    TreeRoot.StatusText = "Using item on - " + Mob.Name;
                                    (Mob as WoWUnit).Target();

                                    if (Item == null)
                                    {
                                        Logging.Write(Color.Red, "Could not find item with id:{0} for BaronGeddon behavior!", ItemId);
                                        Logging.Write(Color.Red, "Honorbuddy stopped!");
                                        TreeRoot.Stop();
                                        return;
                                    }

                                    WoWMovement.Face(Mob.Guid);

                                    Item.UseContainerItem();
                                    castTime.Start();
                                    StyxWoW.SleepForLagDuration();
                                    Counter++;
                                })
                                )
                            )
                        )                    
                    )
                );
        }

        public bool DoWeHaveQuest()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
            return quest != null;
        }

        public bool IsQuestComplete()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
            return quest == null || quest.IsCompleted;
        }

        public bool HasAura(WoWUnit unit, int auraId)
        {
            WoWAura aura = (from a in unit.Auras
                            where a.Value.SpellId == auraId
                            select a.Value).FirstOrDefault();
            return aura != null;
        }

        private bool _isDone;
        public override bool IsDone
        {
            get
            {
                // different handling than most quests, becuase
                // .. when we reach the IsQuestCompleted state 
                // .. it has to run back out of danger before
                // .. leaving the QB.
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
                return _isDone || quest == null;
            }
        }

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
            if (quest != null )
            {
                TreeRoot.GoalText = string.Format("Doing quest: {0}", quest.Name);
            }
        }

        #endregion
    }
}
