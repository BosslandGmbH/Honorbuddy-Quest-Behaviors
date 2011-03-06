using System;
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
        private Composite CreatePullBehavior
        {
            get
            {
                return
                    new PrioritySelector(
                        CheckAndClearTargetWhenAggro,

                        new DecoratorEx(ret => CT.NotValid(),
                            new ActionIdle()),

                        new DecoratorEx(ret => !Me.IsSafelyFacing(CT),
                            new Action(delegate
                            {
                                WoWMovement.MoveStop();
                                CT.Face();
                                Thread.Sleep(100);
                                return RunStatus.Success;
                            })),

                        CombatBehavior);
            }
        }

        #region Composites

        #region CheckAndClearTargetWhenAggro

        private Composite CheckAndClearTargetWhenAggro
        {
            get
            {
                return new PrioritySelector(
                            new DecoratorEx(ret => CT.Valid() &&
                                                 Adds.Count > 0 &&
                                                 !CT.IsTargetingMeOrPet &&
                                                 CT.HealthPercent == 100 &&
                                                 !CT.Name.ToLower().Contains("dummy") &&
                                                 !Me.IsInParty,
                                new Action(delegate
                                {
                                    Thread.Sleep(250);
                                    if (Adds.Count > 0)
                                        Adds[0].Target();

                                    Thread.Sleep(100);
                                    return RunStatus.Success;
                                })));
            }
        }

        #endregion

        #endregion
    }
}