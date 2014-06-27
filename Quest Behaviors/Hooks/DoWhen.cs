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
// DOWHEN either uses an item, or casts a spell, when certain trigger criteria have been met.
// DOWHEN is a 'hook' behavior. Unlike normal quest behaviors, it runs constantly in the background
// once the criteria for performing a 'use item' or 'cast spell' activity have been established.
//
// Basic Attributes:
//      ItemId  [at least one is REQUIRED if Command is "Update": ItemId, SpellId]
//          Identifies the item to be used when the trigger condition defined in USEWHEN
//          has been met.
//          The ItemId attribute is mutually exclusive with SpellId attribute.
//      SpellId [at least one is REQUIRED if Command is "Update": ItemId, SpellId]
//          Identifies the spell to be cast when the trigger condition defined in USEWHEN
//          has been met.
//          The SpellId attribute is mutually exclusive with ItemId attribute.
//      UseWhen [REQUIRED if Command is"Update"]
//          Defines a predicate that must return a boolean value.  When the predicate
//          evaluates to 'true', the item is used, or spell is cast.  Once the activity
//          succeeds, the predicate must evaluate to 'false'; otherwise, the behavior will
//          flag an error with the predicate.
//
// Tunables:
//      AllowUseDuringCombat [optional; Default: false]
//          If true, then using the item or casting the spell is acceptable to try during combat.
//      AllowUseInVehicle [optional; Default: false]
//          If true, then using the item or casting the spell is acceptable to try while in a vehicle.
//      AllowUseWhileFlying [optional; Default: false]
//          If true, then using the item or casting the spell is acceptable to try while flying.
//      AllowUseWhileMounted [optional; Default: false]
//          If true, then using the item or casting the spell is acceptable to try while mounted.
//      Command [optional; ONE OF: Disable, Enable, Remove, ShowActivities, Update; Default: Update]
//          Determines the disposition of the activity that evaluates the item use or spell cast.
//          Please see the examples below, and the purpose will become clear.
//
// THINGS TO KNOW:
// * We refer to the item id (or spell cast) and its trigger condition as an DoWhen 'activity'.
//
// * Once a UseWhen predicate evaluates to 'true', after the associated action is taken
//   (e.g., item is used, or spell is cast), the predicate must immediately return to
//   evaluating as 'false'.  If the predicate remains true after a successful action,
//   then the behavior will flag an error with the predicate.  With bad predicates,
//   erratic Honorbuddy behavior may ensue.
//
// * Attempting to use an Item that is not in the backpack will cause the behavior to
//   emit errors warning of a problem with the profile.  There are two resolutions to
//   this situation:
//     + (Preferred) Remove the corresponding DoWhen in the profile until the item is in the backpack.
//     + Include a "HasItem(XXX) term as part of the UseWhen predicate expression.
//   An isomorphic situation exists when trying to use a spell that is not known.
//
// * Any time the bot is stopped, or a new profile is loaded, DoWhen will clear the list
//   of activities (e.g., item usage, or spell casts).  When the bot is started, the
//   profile will re-load whatever is appropriate.  Similarly, the new profile has a clean
//   slate with which to start working without concern for DoWhen remnants left by
//   a previous profile.
//
// * There is only one activity entry for any given ItemId or SpellId.
//   If you think you are creating more than one activity for the same Id, you are actually replacing
//   the activity already present, instead.
//   However, ItemId and SpellId activities are distinct.  (I.e., you can have an ItemId="123"
//   and SpellId="123" and these will be kept separately.)
//
#endregion


