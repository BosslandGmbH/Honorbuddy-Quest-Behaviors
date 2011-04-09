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
                float       distance;
                WoWPoint    location;

                CheckForUnrecognizedAttributes(new Dictionary<string, object>()
                                                {
                                                    { "Distance",   null },
                                                    { "Name",       null },
                                                    { "X",          null },
                                                    { "Y",          null },
                                                    { "Z",          null },
                                                });

                _isAttributesOkay = true;
                _isAttributesOkay &= GetAttributeAsFloat("Distance", false, "10.0", 0.05f, float.MaxValue, out distance); 
                _isAttributesOkay &= GetAttributeAsString("Name", false, "", out _destinationName);
                _isAttributesOkay &= GetXYZAttributeAsWoWPoint(true, WoWPoint.Empty, out location);

                if (_isAttributesOkay)
                {
                    Location = location;
                    Distance = distance;

                    _configSnapshot = new HonorbuddyUserConfigSnapshot();
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
                }
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
				_isAttributesOkay = false;
			}
        }


        public float    Distance { get; private set; }
        public WoWPoint Location { get; private set; }

        private bool                            _isAttributesOkay;
        private HonorbuddyUserConfigSnapshot    _configSnapshot;
        private string                          _destinationName;
        private Composite                       _root;


        private void    BehaviorCleanup()
        {
           // Restore any settings we may have altered...
           if (_configSnapshot != null)
           {
                // Restore PullDistance to normal, and anything else we may have changed.
                _configSnapshot.Restore();
                _configSnapshot = null;
           }
   
            // Unhook event handler
           BotEvents.OnBotStop -= BotEvents_OnBotStop;
        }


        public void    BotEvents_OnBotStop(EventArgs args)
        {
             BehaviorCleanup();
        }

        public override void    Dispose()
        {
            BehaviorCleanup();
        }


        #region Overrides of CustomForcedBehavior

        protected override TreeSharp.Composite CreateBehavior()
        {
            return (_root ?? (_root = new Action(ret => Flightor.MoveTo(Location))));
        }


        public override bool IsDone
        {
            get { return (Location.Distance(StyxWoW.Me.Location) <= Distance); }
        }


        public override void OnStart()
		{
			if (!_isAttributesOkay)
			{
				UtilLogMessage("error", "Stopping Honorbuddy.  Please repair the profile!");

                // *Never* want to stop Honorbuddy (e.g., TreeRoot.Stop()) in the constructor --
                // This would defeat the "ProfileDebuggingMode" configurable that builds an instance of each
                // used behavior when the profile is loaded.
				TreeRoot.Stop();
			}

            else if (!IsDone)
            {
                TreeRoot.GoalText = string.Format("Flying to {0}", (!string.IsNullOrEmpty(_destinationName)
                                                                    ? _destinationName
                                                                    : Location.ToString()));
            }
		}

        #endregion
    }


    class HonorbuddyUserConfigSnapshot
    {
        public HonorbuddyUserConfigSnapshot()
        {
            _characterSettings = CharacterSettings.Instance.GetXML();
            _levelBotSettings  = LevelbotSettings.Instance.GetXML();
            _styxSettings      = StyxSettings.Instance.GetXML();
        }
   
        public void     Restore()
        {
            CharacterSettings.Instance.LoadFromXML(_characterSettings);
            LevelbotSettings.Instance.LoadFromXML(_levelBotSettings);
            StyxSettings.Instance.LoadFromXML(_styxSettings);
        }
   
        public string       GetSettingsAsString()
        {
            string      outString   = "";
               
            outString += _characterSettings.ToString();
            outString += _levelBotSettings.ToString();
            outString += _styxSettings.ToString();
   
            return (outString);
        }
   
        private System.Xml.Linq.XElement        _characterSettings;
        private System.Xml.Linq.XElement        _levelBotSettings;
        private System.Xml.Linq.XElement        _styxSettings;             
    }
}
