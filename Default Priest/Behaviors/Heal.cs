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
using RaFHelper = Styx.Logic.RaFHelper;

namespace DefaultPriest
{
    public partial class Priest : CombatRoutine
    {
        private static Composite CreateHealBehavior
        {
            get
            {
                return new PrioritySelector(
                            new DecoratorEx(ret => Me.IsCasting || StyxWoW.GlobalCooldown,
                                new PrioritySelector(
                                    new DecoratorEx(ret => Me.ChanneledCastingSpellId == 0 && CT.Valid() &&
                                                           !Me.IsSafelyFacing(CT),
                                        new Action(delegate
                                        {
                                            WoWMovement.MoveStop();
                                            CT.Face();
                                            Thread.Sleep(100);
                                            return RunStatus.Failure;
                                        })),
                                    new Action(ret => wasCasting = true))),

                            new DecoratorEx(ret => wasCasting,
                                new Action(delegate
                                {
                                    wasCasting = false;
                                    Thread.Sleep(RandomNumber(200, 300));
                                    ObjectManager.Update();
                                    return RunStatus.Success;
                                })),

                             new NeedToHeal(new HealNow()));
            }
        }

        private static WoWUnit HealTarget;
        private static WoWSpell HealSpell;
        private static WoWUnit DispelTarget;
        private static WoWSpell DispelSpell;
        internal class NeedToHeal : Decorator
        {
            public NeedToHeal(Composite decorated)
                : base(decorated)
            {
            }

            protected override bool CanRun(object context)
            {
                if (!ObjectManager.IsInGame || Me.NotValid())
                    return false;
                if (Me.Dead && !Me.Stunned && !Me.Fleeing && !Me.Pacified && !Me.Possessed && !Me.Silenced)
                    return false;

                DispelTarget = null;
                DispelSpell = null;

                List<WoWUnit> healList = new List<WoWUnit>();

                if (Me.IsInRaid)
                {
                    foreach (WoWPlayer p in Me.RaidMembers)
                    {
                        if (p.Valid() && p.Distance < 60 && p.HealthPercent > 1)
                        {
                            if (RaFHelper.Leader.Valid() && RaFHelper.Leader == p)
                                continue;

                            healList.Add(p);

                            if (p.GotAlivePet)
                                healList.Add(p.Pet);
                        }
                    }
                }
                else if (Me.IsInParty)
                {
                    foreach (WoWPlayer p in Me.PartyMembers)
                    {
                        if (p.Valid() && p.Distance < 60 && p.HealthPercent > 1)
                        {
                            if (RaFHelper.Leader.Valid() && RaFHelper.Leader == p)
                                continue;

                            healList.Add(p);

                            if (p.GotAlivePet)
                                healList.Add(p.Pet);
                        }
                    }
                }

                if (RaFHelper.Leader.Valid())
                {
                    if (!SpellManager.HasSpell("Beacon of Light") && RaFHelper.Leader.HealthPercent > 60)
                        healList.Add(RaFHelper.Leader);

                    if (Me.HealthPercent > 60)
                        healList.Add(Me);
                }
                else
                {
                    if (!SpellManager.HasSpell("Beacon of Light") && Me.HealthPercent > 60)
                        healList.Add(Me);
                }

                if (healList.Count > 1)
                {
                    healList.Sort(delegate(WoWUnit u1, WoWUnit u2)
                    { return u1.CurrentHealth.CompareTo(u2.CurrentHealth); });
                }

                if (RaFHelper.Leader.Valid() && !healList.Contains(RaFHelper.Leader))
                    healList.Insert(0, RaFHelper.Leader);

                if (Me.HealthPercent > 1 && !healList.Contains(Me))
                    healList.Insert(0, Me);

                //
                Dictionary<SpellPriority, CastRequirements> heals = new Dictionary<SpellPriority,CastRequirements>();

                if (IsInBattleground)
                {
                    switch (CurrentSpec)
                    {
                        case "Discipline":
                            heals = PriestDiscHealPvP;
                            break;

                        case "Holy":
                            heals = PriestHolyHealPvP;
                            break;

                        case "Shadow":
                            heals = PriestShadowHealPvP;
                            break;
                    }

                }
                else if (Me.IsInParty && Me.Class == WoWClass.Priest &&
                        (_talentManager.Spec == 3 ||
                        _talentManager.Spec == 0))
                {
                    switch (_talentManager.Spec)
                    {
                        case 3:
                            heals = PriestShadowHealRaF;
                            break;

                        case 0:
                            heals = PriestLowHealRaF;
                            break;
                    }
                }
                else
                {
                    switch (CurrentSpec)
                    {
                        case "Lowbie":
                            heals = PriestLowHeal;
                            break;

                        case "Discipline":
                            heals = PriestDiscHeal;
                            break;

                        case "Holy":
                            heals = PriestHolyHeal;
                            break;

                        case "Shadow":
                            heals = PriestShadowHeal;
                            break;
                    }
                }

                if (heals.Count == 0)
                    return false;
        
                var items = (from k in heals.Keys
                             orderby k.Priority descending
                             select k);


                foreach (WoWUnit p in healList)
                {
                    if (p.NotValid() || p.HealthPercent < 2)
                        continue;

                    foreach (var s in items)
                    {
                        if (heals[s].Invoke(p))
                        {
                                HealTarget = p;
                                HealSpell = SpellManager.Spells[s.Name];
                                return true;
                        }
                    }
                    
                    if (p.Debuffs.Values.ToList().Exists(aura => aura.Spell.DispelType == WoWDispelType.Magic &&
                                    CanCast("Dispel Magic") &&
                                    Instance.Settings.DispelMagic &&
                                    (!Me.Combat || !Instance.Settings.DispelOnlyOOC) &&
                                    !dispelBlacklist.Contains(aura.Name)))
                    {
                        DispelTarget = p;
                        DispelSpell = SpellManager.Spells["Dispel Magic"];
                    }
                    else if (p.Debuffs.Values.ToList().Exists(aura => aura.Spell.DispelType == WoWDispelType.Disease &&
                                                                CanCast("Cure Disease") &&
                                                                Instance.Settings.RemoveDisease &&
                                                                (!Me.Combat || !Instance.Settings.DispelOnlyOOC) &&
                                                                !diseaseBlacklist.Contains(aura.Name)))
                    {
                        DispelTarget = p;
                        DispelSpell = SpellManager.Spells["Cure Disease"];
                    }
                }
                if (DispelTarget.Valid())
                    return true;

                return false;
            }
        }