#region Examples
// EXAMPLE:
// This trivial example arranges for the toon to drink all the Refreshing Spring Water
// in his backpack:
//      <CustomBehavior File="Hooks\DoWhen" ItemId="159" UseWhen="!Me.HasAura(&quot;Drink&quot;)" />
//
// The problem with the above example is the toon will continue to try to drink the Refreshing Spring
// Water when none remains in the backpack.  The DoWhen will flag this as an error since the item
// is not in the backpack.  To avoid this situation, we must augment the UseWhen term to take the
// supply of Refreshing Spring Water into consideration:
//      <CustomBehavior File="Hooks\DoWhen" ItemId="159" UseWhen="HasItem(159) &amp;&amp; !Me.HasAura(&quot;Drink&quot;)" />
// (Recall that the DoWhen "Command" attribute defaults to "Update".)
//
// A more realistic example... deploys the Lashtail Hatchling when in Northern Stranglethorn Vale:
//      <CustomBehavior File="Hooks\DoWhen" ItemId="58165" UseWhen="(Me.ZoneId == 33) &amp;&amp; !Me.Minions.Any(m => m.Entry == 42736)" />
// (Recall that Command defaults to "Update".)
// Note that our predicate did not check for the existence of the Lashtail Raptor Egg in the backpack.
// Instead, we chose the better solution of configuring the DoWhen only when the profile already knows the egg
// must already be in our backpack.  This helps a profile writer to locate hard-to-find errors.
//
// At any time, to see the list of current DoWhen activities, use the "ShowActivities" Command:
//      <CustomBehavior File="Hooks\DoWhen" Command="ShowActivities" />
// Output will be generated to the log that looks like the following:
//      [DoWhen-v$Rev$(info)] DoWhenActivities in use (count:2):
//          SpellId(159)
//              Used when: "Me.GotTarget && (Me.CurrentTarget.Entry == 43034)"
//              Enabled=True
//              AllowUseDuringCombat=False, AllowUseInVehicle=False, AllowUseWhileFlying=False, AllowUseWhileMounted=False
//          SpellId(4540)
//              Used when: "Me.GotTarget && (Me.CurrentTarget.Entry == 22435)"
//              Enabled=True
//              AllowUseDuringCombat=False, AllowUseInVehicle=False, AllowUseWhileFlying=False, AllowUseWhileMounted=False
//
// If you need to temporarily disable an activity:
//     <CustomBehavior File="DoWhen" ItemId="159" Command="Disable" />
// and conversely to restore it:
//     <CustomBehavior File="DoWhen" ItemId="159" Command="Enable" />
//
// And lastly, when you are done with an activity, you should remove it:
//     <CustomBehavior File="DoWhen" ItemId="159" Command="Remove" />
//
#endregion


#region Usings
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
#endregion


namespace Honorbuddy.Quest_Behaviors.DoWhen
{
	[CustomBehaviorFileName(@"Hooks\DoWhen")]
	public class DoWhen : QuestBehaviorBase
	{
		#region Constructor and Argument Processing
		private enum CommandType
		{
			Disable,
			Enable,
			Remove,
			ShowActivities,
			Update,
		};


