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



namespace Honorbuddy.QuestBehaviorCore.XmlElements
{
    public class SpellType : QuestBehaviorXmlBase
    {        
        public SpellType(XElement xElement)
            : base(xElement)
        {
            try
            {
                Name = GetAttributeAs<string>("Name", false, ConstrainAs.StringNonEmpty, null) ?? string.Empty;
                SpellId = GetAttributeAsNullable<int>("SpellId", true, ConstrainAs.SpellId, null) ?? 0;

                if (string.IsNullOrEmpty(Name))
                    { Name = GetDefaultName(SpellId); }

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

        public SpellType(int spellId, string name = null)
        {
            Name = name ?? GetDefaultName(SpellId);
            SpellId = spellId;
        }

        public string Name { get; set; }
        public int SpellId { get; set; }

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

            tmp.AppendFormat("<Spell");
            tmp.AppendFormat("{0}SpellId=\"{1}\"", fieldSeparator, SpellId);
            tmp.AppendFormat("{0}Name=\"{1}\"", fieldSeparator, Name);
            tmp.AppendFormat("{0}/>", fieldSeparator);

            return tmp.ToString();
        }


        private string GetDefaultName(int spellId)
        {
            return string.Format("SpellId({0})", spellId);
        }
    }
}
