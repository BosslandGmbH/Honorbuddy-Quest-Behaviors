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
using System.Text;
using System.Xml.Linq;

using Styx;
using Styx.Pathing;


namespace Honorbuddy.QuestBehaviorCore.XmlElements
{
    public class WaypointType : QuestBehaviorXmlBase
    {
        public const double DefaultRadius = 10.0;

        public WaypointType(XElement xElement)
            : base(xElement)
        {
            try
            {
                Location = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
                Name = GetAttributeAs<string>("Name", false, ConstrainAs.StringNonEmpty, null) ?? string.Empty;
                Radius = GetAttributeAsNullable<double>("Radius", false, ConstrainAs.Range, null) ?? DefaultRadius;

                if (string.IsNullOrEmpty(Name))
                    { Name = GetDefaultName(Location); }

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


        public WaypointType(WoWPoint location, string name = "", double radius = DefaultRadius)
        {
            Location = location;
            Name = name ?? GetDefaultName(location);
            Radius = radius;
        }


        public WoWPoint Location { get; set; }
        public string Name { get; set; }
        public double Radius
        {
            get
            {
                // NB: Since an improperly chosen radius can cause Honorbuddy to stall, we must check for it.
                // HBcore may choose to 'scale' the radius based on our mount speed.  So the static check we
                // did at initialization is insufficient.  Because of this, we must guard against an inadequate
                // radius every time it is used.
                _radius = AdjustedRadius(_radius);
                return _radius;
            }

            set
            {
                _radius = value;    // Must assure baseline is established, before adjustment.
                                    // Otherwise, the error information will be incorrect.
                _radius = AdjustedRadius(value);
            }
        }

        private double _radius;

        const double NavigatorPathPrecisionBuffer = 1.5;

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return "$Id: WaypointType.cs 593 2013-07-08 10:41:44Z chinajade $"; } }
        public override string SubversionRevision { get { return "$Rev: 593 $"; } }


        public override string ToString()
        {
            return ToString_FullInfo(true);
        }


        public string ToString_FullInfo(bool useCompactForm = false, int indentLevel = 0)
        {
            var tmp = new StringBuilder();

            var indent = string.Empty.PadLeft(indentLevel);
            var fieldSeparator = useCompactForm ? " " : string.Format("\n  {0}", indent);

            tmp.AppendFormat("<WaypointType");
            tmp.AppendFormat("{0}Name=\"{1}\"", fieldSeparator, Name);
            tmp.AppendFormat("{0}X=\"{1}\"", fieldSeparator, Location.X);
            tmp.AppendFormat("{0}Y=\"{1}\"", fieldSeparator, Location.Y);
            tmp.AppendFormat("{0}Z=\"{1}\"", fieldSeparator, Location.Z);
            // NB: We must use _radius instead of Radius.  The Radius error checks calls this method.
            tmp.AppendFormat("{0}Radius=\"{1}\"", fieldSeparator, _radius);
            tmp.AppendFormat("{0}/>", fieldSeparator);

            return tmp.ToString();
        }


        private double AdjustedRadius(double proposedRadius)
        {
            var adjustedRadius = proposedRadius;

            if (proposedRadius <= Navigator.PathPrecision)
            {
                adjustedRadius = Navigator.PathPrecision + NavigatorPathPrecisionBuffer;

                QBCLog.Warning("Probematical Waypoint Radius: {1}{0}"
                    + "   The Waypoint Radius ({2}) is less than or equal to the Navigator Path Precision ({3})."
                    + "  This will cause Honorbuddy to get hung."
                    + " We are internally adjusting the Waypoint Radius to {4} to prevent problems.",
                    Environment.NewLine,
                    ToString_FullInfo(true),
                    proposedRadius,
                    Navigator.PathPrecision,
                    adjustedRadius);
            }

            return adjustedRadius;
        }


        private string GetDefaultName(WoWPoint wowPoint)
        {
            return string.Format("Waypoint({0})", wowPoint.ToString());
        }
    }
}
