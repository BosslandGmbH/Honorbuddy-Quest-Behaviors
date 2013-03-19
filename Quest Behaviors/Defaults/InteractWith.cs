// Behavior originally contributed by Nesox
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_InteractWith
//
// TODO:
// * This behavior needs a serious overhaul.
//      + It contains Thread.Sleep() and many more sins.
//      + It runs 'open loop', if you get interrupted while interacting, it counts it towards its completion goal
//      + It needs to be recast in terms of TreeHooks, since it expects to act during combat.
//          The old technique this behavior uses is glitchy.
//      + etc.


#region Summary and Documentation
//
// QUICK DOX:
// INTERACTWITH interacts with mobs or objects in various fashions, including:
//  * Gossiping with the mob through a set of dialogs
//  * "Right-clicking" on the mob to complete a goal
//  * Buying particular items off of vendors
// The behavior initiates interaction by "right clicking" on the mob of interest.
// The subsequent actions taken by the behavior depend on the attributes provided.
//
// BEHAVIOR ATTRIBUTES:
// Basic Attributes:
//      MobId1, MobId2, ... MobIdN [at least one MobId is REQUIRED]
//          Identifies the mobs on which the interaction should take place.
//          These Ids can represent either NPCs (WoWUnit) or Object (WoWObject);
//          however, the two cannot be mixed.  To choose the 'flavor' of the
//          Id, set the ObjectType attribute appropriately.
//      NpcState [optional; Default: DontCare]
//          [Allowed values: Alive, BelowHp, Dead, DontCare]
//          This represents the state the NPC must be in when searching for targets
//          with which we can interact.
//          (NB: You probably don't want to select "BelowHp"--it is here for backward
//           compatibility only.  I.e., You do not want to use this behavior to
//          "fight a mob then use an item"--as it is *very* unreliable
//          in performing that action.  Instead, use the CombatUseItemOnV2 behavior.) 
//      NumOfTimes [optional; Default: 1]
//          This is the number of times the behavior should interact with MobIdN.
//          Once this value is achieved, the behavior considers itself done.
//      ObjectType [optional; Default: Npc]
//          [Allowed values: GameObject, Npc]
//          Selects whether the provided MobIdN are used to identify
//          NPCs (WoWUnit) or Objects (WoWObject).  This attribute affects
//          how _all_ MobIdN will be treated--there is no way to mix-n-match.
//
// Interaction type-Buying Items:
//      BuyItemCount [optional; Default: 1]
//          This is the number of items (specified by BuyItemId) that should be
//          purchased from the Vendor (specified by MobId).
//      BuyItemId [optional; Default: none]
//          This is the ItemId of the item that should be purchased from the
//          Vendor (specified by MobId).
//
// Interaction type-Fighting Mobs:
// (NB: You do not want to use this behavior to "fight a mob then use an item"--
// as it is *very* unreliable in performing that action.  Instead, use the
// CombatUseItemOnV2 behavior.)
//      MobHpPercentLeft [optional; Default: 100.0]
//
// Interaction type-Gossiping:
//      GossipOptions [optional; Default: none]
//          Defines a comma-separated list of (1-based) numbers tha specifies
//          which Gossip option to select in each dialog frame when chatting with an NPC.
//          This value should be separated with commas. ie. GossipOption="1,1,4,2".
//
// Quest binding:
//      QuestId [optional; Default:none]:
//      QuestCompleteRequirement [Default:NotComplete]:
//      QuestInLogRequirement [Default:InLog]:
//          A full discussion of how the Quest* attributes operate is described in
//          http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
//
// Tunables:
//      CollectionDistance [optional; Default: 100.0]
//          Measured from the toon's current location, this value specifies
//          the maximum distance that should be searched when looking for
//          a viable MobId with which to interact.
//      IgnoreCombat [optional; Default: false]
//          If true, this behavior will not defend itself if attacked, and
//          will carry on with its main task.
//      IgnoreMobsInBlackspots [optional; Default: true]
//          When true, any mobs within (or too near) a blackspot will be ignored
//          in the list of viable targets that are considered for item use.
//      KeepTargetSelected [optional; Default: true]
//          If true, the behavior will not clear the toon's target after the interaction
//          is complete.  Instead, the target will remain on the last interacted
//          mob until a new mob is ready for interaction.
//          If false, the behavior clears the toon's target immediately after
//          it considers the interaction complete.
//      Loot [optional; Default: false]
//          If true, the behavior will pick up loot from any loot frame
//          offered by the MobIdN.
//          This feature is largely unused since the WoW game mechanics
//          have changed.
//      Nav [optional; Default: Mesh]
//          [Allowed values: CTM, Mesh, None]
//          Selects the navigational machinery that should be used to move.
//          Mesh is the normal method used by Honorbuddy, and should _always_
//          be preferred.
//          CTM (Click-To-Move) should be used as a last resort for areas that
//          have substantial navigation issues.
//          "None" is deprecated, and should not be used.
//      NotMoving [optional; Default: false]
//          If true, the behavior will only consider MobIdN that are not moving
//          for purposes of interaction.
//      Range [optional; Default: 4.0]
//          Defines the maximum range at which the interaction with MobIdN should take place.
//          If the toon is out of range, the toon will be moved within this distance
//          of the mob.
//      WaitForNpcs [optional; Default: true]
//          This value affects what happens if there are no MobIds in the immediate area.
//          If true, the behavior will move to the next hunting ground waypoint, or if there
//          is only one waypoint, the behavior will stand and wait for MobIdN to respawn.
//          If false, and the behavior cannot locate MobIdN in the immediate area, the behavior
//          considers itself complete.
//      WaitTime [optional; Default: 3000ms]
//          Defines the number of milliseconds to wait after the interaction is successfully
//          conducted before carrying on with the behavior on other mobs.
//      X/Y/Z [optional; Default: toon's current location when behavior is started]
//          This specifies the location where the toon should loiter
//          while waiting to interact with MobIdN.  If you need a large hunting ground
//          you should prefer using the <HuntingGrounds> sub-element, as it allows for
//          multiple locations (waypoints) to visit.
//          This value is automatically converted to a <HuntingGrounds> waypoint.
//
// BEHAVIOR EXTENSION ELEMENTS (goes between <CustomBehavior ...> and </CustomBehavior> tags)
// See the "Examples" section for typical usage.
//      HuntingGrounds [optional; Default: none]
//          The HuntingGrounds contains a set of Waypoints we will visit to seek mobs
//          that fulfill the quest goal.  The <HuntingGrounds> element accepts the following
//          attributes:
//              WaypointVisitStrategy= [optional; Default: Random]
//              [Allowed values: InOrder, Random]
//              Determines the strategy that should be employed to visit each waypoint.
//              Any mobs encountered while traveling between waypoints will be considered
//              viable.  The Random strategy is highly recommended unless there is a compelling
//              reason to otherwise.  The Random strategy 'spread the toons out', if
//              multiple bos are running the same quest.
//          Each Waypoint is provided by a <Hotspot ... /> element with the following
//          attributes:
//              Name [optional; Default: ""]
//                  The name of the waypoint is presented to the user as it is visited.
//                  This can be useful for debugging purposes, and for making minor adjustments
//                  (you know which waypoint to be fiddling with).
//              X/Y/Z [REQUIRED; Default: none]
//                  The world coordinates of the waypoint.
//              Radius [optional; Default: 7.0]
//                  Once the toon gets within Radius of the waypoint, the next waypoint
//                  will be sought.
//
// THiNGS TO KNOW:
//  * The BuySlot attribute is still present for backward compatibility--but DO NOT USE IT!
//      This attribute is deprecated, and BuySlot presents a number of problems.
//      If a vendor presents 'seasonal' or limited-quantity wares, the slot number
//      for the desired item can change.
//
#endregion