		public DoWhen(Dictionary<string, string> args)
			: base(args)
		{
			try
			{
				// NB: Core attributes are parsed by QuestBehaviorBase parent (e.g., QuestId, NonCompeteDistance, etc)

				// Behavior-specific attributes...
				// NB: We must parse the Command first, as this helps determine whether certain attributes are
				// mandator or optional.
				Command = GetAttributeAsNullable<CommandType>("Command", false, null, null) ?? CommandType.Update;
				
				// Primary attributes...
				ItemId = GetAttributeAsNullable<int>("ItemId", false, ConstrainAs.ItemId, null) ?? 0;
				SpellId = GetAttributeAsNullable<int>("SpellId", false, ConstrainAs.SpellId, null) ?? 0;

				// We test compile the "UseWhen" expression to look for problems.
				// If there is a problem, an exception will be thrown (and handled here).
				Func<bool> isUseWhenRequired = () => { return Command == CommandType.Update; };
				UseWhenExpression = GetAttributeAs<string>("UseWhen", isUseWhenRequired(), ConstrainAs.StringNonEmpty, null) ?? "false";
				if (CompileAttributePredicateExpression("UseWhen", UseWhenExpression) == null)
					{ IsAttributeProblem = true; }

				// Tunables...
				AllowUseDuringCombat = GetAttributeAsNullable<bool>("AllowUseDuringCombat", false, null, null) ?? false;
				AllowUseInVehicle = GetAttributeAsNullable<bool>("AllowUseInVehicle", false, null, null) ?? false;
				AllowUseWhileFlying = GetAttributeAsNullable<bool>("AllowUseWhileFlying", false, null, null) ?? false;
				AllowUseWhileMounted = GetAttributeAsNullable<bool>("AllowUseWhileMounted", false, null, null) ?? false;

				CfbContextForHook = this;
			}

			catch (Exception except)
			{
				// Maintenance problems occur for a number of reasons.  The primary two are...
				// * Changes were made to the behavior, and boundary conditions weren't properly tested.
				// * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
				// In any case, we pinpoint the source of the problem area here, and hopefully it can be quickly
				// resolved.
				QBCLog.Exception(except);
				IsAttributeProblem = true;
			}
		}


		// Variables for Attributes provided by caller
		private bool AllowUseDuringCombat { get; set; }
		private bool AllowUseInVehicle { get; set; }
		private bool AllowUseWhileFlying { get; set; }
		private bool AllowUseWhileMounted { get; set; }
		private CommandType Command { get; set; }
		private int ItemId { get; set; }
		private int SpellId { get; set; }
		private string UseWhenExpression { get; set; }

		private static CustomForcedBehavior CfbContextForHook { get; set; }


		protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
		{
			// empty, for now...
		}

		protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
		{
			// If Id is required, then ItemId or SpellId must have been specified...
			var isIdRequired = (Command != CommandType.ShowActivities);
			UsageCheck_SemanticCoherency(xElement,
				isIdRequired && !(Args.ContainsKey("ItemId") || Args.ContainsKey("SpellId")),
				context => "ItemId or SpellId attribute is required.");

			// If Id is not allowed, then neither ItemId nor SpellId can be specified...
			var isIdAllowed = (Command != CommandType.ShowActivities);
			UsageCheck_SemanticCoherency(xElement,
				!isIdAllowed && (Args.ContainsKey("ItemId") || Args.ContainsKey("SpellId")),
				context => "ItemId and SpellId attributes are not allowed when Command is CommandType.ShowActivities.");

			// ItemId and SpellId attributes are mutually exclusive...
			UsageCheck_SemanticCoherency(xElement,
				Args.ContainsKey("ItemId") && Args.ContainsKey("SpellId"),
				context => "ItemId and SpellId attributes are mutually exclusive.");

			// Is UseWhen allowed?
			var isUseWhenAllowed = (Command == CommandType.Update);
			UsageCheck_SemanticCoherency(xElement,
				Args.ContainsKey("UseWhen") && !isUseWhenAllowed,
				context => "UseWhen attribute is only allowed when Command is \"Update\".");

			// Is Allow* allowed?
			var isAllowUseAllowed = (Command == CommandType.Update);
			UsageCheck_SemanticCoherency(xElement,
				Args.ContainsKey("AllowUseDuringCombat") && !isAllowUseAllowed,
				context => "AllowUseDuringCombat attribute is only allowed when Command is \"Update\".");

			UsageCheck_SemanticCoherency(xElement,
				Args.ContainsKey("AllowUseInVehicle") && !isAllowUseAllowed,
				context => "AllowUseInVehicle attribute is only allowed when Command is \"Update\".");

			UsageCheck_SemanticCoherency(xElement,
				Args.ContainsKey("AllowUseWhileFlying") && !isAllowUseAllowed,
				context => "AllowUseWhileFlying attribute is only allowed when Command is \"Update\".");

			UsageCheck_SemanticCoherency(xElement,
				Args.ContainsKey("AllowUseWhileMounted") && !isAllowUseAllowed,
				context => "AllowUseWhileMounted attribute is only allowed when Command is \"Update\".");
		}
		#endregion


