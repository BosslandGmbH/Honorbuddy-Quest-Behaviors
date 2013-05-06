// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#if LATER
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


namespace Honorbuddy.QuestBehaviorCore2
{
    public abstract partial class QuestBehaviorBase
    {
        public Composite UtilityBehaviorPS_SwimBreath()
        {
            const int AuraId_DruidAquaticForm = 1066;
            const int AuraId_WarlockUnendingBreath = 5697;
            const int SpellId_DruidAquaticForm = 1066;
            const int SpellId_WarlockUnendingBreath = 5697;

            return new Decorator(ret => Me.IsSwimming,
                new PrioritySelector(

                    // Moving to, or fetching breath...
                    new Decorator(ret => _ubpsSwimBreath_IsBreathNeeded,
                        new PrioritySelector(

                            // If toon is filling lungs, stay put until full...
                            new Decorator(ret => (_ubpsSwimBreath__SwimBreathMirrorTimer.ChangePerMillisecond > 0)
                                                    && (_ubpsSwimBreath__SwimBreathMirrorTimer.CurrentTime < _ubpsSwimBreath__SwimBreathMirrorTimer.MaxValue),
                                new Action(delegate
                                {
                                    WoWMovement.MoveStop();
                                    TreeRoot.StatusText = "Waiting for full breath";
                                })),

                            // If lungs are full, back to work...
                            new Decorator(ret => _ubpsSwimBreath__SwimBreathMirrorTimer.CurrentTime >= _ubpsSwimBreath__SwimBreathMirrorTimer.MaxValue,
                                new Action(delegate { _ubpsSwimBreath_IsBreathNeeded = false; })),

                            // Move toon to air source, if needed...
                            new Decorator(ret =>
                                {
                                    _ubpsSwimBreath_NearestAirSource = GetNearestAirSource();
                                    return (_ubpsSwimBreath_NearestAirSource.Distance > Navigator.PathPrecision);
                                },
                                new Sequence(
                                    new CompositeThrottleContinue(Delay_StatusUpdateThrottle,
                                        new Action(delegate
                                        {
                                            TreeRoot.StatusText = string.Format("Moving to {0} for breath. (distance {1:0.0})",
                                                                                _ubpsSwimBreath_NearestAirSource.Name,
                                                                                _ubpsSwimBreath_NearestAirSource.Distance);
                                        })),

                                    UtilityBehaviorPS_MoveTo(context => _ubpsSwimBreath_NearestAirSource.Location,
                                                            context => _ubpsSwimBreath_NearestAirSource.Name)
                                    )
                                )
                        )),


                    // If we're a Warlock, refresh Unending Breath as needed...
                    new DecoratorThrottled(ThrottleTimer_WarlockBreath,
                        ret => (SpellManager.CanCast(SpellId_WarlockUnendingBreath)
                                && (AuraTimeLeft(AuraId_WarlockUnendingBreath) <= Timer_AuraRefresh_EnduringBreath)),
                        new Action(delegate { SpellManager.Cast(SpellId_WarlockUnendingBreath); })),


                    // If time to breathe, do something about it...
                    new DecoratorThrottled(ThrottleTimer_BreathCheck,
                        ret => IsBreathNeeded(),
                        new PrioritySelector(

                            // If we're a Druid, switch to Aquatic form for breath...
                            new Decorator(ret => (SpellManager.CanCast(SpellId_DruidAquaticForm)
                                                    && !Me.HasAura(AuraId_DruidAquaticForm)),
                                new Action(delegate
                                {
                                    SpellManager.Cast(SpellId_DruidAquaticForm);
                                    TreeRoot.StatusText = "Switching to Aquatic Form for breath";
                                    _ubpsSwimBreath_IsBreathNeeded = true;
                                })),


                            // Otherwise, we need to deal with 'normal' way to catch breath...
                            new Action(delegate
                            {
                                _ubpsSwimBreath_NearestAirSource = GetNearestAirSource();
                                LogInfo("Moving to {0} for breath. (distance {1:0.0})",
                                        _ubpsSwimBreath_NearestAirSource.Name, _ubpsSwimBreath_NearestAirSource.Distance);
                                _ubpsSwimBreath_IsBreathNeeded = true;
                            })
                        ))
                )));         
        }
        private bool _ubpsSwimBreath_IsBreathNeeded;
        private WoWObject _ubpsSwimBreath_NearestAirSource;
        private MirrorTimerInfo _ubpsSwimBreath__SwimBreathMirrorTimer { get { return (Me.GetMirrorTimerInfo(MirrorTimerType.Breath)); } }
    }
}
#endif
