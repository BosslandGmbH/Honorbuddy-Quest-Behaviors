// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using Styx;
using Styx.Pathing;


namespace Honorbuddy.QuestBehaviorCore.XmlElements
{
    public class HuntingGroundsType : QuestBehaviorXmlBase
    {        
        public enum WaypointVisitStrategyType
        {
            InOrder,
            Random,
        }
        
        public HuntingGroundsType(XElement xElement)
            : base(xElement)
        {
            try
            {
                WaypointVisitStrategy = GetAttributeAsNullable<WaypointVisitStrategyType>("WaypointVisitStrategy", false, null, null) ?? WaypointVisitStrategyType.Random;

                Waypoints = new List<WaypointType>();

                int unnamedWaypointNumber = 0;
                foreach (XElement childElement in xElement.Elements().Where(elem => (elem.Name == "Hotspot")))
                {
                    var waypoint = new WaypointType(childElement);

                    if (!waypoint.IsAttributeProblem)
                    {
                        if (string.IsNullOrEmpty(waypoint.Name))
                            {  waypoint.Name = string.Format("UnnamedWaypoint{0}", ++unnamedWaypointNumber); }
                        Waypoints.Add(waypoint);
                    }

                    IsAttributeProblem |= waypoint.IsAttributeProblem;
                }

                OnStart_HandleAttributeProblem();
            }

            catch (Exception except)
            {
                if (QuestBehaviorBase.IsExceptionReportingNeeded(except))
                {
                    QuestBehaviorBase.LogError("[PROFILE PROBLEM with \"{0}\"]: {1}\nFROM HERE ({2}):\n{3}\n",
                                               xElement.ToString(), except.Message, except.GetType().Name,
                                               except.StackTrace);
                }
                IsAttributeProblem = true;
            }
        }

        public WaypointVisitStrategyType WaypointVisitStrategy { get; set; }
        public List<WaypointType> Waypoints { get; set; }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return "$Id$"; } }
        public override string SubversionRevision { get { return "$Rev$"; } }


        public void AppendWaypoint(WoWPoint wowPoint, string name = "", double radius = 7.0)
        {
            Waypoints.Add(new WaypointType(wowPoint, name, radius));
        }


        public WaypointType FindFirstWaypoint(WoWPoint currentLocation)
        {
            return (WaypointVisitStrategy == WaypointVisitStrategyType.Random)
                ? FindNextWaypoint(currentLocation)
                : FindNearestWaypoint(currentLocation);
        }


        public WaypointType FindNearestWaypoint(WoWPoint currentLocation)
        {
            return
               (from waypoint in Waypoints
                orderby waypoint.Location.Distance(currentLocation)
                select waypoint)
                .FirstOrDefault();
        }


        public WaypointType FindNextWaypoint(WoWPoint currentLocation)
        {
            if (WaypointVisitStrategy == WaypointVisitStrategyType.Random)
            {
                return
                    (from waypoint in Waypoints
                    orderby QuestBehaviorBase._random.Next()
                    select waypoint)
                    .FirstOrDefault();
            }

            // If we haven't reached the nearest waypoint yet, use it...
            var nearestWaypoint = FindNearestWaypoint(currentLocation);
            if (nearestWaypoint.Location.Distance(currentLocation) > nearestWaypoint.Radius)
                { return nearestWaypoint; }

            var queue = new Queue<WaypointType>(Waypoints);
            WaypointType tmpWaypoint;

            // Rotate the queue so the nearest waypoint is on the front...
            while (nearestWaypoint != queue.Peek())
            {
                tmpWaypoint = queue.Dequeue();
                queue.Enqueue(tmpWaypoint);
            }

            // Rotate one more time to get the 'next' waypoint...
            // NB: We can't simply Dequeue to access the 'next' waypoint,
            // because we must take into consideration that the queue may only
            // contain one point.
            tmpWaypoint = queue.Dequeue();
            queue.Enqueue(tmpWaypoint);

            return (queue.Peek());
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
                    { huntingGrounds.AppendWaypoint(defaultHuntingGroundCenter.Value, "hunting ground center", Navigator.PathPrecision); }

                if (!huntingGrounds.Waypoints.Any())
                {
                    QuestBehaviorBase.LogError("Neither the X/Y/Z attributes nor the <{0}> sub-element has been specified.", elementName);
                    huntingGrounds.IsAttributeProblem = true;
                }
            }

            return huntingGrounds;
        }
        
        
        public override string ToString()
        {
            return ToString_FullInfo(true);
        }


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
    }
}
