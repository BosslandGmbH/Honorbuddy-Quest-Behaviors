// Behavior originally contributed by Unknown.
//
// DOCUMENTATION:
//     
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using CommonBehaviors.Decorators;
using Styx;
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


namespace Honorbuddy.Quest_Behaviors.FlyTo
{
    [CustomBehaviorFileName(@"FlyTo")]
    class FlyTo : CustomForcedBehavior
    {
        public FlyTo(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                Destination = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
                DestinationName = GetAttributeAs<string>("DestName", false, ConstrainAs.StringNonEmpty, new[] { "Name" }) ?? string.Empty;
                Distance = GetAttributeAsNullable<double>("Distance", false, new ConstrainTo.Domain<double>(0.25, double.MaxValue), null) ?? 10.0;
                QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
                Land = GetAttributeAsNullable<bool>("Land", false, null, null) ?? false;
                IgnoreIndoors = GetAttributeAsNullable<bool>("IgnoreIndoors", false, null, null) ?? false;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;

                if (string.IsNullOrEmpty(DestinationName))
                { DestinationName = Destination.ToString(); }
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
        public WoWPoint Destination { get; private set; }
        public string DestinationName { get; private set; }
        public double Distance { get; private set; }
        public bool Land { get; private set; }
        public bool IgnoreIndoors { get; private set; }
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }

        // Private variables for internal state
        private QuestBehaviorCore.ConfigMemento _configMemento;
        private bool _isDisposed;
        private Composite _root;
        // ToDo: remove once LootMobs state is saved and restored by ConfigMemento
        private bool? _lootMobs;

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id: FlyTo.cs 501 2013-05-10 16:29:10Z chinajade $"); } }
        public override string SubversionRevision { get { return ("$Revision: 501 $"); } }


        ~FlyTo()
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
                    // ToDo: remove once LootMobs state is saved and restored by ConfigMemento
                    ProfileManager.CurrentProfile.LootMobs = _lootMobs;
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


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return (_root ?? (_root = 
                new PrioritySelector(
                    new Decorator(
                        ret => Land && Destination.DistanceSqr(StyxWoW.Me.Location) < Distance * Distance,
                        new Mount.ActionLandAndDismount()),
					// Don't run FlyTo when there is a poi set
					new DecoratorIsPoiType(PoiType.None, 
						new Action(ret => Flightor.MoveTo(Destination, !IgnoreIndoors))))));
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
                return ((Destination.Distance(StyxWoW.Me.Location) <= Distance) && (!Land || !StyxWoW.Me.Mounted)     // normal completion
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
                // The ConfigMemento() class captures the user's existing configuration.
                // After its captured, we can change the configuration however needed.
                // When the memento is dispose'd, the user's original configuration is restored.
                // More info about how the ConfigMemento applies to saving and restoring user configuration
                // can be found here...
                //     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_Saving_and_Restoring_User_Configuration
                _configMemento = new QuestBehaviorCore.ConfigMemento();
                // ToDo: remove once LootMobs state is saved and restored by ConfigMemento
                _lootMobs = ProfileManager.CurrentProfile.LootMobs;
                BotEvents.OnBotStop += BotEvents_OnBotStop;

                // Disable any settings that may cause us to dismount --
                // When we mount for travel via FlyTo, we don't want to be distracted by other things.
                // We also set PullDistance to its minimum value.  If we don't do this, HB will try
                // to dismount and engage a mob if it is within its normal PullDistance.
                // NOTE: these settings are restored to their normal values when the behavior completes
                // or the bot is stopped.
                CharacterSettings.Instance.HarvestHerbs = false;
                CharacterSettings.Instance.HarvestMinerals = false;
                CharacterSettings.Instance.LootChests = false;
                ProfileManager.CurrentProfile.LootMobs = false;
                CharacterSettings.Instance.NinjaSkin = false;
                CharacterSettings.Instance.SkinMobs = false;
                CharacterSettings.Instance.PullDistance = 1;

                TreeRoot.GoalText = "Flying to " + DestinationName;

                // This information was directly requested by profile writers...
                LogMessage("debug", "Flying to '{0}': {1}.", DestinationName, Destination);
            }
        }

        #endregion
    }
}
