#pragma warning disable
namespace Hunttard
{
    using Styx.Combat.CombatRoutine;
    using Styx.Helpers;
    using Styx.Logic.Combat;
    using Styx.WoWInternals;
    using Styx.WoWInternals.WoWObjects;
    using Styx;
    using System;
    using System.Diagnostics;
    using Styx.Logic;
    using System.Collections.Generic;
    using Styx.Logic.Pathing;
    using System.Threading;
    using Styx.WoWInternals.Misc;

    class Hunter : CombatRoutine
    {
        #region variables
        public override WoWClass Class { get { return WoWClass.Hunter; } }
        public override string Name { get { return "Hawker's Beast Master 2.5"; } }

        private string _logspam;
        private readonly LocalPlayer Me = ObjectManager.Me;

        private string petName = string.Empty;

        private static Stopwatch feedPetTimer = new Stopwatch();
        private static Stopwatch aspectTimer = new Stopwatch();
        private static Stopwatch petPresenceTimer = new Stopwatch();
        private static Stopwatch fightTimer = new Stopwatch();
        private static ulong lastGuid = 0;
        private bool noDrink;
        private bool noFood;
        private double restAspectOfTheFox = 90;
        private double restHealth = 55;

        private bool enemyClose = false;

        WoWPoint pointNow = new WoWPoint();

        List<WoWUnit> addsList = new List<WoWUnit>();

        private WoWUnit myTarget { get; set; }

        private double targetDistance
        {
            get
            {
                return Me.GotTarget ? Me.CurrentTarget.Distance : uint.MaxValue - 1;
            }
        }

        private static bool IsInGame
        {
            get { return ObjectManager.IsInGame; }
        }

        private static WoWUnit MyTarget
        {
            get { return ObjectManager.Me.CurrentTarget; }
        }

        private static bool GotTarget
        {
            get { return ObjectManager.Me.CurrentTarget != null; }
        }

        private bool SafeCast(string spellName)
        {
            if (Me.IsCasting || !SpellManager.HasSpell(spellName))
            {
                return false;
            }

            if (SpellManager.HasSpell(spellName) && !StyxWoW.Me.IsCasting)
            {
                if (SpellManager.Spells[spellName].CastTime > 1)
                {
                    WoWMovement.MoveStop();
                    WoWMovement.Face();
                }

                try
                {
                    if (StyxWoW.Me.GotTarget && MyTarget.Attackable)
                    {
                        WoWMovement.Face();
                    }
                }
                catch
                {
                    Logging.WriteDebug("No need to face target.");
                }

                while (StyxWoW.GlobalCooldown)
                {
                    Thread.Sleep(100);
                }

                SpellManager.Cast(spellName);

                return true;
            }

            Logging.WriteDebug("{0} can't cast {1}.", Name, spellName);

            return false;
        }

        private static Stopwatch pullTimer = new Stopwatch();
        private static Stopwatch moveTimer = new Stopwatch();

        /// <summary>
        /// Item ID's of all ammos taken on 9/23/2009 from http://www.wowhead.com/?items=6 
        /// Total of 50 ID's were found.
        /// THANKS IGGY
        /// </summary>
        bool outOfAmmo
        {
            get
            {

                List<uint> _ammoEntryId = new List<uint>()
                {
                    30319,34581,34582,32760,32761,31737,31735,23773,33803,12654,10579,32883,30612,30611,32882,41164,
                    13377,11630,41165,31949,3465,3464,23772,10512,19316,19317,10513,9399,24417,18042,15997,11284,
                    28056,8068,8067,8069,4960,41584,2519,28060,28061,11285,2516,3030,2512,2515,5568,3033,41586,28053,52020,52021
                };

                List<WoWItem> itemList = ObjectManager.GetObjectsOfType<WoWItem>(false);

                foreach (WoWItem item in itemList)
                {
                    if (_ammoEntryId.Contains(item.Entry))
                        return false;
                }
                return true;
            }
        }

        #endregion

        #region startup

        public override void Initialize()
        {
            Lua.Events.AttachEvent("COMBAT_LOG_EVENT", CombatLogEventHander);

            if (Me.GotAlivePet)
            {
                petName = Me.Pet.Name;
                Logging.Write("My pet name is {0}.", petName);
            }
        }

        public override void ShutDown()
        {
            Lua.Events.DetachEvent("COMBAT_LOG_EVENT", CombatLogEventHander);
        }

        #endregion

        #region helper functions
        private static readonly Stopwatch PotionTimer = new Stopwatch();

        private bool UseHealthPotion()
        {
            if (!PotionTimer.IsRunning || PotionTimer.ElapsedMilliseconds > (100 + (1000 * 60)))
            {
                Healing.UseHealthPotion();
                PotionTimer.Reset();
                PotionTimer.Start();
            }
            return false;
        }
        #endregion

        #region rest and buffs
        public override bool NeedRest
        {
            get
            {
                bool restNeeded = false;

                if (!feedPetTimer.IsRunning)
                {
                    feedPetTimer.Reset();
                    feedPetTimer.Start();
                }

                if (!aspectTimer.IsRunning)
                {
                    aspectTimer.Reset();
                    aspectTimer.Start();
                }

                if (!petPresenceTimer.IsRunning || Me.GotAlivePet || Me.Mounted)
                {
                    petPresenceTimer.Reset();
                    petPresenceTimer.Start();
                }

                if (!Me.Mounted && petPresenceTimer.ElapsedMilliseconds > 3000 && SpellManager.Spells.ContainsKey("Call Pet 1") && !Me.GotAlivePet)
                {
                    slog("No pet.");
                    restNeeded = true;
                }

                if (petName == string.Empty && Me.GotAlivePet)
                {
                    petName = Me.Pet.Name;
                }

                if ((feedPetTimer.IsRunning && feedPetTimer.ElapsedMilliseconds > 60 * 1000) && SpellManager.Spells.ContainsKey("Feed Pet") &&
                    Me.GotAlivePet && Me.Pet.CurrentHappiness > 0 && Me.Pet.CurrentHappiness < 65 && !Me.Pet.Auras.ContainsKey("Feed Pet"))
                {
                    slog("Pet happiness: " + Me.Pet.CurrentHappiness.ToString());
                    restNeeded = true;
                }

                if (SpellManager.Spells.ContainsKey("Mend Pet") &&
                    Me.GotAlivePet &&
                    Me.Pet.HealthPercent < 50 &&
                    !Me.Pet.Auras.ContainsKey("Mend Pet"))
                {
                    slog("Heal the pet.");
                    restNeeded = true;
                }

                if (Battlegrounds.IsInsideBattleground)
                {
                    return false;
                }

                if (!noFood && Me.HealthPercent < restHealth)
                {
                    slog("Low health.");
                    restNeeded = true;
                }

                return restNeeded;
            }
        }



