// Behavior originally contributed by HighVoltz.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.


#region Summary and Documentation
// QUICK DOX:
// AVOID will try to avoid harmful effects such as poison pools. 
// It will run out of such effects and also try to navigate around them
// AVOID is a 'hook' behavior. Unlike normal quest behaviors, it runs constantly in the background.
//
// Basic Attributes:
//      AvoidName [REQUIRED]
//          Identifies an avoidance definition 
//      ObjectId#  [REQUIRED if Command is "Add" and AvoidWhen not used]
//          Identifies the object or objects that a harmful effect belongs to. 
//          This can be the ID of a NPC, game object, area trigger, dynamic object or a missile spell/spellVisual ID
//          Missile spell visual Id should only be used if the missile does not have a spell ID which is pretty rare.
//			Acceptable formats are ObjectId="####" ObjectId1="####" ObjectId2="####" ObjectIds="####,####,####"
//      AvoidWhen [REQUIRED if Command is "Add" and ObjectId not used]
//          Defines a predicate that must return a boolean value.  When the predicate
//          evaluates to 'true', the object is avoided. This should be used when 
//          specifying ObjectId is not enough. Perhaps the object being avoided is only 
//          harmful when it has a certain aura. The object is exposed via a variable and the 
//          variable name depends on object type. The variable name will be one of the following
//          UNIT when a Npc, AREATRIGGER, GAMEOBJECT, DYNAMICOBJECT or MISSILE
//      Radius [REQUIRED if Command is "Add"]
//          Defines the radius of the area to be avoided
//
// Tunables:
//      Command [optional; ONE OF: Add, Remove; Default: Add]
//          Specifies whether to add or remove an avoidance definition.
//      ObjectType [optional; ONE OF: Npc, GameObject, AreaTrigger, DynamicObject, Missile; Default: Npc]
//          Identifies that type of object that needs to be avoided. 
//      IgnoreIfBlocking [optional; Default: false]
//          Specifies whether effect should be ignored if it blocks path
//      AvoidLocationProducer [optional; Default: Location of effect object]
//          This allows the user to customize the location that needs to be avoided 
//      LeashRadius [optional; Default 40]
//          Defines the maximum distance that bot will move from X/Y/Z while avoiding something. 
//          Only used if a X/Y/Z is specified
//      X/Y/Z [optional; Default: NONE]
//          Defines a leash anchor point that is used to prevent bot from leaving an area while avoiding something
//
// THINGS TO KNOW:
// * It is VERY important to remove the Avoid hook behavior when it is no longer needed.
//      This is accomplished with a command similar to the following:
//          <CustomBehavior File="Hooks\Avoid" AvoidName="AvoidGreenStuff" Command="Remove" />
//      If you add Avoid's, and don't remove them when no longer needed.  Honorbuddy performance
//      will be negatively impacted by constantly checking (with a very high frequency)
//      for conditions that will never be present.
//
// * The algorithm that generates path around avoided areas is pretty simple, it uses a recursive algorithm 
//      that picks the left or right edge of the area being avoided and traces along it until if finds a path round it
//      or hits an obstacle. If an obstacle is found along the edge that was picked then it will try the other edge.
//      If both edges are blocked then the algorthim will either ignore the avoided area if IgnoreIfBlocking is true
//      or move to the edge of the avoided area and wait for effect to disapear or move out of the way.
//      
//      Use a blackspot for avoiding mob aggro if mob is stationary, otherwise bot could get stuck when 
//      the avoidance system is unable to find a path around the mob.
//      
//      This can only avoid ciruclar areas but it's posible to cover cone areas by using the AvoidLocationProducer 
//      tunable to place avoid location in the center of the cone area
//
#endregion


