// Behavior originally contributed by Chinajade.
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
// The sole purpose of this behavior is to report compatibility issues when the
// user attempts to use the Honorbuddy for questing.  The addon does checks
// for the following:
//    * Looks for incompatible WoWclient addons, and reports them as errors
//    * Looks for incompatible Honorbuddy plugsin, and reports them as errors
//    * Makes certain the questing bot isn't running as MixedMode
//    * Makes certain WoWclient auto-loot is turned on
//    * Makes certain the WoWclient is running in Windowed mode
//    * Makes certain the WoWclient FPS is suitable for questing
//    * Makes certain the WoWclient Latency is suitable for questing
//    * etc.
// If the addon encounters any of these situations, it emits error messages and
// terminates the profile.
//
// BEHAVIOR ATTRIBUTES:
//      AllowBrokenAddons [optional; Default: false]
//          When set to true and incompatible addons are encountered,
//          the addon errors are converted to warnings, and the profile
//          will not be terminated due to this condition.
//      AllowBrokenPlugins [optional; Default: false]
//          When set to true and incompatible plugins are encountered,
//          the plugin errors are converted to warnings, and the profile
//          will not be terminated due to this condition.
//      AllowMixedModeBot [optional; Default: false]
//          When set to true and the user has selected Mixed Mode,
//          the MixedMode error is converted to warnings, and the profile
//          will not be terminated due to this condition.
//
// THINGS TO KNOW:
//
#endregion


#region Examples
// EXAMPLE:
//     <CustomBehavior File="ProfileCompatibilityInfo" />
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;


using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Plugins;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;


#endregion


namespace Honorbuddy.Quest_Behaviors.ProfileCompatibilityInfo
{
    [CustomBehaviorFileName(@"Misc\ProfileCompatibilityInfo")]
    public class ProfileCompatibilityInfo : CustomForcedBehavior
    {
        #region Constructor and Argument Processing
        public ProfileCompatibilityInfo(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // NB: Core attributes are parsed by QuestBehaviorBase parent (e.g., QuestId, NonCompeteDistance, etc)

                // Behavior-specific attributes...
                AllowBrokenAddOns = GetAttributeAsNullable<bool>("AllowBrokenAddOns", false, null, null) ?? false;
                AllowBrokenPlugIns = GetAttributeAsNullable<bool>("AllowBrokenPlugIns", false, null, null) ?? false;
                AllowMixedModeBot = GetAttributeAsNullable<bool>("AllowMixedModeBot", false, null, null) ?? false;
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it can be quickly
                // resolved.
                QBCLog.Error("[MAINTENANCE PROBLEM]: " + except.Message
                        + "\nFROM HERE:\n"
                        + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }


        // Variables for Attributes provided by caller
        private bool AllowBrokenAddOns { get; set; }
        private bool AllowBrokenPlugIns { get; set; }
        private bool AllowMixedModeBot { get; set; }
        #endregion


        #region Private and Convenience variables
        // Match is case-insensitive StartsWith.
        // NB: *Please* keep alphabetized.  Any dependent addons will also
        // be ignored if the base addon is disabled.
        private static readonly string[] KnownProblemGameClientAddOnNames =
            {
                "Bartender4",           // Bartender4: http://www.zygorguides.com/
                "CollectMe",            // CollectMe: http://www.curse.com/addons/wow/collect_me
                "ElvUI",                // ElvUI: http://www.tukui.org/dl.php
                "Tukui",                // TukUI: http://www.tukui.org/dl.php
                "Zygor Guides Viewer"   // Zygor Guides: http://www.zygorguides.com/
            };

        // Match is case-insensitive StartsWith.
        // NB: *Please* keep alphabetized
        private static readonly string[] KnownProblemPlugInNames =
            {
                // empty, for now
            };

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private const int MaximumLatencyWarningThreshold = 850;
        private const int MinimumFpsWarningThreshold = 7;


        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return "$Id$"; } }
        public override string SubversionRevision { get { return "$Rev$"; } }
        #endregion


        #region Destructor, Dispose, and cleanup
        // Empty, for now...
        #endregion


        #region Overrides of CustomForcedBehavior

        // CreateBehavior supplied by QuestBehaviorBase.
        // Instead, provide CreateMainBehavior definition.


        // Dispose provided by QuestBehaviorBase.


        // IsDone provided by QuestBehaviorBase.
        // Call the QuestBehaviorBase.BehaviorDone() method when you want to indicate your behavior is complete.


        public override bool IsDone
        {
            get { return true; }
        }


        public override void OnStart()
        {
            QBCLog.BehaviorLoggingContext = this;

            var isFatalErrorsEncountered = EmitStateInfo();

            if (isFatalErrorsEncountered)
                { TreeRoot.Stop("Game client state is incompatible with profile.  Please repair errors."); }
        }
        #endregion


