﻿// Originally contributed by Chinajade.
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
using System.Collections;
using System.Collections.Generic;
using CommonBehaviors.Actions;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

#endregion

namespace Honorbuddy.QuestBehaviorCore
{
	public static partial class UtilityCoroutine
	{
		// I realize this isn't thread safe but should not be a problem since behaviors are generally run on same thread..
		// if it is a problem then we need to wrap CastSpell in a class instance
		private static bool IsInterrupted { get; set; }

		public static IEnumerator CastSpell(
			int spellId,
			WoWObject target = null,
			System.Action actionOnSuccessfulSpellCastDelegate = null)
		{
			// Viable target?
			// If target is null, then assume 'self'.
			// NB: Since target may go invalid immediately upon casting the spell,
			// we cache its name for use in subsequent log entries.
			var selectedObject = target ?? Me;
			if (!Query.IsViable(selectedObject))
			{
				QBCLog.Warning("Target is not viable!");
				yield return false;
				yield break;
			}

			var targetName = selectedObject.SafeName;

			// Target must be a WoWUnit for us to be able to cast a spell on it...
			var selectedTarget = selectedObject as WoWUnit;
			if (!Query.IsViable(selectedTarget))
			{
				QBCLog.Warning("Target {0} is not a WoWUnit--cannot cast spell on it.", targetName);
				yield return false;
				yield break;
			}

			// Spell known?
			WoWSpell selectedSpell = WoWSpell.FromId(spellId);
			if (selectedSpell == null)
			{
				QBCLog.Warning("{0} is not known.", Utility.GetSpellNameFromId(spellId));
				yield return false;
				yield break;
			}
			var spellName = selectedSpell.Name;

			// Need to be facing target...
			// NB: Not all spells require this, but many do.
			Utility.Target(selectedTarget, true);

			// Wait for spell to become ready...
			if (!SpellManager.CanCast(selectedSpell))
			{
				QBCLog.Warning(
					"{0} is not usable, yet.  (cooldown remaining: {1})",
					spellName,
					Utility.PrettyTime(selectedSpell.CooldownTimeLeft));
				yield return false;
				yield break;
			}

			// Notify user of intent...
			var message = string.Format("Attempting cast of '{0}' on '{1}'", spellName, targetName);
			message +=
				selectedTarget.IsDead
					? " (dead)"
					: string.Format(" (health: {0:F1})", selectedTarget.HealthPercent);
			QBCLog.DeveloperInfo(message);

			// Set up 'interrupted use' detection, and cast spell...
			// MAINTAINER'S NOTE: Once these handlers are installed, make sure all possible exit paths from the outer
			// Sequence unhook these handlers.  I.e., if you plan on returning RunStatus.Failure, be sure to call
			// UtilityBehaviorSeq_UseItemOn_HandlersUnhook() first.
			InterruptDetection_Hook();
			IsInterrupted = false;
			SpellManager.Cast(selectedSpell, selectedTarget);

			// NB: The target or the spell may not be valid after this point...
			// Some targets will go 'invalid' immediately afer interacting with them.
			// Most of the time this happens, the target is immediately and invisibly replaced with
			// an identical looking target with a different script.
			// We must assume our target and spell is no longer available for use after this point.

			yield return StyxCoroutine.Sleep((int)Delay.AfterItemUse.TotalMilliseconds);

			// If item use requires a second click on the target (e.g., item has a 'ground target' mechanic)...
			yield return CastPendingSpell(selectedTarget);

			// Wait for any casting to complete...
			// NB: Some interactions or item usages take time, and the WoWclient models this as spellcasting.
			yield return StyxCoroutine.Wait(15000, () => !(Me.IsCasting || Me.IsChanneling));
			// Were we interrupted in spell casting?
			InterruptDectection_Unhook();

			if (IsInterrupted)
			{
				QBCLog.Warning("Cast of {0} interrupted.", spellName);
				// Give whatever issue encountered a chance to settle...
				// NB: --we want the Sequence to fail when delay completes.
				yield return StyxCoroutine.Sleep(1500);
				yield return false;
				yield break;
			}

			QBCLog.DeveloperInfo("Cast of '{0}' on '{1}' succeeded.", spellName, targetName);

			if (actionOnSuccessfulSpellCastDelegate != null)
				actionOnSuccessfulSpellCastDelegate();

			yield return true;
		}

		private static void InterruptDetection_Hook()
		{
			Lua.Events.AttachEvent("UNIT_SPELLCAST_FAILED", HandleInterrupted);
			Lua.Events.AttachEvent("UNIT_SPELLCAST_INTERRUPTED", HandleInterrupted);
		}

		private static void InterruptDectection_Unhook()
		{
			Lua.Events.DetachEvent("UNIT_SPELLCAST_FAILED", HandleInterrupted);
			Lua.Events.DetachEvent("UNIT_SPELLCAST_INTERRUPTED", HandleInterrupted);
		}

		private static void HandleInterrupted(object sender, LuaEventArgs args)
		{
			var unitId = args.Args[0].ToString();

			if (unitId != "player") return;
			// If it was a channeled spell, and still casting

			var spellName = args.Args[1].ToString();
			//var rank = args.Args[2].ToString();
			//var lineId = args.Args[3].ToString();
			var spellId = args.Args[4].ToString();

			QBCLog.DeveloperInfo(
				"\"{0}\"({1}) interrupted via {2} Event.",
				spellName,
				spellId,
				args.EventName);
			IsInterrupted = true;
		}

		public static IEnumerator CastPendingSpell(WoWObject selectedTarget)
		{
			if (StyxWoW.Me.CurrentPendingCursorSpell != null)
			{
				// If target is still viable, click it as destination of spell...
				if (Query.IsViable(selectedTarget))
				{
					SpellManager.ClickRemoteLocation(selectedTarget.Location);
				}
				else
				{
					Lua.DoString("SpellStopTargeting()");
				}
				yield return StyxCoroutine.Wait(
					(int) Delay.LagDuration.TotalMilliseconds,
					() => StyxWoW.Me.CurrentPendingCursorSpell == null);

				// If we've leftover spell cursor dangling, clear it...
				// NB: This can happen for "use item on location" type activites where you get interrupted
				// (e.g., a walk-in mob).
				if (StyxWoW.Me.CurrentPendingCursorSpell != null)
				{
					Lua.DoString("SpellStopTargeting()");
				}
			}
		}

	}
}