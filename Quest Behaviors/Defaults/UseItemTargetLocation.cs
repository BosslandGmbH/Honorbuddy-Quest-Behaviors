// Behavior originally contributed by Raphus.
//
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_WaitTimer
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using CommonBehaviors.Actions;

using Styx.Logic.BehaviorTree;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class UseItemTargetLocation : CustomForcedBehavior
    {
        /// <summary>
        /// Allows you to use item on an object or at a location
        /// ##Syntax##
        /// [Optional] QuestId: Id of the quest. If specified the QB will run until the quest is completed
        /// [Optional] ObjectId: Id of the object/npc that the item will be used on
        /// ItemId: Id of the item that will be used
        /// [Optional]WaitTime: Time to wait after using the item 
        /// UseType: PointToObject (from X,Y,Z to an object's location)
        ///          PointToPoint  (from X,Y,Z to ClickToX,ClickToY,ClickToZ)
        ///          ToObject      (from range of an object to object's location)
        ///          Default is PointToPoint
        /// X,Y,Z: If the UseType is AtLocation, QB will move to that location before using item. Otherwise it will move towards that point to search for objects
        /// [Optional]ClickToX,ClickToY,ClickToZ: If the UseType is PoinToPoint, this location will be used to remote click 
        /// [Optional]Range: If the UseType is ToObject, QB will move to that range of an object/npc before using item. (default 4)
        /// </summary>
        /// 
        public enum QBType
        {
            PointToPoint = 0,
            PointToObject = 1,
            ToObject = 2
        }


        public UseItemTargetLocation(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                ClickToLocation = GetXYZAttributeAsWoWPoint("ClickTo", false, null) ?? WoWPoint.Empty;
                ItemId      = GetAttributeAsItemId("ItemId", true, null) ?? 0;
                MoveToLocation = GetXYZAttributeAsWoWPoint("", true, null) ?? WoWPoint.Empty;
                MobId       = GetAttributeAsMobId("MobId", false, new [] { "ObjectId" }) ?? 0;
                QuestId     = GetAttributeAsQuestId("QuestId", false, null) ?? 0;
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;
                Range       = GetAttributeAsRange("Range", false, null) ?? 4;
                UseType     = GetAttributeAsEnum<QBType>("UseType", false, null) ?? QBType.PointToPoint;
                WaitTime    = GetAttributeAsWaitTime("WaitTime", false, null) ?? 0;
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
        public WoWPoint                 ClickToLocation { get; private set; }
        public int                      ItemId { get; private set; }
        public int                      MobId { get; private set; }
        public WoWPoint                 MoveToLocation { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }
        public int                      Range { get; private set; }
        public QBType                   UseType { get; private set; }
        public int                      WaitTime { get; private set; }

        // Private variables for internal state
        private bool                _isBehaviorDone;
        private Composite           _root;

        // Private properties
        private WoWItem             Item { get { return Me.CarriedItems.FirstOrDefault(i => i.Entry == ItemId && i.Cooldown == 0); } }
        private LocalPlayer         Me { get { return (ObjectManager.Me); } }
        private WoWObject           UseObject { get { return ObjectManager.GetObjectsOfType<WoWObject>(true, false)
                                                                .Where(o => o.Entry == MobId)
                                                                .OrderBy(o => o.Distance)
                                                                .FirstOrDefault(); }}


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(
                    new Decorator(
                        ret => Item == null,
                        new ActionAlwaysSucceed()),

                    new Decorator(
                        ret => UseType == QBType.PointToPoint,
                        new PrioritySelector(
                            new Decorator(
                                ret => Me.Location.Distance(MoveToLocation) > 3,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Moving to location"),
                                    new Action(ret => Navigator.MoveTo(MoveToLocation)))),
                            new Sequence(
                                new Action(ret => TreeRoot.StatusText = "Using Item"),
                                new Action(ret => Navigator.PlayerMover.MoveStop()),
                                new Action(ret => Me.SetFacing(ClickToLocation)),
                                new Action(ret => StyxWoW.SleepForLagDuration()),
                                new Action(ret => Item.UseContainerItem()),
                                new Action(ret => StyxWoW.SleepForLagDuration()),
                                new Action(ret => LegacySpellManager.ClickRemoteLocation(ClickToLocation)),
                                new Action(ret => Thread.Sleep(WaitTime)),
                                new DecoratorContinue(
                                    ret => QuestId == 0,
                                    new Action(ret => _isBehaviorDone = true)))
                            )),

                    new Decorator(
                        ret => UseType == QBType.PointToObject,
                        new PrioritySelector(
                            new Decorator(
                                ret => Me.Location.Distance(MoveToLocation) > 3,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Moving to location"),
                                    new Action(ret => Navigator.MoveTo(MoveToLocation)))),
                            new Decorator(
                                ret => UseObject != null,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Using Item"),
                                    new Action(ret => Navigator.PlayerMover.MoveStop()),
                                    new Action(ret => StyxWoW.SleepForLagDuration()),
                                    new Action(ret => Item.UseContainerItem()),
                                    new Action(ret => StyxWoW.SleepForLagDuration()),
                                    new Action(ret => LegacySpellManager.ClickRemoteLocation(UseObject.Location)),
                                    new Action(ret => Thread.Sleep(WaitTime)),
                                    new DecoratorContinue(
                                        ret => QuestId == 0,
                                        new Action(ret => _isBehaviorDone = true)))),
                            new Action(ret => TreeRoot.StatusText = "No objects around. Waiting")
                            )),

                    new Decorator(
                        ret => UseType == QBType.ToObject,
                        new PrioritySelector(
                            new Decorator(
                                ret => UseObject != null,
                                new PrioritySelector(
                                    new Decorator(
                                        ret => UseObject.Distance > Range,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Moving to object's range"),
                                            new Action(ret => Navigator.MoveTo(UseObject.Location)))),
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Using Item"),
                                        new Action(ret => Navigator.PlayerMover.MoveStop()),
                                        new Action(ret => StyxWoW.SleepForLagDuration()),
                                        new Action(ret => Item.UseContainerItem()),
                                        new Action(ret => StyxWoW.SleepForLagDuration()),
                                        new Action(ret => LegacySpellManager.ClickRemoteLocation(UseObject.Location)),
                                        new Action(ret => Thread.Sleep(WaitTime)),
                                        new DecoratorContinue(
                                            ret => QuestId == 0,
                                            new Action(ret => _isBehaviorDone = true))))),
                            new Decorator(
                                ret => Me.Location.Distance(MoveToLocation) > 3,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Moving to location"),
                                    new Action(ret => Navigator.MoveTo(MoveToLocation))))
                        ))
                    ));
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
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = this.GetType().Name + ": " + ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
            }
        }

        #endregion
    }
}
