﻿// Behavior originally contributed by HighVoltz.
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

// Shoots a Cannon
// ##Syntax##
//		MobIdN: [optional; default: Kill everything that moves] Identifies the mobs to shoot at with cannon
//		VehicleId: ID of the vehicle
//		MaxAngle: Maximum Angle to aim, use /dump VehicleAimGetAngle() in game to get the angle
//		MinAngle: Minimum Angle to aim, use /dump VehicleAimGetAngle() in game to get the angle
//		Gravity: [optional; default: 30] The amount of gravity that effects projectile/s. 
//		Velecity:[optional; default: 70] The velocity of the Projectile/s
//		Buttons: A series of numbers that represent the buttons to press in order of importance, 
//				separated by comma, for example Buttons ="2,1" 
//		ExitButton: [optional] Button to press to exit the cannon such as the 'Skeletal Gryphon Escape'
//				ability that can be used on the cannon for the quest 'Massacre At Light's Point'. 1-12

#endregion

#region Examples

#endregion

#region Usings

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Honorbuddy.Quest_Behaviors.WaitTimerBehavior;
using JetBrains.Annotations;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Tripper.Tools.Math;
using Action = Styx.TreeSharp.Action;

#endregion

namespace Honorbuddy.Quest_Behaviors.Vehicles.CannonControl
{
	[CustomBehaviorFileName(@"Vehicles\CannonControl")]
	public class CannonControl : QuestBehaviorBase
	{
		private Composite _root;
		private WoWUnit _target;

