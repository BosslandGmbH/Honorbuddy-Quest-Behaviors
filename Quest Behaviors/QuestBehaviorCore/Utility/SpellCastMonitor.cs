#region Usings
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Bots.Grind;
using Buddy.Coroutines;
using Honorbuddy.Quest_Behaviors.WaitTimerBehavior;
using Styx;
using Styx.WoWInternals;
using WaitTimer = Styx.Common.Helpers.WaitTimer;

#endregion


namespace Honorbuddy.QuestBehaviorCore
{
	/// <summary>Monitors a spell cast</summary>
	internal class SpellCastMonitor : IDisposable
	{
		#region Constructor and argument processing

		/// <summary>
		/// Initializes a new instance of the <see cref="SpellCastMonitor" /> class.
		/// </summary>
		/// <param name="spellId">The spell identifier.</param>
		private SpellCastMonitor(int? spellId = null)
		{
			SpellId = spellId;
			Result = SpellCastResult.Indeterminate;

			Lua.Events.AttachEvent(
				"COMBAT_LOG_EVENT_UNFILTERED",
				CombatLogEventUnfilteredHandler,
				"return args[4] == UnitGUID('player') and (args[2] == 'SPELL_MISSED' or args[2] == 'SPELL_CAST_FAILED')");

			Lua.Events.AttachEvent("UNIT_SPELLCAST_SUCCEEDED", UnitSpellcastSucceededHandler);
			Lua.Events.AttachEvent("UNIT_SPELLCAST_INTERRUPTED", UnitSpellcastInterruptedHandler);
		}

		/// <summary>
		/// Starts monitoring a spell cast 
		/// The spell being monitord should be casted after this call.
		/// </summary>
		/// <param name="spellId">The spell identifier.</param>
		/// <returns></returns>
		public static SpellCastMonitor Start(int? spellId)
		{
			return new SpellCastMonitor(spellId);
		}

		#endregion

		#region Fields

		private static readonly Dictionary<string, string> _localizedSymbols = new Dictionary<string, string>();

		#endregion

		#region Properties

		/// <summary>Gets the spell identifier. If <c>null</c> then the first spell cast by toon is monitored</summary>
		public int? SpellId { get; private set; }
		public string FailReason { get; private set; }
		public bool HasResult { get; private set; }
		public SpellCastResult Result { get; private set; }

		#endregion

		#region Handlers

		private void CombatLogEventUnfilteredHandler(object sender, LuaEventArgs args)
		{
			if (HasResult)
				return;

			var sourceGuid = ArgToGuid(args.Args[3]);
			if (sourceGuid != StyxWoW.Me.Guid)
				return;

			if (SpellId.HasValue)
			{
				var spellId = (int)(double)args.Args[11];
				if (spellId != SpellId.Value)
					return;
			}

			var eventName = args.Args[1].ToString();

			if (eventName == "SPELL_MISSED")
			{
				Result = SpellCastResult.Missed;
				HasResult = true;
				return;
			}

			if (eventName != "SPELL_CAST_FAILED")
				return;

			FailReason = args.Args[14].ToString();

			Result = GetResultFromError(FailReason);
			HasResult = true;
		}

		private void UnitSpellcastSucceededHandler(object sender, LuaEventArgs args)
		{
			UnitSpellcastHandler(args, SpellCastResult.Succeeded);
		}

		private void UnitSpellcastInterruptedHandler(object sender, LuaEventArgs args)
		{
			UnitSpellcastHandler(args, SpellCastResult.Interrupted);
		}

		private void UnitSpellcastHandler(LuaEventArgs args, SpellCastResult result)
		{
			if (HasResult)
				return;

			if (args.Args[0].ToString() != "player")
				return;

			if (SpellId.HasValue)
			{
				var spellId = (int)(double)args.Args[4];
				if (spellId != SpellId.Value)
					return;
			}

			Result = result;
			HasResult = true;
		}

		#endregion


