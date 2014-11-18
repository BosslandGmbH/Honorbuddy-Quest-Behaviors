// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Summary and Documentation
// Documentation is in QuestBehaviorBase
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Styx.CommonBot.Profiles;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

#endregion


namespace Honorbuddy.QuestBehaviorCore.XmlElements
{
    public class PursuitListType : QuestBehaviorXmlBase
    {        
        #region Constructor and Argument Processing
        public PursuitListType(XElement xElement)
            : base(xElement)
        {
            try
            {
                PursueObjects = new List<PursueObjectTypeBase>();

                if (xElement != null)
                {
                    var pursueObjectElementsQuery =
                        from element in xElement.Elements()
                        where
                            (element.Name == "PursueObject")
                            || (element.Name == "PursueUnit")
                            || (element.Name == "PursueGameObject")
                            || (element.Name == "PursueSelf")
                        select element;

                    foreach (XElement childElement in pursueObjectElementsQuery)
                    {
                        PursueObjectTypeBase pursueObj;
                        if (childElement.Name == "PursueObject")
                            pursueObj = new PursueObjectType<WoWObject>(childElement);
                        else if (childElement.Name == "PursueUnit")
                            pursueObj = new PursueObjectType<WoWUnit>(childElement);
                        else if (childElement.Name == "PursueGameObject")
                            pursueObj = new PursueObjectType<WoWGameObject>(childElement);
                        else if (childElement.Name == "PursueSelf")
                            pursueObj = new PursueObjectType<LocalPlayer>(childElement);
                        else
                            throw new InvalidDataException(string.Format("{0} is not a recognized type", childElement.Name));

                        if (!pursueObj.IsAttributeProblem)
                            PursueObjects.Add(pursueObj);

                        IsAttributeProblem |= pursueObj.IsAttributeProblem;
                    }
                }

                HandleAttributeProblem();
            }

            catch (Exception except)
            {
                if (Query.IsExceptionReportingNeeded(except))
                    QBCLog.Exception(except, "PROFILE PROBLEM with \"{0}\"", xElement.ToString());
                IsAttributeProblem = true;
            }
        }

        public PursuitListType()
        {
            PursueObjects = new List<PursueObjectTypeBase>();
        }

        #endregion

        public List<PursueObjectTypeBase> PursueObjects { get; set; }

        public bool ShouldPursue(WoWObject woWObject)
        {
            return PursueObjects.Any(p => p.ShouldPursue(woWObject));
        }

        public bool ShouldPursue(WoWObject woWObject, out float priority)
        {
            var pursueObject =  PursueObjects.FirstOrDefault(p => p.ShouldPursue(woWObject));
            if (pursueObject == null)
            {
                priority = 0;
                return false;
            }
            priority = pursueObject.Priority;
            return true;
        }

        public bool CanConvert(WoWObject woWObject, ConvertByType convertBy)
        {
            return PursueObjects.Any(p => p.CanConvert(woWObject, convertBy));
        }

		#region Concrete class required implementations...
		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return "$Id$"; } }
		public override string SubversionRevision { get { return "$Rev$"; } }

		public override XElement ToXml(string elementName = null)
		{
			if (string.IsNullOrEmpty(elementName))
                elementName = "PursuitList";

			var root = new XElement(elementName);

            foreach (var pursueObject in PursueObjects)
				root.Add(pursueObject.ToXml());

			return root;
		}
		#endregion
		
		
		#region Private and Convenience variables
        #endregion

        public IEnumerable<WoWObject> GetPursuitedObjects()
        {
            return from obj in ObjectManager.ObjectList
                let pursueObj = PursueObjects.FirstOrDefault(p => p.ShouldPursue(obj))
                where pursueObj != null
                orderby pursueObj.Priority descending
                orderby obj.DistanceSqr
                select obj;
        }

        public static PursuitListType GetOrCreate(XElement parentElement, string elementName)
        {
            var pursuitListType = new PursuitListType(parentElement
                                                .Elements()
                                                .DefaultIfEmpty(new XElement(elementName))
                                                .FirstOrDefault(elem => (elem.Name == elementName)));
            return pursuitListType;
        }
	}
}