        #region Helpers
        private void BuildGameClientAddOnList(StringBuilder builder, string linePrefix, out List<string> problemAddOnList)
        {
            problemAddOnList = new List<string>();

            var addOns = new Dictionary<string, bool>();
            var numAddOns = Lua.GetReturnVal<int>("return GetNumAddOns()", 0);

            // Build list of game client addon names & their enabled/disabled state...
            for (int i = 1; i <= numAddOns; ++i)
            {
                // NB: We check 'enabled' and 'loadable' for addons...
                // If an addon cannot be loaded because it is out-of-date, or missing 
                // a dependency, then it is indirectly disabled.
                var addOnInfoQuery = string.Format("return GetAddOnInfo({0})", i);
                var addOnTitle = StripUiEscapeSequences(Lua.GetReturnVal<string>(addOnInfoQuery, 1));
                var addOnEnabled =
                    Lua.GetReturnVal<bool>(addOnInfoQuery, 3) /*enabled*/
                    && Lua.GetReturnVal<bool>(addOnInfoQuery, 4) /*loadable*/;

                addOns.Add(addOnTitle ?? string.Empty, addOnEnabled);
            }


            // Analyze addons for known problem ones...
            builder.AppendFormat("{0}Game client addons:", linePrefix);
            builder.Append(Environment.NewLine);
            if (!addOns.Any())
                { builder.AppendFormat("{0}    NONE", linePrefix); }
            else
            {
                foreach (var addOn in addOns.OrderBy(a => a.Key))
                {
                    var addOnName = addOn.Key;
                    var addOnEnabled = addOn.Value;
                    var attentionPrefix = string.Empty;
                    var enabledMessage = addOnEnabled ? "enabled" : "disabled";
                    var isProblemAddOn = IsKnownProblemName(KnownProblemGameClientAddOnNames, addOnName) && addOnEnabled;

                    if (isProblemAddOn)
                    {
                        problemAddOnList.Add(addOnName);
                        attentionPrefix = "*** ";
                        enabledMessage = "ENABLED ***PROBLEMATICAL*** ADDON";
                    }

                    builder.AppendFormat("{0}    {1}{2}: {3}",
                        linePrefix,
                        attentionPrefix,
                        (string.IsNullOrEmpty(addOnName) ? "UnnamedAddOn" : addOnName),
                        enabledMessage);
                    builder.Append(Environment.NewLine);
                }
            }
        }


        private void BuildPluginList(StringBuilder builder, string linePrefix, out List<string> problemPlugInList)
        {
            problemPlugInList = new List<string>();

            var plugIns = PluginManager.Plugins;

            // Analyze plugins for known problem ones...
            builder.AppendFormat("{0}Plugins:", linePrefix);
            builder.Append(Environment.NewLine);
            if (!plugIns.Any())
                { builder.AppendFormat("{0}    NONE", linePrefix); }
            else
            {
                foreach (var plugIn in plugIns.OrderBy(plugin => plugin.Name))
                {
                    var attentionPrefix = string.Empty;
                    var enabledMessage = plugIn.Enabled ? "enabled" : "disabled";
                    var isProblemPlugIn = IsKnownProblemName(KnownProblemPlugInNames, plugIn.Name) && plugIn.Enabled;

                    if (isProblemPlugIn)
                    {
                        problemPlugInList.Add(plugIn.Name);
                        attentionPrefix = "*** ";
                        enabledMessage = "ENABLED ***PROBLEMATICAL*** PLUGIN";
                    } 
                    
                    builder.AppendFormat("{0}    {1}{2} v{3} (by {4}): {5}",
                        linePrefix,
                        attentionPrefix,
                        (string.IsNullOrEmpty(plugIn.Name) ? "UnnamedPlugin" : plugIn.Name),
                        plugIn.Version,
                        (string.IsNullOrEmpty(plugIn.Author) ? "UnknownAuthor" : plugIn.Author),
                        enabledMessage);
                    builder.Append(Environment.NewLine);
                }
            }
        }


