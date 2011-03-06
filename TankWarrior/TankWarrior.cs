using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using System.Linq;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.Patchables;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;


namespace Styx.Bot.CustomClasses
{

    public class Warrior : CombatRoutine
    {
        public override WoWClass Class { get { return WoWClass.Warrior; } }

        public override string Name { get { return "Hawker's Tank 2.0"; } }

        #region Utilities
        private static void Player_OnMobKilled(BotEvents.Player.MobKilledEventArgs args)
        {
            StyxWoW.Me.ClearTarget();

            if (StyxWoW.Me.IsAutoAttacking)
            {
                StyxWoW.Me.ToggleAttack();
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
            get { return MyTarget != null; }
        }

        private bool SafeCast(int spellId)
        {
            if (SpellManager.HasSpell(spellId) && !StyxWoW.Me.IsCasting)
            {
                while (StyxWoW.GlobalCooldown)
                {
                    Thread.Sleep(100);
                }

                SpellManager.Cast(spellId);
                return true;
            }

            return false;
        }

        private bool SafeCast(string spellName)
        {
            if (Me.IsCasting || !SpellManager.HasSpell(spellName) || SpellManager.Spells[spellName].Cooldown)
            {
                return false;
            }

            if (SpellManager.Spells[spellName].PowerType == WoWPowerType.Rage && Me.CurrentRage < SpellManager.Spells[spellName].PowerCost)
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
                    if (GotTarget && MyTarget.Attackable)
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

                if (!SpellManager.CanCast(spellName))
                {
                    Logging.WriteDebug("{0} can't cast {1}.", Name, spellName);
                }

                if (SpellManager.Cast(spellName))
                {
                    return true;
                }
            }



            return false;
        }

