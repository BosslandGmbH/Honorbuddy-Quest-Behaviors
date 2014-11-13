// Originally contributed by Chinajade.
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
using System.Xml.Linq;
using Styx;
using Styx.CommonBot.Profiles;
using Styx.Patchables;
using Styx.WoWInternals.WoWObjects;

#endregion


namespace Honorbuddy.QuestBehaviorCore.XmlElements
{
    public enum ConvertByType
    {
        Killing,
        // Commented out until it is certain they are needed.
        //UseItem,
        //CastSpell,
    }

    public abstract class PursueObjectTypeBase : QuestBehaviorXmlBase
    {
        #region Constructor and Argument Processing

        protected PursueObjectTypeBase(XElement xElement)
            : base(xElement)
        {
            try
            {
                Id = GetAttributeAsNullable<int>("Id", false, ConstrainAs.MobId, null) ?? 0;
                Priority = GetAttributeAsNullable<int>("Priority", false, new ConstrainTo.Domain<int>(-10000, 10000), null) ?? 0;
                PursueWhenExpression = GetAttributeAs<string>("PursueWhen", false, ConstrainAs.StringNonEmpty, null) ; 
                ConvertWhenExpression = GetAttributeAs<string>("ConvertWhen", false, ConstrainAs.StringNonEmpty, null) ?? "false"; 
                ConvertBy = GetAttributeAsNullable<ConvertByType>("ConvertBy", false, null, null) ?? ConvertByType.Killing;

                if (string.IsNullOrEmpty(PursueWhenExpression))
                {
                    if (Id == 0)
                    {
                        QBCLog.Error("PursueWhen is required when no Id is specified");
                        IsAttributeProblem = true; 
                    }
                    else
                    {
                        PursueWhenExpression = "true";
                    }
                }
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
            string pursueWhenExp,
            string convertWhenExp,
            ConvertByType convertBy = ConvertByType.Killing)
        {
            Id = id;
            PursueWhenExpression = pursueWhenExp ?? "true";
            ConvertWhenExpression = convertWhenExp ?? "false";
            ConvertBy = convertBy;
        }

        #endregion

        public int Id { get; private set; }
        protected string ConvertWhenExpression { get; private set; }
        protected string PursueWhenExpression { get; private set; }
        protected ConvertByType ConvertBy { get; private set; }
        public int Priority { get; private set; }

        #region Concrete class required implementations...
        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return "$Id: PursueObjectType.cs 1787 2014-11-13 12:21:08Z highvoltz $"; } }
        public override string SubversionRevision { get { return "$Rev: 1787 $"; } }

        public override XElement ToXml(string elementName = null)
        {
            if (string.IsNullOrEmpty(elementName))
                elementName = "PursueObject";

            var element = new XElement(elementName,
                             new XAttribute("Id", Id),
                             new XAttribute("Priority", Priority),
                             new XAttribute("PursueWhen", PursueWhenExpression),
                             new XAttribute("ConvertWhen", ConvertWhenExpression),
                             new XAttribute("ConvertBy", ConvertBy));
         
            return element;
        }

        public abstract bool ShouldPursue(WoWObject obj);
        public abstract bool ShouldPursue(WoWObject obj, out int priority);
        public abstract bool CanConvert(WoWObject obj, ConvertByType convertBy);

        #endregion

    }

    public class PursueObjectType<T> : PursueObjectTypeBase where T : WoWObject
    {
        #region Constructor and Argument Processing

        public PursueObjectType(XElement xElement)
            : base(xElement)
        {
            try
            {
                CompileExpressions();
                
                if (PursueWhen.HasErrors || ConvertWhen.HasErrors)
                    IsAttributeProblem = true;

                HandleAttributeProblem();
            }

            catch (Exception except)
            {
                if (Query.IsExceptionReportingNeeded(except))
                    QBCLog.Exception(except, "PROFILE PROBLEM with \"{0}\"", xElement.ToString());
                IsAttributeProblem = true;
            }
        }

        public PursueObjectType(int id, string pursueWhenExp, string convertWhenExp, ConvertByType convertBy)
            : base(id, pursueWhenExp, convertWhenExp, convertBy)
        {
            CompileExpressions();
        }

        private void CompileExpressions()
        {
            var expressionName = string.Format("PursueWhen Id: {0}", Id);
            string paramaterName = GetParameterName();

            // We test compile the "PursueWhen" expression to look for problems.
            // Doing this in the constructor allows us to catch 'blind change'problems when ProfileDebuggingMode is turned on.
            // If there is a problem, an exception will be thrown (and handled here).
            PursueWhen = new UserDefinedExpression<T, bool>(expressionName, PursueWhenExpression, paramaterName);

            expressionName = string.Format("ConvertWhen Id: {0}", Id);
            ConvertWhen = new UserDefinedExpression<T, bool>(expressionName, ConvertWhenExpression, paramaterName);
        }

        #endregion

        public UserDefinedExpression<T, bool> ConvertWhen { get; private set; }
        public UserDefinedExpression<T, bool> PursueWhen { get; private set; }

        private string GetParameterName()
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
        public override string SubversionId { get { return "$Id: PursueObjectType.cs 1787 2014-11-13 12:21:08Z highvoltz $"; } }
        public override string SubversionRevision { get { return "$Rev: 1787 $"; } }

        public override bool ShouldPursue(WoWObject obj)
        {
           return (Id == 0 || obj.Entry == Id) &&  obj is T && PursueWhen.Evaluate((T)obj) ;
        }

        public override bool ShouldPursue(WoWObject obj, out int priority)
        {
            priority = Priority;
            return (Id == 0 || obj.Entry == Id) && obj is T && PursueWhen.Evaluate((T)obj);
        }
        public override bool CanConvert(WoWObject obj, ConvertByType convertBy)
        {
            return (Id == 0 || obj.Entry == Id) && ConvertBy == convertBy && obj is T && ConvertWhen.Evaluate((T)obj);
        }

        #endregion
    }
}
