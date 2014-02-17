// Behavior originally contributed by Chinajade.
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
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_UserSettings
//
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
#endregion


namespace Honorbuddy.Quest_Behaviors.UserSettings
{
    [CustomBehaviorFileName(@"UserSettings")]
    class UserSettings : QuestBehaviorBase
    {
        #region Constructor and Argument Processing
        public UserSettings(Dictionary<string, string> args)
            : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            try
            {
                // Build the 'presets'...
                // Note that the "UserOriginal" configuration will also be captured as a preset.
                _presetChangeSets = BuildPresets();

                // Behavior-specific attributes...
                PersistedDebugShowChangesApplied = GetAttributeAsNullable<bool>("DebugShowChangesApplied", false, null, null)
                    ?? PersistedDebugShowChangesApplied;
                DebugShowDetails = GetAttributeAsNullable<bool>("DebugShowDetails", false, null, null) ?? false;
                DebugShowDiff = GetAttributeAsNullable<bool>("DebugShowDiff", false, null, null) ?? false;
                PresetName = GetAttributeAs<string>("Preset", false, new ConstrainTo.SpecificValues<string>(_presetChangeSets.Keys.ToArray()), null) ?? "";
                IsStopBot = GetAttributeAsNullable<bool>("StopBot", false, null, null) ?? false;

                // Attempted to read the 'recognized attributes', so they won't be marked as "not recognized" by the argument processor...
                foreach (var recognizedAttribute in ChangeSet.RecognizedSettings)
                {
                    GetAttributeAs<object>(recognizedAttribute.Name, false, null, null);
                }

                _userChangeRequest = ChangeSet.FromXmlAttributes(args,
                    new List<string>()
                        {
                            // Behavior-specific attributes...
                            "DebugShowChangesApplied",
                            "DebugShowDetails",
                            "DebugShowDiff",
                            "Preset",
                            "StopBot",

                            // QuestBehaviorBase attributes...
                            "QuestId",
                            "QuestCompleteRequirement",
                            "QuestInLogRequirement",
                            "QuestObjectiveIndex",
                            "IgnoreMobsInBlackspots",
                            "MovementBy",
                            "NonCompeteDistance",
                            "TerminateWhen",
                            "TerminationChecksQuestProgress",
                        });

                // If we were unable to create an (even empty) changeset, then we ran into a problem...
                if (_userChangeRequest == null)
                    { IsAttributeProblem = true;  }
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
        private bool DebugShowDetails { get; set; }
        private bool DebugShowDiff { get; set; }
        private bool IsStopBot { get; set; }
        private string PresetName { get; set; }


        protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
        {
            UsageCheck_DeprecatedAttribute(xElement,
                Args.Keys.Contains("LootMobs"),
                "LootMobs",
                context => string.Format("Please update profile to use <LootMobs Value=\"{1}\" />, instead.",
                    Environment.NewLine,
                    Args["LootMobs"]));

            UsageCheck_DeprecatedAttribute(xElement,
                Args.Keys.Contains("PullDistance"),
                "PullDistance",
                context => string.Format("Please update profile to use <TargetingDistance Value=\"{1}\" />, instead.{0}"
                    + "  To restore the original value when done, <TargetingDistance Value=\"null\" />.{0}"
                    + "  Please do not fiddle with TargetingDistance unless _absolutely_ necessary.",
                    Environment.NewLine,
                    Args["PullDistance"]));

            UsageCheck_DeprecatedAttribute(xElement,
                Args.Keys.Contains("UseMount"),
                "UseMount",
                context => string.Format("Please update profile to use <UseMount Value=\"{1}\" />, instead.",
                    Environment.NewLine,
                    Args["UseMount"]));
        }

        protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
        {
            // Empty, for now...
            // See TEMPLATE_QB for an example.
        }
        #endregion


        #region Private and Convenience variables
        private readonly Dictionary<string, ChangeSet> _presetChangeSets;
        private readonly ChangeSet _userChangeRequest;

        // Persisted Data...
        private static bool PersistedDebugShowChangesApplied = false;
        private static bool PersistedIsBotStopHooked = false;
        #endregion


        #region Overrides of QuestBehaviorBase
        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id$"); } }
        public override string SubversionRevision { get { return ("$Revision$"); } }


        protected override ConfigMemento CreateConfigMemento()
        {
            // Suppress the creation of a ConfigMemento...
            // We do NOT want the user settings restored after we have altered them with this behavior.
            return null;
        }


        public override void OnFinished()
        {
            // Defend against being called multiple times (just in case)...
            if (!IsOnFinishedRun)
            {
                // Call parent Dispose() (if it exists) here ...
                base.OnFinished();
            }
        }


        public override void OnStart()
        {
            // Let QuestBehaviorBase do basic initialization of the behavior, deal with bad or deprecated attributes,
            // capture configuration state, install BT hooks, etc.  This will also update the goal text.
            var isBehaviorShouldRun = OnStart_QuestBehaviorCore();

            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (isBehaviorShouldRun)
            {
                var logInfo = new StringBuilder();
                var logDeveloperInfo = new StringBuilder();

                // The BotStop handler will put the original configuration settings back in place...
                // Note, we only want to hook it once for this behavior.
                if (!PersistedIsBotStopHooked)
                {
                    BotEvents.OnBotStopped += BotEvents_OnBotStopped;
                    PersistedIsBotStopHooked = true;
                }

                // First, process Preset request, if any...
                if (!string.IsNullOrEmpty(PresetName))
                {
                    var presetChangeSet =
                       (from preset in _presetChangeSets
                        where preset.Key == PresetName
                        select preset.Value)
                        .FirstOrDefault();

                    if (presetChangeSet == null)
                    {
                        QBCLog.Error("Unable to locate any preset named '{0}'", PresetName);
                        TreeRoot.Stop();
                    }

                    var appliedChanges = presetChangeSet.Apply("    ");

                    var appliedChangesBuilder = PersistedDebugShowChangesApplied ? logInfo : logDeveloperInfo;
                    appliedChangesBuilder.AppendFormat("Using preset '{0}'...{1}", PresetName, appliedChanges);
                    appliedChangesBuilder.Append(Environment.NewLine);
                }

                // Second, apply any change requests...
                if (_userChangeRequest.Count > 0)
                {
                    string appliedChanges = _userChangeRequest.Apply("    ");

                    var appliedChangesBuilder = PersistedDebugShowChangesApplied ? logInfo : logDeveloperInfo;
                    appliedChangesBuilder.AppendFormat("Applied changes...{0}", appliedChanges);
                    appliedChangesBuilder.Append(Environment.NewLine);
                }

                // Third, show state, if requested...                
                if (DebugShowDetails)
                {
                    var currentConfiguration = ChangeSet.FromCurrentConfiguration();

                    logInfo.AppendFormat("Details...{0}", currentConfiguration.BuildDetails("    "));
                    logInfo.Append(Environment.NewLine);
                }

                var diffBuilder = DebugShowDiff ? logInfo : logDeveloperInfo;
                diffBuilder.AppendFormat("Difference from user's original settings...{0}",
                    ChangeSet.BuildDifferencesFromOriginalSettings("    "));
                diffBuilder.Append(Environment.NewLine);

                // Forth, stop the bot, if requested...
                if (IsStopBot)
                {
                    const string message = "Stopping the bot per profile request.";
                    logInfo.AppendFormat(message);
                    logInfo.Append(Environment.NewLine);

                    var logInfoString = logInfo.ToString();
                    if (!string.IsNullOrEmpty(logInfoString))
                        { QBCLog.Info(logInfoString); }

                    var logDeveloperString = logDeveloperInfo.ToString();
                    if (!string.IsNullOrEmpty(logDeveloperString))
                        { QBCLog.DeveloperInfo(logDeveloperString); }

                    TreeRoot.Stop(message);
                }

                else
                {
                    var logInfoString = logInfo.ToString();
                    if (!string.IsNullOrEmpty(logInfoString))
                        { QBCLog.Info(logInfoString); }

                    var logDeveloperString = logDeveloperInfo.ToString();
                    if (!string.IsNullOrEmpty(logDeveloperString))
                        { QBCLog.DeveloperInfo(logDeveloperString); }
                }

                BehaviorDone();
            }
        }
        #endregion


        private void BotEvents_OnBotStopped(EventArgs args)
        {
            // Restore the user's original configuration, since the bot is stopping...
            if (ChangeSet.OriginalConfiguration != null)
            {
                var changesApplied = ChangeSet.OriginalConfiguration.Apply("    ", true);
                var logMessage =
                    string.Format("Bot stopping.  Original user settings restored as follows...{0}",
                        (!string.IsNullOrEmpty(changesApplied)
                        ? changesApplied
                        : (Environment.NewLine + "    Original Settings intact--no changes to restore.")));

                if (PersistedDebugShowChangesApplied)
                    { QBCLog.Info(this, logMessage); }
                else
                    { QBCLog.DeveloperInfo(this, logMessage); }

                // Remove our OnBotStop handler
                BotEvents.OnBotStopped -= BotEvents_OnBotStopped;

                // Reset persistent data...
                PersistedIsBotStopHooked = false;
                PersistedDebugShowChangesApplied = false;
                ChangeSet.OriginalConfiguration = null;
                ChangeSet.RecognizedSettings = null;
            }
        }


        #region Preset ChangeSets
        // To add, adjust, or remove presets, this is the only method that needs to be modified...
        // All other code in this class uses the information contained in the returned presetChangeRequests.
        // Note: If you make a spelling error while maintaining this code, by design an exception will be thrown
        // at runtime pointing you directly to the problem.
        private Dictionary<string, ChangeSet> BuildPresets()
        {
            var presets = new Dictionary<string, ChangeSet>
                {
                    {
                        "Grind",
                        new ChangeSet(new Dictionary<string, object>()
                        {
                            { "GroundMountFarmingMode", false },
                            { "KillBetweenHotspots", true },
                        })
                    },
                    {
                        "HarvestsOff",
                        new ChangeSet(new Dictionary<string, object>()
                        {
                            { "HarvestHerbs", false },
                            { "HarvestMinerals", false },
                            { "LootMobs", false },
                            { "NinjaSkin", false },
                            { "SkinMobs", false },
                        })
                    },
                    {
                        "HarvestsOn",
                        new ChangeSet(new Dictionary<string, object>()
                        {
                            { "HarvestHerbs", (Me.GetSkill(SkillLine.Herbalism).MaxValue > 0) },
                            { "HarvestMinerals", (Me.GetSkill(SkillLine.Mining).MaxValue > 0) },
                            { "LootMobs", true },
                            { "LootRadius", 45 },
                            { "NinjaSkin", (Me.GetSkill(SkillLine.Skinning).MaxValue > 0) },
                            { "SkinMobs", (Me.GetSkill(SkillLine.Skinning).MaxValue > 0) },
                        })
                    },
                    {
                        "NoDistractions",
                        new ChangeSet(new Dictionary<string, object>()
                        {
                            { "GroundMountFarmingMode", true },
                            { "HarvestHerbs", false },
                            { "HarvestMinerals", false },
                            { "KillBetweenHotspots", false },
                            { "LootMobs", false },
                            { "NinjaSkin", false },
                            { "SkinMobs", false },
                        })
                    },
                    {
                        "NoTrain",
                        new ChangeSet(new Dictionary<string, object>())
                    },
                    {
                        "NormalQuesting",
                        new ChangeSet(new Dictionary<string, object>()
                        {
                            { "GroundMountFarmingMode", false },
                            { "HarvestHerbs", (Me.GetSkill(SkillLine.Herbalism).MaxValue > 0) },
                            { "HarvestMinerals", (Me.GetSkill(SkillLine.Mining).MaxValue > 0) },
                            { "KillBetweenHotspots", false },
                            { "LootMobs", true },
                            { "LootRadius", 45 },
                            { "NinjaSkin", (Me.GetSkill(SkillLine.Skinning).MaxValue > 0) },
                            { "RessAtSpiritHealers", false },
                            { "SkinMobs", (Me.GetSkill(SkillLine.Skinning).MaxValue > 0) },
                            { "UseRandomMount", true },
                        })
                    },
                    {
                        "UserOriginal",
                        ChangeSet.OriginalConfiguration
                    }
                };

            return (presets);
        }
        #endregion
    }


