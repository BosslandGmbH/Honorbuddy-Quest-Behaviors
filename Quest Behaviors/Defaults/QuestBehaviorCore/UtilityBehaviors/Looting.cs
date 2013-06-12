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

using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.Helpers;
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
        public class LootingPS : PrioritySelector
        {
            private LootingPS()
            {
                Children = CreateChildren();
            }


            // BT visit-time properties...
            private WoWUnit CachedLootMob { get; set; }


            private List<Composite> CreateChildren()
            {
                return new List<Composite>()
                {
                    new Decorator(context => CharacterSettings.Instance.LootMobs,
                        new PrioritySelector(
                            // If current loot mob is no good, find another...
                            new Decorator(context => !Query.IsViable(CachedLootMob),
                                new ActionFail(context => { CachedLootMob = NearestLootableMob(); })),

                            // If we found a mob to loot, go for it...
                            new Decorator(context => Query.IsViable(CachedLootMob),
                                new PrioritySelector(
                                    // If too far away, move to mob...
                                     new Decorator(context =>   CachedLootMob.Distance > CachedLootMob.InteractRange,
                                         new Action(context => { Navigator.MoveTo(CachedLootMob.Location); }))
                                        //QuestBehaviorBase.UtilityBehaviorPS_MoveTo(context => SelectedLootMob.Location,
                                        //                                           context => SelectedLootMob.Name))
                                ))
                        ))
                };
            }


            private WoWUnit NearestLootableMob()
            {
                return
                   (from wowUnit in ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                    where
                        wowUnit.Lootable
                        || wowUnit.CanSkin
                    orderby wowUnit.DistanceSqr
                    select wowUnit)
                    .FirstOrDefault();
            }


            public static Composite CreateBehavior()
            {
                return new LootingPS();
            }
        }
    }
}