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
using System.Xml;
using System.Xml.Linq;

using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
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
        public TimeSpan CalculateMaxTimeToDestination(WoWPoint destination, bool includeSafetyMargin = true)
        {
            const double upperLimitOnMaxTime = 10/*mins*/ * 60/*secs*/;

            double distanceToCover = 
                (Me.IsSwimming || Me.IsFlying)
                ? Me.Location.Distance(destination)
                : Me.Location.SurfacePathDistance(destination);

            double myMovementSpeed =
                Me.IsSwimming
                ? Me.MovementInfo.SwimmingForwardSpeed
                : Me.MovementInfo.RunSpeed;

            double timeToDestination = distanceToCover / myMovementSpeed;

            if (includeSafetyMargin)
            {
                timeToDestination = Math.Max(timeToDestination, 20.0);  // 20sec hard lower limit
                timeToDestination *= 2.5;   // factor of safety
            }

            // Place an upper limit on the maximum time to reach the destination...
            // NB: We can get times that are effectively 'infinite' in situations where the Navigator
            // was unable to calculate a path to the target.  This puts an upper limit on such
            // bogus values.
            timeToDestination = Math.Min(timeToDestination, upperLimitOnMaxTime);

            return (TimeSpan.FromSeconds(timeToDestination));            
        }


        // 20Apr2013-12:50UTC chinajade
        public static string GetItemNameFromId(int wowItemId)
        {
            var wowItem = Me.CarriedItems.FirstOrDefault(i => (i.Entry == wowItemId));

            return (wowItem != null)
                ? wowItem.Name
                : string.Format("ItemId({0})", wowItemId);
        }

        
        // 11Apr2013-04:41UTC chinajade
        public static string GetObjectNameFromId(int wowObjectId)
        {
            var wowObject =
                ObjectManager.GetObjectsOfType<WoWObject>(true, false)
                .FirstOrDefault(o => IsViable(o) && (o.Entry == wowObjectId));

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


        // 19Apr2013-05:58UTC chinajade
        public static string GetProfileName()
        {
            return ProfileManager.CurrentOuterProfile.Name ?? "UnknownProfile";
        }


        // 20Apr2013-01:23UTC chinajade
        public static string GetProfileReference(XElement xElement)
        {
            var location =
                ((xElement != null) && ((IXmlLineInfo)xElement).HasLineInfo())
                ? ("@line " + ((IXmlLineInfo)xElement).LineNumber.ToString())
                : "@unknown line";   
 
            return string.Format("[Ref: \"{0}\" {1}]", GetProfileName(), location);
        }


        // 15May2013-11:42UTC chinajade
        public static string GetSpellNameFromId(int spellId)
        {
            SpellFindResults spellInfo; 
            bool isSpellFound = SpellManager.FindSpell(spellId, out spellInfo);

            return
                isSpellFound
                ? spellInfo.Original.Name
                : string.Format("SpellId({0})", spellId);
        }


        // 25Apr2013-11:42UTC chinajade
        public static string GetVersionedBehaviorName(CustomForcedBehavior cfb)
        {
            if (_versionedBehaviorLastCfb == cfb)
                { return _versionedBehaviorName; }

            Func<string, string>    utilStripSubversionDecorations =
                (subversionRevision) =>
                {
                    var regexSvnDecoration = new Regex("^\\$[^:]+:[:]?[ \t]*([^$]+)[ \t]*\\$$");

                    return regexSvnDecoration.Replace(subversionRevision, "$1").Trim();
                };

            var behaviorName = (cfb != null) ? cfb.GetType().Name : "UnknownBehavior";
            var versionNumber = (cfb != null) ? utilStripSubversionDecorations(cfb.SubversionRevision) : "0";

            _versionedBehaviorLastCfb = cfb;
            _versionedBehaviorName = string.Format("{0}-v{1}", behaviorName, versionNumber);

            return _versionedBehaviorName;
        }       
        private static string _versionedBehaviorName;
        private static CustomForcedBehavior _versionedBehaviorLastCfb;        
        

        //  1May2013-07:49UTC chinajade
        public static string GetXmlFileReference(XElement xElement)
        {
            string fileLocation =
                ((xElement == null) || string.IsNullOrEmpty(xElement.BaseUri))
                    ? QuestBehaviorBase.GetProfileName()
                    : xElement.BaseUri;

            var lineLocation =
                ((xElement != null) && ((IXmlLineInfo)xElement).HasLineInfo())
                ? ("@line " + ((IXmlLineInfo)xElement).LineNumber.ToString())
                : "@unknown line";

            return string.Format("[Ref: \"{0}\" {1}]", fileLocation, lineLocation);
        }


        /// <summary>
        /// <para>The Movement observer is a vehicle, if we are in a vehicle.  Or "Me", if we are not.
        /// The observer should be used to make all movement and distance decisions.</para>
        /// <para>The returned value will never be null.</para>
        /// </summary>
        /// <remarks>29Apr2013-09:39UTC chinajade</remarks>
        // A number of quests put us in the bodies of NPCs.  These bodies are frequently implemented
        // as vehicles.  An example of such a quest is "SI:7 Report: Hostile Natives".
        public static WoWUnit MovementObserver
        {
            get { return (Me.Transport as WoWUnit) ?? Me; }
        }


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
            string format = string.Empty;

            if ((int)duration.TotalMilliseconds == 0)
                { return "0s"; }
            else if (duration.Hours > 0)
                { format = "{0}h{1:D2}m{2:D2}s"; }
            else if (duration.Minutes > 0)
                { format = "{1}m{2:D2}s"; }
            else if ((duration.Seconds > 0) && (duration.Milliseconds > 0))
                { format = "{2}.{3:D3}s"; }
            else if (duration.Seconds > 0)
                { format = "{2}s"; }
            else if (duration.Milliseconds > 0)
                { format = "{3}ms"; }

            return string.Format(format, duration.Hours, duration.Minutes, duration.Seconds, duration.Milliseconds);
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
                GetVersionedBehaviorName(this),
                ((quest != null)
                    ? string.Format("\"{0}\" (QuestId: {1})", quest.Name, QuestId)
                    : "In Progress (no associated quest)"),
                (extraGoalTextDescription ?? string.Empty),
                GetProfileReference(Element));            
        }
    }
}