// Behavior originally contributed by BarryDurex
// [Quest Behavior] MrFishIt - v1.0.1
//
// Credits: [Bot]MrFishIt by Nesox | [Bot]PoolFishingBuddy by Iggi66
//
// DOCUMENTATION: http://www.thebuddyforum.com/honorbuddy-forum/submitted-profiles/neutral/96244-quest-behavior-mrfishit-fishing-some-questitems.html
//     
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.Plugins;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using CommonBehaviors.Actions;
using Styx.Common;
using Styx.CommonBot.Frames;
using System.Windows.Media;
using Styx.WoWInternals.World;


namespace Styx.Bot.Quest_Behaviors.FishinItem
{
    class MrFishIt : CustomForcedBehavior
    {
        public MrFishIt(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                WaterPoint = GetAttributeAsNullable<WoWPoint>("", false, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
                PoolId = GetAttributeAsNullable<int>("PoolId", false, ConstrainAs.ItemId, null) ?? 0;
                CollectItemId = GetAttributeAsNullable<int>("CollectItemId", true, ConstrainAs.ItemId, null) ?? 0;
                CollectItemCount = GetAttributeAsNullable<int>("CollectItemCount", false, ConstrainAs.RepeatCount, null) ?? 1;
                MoveToPool = GetAttributeAsNullable<bool>("MoveToPool", false, null, null) ?? true;
                PoolFishingBuddy.MaxCastRange = GetAttributeAsNullable<int>("MaxCastRange", false, ConstrainAs.ItemId, null) ?? 20;
                PoolFishingBuddy.MinCastRange = GetAttributeAsNullable<int>("MinCastRange", false, ConstrainAs.ItemId, null) ?? 15;
                QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;

            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
                                    + "\nFROM HERE:\n"
                                    + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }


        // Attributes provided by caller
        public WoWPoint WaterPoint { get; private set; }
        public int CollectItemId { get; private set; }
        public static int PoolId { get; private set; }
        public int CollectItemCount { get; private set; }
        public bool MoveToPool { get; private set; }
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }

        // Private variables for internal state
        private Version _Version { get { return new Version(1, 0, 1); } }
        public static double _PoolGUID;
        private ConfigMemento _configMemento;
        private bool _isDisposed, _cancelBehavior;
        private Composite _root;


        ~MrFishIt()
        {
            Dispose(false);
        }


        public void Dispose(bool isExplicitlyInitiatedDispose)
        {
            if (!_isDisposed)
            {
                // NOTE: we should call any Dispose() method for any managed or unmanaged
                // resource, if that resource provides a Dispose() method.

                // Clean up managed resources, if explicit disposal...
                if (isExplicitlyInitiatedDispose)
                {
                    // empty, for now
                }

                // Clean up unmanaged resources (if any) here...
                if (_configMemento != null)
                { _configMemento.Dispose(); }

                _configMemento = null;

                BotEvents.OnBotStop -= BotEvents_OnBotStop;
                Lua.Events.DetachEvent("LOOT_OPENED", HandleLootOpened);
                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                if (StyxWoW.Me.IsCasting)
                    SpellManager.StopCasting();

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }


        public void BotEvents_OnBotStop(EventArgs args)
        {
            Dispose();
        }


        #region Overrides of CustomForcedBehavior

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        public override bool IsDone
        {
            get
            {
                return ((StyxWoW.Me.BagItems.FirstOrDefault(i => i.Entry == CollectItemId && i.StackCount == CollectItemCount) != null)     // normal completion
                        || !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete) || _cancelBehavior);
            }
        }

