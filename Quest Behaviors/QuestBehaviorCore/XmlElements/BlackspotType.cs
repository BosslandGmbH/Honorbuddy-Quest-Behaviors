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
        #region Constructor and Argument Processing
        public BlackspotType(XElement xElement)
            : base(xElement)
        {
            try
            {
                var height = GetAttributeAsNullable<double>("Height", false, new ConstrainTo.Domain<double>(0.0, 10000.0), null) ?? 1.0;
                var location = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
                var name = GetAttributeAs<string>("Name", false, ConstrainAs.StringNonEmpty, null) ?? string.Empty;
                var radius = GetAttributeAsNullable<double>("Radius", false, ConstrainAs.Range, null) ?? 10.0;

                name = string.IsNullOrEmpty(name) ? GetDefaultName(location) : name;
                _blackspot = new Blackspot(location, (float)radius, (float)height, name);

                HandleAttributeProblem();
            }

            catch (Exception except)
            {
                if (Query.IsExceptionReportingNeeded(except))
                    { QBCLog.Exception(except, "PROFILE PROBLEM with \"{0}\"", xElement.ToString()); }
                IsAttributeProblem = true;
            }
        }

        public BlackspotType(WoWPoint location, string name = "", double radius = 10.0, double height = 1.0)
        {
            name = string.IsNullOrEmpty(name) ? GetDefaultName(location) : name;
            _blackspot = new Blackspot(location, (float) radius, (float) height, name);
        }
        #endregion


        #region Private and Convenience variables
        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return "$Id$"; } }
        public override string SubversionRevision { get { return "$Rev$"; } }

        private readonly Blackspot _blackspot;
        #endregion


        public Blackspot AsBlackspot()
        {
            return _blackspot;
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

            tmp.AppendFormat("<BlackspotType");
            tmp.AppendFormat("{0}Name=\"{1}\"", fieldSeparator, _blackspot.Name);
            tmp.AppendFormat("{0}X=\"{1}\"", fieldSeparator, _blackspot.Location.X);
            tmp.AppendFormat("{0}Y=\"{1}\"", fieldSeparator, _blackspot.Location.Y);
            tmp.AppendFormat("{0}Z=\"{1}\"", fieldSeparator, _blackspot.Location.Z);
            tmp.AppendFormat("{0}Radius=\"{1}\"", fieldSeparator, _blackspot.Radius);
            tmp.AppendFormat("{0}Height=\"{1}\"", fieldSeparator, _blackspot.Height);
            tmp.AppendFormat("{0}/>", fieldSeparator);

            return tmp.ToString();
        }


        private string GetDefaultName(WoWPoint wowPoint)
        {
            return string.Format("Blackspot({0})", wowPoint.ToString());   
        }
    }
}
