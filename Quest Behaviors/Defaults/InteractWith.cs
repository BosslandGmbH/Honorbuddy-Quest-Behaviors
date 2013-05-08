// Behavior originally contributed by Nesox / completely reworked by Chinajade
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
// *** ALSO see the documentation in QuestBehaviorBase.cs.  All of the attributes it provides
// *** are available here, also.  The documentation on the attributes QuestBehaviorBase provides
// *** is _not_ repeated here, to prevent documentation inconsistencies.
//
// Basic Attributes:
//      FactionIdN [at least one FactionIdN or one MobIdN is REQUIRED]
//          Identifies the faction of the mobs on which the interaction should take place.
//          If you specify both MobIdN and FactionIdN, the two sets will be combined,
//          and any target that fulfillseither MobIdN or FactionIdN will be selected for interaction.
//      MobIdN [at least one FactionIdN or one MobIdN is REQUIRED]
//          Identifies the mobs on which the interaction should take place.
//          The MobIdN can represent either an NPC (WoWUnit) or an Object (WoWObject).
//          The two types can freely be mixed.
//          If you specify both MobIdN and FactionIdN, the two sets will be combined,
//          and any target that fulfillseither MobIdN or FactionIdN will be selected for interaction.
//      NumOfTimes [optional; Default: 1]
//          This is the number of times the behavior should interact with MobIdN.
//          Once this value is achieved, the behavior considers itself done.
//          If the Quest or QuestObjectiveIndex completes prior to reaching this
//          count, the behavior also terminates.
//
// Potential Target Qualifiers:
//      AuraIdOnMobN [optional; Default: none]
//          This attribute qualifies a target that fullfills the MobIdN or FactionIdN selection.
//          The target *must* possess an aura that matches one of the defined 
//          AuraIdMissingFromMobN, in order to be considered a target for interaction.
//      AuraIdMissingFromMob [optional; Default: none]
//          This attribute qualifies a target that fullfills the MobIdN or FactionIdN selection.
//          The target must *not* possess an aura that matches one of the defined 
//          AuraIdMissingFromMobN, in order to be considered a target for interaction.
//      MobState [optional; Default: DontCare]
//          [Allowed values for NPC targets: Alive, AliveNotInCombat, BelowHp, Dead, DontCare]
//          This attribute qualifies the state the MobIdN or FactionIdN must be in,
//          when selecting targets for interaction.
//          (NB: You probably don't want to select "BelowHp"--it is here for backward
//           compatibility only.  I.e., You do not want to use this behavior to
//          "fight a mob then use an item"--as it is *very* unreliable
//          in performing that action.  Instead, use the CombatUseItemOnV2 behavior.) 
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
//      InteractByQuestFrameDisposition [optional; Default: TerminateProfile]
//          [Allowed values: Accept/Complete/Continue/Ignore/TerminateBehavior/TerminateProfile]
//          This attribute determines the behavior's response should the NPC
//          with which we've interacted offer us a quest frame.
//          Accept/Complete/Continue
//              clicks on the appropriate button in the offered quest frame.
//              The behavior continues after the appropriate button is clicked.
//          Ignore
//              closes the offered quest frame, and continues with the behavior.
//          TerminateBehavior
//              closes the offered quest frame, and terminates the behavior.
//              This is useful in situations where the Quest frame was unexpected,
//              but tells us what 'state' an NPC may be in.  See the "By Any Means Necessary"
//              in the Examples section below.
//
// Interact by Using Item:
//      InteractByUsingItemId [optional; Default: none]
//          Specifies an ItemId to use on the specified MobInN.
//          The item may be a normal 'one-click-to-use' item, or it may be
//          a (two-click) item that needs to be placed on the ground at
//          the MobIdN's location.
//
// Tunables:
//      CollectionDistance [optional; Default: 100.0]
//          Measured from the toon's current location, this value specifies
//          the maximum distance that should be searched when looking for
//          a viable MobId with which to interact.
//      IgnoreCombat [optional; Default: false]
//          If true, this behavior will not defend itself if attacked, and
//          will carry on with its main task.
//      IgnoreLoSToTarget [optional; Default: false]
//          If true, the behavior will not consider Line of Sight when trying to interact
//          with the selected target.
//      KeepTargetSelected [optional; Default: false]
//          If true, the behavior will not clear the toon's target after the interaction
//          is complete.  Instead, the target will remain on the last interacted
//          mob until a new mob is ready for interaction.
//          If false, the behavior clears the toon's target immediately after
//          it considers the interaction complete.
//      MinRange [optional; Default: 0.0]
//          Defines the minimum range at which the interaction with MobIdN should take place.
//          If the toon is too close to the mob, the toon will move to acquire this minimum
//          distance to the mob.
//      NotMoving [optional; Default: false]
//          If true, the behavior will only consider MobIdN that are not moving
//          for purposes of interaction.
//      PreInteractMountStrategy [optional; Default: None]
//          [allowed values: CancelShapeshift, Dismount, DismountOrCancelShapeshift, Mount, None]
//          Provides the opportunity to alter the mounting state of a toon immediately prior
//          to interacting, or using an object.  The options are defined as follows:
//              CancelShapeshift
//                  Cancels *any* shapeshifted form, whether it represents a 'mounted form'
//                  or not.  Examples of non-mounted forms include a Druid's Bear or Cat Form.
//              Dismount
//                  Unmounts from an explicit mount, or cancels a 'mounted form'.  Examples
//                  of mounted forms are Druid Flight Forms, Druid Travel Form, Shaman Ghost Wolf,
//                  and Worgen Running Wild.
//              DismountOrCancelShapeshift
//                  Dismounts or cancel's a shapeshifted form--whichever is appropriate.
//              Mount
//                  Mounts the mount defined in the user's preference settings.
//              None
//                  Does not alter the existing mounting state prior to interaction.
//      ProactiveCombatStrategy [optional; Default: ClearAll]
//          [allowed values: NoClear/ClearMobsTargetingUs/ClearMobsThatWillAggro/ClearAll]
//              NoClear
//                  Will not proactively clear around the selected target.  This means that
//                  an attempt to interact with the target could be interrupted by a mob.
//              ClearMobsTargetingUs
//                  If a mob targets us we immediately move to engage it, rather than wait
//                  for the mob to actually close the range to us, put us in combat, then
//                  we deal with it.  This option prevents dragging a large number of mobs
//                  to our selected interact target only to have to fight them.
//              ClearMobsThatWillAggro
//                  If mobs within 'aggro range' of our selected interact target are cleared
//                  before we attempt to interact with the selected target.  This prevents us
//                  from being interrupted when we attempt to interact with our selected target.
//              ClearAll
//                  Combines ClearMobsThatWillAggro and ClearMobsTargetingUs.
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
//          Please see "Things to know", below.
//      WaitTime [optional; Default: 0ms]
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
// * As a design choice, this behavior waits a variant amount of time when 'clicking on things'.
//      We try to simulate an attentive human as much as possible.
//
// * The InteractByGossipOptions and InteractByBuyingItemId attributes can be combined to get to
//      Innkeeper goods.  Innkeepers require you to select one of their gossip options before they
//      will show you their wares for sale.
//
// * If the InteractByGossipOptions selects a "bind to this location" option, then InteractWith
//      will automatically confirm the request to bind at the location.  After the binding is complete
//      InteractWith displays a confirmation message of the new bind location.
//
// * The behavior pro-actively clears mobs within aggro range of the selected interact target.
//      This prevents the mobs from interfering with the interaction.  If IgnoreCombat="true",
//      then this pro-active clearing is also turned off.
//
// * If you are trying to gossip with NPCs that are in combat, this behavior will help the NPC
//      kill the mob so the NPC will leave combat.  This is necessary because many NPCs will not
//      gossip when they are in combat.
//
// * Be careful when specifying the WaitForNpcs="true".
//      The 'interact blacklist' is internal to the behavior.  The means the blacklist is destroyed
//      any time the behavior exits, and a fresh one is created upon re-entry to the behavior.
//      This means InteractWith does not know which mobs have already been interacted and which
//      have not, when WaitForNpcs="true".  The can cause slow progress as the failure detection
//      mechnaisms built into the behavior kick in and re-exclude non-viable mobs as they are
//      rediscovered.
//
// * Deprecated attributes:
//      + BuySlot
//          This attribute is deprecated, and BuySlot presents a number of problems.
//          If a vendor presents 'seasonal' or limited-quantity wares, the slot number
//          for the desired item can change.
//          OLD DOX: Buys the item from the slot. Slots are:    0 1
//                                                              2 3
//                                                              4 5
//                                                              6 7
//                                                              page2
//                                                              8 9 etc.
//      + ObjectType
//          This option should no longer be specified--the behavior just 'figures it out'
//      + Nav--prefer MovementBy, instead.
//          For support reasons, here is the old documentation:
//              Nav [optional; Default: not any]
//              [Allowed values: CTM, Mesh, None]
//              This attribute is no longer used--use MovementBy, instead.
//              (See documentation in QuestBehaviorBase for details).
//
#endregion