        private bool EmitStateInfo()
        {
            var fps = GetFPS();
            var latency = StyxWoW.WoWClient.Latency;
            var linePrefix = new String(' ', 4);

            // Build compatibility info...
            var builderInfo = new StringBuilder();

            builderInfo.Append("---------- BEGIN: Profile Compatibility Info ----------");

            // Toon info...
            builderInfo.Append(Environment.NewLine);
            builderInfo.AppendFormat("{0}Toon: {1:F1} {2} {3} ({4}, {5})",
                linePrefix,
                Me.LevelFraction,
                Me.Race,
                Me.Class,
                Me.Gender,
                GetPlayerFaction(Me)
                );
            builderInfo.Append(Environment.NewLine);

            builderInfo.AppendFormat("{0}Location: {1} => {2} => {3} => {4}",
                linePrefix,
                GetCurrentMapContinentName(),
                (Me.ZoneText ?? "noZone"),
                (Me.SubZoneText ?? "noSubZone"),
                Me.Location);
            builderInfo.Append(Environment.NewLine);
            builderInfo.AppendFormat("{0}Profile: {1}", linePrefix, GetCurrentProfileName());
            builderInfo.Append(Environment.NewLine);
            builderInfo.AppendFormat("{0}Combat Routine: {1}", linePrefix, GetCombatRoutineName());
            builderInfo.Append(Environment.NewLine);


            // Executable environment info...
            builderInfo.Append(Environment.NewLine);
            builderInfo.AppendFormat("{0}Honorbuddy: v{1}", linePrefix, Assembly.GetEntryAssembly().GetName().Version);
            builderInfo.Append(Environment.NewLine);
            builderInfo.AppendFormat("{0}Game client: v{1} ({2} FPS, {3}ms latency)",
                linePrefix,
                StyxWoW.GameVersion,
                fps,
                latency);
            builderInfo.Append(Environment.NewLine);
            builderInfo.AppendFormat("{0}    Auto Loot? {1}", linePrefix, (IsGameClientAutoLootEnabled() ? "enabled" : "DISABLED"));
            builderInfo.Append(Environment.NewLine);
            builderInfo.AppendFormat("{0}    Windowed mode? {1}", linePrefix, (IsGameClientWindowedModeEnabled() ? "enabled" : "DISABLED"));
            builderInfo.Append(Environment.NewLine);


            // Configuration info...
            List<string> problemAddOnList;
            builderInfo.Append(Environment.NewLine);
            BuildGameClientAddOnList(builderInfo, linePrefix, out problemAddOnList);

            List<string> problemPlugInList;
            builderInfo.Append(Environment.NewLine);
            BuildPluginList(builderInfo, linePrefix, out problemPlugInList);


            // Warnings & Errors...
            var builderErrors = new StringBuilder();
            var builderWarnings = new StringBuilder();

            // Does Combat Routine match toon's class?
            // NB: This can happen if the user logs out and back in to the game client
            // without restarting HB.
            if (Me.Class != RoutineManager.Current.Class)
            {
                builderErrors.AppendFormat("{0}* Combat Routine class({1}) does not match Toon class({2})"
                    + "--please restart Honorbuddy.",
                    linePrefix,
                    RoutineManager.Current.Class,
                    Me.Class);
                builderErrors.Append(Environment.NewLine);
            }

            // Is AutoLoot turned off?
            if (!IsGameClientAutoLootEnabled())
            {
                builderErrors.AppendFormat("{0}* AutoLoot is not enabled in game client"
                    + "--please turn it on.",
                    linePrefix);
                builderErrors.Append(Environment.NewLine);
            }


            // Is Windowed mode?
            if (!IsGameClientWindowedModeEnabled())
            {
                builderErrors.AppendFormat("{0}* The game client is running in 'full screen' mode"
                    + "--please set it to 'windowed' mode.",
                    linePrefix);
                builderErrors.Append(Environment.NewLine);                
            }


            // Mixed mode?
            if (IsMixedModeBot())
            {
                var builder = AllowMixedModeBot ? builderWarnings : builderErrors;

                builder.AppendFormat("{0}* MixedMode bot is not compatible with this profile"
                    + "--please select >Questing< bot.",
                    linePrefix);
                builder.Append(Environment.NewLine);
            }

            // Problematic AddOns?
            if (problemAddOnList.Any())
            {
                var builder = AllowBrokenAddOns ? builderWarnings : builderErrors;

                builder.AppendFormat("{0}* Problematic game client addons were encountered"
                    + "--please disable these addons:{1}",
                    linePrefix,
                    string.Join(", ", problemAddOnList.Select(addOnName => string.Format("{0}{1}    {2}", Environment.NewLine, linePrefix, addOnName))));
                builder.Append(Environment.NewLine);
            }

            // Problematic PlugIns?
            if (problemPlugInList.Any())
            {
                var builder = AllowBrokenPlugIns ? builderWarnings : builderErrors;

                builder.AppendFormat("{0}* Problematic plugins were encountered"
                    + "--please disable these plugins:{1}",
                    linePrefix,
                    string.Join(", ", problemPlugInList.Select(plugInName => string.Format("{0}{1}    {2}", Environment.NewLine, linePrefix, plugInName))));
                builder.Append(Environment.NewLine);
            }

            // Absurdly low FPS?
            if (fps < MinimumFpsWarningThreshold)
            {
                builderWarnings.AppendFormat("{0}* FPS ({1}) is absurdly low (expected {2} FPS, minimum).",
                    linePrefix,
                    fps,
                    MinimumFpsWarningThreshold);
                builderWarnings.Append(Environment.NewLine);
            }

            // Absurdly high latency?
            if (latency > MaximumLatencyWarningThreshold)
            {
                builderWarnings.AppendFormat("{0}* Latency ({1}ms) is absurdly high (expected {2}ms latency, maximum).",
                    linePrefix,
                    latency,
                    MaximumLatencyWarningThreshold);
                builderWarnings.Append(Environment.NewLine);                
            }


            // Emit compatibility info...
            QBCLog.DeveloperInfo(builderInfo.ToString());

            // Emit warnings...
            if (builderWarnings.Length > 0)
            {
                QBCLog.Warning("PROFILE COMPATIBILITY WARNINGS:{0}{1}",
                    Environment.NewLine,
                    builderWarnings.ToString());
            }

            // Emit errors...
            if (builderErrors.Length > 0)
            {
                QBCLog.Error("PROFILE COMPATIBILITY ERRORS:{0}{1}",
                    Environment.NewLine,
                    builderErrors.ToString());
            }

            // Emit end demark...
            QBCLog.DeveloperInfo("---------- END: Profile Compatibility Info ----------");

            // Return value indicating whether or not fatal errors encountered...
            return builderErrors.Length > 0;
        }

        
        private string GetCombatRoutineName()
        {
            if (RoutineManager.Current == null)
                { return "UnknownCombatRoutine";  }

            return string.IsNullOrEmpty(RoutineManager.Current.Name)
                ? "UnnamedCombatRoutine"
                : RoutineManager.Current.ToString();
        }


