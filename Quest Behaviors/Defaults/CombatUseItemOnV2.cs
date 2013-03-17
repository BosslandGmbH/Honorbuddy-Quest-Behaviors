// Behavior originally contributed by Chinajade
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Summary and Documentation
//
// QUICK DOX:
// COMBATUSEITEMON uses an item on a target while the toon is in combat.
// The caller can determine at what point in combat the item will be used:
//  * When the target's health drops below a certain percentage
//  * When the target is casting a particular spell
//  * When the target gains a particular aura
//  * When the target loses a particular aura
//  * When the toon gains a particular aura
//  * When the toon loses a particular aura
//  * one or more of the above happens (the conditions are OR'd together)
//
// BEHAVIOR ATTRIBUTES:
// Basic Attributes:
//      ItemId [REQUIRED]
//          This is the Id of the Item that should be used on the mobs.
//          The item must exist in the backpack; otherwise, Honorbuddy will be stopped.
//      ItemAppliesAuraId [REQUIRED, if UseItemStrategy is UseItemOncePerTarget or UseItemOncePerTargetDontDefend,
//          or if a QuestId is not provided]
//          This is the aura that the item applies to the target once the item is used.  This is our only
//          means of telling if the Item use was successful.  (If no aura was applied, we must have been
//          interrupted in trying to use it, and need to keep trying).
//      MobId1, MobId2, ... MobIdN [at least one MobId is REQUIRED]
//          Identifies the mobs on which the item should be used.
//      NumOfTimesToUseItem [REQUIRED if a QuestId is not provided; otherwise IGNORED]
//          Specifies the number of items the item should be used on te identified mobs.
//      UseWhenMeHasAuraId [at least one of the UseWhen* attributes is REQUIRED; Default: none]
//          When the toon acquires the identified AuraId, the item is used on the mob.
//          If multiple UseWhen* attributes are provided, the item is used when _any_
//          of the UseWhen* conditions go true.
//      UseWhenMeMissingAuraId [at least one of the UseWhen* attributes is REQUIRED; Default: none]
//          When the toon loses the identified AuraId, the item is used on the mob.
//          If multiple UseWhen* attributes are provided, the item is used when _any_
//          of the UseWhen* conditions go true.
//      UseWhenMobCastingSpellId [at least one of the UseWhen* attributes is REQUIRED; Default: none]
//          When the mob is casting the identified SpellId, the item is used on the mob.
//          If multiple UseWhen* attributes are provided, the item is used when _any_
//          of the UseWhen* conditions go true.
//      UseWhenMobHasAuraId [at least one of the UseWhen* attributes is REQUIRED; Default: none]
//          When the mob acquires the identified AuraId, the item is used on the mob.
//          If multiple UseWhen* attributes are provided, the item is used when _any_
//          of the UseWhen* conditions go true.
//      UseWhenMobMissingAuraId [at least one of the UseWhen* attributes is REQUIRED; Default: none]
//          When the mob loses the identified AuraId, the item is used on the mob.
//          If multiple UseWhen* attributes are provided, the item is used when _any_
//          of the UseWhen* conditions go true.
//      UseWhenMobHasHealthPercent [at least one of the UseWhen* attributes is REQUIRED; Default: 0]
//          When the mob's health drops to the identified percentage, the item is used on the mob.
//          If multiple UseWhen* attributes are provided, the item is used when _any_
//          of the UseWhen* conditions go true.
//      X/Y/Z [REQUIRED, if <HuntingGrounds> sub-element is omitted; Default: none]
//          This specifies the location of a 'safe spot' where the toon should loiter
//          while waiting for the AvoidMobId to clear the area.
//
// Quest binding:
//      QuestId [optional; Default:none]:
//      QuestCompleteRequirement [Default:NotComplete]:
//      QuestInLogRequirement [Default:InLog]:
//          A full discussion of how the Quest* attributes operate is described in
//          http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
//
// Tunables:
//      IgnoreMobsInBlackspots [optional; Default: true]
//          When true, any mobs within (or too near) a blackspot will be ignored
//          in the list of viable targets that are considered for item use.
//      MaxRangeToUseItem [optional; Default: 25.0]
//          Defines the maximum range at which the item can be used on a mob.
//          If the toon is out of range (i.e., due to a ranged pull), the toon
//          will be moved within this distance of the mob to assure the item can be used
//          immediately when the appropriate conditions are met.
//      NonCompeteDistance [optional; Default: 15.0]
//          If any player is within NonCompeteDistance of the mob of interest,
//          the mob will be discarded as a viable target.
//      RecallPetAtMobPercentHealth [optional: Default: provided by UseWhenMobHasHealthPercent]
//          Pet playing classes may need to recall their pet prematurely to prevent
//          killing the mob before the item is used.  This attribute allows for early
//          pet recall.
//      UseItemStrategy [optional; Default: UseItemOncePerTarget]
//          [Allowed values: UseItemOncePerTarget, UseItemOncePerTargetDontDefend,
//              UseItemContinuouslyOnTarget, UseItemContinuouslyOnTargetDontDefend]
//          Defines how the item is to be used on the mob:
//              UseItemOncePerTarget
//                  Uses the item on the mob.  If the mob acquires the ItemAppliesAuraId
//                  then the use is considered successful; otherwise, we keep trying
//                  to use the item on the mob.
//                  If the mob continues to attack us after a successful use of the item,
//                  we will defend ourselves to the point of killing the mob, if necessary.
//              UseItemOncePerTargetDontDefend
//                  Uses the item on the mob.  If the mob acquires the ItemAppliesAuraId
//                  then the use is considered successful; otherwise, we keep trying
//                  to use the item on the mob.
//                  If the mob continues to attack us after a successful use of the item,
//                  we will not defend ourself and move on to the next mob.
//              UseItemContinuouslyOnTarget
//                  Uses the item on the same mob everytime the item is not on cooldown.
//                  If the mob continues to attack us after a successful use of the item,
//                  we will defend ourselves to the point of killing the mob, if necessary.
//              UseItemContinuouslyOnTargetDontDefend
//                  Uses the item on the same mob everytime the item is not on cooldown.
//                  If the mob continues to attack us after a successful use of the item,
//                  we will not defend ourself to the point where the mob gives up, or
//                  we die.
//      WaitTimeAfterItemUse [optional; Default 0ms]
//          Defines the number of milliseconds to wait after the item is successfully
//          used before carrying on with the behavior on other mobs.
//
// BEHAVIOR EXTENSION ELEMENTS (goes between <CustomBehavior ...> and </CustomBehavior> tags)
// See the "Examples" section for typical usage.
//      HuntingGrounds [REQUIRED, if X/Y/Z omitted in main attributes]
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
#endregion


#region FAQs
#endregion