    //==================================================
    // All classes below this point are support for getting the work done
    //

    public class ChangeSet
    {
        public ChangeSet(Dictionary<string, object> changes)
        {
            var changeSet = new List<Tuple<SettingDescriptor, object>>();
            var isProblemAttribute = false;

            foreach (var change in changes)
            {
                try
                {
                    var name = change.Key;
                    var value = change.Value;

                    // Setting name cannot be null or empty...
                    if (string.IsNullOrEmpty(name))
                    {
                        QBCLog.Error("Name may not be null or empty");
                        isProblemAttribute = true;
                        continue;
                    }

                    // Check that setting exists...
                    var settingDescriptor = RecognizedSettings.FirstOrDefault(s => s.Name == name);
                    if (settingDescriptor == null)
                    {
                        QBCLog.Error("Unable to locate setting for {0}", name);
                        isProblemAttribute = true;
                        continue;
                    }

                    // Check that setting doesn't already exist in the changeset...
                    if (changeSet.Any(t => t.Item1.Name == name))
                    {
                        QBCLog.Error("Setting '{0}' already exists in the changeset.", name);
                        isProblemAttribute = true;
                        continue;
                    }

                    // If user specified 'original' value, go look it up and substitute it for 'value'...
                    if ((value is string) && ((string)value == "original"))
                    {
                        object originalValue;

                        if (!OriginalConfiguration.TryGetValue(settingDescriptor.Name, out originalValue))
                        {
                            // A missing 'original configuration' is a maintenance issue, not a user error...
                            QBCLog.MaintenanceError("For setting '{0}', there is no original configuration value.",
                                settingDescriptor.Name);
                            isProblemAttribute = true;
                            continue;
                        }

                        value = originalValue;
                    }
                    
                    // Check that setting is an appropriate type...
                    var newValue = settingDescriptor.ToCongruentObject(value);

                    if (!settingDescriptor.ConstraintChecker.IsWithinConstraints(newValue))
                    {
                        QBCLog.Error("For setting '{0}', the provided value '{1}' is not within the required constraints of {2}.",
                            name, value, settingDescriptor.ConstraintChecker.Description);
                        isProblemAttribute = true;
                        continue;
                    }
                    
                    // Setting change is acceptable...
                    changeSet.Add(Tuple.Create(settingDescriptor, value));
                }

                catch (Exception)
                {
                    isProblemAttribute = true;
                }
            }

            // If problem encountered with any change, we're unable to build the ChangeSet...
            if (isProblemAttribute)
            {
                _changeSet = null;
                throw new ArgumentException("Problems encountered with provided argument");
            }

            Count = changeSet.Count;
            _changeSet = new ReadOnlyCollection<Tuple<SettingDescriptor, object>>(changeSet);
        }

