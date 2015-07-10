// Originally contributed by Highvoltz.
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
using System.Collections.Generic;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Styx.CommonBot.Profiles;

#endregion

namespace Honorbuddy.QuestBehaviorCore
{
    public class HookHelpers
    {
        private static readonly Dictionary<string, bool> HookExecutionStates = new Dictionary<string, bool>();

        /// <summary>
        ///     Executes a hook. This also sets QBCLog.BehaviorLoggingContext and logs when hook starts/stops executing, if enabled
        /// </summary>
        /// <param name="cb">The CustomForcedBehavior.</param>
        /// <param name="taskProducer">The task producer.</param>
        /// <param name="name">The name. Used to identify hooks that can have multiple instances running at a time.</param>
        /// <param name="logExecution">if set to <c>true</c> [log execution].</param>
        /// <returns>
        ///     Returns value produced by the coroutine returned from <paramref name="taskProducer" />.
        /// </returns>
        /// <exception cref="Exception">Coroutine status was invalid</exception>
        /// <exception cref="System.Exception">Coroutine status was invalid</exception>
        public static async Task<bool> ExecuteHook(CustomForcedBehavior cb, Func<Task<bool>> taskProducer, string name = "", bool logExecution = true)
        {
            using (var coroutine = new Coroutine(async () => await taskProducer()))
            {
                var hookType = cb.GetType();

                string identifier = hookType + name;

                // Change the logging context to the Hook instance so QBCLog messages correctly display the originating QB.
                var originalLoggingContext = QBCLog.BehaviorLoggingContext;
                QBCLog.BehaviorLoggingContext = cb;
                try
                {
                    while (true)
                    {
                        coroutine.Resume();

                        bool executed;
                        switch (coroutine.Status)
                        {
                            case CoroutineStatus.Runnable:
                                executed = true;
                                break;
                            case CoroutineStatus.RanToCompletion:
                                executed = (bool) coroutine.Result;
                                break;
                            default:
                                throw new Exception("Unexpected Coroutine status");
                        }

                        if (logExecution)
                        {
                            bool executedPreviously;
                            HookExecutionStates.TryGetValue(identifier, out executedPreviously);

                            if (executed != executedPreviously)
                            {
                                HookExecutionStates[identifier] = executed;
                                // log execution state changes.
                                QBCLog.Debug(executed ? "Executing {0}hook" : "Stopped executing {0}hook", name != null ? name + " " : "");
                            }
                        }

                        if (coroutine.Status != CoroutineStatus.Runnable)
                            return executed;

                        await Coroutine.Yield();
                    }
                }
                finally
                {
                    QBCLog.BehaviorLoggingContext = originalLoggingContext;
                }
            }
        }
    }
}