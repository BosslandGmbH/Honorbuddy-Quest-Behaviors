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
    public class MobType : QuestBehaviorXmlBase
    {
        #region Constructor and Argument Processing
        public MobType(XElement xElement)
            : base(xElement)
        {
            try
            {
                Name = GetAttributeAs<string>("Name", false, ConstrainAs.StringNonEmpty, null) ?? string.Empty;
                Entry = GetAttributeAsNullable<int>("Entry", false, ConstrainAs.MobId, null)
                    ?? GetAttributeAsNullable<int>("Id", false, ConstrainAs.MobId, null)
                    ?? 0;

                if (Entry == 0)
                {
                    QBCLog.Error(QBCLog.BuildMessageWithContext(Element,
                        "Attribute '{1}' is required, but was not provided.",
                        Environment.NewLine,
                        "Entry"));
                    IsAttributeProblem = true;
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

        public string Name { get; set; }
        public int Entry { get; set; }
        #endregion


        #region Private and Convenience variables

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return "$Id: Copy of WaypointType.cs 555 2013-06-12 09:00:14Z chinajade $"; } }
        public override string SubversionRevision { get { return "$Rev: 555 $"; } }
        #endregion


        public override string ToString()
        {
            return ToString_FullInfo(true);
        }


        public string ToString_FullInfo(bool useCompactForm = false, int indentLevel = 0)
        {
            var tmp = new StringBuilder();

            var indent = string.Empty.PadLeft(indentLevel);
            var fieldSeparator = useCompactForm ? " " : string.Format("\n  {0}", indent);

            tmp.AppendFormat("<MobType");
            tmp.AppendFormat("{0}Name=\"{1}\"", fieldSeparator, Name);
            tmp.AppendFormat("{0}Id=\"{1}\"", fieldSeparator, Entry);
            tmp.AppendFormat("{0}/>", fieldSeparator);

            return tmp.ToString();
        }
    }
}
