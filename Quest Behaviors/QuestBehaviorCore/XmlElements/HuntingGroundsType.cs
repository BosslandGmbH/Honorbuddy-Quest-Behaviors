﻿// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.


#region Summary and Documentation
//      HuntingGrounds [optional; Default: none]
//          The HuntingGrounds contains a set of Waypoints we will visit to seek mobs
//          that fulfill the quest goal.  The <HuntingGrounds> element accepts the following
//          attributes:
//              WaypointVisitStrategy= [optional; Default: Random]
//              [Allowed values: InOrder, PickOneAtRandom, Random]
//              Determines the strategy that should be employed to visit each waypoint.
//              Any mobs encountered while traveling between waypoints will be considered
//              viable.  The Random strategy is highly recommended unless there is a compelling
//              reason to otherwise.  The Random strategy 'spread the toons out', if
//              multiple bos are running the same quest.
//              The PickOneAtRandom strategy will only visit one waypoint on the list
//              and camp the mobs from the single selected waypoint.  This is another good tactic
//              for spreading toons out in heavily populated areas.
//          Each Waypoint is provided by a <Hotspot ... /> element with the following
//          attributes:
//              Name [optional; Default: X/Y/Z location of the waypoint]
//                  The name of the waypoint is presented to the user as it is visited.
//                  This can be useful for debugging purposes, and for making minor adjustments
//                  (you know which waypoint to be fiddling with).
//              X/Y/Z [REQUIRED; Default: none]
//                  The world coordinates of the waypoint.
//              Radius [optional; Default: 10.0]
//                  Once the toon gets within Radius of the waypoint, the next waypoint
//                  will be sought.
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using Styx;


namespace Honorbuddy.QuestBehaviorCore.XmlElements
{
    public class HuntingGroundsType : QuestBehaviorXmlBase
    {        
        #region Constructor and Argument Processing
        public enum WaypointVisitStrategyType
        {
            InOrder,
            PickOneAtRandom,
            Random,
        }
        
        // 22Mar2013-11:49UTC chinajade
        public HuntingGroundsType(XElement xElement)
            : base(xElement)
        {
            try
            {
                // Acquire the visit strategy...
                WaypointVisitStrategy = GetAttributeAsNullable<WaypointVisitStrategyType>("WaypointVisitStrategy", false, null, null) ?? WaypointVisitStrategyType.Random;
                if (WaypointVisitStrategy == WaypointVisitStrategyType.InOrder)
                    { _visitStrategy = new VisitStrategy_InOrder(this); }
                else if (WaypointVisitStrategy == WaypointVisitStrategyType.PickOneAtRandom)
                    { _visitStrategy = new VisitStrategy_PickOneAtRandom(this); }
                else if (WaypointVisitStrategy == WaypointVisitStrategyType.Random)
                    { _visitStrategy = new VisitStrategy_Random(this); }
                else
                {
                    QBCLog.MaintenanceError("Unhandled WaypointVisitStrategy({0})", WaypointVisitStrategy);
                }

                // Acquire the waypoints...
                Waypoints = new List<WaypointType>();
                if (xElement != null)
                {
                    var waypointElementsQuery =
                        from element in xElement.Elements()
                        where
                            (element.Name == "Hotspot")
                            || (element.Name == "Waypoint")
                            || (element.Name == "WaypointType")
                        select element;

                    foreach (XElement childElement in waypointElementsQuery)
                    {
                        var waypoint = new WaypointType(childElement);

                        if (!waypoint.IsAttributeProblem)
                        { Waypoints.Add(waypoint); }

                        IsAttributeProblem |= waypoint.IsAttributeProblem;
                    }
                }

                HandleAttributeProblem();
            }

            catch (Exception except)
            {
                if (Query.IsExceptionReportingNeeded(except))
                {
                    QBCLog.Error("[PROFILE PROBLEM with \"{0}\"]: {1}\nFROM HERE ({2}):\n{3}\n",
                        xElement.ToString(), except.Message, except.GetType().Name,
                        except.StackTrace);
                }
                IsAttributeProblem = true;
            }
        }

        public WaypointVisitStrategyType WaypointVisitStrategy { get; set; }
        public List<WaypointType> Waypoints { get; set; }
        #endregion


        #region Private and Convenience variables

