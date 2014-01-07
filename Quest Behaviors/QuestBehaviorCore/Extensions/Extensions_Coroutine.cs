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
	// Note: Remove once an equivalent class is in HB core.
	/// <summary>
	/// Represents a sub coroutine that can be derived from to implement coroutine parts with state.
	/// </summary>
	/// <example></example>
	public abstract class SubCoroutine : IEnumerator
	{
		/// <summary>
		/// Provides the body of this sub coroutine.
		/// </summary>
		/// <returns>An <see cref="IEnumerator"/> that when iterated is the body of this sub coroutine.</returns>
		protected abstract IEnumerator Run();

		private bool _yielded;

		bool IEnumerator.MoveNext()
		{
			// The first MoveNext returns true and constructs _current
			// That is then returned and ran to full by Coroutine
			// When the inner IEnumerator is done, MoveNext is called again and returns false
			// This IEnumerator is then popped off (and disposed, if applicable) - ready to be yielded again.

			_yielded = !_yielded;
			if (!_yielded)
				return false;

			_current = Coroutine.WithDebugInfo(Run(), "Styx.CommonBot.Coroutines.MoveNext");
			return true;
		}

		private object _current;
		object IEnumerator.Current { get { return _current; } }

		void IEnumerator.Reset()
		{
		}
	}

	public class ThrottleSubCoroutine : SubCoroutine
	{
		public ThrottleSubCoroutine(TimeSpan throttle, Func<IEnumerator> child)
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