﻿// Behavior originally contributed by HighVoltz.
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
//      ObjectId  [REQUIRED if Command is "Add" and AvoidWhen not used]
//          Identifies the object that a harmful effect belongs to. 
//          This can be an ID for a NPC, game object, area trigger, dynamic object or a missile spell/spellVisual ID
//          Missile spell visual Id should only be used if the missile does not have a spell ID which is pretty rare.
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
//  <CustomBehavior="Hooks\Avoid" AvoidName="Some Mob" ObjectId="1234" Radius="10" />
// 
//  Avoid above mob only when it contains a certain aura
//  <CustomBehavior="Hooks\Avoid" AvoidName="Some Mob" ObjectId="1234" Radius="10" AvoidWhen="UNIT.HasAura(54321)" />
// 
//  Remove the avoidance that was added above. 
//  <CustomBehavior="Hooks\Avoid" AvoidName="Some Mob" Command="Remove" />
//
// Add an avoidance for an areaTrigger whose ID is 1337. Similar to the one for a mob except ObjectType needs to be specified
// <CustomBehavior="Hooks\Avoid" AvoidName="Some AreaTrigger" ObjectId="1337" Radius="7" ObjectType="AreaTrigger" />
// 
// Avoid a mob's frontal area. The location that is avoided is moved 5 yds out in front of the mob
// <CustomBehavior="Hooks\Avoid" AvoidName="Some Mob" ObjectId="1234" Radius="10"
//                  AvoidLocationProducer="UNIT.Location.RayCast(UNIT.Rotation, 5)" />
//
// Avoid a mob's rear area. The location that is avoided is moved 5 yds behind the mob
// <CustomBehavior="Hooks\Avoid" AvoidName="Some Mob" ObjectId="1234" Radius="10"
//                  AvoidLocationProducer="WoWMathHelper.CalculatePointBehind(UNIT.Location, UNIT.Rotation, 5)" />
//
// Avoid a straight line out in front of a mob that starts at mob's location and ends 15 yds out in front.
// This is useful for avoiding abilites that do damage in a line or mobs that move fast.
// <CustomBehavior="Hooks\Avoid" AvoidName="Some Mob" ObjectId="1234" Radius="10" 
//                  AvoidLocationProducer="WoWMathHelper.GetNearestPointOnLineSegment(Me.Location, UNIT.Location, UNIT.Location.RayCast(UNIT.Rotation, 15))" />
#endregion


