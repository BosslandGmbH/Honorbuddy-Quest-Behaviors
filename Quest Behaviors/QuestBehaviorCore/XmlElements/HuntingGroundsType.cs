﻿// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.


#region Summary and Documentation
//      HuntingGrounds [optional; Default: none]
//          The HuntingGrounds contains a set of Waypoints we will visit to seek mobs
//          that fulfill the quest goal.  The <HuntingGrounds> element accepts the following
//          attributes:
//              WaypointVisitStrategy= [optional; Default: Random]
//              [Allowed values: InOrder, PickOneAtRandom, Random]
//              Determines the strategy that should be employed to visit each waypoint.
//              Any mobs encountered while traveling between waypoints will be considered
//              viable.  The Random strategy is highly recommended unless there is a compelling
//              reason to otherwise.  The Random strategy 'spread the toons out', if
//              multiple bos are running the same quest.
//              The PickOneAtRandom strategy will only visit one waypoint on the list
//              and camp the mobs from the single selected waypoint.  This is another good tactic
//              for spreading toons out in heavily populated areas.
//          Each Waypoint is provided by a <Hotspot ... /> element with the following
//          attributes:
//              Name [optional; Default: X/Y/Z location of the waypoint]
//                  The name of the waypoint is presented to the user as it is visited.
//                  This can be useful for debugging purposes, and for making minor adjustments
//                  (you know which waypoint to be fiddling with).
//              X/Y/Z [REQUIRED; Default: none]
//                  The world coordinates of the waypoint.
//              AllowedVariance [optional; Default: 0.0; RECOMMENDED: 7.0]
//                  ***It is HIGHLY recommended you make this value somewhere around 7.0 - 10.0.  The default value
//                   of zero is to maintain backward compatibility for existing profiles.***
//		        	This value is used to:
//			        * Prevent toons running the same profile from 'stacking up' on each other once they arrive
//			        * Defeat WoWserver-side LCP detection
//			        This value represents a radius.  A fractional percentage of this radius will be added
//			        to the specified X/Y/Z in a random direction, and that new point used for the final destination.
//			        The effect is that X/Y/Z no longer defines a 'landing point', but instead, a 'landing zone'.
//			        The final destination is always selected in a sane fashion, so boundary cases like boat
//			        docks and blimp towers should not be a concern.
//                  By default, this value will move to the exact X/Y/Z specified.  It is HIGHLY recommended you
//                  allow a more 'fuzzy' destination by setting this value from 7.0 - 10.0.  This will help
//                  abate automated WoWserver-side detection, and make the toons look more 'human like' when
//                  they are waiting for boats and whatnot.
//              ArrivalTolerance [optional; Default: 1.5]
//                  Once the toon gets within Radius of the waypoint, the next waypoint
//                  will be sought.
#endregion

#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using Styx;
using Styx.WoWInternals;
#endregion


namespace Honorbuddy.QuestBehaviorCore.XmlElements
{
	public class HuntingGroundsType : QuestBehaviorXmlBase
	{
		#region Constructor and Argument Processing
		public enum WaypointVisitStrategyType
		{
			InOrder,
			PickOneAtRandom,
			Random,
		}


