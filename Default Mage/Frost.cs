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

   
        private Composite Frost_rotation()
        {
            return new PrioritySelector(
                //Frost Elemental Summon if Able. 
                new Decorator(ret => !Me.Mounted && SpellManager.HasSpell("Summon Water Elemental") && !Me.GotAlivePet,
                              new PrioritySelector(
                                  CreateBuffCheckAndCast("Summon Water Elemental")
                                  )),

                //Cast ColdSnap if 2 or more Frost Abilitys are on cooldown.
                new Decorator(ret => ColdSnapCheck() && SpellManager.HasSpell("Cold Snap") && SpellManager.CanCast("Cold Snap"),
                  new Action(ret =>  SpellManager.Cast("Cold Snap"))),

                //ManaGem Logic
               new Decorator(ret => HaveManaGem() && Me.ManaPercent <= 10,
                   new Action(ctx => UseManaGem())),

                //SheepLogic - Disabled in Battlegrounds and Instances.
               new Decorator(ret => NeedTocastSheep() && !Me.IsInInstance && !Battlegrounds.IsInsideBattleground,
                   new Action(ctx => SheepLogic())),

                //FrostNova Logic - Backup -- Only used in battlegound - Uses Blink!
               new Decorator(ret => Me.CurrentTarget.Distance < 6 && Battlegrounds.IsInsideBattleground && SpellManager.CanCast("Frost Nova"),
                   new Sequence(
                       new Action(ret => SpellManager.Cast("Frost Nova")),
                       new Action(ctx => BackUpPVP())
                           )),

                //FrostNova Logic - Backup - Disable if Poly is found -- Enabled if not in an instance and not in a battlegound
                //Added FrostNova Disable if CurrentTarget is Less then 10% Health, to prevent frostnova killing it. Added Version 1.0.1
               new Decorator(ret => !HasSheeped() && Me.CurrentTarget.HealthPercent >= 10 && Me.CurrentTarget.Distance < 6 && !Me.IsInInstance && !Battlegrounds.IsInsideBattleground && SpellManager.CanCast("Frost Nova"),
                   new Sequence(
                       new Action(ret => SpellManager.Cast("Frost Nova")),
                       new Action(ctx => BackUp())
                           )),

                //CounterSpell
             new Decorator(ret => SpellManager.HasSpell("Counterspell") && SpellManager.CanCast("Counterspell") && Me.CurrentTarget.IsCasting,
                   new PrioritySelector(
                       CreateSpellCheckAndCast("Counterspell"))),
                //IceBarrier Logic use when below 50% Health && ManaShield is not Currently on Me.
             new Decorator(ret => !Me.Auras.ContainsKey("Ice Barrier") && Me.HealthPercent <= 70 && SpellManager.HasSpell("Ice Barrier") && SpellManager.CanCast("Ice Barrier") && !Me.Auras.ContainsKey("Mana Shield"),
                   new PrioritySelector(
                       CreateSpellCheckAndCast("Ice Barrier"))),

            //ManaSheild Logic use when below 50% Health
             new Decorator(ret => !Me.Auras.ContainsKey("Mana Shield") && Me.HealthPercent <= 70 && SpellManager.HasSpell("Mana Shield") && SpellManager.CanCast("Mana Shield") && !Me.Auras.ContainsKey("Ice Barrier"),
                   new PrioritySelector(
                       CreateSpellCheckAndCast("Mana Shield"))),
                //Evocation Use on Low Mana. below 30% Will Cast if Mana Sheild is not known, Or After ManaSheild buff is on Self.
            new Decorator(ret => LegacySpellManager.KnownSpells.ContainsKey("Evocation") && SpellManager.CanCast("Evocation") && Me.ManaPercent < 30 && (!SpellManager.HasSpell("Mana Shield") || Me.Auras.ContainsKey("Mana Shield") || Me.Auras.ContainsKey("Ice Barrier")),
                   new PrioritySelector(
                       CreateSpellCheckAndCast("Evocation"))),
                //Low Level Combat, FireBall Spam.
               new Decorator(ret => !SpellManager.HasSpell("Frostbolt"),
                   new PrioritySelector(
                       CreateSpellCheckAndCast("Fireball"))),
                //Fireblast Finisher at 20%
               new Decorator(ret => SpellManager.CanCast("Fire Blast", true) && Me.CurrentTarget.HealthPercent <= 20,
                    new PrioritySelector(
                        CreateSpellCheckAndCast("Fire Blast")
                        )),
                //Arcane Missles if Proc
             new Decorator(ret => SpellManager.CanCast("Arcane Missiles") && Me.Auras.ContainsKey("Arcane Missiles!"),
                    new PrioritySelector(
                        CreateSpellCheckAndCast("Arcane Missiles")
                        )),
                //Brain Freeze Proc - FireBall
             new Decorator(ret => SpellManager.CanCast("Fireball") && !SpellManager.HasSpell("Frostfire Bolt") && Me.ActiveAuras.ContainsKey("Brain Freeze"),
                    new PrioritySelector(
                        CreateSpellCheckAndCast("Fireball")
                        )),
                //Brain Freeze Proc - FrostFirebolt 
             new Decorator(ret => SpellManager.CanCast("Frostfire Bolt") && Me.ActiveAuras.ContainsKey("Brain Freeze"),
                    new PrioritySelector(
                        CreateSpellCheckAndCast("Frostfire Bolt")
                        )),
                        
                //Fingers of Frost Proc - DeepFreeze
             new Decorator(ret => Me.ActiveAuras.ContainsKey("Fingers of Frost"),
                    new PrioritySelector(
                        CreateSpellCheckAndCast("Deep Freeze")
                        )),

              new Decorator(ret => Me.ActiveAuras.ContainsKey("Fingers of Frost"),
                    new PrioritySelector(
                        CreateSpellCheckAndCast("Ice Lance")
                        )),

                //FostBolt Main Spell.
               new Decorator(ret => SpellManager.CanCast("Frostbolt"),
                   new PrioritySelector(
                        CreateSpellCheckAndCast("Frostbolt")
                        )),

                //FireBall if Frostbolt is Unable to Cast for some reason.
               new Decorator(ret => SpellManager.CanCast("Fireball") && SpellManager.HasSpell("Frostbolt") && !SpellManager.CanCast("Frostbolt"),
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