        private string GetCurrentMapContinentName()
        {
            var instanceName = Lua.GetReturnVal<string>("return GetInstanceInfo()", 0);

            return instanceName;
        }


        private string GetCurrentProfileName()
        {
            var currentProfile = ProfileManager.CurrentProfile;
    
            if (currentProfile == null)
                { return "No profile"; }

            return currentProfile.Name ?? "noProfileName";
        }


        private int GetFPS()
        { // swiped from Singular
            try
            {
                return (int)Lua.GetReturnVal<float>("return GetFramerate()", 0);
            }
            catch
            {
                // empty
            }

            return 0;
        }


        private string GetPlayerFaction(LocalPlayer localPlayer)
        {
            if (localPlayer.IsAlliance)
                { return "Alliance"; }

            if (localPlayer.IsHorde)
                { return "Horde"; }

            // Pandarans will be unaligned until level 10 or so...
            return "Unaligned";
        }


        private bool IsGameClientAutoLootEnabled()
        {
            var autoLootEnabled = Lua.GetReturnVal<bool>("return GetCVar('AutoLootDefault')", 0);

            return autoLootEnabled;
        }


        private bool IsGameClientWindowedModeEnabled()
        {
            var windowedModeEnabled = Lua.GetReturnVal<bool>("return GetCVar('gxWindow')", 0);

            return windowedModeEnabled; 
        }


        private bool IsKnownProblemName(IEnumerable<string> knownProblemNameList, string name)
        {
            return knownProblemNameList.Any(o => name.StartsWith(o, true, CultureInfo.InvariantCulture));
            // return knownProblemNameList.Any(o => name.Equals(o, StringComparison.InvariantCultureIgnoreCase));
        }


        private bool IsMixedModeBot()
        {
            if (BotManager.Current == null)
                { return false; }

            var botName = BotManager.Current.Name ?? string.Empty;

            return botName.StartsWith("mixed", true, CultureInfo.InvariantCulture);
        }


        // NB: The WoWclient allows embeddable sequences containing color info in the addon title.
        // See: http://www.wowwiki.com/TOC_format and http://www.wowwiki.com/UI_escape_sequences
        // We must strip away these sequences to create a comparable name.
        // ElvUI, TukUI, and Zygor are examples of addons that make use of this feature.
        private string StripUiEscapeSequences(string possiblyDecoratedName)
        {
            string name = possiblyDecoratedName ?? string.Empty;

            for (MatchCollection matches = _regexUiEscape.Matches(name);
                matches.Count > 0;
                matches = _regexUiEscape.Matches(name))
            {
                name = _regexUiEscape.Replace(name, "$1");
            }

            return name;
        }
        private readonly Regex _regexUiEscape = new Regex(@"\|c[0-9A-Fa-f]{1,8}([^|]*)(\|r)?", RegexOptions.IgnoreCase);
        #endregion
    }
}