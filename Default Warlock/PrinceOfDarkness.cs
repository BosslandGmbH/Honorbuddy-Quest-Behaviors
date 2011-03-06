using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Styx.Combat.CombatRoutine;
using Styx;
using Styx.WoWInternals.WoWObjects;
using Styx.Helpers;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Tripper.Navigation;
using Styx.Logic;
using System.Threading;
using Styx.Logic.Pathing;

/*
 * supports affliction an demonology
 * This is Prince of Darkness v1.31
 * 
 * affliction:  http://www.wowhead.com/talent#Irur0GsGcd0bZb
 * demon:       http://www.wowhead.com/talent#IZIrrbMddRdohb
 */

namespace PrinceOfDarkness
{
    public class PrinceOfDarkness : CombatRoutine
    {
        public override WoWClass Class
        {
            get { return WoWClass.Warlock; }
        }

        public override string Name
        {
            get { return "Prince of Darkness"; }
        }

        /******************************************************************************/

        public static LocalPlayer Me { get { return StyxWoW.Me; } }

        public static void Debug(string msg, params object[] args)
        {
            Logging.WriteDebug(System.Drawing.Color.DarkViolet, "[PoD] " + msg, args);
        }
        public static void Log(string msg, params object[] args)
        {
            Logging.WriteDebug(System.Drawing.Color.DarkViolet, "Warlock: " + msg, args);
        }



        public override bool NeedRest
        {
            get
            {
                MountHandler.Pulse();

                if (Me.Mounted)
                    return false;
                if (Me.Dead || Me.IsGhost || Me.Combat)
                    return false;

                if (SpellManager.HasSpell(Spells.SoulHarvest) 
                    && (Me.HealthPercent < 50 || Me.ManaPercent < 60 || Me.CurrentSoulShards <= 2))
                {
                    Debug("NeedRest is true: life tap or eat/drink");
                    return true;
                }

                if ((Me.HealthPercent < 80 || Me.CurrentSoulShards < 3)
                    && SpellManager.HasSpell(Spells.SoulHarvest))
                {
                    Debug("NeedRest is true: I need soul harvest.");
                    return true;
                }

                // Let the harvest finish. Get our damned health back
                if ((Spells.Locked || Me.IsCasting) && Me.CastingSpellId == Spells.SoulHarvest.Id && Me.HealthPercent <= 95)
                {
                    Debug("NeedRest is true: casting soul harvest... wait.");
                    return true;
                }

                //Debug("NeedRest is false");
                return false;
            }
        }


        private static DateTime SoulStoneTimer = DateTime.Now.AddMinutes(35);
        public static DateTime PetCheckTimer = DateTime.MinValue; //prevent pet actions right after dismounting
        public static int maxAdds = 1; //see behavior.cs to edit this

        public override bool NeedPreCombatBuffs
        {
            get
            {
                MountHandler.Pulse();

                //Debug("casting spell id " + Me.CastingSpellId);
                //if (Me.CurrentTarget != null) Debug("Target faction: " + Me.CurrentTarget.FactionId);
                //return true;

                if (Me.Mounted)
                    return false;

                if (CheckPet(true))
                    return true;

                if (Me.CastingSpellId == Spells.CreateHealthstone.Id || Me.CastingSpellId == Spells.CreateSoulStone.Id)
                {
                    Debug("Need buffs: creating hearthstone or soulstone");
                    return true;
                }
                if (Me.CastingSpellId == Spells.UseSoulStone.Id)
                {
                    Debug("Need buffs: binding soulstone...");
                    return true;
                }

                if (Spells.PlayerNeedsOneOfTheseBuffs(Spells.FelArmor, Spells.DemonArmor))
                {
                    Debug("Need buffs: no armor buff");
                    return true;
                }
                if (Spells.PlayerNeedsBuff(Spells.SoulLink))
                {
                    Debug("Need buffs: no soul link");
                    return true;
                }

                if (NeedsHealthstone || NeedsSoulstone)
                {
                    Debug("Need buffs: no healthstone or soulstone");
                    return true;
                }

                if (PlayerHasItem(SOULSTONE)
                    && !UnitHelper.HasBuff(Me, Spells.UseSoulStone)
                    && DateTime.Now.CompareTo(SoulStoneTimer) >= 0)
                {
                    Debug("Need buffs: soulstone is ready to use");
                    return true;
                }
                else
                {
                    //Debug("Soulstone timer: " + DateTime.Now.Subtract(SoulStoneTimer).TotalMinutes + " minutes");
                }

                //Debug("Need buffs: none :-)");
                return false;
            }
        }


        

