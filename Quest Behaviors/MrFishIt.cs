// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

#region Summary and Documentation
// QUICK DOX:
// MRFISHIT was designed to fulfill quests where you needed a certain amount
// of a particular item acquired by fishing.
//
// BEHAVIOR ATTRIBUTES:
//
// Basic Attributes:
// *** ALSO see the documentation in QuestBehaviorBase.cs.  All of the attributes it provides
// *** are available here, also.  The documentation on the attributes QuestBehaviorBase provides
// *** is _not_ repeated here, to prevent documentation inconsistencies.
//
//      CollectItemId [REQUIRED]
//          Specifies the Id of the item we want to collect.
//      PoolId [REQUIRED, if X/Y/Z is not provided;  Default: none]
//          This Pool will must  faced for each fishing cast,
//          such that the bobber always falls into the water.
//      X/Y/Z [REQUIRED, if PoolId is not provided;  Default: none]
//          This specifies the location that should be faced.  This point
//          is somewhere on the surface of the water.
//
// Tunables:
//      BaitId [optional;  Default: none]
//          Specifies the bait item id. If not specified then any bait in bags is used.
//      CollectItemCount [optional;  Default: 1]
//          Specifies the number of items that must be collected.
//          The behavior terminates when we have this number of CollectItemId
//          or more in our inventory.
//			This can be a math expression e.g. CollectItemCount="10 - GetItemCount(1234)"
//      MaxCastRange [optional;  Default: 20]
//          [Only meaningful if PoolId is specified.]
//          Specifies the maximum cast range to the pool.  If the toon is not within
//          this range of PoolId, the behavior will move the toon closer.
//      MinCastRange [optional;  Default: 15]
//          [Only meaningful if PoolId is specified.]
//          Specifies the minimum cast range to the pool.  If the toon is closer than
//          this range of PoolId, the behavior will move the toon further away.
//      MoveToPool [optional;  Default: true]
//          [Only meaningful if PoolId is specified.]
//          If true, the behavior should find the place to fish.
//      QuestId [optional; Default: none]
//          Specifies the QuestId, if the item is the only thing to complete this quest.
//      UseFishingGear [optional; Default: false]
//          Equips fishing poles and hats using highest stats
//
//
// THINGS TO KNOW:
// * The original documenation can be found here:
//       https://www.thebuddyforum.com/honorbuddy-forum/honorbuddy-profiles/neutral/96244-quest-behavior-mrfishit-fishing-questitems.html
//
#endregion


#region Examples

//<CustomBehavior File = "MrFishIt" PoolIds="182953 182954 182952" TerminateWhen="IsAchievementCompleted(1225, 1) &amp;&amp; IsAchievementCompleted(1225, 6) &amp;&amp; IsAchievementCompleted(1257, 1) ">
//  <Hotspots>
//    <!-- Lake where the entrance to Coilfang Reservoir is. -->
//    <Hotspot X = "655.9274" Y="7451.637" Z="28.26764" />
//    <Hotspot X = "653.071" Y="7472.469" Z="40.24365" />
//    <Hotspot X = "334.6382" Y="7457.971" Z="38.26764" />
//    <Hotspot X = "310.3563" Y="7037.434" Z="38.26764" />
//    <Hotspot X = "355.4409" Y="6677.375" Z="38.26764" />
//    <Hotspot X = "536.5581" Y="6379.3" Z="40.41693" />
//    <Hotspot X = "731.2769" Y="6554.816" Z="39.15802" />

//    <Hotspot X = "-162.1999" Y="5925.791" Z="38.26764" />
//    <Hotspot X = "-565.2664" Y="5832.716" Z="42.28552" />
//    <Hotspot X = "-806.7015" Y="5631.157" Z="39.22736" />
//    <Hotspot X = "-299.672" Y="5565.752" Z="38.35809" />

//    <Hotspot X = "-299.672" Y="5565.752" Z="38.35809" />
//    <Hotspot X = "-198.9077" Y="6812.568" Z="38.26764" />
//    <Hotspot X = "36.74268" Y="6608.008" Z="38.26764" />
//    <Hotspot X = "-126.7816" Y="6317.455" Z="38.26761" />
//  </Hotspots>
//</CustomBehavior>

//<CustomBehavior File = "MrFishIt" CollectItemId="27481" CollectItemCount="1" PoolIds="182952">
//  <Hotspots>
//    <!-- Lake where the entrance to Coilfang Reservoir is. -->
//    <Hotspot X = "655.9274" Y="7451.637" Z="28.26764" />
//    <Hotspot X = "653.071" Y="7472.469" Z="40.24365" />
//    <Hotspot X = "334.6382" Y="7457.971" Z="38.26764" />
//    <Hotspot X = "310.3563" Y="7037.434" Z="38.26764" />
//    <Hotspot X = "355.4409" Y="6677.375" Z="38.26764" />
//    <Hotspot X = "536.5581" Y="6379.3" Z="40.41693" />
//    <Hotspot X = "731.2769" Y="6554.816" Z="39.15802" />

