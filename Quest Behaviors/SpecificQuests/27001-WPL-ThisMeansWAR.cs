// Behavior originally contributed by HighVoltz.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//
//

#region Summary and Documentation
// This behavior is tailored for the quest http://www.wowhead.com/quest=27789/troggish-troubles 
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Honorbuddy.Quest_Behaviors.WaitTimerBehavior;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
using WaitTimer = Styx.Common.Helpers.WaitTimer;

#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.ThisMeansWAR
{
	[CustomBehaviorFileName(@"SpecificQuests\27001-WPL-ThisMeansWAR")]
	public class ThisMeansWAR : CustomForcedBehavior
	{
		public ThisMeansWAR(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;
		}


		private LocalPlayer Me { get { return (StyxWoW.Me); } }
		readonly WoWPoint _lumberMillLocation = new WoWPoint(2427.133, -1649.115, 104.0841);
		readonly WoWPoint _spiderSpawnLocation = new WoWPoint(2332.387, -1694.623, 104.5099);
		private  WoWPoint _spiderScareLoc;
		private WoWUnit _currentTarget;
		readonly Stopwatch _noMoveBlacklistTimer = new Stopwatch();
		private WaitTimer _blacklistTimer = new WaitTimer(TimeSpan.FromSeconds(45));

		public override bool IsDone
		{
			get
			{
				var quest = Me.QuestLog.GetQuestById(27001);

				var done = (quest != null && quest.IsCompleted);
				return done;
			}
		}

		public override void OnStart()
		{
			if (!IsDone)
			{
				Targeting.Instance.RemoveTargetsFilter += Instance_RemoveTargetsFilter;

				this.UpdateGoalText(0);
			}
		}

        public override void OnFinished()
        {
            Targeting.Instance.RemoveTargetsFilter -= Instance_RemoveTargetsFilter;
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }

		private static void Instance_RemoveTargetsFilter(List<WoWObject> units)
		{
			units.Clear();
		}

		private Composite _root;
		protected override Composite CreateBehavior()
		{
			return _root ?? (_root = new ActionRunCoroutine((ctx => ScareSpiders())));
		}

		private async Task<bool> ScareSpiders()
		{
			// if not in a turret than move to one and interact with it
			if (!Query.IsInVehicle())
			{
				var mustang = GetMustang();
				if (mustang == null)
				{
					QBCLog.Warning("No mustang was found nearby");
					return false;
				}
				
				TreeRoot.StatusText = "Moving To Mustang";
				if (mustang.DistanceSqr > 5*5)
					return (await CommonCoroutines.MoveTo(mustang.Location)).IsSuccessful();

				await CommonCoroutines.LandAndDismount();
				QBCLog.Info("Interacting with Mustang");
				mustang.Interact();
				return true;
			}

			// Find the nearest spider and if none exist then move to the spawn location
			if (!Query.IsViable(_currentTarget) || !_currentTarget.IsAlive)
			{
				_currentTarget = ObjectManager.GetObjectsOfType<WoWUnit>()
					.Where(u => u.IsAlive && u.Entry == 44284 && !Blacklist.Contains(u, BlacklistFlags.Interact))
					.OrderBy(u => u.DistanceSqr).FirstOrDefault();

				if (_currentTarget == null)
				{
					if (!Navigator.AtLocation(_spiderSpawnLocation))
						return (await CommonCoroutines.MoveTo(_spiderSpawnLocation)).IsSuccessful();
					TreeRoot.StatusText = "Waiting for spiders to spawn";
					return true;
				}
				_noMoveBlacklistTimer.Reset();
				_blacklistTimer.Reset();
				QBCLog.Info("Locked on a new target. Distance {0}", _currentTarget.Distance);
			}

			TreeRoot.StatusText = "Scaring spider towards lumber mill";

			var moveToPoint = WoWMathHelper.CalculatePointFrom(_lumberMillLocation, _currentTarget.Location, -6);

			if (moveToPoint.DistanceSqr((WoWMovement.ActiveMover ?? StyxWoW.Me).Location) > 4 * 4)
				return (await CommonCoroutines.MoveTo(moveToPoint)).IsSuccessful();

			// spider not moving? blacklist and find a new target.
			if (_noMoveBlacklistTimer.ElapsedMilliseconds > 20000 && _currentTarget.Location.DistanceSqr(_spiderScareLoc) < 10*10)
			{
				Blacklist.Add(_currentTarget, BlacklistFlags.Interact, TimeSpan.FromMinutes(3), "Spider is not moving");
				_currentTarget = null;
			}
			else if (_blacklistTimer.IsFinished)
			{
				Blacklist.Add(_currentTarget, BlacklistFlags.Interact, TimeSpan.FromMinutes(3), "Took too long");
				_currentTarget = null;
			}
			else if (!_currentTarget.HasAura("Fear"))
			{
				await CommonCoroutines.StopMoving();
				Me.SetFacing(_lumberMillLocation);
				await CommonCoroutines.SleepForLagDuration();
				await Coroutine.Sleep(200);
				if (!_noMoveBlacklistTimer.IsRunning || _currentTarget.Location.DistanceSqr(_spiderScareLoc) >= 10 * 10)
				{
					_noMoveBlacklistTimer.Restart();
					_spiderScareLoc = _currentTarget.Location;
				}
				Lua.DoString("CastSpellByID(83605)");
				await Coroutine.Wait(3000, () => Query.IsViable(_currentTarget) && _currentTarget.HasAura("Fear"));
			}

			return true;
		}


		WoWUnit GetMustang()
		{
			return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => (!u.CharmedByUnitGuid.IsValid || u.CharmedByUnitGuid == Me.Guid) && u.Entry == 44836)
				.OrderBy(u => u.DistanceSqr).
				FirstOrDefault();
		}
	}
}