        public override void Initialize()
        {
            Debug("Init: binding lua events");
            Lua.Events.AttachEvent("UNIT_SPELLCAST_SUCCEEDED", Spells.HandleSpellSucceeded);
            Lua.Events.AttachEvent("UNIT_SPELLCAST_STOP", Spells.HandleSpellFailure);
            Lua.Events.AttachEvent("UNIT_SPELLCAST_FAILED", Spells.HandleSpellFailure);
            Lua.Events.AttachEvent("UI_ERROR_MESSAGE", HandleErrorMessage);
            Lua.Events.AttachEvent("PLAYER_REGEN_ENABLED", HandleCombatDone);
            Lua.Events.AttachEvent("PET_BAR_UPDATE", FelPuppy.HandlePetBarUpdated);

            Debug("Getting lua vars");
            SPELL_FAILED_UNIT_NOT_INFRONT = Lua.GetReturnVal<string>("return SPELL_FAILED_UNIT_NOT_INFRONT", 0);

            //handle pet spawn delay on dismount
            MountHandler.OnDismount += delegate
            {
                Debug("Resetting checkpet timer since player dismounted - allow calls in at least 2 seconds.");
                PetCheckTimer = DateTime.Now.AddSeconds(2);
            };

            Debug("Binding bot events...");
            BotEvents.OnBotStarted += delegate(EventArgs args)
            {
                ObjectManager.Update();
                FelPuppy.UpdatePetSkills();
                Spells.ForceUnlock();
                Behavior.Reset();
                Log("{0} is ready for action! Your warlock spec is: {1}", Name, Specialization.ToString());

                //adding a few pet spells here and there...
                if (IsDemonology)
                {
                    Log("Enabling felguard's skills management...");
                    FelPuppy.AddAutoCast(Spells.Felguard_Felstorm, FelPuppy.AutoCastCondition.PetNearTarget);
                    FelPuppy.AddAutoCast(Spells.Felguard_AxeToss, FelPuppy.AutoCastCondition.PetNearTarget);
                }
            };

            BotEvents.OnBotStopped += delegate(EventArgs args)
            {
                //clean anything needed
                FelPuppy.RemoveAllAutoCast();
                Spells.ForceUnlock();
            };
        }

        private string SPELL_FAILED_UNIT_NOT_INFRONT = null;
        private void HandleErrorMessage(object sender, LuaEventArgs args)
        {
            string msg = args.Args[0].ToString();

            if (msg == SPELL_FAILED_UNIT_NOT_INFRONT)
                FaceTarget();
        }

        private void HandleCombatDone(object sender, LuaEventArgs args)
        {
            Debug("--- Combat done! ---");
            Behavior.AddCombatStats();
        }

        public static void FaceTarget()
        {
            if (Me.CurrentTarget != null)
            {
                Debug("Correcting target facing.");
                Me.CurrentTarget.Face();
            }
        }


        public override void Rest()
        {
            MountHandler.Pulse();

            //a bit ugly, but well... as long as it works.
            if (Spells.Locked 
                || Me.IsCasting
                || Me.ActiveAuras.Any(a => a.Value.Name == "Food" || a.Value.Name == "Drink"))
            {
                //Debug("Rest: do nothing, already busy");
                return;
            }

            if (Me.ManaPercent < 80 && Me.HealthPercent > 50 && SpellManager.CanCast(Spells.LifeTap))
            {
                Debug("Rest: life tap");
                Spells.Cast(Spells.LifeTap);
                return;
            }

            if ((Me.HealthPercent < 80 || Me.CurrentSoulShards < 3) && SpellManager.CanCast(Spells.SoulHarvest))
            {
                Debug("Rest: soul harvest");
                Spells.Cast(Spells.SoulHarvest);
                return;
            }

            //this usually never happens
            if (Me.HealthPercent < 50 || Me.ManaPercent < 60)
            {
                Debug("Resting lazily with drink/food...");

                if (Me.HealthPercent < 50)
                    Styx.Logic.Common.Rest.FeedImmediate();
                if (Me.ManaPercent < 60)
                    Styx.Logic.Common.Rest.DrinkImmediate();

                //hb bug detecting auras
                Thread.Sleep(1000);
            }
        }



