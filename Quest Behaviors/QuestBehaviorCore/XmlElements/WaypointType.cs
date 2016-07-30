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
using System.Xml.Linq;

using Styx;
#endregion


namespace Honorbuddy.QuestBehaviorCore.XmlElements
{
    public class WaypointType : QuestBehaviorXmlBase
    {
        public const double DefaultAllowedVariance = 0.0;
        public const double DefaultArrivalTolerance = 1.5;

        public WaypointType(XElement xElement)
            : base(xElement)
        {
            try
            {
                DefinedLocation = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
                AllowedVariance = GetAttributeAsNullable<double>("AllowedVariance", false, new ConstrainTo.Domain<double>(0.0, 50.0), null) ?? DefaultAllowedVariance;
                ArrivalTolerance = GetAttributeAsNullable<double>("ArrivalTolerance", false, ConstrainAs.Range, null) ?? DefaultArrivalTolerance;
                Name = GetAttributeAs<string>("Name", false, ConstrainAs.StringNonEmpty, null) ?? string.Empty;

                if (string.IsNullOrEmpty(Name))
                { Name = GetDefaultName(DefinedLocation); }

                GenerateNewVariantLocation();

                HandleAttributeProblem();
            }

            catch (Exception except)
            {
                if (Query.IsExceptionReportingNeeded(except))
                { QBCLog.Exception(except, "PROFILE PROBLEM with \"{0}\"", xElement.ToString()); }
                IsAttributeProblem = true;
            }
        }


        public WaypointType(WoWPoint definedLocation,
            string name = "",
            double allowedVariance = DefaultAllowedVariance,
            double arrivalTolerance = DefaultArrivalTolerance)
        {
            DefinedLocation = definedLocation;
            Name = name ?? GetDefaultName(definedLocation);
            AllowedVariance = allowedVariance;
            ArrivalTolerance = arrivalTolerance;

            GenerateNewVariantLocation();
        }


        public double ArrivalTolerance { get; set; }
        public double AllowedVariance { get; set; }

        public bool AtLocation(WoWPoint location)
        {
            return Query.AtLocation(location, Location, (float)ArrivalTolerance);
        }

        /// <summary>
        /// This is the original location with which the <see cref="WaypointType"/> was defined.
        /// This location is not affected by <see cref="AllowedVariance"/>.
        /// </summary>
		public WoWPoint DefinedLocation { get; set; }

        /// <summary>
        /// This location is constructed from the <see cref="DefinedLocation"/> and <see cref="AllowedVariance"/>
        /// values when the waypoint is initialized.  This value will not change, unless a call to
        /// <see cref="GenerateNewVariantLocation()"/> is made.
        /// </summary>
        public WoWPoint Location { get; set; }

        public string Name { get; set; }


        /// <summary>
        /// Updates <see cref="Location"/> with a new value constructed from <see cref="DefinedLocation"/>
        /// and <see cref="AllowedVariance"/>.
        /// </summary>
        public void GenerateNewVariantLocation()
        {
            Location = DefinedLocation.FanOutRandom(AllowedVariance);
        }


        #region Concrete class required implementations...
        // DON'T EDIT THIS--it is auto-populated by Git
        public override string GitId => "$Id$";

        public override XElement ToXml(string elementName = null)
        {
            if (string.IsNullOrEmpty(elementName))
                elementName = "Waypoint";

            var root = new XElement(elementName,
                                    new XAttribute("Name", Name),
                                    new XAttribute("X", DefinedLocation.X),
                                    new XAttribute("Y", DefinedLocation.Y),
                                    new XAttribute("Z", DefinedLocation.Z));

            if (AllowedVariance != DefaultAllowedVariance)
                root.Add(new XAttribute("AllowedVariance", AllowedVariance));

            if (ArrivalTolerance != DefaultArrivalTolerance)
                root.Add(new XAttribute("ArrivalTolerance", ArrivalTolerance));

            return root;
        }
        #endregion


        private string GetDefaultName(WoWPoint wowPoint)
        {
            return string.Format("Waypoint({0})", wowPoint.ToString());
        }
    }
}