		#region Behavior

		/// <summary>Waits until a result is available for a spell cast and then retrurn it.</summary>
		/// <param name="maxTimeoutMs">The maximum timeout in milliseconds.</param>
		public async Task<SpellCastResult> GetResult(int maxTimeoutMs = 15000)
		{
			var timer = new WaitTimer(TimeSpan.FromMilliseconds(StyxWoW.WoWClient.Latency * 2 + 50));
			timer.Reset();
			var startedCast = false;
			Func<bool> beganCast = () =>
			{
				startedCast = startedCast || StyxWoW.Me.IsCasting;
				return startedCast;
			};

			await Coroutine.Wait(maxTimeoutMs, () => HasResult || timer.IsFinished && !beganCast());

			if (!HasResult && timer.IsFinished && !beganCast())
				return SpellCastResult.NoCastStarted;

			return Result;
		}

		#endregion

		#region IDisposable

		public void Dispose()
		{
			Lua.Events.DetachEvent("COMBAT_LOG_EVENT_UNFILTERED", CombatLogEventUnfilteredHandler);
			Lua.Events.DetachEvent("UNIT_SPELLCAST_SUCCEEDED", UnitSpellcastSucceededHandler);
			Lua.Events.DetachEvent("UNIT_SPELLCAST_INTERRUPTED", UnitSpellcastInterruptedHandler);
		}

		#endregion

		#region Helpers

		// Stolen from Singluar.
		private static WoWGuid ArgToGuid(object o)
		{
			string svalue = o.ToString();
			try
			{
			    WoWGuid guid;
                if (WoWGuid.TryParseFriendly(svalue, out guid))
			        return guid;
			}
			catch
			{
				QBCLog.Debug("error parsing Guid '{0}'", o.ToString());
			}
		    return WoWGuid.Empty;
		}

		private SpellCastResult GetResultFromError(string error)
		{
			if (error == SpellFailedTooClose)
				return SpellCastResult.TooClose;

			if (error == SpellFailedOutOfRange)
				return SpellCastResult.OutOfRange;

			if (error == SpellFailedLineOfSight)
				return SpellCastResult.LineOfSight;

			if (error == SpellFailedInterrupted)
				return SpellCastResult.Interrupted;

			if (error == SpellFailedBadTargets)
				return SpellCastResult.InvalidTarget;

			if (error == SpellFailedNotKnown)
				return SpellCastResult.SpellNotKnown;

			if (error == SpellFailedNotReady)
				return SpellCastResult.NotReady;

			return SpellCastResult.UnknownFail;
		}

		private static string GetLocalizedSymbol(string symbol)
		{
			string ret;
			if (_localizedSymbols.TryGetValue(symbol, out ret))
				return ret;

			ret = Lua.State.Globals.GetField(symbol).Value.String.Value;
			_localizedSymbols.Add(symbol, ret);
			return ret;
		}

		#endregion

		#region Localized Strings

		private static string SpellFailedBadTargets { get { return GetLocalizedSymbol("SPELL_FAILED_BAD_TARGETS"); } }
		private static string SpellFailedInterrupted { get { return GetLocalizedSymbol("SPELL_FAILED_INTERRUPTED"); } }
		private static string SpellFailedTooClose { get { return GetLocalizedSymbol("SPELL_FAILED_TOO_CLOSE"); } }
		private static string SpellFailedOutOfRange { get { return GetLocalizedSymbol("SPELL_FAILED_OUT_OF_RANGE"); } }
		private static string SpellFailedLineOfSight { get { return GetLocalizedSymbol("SPELL_FAILED_LINE_OF_SIGHT"); } }
		private static string SpellFailedNotKnown { get { return GetLocalizedSymbol("SPELL_FAILED_NOT_KNOWN"); } }
		private static string SpellFailedNotReady { get { return GetLocalizedSymbol("SPELL_FAILED_NOT_READY"); } }

		#endregion
	}
}
