using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic;
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
        private Composite CreatePreCombatBuffsBehavior
        {
            get
            {
                return
                    new PrioritySelector(
                        CreateBuffCheckAndCast("Fear Ward", ret => Instance.Settings.UsePreCombatFearWard),
                        CreateBuffCheckAndCast("Inner Fire", ret => Instance.Settings.UsePreCombatInnerFire),
                        CreateBuffCheckAndCast("Power Word: Fortitude",
                            ret => !Me.HasAura("Blood Pact") &&
                                    !Me.HasAura("Power Word: Fortitude") &&
                                    !Me.HasAura("Qiraji Fortitude") &&
                                    !Me.HasAura("Commanding Shout") &&
                                    Instance.Settings.UsePreCombatPWF),
                        CreateBuffCheckAndCast("Vampiric Embrace", ret => Instance.Settings.UsePreCombatVampiricEmbrace),
                        CreateBuffCheckAndCast("Shadowform", ret => Instance.Settings.UsePreCombatShadowform),
                        CreateBuffCheckAndCast("Shadow Protection", ret => Instance.Settings.UsePreCombatShadowProtection));
            }
        }

        #region NeedtoBuffPlayers

        private static WoWPlayer BuffTarget;
        private static WoWSpell BuffSpell;
        private static WoWPlayer rezTarget;
        private static Stopwatch rezTimer = new Stopwatch();

        public class NeedToBuffPlayers : Decorator
        {
            public NeedToBuffPlayers(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (!ObjectManager.IsInGame || Me.NotValid())
                    return false;

                if (rezTimer.ElapsedMilliseconds >= 15000)
                {
                    rezTimer.Reset();
                    rezTarget = null;
                }

                if (IsInBattleground && !Me.HasAura("Preperation")) return false;
                List<WoWPlayer> playerList = Me.IsInRaid ? Me.RaidMembers : Me.IsInParty ? Me.PartyMembers : null;
                if (playerList == null) return false;

                foreach (WoWPlayer unit in playerList)
                {
                    if (unit != Me &&
                        unit.Dead &&
                        rezTarget != unit &&
                        !IsInBattleground &&
                        CanCast("Resurrection", unit))
                    {
                        rezTarget = unit;
                        rezTimer.Start();
                        BuffTarget = unit;
                        BuffSpell = SpellManager.Spells["Resurrection"];
                        return true;
                    }
                    else if (IsInBattleground && !Me.HasAura("Preperation"))
                    {
                        return false;
                    }
                    else if (unit.IsAlive && unit.Distance < 60)
                    {
                        if (Instance.Settings.UsePartyPWF &&
                            !unit.HasAura("Blood Pact") &&
                            !unit.HasAura("Power Word: Fortitude") &&
                            !unit.HasAura("Qiraji Fortitude") &&
                            !unit.HasAura("Commanding Shout") &&
                            CanCast("Power Word: Fortitude", unit) &&
                            Me.ManaPercent >= (double)Priest.Instance.Settings.BuffMana)
                        {
                            BuffTarget = unit;
                            BuffSpell = SpellManager.Spells["Power Word: Fortitude"];
                            return true;
                        }
                        else if (Instance.Settings.UsePartyShadowProtection &&
                                    !unit.HasAura("Shadow Protection") &&
                                    CanCast("Shadow Protection") &&
                                    Me.ManaPercent >= (double)Priest.Instance.Settings.BuffMana)
                        {
                            BuffTarget = unit;
                            BuffSpell = SpellManager.Spells["Shadow Protection"];
                            return true;
                        }
                    }
                }
                return false;
            }
        }

        public class BuffPlayers : Action
        {
            protected override RunStatus Run(object context)
            {
                if (!BuffTarget.InLineOfSight)
                {
                    Navigator.MoveTo(BuffTarget.Location);
                    return RunStatus.Running;
                }
                else if (BuffTarget.Distance > BuffSpell.MaxRange)
                {
                    Navigator.MoveTo(WoWMovement.CalculatePointFrom(BuffTarget.Location, BuffSpell.MaxRange - 5));
                    return RunStatus.Running;
                }

                Log("Casting {0} on {1}", BuffSpell.Name, BuffTarget.Name);
                bool result = SpellManager.Cast(BuffSpell, BuffTarget);
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }

        #endregion
    }
}