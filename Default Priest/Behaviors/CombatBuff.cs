using System.Linq;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;

namespace DefaultPriest
{
    public partial class Priest : CombatRoutine
    {
        private Composite CreateCombatBuffBehavior
        {
            get
            {
                return new PrioritySelector(
                            CreateSpellCheckAndCast("Dispersion",
                                ret => Me.Shapeshift == ShapeshiftForm.Shadow &&
                                        Me.Stunned &&
                                        Instance.Settings.UseShadowDispersionWhenStunned),
                            CreateBuffCheckAndCast("Power Infusion"),
                            CreateBuffCheckAndCast("Fear Ward", ret => Instance.Settings.UseCombatFearWard),
                            CreateBuffCheckAndCast("Inner Fire", ret => Instance.Settings.UseCombatInnerFire),
                            CreateBuffCheckAndCast("Power Word: Fortitude",
                                ret => !Me.HasAura("Blood Pact") &&
                                        !Me.HasAura("Power Word: Fortitude") &&
                                        !Me.HasAura("Qiraji Fortitude") &&
                                        !Me.HasAura("Commanding Shout") &&
                                        Instance.Settings.UseCombatPWF),
                            CreateBuffCheckAndCast("Vampiric Embrace", ret => Instance.Settings.UseCombatVampiricEmbrace),
                            CreateBuffCheckAndCast("Shadowform", ret => Instance.Settings.UseCombatShadowform),
                            CreateBuffCheckAndCast("Shadow Protection", ret => Instance.Settings.UseCombatShadowProtection)
                            );
            }
        }
    }
}