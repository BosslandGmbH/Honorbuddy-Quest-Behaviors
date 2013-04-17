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
    public partial class QuestBehaviorBase
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
                        new WaitContinue(Delay_WoWClientMovementThrottle,
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
                        new WaitContinue(Delay_WoWClientMovementThrottle,
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
                new Decorator(context => IsViableForFighting(_ubpsGetMobsAttention_Mob),
                    new PrioritySelector(
                        new Decorator(context => !_ubpsGetMobsAttention_Mob.IsTargetingMeOrPet,
                            new PrioritySelector(
                                new Action(context =>
                                {
                                    LogInfo("Getting attention of {0}", _ubpsGetMobsAttention_Mob.Name);
                                    return RunStatus.Failure;
                                }),
                                UtilityBehaviorPS_SpankMob(selectedTargetDelegate)))
                    )));
        }
        private WoWUnit _ubpsGetMobsAttention_Mob;


        /// <summary>
        /// Simple right-click interaction with the UNITTOINTERACT.
        /// </summary>
        /// <param name="unitToInteract"></param>
        /// <returns></returns>
        // TODO: Convert this to take a WoWObject instead of a WoWUnit
        // 24Feb2013-08:11UTC chinajade
        public Composite UtilityBehaviorPS_InteractWithMob(ProvideWoWUnitDelegate unitToInteract)
        {
            return new PrioritySelector(
                new Action(context =>
                { 
                    _ubpsInteractWithMob_Mob = unitToInteract(context);
                    return RunStatus.Failure;   // fall through
                }),
                new Decorator(context => IsViableForInteracting(_ubpsInteractWithMob_Mob),
                    new PrioritySelector(
                        // Show user which unit we're going after...
                        new Decorator(context => Me.CurrentTarget != _ubpsInteractWithMob_Mob,
                            new Action(context => { _ubpsInteractWithMob_Mob.Target(); })),

                        // If not within interact range, move closer...
                        new Decorator(context => !_ubpsInteractWithMob_Mob.WithinInteractRange,
                            new Sequence(
                                new Action(context =>
                                {
                                    LogDeveloperInfo("Moving to interact with {0}", _ubpsInteractWithMob_Mob.Name);
                                }),
                                UtilityBehaviorPS_MoveTo(interactUnitContext => _ubpsInteractWithMob_Mob.Location,
                                                         interactUnitContext => _ubpsInteractWithMob_Mob.Name)
                            )),

                        new Decorator(context => Me.IsMoving,
                            new Action(context => { WoWMovement.MoveStop(); })),
                        new Decorator(context => !Me.IsFacing(_ubpsInteractWithMob_Mob),
                            new Action(context => { Me.SetFacing(_ubpsInteractWithMob_Mob); })),

                        // Blindly interact...
                        // Ideally, we would blacklist the unit if the interact failed.  However, the HB API
                        // provides no CanInteract() method (or equivalent) to make this determination.
                        new Action(context =>
                        {
                            LogDeveloperInfo("Interacting with {0}", _ubpsInteractWithMob_Mob.Name);
                            _ubpsInteractWithMob_Mob.Interact();
                            return RunStatus.Failure;
                        }),
                        new Wait(TimeSpan.FromMilliseconds(1000), context => false, new ActionAlwaysSucceed())
                    )));
        }
        private WoWUnit _ubpsInteractWithMob_Mob;


        public Composite UtilityBehaviorPS_MoveTo(ProvideWoWPointDelegate destinationDelegate,
                                                    ProvideStringDelegate destinationNameDelegate,
                                                    ProvideDoubleDelegate precisionDelegate = null,
                                                    CanRunDecoratorDelegate suppressMountUse = null,
                                                    ProvideWoWPointDelegate locationObserver = null)
        {
            ContractRequires(destinationDelegate != null, context => "locationRetriever may not be null");
            ContractRequires(destinationNameDelegate != null, context => "destinationNameDelegate may not be null");
            precisionDelegate = precisionDelegate ?? (context => Me.Mounted ? 8.0 : 5.0);
            suppressMountUse = suppressMountUse ?? (context => false);
            locationObserver = locationObserver ?? (context => Me.Location);

            return new Decorator(context => MovementBy != MovementByType.None,
                new PrioritySelector(
                    new Action(context =>
                    {
                        _ubpsMoveTo_Location = destinationDelegate(context);
                        return RunStatus.Failure;   // fall through
                    }),
                    new Decorator(context => !suppressMountUse(context) && !Me.InVehicle && !Me.Mounted
                                                        && Mount.CanMount()
                                                        && Mount.ShouldMount(_ubpsMoveTo_Location),
                        new Action(context => { Mount.MountUp(() => _ubpsMoveTo_Location); })),

                    new Decorator(context => (locationObserver(context).Distance((_ubpsMoveTo_Location)) > precisionDelegate(context)),
                        new Sequence(
                            new CompositeThrottle(TimeSpan.FromMilliseconds(1000),
                                new Action(context => {TreeRoot.StatusText = "Moving to " + (destinationNameDelegate(context) ?? _ubpsMoveTo_Location.ToString()); })),
                            new Action(context =>
                            {
                                var moveResult = MoveResult.Failed;

                                // Use Navigator to get there, if allowed...
                                if ((MovementBy == MovementByType.NavigatorPreferred) || (MovementBy == MovementByType.NavigatorOnly))
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
                            }),
                            new WaitContinue(Delay_WoWClientMovementThrottle, context => false, new ActionAlwaysSucceed())
                        ))  
                    ));
        }
        private WoWPoint _ubpsMoveTo_Location;
        
        
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
        /// Unequivocally engages mob in combat.
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
                        new Decorator(context => _ubpsSpankMob_Mob.Distance > CharacterSettings.Instance.PullDistance,
                            UtilityBehaviorPS_MoveTo(context => _ubpsSpankMob_Mob.Location,
                                                     context => _ubpsSpankMob_Mob.Name)),
                        new Decorator(context => Me.CurrentTarget != _ubpsSpankMob_Mob,
                            new Action(context =>
                            {
                                BotPoi.Current = new BotPoi(_ubpsSpankMob_Mob, PoiType.Kill);
                                _ubpsSpankMob_Mob.Target();
                                if (Me.Mounted)
                                    { Mount.Dismount(); }
                            })),
                        new Decorator(context => !_ubpsSpankMob_Mob.IsTargetingMeOrPet,
                            new PrioritySelector(
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
        public Composite UtilityBehaviorPS_SpankMobTargetingUs()
        {
            return new Decorator(context => !Me.Combat,
                new PrioritySelector(
                    // If a mob is targeting us, deal with it immediately, so subsequent activities won't be interrupted...
                    // NB: This can happen if we 'drag mobs' behind us on the way to our destination.
                    new Decorator(context => !IsViableForFighting(_ubpsSpankMobTargetingUs_Mob),
                        new Action(context =>
                        {
                            _ubpsSpankMobTargetingUs_Mob = FindNonFriendlyNpcTargetingMeOrPet().OrderBy(u => u.DistanceSqr).FirstOrDefault();
                            return RunStatus.Failure;   // fall through
                        })),

                    // Spank any mobs we find being naughty...
                    new Decorator(context => _ubpsSpankMobTargetingUs_Mob != null,
                        UtilityBehaviorPS_SpankMob(context => _ubpsSpankMobTargetingUs_Mob))
                ));
        }
        private WoWUnit _ubpsSpankMobTargetingUs_Mob;


        public Composite UtilityBehaviorPS_SpankMobWithinAggroRange(ProvideWoWPointDelegate destinationDelegate,
                                                                    ProvideDoubleDelegate extraRangePaddingDelegate = null,
                                                                    Func<IEnumerable<int>> excludedUnitIdsDelegate = null)
        {
            extraRangePaddingDelegate = extraRangePaddingDelegate ?? (context => 0.0);
            excludedUnitIdsDelegate = excludedUnitIdsDelegate ?? (() => new List<int>());

            return new Decorator(context => !Me.Combat,
                new PrioritySelector(
                    // If a mob is targeting us, deal with it immediately, so subsequent activities won't be interrupted...
                    // NB: This can happen if we 'drag mobs' behind us on the way to our destination.
                    new Decorator(context => !IsViableForFighting(_ubpsSpankMobWithinAggroRange_Mob),
                        new Action(context =>
                        {
                            _ubpsSpankMobWithinAggroRange_Mob =
                                FindHostileNpcWithinAggroRangeOFDestination(
                                    destinationDelegate(context),
                                    extraRangePaddingDelegate(context),
                                    excludedUnitIdsDelegate)
                                .OrderBy(u => u.DistanceSqr).FirstOrDefault();
                            return RunStatus.Failure;   // fall through
                        })),

                    // Spank any mobs we find being naughty...
                    new Decorator(context => _ubpsSpankMobWithinAggroRange_Mob != null,
                        UtilityBehaviorPS_SpankMob(context => _ubpsSpankMobWithinAggroRange_Mob))
                ));
        }
        private WoWUnit _ubpsSpankMobWithinAggroRange_Mob;
    }
}