        public override void Rest()
        {
            if (Me.FocusPercent < restAspectOfTheFox && SpellManager.Spells.ContainsKey("Aspect of the Fox") && !Me.Auras.ContainsKey("Aspect of the Fox"))
            {
                slog("Aspect of the Fox.");
                AspectOfTheFox();
            }

            if (!Me.Mounted && petPresenceTimer.ElapsedMilliseconds > 3000 && SpellManager.Spells.ContainsKey("Call Pet 1") && !Me.GotAlivePet)
            {
                CallPet();
                Thread.Sleep(500);
                if (!Me.GotAlivePet)
                {
                    slog("No pet appeared.");
                    FreezingTrap();
                    RevivePet();
                }
            }

            if (SpellManager.Spells.ContainsKey("Call Pet 1") && Me.GotAlivePet && Me.Pet.CurrentHappiness < 70)
            {
                FeedPet();
            }

            if (SpellManager.Spells.ContainsKey("Mend Pet") && Me.GotAlivePet && Me.Pet.HealthPercent < restHealth)
            {
                MendPet();
            }

            if (Me.HealthPercent < restHealth)
            {
                int foodCount = Convert.ToInt32(Lua.LuaGetReturnValue("return GetItemCount(\"" + LevelbotSettings.Instance.FoodName + "\")", "hawker.lua")[0]);

                if (foodCount < 1)
                {
                    noFood = true;
                    Logging.Write("Hunter Rest - no food.");
                }
                else
                {
                    noFood = false;
                }

                if (!noFood &&
                    Me.HealthPercent < restHealth)
                {
                    Styx.Logic.Common.Rest.Feed();
                }
            }

            if (Me.FocusPercent < 40)
            {
                int DrinkCount = Convert.ToInt32(Lua.LuaGetReturnValue("return GetItemCount(\"" + LevelbotSettings.Instance.DrinkName + "\")", "hawker.lua")[0]);

                if (DrinkCount < 1)
                {
                    noDrink = true;
                }
                else
                {
                    noDrink = false;
                }

                if (!noDrink && Me.FocusPercent < 40)
                {
                    Styx.Logic.Common.Rest.Feed();
                }
            }
        }
        public override bool NeedPreCombatBuffs { get { return false; } }
        public override void PreCombatBuff() { }
        public override bool NeedCombatBuffs { get { return false; } }
        public override void CombatBuff() { }
        public override bool NeedHeal { get { return false; } }
        public override void Heal() { }
        public override bool NeedPullBuffs { get { return false; } }
        public override void PullBuff() { }
        #endregion

        #region pull
        public override void Pull()
        {
            #region checks
            if (Battlegrounds.IsInsideBattleground)
            {
                if (Me.GotTarget &&
                    Me.CurrentTarget.IsPet)
                {
                    Blacklist.Add(Me.CurrentTarget, TimeSpan.FromDays(1));
                    Me.ClearTarget();
                }

                if (Me.GotTarget && Me.CurrentTarget.Mounted)
                {
                    Blacklist.Add(Me.CurrentTarget, TimeSpan.FromMinutes(1));
                    Me.ClearTarget();
                }
            }

            if (Me.CurrentTarget.Guid != lastGuid)
            {
                fightTimer.Reset();
                lastGuid = Me.CurrentTarget.Guid;
                slog("Killing " + Me.CurrentTarget.Name + " at distance " + System.Math.Round(targetDistance).ToString() + ".");
                pullTimer.Reset();
                pullTimer.Start();

            }
            else
            {
                if (pullTimer.ElapsedMilliseconds > 30 * 1000)
                {
                    slog("Cannot pull " + Me.CurrentTarget.Name + " now.  Blacklist for 3 minutes.");
                    Styx.Logic.Blacklist.Add(Me.CurrentTarget.Guid, System.TimeSpan.FromMinutes(3));
                }
            }

            #endregion
            double shotRange = SpellManager.Spells["Auto Shot"].MaxRange - 1;

            if (Me.CurrentTarget.Distance > shotRange)
            {
                Navigator.MoveTo(attackPoint);

                int i = 0;
                while (i < 8 && Me.Location.Distance(attackPoint) > 1)
                {
                    if (ObjectManager.Me.Combat)
                    {
                        Logging.Write("Combat has started.  Abandon pull.");
                        break;
                    }

                    Thread.Sleep(250);
                    ++i;
                }

                return;
            }
            else
            {
                slog("Starting attack");
                if (ObjectManager.Me.IsMoving)
                {
                    WoWMovement.MoveStop();
                }

                if (Me.GotTarget && Me.GotAlivePet)
                {
                    if (!Me.Pet.IsAutoAttacking)
                    {
                        Lua.DoString("PetAttack()");
                    }

                    petPresenceTimer.Reset();
                    if (Me.CurrentTarget.Level - Me.Level == 1)
                    {
                        slog("Let " + Me.Pet.Name + " gain aggro.");
                        Thread.Sleep(1000);
                    }
                    else if (Me.CurrentTarget.Level - Me.Level > 1)
                    {
                        slog("Let " + Me.Pet.Name + " face this mob alone for a bit.");
                        Thread.Sleep(2000);
                    }
                }

                WoWMovement.Face();

                SelectAspect();
                HuntersMark();

                if (SpellManager.Spells.ContainsKey("Concussive Shot") && !SpellManager.Spells["Concussive Shot"].Cooldown)
                {
                    ConcussiveShot();
                    SteadyShot();
                }
                else
                {
                    ArcaneShot();

                    if (!Me.Combat)
                    {
                        AutoShot();
                    }
                }

                if (!Me.CurrentTarget.IsTargetingMeOrPet && !SpellManager.Spells.ContainsKey("Arcane Shot"))
                {
                    ArcaneShot();
                }

                if (!Me.CurrentTarget.IsTargetingMeOrPet && SpellManager.Spells.ContainsKey("Steady Shot"))
                {
                    SteadyShot();
                }

                if (!Me.CurrentTarget.IsTargetingMeOrPet)
                {
                    AutoShot();

                    for (int a = 0; a <= 10; ++a)
                    {
                        if (Me.CurrentTarget.IsTargetingMeOrPet)
                            return;
                    }
                }


            }
        }
        #endregion