        private static Stopwatch _addsWarning = new Stopwatch();
        private List<WoWUnit> AddsList
        {
            get
            {
                List<WoWUnit> mobList = ObjectManager.GetObjectsOfType<WoWUnit>(false);
                var enemyMobList = new List<WoWUnit>();

                foreach (WoWUnit thing in mobList)
                {
                    try
                    {
                        if ((thing.Guid != Me.Guid) && (thing.IsTargetingMeOrPet || thing.Fleeing))
                        {
                            enemyMobList.Add(thing);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logging.WriteException(ex);
                        Logging.Write("Add error.");
                    }
                }

                if (enemyMobList.Count > 1)
                {
                    if (!_addsWarning.IsRunning || _addsWarning.ElapsedMilliseconds > 10)
                    {
                        Logging.Write("Warning: there are " + enemyMobList.Count + " attackers.");
                        _addsWarning.Reset();
                        _addsWarning.Start();
                    }
                }
                return enemyMobList;
            }
        }

        private static void SafeMove(WoWMovement.MovementDirection direction, int duration)
        {
            DateTime start = DateTime.Now;

            if (direction == WoWMovement.MovementDirection.StrafeMovement)
            {
                // Backwards means we want to strafe... check that first.
                // Left first
                if (CanStrafeSafely(true, 15f))
                {
                    direction = WoWMovement.MovementDirection.StrafeLeft;
                }
                else if (CanStrafeSafely(false, 15f))
                {
                    direction = WoWMovement.MovementDirection.StrafeRight;
                }
                else
                {
                    direction = WoWMovement.MovementDirection.Backwards;
                }
            }

            WoWMovement.Move(direction);

            while (ObjectManager.IsInGame && ObjectManager.Me.HealthPercent > 1)
            {
                Thread.Sleep(335);

                if (DateTime.Now.Subtract(start).Milliseconds < 300 ||
                    DateTime.Now.Subtract(start).Milliseconds >= duration)
                {
                    break;
                }
            }

            WoWMovement.MoveStop(direction);
        }

        public static bool CanStrafeSafely(bool left, float distance)
        {
            // Do this in degrees, since I'm lazy for right now.
            float myFacing = StyxWoW.Me.RotationDegrees;
            float direction = left ? myFacing - 90f : myFacing + 90f;

            if (direction < 0)
            {
                direction += 360f;
            }
            else if (direction > 360)
            {
                direction -= 360f;
            }

            direction = WoWMathHelper.DegreesToRadians(direction);
            WoWPoint myLoc = StyxWoW.Me.Location;
            WoWPoint endPoint = WoWPoint.RayCast(myLoc, direction, distance);

            // First, check for terrain collisions.
            if (GameWorld.IsInLineOfSight(endPoint, myLoc))
            {
                // We can strafe on the terrain, so now we check if we will run into aggro over there
                return AggroWithin(endPoint, distance, false) == 0;
            }

            // Can't safely strafe, so return false.
            return false;
        }

        public static int AggroWithin(WoWPoint obj, float range, bool targetingMe)
        {
            if (targetingMe)
            {
                return (from o in ObjectManager.ObjectList
                        where o is WoWUnit && o.Location.Distance(obj) <= range
                        let u = o.ToUnit()
                        where u.Attackable && u.Aggro && !u.Dead && u.CurrentTarget == StyxWoW.Me
                        select o).Count();
            }
            return (from o in ObjectManager.ObjectList
                    where o is WoWUnit && o.Location.Distance(obj) <= range
                    let u = o.ToUnit()
                    where u.Attackable && u.Aggro && !u.Dead
                    select o).Count();
        }

        Dictionary<ulong, WoWUnit> partyMembers;
        List<uint> runners;

        #endregion

        #region Global Variables

        public readonly LocalPlayer Me = ObjectManager.Me;
        private static readonly List<ulong> RunnerList = new List<ulong>();

        uint deathCount = 0;

        private readonly List<uint> buggedMobs = new List<uint>()
            {
                28093, // Sholazar Tickbird
                22979, // Wild Sparrowhawk
            };



        #endregion

        #region Rest

        private static Stopwatch Warbringertimer = new Stopwatch();
        private static bool useWarbringer = false;

        public override bool NeedRest
        {
            get
            {
                if (Battlegrounds.IsInsideBattleground)
                {
                    return false;
                }

                if (ObjectManager.Me.Combat)
                {
                    return false;
                }

                if (ObjectManager.Me.Auras.ContainsKey("Resurrection Sickness"))
                {
                    return true;
                }

                if (ObjectManager.Me.IsSwimming)
                {
                    return false;
                }

                if (!Me.Combat && Me.HealthPercent < 55)
                {
                    Logging.Write("Health is {0}%", Me.HealthPercent);
                    return true;
                }

                foreach (WoWPlayer player in Me.PartyMembers)
                {
                    if (player.Auras.ContainsKey("Food") || player.Auras.ContainsKey("Drink") || player.HealthPercent < 35 || player.ManaPercent < 35 ||
                        player.Dead || player.IsGhost)
                    {
                        return true;
                    }
                }

                if (!Warbringertimer.IsRunning || Warbringertimer.ElapsedMilliseconds > 15 * 60 * 1000)
                {
                    List<string> Warbringers = Lua.LuaGetReturnValue("return GetTalentInfo(3,11)", "hawker.lua");

                    if (Convert.ToInt32(Warbringers[4]) == 1)
                    {
                        useWarbringer = true;
                    }

                    Warbringertimer.Reset();
                    Warbringertimer.Start();

                }

                if (!useWarbringer && !Me.Combat && Me.Shapeshift != ShapeshiftForm.BattleStance)
                {
                    return true;
                }

                return Me.GetPowerPercent(WoWPowerType.Health) <= 50;
            }
        }

        public override void Rest()
        {
            BattleShout();

            if (!useWarbringer && !Me.Combat && Me.Shapeshift != ShapeshiftForm.BattleStance)
            {
                BattleStance();
            }
            Logic.Common.Rest.Feed();
        }

        #endregion

        #region Pull

        private static ulong pullGuid;
        private static Stopwatch pullTimer = new Stopwatch();

        /// <summary>
        /// Used for instance farming and party botting
        /// </summary>
        /// <returns>true if it can throw</returns>
        private bool PullWithThrow()
        {
            if (!Battlegrounds.IsInsideBattleground && Me.PartyMembers.Count > 0 && !MyTarget.IsPlayer && SpellManager.HasSpell("Throw"))
            {
                if (MyTarget.Distance > SpellManager.Spells["Throw"].MaxRange - 2)
                {
                    Logging.Write("Get to {0} yards.", SpellManager.Spells["Throw"].MaxRange);
                    int a = 0;
                    while (a < 50 && ObjectManager.Me.IsAlive && GotTarget && MyTarget.Distance > SpellManager.Spells["Throw"].MaxRange)
                    {
                        if (ObjectManager.Me.Combat)
                        {
                            Logging.Write("Combat has started.  Abandon pull.");
                            break;
                        }
                        WoWMovement.Face();
                        Navigator.MoveTo(WoWMovement.CalculatePointFrom(MyTarget.Location, SpellManager.Spells["Throw"].MaxRange - 2));
                        Thread.Sleep(250);
                        ++a;
                    }
                }

                if (MyTarget.Distance > 4 && MyTarget.Distance <= SpellManager.Spells["Throw"].MaxRange && MyTarget.InLineOfSight)
                {

                    WoWMovement.MoveStop();
                    WoWMovement.Face();
                    Thread.Sleep(500);
                    SafeCast("Throw");
                    MyTarget.Interact();
                    Logging.Write("Pull with Throw.");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// If all else fails, run up and whack the target
        /// </summary>
        private void PullWithMelee()
        {
            float attackRange = 5;

            int a = 0;
            while (a < 6 && GotTarget && MyTarget.Distance > attackRange)
            {
                Navigator.MoveTo(MyTarget.Location);
                ++a;
            }

            WoWMovement.Face();
            if (!Me.Combat)
            {
                Thread.Sleep(500);
            }

            MyTarget.Interact();
        }

        /// <summary>
        /// Enter BattleStance and Charge
        /// </summary>
        /// <returns>true if Charge was cast</returns>
        private bool PullWithCharge()
        {
            if (SpellManager.HasSpell("Charge") && !SpellManager.Spells["Charge"].Cooldown)
            {
                if (!useWarbringer && Me.Shapeshift != ShapeshiftForm.BattleStance)
                {
                    Logging.Write("Enter Battle Stance to Charge.");
                    BattleStance();
                }

                if (MyTarget.Distance > SpellManager.Spells["Charge"].MaxRange - 2)
                {
                    Logging.Write("Get to {0} yards.", SpellManager.Spells["Charge"].MaxRange);
                    int a = 0;
                    while (a < 50 && ObjectManager.Me.IsAlive && GotTarget && MyTarget.Distance > SpellManager.Spells["Charge"].MaxRange)
                    {
                        if (ObjectManager.Me.Combat)
                        {
                            Logging.Write("Combat has started.  Abandon pull.");
                            break;
                        }
                        WoWMovement.Face();
                        Navigator.MoveTo(WoWMovement.CalculatePointFrom(MyTarget.Location, SpellManager.Spells["Charge"].MaxRange - 2));
                        Thread.Sleep(250);
                        ++a;
                    }
                }

                if (MyTarget.Distance > 4 && MyTarget.Distance <= SpellManager.Spells["Charge"].MaxRange && MyTarget.InLineOfSight)
                {

                    WoWMovement.MoveStop();
                    WoWMovement.Face();
                    Thread.Sleep(500);
                    SafeCast("Charge");
                    Thread.Sleep(500);


                    if (SpellManager.Spells["Charge"].Cooldown)
                    {
                        Logging.Write("Pull with Charge.");
                        MyTarget.Interact();
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Intercept to preserve the Berserker Stance rage
        /// </summary>
        /// <returns>true if it has cast Intercept</returns>
        private bool PullWithIntercept()
        {
            if (SpellManager.HasSpell("Intercept") && !SpellManager.Spells["Intercept"].Cooldown &&
                Me.Shapeshift == ShapeshiftForm.BerserkerStance && Me.CurrentRage > SpellManager.Spells["Intercept"].PowerCost)
            {
                if (MyTarget.Distance > SpellManager.Spells["Intercept"].MaxRange - 2)
                {
                    Logging.Write("Get to {0} yards.", SpellManager.Spells["Intercept"].MaxRange);
                    int a = 0;
                    while (a < 50 && ObjectManager.Me.IsAlive && GotTarget && MyTarget.Distance > SpellManager.Spells["Intercept"].MaxRange)
                    {
                        if (ObjectManager.Me.Combat)
                        {
                            Logging.Write("Combat has started.  Abandon pull.");
                            break;
                        }
                        WoWMovement.Face();
                        Navigator.MoveTo(WoWMovement.CalculatePointFrom(MyTarget.Location, SpellManager.Spells["Intercept"].MaxRange - 2));
                        Thread.Sleep(250);
                        ++a;
                    }
                }

                if (MyTarget.Distance > 4 && MyTarget.Distance <= SpellManager.Spells["Intercept"].MaxRange && MyTarget.InLineOfSight)
                {

                    WoWMovement.MoveStop();
                    WoWMovement.Face();
                    Thread.Sleep(500);
                    SafeCast("Intercept");
                    Thread.Sleep(500);


                    if (SpellManager.Spells["Intercept"].Cooldown)
                    {
                        Logging.Write("Pull with Intercept.");
                        MyTarget.Interact();
                        return true;
                    }
                }
            }

            return false;

        }

        public override void Pull()
        {
            if (GotTarget)
            {
                WoWPoint[] pathToTarget = Navigator.GeneratePath(ObjectManager.Me.Location, MyTarget.Location);

                if (MyTarget.IsFlying || pathToTarget.Length == 0 || !GameWorld.IsInLineOfSight(MyTarget.Location, pathToTarget[pathToTarget.Length - 1]))
                {
                    Blacklist.Add(MyTarget.Guid, new TimeSpan(0, 1, 0));
                    Logging.Write("(0} cannot be reached for now.", MyTarget.Name);
                    Thread.Sleep(500);
                    Me.ClearTarget();
                    return;
                }
            }

            if (GotTarget && MyTarget.Guid != pullGuid)
            {
                pullTimer.Reset();
                pullTimer.Start();
                pullGuid = MyTarget.Guid;
                Logging.Write("Killing " + MyTarget.Name + " at distance " + System.Math.Floor(MyTarget.Distance));
                if (RunnerList.Contains(MyTarget.Entry))
                {
                    Logging.Write("{0} is likely to try to run away.", MyTarget.Name);
                }
            }

            if (!MyTarget.IsPlayer && pullTimer.ElapsedMilliseconds > 30 * 1000)
            {
                Logging.Write(" This " + MyTarget.Name + " is a bugged mob.  Blacklisting for 1 hour.");

                Logic.Blacklist.Add(MyTarget.Guid, TimeSpan.FromHours(1.00));

                Me.ClearTarget();
                pullGuid = 0;
            }

            if (Me.Mounted)
            {
                Logging.Write("Dismount to pull " + MyTarget.Name + ".");
                Logic.Mount.Dismount();
            }

            BattleShout();

            if (!PullWithThrow() && !PullWithIntercept() && !PullWithCharge())
            {
                PullWithMelee();
            }
        }

        #endregion

        #region Pull Buffs

        public override bool NeedPullBuffs { get { return NeedPreCombatBuffs; } }

        public override void PullBuff() { }

        #endregion

        #region Pre Combat Buffs

        public override bool NeedPreCombatBuffs
        {
            get
            {
                return false;
            }
        }

        public override void PreCombatBuff()
        {
            if (SpellManager.Spells.ContainsKey("Bloodrage") && !SpellManager.Spells["Bloodrage"].Cooldown)
            {
                Bloodrage();
            }
        }

        #endregion

        #region Combat Buffs

        public override bool NeedCombatBuffs
        {
            get
            {
                return false;
            }
        }

        public override void CombatBuff()
        {
            if (Me.HealthPercent > 30 && SpellManager.Spells.ContainsKey("Bloodrage") && !SpellManager.Spells["Bloodrage"].Cooldown)
            {
                Bloodrage();
            }
        }

        #endregion

        #region Heal

        public bool NeedAHeal { get { return Me.HealthPercent <= 50; } }

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

        public override void Heal()
        {
            LastStand();

            if ((Me.HealthPercent < 30 && MyTarget.HealthPercent > MyTarget.HealthPercent))
            {
                UseHealthPotion();
            }
        }

        #endregion

        #region Falling

        public void HandleFalling() { }

        #endregion

        #region Combat
        private static ulong lastGuid;
        private static Stopwatch fightTimer = new Stopwatch();
        private static DateTime leftCombat;
        public override void Combat()
        {
            try
            {
                if (StyxWoW.GlobalCooldown || StyxWoW.Me.IsCasting)
                    return;

                # region checks
                if (DateTime.Now.Subtract(leftCombat).Seconds > 1)
                {
                    Logging.WriteDebug("Combat cycle: " + DateTime.Now.Subtract(leftCombat).Seconds + "." + DateTime.Now.Subtract(leftCombat).Milliseconds);
                }

                DateTime combatStart = DateTime.Now;

                if (GotTarget && MyTarget.Dead)
                {
                    Logging.Write("My target is dead.");
                    Me.ClearTarget();
                }

                if (!GotTarget)
                {
                    if (!Equals(null, Targeting.Instance.FirstUnit))
                    {
                        Targeting.Instance.FirstUnit.Target();
                    }
                }

                if (Targeting.Instance.FirstUnit != null && Targeting.Instance.FirstUnit.CurrentTargetGuid == Me.Guid &&
                        (!GotTarget || MyTarget.Dead || !MyTarget.Combat))
                {
                    Targeting.Instance.FirstUnit.Target();
                    Thread.Sleep(200);
                }

                if (!Battlegrounds.IsInsideBattleground && Me.PartyMembers.Count > 0)
                {
                    foreach (WoWPlayer player in Me.PartyMembers)
                    {
                        if (player.GotTarget && !player.CurrentTarget.IsPlayer &&
                            !player.CurrentTarget.IsFriendly && player.CurrentTarget.CurrentTargetGuid == player.Guid)
                        {
                            Logging.Write("Pull " + player.CurrentTarget.Name + " off team member.");
                        }
                    }
                }

                if (GotTarget && (MyTarget.Guid != lastGuid || InfoPanel.Deaths > deathCount))
                {
                    if (InfoPanel.Deaths > deathCount)
                    {
                        deathCount = InfoPanel.Deaths;
                    }

                    fightTimer.Reset();
                    fightTimer.Start();
                    lastGuid = MyTarget.Guid;
                    Logging.WriteDebug(MyTarget.Name + " is a new target: " + lastGuid.ToString());
                }

                if (GotTarget && !MyTarget.IsPlayer && fightTimer.IsRunning && fightTimer.ElapsedMilliseconds > 40 * 1000 && MyTarget.HealthPercent > 75)
                {
                    Logging.Write(" This " + MyTarget.Name + " is a bugged mob.  Combat blacklisting for 1 hour.");

                    Logic.Blacklist.Add(MyTarget.Guid, TimeSpan.FromHours(1.00));

                    SafeMove(WoWMovement.MovementDirection.Backwards, 5000);
                    Me.ClearTarget();
                    lastGuid = 0;
                    return;

                }

                // measurements
                DateTime startCheck = DateTime.Now;
                List<WoWUnit> mobList = ObjectManager.GetObjectsOfType<WoWUnit>(false);
                List<WoWUnit> enemyMobList = new List<WoWUnit>();

                if (CombatChecks &&
                    Me.Mounted)
                {
                    Logging.Write("Dismount for combat.");
                    Logic.Mount.Dismount();
                }

                if (!GotTarget)
                {
                    return;
                }
                else if (MyTarget.Distance > 5 && MyTarget.Distance < 30)
                {
                    WoWPoint point = WoWMovement.CalculatePointFrom(MyTarget.Location, 4f);
                    WoWMovement.ClickToMove(point);
                    Thread.Sleep(500);
                    Logging.Write("Get in range");
                    return;
                }
                else
                {
                    WoWMovement.MoveStop();
                }

                if (DateTime.Now.Subtract(combatStart).Seconds > 1)
                {
                    Logging.Write("Combat checks took: " + DateTime.Now.Subtract(combatStart).Seconds.ToString() + "." + DateTime.Now.Subtract(combatStart).Milliseconds.ToString());
                }

                #endregion

                #region survival

                if ((Me.HealthPercent < 30 && MyTarget.HealthPercent > Me.HealthPercent && AddsList.Count == 1) || (Me.HealthPercent < 55 && AddsList.Count > 1))
                {
                    LastStand();
                }

                if ((Me.HealthPercent < 50 && MyTarget.HealthPercent > Me.HealthPercent && AddsList.Count == 1) || (Me.HealthPercent < 75 && AddsList.Count > 1))
                {
                    if (SpellManager.Spells.ContainsKey("Lifeblood") && !SpellManager.Spells["Lifeblood"].Cooldown && SafeCast("Lifeblood"))
                    {
                        Logging.Write("Lifeblood cast at {0}% health.", (int)Me.HealthPercent);
                    }
                    else
                    {
                        UseHealthPotion();
                    }
                }

                if (Me.HealthPercent < 75 || AddsList.Count > 1)
                {
                    if (SpellManager.Spells.ContainsKey("Concussion Blow") && !SpellManager.Spells["Concussion Blow"].Cooldown)
                    {
                        ConcussionBlow();
                    }
                    else if (SpellManager.Spells.ContainsKey("Shield Block") && !SpellManager.Spells["Shield Block"].Cooldown)
                    {
                        ShieldBlock();
                    }
                }


                #endregion

                #region runners
                #endregion

                #region tanking
                // retake aggro asap
                if (!Battlegrounds.IsInsideBattleground && Me.PartyMembers.Count > 0 && !MyTarget.IsPlayer && Me.CurrentTargetGuid > 0 && Me.CurrentTargetGuid != Me.Guid)
                {
                    if (SpellManager.HasSpell("Taunt") && !SpellManager.Spells["Taunt"].Cooldown &&
                        Me.IsGroupLeader && SafeCast("Taunt"))
                    {
                        Logging.Write("Taunt.");
                    }
                    else if (SpellManager.HasSpell("Challenging Shout") && !SpellManager.Spells["Challenging Shout"].Cooldown &&
                        Me.IsGroupLeader && SafeCast("Challenging Shout"))
                    {
                        Logging.Write("Challenging Shout.");
                    }
                    else if (SpellManager.HasSpell("Mocking Blow") && !SpellManager.Spells["Mocking Blow"].Cooldown &&
                        Me.IsGroupLeader && SafeCast("Mocking Blow"))
                    {
                        Logging.Write("Mocking Blow.");
                    }
                    else if (SpellManager.HasSpell("Concussion Blow") && !SpellManager.Spells["Concussion Blow"].Cooldown &&
                        Me.IsGroupLeader && SafeCast("Concussion Blow"))
                    {
                        Logging.Write("Concussion Blow.");
                    }
                }
                #endregion

                ProtectionDPS();
            }
            finally
            {
                if (GotTarget && MyTarget.IsAlive)
                {
                    uint targetEntry = MyTarget.Entry;
                    leftCombat = DateTime.Now;
                    if (MyTarget.Fleeing && !RunnerList.Contains(targetEntry))
                    {

                        Logging.Write("Adding {0} to the list of running mobs.", MyTarget.Name);
                        RunnerList.Add(targetEntry);

                    }
                }

            }
        }

        private WoWUnit getTauntTarget
        {
            get
            {
                List<WoWUnit> mobList = ObjectManager.GetObjectsOfType<WoWUnit>(false);
                List<WoWUnit> enemyMobList = new List<WoWUnit>();
                List<ulong> partyGuids = new List<ulong>();

                if (Me.PartyMembers.Count > 0)
                {
                    foreach (WoWPlayer player in Me.PartyMembers)
                    {
                        if (!partyGuids.Contains(player.Guid))
                            partyGuids.Add(player.Guid);
                    }
                }

                foreach (WoWUnit brute in mobList)
                {
                    if (!brute.IsTargetingMeOrPet && partyGuids.Contains(brute.CurrentTargetGuid))
                        return brute;
                }

                return null;
            }
        }

        #endregion

        #region Spells

        /// <summary>
        /// Check's if Overpower is usable and if it is it casts it
        /// </summary>
        private void Overpower()
        {
            if (GotTarget && MyTarget.Distance > 5)
            {
                return;
            }

            if (SpellManager.HasSpell("Overpower"))
            {
                var isUsable = Lua.LuaGetReturnValue("return IsUsableSpell('Overpower')", "hax.lua");

                if (isUsable != null)
                {
                    if (isUsable[0] == "1")
                    {
                        SafeCast("Overpower");
                        Logging.Write("Overpower.");
                    }
                }
            }
        }

        /// <summary>
        /// Check's if Revenge is usable and if it is it casts it
        /// </summary>
        private void Revenge()
        {
            if (GotTarget && MyTarget.Distance > 5)
            {
                return;
            }

            if (Me.Shapeshift != ShapeshiftForm.DefensiveStance)
            {
                DefensiveStance();
            }

            if (SpellManager.HasSpell("Revenge"))
            {
                //if (HasBuffProcced("Glyph of Revenge", 1) && SafeCast("Heroic Strike"))
                //{
                //    Logging.Write("Free Heroic Strike!");
                //}

                var isUsable = Lua.LuaGetReturnValue("return IsUsableSpell('Revenge')", "hax.lua");

                if (isUsable != null)
                {
                    if (isUsable[0] == "1" && SafeCast("Revenge"))
                    {
                        Logging.Write("Revenge.");
                    }
                }
            }
        }

        /// <summary>
        /// http://www.wowhead.com/?spell=100
        /// Charge an _enemy, generating rage, and stun it for 1.50 sec.  Cannot be used in combat.        
        /// </summary>
        bool Charge()
        {
            if (!Me.Combat)
            {
                if (!useWarbringer)
                {
                    BattleStance();
                }

                if (SafeCast("Charge"))
                {
                    Logging.Write("Pulled with Charge.");
                    return true;

                }
            }
            return false;
        }

        /// <summary>
        /// http://www.wowhead.com/?spell=78
        /// A strong attack that increases melee damage by 11 and causes a high amount of threat.        
        /// Rage cost reduces with talents
        /// </summary>
        void HeroicStrike()
        {
            if (!GotTarget || MyTarget.Distance > 5)
            {
                return;
            }

            if (SafeCast("Heroic Strike"))
            {
                Logging.Write("Heroic Strike.");
            }
        }

        /// <summary>
        /// http://www.wowhead.com/?spell=2457
        /// A balanced combat stance that increases the armor penetration of all of your attacks by 10%.        
        /// </summary>
        void BattleStance()
        {
            if (ObjectManager.Me.Shapeshift != ShapeshiftForm.BattleStance)
            {
                SafeCast("Battle Stance");
                Logging.Write("Battle Stance.");
            }
        }

        /// <summary>
        /// http://www.wowhead.com/?spell=71
        /// A defensive combat stance.  Decreases damage taken by 10% and damage caused by 5%.  Increases threat generated.
        /// </summary>
        void DefensiveStance()
        {
            if (SpellManager.HasSpell("Defensive Stance") && ObjectManager.Me.Shapeshift != ShapeshiftForm.DefensiveStance)
            {
                SafeCast("Defensive Stance");
                Logging.Write("Defensive Stance.");
            }
        }

        /// <summary>
        /// http://www.wowhead.com/?spell=2458
        /// An aggressive stance.  Critical hit chance is increased by 3% and all damage taken is increased by 5%.
        /// </summary>
        void BerserkerStance()
        {
            if (Me.Level < 30)
            {
                return;
            }

            if ((SpellManager.HasSpell("Berserker Stance") &&
                ObjectManager.Me.Shapeshift != ShapeshiftForm.BerserkerStance))
            {
                SafeCast("Berserker Stance");
                Logging.Write("Berserker Stance.");
            }
        }

        /// <summary>
        /// http://www.wowhead.com/?spell=6673
        /// The warrior shouts, increasing attack power of all raid and party members within 30 yards by 15.  Lasts 2 min.
        /// </summary>
        void BattleShout()
        {

            if (!Me.Auras.ContainsKey("Blessing of Might"))
            {
                if ((!Me.Auras.ContainsKey("Battle Shout")) || Me.Auras.ContainsKey("Battle Shout") && Me.Auras["Battle Shout"].TimeLeft < new TimeSpan(0, 0, 30))
                {
                    if (SafeCast("Battle Shout"))
                    {
                        Logging.Write("Battle Shout.");
                    }
                }
            }
        }

        /// <summary>
        /// http://www.wowhead.com/?spell=1680
        /// In a whirlwind of steel you attack up to 4 enemies within 8 yards, causing weapon damage from both melee weapons to each enemy.
        /// </summary>
        void Whirlwind()
        {
            if (GotTarget && MyTarget.Distance > 5)
            {
                return;
            }

            if (SafeCast("Whirlwind"))
            {
                Logging.Write("Whirlwind.");
            }
        }

        /// <summary>
        /// http://www.wowhead.com/?spell=772
        /// Wounds the target causing immediate damage and them to bleed over 15 sec.
        /// </summary>
        void Rend()
        {
            if (ObjectManager.Me.Shapeshift == ShapeshiftForm.BerserkerStance)
            {
                return;
            }

            if (GotTarget && MyTarget.Distance > 5)
            {
                return;
            }

            if (MyTarget.HealthPercent > 50 &&
                !MyTarget.Auras.ContainsKey("Rend") &&
                SafeCast("Rend"))
            {
                Logging.Write("Rend.");
            }

        }

        /// <summary>
        /// http://www.wowhead.com/?spell=18499
        /// The warrior enters a berserker rage, removing and granting immunity to Fear, Sap and Incapacitate effects and generating extra rage when taking damage.  Lasts 10 sec.
        /// </summary>
        void BerserkerRage()
        {
            if (SpellManager.HasSpell("Berserker Rage") && SafeCast("Berserker Rage"))
            {
                Logging.Write("Berserker Rage.");
            }
        }



        /// <summary>
        /// http://www.wowhead.com/?spell=6343
        /// Blasts nearby enemies increasing the time between their attacks by 10% for 10 sec and doing 15 damage to them.  Damage increased by attack power.  This ability causes additional threat.
        /// </summary>
        void ThunderClap()
        {
            if (ObjectManager.Me.Shapeshift == ShapeshiftForm.BerserkerStance)
            {
                return;
            }

            if (GotTarget && MyTarget.Distance > 5)
            {
                return;
            }

            if (SafeCast("Thunder Clap"))
            {
                Logging.Write("Thunder Clap.");
            }
        }

        /// <summary>
        /// http://www.wowhead.com/?spell=1715
        /// Maims the _enemy, reducing movement speed by 50% for 15 sec.    
        /// </summary>
        void Hamstring()
        {
            if (ObjectManager.Me.Shapeshift == ShapeshiftForm.BerserkerStance || ObjectManager.Me.Shapeshift == ShapeshiftForm.BattleStance)
                return;

            if (GotTarget && MyTarget.Distance > 5)
            {
                return;
            }

            if (!MyTarget.Auras.ContainsKey("Hamstring") &&
                SafeCast("Hamstring"))
            {
                Logging.Write("Hamstring.");
            }
        }

        /// <summary>
        /// http://www.wowhead.com/?spell=12809
        /// GStuns the opponent for 5 sec and deals damage (based on attack power).
        /// </summary>
        void ConcussionBlow()
        {
            if (GotTarget && MyTarget.Distance > 5)
            {
                return;
            }

            if (SafeCast("Concussion Blow"))
            {
                Logging.Write("Concussion Blow.");
            }
        }

        void Bloodrage()
        {
            if (SafeCast("Bloodrage"))
            {
                Logging.Write("Bloodrage.");
            }
        }

        void SunderArmor()
        {
            if (GotTarget && MyTarget.Distance > 5)
            {
                return;
            }

            if (GotTarget && MyTarget.Auras.ContainsKey("Sunder Armor") && MyTarget.Auras["Sunder Armor"].StackCount > 4)
            {
                return;
            }

            if (SpellManager.HasSpell("Devastate"))
            {
                SafeCast("Devastate");
                Logging.Write("Devastate.");
            }
            else if (SafeCast("Sunder Armor"))
            {
                Logging.Write("Sunder Armor.");
            }
        }


        /// <summary>
        /// http://www.wowhead.com/?spell=1160
        /// Reduces the melee attack power of all enemies within 10 yards by 35 for 30 sec.
        /// </summary>
        void DemoralizingShout()
        {
            if (GotTarget)
            {
                if (!MyTarget.Auras.ContainsKey("Demoralizing Shout"))
                {
                    if (MyTarget.IsPlayer)
                        return;

                    if (MyTarget.Distance > 10)
                        return;

                    if (SafeCast("Demoralizing Shout"))
                    {
                        Logging.Write("Demoralizing Shout.");
                    }
                }
            }
        }

        /// <summary>
        /// http://www.wowhead.com/?spell=845
        /// A sweeping attack that does your weapon damage plus 15 to the target and his nearest ally.
        /// </summary>
        void Cleave()
        {
            if (SafeCast("Cleave"))
            {
                Logging.Write("Cleave.");
            }
        }

        /// <summary>
        /// http://www.wowhead.com/?spell=23881
        /// Instantly attack the target causing [AP * 50 / 100] damage.  In addition, the next 3 successful melee attacks will restore 1% of max health.
        /// </summary>
        void Bloodthirst()
        {
            if (GotTarget && MyTarget.Distance > 5)
            {
                return;
            }

            if (SpellManager.HasSpell("Bloodthirst"))
            {
                SafeCast("Bloodthirst");
                Logging.Write("Bloodthirst.");
            }
        }


        /// <summary>
        /// http://www.wowhead.com/?spell=12294
        /// A vicious strike that deals weapon damage plus 85 and wounds the target, reducing the effectiveness of any healing by 50% for 10 sec.
        /// </summary>
        void MortalStrike()
        {
            if (GotTarget && MyTarget.Distance > 5)
            {
                return;
            }

            if (SpellManager.HasSpell("Mortal Strike"))
            {
                SafeCast("Mortal Strike");
                Logging.Write("Mortal Strike.");
            }
        }

        /// <summary>
        /// http://www.wowhead.com/?spell=20230
        /// Instantly counterattack any _enemy that strikes you in melee for 12 sec.  
        /// Melee attacks made from behind cannot be counterattacked.  A maximum of 20 attacks will cause retaliation.
        /// </summary>
        void Retaliation()
        {
            if (SpellManager.HasSpell("Retaliation"))
            {
                BattleStance();
                SafeCast("Retaliation");
                Logging.Write("Retaliation.");
            }
        }

        /// <summary>
        /// http://www.wowhead.com/?spell=871
        /// Reduces all damage taken by 60% for 12 sec.
        /// </summary>
        void ShieldWall()
        {
            if (Me.Shapeshift != ShapeshiftForm.DefensiveStance)
            {
                DefensiveStance();
            }

            if (SpellManager.HasSpell("Shield Wall") && SafeCast("Shield Wall"))
            {
                Logging.Write("Shield Wall.");
            }
        }

        void Disarm()
        {
            if (MyTarget.CreatureType == WoWCreatureType.Humanoid || MyTarget.CreatureType == WoWCreatureType.Mechanical)
            {

                if (Me.Shapeshift != ShapeshiftForm.DefensiveStance)
                {
                    DefensiveStance();
                }

                if (SpellManager.HasSpell("Disarm") && SafeCast("Disarm"))
                {
                    Logging.Write("Disarm.");
                }
            }
        }

        /// <summary>
        /// http://www.wowhead.com/?spell=5246
        /// The warrior shouts, causing up to 5 enemies within 8 yards to cower in fear.  The targeted _enemy will be unable to move while cowering.  Lasts 8 sec.
        /// </summary>
        void IntimidatingShout(string msg)
        {
            if (SpellManager.HasSpell("Intimidating Shout"))
            {
                SafeCast("Intimidating Shout");
                Logging.Write(msg);
            }
        }

        /// <summary>
        /// http://www.wowhead.com/?spell=5308
        /// Attempt to finish off a wounded foe.  Only usable on enemies that have less than 20% health.
        /// </summary>
        void Execute()
        {
            if (Me.Shapeshift == ShapeshiftForm.DefensiveStance)
            {
                BattleStance();
                if (SafeCast("Execute"))
                {
                    BattleStance();
                    Logging.Write("Execute done.");
                }
                DefensiveStance();
            }

        }

        void ShieldBash()
        {
            if (GotTarget && MyTarget.Distance < 6 && SafeCast("Shield Bash"))
            {
                Logging.Write("Shield Bash.");
            }
        }

        void ShieldSlam()
        {
            if (GotTarget && MyTarget.Distance < 6 && SafeCast("Shield Slam"))
            {
                Logging.Write("Shield Slam.");
            }

        }

        void Devastate()
        {
            if (GotTarget && MyTarget.Distance < 6 && SafeCast("Devastate"))
            {
                Logging.Write("Devastate.");
            }

        }

        /// <summary>
        /// Sends a wave of force in front of the warrior, causing [0.75 * AP] damage (based on attack power) and stunning all enemy targets 
        /// within 10 yards in a frontal cone for 4 sec.
        /// 
        /// </summary>
        void Shockwave()
        {
            if (GotTarget && MyTarget.Distance < 6 && SafeCast("Shockwave"))
            {
                Logging.Write("Shockwave.");
            }
        }

        void ShieldBlock()
        {
            if (Me.Shapeshift != ShapeshiftForm.DefensiveStance)
            {
                DefensiveStance();
            }

            if (GotTarget && MyTarget.Distance < 6 && SafeCast("Shield Block"))
            {
                Logging.Write("Shield Block.");
            }

        }

        /// <summary>
        /// http://www.wowhead.com/?spell=1464
        /// Slams the opponent, causing weapon damage plus.
        /// </summary>
        void Slam()
        {
            if (GotTarget && MyTarget.Distance < 6 && SafeCast("Slam"))
            {
                Logging.Write("Slam.");
            }
        }

        /// <summary>
        /// http://www.wowhead.com/?spell=12975
        /// When activated, this ability temporarily grants you 30% of your maximum health for 20 sec.  
        /// After the effect expires, the health is lost.
        /// </summary>
        void LastStand()
        {
            if (SafeCast("Last Stand"))
            {
                Logging.Write("Last Stand.");
            }
        }

        /// <summary>
        /// http://www.wowhead.com/?spell=34428
        /// Instantly attack the target causing [AP * 45 / 100] damage.  Can only be used within 20 sec after you kill an enemy that yields experience or honor.  Damage is based on your attack power.
        /// </summary>
        void VictoryRush()
        {
            if (SpellManager.HasSpell("Victory Rush"))
            {
                var isUsable = Lua.LuaGetReturnValue("return IsUsableSpell('Victory Rush')", "hax.lua");

                if (isUsable != null)
                {
                    if (isUsable[0] == "1")
                    {
                        SafeCast("Victory Rush");
                        Logging.Write("Victory Rush.");
                    }
                }
            }
        }
        #endregion

        #region combat styles

        private void warriorStances()
        {
            if (SpellManager.HasSpell("Berserker Stance") && Me.Shapeshift != ShapeshiftForm.BerserkerStance)
            {
                BerserkerStance();
            }

        }

        private void ProtectionPVP()
        {
            if (!Me.GotTarget && Targeting.Instance.FirstUnit != null)
            {
                Targeting.Instance.FirstUnit.Target();
                Thread.Sleep(200);
            }
            else if (GotTarget && MyTarget.Dead)
            {
                Me.ClearTarget();
                Thread.Sleep(200);
            }
            else if ((Me.HealthPercent < 30 && MyTarget.HealthPercent > Me.HealthPercent && AddsList.Count == 1) || (Me.HealthPercent < 55 && AddsList.Count > 1))
            {
                LastStand();
            }
            else if ((Me.HealthPercent < 50 && MyTarget.HealthPercent > Me.HealthPercent && AddsList.Count == 1) || (Me.HealthPercent < 75 && AddsList.Count > 1))
            {
                if (SpellManager.Spells.ContainsKey("Lifeblood") && !SpellManager.Spells["Lifeblood"].Cooldown && SafeCast("Lifeblood"))
                {
                    Logging.Write("Lifeblood cast at {0}% health.", (int)Me.HealthPercent);
                }
                else
                {
                    UseHealthPotion();
                }
            }
            else if (Me.HealthPercent < 75 || AddsList.Count > 1)
            {
                if (SpellManager.Spells.ContainsKey("Concussion Blow") && !SpellManager.Spells["Concussion Blow"].Cooldown)
                {
                    ConcussionBlow();
                }
                else if (SpellManager.Spells.ContainsKey("Shield Block") && !SpellManager.Spells["Shield Block"].Cooldown)
                {
                    ShieldBlock();
                }
            }
            else if (MyTarget.IsCasting)
            {
                ShieldBash();
            }

            VictoryRush();

            if (Me.CurrentRage < 40)
            {
                if (SpellManager.Spells.ContainsKey("Berserker Rage") && !SpellManager.Spells["Berserker Rage"].Cooldown)
                {
                    BerserkerRage();
                }
            }

            DefensiveStance();
            Revenge();
            ShieldSlam();
            Rend();
            ThunderClap();
            Devastate();


            if (AddsList.Count > 1)
            {
                if (!MyTarget.Auras.ContainsKey("Demoralizing Shout"))
                {
                    DemoralizingShout();
                }
                Shockwave();
            }



        }

        private void ProtectionDPS()
        {
            if (!Me.GotTarget && Targeting.Instance.FirstUnit != null)
            {
                Targeting.Instance.FirstUnit.Target();
                Thread.Sleep(200);
            }
            else if (GotTarget && MyTarget.Dead)
            {
                Me.ClearTarget();
                Thread.Sleep(200);
            }

            // racials
            if (MyTarget.HealthPercent > 60)
            {
                if (ObjectManager.Me.Race == WoWRace.Troll && SafeCast("Berserking"))
                {
                    Logging.Write("Berserking.");
                }

                if (ObjectManager.Me.Race == WoWRace.Orc && SafeCast("Bloodrage"))
                {
                    Logging.Write("Bloodrage.");
                }
            }

            if (MyTarget.IsCasting && MyTarget.Distance < 10)
            {
                if (ObjectManager.Me.Race == WoWRace.Tauren && SafeCast("War Stomp"))
                {
                    Logging.Write("War Stomp.");
                }

                if (ObjectManager.Me.Race == WoWRace.BloodElf && SafeCast("Arcane Torrent"))
                {
                    Logging.Write("Arcane Torrent.");
                }
            }

            if (Me.HealthPercent < 50)
            {
                if (ObjectManager.Me.Race == WoWRace.Draenei && SafeCast("Gift of the Naaru"))
                {
                    Logging.Write("Gift of the Naaru.");
                }

                if (ObjectManager.Me.Race == WoWRace.Dwarf && SafeCast("Stoneform"))
                {
                    Logging.Write("Stoneform.");
                }
            }

            if (Me.Auras.ContainsKey("Fear"))
            {
                if (ObjectManager.Me.Race == WoWRace.Undead && SafeCast("Will of the Forsaken"))
                {
                    Logging.Write("Will of the Forsaken.");
                }

                if (ObjectManager.Me.Race == WoWRace.Human && SafeCast("Every Man for Himself"))
                {
                    Logging.Write("Every Man for Himself.");
                }
            }

            if (Me.Auras.ContainsKey("Hamstring"))
            {
                if (ObjectManager.Me.Race == WoWRace.Human && SafeCast("Every Man for Himself"))
                {
                    Logging.Write("Every Man for Himself.");
                }

                if (ObjectManager.Me.Race == WoWRace.Gnome && SafeCast("Escape Artist"))
                {
                    Logging.Write("Escape Artist.");
                }
            }

            // last stand
            if ((SpellManager.Spells.ContainsKey("Last Stand") && !SpellManager.Spells["Last Stand"].Cooldown &&
                (Me.HealthPercent < 30 && MyTarget.HealthPercent > Me.HealthPercent && AddsList.Count == 1) ||
                MyTarget.IsPlayer ||
                (Me.HealthPercent < 55 && AddsList.Count > 1)))
            {
                LastStand();
                UseHealthPotion();
            }

            // defence
            if ((MyTarget.IsPlayer || Me.HealthPercent < 60 || AddsList.Count > 1))
            {
                if (SpellManager.Spells.ContainsKey("Lifeblood") && !SpellManager.Spells["Lifeblood"].Cooldown && SafeCast("Lifeblood"))
                {
                    Logging.Write("Lifeblood cast at {0}% health.", (int)Me.HealthPercent);
                }
                else
                {
                    UseHealthPotion();
                }

                if (!MyTarget.Auras.ContainsKey("Concussion Blow") && !MyTarget.Auras.ContainsKey("Shockwave") && !MyTarget.Auras.ContainsKey("Intimidating Shout"))
                {

                    if (SpellManager.Spells.ContainsKey("Shockwave") && !SpellManager.Spells["Shockwave"].Cooldown)
                    {
                        Shockwave();
                    }
                    else if (SpellManager.Spells.ContainsKey("Concussion Blow") && !SpellManager.Spells["Concussion Blow"].Cooldown)
                    {
                        ConcussionBlow();
                    }
                    else if (Me.HealthPercent < 50 && SpellManager.HasSpell("Intimidating Shout") && !SpellManager.Spells["Intimidating Shout"].Cooldown)
                    {
                        IntimidatingShout("Intimidating Shout for safety");
                    }
                    else if (SpellManager.Spells.ContainsKey("Shield Wall") && !SpellManager.Spells["Shield Wall"].Cooldown)
                    {
                        ShieldWall();
                    }
                    else if (!Me.Auras.ContainsKey("Shield Wall") && SpellManager.Spells.ContainsKey("Shield Block") && !SpellManager.Spells["Shield Block"].Cooldown)
                    {
                        ShieldBlock();
                    }
                }

            }

            // casters
            if (MyTarget.IsCasting)
            {
                if (SpellManager.Spells.ContainsKey("Spell Reflection") && SafeCast("Spell Reflection"))
                {
                    Logging.Write("Spell Reflection.");
                }
                else
                {
                    ShieldBash();
                }
            }

            // runners
            if ((RunnerList.Contains(MyTarget.Guid) && MyTarget.HealthPercent < 30) || MyTarget.Fleeing)
            {
                if (!RunnerList.Contains(MyTarget.Entry))
                {
                    Logging.Write("Adding {0} to the list of running mobs.", MyTarget.Name);
                    RunnerList.Add(MyTarget.Entry);
                }

                if (!MyTarget.Auras.ContainsKey("Concussion Blow") && !MyTarget.Auras.ContainsKey("Shockwave") && !MyTarget.Auras.ContainsKey("Intimidating Shout"))
                {
                    if (SpellManager.Spells.ContainsKey("Shield Block") && !SpellManager.Spells["Shield Block"].Cooldown)
                    {
                        ShieldBlock();
                    }
                    else if (SpellManager.Spells.ContainsKey("Shockwave") && !SpellManager.Spells["Shockwave"].Cooldown)
                    {
                        Shockwave();
                    }
                    else if (SpellManager.Spells.ContainsKey("Concussion Blow") && !SpellManager.Spells["Concussion Blow"].Cooldown)
                    {
                        ConcussionBlow();
                    }
                    else if (SpellManager.HasSpell("Intimidating Shout") && !SpellManager.Spells["Intimidating Shout"].Cooldown)
                    {
                        IntimidatingShout("Intimidating Shout on runner.");
                    }
                }
            }

            // dps
            VictoryRush();

            if (Me.CurrentRage < 40)
            {
                if (SpellManager.Spells.ContainsKey("Berserker Rage") && !SpellManager.Spells["Berserker Rage"].Cooldown)
                {
                    BerserkerRage();
                }
            }

            DefensiveStance();

            if (MyTarget.IsPlayer || AddsList.Count > 1)
            {
                DemoralizingShout();
                Shockwave();
            }

            ShieldSlam();

            Revenge();

            ThunderClap();

            if (SpellManager.Spells.ContainsKey("Devastate"))
            {
                Devastate();
                Devastate();
            }
            else
            {
                Rend();
            }

            if (Me.CurrentRage > 35)
            {
                if (AddsList.Count > 1)
                {
                    Cleave();
                }
                else
                {
                    HeroicStrike();
                }
            }
        }

        private void BerserkerCombat()
        {
            BerserkerStance();
            if (Me.CurrentRage < 40)
            {
                if (SpellManager.Spells.ContainsKey("Berserker Rage") && !SpellManager.Spells["Berserker Rage"].Cooldown)
                {
                    BerserkerRage();
                }
            }

            if (CombatChecks)
            {
                Bloodthirst();
            }

            if (CombatChecks)
            {
                MortalStrike();
            }

            if (CombatChecks)
            {
                HeroicStrike();
            }

            BattleShout();


        }

        private static bool CombatChecks
        {
            get
            {
                if (!GotTarget || ObjectManager.Me.Dead || !ObjectManager.Me.Combat)
                {
                    return false;
                }
                else if (!ObjectManager.Me.IsAutoAttacking)
                {
                    Lua.DoString("StartAttack()");
                }

                if (MyTarget.Distance > 50)
                {
                    if (MyTarget.IsPlayer)
                    {
                        Logging.Write("Out of range: Level " + MyTarget.Level + " " + MyTarget.Race + " " +
                                      Math.Round(MyTarget.Distance) + " yards away.");
                    }
                    else
                    {
                        Logging.Write("Out of range: Level " + MyTarget.Name + " is " +
                                      Math.Round(MyTarget.Distance) +
                                      " yards away.");
                    }


                    if (ObjectManager.Me.Combat)
                    {
                        Blacklist.Add(MyTarget.Guid, TimeSpan.FromSeconds(10));
                        Targeting.Instance.Clear();
                    }
                    ObjectManager.Me.ClearTarget();
                    return false;
                }

                const double meleeRange = 5;

                if (!ObjectManager.Me.IsCasting && MyTarget.Distance > meleeRange)
                {
                    int a = 0;

                    while (a < 50 && ObjectManager.Me.IsAlive && GotTarget && MyTarget.Distance > meleeRange)
                    {
                        WoWMovement.Face();
                        Navigator.MoveTo(WoWMovement.CalculatePointFrom(MyTarget.Location, 2.5f));
                        Thread.Sleep(250);
                        ++a;
                    }
                    WoWMovement.MoveStop();
                }

                WoWMovement.Face();

                return true;
            }
        }


        #endregion

        #region racials

        private void Warstomp()
        {
            if (SafeCast("War Stomp"))
            {
                Logging.Write("Warstomp: BOOM!");
            }
        }
        #endregion

        private bool HasBuffProcced(string cSearchBuffName, int minStackCount)
        {
            int stackCount = 0;

            List<string> myBuffs = Lua.LuaGetReturnValue("return UnitBuff(\"player\",\"" + cSearchBuffName + "\")", "hawker.lua");

            if (Equals(null, myBuffs))
                return false;

            string buffName = myBuffs[0];

            if (minStackCount > 0)
            {
                stackCount = Convert.ToInt32(myBuffs[3]);
            }

            if (buffName == cSearchBuffName ||
                stackCount >= minStackCount)
            {
                return true;
            }
            else
            {
                return false;
            }
        }




    }
}