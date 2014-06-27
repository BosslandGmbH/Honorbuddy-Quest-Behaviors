// Behavior originally contributed by Bobby53 / rework by chinajade
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

#region Summary and Documentation
// QUICK DOX:
// GOTHRUPORTAL walks through portals in a way that does not result in
// WoW "red error" messages, or error messages in the HB log/debug files.
//
// You provide an XYZ coordinate as close as possible to the portal entrance
// without entering (within a couple of yards).  GOTHRUPORTAL then calculates
// a vector from your current position that passes through the provided XYZ.  The endpoint
// of this vector is 15 yards away from your current position.  The toon should
// intercept the portal and zone, before it gets to the calculated endpoint.
//
// If portal entry fails, this behavior will try a few times before giving up.
// Portal entry failures are most often caused by hiccups in the destination WoWserver
// instance.  The number of times, and the delay between attempts is adjustable
// with tunable parameters.
// 
// BEHAVIOR ATTRIBUTES:
// *** ALSO see the documentation in QuestBehaviorBase.cs.  All of the attributes it provides
// *** are available here, also.  The documentation on the attributes QuestBehaviorBase provides
// *** is _not_ repeated here, to prevent documentation inconsistencies.
//
// Basic Attributes:
//      X/Y/Z [REQUIRED]:
//          Defines a point immediately in front of the portal.  A vector will be created
//          that originates from the toon's current position, and passes through this
//          X/Y/Z value.  The portal entrance should lie along this vector.
//          The length of the created vector is 15 yards.
//
// Tunables:
//      InitialX/InitialY/InitialZ [optional; Default: toon's current position]
//          Represents the location the toon should be standing when it uses the X/Y/Z
//          to calculate a vector through the portal.
//      MaxRetryCount [optional; Default: 3]
//          The number of attempts the behavior will make to enter the portal.
//          After this number of retries is exhausted, the behavior gives up and
//          terminates the profile.
//      RetryDelay [optional; Default: 90 (seconds)]:
//          The amount of time the behavior should wait before attempting to re-enter
//          the portal from a failed attempt. If the destination instance server is having
//          issues, we don't want the retries to be too close together.
//      Timeout [optional; Default: 10000 (milliseconds)]:
//          Represents the maximum number of milliseconds the caller expects entering
//          the portal to take.  If the portal is not entered in this amount of time,
//          the retry process is engaged.
//
#endregion


#region Examples
//
// Use MoveTo to get start position, then GoThruPortal to run through XYZ vector
// on way through portal.
//     <MoveTo X="4646.201" Y="-3685.043" Z="954.2496" />
//     <CustomBehavior File="GoThruPortal" X="4656.928" Y="-3685.472" Z="957.185" />
// 
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Xml.Linq;

