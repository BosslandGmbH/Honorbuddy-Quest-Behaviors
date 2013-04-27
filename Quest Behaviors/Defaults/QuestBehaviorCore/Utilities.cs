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
using System.Text.RegularExpressions;

using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public abstract partial class QuestBehaviorBase
    {
        // 18Apr2013-10:41UTC chinajade
        private void AntiAfk()
        {
	        if (_afkTimer.IsFinished)
	        {
		        WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend, TimeSpan.FromMilliseconds(100));
		        _afkTimer.Reset();
	        }   
        }
        private readonly WaitTimer _afkTimer = new WaitTimer(TimeSpan.FromMinutes(2));


        // 25Apr2013-09:15UTC chinajade
        public TimeSpan GetEstimatedMaxTimeToDestination(WoWPoint destination)
        {
            double distanceToCover = 
                (Me.IsSwimming || Me.IsFlying)
                ? Me.Location.Distance(destination)
                : Me.Location.SurfacePathDistance(destination);

            double myMovementSpeed =
                Me.IsSwimming
                ? Me.MovementInfo.SwimmingForwardSpeed
                : Me.MovementInfo.RunSpeed;

            double timeToDestination = distanceToCover / myMovementSpeed;

            timeToDestination = Math.Max(timeToDestination, 20.0);  // 20sec hard lower limit
            timeToDestination *= 2.5;   // factor of safety

            return (TimeSpan.FromSeconds(timeToDestination));            
        }


        // 20Apr2013-12:50UTC chinajade
        public string GetItemNameFromId(int wowItemId)
        {
            var wowItem = Me.CarriedItems.FirstOrDefault(i => (i.Entry == wowItemId));

            return (wowItem != null)
                ? wowItem.Name
                : string.Format("ItemId({0})", wowItemId);
        }

        
        // 11Apr2013-04:41UTC chinajade
        public string GetObjectNameFromId(int wowObjectId)
        {
            var wowObject = FindObjectsFromIds(ToEnumerable<int>(wowObjectId)).FirstOrDefault();

            return (wowObject != null)
                ? wowObject.Name
                : string.Format("MobId({0})", wowObjectId);
        }


        public static WoWPoint GetPointToGainDistance(WoWObject target, double minDistanceNeeded)
        {
            var minDistance = (float)(minDistanceNeeded + /*epsilon*/(2 * Navigator.PathPrecision));
            var myLocation = Me.Location;

            Func<WoWObject, WoWPoint, bool> isPointViable = (selectedTarget, potentialDestination) =>
            {
                var targetLocation = selectedTarget.Location;

                return
                    targetLocation.Distance(potentialDestination) > minDistance
                    && (myLocation.Distance(potentialDestination) < targetLocation.Distance(potentialDestination))
                    && GameWorld.IsInLineOfSight(potentialDestination, targetLocation);
            };

            // If the previously calculated point is still viable, use it...
            if (isPointViable(target, _gainDistancePoint))
            {
                return _gainDistancePoint;
            }

            // Otherwise, find a new point...
            WoWObject moveTowardsObject = null;

            if (!(StyxWoW.Me.IsFlying || StyxWoW.Me.IsSwimming))
            {
                using (StyxWoW.Memory.AcquireFrame())
                {
                    moveTowardsObject =
                       (from wowObject in ObjectManager.GetObjectsOfType<WoWObject>(true, false)
                        where
                            wowObject.IsValid
                            && isPointViable(target, wowObject.Location)
                        orderby
                            myLocation.SurfacePathDistance(wowObject.Location)
                        select wowObject)
                        .FirstOrDefault();
                }
            }

            _gainDistancePoint =
                (moveTowardsObject != null)
                    ? moveTowardsObject.Location
                    // Resort to brute force...
                    : WoWMathHelper.CalculatePointFrom(myLocation, target.Location, minDistance);
  
            return _gainDistancePoint;
        }
        private static WoWPoint _gainDistancePoint;


        // 25Apr2013-11:42UTC chinajade
        public string GetVersionedBehaviorName()
        {
            Func<string, string>    utilStripSubversionDecorations =
                (subversionString) =>
                {
                    var regexSvnDecoration = new Regex("^\\$[^:]+:[:]?[ \t]*([^$]+)[ \t]*\\$$");

                    return regexSvnDecoration.Replace(subversionString, "$1").Trim();
                };

            return _versionedBehaviorName ?? (_versionedBehaviorName = 
                string.Format("{0}-v{1}",
                    GetType().Name,
                    utilStripSubversionDecorations(SubversionRevision)));
        }       
        private string _versionedBehaviorName = null;
        
        
        //  9Mar2013-12:34UTC chinajade
        public static string PrettyMoney(ulong totalCopper)
        {
            ulong moneyCopper = totalCopper % 100;
            totalCopper /= 100;

            ulong moneySilver = totalCopper % 100;
            totalCopper /= 100;

            ulong moneyGold = totalCopper;

            string formatString =
                (moneyGold > 0) ? "{0}g{1:D2}s{2:D2}c"
                : (moneySilver > 0) ? "{1}s{2:D2}c"
                : "{2}c";

            return string.Format(formatString, moneyGold, moneySilver, moneyCopper);
        }


        //  9Mar2013-12:34UTC chinajade
        public static string PrettyTime(TimeSpan duration)
        {
            int milliSeconds = (int)duration.TotalMilliseconds;

            return
                (milliSeconds == 0) ? "0s"  // we prefer zero expressed in terms of seconds, instead of milliseconds
                : (milliSeconds < 1000) ? string.Format("{0}ms", milliSeconds)
                : ((milliSeconds % 1000) == 0) ? string.Format("{0}s", milliSeconds / 1000)
                : string.Format("{0:F3}s", milliSeconds / 1000);
        }

        
        // 12Mar2013-08:27UTC chinajade
        public static IEnumerable<T> ToEnumerable<T>(T item)
        {
            yield return item;
        }


        protected void UpdateGoalText(string extraGoalTextDescription)
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

            TreeRoot.GoalText = string.Format(
                "{1}: \"{2}\"{0}{3}{0}{0}{4}",
                Environment.NewLine,
                GetVersionedBehaviorName(),
                ((quest != null)
                    ? string.Format("\"{0}\" (QuestId: {1})", quest.Name, QuestId)
                    : "In Progress (no associated quest)"),
                (extraGoalTextDescription ?? string.Empty),
                GetProfileReference(Element));            
        }
    }
}