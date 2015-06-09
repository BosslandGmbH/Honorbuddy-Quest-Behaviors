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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Honorbuddy.QuestBehaviorCore;
using Honorbuddy.QuestBehaviorCore.XmlElements;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Plugins;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.Quest_Behaviors.ProfileCompatibilityInfo
{
	[CustomBehaviorFileName(@"Misc\ProfileCompatibilityInfo")]
	public class ProfileCompatibilityInfo : QuestBehaviorBase
	{
		#region Constructor and Argument Processing
		public ProfileCompatibilityInfo(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// NB: Core attributes are parsed by QuestBehaviorBase parent (e.g., QuestId, NonCompeteDistance, etc)

				// Behavior-specific attributes...
                _allowedProblems.AllowBrokenAddOns = GetAttributeAsNullable<bool>("AllowBrokenAddOns", false, null, null) ?? false;
                _allowedProblems.AllowBrokenPlugIns = GetAttributeAsNullable<bool>("AllowBrokenPlugIns", false, null, null) ?? false;
                _allowedProblems.AllowMixedModeBot = GetAttributeAsNullable<bool>("AllowMixedModeBot", false, null, null) ?? false;

			    _cfbContextForHook = this;
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


		// Variables for Attributes provided by caller

        protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
        {
            // For examples, see Development/TEMPLATE_QB.cs
        }

        protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
        {
            // For examples, see Development/TEMPLATE_QB.cs
        }
		#endregion


		#region Private and Convenience variables
        private class AllowedProblems
        {
            public bool AllowBrokenAddOns { get; set; }
            public bool AllowBrokenPlugIns { get; set; }
            public bool AllowMixedModeBot { get; set; }
        }

        private readonly AllowedProblems _allowedProblems = new AllowedProblems();
	    private readonly static BehaviorDatabase _behaviorDatabase = new BehaviorDatabase("ProfileCompatibilityInfo");
        private static bool _isBotStopHooked;
	    private static CustomForcedBehavior _cfbContextForHook;



		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return "$Id$"; } }
		public override string SubversionRevision { get { return "$Rev$"; } }
		#endregion


		#region Overrides of CustomForcedBehavior

		// CreateBehavior supplied by QuestBehaviorBase.
		// Instead, provide CreateMainBehavior definition.


		// Dispose provided by QuestBehaviorBase.


		// IsDone provided by QuestBehaviorBase.
		// Call the QuestBehaviorBase.BehaviorDone() method when you want to indicate your behavior is complete.
		public override void OnStart()
		{
			QBCLog.BehaviorLoggingContext = this;

            _behaviorDatabase.RereadDatabaseIfFileChanged();

			// Install bot stop handler only once...
			if (!_isBotStopHooked)
			{
				BotEvents.OnBotStopped += BotEvents_OnBotStopped;
				_isBotStopHooked = true;
			}

            var isFatalErrorsEncountered = EmitStateInfo();

            if (isFatalErrorsEncountered)
            {
                const string message = "Game client state is incompatible with profile.  Please repair errors.";

                TreeRoot.Stop(message);
                BehaviorDone(message);
                return;
            }

		    BehaviorDone();
		}
		#endregion


        #region Report Generators
        private abstract class ReportGenBase
        {
            public abstract void EmitDetailedInfo(StringBuilder builder, string linePrefix = "");
            public abstract void EmitProblemList(StringBuilder builder, string linePrefix = "");

            public abstract bool IsFatalErrorSeen(AllowedProblems allowedProblems);
        }

        private class ReportGen_Title : ReportGenBase
        {
            public ReportGen_Title(string title)
            {
                _title = title ?? string.Empty;
            }

            private readonly string _title;

            public override void EmitDetailedInfo(StringBuilder builder, string linePrefix = "")
            {
                if (string.IsNullOrEmpty(_title))
                    return;

                builder.AppendFormat("{0}{1}", linePrefix, _title);
                builder.AppendLine();
            }

            public override void EmitProblemList(StringBuilder builder, string linePrefix = "")
            {
                // empty--tites do not belong in problem list...
            }

            public override bool IsFatalErrorSeen(AllowedProblems allowedProblems)
            {
                // Title cannot cause any problems...
                return false;
            }
        }


        private class ReportGen_Bags : ReportGenBase
        {
            public ReportGen_Bags()
            {
                _bagInfos = new List<BagInfo>()
                    {
                        BagInfo.From(Me.Inventory.Backpack),
                        BagInfo.From(WoWBagSlot.Bag1),
                        BagInfo.From(WoWBagSlot.Bag2),
                        BagInfo.From(WoWBagSlot.Bag3),
                        BagInfo.From(WoWBagSlot.Bag4)
                    };

                foreach (var bagInfo in _bagInfos)
                {
                    if (bagInfo == null)
                        continue;

                    if (bagInfo.Type == BagType.NormalBag)
                        _totalSlotsFreeNormal += bagInfo.SlotsFree;
                    else
                        _totalSlotsFreeSpeciality += bagInfo.SlotsFree;

                    _totalSlots += bagInfo.SlotsTotal;
                }         
            }

            private class BagInfo
            {
                public static BagInfo From(WoWBagSlot bagSlot)
                {
                    return From(Me.GetBag(bagSlot));
                }

                public static BagInfo From(WoWBag wowBag)
                {
                    if (wowBag == null)
                        return null;

                    return new BagInfo()
                        {
                            Id = null,
                            Name = wowBag.Name,
                            SlotsTotal = (int)wowBag.Slots,
                            SlotsFree = (int)wowBag.FreeSlots,
                            Type = BagType.NormalBag,
                        };
                }

                public static BagInfo From(WoWContainer wowContainer)
                {
                    if ((wowContainer == null) || !wowContainer.IsValid)
                        return null;

                    return new BagInfo()
                        {
                            Id = (int)wowContainer.Entry,
                            Name = wowContainer.SafeName,
                            SlotsTotal = (int)wowContainer.Slots,
                            SlotsFree = (int)wowContainer.FreeSlots,
                            Type = wowContainer.BagType,
                        };
                }

                public string PrettyInfo()
                {
                    return string.Format("{0} [{1};  {2} free / {3} slots] {4}",
                                            Name,
                                            Type,
                                            SlotsFree,
                                            SlotsTotal,
                                            (Id.HasValue
                                             ? Utility.WowheadLink(Utility.WowheadSubject.Item, Id.Value)
                                             : string.Empty));
                }

                public int? Id { get; private set; }
                public string Name { get; private set; }
                public int SlotsTotal { get; private set; }
                public int SlotsFree { get; private set; }
                public BagType Type { get; private set; }
            }

            private readonly List<BagInfo> _bagInfos;
            private readonly int _totalSlotsFreeNormal;
            private readonly int _totalSlotsFreeSpeciality;
            private readonly int _totalSlots;

            public override void EmitDetailedInfo(StringBuilder builder, string linePrefix = "")
            {
                // Totals...
                builder.AppendFormat("{0}Bags [TOTALS: ({1} normal + {2} speciality) = {3} free / {4} total slots]:",
                                     linePrefix,
                                     _totalSlotsFreeNormal,
                                     _totalSlotsFreeSpeciality,
                                     (_totalSlotsFreeNormal + _totalSlotsFreeSpeciality),
                                     _totalSlots);
                builder.AppendLine();

                // Detail each bag...
                foreach (var bagInfo in _bagInfos)
                {
                    builder.AppendFormat("{0}    {1}", linePrefix, (bagInfo == null) ? "NO BAG" : bagInfo.PrettyInfo());
                    builder.AppendLine();
                }
            }

            public override void EmitProblemList(StringBuilder builder, string linePrefix = "")
            {
                // empty--bags cannot cause problems
            }

            public override bool IsFatalErrorSeen(AllowedProblems allowedProblems)
            {
                // Bag contents can never cause a fatal issue...
                return false;
            }
        }


        private class ReportGen_Equipment : ReportGenBase
        {
            public ReportGen_Equipment()
            {
                var averageItemInfo = Lua.GetReturnValues("return GetAverageItemLevel()");
                AverageItemLevel_Available = (int)Lua.ParseLuaValue<float>(averageItemInfo[0]);
                AverageItemLevel_Equipped = (int)Lua.ParseLuaValue<float>(averageItemInfo[1]);
            }

            public readonly int AverageItemLevel_Available;
            public readonly int AverageItemLevel_Equipped;

            private readonly List<WoWInventorySlot?> _itemDisplayOrder = new List<WoWInventorySlot?>()
            {
                WoWInventorySlot.Head,
                WoWInventorySlot.Neck,
                WoWInventorySlot.Shoulder,
                WoWInventorySlot.Back,
                WoWInventorySlot.Chest,
                WoWInventorySlot.Shirt,
                WoWInventorySlot.Tabard,
                WoWInventorySlot.Wrist,
                WoWInventorySlot.Hands,
                WoWInventorySlot.Finger1,
                WoWInventorySlot.Finger2,
                null,
                WoWInventorySlot.MainHand,
                WoWInventorySlot.OffHand,
                WoWInventorySlot.Ranged,
                null,
                WoWInventorySlot.Waist,
                WoWInventorySlot.Trinket1,
                WoWInventorySlot.Trinket2,
                WoWInventorySlot.Legs,
                WoWInventorySlot.Feet,
            };

            private void BuildPrettyInfo(StringBuilder builder, WoWInventorySlot? wowInventorySlot, string linePrefix)
            {
                if (!wowInventorySlot.HasValue)
                {
                    builder.AppendLine();
                    return;
                }

                var wowItem = Me.Inventory.Equipped.GetEquippedItem(wowInventorySlot.Value);

                // If nothing in item slot...
                if (!Query.IsViable(wowItem))
                {
                    builder.AppendFormat("{0}{1,12}:", linePrefix, wowInventorySlot);
                    builder.AppendLine();
                    return;
                }

                builder.AppendFormat("{0}{1,12}: {2} {3}{4}",
                    linePrefix,
                    wowInventorySlot,
                    wowItem.Name,
                    Utility.WowheadLink(Utility.WowheadSubject.Item, (int)wowItem.Entry),
                    (Query.IsQuestItem(wowItem) ? " ***QUEST ITEM***" : string.Empty));
                builder.AppendLine();
            }


            public override void EmitDetailedInfo(StringBuilder builder, string linePrefix = "")
            {
                builder.AppendFormat("{0}Equipped [Average Item Level: {1} (equipped) / {2} (available)]:",
                    linePrefix, AverageItemLevel_Equipped, AverageItemLevel_Available);
                builder.AppendLine();

                foreach (var inventorySlot in _itemDisplayOrder)
                    BuildPrettyInfo(builder, inventorySlot, linePrefix);
                builder.AppendLine();
            }

            public override void EmitProblemList(StringBuilder builder, string linePrefix = "")
            {
                // empty--inventory cannot cause problems
            }

            public override bool IsFatalErrorSeen(AllowedProblems allowedProblems)
            {
                // Equipment can never cause a fatal issue...
                return false;
            }
        }


        private class ReportGen_ExecutionEnvironment : ReportGenBase
        {
            public ReportGen_ExecutionEnvironment()
            {
                var botBase = BotManager.Current;
                CurrentBotName =
                    (botBase == null)
                    ? "noBot"
                    : string.IsNullOrEmpty(botBase.Name)
                            ? "unnamedBot"
                            : botBase.Name;               

                var currentCombatRoutine = RoutineManager.Current;
                CombatRoutineClass =
                    (currentCombatRoutine == null)
                        ? (WoWClass?)null
                        : currentCombatRoutine.Class;

                CombatRoutineName =
                    (currentCombatRoutine == null)
                    ? "noCombatRoutine"
                    :  string.IsNullOrEmpty(currentCombatRoutine.Name)
                        ? currentCombatRoutine.ToString()
                        : currentCombatRoutine.Name;

                Fps = (int)StyxWoW.WoWClient.Fps;
                GameClientVersion = StyxWoW.GameVersion;
                HonorbuddyVersion = Assembly.GetEntryAssembly().GetName().Version;
                IsAutoLootEnabled = Lua.GetReturnVal<bool>("return GetCVar('AutoLootDefault')", 0);
                IsGameClientWindowedModeEnabled = Lua.GetReturnVal<bool>("return GetCVar('gxWindow')", 0);
                Latency = (int)StyxWoW.WoWClient.Latency;

                var currentProfile = ProfileManager.CurrentProfile;
                ProfileName =
                    (currentProfile == null)
                    ? "noProfile"
                    : string.IsNullOrEmpty(currentProfile.Name)
                      ? "unnamedProfile"
                      : currentProfile.Name;
                ProfilePath =
                    (currentProfile == null)
                    ? "noProfilePath"
                    : string.IsNullOrEmpty(currentProfile.Path)
                            ? "unnamedProfilePath"
                            : currentProfile.Path;
            }

            public readonly WoWClass? CombatRoutineClass;
            public readonly string CombatRoutineName;
            public readonly string CurrentBotName;
            public readonly int Fps;
            public readonly Version GameClientVersion;
            public readonly Version HonorbuddyVersion;
            public readonly bool IsAutoLootEnabled;
            public readonly bool IsGameClientWindowedModeEnabled;
            public readonly int Latency;
            public readonly string ProfileName;
            public readonly string ProfilePath;


		    private bool IsMixedModeBot(string botBaseName)
		    {
			    return !string.IsNullOrEmpty(botBaseName) && botBaseName.StartsWith("mixed", true, CultureInfo.InvariantCulture);
		    }


            public override void EmitDetailedInfo(StringBuilder builder, string linePrefix = "")
            {
                builder.AppendFormat("{0}Honorbuddy: v{1}", linePrefix, HonorbuddyVersion);
                builder.AppendLine();
                builder.AppendFormat("{0}Game client: v{1} ({2} FPS, {3}ms latency)",
                    linePrefix,
                    GameClientVersion,
                    Fps,
                    Latency);
                builder.AppendLine();
                builder.AppendFormat("{0}    Auto Loot? {1}", linePrefix, (IsAutoLootEnabled ? "enabled" : "***DISABLED***"));
                builder.AppendLine();
                builder.AppendFormat("{0}    Windowed mode? {1}", linePrefix, (IsGameClientWindowedModeEnabled ? "enabled" : "***DISABLED***"));
                builder.AppendLine();
                builder.AppendFormat("{0}BotBase: {1}", linePrefix, CurrentBotName);
                builder.AppendLine();
                builder.AppendFormat("{0}Profile: {1} ({2})", linePrefix, ProfileName, ProfilePath);
                builder.AppendLine();
                builder.AppendFormat("{0}Combat Routine: {1}", linePrefix, CombatRoutineName);
                builder.AppendLine();
            }

            public override void EmitProblemList(StringBuilder builder, string linePrefix = "")
            {
                var minimumFpsWarningThreshold = _behaviorDatabase.Threshold_FpsMinimum.Value;
                var maximumLatencyWarningThreshold = _behaviorDatabase.Threshold_LatencyMaximum.Value;

                if (Fps < minimumFpsWarningThreshold)
                {
                    builder.AppendFormat("{0}* FPS ({1}) is absurdly low (expected {2} FPS, minimum).",
                        linePrefix,
                        Fps,
                        minimumFpsWarningThreshold);
                    builder.AppendLine();
                }

                // Absurdly high latency?
                if (Latency > maximumLatencyWarningThreshold)
                {
                    builder.AppendFormat("{0}* Latency ({1}ms) is absurdly high (expected {2}ms latency, maximum).",
                        linePrefix,
                        Latency,
                        maximumLatencyWarningThreshold);
                    builder.AppendLine();
                }


                // Does Combat Routine match toon's class?
                // NB: This can happen if the user logs out and back in to the game client
                // without restarting HB.
                if (Me.Class != RoutineManager.Current.Class)
                {
                    builder.AppendFormat("{0}* Combat Routine class({1}) does not match Toon class({2})"
                        + "--please restart Honorbuddy.",
                        linePrefix,
                        RoutineManager.Current.Class,
                        Me.Class);
                    builder.AppendLine();
                }

                // Is AutoLoot turned off?
                if (!IsAutoLootEnabled)
                {
                    builder.AppendFormat("{0}* AutoLoot is not enabled in game client"
                        + "--please turn it on.",
                        linePrefix);
                    builder.AppendLine();
                }


                // Is Windowed mode?
                if (!IsGameClientWindowedModeEnabled)
                {
                    builder.AppendFormat("{0}* The game client is running in 'full screen' mode"
                        + "--please set it to 'windowed' mode.",
                        linePrefix);
                    builder.AppendLine();
                }


                // Mixed mode?
                if (IsMixedModeBot(CurrentBotName))
                {
                    builder.AppendFormat("{0}* MixedMode bot is not compatible with this profile"
                        + "--please select >Questing< bot.",
                        linePrefix);
                    builder.AppendLine();
                }
            }

            public override bool IsFatalErrorSeen(AllowedProblems allowedProblems)
            {
                return
                    (!allowedProblems.AllowMixedModeBot && IsMixedModeBot(CurrentBotName))
                    || (RoutineManager.Current == null)
                    || (Me.Class != CombatRoutineClass)
                    || !IsGameClientWindowedModeEnabled;
            }
        }


        private class ReportGen_GameClientAddOns : ReportGenBase
        {
            public ReportGen_GameClientAddOns()
            {
                _addOnInfos = new List<AddOnInfo>();
                _numAddOns = Lua.GetReturnVal<int>("return GetNumAddOns()", 0);

                // Build list of game client addon names & their enabled/disabled state...
                for (var i = 1; i <= _numAddOns; ++i)
                {
                    var addOnInfo = Lua.GetReturnValues(string.Format("return GetAddOnInfo({0})", i));

                    var decoratedName = StripUiEscapeSequences(Lua.ParseLuaValue<string>(addOnInfo[0]));
                    var isEnabled = Lua.ParseLuaValue<bool>(addOnInfo[3]);

                    // Make certain addon name is unique...
                    decoratedName = string.IsNullOrEmpty(decoratedName) ? "UnnamedAddon" : decoratedName;
                    var addOnName = decoratedName;
                    for (var sameNameIndex = 2; _addOnInfos.Any(a => a.Name == addOnName); ++sameNameIndex)
                        addOnName = string.Format("{0}_{1}", decoratedName, sameNameIndex);

                    // NB: We check 'enabled' and 'loadable' for addons...
                    // If an addon cannot be loaded because it is out-of-date, or missing 
                    // a dependency, then it is indirectly disabled.
                    var isProblem = IsKnownProblemName(addOnName);

                    _addOnInfos.Add(new AddOnInfo(addOnName, isEnabled, isProblem));
                }
            }

            private class AddOnInfo
            {
                public AddOnInfo(string name, bool isEnabled, bool isProblem)
                {
                    Contract.Requires(!string.IsNullOrEmpty(name), (context) => "name may not be null or empty");

                    Name = name;
                    IsEnabled = isEnabled;
                    IsProblem = isProblem;
                }

                public readonly string Name;
                public readonly bool IsEnabled;
                public readonly bool IsProblem;
            }

            private readonly List<AddOnInfo> _addOnInfos;
            private readonly int _numAddOns;
            private readonly Regex _regexUiEscape = new Regex(@"\|c[0-9A-Fa-f]{1,8}([^|]*)(\s*\|r)?", RegexOptions.IgnoreCase);


            private static bool IsKnownProblemName(string name)
            {
                return _behaviorDatabase.ProblemAddOns.AddOns.Any(o => name.StartsWith(o.Name, true, CultureInfo.InvariantCulture));
            }


            private void BuildPrettyInfo(StringBuilder builder, string linePrefix, AddOnInfo addOnInfo)
            {
                var isEnabledProblemAddOn = addOnInfo.IsProblem && addOnInfo.IsEnabled;
                var enabledMessage = addOnInfo.IsEnabled ? "enabled" : string.Empty;

                if (isEnabledProblemAddOn)
                    enabledMessage = "ENABLED ***PROBLEMATICAL*** ADDON";

                builder.AppendFormat("{0}    {1,3}{2}: {3}",
                        linePrefix,
                        (isEnabledProblemAddOn ? "*** " : "    "),    // attention prefix
                        addOnInfo.Name,
                        enabledMessage);
                builder.AppendLine();
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


            public override void EmitDetailedInfo(StringBuilder builder, string linePrefix = "")
            {
                builder.AppendFormat("{0}Game client AddOns [Showing {1} enabled / {2} total]:",
                    linePrefix, _addOnInfos.Count(a => a.IsEnabled), _numAddOns);
                builder.AppendLine();

                if (!_addOnInfos.Any(a => a.IsEnabled))
                {
                    builder.AppendFormat("{0}    NONE", linePrefix);
                    builder.AppendLine();
                    return;
                }

                foreach (var addOnInfo in _addOnInfos.Where(a => a.IsEnabled).OrderBy(a => a.Name))
                    BuildPrettyInfo(builder, linePrefix, addOnInfo);
            }

            public override void EmitProblemList(StringBuilder builder, string linePrefix = "")
            {
                if (!_addOnInfos.Any(a => a.IsProblem && a.IsEnabled))
                    return;

                builder.AppendFormat("{0}* Problematic game client addons were encountered"
                                     + "--please disable these addons:",
                                     linePrefix);

                foreach (var addOnInfo in _addOnInfos.Where(a => a.IsProblem && a.IsEnabled).OrderBy(a => a.Name))
                {
                    builder.AppendLine();
                    builder.Append(linePrefix);
                    builder.Append("    ");
                    builder.Append(addOnInfo.Name);
                }

                builder.AppendLine();
            }

            public override bool IsFatalErrorSeen(AllowedProblems allowedProblems)
            {
                return !allowedProblems.AllowBrokenAddOns && _addOnInfos.Any(a => a.IsProblem && a.IsEnabled);
            }
        }
    
        
        private class ReportGen_Mounts : ReportGenBase
        {
            public ReportGen_Mounts()
            {
                _flyingMount = Mount.Mounts.FirstOrDefault(m => m.Name == CharacterSettings.Instance.FlyingMountName);
                _groundMount = Mount.Mounts.FirstOrDefault(m => m.Name == CharacterSettings.Instance.MountName);
            }

            private readonly Mount.MountWrapper _flyingMount;
            private readonly Mount.MountWrapper _groundMount;

            public override void EmitDetailedInfo(StringBuilder builder, string linePrefix = "")
            {
                var problemText = string.Empty;

                builder.AppendFormat("{0}Ground Mount: {1} {2} {3}",
                    linePrefix,
                    ((_groundMount == null)
                        ? "UNSPECIFIED" 
                        : _groundMount.Name),
                    ((_groundMount == null)
                        ? string.Empty
                        : Utility.WowheadLink(Utility.WowheadSubject.Spell, _groundMount.CreatureSpellId)),
                    problemText);
                builder.AppendLine();

                builder.AppendFormat("{0}Flying Mount: {1} {2} {3}",
                    linePrefix,
                    ((_flyingMount == null)
                        ? "UNSPECIFIED"
                        : _flyingMount.Name),
                    ((_flyingMount == null) 
                        ? string.Empty 
                        : Utility.WowheadLink(Utility.WowheadSubject.Spell, _flyingMount.CreatureSpellId)),
                    problemText);
                builder.AppendLine();
            }

            public override void EmitProblemList(StringBuilder builder, string linePrefix = "")
            {
                // empty--mounts can no longer cause problems
            }

            public override bool IsFatalErrorSeen(AllowedProblems allowedProblems)
            {
                // At this time, there is no way to screw up mounts...
                return false;
            }
        }


        private class ReportGen_Plugins : ReportGenBase
        {
            public ReportGen_Plugins()
            {
                _plugIns = new List<PlugInInfo>();

                foreach (var plugIn in PluginManager.Plugins)
                    _plugIns.Add(new PlugInInfo(plugIn));
            }

            private class PlugInInfo
            {
                public PlugInInfo(PluginContainer plugIn)
                {
                    Contract.Requires(plugIn != null, (context) => "plugIn != null");

                    Name = string.IsNullOrEmpty(plugIn.Name) ? "UnnamedPlugin" : plugIn.Name;
                    IsBosslandPlugIn =
                        _behaviorDatabase.BosslandGmbHPlugins.PlugIns
                        .Any(o => Name.StartsWith(o.Name, true, CultureInfo.InvariantCulture));
                    IsEnabled = plugIn.Enabled;
                    IsProblemPlugIn =
                        _behaviorDatabase.ProblemPlugIns.PlugIns
                        .Any(o => Name.StartsWith(o.Name, true, CultureInfo.InvariantCulture));
	                PluginAuthor = plugIn.Author ?? "unknown";
                    PlugInVersion = plugIn.Version;
                }

                public readonly string Name;
                public readonly bool IsBosslandPlugIn;
                public readonly bool IsEnabled;
                public readonly bool IsProblemPlugIn;
                public readonly Version PlugInVersion;
	            public readonly string PluginAuthor;
            };

            private readonly List<PlugInInfo> _plugIns;


            private string GetPlugInState(PlugInInfo plugIn)
            {
                if (plugIn.IsEnabled)
                {
                    if (plugIn.IsProblemPlugIn)
                        return "ENABLED (***PROBLEMATICAL PLUGIN***)";

                    if (!plugIn.IsBosslandPlugIn)
                        return "enabled (NON-BosslandGmbH-SHIPPED PLUGIN)";

                    return "enabled";
                }

                return string.Empty;
            }

 
            public override void EmitDetailedInfo(StringBuilder builder, string linePrefix = "")
            {
                builder.AppendFormat("{0}PlugIns:", linePrefix);
                builder.AppendLine();
                if (!_plugIns.Any())
                {
                    builder.AppendFormat("{0}    NONE", linePrefix);
                    builder.AppendLine();
                    return;
                }

                foreach (var plugIn in _plugIns.OrderBy(plugin => plugin.Name))
                {
                    builder.AppendFormat("{0}    {1}{2} v{3} ({4}): {5}",
                                         linePrefix,
                                         ((plugIn.IsEnabled && plugIn.IsProblemPlugIn) ? "*** " : "    "),
                                         plugIn.Name,
                                         plugIn.PlugInVersion,
										 plugIn.PluginAuthor,
                                         GetPlugInState(plugIn));
                    builder.AppendLine();
                }
            }

            public override void EmitProblemList(StringBuilder builder, string linePrefix = "")
            {
                if (_plugIns.Any(p => p.IsEnabled && p.IsProblemPlugIn))
                {
                    var problemPlugInNames =
                        string.Join(", ", _plugIns.Where(p => p.IsProblemPlugIn && p.IsEnabled)
                                                  .Select(p => string.Format("{0}{1}    {2}",
                                                                             Environment.NewLine,
                                                                             linePrefix,
                                                                             p.Name)));

                    builder.AppendFormat("{0}* Problematic plugins were encountered"
                        + "--please disable these plugins:{1}",
                        linePrefix,
                        problemPlugInNames);

                    builder.AppendLine();
                }
            }

            public override bool IsFatalErrorSeen(AllowedProblems allowedProblems)
            {
                return !allowedProblems.AllowBrokenPlugIns && _plugIns.Any(p => p.IsEnabled && p.IsProblemPlugIn);
            }
        }


        private class ReportGen_QuestStates : ReportGenBase
        {
            public ReportGen_QuestStates()
            {
                // Present the quest in the same oder shown in user's quest log...
                QuestsInLog =
                    (from quest in Me.QuestLog.GetAllQuests()
                     let index = Lua.GetReturnVal<int>(string.Format("return GetQuestLogIndexByID({0})", quest.Id), 0)
                     orderby index
                     select new QuestWithIndex(quest, index))
                    .ToList();

                QuestCount = QuestsInLog.Count;
            }


            public readonly List<QuestWithIndex> QuestsInLog;
            public readonly int QuestCount;

            public class QuestWithIndex
            {
                public QuestWithIndex(PlayerQuest playerQuest, int index)
                {
                    Quest = playerQuest;
                    Index = index;
                }

                public readonly PlayerQuest Quest;
                public readonly int Index;
            }


            public override void EmitDetailedInfo(StringBuilder builder, string linePrefix = "")
            {
                builder.AppendFormat("{0}Quests:", linePrefix);
                builder.AppendLine();

                if (QuestCount <= 0)
                {
                    builder.AppendFormat("{0}    None", linePrefix);
                    builder.AppendLine();
                    return;
                }

                foreach (var quest in QuestsInLog)
                {
                    var questState =
                        quest.Quest.IsCompleted ? "COMPLETED"
                        : quest.Quest.IsFailed ? "FAILED"
                        : "incomplete";

                    builder.AppendFormat("{0}    \"{1}\"{2} {3}{4}",
                        linePrefix,
                        quest.Quest.Name,
                        Utility.WowheadLink(Utility.WowheadSubject.Quest, (int)quest.Quest.Id),
                        questState,
                        (quest.Quest.IsDaily ? ", Daily" : string.Empty));
                    builder.AppendLine();

                    foreach (var objective in quest.Quest.GetObjectives())
                    {
                        var objectiveInfo = Lua.GetReturnValues(string.Format("return GetQuestLogLeaderBoard({0},{1})",
                                                                objective.Index + 1,    // HB is zero-based, but LUA is one-based
                                                                quest.Index));
                        var objectiveText = Lua.ParseLuaValue<string>(objectiveInfo[0]);
                        var objectiveIsFinished = Lua.ParseLuaValue<bool>(objectiveInfo[2]);

                        builder.AppendFormat("{0}        {1} (type: {2}) {3}",
                            linePrefix,
                            objectiveText,
                            objective.Type,
                            (objectiveIsFinished ? "OBJECTIVE_COMPLETE" : ""));
                        builder.AppendLine();
                    }
                }
            }

            public override void EmitProblemList(StringBuilder builder, string linePrefix = "")
            {
                // empty
            }

            public override bool IsFatalErrorSeen(AllowedProblems allowedProblems)
            {
                return false;
            }
        }


        private class ReportGen_QuestItems : ReportGenBase
        {
            public ReportGen_QuestItems()
            {
                _questItems =
                    (from item in Me.BagItems
                     where Query.IsQuestItem(item)
                     select item)
                    .Distinct(new WoWItemIdComparer())
                    .ToList();
            }

            private readonly List<WoWItem> _questItems; 


            class WoWItemIdComparer : IEqualityComparer<WoWItem>
            {
                public bool Equals(WoWItem x, WoWItem y)
                {
                    // Comparison to same object always succeed...
                    if (Object.ReferenceEquals(x, y))
                        return true;

                    // Comparison to null always fails...
                    if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
                        return false;

                    // Compare the item's id...
                    return x.Entry == y.Entry;
                }

                // If Equals() returns true for a pair of objects  
                // then GetHashCode() must return the same value for these objects. 
                public int GetHashCode(WoWItem wowItem)
                {
                    // No hash code for null object...
                    if (Object.ReferenceEquals(wowItem, null))
                    { return 0; }

                    // Use hash code for the Entry field if WoWItem is not null...
                    return wowItem.Entry.GetHashCode();
                }
            }


            public override void EmitDetailedInfo(StringBuilder builder, string linePrefix = "")
            {
                builder.AppendFormat("{0}Quest Items in bags:", linePrefix);
                builder.AppendLine();
                if (!_questItems.Any())
                {
                    builder.AppendFormat("{0}    NONE", linePrefix);
                    builder.AppendLine();
                    return;
                }

                foreach (
                    var questItem in
                        _questItems.OrderBy(item => item.ItemInfo.InternalInfo.QuestId).ThenBy(item => item.SafeName)
                    )
                {
                    var stackCount = Me.CarriedItems.Where(i => (i.Entry == questItem.Entry)).Sum(i => i.StackCount);

                    builder.AppendFormat("{0}    {1}{2} {3}",
                                         linePrefix,
                                         questItem.SafeName,
                                         ((stackCount <= 1) ? "" : string.Format(" x{0}", stackCount)),
                                         Utility.WowheadLink(Utility.WowheadSubject.Item, (int)questItem.Entry));
                    builder.AppendLine();

                    if (questItem.ItemInfo.BeginQuestId == 0)
                        continue;

                    var beginsQuest = Quest.FromId((uint)questItem.ItemInfo.BeginQuestId);

                    builder.AppendFormat("{0}        => starts quest \"{1}\" {2}",
                                         linePrefix,
                                         ((beginsQuest == null) ? "UnknownQuest" : beginsQuest.Name),
                                         Utility.WowheadLink(Utility.WowheadSubject.Quest, questItem.ItemInfo.BeginQuestId));
                    builder.AppendLine();
                }
            }

            public override void EmitProblemList(StringBuilder builder, string linePrefix = "")
            {
                // empty
            }

            public override bool IsFatalErrorSeen(AllowedProblems allowedProblems)
            {
                // Quest items cannot cause fatal errors...
                return false;
            }
        }


        private class ReportGen_Talents : ReportGenBase
        {
            public ReportGen_Talents()
            {
                // empty
            }

            public override void EmitDetailedInfo(StringBuilder builder, string linePrefix = "")
            {
                foreach (var talent in Me.GetLearnedTalents().OrderBy(t => t.Tier))
                {
                    builder.AppendFormat("{0}    {1}.{2}: {3}",
                                         linePrefix,
                                         talent.Tier +1,
                                         talent.Index +1,
                                         talent.Name);
                    builder.AppendLine();
                }
            }

            public override void EmitProblemList(StringBuilder builder, string linePrefix = "")
            {
                // empty
                // TODO: WARN OF UNSPENT TALENTS
            }

            public override bool IsFatalErrorSeen(AllowedProblems allowedProblems)
            {
                // Talents can never cause a fatal issue...
                return false;
            }
        }


        private class ReportGen_ToonState : ReportGenBase
        {
            public ReportGen_ToonState()
            {
                // empty, for now...
            }

            public override void EmitDetailedInfo(StringBuilder builder, string linePrefix = "")
            {
                builder.AppendFormat("{0}Toon: {1:F1} {2} {3} ({4}, {5})",
                    linePrefix,
                    Me.LevelFraction,
                    Me.Race,
                    Me.Class,
                    Me.FactionGroup,
                    Me.Gender);
                builder.AppendLine();

                builder.AppendFormat("{0}Specialization: {1}", linePrefix, Me.Specialization);
                builder.AppendLine();

                var talents = new ReportGen_Talents();
                talents.EmitDetailedInfo(builder, linePrefix);
                builder.AppendLine();

                // Toon Location...
                var continentName = Lua.GetReturnVal<string>("return GetInstanceInfo()", 0);
                builder.AppendFormat("{0}Location: {1} (mapid={2})",
                                     linePrefix,
                                     (string.IsNullOrEmpty(continentName) ? "noContinent" : continentName),
                                     Me.MapId);
                builder.AppendLine();
                var zoneName = string.IsNullOrEmpty(Me.ZoneText)
                    ? "noZone"
                    : (Me.CurrentMap.IsGarrison ? "Garrison" : Me.ZoneText);

                builder.AppendFormat("{0}     => {1} {2}",
                                     linePrefix,
                                     zoneName,
                                     Utility.WowheadLink(Utility.WowheadSubject.Zone, (int)Me.ZoneId));
                builder.AppendLine();

                var subZoneName = string.IsNullOrEmpty(Me.SubZoneText)
                    ? "noSubZone"
                    : (Me.CurrentMap.IsGarrison ? "Garrison" : Me.SubZoneText);

                builder.AppendFormat("{0}     => {1} (subzone={2})", 
                    linePrefix,
                    subZoneName,
                    (int)Me.SubZoneId);
                builder.AppendLine();
                builder.AppendFormat("{0}     => {1}", linePrefix, Me.Location);
                builder.AppendLine();
            }

            public override void EmitProblemList(StringBuilder builder, string linePrefix = "")
            {
                // empty
            }

            public override bool IsFatalErrorSeen(AllowedProblems allowedProblems)
            {
                // No toon state info can be fatal...
                return false;
            }
        }


        private class ReportGen_TradeSkills : ReportGenBase
        {
            public ReportGen_TradeSkills()
            {
                // empty, for now...
            }

            private readonly List<SkillLine> _skillLinesOfInterest = new List<SkillLine>()
            {
                SkillLine.Alchemy,
                SkillLine.Archaeology,
                SkillLine.Blacksmithing,
                SkillLine.Cooking,
                SkillLine.Enchanting,
                SkillLine.Engineering,
                SkillLine.FirstAid,
                SkillLine.Fishing,
                SkillLine.Herbalism,
                SkillLine.Jewelcrafting,
                SkillLine.Leatherworking,
                SkillLine.Mining,
                SkillLine.Riding,
                SkillLine.Skinning,
                SkillLine.Tailoring,
            }; 

            private void BuildSkillInfo(StringBuilder builder, string linePrefix, SkillLine skillLine)
            {
                WoWSkill wowSkill = Me.GetSkill(skillLine);

                if ((wowSkill != null) && wowSkill.IsValid && (wowSkill.MaxValue > 0))
                {
                    builder.AppendFormat("{0}{1,15}: {2}/{3}",
                        linePrefix,
                        wowSkill.Name,
                        wowSkill.CurrentValue,
                        wowSkill.MaxValue);

                    if (wowSkill.Bonus > 0)
                        builder.AppendFormat(" [Bonus: +{0}]", wowSkill.Bonus);

                    builder.AppendLine();
                }
            }


            public override void EmitDetailedInfo(StringBuilder builder, string linePrefix = "")
            {
                builder.AppendFormat("{0}Tradeskills:", linePrefix);
                builder.AppendLine();

                if (!_skillLinesOfInterest.Any(s => Me.GetSkill(s) != null && Me.GetSkill(s).MaxValue > 0))
                {
                    builder.AppendFormat("{0}    NONE", linePrefix);
                    builder.AppendLine();
                    return;
                } 
                
                foreach (var skillLine in _skillLinesOfInterest)
                    BuildSkillInfo(builder, linePrefix, skillLine);
            }

            public override void EmitProblemList(StringBuilder builder, string linePrefix = "")
            {
                // empty
            }

            public override bool IsFatalErrorSeen(AllowedProblems allowedProblems)
            {
                // TradeSkills can not cause fatal errors...
                return false;
            }
        }
        #endregion



        #region Helpers
        private void BotEvents_OnBotStopped(EventArgs args)
		{
			EmitStateInfo();

			// Unhook the bot stop handler...
			BotEvents.OnBotStopped -= BotEvents_OnBotStopped;
			_isBotStopHooked = false;
		}


        // Return 'true', if fatal error encounterd; otherwise, false.
		private bool EmitStateInfo()
		{
            // Need to establish logging context, since this may be called from OnBotStopped...
		    QBCLog.BehaviorLoggingContext = _cfbContextForHook;

			using (StyxWoW.Memory.AcquireFrame(true))
			{
				var linePrefix = new String(' ', 4);

				// Build compatibility info...
			    var reportGenerators = new List<ReportGenBase>()
			    {
                    new ReportGen_ExecutionEnvironment(),
                    new ReportGen_ToonState(),
                    new ReportGen_TradeSkills(),
                    new ReportGen_QuestStates(),
                    new ReportGen_QuestItems(),
                    new ReportGen_Bags(),
                    new ReportGen_Equipment(),
                    new ReportGen_Mounts(),
                    new ReportGen_GameClientAddOns(),
                    new ReportGen_Plugins(),
			    };


                var builderDetails = new StringBuilder();
                builderDetails.Append("---------- BEGIN: Profile Compatibility Info ----------");

                foreach (var report in reportGenerators)
                {
                    report.EmitDetailedInfo(builderDetails, linePrefix);
                    builderDetails.AppendLine();
                }

                QBCLog.DeveloperInfo(builderDetails.ToString());

			    var builderProblemErrorSummary = new StringBuilder();
                var builderProblemWarningSummary = new StringBuilder();

                foreach (var report in reportGenerators)
                {
                    var builderProblemSummary =
                        report.IsFatalErrorSeen(_allowedProblems)
                        ? builderProblemErrorSummary
                        : builderProblemWarningSummary;

                    report.EmitProblemList(builderProblemSummary, "    ");
                }

                if (builderProblemWarningSummary.Length > 0)
                {
                    var tmpBuilder = new StringBuilder();

                    tmpBuilder.Append("PROFILE COMPATIBILITY WARNINGS:");
                    tmpBuilder.AppendLine();
                    tmpBuilder.Append(builderProblemWarningSummary);

                    QBCLog.Warning(tmpBuilder.ToString());
                }

                if (builderProblemErrorSummary.Length > 0)
                {
                    var tmpBuilder = new StringBuilder();

                    tmpBuilder.Append("PROFILE COMPATIBILITY ERRORS:");
                    tmpBuilder.AppendLine();
                    tmpBuilder.Append(builderProblemErrorSummary);

                    QBCLog.Error(tmpBuilder.ToString());
                }

                // Emit end demark...
				QBCLog.DeveloperInfo(this, "---------- END: Profile Compatibility Info ----------");

				// Return value indicating whether or not fatal errors encountered...
			    return reportGenerators.Any(r => r.IsFatalErrorSeen(_allowedProblems));
            }
		}

        #endregion



        #region Helper classes: Database
        public class BehaviorDatabase
        {
            public BehaviorDatabase(string databaseName)
            {
                Contract.Requires(!string.IsNullOrEmpty(databaseName),
                                  (context) => "databaseName may not be null or empty");

                _databaseName = databaseName;
                _databaseFullPathName = Utility.GetDataFileFullPath(_databaseName + ".xml");
                _databaseLastModifiedTime = new DateTime(0);
                RereadDatabaseIfFileChanged();
            }

            public GameClientAddOnList ProblemAddOns;
            public PlugInList ProblemPlugIns;
            public PlugInList BosslandGmbHPlugins;
            public XmlValueInt Threshold_FpsMinimum;
            public XmlValueInt Threshold_LatencyMaximum;

            private readonly string _databaseFullPathName;
            private DateTime _databaseLastModifiedTime;
            private readonly string _databaseName;


            public void RereadDatabaseIfFileChanged()
            {
                // NB: We use the absolute path here.  If we don't, then QBs get confused if there are additional
                // QBs supplied in the Honorbuddy/Default Profiles/. directory.
                var lastReadTime = File.GetLastWriteTime(_databaseFullPathName);

                if (lastReadTime <= _databaseLastModifiedTime)
                    return;

                QBCLog.DeveloperInfo("Database \"{0}\" has changed--re-reading.", _databaseName);

                var xDoc = XDocument.Load(_databaseFullPathName, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo);
                var xProfileCompatabilityInfo =
                    xDoc.Elements(_databaseName)
                    .DefaultIfEmpty(new XElement(_databaseName))
                    .ToList();

                // Bossland GmbH PlugIns...
                {
                    BosslandGmbHPlugins =
                        new PlugInList(GetOrCreateElement(xProfileCompatabilityInfo, "BosslandGmbHPlugIns"));
                }

                // Problem AddOns...
                {
                    ProblemAddOns =
                        new GameClientAddOnList(GetOrCreateElement(xProfileCompatabilityInfo, "ProblemAddOns"));
                }

                // Problem PlugIns...
                {
                    ProblemPlugIns =
                        new PlugInList(GetOrCreateElement(xProfileCompatabilityInfo, "ProblemPlugIns"));
                }

                // Thresholds...
                {
                    var xThresholds =
                        xProfileCompatabilityInfo.Elements("Thresholds")
                        .DefaultIfEmpty(new XElement("Thresholds"))
                        .ToList();

                    Threshold_FpsMinimum = new XmlValueInt(GetOrCreateElement(xThresholds, "FpsMinimum"));
                    Threshold_LatencyMaximum = new XmlValueInt(GetOrCreateElement(xThresholds, "LatencyMaximum"));
                }

                QBCLog.DeveloperInfo("Database \"{0}\" re-read complete.", _databaseName);
                _databaseLastModifiedTime = lastReadTime;
            }

            private XElement GetOrCreateElement(IEnumerable<XElement> parent, string elementName)
            {
                Contract.Requires(parent != null, (context) => "parent is not null");
                Contract.Requires(!string.IsNullOrEmpty(elementName), (context) => "elementName is not null or empty");

                return parent.Elements(elementName).DefaultIfEmpty(new XElement(elementName)).FirstOrDefault();
            }


            public XElement ToXml()
            {
                var root = new XElement(_databaseName);

                root.Add(BosslandGmbHPlugins.ToXml("BosslandGmbHPlugIns"));
                root.Add(ProblemAddOns.ToXml("ProblemAddOns"));
                root.Add(ProblemPlugIns.ToXml("ProblemPlugins"));
                root.Add(new XElement("Thresholds",
                        Threshold_FpsMinimum.ToXml(),
                        Threshold_LatencyMaximum.ToXml()
                        ));

                return root;
            }

        }
        #endregion


        #region Helper classes: Database-AddOns
        public class XmlAddOn : QuestBehaviorXmlBase
        {
            public XmlAddOn(XElement xElement)
                : base(xElement)
            {
                Contract.Requires(xElement != null, (context) => "xElement != null");

                _defaultElementName = xElement.Name.ToString();

                try
                {
                    Name = GetAttributeAs<string>("Name", true, ConstrainAs.StringNonEmpty, null) ?? string.Empty;
                    Uri = GetAttributeAs<string>("Uri", false, ConstrainAs.StringNonEmpty, null) ?? string.Empty;

                    HandleAttributeProblem();
                }

                catch (Exception except)
                {
                    if (Query.IsExceptionReportingNeeded(except))
                        QBCLog.Exception(except, "PROFILE PROBLEM with \"{0}\"", xElement.ToString());

                    IsAttributeProblem = true;
                }
            }

            public string Name { get; private set; }
            public string Uri { get; private set; }

            private readonly string _defaultElementName;


            public override XElement ToXml(string elementName = null)
            {
                elementName = string.IsNullOrEmpty(elementName) ? _defaultElementName : elementName;

                var root = new XElement(elementName,
                                        new XAttribute("Name", Name),
                                        new XAttribute("Uri", Uri));

                return root;
            }
        }


        public class GameClientAddOnList : QuestBehaviorXmlBase
        {
            public GameClientAddOnList(XElement xElement)
                : base(xElement)
            {
                Contract.Requires(xElement != null, (context) => "xElement != null");

                _defaultElementName = xElement.Name.ToString();

                try
                {
                    // Acquire the SubGoal info...
                    AddOns = new List<XmlAddOn>();

                    foreach (var childElement in xElement.Elements("AddOn"))
                    {
                        var addOn = new XmlAddOn(childElement);

                        if (!addOn.IsAttributeProblem)
                            AddOns.Add(addOn);

                        IsAttributeProblem |= addOn.IsAttributeProblem;
                    }

                    HandleAttributeProblem();
                }

                catch (Exception except)
                {
                    if (Query.IsExceptionReportingNeeded(except))
                        QBCLog.Exception(except, "PROFILE PROBLEM with \"{0}\"", xElement.ToString());
                    IsAttributeProblem = true;
                }
            }

            public readonly List<XmlAddOn> AddOns;

            private readonly string _defaultElementName;

            public override XElement ToXml(string elementName = null)
            {
                elementName = string.IsNullOrEmpty(elementName) ? _defaultElementName : elementName;

                var root = new XElement(elementName);

                foreach (var addOn in AddOns)
                    root.Add(addOn.ToXml("AddOn"));

                return root;
            }
        }
        #endregion


        #region Helper classes: Database-PlugIn
        public class XmlPlugIn : QuestBehaviorXmlBase
        {
            public XmlPlugIn(XElement xElement)
                : base(xElement)
            {
                Contract.Requires(xElement != null, (context) => "xElement != null");

                _defaultElementName = xElement.Name.ToString();

                try
                {
                    Name = GetAttributeAs<string>("Name", true, ConstrainAs.StringNonEmpty, null) ?? string.Empty;

                    HandleAttributeProblem();
                }

                catch (Exception except)
                {
                    if (Query.IsExceptionReportingNeeded(except))
                        QBCLog.Exception(except, "PROFILE PROBLEM with \"{0}\"", xElement.ToString());

                    IsAttributeProblem = true;
                }
            }

            public readonly string _defaultElementName;
            public string Name { get; private set; }


            public override XElement ToXml(string elementName = null)
            {
                elementName = string.IsNullOrEmpty(elementName) ? _defaultElementName : elementName;

                var root = new XElement(elementName,
                                        new XAttribute("Name", Name));

                return root;
            }
        }


        public class PlugInList : QuestBehaviorXmlBase
        {
            public PlugInList(XElement xElement)
                : base(xElement)
            {
                Contract.Requires(xElement != null, (context) => "xElement != null");

                _defaultElementName = xElement.Name.ToString();

                try
                {
                    // Acquire the SubGoal info...
                    PlugIns = new List<XmlPlugIn>();

                    foreach (var childElement in xElement.Elements("PlugIn"))
                    {
                        var plugIn = new XmlPlugIn(childElement);

                        if (!plugIn.IsAttributeProblem)
                            PlugIns.Add(plugIn);

                        IsAttributeProblem |= plugIn.IsAttributeProblem;
                    }

                    HandleAttributeProblem();
                }

                catch (Exception except)
                {
                    if (Query.IsExceptionReportingNeeded(except))
                        QBCLog.Exception(except, "PROFILE PROBLEM with \"{0}\"", xElement.ToString());
                    IsAttributeProblem = true;
                }
            }

            public readonly List<XmlPlugIn> PlugIns;

            private readonly string _defaultElementName;

            public override XElement ToXml(string elementName = null)
            {
                elementName = string.IsNullOrEmpty(elementName) ? _defaultElementName : elementName;

                var root = new XElement(elementName);

                foreach (var plugIn in PlugIns)
                    root.Add(plugIn.ToXml("PlugIn"));

                return root;
            }
        }
        #endregion


        #region Helper classes: Thresholds
        public class XmlValueInt : QuestBehaviorXmlBase
        {
            public XmlValueInt(XElement xElement)
                : base(xElement)
            {
                Contract.Requires(xElement != null, (context) => "xElement != null");

                _defaultElementName = xElement.Name.ToString();

                try
                {
                    Value = GetAttributeAsNullable<int>("Value", true, new ConstrainTo.Domain<int>(0, int.MaxValue), null) ?? -1;

                    HandleAttributeProblem();
                }

                catch (Exception except)
                {
                    if (Query.IsExceptionReportingNeeded(except))
                        QBCLog.Exception(except, "PROFILE PROBLEM with \"{0}\"", xElement.ToString());

                    IsAttributeProblem = true;
                }
            }

            public int Value { get; private set; }

            private readonly string _defaultElementName;


            public override XElement ToXml(string elementName = null)
            {
                elementName = string.IsNullOrEmpty(elementName) ? _defaultElementName : elementName;

                var root = new XElement(elementName,
                                        new XAttribute("Value", Value));

                return root;
            }
        }
        #endregion
    }
}