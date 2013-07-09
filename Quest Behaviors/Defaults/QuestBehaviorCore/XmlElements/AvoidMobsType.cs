﻿// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
#endregion



namespace Honorbuddy.QuestBehaviorCore.XmlElements
{
    public class AvoidMobsType : QuestBehaviorXmlBase
    {        
        #region Constructor and Argument Processing
        // 30Jun2013-11:49UTC chinajade
        public AvoidMobsType(XElement xElement)
            : base(xElement)
        {
            try
            {
                AvoidMobs = new List<MobType>();

                if (xElement != null)
                {
                    var mobElementsQuery =
                        from element in xElement.Elements()
                        where
                            (element.Name == "Mob")
                            || (element.Name == "MobType")
                        select element;

                    foreach (XElement childElement in mobElementsQuery)
                    {
                        var avoidMob = new MobType(childElement);

                        if (!avoidMob.IsAttributeProblem)
                            { AvoidMobs.Add(avoidMob); }

                        IsAttributeProblem |= avoidMob.IsAttributeProblem;
                    }
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

        
        public List<MobType> AvoidMobs { get; set; }
        #endregion


        #region Private and Convenience variables

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return "$Id: Copy of HuntingGroundsType.cs 555 2013-06-12 09:00:14Z chinajade $"; } }
        public override string SubversionRevision { get { return "$Rev: 555 $"; } }
        #endregion


        public List<uint> GetAvoidMobIds()
        {
            return
               (from avoidMob in AvoidMobs
                select (uint)avoidMob.Entry)
                .ToList();
        }


        // 11Apr2013-04:42UTC chinajade
        public static AvoidMobsType GetOrCreate(XElement parentElement, string elementName)
        {
            var avoidMobsType = new AvoidMobsType(parentElement
                                                .Elements()
                                                .DefaultIfEmpty(new XElement(elementName))
                                                .FirstOrDefault(elem => (elem.Name == elementName)));

            return avoidMobsType;
        }
        
        
        public override string ToString()
        {
            return ToString_FullInfo(true);
        }


        // 22Mar2013-11:49UTC chinajade
        public string ToString_FullInfo(bool useCompactForm = false, int indentLevel = 0)
        {
            var elementTag = "AvoidMobsType";
            var tmp = new StringBuilder();

            var indent = string.Empty.PadLeft(indentLevel);
            var fieldSeparator = useCompactForm ? " " : string.Format("\n  {0}", indent);

            tmp.AppendFormat("<{0}>{1}", elementTag, Environment.NewLine);
            foreach (var avoidMob in AvoidMobs)
                { tmp.AppendFormat("{0}  {1}{2}", fieldSeparator, avoidMob.ToString_FullInfo(true), Environment.NewLine); }
            tmp.AppendFormat("{0}</{1}>", fieldSeparator, elementTag);

            return tmp.ToString();
        }
    }
}