        static public WoWItem IteminBag(uint entry)
        {
            return StyxWoW.Me.BagItems.FirstOrDefault(i => i.Entry == entry);
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
                // The ConfigMemento() class captures the user's existing configuration.
                // After its captured, we can change the configuration however needed.
                // When the memento is dispose'd, the user's original configuration is restored.
                // More info about how the ConfigMemento applies to saving and restoring user configuration
                // can be found here...
                //     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_Saving_and_Restoring_User_Configuration
                _configMemento = new ConfigMemento();

                BotEvents.OnBotStop += BotEvents_OnBotStop; 
                Lua.Events.AttachEvent("LOOT_OPENED", HandleLootOpened);

                // Disable any settings that may cause us to dismount --
                // When we mount for travel via FlyTo, we don't want to be distracted by other things.
                // We also set PullDistance to its minimum value.  If we don't do this, HB will try
                // to dismount and engage a mob if it is within its normal PullDistance.
                // NOTE: these settings are restored to their normal values when the behavior completes
                // or the bot is stopped.
                CharacterSettings.Instance.HarvestHerbs = false;
                CharacterSettings.Instance.HarvestMinerals = false;
                CharacterSettings.Instance.LootChests = false;
                CharacterSettings.Instance.LootMobs = false;
                CharacterSettings.Instance.NinjaSkin = false;
                CharacterSettings.Instance.SkinMobs = false;
                CharacterSettings.Instance.PullDistance = 1;

                // Make sure we don't get logged out
                GlobalSettings.Instance.LogoutForInactivity = false;

                TreeRoot.GoalText = "[MrFishIt][v" + _Version.ToString() + "] Fishing for Item " + CollectItemId;

                Logging.WriteDiagnostic(Colors.Green, "[MrFishIt][v{0}] Fishing for Item: {1} - Quantity: {2}.", _Version.ToString(), CollectItemId, CollectItemCount);
            }
        }

        public static bool hasPoolFound
        {
            get
            {
                ObjectManager.Update();
                WoWGameObject _pool = (from unit in ObjectManager.GetObjectsOfType<WoWGameObject>(true, true)
                                       orderby unit.Distance ascending

                                       where !Blacklist.Contains(unit, BlacklistFlags.All)
                                       where unit.IsValid
                                       where unit.Entry == PoolId
                                       select unit).FirstOrDefault();

                if (_pool != null)
                {
                    //Logging.WriteDiagnostic(DateTime.Now.ToLongTimeString() + " - hasPoolFound - set " + _pool.Guid.ToString() + " - " + _pool.Name + " - " + _pool.Distance2D);
                    if (_PoolGUID != (double)_pool.Guid)
                    {
                        PoolFishingBuddy.looking4NewLoc = true;
                        _PoolGUID = (double)_pool.Guid;
                    }
                    return true;
                }
                _PoolGUID = -1;
                return false;
            }
        }

        protected override TreeSharp.Composite CreateBehavior()
        {
            return _root ?? (_root =
                new Decorator(ret => !IsDone && !StyxWoW.Me.Combat && !StyxWoW.Me.IsDead && !StyxWoW.Me.IsGhost,
                    new PrioritySelector(

                        // Have we a facing waterpoint or a PoolId and PoolGUID? No, then cancel this behavior!
                        new Decorator(ret => WaterPoint == WoWPoint.Empty && (PoolId == 0 || _PoolGUID == -1),
                            new Action(ret => _cancelBehavior = true)),

                        new Decorator(ret => (!Flightor.MountHelper.Mounted || !StyxWoW.Me.IsFlying) && (WaterPoint != WoWPoint.Empty ||
                            (PoolId != 0 && hasPoolFound && PoolFishingBuddy.saveLocation.Count > 0 &&
                            StyxWoW.Me.Location.Distance(PoolFishingBuddy.saveLocation[0]) <= 2.5 && !PoolFishingBuddy.looking4NewLoc)),
                            CreateFishBehavior()
                            ),


                        PoolFishingBuddy.CreateMoveToPoolBehavior()

                        //// Do we need to looking for pool?
                        //new Decorator(ret => PoolId != 0 && MoveToPool && (!_PoolGUID.IsValid() || _PoolGUID > 0),
                        //    new Sequence(
                        //        new Action(ret => Logging.WriteDiagnostic("need looking - " + _PoolGUID.asWoWGameObject().Distance2D)),
                        //        new Decorator(ret => _PoolGUID > 0 && (Flightor.MountHelper.Mounted || _PoolGUID.asWoWGameObject().Distance2D < PoolFishingBuddy.MinCastRange || 
                        //            _PoolGUID.asWoWGameObject().Distance2D > PoolFishingBuddy.MaxCastRange),
                        //            PoolFishingBuddy.CreateMoveToPoolBehavior())))

                        //new Decorator(ret => WaterPoint != WoWPoint.Empty || (PoolId != 0 && _PoolGUID > 0),
                            
                    )));
        }
        
