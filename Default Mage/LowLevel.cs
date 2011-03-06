using System;
using System.Collections.Generic;
using System.Threading;
using CommonBehaviors.Actions;
using Styx;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Sequence = TreeSharp.Sequence;
using System.Linq;
using Action = TreeSharp.Action;

namespace DefaultMage
{
    public partial class DefaultMage
    {


        private Composite Low_rotation()
        {
            return new PrioritySelector(
                //FrostNova Logic - Backup - Disable if Poly is found -- Enabled if not in an instance and not in a battlegound
               new Decorator(ret => !HasSheeped() && Me.CurrentTarget.Distance < 6 && !Me.IsInInstance && !Battlegrounds.IsInsideBattleground && SpellManager.CanCast("Frost Nova"),
                   new Sequence(
                       new Action(ret => SpellManager.Cast("Frost Nova")),
                       new Action(ctx => BackUp())
                           )),
              //Arcane Missles if Proc
             new Decorator(ret => SpellManager.CanCast("Arcane Missiles") && Me.Auras.ContainsKey("Arcane Missiles!"),
                    new PrioritySelector(
                        CreateSpellCheckAndCast("Arcane Missiles")
                        )),
                //Should only be run at low level. - Fireball Till Frostbolt is Learned
               new Decorator(ret => !SpellManager.HasSpell("Frostbolt"),
                   new PrioritySelector(
                       CreateSpellCheckAndCast("Fireball"))),
                //FireBlast Finisher
               new Decorator(ret => SpellManager.CanCast("Fire Blast", true) && Me.CurrentTarget.HealthPercent <= 20,
                    new PrioritySelector(
                        CreateSpellCheckAndCast("Fire Blast")
                        )),

               //Main Spam Spell. - Frostbolt
               new Decorator(ret => SpellManager.CanCast("Frostbolt") ,
                    new PrioritySelector(
                        CreateSpellCheckAndCast("Frostbolt")
                        )),
              //Fireball if for some reason Frostbolt cant be cast. 
               new Decorator(ret => SpellManager.CanCast("Fireball")  && !SpellManager.CanCast("Frostbolt") ,
                    new PrioritySelector(
                        CreateSpellCheckAndCast("Fireball")
                        )),
                //Wand when all above Fails, Normaly on Low Mana. 
               new Decorator(ret => SpellManager.CanCast("Shoot") && IsNotWanding,
                    new PrioritySelector(
                        CreateSpellCheckAndCast("Shoot")
                        ))
          
                        );
        }
    }
}