        public override void PreCombatBuff()
        {
            MountHandler.Pulse();

            // Wait for us to stop casting please...
            if (Spells.Locked || Me.IsCasting)
            {
                Debug("Precombat buffs: do nothing as I'm casting...");
                return;
            }

            //pet
            if (CheckPet(true))
            {
                Debug("Precombat: need a pet!");
                return;
            }

            if (Spells.PlayerNeedsBuff(Spells.FelArmor))
            {
                Debug("Precombat: need fel armor");
                Spells.Cast(Spells.FelArmor);
                return;
            }
            
            if (!SpellManager.HasSpell(Spells.FelArmor) && Spells.PlayerNeedsBuff(Spells.DemonArmor))
            {
                Debug("Precombat: need demon armor (no fel armor)");
                Spells.Cast(Spells.DemonArmor);
                return;
            }

            if (Spells.PlayerNeedsBuff(Spells.SoulLink))
            {
                Debug("Precombat: need soul link");
                Spells.Cast(Spells.SoulLink);
                return;
            }

            if (NeedsHealthstone)
            {
                Debug("Precombat: need a healthstone");
                Spells.Cast(Spells.CreateHealthstone);
                return;
            }

            if (NeedsSoulstone)
            {
                Debug("Precombat: need a soulstone");
                Spells.Cast(Spells.CreateSoulStone);
                return;
            }

            if (PlayerHasItem(SOULSTONE)
                && !UnitHelper.HasBuff(Me, Spells.UseSoulStone)
                && DateTime.Now.CompareTo(SoulStoneTimer) >= 0)
            {
                Debug("Precombat: get stoned (got it?)");
                SoulStoneTimer = DateTime.Now.AddMinutes(35);
                Spells.Cast(Spells.UseSoulStone);
                return;
            }
        }



        public override void Pull()
        {
            MountHandler.Pulse();

            //Don't do anything while casting!!
            if (Spells.Locked || Me.IsCasting)
            {
                //Debug("Pull: Casting... do nothing");
                return;
            }

            //hb bug - it sometimes call Pull() right after killing a target without looting/resting
            if (!Me.Combat)
                FelPuppy.Attack();

            Spells.Cast(Opener, Me.CurrentTarget);
        }