        /// <summary>
        /// This is meant to replace the 'SleepForLagDuration()' method. Should only be used in a Sequence
        /// </summary>
        /// <returns></returns>
        public static Composite CreateWaitForLagDuration()
        {
            return new WaitContinue(TimeSpan.FromMilliseconds((StyxWoW.WoWClient.Latency * 2) + 150), ret => false, new ActionAlwaysSucceed());
        }

        private void HandleLootOpened(object sender, LuaEventArgs args)
        {
            object[] arg = args.Args;
            if (arg[0] == "0")
                Lua.DoString("for i=1, GetNumLootItems() do LootSlot(i) ConfirmBindOnUse() end CloseLoot()");
        }

        private Composite CreateFishBehavior()
        {
            return new PrioritySelector(

                // Don't do anything if the global cooldown timer is running
                new Decorator(ret => SpellManager.GlobalCooldown,
                    new ActionIdle()),

                // Do we need to interact with the bobber?
                new Decorator(ret => Fishing.IsBobberBobbing,
                    new Sequence(

                        // Interact with the bobber
                        new Action(delegate
                        {
                            Logging.Write(Colors.Aqua, "[MrFishIt] Got a bite!");
                            WoWGameObject bobber = Fishing.FishingBobber;

                            if (bobber != null)
                                bobber.Interact();

                            else
                                return RunStatus.Failure;

                            return RunStatus.Success;
                        }),

                        // Wait for the lootframe
                        new Wait(5, ret => LootFrame.Instance.IsVisible,
                            new Sequence(
                                new Action(ret => TreeRoot.StatusText = "[MrFishIt] Looting ..."),
                                new Action(ret => StyxWoW.SleepForLagDuration())
                                ))
                            )),

                // Do we need to recast?
                new Decorator(ret => /*Fishing.FishingPole != null &&*/ !Fishing.IsFishing || (PoolId != 0 && !PoolFishingBuddy.BobberIsInTheHole),
                    new Sequence(
                        new Action(ret => Logging.Write(Colors.Aquamarine, "[MrFishIt] Casting ...")),
                        new Action(ret => { if (WaterPoint != WoWPoint.Empty) { StyxWoW.Me.SetFacing(WaterPoint); Thread.Sleep(200); } }),
                        new Action(ret => { if (PoolId != 0) { StyxWoW.Me.SetFacing(_PoolGUID.asWoWGameObject()); Thread.Sleep(200); } }),
                        new Action(ret => SpellManager.Cast("Fishing")),
                        new Wait(5, ret => !StyxWoW.Me.IsCasting, new ActionIdle()),
                        CreateWaitForLagDuration()
                        )),

                new Sequence(
                    new Action(ret => TreeRoot.StatusText = "[MrFishIt] Waiting for bobber to splash ..."),
                    new ActionIdle()
                    ));
        }