		#region Private and Convenience variables
		private readonly TimeSpan _activityThrottleDelay = TimeSpan.FromMilliseconds(1000);
		private static Composite _persistedDoWhenHookBehavior = null;
		private static readonly List<DoWhenActivity> PersistedActivities = new List<DoWhenActivity>();
		private static WaitTimer _persistedActivityThrottle = null;
		private static bool _persistedIsOnBotStopHooked = false;
		private static bool _persistedIsOnNewProfileLoadedHooked = false;
		#endregion


		#region Overrides of CustomForcedBehavior
		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return "$Id$"; } }
		public override string SubversionRevision { get { return "$Rev$"; } }


		// CreateBehavior supplied by QuestBehaviorBase.
		// Instead, provide CreateMainBehavior definition.

		// Dispose provided by QuestBehaviorBase.

		// IsDone provided by QuestBehaviorBase.
		// Call the QuestBehaviorBase.BehaviorDone() method when you want to indicate your behavior is complete.

		public override void OnFinished()
		{
			// Defend against being called multiple times (just in case)...
			if (!IsOnFinishedRun)
			{
				// QuestBehaviorBase.OnFinished() will set IsOnFinishedRun...
				base.OnFinished();
			}
		}


		public override void OnStart()
		{
			// Acquisition and checking of any sub-elements go here.
			// A common example:
			//     HuntingGrounds = HuntingGroundsType.GetOrCreate(Element, "HuntingGrounds", HuntingGroundCenter);
			//     IsAttributeProblem |= HuntingGrounds.IsAttributeProblem;
			
			// Let QuestBehaviorBase do basic initialization of the behavior, deal with bad or deprecated attributes,
			// capture configuration state, install BT hooks, etc.  This will also update the goal text.
			var isBehaviorShouldRun = OnStart_QuestBehaviorCore();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (isBehaviorShouldRun)
			{
				// The BotStop handler will remove the "use when" activities...
				// Note, we only want to hook BotStopped once for this behavior.
				if (!_persistedIsOnBotStopHooked)
				{
					BotEvents.OnBotStopped += BotEvents_OnBotStopped;
					_persistedIsOnBotStopHooked = true;
				}

				if (!_persistedIsOnNewProfileLoadedHooked)
				{
					BotEvents.Profile.OnNewProfileLoaded += BotEvents_OnNewProfileLoaded;
					_persistedIsOnNewProfileLoadedHooked = true;
				}

				switch (Command)
				{
					case CommandType.Disable:
						ActionSetEnableState(false);
						break;

					case CommandType.Enable:
						ActionSetEnableState(true);
						break;

					case CommandType.ShowActivities:
						ActionShowActivities();
						break;

					case CommandType.Remove:
						ActionRemove();
						break;

					case CommandType.Update:
						ActionUpdate(UseWhenExpression, AllowUseWhileMounted, AllowUseWhileFlying, AllowUseDuringCombat, AllowUseInVehicle);
						break;

					default:
						QBCLog.MaintenanceError("Unhandled action type of '{0}'.", Command);
						TreeRoot.Stop();
						return;
						break;
				}

				// Install or remove behavior as needed...
				// We need to install the hook when its not present, AND there is something to execute.
				// We remove the hook if there is nothing left to execute.  This betters the user's experience,
				// by maximizing performance.
				// NB: We cannot simply override the methods provided by QuestBehaviorBase, because this behavior
				// is unusual.  We want these hooks to remain after this behavior terminates.  If we use the
				// QuestBehaviorBase-provide facilities (e.g., override the methods), then the hooks would be
				// cleaned up (e.g., removed) when this behavior exits.
				if (PersistedActivities.Count > 0)
					{ DoWhenHookInstall(); }

				if (PersistedActivities.Count <= 0)
					{ DoWhenHookRemove(); }

				BehaviorDone();
			}
		}
		#endregion


		#region Main Behaviors
		private Composite CreateDoWhenHook()
		{
			return new ActionRunCoroutine(context => MainCoroutine(context));
		}


		private async Task<bool> MainCoroutine(object context)
		{
			// Ignore, while in non-actionable condition...
			if (Me.IsDead)
			{
				return false;  
			}

			// Ignore if eating or drinking...
			if (IsDrinkingOrEating())
			{
				return false;
			}

			// If throttle is not running, get it started...
			if (_persistedActivityThrottle == null)
			{
				_persistedActivityThrottle = new WaitTimer(_activityThrottleDelay);
				_persistedActivityThrottle.Reset();
			}

			// If throttling in progress, return immediately...
			if (!_persistedActivityThrottle.IsFinished)
			{
				return false;
			}

			// Process each of the activites in play...
			var oldLoggingContext = QBCLog.BehaviorLoggingContext;
			QBCLog.BehaviorLoggingContext = this;
			try
			{
				if (await ExecuteActivities())
					return true;
			}
			finally
			{
				// ensure the QBCLog.BehaviorLoggingContext is reverted back to it's previous value in the event an exception was thown
				QBCLog.BehaviorLoggingContext = oldLoggingContext;
			}

			// Done for this visit...
			_persistedActivityThrottle.Reset();
			return false;
		}

		async Task<bool> ExecuteActivities()
		{
			foreach (var activity in PersistedActivities)
			{
				if (await activity.Execute())
					return true;
			}
			return false;
		}

		#endregion


		#region Helpers
		private void ActionSetEnableState(bool wantEnabled)
		{
			var wantedActivityName = FindActivityName();
			var activity = FindActivity(wantedActivityName);

			// If activity does not exist, that's a problem...
			if (activity == null)
			{
				QBCLog.Error("DoWhenActivity '{0}' is not in use.", wantedActivityName);
				return;
			}

			// Update activity's enabled status...
			var previousState = activity.IsEnabled;
			activity.IsEnabled = wantEnabled;
			QBCLog.DeveloperInfo("DoWhenActivity '{0}' {1} (was {2}).",
				activity.Name,
				(wantEnabled ? "enabled" : "disabled"),
				(previousState ? "enabled" : "disabled"));
		}


		private void ActionRemove()
		{
			var wantedActivityName = FindActivityName();
			var activity = FindActivity(wantedActivityName);

			// If activity does not exist, that's a problem...
			if (activity == null)
			{
				QBCLog.Error("DoWhenActivity '{0}' is not in use.", wantedActivityName);
				return;
			}

			// Remove the activity...
			PersistedActivities.Remove(activity);
			QBCLog.DeveloperInfo("DoWhenActivity '{0}' removed.", activity.Name);
		}


		private void ActionShowActivities()
		{
			var builder = new StringBuilder();

			if (PersistedActivities.Count <= 0)
				{ builder.AppendFormat("No DoWhenActivities in use."); }

			else
			{
				builder.AppendFormat("DoWhenActivities in use (count:{0}):", PersistedActivities.Count);

				foreach (var activity in PersistedActivities.OrderBy(e => e.Name))
				{
					builder.Append(activity.BuildDebugInfo("    "));
				}
			}

			QBCLog.Info(builder.ToString());
		}


		private void ActionUpdate(string useWhenExpression,
								bool allowUseWhileMounted,
								bool allowUseWhileFlying,
								bool allowUseDuringCombat,
								bool allowUseInVehicle)
		{
			var activityName = FindActivityName();
			var existingActivity = FindActivity(activityName);

			// If activity already exists, remove it...
			if (existingActivity != null)
				{ PersistedActivities.Remove(existingActivity); }

			// Install new activity...
			DoWhenActivity doWhenActivity = null;
			if (ItemId > 0)
			{
				doWhenActivity = new DoWhenUseItemActivity(ItemId,
								useWhenExpression,
								allowUseDuringCombat,
								allowUseInVehicle,
								allowUseWhileFlying,
								allowUseWhileMounted);
			}

			if (SpellId > 0)
			{
				doWhenActivity = new DoWhenCastSpellActivity(SpellId,
								useWhenExpression,
								allowUseDuringCombat,
								allowUseInVehicle,
								allowUseWhileFlying,
								allowUseWhileMounted);                
			}

			if (doWhenActivity != null)
			{
				PersistedActivities.Add(doWhenActivity);
				QBCLog.DeveloperInfo("DoWhenActivity '{0}' created:{1}",
					doWhenActivity.Name,
					doWhenActivity.BuildDebugInfo("    "));
			}
		}


		private void BotEvents_OnBotStopped(EventArgs args)
		{
			QBCLog.DeveloperInfo(CfbContextForHook, "OnBotStop cleanup...");
			// Uninstall the behavior from the tree...
			DoWhenHookRemove();

			// Remove our OnNewProfileLoaded handler....
			BotEvents.Profile.OnNewProfileLoaded -= BotEvents_OnNewProfileLoaded;
			_persistedIsOnNewProfileLoadedHooked = false;

			// Remove our OnBotStop handler
			BotEvents.OnBotStopped -= BotEvents_OnBotStopped;
			_persistedIsOnBotStopHooked = false;
		}


		private void BotEvents_OnNewProfileLoaded(EventArgs args)
		{
			QBCLog.DeveloperInfo(CfbContextForHook, "OnNewProfileLoaded cleanup...");
			// Uninstall the behavior from the tree...
			DoWhenHookRemove();
		}


		private void DoWhenHookInstall()
		{
			if (_persistedDoWhenHookBehavior == null)
			{
				_persistedDoWhenHookBehavior = BehaviorHookInstall("Questbot_Main", CreateDoWhenHook());
			}
		}


		private void DoWhenHookRemove()
		{
			if (_persistedDoWhenHookBehavior != null)
			{
				// Log the item activity that are being removed...
				var builder = new StringBuilder();

				if (PersistedActivities.Count <= 0)
					{ builder.AppendFormat("DoWhen hook removed--no DoWhenActivities to clean up."); }

				else
				{
					builder.AppendFormat("Removing DoWhenActivities (count:{0}):", PersistedActivities.Count);

					// Show each activity to be removed...
					foreach (var activity in PersistedActivities.OrderBy(e => e.Name).ToList())
					{
						builder.Append(activity.BuildDebugInfo("    "));
					}
				}

				QBCLog.DeveloperInfo(CfbContextForHook, builder.ToString());
				PersistedActivities.Clear();

				BehaviorHookRemove("Questbot_Main", ref _persistedDoWhenHookBehavior);
				_persistedDoWhenHookBehavior = null;
				_persistedActivityThrottle = null;
			}
		}


		private DoWhenActivity FindActivity(string activityNameToFind)
		{
			return PersistedActivities.FirstOrDefault(e => e.Name == activityNameToFind);
		}


		private string FindActivityName()
		{
			if (ItemId > 0)
				{ return DoWhenUseItemActivity.CreateActivityName(ItemId); }

			if (SpellId > 0)
				{ return DoWhenCastSpellActivity.CreateActivityName(SpellId); }

			return string.Empty;
		}


		private bool IsDrinkingOrEating()
		{
			// NB: We must look up auras "by name" here, instead of "by spell id"...
			// The spell id of a "drinking aura" changes with the drink's level.  However,
			// the name remains the same--"Drink".  Ditto for "Food" auras.
			return Me.GetAllAuras().Any(aura => (aura.Name == "Drink") || (aura.Name == "Food"));
		}
		#endregion


		#region Helper Classes
		private abstract class DoWhenActivity
		{
			protected DoWhenActivity(string activityName,
										string useWhenExpression,
										bool allowUseDuringCombat,
										bool allowUseInVehicle,
										bool allowUseWhileFlying,
										bool allowUseWhileMounted)
			{
				Name = activityName;

				AllowUseDuringCombat = allowUseDuringCombat;
				AllowUseInVehicle = allowUseInVehicle;
				AllowUseWhileFlying = allowUseWhileFlying;
				AllowUseWhileMounted = allowUseWhileMounted;
				IsEnabled = true;

				// We keep the string representation of the expression for use in logging messages, etc...
				UseWhenExpression = useWhenExpression;
				UseWhenPredicateFunc = CompileAttributePredicateExpression("UseWhen", useWhenExpression);
				if (UseWhenPredicateFunc == null)
				{
					throw new ArgumentException("Predicate ({0}) expression doesn't compile.", useWhenExpression);
				}
			}

			public bool AllowUseDuringCombat { get; private set; }
			public bool AllowUseInVehicle { get; private set; }
			public bool AllowUseWhileFlying { get; private set; }
			public bool AllowUseWhileMounted { get; private set; }
			public string Name { get; private set; }
			public bool IsEnabled { get; set; }
			public Func<bool> UseWhenPredicateFunc { get; private set; } 
			public string UseWhenExpression { get; private set; }


			// Methods requiring override...
			public abstract Task<bool> Execute();


			public bool IsExecutionNeeded()
			{
				return
					IsEnabled
					&& (AllowUseDuringCombat || !Me.Combat)
					&& (AllowUseInVehicle || !Query.IsInVehicle())
					&& (AllowUseWhileFlying || !Me.IsFlying)
					&& (AllowUseWhileMounted || !Me.IsMounted())
					&& UseWhenPredicateFunc();
			}
			

			// Utility methods...
			public string BuildDebugInfo(string linePrefix)
			{
				var builder = new StringBuilder();

				builder.AppendFormat("{0}{1}{2}", Environment.NewLine, linePrefix, Name);
				builder.AppendFormat("{0}{1}    Used when: \"{2}\"",
					Environment.NewLine,
					linePrefix,
					UseWhenExpression);
				builder.AppendFormat("{0}{1}    Enabled={2}", Environment.NewLine, linePrefix, IsEnabled);
				builder.AppendFormat("{0}{1}    AllowUseDuringCombat={2}", Environment.NewLine, linePrefix, AllowUseDuringCombat);
				builder.AppendFormat(", AllowUseInVehicle={0}", AllowUseInVehicle);
				builder.AppendFormat(", AllowUseWhileFlying={0}", AllowUseWhileFlying);
				builder.AppendFormat(", AllowUseWhileMounted={0}", AllowUseWhileMounted);

				return builder.ToString();
			}
		}


		/// <summary>
		/// Subclass of DoWhenActivity that knows how to cast spells...
		/// </summary>
		private class DoWhenCastSpellActivity : DoWhenActivity
		{
			public DoWhenCastSpellActivity(int spellId,
									string useWhenExpression,
									bool allowUseDuringCombat,
									bool allowUseInVehicle,
									bool allowUseWhileFlying,
									bool allowUseWhileMounted)
				: base(CreateActivityName(spellId),
						useWhenExpression,
						allowUseDuringCombat, allowUseInVehicle, allowUseWhileFlying, allowUseWhileMounted)
			{
				Contract.Requires(spellId > 0, context => "spellId > 0");

				SpellId = spellId;
			}

			public int SpellId { get; private set; }


			public static string CreateActivityName(int spellId)
			{
				// Note: we can't use Utility.GetSpellNameFromId() for this, because its value
				// will change based on whether or not the spell is known or not.
				return string.Format("SpellId({0})", spellId);
			}


			public override async Task<bool> Execute()
			{
				if (!IsExecutionNeeded())
				{
					return false;
				}

				// If spell is not known at the moment, this is a problem...
				var wowSpell = WoWSpell.FromId(SpellId);
				if ((wowSpell == null) || !wowSpell.IsValid)
				{
					QBCLog.Error(
						"For DoWhenActivity {1}, SpellId({2}) is not known.{0}"
						+ "  This is a profile problem.  To squelch this message there are two options:{0}"
						+ "  * (Preferred) Remove the corresponding DoWhen in the profile until the spell is learned.{0}"
						+ "  * Include a \"HasSpell({2})\" term as part of the UseWhen predicate expression.",
						Environment.NewLine, Name, SpellId);
					return false;
				}

				await UtilityCoroutine.CastSpell(SpellId);

				// If predicate did not clear, then predicate is bad...
				await Coroutine.Sleep((int)Delay.LagDuration.TotalMilliseconds);
				if (UseWhenPredicateFunc())
				{
					QBCLog.Error(
						"For DoWhenActivity {1}, predicate ({2}) was not reset by execution.{0}"
						+ "  This is a profile problem, and can result in erratic Honorbuddy behavior.{0}"
						+ "  The predicate must return to 'false' after the action has been successfully executed.",
						Environment.NewLine, Name, UseWhenExpression);
				}

				return true;
			}
		}


		/// <summary>
		/// Subclass of DoWhenActivity that knows how to use items...
		/// </summary>
		private class DoWhenUseItemActivity : DoWhenActivity
		{
			public DoWhenUseItemActivity(int itemId,
									string useWhenExpression,
									bool allowUseDuringCombat,
									bool allowUseInVehicle,
									bool allowUseWhileFlying,
									bool allowUseWhileMounted)
				: base(CreateActivityName(itemId),
						useWhenExpression,
						allowUseDuringCombat, allowUseInVehicle, allowUseWhileFlying, allowUseWhileMounted)
			{
				Contract.Requires(itemId > 0, context => "itemId > 0");

				ItemId = itemId;
			}

			public int ItemId { get; private set; }


			public static string CreateActivityName(int itemId)
			{
				// Note: we can't use Utility.FindItemNameFromId() for this, because its value
				// will change based on whether or not the item is in our inventory currently.
				return string.Format("ItemId({0})", itemId);                
			}


			public override async Task<bool> Execute()
			{
				if (!IsExecutionNeeded())
				{
					return false;
				}

				// If item is not in inventory at the moment, we consider that a problem...
				// TODO: Convert this to ProfileHelperFunctionBase.HasItem(ItemId), if that method ever becomes static.
				var itemToUse = Me.CarriedItems.FirstOrDefault(i => (i.Entry == ItemId));
				if (!Query.IsViable(itemToUse))
				{
					QBCLog.Error(
						"For DoWhenActivity {1}, ItemId({2}) is not in our inventory.{0}"
						+ "  This is a profile problem.  To squelch this message there are two options:{0}"
						+ "  * (Preferred) Remove the corresponding DoWhen in the profile until the item is in our backpack.{0}"
						+ "  * Include a \"HasItem({2})\" term as part of the UseWhen predicate expression.",
						Environment.NewLine, Name, ItemId);
					return false;
				}

				await UtilityCoroutine.UseItem(ItemId, null /*missing item is non-fatal*/);

				// If predicate did not clear, then predicate is bad...
				await Coroutine.Sleep((int)Delay.LagDuration.TotalMilliseconds);
				if (UseWhenPredicateFunc())
				{
					QBCLog.Error(
						"For DoWhenActivity {1}, predicate ({2}) was not reset by execution.{0}"
						+ "  This is a profile problem, and can result in erratic Honorbuddy behavior.{0}"
						+ "  The predicate must return to 'false' after the action has been successfully executed.",
						Environment.NewLine, Name, UseWhenExpression);
				}

				return true;
			}
		}
		#endregion
	}
}