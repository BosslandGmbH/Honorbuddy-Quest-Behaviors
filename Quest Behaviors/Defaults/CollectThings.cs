// Behavior originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//
// DOCUMENTATION:
//      http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_CollectThings
//     
// QUICK DOX:
//      Collects items from mobs or objects when (right-click) 'interaction' is required.
//      Most useful for those type of quests where you blow something up,
//      then you have to collect the pieces.  For example:
//          <!-- "Liberate the Kaja'mite": Blow up Kaja'mite, then collect results.
//              A single pass may or may not fulfill the quest quota, so we place it in a loop.
//            -->
//          <While Condition="HasQuest() &amp;&amp; !IsQuestComplete()">
//              <!-- throw the dynamite -->
//              <CustomBehavior File="UseItemOn" ... />
//              <!-- Note when collecting we must specify the Object(195492, Kaja'mite Chunk)
//                  to loot that contains the Item(48766, Kaja'mite Chunk) we want.  Yes,
//                  the Object and the Item have the same name, but different Ids.
//               -->
//              <CustomBehavior File="CollectThings" QuestId="14124"
//                  ObjectId="195492"
//                  CollectUntil="NoTargetsInArea" CollectItemId="48766" CollectionDistance="25" />
//          </While>
//
//      It can also be used to simply collect objects in an area:
//          <!-- "The Replacments": need to collect 6 "Replacement Parts".
//              Note that we have to specify the Objects(201603, Replacement Parts) which yield
//              the Items(49752, Replacement Parts) we want.  Yes, the Object and the Item have
//              the same name but different Ids.
//            -->
//          <CustomBehavior File="CollectThings" QuestId="24488"
//              ObjectId="201603"
//              CollectUntil="QuestComplete" CollectItemId="49752" CollectItemCount="6" CollectionDistance="1000"
//              X="-8426.145" Y="1178.538" Z="40.7015" />
//
//      Can also be used for quests where a simple (right-click) interaction is required.
//      In this case the 'thing' is "quest credit", and in this mode, this behavior
//      may overlap functionality provide by other behaviors.
//          <!-- "Good Help is Hard to Find": Need to collect 8 "Adjusted Attitudes".
//              Adjusted Attitudes don't go in the backpack, so we don't speify a
//              CollectItemId this time.  We depend soly on the quest
//              to tell us when its done.  For this type of use, if this is only
//              one part of the quest goals required for completion, you will need
//              to find another behavior to accomplish your quest.
//              Note that we specify the Mobs that yield the items we want.
//            -->
//          <CustomBehavior File="CollectThings" QuestId="14069"
//              MobId="34830"
//              CollectUntil="QuestComplete" CollectItemCount="8" />
//
//
//  Parameters (required, then optional--both listed alphabetically):
//      (***One or more of the following two attributes must be specified***)
//      MobIdN [REQUIRED if ObjectId is omitted]: Defines the mobs that drop the Items we're after.
//              N may be omitted, or any numeric value--multiple mobs are supported.
//      ObjectIdN [REQUIRED if MobId is omitted]: Defines the objects that drop the Items we're after.
//              N may be omitted, or any numeric value--mulitple objects are supported.
//
//      (This attribute is optional, but governs what other attributes are optional)
//      CollectUntil [Default: RequiredCountReached]: Defines the terminating condition for
//              this behavior.  Available options include:  NoTargetsInArea, RequiredCountReached, QuestComplete.
//              "Targets" means mobs or objects--whatever is dropping the items we're after.
//
//      (***These attributes may/may not be optional based on value of CollectUntil attribute***)
//      CollectItemCount [REQUIRED if CollectUntil=RequiredCountReached; Default: 1]:
//              represents the number of items we must collect for the behavior to terminate.
//      CollectItemId [REQUIRED if CollectUntil=NoTargetsInArea or RequiredCountReached; Default:none]:
//              Identifies the item we are collecting.  The only time this attribute may be omitted
//              is when we're collecting intangibles such as 'attitudes' or 'liberations' that
//              will complete the quest.
//      QuestId [REQUIRED if CollectUntil=QuestComplete; Default:none]:
//
//      (***These attibutes are completely optional***)
//      CollectionDistance [Default: 120]: The range from the anchor location (i.e., X/Y/Z) location at which
//              targets (mobs or objects) will be sought.
//      NonCompeteDistance [Default: 25]: If a player is within this distance of a target that looks
//              interesting to us, we'll ignore the target.  The assumption is that the player may
//              be going for the same target, and we don't want to draw attention.
//      QuestCompleteRequirement [Default:NotComplete]:
//      QuestInLogRequirement [Default:InLog]:
//              A full discussion of how the Quest* attributes operate is described in
//              http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
//      X/Y/Z [Default: Toon's initial position]: Defines the anchor of a search area for
//              which targets (mobs or objects) will be sought.  The hunting ground is defined by
//              this value coupled with the CollectionDistance.
// 
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using CommonBehaviors.Actions;

