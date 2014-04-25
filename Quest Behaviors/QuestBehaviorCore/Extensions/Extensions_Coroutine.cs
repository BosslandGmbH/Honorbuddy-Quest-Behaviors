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
using System.Threading.Tasks;
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
		public ThrottleCoroutineTask(TimeSpan throttle, Func<Task> childProducer)
		{
			Throttle = throttle;
			ChildProducer = childProducer;
		}

		public TimeSpan Throttle { get; private set; }
		public Func<Task> ChildProducer { get; private set; }

		private readonly Stopwatch _timer = new Stopwatch();

		protected override async Task Run()
		{
			if (_timer.IsRunning && _timer.Elapsed < Throttle)
			{
				return;
			}

			await ChildProducer();
			_timer.Restart();			
		}
	}

	public class ThrottleCoroutineTask<T> : CoroutineTask<T>
	{
		public ThrottleCoroutineTask(TimeSpan throttle, Func<Task<T>> childProducer)
		{
			Throttle = throttle;
			ChildProducer = childProducer;
		}

		public TimeSpan Throttle { get; private set; }
		public Func<Task<T>> ChildProducer { get; private set; }

		private readonly Stopwatch _timer = new Stopwatch();

		protected override async Task<T> Run()
		{
			if (_timer.IsRunning && _timer.Elapsed < Throttle)
			{
				return default(T);
			}

			var result = await ChildProducer();
			_timer.Restart();
			return result;
		}
	}


}