        internal class HealNow : Action
        {
            protected override RunStatus Run(object context)
            {
                bool result;

                if (DispelTarget.Valid())
                {
                    if (!DispelTarget.InLineOfSight)
                    {
                        Navigator.MoveTo(WoWMathHelper.CalculatePointAtSide(DispelTarget.Location, DispelTarget.Rotation, 5, true));
                        Thread.Sleep(100);
                        return RunStatus.Running;
                    }
                    else if (DispelTarget.Distance > DispelSpell.MaxRange)
                    {
                        Navigator.MoveTo(WoWMovement.CalculatePointFrom(DispelTarget.Location, DispelSpell.MaxRange - 5));
                        Thread.Sleep(100);
                        return RunStatus.Running;
                    }

                    Log("Casting {0} on {1}", DispelSpell.Name, DispelTarget != Me ? DispelTarget.Name : "myself");
                    result = SpellManager.Cast(DispelSpell, DispelTarget);
                    wasCasting = true;
                    return result ? RunStatus.Success : RunStatus.Failure;
                }

                if (!HealTarget.InLineOfSight)
                {
                    Navigator.MoveTo(WoWMathHelper.CalculatePointAtSide(HealTarget.Location, HealTarget.Rotation, 5, true));
                    Thread.Sleep(100);
                    return RunStatus.Running;
                }
                else if (HealTarget.Distance > HealSpell.MaxRange)
                {
                    Navigator.MoveTo(WoWMovement.CalculatePointFrom(HealTarget.Location, HealSpell.MaxRange - 5));
                    Thread.Sleep(100);
                    return RunStatus.Running;
                }
                else if (Me.IsMoving && (HealSpell.CastTime > 0 || HealSpell.Name == "Penance"))
                {
                    WoWMovement.MoveStop();
                    Thread.Sleep(100);
                }

                if (Me.IsInInstance && Me.HasAura("Evangelism") && Me.Auras["Evangelism"].StackCount == 5 && CanCast("Archangel"))
                {
                    Log("Casting Archangel before healing");
                    SpellManager.Cast("Archangel");
                }

                if ((HealSpell.Name == "Flash Heal" ||
                    HealSpell.Name == "Greater Heal" ||
                    HealSpell.Name == "Heal" ||
                    HealSpell.Name == "Prayer of Healing") &&
                    CanCast("Inner Focus"))
                {
                    Log("Casting Inner Focus before healing");
                    SpellManager.Cast("Inner Focus");
                }

                Log("Casting {0} on {1}", HealSpell.Name, HealTarget != Me ? HealTarget.Name : "myself");
                result = SpellManager.Cast(HealSpell, HealTarget);
                wasCasting = true;
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
    }
}