		// 22Mar2013-11:49UTC chinajade
		public HuntingGroundsType(XElement xElement)
			: base(xElement)
		{
			try
			{
				// Acquire the visit strategy...
				var waypointVisitStrategy = GetAttributeAsNullable<WaypointVisitStrategyType>("WaypointVisitStrategy", false, null, null)
				                        ?? WaypointVisitStrategyType.Random;
				SetWaypointStrategy(waypointVisitStrategy, false);

				// Acquire the waypoints...
				Waypoints = new List<WaypointType>();
				if (xElement != null)
				{
					var waypointElementsQuery =
						from element in xElement.Elements()
						where
							(element.Name == "Hotspot")
							|| (element.Name == "Waypoint")
							|| (element.Name == "WaypointType")
						select element;

					foreach (XElement childElement in waypointElementsQuery)
					{
						var waypoint = new WaypointType(childElement);

						if (!waypoint.IsAttributeProblem)
							Waypoints.Add(waypoint);

						IsAttributeProblem |= waypoint.IsAttributeProblem;
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

		public WaypointVisitStrategyType WaypointVisitStrategy
		{
			get
			{
				Contract.Requires(_visitStrategy != null, context => "WaypointVisitStrategy must first be set.");
				return _visitStrategy.VisitStrategyType;
			} 
			set
			{
				SetWaypointStrategy(value, true);
			}
		}

		private void SetWaypointStrategy(WaypointVisitStrategyType visitStrategyType, bool logChanges)
		{
			if ((_visitStrategy == null) || (visitStrategyType != _visitStrategy.VisitStrategyType))
			{
				_visitStrategy = GetVisitStrategyFromType(visitStrategyType);

				if (_visitStrategy != null && logChanges)
					QBCLog.DeveloperInfo("WaypointVisitStrategy set to {0}", _visitStrategy.VisitStrategyType);

				// Strategy change requires current waypoint re-evaluation...
				ResetWaypoints();
			}
		}

		private IVisitStrategy GetVisitStrategyFromType(WaypointVisitStrategyType visitStrategyType )
		{
			IVisitStrategy visitStrategy = null;
			if (visitStrategyType == WaypointVisitStrategyType.InOrder)
				visitStrategy = new VisitStrategy_InOrder();
			else if (visitStrategyType == WaypointVisitStrategyType.PickOneAtRandom)
				visitStrategy = new VisitStrategy_PickOneAtRandom();
			else if (visitStrategyType == WaypointVisitStrategyType.Random)
				visitStrategy = new VisitStrategy_Random();
			else
			{
				QBCLog.MaintenanceError("Unhandled WaypointVisitStrategy({0})", visitStrategyType);
			}
			return visitStrategy;
		}

		public List<WaypointType> Waypoints { get; set; }
		#endregion


		#region Concrete class required implementations...
		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return "$Id$"; } }
		public override string SubversionRevision { get { return "$Rev$"; } }

		public override XElement ToXml(string elementName = null)
		{
			if (string.IsNullOrEmpty(elementName))
				elementName = "HuntingGrounds";

			var root = new XElement(elementName,
			                        new XAttribute("WaypointVisitStrategy", WaypointVisitStrategy));

			foreach (var waypoint in Waypoints)
				root.Add(waypoint.ToXml());

			return root;
		}
		#endregion


		#region Private and Convenience variables

		// NB: The "initial position waypoint" is special.
		// It is only used if no other waypoints have been defined.
		private readonly WaypointType _initialPositionWaypoint = new WaypointType((WoWMovement.ActiveMover ?? StyxWoW.Me).Location, "my initial position");
		private int _indexOfCurrentWaypoint = IVisitStrategy.InvalidWaypointIndex;
		private IVisitStrategy _visitStrategy;

		#endregion


		// 22Apr2013-12:50UTC chinajade
		public WaypointType CurrentWaypoint(WoWPoint? currentLocation = null)
		{
			currentLocation = currentLocation ?? WoWMovement.ActiveMover.Location;

			// If we haven't initialized current waypoint yet, find first waypoint...
			if (_indexOfCurrentWaypoint == IVisitStrategy.InvalidWaypointIndex)
            {
				_indexOfCurrentWaypoint = _visitStrategy.FindIndexOfNextWaypoint(this);
                if (_indexOfCurrentWaypoint != IVisitStrategy.InvalidWaypointIndex)
                    FindWaypointAtIndex(_indexOfCurrentWaypoint).GenerateNewVariantLocation();  // Apply "AllowedVariance"
            }

			// If we haven't arrived at the current waypoint, still use it...
			var currentWaypoint = FindWaypointAtIndex(_indexOfCurrentWaypoint);
			if (!Query.AtLocation(currentLocation.Value, currentWaypoint.Location, (float)currentWaypoint.ArrivalTolerance))
				return currentWaypoint;

			// Otherwise, find next waypoint index, and return new waypoint...
			_indexOfCurrentWaypoint = _visitStrategy.FindIndexOfNextWaypoint(this, _indexOfCurrentWaypoint);
            var waypoint = FindWaypointAtIndex(_indexOfCurrentWaypoint);
            waypoint.GenerateNewVariantLocation();  // Apply "AllowedVariance"
            return waypoint;
		}


        /// <summary>
        /// <para>This method is used to "start over" on the HuntingGround's waypoint evaluation.</para>
        /// <para>* For a visit strategy of <see cref="InOrder"/>, this method causes the <see cref="CurrentWaypoint"/>
        /// to be reset to the 'first' waypoint in the list.</para>
        /// <para>* For a visit strategy of <see cref="PickOneAtRandom"/>, this method will cause  <see cref="CurrentWaypoint"/>
        /// to be set to a new random waypoint from the list.</para>
        /// <para>* etc.</para>
        /// </summary>
        public void ResetWaypoints()
        {
            // Strategy change requires current waypoint re-evaluation...
            _indexOfCurrentWaypoint = IVisitStrategy.InvalidWaypointIndex;
        }


		private WaypointType FindWaypointAtIndex(int index)
		{
			return (index == IVisitStrategy.InvalidWaypointIndex)
				? _initialPositionWaypoint
				: Waypoints[index];
		}


		// 22Mar2013-11:49UTC chinajade
		private int FindIndexOfNearestWaypoint(WoWPoint location)
		{
			var indexOfNearestWaypoint = IVisitStrategy.InvalidWaypointIndex;
			var distanceSqrMin = double.MaxValue;

			for (var index = 0; index < Waypoints.Count; ++index)
			{
				var distanceSqr = location.DistanceSqr(FindWaypointAtIndex(index).Location);

				if (distanceSqr < distanceSqrMin)
				{
					distanceSqrMin = distanceSqr;
					indexOfNearestWaypoint = index;
				}
			}

			return indexOfNearestWaypoint;
		}


		// 11Apr2013-04:42UTC chinajade
		public static HuntingGroundsType GetOrCreate(XElement parentElement, string elementName, WaypointType defaultHuntingGroundCenter = null)
		{
			var huntingGrounds = new HuntingGroundsType(parentElement
				                                            .Elements()
				                                            .DefaultIfEmpty(new XElement(elementName))
				                                            .FirstOrDefault(elem => (elem.Name == elementName)));

			if (!huntingGrounds.IsAttributeProblem)
			{
				// If user didn't provide a HuntingGrounds, and he provided a default center point, add it...
				if (!huntingGrounds.Waypoints.Any() && (defaultHuntingGroundCenter != null))
					huntingGrounds.Waypoints.Add(defaultHuntingGroundCenter);

				if (!huntingGrounds.Waypoints.Any())
				{
					QBCLog.Error("Neither the X/Y/Z attributes nor the <{0}> sub-element has been specified.", elementName);
					huntingGrounds.IsAttributeProblem = true;
				}
			}

			return huntingGrounds;
		}


		// We must support 'implied containers' for backward compatibility purposes.
		// An 'implied container' is just a list of <Hotspot> without the <HuntingGrounds> container.  As such, we use defaults.
		// This method will first look for the 'new style' (with container), and if that fails, looks for just a list of hotspots with
		// which we can construct the object.
		// 22Apr2013-10:29UTC chinajade
		public static HuntingGroundsType GetOrCreate_ImpliedContainer(XElement parentElement, string elementName, WaypointType defaultHuntingGroundCenter = null)
		{
			var huntingGrounds = GetOrCreate(parentElement, elementName, defaultHuntingGroundCenter);

			// If 'new form' succeeded, we're done...
			if (!huntingGrounds.IsAttributeProblem)
				return huntingGrounds;

			// 'Old form' we have to dig out the Hotspots manually...
			huntingGrounds = new HuntingGroundsType(new XElement(elementName));
			if (!huntingGrounds.IsAttributeProblem)
			{
				var waypoints = new List<WaypointType>();

				int unnamedWaypointNumber = 0;
				foreach (XElement childElement in parentElement.Elements().Where(elem => (elem.Name == "Hotspot")))
				{
					var waypoint = new WaypointType(childElement);

					if (!waypoint.IsAttributeProblem)
					{
						if (string.IsNullOrEmpty(waypoint.Name))
							waypoint.Name = string.Format("UnnamedWaypoint{0}", ++unnamedWaypointNumber);
						waypoints.Add(waypoint);
					}

					huntingGrounds.IsAttributeProblem |= waypoint.IsAttributeProblem;
				}
			}

			return huntingGrounds;
		}


		#region Visit Strategies
		private abstract class IVisitStrategy
		{
			public const int InvalidWaypointIndex = -1;

			public abstract int FindIndexOfNextWaypoint(HuntingGroundsType huntingGrounds, int currentWaypointIndex = InvalidWaypointIndex);

			protected IVisitStrategy(WaypointVisitStrategyType visitStrategyType)
			{
				VisitStrategyType = visitStrategyType;
			}

			public WaypointVisitStrategyType VisitStrategyType { get; private set; }
		}


		private class VisitStrategy_InOrder : IVisitStrategy
		{
			public VisitStrategy_InOrder()
				: base(WaypointVisitStrategyType.InOrder)
			{
				// empty
			}


			public override int FindIndexOfNextWaypoint(HuntingGroundsType huntingGrounds, int currentWaypointIndex = InvalidWaypointIndex)
			{
				// Current waypoint index is invalid?
				if (currentWaypointIndex == InvalidWaypointIndex)
				{
					// If no waypoints defined, then nothing to choose from...
					if (huntingGrounds.Waypoints.Count <= 0)
						return InvalidWaypointIndex;

					// Pick initial waypoint--the nearest one from the list of available waypoints...
					return huntingGrounds.FindIndexOfNearestWaypoint(WoWMovement.ActiveMover.Location);
				}

				// Waypoint is simply next one in the list, and wrap around if we've reached the end...
				++currentWaypointIndex;
				if (currentWaypointIndex >= huntingGrounds.Waypoints.Count)
					currentWaypointIndex = 0;

				return currentWaypointIndex;
			}
		}


		private class VisitStrategy_PickOneAtRandom : IVisitStrategy
		{
			public VisitStrategy_PickOneAtRandom()
				: base(WaypointVisitStrategyType.PickOneAtRandom)
			{
				// empty
			}

			public override int FindIndexOfNextWaypoint(HuntingGroundsType huntingGrounds, int currentWaypointIndex = InvalidWaypointIndex)
			{
				// Current waypoint index is invalid?
				if (currentWaypointIndex == InvalidWaypointIndex)
				{
					// If no waypoints defined, then nothing to choose from...
					if (huntingGrounds.Waypoints.Count <= 0)
						return InvalidWaypointIndex;

					// Pick initial waypoint--a random waypoint from those available on the list...
					return StyxWoW.Random.Next(0, huntingGrounds.Waypoints.Count);
				}

				// Once waypoint is selected, we continue to use it...
				return currentWaypointIndex;
			}
		}


		private class VisitStrategy_Random : IVisitStrategy
		{
			public VisitStrategy_Random()
				: base(WaypointVisitStrategyType.Random)
			{
				// empty
			}


			public override int FindIndexOfNextWaypoint(HuntingGroundsType huntingGrounds, int currentWaypointIndex = InvalidWaypointIndex)
			{
				// Current waypoint index is invalid?
				if (currentWaypointIndex == InvalidWaypointIndex)
				{
					// If no waypoints defined, then nothing to choose from...
					if (huntingGrounds.Waypoints.Count <= 0)
						return InvalidWaypointIndex;

					// Pick initial waypoint--fall through to pick initial waypoint...
				}

				// Determine 'next' waypoint based on visit strategy...
				// NB: If we have more than one point to select from, make certain we don't re-select
				// the current point.
				int newWaypointIndex;
				do
				{
					newWaypointIndex = StyxWoW.Random.Next(0, huntingGrounds.Waypoints.Count);
				} while ((huntingGrounds.Waypoints.Count > 1) && (currentWaypointIndex == newWaypointIndex));

				return newWaypointIndex;
			}
		}
		#endregion
	}
}