        #region combat
        #region noammo
        private void MeleeCombat()
        {
            if (Me.GotTarget && Me.CurrentTarget.Distance > 4)
            {
                Navigator.MoveTo(Me.CurrentTarget.Location);

                int a = 0;
                while (Me.GotTarget && Me.CurrentTarget.Distance > 4 && a < 20)
                {
                    Thread.Sleep(250);
                    Navigator.MoveTo(Me.CurrentTarget.Location);
                    ++a;
                }
            }
            else
            {
                WoWMovement.Face();

                if (SpellManager.Spells.ContainsKey("Immolation Trap") && !SpellManager.Spells["Immolation Trap"].Cooldown)
                {
                    ImmolationTrap();
                }
                else if (SpellManager.Spells.ContainsKey("Raptor Strike") && !SpellManager.Spells["Raptor Strike"].Cooldown)
                {
                    RaptorStrike();
                }
                else if (SpellManager.Spells.ContainsKey("Mongoose Bite") && !SpellManager.Spells["Mongoose Bite"].Cooldown)
                {
                    MongooseBite();
                }
            }
        }
        #endregion
        #region ranged/normal dps
        private void RangedCombat()
        {
            double shotRange = SpellManager.Spells["Auto Shot"].MaxRange - 1;

            if (Me.CurrentTarget.Distance > shotRange)
            {
                Navigator.MoveTo(attackPoint);
                return;
            }


            if (ObjectManager.Me.IsMoving)
            {
                WoWMovement.MoveStop();
            }

            WoWMovement.Face();

            HuntersMark();

            if (Me.GotTarget && SpellManager.Spells.ContainsKey("Kill Shot") && !SpellManager.Spells["Kill Shot"].Cooldown && Me.CurrentTarget.HealthPercent < 20)
            {
                KillShot();
            }

            ConcussiveShot();

            KillCommand();

            if (Me.GotTarget && SpellManager.Spells.ContainsKey("Steady Shot") && Me.CurrentFocus < 30)
            {
                SteadyShot();
            }
            else if (Me.GotTarget && SpellManager.Spells.ContainsKey("Arcane Shot"))
            {
                ArcaneShot();
            }
        }
        #endregion
        #region melee on me
        private void UsePet()
        {
            try
            {
                if (SpellManager.Spells.ContainsKey("Disengage") && !SpellManager.Spells["Disengage"].Cooldown)
                {
                    WingClip();
                    Disengage();
                }
                else if (SpellManager.Spells.ContainsKey("Intimidation") && !SpellManager.Spells["Intimidation"].Cooldown)
                {
                    Intimidation();
                    SafeMove(WoWMovement.MovementDirection.StrafeLeft, 1500);

                    if (Me.CurrentTarget != null)
                        WoWMovement.Face();
                }
                else if (SpellManager.Spells.ContainsKey("Frost Trap") && !SpellManager.Spells["Frost Trap"].Cooldown)
                {
                    FrostTrap();
                    SafeMove(WoWMovement.MovementDirection.StrafeRight, 1500);

                    if (Me.CurrentTarget != null)
                        WoWMovement.Face();

                }
                else if (SpellManager.Spells.ContainsKey("Wing Clip") && !SpellManager.Spells["Wing Clip"].Cooldown)
                {
                    WingClip();
                    SafeMove(WoWMovement.MovementDirection.StrafeLeft, 1500);

                    if (Me.CurrentTarget != null)
                        WoWMovement.Face();

                }
                else if (SpellManager.Spells.ContainsKey("Immolation Trap") && !SpellManager.Spells["Immolation Trap"].Cooldown)
                {
                    ImmolationTrap();
                }
                else if (SpellManager.Spells.ContainsKey("Feign Death") && !SpellManager.Spells["Feign Death"].Cooldown &&
                    Me.GotAlivePet)
                {
                    FeignDeath();
                    Thread.Sleep(5 * 1000);
                    KeyboardManager.KeyUpDown(' ');
                }
                else if (SpellManager.Spells.ContainsKey("Raptor Strike") && !SpellManager.Spells["Raptor Strike"].Cooldown)
                {
                    RaptorStrike();
                }
                else if (SpellManager.Spells.ContainsKey("Mongoose Bite") && !SpellManager.Spells["Mongoose Bite"].Cooldown)
                {
                    MongooseBite();
                }
            }
            finally
            {
                if (ObjectManager.Me.IsMoving)
                {
                    WoWMovement.MoveStop();
                }
            }
        }
        #endregion
        #region pvp
        private void KillPlayer()
        {
            if (!Me.GotTarget)
                return;

            double shotRange = SpellManager.Spells["Auto Shot"].MaxRange - 1;

            ImmolationTrap();
            Intimidation();

            if (Me.CurrentTarget.Distance > 7 && Me.CurrentTarget.Distance < shotRange)
            {
                BestialWrath();
                SerpentSting();
                ArcaneShot();
                SteadyShot();
            }
            else if (Me.CurrentTarget.Distance < 6)
            {
                if (SpellManager.Spells.ContainsKey("Disengage") && !SpellManager.Spells["Disengage"].Cooldown)
                {
                    SnakeTrap();
                    WingClip();
                    Disengage();
                    ConcussiveShot();
                    return;
                }
                else
                {
                    FrostTrap();
                    WingClip();
                    SafeMove(WoWMovement.MovementDirection.StrafeLeft, 500);
                    RaptorStrike();
                    MongooseBite();
                }
                BestialWrath();
            }
        }
        #endregion

