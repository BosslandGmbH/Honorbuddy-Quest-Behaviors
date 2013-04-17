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
using System.Threading;
using System.Xml.Linq;

using Styx.CommonBot.Profiles;
using Styx.TreeSharp;


namespace Honorbuddy.QuestBehaviorCore.XmlElements
{
    public abstract class QuestBehaviorXmlBase : CustomForcedBehavior
    {
        protected QuestBehaviorXmlBase(XElement xElement)
            : base(ParseElementAttributes(xElement))
        {
            Element = xElement;
        }


        protected QuestBehaviorXmlBase()
            : base(new Dictionary<string, string>())
        {
            // empty
        }


        private static Dictionary<string, string> ParseElementAttributes(XElement element)
        {
            return element
                .Attributes()
                .ToDictionary(attribute => attribute.Name.ToString(), attribute => attribute.Value);
        }


        #region (No-op) Overrides for CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return new PrioritySelector();
        }

        public override bool IsDone
        {
            get { return false; }
        }

        public override void OnStart()
        {
            /*empty*/
        }

        #endregion
    }
}