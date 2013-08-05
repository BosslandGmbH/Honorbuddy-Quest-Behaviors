// Behavior originally contributed by Raphus.
//
// DOCUMENTATION:
//     
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.Plugins;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.UseTransport
{
    [CustomBehaviorFileName(@"UseTransport")]
    public class UseTransport : CustomForcedBehavior
    {
        /// <summary>
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

                StartLocation = GetAttributeAsNullable<WoWPoint>("TransportStart", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;
                EndLocation = GetAttributeAsNullable<WoWPoint>("TransportEnd", false, ConstrainAs.WoWPointNonEmpty,null) ?? Me.Location;
                GetOffLocation = GetAttributeAsNullable<WoWPoint>("GetOff", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;
                StandLocation = GetAttributeAsNullable<WoWPoint>("StandOn", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;
                WaitAtLocation = GetAttributeAsNullable<WoWPoint>("WaitAt", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;

                DestName = GetAttributeAs<string>("DestName", false, ConstrainAs.StringNonEmpty, null) ?? "";

                TransportId = GetAttributeAsNullable<int>("TransportId", true, ConstrainAs.MobId, new[] { "Transport" }) ?? 0;
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
                                        + "\nFROM HERE:\n"
                                        + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }


        // Attributes provided by caller
        public string DestName { get; private set; }
        public WoWPoint EndLocation { get; private set; }
        public WoWPoint GetOffLocation { get; private set; }
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }
        public WoWPoint StandLocation { get; private set; }
        public WoWPoint StartLocation { get; private set; }
        public int TransportId { get; private set; }
        public WoWPoint WaitAtLocation { get; private set; }

        // Private variables for internal state
        private ConfigMemento _configMemento;
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private Composite _root;

        // Private properties
        private LocalPlayer Me { get { return (StyxWoW.Me); } }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id: UseTransport.cs 501 2013-05-10 16:29:10Z chinajade $"); } }
        public override string SubversionRevision { get { return ("$Revision: 501 $"); } }


        ~UseTransport()
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
                if (_configMemento != null)
                { _configMemento.Dispose(); }

                _configMemento = null;

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


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(
                    new Decorator(
                        ret => GetOffLocation != WoWPoint.Empty && Me.Location.DistanceSqr(GetOffLocation) < 2*2,
                        new Sequence(
                            new Action(ret => LogMessage("Info", "Successfully used the transport.")),
                            new Action(ret => _isBehaviorDone = true))),
                    new Decorator(
                        ret => Me.IsOnTransport,
                        new PrioritySelector(
                             new Decorator(
                                ret => TransportLocation != WoWPoint.Empty && TransportLocation.DistanceSqr(EndLocation) < 1.5*1.5,
                                new Sequence(
                                    new Action(ret => LogMessage("Info", "Moving out of transport")),
                                    new Action(ret => Navigator.PlayerMover.MoveTowards(GetOffLocation)))),
                            new Action(ret => LogMessage("Info", "Waiting for the end location"))
                        )),
                    new Decorator(
                        ret => !Me.IsMoving,
                        new PrioritySelector(
                            new Decorator(
                                ret => TransportLocation != WoWPoint.Empty && TransportLocation.DistanceSqr(StartLocation) < 1.5*1.5 && WaitAtLocation.DistanceSqr(Me.Location) < 2 * 2,
                                new Sequence(
                                    new Action(ret => LogMessage("Info", "Moving inside transport")),
                                    new Action(ret => Navigator.PlayerMover.MoveTowards(StandLocation)))),
                            new Decorator(
                                ret => WaitAtLocation.DistanceSqr(Me.Location) > 2 * 2,
                                new Sequence(
                                    new Action(ret => LogMessage("Info", "Moving to wait location")),
                                    new Action(ret => Navigator.MoveTo(WaitAtLocation)))),
                            new Sequence(
                                new DecoratorContinue(
                                    ret => Me.Mounted,
                                    new Action(ret => Mount.Dismount())),
                                new Action(ret => LogMessage("Info", "Waiting for transport")))
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
                // The ConfigMemento() class captures the user's existing configuration.
                // After its captured, we can change the configuration however needed.
                // When the memento is dispose'd, the user's original configuration is restored.
                // More info about how the ConfigMemento applies to saving and restoring user configuration
                // can be found here...
                //     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_Saving_and_Restoring_User_Configuration
                _configMemento = new ConfigMemento();

                BotEvents.OnBotStop += BotEvents_OnBotStop;

                // Disable any settings that may cause distractions --
                // When we use transport, we don't want to be distracted by other things.
                // We also set PullDistance to its minimum value.
                // NOTE: these settings are restored to their normal values when the behavior completes
                // or the bot is stopped.
                CharacterSettings.Instance.HarvestHerbs = false;
                CharacterSettings.Instance.HarvestMinerals = false;
                CharacterSettings.Instance.LootChests = false;
                CharacterSettings.Instance.LootMobs = false;
                CharacterSettings.Instance.NinjaSkin = false;
                CharacterSettings.Instance.SkinMobs = false;
                CharacterSettings.Instance.PullDistance = 1;


                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = this.GetType().Name + ": " + ((!string.IsNullOrEmpty(DestName)) ? DestName :
                                                                  (quest != null) ? ("\"" + quest.Name + "\"") :
                                                                  "In Progress");
            }
        }


        #endregion
    }
}

