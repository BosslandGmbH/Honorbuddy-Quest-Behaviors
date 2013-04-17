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


namespace Honorbuddy.QuestBehaviorCore.XmlElements
{
    public class WaypointType : QuestBehaviorXmlBase
    {        
        public WaypointType(XElement xElement)
            : base(xElement)
        {
            try
            {
                Location = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
                Name = GetAttributeAs<string>("Name", false, ConstrainAs.StringNonEmpty, null) ?? string.Empty;
                Radius = GetAttributeAsNullable<double>("Radius", false, ConstrainAs.Range, null) ?? 10.0;

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

        public WaypointType(WoWPoint location, string name = "", double radius = 10.0)
        {
            Location = location;
            Name = name ?? string.Empty;
            Radius = radius;
        }

        public WoWPoint Location { get; set; }
        public string Name { get; set; }
        public double Radius { get; set; }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return "$Id$"; } }
        public override string SubversionRevision { get { return "$Rev$"; } }


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
            tmp.AppendFormat("{0}Location=\"{1}\"", fieldSeparator, Location);
            tmp.AppendFormat("{0}Name=\"{1}\"", fieldSeparator, Name);
            tmp.AppendFormat("{0}Radius=\"{1}\"", fieldSeparator, Radius);
            tmp.AppendFormat("{0}/>", fieldSeparator);

            return tmp.ToString();
        }
    }
}
