using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.POI;
using Styx.Helpers;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;

namespace DefaultPriest
{
    public partial class Priest : CombatRoutine
    {
        private Composite CreateCombatBehavior 
        {
            get
            {
                return new PrioritySelector(
                            CheckAndClearDeadTarget,
                            CheckAndAssistPet,
                            CheckAndAssistRaFLeader,
                            CheckAndTargetEnemyPlayer,
                            CheckAndTargetAggroMob,
                            CheckAndTargetTotems,

                            new DecoratorEx(ret => CT.NotValid(),
                                new ActionIdle()),

                            new DecoratorEx(ret => !Me.IsSafelyFacing(CT),
                                new Action(delegate
                                {
                                    WoWMovement.MoveStop();
                                    CT.Face();
                                    Thread.Sleep(100);
                                    return RunStatus.Failure;
                                })),

                            new DecoratorEx(ret => !CT.InLineOfSight,
                                new Action(delegate
                                    {
                                        WoWPoint movePoint = WoWMovement.CalculatePointFrom(CT.Location, 3);

                                        Navigator.MoveTo(movePoint);
                                        Thread.Sleep(100);
                                        return RunStatus.Success;
                                    })),

                            CheckAndUsePotions,
                            CheckAndHandleAdds,
                            CheckAndUseRacials,

                            new DecoratorEx(ret => !Me.Stunned && !Me.Fleeing && !Me.Pacified && !Me.Possessed && !Me.Silenced,
                                new PrioritySelector(
                                    CheckAndUseWand(ret => (CT.CreatureType == WoWCreatureType.Totem ||
                                                            CT.CurrentHealth == 1)),
                                    new DecoratorEx(ret => IsInBattleground,
                                        new Switch<string>(r => CurrentSpec,
                                            new SwitchArgument<string>(PriestLowPvPDpsRotation, PriestTalentSpec.Lowbie.ToString()),
                                            new SwitchArgument<string>(PriestDiscPvPDpsRotation, PriestTalentSpec.Discipline.ToString()),
                                            new SwitchArgument<string>(PriestHolyPvPDpsRotation, PriestTalentSpec.Holy.ToString()),
                                            new SwitchArgument<string>(PriestShadowPvPDpsRotation, PriestTalentSpec.Shadow.ToString())
                                            )),

                                    new DecoratorEx(ret => !IsInBattleground,
                                        new Switch<string>(r => CurrentSpec,
                                            new SwitchArgument<string>(PriestLowDpsRotation, PriestTalentSpec.Lowbie.ToString()),
                                            new SwitchArgument<string>(PriestDiscDpsRotation, PriestTalentSpec.Discipline.ToString()),
                                            new SwitchArgument<string>(PriestHolyDpsRotation, PriestTalentSpec.Holy.ToString()),
                                            new SwitchArgument<string>(PriestShadowDpsRotation, PriestTalentSpec.Shadow.ToString())
                                            ))
                            
                            )));
            }
        }

        #region Composites

        #region CheckAndUseRacials

        private Composite CheckAndUseRacials
        {
            get
            {
                return new PrioritySelector(
                            CreateSpellCheckAndCast("Will of the Forsaken",
                                    ret => Me.Auras.FirstOrDefault(x => x.Value.Spell.Mechanic == WoWSpellMechanic.Fleeing ||
                                                                        x.Value.Spell.Mechanic == WoWSpellMechanic.Charmed ||
                                                                        x.Value.Spell.Mechanic == WoWSpellMechanic.Horrified ||
                                                                        x.Value.Spell.Mechanic == WoWSpellMechanic.Asleep).Value != null &&
                                           Instance.Settings.UseWotF),
                            CreateSpellCheckAndCast("Every Man for Himself",
                                    ret => Me.Auras.FirstOrDefault(x => x.Value.Spell.Mechanic == WoWSpellMechanic.Charmed ||
                                                                        x.Value.Spell.Mechanic == WoWSpellMechanic.Horrified ||
                                                                        x.Value.Spell.Mechanic == WoWSpellMechanic.Asleep ||
                                                                        x.Value.Spell.Mechanic == WoWSpellMechanic.Disoriented ||
                                                                        x.Value.Spell.Mechanic == WoWSpellMechanic.Frozen ||
                                                                        x.Value.Spell.Mechanic == WoWSpellMechanic.Polymorphed ||
                                                                        x.Value.Spell.Mechanic == WoWSpellMechanic.Rooted ||
                                                                        x.Value.Spell.Mechanic == WoWSpellMechanic.Sapped ||
                                                                        x.Value.Spell.Mechanic == WoWSpellMechanic.Slowed ||
                                                                        x.Value.Spell.Mechanic == WoWSpellMechanic.Snared ||
                                                                        x.Value.Spell.Mechanic == WoWSpellMechanic.Stunned).Value != null &&
                                           Instance.Settings.UseEMfHs),
                            CreateSpellCheckAndCast("Berserking",
                                    ret => Me.HealthPercent <= 60 || Adds.Count > 0)
                            );
            }
        }