        private static void CombatLogEventHander(object sender, LuaEventArgs args)
        {
            foreach (object arg in args.Args)
            {
                if (arg is String)
                {
                    var s = (string)arg;
                    if (s.ToUpper() == "EVADE")
                    {
                        if (StyxWoW.Me.GotTarget)
                        {
                            Logging.Write("My target is Evade bugged.");
                            Logging.Write("Blacklisting for 3 hours");
                            Lua.DoString("StopAttack() PetStopAttack() PetFollow()");
                            StyxWoW.Me.ClearTarget();
                            Blacklist.Add(StyxWoW.Me.CurrentTargetGuid, TimeSpan.FromHours(3));
                        }
                    }
                }
            }
        }

        public override void Combat()
        {
            try
            {
                if (StyxWoW.GlobalCooldown || StyxWoW.Me.IsCasting)
                    return;

                if (SpellManager.Spells.ContainsKey("Call Pet 1") &&
                    !Me.GotAlivePet)
                {
                    CallPet();
                }

                if (Battlegrounds.IsInsideBattleground)
                {
                    if (Me.GotTarget &&
                        Me.CurrentTarget.IsPet)
                    {
                        Blacklist.Add(Me.CurrentTarget, TimeSpan.FromDays(1));
                        Me.ClearTarget();
                        return;
                    }
                }

                if (Me.GotTarget)
                {
                    WoWMovement.Face();

                    if (!Me.Combat)
                    {
                        AutoShot();
                    }

                    if (Me.GotAlivePet)
                    {
                        Lua.DoString("PetAttack()");
                    }
                }

                // bugged mobs 
                if (Me.GotTarget && (!fightTimer.IsRunning || Me.CurrentTarget.Guid != lastGuid))
                {
                    fightTimer.Reset();
                    fightTimer.Start();
                    lastGuid = Me.CurrentTarget.Guid;
                }

                if (Me.IsSwimming)
                {
                    KeyboardManager.KeyUpDown(' ');
                    KeyboardManager.KeyUpDown(' ');
                }


                if (!Me.GotTarget && Me.Pet.GotTarget && Me.PetInCombat)
                {
                    Me.Pet.CurrentTarget.Target();
                    Thread.Sleep(300);
                    AutoShot();
                    slog("Take pet's target.");
                    WoWMovement.Face();
                }

                addsList = getAdds();
                if (addsList.Count > 1)
                {
                    if (Me.GotTarget && Me.CurrentTarget.HealthPercent > 50)
                    {
                        MendPet();
                        RapidFire();
                        BestialWrath();
                    }

                    if (Me.HealthPercent < restHealth)
                    {
                        FreezingTrap();
                    }
                    else
                    {
                        ImmolationTrap();
                    }

                    KillCommand();
                }

                // autoattack + pet attack = very cheap level 80
                if (Me.GotTarget && Me.GotAlivePet)
                {
                    if (!Me.Pet.IsAutoAttacking)
                    {
                        Lua.DoString("PetAttack()");
                    }

                    if (Me.CurrentTarget.HealthPercent > 60)
                    {
                        KillCommand();
                    }

                    petPresenceTimer.Reset();
                }

                if (Me.GotTarget && Me.CurrentTarget.HealthPercent > Me.HealthPercent)
                {
                    BestialWrath();
                }

                if (Me.GotAlivePet && Me.Pet.HealthPercent < 50 && !Me.Pet.Auras.ContainsKey("Mend Pet"))
                {
                    MendPet();
                }

                SelectAspect();

                if (Me.HealthPercent < 40)
                {
                    Healing.UseHealthPotion();
                }

                if (!Me.GotTarget)
                {
                    Me.ClearTarget();
                    return;
                }
                else if (!Me.CurrentTarget.InLineOfSight)
                {
                    slog("{0} is not in line of sight.  Close and use melee.", Me.CurrentTarget.Name);
                    MeleeCombat();
                }
                else if (Me.CurrentTarget != null && ObjectManager.Me.CurrentTarget.IsPlayer)
                {
                    KillPlayer();
                }
                else
                {
                    if (Me.CurrentTarget != null && Me.GotTarget && !Me.IsAutoAttacking)
                    {
                        Lua.DoString("StartAttack()");
                    }

                    #region ranged

                    if (Me.GotTarget && Me.CurrentTarget != null && Me.CurrentTarget.Distance > 7)
                    {
                        RangedCombat();
                    }

                    #endregion

                    else if (Me.GotTarget && Me.CurrentTarget.IsAlive && Me.CurrentTarget.Distance < 8)
                    {
                        if (Me.GotAlivePet && Me.CurrentTarget != null && Me.CurrentTarget.CurrentTargetGuid == Me.Guid)
                        // slow the target and get range
                        {
                            slog(Me.CurrentTarget.Name + " is attacking me.  Use pet and get range.");
                            UsePet();
                        }
                        // get range and let pet fight

                        else if (Me.CurrentTarget != null && Me.GotAlivePet)
                        {
                            slog(Me.CurrentTarget.Name + " is too close.");

                            SafeMove(WoWMovement.MovementDirection.StrafeLeft, 1500);

                            if (Me.CurrentTarget != null)
                                WoWMovement.Face();
                        }
                        else
                        {
                            MeleeCombat();
                        }
                    }
                }

                if (Me.CurrentTarget != null && !Me.CurrentTarget.IsPlayer &&
                    fightTimer.ElapsedMilliseconds > 40 * 1000 &&
                    Me.CurrentTarget.HealthPercent > 95)
                {
                    slog(" This " + Me.CurrentTarget.Name + " is a bugged mob.  Blacklisting for 1 hour.");

                    Blacklist.Add(Me.CurrentTarget.Guid, TimeSpan.FromHours(1.00));

                    SafeMove(WoWMovement.MovementDirection.Backwards, 5000);
                    Me.ClearTarget();
                    lastGuid = 0;
                }
            }
            finally
            {
                if (ObjectManager.Me.IsMoving)
                {
                    WoWMovement.MoveStop();
                }
            }

        }
        public void HandleFalling() { }
        #endregion