#region Usings

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Bots.DungeonBuddy;
using Bots.DungeonBuddy.Avoidance;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
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

                    ObjectId = GetAttributeAsNullable<int>("ObjectId", false, ConstrainAs.ObjectId, null) ?? 0;
                    ObjectType = GetAttributeAsNullable<AvoidObjectType>("ObjectType", false, null, null) ?? AvoidObjectType.Npc;

                    AvoidWhenExpression = GetAttributeAs<string>("AvoidWhen", false, ConstrainAs.StringNonEmpty, null) ?? "";
                    AvoidLocationProducerExpression = GetAttributeAs<string>("AvoidLocationProducer", false, ConstrainAs.StringNonEmpty, null) ?? "";
                    IgnoreIfBlocking = GetAttributeAsNullable<bool>("IgnoreIfBlocking", false, null, null) ?? false;

                    AvoidWhen = CreateAvoidWhen(AvoidWhenExpression);
                    if (AvoidWhen != null && AvoidWhen.HasErrors)
                        IsAttributeProblem = true;

                    AvoidLocationProducer = CreateAvoidLocationProducer(AvoidLocationProducerExpression);
                    if (AvoidLocationProducer != null && AvoidLocationProducer.HasErrors)
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
                Command == CommandType.Add && string.IsNullOrEmpty(AvoidWhenExpression) && ObjectId == 0,
                context => "At least a ObjectId or AvoidWhen must be specified");
        }

        #endregion

        private string AvoidName { get; set; }
        private string AvoidWhenExpression { get; set; }
        private string AvoidLocationProducerExpression { get; set; }
        private CommandType Command { get; set; }
        private float Radius { get; set; }
        private bool IgnoreIfBlocking { get; set; }
        private UserDefinedExpressionBase AvoidWhen { get; set; }
        private UserDefinedExpressionBase AvoidLocationProducer { get; set; }
        private int ObjectId { get; set; }

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
                        QBCLog.DeveloperInfo("Removing \"{0}\" avoid - Radius: {1}, ObjectId: {2}, ObjectType: {3}",
                            AvoidName, Radius, ObjectId, ObjectType);
                        var avoidInfo = AvoidDictionary[AvoidName];
                        AvoidDictionary.Remove(AvoidName);
                        AvoidanceManager.RemoveAvoid(avoidInfo);
                    }
                }
                else if (Command == CommandType.Add)
                {
                    AvoidInfo avoidInfo = BuildAvoidInfo();
                    QBCLog.DeveloperInfo("Adding \"{0}\" avoid - Radius: {1}, ObjectId: {2}, ObjectType: {3}",
                       AvoidName, Radius, ObjectId, ObjectType);
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

        void ObjectManager_OnObjectListUpdateFinished(object context)
        {
            AvoidanceManager.Pulse();
        }

        void BotEvents_OnBotStopped(EventArgs args)
        {
            RemoveHook();
        }

        private async Task<bool> HookHandler()
        {
            // prevent Combat Routine from getting called when running out of bad stuff since it might resist.
            return AvoidanceManager.IsRunningOutOfAvoid;
        }

        private void InstallHook()
        {
            _prevNavigator = Navigator.NavigationProvider;
            var avoidNavigator = new AvoidanceNavigationProvider();;
            Navigator.NavigationProvider = avoidNavigator;
            avoidNavigator.UpdateMaps();
            _hook = new ActionRunCoroutine(ctx => HookHandler());
            TreeHooks.Instance.InsertHook("Combat_Main", 0, _hook);
            ObjectManager.OnObjectListUpdateFinished += ObjectManager_OnObjectListUpdateFinished;
            BotEvents.OnBotStopped += BotEvents_OnBotStopped;

            QBCLog.Info("Installed avoidance system");
        }

        private void RemoveHook()
        {
            if (_hook == null)
                return;
            TreeHooks.Instance.RemoveHook("Combat_Main", _hook);
            Navigator.NavigationProvider = _prevNavigator;
            _prevNavigator = null;
            _hook = null;
            foreach (var kv in AvoidDictionary)
                AvoidanceManager.RemoveAvoid(kv.Value);
            AvoidDictionary.Clear();
            ObjectManager.OnObjectListUpdateFinished -= ObjectManager_OnObjectListUpdateFinished;
            BotEvents.OnBotStopped -= BotEvents_OnBotStopped;

            QBCLog.Info("Uninstalled avoidance system");
        }

        private UserDefinedExpressionBase CreateAvoidWhen(string expression)
        {
            if (string.IsNullOrEmpty(expression))
                return null;
            switch (ObjectType)
            {
                case AvoidObjectType.AreaTrigger:
                    return new UserDefinedExpression<WoWAreaTrigger,bool>(AvoidName, expression, "AREATRIGGER");

                case AvoidObjectType.DynamicObject:
                    return new UserDefinedExpression<WoWDynamicObject, bool>(AvoidName, expression, "DYNAMICOBJECT");

                case AvoidObjectType.GameObject:
                    return new UserDefinedExpression<WoWGameObject, bool>(AvoidName, expression, "GAMEOBJECT");

                case AvoidObjectType.Npc:
                    return new UserDefinedExpression<WoWUnit, bool>(AvoidName, expression, "UNIT");

                case AvoidObjectType.Missile:
                    return new UserDefinedExpression<WoWMissile, bool>(AvoidName, expression, "MISSILE");

                default:
                    return null;
            }
        }

        private UserDefinedExpressionBase CreateAvoidLocationProducer(string expression)
        {
            if (string.IsNullOrEmpty(expression))
                return null;

            switch (ObjectType)
            {
                case AvoidObjectType.AreaTrigger:
                    return new UserDefinedExpression<WoWAreaTrigger, WoWPoint>(AvoidName, expression, "AREATRIGGER");

                case AvoidObjectType.DynamicObject:
                    return new UserDefinedExpression<WoWDynamicObject, WoWPoint>(AvoidName, expression, "DYNAMICOBJECT");

                case AvoidObjectType.GameObject:
                    return new UserDefinedExpression<WoWGameObject, WoWPoint>(AvoidName, expression, "GAMEOBJECT");

                case AvoidObjectType.Npc:
                    return new UserDefinedExpression<WoWUnit, WoWPoint>(AvoidName, expression, "UNIT");

                case AvoidObjectType.Missile:
                    return new UserDefinedExpression<WoWMissile, WoWPoint>(AvoidName, expression, "MISSILE");

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
            var includeId = ObjectId != 0;
            var includeAvoidWhen = AvoidWhen != null;
            Predicate<WoWObject> pred;

            if (includeId)
            {
                if (includeAvoidWhen)
                    pred = o => o.Entry == ObjectId && o is T && ((UserDefinedExpression<T, bool>)AvoidWhen).Evaluate((T)o);
                else
                    pred = o => o.Entry == ObjectId && o is T;
            }
            else
            {
                pred = o => o is T && ((UserDefinedExpression<T, bool>)AvoidWhen).Evaluate((T)o);
            }

            Func<WoWObject, WoWPoint> locationProducer;
            if (AvoidLocationProducer != null)
                locationProducer = o => ((UserDefinedExpression<T, WoWPoint>) AvoidLocationProducer).Evaluate((T) o);
            else
                locationProducer = null;

            return new AvoidObjectInfo(ctx => true, pred, o => Radius, ignoreIfBlocking: IgnoreIfBlocking, locationSelector: locationProducer);
        }

        private AvoidLocationInfo BuildAvoidMissileImpact()
        {
            var includeAvoidWhen = AvoidWhen != null;
            Func<IEnumerable<object>> collectionProducer;

            if (includeAvoidWhen)
            {
                collectionProducer = () => WoWMissile.InFlightMissiles
                    .Where(
                        m => (m.SpellId != 0 ? m.SpellId == ObjectId : m.SpellVisualId == ObjectId)
                             && ((UserDefinedExpression<WoWMissile, bool>) AvoidWhen).Evaluate(m));

            }
            else
            {
                collectionProducer = () => WoWMissile.InFlightMissiles
                    .Where(m => m.SpellId != 0 ? m.SpellId == ObjectId : m.SpellVisualId == ObjectId);
            }

            Func<object, WoWPoint> locationProducer;
            if (AvoidLocationProducer != null)
                locationProducer = o => ((UserDefinedExpression<WoWMissile, WoWPoint>)AvoidLocationProducer).Evaluate((WoWMissile)o);
            else
                locationProducer = o => ((WoWMissile) o).ImpactPosition;

            return new AvoidLocationInfo(
                ctx => true,
                locationProducer,
                o => Radius,
                null,
                40,
                collectionProducer,
                IgnoreIfBlocking);
        }
    }
}