        #endregion

        #region CheckAndUsePotions

        private static Stopwatch potionTimer = new Stopwatch();
        private Composite CheckAndUsePotions
        {
            get
            {
                return new PrioritySelector(
                            new DecoratorEx(ret => potionTimer.ElapsedMilliseconds >= 120000,
                                new Action(ret => potionTimer.Reset())),

                            new DecoratorEx(ret => Me.HealthPercent <= (double)Instance.Settings.HealthPotHealth &&
                                                 Instance.Settings.UseHealthPot &&
                                                 !potionTimer.IsRunning,
                                new Action(delegate
                                    {
                                        Log("Using Health Potion");
                                        Healing.UseHealthPotion();
                                        potionTimer.Start();
                                        return RunStatus.Success;
                                    })),

                            new DecoratorEx(ret => Me.ManaPercent <= (double)Instance.Settings.ManaPotMana &&
                                                 Instance.Settings.UseManaPot &&
                                                 !potionTimer.IsRunning,
                                new Action(delegate
                                    {
                                        Log("Using Mana Potion");
                                        Healing.UseManaPotion();
                                        potionTimer.Start();
                                        return RunStatus.Success;
                                    })));
            }
        }

        #endregion

        #region CheckAndAssistRaFLeader

        private Composite CheckAndAssistRaFLeader
        {
            get
            {
                return new PrioritySelector(
                            new DecoratorEx(ret => Me.IsInParty &&
                                                 !Me.IsInInstance &&
                                                 Styx.Logic.RaFHelper.Leader.Valid() &&
                                                 Styx.Logic.RaFHelper.Leader.CurrentTarget.Valid() &&
                                                 Styx.Logic.RaFHelper.Leader.CurrentTarget.HealthPercent < 100 && 
                                                 (CT.NotValid() || CT != Styx.Logic.RaFHelper.Leader.CurrentTarget),
                                new Action(delegate
                                    {
                                        Log("Assisting party Leader");
                                        Styx.Logic.RaFHelper.Leader.CurrentTarget.Target();
                                        Thread.Sleep(100);
                                        return RunStatus.Success;
                                    })));
            }
        }

        #endregion

        #region CheckAndTargetEnemyPlayer

        private Composite CheckAndTargetEnemyPlayer
        {
            get
            {
                return new PrioritySelector(
                            new DecoratorEx(ret => CT.Valid() && CT.IsPlayer && CT.IsFriendly,
                                new Action(delegate
                                    {
                                        Log("Targeting a friendly player. Clearing target");
                                        Me.ClearTarget();
                                        Thread.Sleep(100);
                                        return RunStatus.Success;
                                    })),
                            new DecoratorEx(ret => !IsInBattleground && CT.Valid() && !CT.IsPlayer &&
                                                 ObjectManager.GetObjectsOfType<WoWPlayer>().Exists(x => x.Valid() && x.IsTargetingMeOrPet && x.IsHostile && x.Aggro),
                                new Action(delegate
                                {
                                    Log("A player is attacking us. Targeting them!");
                                    ObjectManager.GetObjectsOfType<WoWPlayer>().Find(x => x.Valid() && x.IsTargetingMeOrPet && x.IsHostile && x.Aggro).Target();
                                    Thread.Sleep(100);
                                    return RunStatus.Success;
                                })));
            }
        }

        #endregion

        #region CheckAndHandleAdds

        private Composite CheckAndHandleAdds
        {
            get
            {
                return new PrioritySelector(
                            new DecoratorEx(ret => Me.Class == WoWClass.Priest &&
                                                   Adds.Count > 0 &&
                                                   Instance.Settings.DotAdds &&
                                                   !Me.IsInInstance &&
                                                   CT.HasAura("Shadow Word: Pain") &&
                                                   (CT.HasAura("Vampiric Touch") || !SpellManager.HasSpell("Vampiric Touch")) && 
                                                   (CT.HasAura("Devouring Plague") || !SpellManager.HasSpell("Devouring Plague")) &&
                                                   Adds.Exists(unit => !unit.HasAura("Shadow Word: Pain")),
                                new Action(delegate
                                    {
                                        Log("Targeting add...");
                                        Adds.Find(unit => !unit.HasAura("Shadow Word: Pain")).Target();
                                        Thread.Sleep(100);
                                        return RunStatus.Success;
                                    })));
            }
        }

        #endregion

        #endregion
    }
}