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
                return
                    !StyxWoW.Me.IsFlying
                    || StyxWoW.Me.GetTraceLinePos().IsOverGroundOrWater(maxDismountHeightDelegate(context));
            });

            return new PrioritySelector(
                // Descend, if needed...
                new Decorator(context => !isReadyToDismount(context), 
                    new Sequence(
                        new Action(context =>
                        {
                            LogDeveloperInfo("Descending before dismount");
                            Navigator.PlayerMover.Move(WoWMovement.MovementDirection.Descend);
                        }),
                        new WaitContinue(Throttle_WoWClientMovement,
                            context => isReadyToDismount(context) || !Me.IsMoving,
                            new ActionAlwaysSucceed())
                    )),

                // Stop descending...
                new Decorator(context => Me.MovementInfo.IsDescending,
                    new Sequence(
                        new Action(context =>
                        {
                            LogDeveloperInfo("Stopping descent");
                            WoWMovement.MoveStop(WoWMovement.MovementDirection.Descend);
                        }),
                        new WaitContinue(Throttle_WoWClientMovement,
                            context => !Me.MovementInfo.IsDescending,
                            new ActionAlwaysSucceed())
                    ))
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
            return new Decorator(context => mountStrategyDelegate(context) != MountStrategyType.None,
                new PrioritySelector(
                    new Decorator(context => Me.IsShapeshifted()
                                            && ((mountStrategyDelegate(context) == MountStrategyType.CancelShapeshift)
                                                || (mountStrategyDelegate(context) == MountStrategyType.DismountOrCancelShapeshift)), 
                        new PrioritySelector(
                            UtilityBehaviorPS_DescendForDismount(context => MaxDismountHeight),
                            new Action(context =>
                            {
                                LogDeveloperInfo("Canceling shapeshift form.");
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
                                    LogDeveloperInfo("Canceling 'mounted' shapeshift form.");
                                    Lua.DoString("CancelShapeshiftForm()");
                                })),
                            new Decorator(context => Me.Mounted,
                                new Action(context =>
                                {
                                    LogDeveloperInfo("Dismounting");
                                    Mount.Dismount();
                                }))
                            )),
                        
                    new Decorator(context => !Me.IsMounted()
                                            && (mountStrategyDelegate(context) == MountStrategyType.Mount)
                                            && Mount.CanMount(),
                        // We make up a destination for MountUp() that is far enough away, it will always choose to mount...
                        new Action(context =>
                        {
                            LogDeveloperInfo("Mounting");
                            Mount.MountUp(() => Me.Location.Add(1000.0, 1000.0, 1000.0));
                        }))
            ));
        }

        
        /// <summary>
        /// This behavior quits attacking the mob, once the mob is targeting us.
        /// </summary>
        // 24Feb2013-08:11UTC chinajade
        public Composite UtilityBehaviorPS_GetMobsAttention(ProvideWoWUnitDelegate selectedTargetDelegate)
        {
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


        /// <summary>
        /// Simple right-click interaction with the UNITTOINTERACT.
        /// </summary>
        /// <param name="objectToInteract"></param>
        /// <returns></returns>
        // 24Feb2013-08:11UTC chinajade
        public Composite UtilityBehaviorPS_InteractWithMob(ProvideWoWObjectDelegate objectToInteract)
        {
            return new PrioritySelector(
                new Action(context =>
                { 
                    _ubpsInteractWithMob_WowObject = objectToInteract(context);
                    _ubpsInteractWithMob_AsWowUnit = _ubpsInteractWithMob_WowObject.ToUnit();
                    return RunStatus.Failure;   // fall through
                }),
                new Decorator(context => IsViableForInteracting(_ubpsInteractWithMob_WowObject),
                    new PrioritySelector(
                        // Show user which unit we're going after...
                        new Decorator(context => (_ubpsInteractWithMob_AsWowUnit != null)
                                                && (Me.CurrentTarget != _ubpsInteractWithMob_AsWowUnit),
                            new Action(context => { _ubpsInteractWithMob_AsWowUnit.Target(); })),

                        // If not within interact range, move closer...
                        new Decorator(context => !_ubpsInteractWithMob_WowObject.WithinInteractRange,
                            UtilityBehaviorPS_MoveTo(interactUnitContext => _ubpsInteractWithMob_WowObject.Location,
                                                     interactUnitContext => string.Format("interact with {0}", _ubpsInteractWithMob_WowObject.Name))),

                        new Decorator(context => Me.IsMoving,
                            new Action(context => { WoWMovement.MoveStop(); })),
                        new Decorator(context => !Me.IsFacing(_ubpsInteractWithMob_WowObject.Location),
                            new Action(context => { Me.SetFacing(_ubpsInteractWithMob_WowObject.Location); })),

                        // Blindly interact...
                        // Ideally, we would blacklist the unit if the interact failed.  However, the HB API
                        // provides no CanInteract() method (or equivalent) to make this determination.
                        new Action(context =>
                        {
                            LogDeveloperInfo("Interacting with {0}", _ubpsInteractWithMob_WowObject.Name);
                            _ubpsInteractWithMob_WowObject.Interact();
                            return RunStatus.Failure;
                        }),
                        new Wait(Delay_Interaction, context => false, new ActionAlwaysSucceed())
                    )));
        }
        private WoWObject _ubpsInteractWithMob_WowObject;
        private WoWUnit _ubpsInteractWithMob_AsWowUnit;


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
                                                    && Mount.ShouldMount(_ubpsMoveTo_Location),
                                new Action(context =>
                                {
                                    Mount.MountUp(() => destinationDelegate(context));
                                    return RunStatus.Failure;
                                })
                            ))
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
            locationObserver = locationObserver ?? (context => Me.Location);

            return new Decorator(context => MovementBy != MovementByType.None,
                new PrioritySelector(
                    new Action(context =>
                    {
                        _ubpsMoveTo_Location = destinationDelegate(context);
                        return RunStatus.Failure;   // fall through
                    }),
                    UtilityBehaviorPS_MountAsNeeded(destinationDelegate, suppressMountUse),

                    new Decorator(context => (locationObserver(context).Distance((_ubpsMoveTo_Location)) > precisionDelegate(context)),
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
                                        Flightor.MoveTo(_ubpsMoveTo_Location);
                                        // <sigh> Its simply a crime that Flightor doesn't implement the INavigationProvider interface...
                                        moveResult = MoveResult.Moved;
                                    }

                                    // Use Navigator to get there, if allowed...
                                    else if ((MovementBy == MovementByType.NavigatorPreferred) || (MovementBy == MovementByType.NavigatorOnly)
                                                || (MovementBy == MovementByType.FlightorPreferred))
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

        
        // 11Apr2013-04:52UTC chinajade
        public Composite UtilityBehaviorPS_Rest()
        {
            return new Decorator(context => RoutineManager.Current.NeedRest,
                new PrioritySelector(
                    new Decorator(context => RoutineManager.Current.RestBehavior != null,
                        RoutineManager.Current.RestBehavior),
                    new Action(context => { RoutineManager.Current.Rest(); })
                ));
        }
        

        /// <summary>
        /// Unequivocally engages mob in combat.  Does no checking for being untagged, etc.
        /// </summary>
        /// <remarks>24Feb2013-08:11UTC chinajade</remarks>
        public Composite UtilityBehaviorPS_SpankMob(ProvideWoWUnitDelegate selectedTargetDelegate)
        {
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
                                // NB: We target the mob before setting the POI.Kill.  This makes
                                // Combat Routines happier.
                                _ubpsSpankMob_Mob.Target();
                                BotPoi.Current = new BotPoi(_ubpsSpankMob_Mob, PoiType.Kill);
                                return RunStatus.Failure; // fall through
                            })),
/*TODO*/                        new Decorator(context => true, // !Me.Combat,
                            new PrioritySelector(
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
                                new Decorator(context => RoutineManager.Current.CombatBehavior != null,
                                    RoutineManager.Current.CombatBehavior),
                                new Action(context => { RoutineManager.Current.Combat(); })
                            ))
                    )));
        }
        private WoWUnit _ubpsSpankMob_Mob;

        
        /// <summary>
        /// Targets and kills any mob targeting Self or Pet.
        /// </summary>
        /// <returns></returns>
        public Composite UtilityBehaviorPS_SpankMobTargetingUs(Func<IEnumerable<int>> excludedUnitIdsDelegate = null)
        {
            excludedUnitIdsDelegate = excludedUnitIdsDelegate ?? (() => Enumerable.Empty<int>());

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
                        && !excludedUnitIdsDelegate().Contains((int)wowUnit.Entry);                                                     
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


        public Composite UtilityBehaviorPS_SpankMobWithinAggroRange(ProvideWoWPointDelegate destinationDelegate,
                                                                    ProvideDoubleDelegate extraRangePaddingDelegate = null,
                                                                    Func<IEnumerable<int>> excludedUnitIdsDelegate = null)
        {
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
    }
}