        private readonly ReadOnlyCollection<Tuple<SettingDescriptor, object>> _changeSet;
        public int Count { get; private set; }


        public static ReadOnlyCollection<SettingDescriptor> RecognizedSettings
        {
            get { return _recognizedSettings ?? (_recognizedSettings = BuildRecognizedSettings()); }

            // Any attempts to set the value, will 'uninitialize' it...
            set { _recognizedSettings = null; }
        }
        private static ReadOnlyCollection<SettingDescriptor> _recognizedSettings;


        public static ChangeSet OriginalConfiguration
        {
            get { return _originalConfiguration ?? (_originalConfiguration = ChangeSet.FromCurrentConfiguration()); }

            // Any attempts to set the value, will 'uninitialize' it...
            set { _originalConfiguration = null; }
        }
        private static ChangeSet _originalConfiguration;


        public string Apply(string linePrefix = "", bool onlyApplyChangesIfDifferent = false)
        {
            var changesApplied = new StringBuilder();

            foreach (var change in _changeSet.OrderBy(t => t.Item1.Name))
            {
                var settingDescriptor = change.Item1;
                var value = change.Item2;
                var previousValue = settingDescriptor.GetValue();
                object originalValue;

                OriginalConfiguration.TryGetValue(settingDescriptor.Name, out originalValue);


                if (!onlyApplyChangesIfDifferent || !value.Equals(previousValue))
                {
                    settingDescriptor.SetValue(value);

                    changesApplied.AppendFormat("{0}{1}{2} = {3} (previous: {4};  original: {5})",
                        Environment.NewLine, linePrefix, settingDescriptor.Name, value, previousValue, originalValue);
                }
            }

            return changesApplied.ToString();
        }