#region Examples
// "Fear No Evil" (http://wowhead.com/quest=28809)
// Revive four injured soldiers (by interacting with them) using Paxton's Prayer Book (http://wowhead.com/item=65733).
//      <CustomBehavior File="InteractWith" QuestId="28809" MobId="50047" NumOfTimes="4"
//          CollectionDistance="1" >
//          <HuntingGrounds WaypointVisitStrategy="Random" >
//              <Hotspot Name="Eastern Tent and Campfire" X="-8789.213" Y="-253.3615" Z="82.46034" />
//              <Hotspot Name="North Campfire" X="-8757.012" Y="-188.6659" Z="85.05094" />
//              <Hotspot Name="Mine entrance" X="-8716.521" Y="-105.2505" Z="87.57959" />
//              <Hotspot Name="NW LeanTo and Campfire" X="-8770.273" Y="-111.1501" Z="84.09385" />
//          </HuntingGrounds>
//      </CustomBehavior>
//
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;

using CommonBehaviors.Actions;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.InteractWith
{
    [CustomBehaviorFileName(@"InteractWith")]
    public class InteractWith : CustomForcedBehavior
    {
        #region Constructor and argument processing
        public enum ObjectType
        {
            Npc,
            GameObject,
        }

        public enum NpcStateType
        {
            Alive,
            BelowHp,
            Dead,
            DontCare,
        }

        public enum NavigationType
        {
            Mesh,
            CTM,
            None,
        }

        public InteractWith(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // Warn of deprecated attributes...
                if (args.ContainsKey("BuySlot"))
                {
                    LogWarning("*****\n"
                                + "* THE BUYSLOT ATTRIBUTE IS DEPRECATED, and may be retired in a near, future release.\n"
                                + "*\n"
                                + "* BuySlot presents a number of problems.  If a vendor presents 'seasonal' or\n"
                                + "* limited-quantity wares, the slot number for the desired item can change.\n"
                                + "\n"
                                + "* Please update the profile to use *BuyItemId* attribute in preference to BuySlot.\n"
                                + "*****");
                }

                DefaultHuntingGroundCenter = Me.Location;

                BuyItemCount = GetAttributeAsNullable<int>("BuyItemCount", false, ConstrainAs.CollectionCount, null) ?? 1;
                BuyItemId = GetAttributeAsNullable<int>("BuyItemId", false, ConstrainAs.ItemId, null) ?? 0;
                BuySlot = GetAttributeAsNullable<int>("BuySlot", false, new ConstrainTo.Domain<int>(-1, 100), null) ?? -1;
                CollectionDistance = GetAttributeAsNullable<double>("CollectionDistance", false, ConstrainAs.Range, null) ?? 100;
                GossipOptions = GetAttributeAsArray<int>("GossipOptions", false, new ConstrainTo.Domain<int>(-1, 10), new[] { "GossipOption" }, null);
                HuntingGroundCenter = GetAttributeAsNullable<WoWPoint>("", false, ConstrainAs.WoWPointNonEmpty, null) ?? DefaultHuntingGroundCenter;
                Loot = GetAttributeAsNullable<bool>("Loot", false, null, null) ?? false;
                MobIds = GetNumberedAttributesAsArray<int>("MobId", 1, ConstrainAs.MobId, new[] { "NpcId" });
                ObjType = GetAttributeAsNullable<ObjectType>("ObjectType", false, null, new[] { "MobType" }) ?? ObjectType.Npc;
                NpcState = GetAttributeAsNullable<NpcStateType>("MobState", false, null, new[] { "NpcState" }) ?? NpcStateType.DontCare;
                NavigationState = GetAttributeAsNullable<NavigationType>("Nav", false, null, new[] { "Navigation" }) ?? NavigationType.Mesh;
                MobHpPercentLeft = GetAttributeAsNullable<double>("MobHpPercentLeft", false, ConstrainAs.Percent, new[] { "HpLeftAmount" }) ?? 100.0;
                NotMoving = GetAttributeAsNullable<bool>("NotMoving", false, null, null) ?? false;
                NumOfTimes = GetAttributeAsNullable<int>("NumOfTimes", false, ConstrainAs.RepeatCount, null) ?? 1;
                Range = GetAttributeAsNullable<double>("Range", false, ConstrainAs.Range, null) ?? 4.0;
                WaitForNpcs = GetAttributeAsNullable<bool>("WaitForNpcs", false, null, null) ?? true;
                WaitTime = GetAttributeAsNullable<int>("WaitTime", false, ConstrainAs.Milliseconds, null) ?? 3000;
                IgnoreCombat = GetAttributeAsNullable<bool>("IgnoreCombat", false, null, null) ?? false;
                IgnoreMobsInBlackspots = GetAttributeAsNullable<bool>("IgnoreMobsInBlackspots", false, null, null) ?? true;
                KeepTargetSelected = GetAttributeAsNullable<bool>("KeepTargetSelected", false, null, null) ?? true;

                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                QuestId = GetAttributeAsNullable("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;


                for (int i = 0; i < GossipOptions.Length; ++i)
                { GossipOptions[i] -= 1; }


                IEnumerable<WoWUnit> mobs = ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                                                    .Where(unit => MobIds.Contains((int)unit.Entry));

                MobNames = string.Join(", ", mobs.Select(mob => (!string.IsNullOrEmpty(mob.Name)
                                                                ? mob.Name
                                                                : ("Mob(" + mob.Entry.ToString() + ")")))
                                                 .ToArray());
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                LogError("[MAINTENANCE PROBLEM]: " + except.Message
                        + "\nFROM HERE:\n"
                        + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }

        // Attributes provided by caller
        public int BuyItemCount { get; private set; }
        public int BuyItemId { get; private set; }
        public int BuySlot { get; private set; }
        public double CollectionDistance { get; private set; }
        public WoWPoint DefaultHuntingGroundCenter { get; private set; }
        public int[] GossipOptions { get; private set; }
        public HuntingGroundType HuntingGrounds { get; set; }
        public WoWPoint HuntingGroundCenter { get; private set; }
        public bool Loot { get; private set; }
        public int[] MobIds { get; private set; }
        public string MobNames { get; private set; }
        public NpcStateType NpcState { get; private set; }
        public NavigationType NavigationState { get; private set; }
        public ObjectType ObjType { get; private set; }
        public bool NotMoving { get; private set; }
        public int NumOfTimes { get; private set; }
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }
        public double Range { get; private set; }
        public bool WaitForNpcs { get; private set; }
        public int WaitTime { get; private set; }
        public bool IgnoreCombat { get; private set; }
        public double MobHpPercentLeft { get; private set; }
        public bool IgnoreMobsInBlackspots { get; private set; }
        public bool KeepTargetSelected { get; private set; }
        #endregion


        #region Private and Convenience variables
        public int Counter { get; private set; }
        private WaypointType CurrentHuntingGroundWaypoint { get; set; }
        private readonly TimeSpan Delay_WoWClientMovementThrottle = TimeSpan.FromMilliseconds(100);
        private LocalPlayer Me { get { return (StyxWoW.Me); } }

        private bool _isBehaviorDone;
        private bool _isDisposed;
        private readonly List<ulong> _npcBlacklist = new List<ulong>();
        public static Random _random = new Random((int)DateTime.Now.Ticks);
        private Composite _root;

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id$"); } }
        public override string SubversionRevision { get { return ("$Revision$"); } }
        #endregion


        #region Destructor, Dispose, and cleanup
        ~InteractWith()
        {
            Dispose(false);
        }


        public void Dispose(bool isExplicitlyInitiatedDispose)
        {
            if (!_isDisposed)
            {
                // NOTE: we should call any Dispose() method for any managed or unmanaged
                // resource, if that resource provides a Dispose() method.

                // Clean up managed resources, if explicit disposal...
                if (isExplicitlyInitiatedDispose)
                {
                    // empty, for now
                }

                // Clean up unmanaged resources (if any) here...
                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }
        #endregion


        /// <summary> Current object we should interact with.</summary>
        /// <value> The object.</value>
        private WoWObject CurrentObject
        {
            get
            {
                WoWObject @object = null;
                switch (ObjType)
                {
                    case ObjectType.GameObject:
                        @object = ObjectManager.GetObjectsOfType<WoWGameObject>().Where(obj =>
                            !_npcBlacklist.Contains(obj.Guid) &&
                            obj.DistanceSqr < CollectionDistance * CollectionDistance &&
                            MobIds.Contains((int)obj.Entry)).OrderBy(ret => ret.DistanceSqr).FirstOrDefault();

                        break;

                    case ObjectType.Npc:



                        List<WoWUnit> baseTargets = ObjectManager.GetObjectsOfType<WoWUnit>()
                                                               .Where(obj => obj.IsValid && !_npcBlacklist.Contains(obj.Guid) && !BehaviorBlacklist.Contains(obj.Guid) &&
                                                                   (!NotMoving || !obj.IsMoving) && (!IgnoreMobsInBlackspots || (IgnoreMobsInBlackspots && !Targeting.IsTooNearBlackspot(ProfileManager.CurrentProfile.Blackspots, obj.Location))) &&
                                                                    MobIds.Contains((int)obj.Entry) &&
                                                                   !Me.Minions.Contains(obj) &&
                                                                   obj.DistanceSqr < CollectionDistance * CollectionDistance)
                                                               .OrderBy(obj => obj.DistanceSqr).ToList();
                        //Fix for undead-quest (and maybe some more), these npcs are minions
                        if (baseTargets.Count == 0) baseTargets = ObjectManager.GetObjectsOfType<WoWUnit>()
                                                                  .Where(obj => obj.IsValid && !_npcBlacklist.Contains(obj.Guid) && !BehaviorBlacklist.Contains(obj.Guid) &&
                                                                      (!NotMoving || !obj.IsMoving) && (!IgnoreMobsInBlackspots || (IgnoreMobsInBlackspots && !Targeting.IsTooNearBlackspot(ProfileManager.CurrentProfile.Blackspots, obj.Location))) &&
                                                                       MobIds.Contains((int)obj.Entry) &&
                                                                      obj.DistanceSqr < CollectionDistance * CollectionDistance)
                                                                  .OrderBy(obj => obj.DistanceSqr).ToList();

                        var npcStateQualifiedTargets = baseTargets
                                                            .OrderBy(obj => obj.DistanceSqr)
                                                            .Where(target => ((NpcState == NpcStateType.DontCare)
                                                                              || ((NpcState == NpcStateType.Dead) && target.IsDead)
                                                                              || ((NpcState == NpcStateType.Alive) && target.IsAlive)
                                                                              || ((NpcState == NpcStateType.BelowHp) && target.IsAlive && (target.HealthPercent < MobHpPercentLeft))));


                        @object = npcStateQualifiedTargets.FirstOrDefault();

                        break;

                }

                if (@object != null)
                {
                    LogDeveloperInfo(@object.Name);
                }


                return @object;
            }
        }

        private bool BlacklistIfPlayerNearby(WoWObject target)
        {
            WoWUnit nearestCompetingPlayer = ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                                                    .OrderBy(player => player.Location.Distance(target.Location))
                                                    .FirstOrDefault(player => player.IsPlayer
                                                                                && player.IsAlive
                                                                                && !player.IsInOurParty());

            // If player is too close to the target, ignore target for a bit...
            if ((nearestCompetingPlayer != null)
                && (nearestCompetingPlayer.Location.Distance(target.Location) <= 25))
            {
                BehaviorBlacklist.Add(target.Guid, TimeSpan.FromSeconds(90));
                return (true);
            }

            return (false);
        }

        private bool CanNavigateFully(WoWObject target)
        {
            if (Navigator.CanNavigateFully(Me.Location, target.Location))
            {
                return (true);
            }

            return (false);
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(
                    new Decorator(ret => !_isBehaviorDone && !IsDone,
                        new PrioritySelector(
                            new Decorator(ret => Counter >= NumOfTimes,
                                new Action(ret => _isBehaviorDone = true)),

                            new PrioritySelector(
                                new Decorator(ret => CurrentObject != null && CurrentObject.DistanceSqr > Range * Range,
                                    new Switch<NavigationType>(ret => NavigationState,
                                        new SwitchArgument<NavigationType>(
                                            NavigationType.CTM,
                                            new Sequence(
                                                new Action(ret => { TreeRoot.StatusText = "Moving to interact with - " + CurrentObject.Name; }),
                                                new Action(ret => WoWMovement.ClickToMove(CurrentObject.Location))
                                            )),
                                        new SwitchArgument<NavigationType>(
                                            NavigationType.Mesh,
                                            new PrioritySelector(

                                                new Decorator(ret => !Navigator.CanNavigateFully(StyxWoW.Me.Location, CurrentObject.Location) && !StyxWoW.Me.IsFlying,
                                                    new Sequence(
														new Action(ret => { TreeRoot.StatusText = "Unable to navigate to object, Skipping - " + CurrentObject.Name + " Distance: " + CurrentObject.Distance; }),
                                                        new Action(ret => _npcBlacklist.Add(CurrentObject.Guid)),
                                                        new ActionAlwaysSucceed())),

                                                new Sequence(
                                                    new Action(delegate { TreeRoot.StatusText = "Moving to interact with \"" + CurrentObject.Name + "\""; }),
                                                    new Action(ret => Navigator.MoveTo(CurrentObject.Location))
                                                ))),

                                        new SwitchArgument<NavigationType>(
                                            NavigationType.None,
                                            new Sequence(
                                                new Action(ret => { TreeRoot.StatusText = "Object is out of range, Skipping - " + CurrentObject.Name + " Distance: " + CurrentObject.Distance; }),
                                                new Action(ret => _isBehaviorDone = true)
                                            )))),

                                new Decorator(ret => CurrentObject != null && CurrentObject.Location.DistanceSqr(Me.Location) <= Range * Range && StyxWoW.Me.IsFlying,
                                        new Action(ret =>
                                        {
                                            WoWMovement.Move(WoWMovement.MovementDirection.Descend);
                                            while (StyxWoW.Me.IsFlying)
                                                Thread.Sleep(100);

                                            WoWMovement.MoveStop(WoWMovement.MovementDirection.Descend);

                                            WoWMovement.MoveStop();          
                                        }
                                        
                                        )),

                                new Decorator(ret => CurrentObject != null && CurrentObject.Location.DistanceSqr(Me.Location) <= Range * Range,
                                    new Sequence(
                                        new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                            new Action(ret =>
                                            {
                                                WoWMovement.MoveStop();
                                                StyxWoW.SleepForLagDuration();
                                            })),

                                        new Action(ret =>
                                        {
                                            TreeRoot.StatusText = "Interacting with - " + CurrentObject.Name;

                                            StyxWoW.Me.SetFacing(CurrentObject.Location);

                                            Thread.Sleep(100);

                                            if (KeepTargetSelected && CurrentObject.Type == WoWObjectType.Unit)
                                                CurrentObject.ToUnit().Target();


                                            CurrentObject.Interact();
                                            if (CurrentObject != null)
                                                _npcBlacklist.Add(CurrentObject.Guid);

                                            Thread.Sleep(2000);
											
											if(!KeepTargetSelected)
												StyxWoW.Me.ClearTarget();
											
                                            Counter++;
                                        }),

                                        new DecoratorContinue(
                                            ret => GossipOptions.Length > 0,
                                            new Action(ret =>
                                            {
                                                foreach (var gos in GossipOptions)
                                                {
                                                    GossipFrame.Instance.SelectGossipOption(gos);
                                                    Thread.Sleep(1000);
                                                }
                                            })),

                                        new DecoratorContinue(
                                            ret => Loot && LootFrame.Instance.IsVisible,
                                            new Action(ret => LootFrame.Instance.LootAll())),

                                        new DecoratorContinue(
                                            ret => BuyItemId != 0 && MerchantFrame.Instance.IsVisible,
                                            new Action(ret =>
                                            {
                                                var items = MerchantFrame.Instance.GetAllMerchantItems();
                                                var item = items.FirstOrDefault(i => i.ItemId == BuyItemId && (i.BuyPrice * (ulong)BuyItemCount) <= Me.Copper && (i.NumAvailable >= BuyItemCount || i.NumAvailable == -1));

                                                if (item != null)
                                                {
                                                    MerchantFrame.Instance.BuyItem(item.Index, BuyItemCount);
                                                    Thread.Sleep(1500);
                                                }
                                            })),

                                        new DecoratorContinue(
                                            ret => BuySlot != -1 && BuyItemId == 0 && MerchantFrame.Instance.IsVisible,
                                            new Action(ret =>
                                            {
                                                var item = MerchantFrame.Instance.GetMerchantItemByIndex(BuySlot);
                                                if (item != null && (item.BuyPrice * (ulong)BuyItemCount) <= Me.Copper && (item.NumAvailable >= BuyItemCount || item.NumAvailable == -1))
                                                {
                                                    MerchantFrame.Instance.BuyItem(BuySlot, BuyItemCount);
                                                    Thread.Sleep(1500);
                                                }
                                            })),
                                        new DecoratorContinue(
                                            ret => Me.CurrentTarget != null && Me.CurrentTarget == CurrentObject && !KeepTargetSelected,
                                            new Action(ret => Me.ClearTarget())),

                                        new Action(ret => Thread.Sleep(WaitTime))

                                    )),

                                // If we couldn't find a mob, move to next hunting grounds waypoint...
                                new Decorator(context => CurrentObject == null,
                                    new PrioritySelector(
                                        new Decorator(context => Me.Location.Distance(CurrentHuntingGroundWaypoint.Location) <= CurrentHuntingGroundWaypoint.Radius,
                                            new Action(context =>
                                            {
                                                if (!WaitForNpcs)
                                                    { _isBehaviorDone = true; }

                                                else if (HuntingGrounds.Waypoints.Count() > 1)
                                                    { CurrentHuntingGroundWaypoint = HuntingGrounds.FindNextWaypoint(CurrentHuntingGroundWaypoint.Location); }

                                                else
                                                {
                                                    string message = "Waiting for mobs or objects to respawn.";
                                                    LogInfo(message);
                                                    TreeRoot.StatusText = message;
                                                }
                                            })),

                                        new Sequence(
                                            new Action(context =>
                                            {
                                                string destinationName =
                                                    string.IsNullOrEmpty(CurrentHuntingGroundWaypoint.Name)
                                                    ? "Moving to next hunting ground waypoint"
                                                    : string.Format("Moving to hunting ground waypoint '{0}'", CurrentHuntingGroundWaypoint.Name);

                                                TreeRoot.StatusText = destinationName;
                                                Navigator.MoveTo(CurrentHuntingGroundWaypoint.Location);
                                            }),
                                            new WaitContinue(Delay_WoWClientMovementThrottle, ret => false, new ActionAlwaysSucceed())
                                        )
                                    ))
                        )))));
        }


        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
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
            // Hunting ground processing...
            IList<HuntingGroundType> tmpHuntingGrounds;
            IsAttributeProblem |= XmlUtil_ParseSubelements<HuntingGroundType>(this, HuntingGroundType.Create, Element, "HuntingGrounds", out tmpHuntingGrounds);

            if (!IsAttributeProblem)
            {
                HuntingGrounds = (tmpHuntingGrounds != null) ? tmpHuntingGrounds.FirstOrDefault() : null;
                HuntingGrounds = HuntingGrounds ?? HuntingGroundType.Create(this, new XElement("HuntingGrounds"));

                // If user didn't provide a HuntingGrounds, or he provided a non-default center point, add it...
                if ((HuntingGrounds.Waypoints.Count() <= 0) || (HuntingGroundCenter != DefaultHuntingGroundCenter))
                    { HuntingGrounds.AppendWaypoint(HuntingGroundCenter, "hunting ground center"); }

                if (HuntingGrounds.Waypoints.Count() <= 0)
                {
                    LogError("Neither the X/Y/Z attributes nor the <HuntingGrounds> sub-element has been specified.");
                    IsAttributeProblem = true;
                }
            }
            

            // This reports problems, and stops BT processing if there was a problem with attributes...
            // We had to defer this action, as the 'profile line number' is not available during the element's
            // constructor call.
            OnStart_HandleAttributeProblem();

            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (!IsDone)
            {
                TreeRoot.GoalText = "Interacting with " + MobNames;

                CurrentHuntingGroundWaypoint = HuntingGrounds.FindFirstWaypoint(Me.Location);
            }

            if (IgnoreCombat && TreeRoot.Current != null && TreeRoot.Current.Root != null && TreeRoot.Current.Root.LastStatus != RunStatus.Running)
            {
                var currentRoot = TreeRoot.Current.Root;
                if (currentRoot is GroupComposite)
                {
                    var root = (GroupComposite)currentRoot;
                    root.InsertChild(0, CreateBehavior());
                }
            }
        }

        #endregion


        #region XML parsing

        public class WaypointType : XmlUtilClass_ElementParser
        {
            // Factory required by XmlUtil_ParseSubelements<T>()
            public static WaypointType Create(CustomForcedBehavior parentBehavior, XElement element)
            {
                return new WaypointType(parentBehavior, element);
            }
            
            private WaypointType(CustomForcedBehavior parentBehavior, XElement xElement)
                : base(parentBehavior, xElement)
            {
                try
                {
                    Name = GetAttributeAs<string>("Name", false, ConstrainAs.StringNonEmpty, null) ?? string.Empty;
                    Radius = GetAttributeAsNullable<double>("Radius", false, ConstrainAs.Range, null) ?? 10.0;
                    Location = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
                }

                catch (Exception except)
                {
                    parentBehavior.LogMessage("error", "[PROFILE PROBLEM with \"{0}\"]: {1}\nFROM HERE:\n{2}\n",
                        xElement.ToString(), except.Message, except.StackTrace);
                    IsAttributeProblem = true;
                }
            }

            public WaypointType(WoWPoint wowPoint, string name, double radius)
            {
                Location = wowPoint;
                Name = name;
                Radius = radius;
            }

            public WoWPoint Location { get; private set; }
            public string Name { get; private set; }
            public double Radius { get; private set; }


            public string ToString_FullInfo(bool useCompactForm = false, int indentLevel = 0)
            {
                var tmp = new StringBuilder();

                var indent = string.Empty.PadLeft(indentLevel);
                var fieldSeparator = useCompactForm ? " " : string.Format("\n  {0}", indent);

                tmp.AppendFormat("<WaypointType");
                tmp.AppendFormat("{0}Location=\"{1}\"", fieldSeparator, Location);
                tmp.AppendFormat("{0}Name=\"{1}\"", fieldSeparator, Name);
                tmp.AppendFormat("{0}Radius=\"{1}\"", fieldSeparator, Radius);
                tmp.AppendFormat("{0}/>", fieldSeparator);

                return tmp.ToString();
            }
        }


        public class HuntingGroundType : XmlUtilClass_ElementParser
        {
            public enum WaypointVisitStrategyType
            {
                InOrder,
                Random,
            }

            // Factory required by XmlUtil_ParseSubelements<T>()
            public static HuntingGroundType Create(CustomForcedBehavior parentBehavior, XElement xElement)
            {
                return new HuntingGroundType(parentBehavior, xElement);
            }

            private HuntingGroundType(CustomForcedBehavior parentBehavior, XElement xElement)
                : base(parentBehavior, xElement)
            {
                try
                {
                    WaypointVisitStrategy = GetAttributeAsNullable<WaypointVisitStrategyType>("WaypointVisitStrategy", false, null, null) ?? WaypointVisitStrategyType.Random;

                    IList<WaypointType> tmpList_Hotspot;
                    IsAttributeProblem |= XmlUtil_ParseSubelements<WaypointType>(parentBehavior, WaypointType.Create, xElement, "Hotspot", out tmpList_Hotspot);
                    if (!IsAttributeProblem)
                        { Waypoints = tmpList_Hotspot; }
                }

                catch (Exception except)
                {
                    parentBehavior.LogMessage("error", "[PROFILE PROBLEM with \"{0}\"]: {1}\nFROM HERE:\n{2}\n",
                        xElement.ToString(), except.Message, except.StackTrace);
                    IsAttributeProblem = true;
                }
            }

            public WaypointVisitStrategyType WaypointVisitStrategy { get; private set; }
            public IList<WaypointType> Waypoints { get; private set; }

            public void AppendWaypoint(WoWPoint newWaypoint, string name = "", double radius = 7.0)
            {
                Waypoints.Add(new WaypointType(newWaypoint, name, radius));
            }


            public WaypointType FindFirstWaypoint(WoWPoint currentLocation)
            {
                return (WaypointVisitStrategy == WaypointVisitStrategyType.Random)
                    ? FindNextWaypoint(currentLocation)
                    : FindNearestWaypoint(currentLocation);
            }


            public WaypointType FindNearestWaypoint(WoWPoint currentLocation)
            {
                return
                    (from waypoint in Waypoints
                    orderby waypoint.Location.Distance(currentLocation)
                    select waypoint)
                    .FirstOrDefault();
            }


            public WaypointType FindNextWaypoint(WoWPoint currentLocation)
            {
                if (WaypointVisitStrategy == WaypointVisitStrategyType.Random)
                {
                    return
                        (from waypoint in Waypoints
                        orderby _random.Next()
                        select waypoint)
                        .FirstOrDefault();
                }

                // If we haven't reached the nearest waypoint yet, use it...
                WaypointType nearestWaypoint = FindNearestWaypoint(currentLocation);
                if (nearestWaypoint.Location.Distance(currentLocation) > nearestWaypoint.Radius)
                    { return nearestWaypoint; }

                var queue = new Queue<WaypointType>(Waypoints);
                WaypointType tmpWaypoint;

                // Rotate the queue so the nearest waypoint is on the front...
                while (nearestWaypoint != queue.Peek())
                {
                    tmpWaypoint = queue.Dequeue();
                    queue.Enqueue(tmpWaypoint);
                }

                // Rotate one more time to get the 'next' waypoint...
                // NB: We can't simply Dequeue to access the 'next' waypoint,
                // because we must take into consideration that the queue may only
                // contain one point.
                tmpWaypoint = queue.Dequeue();
                queue.Enqueue(tmpWaypoint);

                return (queue.Peek());
            }


            public string ToString_FullInfo(bool useCompactForm = false, int indentLevel = 0)
            {
                var tmp = new StringBuilder();

                var indent = string.Empty.PadLeft(indentLevel);
                var fieldSeparator = useCompactForm ? " " : string.Format("\n  {0}", indent);

                tmp.AppendFormat("<HuntingGroundType");
                tmp.AppendFormat("{0}WaypointVisitStrategy=\"{1}\"", fieldSeparator, WaypointVisitStrategy);
                foreach (var waypoint in Waypoints)
                    { tmp.AppendFormat("{0}  {1}", waypoint.ToString_FullInfo()); }
                tmp.AppendFormat("{0}/>", fieldSeparator);

                return tmp.ToString();
            }
        }


        private static bool XmlUtil_ParseSubelements<T>(
            CustomForcedBehavior parentBehavior,
            Func<CustomForcedBehavior, XElement, T> factory,
            XElement xElement,
            string subElementsName,
            out IList<T> returnValue)
            where T: XmlUtilClass_ElementParser
        {
            bool isAttributeProblem = false;
            var tmpList = new List<T>();

            foreach (var element in xElement.Descendants(subElementsName))
            {
                try
                {
                    T parser = factory(parentBehavior, element);

                    isAttributeProblem |= parser.IsAttributeProblem;
                    tmpList.Add(parser);
                }

                catch(Exception ex)
                {
                    parentBehavior.LogMessage("error", "{0}: {1}", element.ToString(), ex.ToString());
                    isAttributeProblem = true;
                }
            }

            returnValue = isAttributeProblem ? null : tmpList;

            return isAttributeProblem;
        }


        public class XmlUtilClass_ElementParser : CustomForcedBehavior
        {
            protected XmlUtilClass_ElementParser(CustomForcedBehavior parentBehavior, XElement xElement)
                : base(ParseElementAttributes(xElement))
            {
                Element = xElement;
                _parent = parentBehavior;
            }

            protected XmlUtilClass_ElementParser()
                : base(new Dictionary<string, string>())
            {
                // empty
            }

            private static Dictionary<string, string> ParseElementAttributes(XElement element)
            {
                var arguments = new Dictionary<string, string>();

                foreach (var attribute in element.Attributes())
                    { arguments.Add(attribute.Name.ToString(), attribute.Value); }

                return arguments;
            }

            #region (No-op) Overrides for CustomForcedBehavior
            protected override Composite CreateBehavior() { return new PrioritySelector(); }

            public override bool IsDone { get { return false; } }

            public override void OnStart() { /*empty*/ }
            #endregion

            CustomForcedBehavior _parent;
        }
        #endregion


        #region Diagnostic Methods
        public delegate string StringProviderDelegate();

        /// <summary>
        /// <para>This is an efficent poor man's mechanism for reporting contract violations in methods.</para>
        /// <para>If the provided ISCONTRACTOKAY evaluates to true, no action is taken.
        /// If ISCONTRACTOKAY is false, a diagnostic message--given by the STRINGPROVIDERDELEGATE--is emitted to the log, along with a stack trace.</para>
        /// <para>This emitted information can then be used to locate and repair the code misusing the interface.</para>
        /// <para>For convenience, this method returns the evaluation if ISCONTRACTOKAY.</para>
        /// <para>Notes:<list type="bullet">
        /// <item><description><para> * The interface is built in terms of a StringProviderDelegate,
        /// so we don't pay a performance penalty to build an error message that is not used
        /// when ISCONTRACTOKAY is true.</para></description></item>
        /// <item><description><para> * The .NET 4.0 Contract support is insufficient due to the way Buddy products
        /// dynamically compile parts of the project at run time.</para></description></item>
        /// </list></para>
        /// </summary>
        /// <param name="isContractOkay"></param>
        /// <param name="stringProviderDelegate"></param>
        /// <returns>the evaluation of the provided ISCONTRACTOKAY predicate delegate</returns>
        ///  30Jun2012-15:58UTC chinajade
        ///  NB: We could provide a second interface to ContractRequires() that is slightly more convenient for static string use.
        ///  But *please* don't!  If helps maintainers to not make mistakes if they see the use of this interface consistently
        ///  throughout the code.
        public bool ContractRequires(bool isContractOkay, StringProviderDelegate stringProviderDelegate)
        {
            if (!isContractOkay)
            {
                // TODO: (Future enhancement) Build a string representation of isContractOkay if stringProviderDelegate is null
                string      message = stringProviderDelegate() ?? "NO MESSAGE PROVIDED";
                StackTrace  trace   = new StackTrace(1);

                LogError("[CONTRACT VIOLATION] {0}\nLocation:\n{1}",  message, trace.ToString());
            }

            return isContractOkay;
        }


        /// <summary>
        /// <para>Returns the name of the method that calls this function. If SHOWDECLARINGTYPE is true,
        /// the scoped method name is returned; otherwise, the undecorated name is returned.</para>
        /// <para>This is useful when emitting log messages.</para>
        /// </summary>
        /// <para>Notes:<list type="bullet">
        /// <item><description><para> * This method uses reflection--making it relatively 'expensive' to call.
        /// Use it with caution.</para></description></item>
        /// </list></para>
        /// <returns></returns>
        ///  7Jul2012-20:26UTC chinajade
        public static string    GetMyMethodName(bool  showDeclaringType   = false)
        {
            var method  = (new StackTrace(1)).GetFrame(0).GetMethod();

            if (showDeclaringType)
                { return (method.DeclaringType + "." + method.Name); }

            return (method.Name);
        }


        /// <summary>
        /// <para>For DEBUG USE ONLY--don't use in production code! (Almost exclusively used by DebuggingTools methods.)</para>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public void LogDeveloperInfo(string message, params object[] args)
        {
            LogMessage("debug", message, args);
        }
        
        
        /// <summary>
        /// <para>Error situations occur when bad data/input is provided, and no corrective actions can be taken.</para>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public void LogError(string message, params object[] args)
        {
            LogMessage("error", message, args);
        }
        
        
        /// <summary>
        /// <para>Normal information to keep user informed.</para>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public void LogInfo(string message, params object[] args)
        {
            LogMessage("info", message, args);
        }
        
        
        /// <summary>
        /// MaintenanceErrors occur as a result of incorrect code maintenance.  There is usually no corrective
        /// action a user can perform in the field for these types of errors.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        ///  30Jun2012-15:58UTC chinajade
        public void LogMaintenanceError(string message, params object[] args)
        {
            string          formattedMessage    = string.Format(message, args);
            StackTrace      trace               = new StackTrace(1);

            LogMessage("error", "[MAINTENANCE ERROR] {0}\nLocation:\n{1}", formattedMessage, trace.ToString());
        }


        /// <summary>
        /// <para>Used to notify of problems where corrective (fallback) actions are possible.</para>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public void LogWarning(string message, params object[] args)
        {
            LogMessage("warning", message, args);
        }
        #endregion
    }


    public static class WoWUnitExtensions
    {
        private static LocalPlayer Me { get { return (StyxWoW.Me); } }

        public static bool IsInOurParty(this WoWUnit wowUnit)
        {
            return ((Me.PartyMembers.FirstOrDefault(partyMember => (partyMember.Guid == wowUnit.Guid))) != null);
        }
    }


    class BehaviorBlacklist
    {
        static readonly Dictionary<ulong, BlacklistTime> SpellBlacklistDict = new Dictionary<ulong, BlacklistTime>();
        private BehaviorBlacklist()
        {
        }

        class BlacklistTime
        {
            public BlacklistTime(DateTime time, TimeSpan span)
            {
                TimeStamp = time;
                Duration = span;
            }
            public DateTime TimeStamp { get; private set; }
            public TimeSpan Duration { get; private set; }
        }

        static public bool Contains(ulong id)
        {
            RemoveIfExpired(id);
            return SpellBlacklistDict.ContainsKey(id);
        }

        static public void Add(ulong id, TimeSpan duration)
        {
            SpellBlacklistDict[id] = new BlacklistTime(DateTime.Now, duration);
        }

        static void RemoveIfExpired(ulong id)
        {
            if (SpellBlacklistDict.ContainsKey(id) &&
                SpellBlacklistDict[id].TimeStamp + SpellBlacklistDict[id].Duration <= DateTime.Now)
            {
                SpellBlacklistDict.Remove(id);
            }
        }
    }
}
