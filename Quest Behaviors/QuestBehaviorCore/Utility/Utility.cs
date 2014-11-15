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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
	public static class Utility
	{
		// 18Apr2013-10:41UTC chinajade
		private static void AntiAfk()
		{
			if (_afkTimer.IsFinished)
			{
				WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend, TimeSpan.FromMilliseconds(100));
				_afkTimer.Reset();
			}   
		}
		private static readonly WaitTimer _afkTimer = new WaitTimer(TimeSpan.FromMinutes(2));


		public static void BlacklistsReset()
		{
			Blacklist.Clear(blacklistEntry => { return (blacklistEntry.Flags & BlacklistFlags.Interact) != 0; });
		}

		public static IEnumerable<Frame> GetNpcFrames()
		{
			yield return AuctionFrame.Instance;
			yield return GossipFrame.Instance;
			yield return MailFrame.Instance;
			yield return MerchantFrame.Instance;
			yield return QuestFrame.Instance;
			yield return TaxiFrame.Instance;
			yield return TrainerFrame.Instance;
		}

		public static void CloseAllNpcFrames()
		{
			foreach (Frame frame in GetNpcFrames())
			{
				if (frame.IsVisible)
					frame.Close();
			}
		}


		// 15Jan2014-10:57UTC chinajade
		public static void ExitVehicle()
		{
			if (Query.IsInVehicle())
				{ Lua.DoString("VehicleExit()"); }
		}


        public static string GetDataFileFullPath(string fileName)
        {
            // NB: We use the absolute path here.  If we don't, then QBs get confused if there are additional
            // QBs supplied in the Honorbuddy/Default Profiles/. directory.
            return Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName),
                                GlobalSettings.Instance.QuestBehaviorsPath,
                                "QuestBehaviorCore",
                                "Data",
                                fileName);
        }
        
        
        // 20Apr2013-12:50UTC chinajade
		public static string GetItemNameFromId(int wowItemId)
		{
			var itemInfo = ItemInfo.FromId((uint)wowItemId);

			return (itemInfo != null)
				? itemInfo.Name
				: string.Format("ItemId({0})", wowItemId);
		}

		
		// 11Apr2013-04:41UTC chinajade
		public static string GetObjectNameFromId(int wowObjectId)
		{
			var wowObject =
				ObjectManager.GetObjectsOfType<WoWObject>(true, false)
				.FirstOrDefault(o => Query.IsViable(o) && (o.Entry == wowObjectId));

			return (wowObject != null)
                ? wowObject.SafeName
				: string.Format("MobId({0})", wowObjectId);
		}


		public static WoWPoint GetPointToGainDistance(WoWObject target, double minDistanceNeeded)
		{
			var minDistance = (float)(minDistanceNeeded + /*epsilon*/(2 * Navigator.PathPrecision));
			var myLocation = Me.Location;

			Func<WoWObject, WoWPoint, bool> isPointViable = (selectedTarget, potentialDestination) =>
			{
				var targetLocation = selectedTarget.Location;

				return
					targetLocation.Distance(potentialDestination) > minDistance
					&& (myLocation.Distance(potentialDestination) < targetLocation.Distance(potentialDestination))
					&& GameWorld.IsInLineOfSight(potentialDestination, targetLocation);
			};

			// If the previously calculated point is still viable, use it...
			if (isPointViable(target, _gainDistancePoint))
			{
				return _gainDistancePoint;
			}

			// Otherwise, find a new point...
			WoWObject moveTowardsObject = null;

			if (!(StyxWoW.Me.IsFlying || StyxWoW.Me.IsSwimming))
			{
				using (StyxWoW.Memory.AcquireFrame())
				{
					moveTowardsObject =
					   (from wowObject in ObjectManager.GetObjectsOfType<WoWObject>(true, false)
						where
							wowObject.IsValid
							&& isPointViable(target, wowObject.Location)
						orderby
							myLocation.PathTraversalCost(wowObject.Location)
						select wowObject)
						.FirstOrDefault();
				}
			}

			_gainDistancePoint =
				(moveTowardsObject != null)
					? moveTowardsObject.Location
					// Resort to brute force...
					: WoWMathHelper.CalculatePointFrom(myLocation, target.Location, minDistance);
  
			return _gainDistancePoint;
		}
		private static WoWPoint _gainDistancePoint;


		// 19Apr2013-05:58UTC chinajade
		public static string GetProfileName()
		{
			return ProfileManager.CurrentOuterProfile.Name ?? "UnknownProfile";
		}


		// 20Apr2013-01:23UTC chinajade
		public static string GetProfileReference(XElement xElement)
		{
			var location =
				((xElement != null) && ((IXmlLineInfo)xElement).HasLineInfo())
				? ("@line " + ((IXmlLineInfo)xElement).LineNumber.ToString())
				: "@unknown line";   
 
			return string.Format("[Ref: \"{0}\" {1}]", GetProfileName(), location);
		}


		// 15May2013-11:42UTC chinajade
		public static string GetSpellNameFromId(int spellId)
		{
			var wowSpell = WoWSpell.FromId(spellId);

			return (wowSpell != null)
				? wowSpell.Name
				: string.Format("SpellId({0})", spellId);
		}


		public static WoWObject LootableObject()
		{
			return
				(BotPoi.Current != null) && (BotPoi.Current.Type == PoiType.Loot)
				? BotPoi.Current.AsObject
				: null;
		}


		//  9Mar2013-12:34UTC chinajade
		public static string PrettyMoney(ulong totalCopper)
		{
			ulong moneyCopper = totalCopper % 100;
			totalCopper /= 100;

			ulong moneySilver = totalCopper % 100;
			totalCopper /= 100;

			ulong moneyGold = totalCopper;

			string formatString =
				(moneyGold > 0) ? "{0}g{1:D2}s{2:D2}c"
				: (moneySilver > 0) ? "{1}s{2:D2}c"
				: "{2}c";

			return string.Format(formatString, moneyGold, moneySilver, moneyCopper);
		}


		//  9Mar2013-12:34UTC chinajade
		public static string PrettyTime(TimeSpan duration, bool microwaveTime = true, bool truncateToSeconds = true)
		{
			string format = string.Empty;

			if (truncateToSeconds)
				{ duration = TimeSpan.FromSeconds((int)duration.TotalSeconds); }

			if ((int)duration.TotalMilliseconds == 0)
				{ return "0s"; }

			if (duration.TotalMinutes >= 100)
				{ format = "{0}h{1:D2}m{2:D2}s"; }
			else if (duration.TotalSeconds >= 100)
				{ format = "{4}m{2:D2}s"; }
			else
			{
				if (!microwaveTime)
					{ format = "{4}m{2:D2}s"; }
				else if (duration.TotalSeconds >= 1)
					{ format = "{5}s"; }
				else
					{ format = "0.{3:D3}s"; }
			}
			
			return string.Format(format,
				(int)duration.TotalHours, duration.Minutes, duration.Seconds, duration.Milliseconds,
				(int)duration.TotalMinutes, (int)duration.TotalSeconds);
		}


		// 30May2013-04:52UTC chinajade
		public static void Target(WoWObject wowObject, bool doFace = false, PoiType poiType = PoiType.None)
		{
			if (!Query.IsViable(wowObject))
				{ return; }

			if (doFace && !StyxWoW.Me.IsSafelyFacing(wowObject.Location))
				{ StyxWoW.Me.SetFacing(wowObject.Location); }

			var wowUnit = wowObject as WoWUnit;
			if (!Query.IsViable(wowUnit))
				{ return; }

			if (StyxWoW.Me.CurrentTargetGuid != wowUnit.Guid)
				{ wowUnit.Target(); }

			// Set POI as needed...
			if ((poiType != PoiType.None) && !Query.IsPoiMatch(wowObject, poiType))
			{
				// accept targets that are within targeting collection range.
				if (wowUnit.Distance < Targeting.CollectionRange)
					{ BotPoi.Current = new BotPoi(wowUnit, poiType); }

				else
				{
					//QBCLog.DeveloperInfo("Unable to set POI({0}, {1})--unit is beyond PullDistance({2:F1}) (saw: {3:F1}).",
					//  poiType, wowUnit.SafeName, CharacterSettings.Instance.PullDistance, wowUnit.Distance);
				}
			}
		}


		// 12Mar2013-08:27UTC chinajade
		public static IEnumerable<T> ToEnumerable<T>(T item)
		{
			yield return item;
		}


        public enum WowheadSubject
        {
            Item,
            Npc,
            Object,
            Quest,
            Spell,
            Zone,
        }
        public static string WowheadLink(WowheadSubject subject, int id, bool wrapWithParens = true)
        {
            Contract.Requires(id >= 0, (context) => "Id greater than or equal to zero");
            return string.Format("{0}http://wowhead.com/{1}={2}{3}",
                (wrapWithParens ? "(" : string.Empty),
                subject.ToString().ToLower(),
                id,
                (wrapWithParens ? ")" : string.Empty));
        }

		private static LocalPlayer Me { get { return StyxWoW.Me; } }
	}
}