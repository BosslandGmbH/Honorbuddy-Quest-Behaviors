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
using System.Collections.Generic;
using System.Linq;

using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public partial class UtilityBehaviorPS
    {
        /// <summary>
        /// <para>This behavior waits MAXWAITTIMEINMILLISECONDSDELEGATE for item ITEMIDDELEGATE to arrive in our bags.
        /// If the item doesn't arrived within the specified time, the behavior is terminated.
        /// </para>
        /// </summary>
        /// <remarks>11May2013-08:25UTC chinajade</remarks>
        public class WaitForInventoryItem : PrioritySelector
            
        {
            public WaitForInventoryItem(ProvideIntDelegate itemIdDelegate,
                                        Action<object> actionOnExpiredTimerDelegate,
                                        ProvideIntDelegate maxWaitTimeInMillisecondsDelegate = null)
            {
                Contract.Requires(itemIdDelegate != null, context => "itemIdDelegate != null");
                Contract.Requires(actionOnExpiredTimerDelegate != null, context => "actionOnExpiredTimerDelegate != null");

                ItemIdDelegate = itemIdDelegate;
                ActionOnExpiredTimerDelegate = actionOnExpiredTimerDelegate;
                MaxWaitTimeInMillisecondsDelegate = maxWaitTimeInMillisecondsDelegate ?? (context => 5000);

                Children = CreateChildren();
            }


            // BT contruction-time properties...
            private Action<object> ActionOnExpiredTimerDelegate { get; set; }
            private ProvideIntDelegate ItemIdDelegate { get; set; }
            private ProvideIntDelegate MaxWaitTimeInMillisecondsDelegate { get; set; }

            // BT visit-time properties...
            private WoWItem CachedWoWItem { get; set; }
            private WaitTimer WaitForItemTimer { get; set; }


            private List<Composite> CreateChildren()
            {
                // NB: Two particular scenarios that this behavior must always perform correctly in...
                // 1) Item not in our bags, the timer is started, we get in combat, when we get out of combat the timer is expired.
                //      The behavior must be certain to check (again) that the item is not in our bag, before terminating the
                //      behavior due to timer expiration.
                // 2) On the last use of the item, it disappears from our bag.
                //      We must address the case where the WoWclient may destroy the item before marking the quest as complete.
                //      So, when the item disappears, we must start the timer again, and allow the quest to complete
                //      before we terminate the behavior due to timer expiration.
                return new List<Composite>()
                {
                    new Action(context =>
                    {
                        CachedWoWItem = Me.CarriedItems.FirstOrDefault(i => (i.Entry == ItemIdDelegate(context)));

                        if (Query.IsViable(CachedWoWItem))
                        {
                            // Squelch the timer, so it will be available if the item disappears from our bags...
                            WaitForItemTimer = null;
                            return RunStatus.Failure;
                        }

                        // If timer is not spinning, create & start it...
                        if (WaitForItemTimer == null)
                        {
                            WaitForItemTimer = new WaitTimer(TimeSpan.FromMilliseconds(MaxWaitTimeInMillisecondsDelegate(context)));
                            WaitForItemTimer.Reset();                                    
                        }

                        // If timer completes, time to call it quits...
                        if (WaitForItemTimer.IsFinished)
                        {
                            QBCLog.ProfileError(
                                "{0} has not arrived in our bag within {1}.",
                                Utility.GetItemNameFromId(ItemIdDelegate(context)),
                                Utility.PrettyTime(WaitForItemTimer.WaitTime));
                            ActionOnExpiredTimerDelegate(context);                                     
                        }

                        else
                        {
                            TreeRoot.StatusText = string.Format("Waiting {0} for {1} to arrive in our bags.",
                                Utility.PrettyTime(WaitForItemTimer.WaitTime),
                                Utility.GetItemNameFromId(ItemIdDelegate(context)));
                        }

                        return RunStatus.Success;
                    })
                };
            }
        }
    }
}