        #region hunter spells

        /// <summary>
        /// Automatically shoots the target until cancelled.  Ranged attacks fired by a Hunter all have 15% increased attack speed.
        /// </summary>
        private bool AutoShot()
        {
            if (Me.GotTarget && SpellManager.Cast("Auto Shot"))
            {
                slog("Auto Shot.");
                return true;
            }
            else
            {
                // slog("Can't use shoot now.");
                return false;
            }
        }

        /// <summary>
        /// A strong attack that increases melee damage
        /// </summary>        
        private bool RaptorStrike()
        {
            if (Me.GotTarget && SafeCast("Raptor Strike"))
            {
                WoWMovement.Face();
                slog("Raptor Strike.");
                return true;
            }
            else
            {
                // slog("Can't use Raptor Strike now.");
                return false;
            }
        }

        /// <summary>
        /// The hunter takes on the aspects of a monkey, increasing chance to dodge by 18%.  Only one Aspect can be active at a time.
        /// </summary>        
        private bool AspectofTheMonkey()
        {
            if (SafeCast("Aspect of the Monkey"))
            {
                slog("Aspect of the Monkey.");
                aspectTimer.Reset();
                aspectTimer.Start();
                return true;
            }
            else
            {
                // slog("Can't use Aspect of the Monkey now.");
                return false;
            }
        }

        /// <summary>
        /// Stings the target, causing Nature damage over 15 sec.  Only one Sting per Hunter can be active on any one target.
        /// </summary>        
        private bool SerpentSting()
        {
            if (Me.GotTarget && SafeCast("Serpent Sting"))
            {
                slog("Serpent Sting.");
                return true;
            }
            else
            {
                // slog("Can't use Serpent Sting now.");
                return false;
            }
        }

        /// <summary>
        /// An instant shot that causes Arcane damage.
        /// </summary>        
        private bool ArcaneShot()
        {
            if (Me.GotTarget && SafeCast("Arcane Shot"))
            {
                slog("Arcane Shot.");
                return true;
            }
            else
            {
                // slog("Can't use Arcane Shot now.");
                return false;
            }
        }

        /// <summary>
        /// Places the Hunter's Mark on the target, increasing the ranged attack power of all attackers against that target by 20.  In addition, the target of this ability can always be seen by the hunter whether it stealths or turns invisible.  The target also appears on the mini-map.  Lasts for 5 min.
        /// </summary>        
        private bool HuntersMark()
        {
            if (Me.GotTarget &&
                Me.CurrentTarget.Auras.ContainsKey("Hunter's Mark"))
                return false;

            if (Me.GotTarget && SafeCast("Hunter's Mark"))
            {
                slog("Hunter's Mark.");
                return true;
            }
            else
            {
                // slog("Can't use Hunter's Mark now.");
                return false;
            }
        }

        /// <summary>
        /// Dazes the target, slowing movement speed by 50% for 4 sec.
        /// </summary>        
        private bool ConcussiveShot()
        {
            if (Me.GotTarget && Me.CurrentTarget.Auras.ContainsKey("Concussive Shot"))
            {
                return true;
            }

            if (Me.GotTarget && SafeCast("Concussive Shot"))
            {
                WoWMovement.Face();
                slog("Concussive Shot.");
                return true;
            }
            else
            {
                // slog("Can't use Concussive Shot now.");
                return false;
            }
        }

        /// <summary>
        /// The hunter takes on the aspects of a hawk, increasing ranged attack power by 20.  Only one Aspect can be active at a time.
        /// </summary>
        private bool AspectOfTheHawk()
        {
            if (SafeCast("Aspect of the Hawk"))
            {
                slog("Aspect of the Hawk.");
                aspectTimer.Reset();
                aspectTimer.Start();
                return true;
            }
            else
            {
                // slog("Can't use Aspect of the Hawk now.");
                return false;
            }
        }

        /// <summary>
        /// Summons your pet to you.
        /// </summary>        
        private bool CallPet()
        {
            if (petName == string.Empty)
            {
                if (SafeCast("Call Pet 1"))
                {
                    slog("Call Pet.");
                    return true;
                }
            }
            else
            {
                var CarriedPets = ObjectManager.Me.Stable.GetCarriedPets();

                foreach (StabledPet one in CarriedPets)
                {
                    if (one.Name == petName)
                    {
                        slog("Summoning {0}.", one.Name);
                        one.Summon();
                        return true;
                    }
                }
            }

            return false;

        }

        /// <summary>
        /// Dismiss your pet.  Dismissing your pet will reduce its happiness by 50.
        /// </summary>        
        private bool DismissPet()
        {
            if (SafeCast("Dismiss Pet"))
            {
                slog("Dismiss Pet.");
                return true;
            }
            else
            {
                // slog("Can't use Dismiss Pet now.");
                return false;
            }
        }

