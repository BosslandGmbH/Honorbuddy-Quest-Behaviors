using System;
using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using TreeSharp;
using System.Threading;
using Styx.Logic.Pathing;
using Action = TreeSharp.Action;
using Sequence = TreeSharp.Sequence;

namespace DefaultMage
{
    public partial class DefaultMage
    {
        private Composite _pullBehavior;
        public override Composite PullBehavior
        {
            get
            {
                if (_pullBehavior == null)
                {
                    Log("Creating 'Pull' behavior");
                    _pullBehavior = CreatePullBehavior();
                }

                return _pullBehavior;
            }
        }

        private static readonly WaitTimer PullTimer = WaitTimer.TenSeconds;
        /// <summary>
        /// Creates the behavior used for pulling mobs, (approach, attack)
        /// </summary>
        /// <returns></returns>
        private PrioritySelector CreatePullBehavior()
        {
            return new PrioritySelector(

                // Use leaders target
                //Added instance Switch for Instancebuddy support Added Version 1.0.1
                new Decorator(
                    ret =>
                    Me.IsInParty && !Me.IsInInstance && RaFHelper.Leader != null && RaFHelper.Leader.GotTarget && Me.GotTarget &&
                    Me.CurrentTargetGuid != RaFHelper.Leader.CurrentTargetGuid,
                    new Action(ret =>
                               RaFHelper.Leader.CurrentTarget.Target())),

                // Clear target and return failure if it's tagged by someone else
                new Decorator(ret => !Me.IsInParty && Me.GotTarget && Me.CurrentTarget.TaggedByOther,
                              new Action(delegate
                              {
                                  SpellManager.StopCasting();
                                  Log("Current target is not tagged by me, Aborting pull!");
                                  Blacklist.Add(Me.CurrentTarget, TimeSpan.FromMinutes(30));
                                  Me.ClearTarget();
                                  return RunStatus.Failure;
                              })
                    ),

                // If we are casting we assume we are already pulling so let it 'return' smoothly. 
                // if we are in combat pull suceeded and the combat behavior should run
                new Decorator(ret => (Me.IsCasting || Me.Combat) && Me.CurrentTarget.Distance < PullDistance + 3,
                              new Action(delegate { return RunStatus.Success; })),

                // Make sure we got a proper target
                new Decorator(ret => !Me.GotTarget && !Me.IsInParty,
                              new Action(delegate
                              {
                                  Targeting.Instance.TargetList[0].Target();
                                  WoWMovement.Face();
                                  Thread.Sleep(100);
                                  return RunStatus.Success;
                              })),

                // Blacklist target's we can't move to
                new Decorator(ret => Navigator.GeneratePath(Me.Location, Me.CurrentTarget.Location).Length <= 0,
                              new Action(delegate
                              {
                                  Blacklist.Add(Me.CurrentTargetGuid, TimeSpan.FromDays(365));
                                  Log("Failed to generate path to: {0} blacklisted!",
                                      Me.CurrentTarget.Name);
                                  return RunStatus.Success;
                              })
                    ),

                // Move closer to the target if we are too far away or in !Los
                new Decorator(ret => Me.GotTarget && (Me.CurrentTarget.Distance > PullDistance - 1|| !Me.CurrentTarget.InLineOfSight),
                              new Action(delegate
                              {
                                  Log("Moving towards:{0}", Me.CurrentTarget);
                                  Navigator.MoveTo(Me.CurrentTarget.Location);
                              })),

                // Stop moving if we are moving
                new Decorator(ret => Me.IsMoving,
                              new Action(ret => WoWMovement.MoveStop())),

                // Face the target if we aren't
                new Decorator(ret => Me.GotTarget && !Me.IsFacing(Me.CurrentTarget),
                              new Action(ret => WoWMovement.Face())
                    ),

                new PrioritySelector(



              //Pull spells, Check Arcane Missles if Proc
               new Decorator(ret => SpellManager.CanCast("Arcane Missiles") && Me.Auras.ContainsKey("Arcane Missles"),
                    new PrioritySelector(
                        CreateSpellCheckAndCast("Arcane Missiles")
                        )),

                //Low Level Forced Fireball, should never be run after level 6
               new Decorator(ret => !SpellManager.HasSpell("Frostbolt"),
                   new PrioritySelector(
                       CreateSpellCheckAndCast("Fireball")
                       )),

               new Decorator(ret => SpellManager.HasSpell("Frostbolt"),
                    new PrioritySelector(
                        CreateSpellCheckAndCast("Frostbolt")
                        )),


               new Decorator(ret => SpellManager.HasSpell("Fireball") && SpellManager.HasSpell("Frostbolt") && !SpellManager.CanCast("Frostbolt"),
                    new PrioritySelector(
                        CreateSpellCheckAndCast("Fireball")
                        ))


                        
                                          )
                );
        }
    }
}
