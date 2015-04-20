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
using System.Threading.Tasks;
using Buddy.Coroutines;

using Styx;
using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
	public static partial class UtilityCoroutine
	{
		public static async Task<SpellCastResult> CastSpell(
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
				return SpellCastResult.InvalidTarget;
			}

			var targetName = selectedObject.SafeName;

			// Target must be a WoWUnit for us to be able to cast a spell on it...
			var selectedTarget = selectedObject as WoWUnit;
			if (!Query.IsViable(selectedTarget))
			{
				QBCLog.Warning("Target {0} is not a WoWUnit--cannot cast spell on it.", targetName);
				return SpellCastResult.InvalidTarget;
			}

			// Spell known?
			WoWSpell selectedSpell = WoWSpell.FromId(spellId);
			if (selectedSpell == null)
			{
				QBCLog.Warning("{0} is not known.", Utility.GetSpellNameFromId(spellId));
				return SpellCastResult.SpellNotKnown;
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
				return SpellCastResult.NotReady;
			}

			// Notify user of intent...
			var message = string.Format("Attempting cast of '{0}' on '{1}'", spellName, targetName);
			message +=
				selectedTarget.IsDead
					? " (dead)"
					: string.Format(" (health: {0:F1})", selectedTarget.HealthPercent);
			QBCLog.DeveloperInfo(message);

			// Set up 'interrupted use' detection, and cast spell...
			using (var castMonitor = SpellCastMonitor.Start(spellId))
			{
				SpellManager.Cast(selectedSpell, selectedTarget);

				// NB: The target or the spell may not be valid after this point...
				// Some targets will go 'invalid' immediately afer interacting with them.
				// Most of the time this happens, the target is immediately and invisibly replaced with
				// an identical looking target with a different script.
				// We must assume our target and spell is no longer available for use after this point.

				await Coroutine.Sleep((int)Delay.AfterItemUse.TotalMilliseconds);

				// If item use requires a second click on the target (e.g., item has a 'ground target' mechanic)...
				await CastPendingSpell(selectedTarget);

				// Wait for any casting to complete...
				// NB: Some interactions or item usages take time, and the WoWclient models this as spellcasting.

				var castResult = await castMonitor.GetResult();

				if (castResult != SpellCastResult.Succeeded)
				{
					string reason = castResult == SpellCastResult.UnknownFail 
						? castMonitor.FailReason
						: castResult.ToString();

					QBCLog.Warning("Cast of {0} failed. Reason: {1}", spellName, reason);
					// Give whatever issue encountered a chance to settle...
					// NB: --we want the Sequence to fail when delay completes.
					if (castResult != SpellCastResult.LineOfSight
						&& castResult != SpellCastResult.OutOfRange
						&& castResult != SpellCastResult.TooClose)
					{
						await Coroutine.Sleep(1500);
					}
					return castResult;
				}

				QBCLog.DeveloperInfo("Cast of '{0}' on '{1}' succeeded.", spellName, targetName);

				if (actionOnSuccessfulSpellCastDelegate != null)
					actionOnSuccessfulSpellCastDelegate();

				return SpellCastResult.Succeeded;
			}
		}

		public static async Task CastPendingSpell(WoWObject selectedTarget)
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
				await Coroutine.Wait(
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