#region Examples
// EXAMPLES:
// 
//  Add an avoidance for a mob whose Id is 1234. This will try to stay at least 10 yds away from mob.
//  <CustomBehavior File="Hooks\Avoid" AvoidName="Some Mob" ObjectId="1234" Radius="10" />
// 
//  Avoid above mob only when it contains a certain aura
//  <CustomBehavior File="Hooks\Avoid" AvoidName="Some Mob" ObjectId="1234" Radius="10" AvoidWhen="UNIT.HasAura(54321)" />
// 
//  Remove the avoidance that was added above. 
//  <CustomBehavior="Hooks\Avoid" AvoidName="Some Mob" Command="Remove" />
//
// Add an avoidance for an areaTrigger whose ID is 1337. Similar to the one for a mob except ObjectType needs to be specified
// <CustomBehavior File="Hooks\Avoid" AvoidName="Some AreaTrigger" ObjectId="1337" Radius="7" ObjectType="AreaTrigger" />
// 
// Avoid a mob's frontal area. The location that is avoided is moved 8 yds out in front of the mob
// <CustomBehavior File="Hooks\Avoid" AvoidName="Some Mob" ObjectId="1234" Radius="10"
//                  AvoidLocationProducer="UNIT.Location.RayCast(UNIT.Rotation, 8)" />
//
// Avoid a mob's rear area. The location that is avoided is moved 8 yds behind the mob
// <CustomBehavior File="Hooks\Avoid" AvoidName="Some Mob" ObjectId="1234" Radius="10"
//                  AvoidLocationProducer="WoWMathHelper.CalculatePointBehind(UNIT.Location, UNIT.Rotation, 8)" />
//
// Avoid a straight line out in front of a mob that starts at mob's location and ends 15 yds out in front.
// This is useful for avoiding abilites that do damage in a line or mobs that move fast.
// <CustomBehavior File="Hooks\Avoid" AvoidName="Some Mob" ObjectId="1234" Radius="10" 
//                  AvoidLocationProducer="WoWMathHelper.GetNearestPointOnLineSegment(Me.Location, UNIT.Location, UNIT.Location.RayCast(UNIT.Rotation, 15))" />
//
// If the above example doesn't work for avoiding a straight line then you will need to add multiple avoids lined up to cover the line.
// This might seem dirty but because each 'avoid' is cicular using this method is sometimes needed to avoid other shapes
// For Example.

// COMPOSITE AVOID AREA
// The basic "avoid area" is a circle defined by a center and a radius.  By default, the behavior
// places the center at the mob's location, and the Radius attribute is provided directly.
   
// Alternatively, it is possible to ask the behavior to use a use a different center
// by providing the AvoidLocationProducer attribute.
// Defining a AvoidLocationProducer attribute, coupled with the ability to fire multiple
// Avoid areas using the same trigger, allows us to define avoid areas of different
// 'shapes' (other than the default basic 'circle').
   
// In some encounters, it is important to define the "avoid area" precisely.
// If we grossly define an avoid area as centered on the mob, and a large radius, this
// leaves the toon no where to run to get out of the impending attack.
   
// For instance, in the example below an Earthrending Slam affects all toons in front of the mob out to 20 feet.
// The attack is not circular around the mob, but a "fat line" six feet wide in front of the mob.
// The initial developer instinct will be to define a circle centered on the mob and 20 feet
// in diameter.  This would be a grave mistake, as it leaves the toon no where it knows it
// can run to avoid the attack.
   
// A better solution would be to define a small set of overlapping circles that cover the path
// of the attack.  Since our example attack width is six feet wide, we'll use a small series of
// circles with a 3 foot radius, and place several of them such as the circles overlap.  The result
// is the "fat line" of the attack (6 feet wide by 20 feet long in front of the mob) as being
// covered by a set of small circles.  The Avoid behavior effective takes a 'union'
// of all these overlapping avoid areas to define one large avoid area. When done in this fashion,
// the toon can see that it has to step no more than three feet to one side or the other
// to avoid the attack.
   
// The profile code that implements this example is shown below.  Note that, we use several
// Avoid behaviors with the same trigger, but different placements for the "circle".  This is
// what creates our composite avoid area.

// <CustomBehavior File="Hooks\Avoid" AvoidName="Earthrending Slam1" ObjectId="80167" Radius="6" AvoidWhen="UNIT.CastingSpellId == 165907" 
//    AvoidLocationProducer="UNIT.Location.RayCast(UNIT.Rotation, 3)" />
//
// <CustomBehavior File="Hooks\Avoid" AvoidName="Earthrending Slam2" ObjectId="80167" Radius="6" AvoidWhen="UNIT.CastingSpellId == 165907" 
//    AvoidLocationProducer="UNIT.Location.RayCast(UNIT.Rotation, 8)" />
//
//<CustomBehavior File="Hooks\Avoid" AvoidName="Earthrending Slam3" ObjectId="80167" Radius="6" AvoidWhen="UNIT.CastingSpellId == 165907" 
//    AvoidLocationProducer="UNIT.Location.RayCast(UNIT.Rotation, 13)" />
//
//<CustomBehavior File="Hooks\Avoid" AvoidName="Earthrending Slam4" ObjectId="80167" Radius="6" AvoidWhen="UNIT.CastingSpellId == 165907" 
//    AvoidLocationProducer="UNIT.Location.RayCast(UNIT.Rotation, 18)" /> 


