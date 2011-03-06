using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.Logic.Pathing;
using System.Threading;

namespace PrinceOfDarkness
{
    public class Spells
    {
        /* Global spells */
        public static WoWSpell SoulHarvest { get { return WoWSpell.FromId(79268); } }
        public static WoWSpell ShadowBolt { get { return WoWSpell.FromId(686); } }
        public static WoWSpell SummonImp { get { return WoWSpell.FromId(688); } }
        public static WoWSpell Immolate { get { return WoWSpell.FromId(348); } }
        public static WoWSpell Corruption { get { return WoWSpell.FromId(172); } }
        public static WoWSpell LifeTap { get { return WoWSpell.FromId(1454); } }
        public static WoWSpell DrainLife { get { return WoWSpell.FromId(689); } }
        public static WoWSpell DemonArmor { get { return WoWSpell.FromId(687); } }
        public static WoWSpell SummonVoidwalker { get { return WoWSpell.FromId(697); } }
        public static WoWSpell CreateHealthstone { get { return WoWSpell.FromId(6201); } }
        public static WoWSpell DrainSoul { get { return WoWSpell.FromId(1120); } }
        public static WoWSpell Soulburn { get { return WoWSpell.FromId(74434); } }
        public static WoWSpell BaneOfAgony { get { return WoWSpell.FromId(980); } }
        public static WoWSpell HealthFunnel { get { return WoWSpell.FromId(755); } }
        public static WoWSpell Fear { get { return WoWSpell.FromId(5782); } }
        public static WoWSpell SoulLink { get { return WoWSpell.FromId(19028); } }
        public static WoWSpell SummonSuccubus { get { return WoWSpell.FromId(712); } }
        public static WoWSpell SummonFelhunter { get { return WoWSpell.FromId(691); } }
        public static WoWSpell DeathCoil { get { return WoWSpell.FromId(6789); } }
        public static WoWSpell SummonInfernal { get { return WoWSpell.FromId(1122); } }
        public static WoWSpell SoulFire { get { return WoWSpell.FromId(6353); } }
        public static WoWSpell FelArmor { get { return WoWSpell.FromId(28176); } }
        public static WoWSpell CreateSoulStone { get { return WoWSpell.FromId(693); } }
        public static WoWSpell Soulshatter { get { return WoWSpell.FromId(29858); } }
        public static WoWSpell HowlOfTerror { get { return WoWSpell.FromId(5484); } }

        /* Special global spells */
        public static WoWSpell UseSoulStone { get { return WoWSpell.FromId(20707); } }
        public static WoWSpell UseHealthStone { get { return WoWSpell.FromId(6262); } }
        //XXX BUGGY - RMVD public static WoWSpell Shoot { get { return WoWSpell.FromId(5019); } }

        /* Affliction spells */
        public static WoWSpell Haunt { get { return WoWSpell.FromId(48181); } }
        public static WoWSpell SoulSwap { get { return WoWSpell.FromId(86121); } }
        public static WoWSpell UnstableAffliction { get { return WoWSpell.FromId(30108); } }

        /* Demonology spells */
        public static WoWSpell SummonFelguard { get { return WoWSpell.FromId(30146); } }
        public static WoWSpell HandOfGuldan { get { return WoWSpell.FromId(71521); } }
        public static WoWSpell Metamorphosis { get { return WoWSpell.FromId(59672); } }
        public static WoWSpell ImmolationAura { get { return WoWSpell.FromId(50589); } }

        /* Pet spells used by the bot */
        //MUST BE CALLED BY FELPUPPY ONLY
        public static WoWSpell VoidWalker_Sacrifice { get { return WoWSpell.FromId(7812); } } //Because *I'm blue, da be di da be da..*.
        public static WoWSpell Felguard_Felstorm { get { return WoWSpell.FromId(89751); } } //phat aoe skill
        public static WoWSpell Felguard_AxeToss { get { return WoWSpell.FromId(89766); } } //stun, cooldown
        
        /* skills used to detect lock's spec. */
        public static WoWSpell SPEC_Affliction { get { return UnstableAffliction; } }
        public static WoWSpell SPEC_Demonology { get { return SummonFelguard; } }

        /**************************************************************************************/

        /* Helpers */

        public static bool PlayerNeedsBuff(WoWSpell spell)
        {
            return PlayerNeedsOneOfTheseBuffs(spell);
        }

        public static bool CanCast(WoWSpell s)
        {
            return
                !Locked &&
                !PrinceOfDarkness.Me.IsCasting &&
                s.CanCast &&
                SpellManager.HasSpell(s) &&
                !s.Cooldown;
        }

        public static bool PlayerNeedsOneOfTheseBuffs(params WoWSpell[] spells)
        {
            //returns true if player has none of these buffs and is able to cast at least one of them

            var auras = PrinceOfDarkness.Me.GetAllAuras();
            foreach (var spell in spells)
            {
                //a few fixes...
                //in those cases, check by buff name instead of spell id (less accurate)
                if (spell.Id == Spells.SoulLink.Id && PrinceOfDarkness.Me.Pet != null)
                {
                    if (auras.Exists(a => a.Name == spell.Name))
                        return false;
                }
                else
                {
                    if (auras.Exists(a => a.SpellId == spell.Id))
                        return false;
                }
            }

            return spells.ToList().Exists(s => SpellManager.HasSpell(s));
        }