        private bool TryGetValue(string name, out object outValue)
        {
            Contract.Requires(!string.IsNullOrEmpty(name), context => "Name cannot be null or empty");

            var changeEntry = _changeSet.FirstOrDefault(v => v.Item1.Name == name);
            if (changeEntry == null)
            {
                outValue = null;
                return false;
            }

            outValue = changeEntry.Item2;
            return true;
        }


        public string BuildDetails(string linePrefix)
        {
            var builder = new StringBuilder();

            foreach (var change in _changeSet.OrderBy(t => t.Item1.Name))
            {
                object originalValue = null;
                OriginalConfiguration.TryGetValue(change.Item1.Name, out originalValue);

                var instanceName = change.Item1.SettingsInstance.GetType().Name;

                if (change.Item2.Equals(originalValue))
                {
                    builder.AppendFormat("{0}{1}{2}.{3} = {4}",
                        Environment.NewLine, linePrefix, instanceName, change.Item1.Name, change.Item2);
                }
                else
                {
                    builder.AppendFormat("{0}{1}{2}.{3} = {4} (original: {5})",
                        Environment.NewLine, linePrefix, instanceName, change.Item1.Name, change.Item2, originalValue);
                }
            }

            return builder.ToString();
        }


        public static string BuildDifferencesFromOriginalSettings(string linePrefix)
        {
            var builder = new StringBuilder();

            foreach (var setting in OriginalConfiguration._changeSet.OrderBy(t => t.Item1.Name))
            {
                var currentValue = setting.Item1.GetValue();

                if (currentValue.Equals(setting.Item2))
                    { continue; }

                builder.AppendFormat("{0}{1}{2} = {3} (originally: {4})",
                    Environment.NewLine, linePrefix, setting.Item1.Name, currentValue, setting.Item2);
            }

            if (builder.Length <= 0)
            {
                builder.AppendFormat("{0}{1}No changes from original settings", 
                    Environment.NewLine, linePrefix);
            }

            return builder.ToString();
        }