        /// <summary>
        /// Feed your pet the selected item.  Feeding your pet increases happiness.  Using food close to the pet's level will have a better result.
        /// </summary>        
        private bool FeedPet()
        {

            if (!Me.GotAlivePet)
                return false;

            feedPetTimer.Reset();
            feedPetTimer.Start();

            int foodCount = Convert.ToInt32(Lua.LuaGetReturnValue("return GetItemCount(\"" + LevelbotSettings.Instance.FoodName + "\")", "hawker.lua")[0]);

            if (foodCount < 1)
            {
                slog("No food!!");
                return false;
            }

            if (SpellManager.Cast("Feed Pet"))
            {
                WoWMovement.MoveStop();
                Thread.Sleep(500);

                Lua.DoString("UseItemByName(\"" + LevelbotSettings.Instance.FoodName + "\")");
                slog("Feed Pet...");

                int a = 0;

                while (a < 15 && !Me.Combat && Me.Pet.Auras.ContainsKey("Feed Pet"))
                {
                    Thread.Sleep(1000);
                    ++a;
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Revive your pet, returning it to life with 15% of its base health.
        /// </summary>        
        private bool RevivePet()
        {
            if (SafeCast("Revive Pet"))
            {
                slog("Revive Pet.");
                return true;
            }
            else
            {
                slog("Can't use Revive Pet now.");
                return false;
            }
        }

        /// <summary>
        /// Heals your pet for 125 health over 15 sec.
        /// </summary>        
        private bool MendPet()
        {
            if (!SpellManager.Spells.ContainsKey("Mend Pet"))
                return false;

            if (!Me.GotAlivePet || Me.Pet.Auras.ContainsKey("Mend Pet"))
            {
                slog("Not Mend Pet time.");
                return true;
            }

            if (!Me.Pet.Auras.ContainsKey("Mend Pet") &&
                SafeCast("Mend Pet"))
            {
                slog("Mend Pet.");
                return true;
            }
            else
            {
                // slog("Can't use Mend Pet now.");
                return false;
            }
        }

        /// <summary>
        /// Maims the enemy, reducing the target's movement speed by 50% for 10 sec.
        /// </summary>        
        private bool WingClip()
        {
            if (Me.GotTarget && Me.CurrentTarget.Distance < 6)
            {
                WoWMovement.Face();

                Thread.Sleep(250);

                if (SafeCast("Wing Clip"))
                {
                    slog("Wing Clip.");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Scares a beast, causing it to run in fear for up to 10 sec.  Damage caused may interrupt the effect.  Only one beast can be feared at a time.
        /// </summary>        
        private bool ScareBeast()
        {
            if (Me.GotTarget && SafeCast("Scare Beast"))
            {
                slog("Scare Beast.");
                return true;
            }
            else
            {
                // slog("Can't use Scare Beast now.");
                return false;
            }
        }

        /// <summary>
        /// Place a fire trap that will burn the first enemy to approach for Fire damage over 15 sec.  Trap will exist for 30 sec.  Only one trap can be active at a time.
        /// </summary>        
        private bool ImmolationTrap()
        {
            if (SafeCast("Immolation Trap"))
            {
                slog("Immolation Trap.");
                return true;
            }
            else
            {
                // slog("Can't use Immolation Trap now.");
                return false;
            }
        }

        /// <summary>
        /// Attack the enemy for damage.
        /// </summary>        
        private bool MongooseBite()
        {
            if (Me.GotTarget && SafeCast("Mongoose Bite"))
            {
                WoWMovement.Face();
                slog("Mongoose Bite.");
                return true;
            }
            else
            {
                // slog("Can't use Mongoose Bite now.");
                return false;
            }
        }

        /// <summary>
        /// The hunter takes on the aspect of the Fox, causing ranged and melee attacks to regenerate mana but reducing your total damage done by 50%.  In addition, you gain 4% of maximum mana every 3 sec.  Mana gained is based on the speed of your ranged weapon. Requires a ranged weapon. Only one Aspect can be active at a time.
        /// </summary>        
        private bool AspectOfTheFox()
        {
            if (SafeCast("Aspect of the Fox"))
            {
                if (Me.CurrentTarget == Me)
                {
                    Me.ClearTarget();
                }

                aspectTimer.Reset();
                aspectTimer.Start();
                return true;
            }
            else
            {
                // slog("Can't use Aspect of the Fox now.");
                return false;
            }
        }

        /// <summary>
        /// You attempt to disengage from combat, leaping backwards. Can only be used while in combat.
        /// </summary>        
        private bool Disengage()
        {
            if (SafeCast("Disengage"))
            {
                slog("Disengage.");
                return true;
            }
            else
            {
                // slog("Can't use Disengage now.");
                return false;
            }
        }

        /// <summary>
        /// Place a frost trap that freezes the first enemy that approaches, preventing all action for up to 10 sec.  Any damage caused will break the ice.  Trap will exist for 30 sec.  Only one trap can be active at a time.
        /// </summary>        
        private bool FreezingTrap()
        {
            if (SafeCast("Freezing Trap"))
            {
                slog("Freezing Trap.");
                return true;
            }
            else
            {
                // slog("Can't use Freezing Trap now.");
                return false;
            }
        }

        /// <summary>
        /// Stings the target, reducing chance to hit with melee and ranged attacks by 3% for 20 sec.  Only one Sting per Hunter can be active on any one target.
        /// </summary>        
        private bool ScorpidSting()
        {
            if (Me.GotTarget && SafeCast("Scorpid Sting"))
            {
                slog("Scorpid Sting.");
                return true;
            }
            else
            {
                // slog("Can't use Scorpid Sting now.");
                return false;
            }
        }

        /// <summary>
        /// Increases ranged attack speed by 40% for 15 sec.
        /// </summary>        
        private bool RapidFire()
        {
            if (Me.GotTarget && SafeCast("Rapid Fire"))
            {
                slog("Rapid Fire.");
                return true;
            }
            else
            {
                // slog("Can't use Rapid Fire now.");
                return false;
            }
        }

        private bool AimedShot()
        {
            if (Me.GotTarget &&
                !Me.CurrentTarget.Auras.ContainsKey("Aimed Shot") &&
                SafeCast("Aimed Shot"))
            {
                slog("Aimed Shot.");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Place a frost trap that creates an ice slick around itself for 30 sec when the first enemy approaches it.  All enemies within 10 yards will be slowed by 50% while in the area of effect.  Trap will exist for 30 sec.  Only one trap can be active at a time.
        /// </summary>
        private bool FrostTrap()
        {
            if (SafeCast("Frost Trap"))
            {
                slog("Frost Trap.");
                return true;
            }
            else
            {
                // slog("Can't use use Frost Trap now.");
                return false;
            }
        }

        /// <summary>
        /// Command your pet to intimidate the target on the next successful melee attack, causing a high amount of threat and stunning the target for 3 sec. Lasts 15 sec.
        /// </summary>        
        private bool Intimidation()
        {
            if (SafeCast("Intimidation"))
            {
                slog("Intimidation.");
                return true;
            }
            else
            {
                // slog("Can't use Intimidation now.");
                return false;
            }
        }

        /// <summary>
        /// The hunter takes on the aspects of a beast, becoming untrackable and increasing melee attack power of the hunter and the hunter's pet by 10%.  Only one Aspect can be active at a time.
        /// </summary>        
        private bool AspectOfTheBeast()
        {
            if (SafeCast("Aspect of the Beast"))
            {
                slog("Aspect of the Beast.");
                aspectTimer.Reset();
                aspectTimer.Start();
                return true;
            }
            else
            {
                // slog("Can't use Aspect of the Beast now.");
                return false;
            }
        }

        /// <summary>
        /// Feign death which may trick enemies into ignoring you.  Lasts up to 6 min.
        /// </summary>
        private bool FeignDeath()
        {
            if (SafeCast("Feign Death"))
            {
                slog("Feign Death.");
                return true;
            }
            else
            {
                // slog("Can't use Feign Death now.");
                return false;
            }
        }

        /// <summary>
        /// Place a fire trap that explodes when an enemy approaches, causing [RAP * 0.1 + 100] to [RAP * 0.1 + 130] Fire damage and burning all enemies for 150 additional Fire damage over 20 sec to all within 10 yards.  Trap will exist for 30 sec.  Only one trap can be active at a time.
        /// </summary>        
        private bool ExplosiveTrap()
        {
            if (SafeCast("Explosive Trap"))
            {
                slog("Explosive Trap.");
                return true;
            }
            else
            {
                // slog("Can't use Explosive Trap now.");
                return false;
            }
        }

        /// <summary>
        /// A steady shot that causes unmodified weapon damage, plus ammo, plus [RAP * 0.1 + 45].  Causes an additional 175 against Dazed targets.
        /// </summary>        
        private bool SteadyShot()
        {
            if (Me.GotTarget && SafeCast("Steady Shot"))
            {
                slog("Steady Shot.");
                return true;
            }
            else
            {
                // slog("Can't use Steady Shot now.");
                return false;
            }
        }

        /// <summary>
        /// Send your pet into a rage causing 50% additional damage for 10 sec.  While enraged, the beast does not feel pity or remorse or fear and it cannot be stopped unless killed.
        /// </summary>        
        private bool BestialWrath()
        {
            if (SafeCast("Bestial Wrath"))
            {
                slog("Bestial Wrath.");
                return true;
            }
            else
            {
                // slog("Can't use Bestial Wrath now.");
                return false;
            }
        }

        /// <summary>
        /// When activated, increases parry chance by 100% and grants a 100% chance to deflect spells.  While Deterrence is active, you cannot attack.  Lasts 5 sec.
        /// </summary>        
        private bool Deterrence()
        {
            if (SafeCast("Deterrence"))
            {
                slog("Deterrence.");
                return true;
            }
            else
            {
                // slog("Can't use Deterrence now.");
                return false;
            }
        }

        /// <summary>
        /// Give the command to kill, increasing your pet's damage done from special attacks by 60% for 30 sec.  Each special attack done by the pet reduces the damage bonus by 20%.
        /// </summary>
        private bool KillCommand()
        {
            if (SafeCast("Kill Command"))
            {
                slog("Kill Command.");
                return true;
            }
            else
            {
                // slog("Can't use Kill Command now.");
                return false;
            }
        }

        /// <summary>
        /// Place a trap that will release several venomous snakes to attack the first enemy to approach.  The snakes will die after 15 sec.  Trap will exist for 30 sec.  Only one trap can be active at a time.
        /// </summary>
        private bool SnakeTrap()
        {
            if (SafeCast("Snake Trap"))
            {
                slog("Snake Trap.");
                return true;
            }
            else
            {
                // slog("Can't use Snake Trap now.");
                return false;
            }
        }

        /// <summary>
        /// You attempt to finish the wounded target off, firing a long range attack dealing 200% weapon damage plus [RAP * 0.40 + 410]. Kill Shot can only be used on enemies that have 20% or less health.
        /// </summary>
        private bool KillShot()
        {
            if (Me.GotTarget && SafeCast("Kill Shot"))
            {
                slog("Kill Shot.");
                return true;
            }
            else
            {
                // slog("Can't use Kill Shot now.");
                return false;
            }
        }

        /// <summary>
        /// The hunter takes on the aspects of a dragonhawk, increasing ranged attack power by 230 and chance to dodge by 18%.  Only one Aspect can be active at a time.
        /// </summary>        
        private bool AspectOfTheDragonhawk()
        {
            if (SafeCast("Aspect of the Dragonhawk"))
            {
                slog("Aspect of the Dragonhawk.");
                aspectTimer.Reset();
                aspectTimer.Start();
                return true;
            }
            else
            {
                // slog("Can't use Aspect of the Dragonhawk now.");
                return false;
            }
        }

        /// <summary>
        /// Fire a freezing arrow that places a Freezing Trap at the target location, freezing the first enemy that approaches, preventing all action for up to 20 sec.  Any damage caused will break the ice.  Trap will exist for 30 sec.  Only one trap can be active at a time.
        /// </summary>
        /// <param name="targetSpot"></param>        
        private bool FreezingArrow(WoWPoint targetSpot)
        {
            if (SafeCast("Freezing Arrow"))
            {
                slog("Freezing Arrow.");
                return true;
            }
            else
            {
                // slog("Can't use Freezing Arrow now.");
                return false;
            }
        }

        #endregion

        #region aspects

        bool usingAspectOfTheFox = false;
        private void SelectAspect()
        {
            if (SpellManager.HasSpell("Aspect of the Fox") && Me.FocusPercent < 15)
            {
                AspectOfTheFox();
                usingAspectOfTheFox = true;
            }

            if (usingAspectOfTheFox && Me.FocusPercent < 100)
            {
                return;
            }
            else
            {
                usingAspectOfTheFox = false;
            }

            if (Me.GotTarget)
            {
                // ranged
                if (Me.CurrentTarget.Distance > 10 || Me.HealthPercent > 50)
                {
                    if (SpellManager.Spells.ContainsKey("Aspect of the Dragonhawk"))
                    {
                        if (!Me.Auras.ContainsKey("Aspect of the Dragonhawk"))
                        {
                            AspectOfTheDragonhawk();
                        }
                    }
                    else if (SpellManager.Spells.ContainsKey("Aspect of the Hawk"))
                    {
                        if (!Me.Auras.ContainsKey("Aspect of the Hawk"))
                        {
                            AspectOfTheHawk();
                        }
                    }
                    else if (SpellManager.Spells.ContainsKey("Aspect of the Monkey") &&
                        !Me.Auras.ContainsKey("Aspect of the Monkey"))
                    {
                        AspectofTheMonkey();
                    }
                }
                else
                //melee
                {
                    if (SpellManager.Spells.ContainsKey("Aspect of the Beast"))
                    {
                        if (!Me.Auras.ContainsKey("Aspect of the Beast"))
                        {
                            AspectOfTheBeast();
                        }
                    }
                    else if (SpellManager.Spells.ContainsKey("Aspect of the Monkey") &&
                        !Me.Auras.ContainsKey("Aspect of the Monkey"))
                    {
                        AspectofTheMonkey();
                    }
                }
            }

        }
        #endregion

        #region melee

        #endregion

        #region ranged
        #endregion

        #region traps
        #endregion

        #region petcare
        #endregion

        #region utilities

        private List<WoWUnit> getAdds()
        {
            DateTime startLists = DateTime.Now;

            List<WoWUnit> mobList = ObjectManager.GetObjectsOfType<WoWUnit>(false);

            if (DateTime.Now.Subtract(startLists).Seconds > 0)
            {
                Logging.WriteDebug("Make Lists: " + DateTime.Now.Subtract(startLists).Seconds + "." + DateTime.Now.Subtract(startLists).Milliseconds);
            }

            List<WoWUnit> enemyMobList = new List<WoWUnit>();

            foreach (WoWUnit thing in mobList)
            {
                try
                {
                    if ((thing.Guid != Me.Guid) &&
                    thing.IsTargetingMeOrPet)
                    {
                        enemyMobList.Add(thing);
                    }
                }
                catch (Exception ex)
                {
                    Logging.WriteException(ex);
                    slog("Add error.");
                }
            }

            if (enemyMobList.Count > 1)
            {
                slog("Warning: there are " + enemyMobList.Count.ToString() + " attackers.");
            }
            return enemyMobList;
        }

        private void slog(string msg)
        {
            if (msg != _logspam)
            {
                Logging.Write(msg);
                _logspam = msg;
            }
        }

        private void slog(string format, params object[] args)
        {
            Logging.Write(format, args);
        }

        #endregion

        #region movement logic

        /// <summary>
        /// Move while alive and in game
        /// </summary>
        /// <param name="direction">The way to go</param>
        /// <param name="duration">Time to go in milliseconds</param>
        private void SafeMove(WoWMovement.MovementDirection direction, int duration)
        {
            DateTime start = DateTime.Now;

            WoWMovement.Move(direction);

            while (ObjectManager.IsInGame && ObjectManager.Me.HealthPercent > 1)
            {
                Thread.Sleep(335);

                if (DateTime.Now.Subtract(start).Milliseconds < 300 || DateTime.Now.Subtract(start).Milliseconds >= duration)
                {
                    break;
                }

            }

            WoWMovement.MoveStop(direction);
        }

        WoWPoint attackPoint
        {
            get
            {
                if (Me.GotTarget && SpellManager.Spells.ContainsKey("Auto Shot"))
                {
                    return WoWMovement.CalculatePointFrom(Me.CurrentTarget.Location, (float)SpellManager.Spells["Auto Shot"].MaxRange - 3);
                }
                else
                {
                    WoWPoint noSpot = new WoWPoint();
                    return noSpot;
                }
            }
        }



        #endregion

        #region horde racials

        // Berserking
        void Berserking()
        {
            if (ObjectManager.Me.Race == WoWRace.Troll &&
                SafeCast("Berserking"))
            {
                Styx.Helpers.Logging.Write("Berserking.");
            }
        }

        // Bloodrage
        void Bloodrage()
        {
            if (ObjectManager.Me.Race == WoWRace.Orc &&
                SafeCast("Bloodrage"))
            {
                Styx.Helpers.Logging.Write("Bloodrage.");
            }
        }

        // War Stomp
        void WarStomp()
        {
            if (ObjectManager.Me.Race == WoWRace.Tauren &&
                SafeCast("War Stomp"))
            {
                Styx.Helpers.Logging.Write("War Stomp.");
            }
        }

        // Arcane Torrent
        void ArcaneTorrent()
        {
            if (ObjectManager.Me.Race == WoWRace.BloodElf &&
                SafeCast("Arcane Torrent"))
            {
                Styx.Helpers.Logging.Write("Arcane Torrent.");
            }
        }

        // Will of the Forsaken 
        void WillOfTheForsaken()
        {
            if (ObjectManager.Me.Race == WoWRace.Undead &&
                SafeCast("Will of the Forsaken"))
            {
                Styx.Helpers.Logging.Write("Will of the Forsaken.");
            }
        }
        #endregion

        #region alliance racials

        // Stoneform
        void Stoneform()
        {
            if (ObjectManager.Me.Race == WoWRace.Dwarf &&
                SafeCast("Stoneform"))
            {
                Styx.Helpers.Logging.Write("Stoneform.");
            }
        }

        // Gift of the Naaru
        void GiftOfTheNaaru()
        {
            if (ObjectManager.Me.Race == WoWRace.Draenei &&
                SafeCast("Gift of the Naaru"))
            {
                Styx.Helpers.Logging.Write("Gift of the Naaru.");
            }
        }

        // Every Man for Himself
        void EveryManForHimself()
        {
            if (ObjectManager.Me.Race == WoWRace.Human &&
                SafeCast("Every Man for Himself"))
            {
                Styx.Helpers.Logging.Write("Every Man for Himself.");
            }
        }

        // Shadowmeld
        void Shadowmeld()
        {
            if (ObjectManager.Me.Race == WoWRace.NightElf &&
                SafeCast("Shadowmeld"))
            {
                Styx.Helpers.Logging.Write("Shadowmeld.");
            }
        }

        // Escape Artist
        void EscapeArtist()
        {
            if (ObjectManager.Me.Race == WoWRace.Gnome &&
                SafeCast("Escape Artist"))
            {
                Styx.Helpers.Logging.Write("Escape Artist.");
            }
        }


        #endregion
    }
}
#pragma warning restore