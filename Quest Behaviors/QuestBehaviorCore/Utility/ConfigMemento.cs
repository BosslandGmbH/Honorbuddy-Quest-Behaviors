// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.


#region Usings
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Bots.Grind;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Profiles.Quest;
using Styx.Helpers;
using Styx.Pathing;

#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public class ConfigMemento
    {
        /// <summary>
        /// The ConfigMemento() class captures the user's existing configuration.
        /// After its captured, we can change the configuration however needed.
        /// When the memento is dispose'd, the user's original configuration is restored.
        /// More info about how the ConfigMemento applies to saving and restoring user configuration
        /// can be found here...
        ///     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_Saving_and_Restoring_User_Configuration
        /// </summary>
        public ConfigMemento()
        {
            // Settings...
            _settingsCharacter = CharacterSettings.Instance.GetXML();
            _settingsLevelBot = LevelbotSettings.Instance.GetXML();
            _settingsStyx = GlobalSettings.Instance.GetXML();

            // Sub-Mementos...
            _subMementoLevelBot = new LevelBotMemento();
            _subMementoNavigator = new NavigatorMemento();
            _subMementoProfileSettings = new ProfileSettingsMemento();
        }


        #region Private and Convenience variables
        private bool _isDisposed = false;
        private XElement _settingsCharacter;
        private XElement _settingsLevelBot;
        private XElement _settingsStyx;
        private LevelBotMemento _subMementoLevelBot;
        private NavigatorMemento _subMementoNavigator;
        private ProfileSettingsMemento _subMementoProfileSettings;
        #endregion


        /// <summary>   Finaliser. </summary>
        ~ConfigMemento()
        {
            Dispose(false);
        }


        /// <summary>
        /// Disposing of a memento restores the Honorbuddy configuration that existed when the memento
        /// was created.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        /// <summary>
        /// Disposing of a memento restores the Honorbuddy configuration that existed when the memento
        /// was created.
        /// </summary>
        ///
        /// <param name="isExplicitlyInitiatedDispose"> true if this object is explicitly initiated
        ///                                             dispose. </param>
        public void Dispose(bool isExplicitlyInitiatedDispose)
        {
            if (!_isDisposed)
            {
                // NOTE: we should call any Dispose() method for any managed or unmanaged
                // resource, if that resource provides a Dispose() method.

                // Clean up managed resources, if explicit disposal...
                if (isExplicitlyInitiatedDispose)
                {
                    // empty for now
                }

                // Clean up unmanaged resources (if any) here...

                // Sub-mementos...
                if (_subMementoLevelBot != null)
                {
                    _subMementoLevelBot.Restore();
                    _subMementoLevelBot = null;
                }

                if (_subMementoNavigator != null)
                {
                    _subMementoNavigator.Restore();
                    _subMementoNavigator = null;
                }

                if (_subMementoProfileSettings != null)
                {
                    _subMementoProfileSettings.Restore();
                    _subMementoProfileSettings = null;
                }

                // Settings...
                if (_settingsCharacter != null)
                {
                    CharacterSettings.Instance.LoadFromXML(_settingsCharacter);
                    CharacterSettings.Instance.Save();
                    _settingsCharacter = null;
                }

                if (_settingsLevelBot != null)
                {
                    LevelbotSettings.Instance.LoadFromXML(_settingsLevelBot);
                    LevelbotSettings.Instance.Save();
                    _settingsLevelBot = null;
                }

                if (_settingsStyx != null)
                {
                    GlobalSettings.Instance.LoadFromXML(_settingsStyx);
                    GlobalSettings.Instance.Save();
                    _settingsStyx = null;
                }


                // Call parent Dispose() (if it exists) here ...
                // base.Dispose();
            }

            _isDisposed = true;
        }


        /// <summary>
        /// Returns a <see cref="T:System.String" /> that represents the current
        /// <see cref="T:System.Object" />.
        /// </summary>
        ///
        /// <exception cref="ObjectDisposedException">  Thrown when a supplied object has been disposed. </exception>
        ///
        /// <returns>
        /// A <see cref="T:System.String" /> that represents the current <see cref="T:System.Object" />.
        /// </returns>
        public override string ToString()
        {
            string outString = "";

            if (_isDisposed)
                { throw (new ObjectDisposedException(this.GetType().Name)); }

            if (_settingsCharacter != null)
                { outString += (_settingsCharacter.ToString() + "\n"); }
            if (_settingsLevelBot != null)
                { outString += (_settingsLevelBot.ToString() + "\n"); }
            if (_settingsStyx != null)
                { outString += (_settingsStyx.ToString() + "\n"); }

            return (outString);
        }


        // NB: Not all LevelBot settings are in LevelBotSettings--some are in the
        // LevelBot itself.  This memento captures the settings embedded in the LevelBot
        // proper.
        private class LevelBotMemento
        {
            private readonly BehaviorFlags _behaviorFlags;
            private readonly bool _shouldUseSpiritHealer;


            public LevelBotMemento()
            {
                _behaviorFlags = LevelBot.BehaviorFlags;
                _shouldUseSpiritHealer = LevelBot.ShouldUseSpiritHealer;
            }


            public void Restore()
            {
                LevelBot.BehaviorFlags = _behaviorFlags;
                LevelBot.ShouldUseSpiritHealer = _shouldUseSpiritHealer;
            }
        }


        // This memento captures the settings embedded in the Navigator
        // that a quest behavior is interested in altering.
        private class NavigatorMemento
        {
            private readonly float _pathPrecision;


            public NavigatorMemento()
            {
                _pathPrecision = Navigator.PathPrecision;
            }


            public void Restore()
            {
                Navigator.PathPrecision = _pathPrecision;
            }
        }
        
        
        // This class captures any profile settings that can be directly modified (i.e.,
        // that have a 'setter' operation defined for them), or indirectly modified
        // (i.e., they directly expose their internal representation).
        private class ProfileSettingsMemento
        {
            private readonly Dictionary<Tuple<uint, WoWFactionGroup>, List<Vector2[]>> _aerialBlackspots;
            private readonly DualHashSet<uint, string> _avoidMobs;
            private readonly Dictionary<uint, BlacklistFlags> _blacklist;
            private readonly DualHashSet<string, uint> _blacklistQuestgivers;
            private readonly DualHashSet<string, uint> _blacklistedQuests;
            private readonly List<Blackspot> _blackspots;
            // IMMUTABLE: private readonly int _continentId;
            private readonly HashSet<uint> _factions;
            private readonly DualHashSet<uint, string> _forceMail;
            // IMMUTABLE: private readonly Styx.CommonBot.AreaManagement.GrindArea _grindArea;
            // NON-CONFIGURATION: private readonly Styx.CommonBot.AreaManagement.HotspotManager _hotspotManager;
            private readonly bool? _lootMobs;
            private readonly double? _lootRadius;
            // IMMUTABLE: private readonly bool _mailBlue;
            // NON-CONFIGURATION: private readonly MailboxManager _mailboxManager;
            // IMMUTABLE: private readonly bool _mailGreen;
            // IMMUTABLE: private readonly bool _mailGrey;
            // IMMUTABLE: private readonly bool _mailPurple;
            private readonly List<WoWItemQuality> _mailQualities;
            // IMMUTABLE: private readonly bool _mailWhite;
            // IMMUTABLE: private readonly int _maxLevel;
            // IMMUTABLE: private readonly float _minDurability;
            // IMMUTABLE: private readonly int _minFreeBagSlots;
            // IMMUTABLE: private readonly int _minLevel;
            // IMMUTABLE: private readonly int _minMailLevel;
            private readonly HashSet<uint> _mobIds;
            // IMMUTABLE: private readonly string _name;
            // NON-CONFIGURATION: private readonly Styx.CommonBot.Profiles.Profile _parent;
            private readonly DualHashSet<uint, string> _protectedItems;
            // IMMUTABLE: private readonly Styx.CommonBot.Profiles.Quest.Order.OrderNodeCollection _questOrder;
            private readonly List<QuestInfo> _quests;
            // IMMUTABLE: private readonly bool _sellBlue;
            // IMMUTABLE: private readonly bool _sellGreen;
            // IMMUTABLE: private readonly bool _sellGrey;
            // IMMUTABLE: private readonly bool _sellPurple;
            // IMMUTABLE: private readonly bool _sellWhite;
            private readonly List<Profile> _subProfiles;
            // IMMUTABLE: private readonly bool _targetElites;
            private readonly double? _targetingDistance;
            // IMMUTABLE: private readonly int _targetMaxLevel;
            // IMMUTABLE: private readonly int _targetMinLevel;
            private readonly bool? _useMount;
            // NON-CONFIGURATION: private readonly VendorManager _vendorManager;
            // NON-CONFIGURATION: private readonly XmlElement _xmlElement;


            public ProfileSettingsMemento()
            {
                var currentProfile = ProfileManager.CurrentProfile;

                if (currentProfile != null)
                {
                    // NB: We can't use reflection, because not all data members are 'configuration items'.
                    // Of the configuration items, not all of them are mutable.  And of the mutable ones,
                    // some are directly mutable, and others are indirectly mutable. (So, different techniques
                    // must be used).
                    _aerialBlackspots = new Dictionary<Tuple<uint, WoWFactionGroup>, List<Vector2[]>>(currentProfile.AerialBlackspots);
                    _avoidMobs = currentProfile.AvoidMobs.Clone();
                    _blacklist = new Dictionary<uint, BlacklistFlags>(currentProfile.Blacklist);
                    _blacklistQuestgivers = currentProfile.BlacklistedQuestgivers.Clone();
                    _blacklistedQuests = currentProfile.BlacklistedQuests.Clone();
                    _blackspots = new List<Blackspot>(currentProfile.Blackspots);
                    _factions = new HashSet<uint>(currentProfile.Factions);
                    _forceMail = currentProfile.ForceMail.Clone();
                    _lootMobs = currentProfile.LootMobs;
                    _lootRadius = currentProfile.LootRadius;
                    _mailQualities = new List<WoWItemQuality>(currentProfile.MailQualities);
                    _mobIds = new HashSet<uint>(currentProfile.MobIDs);
                    _protectedItems = currentProfile.ProtectedItems.Clone();
                    _quests = new List<QuestInfo>(currentProfile.Quests);
                    _subProfiles = new List<Profile>(currentProfile.SubProfiles);
                    _targetingDistance = currentProfile.TargetingDistance;
                    _useMount = currentProfile.UseMount;
                }
            }


            public void Restore()
            {
                var currentProfile = ProfileManager.CurrentProfile;

                if (currentProfile != null)
                {
                    // NB: We can't use reflection, because not all data members are 'configuration items'.
                    // Of the configuration items, not all of them are mutable.  And of the mutable ones,
                    // some are directly mutable, and others are indirectly mutable. (So, different techniques
                    // must be used).
                    // NB: We depend on HBcore to suppress the logging of any setting for which the 'new'
                    // value is the same as the 'current' value.
                    currentProfile.AerialBlackspots.CopyFrom(_aerialBlackspots);
                    currentProfile.AvoidMobs.CopyFrom(_avoidMobs);
                    currentProfile.Blacklist.CopyFrom(_blacklist);
                    currentProfile.BlacklistedQuestgivers.CopyFrom(_blacklistQuestgivers);
                    currentProfile.BlacklistedQuests.CopyFrom(_blacklistedQuests);
                    currentProfile.Blackspots.CopyFrom(_blackspots);
                    currentProfile.Factions.CopyFrom(_factions);
                    currentProfile.ForceMail.CopyFrom(_forceMail);
                    currentProfile.LootMobs = _lootMobs;
                    currentProfile.LootRadius = _lootRadius;
                    currentProfile.MailQualities.CopyFrom(_mailQualities);
                    currentProfile.MobIDs.CopyFrom(_mobIds);
                    currentProfile.ProtectedItems.CopyFrom(_protectedItems);
                    currentProfile.Quests.CopyFrom(_quests);
                    currentProfile.SubProfiles.CopyFrom(_subProfiles);
                    currentProfile.TargetingDistance = _targetingDistance;
                    currentProfile.UseMount = _useMount;
                }
            }
        }
    }
}