using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors.CollectThings
{
    public class CollectThings : CustomForcedBehavior
    {
        public enum CollectUntilType
        {
            NoTargetsInArea,
            RequiredCountReached,
            QuestComplete,
        }


        public CollectThings(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                bool    isCollectItemCountRequired  = false;
                bool    isCollectItemIdRequired     = false;
                bool    isQuestIdRequired           = false;

                CollectUntil = GetAttributeAsEnum<CollectUntilType>("CollectUntil", false, null) ?? CollectUntilType.RequiredCountReached;
                if (CollectUntil == CollectUntilType.NoTargetsInArea)
                    { isCollectItemIdRequired = true; }
                else if (CollectUntil == CollectUntilType.RequiredCountReached)
                {
                    isCollectItemCountRequired = true;
                    isCollectItemIdRequired = true;
                }
                else if (CollectUntil == CollectUntilType.QuestComplete)
                    { isQuestIdRequired = true; }

                CollectItemCount = GetAttributeAsInteger("CollectItemCount", isCollectItemCountRequired, 1, int.MaxValue, null) ?? 1;
                CollectItemId = GetAttributeAsItemId("CollectItemId", isCollectItemIdRequired, null) ?? 0;
                CollectionDistance = GetAttributeAsDouble("CollectionDistance", false, 1.0, 10000.0, null) ?? 120.0;
                SearchAreaAnchor   = GetXYZAttributeAsWoWPoint("", false, null) ?? Me.Location;
                MobIds      = GetNumberedAttributesAsIntegerArray("MobId", 0, 1, int.MaxValue, null) ?? new int[0];
                NonCompeteDistance = GetAttributeAsDouble("NonCompeteDistance", false, 1.0, double.MaxValue, null) ?? 25.0;
                ObjectIds   = GetNumberedAttributesAsIntegerArray("ObjectId", 0, 1, int.MaxValue, null) ?? new int[0];
                QuestId     = GetAttributeAsQuestId("QuestId", isQuestIdRequired, null) ?? 0;
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;


                // Semantic coherency --
                if ((MobIds.Count() <= 0)  &&  (ObjectIds.Count() <= 0))
                {
                    UtilLogMessage("error", "You must specify one or more MobId(s) or ObjectId(s)");
                    IsAttributeProblem = true;
                }

                if (CollectionDistance >= (NonCompeteDistance * 2))
                {
                    UtilLogMessage("error", "The CollectionDistance (saw '{0}') must be at least twice the size"
                                            + " of the NonCompeteDistance (saw '{1}').",
                                            CollectionDistance,
                                            NonCompeteDistance);
                    IsAttributeProblem = true;
                }


                // Find the item name --
                ItemInfo    info    = ItemInfo.FromId((uint)CollectItemId);

                _itemName = (info != null)  ? info.Name  : "Item";
			}

			catch (Exception except)
			{
				// Maintenance problems occur for a number of reasons.  The primary two are...
				// * Changes were made to the behavior, and boundary conditions weren't properly tested.
				// * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
				// In any case, we pinpoint the source of the problem area here, and hopefully it can be quickly
				// resolved.
				UtilLogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
										+ "\nFROM HERE:\n"
										+ except.StackTrace + "\n");
				IsAttributeProblem = true;
			}
        }

        // Attributes provided by caller
        public double                   CollectionDistance { get; private set; }
        public int                      CollectItemCount { get; private set; }
        public int                      CollectItemId { get; private set; }
        public CollectUntilType         CollectUntil { get; private set; }
        public int[]                    MobIds { get; private set; }
        public double                   NonCompeteDistance { get; private set; }
        public int[]                    ObjectIds { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }
        public WoWPoint                 SearchAreaAnchor { get; private set; }

        // Private variables for internal state
        private bool                    _isBehaviorDone                 = false;
        private readonly TimeSpan       _delay_BlackListExpiry          = TimeSpan.FromMinutes(5);
        private readonly TimeSpan       _delay_PlayerTooClose           = TimeSpan.FromSeconds(90);
        private readonly TimeSpan       _delay_RepopWait                = TimeSpan.FromMilliseconds(3000);
        private readonly TimeSpan       _delay_WoWClientMobInteract     = TimeSpan.FromMilliseconds(250);
        private readonly TimeSpan       _delay_WoWClientMoveDirectives  = TimeSpan.FromMilliseconds(100);
        private readonly string         _itemName                       = string.Empty;
        private WoWPoint                _searchAreaWaitPoint            = WoWPoint.Empty;

        // Private properties
        private int                     CollectedItemCount { get {
                                            return ((int)Me.BagItems
                                                        .Where(item => (item.ItemInfo.Id == CollectItemId))
                                                        .Sum(item => item.StackCount));
                                            }}
        private IEnumerable<WoWUnit>    LootList { get {
                                                return (ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                                                        .Where(u => u.Dead && u.Lootable));
                                            }}
        private LocalPlayer             Me { get { return (ObjectManager.Me); } }
        private IEnumerable<WoWUnit>    NearbyPlayers { get {
                                            return (ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                                                    .Where(u => u.IsPlayer &&  (u.Distance < (CollectionDistance + NonCompeteDistance))));                                                             
                                            }}
        private IEnumerable<WoWObject>  ViableTargets { get {
                                            return (ObjectManager.GetObjectsOfType<WoWObject>(true, false)
                                                    .Where(target => ((MobIds.Contains((int)target.Entry) || ObjectIds.Contains((int)target.Entry))
                                                                        && (target.Distance < CollectionDistance)
                                                                        && !target.IsLocallyBlacklisted()
                                                                        && !UtilBlacklistIfPlayerNearby(target)))
                                                    .OrderBy(target => Me.Location.SurfaceTravelDistance(target.Location)));
                                            }}

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string      SubversionId { get { return ("$Id$"); } }
        public override string      SubversionRevision { get { return ("$Revision$"); } }


        private void    UtilGuiShowProgress(string       completionReason)
        {
            TreeRoot.GoalText = string.Format("{0}: {1}/{2} {3}", this.GetType().Name, CollectedItemCount, CollectItemCount, _itemName);

            if (completionReason != null)
            {
                UtilLogMessage("info", "Behavior done (" + completionReason + ")");
                TreeRoot.GoalText = string.Empty;
            }
        }


        // If player is close to a target that is interesting to us, ignore the target...
        // The player may be going for the same mob, and we don't want to draw attention.
        // We'll blacklist the mob for a bit, in case the player is running around, or following
        // us.  The excaption is ithe player is in our party, then we can freely kill any target
        // close to him.
        private bool    UtilBlacklistIfPlayerNearby(WoWObject   target)
        {
            WoWUnit     nearestCompetingPlayer   = ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                                                    .OrderBy(player => player.Location.Distance(target.Location))
                                                    .FirstOrDefault(player => player.IsPlayer
                                                                                && player.IsAlive
                                                                                && !player.IsInOurParty());

            // If player is too close to the target, ignore target for a bit...
            if ((nearestCompetingPlayer != null)
                && (nearestCompetingPlayer.Location.Distance(target.Location) <= NonCompeteDistance))
            {
                target.LocallyBlacklist(_delay_PlayerTooClose);
                return (true);
            }

            return (false);
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return (
                new PrioritySelector(selectedTarget => ViableTargets.FirstOrDefault(),

                    // If behavior done, bail...
                    // Note that this is also an implicit "is quest complete" exit criteria, also.
                    new Decorator(ret => IsDone,
                        new Action(delegate { UtilGuiShowProgress("quest complete"); })),


                    // If we've exhausted mob/object supply in area, and that's our exit criteria, we're done...
                    new Decorator(
                        selectedTarget => ((CollectUntil == CollectUntilType.NoTargetsInArea) && (selectedTarget == null)),
                        new Action(delegate
                        {
                            UtilGuiShowProgress("No more objects/mobs in area");
                            _isBehaviorDone = true;
                        })),


                    // If we've filled our inventory quota, and that's our exit criteria, we're done...
                    new Decorator(
                        ret => ((CollectUntil == CollectUntilType.RequiredCountReached) && (CollectedItemCount >= CollectItemCount)),
                        new Action(delegate
                        {
                            UtilGuiShowProgress(string.Format("{0}/{1} items collected", CollectedItemCount, CollectItemCount));
                            _isBehaviorDone = true;
                        })),


                    // If there is loot to clean up, wait for it...
                    // This keeps it from taking a few steps towards next mob, only to go back to
                    // previous mob and loot it.  This technique can still fail if Honorbddy is slow to update
                    // infomation.  However, this shuts a lot of it down.
                    new Decorator(ret => (CharacterSettings.Instance.LootMobs && (LootList.Count() > 0)),
                        new ActionAlwaysSucceed()),


                    // If no targets in the area, move back to search area anchor --
                    new Decorator(selectedTarget => (selectedTarget == null),
                        new PrioritySelector(
                            // We find a point 'near' our anchor at which to wait.  This way, if multiple people are using the same
                            // profile at the same time, they won't be standing on top of each other.
                            new Decorator(ret => (_searchAreaWaitPoint == WoWPoint.Empty),
                                new Action(delegate { _searchAreaWaitPoint = SearchAreaAnchor.FanOutRandom(CollectionDistance * 0.25); })),

                            // Move to our selected random point...
                            new Decorator(ret => (Me.Location.Distance(_searchAreaWaitPoint) > Navigator.PathPrecision),
                                new Action(delegate { Navigator.MoveTo(_searchAreaWaitPoint); })),

                            // Tell user what's going on...
                            new Sequence(
                                new Action(delegate
                                    {
                                        UtilLogMessage("info", "No targets in area--waiting for repops (CollectionDistance={0}).", CollectionDistance);
                                        TreeRoot.GoalText = this.GetType().Name + ": Waiting for Repops";
                                    }),
                                new WaitContinueTimeSpan(_delay_RepopWait, ret => false, new ActionAlwaysSucceed()))
                        )),
                    

                    // Keep progress updated...
                    new Action(delegate
                        {
                            _searchAreaWaitPoint = WoWPoint.Empty;
                            UtilGuiShowProgress(null);
                            return (RunStatus.Failure);
                        }),


                    // If we're not at target, move to it...
                    new Decorator(selectedTarget => (((WoWObject)selectedTarget).Distance > ((WoWObject)selectedTarget).InteractRange),
                        new Sequence(
                            new Action(selectedTarget =>
                                {
                                    WoWObject   target = (WoWObject)selectedTarget;

                                    TreeRoot.StatusText = string.Format("Moving to target '{0}' at distance {1:0.0}",
                                                                        target.Name,
                                                                        target.Distance);
                                    Navigator.MoveTo(target.Location);
                                }),
                            new WaitContinueTimeSpan(_delay_WoWClientMoveDirectives, ret => false, new ActionAlwaysSucceed())
                        )),


                    // We're within interact range, collect the object...
                    new Sequence(
                        new Action(selectedTarget => {
                                WoWMovement.MoveStop();
                                ((WoWObject)selectedTarget).Interact();
                            }),
                        new WaitContinueTimeSpan(_delay_WoWClientMobInteract, ret => false, new ActionAlwaysSucceed()),
                        new Action(selectedTarget => ((WoWObject)selectedTarget).LocallyBlacklist(_delay_BlackListExpiry))
                        )
                )
            );
        }


        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone     // normal completion
                        || !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
            }
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
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = this.GetType().Name + ": " + ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");

                UtilGuiShowProgress(null);
            }
        }

        #endregion
    }


    #region Extensions to HBcore

    // The HBcore 'global' blacklist will also prevent looting.  We don't want that.
    // Since the HBcore blacklist is not built to instantiate, we have to roll our
    // own.  <sigh>
    public class LocalBlackList
    {
        public LocalBlackList(TimeSpan  maxSweepTime)
        {
            _maxSweepTime = maxSweepTime;
            _stopWatchForSweeping.Start();
        }

        private Dictionary<ulong, DateTime>     _blackList              = new Dictionary<ulong, DateTime>();
        private TimeSpan                        _maxSweepTime;
        private Stopwatch                       _stopWatchForSweeping   = new Stopwatch();      


        public void     Add(ulong guid,   TimeSpan timeSpan)
        {
            if (_stopWatchForSweeping.Elapsed > _maxSweepTime)
                { RemoveExpired(); }

            _blackList[guid] = DateTime.Now.Add(timeSpan);
        }


        public bool     Contains(ulong  guid)
        {
            if (_stopWatchForSweeping.Elapsed > _maxSweepTime)
                { RemoveExpired(); }

            return (_blackList.ContainsKey(guid));
        }


        public void     RemoveExpired()
        {
            DateTime    now     = DateTime.Now;

            List<ulong> expiredEntries  = (from key in _blackList.Keys
                                            where (_blackList[key] < now)
                                            select key).ToList();

            foreach (ulong entry in expiredEntries)
                { _blackList.Remove(entry); }

            _stopWatchForSweeping.Reset();
            _stopWatchForSweeping.Start();
        }
    }


    public static class WoWObject_Extensions
    {
        private static LocalPlayer      Me { get { return (ObjectManager.Me); } }

        // We provide our own 'local' blacklist.  If we use the global one maintained by HBcore,
        // that will prevent us from looting also.
        private static LocalBlackList   _blackList  = new LocalBlackList(TimeSpan.FromSeconds(30));

        public static void      LocallyBlacklist(this WoWObject    wowObject,
                                                 TimeSpan          timeSpan)
        {
            _blackList.Add(wowObject.Guid, timeSpan);
        }


        public static bool      IsLocallyBlacklisted(this WoWObject    wowObject)
        {
            return (_blackList.Contains(wowObject.Guid));
        }
    }


    // HBcore granularity on "WaitContinue" is seconds.  This one accepts a TimeSpan, thus
    // allow for much finer granularity.
    public class WaitContinueTimeSpan    : WaitContinue
    {
        public WaitContinueTimeSpan(TimeSpan timeOut, CanRunDecoratorDelegate decorator, Composite child)
            : base(1, decorator, child)
        {
            Timeout = timeOut;
        }
    }


    // HBcore granularity on "Wait" is seconds.  This one accepts a TimeSpan, thus
    // allow for much finer granularity.
    public class WaitTimeSpan    : Wait
    {
        public WaitTimeSpan(TimeSpan timeOut, CanRunDecoratorDelegate decorator, Composite child)
            : base(1, decorator, child)
        {
            Timeout = timeOut;
        }
    }


    public static class WoWUnit_Extensions
    {
        private static LocalPlayer      Me { get { return (ObjectManager.Me); } }

        public static bool      IsInOurParty(this WoWUnit  wowUnit)
        {
            return ((Me.PartyMembers.FirstOrDefault(partyMember => (partyMember.Guid == wowUnit.Guid))) != null);
        }
    }



    public static class WoWPoint_Extensions
    {
        public static Random            _random     = new Random((int)DateTime.Now.Ticks);

        private static LocalPlayer      Me { get { return (ObjectManager.Me); } }
        public const double             TAU         = (2 * Math.PI);    // See http://tauday.com/


        public static WoWPoint  Add(this WoWPoint   wowPoint,
                                    double          x,
                                    double          y,
                                    double          z)
        {
            return (new WoWPoint((wowPoint.X + x), (wowPoint.Y + y), (wowPoint.Z + z)));
        }


        public static WoWPoint  AddPolarXY(this WoWPoint  wowPoint,
                                           double         xyHeadingInRadians,
                                           double         distance,
                                           double         zModifier)
        {
            return (wowPoint.Add((Math.Cos(xyHeadingInRadians) * distance),
                                 (Math.Sin(xyHeadingInRadians) * distance),
                                 zModifier));
        }


        // Finds another point near the destination.  Useful when toon is 'waiting' for something
        // (e.g., boat, mob repops, etc). This allows multiple people running
        // the same profile to not stand on top of each other while waiting for
        // something.
        public static WoWPoint     FanOutRandom(this WoWPoint  location,
                                                double         maxRadius)
        {
            const int           CYLINDER_LINE_COUNT     =  12;
            const int           MAX_TRIES               =  50;
            const double        SAFE_DISTANCE_BUFFER    =   1.75;

            WoWPoint            candidateDestination  = location;
            int                 tryCount;

            // Most of the time we'll find a viable spot in less than 2 tries...
            // However, if you're standing on a pier, or small platform a
            // viable alternative may take 10-15 tries--its all up to the
            // random number generator.
            for (tryCount = MAX_TRIES;   tryCount > 0;    --tryCount)
            {
                WoWPoint        circlePoint;
                bool[]          hitResults;
                WoWPoint[]      hitPoints;
                int             index;
                WorldLine[]     traceLines  = new WorldLine[CYLINDER_LINE_COUNT +1];

                candidateDestination = location.AddPolarXY((TAU * _random.NextDouble()),  (maxRadius * _random.NextDouble()),  0.0);

                // Build set of tracelines that can evaluate the candidate destination --
                // We build a cone of lines with the cone's base at the destination's 'feet',
                // and the cone's point at maxRadius over the destination's 'head'.  We also
                // include the cone 'normal' as the first entry.

                // 'Normal' vector
                index = 0;
                traceLines[index].Start = candidateDestination.Add(0.0, 0.0, maxRadius);
                traceLines[index].End   = candidateDestination.Add(0.0, 0.0, -maxRadius);

                // Cylinder vectors
                for (double turnFraction = 0.0;    turnFraction < TAU;    turnFraction += (TAU / CYLINDER_LINE_COUNT))
                {
                    ++index;
                    circlePoint = candidateDestination.AddPolarXY(turnFraction, SAFE_DISTANCE_BUFFER, 0.0);
                    traceLines[index].Start = circlePoint.Add(0.0, 0.0, maxRadius);
                    traceLines[index].End   = circlePoint.Add(0.0, 0.0, -maxRadius);
                }
                

                // Evaluate the cylinder...
                // The result for the 'normal' vector (first one) will be the location where the
                // destination meets the ground.  Before this MassTrace, only the candidateDestination's
                // X/Y values were valid.
                GameWorld.MassTraceLine(traceLines.ToArray(),
                                        GameWorld.CGWorldFrameHitFlags.HitTestGroundAndStructures,
                                        out hitResults,
                                        out hitPoints);

                candidateDestination = hitPoints[0];    // From 'normal', Destination with valid Z coordinate


                // Sanity check...
                // We don't want to be standing right on the edge of a drop-off (say we'e on
                // a plaform or pier).  If there is not solid ground all around us, we reject
                // the candidate.  Our test for validity is that the walking distance must
                // not be more than 20% greater than the straight-line distance to the point.
                int     viableVectorCount = hitPoints.Sum(point => ((Me.Location.SurfaceTravelDistance(point) < (Me.Location.Distance(point) * 1.20))
                                                                      ? 1
                                                                      : 0));

                if (viableVectorCount < (CYLINDER_LINE_COUNT +1))
                    { continue; }

                // If new destination is 'too close' to our current position, try again...
                if (Me.Location.Distance(candidateDestination) <= SAFE_DISTANCE_BUFFER)
                    { continue; }

                break;
            }

            // If we exhausted our tries, just go with simple destination --
            if (tryCount <= 0)
                { candidateDestination = location; }
            
            return (candidateDestination);
        }


        public static double    SurfaceTravelDistance(this WoWPoint     start,
                                                      WoWPoint          destination)
        {
            WoWPoint[]  groundPath = Navigator.GeneratePath(start, destination) ?? new WoWPoint[0];

            // We define an invalid path to be of 'infinite' length
            if (groundPath.Length <= 0)
                { return (double.MaxValue); }

 
            double      pathDistance = start.Distance(groundPath[0]);

            for (int i = 0;    i < (groundPath.Length - 1);    ++i)
                { pathDistance += groundPath[i].Distance(groundPath[i+1]); }

            return (pathDistance);
        }
    }

    #endregion      // Extensions to HBcore
}

