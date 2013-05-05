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

using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public abstract partial class QuestBehaviorBase
    {
        /// <summary>
        /// Descends to a height safe for dismounting.  "Safe" is defined as 'not flying, or no more than
        /// MAXDISMOUNTHEIGHTDELEGATE above the ground.
        /// <para>Notes:<list type="bullet">
        /// <item><description><para> * If MAXDISMOUNTHEIGHTDELEGATE is not provided, a suitable value
        /// is used.</para></description></item>
        /// </list></para>
        /// </summary>
        /// <returns></returns>
        /// <remarks>17Apr2013-01:44UTC chinajade</remarks>
        public Composite UtilityBehaviorPS_DescendForDismount(ProvideDoubleDelegate maxDismountHeightDelegate = null)
        {
            maxDismountHeightDelegate = maxDismountHeightDelegate ?? (context => 8.0);

            Func<object, bool> isReadyToDismount = (context =>
            {
                return !Me.IsFlying
                        || (Me.GetTraceLinePos().HeightOverGroundOrWater() < maxDismountHeightDelegate(context));
            });

            return new PrioritySelector(
                // Descend, if needed...
                new Decorator(context => !isReadyToDismount(context),
                    new PrioritySelector(
                        new Decorator(context => !Me.MovementInfo.IsDescending,
                            new Action(context =>
                            {
                                TreeRoot.StatusText = "Descending before dismount";
                                WoWMovement.Move(WoWMovement.MovementDirection.Descend);
                            })),
                        new Action(context =>
                        {
                            TreeRoot.StatusText = string.Format("Descending from {0:F1}", 
                                Me.GetTraceLinePos().HeightOverGroundOrWater());
                        })
                    )),

                // Stop descending...
                new Decorator(context => Me.MovementInfo.IsDescending,
                    new Action(context =>
                    {
                        TreeRoot.StatusText = "Descent Stopped";
                        WoWMovement.MoveStop(WoWMovement.MovementDirection.Descend);
                    }))
            );
        }


        /// <summary>
        /// Mounts or Dismounts according to the provided MOUNTSTRATEGYDELEGATE.
        /// <para>Notes:<list type="bullet">
        /// <item><description><para> * A "CancelShapeshift" will cancel _any_ shapeshift form whether or not
        /// that form represents a 'mounted' form or not.</para></description></item>
        /// <item><description><para> * A "Dismount" will unmount, or cancel a 'mounted' shapeshift form.
        /// Examples of the latter include: Druid Flight Form, Druid Travel Form, Shaman Ghost Wolf, Worgen Running Wild.
        /// </para></description></item>
        /// <item><description><para> * Requests to "Mount" will only be honored if the area allows it.</para></description></item>
        /// </list></para>
        /// </summary>
        /// <param name="mountStrategyDelegate"></param>
        /// <returns></returns>
        /// <remarks>17Apr2013-03:11UTC chinajade</remarks>
        public Composite UtilityBehaviorPS_ExecuteMountStrategy(Func<object, MountStrategyType> mountStrategyDelegate)
        {
            ContractRequires(mountStrategyDelegate != null, context => "mountStrategyDelegate != null");

            return new Decorator(context => mountStrategyDelegate(context) != MountStrategyType.None,
                new PrioritySelector(
                    new Decorator(context => Me.IsShapeshifted()
                                            && ((mountStrategyDelegate(context) == MountStrategyType.CancelShapeshift)
                                                || (mountStrategyDelegate(context) == MountStrategyType.DismountOrCancelShapeshift)), 
                        new PrioritySelector(
                            UtilityBehaviorPS_DescendForDismount(context => MaxDismountHeight),
                            new Action(context =>
                            {
                                TreeRoot.StatusText = "Canceling shapeshift form.";
                                Lua.DoString("CancelShapeshiftForm()");
                            })
                        )),

                    new Decorator(context => Me.IsMounted()
                                            && ((mountStrategyDelegate(context) == MountStrategyType.Dismount)
                                                || (mountStrategyDelegate(context) == MountStrategyType.DismountOrCancelShapeshift)),
                        new PrioritySelector(
                            UtilityBehaviorPS_DescendForDismount(context => MaxDismountHeight),
                            new Decorator(context => Me.IsShapeshifted(),
                                new Action(context =>
                                {
                                    TreeRoot.StatusText = "Canceling 'mounted' shapeshift form.";
                                    Lua.DoString("CancelShapeshiftForm()");
                                })),
                            new Decorator(context => Me.IsMounted(),
                                new Action(context =>
                                {
                                    TreeRoot.StatusText = "Dismounting";

                                    // Mount.Dismount() uses the Flightor landing system, which sometimes get stuck
                                    // a yard or two above the landing zone...
                                    // So, we opt to dismount via LUA since we've controlled the landing ourselves.
                                    Lua.DoString("Dismount()");
                                }))
                            )),
                        
                    new Decorator(context => !Me.IsMounted()
                                            && (mountStrategyDelegate(context) == MountStrategyType.Mount)
                                            && Mount.CanMount(),
                        // We make up a destination for MountUp() that is far enough away, it will always choose to mount...
                        new Action(context =>
                        {
                            TreeRoot.StatusText = "Mounting";
                            Mount.MountUp(() => Me.Location.Add(1000.0, 1000.0, 1000.0));
                        }))
            ));
        }

        
        // 29Apr2013-05:20UTC chinajade
        public Composite UtilityBehaviorPS_FaceMob(ProvideWoWObjectDelegate wowObjectDelegate)
        {
            ContractRequires(wowObjectDelegate != null, context => "wowObjectDelegate != null");

            return new Decorator(context => !MovementObserver.IsSafelyFacing(wowObjectDelegate(context)),
                new Action(context => { Me.SetFacing(wowObjectDelegate(context).Location); }));
        }
        
        
        /// <summary>
        /// This behavior quits attacking the mob, once the mob is targeting us.
        /// </summary>
        // 24Feb2013-08:11UTC chinajade
        public Composite UtilityBehaviorPS_GetMobsAttention(ProvideWoWUnitDelegate selectedTargetDelegate)
        {
            ContractRequires(selectedTargetDelegate != null, context => "selectedTargetDelegate != null");

            return new PrioritySelector(
                new Action(context =>
                {
                    _ubpsGetMobsAttention_Mob = selectedTargetDelegate(context);
                    return RunStatus.Failure; // fall through
                }),
                new Decorator(context => IsViableForFighting(_ubpsGetMobsAttention_Mob)
                                        && !_ubpsGetMobsAttention_Mob.IsTargetingMeOrPet,
                    new PrioritySelector(
                        new CompositeThrottle(TimeSpan.FromSeconds(3),
                            new Action(context =>
                            {
                                TreeRoot.StatusText = string.Format("Getting attention of {0}", _ubpsGetMobsAttention_Mob.Name);
                                return RunStatus.Failure;   // fall through
                            })),
                        UtilityBehaviorPS_SpankMob(selectedTargetDelegate)))
                    );
        }
        private WoWUnit _ubpsGetMobsAttention_Mob;


        // 22Apr2013-09:02UTC chinajade
        public Composite UtilityBehaviorPS_MountAsNeeded(ProvideWoWPointDelegate destinationDelegate,
                                                            CanRunDecoratorDelegate suppressMountUse = null)
        {
            ContractRequires(destinationDelegate != null, context => "locationRetriever may not be null");
            suppressMountUse = suppressMountUse ?? (context => false);

            const int AuraId_AquaticForm = 1066;
            const int AuraId_WarlockUnendingBreath = 5697;
            const int SpellId_DruidAquaticForm = 1066;
            const int SpellId_WarlockUnendingBreath = 5697;

            return
                new CompositeThrottle(TimeSpan.FromMilliseconds(3000),
                    new PrioritySelector(
                        new Decorator(context => Me.IsSwimming,
                            new Action(context =>
                            {
                                if (SpellManager.CanCast(SpellId_DruidAquaticForm) && !Me.HasAura(AuraId_AquaticForm))
                                    { SpellManager.Cast(SpellId_DruidAquaticForm); }

                                else if (SpellManager.CanCast(SpellId_WarlockUnendingBreath) && !Me.HasAura(AuraId_WarlockUnendingBreath))
                                    { SpellManager.Cast(SpellId_WarlockUnendingBreath); }

                                return RunStatus.Failure;
                            })
                        ),

                        new Decorator(context => !Me.IsSwimming,
                            new Decorator(context => !suppressMountUse(context)
                                                    && !Me.InVehicle
                                                    && !Me.Mounted
                                                    && Mount.CanMount()
                                                    && (Mount.ShouldMount(_ubpsMoveTo_Location)
                                                        || (Me.Location.SurfacePathDistance(_ubpsMoveTo_Location) > CharacterSettings.Instance.MountDistance)
                                                        // This allows Me.MovementInfo.CanFly to have a chance at evaluating 'true' in rough terrain
                                                        || !Navigator.CanNavigateFully(Me.Location, _ubpsMoveTo_Location)),
                                new Action(context =>
                                {
                                    Mount.MountUp(() => destinationDelegate(context));
                                    return RunStatus.Failure;
                                })
                            ))
                    ));
        }

        
        // 29Apr2013-05:20UTC chinajade
        public Composite UtilityBehaviorPS_MoveStop()
        {
            return new Decorator(context => MovementObserver.IsMoving,
                new Sequence(
                    new Action(context => { Navigator.PlayerMover.MoveStop(); }),
                    new Wait(Delay_LagDuration, context => MovementObserver.IsMoving, new ActionAlwaysSucceed())
                ));
        }


        // 22Apr2013-12:45UTC chinajade
        public Composite UtilityBehaviorPS_MoveTo(ProvideHuntingGroundsDelegate huntingGroundsProvider)
        {
            ContractRequires(huntingGroundsProvider != null, context => "huntingGroundsProvider may not be null");

            return new PrioritySelector(
                UtilityBehaviorPS_MoveTo(
                    context => huntingGroundsProvider(context).CurrentWaypoint().Location,
                    context => string.Format("hunting ground waypoint '{0}'",
                                            huntingGroundsProvider(context).CurrentWaypoint().Name))
                                            );
        }

    
        // 24Feb2013-08:11UTC chinajade
        public Composite UtilityBehaviorPS_MoveTo(ProvideWoWPointDelegate destinationDelegate,
                                                    ProvideStringDelegate destinationNameDelegate,
                                                    ProvideDoubleDelegate precisionDelegate = null,
                                                    CanRunDecoratorDelegate suppressMountUse = null,
                                                    ProvideWoWPointDelegate locationObserver = null)
        {
            ContractRequires(destinationDelegate != null, context => "locationRetriever may not be null");
            ContractRequires(destinationNameDelegate != null, context => "destinationNameDelegate may not be null");
            precisionDelegate = precisionDelegate ?? (context => Navigator.PathPrecision);
            locationObserver = locationObserver ?? (context => MovementObserver.Location);

            return new Decorator(context => MovementBy != MovementByType.None,
                new PrioritySelector(
                    new Action(context =>
                    {
                        _ubpsMoveTo_Location = destinationDelegate(context);
                        return RunStatus.Failure;   // fall through
                    }),
                    UtilityBehaviorPS_MountAsNeeded(destinationDelegate, suppressMountUse),

                    new Decorator(context => (locationObserver(context).Distance(_ubpsMoveTo_Location) > precisionDelegate(context)),
                        new Sequence(
                            new CompositeThrottleContinue(TimeSpan.FromMilliseconds(1000),
                                new Action(context => { TreeRoot.StatusText = "Moving to " + (destinationNameDelegate(context) ?? _ubpsMoveTo_Location.ToString()); })),
                            new CompositeThrottleContinue(Throttle_WoWClientMovement,
                                new Action(context =>
                                {
                                    var moveResult = MoveResult.Failed;

                                    // Use Flightor, if allowed...
                                    if ((MovementBy == MovementByType.FlightorPreferred) && Me.IsOutdoors && Me.MovementInfo.CanFly)
                                    {
                                        var immediateDestination = _ubpsMoveTo_Location.FindFlightorUsableLocation();

                                        if (immediateDestination == default(WoWPoint))
                                            { moveResult = MoveResult.Failed; }

                                        else if (Me.Location.Distance(immediateDestination) > Navigator.PathPrecision)
                                        {
                                            // <sigh> Its simply a crime that Flightor doesn't implement the INavigationProvider interface...
                                            Flightor.MoveTo(immediateDestination, 15.0f);
                                            moveResult = MoveResult.Moved;
                                        }
                                        
                                        else if (Me.IsFlying)
                                        {
                                            WoWMovement.Move(WoWMovement.MovementDirection.Descend, TimeSpan.FromMilliseconds(400));
                                            moveResult = MoveResult.Moved;
                                        }
                                    }

                                    // Use Navigator to get there, if allowed...
                                    if ((MovementBy == MovementByType.NavigatorPreferred)
                                        || (MovementBy == MovementByType.NavigatorOnly)
                                        || (moveResult == MoveResult.Failed))
                                    {
                                        if (!Me.IsSwimming)
                                            { moveResult = Navigator.MoveTo(_ubpsMoveTo_Location); }
                                    }

                                    // If Navigator couldn't move us, resort to click-to-move if allowed...
                                    if (!((moveResult == MoveResult.Moved)
                                            || (moveResult == MoveResult.ReachedDestination)
                                            || (moveResult == MoveResult.PathGenerated)))
                                    {
                                        if (MovementBy == MovementByType.NavigatorOnly)
                                        {
                                            LogWarning("Failed to mesh move--is area unmeshed? Or, are we flying or swimming?");
                                            return RunStatus.Failure;
                                        }

                                        WoWMovement.ClickToMove(_ubpsMoveTo_Location);
                                    }

                                    return RunStatus.Success;
                                }))
                        ))  
                    ));
        }
        private WoWPoint _ubpsMoveTo_Location;
        

        // 22Apr2013-01:15UTC chinajade
        public Composite UtilityBehaviorPS_NoMobsAtCurrentWaypoint(ProvideHuntingGroundsDelegate huntingGroundsProvider,
                                                                    ProvideBoolDelegate terminateBehaviorIfNoTargetsProvider = null,
                                                                    Func<object, IEnumerable<string>> huntedMobNamesProvider = null,
                                                                    ProvideStringDelegate huntedMobExclusions = null)
        {
            ContractRequires(huntingGroundsProvider != null, context => "huntingGroundsProvider may not be null");
            terminateBehaviorIfNoTargetsProvider = terminateBehaviorIfNoTargetsProvider ?? (context => false);
            huntedMobNamesProvider = huntedMobNamesProvider ?? (context => Enumerable.Empty<string>());
            huntedMobExclusions = huntedMobExclusions ?? (context => string.Empty);

            return
                new PrioritySelector(
                    // Move to next hunting ground waypoint...
                    UtilityBehaviorPS_MoveTo(huntingGroundsProvider),

                    // Terminate of no targets available?
                    new Decorator(context => terminateBehaviorIfNoTargetsProvider(context),
                        new Action(context =>
                        {
                            string message = "No mobs in area--terminating due to WaitForNpcs=\"false\"";
                            TreeRoot.StatusText = message;

                            // Show excluded units before terminating.  This aids in profile debugging if WaitForNpcs="false"...
                            string excludedUnitReasons = huntedMobExclusions(context);
                            if (!string.IsNullOrEmpty(excludedUnitReasons))
                            {
                                message += excludedUnitReasons;
                                LogDeveloperInfo("{0}", message);                                            
                            }
                            BehaviorDone();
                        })),

                    // Only one hunting ground waypoint to move to?
                    new CompositeThrottle(context => huntingGroundsProvider(context).Waypoints.Count() <= 1,
                        TimeSpan.FromSeconds(30),
                        new Action(context =>
                        {
                            string message = "Waiting for respawn";

                            if (huntedMobNamesProvider(context).Any())
                            {
                                message += " of ";
                                message += string.Join(", ", huntedMobNamesProvider(context));
                            }
                                
                            TreeRoot.StatusText = message;

                            string excludedUnitReasons = huntedMobExclusions(context);
                            if (!string.IsNullOrEmpty((excludedUnitReasons)))
                            {
                                message += excludedUnitReasons;
                                LogDeveloperInfo("{0}", message);
                            }
                        }))
                );
        }


        /// <summary>
        /// Unequivocally engages mob in combat.  Does no checking for being untagged, etc.
        /// </summary>
        /// <remarks>24Feb2013-08:11UTC chinajade</remarks>
        public Composite UtilityBehaviorPS_SpankMob(ProvideWoWUnitDelegate selectedTargetDelegate)
        {
            ContractRequires(selectedTargetDelegate != null, context => "selectedTargetDelegate != null");

            return new PrioritySelector(
                new Action(context =>
                {
                    _ubpsSpankMob_Mob = selectedTargetDelegate(context);
                    return RunStatus.Failure;   // fall through     
                }),
                new Decorator(context => IsViableForFighting(_ubpsSpankMob_Mob),
                    new PrioritySelector(
                        new Decorator(context => Me.CurrentTarget != _ubpsSpankMob_Mob,
                            new Action(context =>
                            {
                                _ubpsSpankMob_Mob.Target();
                                BotPoi.Current = new BotPoi(_ubpsSpankMob_Mob, PoiType.Kill);
                                return RunStatus.Failure; // fall through
                            })),

                        // NB: Some Combat Routines (CR) will stall when asked to kill things from too far away.
                        // So, we manually move the toon within reasonable range before asking the CR to kill it.
                        // Note that some behaviors will set the PullDistance to zero or one while they run, but we don't want to
                        // actually get that close to engage, so we impose a lower bound of 23 feet that we move before handing
                        // things over to the combat routine.
                        new Decorator(context => _ubpsSpankMob_Mob.Distance > Math.Max(23, CharacterSettings.Instance.PullDistance),
                            UtilityBehaviorPS_MoveTo(context => _ubpsSpankMob_Mob.Location,
                                                    context => _ubpsSpankMob_Mob.Name)),
                        new Decorator(context => Me.Mounted,
                            new Action(context => { Mount.Dismount(); })),

                        // The NeedHeal and NeedCombatBuffs are part of legacy custom class support
                        // and pair with the Heal and CombatBuff virtual methods.  If a legacy custom class is loaded,
                        // HonorBuddy automatically wraps calls to Heal and CustomBuffs it in a Decorator checking those for you.
                        // So, no need to duplicate that work here.
                        new Decorator(ctx => RoutineManager.Current.HealBehavior != null,
                            RoutineManager.Current.HealBehavior),
                        new Decorator(ctx => RoutineManager.Current.CombatBuffBehavior != null,
                            RoutineManager.Current.CombatBuffBehavior),
                        RoutineManager.Current.CombatBehavior
                    ))
                );
        }
        private WoWUnit _ubpsSpankMob_Mob;

        
        /// <summary>
        /// Targets and kills any mob targeting Self or Pet.
        /// </summary>
        /// <returns></returns>
        public Composite UtilityBehaviorPS_SpankMobTargetingUs(Func<object, IEnumerable<WoWUnit>> excludedUnitsDelegate = null)
        {
            excludedUnitsDelegate = excludedUnitsDelegate ?? (context => Enumerable.Empty<WoWUnit>());

            Func<object, bool> isInterestingToUs =
                (obj) =>
                {
                    var wowUnit = obj as WoWUnit;

                    return
                        IsViableForFighting(wowUnit)
                        && (wowUnit.IsTargetingMeOrPet
                            || wowUnit.IsTargetingAnyMinion
                            || wowUnit.IsTargetingMyPartyMember)
                        // exclude opposing faction: both players and their pets show up as "PlayerControlled"
                        && !wowUnit.PlayerControlled
                        // Do not pull mobs on the AvoidMobs list
                        && !ProfileManager.CurrentOuterProfile.AvoidMobs.Contains(wowUnit.Entry)
                        // exclude any units that are candidates for interacting
                        && !excludedUnitsDelegate(obj).Contains(wowUnit);                                                     
                };
                            
            return new PrioritySelector(
                // If a mob is targeting us, deal with it immediately, so subsequent activities won't be interrupted...
                // NB: This can happen if we 'drag mobs' behind us on the way to our destination.
                new Decorator(context => !isInterestingToUs(_ubpsSpankMobTargetingUs_Mob),
                    new Action(context =>
                    {
                        _ubpsSpankMobTargetingUs_Mob =
                            ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                            .Where(u => isInterestingToUs(u))
                            .OrderBy(u => u.DistanceSqr)
                            .FirstOrDefault();

                        return RunStatus.Failure;   // fall through
                    })),

                // Spank any mobs we find being naughty...
                new CompositeThrottle(context => _ubpsSpankMobTargetingUs_Mob != null,
                    TimeSpan.FromMilliseconds(3000),
                    new Action(context =>
                    {
                        TreeRoot.StatusText = string.Format("Spanking {0} that has targeted us.",
                            _ubpsSpankMobTargetingUs_Mob.Name);                                    
                    })),
                UtilityBehaviorPS_SpankMob(context => _ubpsSpankMobTargetingUs_Mob)
            );
        }
        private WoWUnit _ubpsSpankMobTargetingUs_Mob;


        // 24Feb2013-08:11UTC chinajade
        public Composite UtilityBehaviorPS_SpankMobWithinAggroRange(ProvideWoWPointDelegate destinationDelegate,
                                                                    ProvideDoubleDelegate extraRangePaddingDelegate = null,
                                                                    Func<IEnumerable<int>> excludedUnitIdsDelegate = null)
        {
            ContractRequires(destinationDelegate != null, context => "destinationDelegate != null");
            extraRangePaddingDelegate = extraRangePaddingDelegate ?? (context => 0.0);
            excludedUnitIdsDelegate = excludedUnitIdsDelegate ?? (() => Enumerable.Empty<int>());

            Func<object, bool> isInterestingToUs =
                (obj) =>
                {
                    WoWUnit wowUnit = obj as WoWUnit;

                    return
                        IsViableForFighting(wowUnit)
                        && wowUnit.IsHostile
                        && wowUnit.IsUntagged()
                        // exclude opposing faction: both players and their pets show up as "PlayerControlled"
                        && !wowUnit.PlayerControlled
                        // exclude any units that are candidates for interacting
                        && !excludedUnitIdsDelegate().Contains((int)wowUnit.Entry)
                        // Do not pull mobs on the AvoidMobs list
                        && !ProfileManager.CurrentOuterProfile.AvoidMobs.Contains(wowUnit.Entry)
                        && (wowUnit.Location.SurfacePathDistance(destinationDelegate(obj)) <= (wowUnit.MyAggroRange + extraRangePaddingDelegate(obj)));
                };
        
            return new Decorator(context => !Me.Combat,
                new PrioritySelector(
                    // If a mob is within aggro range of our destination, deal with it immediately...
                    // Otherwise, it will interrupt our attempt to interact or use items.
                    new Decorator(context => !isInterestingToUs(_ubpsSpankMobWithinAggroRange_Mob),
                        new Action(context =>
                        {
                            _ubpsSpankMobWithinAggroRange_Mob =
                                ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                                .Where(o => isInterestingToUs(o))
                                .OrderBy(u => u.DistanceSqr)
                                .FirstOrDefault();

                            return RunStatus.Failure;   // fall through
                        })),

                    // Spank any mobs we find being naughty...
                    new CompositeThrottle(context => _ubpsSpankMobWithinAggroRange_Mob != null,
                        TimeSpan.FromMilliseconds(3000),
                        new Action(context =>
                        {
                            TreeRoot.StatusText = string.Format("Spanking {0}({1}) within aggro range ({2:F1}) of our destination.",
                                _ubpsSpankMobWithinAggroRange_Mob.Name,
                                _ubpsSpankMobWithinAggroRange_Mob.Entry,
                                (_ubpsSpankMobWithinAggroRange_Mob.MyAggroRange + extraRangePaddingDelegate(context)));                                    
                        })),
                    UtilityBehaviorPS_SpankMob(context => _ubpsSpankMobWithinAggroRange_Mob)
            ));
        }
        private WoWUnit _ubpsSpankMobWithinAggroRange_Mob;
            

        /// <summary>
        /// <para>Uses item defined by WOWITEMDELEGATE on target defined by SELECTEDTARGETDELEGATE.</para>
        /// <para>Notes:<list type="bullet">
        /// <item><description><para>* It is up to the caller to assure that all preconditions have been met for
        /// using the item (i.e., the target is in range, the item is off cooldown, etc).</para></description></item>
        /// <item><description><para> * If item use was successful, BT is provided with RunStatus.Success;
        /// otherwise, RunStatus.Failure is returned (e.g., item is not ready for use,
        /// item use was interrupted by combat, etc).</para></description></item>
        /// <item><description><para>* It is up to the caller to blacklist the target, or select a new target
        /// after successful item use.</para></description></item>
        /// </list></para>
        /// </summary>
        /// <param name="selectedTargetDelegate">may NOT be null.  The target provided by the delegate should be viable.</param>
        /// <param name="wowItemDelegate">may NOT be null.  The item provided by the delegate should be viable, and ready for use.</param>
        /// <returns></returns>
        public Composite UtilityBehaviorSeq_UseItemOn(ProvideWoWItemDelegate wowItemDelegate,
                                                     ProvideWoWObjectDelegate selectedTargetDelegate)
        {
            ContractRequires(selectedTargetDelegate != null, context => "selectedTargetDelegate != null");
            ContractRequires(wowItemDelegate != null, context => "wowItemDelegate != null");

            return new Sequence(
                new DecoratorContinue(context => !IsViable(_ubseqUseItemOn_SelectedTarget = selectedTargetDelegate(context)),
                    new Action(context =>
                    {
                        LogWarning("Target is not viable!");
                        return RunStatus.Failure;                        
                    })),

                new DecoratorContinue(context => !IsViable(_ubseqUseItemOn_ItemToUse = wowItemDelegate(context)),
                    new Action(context =>
                    {
                        LogWarning("We do not possess the item to use on {0}!", _ubseqUseItemOn_SelectedTarget.Name);
                        return RunStatus.Failure;
                    })),

                new DecoratorContinue(context => !_ubseqUseItemOn_ItemToUse.Usable,
                    new Action(context =>
                    {
                        LogWarning("{0} is not usable (yet).", _ubseqUseItemOn_ItemToUse.Name);
                        return RunStatus.Failure;
                    })),

                // Use the item...
                new Action(context =>
                {
                    // Set up 'interrupted use' detection...
                    // MAINTAINER'S NOTE: Once these handlers are installed, make sure all possible exit paths from the outer
                    // Sequence unhook these handlers.  I.e., if you plan on returning RunStatus.Failure, be sure to call
                    // UtilityBehaviorSeq_UseItemOn_HandlersUnhook() first.
                    UtilityBehaviorSeq_UseItemOn_HandlersHook();

                    // Notify user of intent...
                    var message = string.Format("Using '{0}' on '{1}'",
                                                _ubseqUseItemOn_ItemToUse.Name,
                                                _ubseqUseItemOn_SelectedTarget.Name);

                    var selectedTargetAsWoWUnit = _ubseqUseItemOn_SelectedTarget as WoWUnit;
                    if (selectedTargetAsWoWUnit != null)
                        { message += string.Format(" (health: {0:F1})", selectedTargetAsWoWUnit.HealthPercent); }

                    LogInfo(message);

                    // Do it...
                    _ubseqUseItemOn_IsUseItemInterrupted = false;    
                    _ubseqUseItemOn_ItemToUse.Use(_ubseqUseItemOn_SelectedTarget.Guid);
                }),
                new WaitContinue(Delay_AfterItemUse, context => false, new ActionAlwaysSucceed()),

                // If item use requires a second click on the target (e.g., item has a 'ground target' mechanic)...
                new DecoratorContinue(context => StyxWoW.Me.CurrentPendingCursorSpell != null,
                    new Sequence(
                        new Action(context => { SpellManager.ClickRemoteLocation(_ubseqUseItemOn_SelectedTarget.Location); }),
                        new WaitContinue(Delay_AfterItemUse,
                            context => StyxWoW.Me.CurrentPendingCursorSpell == null,
                            new ActionAlwaysSucceed()),
                        // If we've leftover spell cursor dangling, clear it...
                        // NB: This can happen for "use item on location" type activites where you get interrupted
                        // (e.g., a walk-in mob).
                        new DecoratorContinue(context => StyxWoW.Me.CurrentPendingCursorSpell != null,
                            new Action(context => { Lua.DoString("SpellStopTargeting()"); }))       
                    )),

                // Wait for any casting to complete...
                // NB: Some interactions or item usages take time, and the WoWclient models this as spellcasting.
                new WaitContinue(TimeSpan.FromSeconds(15),
                    context => !(Me.IsCasting || Me.IsChanneling),
                    new ActionAlwaysSucceed()),

                // Were we interrupted in item use?
                new Action(context => { UtilityBehaviorSeq_UseItemOn_HandlersUnhook(); }),
                new DecoratorContinue(context => _ubseqUseItemOn_IsUseItemInterrupted,
                    new Sequence(
                        new Action(context => { LogDeveloperInfo("Use of {0} interrupted.", _ubseqUseItemOn_ItemToUse.Name); }),
                        // Give whatever issue encountered a chance to settle...
                        // NB: Wait, not WaitContinue--we want the Sequence to fail when delay completes.
                        new Wait(TimeSpan.FromMilliseconds(1500), context => false, new ActionAlwaysFail())
                    ))
            );
        }
        private bool _ubseqUseItemOn_IsUseItemInterrupted;
        private WoWItem _ubseqUseItemOn_ItemToUse;
        private WoWObject _ubseqUseItemOn_SelectedTarget;

        private void UtilityBehaviorSeq_UseItemOn_HandleUseItemInterrupted(object sender, LuaEventArgs args)
        {
            if (args.Args[0].ToString() == "player")
            {
                LogDeveloperInfo("Interrupted via {0} Event.", args.EventName);
                _ubseqUseItemOn_IsUseItemInterrupted = true;
            }
        }

        private void UtilityBehaviorSeq_UseItemOn_HandlersHook()
        {
            Lua.Events.AttachEvent("UNIT_SPELLCAST_CHANNEL_UPDATE", UtilityBehaviorSeq_UseItemOn_HandleUseItemInterrupted);
            Lua.Events.AttachEvent("UNIT_SPELLCAST_FAILED", UtilityBehaviorSeq_UseItemOn_HandleUseItemInterrupted);
            Lua.Events.AttachEvent("UNIT_SPELLCAST_INTERRUPTED", UtilityBehaviorSeq_UseItemOn_HandleUseItemInterrupted);
        }

        private void UtilityBehaviorSeq_UseItemOn_HandlersUnhook()
        {
            Lua.Events.DetachEvent("UNIT_SPELLCAST_CHANNEL_UPDATE", UtilityBehaviorSeq_UseItemOn_HandleUseItemInterrupted);
            Lua.Events.DetachEvent("UNIT_SPELLCAST_FAILED", UtilityBehaviorSeq_UseItemOn_HandleUseItemInterrupted);
            Lua.Events.DetachEvent("UNIT_SPELLCAST_INTERRUPTED", UtilityBehaviorSeq_UseItemOn_HandleUseItemInterrupted);    
        }



        public Composite UtilityBehaviorSeq_InteractWith(ProvideWoWObjectDelegate selectedTargetDelegate,
                                                        ProvideBoolDelegate doMovementDelegate = null)
        {
            ContractRequires(selectedTargetDelegate != null, context => "selectedTargetDelegate != null");
            doMovementDelegate = doMovementDelegate ?? (context => true);

            return new Sequence(
                new DecoratorContinue(context => !IsViable(_ubseqInteractWith_SelectedTarget = selectedTargetDelegate(context)),
                    new Action(context =>
                    {
                        LogWarning("Target is not viable!");
                        return RunStatus.Failure;                        
                    })),

                new DecoratorContinue(context => doMovementDelegate(context),
                    new PrioritySelector(
                        // Show user which unit we're going after...
                        new Decorator(context => (_ubseqInteractWith_SelectedTarget.ToUnit() != null)
                                                    && (Me.CurrentTarget != _ubseqInteractWith_SelectedTarget),
                            new Action(context => { _ubseqInteractWith_SelectedTarget.ToUnit().Target(); })),

                        // If not within interact range, move closer...
                        new Decorator(context => !_ubseqInteractWith_SelectedTarget.WithinInteractRange,
                            UtilityBehaviorPS_MoveTo(interactUnitContext => _ubseqInteractWith_SelectedTarget.Location,
                                                     interactUnitContext => string.Format("interact with {0}", _ubseqInteractWith_SelectedTarget.Name))),

                        UtilityBehaviorPS_MoveStop(),
                        UtilityBehaviorPS_FaceMob(context => _ubseqInteractWith_SelectedTarget),
                        new ActionAlwaysSucceed()
                    )),

                // Interact with the mob...
                new Action(context =>
                {
                    // Set up 'interrupted use' detection...
                    // MAINTAINER'S NOTE: Once these handlers are installed, make sure all possible exit paths from the outer
                    // Sequence unhook these handlers.  I.e., if you plan on returning RunStatus.Failure, be sure to call
                    // UtilityBehaviorSeq_UseItemOn_HandlersUnhook() first.
                    UtilityBehaviorSeq_InteractWith_HandlersHook();

                    // Notify user of intent...
                    LogInfo("Interacting with '{0}'", _ubseqInteractWith_SelectedTarget.Name);

                    // Do it...
                    _ubseqInteractWith_IsInteractInterrupted = false;    
                    _ubseqInteractWith_SelectedTarget.Interact();
                }),
                new WaitContinue(Delay_AfterInteraction, context => false, new ActionAlwaysSucceed()),

                // Wait for any casting to complete...
                // NB: Some interactions or item usages take time, and the WoWclient models this as spellcasting.
                new WaitContinue(TimeSpan.FromSeconds(15),
                    context => !(Me.IsCasting || Me.IsChanneling),
                    new ActionAlwaysSucceed()),

                // Were we interrupted in item use?
                new Action(context => { UtilityBehaviorSeq_InteractWith_HandlersUnhook(); }),
                new DecoratorContinue(context => _ubseqInteractWith_IsInteractInterrupted,
                    new Sequence(
                        new Action(context => { LogDeveloperInfo("Interaction with {0} interrupted.", _ubseqInteractWith_SelectedTarget.Name); }),
                        // Give whatever issue encountered a chance to settle...
                        // NB: Wait, not WaitContinue--we want the Sequence to fail when delay completes.
                        new Wait(TimeSpan.FromMilliseconds(1500), context => false, new ActionAlwaysFail())
                    ))  
            );
        }
        private bool _ubseqInteractWith_IsInteractInterrupted;
        private WoWObject _ubseqInteractWith_SelectedTarget;

        private void UtilityBehaviorSeq_InteractWith_HandleInteractInterrupted(object sender, LuaEventArgs args)
        {
            if (args.Args[0].ToString() == "player")
            {
                LogDeveloperInfo("Interrupted via {0} Event.", args.EventName);
                _ubseqInteractWith_IsInteractInterrupted = true;
            }
        }

        private void UtilityBehaviorSeq_InteractWith_HandlersHook()
        {
            Lua.Events.AttachEvent("UNIT_SPELLCAST_CHANNEL_UPDATE", UtilityBehaviorSeq_InteractWith_HandleInteractInterrupted);
            Lua.Events.AttachEvent("UNIT_SPELLCAST_FAILED", UtilityBehaviorSeq_InteractWith_HandleInteractInterrupted);
            Lua.Events.AttachEvent("UNIT_SPELLCAST_INTERRUPTED", UtilityBehaviorSeq_InteractWith_HandleInteractInterrupted);
        }

        private void UtilityBehaviorSeq_InteractWith_HandlersUnhook()
        {
            Lua.Events.DetachEvent("UNIT_SPELLCAST_CHANNEL_UPDATE", UtilityBehaviorSeq_InteractWith_HandleInteractInterrupted);
            Lua.Events.DetachEvent("UNIT_SPELLCAST_FAILED", UtilityBehaviorSeq_InteractWith_HandleInteractInterrupted);
            Lua.Events.DetachEvent("UNIT_SPELLCAST_INTERRUPTED", UtilityBehaviorSeq_InteractWith_HandleInteractInterrupted);    
        }

    }
}