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
using Styx.CommonBot.Profiles;


namespace Honorbuddy.QuestBehaviorCore.XmlElements
{
    public class BlackspotType : QuestBehaviorXmlBase
    {        
        public BlackspotType(XElement xElement)
            : base(xElement)
        {
            try
            {
                Height = GetAttributeAsNullable<double>("Height", false, new ConstrainTo.Domain<double>(0.0, 10000.0), null) ?? 1.0;
                Location = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
                Name = GetAttributeAs<string>("Name", false, ConstrainAs.StringNonEmpty, null) ?? string.Empty;
                Radius = GetAttributeAsNullable<double>("Radius", false, ConstrainAs.Range, null) ?? 10.0;

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

        public BlackspotType(WoWPoint location, string name = "", double radius = 10.0, double height = 1.0)
        {
            Height = height;
            Location = location;
            Name = name ?? GetDefaultName(location);
            Radius = radius;
        }

        public double Height { get; set; }
        public WoWPoint Location { get; set; }
        public string Name { get; set; }
        public double Radius { get; set; }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return "$Id: Copy of WaypointType.cs 555 2013-06-12 09:00:14Z chinajade $"; } }
        public override string SubversionRevision { get { return "$Rev: 555 $"; } }


        public override string ToString()
        {
            return ToString_FullInfo(true);
        }


        public string ToString_FullInfo(bool useCompactForm = false, int indentLevel = 0)
        {
            var tmp = new StringBuilder();

            var indent = string.Empty.PadLeft(indentLevel);
            var fieldSeparator = useCompactForm ? " " : string.Format("\n  {0}", indent);

            tmp.AppendFormat("<BlackspotType");
            tmp.AppendFormat("{0}Location=\"{1}\"", fieldSeparator, Location);
            tmp.AppendFormat("{0}Name=\"{1}\"", fieldSeparator, Name);
            tmp.AppendFormat("{0}Radius=\"{1}\"", fieldSeparator, Radius);
            tmp.AppendFormat("{0}Height=\"{1}\"", fieldSeparator, Height);
            tmp.AppendFormat("{0}/>", fieldSeparator);

            return tmp.ToString();
        }


        private Blackspot AsBlackspot()
        {
            return new Blackspot(Location, (float)Radius, (float)Height);
        }


        private string GetDefaultName(WoWPoint wowPoint)
        {
            return string.Format("Blackspot({0})", wowPoint.ToString());   
        }
    }
}