        public override void Combat()
        {
            //pulses whatever needs it.
            FelPuppy.Pulse();
            MountHandler.Pulse();

            if (Me.CurrentTarget == null || Me.CurrentTarget.Dead)
            {
                Debug("WARN: Me.Target is null/dead in Combat()");
                if (Targeting.Instance.FirstUnit == null || Targeting.Instance.FirstUnit.Dead || !Targeting.Instance.FirstUnit.Combat)
                {
                    // Just try to pick up our pets target
                    if (Me.Pet != null && Me.Pet.Combat && Me.Pet.CurrentTarget != null)
                    {
                        Debug("Combat: picked up pet target");
                        TargetUnit(Me.Pet.CurrentTarget);
                        return;
                    }

                    //this seems to be ok... happens when combat done. still in combat but target died
                    //Debug("Combat: oops, no target!");
                    return;
                }
                TargetUnit(Targeting.Instance.FirstUnit);
                if (Me.CurrentTarget != null)
                    Debug("Picked up new target: " + Me.CurrentTarget.Name);
                return;
            }

            //Debug("Combat: targetting " + Me.CurrentTarget.Name);

            // Don't do anything while casting!!
            if (Me.IsCasting || Spells.Locked)
            {
                //Debug("Combat: do nothing, casting...");
                if (!Me.IsFacing(Me.CurrentTarget))
                {
                    FaceTarget();
                }
                return;
            }

            if (CheckPet(false))
            {
                Debug("Stopping Combat - CheckPet=true");
                return;
            }

            //demon bad guys: cast immolate aura under metamorphosis
            if (MetamorphosisActive
                //&& !UnitHelper.HasBuff(Me, Spells.ImmolationAura)
                && Spells.CanCast(Spells.ImmolationAura))
            {
                Debug("I'm a very bad demon, but I doesn't look like one without fire here and there... Casting Immolation aura.");
                Spells.Cast(Spells.ImmolationAura);
                return;
            }

            //if low life, try casting voidie's bubble
            if (Me.HealthPercent < 50 && FelPuppy.HasSpell(Spells.VoidWalker_Sacrifice))
            {
                FelPuppy.Cast(Spells.VoidWalker_Sacrifice);
            }

            // Aggro management with more than one add
            var adds = ObjectManager.GetObjectsOfType<WoWUnit>(true, false).
                Where(u => u.CurrentTargetGuid == Me.Guid && u.Distance < 50 && u.IsHostile).OrderBy(u => u.Guid).ToList();
            if (adds.Count > 0)
            {
                Debug("I have {0} bad guys on me", adds.Count);

                //help me, stupid pet
                if (FelPuppy.IsValid
                    && Me.Pet.CurrentTarget != null 
                    && Me.Pet.CurrentTarget.CurrentTarget == Me.Pet)
                {
                    Debug("Sending pet on add");
                    var oldTarget = Me.CurrentTarget;
                    TargetUnit(adds[0]);

                    //death coil add
                    if (Spells.CanCast(Spells.DeathCoil))
                    {
                        Debug("Death coil on add");
                        Spells.Cast(Spells.DeathCoil, adds[0]);
                    }

                    FelPuppy.Attack();
                    TargetUnit(oldTarget);
                    return;
                }

                //too many bad guys?
                if (adds.Count > maxAdds)
                {
                    Debug("WARN: Too many adds on me!");

                    //compute average adds pos. etc.
                    WoWPoint middle = WoWPoint.Zero;
                    int nearFoes = 0;
                    foreach (var add in adds)
                    {
                        middle.Add(add.Location.X / adds.Count,
                                    add.Location.Y / adds.Count,
                                    add.Location.Z / adds.Count);
                        if (add.DistanceSqr < 100)
                            nearFoes++;
                    }

                    //demonology locks can pop metamorph. spell to aoe them. and prevent dying
                    if (IsDemonology && Spells.CanCast(Spells.Metamorphosis))
                    {
                        Debug("Demonology adds handler is ready. Adds must be closer.");
                        if (nearFoes > 1)
                        {
                            Debug("Metamorphosis! Gotta kill them all.");
                            Spells.Cast(Spells.Metamorphosis);

                            //bring pet and its potential target to me, so its target will get aoe dmg too
                            var focus = adds.FirstOrDefault(u => u.CurrentTarget.Guid == Me.Guid
                                && u.DistanceSqr < 100);
                            if (focus != null)
                            {
                                Debug("Bringing pet near me. Nearest target around is " + focus.Name);
                                FelPuppy.Attack(focus);
                            }
                            return;
                        }
                    }

                    //try soulshatter if we got a pet
                    if (!PVPLogicEnabled
                        && Me.HealthPercent < 65 
                        && FelPuppy.IsValid
                        && Spells.CanCast(Spells.Soulshatter))
                    {
                        Spells.Cast(Spells.Soulshatter);
                        return;
                    }

                    //pvp/im-gonna-die aoe fear!
                    if ((PVPLogicEnabled || Me.CurrentHealth < 30) 
                        && nearFoes > 1
                        && Spells.CanCast(Spells.HowlOfTerror))
                    {
                        Debug("Popping howl of terror");
                        Spells.Cast(Spells.HowlOfTerror);
                        return;
                    }

                    /*
                    //or pop an infernal. yay.
                    if (Spells.SummonInfernal.CanCast
                        && (!PVPLogicEnabled || (PVPLogicEnabled && Me.CurrentHealth > 70))
                        && middle.Distance(Me.Location) < Spells.SummonInfernal.MaxRange-8)
                    {
                        Spells.Cast(Spells.SummonInfernal, middle); //fix this
                        return;
                    }*/
                }
            }

            if (Me.HealthPercent >= 50 && Me.ManaPercent <= 70 && Spells.CanCast(Spells.LifeTap))
            {
                Spells.Cast(Spells.LifeTap);
                return;
            }

            if (Me.HealthPercent <= 40 && PlayerHasItem(HEALTHSTONE) && Spells.CanCast(Spells.UseHealthStone))
            {
                Debug("Use healthstone");
                Spells.Cast(Spells.UseHealthStone);
                return;
            }

            if (Me.HealthPercent >= 40 && Me.Pet != null && Me.Pet.HealthPercent < 60 && Spells.CanCast(Spells.HealthFunnel))
            {
                Spells.Cast(Spells.HealthFunnel);
                return;
            }

            /* affliction combat rotation
             * ***************************
             * that's why the bot should be doing:
             *  - should have pulled with best useful affliction spell, like haunt.
             *  - check that pet actually attacks target. else send pet to attack
             *  - Nuke loop:
             *          - if target hp < 25%, drain soul only!
             *          - else if soulburn ready, cast it with SF or drain life
             *          - refresh debuffs if necessary (corr, immolate/UA, bane of agony if no haunt)
             *          - haunt / drain life / shadowbolt
             **************************************************************
             * demonololgy rotation
             * *********************
             *  - pull with corruption
             *  - check pet's target
             *  - Nuke loop:
             *          - if target hp < 25%, cast drain soul/SF depending on "Decimation" buff on player
             *          - else if soulburn ready, cast it with SF. !!! no super fast drain life
             *          - refresh debuffs if necessary (corr, immolate, bane of agony)
             *          - HG / shadowbolt / drain life
             *  !!: We mostly keep soulburn to rez pet if necessary, else instant SF
             *      if pet dies, fast summon a new one if "Demonic rebirth" buff is up
             */

            //pet attack
            if (FelPuppy.IsValid && Me.Pet.CurrentTarget == null)
            {
                Debug("Send pet on target.");
                FelPuppy.Attack();
            }

            //finish spell
            if (Me.CurrentTarget.HealthPercent < 25)
            {
                //demonology
                if (Decimation && Spells.CanCast(Spells.SoulFire))
                {
                    Debug("Low HP target => soulfire under decimation!");
                    Spells.Cast(Spells.SoulFire, Me.CurrentTarget);
                    return;
                }
                //any spec, but mostly fits affliction and lowlvl demonology
                else if (Spells.CanCast(Spells.DrainSoul))
                {
                    Debug("Low HP target => drain soul!");
                    Spells.Cast(Spells.DrainSoul, Me.CurrentTarget);
                    return;
                }
            }

            //instant SF
            if (Me.CurrentTarget.HealthPercent > 60 && SoulburnReady && Spells.CanCast(Spells.SoulFire))
            {
                Debug("Instant SF!§1");
                Spells.Cast(Spells.SoulFire, Me.CurrentTarget, true);
                return;
            }

            //corr
            if (Spells.CanCast(Spells.Corruption) 
                && !UnitHelper.HasBuff(Me.CurrentTarget, Spells.Corruption))
            {
                Debug("Target needs corruption");
                Spells.Cast(Spells.Corruption, Me.CurrentTarget);
                return;
            }

            //immolate / UA
            var castDot = Immolate_OR_UnstableAffliction;
            if (castDot != null 
                && Spells.CanCast(castDot)
                && UnitHelper.MustRefreshBuff(Me.CurrentTarget, castDot))
            {
                Debug("Target needs " + castDot.Name);
                Spells.Cast(castDot, Me.CurrentTarget);
                return;
            }

            //bane of agony
            castDot = null;
            //Debug("HasHaunt={0} HasBOA={1}", SpellManager.HasSpell(Spells.Haunt), SpellManager.HasSpell(Spells.BaneOfAgony));
            if (!SpellManager.HasSpell(Spells.Haunt)
                && SpellManager.HasSpell(Spells.BaneOfAgony))
                castDot = Spells.BaneOfAgony;
            if (castDot != null
                && Spells.CanCast(castDot)
                && !UnitHelper.HasBuff(Me.CurrentTarget, castDot))
            {
                Debug("Target needs " + castDot.Name);
                Spells.Cast(castDot, Me.CurrentTarget);
                return;
            }

            //super drain life - affliction only!
            if (IsAffliction 
                && SoulburnReady 
                && Spells.CanCast(Spells.DrainLife))
            {
                Debug("Empowered drain life!");
                Spells.Cast(Spells.DrainLife, Me.CurrentTarget, true);
                return;
            }

            //pvp fear
            if (PVPLogicEnabled && Spells.CanCast(Spells.Fear))
            {
                List<WoWPlayer> players = ObjectManager.GetObjectsOfType<WoWPlayer>(false, false).
                    Where(p =>
                                p.DistanceSqr < Math.Pow(Spells.Fear.MaxRange, 2) - 10
                                && !p.Mounted
                                && !UnitHelper.HasBuff(p, Spells.Fear)).
                    OrderBy(p => p.DistanceSqr).
                    ToList();
                WoWPlayer fearme = null;
                if (players.Count > 0)
                    fearme = players.Count > 1 ? players[1] : players[0]; //dont fear wars/rogues/...

                if (fearme != null)
                {
                    Debug("PVP Logic: fearing player {0}. Distance={1}, FearRange={2}",
                        fearme.Name, fearme.Distance, Spells.Fear.MaxRange);
                    Spells.Cast(Spells.Fear, fearme);
                    return;
                }
            }

            //can I pulls moar mobz? ********************************************
            //disabled for demon. specced warlocks unless you have metamorphosis ready

            if ((IsAffliction || Spells.CanCast(Spells.Metamorphosis))
                && Spells.CanCast(Spells.Corruption)
                && Me.HealthPercent > 85 //this can be achieved by casting drain life on main target
                && (Me.Pet != null && Me.Pet.HealthPercent > 60)
                && Me.ManaPercent > 70 
                && TotalAdds < maxAdds)
            {
                //anything around?
                var nearestTarget = ObjectManager.GetObjectsOfType<WoWUnit>(true, false).
                    FirstOrDefault(u =>
                                   u.FactionId == Me.CurrentTarget.FactionId //this also fixes adds pulling in pvp
                                && u.CurrentTarget == null 
                                && !u.TaggedByOther
                                && u.DistanceSqr < Math.Pow(Targeting.PullDistance, 2));

                if (nearestTarget != null)
                {
                    Debug("Found a near target to pull!");
                    TargetUnit(nearestTarget);
                    Spells.Cast(Spells.Corruption); //instant pull is better!
                    return;
                }
            }

            //stupid nuke spells here. ******************************************

            switch (Specialization) {
                case Specializations.Demonology:
                    if (Spells.CanCast(Spells.HandOfGuldan))
                    {
                        Debug("Hand of Guldan + pet attack");
                        FelPuppy.Attack();
                        Spells.Cast(Spells.HandOfGuldan, Me.CurrentTarget);
                        return;
                    }

                    if (Me.HealthPercent < 85
                        && Spells.CanCast(Spells.DrainLife))
                    {
                        Debug("drain life");
                        Spells.Cast(Spells.DrainLife, Me.CurrentTarget);
                        return;
                    }

                    //if no soul harvest yet and needs to farm soul shards
                    if (!SpellManager.HasSpell(Spells.SoulHarvest)
                        && Spells.CanCast(Spells.DrainSoul)
                        && Me.CurrentSoulShards < 1
                        && Me.CurrentTarget.HealthPercent < 60)
                    {
                        Debug("Need soulshards. draining soul");
                        Spells.Cast(Spells.DrainSoul);
                        return;
                    }

                    if (Spells.CanCast(Spells.ShadowBolt))
                    {
                        Debug("shadow bolt");
                        Spells.Cast(Spells.ShadowBolt, Me.CurrentTarget);
                        return;
                    }
                    break;

                case Specializations.Affliction:
                case Specializations.None:
                    if (Spells.CanCast(Spells.Haunt))
                    {
                        Debug("Haunt");
                        Spells.Cast(Spells.Haunt, Me.CurrentTarget);
                        return;
                    }

                    if (Me.HealthPercent < 85
                        && Spells.CanCast(Spells.DrainLife))
                    {
                        Debug("drain life");
                        Spells.Cast(Spells.DrainLife, Me.CurrentTarget);
                        return;
                    }

                    //if no soul harvest yet and needs to farm soul shards
                    if (!SpellManager.HasSpell(Spells.SoulHarvest)
                        && Spells.CanCast(Spells.DrainSoul)
                        && Me.CurrentSoulShards < 1
                        && Me.CurrentTarget.HealthPercent < 60)
                    {
                        Debug("Need soulshards. draining soul");
                        Spells.Cast(Spells.DrainSoul);
                        return;
                    }

                    if (Spells.CanCast(Spells.ShadowBolt))
                    {
                        Debug("shadow bolt");
                        Spells.Cast(Spells.ShadowBolt, Me.CurrentTarget);
                        return;
                    }
                    break;
            }

            /*
            //nothing to do ?! desperate attack.
            if (Me.CurrentMana < 5 && Spells.CanCast(Spells.Shoot))
            {
                Debug("Dont know what to cast... shooting");
                Spells.Cast(Spells.Shoot, Me.CurrentTarget);
            }*/

            Debug("Did nothing in Combat() loop. Slow FPS? client lag?");
        }


