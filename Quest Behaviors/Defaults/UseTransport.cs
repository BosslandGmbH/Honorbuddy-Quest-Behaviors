using System;
using System.Collections.Generic;
using System.Linq;

using Styx.Logic;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class UseTransport : CustomForcedBehavior
    {
        /// <summary>
        /// Transport by raphus
        /// Allows you to use Transports.
        /// ##Syntax##
        /// TransportId: ID of the transport.
        /// TransportStart: Start point of the transport that we will get on when its close enough to that point.
        /// TransportEnd: End point of the transport that we will get off when its close enough to that point.
        /// WaitAt: Where you wish to wait the transport at
        /// GetOff: Where you wish to end up at when transport reaches TransportEnd point
        /// StandOn: The point you wish the stand while you are in the transport
        /// </summary>
        ///
        public UseTransport(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                WoWPoint?   legacyEndLocation       = LegacyGetAttributeAsWoWPoint("End", false, null, "TransportEnd");
                WoWPoint?   legacyGetOffLocation    = LegacyGetAttributeAsWoWPoint("Exit", false, null,"GetOff");
                WoWPoint?   legacyStartLocation     = LegacyGetAttributeAsWoWPoint("Start", false, null, "TransportStart");
                WoWPoint?   legacyWaitAtLocation    = LegacyGetAttributeAsWoWPoint("Entry", false, null, "WaitAt");

                Counter         = 0;
                EndLocation     = GetXYZAttributeAsWoWPoint("TransportEnd", !legacyEndLocation.HasValue, null)
                                    ?? legacyEndLocation
                                    ?? WoWPoint.Empty;
                GetOffLocation  = GetXYZAttributeAsWoWPoint("GetOff", !legacyGetOffLocation.HasValue, null)
                                    ?? legacyGetOffLocation
                                    ?? WoWPoint.Empty;
                StandLocation   = GetXYZAttributeAsWoWPoint("StandOn", false, null) ?? WoWPoint.Empty;
                StartLocation   = GetXYZAttributeAsWoWPoint("TransportStart", !legacyStartLocation.HasValue, null)
                                    ?? legacyStartLocation
                                    ?? WoWPoint.Empty;
                TransportId     = GetAttributeAsMobId("TransportId", true, new [] { "Transport" }) ?? 0;
                WaitAtLocation  = GetXYZAttributeAsWoWPoint("WaitAt", !legacyWaitAtLocation.HasValue, null)
                                    ?? legacyWaitAtLocation
                                    ?? WoWPoint.Empty;
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


        public WoWPoint                 WaitAtLocation { get; private set; }
        public WoWPoint                 GetOffLocation { get; private set; }
        public WoWPoint                 StartLocation { get; private set; }
        public WoWPoint                 EndLocation { get; private set; }
        public WoWPoint                 StandLocation { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }
        public int                      TransportId { get; private set; }

        private bool                _isBehaviorDone;
        private Composite           _root;
        private bool                _usedTransport;
        private bool                _wasOnWaitLocation;

        private int                 Counter { get; set; }
        private LocalPlayer         Me { get { return (ObjectManager.Me); } }
        private bool                MovedOnShip { get; set; }
        private bool                MovedToTarget { get; set; }
        private bool                OnShip { get; set; }


        private WoWPoint TransportLocation
        {
            get
            {
               var transport = ObjectManager.GetObjectsOfType<WoWGameObject>(true, false).FirstOrDefault(o => o.Entry == TransportId);

               if (transport == null)
                   return WoWPoint.Empty;

               //Tripper.Tools.Math.Matrix m = transport.GetWorldMatrix();

               //return new WoWPoint(m.M41, m.M42, m.M43);

               return transport.WorldLocation;
            }
        }


        #region Legacy XML support

        private WoWPoint?   LegacyGetAttributeAsWoWPoint(string    attributeName,
                                                         bool      isRequired,
                                                         string[]  attributeAliases,
                                                         string     preferredName)
        {
            double[]    tmpPoint    = GetAttributeAsDoubleArray(attributeName, isRequired, double.MinValue, double.MaxValue, attributeAliases);

            if (tmpPoint == null)
                { return (null); }

            UtilLogMessage("warning", string.Format("The attribute '{0}' is DEPRECATED.\n"
                                                    + "Please modify the profile to use the new '{1}' attribute, instead.",
                                                    attributeName, preferredName));

            if (tmpPoint.Length != 3)
            {
                UtilLogMessage("error", string.Format("The '{0}' attribute's value should have three"
                                                      + " coordinate contributions (saw '{1}')",
                                                      attributeName,
                                                      tmpPoint.Length));
                IsAttributeProblem = true;
                return (null);
            }

            return (new WoWPoint(tmpPoint[0], tmpPoint[1], tmpPoint[2]));
        }

        #endregion


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(
                    new Decorator(
                        ret => !_wasOnWaitLocation,
                        new PrioritySelector(
                            new Decorator(
                                ret => WaitAtLocation.Distance(Me.Location) > 2,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Moving to wait location"),
                                    new Action(ret => Navigator.MoveTo(WaitAtLocation)))),
                            new Sequence(
                                new Action(ret => Navigator.PlayerMover.MoveStop()),
                                new Action(ret => Mount.Dismount()),
                                new Action(ret => _wasOnWaitLocation = true),
                                new Action(ret => TreeRoot.StatusText = "Waiting for transport")))),
                    new Decorator(
                        ret => TransportLocation != WoWPoint.Empty && TransportLocation.Distance(EndLocation) < 2 && _usedTransport,
                        new PrioritySelector(
                            new Decorator(
                                ret => Me.Location.Distance(GetOffLocation) > 2,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Moving out of transport"),
                                    new Action(ret => Navigator.PlayerMover.MoveTowards(GetOffLocation)),
                                    new Action(ret => StyxWoW.SleepForLagDuration()),
                                    new DecoratorContinue(
                                        ret => Me.IsOnTransport,
                                        new Action(ret => WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend, TimeSpan.FromMilliseconds(50)))))),
                            new Action(ret => _isBehaviorDone = true))),
                    new Decorator(
                        ret => Me.IsOnTransport && StandLocation != WoWPoint.Empty && !_usedTransport,
                        new PrioritySelector(
                            new Decorator(
                                ret => Me.Location.Distance2D(StandLocation) > 2,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Moving to stand location"),
                                    new Action(ret => Navigator.PlayerMover.MoveTowards(StandLocation)))),
                            new Sequence(
                                new Action(ret => _usedTransport = true),
                                new Action(ret => Navigator.PlayerMover.MoveStop()),
                                new Action(ret => TreeRoot.StatusText = "Waiting for the end location"))
                        )),
                    new Decorator(
                        ret => TransportLocation != WoWPoint.Empty && TransportLocation.Distance(StartLocation) < 2 && !_usedTransport,
                        new PrioritySelector(
                            new Decorator(
                                ret => Me.Location.Distance2D(TransportLocation) > 2,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Moving inside transport"),
                                    new Action(ret => Navigator.PlayerMover.MoveTowards(TransportLocation)),
                                    new Action(ret => StyxWoW.SleepForLagDuration()),
                                    new DecoratorContinue(
                                        ret => !Me.IsOnTransport,
                                        new Action(ret => WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend, TimeSpan.FromMilliseconds(50)))))),
                            new Sequence(
                                new Action(ret => _usedTransport = true),
                                new Action(ret => Navigator.PlayerMover.MoveStop()),
                                new Action(ret => TreeRoot.StatusText = "Waiting for the end location"))))
                    ));
        }


        public override bool IsDone
        {
            get { return (_isBehaviorDone); }
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

