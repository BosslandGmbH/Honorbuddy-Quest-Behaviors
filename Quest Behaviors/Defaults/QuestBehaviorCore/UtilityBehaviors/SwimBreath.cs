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

using Styx;
using Styx.CommonBot;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public partial class UtilityBehaviorPS
    {
        public class SwimBreath : PrioritySelector
        {
            public SwimBreath(ProvideMovementByDelegate movementByDelegate)
            {
                Contract.Requires(movementByDelegate != null, context => "movementByDelegate may not be null");

                MovementByDelegate = movementByDelegate ?? (context => MovementByType.FlightorPreferred);

                Children = CreateChildren();
            }


            // BT contruction-time properties...
            private ProvideMovementByDelegate MovementByDelegate { get; set; }

            // BT visit-time properties...
            private bool IsSwimBreathNeeded { get; set; }
            private AirSource NearestAirSource { get; set; }
            private MirrorTimerInfo SwimBreathMirrorTimer { get { return (Me.GetMirrorTimerInfo(MirrorTimerType.Breath)); } }

            // Convenience properties...
            private const int AuraId_DruidAquaticForm = 1066;
            private const int AuraId_WarlockUnendingBreath = 5697;
            private const string AuraName_WarlockUnendingBreath = "Unending Breath";
            private int BreathTimeRemaining
            {
                get
                {
                    return ((Timer_SwimBreath.IsVisible)
                           ? (int)Timer_SwimBreath.CurrentTime
                           : int.MaxValue);
                }
            }
            private int MinTime_DruidBreath = 30000;    // in milliseconds
            private int MinTime_WarlockBreath = 30000;    // in milliseconds
            private const int SpellId_DruidAquaticForm = 1066;
            private const int SpellId_WarlockUnendingBreath = 5697;
            private readonly TimeSpan ThrottleTimer_BreathCheck = TimeSpan.FromSeconds(5);
            private readonly TimeSpan ThrottleTimer_WarlockBreath = TimeSpan.FromSeconds(30);
            private readonly TimeSpan Timer_AuraRefresh_EnduringBreath = TimeSpan.FromSeconds(3 /*min*/ * 60 /*sec*/);
            private MirrorTimerInfo Timer_SwimBreath { get { return (Me.GetMirrorTimerInfo(MirrorTimerType.Breath)); } }
            private readonly int[] UnderwaterAirSourceObjectIds =
            {
                177524 /* bubbly fissure */
            };


            private List<Composite> CreateChildren()
            {
                return new List<Composite>()
                {
                    new Decorator(ret => Me.IsSwimming,
                        new PrioritySelector(

                            // Moving to, or fetching breath...
                            new Decorator(ret => IsSwimBreathNeeded,
                                new PrioritySelector(

                                    // If toon is filling lungs, stay put until full...
                                    new Decorator(ret => (SwimBreathMirrorTimer.ChangePerMillisecond > 0)
                                                            && (SwimBreathMirrorTimer.CurrentTime < SwimBreathMirrorTimer.MaxValue),
                                        new Action(delegate
                                        {
                                            WoWMovement.MoveStop();
                                            TreeRoot.StatusText = "Waiting for full breath";
                                        })),

                                    // If lungs are full, back to work...
                                    new Decorator(ret => SwimBreathMirrorTimer.CurrentTime >= SwimBreathMirrorTimer.MaxValue,
                                        new Action(delegate { IsSwimBreathNeeded = false; })),

                                    // Move toon to air source, if needed...
                                    new Decorator(ret =>
                                        {
                                            NearestAirSource = GetNearestAirSource();
                                            return (NearestAirSource.Distance > Navigator.PathPrecision);
                                        },
                                        new Sequence(
                                            new CompositeThrottleContinue(Throttle.UserUpdate,
                                                new Action(context =>
                                                {
                                                    TreeRoot.StatusText = string.Format("Moving to {0} for breath. (distance {1:0.0})",
                                                                                        NearestAirSource.Name,
                                                                                        NearestAirSource.Distance);
                                                })),

                                            new UtilityBehaviorPS.MoveTo(
                                                context => NearestAirSource.Location,
                                                context => NearestAirSource.Name,
                                                context => MovementByDelegate(context))
                                            )
                                        )
                                )),


                            // If we're a Warlock, refresh Unending Breath as needed...
                            new CompositeThrottle(
                                context => (SpellManager.CanCast(SpellId_WarlockUnendingBreath)
                                        && (AuraTimeLeft(AuraName_WarlockUnendingBreath) <= Timer_AuraRefresh_EnduringBreath)),
                                ThrottleTimer_WarlockBreath,
                                new Action(delegate { SpellManager.Cast(SpellId_WarlockUnendingBreath); })),


                            // If time to breathe, do something about it...
                            new CompositeThrottle(
                                context => IsBreathNeeded(),
                                ThrottleTimer_BreathCheck,
                                new PrioritySelector(

                                    // If we're a Druid, switch to Aquatic form for breath...
                                    new Decorator(ret => (SpellManager.CanCast(SpellId_DruidAquaticForm)
                                                            && !Me.HasAura(AuraId_DruidAquaticForm)),
                                        new Action(delegate
                                        {
                                            SpellManager.Cast(SpellId_DruidAquaticForm);
                                            TreeRoot.StatusText = "Switching to Aquatic Form for breath";
                                            IsSwimBreathNeeded = true;
                                        })),


                                    // Otherwise, we need to deal with 'normal' way to catch breath...
                                    new Action(delegate
                                    {
                                        NearestAirSource = GetNearestAirSource();
                                        QBCLog.Info("Moving to {0} for breath. (distance {1:0.0})",
                                                NearestAirSource.Name, NearestAirSource.Distance);
                                        IsSwimBreathNeeded = true;
                                    })
                                ))
                        ))
                };         
            }


            private TimeSpan AuraTimeLeft(string auraName)
            {
                WoWAura wowAura = Me.GetAuraByName(auraName);

                return ((wowAura != null) ? wowAura.TimeLeft : TimeSpan.Zero);
            }


            private AirSource GetNearestAirSource()
            {
                // Assume water's surface is nearest breath...
                AirSource nearestAirSource = new AirSource(Me.Location.WaterSurface(), "water's surface");
                WoWObject underwaterAirSource = UnderwaterAirSources.FirstOrDefault();

                // If underwater air source exists, and is closer that the water's surface...
                if ((underwaterAirSource != null)
                    && (Me.Location.Distance(underwaterAirSource.Location) <= nearestAirSource.Distance))
                {
                    nearestAirSource.Location = underwaterAirSource.Location;
                    nearestAirSource.Name = underwaterAirSource.Name;
                }

                return (nearestAirSource);
            }


            private bool IsBreathNeeded()
            {
                int breathTimeRemaining = BreathTimeRemaining;

                if (Me.Class == WoWClass.Druid)
                { return (breathTimeRemaining < MinTime_DruidBreath); }

                else if (Me.Class == WoWClass.Warlock)
                { return (breathTimeRemaining < MinTime_WarlockBreath); }

                // Calculate time needed to get to an air source...
                AirSource airSource = GetNearestAirSource();
                double travelTime;

                travelTime = (((airSource.Location.Distance(Me.Location) / Me.MovementInfo.SwimmingForwardSpeed)
                              * 2.75)   // factor of safety
                              + (3 * ThrottleTimer_BreathCheck.TotalSeconds));
                travelTime = Math.Min(travelTime, 30.0);    // Hard-minimum of 30secs
                travelTime *= 1000;     // to milliseconds

                return (breathTimeRemaining <= travelTime);
            }
            
            
            private IEnumerable<WoWObject> UnderwaterAirSources
            {
                get
                {
                    return (
                        ObjectManager.GetObjectsOfType<WoWObject>(true, false)
                    .OrderBy(target => Me.Location.Distance(target.Location))
                    .Where(target => UnderwaterAirSourceObjectIds.Contains((int)target.Entry))
                        );
                }
            }


            private struct AirSource
            {
                public WoWPoint Location;
                public string Name;

                public AirSource(WoWPoint location, string name) { Location = location; Name = name; }
                public double Distance { get { return (Location.Distance(StyxWoW.Me.Location)); } }
                public static AirSource Empty = new AirSource(WoWPoint.Empty, "NONE");
            }
        }
    }
}