#region Examples
// BUYING AN ITEM:
// From a simple vendor that immediately presents you with a frame of their wares:
//      <CustomBehavior File="InteractWith" MobId="44236" InteractByBuyingItemId="2723"
//			X="-8365.76" Y="594.658" Z="97.00068" />
//
// From an Innkeeper that requires you to gossip before showing you their wares.
// Gossip entry 2 is the "Let me browse your goods." option from Thaegra Tillstone.  (The InteractBy* options
// are not order dependent.)
//      <CustomBehavior File="InteractWith" MobId="44235" InteractByGossipOptions="2" InteractByBuyingItemId="4540"
//			X="-8365.76" Y="594.658" Z="97.00068" />
//
//
// BINDING AT AN INN:
// Gossip entry 1 is the "Make this inn your home." option in Thaegra Tillstone's Inn in the Stormwind
// Dwarven District:
//      <If Condition="Me.HearthstoneAreaId != 5150">
//          <CustomBehavior File="InteractWith" MobId="44235" InteractByGossipOptions="1" />
//		        X="-8365.76" Y="594.658" Z="97.00068" />
//      </If>
// The only way to obtain the "area id" for the Condition is to actually set your hearthstone,
// then use the following command with HBConsole (or Developer Tools):
//      Logging.Write("AreaID: {0}", StyxWoW.Me.HearthstoneAreaId);
//
//
// QUEST EXAMPLES:  
// "By Any Means Necessary" (http://wowhead.com/quest=9978)
// This is a difficult quest for a number of reasons. First, a bug in the WoWclient indicates the quest
// is always completed, whether it is or not.  Thus we can not use an "IsCompleted" test to make decisions.
// Because of this bug, we cannot associate a QuestId attribute with our invocation of InteractWith.
// Second, Empoor may be in one of two states.  In one state, he'll offer us a gossip frame, and we must gossip
// and finish the ensuing fight before we can turn in the quest.  In the other state, he allows us to turn in
// the quest immediately (this happens when someone else has just fought him, and he stays in the second state
// for a while).
// To solve the problem, we arrange for InteractWith to gossip with the NPC.  If he offers the Quest frame,
// we terminate the 'gossip' behavior (via providing the InteractByQuestFrameDisposition="TerminateBehavior"
// attribute.
//      <!-- A couple of states for Empoor:
//      	1. We may need to gossip, do the fight, then turn in quest
//  	   	2. Someone else may have started and finished the fight already, and we can
//      		turn in quest immediately.
//      	NB: The WoWclient makes this quest appears complete, even though its not.
//      	So, we can't use the "IsQuestCompleted()" qualifier as a valid check.
//      	-->
//      <CustomBehavior File="InteractWith" MobId="18482" GossipOptions="1"
//      	InteractByQuestFrameDisposition="TerminateBehavior" >
//      	<HuntingGrounds>
//      		<Hotspot Name="Tuurem" X="-2037.871" Y="4377.199" Z="1.805441" />
//      		<Hotspot Name="Shattrath upper ring" X="-1955.325" Y="5029.867" Z="31.30444" />
//      		<Hotspot Name="Shattrath tunnel entrance" X="-1548.137" Y="5079.232" Z="-17.91318" />
//      	</HuntingGrounds>
//      </CustomBehavior>
//      <TurnIn QuestName="By Any Means Necessary" QuestId="9978" TurnInName="Empoor" TurnInId="18482" />
//
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
using System.Linq;
using System.Text;
using System.Xml.Linq;

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Honorbuddy.QuestBehaviorCore.XmlElements;
using JetBrains.Annotations;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Profiles;
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
    public class InteractWith : QuestBehaviorBase
    {
        #region Constructor and argument processing

        private enum ProactiveCombatStrategyType
        {
            [UsedImplicitly] NoClear,
            ClearMobsTargetingUs,
            ClearMobsThatWillAggro,
            ClearAll
        }

        private enum NavigationModeType
        {
            Mesh,
            CTM,
            [UsedImplicitly] None,
        }

        private enum QuestFrameDisposition
        {
            Accept,
            Complete,
            Continue,
            Ignore,
            TerminateBehavior,
            TerminateProfile
        }

        public InteractWith(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // NB: Core attributes are parsed by QuestBehaviorBase parent (e.g., QuestId, NonCompeteDistance, etc)

                // Primary attributes...
                MobIds = GetNumberedAttributesAsArray<int>("MobId", 0, ConstrainAs.MobId, new[] { "NpcId" });
                FactionIds = GetNumberedAttributesAsArray<int>("FactionId", 0, ConstrainAs.MobId, null );
                AuraIdsOnMob = GetNumberedAttributesAsArray<int>("AuraIdOnMob", 0, ConstrainAs.AuraId, null);
                AuraIdsMissingFromMob = GetNumberedAttributesAsArray<int>("AuraIdMissingFromMob", 0, ConstrainAs.AuraId, null);

                MobState = GetAttributeAsNullable<MobStateType>("MobState", false, null, new[] { "NpcState" }) ?? MobStateType.DontCare;
                NumOfTimes = GetAttributeAsNullable<int>("NumOfTimes", false, ConstrainAs.RepeatCount, null) ?? 1;


                // InteractionBy attributes...
                InteractByBuyingItemId = GetAttributeAsNullable("InteractByBuyingItemId", false, ConstrainAs.ItemId, null)
                    ?? GetAttributeAsNullable<int>("BuyItemId", false, ConstrainAs.ItemId, null) /*Legacy name--don't use */
                    ?? 0;
                InteractByGossipOptions = GetAttributeAsArray<int>("InteractByGossipOptions", false, new ConstrainTo.Domain<int>(-1, 10), null, null);
                if (InteractByGossipOptions.Length <= 0)
                    { InteractByGossipOptions = GetAttributeAsArray<int>("GossipOptions", false, new ConstrainTo.Domain<int>(-1, 10), new[] { "GossipOption" }, null); } /*Legacy name--don't use */
                InteractByLooting = GetAttributeAsNullable<bool>("InteractByLooting", false, null, null)
                    ?? GetAttributeAsNullable<bool>("Loot", false, null, null) /* Legacy name--don't use*/
                    ?? false;
                InteractByQuestFrameAction = GetAttributeAsNullable<QuestFrameDisposition>("InteractByQuestFrameDisposition", false, null, null)
                    ?? QuestFrameDisposition.TerminateProfile;
                InteractByUsingItemId = GetAttributeAsNullable<int>("InteractByUsingItemId", false, ConstrainAs.ItemId, null) ?? 0;


                // Tunables...
                BuyItemCount = GetAttributeAsNullable<int>("BuyItemCount", false, ConstrainAs.CollectionCount, null) ?? 1;
                CollectionDistance = GetAttributeAsNullable<double>("CollectionDistance", false, ConstrainAs.Range, null) ?? 100;
                HuntingGroundCenter = GetAttributeAsNullable<WoWPoint>("", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;
                IgnoreCombat = GetAttributeAsNullable<bool>("IgnoreCombat", false, null, null) ?? false;
                IgnoreLoSToTarget = GetAttributeAsNullable<bool>("IgnoreLoSToTarget", false, null, null) ?? false;
                KeepTargetSelected = GetAttributeAsNullable<bool>("KeepTargetSelected", false, null, null) ?? false;
                MobHpPercentLeft = GetAttributeAsNullable<double>("MobHpPercentLeft", false, ConstrainAs.Percent, new[] { "HpLeftAmount" }) ?? 100.0;
                NotMoving = GetAttributeAsNullable<bool>("NotMoving", false, null, null) ?? false;
                PreInteractMountStrategy = GetAttributeAsNullable<MountStrategyType>("PreInteractMountStrategy", false, null, null)
                    ?? MountStrategyType.None;
                ProactiveCombatStrategy = GetAttributeAsNullable<ProactiveCombatStrategyType>("ProactiveCombatStrategy", false, null, null)
                    ?? ProactiveCombatStrategyType.ClearAll;
                RangeMax = GetAttributeAsNullable<double>("Range", false, ConstrainAs.Range, null) ?? 4.0;
                RangeMin = GetAttributeAsNullable<double>("MinRange", false, ConstrainAs.Range, null) ?? 0.0;
                WaitForNpcs = GetAttributeAsNullable<bool>("WaitForNpcs", false, null, null) ?? true;
                WaitTime = GetAttributeAsNullable<int>("WaitTime", false, ConstrainAs.Milliseconds, null) ?? 0;            

                // Deprecated attributes...
                InteractByBuyingItemInSlotNum = GetAttributeAsNullable<int>("InteractByBuyingItemInSlotNum", false, new ConstrainTo.Domain<int>(-1, 100), new [] { "BuySlot" }) ?? -1;
                GetAttributeAsNullable<MobType>("ObjectType", false, null, new[] { "MobType" }); // Deprecated--no longer used
                var navigationMode = GetAttributeAsNullable<NavigationModeType>("Nav", false, null, new[] { "Navigation" });
                if (navigationMode != null)
                {
                    MovementBy =
                        (navigationMode == NavigationModeType.CTM) ? MovementByType.ClickToMoveOnly
                        : (navigationMode == NavigationModeType.Mesh) ? MovementByType.NavigatorPreferred
                        : MovementByType.None;
                }

                // Pre-processing into a form we can use directly...
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
        private int[] AuraIdsOnMob { get; set; }
        private int[] AuraIdsMissingFromMob { get; set; }
        private int BuyItemCount { get; set; }
        private double CollectionDistance { get; set; }
        private int[] FactionIds { get; set; }
        private WoWPoint HuntingGroundCenter { get; set; }
        private bool IgnoreCombat { get; set; }
        private bool IgnoreLoSToTarget { get; set; }

        private int InteractByBuyingItemId { get; set; }
        private int InteractByBuyingItemInSlotNum { get; set; }
        private int[] InteractByGossipOptions { get; set; }
        private int InteractByUsingItemId { get; set; }
        private bool InteractByLooting { get; set; }
        private bool KeepTargetSelected { get; set; }
        private double MobHpPercentLeft { get; set; }
        private int[] MobIds { get; set; }
        private MobStateType MobState { get; set; }
        private bool NotMoving { get; set; }
        private int NumOfTimes { get; set; }
        private QuestFrameDisposition InteractByQuestFrameAction { get; set; }
        private MountStrategyType PreInteractMountStrategy { get; set; }
        private ProactiveCombatStrategyType ProactiveCombatStrategy { get; set; }
        private double RangeMax { get; set; }
        private double RangeMin { get; set; }
        private bool WaitForNpcs { get; set; }
        private int WaitTime { get; set; }


        protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
        {
            UsageCheck_DeprecatedAttribute(xElement,
                (InteractByBuyingItemInSlotNum != -1),
                "InteractByBuyingItemInSlotNum/BuySlot", 
                context => "The InteractByBuyingItemInSlotNum/BuySlot attributes have been deprecated.\n"
                            + "Please replace them with InteractByBuyingItemId attribute."
                            + "Your InteractByBuyingItemInSlotNum/BuySlot attribute will still be honored, but it may yield unexpected surprises,"
                            + " if the vendor is offering seasonal or other such items.");

            UsageCheck_DeprecatedAttribute(xElement,
                Args.Keys.Contains("Nav"),
                "Nav",
                context => string.Format("Automatically converted Nav=\"{0}\" attribute into MovementBy=\"{1}\"."
                                        + "  Please update profile to use MovementBy, instead.",
                                        Args["Nav"],
                                        MovementBy));

            UsageCheck_DeprecatedAttribute(xElement,
                Args.Keys.Contains("ObjectType"),
                "ObjectType",
                context => "The ObjectType attribute is no longer used by InteractWith."
                            + "  You may safely remove it from the profile call to the InteractWith behavior.");
        }


        protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
        {
            UsageCheck_SemanticCoherency(xElement,
                (!MobIds.Any() && !FactionIds.Any()),
                context => "You must specify one or more MobIdN, or one or more FactionIdN, or both.");

            UsageCheck_SemanticCoherency(xElement,
                Args.Keys.Contains("MinRange") && ((RangeMax - RangeMin) < RangeMinMaxEpsilon),
                context => string.Format("Range({0}) must be at least {1} greater than MinRange({2}).",
                                        RangeMax, RangeMinMaxEpsilon, RangeMin)); 
        }
        #endregion


        #region Private and Convenience variables
        private const int AttemptCountMax = 7;
        private enum BindingEventStateType
        {
            BindingEventUnhooked,
            BindingEventHooked,
            BindingEventFired,
        }

        private BindingEventStateType BindingEventState { get; set; }
        private int Counter { get; set; }
        private string GoalText
        {
            get
            {
                return
                    string.Format("Interacting {0} {1}",
                        ((InteractByUsingItemId > 0)
                            ? string.Format("by using {0} on", GetItemNameFromId(InteractByUsingItemId))
                            : "with"),
                        string.Join(", ", MobIds.Select(m => GetObjectNameFromId(m)).Distinct()));
            }
        }
        private int GossipPageIndex { get; set; }
        private HuntingGroundsType HuntingGrounds { get; set; }
        private int InteractAttemptCount { get; set; }
        private WoWItem ItemToUse { get; set; }
        private const double RangeMinMaxEpsilon = 3.0;
        private WoWUnit SelectedAliveTarget { get; set; }
        private WoWObject SelectedInteractTarget { get; set; }

        private readonly WaitTimer _waitTimerAfterInteracting = new WaitTimer(TimeSpan.Zero);
        private readonly WaitTimer _waitTimerForItemToAppear = new WaitTimer(TimeSpan.Zero);
        private WaitTimer _timerToReachDestination = null;

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id$"); } }
        public override string SubversionRevision { get { return ("$Revision$"); } }
        #endregion


        #region Destructor, Dispose, and cleanup
        ~InteractWith()
        {
            Dispose(false);
        }
        #endregion


        #region Overrides of CustomForcedBehavior
        // CreateBehavior supplied by QuestBehaviorBase.
        // Instead, provide CreateMainBehavior definition.


        // Dispose supplied by QuestBehaviorBase.
        // Instead, provide CreateMainBehavior definition.


        // IsDone provided by QuestBehaviorBase.
        // Call the QuestBehaviorBase.BehaviorDone() method when you want to indicate your behavior is complete.

        public override void OnStart()
        {
            // Hunting ground processing...
            // NB: We had to defer this processing from the constructor, because XElement isn't available
            // to parse child XML nodes until OnStart() is called.
            HuntingGrounds = HuntingGroundsType.GetOrCreate(Element, "HuntingGrounds", HuntingGroundCenter);
            IsAttributeProblem |= HuntingGrounds.IsAttributeProblem;          

            // Let QuestBehaviorBase do basic initializaion of the behavior, deal with bad or deprecated attributes,
            // capture configuration state, install BT hooks, etc.  This will also update the goal text.
            OnStart_QuestBehaviorCore(GoalText);

            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (!IsDone)
            {
                // Setup settings to prevent interference with your behavior --
                // These settings will be automatically restored by QuestBehaviorBase when Dispose is called
                // by Honorbuddy, or the bot is stopped.

                // We need to disable 'pull distance' while this behavior in progress.
                // If we don't, HBcore tends to retarget things and pull them while we are in the middle of
                // an activity.
                CharacterSettings.Instance.PullDistance = 0;

                _waitTimerAfterInteracting.WaitTime = TimeSpan.FromMilliseconds(WaitTime);

                // NB: This clumsiness is because Honorbuddy can launch and start using the behavior before the pokey
                // WoWclient manages to put the item into our bag after accepting a quest.  This delay waits
                // for the item to show up, if its going to.
                _waitTimerForItemToAppear.WaitTime = TimeSpan.FromSeconds(5);
                _waitTimerForItemToAppear.Reset();
            }
        }

        #endregion


        #region Main Behaviors
        protected override Composite CreateBehavior_CombatMain()
        {
            return new Decorator(context => !IsDone,
                new PrioritySelector(
                    // If a mob is targeting us, deal with it immediately, so our interact actions won't be interrupted...
                    new Decorator(context => !IgnoreCombat
                                            && !Me.IsFlying
                                            && ((ProactiveCombatStrategy == ProactiveCombatStrategyType.ClearAll)
                                                || (ProactiveCombatStrategy == ProactiveCombatStrategyType.ClearMobsTargetingUs)),
                       UtilityBehaviorPS_SpankMobTargetingUs(context => FindBestInteractTargets(MobState) /*excluded units*/
                                                                        .Where(o => o.ToUnit() != null)
                                                                        .Select(o => o.ToUnit()))),

                    new Decorator(context => !Me.Combat,
                        UtilityBehaviorPS_HealAndRest()),
                            
                    // Delay, if necessary...
                    // NB: We must do this prior to checking for 'behavior done'.  Otherwise, profiles
                    // that don't have an associated quest, and put the behavior in a <While> loop will not behave
                    // as the profile writer expects.  They expect the delay to be executed if the interaction
                    // succeeded.
                    new Decorator(context => !_waitTimerAfterInteracting.IsFinished,
                        new Action(context =>
                        {
                            TreeRoot.StatusText = string.Format("Completing {0} wait of {1}",
                                PrettyTime(TimeSpan.FromSeconds((int)_waitTimerAfterInteracting.TimeLeft.TotalSeconds)),
                                PrettyTime(_waitTimerAfterInteracting.WaitTime));
                        })),

                    // Counter is used to determine 'done'...
                    // NB: If QuestObjectiveIndex was specified, we don't care what counter is.  Instead,
                    // we want the objective to complete.
                    new Decorator(context => (QuestObjectiveIndex == 0) && (Counter >= NumOfTimes),
                        new Action(context => { BehaviorDone(); })),
                    
                    // If quest is done, behavior is done...
                    new Decorator(context => IsDone,
                        new Action(context => { BehaviorDone(); })),

                    // If WoWclient has not placed items in our bag, wait for it...
                    // If it doesn't show up in a reasonable time, we're done.
                    new Decorator(context => (InteractByUsingItemId > 0) && !IsViable(ItemToUse),
                        new PrioritySelector(
                            new Decorator(context => _waitTimerForItemToAppear.IsFinished,
                                new Action(context =>
                                {
                                    LogProfileError(BuildMessageWithContext(Element,
                                        "Unable to locate {0} in our bags--terminating behavior.",
                                        GetItemNameFromId(InteractByUsingItemId)));
                                    BehaviorDone();                                    
                                })),
                            new Decorator(context => ItemToUse == null,
                                new Action(context =>
                                {
                                    TreeRoot.StatusText = string.Format("Waiting {0} for {1} to arrive in our bags.",
                                        PrettyTime(_waitTimerForItemToAppear.WaitTime),
                                        GetItemNameFromId(InteractByUsingItemId));
                                    ItemToUse = Me.CarriedItems.FirstOrDefault(i => (i.Entry == InteractByUsingItemId));
                                }))
                        )),
                        
                    // If we've an alive target to make dead, go make it so...
                    new Decorator(context => IsViable(SelectedAliveTarget) && SelectedAliveTarget.IsAlive,
                        new Sequence(
                            new Action(context => { LogInfo("Going after 'alive' {0} to make it 'dead'", SelectedAliveTarget.Name); }),
                            UtilityBehaviorPS_SpankMob(context => SelectedAliveTarget))),

                    // If interact target no longer meets qualifications, try to find another...
                    new Decorator(context => !IsInteractNeeded(SelectedInteractTarget, MobState),
                        new PrioritySelector(
                            new Action(context =>
                            {
                                CloseOpenFrames();
                                Me.ClearTarget();
                                SelectedInteractTarget = FindBestInteractTargets(MobState).FirstOrDefault();
                                _timerToReachDestination = null;
                                InteractAttemptCount = 0;
                                if (SelectedInteractTarget != null)
                                    { UpdateGoalText(GoalText); }

                                // If we're looking for 'dead' targets, and there are none...
                                // But, there are 'alive' targets that fit the bill, go convert the 'alive' ones to 'dead'.
                                if ((SelectedInteractTarget == null) && (MobState == MobStateType.Dead))
                                {
                                    SelectedAliveTarget = FindBestInteractTargets(MobStateType.Alive).FirstOrDefault() as WoWUnit;
                                    if (SelectedAliveTarget != null)
                                        { return RunStatus.Success; }
                                }

                                return RunStatus.Failure;   // fall through
                            }),

                            // No mobs in immediate vicinity...
                            // NB: if the terminateBehaviorIfNoTargetsProvider argument evaluates to 'true', calling
                            // this sub-behavior will terminate the overall behavior.
                            new Decorator(context => !IsViable(SelectedInteractTarget),
                                UtilityBehaviorPS_NoMobsAtCurrentWaypoint(
                                    context => HuntingGrounds,
                                    context => !WaitForNpcs,
                                    context => MobIds.Select(m => GetObjectNameFromId(m)).Distinct(),
                                    context => Debug_BuildExclusions()))
                        )),

                    #region Deal with mob we've selected for interaction...
                    new Decorator(context => IsViableForInteracting(SelectedInteractTarget),
                        new PrioritySelector(
                            // Place upper bound on time allowed to reach destination...
                            new Decorator(context => _timerToReachDestination == null,
                                new Action(context =>
                                {
                                    _timerToReachDestination = new WaitTimer(CalculateMaxTimeToDestination(SelectedInteractTarget.Location));
                                    _timerToReachDestination.Reset();
                                    return RunStatus.Failure; // fall through
                                })),

                            new Decorator(context => _timerToReachDestination.IsFinished,
                                new Action(context =>
                                {
                                    var blacklistTime = BlacklistInteractTarget(SelectedInteractTarget);
                                    LogWarning("Taking too long to reach {0}--blacklisting for {1}",
                                        SelectedInteractTarget.SafeName(),
                                        PrettyTime(blacklistTime));
                                    _timerToReachDestination = null;
                                })),

                            // Take out any nearby mobs that will aggro, if we get close to destination...
                            new Decorator(context => !IgnoreCombat
                                                    && !Me.IsFlying
                                                    && ((ProactiveCombatStrategy == ProactiveCombatStrategyType.ClearAll)
                                                        || (ProactiveCombatStrategy == ProactiveCombatStrategyType.ClearMobsThatWillAggro)),
                                UtilityBehaviorPS_SpankMobWithinAggroRange(context => SelectedInteractTarget.Location,
                                                                           context => SelectedInteractTarget.InteractRange,
                                                                           () => MobIds /*excluded mobs*/)),

                            SubBehaviorPS_HandleLootFrame(),
                            SubBehaviorPS_HandleGossipFrame(),
                            SubBehaviorPS_HandleMerchantFrame(),
                            SubBehaviorPS_HandleQuestFrame(),
                            SubBehaviorPS_HandleFramesComplete(),

                            // If anything in the frame handling made the target no longer viable for interacting,
                            // go find a new target...
                            // NB: This mostly happens when NPCs close the gossip dialog on their end and walk away
                            // or despawn, or outright go 'non viable' after the chat.
                            new Decorator(context => !IsViableForInteracting(SelectedInteractTarget),
                                new ActionAlwaysSucceed()),

                            #region Interact with, or use item on, selected target...
                            // Show user the target that's interesting to us...
                            // NB: If we've been in combat, our current target may remain on a dead body or other object.
                            // This will confuse HB as we try to interact with an object.  So, we either guarantee that
                            // a target is selected if it is a WoWUnit, or clear the target if its an object.
                            new Decorator(context => Me.CurrentTarget != SelectedInteractTarget,
                                new Action(context =>
                                {
                                    WoWUnit wowUnit = SelectedInteractTarget.ToUnit();
                                    if ((wowUnit != null) && ((wowUnit.Distance < RangeMax) || IsInLineOfSight(wowUnit)))
                                        { wowUnit.Target(); }
                                    else if (Me.CurrentTarget != null)
                                        { Me.ClearTarget(); }

                                    return RunStatus.Failure;   // fall through
                                })),                                       

                            SubBehaviorPS_DoMoveToTarget(),

                            // NB: We do the move before waiting for the cooldown.  The hope is that for most items, the
                            // cooldown will have elapsed by the time we get within range of the next target.
                            new Decorator(context => (ItemToUse != null) && (ItemToUse.CooldownTimeLeft > TimeSpan.Zero),
                                new Action(context =>
                                {
                                    TreeRoot.StatusText = string.Format("Waiting for {0} cooldown ({1} remaining)",
                                        ItemToUse.Name,
                                        PrettyTime(TimeSpan.FromSeconds((int)ItemToUse.CooldownTimeLeft.TotalSeconds)));
                                })),

                            new Sequence(
                                // If we've exceeded our maximum allowed attempts to interact, blacklist mob for a while...
                                new Action(context =>
                                {
                                    if (++InteractAttemptCount > AttemptCountMax)
                                    {
                                        var blacklistTime = TimeSpan.FromSeconds(180);

                                        LogWarning("Exceeded our maximum count({0}) at attempted interactions--blacklisting {1} for {2}",
                                            AttemptCountMax, SelectedInteractTarget.SafeName(), PrettyTime(blacklistTime));
                                        BlacklistInteractTarget(SelectedInteractTarget);
                                        return RunStatus.Failure;
                                    }

                                    return RunStatus.Success;
                                }),

                                // Interact by item use...
                                new DecoratorContinue(context => ItemToUse != null,
                                    UtilityBehaviorSeq_UseItemOn(context => ItemToUse, context => SelectedInteractTarget)),

                                // Interact by right-click...
                                new DecoratorContinue(context => ItemToUse == null,
                                    UtilityBehaviorSeq_InteractWith(context => SelectedInteractTarget, context => false)),

                                // Record usage...
                                new Action(context =>
                                {
                                    LogDeveloperInfo("{0} {1}",
                                        ((ItemToUse != null)
                                            ? string.Format("Used {0}({1}) on", ItemToUse.Name, ItemToUse.Entry)
                                            : "Interacted with"),
                                        GetName(SelectedInteractTarget));
                                }),
                            
                                // Peg tally, if follow-up actions not expected...
                                new DecoratorContinue(context => !IsFrameExpectedFromInteraction(),
                                    new Action(context =>
                                    {
                                        BlacklistInteractTarget(SelectedInteractTarget);
                                        _waitTimerAfterInteracting.Reset();
                                        ++Counter;

                                        if (IsClearTargetNeeded(SelectedInteractTarget))
                                            { Me.ClearTarget(); }

                                        SelectedInteractTarget = null;
                                    }))
                            )
                            #endregion
                    ))
                    #endregion
                ));
        }


        protected override Composite CreateBehavior_CombatOnly()
        {
            return new PrioritySelector(
                new Action(context =>
                {
                    CloseOpenFrames(true);
                    // Force recalculation of time to reach destination after combat completes...
                    _timerToReachDestination = null;
                    return RunStatus.Failure;   // fall through
                }),
                    
                // If current target is not attackable, blacklist it...
                new Decorator(context => (Me.CurrentTarget != null) && !Me.CurrentTarget.Attackable,
                    new Action(context =>
                    {
                        Blacklist.Add(Me.CurrentTarget, BlacklistFlags.Combat, TimeSpan.FromSeconds(120));
                        Me.ClearTarget();
                    })),

                // If we're ignoring combat, deprive Combat Routine of chance to run...
                new Decorator(context => IgnoreCombat,
                    new ActionAlwaysSucceed())
            );
        }


        protected override Composite CreateBehavior_DeathMain()
        {
            return new PrioritySelector(
                // empty, for now
                );
        }


        protected override Composite CreateMainBehavior()
        {
            return new PrioritySelector(
                // empty, for now
                );
        }
        #endregion


        #region Sub-Behaviors
        private Composite SubBehaviorPS_DoMoveToTarget()
        {
            return
                new PrioritySelector(
                    new Decorator(context => IsDistanceGainNeeded(SelectedInteractTarget),
                        UtilityBehaviorPS_MoveTo(
                            context => GetPointToGainDistance(SelectedInteractTarget, RangeMin),
                            context => string.Format("gain distance from {0} (id:{1}, dist:{2:F1}/{3:F1})",
                                GetName(SelectedInteractTarget),
                                SelectedInteractTarget.Entry,
                                SelectedInteractTarget.Distance,
                                RangeMin))),

                    new Decorator(context => IsDistanceCloseNeeded(SelectedInteractTarget),
                        new PrioritySelector(
                            new Decorator(context => MovementBy == MovementByType.NavigatorOnly
                                                        && !Navigator.CanNavigateFully(StyxWoW.Me.Location, SelectedInteractTarget.Location)
                                                        && (!Me.IsFlying || !Me.IsOnTransport),
                                new Action(context =>
                                {
                                    TimeSpan blacklistDuration = BlacklistInteractTarget(SelectedInteractTarget);
                                    TreeRoot.StatusText = string.Format("Unable to navigate to {0} (dist: {1:F1})--blacklisting for {2}.",
                                                                        GetName(SelectedInteractTarget), SelectedInteractTarget.Distance, blacklistDuration);
                                })),

                            new Decorator(context => MovementBy == MovementByType.None,
                                new Action(ret =>
                                {
                                    TimeSpan blacklistDuration = BlacklistInteractTarget(SelectedInteractTarget);
                                    TreeRoot.StatusText = string.Format("{0} is out of range (dist: {1:F1})--blacklisting for {2}.",
                                                                        GetName(SelectedInteractTarget), SelectedInteractTarget.Distance, blacklistDuration);
                                    BehaviorDone();
                                })),

                            UtilityBehaviorPS_MoveTo(
                                context => SelectedInteractTarget.Location,
                                context => string.Format("interact with {0} (id: {1}, dist: {2:F1}{3})",
                                                        GetName(SelectedInteractTarget),
                                                        SelectedInteractTarget.Entry,
                                                        SelectedInteractTarget.Distance,
                                                        IsInLineOfSight(SelectedInteractTarget) ? "" : ", noLoS"))
                        )),

                    // If we expect to gossip, and mob in combat and offers no gossip, help mob...
                    // NB: Mobs frequently will not offer their gossip options while they are in combat.
                    new Decorator(context => (InteractByGossipOptions.Length > 0)
                                                && (SelectedInteractTarget.ToUnit() != null)
                                                && (SelectedInteractTarget.ToUnit().Combat),
                        UtilityBehaviorPS_SpankMob(context => SelectedInteractTarget.ToUnit().CurrentTarget)),

                    // Prep to interact...
                    UtilityBehaviorPS_MoveStop(),
                    UtilityBehaviorPS_ExecuteMountStrategy(context => PreInteractMountStrategy),
                    UtilityBehaviorPS_FaceMob(context => SelectedInteractTarget)          
            );
        }


        private Composite SubBehaviorPS_HandleFramesComplete()
        {
            return new PrioritySelector(
                new Decorator(context => GossipFrame.Instance.IsVisible
                                        || MerchantFrame.Instance.IsVisible
                                        || QuestFrame.Instance.IsVisible
                                        || TaxiFrame.Instance.IsVisible
                                        || TrainerFrame.Instance.IsVisible,
                    new Action(context =>
                    {
                        TreeRoot.StatusText = string.Format("Interaction with {0} complete.", GetName(SelectedInteractTarget));
                        CloseOpenFrames();
                        _waitTimerAfterInteracting.Reset();
                        ++Counter;

                        // Some mobs go non-viable immediately after interacting with them...
                        if (IsViable(SelectedInteractTarget))
                        {
                            BlacklistInteractTarget(SelectedInteractTarget);
                            if (IsClearTargetNeeded(SelectedInteractTarget))
                                { Me.ClearTarget(); }
                        }

                        SelectedInteractTarget = null;
                    })),

                // Some mobs go non-viable immediately after interacting with them...
                // For instance, interacting with mobs that give you automatic taxi rides.
                // We must guard against trying to interact with them further.
                new Decorator(context => !IsViable(SelectedInteractTarget),
                    new Action(context => { SelectedInteractTarget = null; }))
                );
        }


        private Composite SubBehaviorPS_HandleGossipFrame()
        {
            return
                new PrioritySelector(
                    new Decorator(context => GossipFrame.Instance.IsVisible,
                        new Sequence(
                            new DecoratorContinue(context => InteractByGossipOptions.Length > 0,
                                new Sequence(
                                    new Action(context =>
                                    {
                                        TreeRoot.StatusText = string.Format("Gossiping with {0}", GetName(SelectedInteractTarget));
                                        BindingEventState = BindingEventStateType.BindingEventUnhooked;
                                        GossipPageIndex = 0;
                                    }),

                                    new WhileLoop(RunStatus.Success, context => !IsDone && (GossipPageIndex < InteractByGossipOptions.Length),
                                        new Action(context =>
                                        {
                                            GossipEntry gossipEntry;

                                            try
                                            {
                                                // NB: This clumsiness is because HB defines the default GossipEntry with an
                                                // an Index of 0.  Since this is a valid gossip option index, this leaves us with
                                                // no way to determine the difference between the 'default' value, and a valid
                                                // value. So, we try to get the gossip entry using First() (vs. FirstOrDefault()),
                                                // and if an exception gets thrown, we know the entry is not present.
                                                if (GossipFrame.Instance.GossipOptionEntries == null)
                                                    { throw new InvalidOperationException(); }
                                                    
                                                gossipEntry = GossipFrame.Instance.GossipOptionEntries
                                                    .First(o => o.Index == InteractByGossipOptions[GossipPageIndex]);
                                            }
                                            catch (InvalidOperationException)
                                            {
                                                LogError(
                                                    "{0} is not offering gossip option {1} on page {2}."
                                                    + "  Did competing player alter NPC state?"
                                                    + "  Did you stop/start Honorbuddy?"
                                                    + "  Terminating behavior.",
                                                    GetName(SelectedInteractTarget),
                                                    InteractByGossipOptions[GossipPageIndex] +1,
                                                    GossipPageIndex +1);
                                                CloseOpenFrames();
                                                Me.ClearTarget();
                                                BehaviorDone();
                                                return;
                                            }

                                            // Log the gossip option we're about to take...
                                            LogDeveloperInfo("Selecting Gossip Option({0}) on page {1}: \"{2}\"",
                                                gossipEntry.Index +1, GossipPageIndex +1, gossipEntry.Text);

                                            // If Innkeeper 'binding option', arrange to confirm ensuing popup...
                                            if (gossipEntry.Type == GossipEntry.GossipEntryType.Binder)
                                            {
                                                BindingEventState = BindingEventStateType.BindingEventHooked;
                                                Lua.Events.AttachEvent("CONFIRM_BINDER", HandleConfirmForBindingAtInn);
                                            }

                                            GossipFrame.Instance.SelectGossipOption(InteractByGossipOptions[GossipPageIndex]);
                                            ++GossipPageIndex;

                                            // If gossip is complete, claim credit...
                                            // Frequently, the last gossip option in a chain will start a fight.  If this happens,
                                            // and we don't claim credit, the behavior will hang trying to re-try a gossip with the NPC,
                                            // and the NPC doesn't want to gossip any more.
                                            if (GossipPageIndex >= InteractByGossipOptions.Length)
                                            {
                                                LogDeveloperInfo("Gossip with {0} complete.", GetName(SelectedInteractTarget));

                                                // NB: Some merchants require that we gossip with them before purchase.
                                                // If the caller has also specified a "buy item", then we're not done yet.
                                                if ((InteractByBuyingItemId <= 0) && (InteractByBuyingItemInSlotNum <= 0))
                                                {
                                                    BlacklistInteractTarget(SelectedInteractTarget);
                                                    _waitTimerAfterInteracting.Reset();
                                                    ++Counter;
                                                }
                                            }
                                        }),
                                        new WaitContinue(Delay_AfterInteraction, context => !GossipFrame.Instance.IsVisible, new ActionAlwaysSucceed())
                                    ),

                                    // If the NPC pops down the dialog for us, or goes non-viable after gossip...
                                    // Go ahead and blacklist it, so we don't try to interact again.
                                    new DecoratorContinue(context => !GossipFrame.Instance.IsVisible || !IsViable(SelectedInteractTarget),
                                        new Action(context =>
                                        {
                                            TreeRoot.StatusText = string.Format("Gossip with {0} complete.", GetName(SelectedInteractTarget));
                                            _waitTimerAfterInteracting.Reset();
                                            ++Counter;

                                            BlacklistInteractTarget(SelectedInteractTarget);
                                            if (IsClearTargetNeeded(SelectedInteractTarget))
                                                { Me.ClearTarget(); }
                                        }))
                                )),

                            // Only a problem if Gossip frame, and not also another frame type...
                            new DecoratorContinue(context => (InteractByGossipOptions.Length <= 0) && GossipFrame.Instance.IsVisible && !IsMultipleFramesVisible(),
                                new Action(context => { LogWarning("[PROFILE ERROR]: Gossip frame not expected--ignoring."); })),

                            // Wait, not WaitContinue, because we want to 'fall through' when delay is complete...
                            new Wait(Delay_AfterInteraction, context => false, new ActionAlwaysFail())
                        )),

                    // Tell user if he is now bound to a new location...
                    new Decorator(context => BindingEventState != BindingEventStateType.BindingEventUnhooked,
                        new Wait(TimeSpan.FromSeconds(10),
                            context => BindingEventState == BindingEventStateType.BindingEventFired,
                            new Sequence(
                                new WaitContinue(Delay_AfterInteraction, context => false, new ActionAlwaysSucceed()),
                                new Action(context =>
                                {
                                    Lua.DoString("ConfirmBinder(); StaticPopup_Hide('CONFIRM_BINDER')");
                                }),
                                // NB: We give the WoWclient a little time to register our new location
                                // before asking it "where is our hearthstone set?"
                                new WaitContinue(TimeSpan.FromMilliseconds(1000), context => false, new ActionAlwaysSucceed()),
                                new Action(context =>
                                {
                                    var boundLocation = Lua.GetReturnVal<string>("return GetBindLocation()", 0);

                                    LogInfo("You are now bound at {0} Inn in {1}({2})",
                                        (IsViable(SelectedInteractTarget) ? GetName(SelectedInteractTarget) : "the"),
                                        boundLocation,
                                        Me.HearthstoneAreaId);

                                    BindingEventState = BindingEventStateType.BindingEventUnhooked;
                                    Lua.Events.DetachEvent("CONFIRM_BINDER", HandleConfirmForBindingAtInn);
                                })
                            )))
                );
        }


        private Composite SubBehaviorPS_HandleLootFrame()
        {
            return
                // Nothing really special for us to do here.  HBcore will take care of 'normal' looting.
                // And looting objects through "interaction" is usually nothing more than right-clicking
                // on the object and a loot frame is not even produced.  But this is here, just in case
                // a loot frame is produced, and HBcore doesn't deal with it.
                new Decorator(context => LootFrame.Instance.IsVisible,
                    new Sequence(
                        new Action(context =>
                        {
                            TreeRoot.StatusText = string.Format("Looting {0}", GetName(SelectedInteractTarget));
                            LootFrame.Instance.LootAll();
                            return RunStatus.Failure; // fall through
                        }),
                        // Wait, not WaitContinue, because we want to 'fall through' when delay is complete...
                        new Wait(Delay_AfterInteraction, context => false, new ActionAlwaysFail())
                    ));
        }


        private Composite SubBehaviorPS_HandleMerchantFrame()
        {
            return
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
                                        LogProfileError("{0} does not appear to carry ItemId({1})--abandoning transaction.",
                                            GetName(SelectedInteractTarget), InteractByBuyingItemId);
                                    }
                                    else
                                    {
                                        LogProfileError("{0} does not have an item to sell in slot #{1}--abandoning transaction.",
                                            GetName(SelectedInteractTarget), InteractByBuyingItemInSlotNum);
                                    }
                                }
                                else if ((item.BuyPrice * (ulong)BuyItemCount) > Me.Copper)
                                {
                                    LogProfileError("Toon does not have enough money to purchase {0} (qty: {1})"
                                        + "--(requires: {2}, have: {3})--abandoning transaction.",
                                        item.Name, BuyItemCount, PrettyMoney(item.BuyPrice * (ulong)BuyItemCount), PrettyMoney(Me.Copper));
                                }
                                else if ((item.NumAvailable != /*unlimited*/-1) && (item.NumAvailable < BuyItemCount))
                                {
                                    LogProfileError("{0} only has {1} units of {2} (we need {3})--abandoning transaction.",
                                        GetName(SelectedInteractTarget), item.NumAvailable, item.Name, BuyItemCount);
                                }
                                else
                                {
                                    LogInfo("Buying {0} (qty: {1}) from {2}", item.Name, BuyItemCount, GetName(SelectedInteractTarget));
                                    MerchantFrame.Instance.BuyItem(item.Index, BuyItemCount);
                                }
                                // NB: We do not blacklist merchants.
                                ++Counter;
                            }

                            else if (!IsMultipleFramesVisible())
                                { LogWarning("[PROFILE ERROR] Merchant frame not expected--ignoring."); }
                        }),

                        // Wait, not WaitContinue, because we want to 'fall through' when delay is complete...
                        new Wait(Delay_AfterInteraction, context => false, new ActionAlwaysFail())
                    ));
        }


        private Composite SubBehaviorPS_HandleQuestFrame()
        {
            return
                // Side-effect of interacting with some NPCs for quests...
                new Decorator(context => QuestFrame.Instance.IsVisible,
                    new Sequence(
                        new DecoratorContinue(context => InteractByQuestFrameAction == QuestFrameDisposition.Accept,
                            new Action(context => { QuestFrame.Instance.AcceptQuest(); })),
                        new DecoratorContinue(context => InteractByQuestFrameAction == QuestFrameDisposition.Complete,
                            new Action(context => { QuestFrame.Instance.CompleteQuest(); })),
                        new DecoratorContinue(context => InteractByQuestFrameAction == QuestFrameDisposition.Continue,
                            new Action(context => { QuestFrame.Instance.ClickContinue(); })),
                            
                        // If the NPC pops down the dialog for us, or goes non-viable after gossip...
                        // Go ahead and blacklist it, so we don't try to interact again.
                        new DecoratorContinue(context => !QuestFrame.Instance.IsVisible || !IsViable(SelectedInteractTarget),
                            new Action(context =>
                            {
                                TreeRoot.StatusText = string.Format("Quest accept from {0} complete.", GetName(SelectedInteractTarget));
                                _waitTimerAfterInteracting.Reset();
                                ++Counter;

                                BlacklistInteractTarget(SelectedInteractTarget);
                                if (IsClearTargetNeeded(SelectedInteractTarget))
                                    { Me.ClearTarget(); }
                            })),

                        new DecoratorContinue(context => InteractByQuestFrameAction == QuestFrameDisposition.Ignore,
                            new Action(context => { QuestFrame.Instance.Close(); })),
                        new DecoratorContinue(context => InteractByQuestFrameAction == QuestFrameDisposition.TerminateBehavior && !IsMultipleFramesVisible(),
                            new Action(context =>
                            {
                                LogDeveloperInfo("Behavior Done--due to {0} providing a quest frame, and InteractByQuestFrameDisposition=TerminateBehavior",
                                    GetName(SelectedInteractTarget));
                                CloseOpenFrames(true);
                                BehaviorDone();
                            })),
                        new DecoratorContinue(context => InteractByQuestFrameAction == QuestFrameDisposition.TerminateProfile && !IsMultipleFramesVisible(),
                            new Action(context =>
                            {
                                LogProfileError("{0} provided an unexpected Quest frame--terminating profile."
                                    + "  Please provide an appropriate InteractByQuestFrameDisposition attribute to instruct"
                                    + " the behavior how to handle this situation.",
                                    GetName(SelectedInteractTarget));
                                CloseOpenFrames(true);
                                BehaviorDone();
                            })),

                        // Wait, not WaitContinue, because we want to 'fall through' when delay is complete...
                        new Wait(Delay_AfterInteraction, context => false, new ActionAlwaysFail())
                    ));
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
            bool isShortBlacklist = (wowUnit != null) && IsSharedWorldResource(wowUnit);
            TimeSpan blacklistDuration = TimeSpan.FromSeconds(isShortBlacklist ? 30 : 180);

            BlacklistForInteracting(selectedTarget, blacklistDuration);
            return blacklistDuration;
        }


        private void CloseOpenFrames(bool forceClose = false)
        {
            if (forceClose || IsClearTargetNeeded(SelectedInteractTarget))
            {
                if (GossipFrame.Instance.IsVisible)
                    { GossipFrame.Instance.Close(); }
                if (MerchantFrame.Instance.IsVisible)
                    { MerchantFrame.Instance.Close(); }
                if (QuestFrame.Instance.IsVisible)
                    { QuestFrame.Instance.Close(); }
                if (TaxiFrame.Instance.IsVisible)
                {
                    // HBCORE BUG: TaxiFrame does not have a close method
                    // new Action(context => { TaxiFrame.Instance.Close(); }),
                    LogError("Unable to close Taxi Frame");
                }
                if (TrainerFrame.Instance.IsVisible)
                    { TrainerFrame.Instance.Close(); }
            }
        }


        /// <summary> Current object we should interact with.</summary>
        /// <value> The object.</value>
        private IEnumerable<WoWObject> FindBestInteractTargets(MobStateType mobState)
        {
            double collectionDistanceSqr = CollectionDistance * CollectionDistance;
            bool isMeSwimmingOrFlying = Me.IsSwimming || Me.IsFlying;
            WoWPoint myLocation = Me.Location;
            const double minionWeighting = 1000;

            Func<WoWObject, bool>   isInterestingToUs =
                ((wowObject) =>
                {
                    WoWGameObject wowGameObject = wowObject.ToGameObject();
                    WoWUnit wowUnit = wowObject.ToUnit();

                    return (((wowGameObject != null) || (wowUnit != null)) && MobIds.Contains((int)wowObject.Entry))
                            || ((wowUnit != null) && FactionIds.Contains((int)wowUnit.FactionId));
                });

            var entities = 
                from wowObject in ObjectManager.GetObjectsOfType<WoWObject>(true, false)
                where
                    IsViable(wowObject)
                    && isInterestingToUs(wowObject)
                    && (wowObject.DistanceSqr < collectionDistanceSqr)
                    && IsInteractNeeded(wowObject, mobState)
                    && ((MovementBy != MovementByType.NavigatorOnly) || Navigator.CanNavigateFully(myLocation, wowObject.Location))
                orderby
                    // NB: we use the 'surface path' to calculate distance to mobs.
                    // This is important in tunnels/caves where mobs may be within X feet of us,
                    // but they are below or above us, and we have to traverse much tunnel to get to them.
                    (isMeSwimmingOrFlying
                        ? myLocation.Distance(wowObject.Location)
                        : myLocation.SurfacePathDistance(wowObject.Location))

                    // Fix for undead-quest (and maybe some more), where the targets can be minions...
                    + (Me.Minions.Contains(wowObject) ? minionWeighting : 1)
                select wowObject;

            return entities;
        }


        /// <summary>
        /// The SelectedInteractTarget may no longer be viable after interacting with it.
        /// For instance, the NPC may disappear, or if the toon was forced on a taxi ride
        /// as a result of the gossip, the SelectedInteractTarget will no longer be viable
        /// once we land.
        /// </summary>
        private string GetName(WoWObject target)
        {
            return IsViable(target) ? target.Name
                       : (target == SelectedInteractTarget) ? "selected target"
                       : (target.ToUnit() != null) ? "unit"
                       : "object";
        }


        private void HandleConfirmForBindingAtInn(object sender, LuaEventArgs args)
        {
            BindingEventState = BindingEventStateType.BindingEventFired;
        }
       

        private bool IsClearTargetNeeded(WoWObject wowObject)
        {
            if (wowObject == null)
                { return false; }

            WoWUnit wowUnit = wowObject.ToUnit();

            return
                (wowUnit != null)
                && (Me.CurrentTarget == wowUnit)
                && !KeepTargetSelected;
        }


        private bool IsDistanceCloseNeeded(WoWObject wowObject)
        {
            double targetDistance = MovementObserver.Location.Distance(wowObject.Location);

            bool canInteract =
                (ItemToUse != null)
                ? (targetDistance <= RangeMax) && (IgnoreLoSToTarget || IsInLineOfSight(wowObject))    // Items need LoS to use
                : (targetDistance <= RangeMax);     // Interactions just require being within range

            return !canInteract;
        }


        private bool IsDistanceGainNeeded(WoWObject wowObject)
        {
            double targetDistance = MovementObserver.Location.Distance(wowObject.Location);

            return targetDistance < RangeMin;
        }


        private bool IsFrameExpectedFromInteraction()
        {
            // NB: InteractByLoot is nothing more than a normal "right click" activity
            // on something. If something is normally 'lootable', HBcore will deal with it.
            return
                (InteractByBuyingItemId > 0)
                || (InteractByBuyingItemInSlotNum > -1)
                || (InteractByGossipOptions.Length > 0)
                || (InteractByQuestFrameAction == QuestFrameDisposition.Accept)
                || (InteractByQuestFrameAction == QuestFrameDisposition.Complete)
                || (InteractByQuestFrameAction == QuestFrameDisposition.Continue);
        }


        // 24Feb2013-08:11UTC chinajade
        private bool IsInteractNeeded(WoWObject wowObject, MobStateType mobState)
        {
            if (wowObject == null)
                { return false; }

            bool isViableForInteracting = IsViableForInteracting(wowObject);
            WoWUnit wowUnit = wowObject.ToUnit();

            // We're done, if not a WoWUnit...
            if (wowUnit == null)
                { return isViableForInteracting; }
                
            // Additional qualifiers for WoWUnits...        
            return
                isViableForInteracting
                && (!NotMoving || !wowUnit.IsMoving)
                // Many times, units can't gossip unti they're out of combat.  So, assume they can gossip if they are in combat...
                // Once out of combat, we can re-evaluate whether this was a good choice or not.w
                && ((InteractByGossipOptions.Length <= 0) || wowUnit.Combat || wowUnit.CanGossip)
                && ((AuraIdsOnMob.Length <= 0) || wowUnit.GetAllAuras().Any(a => AuraIdsOnMob.Contains(a.SpellId)))
                && ((AuraIdsMissingFromMob.Length <= 0) || !wowUnit.GetAllAuras().Any(a => AuraIdsMissingFromMob.Contains(a.SpellId)))
                && IsMob_StateTypeMatch(wowObject, mobState, MobHpPercentLeft);
        }

        
        private bool IsMultipleFramesVisible()
        {
            int score =
                (GossipFrame.Instance.IsVisible ? 1 : 0)
                + (MerchantFrame.Instance.IsVisible ? 1 : 0)
                + (QuestFrame.Instance.IsVisible ? 1 : 0)
                + (TaxiFrame.Instance.IsVisible ? 1 : 0)
                + (TrainerFrame.Instance.IsVisible ? 1 : 0);

            return score > 1;
        }
        #endregion


        #region Debug
        private string Debug_BuildExclusions()
        {
            IEnumerable<WoWObject> interactCandidates =
                from wowObject in ObjectManager.GetObjectsOfType<WoWObject>(true)
                where
                    IsViable(wowObject)
                    && MobIds.Contains((int)wowObject.Entry)
                select wowObject;

            var excludedUnitReasons = new StringBuilder();

            foreach (var wowObject in interactCandidates)
            {
                excludedUnitReasons.Append("    ");
                excludedUnitReasons.Append(Debug_TellWhyExcluded(wowObject));
                excludedUnitReasons.AppendLine();
            }

            if (excludedUnitReasons.Length > 0)
            {
                excludedUnitReasons.Insert(0, string.Format("{0}Excluded Units:{0}",
                    Environment.NewLine));
            }

            return excludedUnitReasons.ToString();
        }


        private string Debug_TellWhyExcluded(WoWObject wowObject)
        {
            var reasons = new List<string>();

            if (!IsViable(wowObject))
                { return "[NotViable]"; }

            if (wowObject.Distance > CollectionDistance)
                { reasons.Add(string.Format("ExceedsCollectionDistance({0})", CollectionDistance)); }

            if ((MovementBy == MovementByType.NavigatorOnly) && !Navigator.CanNavigateFully(Me.Location, wowObject.Location))
                { reasons.Add("NotMeshNavigable"); }

            if (IsBlacklistedForInteraction(wowObject))
                { reasons.Add("Blacklisted"); }

            if (IgnoreMobsInBlackspots && Targeting.IsTooNearBlackspot(ProfileManager.CurrentProfile.Blackspots, wowObject.Location))
                { reasons.Add(string.Format("InBlackspot(object @{0})", wowObject.Location)); }

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

                if ((InteractByGossipOptions.Length > 0) && !wowUnit.CanGossip)
                    { reasons.Add("NoGossip"); }

                if (!wowUnit.IsUntagged())
                    { reasons.Add("Tagged"); }

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

                if (!IsMob_StateTypeMatch(wowUnit, MobState, MobHpPercentLeft))
                {
                    reasons.Add(MobState == MobStateType.BelowHp
                        ? string.Format("!{0}({1}%)", MobState, MobHpPercentLeft)
                        : string.Format("!{0}", MobState));
                }
            }

            return string.Format("{0} [{1}]", wowObject.Name, string.Join(",", reasons));
        }
        #endregion
    }
}
