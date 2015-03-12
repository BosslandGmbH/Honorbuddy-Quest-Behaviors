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
using System.Linq;
using System.Xml.Linq;
using Styx;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Profiles.Quest.Order;
using Styx.Patchables;
using Styx.WoWInternals.WoWObjects;

#endregion


namespace Honorbuddy.QuestBehaviorCore.XmlElements
{
    public enum ConvertByType
    {
        Killing,
        // Commented out until it is certain they are needed.
        // UseItem,
        // CastSpell,
    }

    public abstract class PursueObjectTypeBase : QuestBehaviorXmlBase
    {
        #region Constructor and Argument Processing

        protected PursueObjectTypeBase(XElement xElement, string parameterName, Type expressionType)
            : base(xElement)
        {
            try
            {
                Id = GetAttributeAsNullable<int>("Id", false, ConstrainAs.MobId, null) ?? 0;
                Priority = GetAttributeAsNullable<int>("Priority", false, new ConstrainTo.Domain<int>(-10000, 10000), null) ?? 0;

                var pursueWhenExpression = GetAttributeAs<string>("PursueWhen", false, ConstrainAs.StringNonEmpty, null) ; 
                var convertWhenExpression = GetAttributeAs<string>("ConvertWhen", false, ConstrainAs.StringNonEmpty, null) ?? "false";

                ConvertBy = GetAttributeAsNullable<ConvertByType>("ConvertBy", false, null, null) ?? ConvertByType.Killing;

                if (string.IsNullOrEmpty(pursueWhenExpression))
                {
                    if (Id == 0)
                    {
                        QBCLog.Error("Either Id, PursueWhen, or both must be specified.");
                        IsAttributeProblem = true; 
                    }
                    else
                    {
                        pursueWhenExpression = "true";
                    }
                }

				ConvertWhenDelayCompiledExpression = (DelayCompiledExpression)Activator.CreateInstance(expressionType, parameterName + "=>" + convertWhenExpression);
				ConvertWhen = ConvertWhenDelayCompiledExpression.CallableExpression;

				PursueWhenDelayCompiledExpression = (DelayCompiledExpression)Activator.CreateInstance(expressionType, parameterName + "=>" + pursueWhenExpression);
				PursueWhen = PursueWhenDelayCompiledExpression.CallableExpression;
            }
            catch (Exception except)
            {
                if (Query.IsExceptionReportingNeeded(except))
                    QBCLog.Exception(except, "PROFILE PROBLEM with \"{0}\"", xElement.ToString());
                IsAttributeProblem = true;
            }
        }

        protected PursueObjectTypeBase(
            int id,
			Delegate pursueWhen,
			Delegate convertWhen,
            ConvertByType convertBy = ConvertByType.Killing)
        {
            Id = id;
			PursueWhen = pursueWhen;
			ConvertWhen = convertWhen;
            ConvertBy = convertBy;
        }

        #endregion

        public int Id { get; private set; }
        protected ConvertByType ConvertBy { get; private set; }
        public float Priority { get; private set; }

        #region Concrete class required implementations...
        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return "$Id$"; } }
        public override string SubversionRevision { get { return "$Rev$"; } }

        public override XElement ToXml(string elementName = null)
        {
            if (string.IsNullOrEmpty(elementName))
                elementName = "PursueObject";

            var element = new XElement(elementName,
                             new XAttribute("Id", Id),
                             new XAttribute("Priority", Priority),
                             new XAttribute("PursueWhen", PursueWhenDelayCompiledExpression.ExpressionString),
                             new XAttribute("ConvertWhen", ConvertWhenDelayCompiledExpression.ExpressionString),
                             new XAttribute("ConvertBy", ConvertBy));
         
            return element;
        }

        #endregion

		// These DelayCompiledExpression are only needed when an instance of this type is constructed from an XElement,
		// when the ConvertWhen/PursueWhen expressions are not known at compile time. 
		[CompileExpression]
		public DelayCompiledExpression ConvertWhenDelayCompiledExpression { get; protected set; }

		[CompileExpression]
		public DelayCompiledExpression PursueWhenDelayCompiledExpression { get; protected set; }

		public Delegate ConvertWhen{ get; protected set; }

		public Delegate PursueWhen { get; protected set; }

        public abstract bool ShouldPursue(WoWObject obj);
        public abstract bool ShouldPursue(WoWObject obj, out float priority);
        public abstract bool CanConvert(WoWObject obj, ConvertByType convertBy);
    }

    public class PursueObjectType<T> : PursueObjectTypeBase where T : WoWObject
    {
        #region Constructor and Argument Processing

        public PursueObjectType(XElement xElement)
			: base(xElement, GetParameterName(), typeof(DelayCompiledExpression<Func<T, bool>>))
        {
            try
            {
                HandleAttributeProblem();
            }

            catch (Exception except)
            {
                if (Query.IsExceptionReportingNeeded(except))
                    QBCLog.Exception(except, "PROFILE PROBLEM with \"{0}\"", xElement.ToString());
                IsAttributeProblem = true;
            }
        }

        public PursueObjectType(
            int id,
			Func<T, bool> pursueWhenExp = null,
			Func<T, bool> convertWhenExp = null,
            ConvertByType convertBy = ConvertByType.Killing)
			: base(id, pursueWhenExp ?? (unit => true), convertWhenExp ?? (unit => false), convertBy)
        {
        }

        #endregion

        private static string GetParameterName()
        {
            if (typeof(T) == typeof(LocalPlayer))
                return "ME";

            if (typeof (T) == typeof (WoWUnit))
                return "UNIT";

            if (typeof(T) == typeof(WoWGameObject))
                return "GAMEOBJECT";

            return "OBJECT";
        }

        #region Concrete class required implementations...
        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return "$Id$"; } }
        public override string SubversionRevision { get { return "$Rev$"; } }

        public override bool ShouldPursue(WoWObject obj)
        {
           return (Id == 0 || obj.Entry == Id) &&  obj is T && ((Func<T, bool>) PursueWhen)((T)obj) ;
        }

        public override bool ShouldPursue(WoWObject obj, out float priority)
        {
            priority = Priority;
	        return (Id == 0 || obj.Entry == Id) && obj is T && ((Func<T, bool>) PursueWhen)((T) obj);
        }
        public override bool CanConvert(WoWObject obj, ConvertByType convertBy)
        {
	        return (Id == 0 || obj.Entry == Id) && ConvertBy == convertBy && obj is T 
				&& ((Func<T, bool>) ConvertWhen)((T) obj);
        }

        #endregion
    }
}