        //should we use special pvp spells like fear?
        public static bool PVPLogicEnabled
        {
            get
            {
                return
                    Battlegrounds.IsInsideBattleground
                    || (Me.CurrentTarget != null 
                        && Me.CurrentTarget.IsPlayer 
                        && Me.CurrentTarget.IsTargetingMeOrPet 
                        && Me.CurrentTarget.IsHostile);
            }
        }

        /**************************************************************************/

        /* Lock helpers */

        public static uint HEALTHSTONE { get { return 5512; } }
        public static uint SOULSTONE { get { return 5232; } }

        public static WoWSpell Opener
        {
            get
            {
                var wish = new WoWSpell[] {
                    //affliction
                    Spells.Haunt,
                    Spells.UnstableAffliction,

                    //demonology
                    Spells.HandOfGuldan,

                    //generic
                    Spells.Corruption,
                    Spells.BaneOfAgony,
                    Spells.Immolate,
                    Spells.ShadowBolt
                };

                WoWSpell spell = wish.First(s => s.CanCast);
                Debug("Opener: " + spell.Name);
                return spell;
            }
        }

        //fast spec respawn - demonology
        public static bool DemonicRebirth { get { return Me.HasAura("Demonic Rebirth"); } }


        //fast SF cast on low life targets - demonology
        public static bool Decimation { get { return Me.HasAura("Decimation"); } }