#region Examples
// "Camel Tow" (http://wowhead.com/quest=28352)
// Beat camels into submission (25% health), then use the Sullah's Camel Hareness (http://wowhead.com/item=67241)
// on them.  The quest is complete after doing this three times.
//      <CustomBehavior File="CombatUseItemOn" QuestId="28352" ItemId="67241" ItemAppliesAuraId="94956" MobId="51193"
//          UseWhenMobHasHealthPercent="25" MaxRangeToUseItem="15" UseItemStrategy="UseItemOncePerTargetDontDefend"
//          RecallPetAtMobPercentHealth="50" >
//          <HuntingGrounds WaypointVisitStrategy="Random" >
//              <Hotspot Name="Sullah's Sideshow" X="-8986.617" Y="677.9194" Z="177.0783" />
//              <Hotspot Name="NW of Temple of Uldum" X="-9223.614" Y="666.8814" Z="188.2858" />
//              <Hotspot Name="W of Temple of Uldum" X="-9401.372" Y="686.7933" Z="185.5984" />
//              <Hotspot Name="SW of Temple of Uldum" X="-9523.359" Y="618.4017" Z="137.2736" />
//              <Hotspot Name="S of Temple of Uldum (upper ridge)" X="-9589.536" Y="399.2485" Z="132.335" />
//              <Hotspot Name="SW of Temple of Uldum (lower ridge)" X="-9743.044" Y="579.4816" Z="75.00929" />
//          </HuntingGrounds>
//      </CustomBehavior>
//
// "Do the Imp-Possible" (http://wowhead.com/quest=28000)
// Fight Impsy.  When he is 15% health, use the Enchanted Imp Sack (http://wowhead.com/item=62899) to capture him.
//      <CustomBehavior File="CombatUseItemOn" QuestId="28000" ItemId="62899" MobId="47339"
//          ItemAppliesAuraId="88330" UseWhenMobHasHealthPercent="15" MaxRangeToUseItem="15"
//          UseItemStrategy="UseItemOncePerTargetDontDefend"
//          X="4287.259" Y="-1112.751" Z="323.6652" />
//
// "Unlimited Potential" (http://wowhead.com/quest=28351)
// Beat pygmies into submission (25% health), then use Sullah's Pygmy Pen (http://wowhead.com/item=67232)
// on them to capture.  The quest is complete after doing this five times.
//      <CustomBehavior File="CombatUseItemOn" QuestId="28351" ItemId="67232" ItemAppliesAuraId="94365" MobId="51217"
//          UseWhenMobHasHealthPercent="25" MaxRangeToUseItem="10" UseItemStrategy="UseItemOncePerTargetDontDefend" 
//          RecallPetAtMobPercentHealth="40" >
//          <HuntingGrounds WaypointVisitStrategy="Random" >
//              <Hotspot Name="NW of Temple of Uldum" X="-9223.614" Y="666.8814" Z="188.2858" />
//              <Hotspot Name="W of Temple of Uldum" X="-9401.372" Y="686.7933" Z="185.5984" />
//              <Hotspot Name="SW of Temple of Uldum" X="-9523.359" Y="618.4017" Z="137.2736" />
//              <Hotspot Name="S of Temple of Uldum (upper ridge)" X="-9589.536" Y="399.2485" Z="132.335" />
//              <Hotspot Name="SW of Temple of Uldum (lower ridge)" X="-9743.044" Y="579.4816" Z="75.00929" />
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
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.Plugins;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Styx.Bot.Quest_Behaviors.CombatUseItemOnV2
{
    public partial class CombatUseItemOnV2 : CustomForcedBehavior
    {
        #region Consructor and Argument Processing

        public enum UseItemStrategyType
        {
            UseItemOncePerTarget,
            UseItemOncePerTargetDontDefend,
            UseItemContinuouslyOnTarget,
            UseItemContinuouslyOnTargetDontDefend,
        }

        public CombatUseItemOnV2(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // Primary attributes...
                ItemId = GetAttributeAsNullable<int>("ItemId", true, ConstrainAs.ItemId, null) ?? 0;
                ItemAppliesAuraId = GetAttributeAsNullable<int>("ItemAppliesAuraId", false, ConstrainAs.AuraId, null) ?? 0;
                MobIds = GetNumberedAttributesAsArray<int>("MobId", 1, ConstrainAs.MobId, null);
                UseWhenMeHasAuraId = GetAttributeAsNullable<int>("UseWhenMeHasAuraId", false, ConstrainAs.AuraId, null) ?? 0;
                UseWhenMeMissingAuraId = GetAttributeAsNullable<int>("UseWhenMeMissingAuraId", false, ConstrainAs.AuraId, null) ?? 0;
                UseWhenMobCastingSpellId = GetAttributeAsNullable<int>("UseWhenMobCastingSpellId", false, ConstrainAs.SpellId, null) ?? 0;
                UseWhenMobHasAuraId = GetAttributeAsNullable<int>("UseWhenMobHasAuraId", false, ConstrainAs.AuraId, null) ?? 0;
                UseWhenMobMissingAuraId = GetAttributeAsNullable<int>("UseWhenMobMissingAuraId", false, ConstrainAs.AuraId, null) ?? 0;
                UseWhenMobHasHealthPercent = GetAttributeAsNullable<double>("UseWhenMobHasHealthPercent", false, ConstrainAs.Percent, null) ?? 0;

                // Either HuntingGroundCenter or <HuntingGrounds> subelement must be provided...
                // The sanity check for this is done in OnStart() since that's where we must do
                // all sub-element processing due to the way CustomForcedBehavior is architected.
                HuntingGroundCenter = GetAttributeAsNullable<WoWPoint>("", false, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;

                // Quest handling...
                QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;

                // Tunables...
                IgnoreMobsInBlackspots = GetAttributeAsNullable<bool>("IgnoreMobsInBlackspots", false, null, null) ?? true;
                MaxRangeToUseItem = GetAttributeAsNullable<double>("MaxRangeToUseItem", false, ConstrainAs.Range, null) ?? 25.0;
                NonCompeteDistance = GetAttributeAsNullable<double>("NonCompeteDistance", false, new ConstrainTo.Domain<double>(1.0, 40.0), null) ?? 15.0;
                NumOfTimesToUseItem = GetAttributeAsNullable<int>("NumOfTimesToUseItem", false, ConstrainAs.RepeatCount, null) ?? 1;
                RecallPetAtMobPercentHealth = GetAttributeAsNullable<double>("RecallPetAtMobPercentHealth", false, ConstrainAs.Percent, null) ?? UseWhenMobHasHealthPercent;
                UseItemStrategy = GetAttributeAsNullable<UseItemStrategyType>("UseItemStrategy", false, null, null) ?? UseItemStrategyType.UseItemOncePerTarget;
                WaitTimeAfterItemUse = GetAttributeAsNullable<int>("WaitTimeAfterItemUse", false, ConstrainAs.Milliseconds, null) ?? 0;


                // Semantic coherency / covariant dependency checks --
                if ((UseWhenMeHasAuraId <= 0)
                    && (UseWhenMeMissingAuraId <= 0)
                    && (UseWhenMobCastingSpellId <= 0)
                    && (UseWhenMobHasAuraId <= 0)
                    && (UseWhenMobMissingAuraId <= 0)
                    && (UseWhenMobHasHealthPercent <= 0))
                {
                    LogError("One or more of the following attributes must be specified:\n"
                                + "UseWhenMeHasAuraId, UseWhenMeMissingAuraId, UseWhenMobCastingSpellId,"
                                + " UseWhenMobHasAuraId, UseWhenMobMissingAuraId, UseWhenMobHasHpPercentLeft");
                    IsAttributeProblem = true;
                }


                if ((ItemAppliesAuraId <= 0)
                    && ((UseItemStrategy == UseItemStrategyType.UseItemOncePerTarget)
                        || (UseItemStrategy == UseItemStrategyType.UseItemOncePerTargetDontDefend)
                        || (QuestId <= 0)))
                {
                    LogError("For a UseItemStrategy of {0}, ItemAppliesAuraId must be specified",
                        UseItemStrategy);
                    IsAttributeProblem = true;
                }


                if ((QuestId > 0) && (NumOfTimesToUseItem > 1))
                {
                    LogError("The NumOfTimesToUseItem attribute should only be specified when behavior"
                        + "  is not associated with a quest (i.e., the QuestId attribute is not specified)");
                    IsAttributeProblem = true;
                }
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it can be quickly
                // resolved.
                LogError("[MAINTENANCE PROBLEM]: " + except.Message
                        + "\nFROM HERE:\n"
                        + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }


        // Variables for Attributes provided by caller
        public WoWPoint HuntingGroundCenter { get; private set; }
        public bool IgnoreMobsInBlackspots { get; private set; }
        public int ItemId { get; private set; }
        public int ItemAppliesAuraId { get; private set; }
        public double MaxRangeToUseItem { get; private set; }
        public int[] MobIds { get; private set; }
        public double NonCompeteDistance { get; private set; }
        public int NumOfTimesToUseItem { get; private set; }
        public double RecallPetAtMobPercentHealth { get; private set; }
        public UseItemStrategyType UseItemStrategy { get; private set; }
        public int UseWhenMobCastingSpellId { get; private set; }
        public int UseWhenMeMissingAuraId { get; private set; }
        public int UseWhenMeHasAuraId { get; private set; }
        public int UseWhenMobMissingAuraId { get; private set; }
        public int UseWhenMobHasAuraId { get; private set; }
        public double UseWhenMobHasHealthPercent { get; private set; }
        public int WaitTimeAfterItemUse { get; private set; }

        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }


        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id: CombatUseItemOn.cs 273 2013-01-22 23:00:43Z natfoth $"); } }
        public override string SubversionRevision { get { return ("$Revision: 273 $"); } }
        #endregion


        #region Private and Convenience variables
        public delegate WoWPoint LocationDelegate(object context);
        public delegate string StringDelegate(object context);
        public delegate double RangeDelegate(object context);
        public delegate WoWUnit WoWUnitDelegate(object context);

        private int Counter { get; set; }
        private WaypointType CurrentHuntingGroundWaypoint { get; set; }
        private readonly TimeSpan Delay_WoWClientMovementThrottle = TimeSpan.FromMilliseconds(100);
        private HuntingGroundType HuntingGrounds { get; set; }
        private WoWItem ItemToUse { get; set; }
        private LocalPlayer Me { get { return StyxWoW.Me; } }
        private WoWUnit SelectedTarget { get; set; }

        private Composite _behaviorTreeHook_CombatMain = null;
        private Composite _behaviorTreeHook_CombatOnly = null;
        private Composite _behaviorTreeHook_DeathMain = null;
        private Composite _behaviorTreeHook_Main = null;
        private ConfigMemento _configMemento = null;
        private bool _isBehaviorDone = false;
        private bool _isDisposed = false;
        public static Random _random = new Random((int)DateTime.Now.Ticks);
        #endregion


        #region Destructor, Dispose, and cleanup
        ~CombatUseItemOnV2()
        {
            Dispose(false);
        }


        // 24Feb2013-08:10UTC chinajade
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

                // NB: we don't unhook _behaviorTreeHook_Main
                // This was installed when HB created the behavior, and its up to HB to unhook it

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

                // Restore configuration...
                if (_configMemento != null)
                {
                    _configMemento.Dispose();
                    _configMemento = null;
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
                return _isBehaviorDone     // normal completion
                        || !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete);
            }
        }


        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

            if ((QuestId != 0) && (quest == null))
            {
                LogError("This behavior has been associated with QuestId({0}), but the quest is not in our log", QuestId);
                IsAttributeProblem = true;
            }

            // Hunting ground processing...
            IList<HuntingGroundType> tmpHuntingGrounds = null;
            IsAttributeProblem |= XmlUtil_ParseSubelements<HuntingGroundType>(this, HuntingGroundType.Create, Element, "HuntingGrounds", out tmpHuntingGrounds);

            if (!IsAttributeProblem)
            {
                HuntingGrounds = (tmpHuntingGrounds != null) ? tmpHuntingGrounds.FirstOrDefault() : null;

                if (HuntingGrounds == null)
                    { HuntingGrounds = HuntingGroundType.Create(this, new XElement("HuntingGrounds")); }

                // If user provided a hunting ground center, add it to our hunting ground waypoints...
                if (HuntingGroundCenter != WoWPoint.Empty)
                    { HuntingGrounds.AppendWaypoint(HuntingGroundCenter); }


                if (HuntingGrounds.Waypoints.Count() <= 0)
                {
                    LogError("No X/Y/Z attributes or <HuntingGrounds> sub-element has been specified.");
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
                // The ConfigMemento() class captures the user's existing configuration.
                // After its captured, we can change the configuration however needed.
                // When the memento is dispose'd, the user's original configuration is restored.
                // More info about how the ConfigMemento applies to saving and restoring user configuration
                // can be found here...
                //     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_Saving_and_Restoring_User_Configuration
                _configMemento = new ConfigMemento();

                BotEvents.OnBotStop += BotEvents_OnBotStop;

                // Disable any settings that may interfere with the escort --
                // When we escort, we don't want to be distracted by other things.
                // NOTE: these settings are restored to their normal values when the behavior completes
                // or the bot is stopped.

                TreeRoot.GoalText = string.Format(
                    "{0}: \"{1}\"",
                    this.GetType().Name,
                    ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress (no associated quest)"));

                ItemToUse = Me.CarriedItems.FirstOrDefault(i => (i.Entry == ItemId));
                if (ItemToUse == null)
                {
                    LogError("[PROFILE ERROR] Unable to locate ItemId({0}) in our bags", ItemId);
                    TreeRoot.Stop();
                    _isBehaviorDone = true;
                }

                _behaviorTreeHook_CombatMain = CreateBehavior_CombatMain();
                TreeHooks.Instance.InsertHook("Combat_Main", 0, _behaviorTreeHook_CombatMain);
                _behaviorTreeHook_CombatOnly = CreateBehavior_CombatOnly();
                TreeHooks.Instance.InsertHook("Combat_Only", 0, _behaviorTreeHook_CombatOnly);
                _behaviorTreeHook_DeathMain = CreateBehavior_DeathMain();
                TreeHooks.Instance.InsertHook("Death_Main", 0, _behaviorTreeHook_DeathMain);

                CurrentHuntingGroundWaypoint = HuntingGrounds.FindNearestWaypoint(Me.Location);
            }
        }
        #endregion


        #region Main Behaviors
        
        private Composite CreateBehavior_CombatMain()
        {
            return new Decorator(context => IsViable(SelectedTarget),
                new PrioritySelector(
                    // If the target has the Item's Aura...
                    new Decorator(context => SelectedTarget.HasAura(ItemAppliesAuraId),
                        new PrioritySelector(
                            // Count our success if no associated quest...
                            new Decorator(context => QuestId == 0,
                                new Action(context => { ++Counter; })),

                            // Wait additional time requested by profile writer...
                            new Wait(TimeSpan.FromMilliseconds(WaitTimeAfterItemUse),
                                context => false,
                                new ActionAlwaysSucceed()),

                            new Action(context =>
                            {
                                // If we can only use the item once per target, blacklist this target from subsequent selection...
                                if ((UseItemStrategy == UseItemStrategyType.UseItemOncePerTarget)
                                    || (UseItemStrategy == UseItemStrategyType.UseItemOncePerTargetDontDefend))
                                {
                                    Blacklist.Add(SelectedTarget, BlacklistFlags.Node, TimeSpan.FromSeconds(180));
                                }

                                // If we can't defend ourselves from the target, blacklist it for combat and move on...
                                if ((UseItemStrategy == UseItemStrategyType.UseItemContinuouslyOnTargetDontDefend)
                                    || (UseItemStrategy == UseItemStrategyType.UseItemOncePerTargetDontDefend))
                                {
                                    Blacklist.Add(SelectedTarget, BlacklistFlags.Combat, TimeSpan.FromSeconds(180));
                                    BotPoi.Clear();
                                    Me.ClearTarget();
                                    SelectedTarget = null;
                                }
                            })
                        )),
                        
                    // If any mob aggros on us while are heading to deal with SelectedTarget, finish aggro mob first...
                    // NB: We don't want to wait until the mob hits us and we get in combat;
                    // otherwise, we may wind up at our target with multiple mobs to fight.
                    new Decorator(context => !Me.Combat,
                        new PrioritySelector(aggrodMobContext => FindMobsTargetingMeOrPet().FirstOrDefault(),
                            new Decorator(aggrodMobContext => (aggrodMobContext != null),
                                UtilityBehavior_GetMobsAttention(aggrodMobContext => (WoWUnit)aggrodMobContext))
                        ))
                )
            );
        }


        private Composite CreateBehavior_CombatOnly()
        {
            return new Decorator(context => IsViable(SelectedTarget),
                new PrioritySelector(
                    // If we're fighting some other mob, make certain pet is helping...
                    new Decorator(context => Me.CurrentTarget != SelectedTarget,
                        UtilityBehaviorPS_PetSetStance(context => "Defensive")),
                    
                    // Go after our chosen target...
                    // NB: If someone else tagged the mob, it will no longer be viable.
                    new Decorator(context => !IsDone && (Me.CurrentTarget == SelectedTarget),
                        new PrioritySelector(

                            // Recall pet, if necessary...
                            new Decorator(context => (SelectedTarget.HealthPercent < RecallPetAtMobPercentHealth)
                                                    && (Me.GotAlivePet && (Me.Pet.CurrentTarget == SelectedTarget)),
                                new PrioritySelector(
                                    new Action(context =>
                                    {
                                        LogInfo("Recalling Pet from '{0}' (health: {1:F1})",
                                            SelectedTarget.Name, SelectedTarget.HealthPercent);
                                        return RunStatus.Failure;
                                    }),
                                    UtilityBehaviorPS_PetActionFollow(),
                                    UtilityBehaviorPS_PetSetStance(context => "Passive")
                                )),

                            // If we are beyond the max range allowed to use the item, move within range...
                            new Decorator(context => (Me.CurrentTarget == SelectedTarget)
                                                        && (SelectedTarget.Distance > MaxRangeToUseItem),
                                UtilityBehavior_MoveTo(
                                    context => SelectedTarget.Location,
                                    context => string.Format("within {0} feet of {1}", MaxRangeToUseItem, SelectedTarget.Name))),
                            
                            // If time to use the item, do so...
                            new Decorator(context => IsViable(ItemToUse) && IsUseItemNeeded(SelectedTarget),
                                new PrioritySelector(

                                    new Decorator(context => !IsOnCooldown(ItemToUse),
                                        new Sequence(
                                            new Action(context =>
                                            {
                                                // We use LUA to stop casting, since SpellManage.StopCasting() doesn't seem to work...
                                                Lua.DoString("SpellStopCasting()");
                                                WoWMovement.MoveStop();

                                                LogInfo("Using '{0}' on '{1}' (health: {2:F1})",
                                                    ItemToUse.Name, SelectedTarget.Name, SelectedTarget.HealthPercent);

                                                ItemToUse.Use(SelectedTarget.Guid);
                                                // NB: Neither the HB API nor the WoW API give us a way to determine
                                                // if an item was successfully used (i.e., we weren't interrupted while
                                                // we were trying to use it).
                                            })
                                        )),

                                    // If we're not allowed to defend after using the item, prevent combat routine from running...
                                    new Decorator(context => (UseItemStrategy == UseItemStrategyType.UseItemContinuouslyOnTargetDontDefend)
                                                            || (UseItemStrategy == UseItemStrategyType.UseItemOncePerTargetDontDefend),
                                        new ActionAlwaysSucceed())
                                ))
                        ))
                    ));
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

                // If quest is done, behavior is done...
                new Decorator(context => IsDone,
                    new Action(context =>
                    {
                        _isBehaviorDone = true;
                        LogInfo("Finished");
                    })),

                // Done with no quest?
                new Decorator(context => (QuestId == 0) && (Counter >= NumOfTimesToUseItem),
                    new Action(context => { _isBehaviorDone = true; })),

                // If item is no longer viable to use, warn user and we're done...
                new Decorator(context => !IsViable(ItemToUse),
                    new Action(context =>
                    {
                        LogError("We no longer have a viable Item({0}) to use--terminating", ItemId);
                        TreeRoot.Stop();
                        _isBehaviorDone = true;
                    })),

                // If no viable target, find a new mob to harass...
                new Decorator(context => !IsViable(SelectedTarget),
                    new PrioritySelector(
                        new Action(context =>
                        {
                            Me.ClearTarget();
                            SelectedTarget = FindBestTarget();

                            // Target selected mob as feedback to user...
                            if ((SelectedTarget != null) && (Me.CurrentTarget != SelectedTarget))
                                { SelectedTarget.Target(); }

                            return RunStatus.Failure;   // fall through
                        }),

                        // If we couldn't find a mob, move back to center of hunting grounds...
                        new Decorator(context => SelectedTarget == null,
                            new PrioritySelector(
                                new Decorator(context => Me.Location.Distance(CurrentHuntingGroundWaypoint.Location) <= CurrentHuntingGroundWaypoint.Radius,
                                    new Action(context => { CurrentHuntingGroundWaypoint = HuntingGrounds.FindNextWaypoint(CurrentHuntingGroundWaypoint.Location); })),

                                UtilityBehavior_MoveTo(
                                    context => CurrentHuntingGroundWaypoint.Location,
                                    context => string.IsNullOrEmpty(CurrentHuntingGroundWaypoint.Name)
                                               ? "to next hunting ground waypoint"
                                               : string.Format("to hunting ground waypoint '{0}'", CurrentHuntingGroundWaypoint.Name)
                                    ),

                                new Decorator(context => Me.Location.Distance(HuntingGroundCenter) <= Navigator.PathPrecision,
                                    new Action(context => { LogInfo("Waiting for mobs to respawn."); }))
                            ))
                    )),

                // Pick a fight, if needed...
                new Decorator(context => !Me.Combat && IsViable(SelectedTarget),
                    UtilityBehavior_GetMobsAttention(context => SelectedTarget))
            );
        }
        #endregion


        #region Helpers

        // NB: We abuse BlacklistFlags.Node to blacklist targets with which
        // we don't want to try using an item on.
        public WoWUnit FindBestTarget()
        {
            return
               (from unit in FindUnitsFromIds(MobIds)
                where
                    !unit.HasAura(ItemAppliesAuraId)
                    && (FindPlayersNearby(unit.Location, NonCompeteDistance).Count() <= 0)
                    && !Blacklist.Contains(unit, BlacklistFlags.Node)
                    && (!IgnoreMobsInBlackspots
                        || (IgnoreMobsInBlackspots
                            && !Targeting.IsTooNearBlackspot(ProfileManager.CurrentProfile.Blackspots, unit.Location)))
                orderby unit.Distance
                select unit)
                .FirstOrDefault();
        }


        // 25Feb2013-12:50UTC chinajade
        private IEnumerable<WoWUnit> FindMobsTargetingMeOrPet()
        {
            return
                from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
                where
                    IsViable(unit)
                    && !unit.IsFriendly
                    && ((unit.CurrentTarget == Me)
                        || (Me.GotAlivePet && unit.CurrentTarget == Me.Pet))
                select unit;
        }

        
        // 25Feb2013-12:50UTC chinajade
        private IEnumerable<WoWPlayer> FindPlayersNearby(WoWPoint location, double radius)
        {
            return
                from player in ObjectManager.GetObjectsOfType<WoWPlayer>()
                where
                    player.IsAlive
                    && player.Location.Distance(location) < radius
                select player;
        }


        // 24Feb2013-08:11UTC chinajade
        private IEnumerable<WoWUnit> FindUnitsFromIds(IEnumerable<int> unitIds)
        {
            ContractRequires(unitIds != null, () => "unitIds argument may not be null");

            return
                from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
                where
                    IsViable(unit)
                    && unitIds.Contains((int)unit.Entry)
                    && (unit.TappedByAllThreatLists || !unit.TaggedByOther)
                select unit;
        }


        private string GetMobNameFromId(int wowUnitId)
        {
            WoWUnit wowUnit = FindUnitsFromIds(new int[] { wowUnitId }).FirstOrDefault();

            return (wowUnit != null)
                ? wowUnit.Name
                : string.Format("MobId({0})", wowUnitId);
        }


        private bool IsOnCooldown(WoWItem wowItem)
        {
            if (wowItem == null)
                { return false; }

            return wowItem.CooldownTimeLeft > TimeSpan.Zero;
        }


        // 24Feb2013-08:11UTC chinajade
        private bool IsQuestObjectiveComplete(int questId, int objectiveIndex)
        {
            if (Me.QuestLog.GetQuestById((uint)questId) == null)
                { return false; }

            int questLogIndex = Lua.GetReturnVal<int>(string.Format("return GetQuestLogIndexByID({0})", questId), 0);

            return
                Lua.GetReturnVal<bool>(string.Format("return GetQuestLogLeaderBoard({0},{1})", objectiveIndex, questLogIndex), 2);
        }


        private bool IsUseItemNeeded(WoWUnit target)
        {
            if (!IsViable(target))
                { return false; }

            if (target.HasAura(ItemAppliesAuraId))
                { return false; }

            return
                ((UseWhenMeHasAuraId > 0) && Me.HasAura(UseWhenMeHasAuraId))
                || ((UseWhenMeMissingAuraId > 0) && !Me.HasAura(UseWhenMeMissingAuraId))
                || ((UseWhenMobCastingSpellId > 0) && (target.CastingSpellId == UseWhenMobCastingSpellId))
                || ((UseWhenMobHasAuraId > 0) && target.HasAura(UseWhenMobHasAuraId))
                || ((UseWhenMobMissingAuraId > 0) && !target.HasAura(UseWhenMobMissingAuraId))
                || ((UseWhenMobHasHealthPercent > 0) && (target.HealthPercent <= UseWhenMobHasHealthPercent));
        }


        private bool IsViable(WoWItem wowItem)
        {
            return
                (wowItem != null)
                && wowItem.IsValid;
        }


        // 24Feb2013-08:11UTC chinajade
        // NB: We abuse BlacklistFlags.Node to blacklist targets with which
        // we don't want to try using an item on.
        private bool IsViable(WoWUnit wowUnit)
        {
            return
                (wowUnit != null)
                && wowUnit.IsValid
                && wowUnit.IsAlive
                && (wowUnit.TappedByAllThreatLists || wowUnit.TaggedByMe || !wowUnit.TaggedByOther)
                && !Blacklist.Contains(wowUnit, BlacklistFlags.Node);
        }


        // 12Mar2013-08:27UTC chinajade
        private IEnumerable<T> ToEnumerable<T>(T item)
        {
            yield return item;
        }
        #endregion


        #region Utility Behaviors
        /// <summary>
        /// This behavior quits attacking the mob, once the mob is targeting us.
        /// </summary>
        // 24Feb2013-08:11UTC chinajade
        private Composite UtilityBehavior_GetMobsAttention(WoWUnitDelegate selectedTargetDelegate)
        {

            return new PrioritySelector(targetContext => selectedTargetDelegate(targetContext),
                new Decorator(targetContext => IsViable((WoWUnit)targetContext),
                    new PrioritySelector(
                        new Decorator(targetContext => !((((WoWUnit)targetContext).CurrentTarget == Me)
                                                        || (Me.GotAlivePet && ((WoWUnit)targetContext).CurrentTarget == Me.Pet)),
                            new PrioritySelector(
                                new Action(targetContext =>
                                {
                                    LogInfo("Getting attention of {0}", ((WoWUnit)targetContext).Name);
                                    return RunStatus.Failure;
                                }),
                                UtilityBehavior_SpankMob(selectedTargetDelegate)))
                    )));
        }


        private Composite UtilityBehavior_InteractWithMob(WoWUnitDelegate unitToInteract)
        {
            return new PrioritySelector(interactUnitContext => unitToInteract(interactUnitContext),
                new Decorator(interactUnitContext => IsViable((WoWUnit)interactUnitContext),
                    new PrioritySelector(
                        // Show user which unit we're going after...
                        new Decorator(interactUnitContext => Me.CurrentTarget != (WoWUnit)interactUnitContext,
                            new Action(interactUnitContext => { ((WoWUnit)interactUnitContext).Target(); })),

                        // If not within interact range, move closer...
                        new Decorator(interactUnitContext => !((WoWUnit)interactUnitContext).WithinInteractRange,
                            new Action(interactUnitContext =>
                            {
                                LogDeveloperInfo("Moving to interact with {0}", ((WoWUnit)interactUnitContext).Name);
                                Navigator.MoveTo(((WoWUnit)interactUnitContext).Location);
                            })),

                        new Decorator(interactUnitContext => Me.IsMoving,
                            new Action(interactUnitContext => { WoWMovement.MoveStop(); })),
                        new Decorator(interactUnitContext => !Me.IsFacing((WoWUnit)interactUnitContext),
                            new Action(interactUnitContext => { Me.SetFacing((WoWUnit)interactUnitContext); })),

                        // Blindly interact...
                        // Ideally, we would blacklist the unit if the interact failed.  However, the HB API
                        // provides no CanInteract() method (or equivalent) to make this determination.
                        new Action(interactUnitContext =>
                        {
                            LogDeveloperInfo("Interacting with {0}", ((WoWUnit)interactUnitContext).Name);
                            ((WoWUnit)interactUnitContext).Interact();
                            return RunStatus.Failure;
                        }),
                        new Wait(TimeSpan.FromMilliseconds(1000), context => false, new ActionAlwaysSucceed())
                    )));
        }


        private Composite UtilityBehavior_MoveTo(LocationDelegate locationDelegate,
                                                    StringDelegate locationNameDelegate,
                                                    RangeDelegate precisionDelegate = null)
        {
            ContractRequires(locationDelegate != null, () => "locationRetriever may not be null");
            ContractRequires(locationNameDelegate != null, () => "locationNameDelegate may not be null");
            precisionDelegate = precisionDelegate ?? (context => Navigator.PathPrecision);

            return new PrioritySelector(locationContext => locationDelegate(locationContext),
                new Decorator(locationContext => !Me.InVehicle && !Me.Mounted
                                                    && Mount.CanMount()
                                                    && Mount.ShouldMount((WoWPoint)locationContext),
                    new Action(locationContext => { Mount.MountUp(() => (WoWPoint)locationContext); })),

                new Decorator(locationContext => (Me.Location.Distance((WoWPoint)locationContext) > precisionDelegate(locationContext)),
                    new Sequence(
                        new Action(context =>
                        {
                            WoWPoint destination = locationDelegate(context);
                            string locationName = locationNameDelegate(context) ?? destination.ToString();

                            TreeRoot.StatusText = "Moving to " + locationName;

                            MoveResult moveResult = Navigator.MoveTo(destination);

                            // If Navigator couldn't move us, resort to click-to-move...
                            if (!((moveResult == MoveResult.Moved)
                                    || (moveResult == MoveResult.ReachedDestination)
                                    || (moveResult == MoveResult.PathGenerated)))
                            {
                                WoWMovement.ClickToMove(destination);
                            }
                        }),
                        new WaitContinue(Delay_WoWClientMovementThrottle, ret => false, new ActionAlwaysSucceed())
                    ))
                );
        }
        
        
        /// <summary>
        /// Unequivocally engages mob in combat.
        /// </summary>
        // 24Feb2013-08:11UTC chinajade
        private Composite UtilityBehavior_SpankMob(WoWUnitDelegate selectedTargetDelegate)
        {
            return new PrioritySelector(targetContext => selectedTargetDelegate(targetContext),
                new Decorator(targetContext => IsViable((WoWUnit)targetContext),
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
        #endregion


        #region Pet Control

        /// <summary>
        /// <para>Returns true if you can cast the PETACTIONNAME; otherwise, false.</para>
        /// <para>This method checks for both spell existence, and if the spell is on cooldown.</para>
        /// <para>Notes:<list type="bullet">
        /// 
        /// <item><description><para>* To return 'true', the PETACTIONNAME spell must be hot-barred!  Existence in the spellbook
        /// is insufficient.  This is a limitation of the WoWclient and HB APIs.</para></description></item>
        /// </list></para>
        /// </summary>
        /// <param name="petActionName">may not be null. Examples include: "Follow", "Attack", "Passive", "Growl".</param>
        /// <returns></returns>
        // 24Feb2013-07:42UTC chinajade
        public bool CanCastPetAction(string petActionName)
        {
            ContractRequires(!string.IsNullOrEmpty(petActionName), () => "petActionName may not be null or empty");

            WoWPetSpell petAction = Me.PetSpells.FirstOrDefault(p => p.ToString() == petActionName);
            if (petAction == null)
                { return false; }
            if ((petAction.SpellType == WoWPetSpell.PetSpellType.Spell) && (petAction.Spell == null))
                { return false; }

            return (petAction.SpellType == WoWPetSpell.PetSpellType.Spell)
                ? !petAction.Spell.Cooldown
                : true;
        }


        /// <summary>
        /// <para>Casts the PETACTIONNAME.</para>
        /// <para>Notes:<list type="bullet">
        /// <item><description><para>* The PETACTIONNAME spell must be hot-barred!  Existence in the spellbook
        /// is insufficient.  This is a limitation of the WoWclient and HB APIs.</para></description></item>
        /// <item><description><para>* If PETACTIONNAME doesn't exist, or is not hot-barred
        /// an error message is emitted.  To avoid this, use CanCastPetAction() as an entry condition to use of this method.</para></description></item>
        /// <item><description><para>* If PETACTIONNAME is on cooldown, no action is performed.</para></description></item>
        /// </list></para>
        /// </summary>
        /// <param name="petActionName">may not be null. Examples include: "Follow", "Attack", "Passive", "Growl".</param>
        // 24Feb2013-07:42UTC chinajade
        public void CastPetAction(string petActionName)
        {
            ContractRequires(!string.IsNullOrEmpty(petActionName), () => "petActionName may not be null or empty");

            WoWPetSpell petAction = Me.PetSpells.FirstOrDefault(p => p.ToString() == petActionName);
            if (petAction == null)
            {
                LogMaintenanceError("or [USER ERROR]: PetAction('{0}') is either not known, or not hot-barred.",
                    petActionName);
                return;
            }

            LogDeveloperInfo("Instructing pet to \"{0}\"", petActionName);
            Lua.DoString("CastPetAction({0})", petAction.ActionBarIndex +1);
        }


        /// <summary>
        /// <para>Casts the PETACTIONNAME on WOWUNIT.</para>
        /// <para>Notes:<list type="bullet">
        /// <item><description><para>* The PETACTIONNAME spell must be hot-barred!  Existence in the spellbook
        /// is insufficient.  This is a limitation of the WoWclient and HB APIs.</para></description></item>
        /// <item><description><para>* If PETACTIONNAME doesn't exist, or is not hot-barred
        /// an error message is emitted.  To avoid this, use CanCastPetAction() as an entry condition to use of this method.</para></description></item>
        /// <item><description><para>* If PETACTIONNAME is on cooldown, no action is performed.</para></description></item>
        /// </list></para>
        /// </summary>
        /// <param name="petActionName">may not be null. Examples include: "Follow", "Attack", "Passive", "Growl".</param>
        /// <param name="wowUnit">may not be null</param>
        // 24Feb2013-07:42UTC chinajade
        public void CastPetAction(string petActionName, WoWUnit wowUnit)
        {
            ContractRequires(!string.IsNullOrEmpty(petActionName), () => "petActionName may not be null or empty");
            ContractRequires(wowUnit != null, () => "wowUnit may not be null");

            WoWPetSpell petAction = Me.PetSpells.FirstOrDefault(p => p.ToString() == petActionName);
            if (petAction == null)
            {
                LogMaintenanceError("or [USER ERROR]: PetAction('{0}') is either not known, or not hot-barred.",
                    petActionName);
                return;
            }

            LogDeveloperInfo("Instructing pet \"{0}\" on {1}", petActionName, wowUnit.Name);
            uint originalFocus = Me.CurrentFocus;
            StyxWoW.Me.SetFocus(wowUnit);
            Lua.DoString("CastPetAction({0}, 'focus')", petAction.ActionBarIndex +1);
            StyxWoW.Me.SetFocus(originalFocus);
        }


        /// <summary>
        /// <para>Returns true if the pet is executing the PETACTIONNAME.</para>
        /// <para>The way the WoWclient works, because a PetAction is active doesn't necessarily mean it is being immediately obeyed.</para>
        /// <para>For instance, a pet's "Passive" ability may be active, but the pet may be attacking due to an explicit "Attack" command temporarily overriding it.</para>
        /// <para>During the attack, the "Passive" ability still shows active.</para>
        /// <para>Notes:<list type="bullet">
        /// <item><description><para>* The PETACTIONNAME spell must be hot-barred!  Existence in the spellbook
        /// is insufficient.  This is a limitation of the WoWclient and HB APIs.</para></description></item>
        /// <item><description><para>* If PETACTIONNAME doesn't exist, or is not hot-barred, 'false' is returned.</para></description></item>
        /// </list></para>
        /// </summary>
        /// <param name="petActionName">may not be null. Examples include: "Follow", "Attack", "Passive"</param>
        /// <returns></returns>
        // 24Feb2013-07:42UTC chinajade
        public bool IsPetActionActive(string petActionName)
        {
            ContractRequires(!string.IsNullOrEmpty(petActionName), () => "petActionName may not be null or empty");

            WoWPetSpell petAction = Me.PetSpells.FirstOrDefault(p => p.ToString() == petActionName);
            if (petAction == null)
                { return false; }

            return Lua.GetReturnVal<bool>(string.Format("return GetPetActionInfo({0})", petAction.ActionBarIndex +1), 4);
        }


        /// <summary>
        /// <para>Sends the user's pet to attack the target identified by WOWUNITDELEGATE.</para>
        /// <para>Notes:<list type="bullet">
        /// <item><description><para>* This behavior performs all appropriate checks: pet exists and is alive, target is viable and not friendly, etc.</para></description></item>
        /// <item><description><para>* The 'attack' command will continue to be issued until the pet obeys (by targeting the mob).</para></description></item>
        /// <item><description><para>* The pet's "Attack" command must be hot-barred!  Existence in the spellbook
        /// is insufficient.  This is a limitation of the WoWclient and HB APIs.</para></description></item>
        /// <item><description><para>* The returned Composite is suitable for use in a behavior tree (Priority)Selector container
        /// (i.e., placing it in a Sequence container will not yield the desired results).</para></description></item>
        /// </list></para>
        /// </summary>
        /// <param name="wowUnitDelegate">may not be null</param>
        /// <returns>a behavior tree Composite suitable for use in a (Priority)Selector container</returns>
        public Composite UtilityBehaviorPS_PetActionAttack(WoWUnitDelegate wowUnitDelegate)
        {
            ContractRequires(wowUnitDelegate != null, () => "wowUnitDelegate may not be null");

            string spellName = "Attack";

            // NB: We can't issue "Attack" directive while mounted, so don't try...
            return new Decorator(context => Me.GotAlivePet
                                            && !Me.Mounted
                                            && IsViable(wowUnitDelegate(context))
                                            && (Me.Pet.CurrentTarget != wowUnitDelegate(context))
                                            && !wowUnitDelegate(context).IsFriendly
                                            && CanCastPetAction(spellName),
                new Action(context => { CastPetAction(spellName, wowUnitDelegate(context)); }));
        }


        /// <summary>
        /// <para>Instructs the user's pet to follow its owner.</para>
        /// <para>Notes:<list type="bullet">
        /// <item><description><para>* This behavior performs all appropriate checks: pet exists and is alive, etc.</para></description></item>
        /// <item><description><para>* If the pet is attacking a mob, the 'follow' command will continue to be issued until the pet obeys.</para></description></item>
        /// <item><description><para>* The pet's "Follow" command must be hot-barred!  Existence in the spellbook
        /// is insufficient.  This is a limitation of the WoWclient and HB APIs.</para></description></item>
        /// <item><description><para>* The returned Composite is suitable for use in a behavior tree (Priority)Selector container
        /// (i.e., placing it in a Sequence container will not yield the desired results).</para></description></item>
        /// </list></para>
        /// </summary>
        /// <returns>a behavior tree Composite suitable for use in a (Priority)Selector container</returns>
        public Composite UtilityBehaviorPS_PetActionFollow()
        {
            string spellName = "Follow";

            // NB: We can't issue "Follow" directive while mounted, so don't try...
            return new Decorator(context => Me.GotAlivePet
                                            && !Me.Mounted
                                            && CanCastPetAction(spellName)
                                            && (!IsPetActionActive(spellName) || IsViable(Me.Pet.CurrentTarget)),
                new Action(context => { CastPetAction(spellName); }));
        }
        

        /// <summary>
        /// <para>Instructs the user's pet to assume one of the following stances: "Assist", "Defensive", "Passive"</para>
        /// <para>Notes:<list type="bullet">
        /// <item><description><para>* This behavior performs all appropriate checks: pet exists and is alive, etc.</para></description></item>
        /// <item><description><para>* The pet's "Assist", "Defensive", and "Passive" commands must be hot-barred!  Existence in the spellbook
        /// is insufficient.  This is a limitation of the WoWclient and HB APIs.</para></description></item>
        /// <item><description><para>* The returned Composite is suitable for use in a behavior tree (Priority)Selector container
        /// (i.e., placing it in a Sequence container will not yield the desired results).</para></description></item>
        /// </list></para>
        /// </summary>
        /// <param name="petStanceNameDelegate"></param>
        /// <returns>a behavior tree Composite suitable for use in a (Priority)Selector container</returns>
        public Composite UtilityBehaviorPS_PetSetStance(StringDelegate petStanceNameDelegate)
        {
            string[] knownStanceNames = { "Assist", "Defensive", "Passive" };

            // We can't change pet stance while mounted, so don't try...
            return new Decorator(context => !Me.Mounted,
                new PrioritySelector(petStanceNameContext => petStanceNameDelegate(petStanceNameContext),
                    new Decorator(petStanceNameContext => !knownStanceNames.Contains((string)petStanceNameContext),
                        new Action(petStanceNameContext =>
                        {
                            LogMaintenanceError("Unknown pet stance '{0}'.  Must be one of: {1}",
                                (string)petStanceNameContext,
                                string.Join(", ", knownStanceNames));
                        })),

                    new Decorator(petStanceNameContext => Me.GotAlivePet
                                                            && CanCastPetAction((string)petStanceNameContext)
                                                            && !IsPetActionActive((string)petStanceNameContext),
                        new Action(petStanceNameContext => { CastPetAction((string)petStanceNameContext); }))
                ));
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


        #region Extensions_WoWPoint
        // Cut-n-paste any Quest Behaviors/Development/Extensions_WoWPoint helper methods you need, here...
        #endregion
    }

        #region DEBUG
    public static class DebugExtensions
    {
        // 9Mar2013-07:55UTC chinajade
        public static string ToString_FullInfo(this PlayerQuest playerQuest, bool useCompactForm = false, int indentLevel = 0)
        {
            var tmp = new StringBuilder();

            if (playerQuest != null)
            {
                var indent = string.Empty.PadLeft(indentLevel);
                var fieldSeparator = useCompactForm ? " " : string.Format("\n  {0}", indent);

                tmp.AppendFormat("<PlayerQuest Key_Id=\"{0}\" Key_Name=\"{1}\"", playerQuest.Id, playerQuest.Name);
                tmp.AppendFormat("{0}CompletionText=\"{1}\"", fieldSeparator, playerQuest.CompletionText);
                tmp.AppendFormat("{0}Description=\"{1}\"", fieldSeparator, playerQuest.Description);
                tmp.AppendFormat("{0}FlagsPvP=\"{1}\"", fieldSeparator, playerQuest.FlagsPVP);
                tmp.AppendFormat("{0}Id=\"{1}\"", fieldSeparator, playerQuest.Id);
                tmp.AppendFormat("{0}InternalInfo=\"{1}\"", fieldSeparator, playerQuest.InternalInfo);
                tmp.AppendFormat("{0}IsAutoAccepted=\"{1}\"", fieldSeparator, playerQuest.IsAutoAccepted);
                tmp.AppendFormat("{0}IsCompleted=\"{1}\"", fieldSeparator, playerQuest.IsCompleted);
                tmp.AppendFormat("{0}IsDaily=\"{1}\"", fieldSeparator, playerQuest.IsDaily);
                tmp.AppendFormat("{0}IsFailed=\"{1}\"", fieldSeparator, playerQuest.IsFailed);
                tmp.AppendFormat("{0}IsPartyQuest=\"{1}\"", fieldSeparator, playerQuest.IsPartyQuest);
                tmp.AppendFormat("{0}IsSharable=\"{1}\"", fieldSeparator, playerQuest.IsShareable);
                tmp.AppendFormat("{0}IsStayAliveQuest=\"{1}\"", fieldSeparator, playerQuest.IsStayAliveQuest);
                tmp.AppendFormat("{0}IsWeekly=\"{1}\"", fieldSeparator, playerQuest.IsWeekly);
                tmp.AppendFormat("{0}Level=\"{1}\"", fieldSeparator, playerQuest.Level);
                tmp.AppendFormat("{0}Name=\"{1}\"", fieldSeparator, playerQuest.Name);
                tmp.AppendFormat("{0}NormalObjectiveRequiredCounts=\"{1}\"", fieldSeparator,
                    (playerQuest.NormalObjectiveRequiredCounts == null)
                    ? "NONE"
                    : string.Join(", ", playerQuest.NormalObjectiveRequiredCounts.Select(c => c.ToString())));
                tmp.AppendFormat("{0}Objectives=\"{1}\"", fieldSeparator,
                    (playerQuest.Objectives == null)
                    ? "NONE"
                    : string.Join(",", playerQuest.Objectives.Select(o => string.Format("{0}  \"{1}\"", fieldSeparator, o))));
                tmp.AppendFormat("{0}ObjectiveText=\"{1}\"", fieldSeparator, playerQuest.ObjectiveText);
                tmp.AppendFormat("{0}RequiredLevel=\"{1}\"", fieldSeparator, playerQuest.RequiredLevel);
                tmp.AppendFormat("{0}RewardMoney=\"{1}\"", fieldSeparator, playerQuest.RewardMoney);
                tmp.AppendFormat("{0}RewardMoneyAtMaxLevel=\"{1}\"", fieldSeparator, playerQuest.RewardMoneyAtMaxLevel);
                tmp.AppendFormat("{0}RewardNumTalentPoints=\"{1}\"", fieldSeparator, playerQuest.RewardNumTalentPoints);
                tmp.AppendFormat("{0}RewardSpell=\"{1}\"", fieldSeparator, 
                    (playerQuest.RewardSpell == null)
                    ? null
                    : ToString_FullInfo(playerQuest.RewardSpell, false, indentLevel +4));
                tmp.AppendFormat("{0}RewardSpellId=\"{1}\"", fieldSeparator, playerQuest.RewardSpellId);
                tmp.AppendFormat("{0}RewardTitleId=\"{1}\"", fieldSeparator, playerQuest.RewardTitleId);
                tmp.AppendFormat("{0}RewardXp=\"{1}\"", fieldSeparator, playerQuest.RewardXp);
                tmp.AppendFormat("{0}SubDescription=\"{1}\"", fieldSeparator, playerQuest.SubDescription);
                tmp.AppendFormat("{0}SuggestedPlayers=\"{1}\"", fieldSeparator, playerQuest.SuggestedPlayers);
                tmp.AppendFormat("{0}/>", fieldSeparator);
            }

            return tmp.ToString();
        }


        // 9Mar2013-07:55UTC chinajade
        public static string ToString_FullInfo(this Styx.WoWInternals.Quest.QuestObjective questObjective, bool useCompactForm = false, int indentLevel = 0)
        {
            var tmp = new StringBuilder();

            var indent = string.Empty.PadLeft(indentLevel);
            var fieldSeparator = useCompactForm ? " " : string.Format("\n  {0}", indent);

            tmp.AppendFormat("<QuestObjective Key_Index=\"{0}\"", questObjective.Index);
            tmp.AppendFormat("{0}Count=\"{1}\"", fieldSeparator, questObjective.Count);
            tmp.AppendFormat("{0}ID=\"{1}\"", fieldSeparator, questObjective.ID);
            tmp.AppendFormat("{0}Index=\"{1}\"", fieldSeparator, questObjective.Index);
            tmp.AppendFormat("{0}IsEmpty=\"{1}\"", fieldSeparator, questObjective.IsEmpty);
            tmp.AppendFormat("{0}Objective=\"{1}\"", fieldSeparator, questObjective.Objective);
            tmp.AppendFormat("{0}Type=\"{1}\"", fieldSeparator, questObjective.Type);
            tmp.AppendFormat("{0}/>", fieldSeparator);

            return tmp.ToString();
        }


        //  9Mar2013-07:55UTC chinajade
        public static string ToString_FullInfo(this SpellEffect spellEffect, bool useCompactForm = false, int indentLevel = 0)
        {
            var tmp = new StringBuilder();

            if (spellEffect != null)
            {
                var indent = string.Empty.PadLeft(indentLevel);
                var fieldSeparator = useCompactForm ? " " : string.Format("\n  {0}", indent);

                tmp.AppendFormat("<SpellEffect Key_TriggerSpell=\"{0}\"", spellEffect.TriggerSpell);
                tmp.AppendFormat("{0}Amplitude=\"{1}\"", fieldSeparator, spellEffect.Amplitude);
                tmp.AppendFormat("{0}AuraType=\"{1}\"", fieldSeparator, spellEffect.AuraType);
                tmp.AppendFormat("{0}BasePoints=\"{1}\"", fieldSeparator, spellEffect.BasePoints);
                tmp.AppendFormat("{0}ChainTarget=\"{1}\"", fieldSeparator, spellEffect.ChainTarget);
                tmp.AppendFormat("{0}EffectType=\"{1}\"", fieldSeparator, spellEffect.EffectType);
                tmp.AppendFormat("{0}ImplicitTargetA=\"{1}\"", fieldSeparator, spellEffect.ImplicitTargetA);
                tmp.AppendFormat("{0}ImplicitTargetB=\"{1}\"", fieldSeparator, spellEffect.ImplicitTargetB);
                tmp.AppendFormat("{0}ItemType=\"{1}\"", fieldSeparator, spellEffect.ItemType);
                tmp.AppendFormat("{0}Mechanic=\"{1}\"", fieldSeparator, spellEffect.Mechanic);
                tmp.AppendFormat("{0}MiscValueA=\"{1}\"", fieldSeparator, spellEffect.MiscValueA);
                tmp.AppendFormat("{0}MiscValueB=\"{1}\"", fieldSeparator, spellEffect.MiscValueB);
                tmp.AppendFormat("{0}MultipleValue=\"{1}\"", fieldSeparator, spellEffect.MultipleValue);
                tmp.AppendFormat("{0}PointsPerComboPoint=\"{1}\"", fieldSeparator, spellEffect.PointsPerComboPoint);
                tmp.AppendFormat("{0}RadiusIndex=\"{1}\"", fieldSeparator, spellEffect.RadiusIndex);
                tmp.AppendFormat("{0}RealPointsPerLevel=\"{1}\"", fieldSeparator, spellEffect.RadiusIndex);
                tmp.AppendFormat("{0}SpellClassMask=\"{1}\"", fieldSeparator, spellEffect.SpellClassMask);
                tmp.AppendFormat("{0}TriggerSpell=\"{1}\"", fieldSeparator, spellEffect.TriggerSpell);
                tmp.AppendFormat("{0}/>", fieldSeparator);
            }

            return tmp.ToString();
        }


        //  9Mar2013-07:55UTC chinajade
        public static string ToString_FullInfo(this WoWMissile wowMissile, bool useCompactForm = false, int indentLevel = 0)
        {
            var tmp = new StringBuilder();

            if (wowMissile != null)
            {
                var indent = string.Empty.PadLeft(indentLevel);
                var fieldSeparator = useCompactForm ? " " : string.Format("\n  {0}", indent);

                bool isInFlight = WoWMissile.InFlightMissiles.FirstOrDefault(m => m.BaseAddress == wowMissile.BaseAddress) != null;

                tmp.AppendFormat("<WoWMissile Key_Spell=\"{0}\" BaseAddress=\"0x{1:x}\"",
                    ((wowMissile.Spell == null) ? "UNKNOWN" : wowMissile.Spell.Name),
                    (wowMissile.BaseAddress));
                tmp.AppendFormat("{0}Caster=\"{1}\"", fieldSeparator,
                    (wowMissile.Caster == null) ? "UNKNOWN" : wowMissile.Caster.Name);
                tmp.AppendFormat("{0}CasterGuid=\"0x{1:x}\" <!--Me=\"0x{2:x}\" MyVehicle=\"0x{3:x}\" -->",
                    fieldSeparator, wowMissile.Caster.Guid, StyxWoW.Me.Guid, StyxWoW.Me.TransportGuid);
                tmp.AppendFormat("{0}FirePosition=\"{1}\"", fieldSeparator, wowMissile.FirePosition);
                tmp.AppendFormat("{0}Flags=\"0x{1:x}\"", fieldSeparator, wowMissile.Flags);
                tmp.AppendFormat("{0}ImpactPosition=\"{1}\" <!--dist: {2:F1}-->", fieldSeparator, wowMissile.ImpactPosition,
                    wowMissile.ImpactPosition.Distance(StyxWoW.Me.Location));
                tmp.AppendFormat("{0}IsInFlight=\"{1}\"", fieldSeparator, isInFlight);
                tmp.AppendFormat("{0}Position=\"{1}\" <!--dist: {2:F1}-->", fieldSeparator, wowMissile.Position,
                    wowMissile.Position.Distance(StyxWoW.Me.Location));
                tmp.AppendFormat("{0}Spell=\"{1}\"", fieldSeparator,
                    (wowMissile.Spell == null) ? "NONE" : wowMissile.Spell.Name);
                tmp.AppendFormat("{0}SpellId=\"{1}\"", fieldSeparator, wowMissile.SpellId);
                tmp.AppendFormat("{0}SpellVisualId=\"{1}\"", fieldSeparator, wowMissile.SpellVisualId);
                tmp.AppendFormat("{0}Target=\"{1}\"", fieldSeparator,
                    (wowMissile.Target == null) ? "NONE" : wowMissile.Target.Name);
                tmp.AppendFormat("{0}TargetGuid=\"0x{1:x}\"", fieldSeparator, wowMissile.TargetGuid);
                tmp.AppendFormat("{0}/>", fieldSeparator);
            }

            return tmp.ToString();
        }


        //  9Mar2013-07:55UTC chinajade
        public static string ToString_FullInfo(this WoWPetSpell wowPetSpell, bool useCompactForm = false, int indentLevel = 0)
        {
            StringBuilder tmp = new StringBuilder();

            if (wowPetSpell != null)
            {
                var indent = string.Empty.PadLeft(indentLevel);
                var fieldSeparator = useCompactForm ? " " : string.Format("\n  {0}", indent);

                tmp.AppendFormat("<WoWPetSpell Key_ActionBarIndex=\"{0}\"", wowPetSpell.ActionBarIndex);
                tmp.AppendFormat("{0}Action=\"{1}\"", fieldSeparator, wowPetSpell.Action);
                tmp.AppendFormat("{0}Cooldown=\"{1}\"", fieldSeparator, wowPetSpell.Cooldown);
                tmp.AppendFormat("{0}Spell=\"{1}\"", fieldSeparator,
                    (wowPetSpell.Spell == null)
                    ? "NONE"
                    : ToString_FullInfo(wowPetSpell.Spell, useCompactForm, indentLevel + 4));
                tmp.AppendFormat("{0}SpellType=\"{1}\"", fieldSeparator, wowPetSpell.SpellType);
                tmp.AppendFormat("{0}Stance=\"{1}\"", fieldSeparator, wowPetSpell.Stance);
                tmp.AppendFormat("{0}/>", fieldSeparator);
            }

            return tmp.ToString();
        }


        //  9Mar2013-07:55UTC chinajade
        public static string ToString_FullInfo(this WoWSpell wowSpell, bool useCompactForm = false, int indentLevel = 0)
        {
            StringBuilder tmp = new StringBuilder();

            if (wowSpell != null)
            {
                var indent = string.Empty.PadLeft(indentLevel);
                var fieldSeparator = useCompactForm ? " " : string.Format("\n  {0}", indent);

                tmp.AppendFormat("<WoWSpell Key_Id=\"{0}\" Key_Name=\"{1}\"", wowSpell.Id, wowSpell.Name);
                tmp.AppendFormat("{0}BaseCooldown=\"{1}\"", fieldSeparator, wowSpell.BaseCooldown);
                // tmp.AppendFormat("{0}BaseDuration=\"{1}\"", fieldSeparator, wowSpell.BaseDuration);
                tmp.AppendFormat("{0}BaseLevel=\"{1}\"", fieldSeparator, wowSpell.BaseLevel);
                tmp.AppendFormat("{0}CanCast=\"{1}\"", fieldSeparator, wowSpell.CanCast);
                tmp.AppendFormat("{0}CastTime=\"{1}\"", fieldSeparator, wowSpell.CastTime);
                tmp.AppendFormat("{0}Category=\"{1}\"", fieldSeparator, wowSpell.Category);
                tmp.AppendFormat("{0}CooldownTime=\"{1}\"", fieldSeparator, wowSpell.Cooldown);
                tmp.AppendFormat("{0}CooldownTimeLeft=\"{1}\"", fieldSeparator, wowSpell.CooldownTimeLeft);
                tmp.AppendFormat("{0}CreatesItemId=\"{1}\"", fieldSeparator, wowSpell.CreatesItemId);
                tmp.AppendFormat("{0}DispellType=\"{1}\"", fieldSeparator, wowSpell.DispelType);
                // tmp.AppendFormat("{0}DurationPerLevel=\"{1}\"", fieldSeparator, wowSpell.DurationPerLevel);
                tmp.AppendFormat("{0}HasRange=\"{1}\"", fieldSeparator, wowSpell.HasRange);
                tmp.AppendFormat("{0}Id=\"{1}\"", fieldSeparator, wowSpell.Id);
                tmp.AppendFormat("{0}IsFunnel=\"{1}\"", fieldSeparator, wowSpell.IsFunnel);
                tmp.AppendFormat("{0}IsMelee=\"{1}\"", fieldSeparator, wowSpell.IsMeleeSpell);
                tmp.AppendFormat("{0}IsSelfOnly=\"{1}\"", fieldSeparator, wowSpell.IsSelfOnlySpell);
                tmp.AppendFormat("{0}Level: {1}", fieldSeparator, wowSpell.Level);
                // tmp.AppendFormat("{0}MaxDuration=\"{1}\"", fieldSeparator, wowSpell.MaxDuration);
                tmp.AppendFormat("{0}MaxRange=\"{1}\"", fieldSeparator, wowSpell.MaxRange);
                tmp.AppendFormat("{0}MaxStackCount=\"{1}\"", fieldSeparator, wowSpell.MaxStackCount);
                tmp.AppendFormat("{0}MaxTargets=\"{1}\"", fieldSeparator, wowSpell.MaxTargets);
                tmp.AppendFormat("{0}Mechanic=\"{1}\"", fieldSeparator, wowSpell.Mechanic);
                tmp.AppendFormat("{0}MinRange=\"{1}\"", fieldSeparator, wowSpell.MinRange);
                tmp.AppendFormat("{0}Name=\"{1}\"", fieldSeparator, wowSpell.Name);
                tmp.AppendFormat("{0}PowerCost=\"{1}\"", fieldSeparator, wowSpell.PowerCost);
                tmp.AppendFormat("{0}ResearchProjectId=\"{1}\"", fieldSeparator, wowSpell.ResearchProjectId);
                tmp.AppendFormat("{0}School=\"{1}\"", fieldSeparator, wowSpell.School);
                tmp.AppendFormat("{0}SpellDescriptionVariableId=\"{1}\"", fieldSeparator, wowSpell.SpellDescriptionVariableId);

                tmp.AppendFormat("{0}SpellEffects=\"{1}\"", fieldSeparator, (wowSpell.SpellEffects.Count() == 0) ? " NONE" : "");
                foreach (var effect in wowSpell.SpellEffects)
                    { tmp.AppendFormat("{0}  {1}", fieldSeparator, ToString_FullInfo(effect, useCompactForm, indentLevel + 4)); }

                tmp.AppendFormat("{0}SpellMissileId=\"{1}\"", fieldSeparator, wowSpell.SpellMissileId);
                tmp.AppendFormat("{0}TargetType=\"0x{1:x}\"", fieldSeparator, wowSpell.TargetType);
                tmp.AppendFormat("{0}/>", fieldSeparator);
            }

            return tmp.ToString();
        }


        //  9Mar2013-07:55UTC chinajade
        public static ulong Util_AbbreviatedGuid(ulong guid)
        {
            return guid & 0x0ffffff;
        }
    }
    #endregion

}