        #endregion
    }
    static class Fishing
    {
        //static readonly List<int> FishingIds = new List<int> { 131474, 7620, 7731, 7732, 18248, 33095, 51294, 88868 };
        /// <summary>
        /// Returns true if you are fishing
        /// </summary>
        public static bool IsFishing { get { return /*FishingIds.Contains(StyxWoW.Me.ChanneledCastingSpellId);*/ StyxWoW.Me.IsCasting; } }

        /// <summary>
        /// Returns your fishing pole
        /// </summary>
        public static WoWItem FishingPole
        { get { return ObjectManager.GetObjectsOfType<WoWItem>().Where(b => b.IsFishingPole()).FirstOrDefault(); } }

        /// <summary>
        /// Returns true if you have a temp-enchantm on your pole
        /// </summary>
        public static bool GotLure { get { return Lua.GetReturnVal<bool>("return GetWeaponEnchantInfo()", 0); } }

        /// <summary>
        /// Returns true if the fishing bobber is bobbing
        /// </summary>
        public static bool IsBobberBobbing { get { return FishingBobber != null && FishingBobber.IsBobbing(); } }

        /// <summary>
        /// Returns the current fishing bobber in use, null otherwise
        /// </summary>
        public static WoWGameObject FishingBobber
        {
            get
            {
                ObjectManager.Update();
                return ObjectManager.GetObjectsOfType<WoWGameObject>()
                    .FirstOrDefault(o => o != null && o.IsValid && o.CreatedByGuid == StyxWoW.Me.Guid &&
                        o.SubType == WoWGameObjectType.FishingNode);
            }
        }
    }

    static class Extensions
    {
        static readonly List<uint> PoleIds = new List<uint> { 44050, 19970, 45991, 45992, 45858, 19022, 25978, 6367, 12225, 6366, 6256, 6365 };

        public static bool IsFishingPole(this WoWItem value)
        {
            if (value == null)
                return false;

            return PoleIds.Contains(value.Entry);
        }

        public static bool IsBobbing(this WoWGameObject value)
        {
            if (value == null || value.SubType != WoWGameObjectType.FishingNode)
                return false;

            //return ((WoWFishingBobber)value.SubObj).IsBobbing;

            return null != Fishing.FishingBobber ? 1 == Fishing.FishingBobber.AnimationState : false;
        }

        public static WoWGameObject asWoWGameObject(this double GUID)
        {
            ObjectManager.Update();
            WoWGameObject _o = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(o => o.Guid == GUID);
            //if (_o == null)
            //    Logging.WriteDiagnostic("{0} - asWoWGameObject - null", DateTime.Now.ToLongTimeString());
            //else
            //    Logging.WriteDiagnostic(DateTime.Now.ToLongTimeString() + " - asWoWGameObject - " + _o.Guid.ToString() + " - " + _o.Name + " - " + _o.Distance2D);
            return _o;
        }
    }

    static class PoolFishingBuddy
    {
        private static Stopwatch movetopoolTimer = new Stopwatch();
        public static bool looking4NewLoc;
        private static WoWGameObject Pool { get { if (MrFishIt.hasPoolFound) { } return MrFishIt._PoolGUID.asWoWGameObject(); } }
        static public List<WoWPoint> saveLocation = new List<WoWPoint>(100);
        static public List<WoWPoint> badLocations = new List<WoWPoint>(100);
        static public int newLocAttempts = 0;

        private static int MaxNewLocAttempts = 5;
        public static int MaxCastRange { get; set; }
        public static int MinCastRange { get; set; }

        /// <summary>
        /// fixed by BarryDurex
        /// </summary>
        static public WoWPoint getSaveLocation(WoWPoint Location, int minDist, int maxDist, int traceStep, int traceStep2)
        {
            Logging.WriteDiagnostic("[MrFishIt] - Navigation: Looking for save Location around {0}.", Location);

            WoWPoint point = WoWPoint.Empty;
            float _PIx2 = 3.14159f * 2f;

            for (int i = 0, x = minDist; i < traceStep && x < maxDist && looking4NewLoc == true; i++)
            {
                WoWPoint p = Location.RayCast((i * _PIx2) / traceStep, x);

                p.Z = getGroundZ(p);
                WoWPoint pLoS = p;
                pLoS.Z = p.Z + 0.5f;

                if (p.Z != float.MinValue && !badLocations.Contains(p) && StyxWoW.Me.Location.Distance(p) > 1)
                {
                    if (getHighestSurroundingSlope(p) < 1.2f && GameWorld.IsInLineOfSight(pLoS, Location))
                    {
                        point = p;
                        break;
                    }
                }

                if (i == (traceStep - 1))
                {
                    i = 0;
                    x++;
                }
            }

            for (int i = 0, x = 10; i < traceStep2 && x < maxDist && looking4NewLoc == true; i++)
            {
                WoWPoint p2 = point.RayCast((i * _PIx2) / traceStep2, x);

                p2.Z = getGroundZ(p2);
                WoWPoint pLoS = p2;
                pLoS.Z = p2.Z + 0.5f;

                if (p2.Z != float.MinValue && !badLocations.Contains(p2) && StyxWoW.Me.Location.Distance(p2) > 1)
                {
                    if (getHighestSurroundingSlope(p2) < 1.2f && GameWorld.IsInLineOfSight(pLoS, Location) && p2.Distance2D(Location) <= maxDist)
                    {
                        looking4NewLoc = false;
                        Logging.WriteDiagnostic("[MrFishIt] - Navigation: Moving to {0}. Distance: {1}", p2, Location.Distance(p2));
                        return p2;
                    }
                }

                if (i == (traceStep2 - 1))
                {
                    i = 0;
                    x++;
                }
            }

            Logging.Write(Colors.Red, "{0}[MrFishIt] - No valid points returned by RayCast, blacklisting for 2 minutes.", DateTime.Now.ToLongTimeString());
            Blacklist.Add(Pool, BlacklistFlags.All, TimeSpan.FromMinutes(2));
            return WoWPoint.Empty;
        }

        static public WoWPoint getSaveLocation(WoWPoint Location, int minDist, int maxDist, int traceStep)
        {
            Logging.WriteDiagnostic("[MrFishIt] - Navigation: Looking for save Location around {0}.", Location);

            float _PIx2 = 3.14159f * 2f;

            for (int i = 0, x = minDist; i < traceStep && x < maxDist && looking4NewLoc == true; i++)
            {
                WoWPoint p = Location.RayCast((i * _PIx2) / traceStep, x);

                p.Z = getGroundZ(p);
                WoWPoint pLoS = p;
                pLoS.Z = p.Z + 0.5f;

                if (p.Z != float.MinValue && !badLocations.Contains(p) && StyxWoW.Me.Location.Distance(p) > 1)
                {
                    if (getHighestSurroundingSlope(p) < 1.2f && GameWorld.IsInLineOfSight(pLoS, Location) /*&& Navigator.CanNavigateFully(StyxWoW.Me.Location, Location)*/)
                    {
                        looking4NewLoc = false;
                        Logging.WriteDiagnostic("[MrFishIt] - Navigation: Moving to {0}. Distance: {1}", p, Location.Distance(p));
                        return p;
                    }
                }

                if (i == (traceStep - 1))
                {
                    i = 0;
                    x++;
                }
            }

            if (Pool != null)
            {
                Logging.Write(Colors.Red, "[MrFishIt] - No valid points returned by RayCast, blacklisting for 2 minutes.");
                Blacklist.Add(Pool, BlacklistFlags.All, TimeSpan.FromMinutes(2));
                return WoWPoint.Empty;
            }
            else
            {
                Logging.Write(Colors.Red, "[MrFishIt] - No valid points returned by RayCast, can't navigate without user interaction. Stopping!");
                TreeRoot.Stop();
                return WoWPoint.Empty;
            }
            
        }

        /// <summary>
        /// Credits to funkescott.
        /// </summary>
        /// <returns>Highest slope of surrounding terrain, returns 100 if the slope can't be determined</returns>
        public static float getHighestSurroundingSlope(WoWPoint p)
        {
            Logging.WriteDiagnostic("[MrFishIt] - Navigation: Sloapcheck on Point: {0}", p);
            float _PIx2 = 3.14159f * 2f;
            float highestSlope = -100;
            float slope = 0;
            int traceStep = 15;
            float range = 0.5f;
            WoWPoint p2;
            for (int i = 0; i < traceStep; i++)
            {
                p2 = p.RayCast((i * _PIx2) / traceStep, range);
                p2.Z = getGroundZ(p2);
                slope = Math.Abs( getSlope(p, p2) );
                if( slope > highestSlope )
                {
                    highestSlope = (float)slope;
                }
            }
            Logging.WriteDiagnostic("[MrFishIt] - Navigation: Highslope {0}", highestSlope);
            return Math.Abs( highestSlope );
        }

        /// <summary>
        /// Credits to funkescott.
        /// </summary>
        /// <param name="p1">from WoWPoint</param>
        /// <param name="p2">to WoWPoint</param>
        /// <returns>Return slope from WoWPoint to WoWPoint.</returns>
        public static float getSlope(WoWPoint p1, WoWPoint p2)
        {
            float rise = p2.Z - p1.Z;
            float run = (float)Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));

            return rise / run;
        }

        /// <summary>
        /// Credits to exemplar.
        /// </summary>
        /// <returns>Z-Coordinates for PoolPoints so we don't jump into the water.</returns>
        public static float getGroundZ(WoWPoint p)
        {
            WoWPoint ground = WoWPoint.Empty;

            GameWorld.TraceLine(new WoWPoint(p.X, p.Y, (p.Z + MaxCastRange)), new WoWPoint(p.X, p.Y, (p.Z - 0.8f)), GameWorld.CGWorldFrameHitFlags.HitTestGroundAndStructures/* | GameWorld.CGWorldFrameHitFlags.HitTestBoundingModels | GameWorld.CGWorldFrameHitFlags.HitTestWMO*/, out ground);

            if (ground != WoWPoint.Empty)
            {
                Logging.WriteDiagnostic("[MrFishIt] - Ground Z: {0}.", ground.Z);
                return ground.Z;
            }
            Logging.WriteDiagnostic("[MrFishIt] - Ground Z returned float.MinValue.");
            return float.MinValue;
        }

        public static  Composite CreateMoveToPoolBehavior()
        {
            return new Decorator(ret => Pool != null && !Blacklist.Contains(Pool, BlacklistFlags.All),
                new Sequence(
                    new Action(ret => Logging.WriteDiagnostic("[MrFishIt][PFB] - Composit: CreateMoveToPoolBehaviour")),
                    new Action(ret => movetopoolTimer.Start()),

                    new PrioritySelector(

                        // Timer
                        new Decorator(ret => movetopoolTimer.ElapsedMilliseconds > 30000,
                            new Sequence(
                                new Action(ret => Logging.Write(LogLevel.Diagnostic, Colors.Red, "[MrFishIt] - Timer for moving to ground elapsed, blacklisting for 2 minutes.")),
                                new Action(ret => Blacklist.Add(MrFishIt._PoolGUID.asWoWGameObject(), BlacklistFlags.All, TimeSpan.FromMinutes(2)))                                
                        )),

                        //// Blacklist if other Player is detected
                        //new Decorator(ret => Helpers.PlayerDetected && !PoolFisherSettings.Instance.NinjaPools,
                        //    new Sequence(
                        //            new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Detected another player in pool range, blacklisting for 2 minutes.", Helpers.LogPrefix)),
                        //            new Action(ret => Helpers.BlackListPool(Pool)),
                        //            new Action(delegate { return RunStatus.Success; })
                        //)),

                        // Get PoolPoint
                        new Decorator(ret => looking4NewLoc,
                            new Sequence(
                                new ActionSetActivity(ret => "[MrFishIt] Looking for valid Location"),
                                new Action(ret => WoWMovement.MoveStop()),
                                new PrioritySelector(

                                    // Pool ist Feuerteich 
                                    new Decorator(ret => Pool.Entry == 207734,
                                        new Sequence(
                                        new Action(ret => saveLocation.Add(getSaveLocation(Pool.Location, MinCastRange, 
                                            MaxCastRange, 50, 60))),
                                        new Action(ret => Logging.WriteDiagnostic(Colors.Green, "[MrFishIt] - Added {0} to saveLocations.", saveLocation[0]))
                                    )),

                                    // Pool ist kein Feuerteich 
                                    new Decorator(ret => Pool.Entry != 207734,
                                        new Sequence(
                                        new Action(ret => saveLocation.Add(getSaveLocation(Pool.Location, MinCastRange, 
                                            MaxCastRange, 50))),
                                        new Action(ret => Logging.WriteDiagnostic(Colors.Green, "[MrFishIt] - Added {0} to saveLocations.", saveLocation[0]))
                                    ))                                
                        ))),

                        // Move to PoolPoint
                        new Decorator(pool => Pool != null && saveLocation.Count > 0 && !looking4NewLoc,
                            new PrioritySelector(

                                // Pool still there?
                                new Decorator(ret => MrFishIt._PoolGUID.asWoWGameObject() == null,
                                    new Sequence(
                                        new Action(ret => Logging.Write(Colors.DarkCyan, "[MrFishIt] - Fishing Pool is gone, moving on."))
                                )),

                                // reached max attempts for new locations?
                                new Decorator(ret => newLocAttempts == MaxNewLocAttempts + 1,
                                    new Sequence(
                                    new Action(ret => Logging.Write(Colors.Red, "[MrFishIt] - Reached max. attempts for new locations, blacklisting for 2 minutes.")),
                                    new Action(ret => Blacklist.Add(Pool, BlacklistFlags.All, TimeSpan.FromMinutes(2)))
                                )),

                                // tries++
                                new Decorator(ret => StyxWoW.Me.Location.Distance(saveLocation[0]) <= 2 && !Flightor.MountHelper.Mounted && !StyxWoW.Me.IsMoving,
                                    new Sequence(
                                        new Wait(2, ret => StyxWoW.Me.IsCasting, new ActionIdle()),
                                        new Action(ret => newLocAttempts++),
                                        new Action(ret => Logging.Write(Colors.Red, "[MrFishIt] - Moving to new Location.. Attempt: {0} of {1}.", newLocAttempts, MaxNewLocAttempts))
                                )),
                                            

                                // Dismount
                                new Decorator(ret => StyxWoW.Me.Location.Distance(new WoWPoint(saveLocation[0].X, saveLocation[0].Y, saveLocation[0].Z + 2)) <= 1.5 && Flightor.MountHelper.Mounted, //&& !StyxWoW.Me.IsMoving,
                                    //new PrioritySelector(

                                        //new Decorator(ret => Helpers.CanWaterWalk && !Helpers.hasWaterWalking,
                                            new Sequence(
                                                new Action(ret => StyxWoW.Me.SetFacing(Pool.Location)),
                                                //new Action(ret => Helpers.WaterWalk()),
                                                //new Action(ret => Thread.Sleep((Ping * 2) + 500)),
                                                new Action(ret => WoWMovement.Move(WoWMovement.MovementDirection.Descend)),
                                                new Action(ret => Flightor.MountHelper.Dismount()),
                                                new Action(ret => WoWMovement.MoveStop()),
                                                new Action(ret => Logging.WriteDiagnostic(Colors.Red, "[MrFishIt] - Navigation: Dismount. Current Location {0}, PoolPoint: {1}, Distance: {2}", StyxWoW.Me.Location, new WoWPoint(saveLocation[0].X, saveLocation[0].Y, saveLocation[0].Z + 2), StyxWoW.Me.Location.Distance(new WoWPoint(saveLocation[0].X, saveLocation[0].Y, saveLocation[0].Z + 2)))),
                                                new Wait(3, ret => Flightor.MountHelper.Mounted, new ActionIdle()),
                                                MrFishIt.CreateWaitForLagDuration()

                                        //new Decorator(ret => !Helpers.CanWaterWalk || (Helpers.CanWaterWalk && Helpers.hasWaterWalking),
                                        //    new Sequence(
                                        //        new Action(ret => StyxWoW.Me.SetFacing(Pool.Location)),
                                        //        new Action(ret => WoWMovement.Move(WoWMovement.MovementDirection.Descend)),
                                        //        new Action(ret => Mount.Dismount()),
                                        //        new Action(ret => WoWMovement.MoveStop()),
                                        //        new Action(ret => Logging.WriteNavigator(System.Drawing.Color.Red, "{0} - Navigation: Dismount. Current Location {1}, PoolPoint: {2}, Distance: {3}", Helpers.LogPrefix, StyxWoW.Me.Location, new WoWPoint(saveLocation[0].X, saveLocation[0].Y, saveLocation[0].Z + 2), StyxWoW.Me.Location.Distance(new WoWPoint(saveLocation[0].X, saveLocation[0].Y, saveLocation[0].Z + 2)))),
                                        //        new Wait(3, ret => Flightor.MountHelper.Mounted, new ActionIdle()),
                                        //        new Action(ret => Thread.Sleep((Ping * 2) + 500))))
                                        
                                )),

                                // in Line Line of sight?
                                new Decorator(ret => StyxWoW.Me.Location.Distance(saveLocation[0]) <= 2 && !Pool.InLineOfSight && !StyxWoW.Me.IsMoving && !Flightor.MountHelper.Mounted,
                                    new Sequence(
                                    new Action(ret => Logging.Write(Colors.Red, "[MrFishIt] - Pool is not in Line of Sight!")),
                                    new Action(ret => badLocations.Add(saveLocation[0])),
                                    new Action(ret => saveLocation.Clear()),
                                    new Action(ret => newLocAttempts++),
                                    new Action(ret => Logging.Write(Colors.Red, "[MrFishIt] - Moving to new Location.. Attempt: {0} of {1}.", newLocAttempts, MaxNewLocAttempts)),
                                    new Action(ret => looking4NewLoc = true)
                                )),

                                // Move without Mount
                                new Decorator(ret => (StyxWoW.Me.Location.Distance(saveLocation[0]) > 1 && StyxWoW.Me.Location.Distance(saveLocation[0]) <= 10 && !Flightor.MountHelper.Mounted && GameWorld.IsInLineOfSight(StyxWoW.Me.Location, saveLocation[0])) && !StyxWoW.Me.IsSwimming,
                                    new PrioritySelector(

                                        // Mount if not mounted and Navigator is not able to generate a path
                                        new Decorator(ret => !Navigator.CanNavigateFully(StyxWoW.Me.Location, saveLocation[0]),
                                            new Action(ret => Flightor.MountHelper.MountUp())),

                                        new Sequence(
                                            new ActionSetActivity(ret => "Moving to new Location"),
                                            new Action(ret => Logging.WriteDiagnostic("[MrFishIt] - Navigation: Moving to Pool: " + saveLocation[0] + ", Location: " + StyxWoW.Me.Location + ", distance: " + saveLocation[0].Distance(StyxWoW.Me.Location) + " (Not Mounted)")),
                                            //new Action(ret => Logging.Write(System.Drawing.Color.DarkCyan, "{0} - Moving to Pool: {1}, Location: {2}, Distance: {3}. (Not Mounted)", Helpers.TimeNow, PoolPoints[0], StyxWoW.Me.Location, PoolPoints[0].Distance(StyxWoW.Me.Location))),
                                            // Move
                                            new Action(ret => Navigator.MoveTo(saveLocation[0]))
                                        )
                                )),

                                // Move with Mount
                                new Decorator(ret => (StyxWoW.Me.Location.Distance(saveLocation[0]) > 10 || Flightor.MountHelper.Mounted || (StyxWoW.Me.Location.Distance(saveLocation[0]) <= 10 && !GameWorld.IsInLineOfSight(StyxWoW.Me.Location, saveLocation[0])) && !StyxWoW.Me.IsSwimming),
                                    new PrioritySelector(

                                        // Mount if not mounted
                                        new Decorator(ret => !Flightor.MountHelper.Mounted && !StyxWoW.Me.Combat,
                                            new Action(ret => Flightor.MountHelper.MountUp())),

                                        // Move
                                        new Sequence(
                                            new ActionSetActivity(ret => "[MrFishIt] Moving to Ground"),
                                            new Action(ret => Logging.WriteDiagnostic("[MrFishIt] - Navigation: Moving to Pool: " + saveLocation[0] + ", Location: " + StyxWoW.Me.Location + ", distance: " + StyxWoW.Me.Location.Distance(new WoWPoint(saveLocation[0].X, saveLocation[0].Y, saveLocation[0].Z + 2)) + " (Mounted)")),
                                            //new Action(ret => Logging.Write(System.Drawing.Color.DarkCyan, "{0} - Moving to Pool: {1}, Location: {2}, Distance: {3}. (Mounted)", Helpers.TimeNow, PoolPoints[0], StyxWoW.Me.Location, PoolPoints[0].Distance(StyxWoW.Me.Location))),
                                            new Action(ret => Flightor.MoveTo(new WoWPoint(saveLocation[0].X, saveLocation[0].Y, saveLocation[0].Z + 2)))
                                        )
                                ))
                            ))
                )));
        }

        /// <summary>
        /// Checks if the bobber is in distance of 3.6 to location of the pool.
        /// </summary>
        static public bool BobberIsInTheHole
        {
            get
            {
                //Thread.Sleep(TimeSpan.FromMilliseconds((StyxWoW.WoWClient.Latency * 2) + 150));
                Thread.Sleep(300);
                if (Fishing.FishingBobber != null && Pool != null)
                {
                    if (Fishing.FishingBobber.Location.Distance2D(Pool.Location) <= 3.6f)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
