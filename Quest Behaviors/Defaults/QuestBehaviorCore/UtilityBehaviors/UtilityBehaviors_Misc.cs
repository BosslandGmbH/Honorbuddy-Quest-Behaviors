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
using System.Linq;

using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public abstract partial class QuestBehaviorBase
    {
        /// <summary>
        /// <para>This behavior waits MAXWAITTIMEINMILLISECONDSDELEGATE for item ITEMIDDELEGATE to arrive in our bags.
        /// If the item doesn't arrived within the specified time, the behavior is terminated.
        /// </para>
        /// </summary>
        /// <remarks>11May2013-08:25UTC chinajade</remarks>
        public Composite UtilityBehaviorPS_WaitForInventoryItem(ProvideIntDelegate itemIdDelegate,
                                                                ProvideIntDelegate maxWaitTimeInMillisecondsDelegate = null)
        {
            ContractRequires(itemIdDelegate != null, context => "itemIdDelegate != null");
            maxWaitTimeInMillisecondsDelegate = maxWaitTimeInMillisecondsDelegate ?? (context => 5000);

            // NB: Two particular scenarios that this behavior must always perform correctly in...
            // 1) Item not in our bags, the timer is started, we get in combat, when we get out of combat the timer is expired.
            //      The behavior must be certain to check (again) that the item is not in our bag, before terminating the
            //      behavior due to timer expiration.
            // 2) On the last use of the item, it disappears from our bag.
            //      We must address the case where the WoWclient may destroy the item before marking the quest as complete.
            //      So, when the item disappears, we must start the timer again, and allow the quest to complete
            //      before we terminate the behavior due to timer expiration.
            return new Action(context =>
                {
                    _ubpsWaitForInventoryItem_WoWItem = Me.CarriedItems.FirstOrDefault(i => (i.Entry == itemIdDelegate(context)));

                    if (IsViable(_ubpsWaitForInventoryItem_WoWItem))
                    {
                        // Squelch the timer, so it will be available if the item disappears from our bags...b
                        _ubpsWaitForInventoryItem_WaitTimer = null;
                        return RunStatus.Failure;
                    }

                    // If timer is not spinning, create & start it...
                    if (_ubpsWaitForInventoryItem_WaitTimer == null)
                    {
                        _ubpsWaitForInventoryItem_WaitTimer = new WaitTimer(TimeSpan.FromMilliseconds(maxWaitTimeInMillisecondsDelegate(context)));
                        _ubpsWaitForInventoryItem_WaitTimer.Reset();                                    
                    }

                    // If timer completes, time to call it quits...
                    if (_ubpsWaitForInventoryItem_WaitTimer.IsFinished)
                    {
                        LogProfileError(BuildMessageWithContext(Element,
                            "Unable to locate {0} in our bags--terminating behavior.",
                            GetItemNameFromId(itemIdDelegate(context))));
                        BehaviorDone();                                      
                    }

                    else
                    {
                        TreeRoot.StatusText = string.Format("Waiting {0} for {1} to arrive in our bags.",
                            PrettyTime(_ubpsWaitForInventoryItem_WaitTimer.WaitTime),
                             GetItemNameFromId(itemIdDelegate(context)));
                    }

                    return RunStatus.Success;
                });
        }
        private WoWItem _ubpsWaitForInventoryItem_WoWItem;
        private WaitTimer _ubpsWaitForInventoryItem_WaitTimer = null;
    }
}