		public CannonControl(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;
			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				// Primary attributes...
				MobIds = GetNumberedAttributesAsArray<int>("MobId", 0, ConstrainAs.MobId, new[] { "NpcId" });
				if (MobIds != null && !MobIds.Any())
					MobIds = GetAttributeAsArray<int>("MobIds", false, ConstrainAs.MobId, new[] { "NpcIds" }, null);

				VehicleSearchLocation = GetAttributeAsNullable<WoWPoint>("", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;
				
				Buttons = GetAttributeAsArray<int>("Buttons", true, new ConstrainTo.Domain<int>(-1, 12), null, null);

				ExitButton = GetAttributeAsNullable<int>("ExitButton", false, ConstrainAs.HotbarButton, null) ?? 0;
				MaxAngle = GetAttributeAsNullable<double>("MaxAngle", true, new ConstrainTo.Domain<double>(-1.5, 1.5), null) ?? 0;
				MinAngle = GetAttributeAsNullable<double>("MinAngle", true, new ConstrainTo.Domain<double>(-1.5, 1.5), null) ?? 0;
				Velocity = GetAttributeAsNullable<double>("Velocity", false, new ConstrainTo.Domain<double>(2.0, 1000), null) ?? 70;
				Gravity = GetAttributeAsNullable<double>("Gravity", false, new ConstrainTo.Domain<double>(0.0, 80), null) ?? 30;
				VehicleId = GetAttributeAsNullable<int>("VehicleId", true, ConstrainAs.VehicleId, null) ?? 0;

				WeaponArticulation = new WeaponArticulation(MinAngle, MaxAngle);
				Weapons = Buttons.Select(b => new VehicleWeapon(b, WeaponArticulation, Velocity, Gravity)).ToArray();
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
		private int[] Buttons { get; set; }
		private int[] MobIds { get; set; }
		private int ExitButton { get; set; }
		private double MaxAngle { get; set; }
		private double MinAngle { get; set; }
		private double? Gravity { get; set; }
		private double? Velocity { get; set; }
		private int VehicleId { get; set; }
		private WoWPoint VehicleSearchLocation { get; set; }

		// Private variables for internal state

		// Private properties
		private VehicleWeapon[] Weapons { get; set; }
		private WeaponArticulation WeaponArticulation { get; set; }

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId
		{
			get { return ("$Id$"); }
		}

		public override string SubversionRevision
		{
			get { return ("$Revision$"); }
		}

		#region Overrides of QuestBehaviorBase

		protected override Composite CreateMainBehavior()
		{
			return _root ?? (_root = new ActionRunCoroutine(ctx => MainCoroutine()));
		}

		public override void OnStart()
		{
			// This reports problems, and stops BT processing if there was a problem with attributes...
			// We had to defer this action, as the 'profile line number' is not available during the element's
			// constructor call.
			OnStart_HandleAttributeProblem();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (!IsDone)
			{
				this.UpdateGoalText(QuestId);
			}
		}

		public override void OnFinished()
		{
			if (IsDone && Query.IsInVehicle())
			{
				if (ExitButton > 0)
					CastPetAction(ExitButton);
				else
					Lua.DoString("VehicleExit()");
			}
			base.OnFinished();
		}

		protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
		{
			//// EXAMPLE: 
			//UsageCheck_DeprecatedAttribute(xElement,
			//    Args.Keys.Contains("Nav"),
			//    "Nav",
			//    context => string.Format("Automatically converted Nav=\"{0}\" attribute into MovementBy=\"{1}\"."
			//                              + "  Please update profile to use MovementBy, instead.",
			//  
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

		#region Logic

		IEnumerator MainCoroutine()
		{
			// move to cannon.
			if (!Query.IsInVehicle())
			{
				var cannon = Vehicle;
				if (cannon == null)
				{
					if (Navigator.AtLocation(VehicleSearchLocation))
					{
						QBCLog.Fatal("No cannons found.");
						yield return false;
						yield break;
					}
					yield return UtilityCoroutine.MoveTo(VehicleSearchLocation, "Vehicle search area", MovementBy);
				}
				else if (!cannon.WithinInteractRange)
				{
					yield return UtilityCoroutine.MoveTo(cannon.Location, cannon.Name, MovementBy);
				}
				else
				{
					cannon.Interact();
				}
				yield return true;
				yield break;
			}

			while (!IsDone && Query.IsInVehicle())
			{
				// find the first weapon that is ready.
				var weapon = Weapons.FirstOrDefault(w => w.IsWeaponReady());
				if (weapon != null)
				{
					//_target = StyxWoW.Me.CurrentTarget;
					if (!Query.IsViable(_target) || !weapon.WeaponAim(_target))
					{
						// acquire a target that is within shooting range
						_target = Npcs.FirstOrDefault(n => weapon.WeaponAim(n));
						yield return StyxCoroutine.Sleep((int)Delay.BeforeButtonClick.TotalMilliseconds);
						yield break;
					}
					// fire away.
					if (Query.IsViable(_target) && weapon.WeaponAim(_target) && weapon.WeaponFire())
					{
						yield return StyxCoroutine.Sleep((int)Delay.AfterWeaponFire.TotalMilliseconds);
					}
				}
				yield return StyxCoroutine.EndTick;
			}
			yield return false;
		}

		public WoWUnit Vehicle
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>(true).Where(o => o.Entry == VehicleId).
					OrderBy(o => o.Location.DistanceSqr(VehicleSearchLocation)).FirstOrDefault();
			}
		}

		public IEnumerable<WoWUnit> Npcs
		{
			get
			{
				var killEverything = MobIds == null || !MobIds.Any();
				return ObjectManager.GetObjectsOfType<WoWUnit>(true).Where(o => o.IsAlive 
					&& (killEverything && o.Attackable && o.CanSelect && o.IsHostile || MobIds.Contains((int)o.Entry))).
					OrderBy(o => o.DistanceSqr);
			}
		}

		/// <summary>
		/// Casts the pet action.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <returns>Returns true if cast was successful; false otherwise</returns>
		bool CastPetAction(int index)
		{
			return Lua.GetReturnVal<bool>(string.Format("if GetPetActionCooldown({0}) ~= 0 then return false end CastPetAction({0}) return true", index), 0);
		}

		#endregion
	}
}