        // Note: The RecognizedAttribute's Dictionary value field was left open for user data, by design.
        // We take advantage of that here by storing ConfigurationDescriptors to help us further process
        // the data by moving it into and out of the appropriate properties.
        // Note: If you make a spelling error while maintaining this code, by design an exception will be thrown
        // at runtime pointing you directly to the problem.
        private static ReadOnlyCollection<SettingDescriptor> BuildRecognizedSettings()
        {
            // Attach constraints to particular elements --
            var constraints = new Dictionary<string, ConstraintChecker>()
            {
                { "DrinkAmount",            new ConstrainInteger(0, 100) },
                { "FoodAmount",             new ConstrainInteger(0, 100) },
                { "LogoutInactivityTimer",  new ConstrainInteger(1, int.MaxValue) },
                { "LootRadius",             new ConstrainInteger(0, 100) },
                { "MountDIstance",          new ConstrainInteger(30, 200) },
                { "TicksPerSecond",         new ConstrainInteger(5, 100) }
            };

            var ignoredPropertyNames =
                new List<string>()
                {
                    "EnabledPlugins",
                    "FindVendorsAutomatically",
                    "FormLocationX",
                    "FormLocationY",
                    "LastUsedPath",
                    "LogLevel",
                    "LootChests",
                    "MailRecipient",
                    "MeshesFolderPath",
                    "MountDistance",
                    "Password",
                    "PluginKey",
					"RecentProfiles",
                    "SelectedBotIndex",
                    "UseFlightPaths",
                    "Username",
                };
            var recognizedSettings = new List<SettingDescriptor>();
            var settingsInstances = new Settings[]
                {   // ordering is significant--earlier setting names mask later setting names in this list
                    CharacterSettings.Instance,
                    LevelbotSettings.Instance,
                    GlobalSettings.Instance
                };

            // Allowed 'Configuration' attributes--
            // A default value of 'null' means the item has no default value.
            var noConstraintCheck = new NoConstraint();
            foreach (var settingsInstance in settingsInstances)
            {
                foreach (var configItemName in from propertyName in settingsInstance.GetSettings().Keys
                                               where
                                                    !string.IsNullOrEmpty(propertyName)
                                                    && !ignoredPropertyNames.Contains(propertyName)
                                               select propertyName)
                {
                    if (recognizedSettings.All(setting => setting.Name != configItemName))
                    {
                        var constraintChecker =
                            constraints.Keys.Contains(configItemName)
                            ? constraints[configItemName]
                            : noConstraintCheck;

                        recognizedSettings.Add(new SettingDescriptor(settingsInstance, configItemName, constraintChecker));
                    }
                }
            }

            return (new ReadOnlyCollection<SettingDescriptor>(recognizedSettings));
        }


        // Factories...
        public static ChangeSet FromCurrentConfiguration()
        {
            return new ChangeSet(RecognizedSettings.ToDictionary(
                setting => setting.Name,
                setting => setting.GetValue()
                ));
        }