        // NB: The "initial position waypoint" is special.
        // It is only used if no other waypoints have been defined.
        private readonly WaypointType _initialPositionWaypoint = new WaypointType(StyxWoW.Me.Location, "my initial position");
        private int _indexOfCurrentWaypoint = IVisitStrategy.InvalidWaypointIndex;
        private readonly IVisitStrategy _visitStrategy;

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return "$Id: HuntingGroundsType.cs 712 2013-07-25 15:25:19Z dogan $"; } }
        public override string SubversionRevision { get { return "$Rev: 712 $"; } }
        #endregion


        // 22Mar2013-11:49UTC chinajade
        public void AppendWaypoint(WoWPoint wowPoint, string name = "", double radius = WaypointType.DefaultRadius)
        {
            Waypoints.Add(new WaypointType(wowPoint, name, radius));
        }


        // 22Apr2013-12:50UTC chinajade
        public WaypointType CurrentWaypoint(WoWPoint? currentLocation = null)
        {
            currentLocation = currentLocation ?? StyxWoW.Me.Location;

            // If we haven't initialized current waypoint yet, find first waypoint...
            if (_indexOfCurrentWaypoint == IVisitStrategy.InvalidWaypointIndex)
                { _indexOfCurrentWaypoint = _visitStrategy.FindIndexOfNextWaypoint(); }

            // If we haven't arrived at the current waypoint, still use it...
            var currentWaypoint = FindWaypointAtIndex(_indexOfCurrentWaypoint);
            if (currentLocation.Value.Distance(currentWaypoint.Location) >= currentWaypoint.Radius)
                { return currentWaypoint; }

            // Otherwise, find next waypoint index, and return new waypoint...
            _indexOfCurrentWaypoint = _visitStrategy.FindIndexOfNextWaypoint(_indexOfCurrentWaypoint);
            return FindWaypointAtIndex(_indexOfCurrentWaypoint);
        }


        private WaypointType FindWaypointAtIndex(int index)
        {
            return (index == IVisitStrategy.InvalidWaypointIndex)
                ? _initialPositionWaypoint
                : Waypoints[index];
        }


       // 22Mar2013-11:49UTC chinajade
        private int FindIndexOfNearestWaypoint(WoWPoint location)
        {
            var indexOfNearestWaypoint = IVisitStrategy.InvalidWaypointIndex;
            var distanceSqrMin = double.MaxValue;

            for (var index = 0; index < Waypoints.Count; ++index)
            {
                var distanceSqr = location.DistanceSqr(FindWaypointAtIndex(index).Location);

                if (distanceSqr < distanceSqrMin)
                {
                    distanceSqrMin = distanceSqr;
                    indexOfNearestWaypoint = index;
                }
            }

            return indexOfNearestWaypoint;
        }


        // 11Apr2013-04:42UTC chinajade
        public static HuntingGroundsType GetOrCreate(XElement parentElement, string elementName, WoWPoint? defaultHuntingGroundCenter = null)
        {
            var huntingGrounds = new HuntingGroundsType(parentElement
                                                    .Elements()
                                                    .DefaultIfEmpty(new XElement(elementName))
                                                    .FirstOrDefault(elem => (elem.Name == elementName)));

            if (!huntingGrounds.IsAttributeProblem)
            {
                // If user didn't provide a HuntingGrounds, and he provided a default center point, add it...
                if (!huntingGrounds.Waypoints.Any() && defaultHuntingGroundCenter.HasValue)
                    { huntingGrounds.AppendWaypoint(defaultHuntingGroundCenter.Value, "hunting ground center"); }

                if (!huntingGrounds.Waypoints.Any())
                {
                    QBCLog.Error("Neither the X/Y/Z attributes nor the <{0}> sub-element has been specified.", elementName);
                    huntingGrounds.IsAttributeProblem = true;
                }
            }

            return huntingGrounds;
        }


        // We must support 'implied containers' for backward compatibility purposes.
        // An 'implied container' is just a list of <Hotspot> without the <HuntingGrounds> container.  As such, we use defaults.
        // This method will first look for the 'new style' (with container), and if that fails, looks for just a list of hotspots with
        // which we can construct the object.
        // 22Apr2013-10:29UTC chinajade
        public static HuntingGroundsType GetOrCreate_ImpliedContainer(XElement parentElement, string elementName, WoWPoint? defaultHuntingGroundCenter = null)
        {
            var huntingGrounds = GetOrCreate(parentElement, elementName, defaultHuntingGroundCenter);

            // If 'new form' succeeded, we're done...
            if (!huntingGrounds.IsAttributeProblem)
                { return huntingGrounds; }

            // 'Old form' we have to dig out the Hotspots manually...
            huntingGrounds = new HuntingGroundsType(new XElement(elementName));
            if (!huntingGrounds.IsAttributeProblem)
            {
                var waypoints = new List<WaypointType>();

                int unnamedWaypointNumber = 0;
                foreach (XElement childElement in parentElement.Elements().Where(elem => (elem.Name == "Hotspot")))
                {
                    var waypoint = new WaypointType(childElement);

                    if (!waypoint.IsAttributeProblem)
                    {
                        if (string.IsNullOrEmpty(waypoint.Name))
                            {  waypoint.Name = string.Format("UnnamedWaypoint{0}", ++unnamedWaypointNumber); }
                        waypoints.Add(waypoint);
                    }

                    huntingGrounds.IsAttributeProblem |= waypoint.IsAttributeProblem;
                }                
            }

            return huntingGrounds;
        }
        
        
        public override string ToString()
        {
            return ToString_FullInfo(true);
        }


        // 22Mar2013-11:49UTC chinajade
        public string ToString_FullInfo(bool useCompactForm = false, int indentLevel = 0)
        {
            var tmp = new StringBuilder();

            var indent = string.Empty.PadLeft(indentLevel);
            var fieldSeparator = useCompactForm ? " " : string.Format("\n  {0}", indent);

            tmp.AppendFormat("<HuntingGroundType");
            tmp.AppendFormat("{0}WaypointVisitStrategy=\"{1}\"", fieldSeparator, WaypointVisitStrategy);
            foreach (var waypoint in Waypoints)
                { tmp.AppendFormat("{0}  {1}", fieldSeparator, waypoint.ToString_FullInfo()); }
            tmp.AppendFormat("{0}/>", fieldSeparator);

            return tmp.ToString();
        }


        #region Visit Strategies
        private abstract class IVisitStrategy
        {
            public const int InvalidWaypointIndex = -1;

            public abstract int FindIndexOfNextWaypoint(int currentWaypointIndex = InvalidWaypointIndex);


            protected IVisitStrategy(HuntingGroundsType huntingGrounds)
            {
                HuntingGrounds = huntingGrounds;
            }

            protected HuntingGroundsType HuntingGrounds { get; private set; }
        }


        private class VisitStrategy_InOrder : IVisitStrategy
        {
            public VisitStrategy_InOrder(HuntingGroundsType huntingGrounds)
                : base(huntingGrounds)
            {
                // empty
            }


            public override int FindIndexOfNextWaypoint(int currentWaypointIndex = InvalidWaypointIndex)
            {
                // Current waypoint index is invalid?
                if (currentWaypointIndex == InvalidWaypointIndex)
                {
                    // If no waypoints defined, then nothing to choose from...
                    if (HuntingGrounds.Waypoints.Count <= 0)
                        { return InvalidWaypointIndex; }

                    // Pick initial waypoint--the nearest one from the list of available waypoints...
                    return HuntingGrounds.FindIndexOfNearestWaypoint(StyxWoW.Me.Location);
                }

                // Waypoint is simply next one in the list, and wrap around if we've reached the end...
                ++currentWaypointIndex;
                if (currentWaypointIndex >= HuntingGrounds.Waypoints.Count)
                    { currentWaypointIndex = 0; }

                return currentWaypointIndex;
            }
        }


        private class VisitStrategy_PickOneAtRandom : IVisitStrategy
        {
            public VisitStrategy_PickOneAtRandom(HuntingGroundsType huntingGrounds)
                : base(huntingGrounds)
            {
                // empty
            }

            public override int FindIndexOfNextWaypoint(int currentWaypointIndex = InvalidWaypointIndex)
            {
                // Current waypoint index is invalid?
                if (currentWaypointIndex == InvalidWaypointIndex)
                {
                    // If no waypoints defined, then nothing to choose from...
                    if (HuntingGrounds.Waypoints.Count <= 0)
                        { return InvalidWaypointIndex; }

                    // Pick initial waypoint--a random waypoint from those available on the list...
                    return QuestBehaviorBase._random.Next(0, HuntingGrounds.Waypoints.Count);
                }

                // Once waypoint is selected, we continue to use it...
                return currentWaypointIndex;
            }
        }


        private class VisitStrategy_Random : IVisitStrategy
        {
            public VisitStrategy_Random(HuntingGroundsType huntingGrounds)
                : base(huntingGrounds)
            {
                // empty
            }


            public override int FindIndexOfNextWaypoint(int currentWaypointIndex = InvalidWaypointIndex)
            {
                // Current waypoint index is invalid?
                if (currentWaypointIndex == InvalidWaypointIndex)
                {
                    // If no waypoints defined, then nothing to choose from...
                    if (HuntingGrounds.Waypoints.Count <= 0)
                        { return InvalidWaypointIndex; }

                    // Pick initial waypoint--fall through to pick initial waypoint...
                }

                // Determine 'next' waypoint based on visit strategy...
                // NB: If we have more than one point to select from, make certain we don't re-select
                // the current point.
                int newWaypointIndex;
                do
                {
                    newWaypointIndex = QuestBehaviorBase._random.Next(0, HuntingGrounds.Waypoints.Count);
                } while ((HuntingGrounds.Waypoints.Count > 1) && (currentWaypointIndex == newWaypointIndex));

                return newWaypointIndex;
            }
        }


        #endregion
    }
}