using Bots.Grind;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.GoThruPortal
{
	[CustomBehaviorFileName(@"GoThruPortal")]
	public class GoThruPortal : QuestBehaviorBase
	{
		#region Constructor and Argument Processing
		public GoThruPortal(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// NB: Core attributes are parsed by QuestBehaviorBase parent (e.g., QuestId, NonCompeteDistance, etc)

				// Behavior-specific attributes...
				MovePoint = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;

				// Tunables...
				StartingPoint = GetAttributeAsNullable<WoWPoint>("Initial", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;
				MaxRetryCount = GetAttributeAsNullable<int>("MaxRetryCount", false, new ConstrainTo.Domain<int>(1, 10), null) ?? 3;
				int retryDelay = GetAttributeAsNullable<int>("RetryDelay", false, new ConstrainTo.Domain<int>(0, 300000), null) ?? 90000;
				int zoningMaxWaitTime = GetAttributeAsNullable<int>("Timeout", false, new ConstrainTo.Domain<int>(1, 60000), null) ?? 10000;

				MovePoint = WoWMathHelper.CalculatePointFrom(StartingPoint, MovePoint, -15.0f);
				RetryDelay = TimeSpan.FromMilliseconds(retryDelay);
				MaxTimeToPortalEntry = TimeSpan.FromMilliseconds(zoningMaxWaitTime);
			}

			catch (Exception except)
			{
				// Maintenance problems occur for a number of reasons.  The primary two are...
				// * Changes were made to the behavior, and boundary conditions weren't properly tested.
				// * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
				// In any case, we pinpoint the source of the problem area here, and hopefully it
				// can be quickly resolved.
				QBCLog.Exception(except);
				IsAttributeProblem = true;
			}
		}


		// Attributes provided by caller
		private WoWPoint MovePoint { get; set; }
		private int MaxRetryCount { get; set; }
		private TimeSpan RetryDelay { get; set; }
		private WoWPoint StartingPoint { get; set; }
		private TimeSpan MaxTimeToPortalEntry { get; set; }

		protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
		{
			//// EXAMPLE: 
			//UsageCheck_DeprecatedAttribute(xElement,
			//    Args.Keys.Contains("Nav"),
			//    "Nav",
			//    context => string.Format("Automatically converted Nav=\"{0}\" attribute into MovementBy=\"{1}\"."
			//                              + "  Please update profile to use MovementBy, instead.",
			//                              Args["Nav"], MovementBy));
		}

		protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
		{
			//// EXAMPLE:
			//UsageCheck_SemanticCoherency(xElement,
			//    (!MobIds.Any() && !FactionIds.Any()),
			//    context => "You must specify one or more MobIdN, one or more FactionIdN, or both.");
			//
			//const double rangeEpsilon = 3.0;
			//UsageCheck_SemanticCoherency(xElement,
			//    ((RangeMax - RangeMin) < rangeEpsilon),
			//    context => string.Format("Range({0}) must be at least {1} greater than MinRange({2}).",
			//                  RangeMax, rangeEpsilon, RangeMin)); 
		}
		#endregion


		#region Private and Convenience variables
		private enum StateType_MainBehavior
		{
			CheckZoningSuccess,
			MovingToStartPosition,  // Initial state
			MovingToPortal,
			WaitingForRetryDelay,
			WaitingForZoningToComplete,
			Undefined,
		};

		// Private properties
		private bool IsInPortal { get { return !StyxWoW.IsInGame || (StyxWoW.Me == null);  } }
		private readonly TimeSpan PostZoningDelay = TimeSpan.FromMilliseconds(1250);
		private StateType_MainBehavior State_MainBehavior
		{
			get { return _state_MainBehavior; }
			set
			{
				// For DEBUGGING...
				if (_state_MainBehavior != value)
					{ QBCLog.DeveloperInfo("State_MainBehavior: {0}", value); }

				_state_MainBehavior = value;
			}
		}
		private string InitialZoneText { get; set; }
		private BehaviorFlags OriginalBehaviorFlags { get; set; }
		private WaitTimer PostZoningTimer { get; set; }
		private WaitTimer RetryDelayTimer { get; set; }
		private WaitTimer PortalEntryTimer { get; set; }

		private int _retryCount = 1;
		private StateType_MainBehavior _state_MainBehavior  = StateType_MainBehavior.Undefined;
		#endregion


		#region Destructor, Dispose, and cleanup
		#endregion


		#region Overrides of CustomForcedBehavior
		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }


		public override void OnFinished()
		{
			// Defend against being called multiple times (just in case)...
			if (!IsOnFinishedRun)
			{
				// QuestBehaviorBase.OnFinished() will set IsOnFinishedRun...
				base.OnFinished();
			}
		}


		public override void OnStart()
		{
			// Let QuestBehaviorBase do basic initialization of the behavior, deal with bad or deprecated attributes,
			// capture configuration state, install BT hooks, etc.  This will also update the goal text.
			var isBehaviorShouldRun = OnStart_QuestBehaviorCore("Moving through Portal");

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (isBehaviorShouldRun)
			{
				InitialZoneText = StyxWoW.Me.ZoneText;
				OriginalBehaviorFlags = LevelBot.BehaviorFlags;
				State_MainBehavior = StateType_MainBehavior.MovingToStartPosition;
			}
		}
		#endregion


		#region Main Behaviors
		protected override Composite CreateMainBehavior()
		{
			// Stateful Operation:
			return new Switch<StateType_MainBehavior>(context => State_MainBehavior,
				new Action(context =>   // default case
				{
					var message = string.Format("StateType_MainBehavior({0}) is unhandled", State_MainBehavior);
					QBCLog.MaintenanceError(message);
					TreeRoot.Stop();
					BehaviorDone(message);
				}),

				new SwitchArgument<StateType_MainBehavior>(StateType_MainBehavior.MovingToStartPosition,
					StateBehaviorPS_MovingToStartPosition()),

				new SwitchArgument<StateType_MainBehavior>(StateType_MainBehavior.MovingToPortal,
					StateBehaviorPS_MovingToPortal()),

				new SwitchArgument<StateType_MainBehavior>(StateType_MainBehavior.WaitingForZoningToComplete,
					StateBehaviorPS_WaitingForZoningToComplete()),

				new SwitchArgument<StateType_MainBehavior>(StateType_MainBehavior.CheckZoningSuccess,
					StateBehaviorPS_CheckZoningSuccess()),

				new SwitchArgument<StateType_MainBehavior>(StateType_MainBehavior.WaitingForRetryDelay,
					StateBehaviorPS_WaitingForRetryDelay())
			);
		}
		#endregion


		#region State Behaviors

		private Composite StateBehaviorPS_CheckZoningSuccess()
		{
			return new Action(context =>
			{
				// Once zone complete, enable defending ourself...
				LevelBot.BehaviorFlags = OriginalBehaviorFlags;

				// Successful Zoning?
				if (InitialZoneText != Me.ZoneText)
				{
					BehaviorDone("Destination Reached");
					return RunStatus.Success;
				}

				// Zoning failed, do we have any retries left?
				_retryCount += 1;
				if (_retryCount > MaxRetryCount)
				{
					var message = string.Format("Unable to go through portal in {0} attempts.", MaxRetryCount);

					// NB: Posting a 'fatal' message will stop the bot--which is what we want.
					QBCLog.Fatal(message);
					BehaviorDone(message);
					return RunStatus.Success;
				}

				State_MainBehavior = StateType_MainBehavior.MovingToStartPosition;
				return RunStatus.Success;
			});
		}
		
		
		private Composite StateBehaviorPS_MovingToPortal()
		{
			return new Action(context =>
			{
				// Turn off anything that might distract us...
				LevelBot.BehaviorFlags &= ~(BehaviorFlags.Combat | BehaviorFlags.Loot | BehaviorFlags.Pull);

				// If we're in portal, wait for exit on other side...
				// NB: Honorbuddy tends to suspend 'main' behaviors while IsInGame is false.
				// So, we need to have viable secondary tests as backup.
				if ((InitialZoneText != Me.ZoneText) || IsInPortal)
				{
					QBCLog.DeveloperInfo("Zoned into portal");
					PostZoningTimer = null;
					State_MainBehavior = StateType_MainBehavior.WaitingForZoningToComplete;
					return RunStatus.Success;
				}

				// NB: We need to qualify with zone text, since Honorbuddy tends to suspend
				// 'main' behaviors while IsInGame is false.
				if (InitialZoneText == Me.ZoneText)
				{
					// If zoning timer isn't started, start it...
					if (PortalEntryTimer == null)
					{
						PortalEntryTimer = new WaitTimer(MaxTimeToPortalEntry);
						PortalEntryTimer.Reset();
						QBCLog.DeveloperInfo("Portal Entry Timer Started");
						// fall through
					}

					// If portal entry timer expired, deal with it...
					if (PortalEntryTimer.IsFinished)
					{
						QBCLog.Warning("Unable to enter portal within allotted time of {0}",
							Utility.PrettyTime(MaxTimeToPortalEntry));
						State_MainBehavior = StateType_MainBehavior.CheckZoningSuccess;
						return RunStatus.Success;
					}

					// If we are within 2 yards of calculated end point we should never reach...
					if (Me.Location.Distance(MovePoint) < 2)
					{
						QBCLog.Warning("Seems we missed the portal. Is Portal activated? Profile needs to pick better alignment?");
						State_MainBehavior = StateType_MainBehavior.CheckZoningSuccess;
						return RunStatus.Success;
					}

					// If we're not moving toward portal, get busy...
					if (!StyxWoW.Me.IsMoving || Navigator.AtLocation(StartingPoint))
					{
						QBCLog.DeveloperInfo("Entering portal via {0}", MovePoint);
						WoWMovement.ClickToMove(MovePoint);
						return RunStatus.Success;
					}
				}

				return RunStatus.Success;
			});
		}


		private Composite StateBehaviorPS_MovingToStartPosition()
		{
			return new PrioritySelector(
				// If our moving to the portal caused us to go thru it, wait for zoning to complete...
				new Decorator(context => IsInPortal,
					new Action(context =>
					{
						QBCLog.DeveloperInfo("Zoned into portal");
						PortalEntryTimer = null;
						PostZoningTimer = null;
						State_MainBehavior = StateType_MainBehavior.WaitingForZoningToComplete;
						return RunStatus.Success;
					})),

				// Move to portal starting position...
				new UtilityBehaviorPS.MoveTo(
					context => StartingPoint,
					context => "Portal",
					context => MovementBy),

			   // If we're in position, either move to portal, or wait for retry delay to expire...
			   new Decorator(context => Navigator.AtLocation(StartingPoint),
					new Action(context =>
					{
						PortalEntryTimer = null;
						PostZoningTimer = null;

						if (_retryCount > 1)
						{
							QBCLog.Info("Last portal entry attempt failed.  Will try re-entering portal again in {0} (try #{1}).",
								Utility.PrettyTime(RetryDelay),
								_retryCount);
							RetryDelayTimer = null;
							State_MainBehavior = StateType_MainBehavior.WaitingForRetryDelay;
							return RunStatus.Success;
						}

						State_MainBehavior = StateType_MainBehavior.MovingToPortal;
						return RunStatus.Success;
					}))
			);
		}


		private Composite StateBehaviorPS_WaitingForRetryDelay()
		{
			return new Action(context =>
			{
				// If retry delay timer isn't running, start it...
				if (RetryDelayTimer == null)
				{
					RetryDelayTimer = new WaitTimer(RetryDelay);
					RetryDelayTimer.Reset();
					// fall through
				}

				// Wait for retry delay timer to expire...
				if (!RetryDelayTimer.IsFinished)
				{
					TreeRoot.StatusText =
						string.Format("Retrying portal entry in {0} of {1}.",
							Utility.PrettyTime(RetryDelayTimer.TimeLeft),
							Utility.PrettyTime(RetryDelay));
					return RunStatus.Success;
				}

				State_MainBehavior = StateType_MainBehavior.MovingToPortal;
				return RunStatus.Success;
			});
		}


		private Composite StateBehaviorPS_WaitingForZoningToComplete()
		{
			return new Action(context =>
			{
				// Wait, if not done zoning...
				if (IsInPortal)
					{ return RunStatus.Success; }

				// Once zone complete, enable defending ourself...
				LevelBot.BehaviorFlags = OriginalBehaviorFlags;

				// Stop moving once we reach other side...
				if (Me.IsMoving)
				{
					WoWMovement.MoveStop();
					return RunStatus.Success;
				}

				// If Post-Zoning timer is not running start it...
				// The post zoning timer, allows the WoWclient to settle before further
				// decisions are made.
				if (PostZoningTimer == null)
				{
					PostZoningTimer = new WaitTimer(PostZoningDelay);
					PostZoningTimer.Reset();
					// fall through
				}

				// Wait for the Post-Zoning timer to expire...
				if (PostZoningTimer.IsFinished)
				{
					State_MainBehavior = StateType_MainBehavior.CheckZoningSuccess;
					return RunStatus.Success;
				} 
				
				return RunStatus.Success;
			});
		}
		#endregion
	}
}