        // If 'null' return, then error was encountered, and offending messages already logged...
        // Otherwise, a (possibly empty) ChangeSet is returned.
        public static ChangeSet FromXmlAttributes(Dictionary<string, string> attributes, List<string> excludedAttributes)
        {
            var changes = new Dictionary<string, object>();

            try
            {
                foreach (var attribute in attributes.Where(kvp => !excludedAttributes.Contains(kvp.Key)))
                {
                    changes.Add(attribute.Key, attribute.Value);
                }

                return new ChangeSet(changes);
            }

            catch (Exception)
            {
                // empty
            }

            return null;
        }
    }


    #region ConstraintChecker classes
    public abstract class ConstraintChecker
    {
        protected ConstraintChecker(string description = null)
        {
            Description = description ?? string.Empty;
        }

        public string Description { get; private set; }

        public abstract bool IsWithinConstraints(object value);
    }


    public class ConstrainInteger : ConstraintChecker
    {
        public ConstrainInteger(int minValue, int maxValue)
            : base(string.Format("[{0}..{1}]", minValue, maxValue))
        {
            _maxValue = maxValue;
            _minValue = minValue;
        }

        private readonly int _maxValue;
        private readonly int _minValue;


        public override bool IsWithinConstraints(object value)
        {
            try
            {
                var newValue = (int)Convert.ChangeType(value, typeof(int));

                return (newValue >= _minValue) && (newValue <= _maxValue);
            }
            catch (Exception)
            {
                // empty
            }

            return false;
        }
    }


    public class NoConstraint : ConstraintChecker
    {
        public NoConstraint()
            : base(string.Empty)
        {
        }

        public override bool IsWithinConstraints(object value)
        {
            return true;
        }
    }
    #endregion


    /// <summary>
    /// Captures the details of a property that the user may alter.
    /// It provides generic Get/Set mechanics without regard of 'type'.
    /// </summary>
    public class SettingDescriptor
    {
        public SettingDescriptor(Settings settingsInstance, 
                                string name,
                                ConstraintChecker constraintCheck)
        {
            // We are a bit aggressive in our error checking here--
            // The most likely source of errors will be people that maintain the code in the future,
            // and we want to weed out as many newbie mistakes as possible.
            Contract.Requires(
                !string.IsNullOrEmpty(name),
                context => "name cannot be null or empty.");
            Contract.Requires(
                settingsInstance != null,
                context => String.Format("Null settingsInstance now allowed for {0}", name));
            Contract.Requires(settingsInstance.GetSettings().Any(kvp => kvp.Key == name),
                context => string.Format("The settingsInstance does not contain a \"{0}\" property", name));

            ConstraintChecker = constraintCheck;
            Name = name;
            SettingsInstance = settingsInstance;
        }

        public ConstraintChecker ConstraintChecker { get; private set; }
        public string Name { get; private set; }
        public Settings SettingsInstance { get; private set; }


        public object GetValue()
        {
            var propertyInfo = SettingsInstance.GetType().GetProperty(Name);

            return propertyInfo.GetValue(SettingsInstance, null);
        }


        public void SetValue(object newValueAsObject)
        {
            var propertyInfo = SettingsInstance.GetType().GetProperty(Name);
            var newValue = ToCongruentObject(newValueAsObject);

            if (!ConstraintChecker.IsWithinConstraints(newValue))
            {
                var message = string.Format("For '{0}', provided value ('{1}') is not within required constraints {2}.",
                                            Name, newValue, ConstraintChecker.Description);
                QBCLog.Error(message);
                throw new ArgumentException(message);
            }

            propertyInfo.SetValue(SettingsInstance, newValue, null);
        }


        // Largely used to convert 'string' representation of a value into the value's type...
        public object ToCongruentObject(object value)
        {
            var propertyInfo = SettingsInstance.GetType().GetProperty(Name);
            var backingType = propertyInfo.PropertyType;
            var providedType = value.GetType();

            try
            {
                // Disallow int => bool conversions...
                // These are almost _always_ mistakes on the profile writer's part.
                if ((providedType == typeof(int) && (backingType == typeof(bool))))
                    { throw new ArgumentException(); }

                return Convert.ChangeType(value, backingType);
            }
            catch (Exception)
            {
                var message = string.Format("For setting '{0}', the provided value '{1}' ({2})"
                                            + " cannot be converted to the backing type ({3}).",
                                            Name, value, providedType.Name, backingType.Name);
                QBCLog.Error(message);
                throw new ArgumentException(message);
            }
        }
    }
}
