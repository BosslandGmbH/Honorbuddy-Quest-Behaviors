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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;



namespace Honorbuddy.QuestBehaviorCore.XmlElements
{
    public class SwimBreathType : QuestBehaviorXmlBase
    {        
        public SwimBreathType(XElement xElement)
            : base(xElement)
        {
            try
            {
                AirSources = new List<AirSourceType>();
                foreach (var childElement in xElement.Descendants().Where(elem => (elem.Name == "AirSource")))
                {
                    var airSource = new AirSourceType(childElement);

                    if (!airSource.IsAttributeProblem)
                        { AirSources.Add(airSource); }

                    IsAttributeProblem |= airSource.IsAttributeProblem;
                }

                Consumables = new List<ConsumablesType>();
                foreach (var childElement in xElement.Descendants().Where(elem => (elem.Name == "Consumable")))
                {
                    var consumable = new ConsumablesType(childElement);

                    if (!consumable.IsAttributeProblem)
                        { Consumables.Add(consumable); }

                    IsAttributeProblem |= consumable.IsAttributeProblem;
                }


                Equipments = new List<EquipmentType>();
                foreach (var childElement in xElement.Descendants().Where(elem => (elem.Name == "Equipment")))
                {
                    var equipment = new EquipmentType(childElement);

                    if (!equipment.IsAttributeProblem)
                        { Equipments.Add(equipment); }

                    IsAttributeProblem |= equipment.IsAttributeProblem;
                }
                
                
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

        public SwimBreathType()
        {
            AirSources = new List<AirSourceType>();
            Consumables = new List<ConsumablesType>();
            Equipments = new List<EquipmentType>();
        }

        public List<AirSourceType>       AirSources { get; private set; }
        public List<ConsumablesType>     Consumables { get; private set; }
        public List<EquipmentType>       Equipments { get; private set; }


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

            tmp.AppendFormat("<SwimBreath>");
            if (AirSources.Count > 0)
            {
                tmp.AppendFormat("{0}  <AirSources>", fieldSeparator);
                foreach (var airSource in AirSources)
                    { tmp.AppendFormat("{0}    {1}", fieldSeparator, airSource.ToString_FullInfo(true, indentLevel +4)); }
                tmp.AppendFormat("{0}  </AirSources>", fieldSeparator);
            }

            if (Consumables.Count > 0)
            {
                tmp.AppendFormat("{0}<Consumables>", fieldSeparator);
                foreach (var consumable in Consumables)
                    { tmp.AppendFormat("{0}    {1}", fieldSeparator, consumable.ToString_FullInfo(true, indentLevel +4)); }
                tmp.AppendFormat("{0}</Consumables>", fieldSeparator);                
            }

            if (Equipments.Count > 0)
            {
                tmp.AppendFormat("{0}<Equipments>", fieldSeparator);
                foreach (var equipment in Equipments)
                    { tmp.AppendFormat("{0}    {1}", fieldSeparator, equipment.ToString_FullInfo(true, indentLevel +4)); }
                tmp.AppendFormat("{0}</Equipments>", fieldSeparator);
            }
            tmp.AppendFormat("{0}</SwimBreath>", fieldSeparator);

            return tmp.ToString();
        }

        public class AirSourceType : QuestBehaviorXmlBase
        {        
            public AirSourceType(XElement xElement)
                : base(xElement)
            {
                try
                {
                    ObjectId = GetAttributeAsNullable<int>("ObjectId", true, ConstrainAs.ObjectId, null) ?? 0;
                    Name = GetAttributeAs<string>("Name", false, ConstrainAs.StringNonEmpty, null) ?? string.Empty;

                    if (string.IsNullOrEmpty(Name))
                        { Name = GetDefaultName(ObjectId); }

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

            public AirSourceType(int objectId, string name = null)
            {
                ObjectId = objectId;
                Name = name ?? GetDefaultName(objectId);
            }

            public int ObjectId { get; set; }
            public string Name { get; set; }

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

                tmp.AppendFormat("<AirSource");
                tmp.AppendFormat("{0}ObjectId=\"{1}\"", fieldSeparator, ObjectId);
                tmp.AppendFormat("{0}Name=\"{1}\"", fieldSeparator, Name);
                tmp.AppendFormat("{0}/>", fieldSeparator);

                return tmp.ToString();
            }


            private string GetDefaultName(int itemId)
            {
                return string.Format("ObjectId({0})", itemId);
            }
        }

    
        public class ConsumablesType : QuestBehaviorXmlBase
        {        
            public ConsumablesType(XElement xElement)
                : base(xElement)
            {
                try
                {
                    ItemId = GetAttributeAsNullable<int>("ItemId", true, ConstrainAs.ItemId, null) ?? 0;
                    Name = GetAttributeAs<string>("Name", false, ConstrainAs.StringNonEmpty, null) ?? string.Empty;

                    if (string.IsNullOrEmpty(Name))
                        { Name = GetDefaultName(ItemId); }

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

            public ConsumablesType(int itemId, string name = null)
            {
                ItemId = itemId;
                Name = name ?? GetDefaultName(itemId);
            }

            public int ItemId { get; set; }
            public string Name { get; set; }

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

                tmp.AppendFormat("<Consumable");
                tmp.AppendFormat("{0}ItemId=\"{1}\"", fieldSeparator, ItemId);
                tmp.AppendFormat("{0}Name=\"{1}\"", fieldSeparator, Name);
                tmp.AppendFormat("{0}/>", fieldSeparator);

                return tmp.ToString();
            }


            private string GetDefaultName(int itemId)
            {
                return string.Format("ItemId({0})", itemId);
            }
        }


        public class EquipmentType : QuestBehaviorXmlBase
        {        
            public EquipmentType(XElement xElement)
                : base(xElement)
            {
                try
                {
                    ItemId = GetAttributeAsNullable<int>("ItemId", true, ConstrainAs.ItemId, null) ?? 0;
                    Name = GetAttributeAs<string>("Name", false, ConstrainAs.StringNonEmpty, null) ?? string.Empty;

                    if (string.IsNullOrEmpty(Name))
                        { Name = GetDefaultName(ItemId); }

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

            public EquipmentType(int itemId, string name = null)
            {
                ItemId = itemId;
                Name = name ?? GetDefaultName(itemId);
            }

            public int ItemId { get; set; }
            public string Name { get; set; }

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

                tmp.AppendFormat("<Equipment");
                tmp.AppendFormat("{0}ItemId=\"{1}\"", fieldSeparator, ItemId);
                tmp.AppendFormat("{0}Name=\"{1}\"", fieldSeparator, Name);
                tmp.AppendFormat("{0}/>", fieldSeparator);

                return tmp.ToString();
            }


            private string GetDefaultName(int itemId)
            {
                return string.Format("ItemId({0})", itemId);
            }
        }


    }
}
