// Behavior originally contributed by Nesox / rework by Chinajade
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


#region Summary and Documentation
//
// QUICK DOX:
// INTERACTWITH interacts with mobs or objects in various fashions, including:
//  * Gossiping with the mob through a set of dialogs
//  * "Right-clicking" on the mob to complete a goal
//  * Buying particular items off of vendors
//  * Using an item on a mob or object.  The item can be one-click use, or two-click
//      use (two-click: clicks once to get a 'placement cursor', and clicks the
//      second time to drop the placement cursor on the mob or object).
//  * Looting or harvesting (through interaction) an item off a mob or object
// The behavior initiates interaction by "right clicking" on the mob of interest.
// The subsequent actions taken by the behavior depend on the attributes provided.
//
// BEHAVIOR ATTRIBUTES:
// Basic Attributes:
//      AuraIdOnMobN [optional; Default: none]
//          Selects only MobIdN that have AuraIdOnMobN on them.
//      AuraIdMissingFromMob [optional; Default: none]
//          Selects only MobIdN that have do not have AuraIdMissingFromMobN on them.
//      MobIdN [at least one MobIdN is REQUIRED]
//          Identifies the mobs on which the interaction should take place.
//          These Ids can represent either NPCs (WoWUnit) or Object (WoWObject);
//          however, the two cannot be mixed.  To choose the 'flavor' of the
//          Id, set the ObjectType attribute appropriately.
//          This attribute may be safely combined with AuraIdOnMobN, AuraIdMissingFromMobN,
//          and MobIdN.
//      MobState [optional; Default: DontCare]
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
//          If the Quest or QuestObjectiveIndex completes prior to reaching this
//          count, the behavior also terminates.
//      ObjectType [optional; Default: Npc]
//          [Allowed values: GameObject, Npc]
//          Selects whether the provided MobIdN are used to identify
//          NPCs (WoWUnit) or Objects (WoWObject).  This attribute affects
//          how _all_ MobIdN will be treated--there is no way to mix-n-match.
//
// Interaction by Buying Items:
//      BuyItemCount [optional; Default: 1]
//          This is the number of items (specified by BuyItemId) that should be
//          purchased from the Vendor (specified by MobId).
//      InteractByBuyingItemId [optional; Default: none]
//          This is the ItemId of the item that should be purchased from the
//          Vendor (specified by MobId).
//
// Interaction by Fighting Mobs:
// (NB: You do not want to use this behavior to "fight a mob then use an item"--
// as it is *very* unreliable in performing that action.  Instead, use the
// CombatUseItemOnV2 behavior.)
//      MobHpPercentLeft [optional; Default: 100.0]
//
// Interaction by Gossiping:
//      InteractByGossipOptions [optional; Default: none]
//          Defines a comma-separated list of (1-based) numbers that specifies
//          which Gossip option to select in each dialog frame when chatting with an NPC.
//          This value should be separated with commas. ie. InteractByGossipOptions="1,1,4,2".
//
// Interaction by Looting:
//      InteractByLooting [optional; Default: false]
//          If true, the behavior will pick up loot from any loot frame
//          offered by the MobIdN.
//          This feature is largely unused since the WoW game mechanics
//          have changed.
//
// Interaction by Quest frames:
//      InteractByQuestFrameDisposition [optional; Default: Ignore]
//          [Allowed values: Accept, Complete, Continue, Ignore]
//          This attribute determines the behavior's response should the NPC
//          with which we've interacted offer us a quest frame.
//
// Interact by Using Item:
//      InteractByUsingItemId [optional; Default: none]
//          Specifies an ItemId to use on the specified MobInN.
//          The item may be a normal 'one-click-to-use' item, or it may be
//          a (two-click) item that needs to be placed on the ground at
//          the MobIdN's location.
//
// Quest binding:
//      QuestId [optional; Default:none]:
//      QuestCompleteRequirement [Default:NotComplete]:
//      QuestInLogRequirement [Default:InLog]:
//          A full discussion of how the Quest* attributes operate is described in
//          http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
//      QuestObjectiveIndex [optional; Default: none]
//          Qualifies the behavior to consider only an objective of the quest--
//          not the full quest--to determine whether or not the behavior is done.
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
//      Nav [optional; Default: Mesh]
//          [Allowed values: CTM, Mesh, None]
//          Selects the navigational machinery that should be used to move.
//          Mesh is the normal method used by Honorbuddy, and should _always_
//          be preferred.
//          CTM (Click-To-Move) should be used as a last resort for areas that
//          have substantial navigation issues.
//          "None" is deprecated, and should not be used.
//      NonCompeteDistance [optional; Default: 0]
//          If a player is within this distance of a target that looks
//          interesting to us, we'll ignore the target.  The assumption is that the player may
//          be going for the same target, and we don't want to draw attention.
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
//      WaitTime [optional; Default: 1500ms]
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
//      OLD DOX: Buys the item from the slot. Slots are:    0 1
//                                                          2 3
//                                                          4 5
//                                                          6 7
//                                                          page2
//                                                          8 9 etc.
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
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
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
        public enum MobStateType
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

        public enum ObjectType
        {
            Npc,
            GameObject,
        }

        public enum QuestFrameDisposition
        {
            Accept,
            Complete,
            Continue,
            Ignore,
        }

        public InteractWith(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                DefaultHuntingGroundCenter = Me.Location;

                // Basic attributes...
                MobIds = GetNumberedAttributesAsArray<int>("MobId", 1, ConstrainAs.MobId, new[] { "NpcId" });
                AuraIdsOnMob = GetNumberedAttributesAsArray<int>("AuraIdOnMob", 0, ConstrainAs.AuraId, null);
                AuraIdsMissingFromMob = GetNumberedAttributesAsArray<int>("AuraIdMissingFromMob", 0, ConstrainAs.AuraId, null);

                MobState = GetAttributeAsNullable<MobStateType>("MobState", false, null, new[] { "NpcState" }) ?? MobStateType.DontCare;
                NumOfTimes = GetAttributeAsNullable<int>("NumOfTimes", false, ConstrainAs.RepeatCount, null) ?? 1;
                ObjType = GetAttributeAsNullable<ObjectType>("ObjectType", false, null, new[] { "MobType" }) ?? ObjectType.Npc;


                // InteractionBy attributes...
                InteractByBuyingItemId = GetAttributeAsNullable<int>("InteractByBuyingItemId", false, ConstrainAs.ItemId, null)
                    ?? GetAttributeAsNullable<int>("BuyItemId", false, ConstrainAs.ItemId, null) /*Legacy name--don't use */
                    ?? 0;
                InteractByGossipOptions = GetAttributeAsArray<int>("InteractByGossipOptions", false, new ConstrainTo.Domain<int>(-1, 10), null, null);
                if (InteractByGossipOptions.Length <= 0)
                    { InteractByGossipOptions = GetAttributeAsArray<int>("GossipOptions", false, new ConstrainTo.Domain<int>(-1, 10), new[] { "GossipOption" }, null); } /*Legacy name--don't use */
                InteractByLooting = GetAttributeAsNullable<bool>("InteractByLooting", false, null, null)
                    ?? GetAttributeAsNullable<bool>("Loot", false, null, null) /* Legacy name--don't use*/
                    ?? false;
                InteractByQuestFrameAction = GetAttributeAsNullable<QuestFrameDisposition>("InteractByQuestFrameDisposition", false, null, null)
                    ?? QuestFrameDisposition.Ignore;
                InteractByUsingItemId = GetAttributeAsNullable<int>("InteractByUsingItemId", false, ConstrainAs.ItemId, null) ?? 0;


                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                QuestId = GetAttributeAsNullable("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
                QuestObjectiveIndex = GetAttributeAsNullable<int>("QuestObjectiveIndex", false, new ConstrainTo.Domain<int>(1, 5), null) ?? 0;


                // Tunables...
                BuyItemCount = GetAttributeAsNullable<int>("BuyItemCount", false, ConstrainAs.CollectionCount, null) ?? 1;
                CollectionDistance = GetAttributeAsNullable<double>("CollectionDistance", false, ConstrainAs.Range, null) ?? 100;
                HuntingGroundCenter = GetAttributeAsNullable<WoWPoint>("", false, ConstrainAs.WoWPointNonEmpty, null) ?? DefaultHuntingGroundCenter;
                IgnoreCombat = GetAttributeAsNullable<bool>("IgnoreCombat", false, null, null) ?? false;
                IgnoreMobsInBlackspots = GetAttributeAsNullable<bool>("IgnoreMobsInBlackspots", false, null, null) ?? true;
                KeepTargetSelected = GetAttributeAsNullable<bool>("KeepTargetSelected", false, null, null) ?? false;
                MobHpPercentLeft = GetAttributeAsNullable<double>("MobHpPercentLeft", false, ConstrainAs.Percent, new[] { "HpLeftAmount" }) ?? 100.0;
                NavigationState = GetAttributeAsNullable<NavigationType>("Nav", false, null, new[] { "Navigation" }) ?? NavigationType.Mesh;
                NonCompeteDistance = GetAttributeAsNullable<double>("NonCompeteDistance", false, new ConstrainTo.Domain<double>(1.0, 40.0), null) ?? 0.0;
                NotMoving = GetAttributeAsNullable<bool>("NotMoving", false, null, null) ?? false;
                Range = GetAttributeAsNullable<double>("Range", false, ConstrainAs.Range, null) ?? 4.0;
                WaitForNpcs = GetAttributeAsNullable<bool>("WaitForNpcs", false, null, null) ?? true;
                WaitTime = GetAttributeAsNullable<int>("WaitTime", false, ConstrainAs.Milliseconds, null) ?? 1500;
                
                
                // Semantic coherency / covariant dependency checks --
                if ((QuestObjectiveIndex > 0) && (QuestId <= 0))
                {
                    LogError("QuestObjectiveIndex of '{0}' specified, but no corresponding QuestId provided", QuestObjectiveIndex);
                    IsAttributeProblem = true;
                }


                // Deprecated attributes...
                InteractByBuyingItemInSlotNum = GetAttributeAsNullable<int>("InteractByBuyingItemInSlotNum", false, new ConstrainTo.Domain<int>(-1, 100), new string[] { "BuySlot" }) ?? -1;

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


                for (int i = 0; i < InteractByGossipOptions.Length; ++i)
                    { InteractByGossipOptions[i] -= 1; }
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
        public int[] AuraIdsOnMob { get; private set; }
        public int[] AuraIdsMissingFromMob { get; private set; }
        public int BuyItemCount { get; private set; }
        public double CollectionDistance { get; private set; }
        public WoWPoint DefaultHuntingGroundCenter { get; private set; }
        public HuntingGroundType HuntingGrounds { get; set; }
        public WoWPoint HuntingGroundCenter { get; private set; }
        public bool IgnoreCombat { get; private set; }
        public bool IgnoreMobsInBlackspots { get; private set; }
        public int InteractByBuyingItemId { get; private set; }
        public int InteractByBuyingItemInSlotNum { get; private set; }
        public int[] InteractByGossipOptions { get; private set; }
        public int InteractByUsingItemId { get; private set; }
        public bool KeepTargetSelected { get; private set; }
        public bool InteractByLooting { get; private set; }
        public double MobHpPercentLeft { get; private set; }
        public int[] MobIds { get; private set; }
        public MobStateType MobState { get; private set; }
        public NavigationType NavigationState { get; private set; }
        public ObjectType ObjType { get; private set; }
        public double NonCompeteDistance { get; private set; }
        public bool NotMoving { get; private set; }
        public int NumOfTimes { get; private set; }
        public int QuestId { get; private set; }
        public QuestFrameDisposition InteractByQuestFrameAction { get; private set; }
        public int QuestObjectiveIndex { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }
        public double Range { get; private set; }
        public bool WaitForNpcs { get; private set; }
        public int WaitTime { get; private set; }
        #endregion


        #region Private and Convenience variables
        public delegate WoWUnit WoWUnitDelegate(object context);

        public int Counter { get; private set; }
        private WaypointType CurrentHuntingGroundWaypoint { get; set; }
        private readonly TimeSpan Delay_LagDuration = TimeSpan.FromMilliseconds((StyxWoW.WoWClient.Latency * 2) + 150);
        private readonly TimeSpan Delay_WoWClientMovementThrottle = TimeSpan.FromMilliseconds(250);
        private TimeSpan Delay_AfterItemUse { get { return VariantTimeSpan(400, 900); } }
        private TimeSpan Delay_Interaction { get { return VariantTimeSpan(900, 1900); } }
        private int GossipOptionIndex { get; set; }
        private WoWItem ItemToUse { get; set; }
        private LocalPlayer Me { get { return (StyxWoW.Me); } }
        private WoWObject SelectedInteractTarget { get; set; }

        private Composite _behaviorTreeHook_CombatMain = null;
        private Composite _behaviorTreeHook_CombatOnly = null;
        private Composite _behaviorTreeHook_DeathMain = null;
        private Composite _behaviorTreeHook_Main = null;
        private LocalBlacklist _interactBlacklist = new LocalBlacklist(TimeSpan.FromSeconds(30));
        private bool _isBehaviorDone;
        private bool _isDisposed;
        public static Random _random = new Random((int)DateTime.Now.Ticks);
        public Stopwatch _waitTimer = new Stopwatch();

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

                if (_behaviorTreeHook_CombatMain != null)
                {
                    TreeHooks.Instance.RemoveHook("Combat_Main", _behaviorTreeHook_CombatMain);
                    _behaviorTreeHook_CombatMain = null;
                }

                if (_behaviorTreeHook_CombatOnly != null)
                {
                    TreeHooks.Instance.RemoveHook("Combat_Only", _behaviorTreeHook_CombatOnly);
                    _behaviorTreeHook_CombatOnly = null;
                }

                if (_behaviorTreeHook_DeathMain != null)
                {
                    TreeHooks.Instance.RemoveHook("Death_Main", _behaviorTreeHook_DeathMain);
                    _behaviorTreeHook_DeathMain = null;
                }
                
                BotEvents.OnBotStop -= BotEvents_OnBotStop;
                
                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }
        

        public void BotEvents_OnBotStop(EventArgs args)
        {
            Dispose();
        }
        #endregion


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _behaviorTreeHook_Main ?? (_behaviorTreeHook_Main = CreateMainBehavior());
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
                        || IsQuestObjectiveComplete(QuestId, QuestObjectiveIndex)
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
                    { HuntingGrounds.AppendWaypoint(HuntingGroundCenter, "hunting ground center", Navigator.PathPrecision); }

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
                BotEvents.OnBotStop += BotEvents_OnBotStop;
        
                PlayerQuest quest = Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = string.Format(
                    "{0}: \"{1}\"\nInteracting with: {2}",
                    this.GetType().Name,
                    ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress (no associated quest)"),
                    string.Join(", ", MobIds.Select(m => FindMobName(m))));
                
                CurrentHuntingGroundWaypoint = HuntingGrounds.FindFirstWaypoint(Me.Location);

                ItemToUse = Me.CarriedItems.FirstOrDefault(i => (i.Entry == InteractByUsingItemId));
                if ((InteractByUsingItemId > 0) && (ItemToUse == null))
                {
                    LogError("[PROFILE ERROR] Unable to locate ItemId({0}) in our bags", InteractByUsingItemId);
                    TreeRoot.Stop();
                    _isBehaviorDone = true;
                }
            }
            
            _behaviorTreeHook_CombatMain = CreateBehavior_CombatMain();
            TreeHooks.Instance.InsertHook("Combat_Main", 0, _behaviorTreeHook_CombatMain);
            _behaviorTreeHook_CombatOnly = CreateBehavior_CombatOnly();
            TreeHooks.Instance.InsertHook("Combat_Only", 0, _behaviorTreeHook_CombatOnly);
            _behaviorTreeHook_DeathMain = CreateBehavior_DeathMain();
            TreeHooks.Instance.InsertHook("Death_Main", 0, _behaviorTreeHook_DeathMain);
        }

        #endregion


        #region Main Behaviors
        private Composite CreateBehavior_CombatMain()
        {
            return new PrioritySelector(
                // empty, for now
                );
        }


        private Composite CreateBehavior_CombatOnly()
        {
            return new PrioritySelector(
                // If current target is not attackable, blacklist it...
                new Decorator(context => (Me.CurrentTarget != null) && !Me.CurrentTarget.Attackable,
                    new Action(context => { Blacklist.Add(Me.CurrentTarget, BlacklistFlags.Combat, TimeSpan.FromSeconds(120)); })),

                // If we're ignoring combat, deprive Combat Routine of chance to run...
                new Decorator(context => IgnoreCombat,
                    new ActionAlwaysSucceed())
            );
        }


        private Composite CreateBehavior_DeathMain()
        {
            return new PrioritySelector(
                // empty, for now
                );
        }


        private Composite CreateMainBehavior()
        {
            return new PrioritySelector(

                // Delay, if necessary...
                // NB: We must do this prior to checking for 'behavior done'.  Otherwise, profiles
                // that don't have an associated quest, and put the behavior in a <While> loop will not behave
                // as the profile writer expects.  They expect the delay to be executed if the interaction
                // succeeded.
                new Decorator(context => _waitTimer.IsRunning && (_waitTimer.ElapsedMilliseconds < WaitTime),
                    new Action(context => { LogInfo("Completing wait of {0}", PrettyTime(WaitTime)); })),

                // Counter is used to determine 'done'...
                new Decorator(ret => Counter >= NumOfTimes,
                    new Action(ret => _isBehaviorDone = true)),
                    
                // If quest is done, behavior is done...
                new Decorator(context => IsDone,
                    new Action(context =>
                    {
                        _isBehaviorDone = true;
                        LogInfo("Finished");
                    })),

                // If we've leftover spell cursor dangling, clear it...
                // NB: This can happen for "use item on location" type activites where you get interrupted
                // (by say a walk-in mob).
                new Decorator(context => StyxWoW.Me.CurrentPendingCursorSpell != null,
                    new Action(context => { Lua.DoString("SpellStopTargeting()"); })),

                // HBcore bug work-around:
                // HBcore (.552) doesn't alway rest between combat when it should.  This takes care of the problem...
                UtilityBehavior_Rest(),

                // If a mob is targeting us, deal with it immediately, so our interact actions won't be interrupted...
                UtilityBehavior_SpankMobTargetingUs(),


                // If interact target is no longer viable, try to find another...s
                new Decorator(context => !IsViableForInteracting(SelectedInteractTarget),
                    new Action(context =>
                    {
                        SelectedInteractTarget = FindBestInteractTarget();
                        return RunStatus.Failure;   // fall through
                    })),


                #region Deal with mob we've selected for interaction...
                new Decorator(context => SelectedInteractTarget != null,
                    new PrioritySelector(

                        // Show user the target that's interesting to us...
                        new Decorator(context => Me.CurrentTarget != SelectedInteractTarget,
                            new Action(context =>
                            {
                                WoWUnit wowUnit = SelectedInteractTarget.ToUnit();
                                if ((wowUnit != null) && IsInLineOfSight(wowUnit))
                                    { wowUnit.Target(); }
                                return RunStatus.Failure;   // fall through
                            })),
                                        

                        #region Move close enough to the mob/object to interact...
                        new Decorator(ret => (SelectedInteractTarget.DistanceSqr > Range * Range)
                                                || (!IsInLineOfSight(SelectedInteractTarget)
                                                    && (SelectedInteractTarget.DistanceSqr > SelectedInteractTarget.InteractRangeSqr)),
                            new Switch<NavigationType>(ret => NavigationState,
                                new SwitchArgument<NavigationType>(NavigationType.CTM,
                                    new Action(ret =>
                                    {
                                        TreeRoot.StatusText = string.Format("Moving to interact with {0} (dist: {1:F1}{2})",
                                            SelectedInteractTarget.Name, SelectedInteractTarget.Distance,
                                            IsInLineOfSight(SelectedInteractTarget) ? "" : " noLoS");
                                        WoWMovement.ClickToMove(SelectedInteractTarget.Location);
                                    })),

                                new SwitchArgument<NavigationType>(NavigationType.Mesh,
                                    new PrioritySelector(
                                        new Decorator(ret => !Navigator.CanNavigateFully(StyxWoW.Me.Location, SelectedInteractTarget.Location)
                                                            && (!Me.IsFlying || !Me.IsOnTransport),
                                            new Action(ret =>
                                            {
                                                TimeSpan blacklistDuration = BlacklistInteractTarget(SelectedInteractTarget);
                                                TreeRoot.StatusText = string.Format("Unable to navigate to {0} (dist: {1:F1})--blacklisting for {2}.",
                                                                                    SelectedInteractTarget.Name, SelectedInteractTarget.Distance, blacklistDuration);
                                            })),

                                        new Action(ret =>
                                        {
                                            TreeRoot.StatusText = string.Format("Moving to interact with {0} (dist: {1:F1}{2})",
                                                SelectedInteractTarget.Name, SelectedInteractTarget.Distance,
                                                IsInLineOfSight(SelectedInteractTarget) ? "" : ", noLoS");
                                            Navigator.MoveTo(SelectedInteractTarget.Location);
                                        })
                                    )),

                                new SwitchArgument<NavigationType>(NavigationType.None,
                                    new Action(ret =>
                                    {
                                        TimeSpan blacklistDuration = BlacklistInteractTarget(SelectedInteractTarget);
                                        TreeRoot.StatusText = string.Format("{0} is out of range (dist: {1:F1})--blacklisting for {2}.",
                                                                            SelectedInteractTarget.Name, SelectedInteractTarget.Distance, blacklistDuration);
                                        _isBehaviorDone = true;
                                    }))
                            )),
                        #endregion


                        #region Interact, if we're within range of the object...
                        new Decorator(ret => SelectedInteractTarget.Location.DistanceSqr(Me.Location) <= Range * Range,
                            new PrioritySelector(

                                // Land, if we are flying and the interact target is standing on the ground...
                                new Decorator(context => Me.IsFlying
                                                        && (SelectedInteractTarget.ToUnit() != null)
                                                        && !SelectedInteractTarget.ToUnit().IsFlying,
                                    new Sequence(
                                        new Action(context => { WoWMovement.Move(WoWMovement.MovementDirection.Descend); }),
                                        new WaitContinue(Delay_WoWClientMovementThrottle, context => false, new ActionAlwaysSucceed())
                                    )),
                                        
                                // Dismount to interact, if target is an object, or a non-flying NPC...
                                new Decorator(context => Me.Mounted
                                                        && ((SelectedInteractTarget.ToUnit() == null)
                                                            || !SelectedInteractTarget.ToUnit().IsFlying),
                                    new Action(context => { Mount.Dismount(); })),
                                    
                                // Prep to interact...
                                new Decorator(context => !Me.IsSafelyFacing(SelectedInteractTarget),
                                    new Action(context => { Me.SetFacing(SelectedInteractTarget.Location); })),
                                new Decorator(context => Me.IsMoving,
                                    new Sequence(
                                        new Action(context => { WoWMovement.MoveStop(); }),
                                        new WaitContinue(Delay_LagDuration, context => false, new ActionAlwaysSucceed())
                                    )),
                                    
    
                                #region Deal with loot frame, if open...
                                // Nothing really special for us to do here.  HBcore will take care of 'normal' looting.
                                // And looting objects through "interaction" is usually nothing more than right-clicking
                                // on the object and a loot frame is not even produced.  But this is here, just in case
                                // a loot frame is produced, and HBcore doesn't deal with it.
                                new Decorator(context => LootFrame.Instance.IsVisible,
                                    new Sequence(
                                        new Action(context =>
                                        {
                                            LogInfo("Looting {0}", SelectedInteractTarget.Name);
                                            LootFrame.Instance.LootAll();
                                        }),
                                        new WaitContinue(Delay_Interaction, context => false, new ActionAlwaysSucceed()),
                                        // Make certain loot frame didn't morph into another frame type...
                                        new Decorator(context => LootFrame.Instance.IsVisible,
                                            new Sequence(
                                                new Action(context => { LootFrame.Instance.Close(); }),
                                                new DecoratorContinue(context => (Me.CurrentTarget == SelectedInteractTarget) && !KeepTargetSelected,
                                                    new Action(context => { Me.ClearTarget(); }))
                                            ))
                                    )),
                                #endregion


                                #region Deal with gossip frame, if open...
                                new Decorator(context => GossipFrame.Instance.IsVisible,
                                    new Sequence(
                                        new DecoratorContinue(context => InteractByGossipOptions.Length > 0,
                                            new Sequence(selectedTargetNameContext => SelectedInteractTarget.Name,
                                                new Action(selectedTargetNameContext => { LogInfo("Gossiping with {0}", (string)selectedTargetNameContext); }),
                                                new Action(selectedTargetNameContext => { GossipOptionIndex = 0; }),
                                                new WhileLoop(RunStatus.Success, selectedTargetNameContext => GossipOptionIndex < InteractByGossipOptions.Length,
                                                    new Action(selectedTargetNameContext => { GossipFrame.Instance.SelectGossipOption(InteractByGossipOptions[GossipOptionIndex++]); }),
                                                    new WaitContinue(Delay_Interaction, selectedTargetNameContext => false, new ActionAlwaysSucceed())
                                                ),
                                                new Action(selectedTargetNameContext =>
                                                {
                                                    // NB: The SelectedInteractTarget may no longer be viable after gossiping.
                                                    // For instance, the NPC may disappear, or if the toon was forced on a taxi ride
                                                    // as a result of the gossip, the SelectedInteractTarget will no longer be viable
                                                    // once we land.
                                                    LogInfo("Gossip with {0} complete.", (string)selectedTargetNameContext);

                                                    // NB: Some merchants require that we gossip with them before purchase.
                                                    // If the caller has also specified a "buy item", then we're not done yet.
                                                    if ((InteractByBuyingItemId <= 0) && (InteractByBuyingItemInSlotNum <= 0))
                                                    {
                                                        BlacklistInteractTarget(SelectedInteractTarget);
                                                        _waitTimer.Restart();
                                                        ++Counter;
                                                    }
                                                })
                                            )),
                                        new DecoratorContinue(context => InteractByGossipOptions.Length <= 0,
                                            new Sequence(
                                                new Action(context => { LogError("[PROFILE ERROR]: Gossip frame not expected--ignoring."); }),
                                                new WaitContinue(Delay_Interaction, context => false, new ActionAlwaysSucceed())
                                            )),
                                        // Make certain gossip frame didn't morph into another frame type (e.g., merchant frame)...
                                        new Decorator(context => GossipFrame.Instance.IsVisible,
                                            new Sequence(
                                                new Action(context => { GossipFrame.Instance.Close(); }),
                                                new DecoratorContinue(context => (Me.CurrentTarget == SelectedInteractTarget) && !KeepTargetSelected,
                                                    new Action(context => { Me.ClearTarget(); }))
                                            ))
                                    )),
                                #endregion


                                #region Deal with merchant frame, if open...
                                new Decorator(context => MerchantFrame.Instance.IsVisible,
                                    new Sequence(
                                        new Action(context =>
                                        {
                                            if ((InteractByBuyingItemId > 0) || (InteractByBuyingItemInSlotNum >= 0))
                                            {
                                                MerchantItem item = (InteractByBuyingItemId > 0)
                                                    ? MerchantFrame.Instance.GetAllMerchantItems().FirstOrDefault(i => i.ItemId == InteractByBuyingItemId)
                                                    : (InteractByBuyingItemInSlotNum >= 0) ? MerchantFrame.Instance.GetMerchantItemByIndex(InteractByBuyingItemInSlotNum)
                                                    : null;

                                                if (item == null)
                                                {
                                                    if (InteractByBuyingItemId > 0)
                                                    {
                                                        LogError("[PROFILE ERROR]: {0} does not appear to carry ItemId({1})--abandoning transaction.",
                                                            SelectedInteractTarget.Name, InteractByBuyingItemId);
                                                    }
                                                    else
                                                    {
                                                        LogError("[PROFILE ERROR]: {0} does not have an item to sell in slot #{1}--abandoning transaction.",
                                                            SelectedInteractTarget.Name, InteractByBuyingItemInSlotNum);
                                                    }
                                                }
                                                else if ((item.BuyPrice * (ulong)BuyItemCount) > Me.Copper)
                                                {
                                                    LogError("[PROFILE ERROR]: Toon does not have enough money to purchase {0} (qty: {1})"
                                                        + "--(requires: {2}, have: {3})--abandoning transaction.",
                                                        item.Name, BuyItemCount, PrettyMoney(item.BuyPrice * (ulong)BuyItemCount), PrettyMoney(Me.Copper));
                                                }
                                                else if ((item.NumAvailable != /*unlimited*/-1) && (item.NumAvailable < BuyItemCount))
                                                {
                                                    LogError("[PROFILE ERROR]: {0} only has {1} units of {2} (we need {3})--abandoning transaction.",
                                                        SelectedInteractTarget.Name, item.NumAvailable, item.Name, BuyItemCount);
                                                }
                                                else
                                                {
                                                    LogInfo("Buying {0} (qty: {1}) from {2}", item.Name, BuyItemCount, SelectedInteractTarget.Name);
                                                    MerchantFrame.Instance.BuyItem(item.Index, BuyItemCount);
                                                }
                                                // NB: We do not blacklist merchants.
                                                ++Counter;
                                            }

                                            else
                                                { LogError("[PROFILE ERROR] Merchant frame not expected--ignoring."); }
                                        }),
                                        new WaitContinue(Delay_Interaction, context => false, new ActionAlwaysSucceed()),
                                        // Make certain merchant frame didn't morph into another frame type...
                                        new Decorator(context => MerchantFrame.Instance.IsVisible,
                                            new Sequence(
                                                new Action(context => { MerchantFrame.Instance.Close(); }),
                                                new DecoratorContinue(context => (Me.CurrentTarget == SelectedInteractTarget) && !KeepTargetSelected,
                                                    new Action(context => { Me.ClearTarget(); }))
                                            ))
                                    )),
                                #endregion


                                #region Deal with quest frame, if open...
                                // Side-effect of interacting with some NPCs for quests...
                                new Decorator(context => QuestFrame.Instance.IsVisible,
                                    new Sequence(
                                        new DecoratorContinue(context => InteractByQuestFrameAction == QuestFrameDisposition.Accept,
                                            new Action(context => { QuestFrame.Instance.AcceptQuest(); })),
                                        new DecoratorContinue(context => InteractByQuestFrameAction == QuestFrameDisposition.Complete,
                                            new Action(context => { QuestFrame.Instance.CompleteQuest(); })),
                                        new DecoratorContinue(context => InteractByQuestFrameAction == QuestFrameDisposition.Continue,
                                            new Action(context => { QuestFrame.Instance.ClickContinue(); })),
                                        new WaitContinue(Delay_Interaction, context => false, new ActionAlwaysSucceed()),
                                        // Make certain merchant frame didn't morph into another frame type...
                                        new Decorator(context => QuestFrame.Instance.IsVisible,
                                            new Action(context => { QuestFrame.Instance.Close(); }))
                                    )),
                                #endregion
                                        
                                        
                                #region Interact with, or use item on, selected target...
                                new Decorator(context => (ItemToUse != null) && (ItemToUse.CooldownTimeLeft > TimeSpan.Zero),
                                    new Action(context => { LogInfo("Waiting for {0} cooldown", ItemToUse.Name); })),

                                new Sequence(
                                    // Interact by item use...
                                    new DecoratorContinue(context => ItemToUse != null,
                                        new Sequence(
                                            new Action(context =>
                                            {
                                                LogInfo("Using {0} on {1} ", ItemToUse.Name, SelectedInteractTarget.Name);
                                                ItemToUse.Use(SelectedInteractTarget.Guid);
                                            }),
                                            new WaitContinue(Delay_AfterItemUse, context => false, new ActionAlwaysSucceed()),
                                            new DecoratorContinue(context => StyxWoW.Me.CurrentPendingCursorSpell != null,
                                                new Action(context => { SpellManager.ClickRemoteLocation(SelectedInteractTarget.Location); }))
                                        )),
                                    // Interact by right-click...
                                    new DecoratorContinue(context => ItemToUse == null,
                                        new Sequence(
                                            new Action(context =>
                                            {
                                                LogInfo("Interacting with {0}", SelectedInteractTarget.Name);
                                                SelectedInteractTarget.Interact();
                                            }),
                                            new WaitContinue(Delay_Interaction, context => false, new ActionAlwaysSucceed())
                                        )),
                                    // Peg tally, if follow-up actions not expected...
                                    new DecoratorContinue(context => !IsFrameExpectedFromInteraction(),
                                        new Action(context =>
                                        {
                                            BlacklistInteractTarget(SelectedInteractTarget);
                                            _waitTimer.Restart();
                                            ++Counter;

                                            if ((Me.CurrentTarget == SelectedInteractTarget) && !KeepTargetSelected)
                                                { Me.ClearTarget(); }
                                        }))
                                )
                                #endregion
                        ))
                        #endregion
                    )),
                #endregion


                #region Deal with no available mobs in immediate vicinity...
                new Decorator(context => SelectedInteractTarget == null,
                    new PrioritySelector(
                        new Decorator(context => Me.Location.Distance(CurrentHuntingGroundWaypoint.Location) <= CurrentHuntingGroundWaypoint.Radius,
                            new PrioritySelector(
                                new Decorator(context => !WaitForNpcs,
                                    new Action(context => { _isBehaviorDone = true;})),
                                new Decorator(context => HuntingGrounds.Waypoints.Count() > 1,
                                    new Action(context =>  { CurrentHuntingGroundWaypoint = HuntingGrounds.FindNextWaypoint(CurrentHuntingGroundWaypoint.Location); })),
                                new CompositeThrottle(TimeSpan.FromSeconds(30),
                                    new Action(context =>
                                    {
                                        string message = string.Format("Waiting for {0} to respawn.", 
                                                                        string.Join(", ", MobIds.Select(m => FindMobName(m))));
                                        TreeRoot.StatusText = message;

                                        var exclusions = Debug_ShowExclusions();
                                        LogInfo("{0}{1}{2}", message, (!string.IsNullOrEmpty(exclusions) ? "\n" : ""), exclusions);
                                    }))
                                )),

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
                #endregion

            );
        }
        #endregion


        #region Helpers
        private TimeSpan BlacklistInteractTarget(WoWObject selectedTarget)
        {
            // NB: The selectedTarget can sometimes go "non viable".
            // An example: We gossip with an NPC that results in a forced taxi ride.  Honorbuddy suspends
            // this behavior while the taxi ride is in progress, and when we land, the selectedTarget
            // is no longer viable to blacklist.
            if (!IsViable(selectedTarget))
                { return TimeSpan.Zero; }

            WoWUnit wowUnit = selectedTarget.ToUnit();
            bool isShortBlacklist = (wowUnit != null) && (wowUnit.IsVendor || wowUnit.IsFlightMaster);
            TimeSpan blacklistDuration = TimeSpan.FromSeconds(isShortBlacklist ? 30 : 180);

            _interactBlacklist.Add(selectedTarget, blacklistDuration);
            return blacklistDuration;
        }


        /// <summary> Current object we should interact with.</summary>
        /// <value> The object.</value>
        private WoWObject FindBestInteractTarget()
        {
            double collectionDistanceSqr = CollectionDistance * CollectionDistance;
            double minionWeighting = Math.Pow(1000, 4);

            WoWObject entity = 
               (from wowObject in ObjectManager.GetObjectsOfType<WoWObject>(true)
                where
                    MobIds.Contains((int)wowObject.Entry)
                    && (wowObject.DistanceSqr < collectionDistanceSqr)
                    && IsViableForInteracting(wowObject)
                // NB: we use a weighted vertical difference to make mobs higher or lower
                // than us 'more expensive' to get to.  This is important in tunnels/caves
                // where mobs may be within X feet of us, but they are below or above us,
                // and we have to traverse much tunnel to get to them.
                let verticalDiff = Math.Abs(wowObject.Location.Z - Me.Location.Z)
                let weightedVerticalDiff = Math.Pow(verticalDiff, 4)
                orderby
                    wowObject.Distance2DSqr + weightedVerticalDiff
                    // Fix for undead-quest (and maybe some more), where the targets can be minions...
                    + (Me.Minions.Contains(wowObject) ? minionWeighting : 1)
                select wowObject)
                .FirstOrDefault();

            if (entity != null)
            {
                LogDeveloperInfo(entity.Name);
            }

            return entity;
        }


        private string FindMobName(int mobId)
        {
            WoWObject wowObject = ObjectManager.GetObjectsOfType<WoWObject>(true)
                                .Where(o => o.Entry == mobId)
                                .FirstOrDefault();

            return (wowObject != null) ? wowObject.Name : string.Format("MobId({0})", mobId);
        }


        // 25Feb2013-12:50UTC chinajade
        private IEnumerable<WoWUnit> FindMobsTargetingMeOrPet()
        {
            return
                from unit in ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                where
                    unit.IsValid
                    && unit.IsAlive
                    && !unit.IsFriendly
                    && ((unit.CurrentTarget == Me)
                        || (Me.GotAlivePet && unit.CurrentTarget == Me.Pet))
                    && !Blacklist.Contains(unit, BlacklistFlags.Combat)
                select unit;
        }


        // 25Feb2013-12:50UTC chinajade
        private IEnumerable<WoWUnit> FindNonFriendlyTargetingMeOrPet()
        {
            return
                from unit in ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                where
                    IsViableForFighting(unit)
                    && unit.IsTargetingMeOrPet
                select unit;
        }
        
        
        // 25Feb2013-12:50UTC chinajade
        private IEnumerable<WoWPlayer> FindPlayersNearby(WoWPoint location, double radius)
        {
            return
                from player in ObjectManager.GetObjectsOfType<WoWPlayer>(true, false)
                where
                    IsViable(player)
                    && player.IsAlive
                    && player.Location.Distance(location) < radius
                select player;
        }


        private bool IsFrameExpectedFromInteraction()
        {
            // NB: InteractByLoot is nothing more than a normal "right click" activity
            // on something. If something is normally 'lootable', HBcore will deal with it.
            return
                (InteractByBuyingItemId > 0)
                || (InteractByBuyingItemInSlotNum > -1)
                || (InteractByGossipOptions.Length > 0)
                || (InteractByQuestFrameAction != QuestFrameDisposition.Ignore);
        }


        private bool IsInCompetition(WoWObject wowObject)
        {
            return FindPlayersNearby(wowObject.Location, NonCompeteDistance).Count() > 0;
        }


        private bool IsInLineOfSight(WoWObject wowObject)
        {
            WoWUnit wowUnit = wowObject.ToUnit();

            return (wowUnit == null)
                ? wowObject.InLineOfSight
                // NB: For WoWUnit, we do two checks.  This keeps us out of trouble when the
                // mobs are up a stairway and we're looking at them through a guardrail and
                // other boundary conditions.
                : (wowUnit.InLineOfSight && wowUnit.InLineOfSpellSight);
        }


        // 24Feb2013-08:11UTC chinajade
        private bool IsQuestObjectiveComplete(int questId, int objectiveIndex)
        {
            // If quest and objective was not specified, obviously its not complete...
            if ((questId <= 0) || (objectiveIndex <= 0))
                { return false; }

            // If quest is not in our log, obviously its not complete...
            if (Me.QuestLog.GetQuestById((uint)questId) == null)
                { return false; }

            int questLogIndex = Lua.GetReturnVal<int>(string.Format("return GetQuestLogIndexByID({0})", questId), 0);

            return
                Lua.GetReturnVal<bool>(string.Format("return GetQuestLogLeaderBoard({0},{1})", objectiveIndex, questLogIndex), 2);
        }


        // 24Feb2013-08:11UTC chinajade
        private bool IsViable(WoWObject wowObject)
        {
            return
                (wowObject != null)
                && wowObject.IsValid;
        }
        
        
        // 24Feb2013-08:11UTC chinajade
        private bool IsViableForFighting(WoWUnit wowUnit)
        {
            return
                IsViable(wowUnit)
                && wowUnit.IsAlive
                && !Blacklist.Contains(wowUnit, BlacklistFlags.Combat);
        }


        // 24Feb2013-08:11UTC chinajade
        private bool IsViableForInteracting(WoWObject wowObject)
        {
            if (wowObject == null)
                { return false; }

            bool baseQualifiers = 
                IsViable(wowObject)
                && !_interactBlacklist.Contains(wowObject)
                && (!IgnoreMobsInBlackspots || (IgnoreMobsInBlackspots && !Targeting.IsTooNearBlackspot(ProfileManager.CurrentProfile.Blackspots, wowObject.Location)))
                && !IsInCompetition(wowObject);

            // We're done, if not a WoWUnit...
            WoWUnit wowUnit = wowObject.ToUnit();
            if (wowUnit == null)
                { return baseQualifiers; }
                
            // Additional qualifiers for WoWUnits...        
            return
                baseQualifiers
                && (!NotMoving || !wowUnit.IsMoving)
                && ((AuraIdsOnMob.Length <= 0) || wowUnit.GetAllAuras().Any(a => AuraIdsOnMob.Contains(a.SpellId)))
                && ((AuraIdsMissingFromMob.Length <= 0) || !wowUnit.GetAllAuras().Any(a => AuraIdsMissingFromMob.Contains(a.SpellId)))
                && ((MobState == MobStateType.DontCare)
                    || ((MobState == MobStateType.Dead) && wowUnit.IsDead)
                    || ((MobState == MobStateType.Alive) && wowUnit.IsAlive)
                    || ((MobState == MobStateType.BelowHp) && wowUnit.IsAlive && (wowUnit.HealthPercent < MobHpPercentLeft)));
        }


        // 24Feb2013-08:11UTC chinajade
        private bool IsViableTargetForPulling(WoWUnit wowUnit)
        {
            return
                IsViableForFighting(wowUnit)
                && (wowUnit.TappedByAllThreatLists || !wowUnit.TaggedByOther);
        }


        private string PrettyMoney(ulong totalCopper)
        {
            ulong moneyCopper = totalCopper % 100;
            totalCopper /= 100;

            ulong moneySilver = totalCopper % 100;
            totalCopper /= 100;

            ulong moneyGold = totalCopper;

            string formatString =
                (moneyGold > 0) ? "{0}g{1:D2}s{2:D2}c"
                : (moneySilver > 0) ? "{1}s{2:D2}c"
                : "{2}c";

            return string.Format(formatString, moneyGold, moneySilver, moneyCopper);
        }


        private string PrettyTime(int milliSeconds)
        {
            if (milliSeconds < 1000)
                { return string.Format("{0}ms", milliSeconds); }

            return string.Format("{0}s", milliSeconds / 1000);
        }
        

        private static TimeSpan VariantTimeSpan(int milliSecondsMin, int milliSecondsMax)
        {
            return TimeSpan.FromMilliseconds(_random.Next(milliSecondsMin, milliSecondsMax));
        }
        #endregion


        #region Utility Behavior
        private Composite UtilityBehavior_Rest()
        {
            return new Decorator(context => RoutineManager.Current.NeedRest,
                new PrioritySelector(
                    new Decorator(context => RoutineManager.Current.RestBehavior != null,
                        RoutineManager.Current.RestBehavior),
                    new Action(context => { RoutineManager.Current.Rest(); })
                ));
        }

        
        /// <summary>
        /// Unequivocally engages mob in combat.
        /// </summary>
        // 24Feb2013-08:11UTC chinajade
        private Composite UtilityBehavior_SpankMob(WoWUnitDelegate selectedTargetDelegate)
        {
            return new PrioritySelector(targetContext => selectedTargetDelegate(targetContext),
                new Decorator(targetContext => IsViableForFighting((WoWUnit)targetContext),
                    new PrioritySelector(               
                        new Decorator(targetContext => ((WoWUnit)targetContext).Distance > CharacterSettings.Instance.PullDistance,
                            new Action(targetContext => { Navigator.MoveTo(((WoWUnit)targetContext).Location); })),
                        new Decorator(targetContext => Me.CurrentTarget != (WoWUnit)targetContext,
                            new Action(targetContext =>
                            {
                                BotPoi.Current = new BotPoi((WoWUnit)targetContext, PoiType.Kill);
                                ((WoWUnit)targetContext).Target();
                                if (Me.Mounted)
                                    { Mount.Dismount(); }
                            })),
                        new Decorator(targetContext => !((WoWUnit)targetContext).IsTargetingMeOrPet,
                            new PrioritySelector(
                                new Decorator(targetContext => RoutineManager.Current.CombatBehavior != null,
                                    RoutineManager.Current.CombatBehavior),
                                new Action(targetContext => { RoutineManager.Current.Combat(); })
                            ))
                    )));
        }


        private WoWUnit MobTargetingUs { get; set; }
        private Composite UtilityBehavior_SpankMobTargetingUs()
        {
            return new PrioritySelector(
                // If a mob is targeting us, deal with it immediately, so subsequent activities won't be interrupted...
                // NB: This can happen if we 'drag mobs' behind us on the way to our destination.
                new Decorator(context => !IsViableForFighting(MobTargetingUs),
                    new Action(context =>
                    {
                        MobTargetingUs = FindNonFriendlyTargetingMeOrPet().OrderBy(u => u.DistanceSqr).FirstOrDefault();
                        return RunStatus.Failure;   // fall through
                    })),

                // Spank any mobs we find being naughty...
                new Decorator(context => MobTargetingUs != null,
                    UtilityBehavior_SpankMob(context => MobTargetingUs))
            );
        }
        #endregion


        #region Debug
        private string Debug_ShowExclusions()
        {
            IEnumerable<WoWObject> interactCandidates =
                from wowObject in ObjectManager.GetObjectsOfType<WoWObject>(true)
                where
                    IsViable(wowObject)
                    && MobIds.Contains((int)wowObject.Entry)
                select wowObject;

            if (MobIds.Count() <= 1)
            {
                StringBuilder excludeReasons = new StringBuilder();

                excludeReasons.Append("Excluded Units:");
                excludeReasons.AppendLine();
                foreach (var wowObject in interactCandidates)
                {
                    excludeReasons.Append("    ");
                    excludeReasons.Append(Debug_TellWhyExcluded(wowObject));
                    excludeReasons.AppendLine();
                }

                return excludeReasons.ToString();
            }

            return string.Empty;
        }


        private string Debug_TellWhyExcluded(WoWObject wowObject)
        {
            List<string> reasons = new List<string>();

            if (!IsViable(wowObject))
                { return "[NotViable]"; }

            if (wowObject.Distance > CollectionDistance)
                { reasons.Add(string.Format("ExceedsCollectionDistance({0})", CollectionDistance)); }

            if (_interactBlacklist.Contains(wowObject))
                { reasons.Add("Blacklisted"); }

            if (IgnoreMobsInBlackspots && Targeting.IsTooNearBlackspot(ProfileManager.CurrentProfile.Blackspots, wowObject.Location))
                { reasons.Add("InBlackspot"); }

            if (IsInCompetition(wowObject))
            {
                reasons.Add(string.Format("InCompetition({0} players within {1:F1})",
                    FindPlayersNearby(wowObject.Location, NonCompeteDistance).Count(),
                    NonCompeteDistance));
            }

            WoWUnit wowUnit = wowObject.ToUnit();
            if (wowUnit != null)
            {
                var wowUnitAuras = wowUnit.GetAllAuras().ToList();

                if (NotMoving && wowUnit.IsMoving)
                    { reasons.Add("Moving"); }

                if ((AuraIdsOnMob.Length > 0) && !wowUnitAuras.Any(a => AuraIdsOnMob.Contains(a.SpellId)))
                {
                    reasons.Add(string.Format("MissingRequiredAura({0})",
                        string.Join(",", AuraIdsOnMob.Select(i => i.ToString()))));
                }

                if ((AuraIdsMissingFromMob.Length > 0) && wowUnitAuras.Any(a => AuraIdsMissingFromMob.Contains(a.SpellId)))
                {
                    reasons.Add(string.Format("HasUnwantedAura({0})",
                        string.Join(",", wowUnitAuras.Where(a => AuraIdsMissingFromMob.Contains(a.SpellId)).Select(a => a.SpellId.ToString()))
                        ));
                }

                if (!((MobState == MobStateType.DontCare)
                        || ((MobState == MobStateType.Dead) && wowUnit.IsDead)
                        || ((MobState == MobStateType.Alive) && wowUnit.IsAlive)
                        || ((MobState == MobStateType.BelowHp) && wowUnit.IsAlive && (wowUnit.HealthPercent < MobHpPercentLeft))))
                {
                    if (MobState == MobStateType.BelowHp)
                        { reasons.Add(string.Format("!{0}({1}%)", MobState, MobHpPercentLeft)); }
                    else
                        { reasons.Add(string.Format("!{0}", MobState)); }
                }
            }

            return string.Format("{0} [{1}]", wowObject.Name, string.Join(",", reasons));
        }
        #endregion


        #region Local Blacklist
        // The HBcore 'global' blacklist will also prevent looting.  We don't want that.
        // Since the HBcore blacklist is not built to instantiate, we have to roll our
        // own.  <sigh>
        public class LocalBlacklist
        {
            public LocalBlacklist(TimeSpan maxSweepTime)
            {
                _maxSweepTime = maxSweepTime;
                _stopWatchForSweeping.Start();
            }

            private Dictionary<ulong, DateTime> _blackList = new Dictionary<ulong, DateTime>();
            private TimeSpan _maxSweepTime;
            private Stopwatch _stopWatchForSweeping = new Stopwatch();


            public void Add(ulong guid, TimeSpan timeSpan)
            {
                if (_stopWatchForSweeping.Elapsed > _maxSweepTime)
                    { RemoveExpired(); }

                _blackList[guid] = DateTime.Now.Add(timeSpan);
            }


            public void Add(WoWObject wowObject, TimeSpan timeSpan)
            {
                if (wowObject != null)
                    { Add(wowObject.Guid, timeSpan); }
            }


            public bool Contains(ulong guid)
            {
                return (_blackList.ContainsKey(guid) && (_blackList[guid] > DateTime.Now));
            }


            public bool Contains(WoWObject wowObject)
            {
                return (wowObject == null)
                    ? false
                    : Contains(wowObject.Guid);
            }


            public void RemoveExpired()
            {
                DateTime now = DateTime.Now;

                List<ulong> expiredEntries = (from key in _blackList.Keys
                                                where (_blackList[key] < now)
                                                select key).ToList();

                foreach (ulong entry in expiredEntries)
                    { _blackList.Remove(entry); }

                _stopWatchForSweeping.Restart();
            }
        }
        #endregion


        #region TreeSharp extensions
        public class CompositeThrottle : DecoratorContinue
        {
            public CompositeThrottle(TimeSpan throttleTime, Composite composite)
                : base(composite)
            {
                _throttleTime = throttleTime;
                // Timer was created with "0" time--this makes it "good to go" for first iteration
                _throttle.Reset();
            }


            protected override bool CanRun(object context)
            {
                if (!_throttle.IsFinished)
                    { return false; }
                
                _throttle.WaitTime = _throttleTime;
                _throttle.Reset();
                return true;
            }

            private readonly TimeSpan _throttleTime;
            private readonly WaitTimer _throttle = new WaitTimer(TimeSpan.FromSeconds(0));
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
        /// <para>Error situations occur when bad data/input is provided, and no corrective actions can be taken.</para>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public void LogFatal(string message, params object[] args)
        {
            LogMessage("fatal", message, args);
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
}