        public static WoWSpell Immolate_OR_UnstableAffliction
        {
            get
            {
                if (SpellManager.HasSpell(Spells.UnstableAffliction))
                    return Spells.UnstableAffliction;
                else if (SpellManager.HasSpell(Spells.Immolate))
                    return Spells.Immolate;
                else
                    return null;
            }
        }

        public static int TotalAdds
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>(true, false).
                    Where(u => u.Attackable && (u.CurrentTargetGuid == Me.Guid || (Me.Pet != null && u.CurrentTargetGuid == Me.Pet.Guid))).
                    Count();
            }
        }

        public static bool NeedsHealthstone
        {
            get
            {
                return
                    Me.BagItems != null && Me.BagItems.Count > 0 && //bug
                    !PlayerHasItem(HEALTHSTONE)
                    && SpellManager.HasSpell(Spells.CreateHealthstone);
            }
        }

        public static bool NeedsSoulstone
        {
            get
            {
                return 
                    Me.BagItems != null && Me.BagItems.Count > 0 && //bug
                    !PlayerHasItem(SOULSTONE) 
                    && SpellManager.HasSpell(Spells.CreateSoulStone);
            }
        }

        public static bool SoulburnReady
        {
            get
            {
                return
                    Me.CurrentSoulShards > 0 &&
                    Spells.Soulburn.CanCast
                    && !Spells.Soulburn.Cooldown;
            }
        }

        public static bool MetamorphosisActive
        {
            get
            {
                return IsDemonology && Me.HasAura("Metamorphosis");
            }
        }

        public static bool PlayerHasItem(WoWItem item)
        {
            return PlayerHasItem(item.Entry);
        }
        public static bool PlayerHasItem(uint entry)
        {
            /*return
                ObjectManager.GetObjectsOfType<WoWItem>(false, false).
                    Exists(i => i.Entry == entry);*/
            return Me.BagItems.Exists(i => i.Entry == entry);
        }

        public static WoWItem FindItem(uint entry)
        {
            /*return
                ObjectManager.GetObjectsOfType<WoWItem>(false, false).
                    FirstOrDefault(i => i.Entry == entry);*/
            return Me.BagItems.FirstOrDefault(i => i.Entry == entry);
        }

        public static void TargetUnit(WoWUnit unit)
        {
            if (unit == null || unit.Guid == Me.CurrentTargetGuid)
                return;
            Debug("Target unit " + unit.Name);
            unit.Target();
            LuaEventWait evt = new LuaEventWait("PLAYER_TARGET_CHANGED");
            evt.Wait(500);
            Debug("Successfuly changed player's target");
        }

        public enum Specializations
        {
            Affliction,
            Demonology,
            None
        }

        public static Specializations Specialization
        {
            get
            {
                if (SpellManager.HasSpell(Spells.SPEC_Affliction))
                    return Specializations.Affliction;
                else if (SpellManager.HasSpell(Spells.SPEC_Demonology))
                    return Specializations.Demonology;
                else
                    return Specializations.None;
            }
        }

        public static bool IsAffliction { get { return Specialization == Specializations.Affliction; } }
        public static bool IsDemonology { get { return Specialization == Specializations.Demonology; } }

        private static bool noPet = false; //skip pet actions or check?
        public static bool CheckPet(bool forceSummon)
        {
            //returns true if summoning a new pet

            //we're using a timer to prevent checking pet if we just dismounted
            //as it takes a few seconds to see the pet spawning...
            if (DateTime.Now.CompareTo(PetCheckTimer) >= 0)
            {
                Debug("CheckPet timer has ended. Allow calling CheckPet now");
                //reset checkpet timer
                Debug("Resetting checkpet timer - allow calls in at least 10 seconds.");
                PetCheckTimer = DateTime.Now.AddSeconds(10);
            }
            else
            {
                //Debug("Preventing checking pet for a few seconds...");
                return false;
            }

            if (!noPet && !FelPuppy.IsValid)
            {
                Debug("I need a new puppy.");

                if (Me.Combat)
                {
                    if (IsDemonology && DemonicRebirth)
                    {
                        Debug("Summoning new pet on combat. Thanks Demonic rebirth!");
                    }
                    else if (!forceSummon && !SoulburnReady)
                    {
                        Debug("Cannot summon new pet in combat without soulburn/demonic rebirth... wait a bit.");
                        return false;
                    }
                    else
                    {
                        Debug("Summoning new pet in combat using a soul shard.");
                    }
                }
                
                noPet = false;

                //instant summon if needed
                bool instant = DemonicRebirth || (SoulburnReady && (Me.Combat || PVPLogicEnabled));

                if (SpellManager.HasSpell(Spells.SummonFelguard))
                    Spells.Cast(Spells.SummonFelguard, instant);
                if (SpellManager.HasSpell(Spells.SummonVoidwalker))
                    Spells.Cast(Spells.SummonVoidwalker, instant);
                else if (SpellManager.HasSpell(Spells.SummonImp))
                    Spells.Cast(Spells.SummonImp, instant);
                else
                {
                    Debug("Hey... no pet found?!"); //this should never happen
                    noPet = true;
                }
                return true;
            }
            return false;
        }
    }
}
