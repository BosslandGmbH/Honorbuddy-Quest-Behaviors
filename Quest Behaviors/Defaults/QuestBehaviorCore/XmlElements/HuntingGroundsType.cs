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
        #region Consructor and Argument Processing
        public enum WaypointVisitStrategyType
        {
            InOrder,
            Random,
        }
        
        // 22Mar2013-11:49UTC chinajade
        public HuntingGroundsType(XElement xElement)
            : base(xElement)
        {
            try
            {
                WaypointVisitStrategy = GetAttributeAsNullable<WaypointVisitStrategyType>("WaypointVisitStrategy", false, null, null) ?? WaypointVisitStrategyType.Random;

                Waypoints = new List<WaypointType>();

                foreach (XElement childElement in xElement.Elements().Where(elem => (elem.Name == "Hotspot")))
                {
                    var waypoint = new WaypointType(childElement);

                    if (!waypoint.IsAttributeProblem)
                        { Waypoints.Add(waypoint); }

                    IsAttributeProblem |= waypoint.IsAttributeProblem;
                }

                HandleAttributeProblem();
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
        #endregion

        #region Private and Convenience variables
        private WaypointType _currentWaypoint;
        private readonly WaypointType _initialPositionWaypoint = new WaypointType(StyxWoW.Me.Location, "my initial position");
        #endregion


        // 22Mar2013-11:49UTC chinajade
        public void AppendWaypoint(WoWPoint wowPoint, string name = "", double radius = 7.0)
        {
            Waypoints.Add(new WaypointType(wowPoint, name, radius));
        }


        // 22Apr2013-12:50UTC chinajade
        public WaypointType CurrentWaypoint(WoWPoint? currentLocation = null)
        {
            currentLocation = currentLocation ?? StyxWoW.Me.Location;

            // If no current waypoint, initialize it...
            if (_currentWaypoint == null)
                { _currentWaypoint = FindFirstWaypoint(currentLocation.Value); }

            // If we're within range of the current waypoint, find the next one...
            if (currentLocation.Value.Distance(_currentWaypoint.Location) <= _currentWaypoint.Radius)
                { _currentWaypoint = FindNextWaypoint(_currentWaypoint.Location); }

            return _currentWaypoint;
        }


        // 22Mar2013-11:49UTC chinajade
        public WaypointType FindFirstWaypoint(WoWPoint currentLocation)
        {
            return (WaypointVisitStrategy == WaypointVisitStrategyType.Random)
                ? FindNextWaypoint(currentLocation)
                : FindNearestWaypoint(currentLocation);
        }


        // 22Mar2013-11:49UTC chinajade
        public WaypointType FindNearestWaypoint(WoWPoint currentLocation)
        {
            // If no waypoints, our initial position is all we have...
            if (!Waypoints.Any())
                { return _initialPositionWaypoint; }

            bool isMeFlyingOrSwimming = StyxWoW.Me.IsFlying || StyxWoW.Me.IsSwimming;
            
            return
               (from waypoint in Waypoints
                orderby
                   (isMeFlyingOrSwimming
                        ? waypoint.Location.Distance(currentLocation)
                        : waypoint.Location.SurfacePathDistance(currentLocation))
                select waypoint)
                .FirstOrDefault();
        }


        // 22Mar2013-11:49UTC chinajade
        public WaypointType FindNextWaypoint(WoWPoint currentLocation)
        {
            // If no waypoints, our initial position is all we have...
            if (!Waypoints.Any())
                { return _initialPositionWaypoint; }

            if (WaypointVisitStrategy == WaypointVisitStrategyType.Random)
            {
                // NB: If this selects the same waypoint as the current one,
                // the calling code will just return here again until we get
                // something suitable.  If there is just one waypoint on the list,
                // its the best that can be done.  We can't weed out the 'current waypoint'
                // with a 'where' clause, because that would return nothing if there
                // was only one point on the list.
                return
                   (from waypoint in Waypoints
                    orderby QuestBehaviorBase._random.Next()
                    select waypoint)
                    .FirstOrDefault();
            }

            // If we haven't reached the nearest waypoint yet, use it...
            var nearestWaypoint = FindNearestWaypoint(currentLocation);

            double distanceToWaypoint =
                (StyxWoW.Me.IsFlying || StyxWoW.Me.IsSwimming)
                    ? nearestWaypoint.Location.Distance(currentLocation)
                    : nearestWaypoint.Location.SurfacePathDistance(currentLocation);

            if (distanceToWaypoint > nearestWaypoint.Radius)
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
    }
}
