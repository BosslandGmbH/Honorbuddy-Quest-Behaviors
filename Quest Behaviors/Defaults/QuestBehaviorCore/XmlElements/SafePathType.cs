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
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;


namespace Honorbuddy.QuestBehaviorCore.XmlElements
{
    public class SafePathType : QuestBehaviorXmlBase
    {
        public enum StrategyType
        {
            WaitForAvoidDistance,
            StalkMobAtAvoidDistance
        }

        public SafePathType(XElement xElement, double defaultEgressDistance)
            : base(xElement)
        {
            try
            {
                DismissPet = GetAttributeAsNullable<bool>("DismissPet", false, null, null) ?? false;
                EgressDistance = GetAttributeAsNullable<double>("EgressDistance", false, null, null)
                                    ?? defaultEgressDistance;
                Strategy = GetAttributeAsNullable<StrategyType>("Strategy", false, null, null)
                            ?? StrategyType.StalkMobAtAvoidDistance;

                Waypoints = new List<WaypointType>();
                foreach (XElement childElement in xElement.Elements().Where(elem => (elem.Name == "Hotspot")))
                {
                    var waypoint = new WaypointType(childElement);

                    if (!waypoint.IsAttributeProblem)
                        { Waypoints.Add(waypoint); }

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


        public bool DismissPet { get; private set; }
        public double EgressDistance { get; private set; }
        public StrategyType Strategy { get; private set; }
        public IList<WaypointType> Waypoints { get; private set; }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return "$Id$"; } }
        public override string SubversionRevision { get { return "$Rev$"; } }


        public void AppendWaypoint(WoWPoint wowPoint, string name = "", double radius = 7.0)
        {
            Waypoints.Add(new WaypointType(wowPoint, name, radius));
        }
        
        
        public void DismissPetIfNeeded()
        {
            if (DismissPet && StyxWoW.Me.GotAlivePet)
                { Lua.DoString("DismissPet()"); }
        }
        

        public Queue<WaypointType> FindPath_Egress(WoWUnit mobToAvoid)
        {
            var theWayOut = new List<WaypointType>(Waypoints);

            theWayOut.Reverse();

            WaypointType egressStartPoint =
                (from waypoint in theWayOut
                 let mobDistanceToPoint = (mobToAvoid != null)
                                              ? waypoint.Location.Distance(mobToAvoid.Location)
                                              : float.MaxValue
                let myDistanceToPoint = waypoint.Location.Distance(StyxWoW.Me.Location)
                where
                    myDistanceToPoint < mobDistanceToPoint
                orderby
                    myDistanceToPoint
                select waypoint)
                .FirstOrDefault();

            while (theWayOut[0] != egressStartPoint)
                { theWayOut.RemoveAt(0); }

            return new Queue<WaypointType>(theWayOut);
        }


        public Queue<WaypointType> FindPath_Ingress()
        {
            var theWayIn = new List<WaypointType>(Waypoints);

            WaypointType ingressStartPoint =
               (from waypoint in theWayIn
                let myDistanceToPoint = waypoint.Location.Distance(StyxWoW.Me.Location)
                orderby
                    myDistanceToPoint
                select waypoint)
                .FirstOrDefault();

            while (theWayIn[0] != ingressStartPoint)
                { theWayIn.RemoveAt(0); }

            return new Queue<WaypointType>(theWayIn);
        }


        // 11Apr2013-04:42UTC chinajade
        public static SafePathType GetOrCreate(XElement parentElement, string elementName, double defaultEgressDistance, WoWPoint? safespotLocation = null)
        {
            if (safespotLocation.HasValue
                && ((safespotLocation.Value == WoWPoint.Empty) || safespotLocation.Value == WoWPoint.Zero))
            {
                safespotLocation = null;
            }

            var safePath = new SafePathType(parentElement
                                                .Elements()
                                                .DefaultIfEmpty(new XElement(elementName))
                                                .FirstOrDefault(elem => (elem.Name == elementName)),
                                              defaultEgressDistance);

            if (!safePath.IsAttributeProblem)
            {
                // If user didn't provide a HuntingGrounds, and he provided a default center point, add it...
                if (!safePath.Waypoints.Any() && safespotLocation.HasValue)
                    { safePath.AppendWaypoint(safespotLocation.Value, "safe spot", Navigator.PathPrecision); }

                if (!safePath.Waypoints.Any())
                {
                    QuestBehaviorBase.LogError("Neither the X/Y/Z attributes nor the <{0}> sub-element has been specified.",
                                elementName);
                    safePath.IsAttributeProblem = true;
                }
            }

            return safePath;
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

            tmp.AppendFormat("{0}DismissPet=\"{1}\"", fieldSeparator, DismissPet);
            tmp.AppendFormat("{0}EgressDistance=\"{1}\"", fieldSeparator, EgressDistance);
            tmp.AppendFormat("{0}Strategy=\"{1}\"", fieldSeparator, Strategy);
            foreach (var waypoint in Waypoints)
                { tmp.AppendFormat("{0}  {1}", fieldSeparator, waypoint.ToString_FullInfo(useCompactForm, indentLevel+4)); }
            tmp.AppendFormat("{0}/>", fieldSeparator);

            return tmp.ToString();
        }
    }
}