//    <Hotspot X = "-162.1999" Y="5925.791" Z="38.26764" />
//    <Hotspot X = "-565.2664" Y="5832.716" Z="42.28552" />
//    <Hotspot X = "-806.7015" Y="5631.157" Z="39.22736" />
//    <Hotspot X = "-299.672" Y="5565.752" Z="38.35809" />

//    <Hotspot X = "-299.672" Y="5565.752" Z="38.35809" />
//    <Hotspot X = "-198.9077" Y="6812.568" Z="38.26764" />
//    <Hotspot X = "36.74268" Y="6608.008" Z="38.26764" />
//    <Hotspot X = "-126.7816" Y="6317.455" Z="38.26761" />
//  </Hotspots>
//</CustomBehavior>
#endregion


#region Usings

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Xml.Linq;
using Bots.FishingBuddy;
using Bots.Grind;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.CharacterManagement;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Profiles.Quest.Order;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.DB;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.MrFishIt
{
    [CustomBehaviorFileName(@"MrFishIt")]
    internal class MrFishIt : QuestBehaviorBase
    {
        private readonly FishingBuddyProfile _fishingProfile;

        // DON'T EDIT THIS--it is auto-populated by Git
        protected override string GitId => "$Id$";

        public MrFishIt(Dictionary<string, string> args)
            : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            try
            {
                ParseWaypoints();

                var fishingPoint =
                    GetAttributeAsNullable<Vector3>("", WayPoints.Count == 0, ConstrainAs.Vector3NonEmpty, null) ??
                    Vector3.Zero;
                if (fishingPoint != Vector3.Zero)
                    WayPoints.Add(fishingPoint);

                CollectItemId = GetAttributeAsNullable<int>("CollectItemId", false, ConstrainAs.ItemId, null) ?? 0;
                var collectItemCountExpression = GetAttributeAs("CollectItemCount", false, ConstrainAs.StringNonEmpty,
                    null);
                CollectItemCountCompiledExpression =
                    Utility.ProduceParameterlessCompiledExpression<int>(collectItemCountExpression);
                CollectItemCount = Utility.ProduceCachedValueFromCompiledExpression(CollectItemCountCompiledExpression,
                    1);

                bool circlePathing = GetAttributeAsNullable<bool>("CirclePathing", false, null, null) ?? true;
                PoolIds = GetAttributeAsArray<uint>("PoolIds", false, null, new[] {"PoolId"}, null);
                _fishingProfile = new FishingBuddyProfile(WayPoints,
                    circlePathing ? PathingType.Circle : PathingType.Bounce, PoolIds.ToList());


                _fishingLogic = new FishingLogic(
                    // Wowhead Id of the mainhand weapon to switch to when in combat
                    mainHandItemId: GetAttributeAsNullable<uint>("MainHand", false, null, null) ?? 0,
                    // Wowhead Id of the offhand weapon to switch to when in combat
                    offHandItemId: GetAttributeAsNullable<uint>("OffHand", false, null, null) ?? 0,
                    // Wowhead Id of the hat to switch to when not fishing
                    headItemId: GetAttributeAsNullable<uint>("Hat", false, null, null) ?? 0,
                    // Set this to true if you want to fish from pools, otherwise set to false.
                    poolFishing: _fishingProfile.WayPoints.Count > 1,
                    // GetAttributeAsNullable<bool>("Poolfishing", false, null, null) ?? false; 
                    // If set to true bot will attempt to loot any dead lootable NPCs
                    lootNPCs: GetAttributeAsNullable<bool>("LootNPCs", false, null, null) ?? false,
                    // Set to true to enable flying, false to use ground based navigation
                    useFlying: GetAttributeAsNullable<bool>("Fly", false, null, null) ?? true,
                    // If set to true bot will use water walking, either class abilities or pots
                    useWaterWalking: GetAttributeAsNullable<bool>("UseWaterWalking", false, null, null) ?? true,
                    // If set to true, bot will try to avoid landing in lava. Some pools by floating objects such as ice floes will get blacklisted if this is set to true
                    avoidLava: GetAttributeAsNullable<bool>("AvoidLava", false, null, null) ?? false,
                    // If set to true bot will 'ninja' nodes from other players.
                    ninjaNodes: GetAttributeAsNullable<bool>("NinjaNodes", false, null, null) ?? false,
                    // If set to true bot will automatically apply fishing baits6s
                    useBait: GetAttributeAsNullable<bool>("UseBait", false, null, null) ?? true,
                    // Which bait to prefer (item id). If not found, other baits will be used.
                    useBaitPreference:
                    GetAttributeAsNullable<uint>("UseBaitPreference", false, null, new[] {"BaitId"}) ?? 0,
                    // If set to true bot will automatically fillet fish
                    filletFish: GetAttributeAsNullable<bool>("FilletFish", false, null, null) ?? false,
                    // The maximum time in minutes to spend at a pool before it gets blacklisted
                    maxTimeAtPool: GetAttributeAsNullable<int>("MaxTimeAtPool", false, null, null) ?? 5,
                    // The maximum number of failed casts at a pool before moving to a new location at pool
                    maxFailedCasts: GetAttributeAsNullable<int>("MaxFailedCasts", false, null, null) ?? 15,
                    // When bot is within this distance from current hotspot then it cycles to next hotspot. flymode only 
                    pathPrecision: GetAttributeAsNullable<float>("PathPrecision", false, null, null) ?? 15f,
                    // Number of tracelines to do in a 360 deg area. the higher the more likely to find a landing spot.recomended to set at a multiple of 20
                    traceStep: GetAttributeAsNullable<int>("TraceStep", false, null, null) ?? 40,
                    // Each time bot fails to find a landing spot it adds this number to the range and tries again until it hits MaxPoolRange. Can use decimals.
                    poolRangeStep: GetAttributeAsNullable<float>("PoolRangeStep", false, null, null) ?? .5f
                );
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                QBCLog.Exception(except);
                IsAttributeProblem = true;
            }
        }

        private List<Vector3> WayPoints { get; set; }

        private bool ParseWaypoints()
        {
            var waypoints = new List<Vector3>();

            var hotspots = Element.Element("Hotspots");
            XElement waypointContainer = Element;
            if (hotspots != null)
                waypointContainer = hotspots;

            foreach (Vector3 point in waypointContainer.Elements("Hotspot").Select(e => e.FromXml()))
                waypoints.Add(point);

            WayPoints = waypoints;
            return true;
        }

        public uint[] PoolIds { get; private set; }

        // Attributes provided by caller
        private PerFrameCachedValue<int> CollectItemCount { get; set; }

        [CompileExpression]
        public DelayCompiledExpression<Func<int>> CollectItemCountCompiledExpression { get; private set; }

        public int CollectItemId { get; private set; }

        #region Overrides of CustomForcedBehavior

        private BehaviorFlags _behaviorflagsToDisable = (BehaviorFlags.Loot | BehaviorFlags.Vendor | BehaviorFlags.Roam |
                                                 BehaviorFlags.Pull | BehaviorFlags.Rest | BehaviorFlags.FlightPath);

        public override void OnFinished()
        {
            CharacterSettings.Instance.AutoEquip = _autoEquipOldValue;

            // Enable looting again.
            LevelBot.BehaviorFlags = LevelBot.BehaviorFlags | _behaviorflagsToDisable;
            _fishingLogic.Stop();
        }

        private Composite _root;
        private readonly FishingLogic _fishingLogic;

        private bool _autoEquipOldValue;
        public override void OnStart()
        {
            _autoEquipOldValue = CharacterSettings.Instance.AutoEquip;
            CharacterSettings.Instance.AutoEquip = false;

            // Don't try to loot normally.
            LevelBot.BehaviorFlags = LevelBot.BehaviorFlags & ~_behaviorflagsToDisable;

            PlayerQuest quest = GetQuestInLog();
            if (quest == null)
                this.UpdateGoalText(GetQuestId(), "Fishing Item [" + CollectItemId + "]");
            else
                this.UpdateGoalText((int)quest.Id, "Fishing Item for [" + quest.Name + "]");

            QBCLog.DeveloperInfo("Fishing Item (for QuestId {0}): {1}({2}) x{3}", quest?.Id ?? (uint)GetQuestId(), Utility.GetItemNameFromId(CollectItemId), CollectItemId, CollectItemCount.Value);
            _fishingLogic.Start(_fishingProfile);
        }

        protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
        {
        }

        protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
        {
        }

        protected override Composite CreateMainBehavior()
        {
            return _root ?? (_root = new ActionRunCoroutine(ctx => MainCoroutine()));
        }

        private readonly ProfileHelperFunctionsBase _helperFuncs = new ProfileHelperFunctionsBase();
        private async Task<bool> MainCoroutine()
        {
            if (IsDone)
                return false;

            if (_helperFuncs.GetItemCount(CollectItemId) >= CollectItemCount)
            {
                BehaviorDone();
                return true;
            }

            // Have we a facing waterpoint or a PoolId and PoolGUID? No, then cancel this behavior!
            /*if ((!TestFishing && WaterPoint == Vector3.Zero && (PoolId == 0 || !HasFoundPool)) || Me.Combat || Me.IsDead || Me.IsGhost)
            {
                BehaviorDone();
                return true;
            }*/

            return await _fishingLogic.RootLogic();
        }

        #endregion
    }
    public static class Vector3Extensions
    {
        public static Vector3 FromXml(this XElement element)
        {
            XAttribute xAttribute, yAttribute, zAttribute;
            xAttribute = element.Attribute("X");
            yAttribute = element.Attribute("Y");
            zAttribute = element.Attribute("Z");

            float x, y, z;
            float.TryParse(xAttribute.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out x);
            float.TryParse(yAttribute.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out y);
            float.TryParse(zAttribute.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out z);
            return new Vector3(x, y, z);
        }
    }
}
