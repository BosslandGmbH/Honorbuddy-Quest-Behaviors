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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Media;
using System.Runtime.CompilerServices;
using System.Threading;
using Buddy.Coroutines;
using Styx;
using Styx.TreeSharp;
using Styx.CommonBot.Coroutines;
using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
	public class ThrottleCoroutineTask : CoroutineTask
	{
		public ThrottleCoroutineTask(TimeSpan throttle, Func<IEnumerator> child)
		{
			Throttle = throttle;
			Child = child;
		}

		public TimeSpan Throttle { get; private set; }
		public Func<IEnumerator> Child { get; private set; }

		private readonly Stopwatch _timer = new Stopwatch();

		protected override IEnumerator Run()
		{
			if (_timer.IsRunning && _timer.Elapsed < Throttle)
			{
				yield return false;
				yield break;
			}

			yield return Child();
			_timer.Restart();
		}
	}


}