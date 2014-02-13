// Behavior originally contributed by Raphus.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

#region Summary and Documentation
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_WaitTimer
//
// Allows you to use item on an object or at a location
// ##Syntax##
// [Optional] QuestId: Id of the quest. If specified the QB will run until the quest is completed
// [Optional] MobId1, MobId2, ...MobIdN: Id of the object/npc that the item will be used on
// ItemId: Id of the item that will be used
// [Optional]WaitTime: Time to wait after using the item 
// UseType: PointToObject (from X,Y,Z to an object's location)
//          PointToPoint  (from X,Y,Z to ClickToX,ClickToY,ClickToZ)
//          ToObject      (from range of an object to object's location)
//          Default is PointToPoint
// [Optional]X,Y,Z: If the UseType is AtLocation, QB will move to that location before using item. Otherwise it will move towards that point to search for objects
// [Optional]ClickToX,ClickToY,ClickToZ: If the UseType is PoinToPoint, this location will be used to remote click 
// [Optional]Range: If the UseType is ToObject, QB will move to that range of an object/npc before using item. (default 4)
// 
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.UseItemTargetLocation
{
    [CustomBehaviorFileName(@"UseItemTargetLocation")]
    public class UseItemTargetLocation : CustomForcedBehavior
    {
        public enum QBType
        {
            PointToPoint = 0,
            PointToObject = 1,
            ToObject = 2
        }

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


        public UseItemTargetLocation(Dictionary<string, string> args)
            : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;

                ClickToLocation = GetAttributeAsNullable<WoWPoint>("ClickTo", false, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
                CollectionDistance = GetAttributeAsNullable<double>("CollectionDistance", false, ConstrainAs.Range, null) ?? 100;
                ItemId = GetAttributeAsNullable<int>("ItemId", true, ConstrainAs.ItemId, null) ?? 0;
                MoveToLocation = GetAttributeAsNullable<WoWPoint>("", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;
                MobIds = GetNumberedAttributesAsArray<int>("MobId", 0, ConstrainAs.MobId, new[] { "ObjectId" });
                MobHpPercentLeft = GetAttributeAsNullable<double>("MobHpPercentLeft", false, ConstrainAs.Percent, new[] { "HpLeftAmount" }) ?? 100.0;
                NpcState = GetAttributeAsNullable<NpcStateType>("MobState", false, null, new[] { "NpcState" }) ?? NpcStateType.DontCare;
                NumOfTimes = GetAttributeAsNullable<int>("NumOfTimes", false, ConstrainAs.RepeatCount, null) ?? 1;
                ObjType = GetAttributeAsNullable<ObjectType>("ObjectType", false, null, new[] { "MobType" }) ?? ObjectType.Npc;
                Range = GetAttributeAsNullable<double>("Range", false, ConstrainAs.Range, null) ?? 20.0;
                MinRange = GetAttributeAsNullable<double>("MinRange", false, ConstrainAs.Range, null) ?? 4.0;
                UseType = GetAttributeAsNullable<QBType>("UseType", false, null, null) ?? QBType.PointToPoint;
                WaitTime = GetAttributeAsNullable<int>("WaitTime", false, ConstrainAs.Milliseconds, null) ?? 0;

                Counter = 1;
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


        // Attributes provided by caller
        public WoWPoint ClickToLocation { get; private set; }
        public double CollectionDistance { get; private set; }
        public int ItemId { get; private set; }
        public int[] MobIds { get; private set; }
        public double MobHpPercentLeft { get; private set; }
        public WoWPoint MoveToLocation { get; private set; }
        public NpcStateType NpcState { get; private set; }
        public int NumOfTimes { get; private set; }
        public ObjectType ObjType { get; private set; }
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }
        public double Range { get; private set; }
        public double MinRange { get; private set; }
        public QBType UseType { get; private set; }
        public int WaitTime { get; private set; }

        // Private variables for internal state
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private Composite _root;
	    private WaitTimer _waitTimer;

        // Private properties
        public int Counter { get; private set; }
        private WoWItem Item { get { return Me.CarriedItems.FirstOrDefault(i => i.Entry == ItemId); } }
        private LocalPlayer Me { get { return (StyxWoW.Me); } }
        private readonly List<ulong> _npcBlacklist = new List<ulong>();

        private WoWObject UseObject
        {
            get
            {
                WoWObject obj = null;
                switch (ObjType)
                {
                    case ObjectType.GameObject:
                        obj = ObjectManager.GetObjectsOfType<WoWGameObject>()
                            .OrderBy(ret => ret.Distance)
                            .FirstOrDefault(target =>
                                !_npcBlacklist.Contains(target.Guid) &&
                                target.Distance < CollectionDistance &&
                                MobIds.Contains((int)target.Entry));

                        break;

                    case ObjectType.Npc:

                        var baseTargets = ObjectManager.GetObjectsOfType<WoWUnit>()
                                                               .OrderBy(target => target.Distance)
                                                               .Where(target => !_npcBlacklist
                                                               .Contains(target.Guid) &&
                                                                   target.Distance < CollectionDistance &&
                                                                   !Me.Minions.Contains(target) &&
                                                                    MobIds.Contains((int)target.Entry));

                        var npcStateQualifiedTargets = baseTargets
                                                            .OrderBy(target => target.Distance)
                                                            .Where(target => ((NpcState == NpcStateType.DontCare)
                                                                              || ((NpcState == NpcStateType.Dead) && target.IsDead)
                                                                              || ((NpcState == NpcStateType.Alive) && target.IsAlive)
                                                                              || ((NpcState == NpcStateType.BelowHp) && target.IsAlive && (target.HealthPercent < MobHpPercentLeft))));


                        obj = npcStateQualifiedTargets.FirstOrDefault();

                        break;

                }

                if (obj != null)
                { QBCLog.DeveloperInfo(obj.Name); }

                return obj;
            }
        }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id$"); } }
        public override string SubversionRevision { get { return ("$Revision$"); } }


        ~UseItemTargetLocation()
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


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(
					// don't drop down while wait timer is running
					new Decorator(ctx => !_waitTimer.IsFinished, new ActionAlwaysSucceed()),

                    // If item is not in our backpack, behavior is done...
                    new Decorator(context => Item == null,
                        new Action(context =>
                        {
                            QBCLog.Error("ItemId({0}) is not in our backpack", ItemId);
                            TreeRoot.Stop();
                            _isBehaviorDone = true;
                        })),

                    // Wait for item to come off of cooldown...
                    new Decorator(context => Item.CooldownTimeLeft > TimeSpan.Zero,
                        new Action(context => QBCLog.Info("Waiting for {0} to leave cooldown (time remaining: {1})",
                                                         Item.Name, Item.CooldownTimeLeft))),

                    new Decorator(ret => Counter > NumOfTimes && QuestId == 0,
                        new Action(ret => _isBehaviorDone = true)),

					new Decorator(
						ret => UseType == QBType.PointToPoint,
						new PrioritySelector(
							new Decorator(
								ret => Me.Location.Distance(MoveToLocation) > 3,
								new Action(ret => Navigator.MoveTo(MoveToLocation))),
							new Sequence(
								new Action(ret => TreeRoot.StatusText = "Using Quest Item: " + Counter + " Out of " + NumOfTimes + " Times"),
								new Action(ret => Navigator.PlayerMover.MoveStop()),
								new Action(ret => Me.SetFacing(ClickToLocation)),
								new SleepForLagDuration(),
								new Action(ret => Item.UseContainerItem()),
								new SleepForLagDuration(),
								new Action(ret => Counter++),
								new Action(ret => SpellManager.ClickRemoteLocation(ClickToLocation)),
								new Action(ctx => _waitTimer.Reset())
							))),

                    new Decorator(
                        ret => UseType == QBType.PointToObject,
                        new PrioritySelector(
                            new Decorator(
                                ret => UseObject == null && Me.Location.DistanceSqr(MoveToLocation) >= 2 * 2,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Moving to location"),
                                    new Action(ret => Navigator.MoveTo(MoveToLocation)))),
                            new Decorator(
                                ret => UseObject != null,
                                new PrioritySelector(
                                    new Decorator(
                                        ret => UseObject.DistanceSqr >= Range * Range,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Moving closer to the object"),
                                            new Action(ret => Navigator.MoveTo(UseObject.Location)))),
                                    new Decorator(
                                        ret => UseObject.DistanceSqr < MinRange * MinRange,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Too Close, Backing Up"),
                                            new Action(ret => Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(Me.Location, UseObject.Location, (float)MinRange + 2f)))
                                            )),
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Using Item: " + UseObject.Name + " " + Counter + " Out of " + NumOfTimes + " Times"),
                                        new Action(ret => Navigator.PlayerMover.MoveStop()),
                                        new Action(ret => Me.SetFacing(UseObject.Location)),
                                        new Action(ret => StyxWoW.SleepForLagDuration()),
                                        new Action(ret => Item.UseContainerItem()),
                                        new Action(ret => Counter++),
                                        new Action(ret => StyxWoW.SleepForLagDuration()),
                                        new Action(ret => SpellManager.ClickRemoteLocation(UseObject.Location)),
                                        new Action(ret => _npcBlacklist.Add(UseObject.Guid)),
										new Action(ctx => _waitTimer.Reset())))),
                            new Action(ret => TreeRoot.StatusText = "No objects around. Waiting")
                            )),

                    new Decorator(
                        ret => UseType == QBType.ToObject,
                        new PrioritySelector(
                            new Decorator(
                                ret => UseObject != null,
                                new PrioritySelector(
                                    new Decorator(
                                        ret => UseObject.DistanceSqr >= Range * Range,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Moving to object's range"),
                                            new Action(ret => Navigator.MoveTo(UseObject.Location)))),
                                    new Decorator(
                                        ret => UseObject.DistanceSqr < MinRange * MinRange,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Too Close, Backing Up"),
                                            new Action(ret => Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(Me.Location, UseObject.Location, (float)MinRange + 2f)))
                                            )),
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Using Item: " + UseObject.Name + " " + Counter + " Out of " + NumOfTimes + " Times"),
                                        new Action(ret => Navigator.PlayerMover.MoveStop()),
                                        new Action(ret => Me.SetFacing(UseObject.Location)),
                                        new Action(ret => StyxWoW.SleepForLagDuration()),
                                        new Action(ret => Item.UseContainerItem()),
                                        new Action(ret => Counter++),
                                        new Action(ret => StyxWoW.SleepForLagDuration()),
                                        new Action(ret => SpellManager.ClickRemoteLocation(UseObject.Location)),
                                        new Action(ret => _npcBlacklist.Add(UseObject.Guid)),
                                        new Action(ctx => _waitTimer.Reset())))),
							new Decorator(
								ret => Me.Location.DistanceSqr(MoveToLocation) > 2 * 2,
								new Sequence(
									new Action(ret => TreeRoot.StatusText = "Moving to location"),
									new Action(ret => Navigator.MoveTo(MoveToLocation))))
                        ))
                    ));
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
            // This reports problems, and stops BT processing if there was a problem with attributes...
            // We had to defer this action, as the 'profile line number' is not available during the element's
            // constructor call.
            OnStart_HandleAttributeProblem();

            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (!IsDone)
            {
                this.UpdateGoalText(QuestId);
				_waitTimer = new WaitTimer(TimeSpan.FromMilliseconds(WaitTime));
            }
        }

        #endregion
    }
}
