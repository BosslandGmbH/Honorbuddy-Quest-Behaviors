using System;
using System.Collections.Generic;

using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors.FlyTo
{
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
                Destination     = GetXYZAttributeAsWoWPoint("", true, null) ?? WoWPoint.Empty;
                DestinationName = GetAttributeAsString_NonEmpty("DestinationName", false, new [] { "Name" }) ?? "";
                Distance        = GetAttributeAsDouble("Distance", false, 0.25, double.MaxValue, null) ?? 10.0;
                QuestId         = GetAttributeAsQuestId("QuestId", false, null) ?? 0;
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;

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
				UtilLogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
										+ "\nFROM HERE:\n"
										+ except.StackTrace + "\n");
				IsAttributeProblem = true;
			}
        }


        public WoWPoint                 Destination { get; private set; }
        public string                   DestinationName { get; private set; }
        public double                   Distance { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }

        private ConfigMemento   _configMemento;
        private bool            _isDisposed;
        private Composite       _root;


        ~FlyTo()
        {
            Dispose(false);
        }

        public void     Dispose(bool    isExplicitlyInitiatedDispose)
        {
            if (!_isDisposed)
            {
                // NOTE: we should call any Dispose() method for any managed or unmanaged
                // resource, if that resource provides a Dispose() method.

                // Clean up managed resources, if explicit disposal...
                if (isExplicitlyInitiatedDispose)
                {
                    if (_configMemento != null)
                    {
                        _configMemento.Dispose();
                        _configMemento = null;
                    }
                }

                // Clean up unmanaged resources (if any) here...
                BotEvents.OnBotStop -= BotEvents_OnBotStop;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }


        public void    BotEvents_OnBotStop(EventArgs args)
        {
             Dispose(true);
        }


        #region Overrides of CustomForcedBehavior

        protected override TreeSharp.Composite CreateBehavior()
        {
            return (_root ?? (_root = new Action(ret => Flightor.MoveTo(Destination))));
        }


        public override void    Dispose()
        {
             Dispose(true);
        }


        public override bool IsDone
        {
            get
            {
                return ((Destination.Distance(StyxWoW.Me.Location) <= Distance)     // normal completion
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
                _configMemento = new ConfigMemento();
                BotEvents.OnBotStop  += BotEvents_OnBotStop;

                // Disable any settings that may cause us to dismount --
                // When we mount for travel via FlyTo, we don't want to be distracted by other things.
                // We also set PullDistance to its minimum value.  If we don't do this, HB will try
                // to dismount and engage a mob if it is within its normal PullDistance.
                // NOTE: these settings are restored to their normal values when the behavior completes
                // or the bot is stopped.
                LevelbotSettings.Instance.HarvestHerbs = false;
                LevelbotSettings.Instance.HarvestMinerals = false;
                LevelbotSettings.Instance.LootChests = false;
                LevelbotSettings.Instance.LootMobs = false;
                LevelbotSettings.Instance.NinjaSkin = false;
                LevelbotSettings.Instance.SkinMobs = false;
                LevelbotSettings.Instance.PullDistance = 1;

                TreeRoot.GoalText = "Flying to " + DestinationName;
            }
		}

        #endregion


        #region Stopgap services (remove when later HBcore drop provides these)

        /// <summary>
        /// <para>This class captures the current Honorbuddy configuration.  When the memento is Dispose'd
        /// the configuration that existed when the memento was created is restored.</para>
        /// <para>More info about how this class applies to saving and restoring user configuration
        /// can be found here...
        ///     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_Saving_and_Restoring_User_Configuration
        /// </para>
        /// </summary>
        public new sealed class ConfigMemento
        {
            /// <summary>
            /// Creating a memento captures the Honorbuddy configuration that exists when the memento
            /// is created.  You can then alter the Honorbuddy configuration as you wish.  To restore
            /// the configuration to its original state, just Dispose of the memento.
            /// </summary>
            public ConfigMemento()
            {
                _characterSettings = CharacterSettings.Instance.GetXML();
                _levelBotSettings  = LevelbotSettings.Instance.GetXML();
                _styxSettings      = StyxSettings.Instance.GetXML();
            }


            ~ConfigMemento()
            {
                Dispose(false);
            }

            /// <summary>
            /// Disposing of a memento restores the Honorbuddy configuration that existed when
            /// the memento was created.
            /// </summary>
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            public /*virtual*/ void     Dispose(bool    isExplicitlyInitiatedDispose)
            {
                if (!_isDisposed)
                {
                    // NOTE: we should call any Dispose() method for any managed or unmanaged
                    // resource, if that resource provides a Dispose() method.

                    // Clean up managed resources, if explicit disposal...
                    if (isExplicitlyInitiatedDispose)
                    {
                        if (_characterSettings != null)
                            { CharacterSettings.Instance.LoadFromXML(_characterSettings); }
                        if (_levelBotSettings != null)
                            { LevelbotSettings.Instance.LoadFromXML(_levelBotSettings); }
                        if (_styxSettings != null)
                            { StyxSettings.Instance.LoadFromXML(_styxSettings); }

                        _characterSettings = null;
                        _levelBotSettings = null;
                        _styxSettings = null;
                     }

                    // Clean up unmanaged resources (if any) here...

                    // Call parent Dispose() (if it exists) here ...
                    // base.Dispose();
                }

                _isDisposed = true;
            }

   
            public override string  ToString()
            {
                string      outString   = "";

                if (_isDisposed)
                    { throw (new ObjectDisposedException(this.GetType().Name)); }

                if (_characterSettings != null)
                    { outString += (_characterSettings.ToString() + "\n"); }
                if (_levelBotSettings != null)
                    { outString += (_levelBotSettings.ToString() + "\n"); }
                if (_styxSettings != null)
                    { outString += (_styxSettings.ToString() + "\n"); }
   
                return (outString);
            }
   
            private System.Xml.Linq.XElement        _characterSettings;
            bool                                    _isDisposed         = false;
            private System.Xml.Linq.XElement        _levelBotSettings;
            private System.Xml.Linq.XElement        _styxSettings;             
        }

        #endregion
    }
}