//THINGS TO KNOW
//--------------
//* This behavior will try to find the quickest escape route from the area to be avoided.
//  Sometimes, this will be a side-step, sometimes an exit to the rear, or sometimes running
//  directly through the mob.
#endregion


#region Usings

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Bots.DungeonBuddy;
using Bots.DungeonBuddy.Avoidance;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Profiles.Quest.Order;
using Styx.CommonBot.Routines;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

#endregion

namespace Honorbuddy.Quest_Behaviors
{
    [CustomBehaviorFileName(@"Hooks\Avoid")]
    public class Avoid : QuestBehaviorBase
    {
        private readonly static Dictionary<string, AvoidInfo> AvoidDictionary = new Dictionary<string, AvoidInfo>();
        private static NavigationProvider _prevNavigator ;
        private static ActionRunCoroutine _hook;

        #region Constructor and Argument Processing

        private enum CommandType
        {
            Add,
            Remove,
        };

        private enum AvoidObjectType
        {
            Npc,
            GameObject,
            AreaTrigger,
            DynamicObject,
            Missile
        }

        public Avoid(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                AvoidName = GetAttributeAs<string>("AvoidName", true, ConstrainAs.StringNonEmpty, null) ?? "";
                Command = GetAttributeAsNullable<CommandType>("Command", false, null, null) ?? CommandType.Add;

                if (Command == CommandType.Add)
                {
                    Radius = GetAttributeAsNullable<float>(
                        "Radius",
                        true,
                        new ConstrainTo.Domain<float>(0.5f, 50f),
                        null) ?? 5;

                    LeashRadius = GetAttributeAsNullable<float>(
                        "LeashRadius",
                        false,
                        new ConstrainTo.Domain<float>(10f, 150f),
                        null) ?? 40;

                    LeashPoint = GetAttributeAsNullable<WoWPoint>("", false, ConstrainAs.WoWPointNonEmpty, null);

					// Primary attributes...
					var numberedObjectIds = GetAttributeAsArray<int>("ObjectIds", false, ConstrainAs.ObjectId, null, null) ?? new int[0];
					var objectIdArray = GetNumberedAttributesAsArray<int>("ObjectId", 0, ConstrainAs.ObjectId, null) ?? new int[0];

					ObjectIds = numberedObjectIds.Concat(objectIdArray).ToArray();


                    ObjectType = GetAttributeAsNullable<AvoidObjectType>("ObjectType", false, null, null) ?? AvoidObjectType.Npc;

                    AvoidWhenExpression = GetAttributeAs<string>("AvoidWhen", false, ConstrainAs.StringNonEmpty, null) ?? "";
                    AvoidLocationProducerExpression = GetAttributeAs<string>("AvoidLocationProducer", false, ConstrainAs.StringNonEmpty, null) ?? "";
                    IgnoreIfBlocking = GetAttributeAsNullable<bool>("IgnoreIfBlocking", false, null, null) ?? false;

                    AvoidWhen = CreateAvoidWhen(AvoidWhenExpression);

                    AvoidLocationProducer = CreateAvoidLocationProducer(AvoidLocationProducerExpression);
                }
            }
            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it can be quickly
                // resolved.
                QBCLog.Exception(except);
                IsAttributeProblem = true;
            }
        }


        protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
        {
            // empty, for now...
        }

        protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
        {
            UsageCheck_SemanticCoherency(
                xElement,
                Command == CommandType.Add && AvoidDictionary.ContainsKey(AvoidName),
                context => string.Format("An avoid with name {0} has already been added", AvoidName));

            UsageCheck_SemanticCoherency(
                xElement,
                Command == CommandType.Add && string.IsNullOrEmpty(AvoidWhenExpression) && !ObjectIds.Any(),
                context => "At least a ObjectId or AvoidWhen must be specified");

            UsageCheck_SemanticCoherency(
                xElement,
                Command == CommandType.Add && LeashPoint.HasValue && LeashRadius - Radius  < 5,
                context => "LeashRadius MUST be at least 5 more than Radius when X/Y/Z is specified " );
        }

        #endregion

        private string AvoidName { get; set; }
        private string AvoidWhenExpression { get; set; }
        private string AvoidLocationProducerExpression { get; set; }
        private CommandType Command { get; set; }
        public WoWPoint? LeashPoint { get; private set; }
        private float LeashRadius { get; set; }
        private float Radius { get; set; }
        private bool IgnoreIfBlocking { get; set; }

		[CompileExpression]
        public DelayCompiledExpression AvoidWhen { get; private set; }

		[CompileExpression]
		public DelayCompiledExpression AvoidLocationProducer { get; private set; }
		private int[] ObjectIds { get; set; }

        private AvoidObjectType ObjectType { get; set; }

        #region Overrides of CustomForcedBehavior
        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return "$Id: DoWhen.cs 1789 2014-11-13 18:12:31Z highvoltz $"; } }
        public override string SubversionRevision { get { return "$Rev: 1789 $"; } }

        public override void OnStart()
        {
            // Acquisition and checking of any sub-elements go here.
            // A common example:
            //     HuntingGrounds = HuntingGroundsType.GetOrCreate(Element, "HuntingGrounds", HuntingGroundCenter);
            //     IsAttributeProblem |= HuntingGrounds.IsAttributeProblem;

            // Let QuestBehaviorBase do basic initialization of the behavior, deal with bad or deprecated attributes,
            // capture configuration state, install BT hooks, etc.  This will also update the goal text.
            var isBehaviorShouldRun = OnStart_QuestBehaviorCore();
            if (isBehaviorShouldRun)
            {
                if (Command == CommandType.Remove)
                {
                    if (AvoidDictionary.ContainsKey(AvoidName))
                    {
                        QBCLog.DeveloperInfo("Removing \"{0}\" avoid", AvoidName);
                        var avoidInfo = AvoidDictionary[AvoidName];
                        AvoidDictionary.Remove(AvoidName);
                        AvoidanceManager.RemoveAvoid(avoidInfo);
                    }
                }
                else if (Command == CommandType.Add)
                {
                    AvoidInfo avoidInfo = BuildAvoidInfo();
                    QBCLog.DeveloperInfo("Adding \"{0}\" avoid - Radius: {1}, ObjectId: ({2}), ObjectType: {3}",
					   AvoidName, Radius, string.Join(", ", ObjectIds), ObjectType);
                    AvoidDictionary[AvoidName] = avoidInfo;
                    AvoidanceManager.AddAvoid(avoidInfo);
                }
            }

            if (_hook == null && AvoidDictionary.Any())
            {
                InstallHook();
            } // remove hook if no avoid definitions are active
            else if (_hook != null && !AvoidDictionary.Any())
            {
                RemoveHook();
            }
            BehaviorDone();
        }

        #endregion

        private void BotEvents_OnPulse(object sender, EventArgs args)
        {
            AvoidanceManager.Pulse();
        }

        private void BotEvents_OnBotStopped(EventArgs args)
        {
            RemoveHook();
        }

		private void Profile_OnNewOuterProfileLoaded(BotEvents.Profile.NewProfileLoadedEventArgs args)
		{
			RemoveHook();
		}

        private async Task<bool> HookHandler()
        {
	        var supportsCapabilities = RoutineManager.Current.SupportedCapabilities != CapabilityFlags.None;

            // Prevent Combat Routine from getting called when running out of bad stuff and CR doesn't use 
			// the CombatRoutine Capabilities since it might resist.
			// The AvoidanceManager will disallow the Movement capability when running out of bad stuff.
			// For more info on CombatRoutine Capabilities see http://wiki.thebuddyforum.com/index.php?title=Honorbuddy:Developer_Notebook:Combat_Routine_Capabilities

			if (AvoidanceManager.IsRunningOutOfAvoid && !supportsCapabilities)
                return true;
            
            // Special case: Bot will do a lot of fast stop n go when avoiding a mob that moves slowly and trying to
            // loot something near the mob. To fix, a delay is added to slow down the 'Stop n go' behavior
            var poiType = BotPoi.Current.Type;
            if (poiType == PoiType.Loot || poiType == PoiType.Harvest || poiType == PoiType.Skin)
            {
                if (!Me.IsActuallyInCombat && AvoidanceManager.Avoids.Any(o => o.IsPointInAvoid(BotPoi.Current.Location)))
                {
                    TreeRoot.StatusText = "Waiting for 'avoid' to move before attempting to loot " + BotPoi.Current.Name;
                    var randomWaitTime = StyxWoW.Random.Next(3000, 8000);
                    await Coroutine.Wait(randomWaitTime, 
                        () => Me.IsActuallyInCombat || !AvoidanceManager.Avoids.Any(o => o.IsPointInAvoid(BotPoi.Current.Location)));
                }
            }

            return false;
        }

        private void InstallHook()
        {
            _prevNavigator = Navigator.NavigationProvider;
            var avoidNavigator = new AvoidanceNavigationProvider();;
            Navigator.NavigationProvider = avoidNavigator;
            avoidNavigator.UpdateMaps();
            _hook = new ActionRunCoroutine(ctx => HookHelpers.ExecuteHook(this, HookHandler));
            TreeHooks.Instance.InsertHook("Combat_Main", 0, _hook);
	        BotEvents.OnPulse += BotEvents_OnPulse;
            BotEvents.OnBotStopped += BotEvents_OnBotStopped;
			BotEvents.Profile.OnNewOuterProfileLoaded += Profile_OnNewOuterProfileLoaded;

            QBCLog.Info("Installed avoidance system");
        }


        private void RemoveHook()
        {
            if (_hook == null)
                return;
            TreeHooks.Instance.RemoveHook("Combat_Main", _hook);
            Navigator.NavigationProvider = _prevNavigator;

			// Make sure maps for the previous navigator are up-to-date
	        var meshNav = Navigator.NavigationProvider as MeshNavigator;
			if (meshNav != null)
				meshNav.UpdateMaps();

            _prevNavigator = null;
            _hook = null;
            foreach (var kv in AvoidDictionary)
            {
	            AvoidanceManager.RemoveAvoid(kv.Value);
				QBCLog.DeveloperInfo("Removed the \"{0}\" avoidance definition", kv.Key);
            }
            AvoidDictionary.Clear();
			BotEvents.OnPulse -= BotEvents_OnPulse;
            BotEvents.OnBotStopped -= BotEvents_OnBotStopped;
			BotEvents.Profile.OnNewOuterProfileLoaded -= Profile_OnNewOuterProfileLoaded;

            QBCLog.Info("Uninstalled avoidance system");
        }

        private DelayCompiledExpression CreateAvoidWhen(string expression)
        {
            if (string.IsNullOrEmpty(expression))
                return null;
            switch (ObjectType)
            {
                case AvoidObjectType.AreaTrigger:
					return new DelayCompiledExpression<Func<WoWAreaTrigger, bool>>("AREATRIGGER=>" + expression);
                case AvoidObjectType.DynamicObject:
					return new DelayCompiledExpression<Func<WoWDynamicObject, bool>>("DYNAMICOBJECT=>" + expression);
                case AvoidObjectType.GameObject:
					return new DelayCompiledExpression<Func<WoWGameObject, bool>>("GAMEOBJECT=>" + expression);
                case AvoidObjectType.Npc:
					return new DelayCompiledExpression<Func<WoWUnit, bool>>("UNIT=>" + expression);
                case AvoidObjectType.Missile:
					return new DelayCompiledExpression<Func<WoWMissile, bool>>("MISSILE=>" + expression);
                default:
                    return null;
            }
        }

		private DelayCompiledExpression CreateAvoidLocationProducer(string expression)
        {
            if (string.IsNullOrEmpty(expression))
                return null;

            switch (ObjectType)
            {
                case AvoidObjectType.AreaTrigger:
					return new DelayCompiledExpression<Func<WoWAreaTrigger, WoWPoint>>("AREATRIGGER=>" + expression);

                case AvoidObjectType.DynamicObject:
					return new DelayCompiledExpression<Func<WoWDynamicObject, WoWPoint>>("DYNAMICOBJECT=>"+ expression);

                case AvoidObjectType.GameObject:
					return new DelayCompiledExpression<Func<WoWGameObject, WoWPoint>>("GAMEOBJECT=>" + expression);

                case AvoidObjectType.Npc:
					return new DelayCompiledExpression<Func<WoWUnit, WoWPoint>>("UNIT=>" + expression);

                case AvoidObjectType.Missile:
					return new DelayCompiledExpression<Func<WoWMissile, WoWPoint>>("MISSILE=>" + expression);

                default:
                    return null;
            }
        }

        private AvoidInfo BuildAvoidInfo()
        {
            switch (ObjectType)
            {
                case AvoidObjectType.AreaTrigger:
                    return BuildAvoidObjectInfo <WoWAreaTrigger>();

                case AvoidObjectType.DynamicObject:
                    return BuildAvoidObjectInfo <WoWDynamicObject>();

                case AvoidObjectType.GameObject:
                    return BuildAvoidObjectInfo<WoWGameObject>();

                case AvoidObjectType.Npc:
                    return BuildAvoidObjectInfo<WoWUnit>();

                case AvoidObjectType.Missile:
                    return BuildAvoidMissileImpact();

                default:
                    return BuildAvoidObjectInfo<WoWObject>();
            }
        }

        private AvoidObjectInfo BuildAvoidObjectInfo<T>() where T : WoWObject
        {
            var includeId = ObjectIds.Any();
            var includeAvoidWhen = AvoidWhen != null;
            Predicate<WoWObject> pred;

            if (includeId)
            {
                if (includeAvoidWhen)
					pred = o => ObjectIds.Contains((int)o.Entry) && o is T && ((DelayCompiledExpression<Func<T, bool>>)AvoidWhen).CallableExpression((T)o);
                else
                    pred = o => ObjectIds.Contains((int)o.Entry) && o is T;
            }
            else
            {
				pred = o => o is T && ((DelayCompiledExpression<Func<T, bool>>)AvoidWhen).CallableExpression((T)o);
            }

            Func<WoWObject, WoWPoint> locationProducer;
            if (AvoidLocationProducer != null)
				locationProducer = o => ((DelayCompiledExpression<Func<T, WoWPoint>>)AvoidLocationProducer).CallableExpression((T)o);
            else
                locationProducer = null;

            return new AvoidObjectInfo(
                ctx => true,
                pred,
                o => Radius,
                ignoreIfBlocking: IgnoreIfBlocking,
                locationSelector: locationProducer,
                leashPointSelector: LeashPoint.HasValue ? new Func<WoWPoint>(() => LeashPoint.Value) : null,
                leashRadius: LeashRadius);
        }

        private AvoidLocationInfo BuildAvoidMissileImpact()
        {
            var includeAvoidWhen = AvoidWhen != null;
            Func<IEnumerable<object>> collectionProducer;

            if (includeAvoidWhen)
            {
                collectionProducer = () => WoWMissile.InFlightMissiles
                    .Where(m => (m.SpellId != 0 ? ObjectIds.Contains(m.SpellId) : ObjectIds.Contains(m.SpellVisualId))
							 && ((DelayCompiledExpression<Func<WoWMissile, bool>>)AvoidWhen).CallableExpression(m));

            }
            else
            {
                collectionProducer = () => WoWMissile.InFlightMissiles
					.Where(m => m.SpellId != 0 ? ObjectIds.Contains(m.SpellId) : ObjectIds.Contains(m.SpellVisualId));
            }

            Func<object, WoWPoint> locationProducer;
            if (AvoidLocationProducer != null)
				locationProducer = o => ((DelayCompiledExpression<Func<WoWMissile, WoWPoint>>)AvoidLocationProducer).CallableExpression((WoWMissile)o);
            else
                locationProducer = o => ((WoWMissile) o).ImpactPosition;

            return new AvoidLocationInfo(
                ctx => true,
                locationProducer,
                o => Radius,
                LeashPoint.HasValue ? new Func<WoWPoint>(() => LeashPoint.Value) : null ,
                LeashRadius,
                collectionProducer,
                IgnoreIfBlocking);
        }
    }
}