        private static bool spellLock = false;
        public static bool Locked { get { return spellLock; } }
        public static void ForceUnlock()
        {
            spellLock = false;
        }

        private static Random Rand = new Random();
        public static void HandleSpellSucceeded(object sender, LuaEventArgs args)
        {
            if (args.Args[0].ToString() != "player")
                return;

            var spellName = args.Args[1].ToString();

            PrinceOfDarkness.Debug("Successfully cast " + spellName);

            //prevent CC checking pet presence for a sew seconds if we just summoned something
            if (spellName == SummonFelhunter.Name
                || spellName == SummonImp.Name
                || spellName == SummonSuccubus.Name
                || spellName == SummonVoidwalker.Name
                || spellName == SummonFelguard.Name)
            {
                PrinceOfDarkness.Debug("Detected summoning spell success! Resetting checkpet timer.");
                PrinceOfDarkness.PetCheckTimer = DateTime.Now.AddSeconds(10);
            }

            //wait a few ms before unlocking spell mgr
            Thread.Sleep(Rand.Next(200, 300));
            spellLock = false;
        }
        public static void HandleSpellFailure(object sender, LuaEventArgs args)
        {
            if (args.Args[0].ToString() != "player")
                return;

            PrinceOfDarkness.Debug("Failed to cast " + args.Args[1]);

            //wait a few ms before unlocking spell mgr
            Thread.Sleep(Rand.Next(100, 150));
            spellLock = false;
        }

        /********************************************************************************/

        /* Casting helpers */

        public enum CastResult
        {
            MOVING_CLOSER,
            CANNOT_CAST,
            NO_TARGET,
            ACK
        }

        public static CastResult Cast(WoWSpell spell, bool useSoulburn)
        {
            //hax - using items by calling their spell id isnt working!
            if (spell.Id == UseHealthStone.Id || spell.Id == UseSoulStone.Id)
            {
                PrinceOfDarkness.Debug("Detected item use instead of spell cast.");
                if (spellLock || PrinceOfDarkness.Me.IsCasting)
                    return CastResult.CANNOT_CAST;

                //must check that item is in bag before!!!
                if (spell.Id == UseHealthStone.Id)
                {
                    PrinceOfDarkness.FindItem(PrinceOfDarkness.HEALTHSTONE).Use();
                }
                else if (spell.Id == UseSoulStone.Id)
                {
                    PrinceOfDarkness.FindItem(PrinceOfDarkness.SOULSTONE).Use();
                    spellLock = true;
                }
                
                return CastResult.ACK;
            }

            if (spellLock || PrinceOfDarkness.Me.IsCasting || !spell.CanCast || spell.Cooldown)
            {
                PrinceOfDarkness.Debug("Cannot cast spell " + spell.Name);
                return CastResult.CANNOT_CAST;
            }

            if (useSoulburn && !PrinceOfDarkness.SoulburnReady)
            {
                PrinceOfDarkness.Debug("Cannot cast empowered spell - soulburn not ready");
                return CastResult.CANNOT_CAST;
            }

            if (useSoulburn)
                Spells.Soulburn.Cast();

            PrinceOfDarkness.Debug("Casting spell " + spell.Name);

            WoWMovement.MoveStop();
            spell.Cast();
            spellLock = true;
            return CastResult.ACK;
        }
        public static CastResult Cast(WoWSpell spell)
        {
            return Cast(spell, false);
        }

        public static CastResult Cast(WoWSpell spell, WoWUnit target, bool useSoulburn)
        {
            if (target == null || target.Dead) {
                var name = target == null ? "no target" : target.Name;
                PrinceOfDarkness.Debug("Cannot cast spell " + spell.Name + " on " + name + ": invalid target");
                return CastResult.NO_TARGET;
            }

            if (!target.InLineOfSight || target.Distance >= spell.MaxRange)
            {
                PrinceOfDarkness.Debug("Cannot cast spell " + spell.Name + " on " + target.Name + ": not in sight/too far");
                
                //move closer
                Navigator.MoveTo(target.Location);
                return CastResult.MOVING_CLOSER;
            }

            if (spellLock || PrinceOfDarkness.Me.IsCasting || !spell.CanCast || spell.Cooldown)
            {
                PrinceOfDarkness.Debug("Cannot cast spell " + spell.Name);
                return CastResult.CANNOT_CAST;
            }

            if (useSoulburn && !PrinceOfDarkness.SoulburnReady)
            {
                PrinceOfDarkness.Debug("Cannot cast empowered spell on target - soulburn not ready");
                return CastResult.CANNOT_CAST;
            }

            if (useSoulburn)
                Spells.Soulburn.Cast();

            PrinceOfDarkness.Debug("Casting spell " + spell.Name + " on target " + target.Name);
            WoWMovement.MoveStop();
            target.Face();
            SpellManager.Cast(spell, target);
            spellLock = true;
            return CastResult.ACK;
        }
        public static CastResult Cast(WoWSpell spell, WoWUnit target)
        {
            return Cast(spell, target, false);
        }
    }
}
