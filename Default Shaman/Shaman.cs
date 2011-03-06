/*
 * NOTICE:    DO NOT POST ANY MODIFIED VERSIONS OF THIS TO THE FORUMS.  ALL CHANGES
 *            TO THIS CC ARE TO BE REVIEWED AND INCORPORATED BY THE AUTHOR.
 * 
 * ShamWOW Shaman CC - Version: 4.2.04b
 * 
 * Author:  Bobby53
 * 
 * General approach to leveling and spell rotations taken from:
 * 
 *          http://wow-pro.com/class_guides/shaman_leveling_talents_and_tips_180
 *          
 * For use of totems during leveling, I am using strategies outlined in :
 * 
 *          http://www.yawb.info/2009/08/25/learning-about-totems-tips-to-aid-your-growing-shamanism/
 * 
 * Change History moved to CHANGES.TXT file
 *
 */

#define BUNDLED_WITH_HONORBUDDY
#define HIDE_PLAYER_NAMES
#define USE_CHAIN_HEAL_IN_RAF
// #define DEBUG
// #define LIST_HEAL_TARGETS
// #define DISABLE_TARGETING_FOR_INSTANCEBUDDY
// #define USE_SHIELD_SPAM_HACK

/*************************************************************************
 *   !!!!! DO NOT CHANGE ANYTHING IN THIS FILE !!!!!
 *   
 *   User customization is only supported through changing the values
 *   in the SETTINGS.SETTINGS xml file in your Custom Classes folder tree
*************************************************************************/
#pragma warning disable 642

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.Logic.Profiles;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;


namespace ShamWOW
{
    internal class ShamWOW : CombatRoutine
    {
        public static string Version { get { return "4.2.04b"; } }
        public override WoWClass Class { get { return WoWClass.Shaman; } }
#if    BUNDLED_WITH_HONORBUDDY
        public override string Name { get { return "TEST ShamWOW v" + Version + "-BUNDLED by Bobby53"; } }
#else
        public override string Name { get { return "TEST ShamWOW v" + Version + " by Bobby53"; } }
#endif

        #region Global Variables

        public static ConfigValues _cfg;
        public static ShamWOW _local;

        public static CC_PVP _pvp = new CC_PVP();                      // pvp support class for CC's
        private readonly CC_TalentGroup _primaryTalents = new CC_TalentGroup();
        private readonly CC_TalentGroup _secondaryTalents = new CC_TalentGroup();

        public static readonly string ConfigPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), Path.Combine("CustomClasses", "Config"));
        public static readonly string CCPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), Path.Combine("CustomClasses", "ShamWOW"));

        public static string ConfigFilename;

        private uint _countDrinks;
        private uint _countFood;
        private readonly int _tickStart = System.Environment.TickCount;

        private static BotEvents.OnBotStartDelegate btStrt;
        private static BotEvents.OnBotStopDelegate btStp;

        private static double _maxDistForRangeAttack
        {
            get
            {
                double dist = 25.0;

                // use Lightning Bolt range as ranged distance, 
                //  ... unless Nature Immune, figure out how close we need to be
                try
                {
                    dist = SpellManager.Spells["Lightning Bolt"].MaxRange;
                    if (_pvp.IsBattleground() || IsRAF())
                    {
                        dist = Math.Min(dist, SpellManager.Spells["Earth Shock"].MaxRange);
                    }
                }
                catch (ThreadAbortException) { throw; }
                catch
                { ;/* do nothing, default initialized above */ }

                return dist;
            }
        }

        private double _distForRangedPull
        {
            get
            {
                double dist = _maxDistForRangeAttack;
                if (_me.GotTarget && IsImmunneToNature(_me.CurrentTarget))
                {
                    /* if (Safe_KnownSpell("Lava Lash"))
                        dist = SpellManager.Spells["Lava Lash"].MaxRange;
                    else */
                    if (Safe_KnownSpell("Flame Shock"))    // go into melee then
                        dist = SpellManager.Spells["Flame Shock"].MaxRange;
                    else
                        dist = _distForMeleePull + 4.0;

                    Slog("Nature immune:  making pull distance for target {0:F1} yds", dist - 4);
                }

                return dist - 4.0;
            }
        }

        private const double _maxDistForMeleeAttack = 5.0;
        private double _distForMeleePull
        {
            get
            {
                if (_pvp.IsBattleground())
                {
                    if (_me.GotTarget && _me.CurrentTarget.Fleeing)
                    {
                        Dlog("using fleeing target range of 0.0");
                        return 0.0;
                    }

                    return 2.0;
                }

                return 3.0;
            }
        }

        private volatile LocalPlayer _me = ObjectManager.Me;

        private readonly Stopwatch _pullTimer = new Stopwatch();             // kill timer from when we target for pull
        private ulong _pullTargGuid;
        // private bool _pullTargHasBeenInMelee;       // flag indicating that we got in melee range of mob
        private int _pullAttackCount;
        // private WoWPoint _pullStart;

        private uint _killCount;
        private uint _killCountBase;
        private uint _deathCount;
        private uint _deathCountBase;

        private bool _waterShieldUsedLast;       // toggle remember which shield used last shield twist

        private readonly Stopwatch _TotemCheckTimer = new Stopwatch();	// timer to prevent checking totems each time through Combat  (uses LUA funcs)
        private static bool _WereTotemsSet;            // flag indicating whether any totems were cast
        private bool _RecallTotems;
        private WoWPoint _ptTotems;

        // private bool _lastCheckWasInCombat = true;  
        private bool _castGhostWolfForm;            // 
        private bool _needMainHandEnchant;
        private bool _needOffHandEnchant;
        private bool _castCleanse;
        private WoWPlayer _rezTarget;

        private bool _BigScaryGuyHittingMe;      // mob sufficiently higher than us (3+ lvls)
        private bool _OpposingPlayerGanking;    // player from other faction attacking me
        private static int _countMeleeEnemy;               // # of melee mobs in combat with me
        private static int _countTenYardEnemy;               // # of mobs in combat with me within 10 yards
        private static int _countRangedEnemy;              // # of ranged mobs in combat with me
        private static int _countAoe8Enemy;          // # of mobs within 8 yards of current target
        private static int _countAoe12Enemy;          // # of mobs within 12 yards of current target
        private int _countMobs;                     // # of faction mobs as spec'd in current profile
        //	private double _distClosestEnemy ;           // distance to closest hostile or faction mob
        //	private WoWPoint _ptClosestEnemy;

        private bool _needClearWeaponEnchants = true;
        private bool _needTotemBarSetup = true;


        private bool _lastCheckInBattleground;    // 
        private bool _lastCheckInGroup;
        private ShamanType _lastCheckSpec = ShamanType.Unknown;

#if EQUIP_SUPPORTED
        private string EquipDefault;
        private string EquipPVP;
        private string EquipRAF;
#endif
        // public static WoWPlayer _followTarget;

        private bool _pluginMrAutoFight;

        private bool _foundMobsThatFear;

        private bool DidWeSwitchModes()
        {
            bool inGroup = _me.IsInParty || _me.IsInRaid;
            if (_lastCheckInBattleground != _pvp.IsBattleground() || _lastCheckInGroup != inGroup || _lastCheckSpec != _typeShaman)
            {
                _lastCheckInBattleground = _pvp.IsBattleground();
                _lastCheckInGroup = inGroup;
                _lastCheckSpec = _typeShaman;
                return true;
            }

            return false;
        }

        private readonly Stopwatch _potionTimer = new Stopwatch();


        // private int _pointsImprovedStormstrike;
        private bool _hasTalentFulmination;
        private bool _hasTalentImprovedCleanseSpirit;
        private bool _hasTalentAncestralSwiftness;
        private bool _hasTalentImprovedLavaLash;
        private bool _hasTalentMaelstromWeapon;
        private bool _hasGlyphOfStoneClaw;
        private bool _hasGlyphOfShamanisticRage;

        public enum ShamanType
        {
            Unknown,
            Elemental,
            Enhance,
            Resto
        };

        public static ShamanType _typeShaman = ShamanType.Unknown;

        private static int countEnemy
        {
            get
            {
                return _countMeleeEnemy + _countRangedEnemy;
            }
        }

        private bool IsFightStressful()
        {
            return countEnemy >= _cfg.PVE_StressfulMobCount || _BigScaryGuyHittingMe || _OpposingPlayerGanking;
        }

        #endregion

        #region Private Members

#if CTOR_NO_LONGER_NEEDED      

        /*
         * Ctor
         * 
         * initialize and post load messages/checks for user
         */
        public ShamWOW()
        {
            if (_me.Class != WoWClass.Shaman)
            {
                return;
            }
        }

        /*
         * Dtor
         */
        ~ShamWOW()
        {
            if (_me.Class != WoWClass.Shaman)
            {
                return;
            }

            Dlog("UNLOAD:  " + Name);
        }

#endif

        private static bool firstInitialize = true;

        public override void Initialize()
        {
            if (firstInitialize)
            {
                firstInitialize = false;
                InitializeOnce();
            }

            _primaryTalents.Load(1);
            _secondaryTalents.Load(2);

            CC_TalentGroup activeGrp;
            CC_TalentGroup inactiveGrp;

            if (_primaryTalents.IsActiveGroup())
            {
                activeGrp = _primaryTalents;
                inactiveGrp = _secondaryTalents;
            }
            else
            {
                inactiveGrp = _primaryTalents;
                activeGrp = _secondaryTalents;
            }

            if (activeGrp.Spec() == 1)
                _typeShaman = ShamanType.Elemental;
            else if (activeGrp.Spec() == 2)
                _typeShaman = ShamanType.Enhance;
            else if (activeGrp.Spec() == 3)
                _typeShaman = ShamanType.Resto;
            else if (_cfg.MeleeCombatBeforeLevel10)
            {
                _typeShaman = ShamanType.Enhance;
                Slog("Low-level Shaman played as Enhancement due to config setting.");
            }
            else
            {
                _typeShaman = ShamanType.Elemental;
                Slog("Low-level Shaman being played as Elemental.  See the [Melee Combat Beforee Level 10] configuration setting to force melee combat.");
            }

            string sSpecType = activeGrp._tabName[activeGrp.Spec()];
            Slog("Your Level " + _me.Level + " " + _me.Race + " " + sSpecType + " Shaman Build is:  ");
            Slog(activeGrp._tabName[1].Substring(0, 5) + "/" + activeGrp._tabName[2].Substring(0, 5) + "/" + activeGrp._tabName[3].Substring(0, 5)
            + "   " + activeGrp._tabPoints[1] + "/" + activeGrp._tabPoints[2] + "/" + activeGrp._tabPoints[3]);

            if (activeGrp.unspentPoints > 0)
            {
                Wlog("WARNING: {0} unspent Talent Points. Use a talent plug-in or spec manually", activeGrp.unspentPoints);
            }

            if (StyxSettings.Instance.EnabledPlugins == null)
                _pluginMrAutoFight = false;
            else
                _pluginMrAutoFight = StyxSettings.Instance.EnabledPlugins.Contains("Mr.AutoFight");

            if (_cfg.DisableMovement && _pluginMrAutoFight)
                Slog("CC movevement is disabled and Mr.AutoFight is active");
            if (_cfg.DisableMovement && !_pluginMrAutoFight)
                Wlog("CC movevement is disabled but Mr.AutoFight not active");
            if (!_cfg.DisableMovement && _pluginMrAutoFight)
                Wlog("CC movevement is enabled but Mr.AutoFight is active");

            _deathCountBase = InfoPanel.Deaths;

            Slog("Max Pull Ranged:   {0}", _maxDistForRangeAttack);
            Slog("HB Pull Distance:  {0}", Targeting.PullDistance);

            _hasTalentFulmination = 0 < activeGrp.GetTalentInfo(1, 13);
            _hasTalentImprovedCleanseSpirit = Safe_KnownSpell("Cleanse Spirit") && 0 < activeGrp.GetTalentInfo(3, 12);
            _hasTalentAncestralSwiftness = Safe_KnownSpell("Ghost wolf") && (2 == activeGrp.GetTalentInfo(2, 6));
            _hasTalentMaelstromWeapon = (1 <= activeGrp.GetTalentInfo(2, 17));
            _hasTalentImprovedLavaLash = Safe_KnownSpell("Lava Lash") && (1 <= activeGrp.GetTalentInfo(2, 18));
            _hasGlyphOfStoneClaw = Safe_KnownSpell("Stoneclaw Totem") && activeGrp._glyphs.ContainsKey(45778);
            _hasGlyphOfShamanisticRage = Safe_KnownSpell("Shamanistic Rage") && activeGrp._glyphs.ContainsKey(45776);

            if (_hasTalentFulmination)
                Slog("Fulmination: will wait for 9 stacks of Lightning Shield before using Earth Shock");
            if (Safe_KnownSpell("Cleanse Spirit"))
                Slog("Cleanse Spirit: {0}", _hasTalentImprovedCleanseSpirit ? "can remove Curses and Magic" : "can only remove Curses");
            if (Safe_KnownSpell("Ghost wolf"))
                Slog("Ghost Wolf: {0}", _hasTalentAncestralSwiftness ? "can cast Ghost Wolf on the run" : "must stop to cast Ghost Wolf");
            if (_hasTalentMaelstromWeapon)
                Slog("Maelstrom Weapon: will cast Lightning Bolt or Chain Lightning at 5 stacks");
            if (Safe_KnownSpell("Lava Lash"))
                Slog("Lava Lash: {0}", _hasTalentImprovedLavaLash ? "will wait for 5 stacks of Searing Flames" : "when off cooldown");

            if (Safe_KnownSpell("Stoneclaw Totem"))
                Slog("Stoneclaw Totem: {0}", _hasGlyphOfStoneClaw ? "will use as Shaman Bubble" : "missing glyph, no Shaman Bubble available");
            if (Safe_KnownSpell("Shamanistic Rage"))
                Slog("Shamanistic Rage: {0}", _hasGlyphOfShamanisticRage ? "will use as Magic Cleanse" : "missing glyph, no Magic Cleanse available");

            DidWeSwitchModes();                 // set the mode change monitors
            _needClearWeaponEnchants = true;
            _needTotemBarSetup = false;
            TotemSetupBar();

            // InfoPanel.Reset();
            _killCountBase = InfoPanel.MobsKilled;
            _deathCountBase = InfoPanel.Deaths;
        }

        private void InitializeOnce()
        {
            //==============================================================================================
            //  Now do ONE TIME Initialization (needs to occur after we know what spec we are)
            //==============================================================================================
            Slog("LOADED:  " + Name);
            _local = this;

            // load config file (create if doesn't exist)
            string realmName = Lua.GetReturnVal<string>("return GetRealmName()", 0);
            ConfigFilename = Path.Combine(ConfigPath, "ShamWOW-" + realmName + "-" + _me.Name + ".config");

            if (!Directory.Exists(ConfigPath))
            {
                try
                {
                    Directory.CreateDirectory(ConfigPath);
                }
                catch (ThreadAbortException) { throw; }
                catch
                {
                    Wlog("Folder could not be created: '{0}'", ConfigPath);
                    Wlog("Create the folder manually if needed and restart HB");
                    return;
                }
            }

            _cfg = new ConfigValues();
            if (File.Exists(ConfigFilename))
            {
                _cfg.FileLoad(ConfigFilename);
                Slog("Character specific config file loaded");
            }
            else
            {
                _cfg.Save(ConfigFilename);
                Slog("Creating a character specific config file ");
            }

            _cfg.DebugDump();

            const float minPathPrecision = 2.5f;
            float prevPrec = Navigator.PathPrecision;
            if (prevPrec != minPathPrecision)
            {
                Navigator.PathPrecision = minPathPrecision;
                Slog("Changed Navigator precision from {0} to {1}", prevPrec, minPathPrecision);
            }

            btStrt = new BotEvents.OnBotStartDelegate(startBot);
            BotEvents.OnBotStart += btStrt;
            btStp = new BotEvents.OnBotStopDelegate(stopBot);
            BotEvents.OnBotStop += btStp;

            //////////////////////////////////// Following would go in dtor ///////////////////
            // BotEvents.OnBotStart -= btStrt;
            // BotEvents.OnBotStopped -= btStp;

            SequenceManager.AddSequenceExecutorOverride(Sequence.ReleaseSpirit, SequenceOverride_ReleaseSpirit);
            SequenceManager.AddSequenceExecutorOverride(Sequence.MountUp, SequenceOverride_MountUp);
            // SequenceManager.AddSequenceExecutorOverride(Sequence.Pull, SequenceOverride_Pull);

            Lua.Events.AttachEvent("ACTIVE_TALENT_GROUP_CHANGED", HandleTalentGroupChange); //goes to init()
            Lua.Events.AttachEvent("PLAYER_TALENT_UPDATE", HandlePlayerTalentUpdate); //goes to init()
            Lua.Events.AttachEvent("UNIT_SPELLCAST_INTERRUPTIBLE", HandleCastInterruptible);

            //==============================================================================================
            //  end of first time initialize
            //==============================================================================================            
        }

        public static string SafeLogException(string msg)
        {
            msg = msg.Replace("{", "(");
            msg = msg.Replace("}", ")");
            return msg;
        }

        /* Log()
         * 
         * write 'msg' to log window.  message is suppressed if it is identical
         * to prior message.  Intent is to prevent log window spam
         */
        public static void Log(string msg, params object[] args)
        {
            try
            {
                Logging.Write(msg, args);
                _Slogspam = msg;
            }
            catch (ThreadAbortException) { throw; }
            catch (Exception e)
            {
                Log(Color.Red, "An Exception occured. Check debug log for details.");
                Logging.WriteDebug(">>> EXCEPTION: occurred logging msg: \n\t\"" + SafeLogException(msg) + "\"");
                Logging.WriteException(e);
            }
        }


        public static void Log(Color clr, string msg, params object[] args)
        {
            try
            {
                Logging.Write(clr, msg, args);
                _Slogspam = msg;
            }
            catch (ThreadAbortException) { throw; }
            catch (Exception e)
            {
                Log(Color.Red, "An Exception occured. Check debug log for details.");
                Logging.WriteDebug(">>> EXCEPTION: occurred logging msg: \n\t\"" + SafeLogException(msg) + "\"");
                Logging.WriteException(e);
            }
        }


        /* Slog()
         * 
         * write 'msg' to log window.  message is suppressed if it is identical
         * to prior message.  Intent is to prevent log window spam
         */
        private static string _Slogspam;

        public static void Slog(Color clr, string msg, params object[] args)
        {
            try
            {
                msg = String.Format(msg, args);
                if (msg == _Slogspam)
                    return;

                Log(clr, msg);
            }
            catch (ThreadAbortException) { throw; }
            catch (Exception e)
            {
                Log(Color.Red, "An Exception occured. Check debug log for details.");
                Logging.WriteDebug(">>> EXCEPTION: occurred logging msg: \n\t\"" + SafeLogException(msg) + "\"");
                Logging.WriteException(e);
            }
        }

        public static void Slog(string msg, params object[] args)
        {
            Slog(Color.Black, msg, args);
        }


        public static void Elog(string msg, params object[] args)
        {
            Slog(Color.Red, msg, args);
        }

        /* Wlog()
         * 
         * write 'msg' to log window, but only if it hasn't been written already.
         */
        private static readonly List<string> _warnList = new List<string>();	// tracks warning messages issued by Wlog()

        public static void Wlog(string msg, params object[] args)
        {
            msg = String.Format(msg, args);
            String found = _warnList.Find(s => 0 == s.CompareTo(msg));
            if (found == null)
            {
                _warnList.Add(msg);
                Log(Color.Red, msg);
            }
        }

        /* Dlog()
         * 
         * Write Debug log message to log window.  message is suppressed if it
         * is identical to prior log message or verbose mode is turned off.  These
         * messages are trace type in nature to follow in more detail what has occurred
         * in the code.
         * 
         * NOTE:  I am intentionally putting debug message in the Log().  At this point,
         * it helps more having all data be time sequenced in the same window.  This will
         * near the close of development move to the Debug window instead
         */
        static string _Dlogspam;

        public static void Dlog(string msg, params object[] args)
        {
            try
            {
                msg = String.Format(msg, args);
                if (msg == _Dlogspam) // || _cfg.Debug == false)
                    return;

                if (_cfg.Debug)
                    Logging.WriteDebug("%   " + msg);
            }
            catch (ThreadAbortException) { throw; }
            catch (Exception e)
            {
                Log(Color.Red, "An Exception occured. Check debug log for details.");
                Logging.WriteDebug(">>> EXCEPTION: occurred logging msg: \n\t\"" + SafeLogException(msg) + "\"");
                Logging.WriteException(e);
            }

            _Dlogspam = msg;
        }

        private void ReportBodyCount()
        {
            bool rptKill = (_killCountBase + _killCount) < InfoPanel.MobsKilled;
            bool rptDeath = (_deathCountBase + _deathCount) < InfoPanel.Deaths;

            if (rptDeath)
            {
                _deathCount = InfoPanel.Deaths - _deathCountBase;
                Slog("! Death #{0} at {1:F1} per hour fighting at x={2},y={3},z={4}",
                InfoPanel.Deaths,
                InfoPanel.DeathsPerHour,
                _me.Location.X,
                _me.Location.Y,
                _me.Location.Z
                );
            }

            if (rptKill)
            {
                _killCount = InfoPanel.MobsKilled - _killCountBase;
                Slog("! Kill #{0} at {1:F0} xp per hour fighting at x={2},y={3},z={4}",
                InfoPanel.MobsKilled,
                InfoPanel.XPPerHour,
                _me.Location.X,
                _me.Location.Y,
                _me.Location.Z
                );
            }
        }

        //private delegate void startBot();
        private void startBot(EventArgs args)
        {
            Slog(Color.DarkGreen, ">>> ### STARTING {0} ### <<<", Name);
            Initialize();
        }

        private void stopBot(EventArgs args)
        {
            Slog(Color.DarkGreen, ">>> ### STOPPING {0} ### <<<", Name);
        }

        private static void SequenceOverride_ReleaseSpirit()
        {
            _countMeleeEnemy = 0;               // # of melee mobs in combat with me
            _countTenYardEnemy = 0;             // # of mobs withing 10 yards in combat with me
            _countRangedEnemy = 0;              // # of ranged mobs in combat with me
            _WereTotemsSet = false;

            // next phase
            // ... if selfrez available, inspect surrounding area if clear within
            // ... a configurable time delay, then selfrez, otherwise repop
            List<string> hasSoulstone = Lua.GetReturnValues("return HasSoulstone()", "hawker.lua");
            if (hasSoulstone != null && hasSoulstone.Count > 0 && hasSoulstone[0] != "" && hasSoulstone[0].ToLower() != "nil")
            {
                /*
                Lua.DoString("UseSoulstone()");
                int tickCount = Environment.TickCount;
                while (!ObjectManager.Me.IsAlive && Environment.TickCount - tickCount < 7500)
                    Thread.Sleep(100);
                if (ObjectManager.Me.IsAlive)
                    return;
                 */

                Slog("Skipping use of '{0}'", hasSoulstone[0]);
            }

            // SequenceManager.CallDefaultSequenceExecutor(Sequence.ReleaseSpirit);
            Lua.DoString("RepopMe()", "hawker.lua");

            int tickCount = Environment.TickCount;
            while (!ObjectManager.Me.IsAlive && Environment.TickCount - tickCount < 10000)
                Thread.Sleep(100);

            if (!ObjectManager.Me.IsAlive)
            {
                SequenceManager.CallDefaultSequenceExecutor(Sequence.ReleaseSpirit);
            }
        }

        private static void SequenceOverride_MountUp()
        {
            // add a check here for ....
            //  ... if we are healing and heal targets nearby, suppress
            //  ... if we have attack targets nearby, suppress
            if (_typeShaman == ShamanType.Resto
                || (Battlegrounds.IsInsideBattleground && _cfg.PVP_CombatStyle != ConfigValues.PvpCombatStyle.CombatOnly)
                || (IsRAF() && _cfg.RAF_CombatStyle != ConfigValues.RafCombatStyle.CombatOnly))
            {
                double healPct = _cfg.PVP_GroupNeedHeal;
                WoWPlayer p = CC_PVP.chooseHealTarget(healPct, SpellRange.Check);
                if (p != null && !p.IsMe)
                {
                    Dlog("MountUp:  suppressed for heal - {0}[{1}] at {2:F1} yds needs heal", p.Class, p.Level, p.Distance);
                    return;
                }
            }

            if (IsRAF() && !RaFHelper.Leader.Mounted && RaFHelper.Leader.Distance < 60)
            {
                Dlog("MountUp:  suppressed for HB RaF Follow bug - RaFLeader not mounted and only {0:F1} yds away", RaFHelper.Leader.Location);
                return;
            }

            if (_WereTotemsSet)
            {
                Dlog("MountUp:  HB wants to mount and totems exist... recalling totems");
                _local.RecallTotemsForMana();
            }

            Dlog("MountUp:  trying to mount");
            // if ( ObjectManager.Me.IsMoving )
            //     Safe_StopMoving();
            SequenceManager.CallDefaultSequenceExecutor(Sequence.MountUp);
        }



        private static void SequenceOverride_Pull()
        {
            _local.Pull();
            // SequenceManager.CallDefaultSequenceExecutor(Sequence.Pull);
        }


        private void HandleTalentGroupChange(object sender, LuaEventArgs args) // to anywhere
        {
            Log("^EVENT:  Active Talent Group Changed : initializing...");
            Initialize();
        }

        private void HandlePlayerTalentUpdate(object sender, LuaEventArgs args) // to anywhere
        {
            Log("^EVENT:  Player Level/Talent Update : initializing...");
            Initialize();
        }

        private void HandleCastInterruptible(object sender, LuaEventArgs args)
        {
            Log(Color.Orange, "^EVENT:  UNIT_SPELLCAST_INTERRUPTIBLE -- {0}", args.Args[0]);
        }

        #region SAFE_ Functions

        private static string Right(string s, int c)
        {
            return s.Substring(c > s.Length ? 0 : s.Length - c);
        }
        /*
         * Safe_ Functions.  These were created to handle unexpected errors and
         * situations occurring in HonorBuddy.  try/catch handling is provided
         * where an exception is thrown by HB that shouldn't be. multiple
         * attempts at something (like dismounting) are done until the desired
         * state (!_me.Mounted) is achieved
         */
        private static string Safe_UnitName(WoWUnit unit)
        {
            if (unit == null)
                return "(null)";

#if HIDE_PLAYER_NAMES
            if (unit.IsMe)
                return "-me-";
            else if (unit.IsPlayer) // && Safe_IsFriendly(unit)) // !unit.IsHostile ) // unit.IsFriendly)
                return unit.Class.ToString() + "." + Right(String.Format("{0:X3}", unit.Guid), 4);
#endif
            return unit.Name;
        }

        // replacement for WoWUnit.IsFriendly
        // to handle bug in HB 1.9.2.5 where .IsFriendly throws exception casting WoWUnit -> WoWPlayer
        private static bool Safe_IsFriendly(WoWUnit unit)
        {
            if (!unit.IsPlayer)
                return unit.IsFriendly;

            WoWPlayer p = unit.ToPlayer();
            return p.IsHorde == ObjectManager.Me.IsHorde;
        }

        // replacement for WoWUnit.IsNeutral
        // to handle bug in HB 1.9.2.5 where .IsHostile throws exception casting WoWUnit -> WoWPlayer
        private static bool Safe_IsNeutral(WoWUnit unit)
        {
            if (!unit.IsPlayer)
                return unit.IsNeutral;

            return false;
        }

        // replacement for WoWUnit.IsHostile
        // to handle bug in HB 1.9.2.5 where .IsHostile throws exception casting WoWUnit -> WoWPlayer
        private static bool Safe_IsHostile(WoWUnit unit)
        {
            if (!unit.IsPlayer)
                return unit.IsHostile;

            WoWPlayer p = unit.ToPlayer();
            return p.IsHorde != ObjectManager.Me.IsHorde;
        }

        private static bool Safe_IsProfileMob(WoWUnit unit)
        {
            if (unit == null || unit.Faction == null || ProfileManager.CurrentProfile == null || ProfileManager.CurrentProfile.Factions == null)
                return false;
            return ProfileManager.CurrentProfile.Factions.Contains(unit.Faction.Id);
        }

        // replacement for WoWObject.IsValid
        private static bool Safe_IsValid(WoWObject o)
        {
            return o != null;
            // DEBUG VERSION:  return o != null && ObjectManager.ObjectList.Contains(o);
        }

        // replacement for SpellManager.Spells.ContainsKey() which had a bug previously with Shaman spells
        private static bool Safe_KnownSpell(string sSpellName)
        {
            bool bKnown = false;

            try
            {
                if (sSpellName == "")
                    return false;

                // bKnown = SpellManager.Spells.ContainsKey(sSpellName);
                bKnown = SpellManager.HasSpell(sSpellName);
            }
            catch (ThreadAbortException) { throw; }
            catch (Exception e)
            {
                Log(Color.Red, "An Exception occured. Check debug log for details.");
                Logging.WriteDebug(">>> CRITICAL ERROR: SpellManager.Spells.ContainsKey() is corrupt and fails when looking up \"" + sSpellName + "\"");
                Logging.WriteDebug(">>> REPORT ERROR TO CC DEVELOPER WITH LOG");
                Logging.WriteException(e);
                throw;
            }

            return bKnown;
        }

        // replacement for LegacySpellManager.GlobalCooldown -- bug in 1.9.2.3 and later on some systems causing it to always be true
        private static bool _warnedGCD;

        private static bool Safe_GlobalCooldown()
        {
            if (!_warnedGCD)
            {
                Stopwatch timer = new Stopwatch();
                timer.Start();
                while (LegacySpellManager.GlobalCooldown && timer.ElapsedMilliseconds < 1500)
                {
                    Thread.Sleep(50);
                }

                if (LegacySpellManager.GlobalCooldown)
                {
                    _warnedGCD = true;
                    Slog("workaround enabled - disabling HonorBuddy use of LegacySpellManager.GlobalCooldown");
                }
            }

            return false;
        }


        private static bool MeImmobilized()
        {
            if (ObjectManager.Me.Stunned)
                Slog(Color.Orange, "You are stunned and unable to cast");
            else if (ObjectManager.Me.Possessed)
                Slog(Color.Orange, "You are possessed and unable to cast");
            else
                return false;

            return true;
        }

        private static bool MeSilenced()
        {
            if (MeImmobilized())
                ;
            else if (ObjectManager.Me.Silenced)
                Slog(Color.Orange, "You are silenced and unable to cast");
            else
                return false;

            return true;
        }

        private static bool IsCasting()
        {
            return Safe_GlobalCooldown() || ObjectManager.Me.IsCasting; //  || 0 != _me.ChanneledCasting;
        }

        /*
         * Safe_CastSpell()
         * 
         * several different overloads providing the ability to safely
         * cast a spell with or without a range check
         */

        public enum SpellRange { NoCheck, Check };
        public enum SpellWait { NoWait, Complete };

        private bool Safe_CastSpell(string sSpellName)
        {
            return Safe_CastSpell(sSpellName, SpellRange.NoCheck, SpellWait.Complete);
        }

        private bool Safe_CastSpellWithRangeCheck(string sSpellName)
        {
            return Safe_CastSpell(sSpellName, SpellRange.Check, SpellWait.Complete);
        }

        private bool Safe_CastSpell(string sSpellName, SpellRange chkRng, SpellWait chkWait)
        {
            return Safe_CastSpell(null, sSpellName, chkRng, chkWait);
        }

        private bool Safe_CastSpell(WoWUnit unit, string sSpellName, SpellRange chkRng, SpellWait chkWait)
        {
            WoWSpell spell = null;

            try
            {
                // spell = SpellManager.Spells[sSpellName];
                spell = SpellManager.Spells[sSpellName];
                System.Diagnostics.Debug.Assert(spell != null);
            }
            catch (ThreadAbortException) { throw; }
            catch (Exception e)
            {
                Log(Color.Red, "An Exception occured. Check debug log for details.");
                Logging.WriteDebug(">>> HB EXCEPTION in SpellManager.Spells[" + sSpellName + "]");
                Logging.WriteDebug(">>> Spell '" + sSpellName + "' believed to be " + (Safe_KnownSpell(sSpellName) ? "KNOWN" : "UNKNOWN") + " was used ");
                Logging.WriteException(e);
                throw;
                // return false;
            }

            return Safe_CastSpell(unit, spell, chkRng, chkWait);
        }

        private bool Safe_CastSpellWithRangeCheck(WoWSpell spell)
        {
            return Safe_CastSpell(null, spell, SpellRange.Check, SpellWait.Complete);
        }

        private bool Safe_CastSpell(WoWSpell spell)
        {
            return Safe_CastSpell(null, spell, SpellRange.NoCheck, SpellWait.Complete);
        }

        static int spellCount;

        private bool Safe_CastSpell(WoWUnit unit, WoWSpell spell, SpellRange chkRng, SpellWait chkWait)
        {
            bool bCastSuccessful = false;

            // enoughPower = (_me.GetCurrentPower(spell.PowerType) >= spell.PowerCost);
            if (MeSilenced())
                ;
            else if (chkRng == SpellRange.Check && unit != null && spell.MaxRange < unit.Distance)
                Dlog("status: Spell '{0}' has max range of {1:F1} but target is {2:F1} yds away - not cast", spell.Name, spell.MaxRange, _me.CurrentTarget.Distance);
            else
            {
                WaitForCurrentSpell(spell);
                if (false == SpellManager.CanCast(spell))
                {
                    Dlog("status: cannot cast spell '{0}' yet", spell.Name);
                }
                //	    else if (LegacySpellManager.GlobalCooldown)
                //		Dlog("status:  GCD is active -- not cast");
                //	    else if (spell.Cooldown)
                //		Dlog("status:  spell [{0}] on cooldown - not cast", spell.Name );
                //	    else if (!enoughPower)
                //		Dlog("warning:  not enough mana/energy for spell - " + spell.Name);
                //	    else if ( !SpellManager.CastableSpell( spell ))
                //		Dlog("status:  missing proc/reagent/totem to cast spell - " + spell.Name);
                //	    else if (!SpellManager.CanCast( spell.Id ))                    // unclear what this spell does other than periodically
                //		Dlog("warning:  cannot cast spell {0} due to missing proc/reagent/totem", spell.Name  );       // .. block casting which otherwise can occur... disabled
                else
                {
                    try
                    {
                        if (unit == null)
                            bCastSuccessful = SpellManager.Cast(spell);
                        else
                        {
                            bCastSuccessful = SpellManager.Cast(spell, unit);
                        }
                    }

                    catch (ThreadAbortException) { throw; }
                    catch (Exception e)
                    {
                        Log(Color.Red, "An Exception occured. Check debug log for details.");
                        Logging.WriteDebug("HB EXCEPTION in spell.Cast([" + spell.Id + ":" + spell.Name + "])");
                        Logging.WriteException(e);
                        return false;
                    }

                    if (bCastSuccessful)
                        Log(Color.DodgerBlue, "*" + spell.Name + (++spellCount % 2 == 0 ? "" : " "));
                    else
                        Dlog("Cast of {0} failed", spell.Name);

                    StyxWoW.SleepForLagDuration();
                    if (chkWait == SpellWait.Complete)
                    {
                        WaitForCurrentSpell(null);
                    }
                }
            }

            return bCastSuccessful;
        }

        private static void WaitForCurrentSpell(WoWSpell spell)
        {
            // const int ID_WIND_SHEAR = 57994;

            if (Safe_GlobalCooldown())
                Dlog("Waiting for global cooldown to finish");
            else if (ObjectManager.Me.IsCasting) // || _me.ChanneledCasting != 0)
                Dlog("Waiting for current spell to finish casting");

            while (IsCasting())
            {
#if NOTRIGHTNOW
                if (spell != null && _pvp.IsBattleground() && spell.Id != ID_WIND_SHEAR && _me.GotTarget && _me.CurrentTarget.IsCasting )
                {
                    WoWSpell windshear = null;
                    try
                    {
                        windshear = SpellManager.Spells["Wind Shear"];
                        if ( _me.CurrentTarget.Distance < windshear.MaxRange )
                        {
                            Slog( Color.Orange, "Battleground:  detected target casting... stopping current cast and attempting interrupt");
                            SpellManager.StopCasting();
                            if ( !SpellManager.Cast( windshear, _me.CurrentTarget ))
                                Slog("Battleground:  stopped my current cast but unable to cast Wind Shear");
                            else
                                Log(Color.DodgerBlue, "*" + windshear.Name);
                        }
                    }
                    catch 
                    {
                    }
                }
#endif
                Thread.Sleep(75);
            }
        }

        private void RunLUA(string sCmd)
        {
            WaitForCurrentSpell(null);
            Lua.DoString(sCmd, "shaman.lua");
        }

        private List<string> CallLUA(string sCmd)
        {
            WaitForCurrentSpell(null);
            return Lua.GetReturnValues(sCmd, "shaman.lua");
        }


        /*
         * Only issues the Stop moving command if currently moving.  also
         * accounts for any lag which prevents immediate stop
         */
        public static void Safe_StopMoving()
        {
            if (_cfg.DisableMovement)
                return;

            if (!ObjectManager.Me.IsMoving)
                return;

            int countTries = 0;
            Stopwatch stopTimer = new Stopwatch();

            stopTimer.Start();
            while (ObjectManager.Me.IsMoving && stopTimer.ElapsedMilliseconds < 1000)
            {
                countTries++;
                WoWMovement.MoveStop();
                Thread.Sleep(100);          // increased from 50 to handle flag update lag better 
            }

            if (countTries > 1)
            {
                Dlog("Attempted to stop moving " + countTries);
            }

            if (!ObjectManager.Me.IsMoving)
                Dlog("Stopped Moving");
            else
            {
                if (MeImmobilized())
                    Slog("Immobilized is true but still moving and can't stop; am I Feared?");
                else
                    Slog("ERROR: " + countTries + " attempts to stop moving and failed; character Feared?");
            }
        }

        /*
         * Only issues the Stop moving command if currently moving.  also
         * accounts for any lag which prevents immediate stop
         */
        private void Safe_Dismount()
        {
            if (!_me.Mounted)
                return;

            int countTries = 0;
            Stopwatch stopTimer = new Stopwatch();

            stopTimer.Start();
            while (_me.Mounted && stopTimer.ElapsedMilliseconds < 1500)
            {
                countTries++;
                Mount.Dismount();
                Thread.Sleep(100);
            }


            if (_me.Mounted)
            {
                Slog("LAG!! still mounted after {0} dismount attempts - timed out after {1} ms", countTries, stopTimer.ElapsedMilliseconds);
            }
            else if (countTries > 1)
            {
                Dlog("Dismount needed {0} attempts - took {1} ms", countTries, stopTimer.ElapsedMilliseconds);
            }
        }

        /*
         * Only issues the Stop moving command if currently moving.  also
         * accounts for any lag which prevents immediate stop
         */
        private bool Safe_SetCurrentTarget(WoWUnit target)
        {
            Stopwatch stopTimer = new Stopwatch();

            stopTimer.Start();
            if (target == null)
            {
                if (_me.GotTarget)
                    _me.ClearTarget();
            }
            else if (!_me.GotTarget || _me.CurrentTarget.Guid != target.Guid)
            {
                target.Target();
            }

            while (_me.CurrentTarget != target && stopTimer.ElapsedMilliseconds < 2000)
            {
                Thread.Sleep(60);
            }

            if (_me.CurrentTarget != target)
                Dlog("Timeout:  must have died, game state change, or serious lag - .CurrentTarget not updated after {0} ms", stopTimer.ElapsedMilliseconds);
            else
            {
                Dlog("Safe_SetCurrentTarget() took {0} ms to set .CurrentTarget to {0}[{1}]", stopTimer.ElapsedMilliseconds, Safe_UnitName(target), target.Level);
                return true;
            }

            return false;
        }

        /*
         * MoveTo()
         * 
         * if the point to move to is less than PathPrecision, then the toon
         * will not move.  This function checks if we are moving a very small
         * distance and forces movement by changing the precision if needed
         */
        private void MoveTo(WoWPoint newPoint)
        {
            if (_cfg.DisableMovement)
                return;

            float distToMove = _me.Location.Distance(newPoint);
            float prevPrec = Navigator.PathPrecision;

            if (distToMove <= prevPrec)
                Navigator.PathPrecision = distToMove - (float)0.1;

            int baseCount = System.Environment.TickCount;
            while (_me.IsCasting)
            {
                if (baseCount > (System.Environment.TickCount + 10000))
                {
                    Slog(Color.Red, "ERROR:  Waited 10+ secs for cast to finish-- moving anyway");
                    break;
                }
            }

            if (Navigator.GeneratePath(_me.Location, newPoint).Length != 0)
                Navigator.MoveTo(newPoint);
            else
                Log(Color.Red, "Cannot generate navigation path to new position");

            Navigator.PathPrecision = prevPrec;
        }

        private void MoveToCurrentTarget()
        {
            MoveToUnit(_me.CurrentTarget);
        }

        private void MoveToUnit(WoWUnit unit)
        {
            if (_cfg.DisableMovement)
                return;
            Dlog("Move: moving to {0}:{1} thats {2:F2} yds away", unit.IsPlayer ? "player" : "npc", Safe_UnitName(unit), unit.Distance);
            WoWPoint newPoint = WoWMovement.CalculatePointFrom(unit.Location, (float)_distForMeleePull);
            MoveTo(newPoint);      // WoWMovement.ClickToMove(newPoint);
        }

        private void MoveToHealTarget(WoWUnit unit, double distRange)
        {
            if (!IsUnitInRange(unit, distRange))
            {
                if (_me.IsCasting)
                {
                    Slog("MoveToHealTarget:  moving to Heal Target {0} who is {1} yds away", Safe_UnitName(unit), unit.Distance);
                    WaitForCurrentSpell(null);
                }

                MoveToUnit(unit);
                while (_me.IsMoving && unit.IsAlive && !IsUnitInRange(unit, distRange) && unit.Distance < 100)
                {
                    // while running, if someone else needs a heal throw a riptide on them
                    if (Safe_KnownSpell("Riptide") && SpellManager.CanCast("Riptide"))
                    {
                        WoWPlayer otherTarget = CC_PVP.chooseNextHealTarget(unit, (double)_cfg.RAF_GroupNeedHeal);
                        if (otherTarget != null)
                        {
                            Slog("MoveToHealTarget:  healing {0} while moving to heal target {1}", Safe_UnitName(otherTarget), Safe_UnitName(unit));
                            Safe_CastSpell(otherTarget, "Riptide", SpellRange.Check, SpellWait.NoWait);
                            StyxWoW.SleepForLagDuration();
                        }
                    }
                }

                if (_me.IsMoving)
                    Safe_StopMoving();

                Dlog("MoveToHealTarget: stopping now that Heal Target is {0} yds away", unit.Distance);
            }
        }

        private bool FindBestTarget() { return FindBestTarget(_maxDistForRangeAttack); }
        private bool FindBestMeleeTarget() { return FindBestTarget(_maxDistForMeleeAttack); }

        private bool FindBestTarget(double withinDist)
        {
            // find mobs in melee distance
            List<WoWUnit> mobs = null;

#if DISABLE_TARGETING_FOR_INSTANCEBUDDY
            if (_me.IsInInstance)
            {
                Dlog("In instance: targeting disabled");
                return false;
            }
#endif
            if (IsRAF())
            {
                Dlog("FindBestTarget-RAF within {0}", withinDist);
                if (RaFHelper.Leader.IsAlive && RaFHelper.Leader.GotTarget)
                {
                    WoWUnit leaderTarget = RaFHelper.Leader.CurrentTarget;
                    if (leaderTarget.IsPlayer && _me.IsHorde == leaderTarget.ToPlayer().IsHorde)
                        Dlog("Ignore RaF Leader Target -- player is same faction");
                    else if (Safe_IsFriendly(leaderTarget))
                        Dlog("Ignore RaF Leader Target -- unit is friendly");
                    else if (!leaderTarget.Attackable)
                        Dlog("Ignore RaF Leader Target -- unit is not attackable");
                    else if (!_me.GotTarget || leaderTarget.Guid != _me.CurrentTarget.Guid)
                    {
                        Slog(">>> SET LEADERS TARGET:  {0}-{1}[{2}]",
                                leaderTarget.Class,
                                Safe_UnitName(leaderTarget),
                                leaderTarget.Level
                                );
                        Safe_SetCurrentTarget(leaderTarget);
                        return true;
                    }
                }
            }
            else if (!_pvp.IsBattleground())
            {
                Dlog("FindBestTarget within {0}", withinDist);
                mobs = (from o in ObjectManager.ObjectList
                        where o is WoWUnit && o.Distance <= withinDist
                        let unit = o.ToUnit()
                        where unit.Attackable
                            && unit.IsAlive
                            && unit.Combat
                            && !IsMeOrMyStuff(unit)
                            && (IsTargetingMeOrMyStuff(unit) || unit.CreatureType == WoWCreatureType.Totem)
                            && !Blacklist.Contains(unit.Guid)
                        orderby unit.CurrentHealth ascending
                        select unit
                            ).ToList();
            }
            else
            {
                Dlog("FindBestTarget-PVP within {0}", withinDist);
                mobs = (from o in ObjectManager.ObjectList
                        where o is WoWUnit && o.Distance <= withinDist
                        let unit = o.ToUnit()
                        where unit.IsAlive && unit.IsPlayer && unit.ToPlayer().IsHorde != ObjectManager.Me.IsHorde
                            && unit.InLineOfSight && !unit.IsPet
                            && !Blacklist.Contains(unit.Guid)
                        orderby unit.CurrentHealth ascending
                        select unit
                            ).ToList();
            }

            if (mobs != null && mobs.Count > 0)
            {
                Dlog("FindBestTarget:  found {0} mobs within distance", mobs.Count);
                WoWUnit newTarget = mobs[0];

                if (newTarget != null && (!_me.GotTarget || newTarget.Guid != _me.CurrentTarget.Guid))
                {
                    Slog(">>> NEW TARGET:  {0}-{1}[{2}] at {3:F1} yds",
                            newTarget.Class,
                            Safe_UnitName(newTarget),
                            newTarget.Level,
                            newTarget.Distance
                            );
                    Safe_SetCurrentTarget(newTarget);
                    return true;
                }
            }
            else
            {
                Dlog("FindBestTarget:  found 0 mobs within distance");
            }

            return false;
        }

        // compares targets to find one with the lowest health (not lowest % of health)
        // .. to hopefully score a quick kill
        private class HealthSorter : IComparer<WoWUnit>
        {
            public int Compare(WoWUnit obj1, WoWUnit obj2)
            {
                return obj1.CurrentHealth.CompareTo(obj2.CurrentHealth);
            }
        }

        private void CastFakeSpell()
        {
            if (MeSilenced())
                return;

            WoWSpell spell = SpellManager.Spells["Healing Wave"];
            if (_me.GetCurrentPower(spell.PowerType) <= spell.PowerCost)
            {
                return;
            }

            while (true)
            {
                if (spell.Cooldown || Safe_GlobalCooldown() || false == SpellManager.CanCast(spell))
                {
                    Dlog("Fake cast not ready... Checking for adds while waiting...");
                    CheckForAdds();
                    continue;
                }

                try
                {
                    spell.Cast();
                }
                catch (ThreadAbortException) { throw; }
                catch (Exception e)
                {
                    Log(Color.Red, "An Exception occured. Check debug log for details.");
                    Logging.WriteDebug("Exception when casting Fake Healing Wave");
                    Logging.WriteException(e);
                    return;
                }

                Slog("Cast fake Healing Wave...");
                Thread.Sleep(500);
                if (!_me.IsCasting)
                    Slog("Fake cast was interrupted/stolen ... ugghhhh");
                else
                {
                    SpellManager.StopCasting();
                    Slog("... canceled fake cast");
                }

                break;
            }

            return;
        }


        #endregion


        private static bool IsRAF()
        {
            return (ObjectManager.Me.IsInParty || ObjectManager.Me.IsInRaid) && RaFHelper.Leader != null; // from Nesox
            // old test - return !Battlegrounds.IsInsideBattleground && ObjectManager.Me.PartyMember1 != null;
        }

        private static bool IsHealer()
        {
            if (_pvp.IsBattleground())
                return _cfg.PVP_CombatStyle != ConfigValues.PvpCombatStyle.CombatOnly || _typeShaman == ShamanType.Resto;

            return IsRAF() && _cfg.RAF_CombatStyle != ConfigValues.RafCombatStyle.CombatOnly || _typeShaman == ShamanType.Resto;
        }

        private static bool IsHealerOnly()
        {
            if (_pvp.IsBattleground())
                return _cfg.PVP_CombatStyle == ConfigValues.PvpCombatStyle.HealingOnly;

            return IsRAF() && _cfg.RAF_CombatStyle == ConfigValues.RafCombatStyle.HealingOnly;
        }

        private static bool IsCombatOnly()
        {
            return !IsHealer();
        }

        private static bool IsMeOrMyStuff(WoWUnit unit)
        {
            if (unit == null)
                return false;

            if (unit.IsMe)
                return true;

            // check if this unit was created by me
            return IsMeOrMyStuff(ObjectManager.GetObjectByGuid<WoWUnit>(unit.CreatedByUnitGuid));
        }

        private static bool IsTargetingMeOrMyStuff(WoWUnit unit)
        {
            return unit != null && IsMeOrMyStuff(unit.CurrentTarget);
        }


        private static bool IsMeOrMyGroup(WoWUnit unit)
        {
            if (unit == null)
                return false;

            if (unit.IsPet)
                unit = unit.CreatedByUnit;

            if (unit.IsMe)
                return true;

            if (unit.IsPlayer && IsRAF() && (ObjectManager.Me.PartyMembers.Contains(unit.ToPlayer()) || ObjectManager.Me.RaidMembers.Contains(unit.ToPlayer())))
                return true;

            // check if this unit was created by me
            return IsMeOrMyGroup(ObjectManager.GetObjectByGuid<WoWUnit>(unit.CreatedByUnitGuid));
        }

        private static bool IsTargetingMeOrMyGroup(WoWUnit unit)
        {
            return unit != null && IsMeOrMyGroup(unit.CurrentTarget);
        }

        private bool IsImmunneToNature(WoWUnit unit)
        {
            return Safe_IsValid(unit) && _listNatureImmune.Contains(unit.Entry);
        }

        private bool IsImmunneToFire(WoWUnit unit)
        {
            return Safe_IsValid(unit) && _listFireImmune.Contains(unit.Entry);
        }

        private bool IsImmunneToFrost(WoWUnit unit)
        {
            return Safe_IsValid(unit) && _listFrostImmune.Contains(unit.Entry);
        }

        private bool IsFearMob(WoWUnit unit)
        {
            if (!Safe_IsValid(unit))
                return false;

            bool found = _hashTremorTotemMobs.Contains(unit.Entry);
            return found;
        }

        /*
         * Reports whether we to stop for any Weapon Buffs
         */
        private bool IsWeaponEnhanceNeeded(out bool needMainhand, out bool needOffhand)
        {
            needMainhand = false;
            needOffhand = false;

            // now make sure we have a mainhand weapon
            if (_me.Inventory.Equipped.MainHand == null)
                return false;

            if (_me.Inventory.Equipped.MainHand.ItemInfo.WeaponClass == WoWItemWeaponClass.FishingPole)
                return false;

            if (_typeShaman == ShamanType.Unknown)
                return false;

            // see if we trained any weapon enchants yet... if not then don't need to imbue weapon
            string enchantMainhand;
            string enchantOffhand;
            GetBestWeaponEnchants(out enchantMainhand, out enchantOffhand);

            if (string.IsNullOrEmpty(enchantMainhand))
                return false;

            // get the enchant info from LUA
            List<string> weaponEnchants = CallLUA("return GetWeaponEnchantInfo()");

            if (Equals(null, weaponEnchants))
                return false;

            needMainhand = weaponEnchants[0] == "" || weaponEnchants[0] == "nil";
            if (IsOffhandWeapon())
                needOffhand = weaponEnchants[3] == "" || weaponEnchants[3] == "nil";

            if (needMainhand)
                Dlog("Mainhand weapon {0} needs enhancement", _me.Inventory.Equipped.MainHand == null ? "(none)" : _me.Inventory.Equipped.MainHand.Name);

            if (needOffhand)
                Dlog("Offhand  weapon {0} needs enhancement", _me.Inventory.Equipped.OffHand == null ? "(none)" : _me.Inventory.Equipped.OffHand.Name);

            return needMainhand || needOffhand;
        }

        private void GetBestWeaponEnchants(out string enchantMainhand, out string enchantOffhand)
        {
            List<string> listMainhand = null;
            List<string> listOffhand = null;

            enchantMainhand = "";
            enchantOffhand = "";

            if (_pvp.IsBattleground())
            {
                enchantMainhand = _cfg.PVP_MainhandEnchant;
                enchantOffhand = _cfg.PVP_OffhandEnchant;
            }
            else
            {
                enchantMainhand = _cfg.PVE_MainhandEnchant;
                enchantOffhand = _cfg.PVE_OffhandEnchant;
            }

            // Dlog("gbwe1 --  mh:{0},  oh:{1}", enchantMainhand, enchantOffhand);
            switch (_typeShaman)
            {
                case ShamanType.Unknown:
                    return;
                case ShamanType.Elemental:
                    // Dlog("Enchant - choosing Elemental Defaults for Auto");
                    listMainhand = _enchantElemental;
                    listOffhand = listMainhand;
                    break;
                case ShamanType.Resto:
                    // Dlog("Enchant - choosing Restoration Defaults for Auto");
                    listMainhand = _enchantResto;
                    listOffhand = _enchantResto;
                    break;
                case ShamanType.Enhance:
                    if (_pvp.IsBattleground())
                    {
                        // Dlog("Enchant - choosing PVP Enhancement Defaults for Auto");
                        listMainhand = _enchantEnhancementPVP_Mainhand;
                        listOffhand = _enchantEnhancementPVP_Offhand;
                    }
                    else
                    {
                        // Dlog("Enchant - choosing PVE Enhancement Defaults for Auto");
                        listMainhand = _enchantEnhancementPVE_Mainhand;
                        listOffhand = _enchantEnhancementPVE_Offhand;
                    }
                    break;
            }

            // Dlog("gbwe2 --  mh:{0},  oh:{1}", enchantMainhand, enchantOffhand);

            if ('A' == enchantMainhand.ToUpper()[0] && listMainhand != null)
            {
                enchantMainhand = listMainhand.Find(spellname => Safe_KnownSpell(spellname));
                //Dlog("Enchant - Mainhand:  configured for AUTO so choosing '{0}'", enchantMainhand);
            }
            else
            {
                //Dlog("Enchant - Mainhand:  configured for '{0}'", enchantMainhand);
            }

            if ('A' == enchantOffhand.ToUpper()[0] && listOffhand != null)
            {
                enchantOffhand = listOffhand.Find(spellname => Safe_KnownSpell(spellname));
                //Dlog("Enchant - Offhand:   configured for AUTO so choosing '{0}'", enchantOffhand);
            }
            else
            {
                //Dlog("Enchant - Offhand:   configured for '{0}'", enchantOffhand);
            }

            return;
        }

        /*
         * Checks to see if Off Hand slot currently has a weapon in it.
         * Uses a timer so that LUA call is not made more than once a minute
         */
        private Stopwatch _offhandCheck = new Stopwatch();
        private bool _offhandLastResult;

        private bool IsOffhandWeapon()
        {
            if (_me.Inventory.Equipped.OffHand == null)
                _offhandLastResult = false;
            else if (!Safe_KnownSpell("Dual Wield"))
                _offhandLastResult = false;
            else
            {
                List<string> offhandWeapons = CallLUA("return OffhandHasWeapon()");
                if (Equals(null, offhandWeapons))
                    return false;

                string isOffhandWeapon = offhandWeapons[0];
                // Dlog("return from OffhandHasWeapon() = '" + isOffhandWeapon + "'");
                if (String.IsNullOrEmpty(isOffhandWeapon) || isOffhandWeapon == "nil")
                    _offhandLastResult = false;
                else
                    _offhandLastResult = true;
            }

            return _offhandLastResult;
        }

        private bool IsAuraPresent(WoWUnit unit, string sAura)
        {
            uint stackCount;
            return IsAuraPresent(unit, sAura, out stackCount);
        }

        private static bool IsAuraPresent(WoWUnit unit, string sAura, out uint stackCount)
        {
            stackCount = 0;
#if USE_LUA_FOR_AURAS
            List<string> myAuras = Lua.GetReturnValues("return UnitAura(\"player\",\"" + sAura + "\")");
            if (Equals(null, myAuras))
                return false;

            stackCount = (uint) Convert.ToInt32(myAuras[3]);
#else
            WoWAura aura = GetAura(unit, sAura);
            if (aura == null)
                return false;

            stackCount = aura.StackCount;
#endif
            return true;
        }

        private static uint GetAuraStackCount(WoWUnit unit, string auraName)
        {
            uint stackCount = 0;
            bool isPresent = IsAuraPresent(unit, auraName, out stackCount);
            return stackCount;
        }

        private static WoWAura GetAura(WoWUnit unit, string auraName)
        {
            if (unit == null)
                return null;

            WoWAura aura = (from a in unit.Auras
                            where 0 == string.Compare(a.Value.Name, auraName, true)
                            select a.Value).FirstOrDefault();
            return aura;
        }

        private WoWAura GetAuraCreatedByMe(WoWUnit unit, string auraName)
        {
            if (unit == null)
                return null;

            WoWAura aura = (from a in unit.Auras
                            where a.Value.CreatorGuid == _me.Guid
                                  && 0 == string.Compare(a.Value.Name, auraName, true)
                            select a.Value).FirstOrDefault();
            return aura;
        }


        private bool HaveValidTarget()
        {
            return _me.GotTarget && _me.CurrentTarget.IsAlive;
            //                && !Blacklist.Contains( t.Guid );
        }

        /*
         * CurrentTargetInMeleeDistance()
         * 
         * Check to see if CurrentTarget is within melee range.  This allows
         * recognizing when a pulled mob is close enough to melee as well as 
         * as when a pulled mob moves out of melee
         */
        private bool CurrentTargetInMeleeDistance()
        {
            if (_me.Location.Distance(_me.CurrentTarget.Location) < 5.0)
                return true;

            return false;
        }

        /*
         * CurrentTargetInRangedDistance()
         * 
         * Check to see if CurrentTarget is within ranged attack distance and line of sight.  This allows
         * recognizing when a pulled mob is close enough to melee as well as 
         * as when a pulled mob moves out of melee
         */
        private bool CurrentTargetInRangedDistance()
        {
            return IsUnitInRange(_me.CurrentTarget, _maxDistForRangeAttack);
        }

        private bool CurrentTargetInRangedPullDistance()
        {
            return IsUnitInRange(_me.CurrentTarget, _distForRangedPull);
        }

        private static bool IsUnitInRange(WoWUnit unit, double range)
        {
            return (unit.Distance < range && unit.InLineOfSight);
        }

        /*
         * trys to determine if 'unit' points to a mob that is a Caster.  Currently
         * only see .Class as being able to help determine.  the big question marks
         * are Druids and Shamans for grinding purposes, so even though we try
         * to guess we still make routines able to adapt on pulls, etc. to fact
         * mob may not behave like we guessed
         */
        private static bool IsCaster(WoWUnit unit)
        {
            bool isCaster = false;

            switch (unit.Class.ToString().ToLower())
            {

                default:
                    Slog("UKNOWN MOB CLASS:  CONTACT CC DEVELOPER:  Please provide the class name '" + unit.Class + "' and name [" + Safe_UnitName(unit) + "]");
                    break;

                case "paladin":
                case "druid":
                case "rogue":
                case "warrior":
                case "death knight":
                    break;

                case "mage":
                case "warlock":
                case "shaman":
                case "priest":
                    isCaster = true;
                    break;
            }

            // following test added because of "Unyielding Sorcerer" in Hellfire
            // .. having a class of Paladin, yet they fight as ranged casters

            if (!isCaster)
            {
                if (unit.Name.ToLower().Contains("sorcerer"))
                    isCaster = true;
                else if (unit.Name.ToLower().Contains("shaman"))
                    isCaster = true;
                else if (unit.Name.ToLower().Contains("mage"))
                    isCaster = true;
                else if (unit.Name.ToLower().Contains("warlock"))
                    isCaster = true;
                else if (unit.Name.ToLower().Contains("priest"))
                    isCaster = true;
                else if (unit.Name.ToLower().Contains("wizard"))
                    isCaster = true;
                else if (unit.Name.ToLower().Contains("adept"))
                    isCaster = true;
            }

            return isCaster;
        }

        private static void AddToBlacklist(ulong guidMob)
        {
            AddToBlacklist(guidMob, System.TimeSpan.FromMinutes(5));
        }

        private static void AddToBlacklist(ulong guidMob, System.TimeSpan ts)
        {
            if (Blacklist.Contains(guidMob))
                Dlog("already blacklisted mob: " + guidMob);
            else
            {
                Blacklist.Add(guidMob, ts);
                Dlog("blacklisted mob: " + guidMob);
            }
        }

        /*
         * CheckForItem()
         * 
         * Lookup an item by its item # 
         * return null if not found
         */
        private static WoWItem CheckForItem(List<uint> listId)
        {
            WoWItem item = ObjectManager.GetObjectsOfType<WoWItem>(false).Find(unit => listId.Contains(unit.Entry));
            return item;
        }

        private static WoWItem CheckForItem(uint itemId)
        {
            WoWItem item = ObjectManager.GetObjectsOfType<WoWItem>(false).Find(unit => unit.Entry == itemId);
            return item;
        }

        private static WoWItem CheckForItem(string itemName)
        {
            WoWItem item;
            uint id;

            if (uint.TryParse(itemName, out id) && id > 0)
                item = ObjectManager.GetObjectsOfType<WoWItem>(false).Find(unit => unit.Entry == id);
            else
                item = ObjectManager.GetObjectsOfType<WoWItem>(false).Find(unit => 0 == string.Compare(unit.Name, itemName, true));

            return item;
        }

        #endregion


        #region REST

        private static bool _loadingScreen = false;

        private bool IsGameUnstable()
        {

            if (!Safe_IsValid(_me))
            {
                _loadingScreen = true;
                Dlog("HB or WOW initializing... not ready yet");
                Thread.Sleep(1000);
                return true;
            }

            if (!ObjectManager.IsInGame)
            {
                _loadingScreen = true;
                Dlog("Detected loading screen... sleeping for 1 sec");
                Thread.Sleep(1000);
                return true;
            }

            if (_loadingScreen)
            {
                _loadingScreen = false;
                Dlog("loading screen phase 2... waiting for 2 secs");
                Thread.Sleep(2000);
                return true;
            }

            if (_pvp.IsBattleground() && Battlegrounds.Finished)
                return true;

            return false;
        }

        public override void Pulse()
        {
#if PULSE_IS_NEEDED
            if (IsGameUnstable())
                return;

            if ( _lastCheckWasInCombat && !_me.Combat )
            {
                Dlog( "Pulse:  entering REST CYCLE");
                _lastCheckWasInCombat = false;
                ShamanBuffs(true);
            }
            else if ( !_lastCheckWasInCombat && _me.Combat )
            {
                Dlog( "Pulse:  entering COMBAT CYCLE");
                _lastCheckWasInCombat = true;
            }
#else
            base.Pulse();
#endif
        }

#if   FOLLOW_DURING_PVP
        
        public override void Pulse()
        {
            // base.Pulse();  // base doesn't do anything... can ignore
            
			try
			{
                if (_me == null || !_me.IsValid)
                {
                    _loadingScreen = true;
                    Dlog("pvp:  HB still initializing... not ready yet");
                    Thread.Sleep(1000);
                    return;
                }

                if (!ObjectManager.IsInGame)
                {
                    _loadingScreen = true;
                    Dlog("pvp:  Detected loading screen... sleeping for 1 sec");
                    Thread.Sleep(1000);
                    return;
                }

                if (_loadingScreen)
				{
                    Dlog("pvp:  loading screen phase 2... waiting for 2 sec");
                    Thread.Sleep(2000);
                    _loadingScreen = false;
                    return;
				}

                if ( !_pvp.IsBattleground() || Battlegrounds.Finished)
                {
                    _followTarget = null;
                    return;
                }

                // if we have a follow target, validate it
                if (_followTarget != null)
                {
                    if (!_followTarget.IsValid)
                    {
                        Log("Follow Target:  invalid, resetting");
                        _followTarget = null;
                    }
                    if (_followTarget.Dead || !_followTarget.IsAlive || _followTarget.CurrentHealth < 2)
                    {
                        Log("Follow Target:  appears to be dead, resetting");
                        _followTarget = null;
                    }
                }

                // if we DO NOT HAVE a follow target, FIND one
                if (_followTarget == null && !_me.Auras.ContainsKey("Preparation")) 
                {
                    Dlog("pvp: Looking for group member to follow");
                    try
                    {
                        _followTarget = (
                            from p in _me.RaidMembers
                            where p is WoWPlayer
                                && p != null
                                && p.IsValid
                                && p.IsAlive
                                && !p.IsMe
                                && p.Distance < 200
                                && !Blacklist.Contains(p.Guid) 
                                && p.Class != WoWClass.Rogue 
                                && p.Class != WoWClass.Druid
                            orderby p.MaxHealth descending
                            select p
                            ).FirstOrDefault();
                    }
                    catch(Exception e)
                    {
                        Log( Color.Red, "An Exception occured. Check debug log for details.");
                        Logging.WriteDebug(">>> EXCEPTION: occurred trying to find Follow Target");
                        Logging.WriteException(e);
                        _followTarget = null;
                        return;
                    }

                    if (_followTarget != null)
                        Log(Color.Green, "Following: {0}, max health={1}", _followTarget.Name, _followTarget.MaxHealth);
                    else
                    {
                        Log(Color.Orange, "Following: no raid member nearby, no follow target set");
                        return;
                    }
                }
                
	
				// Party buffs pulsing
				if (_me != null && _me.IsValid && !_me.Combat && _me.IsAlive  && _me.Auras.ContainsKey("Preparation"))
				{						
					if (Battlegrounds.GetCurrentBattleground() == BattlegroundType.WSG)
					{
						if (_me.RaidMembers.Count == 10 )
						{
							ShieldTwisting( true);
						}
					}
					else if (Battlegrounds.GetCurrentBattleground() == BattlegroundType.AV || Battlegrounds.GetCurrentBattleground() == BattlegroundType.IoC)
					{
						if (_me.RaidMembers.Count == 40 )
						{
                            ShieldTwisting(true);
                        }
					}
					else
					{
						if (_me.RaidMembers.Count == 15 )
						{
                            ShieldTwisting(true);
                        }
					}						
				}
				
                // now loop right here as long as we are in BG and have a follow target
                while (ObjectManager.IsInGame && _pvp.IsBattleground() && !Battlegrounds.Finished)
                {
                    // quit loop if dead
                    if ( _me == null || !_me.IsValid || _me.Dead )
                    {
                        _followTarget = null;
                        Dlog("pvp follow:  exiting loop because {0}", _me.IsValid ? "I am dead" : "HB still initializing");
                        break;
                    }

                    // quit loop if follow target dead
                    if ( _followTarget == null || !_followTarget.IsValid || _followTarget.Dead )
                    {
                        _followTarget = null;
                        Dlog("pvp follow:  exiting loop because {0}", _followTarget.IsValid ? "follow target is dead" : "no follow target setup");
                        break;
                    }

                    // rest if needed
                    if (_local.NeedRest)
                    {
                        _local.Rest();
                        continue;
                    }

                    // move to follow if needed
                    if (_me.HealthPercent > 1 && (_followTarget.Distance > 30 || !_followTarget.InLineOfSight))
                    {
                        if (_me != null && !_me.Mounted && !_me.Combat && !_me.Stunned && !_me.Fleeing && !_me.Pacified && Mount.CanMount() && !_me.IsCasting && !LegacySpellManager.GlobalCooldown)
                        {
                            Mount.MountUp();
                            Thread.Sleep(500);
                        }
                    
                        // moving 20 yds away from follow target
                        WoWPoint movePoint = WoWMovement.CalculatePointFrom(_followTarget.Location, 20.0f);                       
                        float lenPath = Navigator.GeneratePath(_me.Location, movePoint).Length;

                        if ( Math.Abs(lenPath - movePoint.Distance) < 5 )
                        {
                            Log("pvp follow: navigation path distance of {0} yds not close enough to (1) yds needed to reach {2}. Resetting", lenPath, movePoint.Distance, movePoint);
                            Blacklist.Add(_followTarget.Guid, TimeSpan.FromMinutes(1));
                            _followTarget = null;
                        }
                        else
                        {
                            Log("pvp follow:  travelling {0} yds to position {1}", lenPath, movePoint );
                            Navigator.MoveTo(movePoint);
                            Thread.Sleep(100);
                        }

                        continue;
                    }

                    // set target to /assist follower, otherwise pick the next best target
                    if (_followTarget.IsValid && _followTarget.GotTarget && _me.CurrentTarget.Guid != _followTarget.CurrentTargetGuid )
                    {
                        Dlog("Assisting follow target with {0}", Safe_UnitName( _followTarget.CurrentTarget ));
                        Safe_SetCurrentTarget(_followTarget.CurrentTarget);
                        continue;
                    }

                    // attack if needed
                    if (_me.Combat)
                    {
                        Combat();
                    }
                }

			}
            catch (ThreadAbortException) { throw; }
            catch (Exception e)
            {
                Log(Color.Red, "An Exception occured. Check debug log for details.");
                Logging.WriteDebug("HB EXCEPTION in Pulse");
                Logging.WriteException(e);
            }
            
        }
#endif

        public override bool NeedRest
        {
            get
            {
                bool doWeNeedRest = false;
                try
                {
                    // Dlog("NEEDREST? START: " + _me.HealthPercent + "% health,  " + _me.ManaPercent + "% mana");
                    doWeNeedRest = NeedRestLogic();
                    // Dlog("NEEDREST? RETURN STATUS= {0}", doWeNeedRest  );
                }
                catch (ThreadAbortException) { throw; }
                catch (Exception e)
                {
                    Log(Color.Red, "An Exception occured. Check debug log for details.");
                    Logging.WriteDebug("HB EXCEPTION in NeedRest");
                    Logging.WriteException(e);
                }

                return doWeNeedRest;
            }
        }

        private bool NeedRestLogic()
        {
            if (IsGameUnstable())
                return false;

            // following allows returning immediately when a plug-in or other
            // .. has casted a spell and returned to HB.  this improves the
            // .. bobber recognition of AutoAngler specifically
            if (_me.IsCasting)
            {
                Dlog("NeedRest:  aborted since casting");
                return false;
            }

            if (IsHealer())
            {
                if (HealRaid())         // don't do anything else if we have to heal
                    return false;
            }                           // .. otherwise fall through to NeedRest tests

            ReportBodyCount();

            if (_pullTimer.IsRunning && _pullTimer.ElapsedMilliseconds < 4000)
                return false;

            if (Battlegrounds.Finished)
                return false;

            if (MeImmobilized())
                return false;

            if (_me.Combat)
            {
                Dlog("NeedRest:  Invoking Combat() from here");
                Combat();
                return false;
            }

            if (IsRAF() && RaFHelper.Leader.Combat)
            {
                Dlog("NeedRest:  Invoking RaF Combat() from here");
                Combat();
                return false;
            }

            // if we switched modes ( grouped, battleground, or spec chg)
            if (DidWeSwitchModes())
            {
                Slog("^OPERATIONAL MODE CHANGED:  initializing...");
                Initialize();
                return true;
            }

            if (!IsHealerOnly() && _pvp.IsBattleground())
            {
                if (_me.GotTarget && _me.CurrentTarget.IsPlayer && _me.CurrentTarget.ToPlayer().IsHorde != _me.IsHorde && _me.CurrentTarget.Distance < _maxDistForRangeAttack && !_me.CurrentTarget.Mounted)
                {
                    Slog("BGCHK:  calling Combat() myself from NeedRest for CurrentTarget");
                    Combat();
                    return false;
                }

                if (FindBestTarget())
                {
                    Slog("BGCHK:  calling Combat() myself from NeedRest for FindBestTarget()");
                    Combat();
                    return false;
                }
            }

            // check to be sure not in a travelling state before
            //.. setting switches that will cause a dismount or form change
            if (_me.Mounted || InGhostwolfForm())
            {
                Dlog("Mounted or Ghostwolf - will wait to buff/enchant out of form");
            }
            else
            {
                if (IsRAF() && (_me.Combat || RaFHelper.Leader.Combat))
                    ;
                else if (_WereTotemsSet && CheckForSafeDistance("Totem Recall", _ptTotems, CheckDistanceForRecall()))
                {
                    Dlog("Need rest: TotemsWereSet() and recall CheckForSafeDistance({0:F1})= true", CheckDistanceForRecall());
                    if (!_me.GotTarget)
                        Dlog("Need rest: no current target so looks good to recall");
                    else
                    {
                        WoWUnit unit = _me.CurrentTarget;
                        Dlog("Need rest: target:{0} atkable:{1} hostile:{2}, profile:{3} alive:{4}", unit.Distance,
                             unit.Attackable,
                             Safe_IsHostile(unit),
                             Safe_IsProfileMob(unit),
                             unit.IsAlive);
                    }
                    _RecallTotems = true;
                    return true;
                }

                if (IsWeaponEnhanceNeeded(out _needMainHandEnchant, out _needOffHandEnchant))
                {
                    Dlog("Need rest: true, IsWeaponEnhanceNeeded mh:{0} oh:{1}", _needMainHandEnchant, _needOffHandEnchant);
                    return true;
                }

                if (ShieldBuffNeeded(true))
                {
                    Dlog("Need rest: true, ShieldBuffNeeded");
                    return true;
                }
            }

            if (IsCleanseNeeded(_me) != null)
            {
                Dlog("Need rest: true, IsCleanseNeeded");
                _castCleanse = true;
                return true;
            }

            if (_me.HealthPercent <= _cfg.RestHealthPercent && !_me.IsSwimming)
            {
                Dlog("Need rest: true, CurrentHealth {0:F1}% less than RestHealthPercent {1:F1}%", _me.HealthPercent, _cfg.RestHealthPercent);
                return true;
            }

            if (_me.ManaPercent <= _cfg.RestManaPercent && !_me.IsSwimming && !Styx.Logic.Common.Rest.NoDrink)
            {
                Dlog("Need rest: true, CurrentMana {0:F1}% less than RestManaPercent {1:F1}%", _me.ManaPercent, _cfg.RestManaPercent);
                return true;
            }

            if (IsRAF() && SpellManager.CanCast("Ancestral Spirit"))
            {
                List<WoWPlayer> grp = !_me.IsInRaid ? _me.PartyMembers : _me.RaidMembers;
                // !p.IsAFKFlagged 
                _rezTarget = (from p in grp where Safe_IsValid(p) && p.Dead select p).FirstOrDefault();
                if (_rezTarget != null && !Blacklist.Contains(_rezTarget))
                    return true;
            }

            if (LevelbotSettings.Instance.UseMount && _cfg.UseGhostWolfForm)
            {
                Wlog("warning:  UseMount takes precedence over UseGhostWolf:  the Ghost Wolf setting ignored this session");
            }
            else if (!_me.Mounted && !IsRAF() && _cfg.UseGhostWolfForm)
            {
                if (Safe_KnownSpell("Ghost Wolf") && !InGhostwolfForm() && _me.IsOutdoors)
                {
                    if (CheckForSafeDistance("Ghost Wolf", _me.Location, _cfg.DistanceForGhostWolf))
                    {
                        Dlog("Need rest: true, Ghost Wolf: closest enemy at least {0:F1} yds away", _cfg.DistanceForGhostWolf);
                        if (SpellManager.CanCast("Ghost Wolf"))  // make sure we can so not stuck in loop
                            _castGhostWolfForm = true;
                        return true;
                    }
                }
            }

            return false;
        }

        // bool inRest = false;
        public override void Rest()
        {
            try
            {
                Dlog("RESTING START: " + _me.HealthPercent + "% health,  " + _me.ManaPercent + "% mana");
                RestLogic();
                Dlog("RESTING ENDED: " + _me.HealthPercent + "% health,  " + _me.ManaPercent + "% mana");
            }
            catch (ThreadAbortException) { throw; }
            catch (Exception e)
            {
                Log(Color.Red, "An Exception occured. Check debug log for details.");
                Logging.WriteDebug(">>> EXCEPTION: occurred in Rest()");
                Logging.WriteException(e);
            }
        }

        public void RestLogic()
        {
            ShowCurrentStatus();

            if (_castCleanse && CleanseIfNeeded(_me))
            {
                _castCleanse = false;
                return;
            }

            if (_RecallTotems)
            {
                _RecallTotems = false;
                RecallTotemsForMana();
                return;
            }

            // try to use bandages if requested
            if (_me.HealthPercent < _cfg.RestHealthPercent && _me.ManaPercent > _cfg.RestManaPercent)
            {
                if (_cfg.UseBandages)
                {
                    if (UseBandageIfAvailable())
                        return;
                }
            }

            // try to heal several times (quicker than eating)
            if (_me.HealthPercent < _cfg.RestHealthPercent && _me.ManaPercent >= _cfg.RestManaPercent)
            {
                if (HealMyself())
                    return;
            }

            if (_needClearWeaponEnchants)
            {
                _needClearWeaponEnchants = false;
                RunLUA("CancelItemTempEnchantment(1)");
                RunLUA("CancelItemTempEnchantment(2)");
                return;
            }

            if (_needTotemBarSetup)
            {
                _needTotemBarSetup = false;
                TotemSetupBar();
            }

            if (!Safe_IsValid(_rezTarget) || !_rezTarget.Dead)
            {
                _rezTarget = null;
            }
            else if (!IsUnitInRange(_rezTarget, 28))
            {
                MoveToUnit(_rezTarget);
            }
            else
            {
                Safe_StopMoving();
                Log("^Ressurection:  target {0} is {1:F1} yds away", Safe_UnitName(_rezTarget), _rezTarget.Distance);

#if NOTRIGHTNOW
                if ( !SpellManager.Cast("Ancestral Spirit", _rezTarget ))
                {
                    Dlog("Rez:  spell cast failed?  abort");
                    return;
                }
#else
                if (!Safe_CastSpell(_rezTarget, "Ancestral Spirit", SpellRange.Check, SpellWait.NoWait))
                    return;
#endif
                StyxWoW.SleepForLagDuration();
                while (!IsGameUnstable() && IsCasting() && _me.IsAlive)
                {
                    if (!Safe_IsValid(_rezTarget) || _rezTarget.IsAlive)
                    {
                        Dlog("Rez:  He is alive... Stop Casting...!!!");
                        SpellManager.StopCasting();
                    }
                    StyxWoW.SleepForLagDuration();
                }

                Log("^Ressurection:  attempt completed, blacklisting for 30 seconds");
                // blacklist so if they have a rez pending but haven't clicked yes,
                //  ..  we move onto rezzing someone else
                Blacklist.Add(_rezTarget, TimeSpan.FromSeconds(30));
                _rezTarget = null;
                return;
            }

            Dlog("RestLogic:  before ShamanBuffs");
            ShamanBuffs(true);

            if (_castGhostWolfForm && SpellManager.CanCast("Ghost Wolf"))
            {
                GhostWolf();
                _castGhostWolfForm = false;
            }

            // now eat/drink if needed
            if ((_me.HealthPercent < _cfg.RestHealthPercent || _me.ManaPercent < _cfg.RestManaPercent) && !_me.IsSwimming)
            {
                bool noFood = false;
                bool noDrink = false;
                bool stoppedToEat = _me.HealthPercent < _cfg.RestHealthPercent;
                bool stoppedToDrink = _me.ManaPercent < _cfg.RestManaPercent;

                WaitForCurrentSpell(null);
                if (stoppedToEat)
                {
                    noFood = !UseConsumeable(LevelbotSettings.Instance.FoodName);
                    if (noFood)
                        Log(Color.Red, "No food left, staying here waiting for health to regen to {0}%", _cfg.RestHealthPercent);
                    else
                    {
                        _countFood++;
                        Dlog("Eating:  {0} total used, average {1:F0} per hour",
                            _countFood,
                            (60.0 * 60.0 * 1000.0 * _countFood) / (System.Environment.TickCount - _tickStart)
                            );
                    }
                }

                if (stoppedToDrink)
                {
                    noDrink = !UseConsumeable(LevelbotSettings.Instance.DrinkName);
                    if (noDrink)
                        Log(Color.Red, "No drinks left, staying here waiting for mana to regen to {0}%", _cfg.RestManaPercent);
                    else
                    {
                        _countDrinks++;
                        Dlog("Drinking:  {0} total used, average {1:F0} per hour",
                            _countDrinks,
                            (60.0 * 60.0 * 1000.0 * _countDrinks) / (System.Environment.TickCount - _tickStart)
                            );
                    }
                }

                if (noFood == false && noDrink == false)
                {
                    Slog("Stopped to {0}{1}{2}",
                         stoppedToEat ? "Eat" : "",
                         stoppedToEat && stoppedToDrink ? " and " : "",
                         stoppedToDrink ? "Drink" : ""
                        );
                }

                while ((IsAuraPresent(_me, "Food") && _me.HealthPercent < 100.0)
                    || (IsAuraPresent(_me, "Drink") && _me.HealthPercent < 100.0)
                    || (!_me.Combat && _me.HealthPercent < _cfg.RestHealthPercent)
                    || (!_me.Combat && _me.ManaPercent < _cfg.RestManaPercent))
                {
                    Thread.Sleep(100);
                    Dlog("dbg waiting:  Eating:{0} Health:{1}%  /  Drinking:{2} Mana:{3}%", IsAuraPresent(_me, "Food"), _me.HealthPercent, IsAuraPresent(_me, "Drink"), _me.ManaPercent);
                }
            }
        }

        private bool InGhostwolfForm()
        {
            return _me.Auras.ContainsKey("Ghost Wolf");
        }

        private bool GhostWolf()
        {
            bool b = false;

            if (!_me.IsOutdoors || _me.IsIndoors)
                ;
            else if (!Safe_KnownSpell("Ghost Wolf"))
                ;
            else
            {
                CC_TalentGroup tg = new CC_TalentGroup();
                int impGW = tg.GetTalentInfo(2, 6); // "Improved Ghost Wolf"
                if (impGW != 2)
                {
                    Safe_StopMoving();
                    Thread.Sleep(100);
                }
                b = Safe_CastSpell("Ghost Wolf", SpellRange.NoCheck, SpellWait.Complete);
            }

            return b;
        }

        #endregion

        #region HANDLE FALLING

        public void HandleFalling() { }

        #endregion

        #region Buffs

        /*
         * Note:  the following are interface functions that need to be implemented by the class.  
         * They are not used presently in the ShamWOW implementation.  Buffs are handled within the 
         * flow of the current Pull() and Combat() event handlers
         */
        public override bool NeedPreCombatBuffs { get { return false; } }
        public override void PreCombatBuff() { }

        public override bool NeedPullBuffs { get { return false; } }
        public override void PullBuff() { }

        public override bool NeedCombatBuffs { get { return false; } }
        public override void CombatBuff() { }


        public void ShamanBuffs(bool atRest)
        {
            if (!_me.IsAlive)
                return;

            // Shield Twisting:  Cast based upon amount of Mana available
            ShieldTwisting(atRest);

            Dlog("ShamanBuffs:  AllowNonHealSpells:{0}, atrest:{1}", AllowNonHealSpells(), atRest);
            //            if (AllowNonHealSpells() && atRest != false)
            if (atRest && (_needMainHandEnchant || _needOffHandEnchant))
            {
                try
                {
                    string enchantMainhand;
                    string enchantOffhand;

                    GetBestWeaponEnchants(out enchantMainhand, out enchantOffhand);
                    Dlog("Weapon Enchant Check:  NeedOnMainhand={0}{1}, NeedOnOffhand={2}{3}", _needMainHandEnchant, !_needMainHandEnchant ? "" : "-" + enchantMainhand, _needOffHandEnchant, !_needOffHandEnchant ? "" : "-" + enchantOffhand);

                    if (_needMainHandEnchant)
                    {
                        Stopwatch timer = new Stopwatch();
                        bool checkErrMain = false;
                        bool checkErrOff = false;

                        while (!Safe_CastSpell(enchantMainhand, SpellRange.NoCheck, SpellWait.Complete))
                            ;

                        timer.Reset(); timer.Start();
                        do
                        {
                            IsWeaponEnhanceNeeded(out checkErrMain, out checkErrOff);
                            Thread.Sleep(100);
                        } while (checkErrMain && timer.ElapsedMilliseconds < 1000);

                        if (checkErrMain)
                        {
                            Dlog("failed to apply mainhand weapon enhancement - will reassess need for enchants again");
                            _needMainHandEnchant = false;
                            _needOffHandEnchant = false;
                        }
                    }

                    if (_needOffHandEnchant)
                    {
                        Safe_CastSpell(enchantOffhand, SpellRange.NoCheck, SpellWait.Complete);
                        Thread.Sleep(atRest ? 651 : 285);
                        _needOffHandEnchant = false;
                    }
                }
                catch (ThreadAbortException) { throw; }
                catch (Exception e)
                {
                    Log(Color.Red, "An Exception occured. Check debug log for details.");
                    Logging.WriteDebug("HB EXCEPTION in ShamanBuffs()");
                    Logging.WriteException(e);
                }
            }
        }
        #endregion


        #region Heal

        /*
         * NeedHeal
         * 
         * return a true/false indicating whether the Heal() event handler should be called by the
         * HonorBuddy engine.
         */
        public override bool NeedHeal
        {
            get
            {
                return IsSelfHealNeeded();
            }
        }

        /*
         * Heal()
         * 
         * Called if a heal is needed.
         */
        public override void Heal()
        {
            Dlog("HEAL Enter");
            ShowCurrentStatus();
            HealMyself();
            Dlog("HEAL Exit");
        }

        private bool IsSelfHealNeeded()
        {
            double threshhold;

            if (IsRAF())
                threshhold = IsHealer() ? _cfg.RAF_GroupNeedHeal : _cfg.EmergencyHealthPercent;
            else if (_pvp.IsBattleground())
                threshhold = IsHealer() ? _cfg.PVP_GroupNeedHeal : _cfg.NeedHealHealthPercent;
            else
                threshhold = _cfg.NeedHealHealthPercent + (countEnemy >= _cfg.PVE_StressfulMobCount ? 10 : 0);

            if (_me.HealthPercent <= threshhold
                && countEnemy == 1
                && !IsFightStressful()
                // && (_typeShaman != ShamanType.Enhance || _countMeleeEnemy == 1)
                && AllowNonHealSpells()
                && _me.GotTarget
                && _me.CurrentTarget.HealthPercent < 10.0
                && _me.CurrentTarget.IsAlive
                )
            {
                Log("^Enemy weak at {0:F0}%, skipping heal", _me.CurrentTarget.HealthPercent);
                return false;
            }

            return !MeSilenced() && _me.HealthPercent <= threshhold;
        }

        private bool HealMyself()
        {
            bool healCast = false;

            Stopwatch lagTimer = new Stopwatch();
            double startHealth = _me.HealthPercent;
            double startMana = _me.ManaPercent;

            // use safety heals if health is critical and in combat or being targeted by hostiles
            if ((_me.Combat || countEnemy > 0))
            {
                Warstomp();         // Warstomp checks for enemys in range so isnt wasted

                if (_me.HealthPercent < _cfg.LifebloodPercent)
                {
                    healCast = GiftOfTheNaaru();
                    if (!healCast)
                        healCast = Lifeblood();
                }

                if (_me.ManaPercent <= _cfg.EmergencyManaPercent && _me.HealthPercent > _cfg.NeedHealHealthPercent)
                    UseManaPotionIfAvailable();

                if (_me.HealthPercent < _cfg.EmergencyHealthPercent)
                {
                    if (UseHealthPotionIfAvailable())
                        ;
                    else if (_hasGlyphOfStoneClaw)
                    {
                        if (_totemName[TOTEM_EARTH].ToUpper() != "EARTH ELEMENTAL TOTEM"
                            && _totemName[TOTEM_EARTH].ToUpper() != "STONECLAW TOTEM")
                        {
                            if (TotemCast(TOTEM_EARTH, "Stoneclaw Totem"))
                            {
                                Log("^Shaman Bubble: casting Stoneclaw Totem w/ Glyph");
                            }
                        }
                    }
                }
            }

            healCast = HealPlayer(null);
            double checkHealth = _me.HealthPercent;
            double checkMana = _me.ManaPercent;

            if (!healCast)
            {
                Slog("Casting of heal prevented: Health={0:F0}% Mana={1:F0}%", _me.HealthPercent, _me.ManaPercent);
            }
            else
            {
                Dlog("^Heal begun @ health:{0:F2}% mana:{1:F2}%", startHealth, startMana);
                WaitForCurrentSpell(null);      // wait for 

                // wait for lagged update of health % if it isn't pvp
                if (!_pvp.IsBattleground() || _me.HealthPercent > 60)
                {

                    // DEAL WITH Health% UPDATE LAG ---
                    // .. Make sure before we leave here that if a heal was 
                    // .. cast, the .HealthPercent value has been updated
                    // .. so rest of code can make informed decisions
                    lagTimer.Start();
                    while (lagTimer.ElapsedMilliseconds < 1000 && Math.Abs(_me.HealthPercent - checkHealth) < 5)
                    {
                        Thread.Sleep(100);
                    }

                    Dlog("^Heal ended @ health:{0:F2}% mana:{1:F2}% mana - took {2} ms for char to update", _me.HealthPercent, _me.ManaPercent, lagTimer.ElapsedMilliseconds);
                }
            }

            return healCast;
        }

        private bool AllowNonHealSpells()
        {
            return _me.ManaPercent > _cfg.EmergencyManaPercent && _me.HealthPercent > _cfg.EmergencyHealthPercent;
        }


        /*
         * IsCleanseNeeded()
         * 
         * Called cleanse if needed.
         */
        public WoWAura IsCleanseNeeded(WoWUnit unit)
        {
            // if we don't have any, exit quickly
            if (unit.Debuffs.Count == 0)
                return null;

            bool knowCleanseSpirit = Safe_KnownSpell("Cleanse Spirit");
            bool canCleanMagic = _hasTalentImprovedCleanseSpirit;
            bool canCleanCurse = knowCleanseSpirit;

            WoWAura dispelDebuff = (
                                       from dbf in unit.Debuffs
                                       where
                                           (dbf.Value.Spell.DispelType == WoWDispelType.Curse && canCleanCurse)
                                           || (dbf.Value.Spell.DispelType == WoWDispelType.Magic && canCleanMagic)
                                           || (dbf.Value.Spell.DispelType == WoWDispelType.Magic && unit.IsMe && _hasGlyphOfShamanisticRage)
                                       select dbf.Value
                                   ).FirstOrDefault();

            return dispelDebuff;
        }

        public bool CleanseIfNeeded(WoWUnit unit)
        {
            return CleanseIfNeeded(unit, null);
        }

        public bool CleanseIfNeeded(WoWUnit unit, WoWAura dispelDebuff)
        {
            // if we don't have any, exit quickly
            if (unit.Debuffs.Count == 0)
                return false;

            bool castSpell = false;

            if (dispelDebuff == null)
                dispelDebuff = IsCleanseNeeded(unit);

            if (dispelDebuff != null)
            {
                Log("^Dispel target {0}[{1}] has debuf '{2}' with {3} secs remaining", Safe_UnitName(unit), unit.Level, dispelDebuff.Name, dispelDebuff.TimeLeft.Seconds);
                if (_hasTalentImprovedCleanseSpirit || dispelDebuff.Spell.DispelType == WoWDispelType.Curse)
                    castSpell = Safe_CastSpell(unit, "Cleanse Spirit", SpellRange.Check, SpellWait.Complete);
                else if (unit.IsMe && _hasGlyphOfShamanisticRage && dispelDebuff.Spell.DispelType == WoWDispelType.Magic)
                    castSpell = Safe_CastSpell(unit, "Shamanistic Rage", SpellRange.NoCheck, SpellWait.Complete);
            }

            return castSpell;
        }

        #endregion

        #region Pull

        /*
         * Pull()
         * 
         * Currently always do a ranged pull from '_distForRangedPull' way
         * If HB has given us a mob to pull that is further away, we will
         * run towards him up to within '_distForRangedPull' 
         * 
         */

        public override void Pull()
        {
            Dlog("Entered Pull");
            PullLogic();
            Dlog("Exited Pull");
        }

        public void PullInitialize()
        {
            _pullTargGuid = _me.CurrentTarget.Guid;
            // _pullTargHasBeenInMelee = false;
            _pullAttackCount = 0;
            // _pullStart = _me.Location;

            _pullTimer.Reset();          //  reset timer for new pull 
            _pullTimer.Start();          //  start timer ticking
        }

        public void PullLogic()
        {
            if (_me.IsCasting)
            {
                Dlog("Pull:  aborted while casting");
                return;
            }

            if (IsHealer())
            {
                if (HealRaid())
                {
                    Dlog("Healed raid member -- exiting pull");
                    return;
                }

                if (IsHealerOnly())
                {
                    if (_me.GotTarget && Safe_IsHostile(_me.CurrentTarget))
                        InterruptEnemyCast();

                    ShamanBuffs(false);
                    Dlog("Flagged as healer only -- exiting pull");
                    return;
                }
            }

            if (_pvp.IsBattleground() || IsRAF())
            {
                Combat();
                return;
            }

            if (!_me.GotTarget)
            {
                Dlog("HB gave (null) pull target");
                return;
            }

            if (_me.CurrentTarget.IsPet)
            {
                WoWUnit petOwner = _me.CurrentTarget.CreatedByUnit;
                if (petOwner != null)
                {
                    Dlog("Changing target from pet {0} to owner {1}", Safe_UnitName(_me.CurrentTarget), Safe_UnitName(petOwner));
                    Safe_SetCurrentTarget(petOwner);
                }
                else
                {
                    Dlog("Appears that pet {0} does not have an owner?  guess we'll fight a pet", Safe_UnitName(_me.CurrentTarget));
                }
            }

            if (!_me.CurrentTarget.IsAlive)
            {
                Dlog("HB gave a Dead pull target: " + Safe_UnitName(_me.CurrentTarget) + "[" + _me.CurrentTarget.Level + "]");
                _me.ClearTarget();
                return;
            }

            if (!_pvp.IsBattleground() && !IsRAF() && !TreeRoot.Current.Name.ToLower().Contains("duel"))
            {
                if (_me.CurrentTarget.TaggedByOther && !IsTargetingMeOrMyGroup(_me.CurrentTarget))
                {
                    Slog("Combat Target is tagged by another player -- let them have it");
                    _me.ClearTarget();
                    return;
                }
            }

            if (Blacklist.Contains(_me.CurrentTargetGuid))
            {
                Slog("Skipping pull of blacklisted mob: " + Safe_UnitName(_me.CurrentTarget) + "[" + _me.CurrentTarget.Level + "]");
                _me.ClearTarget();
                return;
            }

            if (_pvp.IsBattleground())
            {
                if (_me.GotTarget && _me.CurrentTarget.IsPlayer && _me.CurrentTarget.Mounted && _me.IsHorde != _me.CurrentTarget.ToPlayer().IsHorde)
                {
                    Slog("Skipping mounted player: " + Safe_UnitName(_me.CurrentTarget) + "[" + _me.CurrentTarget.Level + "]");
                    Blacklist.Add(_me.CurrentTarget.Guid, System.TimeSpan.FromSeconds(2));
                    _me.ClearTarget();
                    return;
                }
            }

            CheckForAdds();

            // reset state values we use to determine what point we are at in 
            //  .. in transition from Pull() to Combat()
            //---------------------------------------------------------------------------
            if (_pullTargGuid != _me.CurrentTarget.Guid)
            {
                PullInitialize();
                Slog(">>> PULL: " + (_me.CurrentTarget.Elite ? "[ELITE] " : "") + Safe_UnitName(_me.CurrentTarget) + "[" + _me.CurrentTarget.Level + "] at " + _me.CurrentTarget.Distance.ToString("F1") + " yds");
                Dlog("pull started at {0}% health, {1}% mana", _me.HealthPercent, _me.ManaPercent);
            }

            if (_pvp.IsBattleground() || _cfg.DisableMovement)  // never timeout in PVP or when disable movement set
                ;
            else if (_pullTimer.ElapsedMilliseconds > ConfigValues.PullTimeout && !(_me.CurrentTarget.TaggedByMe || IsTargetingMeOrMyGroup(_me.CurrentTarget)))
            {
                Blacklist.Add(_me.CurrentTarget.Guid, System.TimeSpan.FromSeconds(30));
                Slog("Pull TIMED OUT for: " + _me.CurrentTarget.Class + "-" + Safe_UnitName(_me.CurrentTarget) + "[" + _me.CurrentTarget.Level + "] after " + _pullTimer.ElapsedMilliseconds + " ms -- blacklisted for 30 secs");
                _me.ClearTarget();
                return;
            }

            if (!WoWMovement.IsFacing)
            {
                WoWMovement.Face();
            }

            if (_typeShaman != ShamanType.Enhance)
                PullRanged();
            else if (_pvp.IsBattleground())
                PullFast();
            else
            {
                switch (_cfg.PVE_PullType)
                {
                    case ConfigValues.TypeOfPull.Auto:
                        PullAuto();
                        break;
                    case ConfigValues.TypeOfPull.Body:
                        PullBody();
                        break;
                    case ConfigValues.TypeOfPull.Fast:
                        PullFast();
                        break;
                    case ConfigValues.TypeOfPull.Ranged:
                        PullRanged();
                        break;
                }
            }


            Dlog("distance after pull: " + _me.CurrentTarget.Distance.ToString("F2"));
            // CheckForPlayers();
        }


        public void PullAuto()
        {
            Dlog("PullType Auto");
            if (IsCaster(_me.CurrentTarget))
                PullFast();
            else
                PullRanged();
        }

        public void PullBody()
        {
            Dlog("PullType Body");
            if (!CurrentTargetInMeleeDistance())
                MoveToCurrentTarget();
            else if (!ShockOpener())
            {
                if (_me.GotTarget && !_me.IsAutoAttacking && _me.CurrentTarget.IsAlive && CurrentTargetInMeleeDistance() && !_pvp.IsBattleground())
                {
                    Dlog("** Auto-Attack started in Combat");
                    AutoAttack();
                }
            }
        }

        public void PullFast()
        {
            Dlog("PullType Fast");
            if (!CurrentTargetInMeleeDistance() && _pullTimer.ElapsedMilliseconds < ConfigValues.PullTimeout)
            {
                MoveToCurrentTarget();
            }

            if (_me.CurrentTarget.Distance < 25)
            {
                if (!ShockOpener())
                {
                    if (_me.GotTarget && !_me.IsAutoAttacking && _me.CurrentTarget.IsAlive && CurrentTargetInMeleeDistance() && !_pvp.IsBattleground())
                    {
                        Dlog("** Auto-Attack started in Combat");
                        AutoAttack();
                    }
                }
            }

            if (_me.CurrentTarget.Distance < 8 && _me.Mounted)
            {
                Safe_Dismount();
            }
        }


        public void PullRanged()
        {
            Dlog("PullType Ranged");
            if (!CurrentTargetInRangedPullDistance())
            {
                MoveToCurrentTarget();
                return;
            }

            if (_me.IsMoving)
                Safe_StopMoving();

            // set totems now for all specs except Enhancement (who does in Combat)
            if (_typeShaman != ShamanType.Enhance && !_pvp.IsBattleground())
            {
                SetTotemsAsNeeded();
            }

            if (_pullAttackCount > 0 && ShockOpener())
            {
                _pullAttackCount++;
            }
            else if (IsImmunneToNature(_me.CurrentTarget) && ShockOpener())
            {
                _pullAttackCount++;
            }
            else if (_typeShaman == ShamanType.Enhance && _me.CurrentTarget.Distance < 20 && IsTargetingMeOrMyGroup(_me.CurrentTarget) && ShockOpener())
            {
                _pullAttackCount++;
            }
            else if (LightningBolt())
            {
                _pullAttackCount++;
            }
        }


        #endregion

        #region Combat

        /*
         * Combat()
         * 
         */
        public override void Combat()
        {
            Dlog("Entered Combat");
            try
            {
                CombatLogic();
            }
            catch (ThreadAbortException) { throw; }
            catch (Exception e)
            {
                if (!_me.GotTarget)
                    Logging.WriteDebug("Exception:  referencing 'null' target: mob expired or out of range? no big deal");
                else
                {
                    Log(Color.Red, "An Exception occured. Check debug log for details.");
                    Logging.WriteDebug("EXCEPTION in Combat() - HonorBuddy API or CC Error");
                }

                Logging.WriteException(e);
            }
            Dlog("Exiting Combat");
        }

        private void CombatLogic()
        {
            if (_me.IsCasting)
            {
                Dlog("CombatLogic:  aborted since casting");
                return;
            }

            ShowCurrentStatus();
            // ListWowUnitsInRange();

            if (IsHealer())
            {
                if (_me.ManaPercent < _cfg.EmergencyManaPercent)
                {
                    if (!TotemExist(TOTEM_WATER) || TotemName(TOTEM_WATER).ToUpper() != "MANA TIDE TOTEM")
                    {
                        if (TotemCast(TOTEM_WATER, "Mana Tide Totem"))
                            return;
                    }

                    if (UseManaPotionIfAvailable())
                        return;
                }

                if (HealRaid())         // don't do anything else if we have to heal
                    return;

                if (IsHealerOnly())
                {
                    if (_me.GotTarget && Safe_IsHostile(_me.CurrentTarget))
                        InterruptEnemyCast();

                    ShamanBuffs(false);
                    return;
                }
            }

            if (MeImmobilized())
            {
                // need to look at trinketting out here...
                // .. otherwise just return because we can't move and can't cast
                if (_typeShaman == ShamanType.Enhance && IsAuraPresent(_me, "Feral Spirit") && SpellManager.CanCast("Spirit Walk"))
                {
                    Log("^Pet Ability - Spirit Walk (remove movement impairing effects)");
                    Safe_CastSpell("Spirit Walk");
                }

                return;
            }

            if (!combatChecks())
                return;

            CheckForAdds();
            if (!combatChecks())
                return;

            if (_me.CurrentTarget.TaggedByOther && !_me.CurrentTarget.TaggedByMe && !IsTargetingMeOrMyGroup(_me.CurrentTarget) && !TreeRoot.Current.Name.ToLower().Contains("duel"))
            {
                Slog("Combat Target is tagged by another player -- let them have it");
                _me.ClearTarget();
                return;
            }

            // check if we agroed a mob unintentionally
            if (_pullTargGuid != _me.CurrentTarget.Guid)
            {
                Safe_StopMoving();
                Slog(">>> ADD: " + Safe_UnitName(_me.CurrentTarget) + "[" + _me.CurrentTarget.Level + "] at " + _me.CurrentTarget.Distance.ToString("F1") + " yds");
                PullInitialize();
            }

            if (_timerCombatStats.ElapsedMilliseconds > 1000)
            {
                Dlog("status: currently at {0:F2}% health, {1:F2}% mana", _me.HealthPercent, _me.ManaPercent);
                _timerCombatStats.Reset(); _timerCombatStats.Start();
            }

            if (_pullTimer.ElapsedMilliseconds > 20000 && _me.CurrentTarget.HealthPercent > 95 && !_me.CurrentTarget.IsPlayer && !_me.CurrentTarget.Name.ToLower().Contains(" dummy"))
            {
                Slog("Evade bugged mob detected: blacklisting: " + _me.CurrentTarget.Class + "-" + Safe_UnitName(_me.CurrentTarget) + "[" + _me.CurrentTarget.Level + "]");
                StopAutoAttack();
                Blacklist.Add(_me.CurrentTarget.Guid, System.TimeSpan.FromHours(1));
                _me.ClearTarget();
                return;
            }

            if (!WoWMovement.IsFacing)
                WoWMovement.Face();

            if (!_me.IsAutoAttacking && _me.GotTarget && _me.CurrentTarget.IsAlive && CurrentTargetInMeleeDistance() && !_pvp.IsBattleground())
            {
                Dlog("** Auto-Attack started in Combat");
                AutoAttack();
            }

            // ok, reevaluate if still need healing in hopes we can attack
            if (NeedHeal && HealMyself())
                return;

            if (_me.GotTarget && _me.CurrentTarget.IsCasting)
                InterruptEnemyCast();

            if (!pvpChecks())
            {
                if (!FindBestTarget())
                    return;
            }

            if (_typeShaman == ShamanType.Unknown)
                CombatUndefined();
            else if (_typeShaman == ShamanType.Enhance)
                CombatMelee();
            else if (_pvp.IsBattleground())
                CombatElementalPVP();
            else
                CombatElemental();


            ShamanBuffs(false);
        }

        private readonly Stopwatch _timerCombatStats = new Stopwatch();


        #region Combat Styles

        /*
         * CombatUndefined()
         * 
         * Rotation used prior to learning any talents.  Characteristics at this level are
         * hi mana and health regen, with Lightning Bolt still having the best damage to mana
         * ratio.  However, due to high regen will use instant attacks which aren't as 
         * efficient but allow higher dps.
         */
        private void CombatUndefined()
        {
            if (!CurrentTargetInRangedDistance() && _me.Rooted)
            {
                // options -- trinket, heal (anticipating being hit)
                if (!FindBestTarget())
                {
                    Slog("Rooted:  no target in range so waiting it out...");
                    return;
                }
            }

            if (!CurrentTargetInRangedDistance())
            {
                // chase until in ranged distance or for 8 seconds
                Dlog("running to mob " + _me.CurrentTarget.Distance.ToString("F2") + " away");
                MoveToCurrentTarget();
                BestShockForMob();
                return;
            }

            if (_me.IsMoving)
            {
                if (!_me.GotTarget)
                    Dlog("Moving: and I don't have a target?  stopping to get my bearings");
                else
                    Dlog("Moving: target {0:F1} yds away, stopping now...", _me.Location.Distance(_me.CurrentTarget.Location));
                Safe_StopMoving();
            }

            if (!combatChecks())
                return;

            // #1 Set Strength of Earth Totem if no buff
            if (!_me.Auras.ContainsKey("Strength of Earth") && SetTotemsAsNeeded())
                return;

            // #2 Cast Racial dps enhance abilitiy
            CastCombatSpecials();

            // #3 Instant (Ranged): Earth Shock
            if (EarthShock())
                return;

            // #4 Instant (Melee):  Primal Strike
            if (PrimalStrike())
                return;

            // #5 Lightning Bolt filler
            if (combatChecks())
                LightningBolt();
        }


        /*
         * CombatMelee()
         * 
         * Rotation: Lightning Bolt, until the mob is near you, then spam Earth Shock while 
         * auto attacking.  Shield Twist between Water Shield and Lightning Shield.  Keep 
         * Windfury Weapon up, use Flametongue if not trained, and Rockbiter if no other choice
         * 
         * Looks redundant, but we use combatChecks() and AllowNonHealSpells() over and over.  They
         * are state checking functions and the conditions they check will change during the 
         * running of this function.  They need to be measured immediately before using an
         * ability.
         */
        private void CombatMelee()
        {
            if (_pvp.IsBattleground())
            {
                CombatMeleePVP();
                return;
            }

            // if I'm rooted then throw anything ranged at them
            if (!CurrentTargetInMeleeDistance())
            {
                if (!_me.Rooted)
                {
                    MoveToCurrentTarget();
                    BestShockForMob();
                    return;
                }

                Dlog("I'm melee and rooted and out of melee range, do something!!!");
                if (FeralSpirit())
                    return;

                if (FindBestMeleeTarget())      // check for weakest targets only first
                    ;
                else if (FindBestTarget())      // settle for weakest target within range
                {
                    CombatElemental();
                    return;
                }
                else
                {
                    Slog("Rooted:  no target in range so waiting it out...");
                    return;
                }
            }

            if (_me.IsMoving)
            {
                if (!_me.GotTarget)
                    Dlog("Moving: and I don't have a target?");
                else
                    Dlog("Moving: target {0:F1} yds away, stopping now...", _me.Location.Distance(_me.CurrentTarget.Location));
                Safe_StopMoving();
            }

            if (!combatChecks())
                return;

            // use Shamanistic Rage when our mana is low and mob we are fighting has 
            // .. a lot of health left.  Since it gives mana back for 
            if (_me.ManaPercent < _cfg.ShamanisticRagePercent)
            {
                if (IsFightStressful() || (countEnemy > 1) || (_me.GotTarget && _me.CurrentTarget.HealthPercent >= 75))
                {
                    ShamanisticRage();
                }
            }

            // for Enhancement:  make first set of totems high priority
            if (!TotemsWereSet() && combatChecks() && AllowNonHealSpells())
            {
                SetTotemsAsNeeded();
            }

            if (CallForReinforcements())
                return;

            CastCombatSpecials();

            if (FlameShock())
                return;

            if (SearingFlamesCheck())
                return;

            if (MaelstromCheck())
                return;

            if (_countTenYardEnemy > 1 && TotemExist(TOTEM_FIRE) && FireNova())
                return;

            if (IsStormstrikeNeeded() && Stormstrike())
                return;

            if (EarthShock())
                return;

            if (Stormstrike())
                return;

            if (PrimalStrike())
                return;

            if (CleanseIfNeeded(_me))
                return;

            // now check to see if any totems still needed
            if (combatChecks() && AllowNonHealSpells())
            {
                SetTotemsAsNeeded();
            }
        }


        private void CombatMeleePVP()
        {
            // if I'm rooted then throw anything ranged at them
            if (!CurrentTargetInMeleeDistance())
            {
                if (_me.GotTarget && _me.CurrentTarget.Distance < 50 && FeralSpirit())
                    return;

                if (!_me.Rooted)
                {
                    MoveToCurrentTarget();
                    BestShockForMob();
                    return;
                }

                Dlog("CombatMeleePVP:  I'm melee and rooted and out of melee range, do something!!!");
                if (IsAuraPresent(_me, "Feral Spirit"))
                {
                    if (!SpellManager.CanCast("Spirit Walk"))
                        Dlog("CombatMeleePVP:  Feral Spirit active but unable to cast Spirit Walk yet");
                    else if (Safe_CastSpell("Spirit Walk", SpellRange.NoCheck, SpellWait.Complete))
                        return;
                }

                if (FindBestMeleeTarget())      // check for weakest targets only first
                {
                    Dlog("... switched to nearby melee range target while rooted");
                    // so fall through to Melee PVP Combat logic
                }
                else if (FindBestTarget())      // settle for weakest target within range
                {
                    Dlog("... switched to lowest health ranged target while rooted");
                    CombatElementalPVP();
                    return;
                }
                else
                {
                    Slog("Rooted:  no target in range so waiting it out...");
                    return;
                }
            }

            if (_me.IsMoving)
            {
                if (!_me.GotTarget)
                    Dlog("Moving: and I don't have a target?");
                else
                    Dlog("Moving: target {0:F1} yds away, stopping now...", _me.Location.Distance(_me.CurrentTarget.Location));
                Safe_StopMoving();
            }

            if (!combatChecks())
                return;

            ShamanisticRage();

            if (CallForReinforcements())
                return;

            // for Enhancement:  make first set of totems high priority
            if (!TotemsWereSet() && AllowNonHealSpells())
            {
                SetTotemsAsNeeded();
            }

            CastCombatSpecials();

            if (FlameShock())
                return;

            if (MaelstromCheck())
                return;

            if (IsStormstrikeNeeded() && Stormstrike())
                return;

            if (EarthShock())
                return;

            if (Stormstrike())
                return;

            if (PrimalStrike())
                return;

            if (LavaLash())
                return;

            if (_countTenYardEnemy > 1 && TotemExist(TOTEM_FIRE) && FireNova())
                return;

            if (CleanseIfNeeded(_me))
                return;

            // now check to see if any totems still needed
            if (combatChecks() && AllowNonHealSpells())
            {
                SetTotemsAsNeeded();
            }
        }


        /*
         * CombatElemental()
         * 
         */
        private void CombatElemental()
        {
            if (_pvp.IsBattleground())
            {
                CombatElementalPVP();
                return;
            }

            if (!CurrentTargetInRangedDistance() && _me.Rooted)
            {
                // options -- trinket, heal (anticipating being hit)
                if (!FindBestTarget())
                {
                    Slog("Rooted:  no target in range so waiting it out...");
                    return;
                }
            }

            if (!CurrentTargetInRangedDistance())
            {
                // chase until in ranged distance or for 8 seconds
                Dlog("running to mob " + _me.CurrentTarget.Distance.ToString("F2") + " away");
                MoveToCurrentTarget();
                BestShockForMob();
                return;
            }

            if (_me.IsMoving)
            {
                Dlog("Stopping movement for Elemental combat");
                Safe_StopMoving();
            }

            if (!combatChecks())
            {
                Dlog("CombatElem: failed combat checks so no attack cast this pass");
                return;
            }

            // we already cast totems in pull, so this is just to see if we need to replace any if in crisis
            if (AllowNonHealSpells() && (countEnemy > 1 || (_me.GotTarget && _me.CurrentTarget.HealthPercent > 25)))
            {
                SetTotemsAsNeeded();
            }

            if (CallForReinforcements())
            {
                Dlog("CombatElem: failed CallForReinforcements() so no attack cast this pass");
                return;
            }

            CastCombatSpecials();

            if ((IsFightStressful() && _countTenYardEnemy >= 1) || _me.ManaPercent <= _cfg.ThunderstormPercent)
            {
                if (Thunderstorm())
                {
                    Dlog("CombatElem: Thunderstorm cast so no further attacks this pass");
                    return;
                }
            }

            if (AllowNonHealSpells() && _me.GotTarget && _countAoe8Enemy >= 3 && _me.CurrentTarget.Distance < 33 && Safe_KnownSpell("Earthquake"))
            {
                if (Safe_CastSpell("Earthquake", SpellRange.Check, SpellWait.Complete))
                {
                    if (!LegacySpellManager.ClickRemoteLocation(_me.CurrentTarget.Location))
                    {
                        Dlog("^Ranged AoE Click FAILED:  cancelling Earthquake");
                        SpellManager.StopCasting();
                    }
                    else
                    {
                        Dlog("^Ranged AoE Click successful:  EARTHQUAKE!!!");
                        StyxWoW.SleepForLagDuration();
                    }

                    return;
                }
            }

            if (FlameShockRenew())
            {
                Dlog("CombatElem: flame shock, so no more attacks cast this pass");
                return;
            }

            ElementalMastery();
            if (LavaBurst())
            {
                Dlog("CombatElem: lavaburst, so no more attacks cast this pass");
                return;
            }

            if (FulminationCheck())
                return;

            if (_countAoe12Enemy > 1 && ChainLightning())
            {
                Dlog("CombatElem: chain lightning, so no more attacks cast this pass");
                return;
            }

            if (_countTenYardEnemy > 1 && TotemExist(TOTEM_FIRE) && FireNova())
            {
                Dlog("CombatElem: firenova, so no more attacks cast this pass");
                return;
            }

            // earth shock now, but only if we don't have Fulmination talent and it won't interfere with Flame Shock DoT
            if (!_hasTalentFulmination && CanAnElemBuyAnEarthShock() && EarthShock())
            {
                Dlog("CombatElem: earth shock, so no more attacks cast this pass");
                return;
            }

            if (LightningBolt())
            {
                Dlog("CombatElem: lightningbolt, so no more attacks cast this pass");
                return;
            }

            if (CleanseIfNeeded(_me))
            {
                Dlog("CombatElem: cleanse, so no more attacks cast this pass");
                return;
            }

            Dlog("CombatElem: made it through entire pass without casting anything!!!!");
        }


        private void CombatElementalPVP()
        {
            if (!CurrentTargetInRangedDistance())
            {
                if (_me.Rooted)
                {
                    // options -- trinket, heal (anticipating being hit)
                    if (!FindBestTarget())
                    {
                        Slog("Rooted:  no target in range so waiting it out...");
                        return;
                    }
                }

                if (!CurrentTargetInRangedDistance())
                {
                    // chase until in ranged distance or for 8 seconds
                    Dlog("running to mob " + _me.CurrentTarget.Distance.ToString("F2") + " away");
                    MoveToCurrentTarget();
                    BestShockForMob();
                    return;
                }
            }

            if (_me.IsMoving)
            {
                Dlog("Stopping movement for Elemental combat");
                Safe_StopMoving();
            }

            if (!combatChecks())
            {
                Dlog("CombatElemPVP: failed combat checks so no attack cast this pass");
                return;
            }

#if NO_TOTEMS_IN_PVP
            // we already cast totems in pull, so this is just to see if we need to replace any if in crisis
            if (AllowNonHealSpells() && (countEnemy > 1 || (_me.GotTarget && _me.CurrentTarget.HealthPercent > 25)))
            {
                SetTotemsAsNeeded();
            }
#endif
            if (_countMeleeEnemy > 0 && CallForReinforcements())
            {
                Dlog("CombatElemPVP: CallForReinforcements() so no attack cast this pass");
                return;
            }

            if ((IsFightStressful() && _countMeleeEnemy > 1) || _me.ManaPercent < _cfg.EmergencyManaPercent)
            {
                if (Thunderstorm())
                {
                    Dlog("CombatElemPVP: Thunderstorm cast so no further attacks this pass");
                    return;
                }
            }

            if (FlameShock())
            {
                Dlog("CombatElemPVP: flame shock, so no more attacks cast this pass");
                return;
            }

            ElementalMastery();
            if (LavaBurst())
            {
                Dlog("CombatElemPVP: lavaburst, so no more attacks cast this pass");
                return;
            }

            if (ChainLightning())
            {
                Dlog("CombatElemPVP: chain lightning, so no more attacks cast this pass");
                return;
            }

            if (LightningBolt())
            {
                Dlog("CombatElemPVP: chain lightning, so no more attacks cast this pass");
                return;
            }

            Dlog("CombatElemPVP: made it through entire pass without casting anything!!!!");
        }

        private void ShowCurrentStatus()
        {
            Dlog("- status [me]:  h/m:{0:F1}%/{1:F1}%, melee:{2}, range:{3}, rooted:{4}, immobile:{5}, silenced:{6}",
                _me.HealthPercent,
                _me.ManaPercent,
                _countMeleeEnemy,
                _countRangedEnemy,
                _me.Rooted,
                MeImmobilized(),
                _me.Silenced
                );

            /*
                        if (_me.HealthPercent < 10)
                            ListWowUnitsInRange();
                        else if (_me.GotTarget)
                        {
                            Dlog("- my target:  {0}[{1}], dist:{2:F1} yds, h:{3:F1}%",
                                Safe_UnitName(_me.CurrentTarget),
                                _me.CurrentTarget.Level,
                                _me.CurrentTarget.Distance,
                                _me.CurrentTarget.HealthPercent
                                );
                        }
                        else
                        {
                            Dlog("my target:  none");
                        }
             */
        }

        private WoWPoint _pursuitStart;
        private readonly Stopwatch _pursuitTimer = new Stopwatch();

        public void PursuitBegin()
        {
            _pursuitTimer.Reset();
            _pursuitTimer.Start();
            _pursuitStart = _me.Location;
        }

        public bool InPursuit()
        {
            return _pursuitTimer.IsRunning;
        }

        public long PursuitTime
        { get { return _pursuitTimer.ElapsedMilliseconds; } }

        public WoWPoint PursuitOrigin
        { get { return _pursuitStart; } }


        #endregion

        #region Spells

        /// <summary>
        /// combatChecks() verifies the minimum necessary elements for combat between 
        /// _me and _me.CurrentTarget.  This verifies:
        ///     _me is alive
        ///     != null
        ///     .CurrentTarget is alive
        ///     .CurrentTarget is not self
        ///     .CurrentTarget is not my pet/totems/etc
        ///     _me is facing the .CurrentTarget
        ///     
        /// if no current target OR if .CurrentTarget is dead and still in combat or in a battleground
        ///     switch to the best available target
        ///     
        /// </summary>
        /// <returns>true - combat can continue
        /// false - unable to fight current target</returns>
        private bool combatChecks()
        {
            WoWUnit add = null;

            // if I am dead
            if (!_me.IsAlive)
            {
                ReportBodyCount();
                return false;
            }

            // if my target is dead
            if (_me.GotTarget && !_me.CurrentTarget.IsAlive)
            {
                ReportBodyCount();
                if (!_me.Combat && !_pvp.IsBattleground())
                    return false;
            }

            // if no target, or target is dead, or its a friendly target
            if (!_me.GotTarget || !_me.CurrentTarget.IsAlive || Safe_IsFriendly(_me.CurrentTarget))
            {
                if (_me.Combat || _pvp.IsBattleground() || IsRAF())
                {
                    if (_me.GotAlivePet && _me.Pet.GotTarget && _me.Pet.CurrentTarget.IsAlive)
                    {
                        add = _me.Pet.CurrentTarget;
                        Slog(">>> SET PETS TARGET: {0}-{1}[{2}]", add.Class, Safe_UnitName(add), add.Level);
                    }
                    else if (FindBestTarget())
                    {
                        add = _me.CurrentTarget;
                    }

                    if (add == null && !_me.IsInInstance)
                    {
                        // target an enemy totem (this just cleans up in some PVE fights)
                        List<WoWUnit> addList
                            = (from o in ObjectManager.ObjectList
                               where o is WoWUnit && o.Distance <= _maxDistForRangeAttack
                               let unit = o.ToUnit()
                               where unit.Attackable
                                && unit.IsAlive
                                && Safe_IsHostile(unit)
                                && unit.InLineOfSight
                                && unit.CreatedByUnitGuid != _me.Guid && unit.SummonedByUnitGuid != _me.Guid  // guard against my own totems being selected
                                && unit.CreatureType == WoWCreatureType.Totem
                               select unit
                                    ).ToList();

                        if (addList != null && addList.Count > 0)
                        {
                            add = addList.First();
                            Slog("Setting to enemy totem: {0}-{1}[{2}]", add.Class, add.Name, add.Level);
                        }
                    }

                    if (add != null)
                    {
                        if (!Safe_SetCurrentTarget(add))
                            return false;
                    }
                }

                if (!_me.GotTarget)
                {
                    Dlog("No Current Target and can't find adds -- why still in Combat()");
                    return false;
                }
            }

            if (_me.GotTarget)
            {
                if (_me.CurrentTarget.IsMe)
                {
                    Dlog("Targeting myself -- clearing and bailing out of Combat()");
                    _me.ClearTarget();
                    return false;
                }

                if (_me.CurrentTarget.CreatedByUnitGuid == _me.Guid || _me.CurrentTarget.SummonedByUnitGuid == _me.Guid)
                {
                    Slog("? HB targeted my own: " + Safe_UnitName(_me.CurrentTarget) + ", blacklisting ?");
                    AddToBlacklist(_me.CurrentTarget.Guid);
                    _me.ClearTarget();
                    return false;
                }

                if (!WoWMovement.IsFacing)
                    WoWMovement.Face();
            }

            return true;
        }


        /// <summary>
        /// pvpChecks() 
        /// Inspects the current target and handles certain PvP specific issues
        ///     blacklists pet
        ///     purges player defensive ability (iceblock, divine shield)
        /// </summary>
        /// <returns>
        /// true - continue with fighting
        /// false- can't fight, find new target if needed
        /// </returns>
        private bool pvpChecks()
        {
            if (!_me.GotTarget)
                return false;

            if (_pvp.IsBattleground() || (_me.CurrentTarget.IsPlayer && _me.IsHorde != _me.CurrentTarget.ToPlayer().IsHorde))
            {
                // check for things we can't fight and should blacklist
                if (_me.CurrentTarget.IsPet)
                {
                    Slog("PVP: Blacklisting pet " + Safe_UnitName(_me.CurrentTarget));
                    Blacklist.Add(_me.CurrentTarget.Guid, TimeSpan.FromMinutes(5));
                    _me.ClearTarget();
                    return false;
                }

                // test, if in battleground and someone is out of line of sight, blacklist for 5 seconds
                if (!_me.CurrentTarget.InLineOfSight)
                {
                    Slog("PVP: Target not in LoS, blacklisting for 2 seconds");
                    Blacklist.Add(_me.CurrentTarget.Guid, TimeSpan.FromSeconds(2));
                    _me.ClearTarget();
                    return false;
                }

                if (_typeShaman == ShamanType.Enhance && _me.CurrentTarget.Distance > _maxDistForRangeAttack)
                {
                    Slog("PVP: Target out of range (" + _me.CurrentTarget.Distance + " yds), blacklisting for 3 seconds");
                    Blacklist.Add(_me.CurrentTarget.Guid, TimeSpan.FromSeconds(3));
                    _me.ClearTarget();
                    return false;
                }

                // _me.CurrentTarget.GetBuffs(true);   // refresh buffs for checking need for blacklist or purge

                if (_me.CurrentTarget.Auras.ContainsKey("Divine Shield"))
                {
                    Slog("PVP: Palidan popped Divine Shield, blacklisted 10 secs");
                    Blacklist.Add(_me.CurrentTarget.Guid, TimeSpan.FromSeconds(10));
                    _me.ClearTarget();
                    return false;
                }

                if (_me.CurrentTarget.Auras.ContainsKey("Ice Block"))
                {
                    Slog("PVP: Mage popped Iceblock, blacklisted 10 secs");
                    Blacklist.Add(_me.CurrentTarget.Guid, TimeSpan.FromSeconds(10));
                    _me.ClearTarget();
                    return false;
                }

                if (Safe_KnownSpell("Purge") && SpellManager.Spells["Purge"].Cooldown)
                {
                    Dlog("PVP:  Purge on cooldown, skipping buff tests");
                }
                else if (_me.CurrentTarget.Auras.ContainsKey("Presence of Mind") && Purge())
                {
                    Slog("PVP: mage had Presence of Mind, purging");
                }
                else if (_me.CurrentTarget.Auras.ContainsKey("Blessing of Protection") && Purge())
                {
                    Slog("PVP: target has Blessing of Protection, purging");
                }
                else if (_me.CurrentTarget.Auras.ContainsKey("Avenging Wrath") && Purge())
                {
                    Slog("PVP: paladin used Avenging Wrath, purging");
                }

                else if (_me.CurrentTarget.Auras.ContainsKey("Power Word: Shield") && Purge())
                {
                    Slog("PVP: priest used Power Word: Shield, purging");
                }
                else if (_me.CurrentTarget.Auras.ContainsKey("Fear Ward") && Purge())
                {
                    Slog("PVP: target has Fear Ward, purging");
                }
            }

            return true;
        }

        #region ATTACK SPELLS

        private void AutoAttack()
        {
            Log(Color.DodgerBlue, "*Auto-Attack");
            RunLUA("StartAttack()");
        }

        private void StopAutoAttack()
        {
            Log(Color.DodgerBlue, "*Stop Auto-Attack");
            RunLUA("StopAttack()");
        }

        private bool CastCombatSpecials()
        {
            bool cast = false;

            if (combatChecks() && AllowNonHealSpells())
            {
                if (_pvp.IsBattleground() || IsFightStressful() || _me.CurrentTarget.HealthPercent > 75)
                {
                    /*
                                        if (_me.Auras.ContainsKey("Elemental Mastery"))
                                            ;
                                        else 
                     */
                    if (_me.Auras.ContainsKey("Berserking"))
                        ;
                    else if (_me.Auras.ContainsKey("Blood Fury"))
                        ;
                    else if (_me.Auras.ContainsKey("Bloodlust"))
                        ;
                    else if (_me.Auras.ContainsKey("Heroism"))
                        ;
                    else if (_me.Auras.ContainsKey("Elemental Mastery"))
                        ;
                    else
                    {
                        // Elemental Mastery is cast a part of rotation
                        cast = Berserking();  // trolls
                        if (!cast)
                            cast = BloodFury();   // orcs
                        if (!cast)
                            cast = BloodlustHeroism();   // horde and alliance shaman
                    }
                }
            }

            return cast;
        }

        private bool LightningBolt()
        {
            if (!combatChecks())
                ;
            else if (!HaveValidTarget() && !AllowNonHealSpells())
                ;
            else if (IsImmunneToNature(_me.CurrentTarget))
                Dlog("skipping Lightning Bolt since {0}[{1}] is immune to Nature damage", Safe_UnitName(_me.CurrentTarget), _me.CurrentTarget.Entry);
            else
            {
                Safe_StopMoving();
                return Safe_CastSpell("Lightning Bolt", SpellRange.Check, SpellWait.NoWait);
            }

            return false;
        }

        private static int EnemyCountInAOE(WoWUnit target, double distRadius)
        {
            int enemyCount = 0;
            Stopwatch timer = new Stopwatch();
            timer.Start();

            if (target != null)
            {
                try
                {
                    if (!_pvp.IsBattleground())
                    {
                        enemyCount = (from o in ObjectManager.ObjectList
                                      where o is WoWUnit && o.Location.Distance(target.Location) <= distRadius
                                      let unit = o.ToUnit()
                                      where unit != target
                                            && unit.Attackable
                                            && unit.IsAlive
                                            && unit.Combat
                                            && !IsMeOrMyGroup(unit)
                                            && IsTargetingMeOrMyGroup(unit)
                                            && !Blacklist.Contains(unit.Guid)
                                      orderby unit.CurrentHealth ascending
                                      select unit
                                     ).Count();
                    }
                    else
                    {
                        enemyCount = (from o in ObjectManager.ObjectList
                                      where o is WoWUnit && o.Location.Distance(target.Location) <= distRadius
                                      let unit = o.ToUnit()
                                      where unit != target
                                            && unit.IsAlive
                                            && unit.IsPlayer && unit.ToPlayer().IsHorde != ObjectManager.Me.IsHorde
                                            && !unit.IsPet
                                            && !Blacklist.Contains(unit.Guid)
                                      orderby unit.CurrentHealth ascending
                                      select unit
                                     ).Count();
                    }

                }
                catch (ThreadAbortException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Log(Color.Red, "An Exception occured. Check debug log for details.");
                    Logging.WriteDebug("HB EXCEPTION in EnemyCountInAOE()");
                    Logging.WriteException(e);
                }
            }

            Dlog("EnemyCountInAOE(): took {0} ms", timer.ElapsedMilliseconds);
            return enemyCount;
        }

#if NOT_RIGHT_NOW
        private static bool WillChainLightningHop(WoWUnit target)
        {
            return 2 >= EnemyCountInAOE(target, 12);
        }

        private static bool UseEarthquake(WoWUnit target)
        {
            return 3 >= EnemyCountInAOE(target, 8);
        }
#endif
        private bool ChainLightning()
        {
            if (!combatChecks())
                ;
            else if (!Safe_KnownSpell("Chain Lightning"))
                ;
            else if (!HaveValidTarget() || !AllowNonHealSpells())
                ;
            else
            {
                Safe_StopMoving();
                return Safe_CastSpellWithRangeCheck("Chain Lightning");
            }

            return false;
        }

        private bool ShamanisticRage()
        {
            if (!combatChecks())
                ;
            else if (!Safe_KnownSpell("Shamanistic Rage"))
                ;
            else
            {
                return Safe_CastSpell("Shamanistic Rage");
            }

            return false;
        }

        private bool EarthShock()
        {
            // NOTE:  with patch 3.3 of WoW using Shock spells is not as much of a concern
            // ... since they drastically increased the mana regen
            // --- SO, force it to ignore Water Shield for now
            if (!combatChecks())
                Dlog("EarthShock:  failed combat check");
            else if (!HaveValidTarget() || !AllowNonHealSpells())
                ;
            else if (!Safe_KnownSpell("Earth Shock"))
                ;
            else if (IsImmunneToNature(_me.CurrentTarget))
                Dlog("skipping Earth Shock since {0}[{1}] is immune to Nature damage", Safe_UnitName(_me.CurrentTarget), _me.CurrentTarget.Entry);
            else
                return Safe_CastSpell("Earth Shock", SpellRange.Check, SpellWait.NoWait);

            return false;
        }

        private bool CanAnElemBuyAnEarthShock()
        {
            if (!_me.GotTarget)
                return false;

            bool permitEarthShock = false;
            WoWAura fsa = GetAuraCreatedByMe(_me.CurrentTarget, "Flame Shock");
            if (fsa == null)
                permitEarthShock = !Safe_KnownSpell("Flame Shock");
            else
            {
                WoWSpell fss = SpellManager.Spells["Flame Shock"];
                permitEarthShock = fsa.TimeLeft.TotalMilliseconds > 6000;
                Dlog("CanBuyEarthShock:  flame shock DoT left={0}", fsa.TimeLeft.TotalMilliseconds);
            }

            return permitEarthShock;
        }

        private bool FlameShock()
        {
            // NOTE:  with patch 3.3 of WoW using Shock spells is not as much of a concern
            // ... since they drastically increased the mana regen
            // --- SO, force it to ignore Water Shield for now
            if (!combatChecks())
                Dlog("FlameShock:  failed combat check");
            else if (!HaveValidTarget() || !AllowNonHealSpells())
                ;
            else if (!Safe_KnownSpell("Flame Shock"))
                ;
            else if (IsImmunneToFire(_me.CurrentTarget))
                Dlog("skipping Flame Shock since {0}[{1}] is immune to Fire damage", Safe_UnitName(_me.CurrentTarget), _me.CurrentTarget.Entry);
            else if (_me.CurrentTarget.Auras.ContainsKey("Flame Shock"))
                Dlog("FlameShock:  target already has DoT");
            else
                return Safe_CastSpell("Flame Shock", SpellRange.Check, SpellWait.NoWait);

            return false;
        }

        private bool FlameShockRenew()
        {
            // NOTE:  with patch 3.3 of WoW using Shock spells is not as much of a concern
            // ... since they drastically increased the mana regen
            // --- SO, force it to ignore Water Shield for now
            if (!combatChecks())
                Dlog("FlameShock:  failed combat check");
            else if (!HaveValidTarget() || !AllowNonHealSpells())
                ;
            else if (!Safe_KnownSpell("Flame Shock"))
                ;
            else if (IsImmunneToFire(_me.CurrentTarget))
                Dlog("skipping Flame Shock since {0}[{1}] is immune to Fire damage", Safe_UnitName(_me.CurrentTarget), _me.CurrentTarget.Entry);
            else
            {
                // following code checks to make sure that DoT won't 
                // ... fall off before Lava Burst cast completes
                WoWAura fs = GetAuraCreatedByMe(_me.CurrentTarget, "Flame Shock");
                if (fs != null)
                {
                    if (!SpellManager.HasSpell("Lava Burst"))
                        return false;

                    WoWSpell lvb = SpellManager.Spells["Lava Burst"];
                    if ((200 + lvb.CastTime) < fs.TimeLeft.TotalMilliseconds)
                        return false;

                    Dlog("FlameShock:  DoT only has {0} ms left, so renewing", fs.TimeLeft.TotalMilliseconds);
                }

                return Safe_CastSpell("Flame Shock", SpellRange.Check, SpellWait.NoWait);
            }
            return false;
        }


        private bool FrostShock()
        {
            if (!combatChecks())
                ;
            else if (!HaveValidTarget() && !AllowNonHealSpells())
                ;
            else if (!Safe_KnownSpell("Frost Shock"))
                ;
            else if (IsImmunneToFrost(_me.CurrentTarget))
                Dlog("skipping Frost Shock since {0}[{1}] is immune to Frost damage", Safe_UnitName(_me.CurrentTarget), _me.CurrentTarget.Entry);
            else if (_me.CurrentTarget.Auras.ContainsKey("Frost Shock"))
                ;
            else
                return Safe_CastSpell("Frost Shock", SpellRange.Check, SpellWait.NoWait);

            return false;
        }

        /*
         * Summary:  determines the best Shock spell opener to
         * use.  This is for use during pull only.  
         */
        private bool ShockOpener()
        {
            bool bCast = false;

            if (_me.GotTarget && !IsHealerOnly())
            {
                if (_me.CurrentTarget.IsPlayer || _me.CurrentTarget.Fleeing)
                    bCast = FrostShock();

                if (!bCast)
                    bCast = FlameShock();

                if (!bCast)
                    bCast = EarthShock();

                if (!bCast)
                    bCast = FrostShock();
            }

            return bCast;
        }

        private bool BestShockForMob()
        {
            bool knowFrost = Safe_KnownSpell("Frost Shock");
            bool knowEarth = Safe_KnownSpell("Earth Shock");
            bool knowFire = Safe_KnownSpell("Fire Shock");

            if (!combatChecks())
                return false;

            if (knowFire && _me.CurrentTarget.Class == WoWClass.Rogue && FlameShock())
                return true;

            if (knowFrost && (_me.CurrentTarget.Fleeing || _me.CurrentTarget.IsPlayer) && FrostShock())
                return true;

            if (knowEarth && EarthShock())
                return true;

            if (knowFire && FlameShock())
                return true;

            //	    if (knowFrost && FrostShock())
            //		return true;

            return false;
        }

        private bool PrimalStrike()
        {
            if (!combatChecks())
                ;
            else if (!HaveValidTarget() || !AllowNonHealSpells())
                ;
            else if (Safe_KnownSpell("Stormstrike"))        // never use if we know Stormstrike
                ;                                           // .. since they share a cooldown
            else if (!Safe_KnownSpell("Primal Strike"))
                ;
            else if (!CurrentTargetInMeleeDistance())
                ;
            else
                return Safe_CastSpell("Primal Strike", SpellRange.NoCheck, SpellWait.NoWait);

            return false;
        }

        private bool Stormstrike()
        {
            if (!combatChecks())
                ;
            else if (!HaveValidTarget() || !AllowNonHealSpells())
                ;
            else if (!Safe_KnownSpell("Stormstrike"))
                ;
            else if (!CurrentTargetInMeleeDistance())
                ;
            else
                return Safe_CastSpell("Stormstrike", SpellRange.NoCheck, SpellWait.NoWait);

            return false;
        }

        private bool IsStormstrikeNeeded()
        {
            if (Safe_KnownSpell("Stormstrike"))
            {
                if (HaveValidTarget() && !_me.CurrentTarget.Auras.ContainsKey("Stormstrike"))
                {
                    if (CurrentTargetInMeleeDistance())
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool LavaLash()
        {
            if (!combatChecks())
                ;
            else if (!HaveValidTarget() && !AllowNonHealSpells())
                ;
            else if (!Safe_KnownSpell("Lava Lash"))
                ;
            else if (!CurrentTargetInMeleeDistance())
                ;
            else if (IsImmunneToFire(_me.CurrentTarget))
                Dlog("skipping Lava Lash since {0}[{1}] is immune to Fire damage", Safe_UnitName(_me.CurrentTarget), _me.CurrentTarget.Entry);
            else
                return Safe_CastSpell("Lava Lash", SpellRange.NoCheck, SpellWait.NoWait);

            return false;
        }

        private bool LavaBurst()
        {
            if (!combatChecks())
                ;
            else if (!HaveValidTarget() && !AllowNonHealSpells())
                ;
            else if (!Safe_KnownSpell("Lava Burst"))
                ;
            else if (IsImmunneToFire(_me.CurrentTarget))
                Dlog("skipping Lava Burst since {0}[{1}] is immune to Fire damage", Safe_UnitName(_me.CurrentTarget), _me.CurrentTarget.Entry);
            else
            {
                return Safe_CastSpell("Lava Burst", SpellRange.Check, SpellWait.NoWait);
            }

            return false;
        }

        private bool ElementalMastery()
        {
            if (!combatChecks())
                ;
            else if (!HaveValidTarget() && !AllowNonHealSpells())
                ;
            else if (!Safe_KnownSpell("Elemental Mastery"))
                ;
            else
                return Safe_CastSpell("Elemental Mastery", SpellRange.NoCheck, SpellWait.NoWait);

            return false;
        }

        private bool Thunderstorm()
        {
            if (!combatChecks())
                ;
            else if (IsRAF() && !_cfg.RAF_UseThunderstorm)
                ;
            else if (!Safe_KnownSpell("Thunderstorm"))
                ;
            else
                return Safe_CastSpell("Thunderstorm", SpellRange.NoCheck, SpellWait.NoWait);

            return false;
        }

        private bool FireNova()
        {
            if (!combatChecks())
                ;
            else if (!HaveValidTarget() && !AllowNonHealSpells())
                ;
            else if (!Safe_KnownSpell("Fire Nova"))
                ;
            else if (IsImmunneToFire(_me.CurrentTarget))
                Dlog("skipping Fire Nova since {0}[{1}] is immune to Fire damage", Safe_UnitName(_me.CurrentTarget), _me.CurrentTarget.Entry);
            else if (_ptTotems.Distance(_me.CurrentTarget.Location) >= 10f)
                Dlog("Fire Totem is {0:F1} yds away, Fire Nova not cast", _ptTotems.Distance(_me.Location));
            else if (!TotemExist(TOTEM_FIRE)
                    || !(_totemName[TOTEM_EARTH].ToUpper() == "MAGMA TOTEM"
                        || _totemName[TOTEM_EARTH].ToUpper() == "FLAMETONGUE TOTEM"
                        || _totemName[TOTEM_EARTH].ToUpper() == "FIRE ELEMENTAL TOTEM")
                    )
                Dlog("Magma/Flametongue/Fire elemental totem doesn't exist, Fire Nova not cast");
            else
                return Safe_CastSpell("Fire Nova", SpellRange.NoCheck, SpellWait.NoWait);

            return false;
        }

        private bool MaelstromCheck()
        {
            if (!_hasTalentMaelstromWeapon)
                ;
            else if (!combatChecks())
                ;
            else if (!HaveValidTarget())
                ;
            else
            {
                uint stackCount;
                if (!IsAuraPresent(_me, "Maelstrom Weapon", out stackCount))
                    ;
                else if (stackCount < 4)
                    ;   // ignore... we are looking for 4 for emergency heal or 5 for attack
                else
                {
                    if (stackCount >= 4 && _me.HealthPercent < _cfg.EmergencyHealthPercent)
                    {
                        Log("^Maelstrom Heal @ " + stackCount + " stks");
                        bool goodHeal = HealPlayer(_me);
                        return goodHeal;
                    }
                    else if (stackCount == 5)
                    {

                        Log("^Maelstrom Attack @ " + stackCount + " stks");
                        if (IsStormstrikeNeeded())
                            Stormstrike();  // throw a Stormstrike if debuff not up
                        if (_countAoe12Enemy > 1)
                            return ChainLightning();
                        else
                            return LightningBolt();
                    }
                }
            }

            return false;
        }


        private bool SearingFlamesCheck()
        {
            if (!_hasTalentImprovedLavaLash)
                ;
            else if (!combatChecks())
                ;
            else if (!HaveValidTarget())
                ;
            else if (!Safe_KnownSpell("Lava Lash"))
                ;
            else if (!CurrentTargetInMeleeDistance())
                ;
            else if (IsImmunneToFire(_me.CurrentTarget))
                Slog("SearingFlames: skipping Lava Lash because mob is Fire immune");
            else
            {
                uint stackCount = GetAuraStackCount(_me.CurrentTarget, "Searing Flames");
                Dlog("SearingFlameCheck: found {0} stacks", stackCount);
                if (stackCount >= 5)
                {
                    Slog("^Searing Flames @ " + stackCount + " stks");
                    if (Safe_CastSpell("Lava Lash", SpellRange.NoCheck, SpellWait.NoWait))
                    {
                        Dlog("CombatElem: Lava Lash cast so no further attacks this pass");
                        return true;
                    }
                }
            }

            return false;
        }


        private bool FulminationCheck()
        {
            if (!_hasTalentFulmination)
                ;
            else if (!combatChecks())
                ;
            else if (!HaveValidTarget())
                ;
            else if (Safe_KnownSpell("Earth Shock"))
            {
                uint stackCount = GetAuraStackCount(_me, "Lightning Shield");
                Dlog("FulminationCheck:  Lightning Shields stack count is {0}", stackCount);
                if (stackCount >= 9)
                {
                    if (IsImmunneToNature(_me.CurrentTarget))
                        Slog("FulminationCheck: skipping Earth Shock because mob is Nature immune");
                    else if (!SpellManager.CanCast("Earth Shock"))
                        Slog("Earth Shield on Cooldown... waiting on Fulmination cast", stackCount);
                    else
                    {
                        Slog("^Fulmination at {0} stacks", stackCount);
                        if (Safe_CastSpell("Earth Shock", SpellRange.Check, SpellWait.NoWait))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool InterruptEnemyCast()
        {
            if (!combatChecks())
                Dlog("Failed combat checks - Wind Shear not cast");
            else if (!HaveValidTarget())
                Dlog("Don't have a valid target - Wind Shear not cast");
            else if (!AllowNonHealSpells())
                Dlog("Non-healing spells not allowed now - Wind Shear not cast");
            else if (!Safe_KnownSpell("Wind Shear"))
                Dlog("Have not trained spell yet - Wind Shear not cast");
            else if (_me.CurrentTarget.IsCasting)
            {
                List<string> ret = Lua.GetReturnValues("return UnitCastingInfo(\"target\")");
                if (ret != null && ret.Count >= 9)
                {
                    Dlog("InterruptEnemyCast:  canInterrupt {0}='{1}'", ret[0], ret[8]);
                    if (!string.IsNullOrEmpty(ret[8]))
                    {
                        if (Safe_CastSpell(_me.CurrentTarget, "Wind Shear", SpellRange.Check, SpellWait.NoWait))
                            return true;
                        else if (_me.CurrentTarget.Distance < 8)
                            return Warstomp();
                    }
                }
            }

            return false;
        }

        private bool Purge()
        {
            if (!combatChecks())
                Dlog("Failed combat checks - Purge not cast");
            else if (!AllowNonHealSpells())
                Dlog("Non-healing spells not allowed now - Purge not cast");
            else if (!Safe_KnownSpell("Purge"))
                Dlog("Have not trained spell yet - Purge not cast");
            else if (Safe_CastSpell("Purge", SpellRange.Check, SpellWait.NoWait))
                return true;

            return false;
        }

        private bool FeralSpirit()
        {
            bool castGood = false;

            if (_cfg.TotemsDisabled)
                return false;

            if (IsRAF() && _cfg.RAF_SaveFeralSpiritForBosses && (!_me.GotTarget || !_me.CurrentTarget.IsAlive || _me.CurrentTarget.CreatureRank != WoWUnitClassificationType.WorldBoss))
            {
                Dlog("Feral Spirit:  not cast because not currently targeting a boss");
            }
            else if (!(_pvp.IsBattleground() || IsFightStressful() || !_cfg.PVE_SaveForStress_FeralSpirit))
            {
                Dlog("Feral Spirit:  not cast because  InBattleground={0}, IsFightStressful()={1}, and SaveForStress={2}",
                    _pvp.IsBattleground(),
                    IsFightStressful(),
                    _cfg.PVE_SaveForStress_FeralSpirit
                    );
            }
            else
            {
                if (Safe_KnownSpell("Feral Spirit"))
                {
                    castGood = Safe_CastSpell("Feral Spirit", SpellRange.NoCheck, SpellWait.Complete);
                    if (castGood)
                    {
                        Log("^Pet Defensive Mode");
                        RunLUA("PetDefensiveMode()");     // turn on defensive mode

                        Log("^Pet Attack");
                        RunLUA("PetAttack()");     // turn on defensive mode

                        if (!_pvp.IsBattleground())
                        {
                            Log("^Pet Ability - Twin Howl");
                            RunLUA("CastPetAction(5)");       // throw a twin howl immediately
                        }
                        else if (_me.Rooted || MeImmobilized())
                        {
                            Log("^Pet Ability - Spirit Walk (remove movement impairing effects)");
                            Safe_CastSpell("Spirit Walk");
                        }
                    }
                }
            }

            return castGood;
        }


        private bool EarthElementalTotem()
        {
            bool castGood = false;

            if (!Safe_KnownSpell("Earth Elemental Totem"))
                ;
            else if (IsRAF() && _cfg.RAF_SaveElementalTotemsForBosses && (!_me.GotTarget || !_me.CurrentTarget.IsAlive || _me.CurrentTarget.CreatureRank != WoWUnitClassificationType.WorldBoss))
            {
                Dlog("Earth Elemental Totem:  not cast because not currently targeting a boss");
            }
            else if (!IsRAF() && !_pvp.IsBattleground() && !IsFightStressful() && _cfg.PVE_SaveForStress_ElementalTotems)
            {
                Dlog("Earth Elemental Totem:  not cast because  InBattleground={0}, IsFightStressful()={1}, and SaveForStress={2}",
                    _pvp.IsBattleground(),
                    IsFightStressful(),
                    _cfg.PVE_SaveForStress_ElementalTotems
                    );
            }
            else
            {
                castGood = TotemCast(TOTEM_EARTH, "Earth Elemental Totem");
            }

            return castGood;
        }

        private bool FireElementalTotem()
        {
            bool castGood = false;

            if (!Safe_KnownSpell("Fire Elemental Totem"))
                ;
            else if (IsRAF() && _cfg.RAF_SaveElementalTotemsForBosses && (!_me.GotTarget || !_me.CurrentTarget.IsAlive || _me.CurrentTarget.CreatureRank != WoWUnitClassificationType.WorldBoss))
            {
                Dlog("Fire Elemental Totem:  not cast because not currently targeting a boss");
            }
            else if (!IsRAF() && !_pvp.IsBattleground() && !IsFightStressful() && _cfg.PVE_SaveForStress_ElementalTotems)
            {
                Dlog("Fire Elemental Totem:  not cast because  InBattleground={0}, IsFightStressful()={1}, and SaveForStress={2}",
                    _pvp.IsBattleground(),
                    IsFightStressful(),
                    _cfg.PVE_SaveForStress_ElementalTotems
                    );
            }
            else
            {
                castGood = TotemCast(TOTEM_FIRE, "Fire Elemental Totem");
            }

            return castGood;
        }

        private bool CallForReinforcements()
        {
            if (!_pvp.IsBattleground() && _cfg.PVE_CombatStyle == ConfigValues.PveCombatStyle.DisableTotemsCDs)
                return false;

            if (_me.GotAlivePet)
                return false;

            if (TotemExist(TOTEM_EARTH) && TotemName(TOTEM_EARTH) == "Earth Elemental Totem")
                return false;

            if (TotemExist(TOTEM_FIRE) && TotemName(TOTEM_EARTH) == "Fire Elemental Totem")
                return false;

            if (FeralSpirit())
                return true;

            if (_pvp.IsBattleground())
            {
                if (!IsHealerOnly())
                {
                    if (FireElementalTotem())
                        return true;

                    if (EarthElementalTotem())
                        return true;
                }
            }
            else if (IsRAF())
            {
                // in instances, use Fire Elemental on bosses if DPS
                if (!IsHealerOnly() && FireElementalTotem())
                    return true;

                // in instances, use Earth Elemental if tank in trouble
                if (RaFHelper.Leader.HealthPercent < 25)
                {
                    if (EarthElementalTotem())
                        return true;
                }
            }
            else // grinding and questing
            {
                if (EarthElementalTotem())
                    return true;

                if (FireElementalTotem())
                    return true;
            }

            return false;
        }

        private bool BloodlustHeroism()
        {
            if (!_pvp.IsBattleground() && _cfg.PVE_CombatStyle == ConfigValues.PveCombatStyle.DisableTotemsCDs)
                return false;

            if (_pvp.IsBattleground())
            {
                ; // okay to have a trigger finger here
            }
            else if (IsRAF())
            {
                if (!_cfg.RAF_UseBloodlustOnBosses)
                    return false;
                if (RaFHelper.Leader.GotTarget && RaFHelper.Leader.CurrentTarget.CreatureRank != WoWUnitClassificationType.WorldBoss)
                    return false;
            }
            else
            {
                if (_cfg.PVE_CombatStyle == ConfigValues.PveCombatStyle.DisableTotemsCDs)
                    return false;
                if (_cfg.PVE_SaveForStress_TotemsSelected && !IsFightStressful())
                    return false;
            }

            bool knowBloodlust = Safe_KnownSpell("Bloodlust");
            bool knowHeroism = Safe_KnownSpell("Heroism");
            if (!knowBloodlust && !knowHeroism)
                ;
            else if (_me.Debuffs.ContainsKey("Sated"))
                ;
            else if (knowBloodlust && Safe_CastSpell("Bloodlust", SpellRange.NoCheck, SpellWait.NoWait))
            {
                Slog("Bloodlust: just broke out a major can of whoop a$$!");
                return true;
            }
            else if (knowHeroism && Safe_CastSpell("Heroism", SpellRange.NoCheck, SpellWait.NoWait))
            {
                Slog("Heroism: just broke out a major can of whoop a$$!");
                return true;
            }

            return false;
        }

        #endregion

        #region HEALING

        private bool HealingWave()
        {
            return Safe_CastSpell("Healing Wave");
        }

        private bool HealingSurge()
        {
            if (!Safe_KnownSpell("Healing Surge"))
                return false;
            return Safe_CastSpell("Healing Surge");
        }

        private bool CanUsePotion()
        {
            return !_potionTimer.IsRunning || _potionTimer.ElapsedMilliseconds > 60 * 1000;
        }

        private bool UseManaPotionIfAvailable()
        {
            return UsePotion(CheckForItem(_potionManaEID));
        }

        private bool UseHealthPotionIfAvailable()
        {
            return UsePotion(CheckForItem(_potionHealthEID));
        }

        private bool UsePotion(WoWItem potion)
        {
            if (potion != null)
            {
                if (MeImmobilized())
                    Slog("Immobilized -- unable to use potion now");
                else
                {
                    if (CanUsePotion())
                    {
                        Slog("POTION:  Using '" + potion.Name + "'");
                        Dlog("{0} has a cooldown of {1}", potion.Name, potion.Cooldown);
                        RunLUA("UseItemByName(\"" + potion.Name + "\")");
                        _potionTimer.Reset();
                        _potionTimer.Start();
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool UseConsumeable(string sItem)
        {
            WoWItem item = CheckForItem(sItem);
            if (item != null)
            {
                item.Use();
                StyxWoW.SleepForLagDuration();
            }

            return item != null;
        }

        private bool UseBandageIfAvailable()
        {
            WoWItem bandage = CheckForItem(_bandageEID);
            if (_cfg.UseBandages && !SpellManager.Spells.ContainsKey("First Aid"))
            {
                Wlog("Use Bandages ignored : your Shaman has not trained First Aid");
            }
            else if (bandage == null)
                Wlog("FIRST-AID:  no bandages in inventory");
            else if (_me.Debuffs.ContainsKey("Recently Bandaged"))
                Dlog("FIRST-AID:  can't bandage -- currently under 'Recently Bandaged' debuff");
            else if (!MeImmobilized())
            {
                foreach (KeyValuePair<string, WoWAura> dbf in _me.Debuffs)
                {
                    if (!dbf.Value.IsHarmful)
                        continue;
                    Dlog("FIRST-AID:  can't bandage -- harmful non-dispellable debuff '{0}' active", dbf.Key);
                    return false;
                }

                Safe_StopMoving();

                double healthStart = _me.HealthPercent;
                Stopwatch timeBandaging = new Stopwatch();
                Slog("FIRST-AID:  using '{0}' at {1:F0}%", bandage.Name, _me.HealthPercent);
                timeBandaging.Start();

                try
                {
                    RunLUA("UseItemByName(\"" + bandage.Name + "\", \"player\")");
                    do
                    {
                        Thread.Sleep(100);
                        Dlog("dbg firstaid:  buff-present:{0}, casting:{1}, channeled:{2}",
                            IsAuraPresent(_me, "First Aid"),
                            _me.IsCasting,
                            _me.ChanneledCastingSpellId != 0);
                    } while ((IsAuraPresent(_me, "First Aid") || timeBandaging.ElapsedMilliseconds < 1000) && _me.HealthPercent < 100.0);
                }
                catch (ThreadAbortException) { throw; }
                catch (Exception e)
                {
                    Log(Color.Red, "An Exception occured. Check debug log for details.");
                    Logging.WriteDebug("WOW LUA Call to UseItemByName() failed");
                    Logging.WriteException(e);
                }

                Dlog("FIRST-AID:  used {0} for {1:F1} secs ending at {2:F0}%", bandage.Name, timeBandaging.Elapsed.TotalSeconds, _me.HealthPercent);
                if (healthStart < _me.HealthPercent)
                    return true;
            }

            return false;
        }


        #endregion

        private bool ShieldBuffNeeded(bool atRest)
        {
            bool bKnowBuffSpell = Safe_KnownSpell("Water Shield") || Safe_KnownSpell("Lightning Shield") || Safe_KnownSpell("Earth Shield");
            bool bNeedBuff = !_me.Auras.ContainsKey("Water Shield") && !_me.Auras.ContainsKey("Lightning Shield") && !_me.Auras.ContainsKey("Earth Shield");

            if (atRest && !bNeedBuff && _me.Auras.ContainsKey("Water Shield"))
            {
                if (GetAuraStackCount(_me, "Water Shield") < 3)
                    bNeedBuff = true;
            }

            if (atRest && !bNeedBuff && _me.Auras.ContainsKey("Lightning Shield"))
            {
                if (GetAuraStackCount(_me, "Lightning Shield") < 3)
                    bNeedBuff = true;
            }

            if (atRest && !bNeedBuff && _me.Auras.ContainsKey("Earth Shield"))
            {
                if (GetAuraStackCount(_me, "Earth Shield") < 3)
                    bNeedBuff = true;
            }

            if (!bNeedBuff && IsRAF() && _typeShaman == ShamanType.Resto && Safe_KnownSpell("Earth Shield"))
            {
                bNeedBuff = RaFHelper.Leader != null && RaFHelper.Leader.IsAlive
                        && !RaFHelper.Leader.Auras.ContainsKey("Earth Shield");
            }

            return bKnowBuffSpell && bNeedBuff;
        }

        /*
         * ShieldTwisting()
         * 
         * Implement a technique known as shield twisting where
         * you alternate between a damage shield and a mana restoration 
         * shield.  basically make sure one or the other is active.
         * 
         * Here is the general approach in priority order:
         * 
         * If Mana is low, then force Mana restoration
         * If Mana is full, then force Damage shield
         * Otherwise alterante between shield types
         * 
         * This is a Level 20 and Higher Technique
         */
        private void ShieldTwisting(bool atRest)
        {
            bool castShield = false;
            bool useWaterShieldThisTime = false;
            SpellWait sw = atRest ? SpellWait.Complete : SpellWait.NoWait;

            // for Resto Shaman in BGs, cast Earth Shield on self
            if (_typeShaman == ShamanType.Resto && _pvp.IsBattleground() && Safe_KnownSpell("Earth Shield"))
            {
                uint uEarthStacks = GetAuraStackCount(_me, "Earth Shield");
                if (uEarthStacks == 0 || (uEarthStacks < 3 && atRest))
                    castShield = Safe_CastSpell(_me, "Earth Shield", SpellRange.NoCheck, sw);
            }
            // for Resto in RAF, cast Water Shield on self
            else if (_typeShaman == ShamanType.Resto && IsRAF() && Safe_KnownSpell("Water Shield"))
            {
                uint uWaterStacks = GetAuraStackCount(_me, "Water Shield");
                if (uWaterStacks == 0 || (uWaterStacks < 3 && atRest))
                    castShield = Safe_CastSpell(_me, "Water Shield", SpellRange.NoCheck, sw);
            }
            else
            {
                bool trainedWaterShield = Safe_KnownSpell("Water Shield");
                bool trainedLightningShield = Safe_KnownSpell("Lightning Shield");
                if (!trainedLightningShield && !trainedWaterShield)
                    return;

                uint uWaterStacks = GetAuraStackCount(_me, "Water Shield");
                uint uLightningStacks = GetAuraStackCount(_me, "Lightning Shield");


                // check if water shield required
                if (trainedWaterShield && _me.ManaPercent <= _cfg.TwistManaPercent && (uWaterStacks == 0 || (atRest && uWaterStacks < 3)))
                {
                    castShield = true;
                    useWaterShieldThisTime = true;
                }
                // check if lightning shield required
                else if (trainedLightningShield && _me.ManaPercent > _cfg.TwistDamagePercent && (uLightningStacks == 0 || (atRest && uLightningStacks < 3)))
                {
                    castShield = true;
                    useWaterShieldThisTime = false;
                }
                // now check if missing a shield and need to Twist
                else if ((uWaterStacks + uLightningStacks) == 0 || (atRest && (uWaterStacks + uLightningStacks) < 3))
                {
                    useWaterShieldThisTime = !_waterShieldUsedLast ? trainedWaterShield : trainedLightningShield;
                    castShield = (trainedWaterShield || trainedLightningShield);
                }

#if DEBUG
                if ( !ShieldBuffNeeded(atRest) != !castShield)
                {
                    
                }
#endif
                if (castShield)
                {
                    string sShield = useWaterShieldThisTime ? "Water Shield" : "Lightning Shield";
                    castShield = Safe_CastSpell(sShield, SpellRange.NoCheck, sw);

                    if (castShield)
                    {
                        _waterShieldUsedLast = useWaterShieldThisTime;
                    }
                }
            }

            if (IsRAF() && _typeShaman == ShamanType.Resto && Safe_KnownSpell("Earth Shield"))
            {
                if (RaFHelper.Leader.IsAlive)
                {
                    uint uEarthStacks = GetAuraStackCount(RaFHelper.Leader, "Earth Shield");
                    if (uEarthStacks == 0 || (!_me.Combat && !RaFHelper.Leader.Combat && uEarthStacks < 5))
                    {
                        if (IsUnitInRange(RaFHelper.Leader, 35.0))
                            Safe_CastSpell(RaFHelper.Leader, "Earth Shield", SpellRange.NoCheck, sw);
                        else
                        {
                            MoveToUnit(RaFHelper.Leader);

                            double needHealPercent = _pvp.IsBattleground() ? _cfg.PVP_GroupNeedHeal : _cfg.RAF_GroupNeedHeal;
                            if (_me.CurrentHealth < needHealPercent)
                            {
                                if (Safe_KnownSpell("Riptide"))
                                {
                                    Safe_CastSpell(RaFHelper.Leader, "Riptide", SpellRange.NoCheck, SpellWait.NoWait);
                                }
                            }
                        }
                    }
                }

                /*
                List<WoWPlayer> grp = _me.IsInRaid ? _me.RaidMembers : _me.PartyMembers;
                List<WoWPlayer> mbrs = (from m in grp
                                        where m.IsAlive
                                        orderby m.MaxHealth descending
                                        select m
                            ).ToList();

                if (mbrs != null && mbrs.Count > 0)
                {
                    WoWPlayer tank = mbrs[0];
                    if (!tank.Auras.ContainsKey("Earth Shield"))
                    {
                        Safe_SetCurrentTarget(tank);
                        Safe_CastSpell("Earth Shield");
                    }
                }
                */
            }

        }


        /*
         * Summary: inspects the list of WoWUnits for one that is within the
         *	    maximum distance provided of the pt given.
         *          
         * Returns: true if clear for atleast that distance
         */
        private static bool CheckForSafeDistance(string reason, WoWPoint pt, double dist)
        {
            WoWUnit unitClose = null;
            Stopwatch timer = new Stopwatch();

            timer.Start();
            try
            {
                if (!_pvp.IsBattleground())
                    unitClose = ObjectManager.GetObjectsOfType<WoWUnit>(false).Find(
                            unit => unit != null
                                && pt.Distance(unit.Location) < dist
                                && unit.Attackable
                                && (Safe_IsHostile(unit) || Safe_IsProfileMob(unit))
                                && unit.IsAlive
                            );
                else
                    unitClose = ObjectManager.GetObjectsOfType<WoWPlayer>(false).Find(
                            unit => unit != null
                                && pt.Distance(unit.Location) < dist
                                && unit.IsPlayer
                                && unit.IsAlive
                                && unit.IsHorde != ObjectManager.Me.IsHorde
                            );

                if (unitClose == null)
                    Dlog("{0} CheckForSafeDistance({1:F1}): no hostiles/profile mobs in range - took {2} ms", reason, dist, timer.ElapsedMilliseconds);
                else
                    Dlog("{0} CheckForSafeDistance({1:F1}): saw {2}{3} - {4}[{5}] around {6:F0} yds away", // - took {6} ms",
                        reason,
                        dist,
                        (unitClose.IsTargetingMeOrPet ? "*" : ""),
                        unitClose.Class,
                        unitClose.Name,
                        unitClose.Level,
                        5 * Math.Round(pt.Distance(unitClose.Location) / 5)
                        //			, timer.ElapsedMilliseconds
                        );
            }
            catch (ThreadAbortException) { throw; }
            catch (Exception e)
            {
                Log(Color.Red, "An Exception occured. Check debug log for details.");
                Logging.WriteDebug("HB EXCEPTION in CheckForSafeDistance()");
                Logging.WriteException(e);
            }

            return unitClose == null;
        }


        private static int _timerLastList = System.Environment.TickCount;

        public static void ListWowUnitsInRange()
        {
            Logging.Write("TEST FOR DUP ELIM");
            Logging.Write("TEST FOR DUP ELIM");
            Logging.Write(Color.DodgerBlue, "*TEST FOR DUP ELIM");
            Logging.Write(Color.DodgerBlue, "*TEST FOR DUP ELIM");
            foreach (KeyValuePair<string, WoWAura> dbf in ObjectManager.Me.Auras)
            {
                WoWAura aura = dbf.Value;

                Slog(Color.Green, "Aura:{0} Id:{1} Ends:{2} Dur:{3} TimeLeft:{4} Spell:{5} SpellId:{6}",
                    aura.Name, aura.SpellId, aura.EndTime, aura.Duration, aura.TimeLeft,
                    aura.Spell.Name, aura.Spell.Id
                    );
            }

#if COMMENTED_OUT 
            // find a specific target
            Slog("!!!! HEAL TEST !!!!!");
            WoWUnit healTarget = ObjectManager.Me.PartyMember1;
            if (healTarget == null)
            {
                Slog("unit NOT FOUND");
                return;
            }

            Slog("found unit named: {0}", healTarget.Name);

            Slog("Finding spell");
            WoWSpell spell = SpellManager.Spells["Healing Wave"];
            Slog("Checking if ok to cast");

            if (ObjectManager.Me.Location.Distance(healTarget.Location) >= spell.MaxRange)
            {
                Slog("Heal target out of range");
                return;
            }

            if (ObjectManager.Me.Location.Distance(healTarget.Location) >= spell.MaxRange)
            {
                Slog("Heal target out of range");
                return;
            }

            if (!healTarget.InLineOfSight)
            {
                Slog("Heal target is not in line of sight");
                return;
            }

            if (!SpellManager.CanCast(spell, healTarget ))
                Slog("thinks we cannot cast this spell on target..?");
            else
                Slog("YES... looks good for a heal");

            bool bCast = SpellManager.Cast(spell, healTarget);

            if (bCast)
            {
                Slog("SUCCESS:  cast heal returned true");
            }
            else
            {
                Slog("Failure:  cast heal was false");
            }

            Slog("Pausing 5 seconds....");
            Thread.Sleep(5000);
            Slog("Now testing targeted heal...");
            healTarget.Target();
            Thread.Sleep(500);
            bCast = SpellManager.Cast( spell);

            if (bCast)
            {
                Slog("SUCCESS:  cast heal returned true");
            }
            else
            {
                Slog("Failure:  cast heal was false");
            }



            return;
            // end of test code

            if ((System.Environment.TickCount - _timerLastList) < 4000)
            {
                return;
            }

            _timerLastList = System.Environment.TickCount;
            List<WoWUnit> adds = ObjectManager.GetObjectsOfType<WoWUnit>(false).FindAll(
                unit => unit != null && unit.IsValid
                    && unit.Attackable
                    && (!Safe_IsFriendly(unit) || (unit.IsPlayer && unit.ToPlayer().IsHorde != ObjectManager.Me.IsHorde))
                    && unit.IsAlive
                    && unit.Distance <= Targeting.PullDistance
                    );

            Slog("ADDLST  -- CURRENT WOWUNIT LIST --");
            foreach (WoWUnit unit in adds)
            {
                LogWowUnit(unit);
            }

            adds = (from o in ObjectManager.ObjectList
                    where o != null && o is WoWUnit
                    let unit = o.ToUnit()
                    where IsMeOrMyStuff(unit) || IsTargetingMeOrMyStuff(unit) || (ObjectManager.Me.GotTarget && unit == ObjectManager.Me.CurrentTarget)
                    select unit
                        ).ToList();
            Slog("QUERY   --GUID           Target           Summon           SumndBy          CreateBy         Critter          Charmed          CharmBy           Name-------");
            foreach (WoWUnit unit in adds)
            {
                DumpWowUnit(unit);
            }

            Slog("ENUM    --GUID           Target           Summon           SumndBy          CreateBy         Critter          Charmed          CharmBy           Name-------");
            foreach (WoWUnit unit in ObjectManager.GetObjectsOfType<WoWUnit>(false, false))
            {
                // if ( unit != null && (IsMeOrMyStuff(unit) || IsTargetingMeOrMyStuff(unit)))
                DumpWowUnit(unit);
            }
#endif
        }

        public static void LogWowUnit(WoWUnit unit)
        {
            try
            {
                Slog("        {0}{1:F2} yds hostile:{2} attackable:{3}  {4}-{5} {6}[{7}] -->{8} | {9} | {10}",
                    IsTargetingMeOrMyStuff(unit) ? "X " : "  ",
                    unit.Distance,
                    Safe_IsHostile(unit),
                    unit.Attackable,
                    unit.CreatureType,
                    unit.Class,
                    unit.Name,
                    unit.Level,
                    !unit.GotTarget ? "has no target" : unit.CurrentTargetGuid == ObjectManager.Me.Guid ? "TARGETTING ME" : unit.CurrentTarget.Name,
                    ObjectManager.Me.CurrentTarget.Guid == unit.Guid ? "MY TARGET<---" : " ",
                    ObjectManager.Me.GotAlivePet && ObjectManager.Me.Pet.GotTarget && ObjectManager.Me.Pet.CurrentTargetGuid == unit.Guid ? "PET TARGET<---" : " "
                    );
            }
            catch
            {
            }
        }


        public static void DumpWowUnit(WoWUnit unit)
        {
            try
            {

                Slog("        {0:X16} {1:X16} {2:X16} {3:X16} {4:X16}  {5} [{6}]",
                    unit.Guid,
                    unit.GotTarget ? unit.CurrentTargetGuid : 0,
                    unit.SummonedByUnitGuid,
                    unit.CreatedByUnitGuid,
                    unit.CharmedByUnitGuid,
                    unit.Name,
                    unit.CreatureType
                    );
            }
            catch { }
        }

        /*
         * Summary: inspects the objects within ranged to check if they are targeting me
         *          and hostile.  breaks down counts between ranged and melee targets
         *          
         * Returns: total number of hostiles fighting me
         */
        private readonly Stopwatch _addsTimer = new Stopwatch();
        private List<WoWUnit> _mobList = new List<WoWUnit>();

        private void CheckForAdds()
        {
            // ListWowUnitsInRange();

            Stopwatch timerCFA = new Stopwatch();
            timerCFA.Start();

            _countMeleeEnemy = 0;
            _countTenYardEnemy = 0;
            _countRangedEnemy = 0;
            _countAoe8Enemy = 0;
            _countAoe12Enemy = 0;
            _countMobs = 0;

            // _distClosestEnemy = 9999.99;
            _OpposingPlayerGanking = false;
            _BigScaryGuyHittingMe = false;

            try
            {
                // List<WoWObject> longList = ObjectManager.ObjectList;
                // List<WoWUnit> mobList = ObjectManager.GetObjectsOfType<WoWUnit>(false);
                // if (_mobList == null || (_addsTimer.ElapsedMilliseconds > 5000 && !_me.Combat ) ) 
                {
                    if (_pvp.IsBattleground())
                    {
                        _mobList = (from o in ObjectManager.ObjectList
                                    where o is WoWUnit && o.Distance <= _maxDistForRangeAttack
                                    let unit = o.ToUnit()
                                    where unit.IsAlive && unit.IsPlayer && unit.ToPlayer().IsHorde != ObjectManager.Me.IsHorde && !unit.IsPet
                                    // orderby o.Distance ascending
                                    select unit
                                    ).ToList();
                        Dlog("CheckForAdds():  PvP list built has {0} entries within {1:F1} yds", _mobList.Count, _maxDistForRangeAttack);
                    }
                    else
                    {
                        _mobList = (from o in ObjectManager.ObjectList
                                    where o is WoWUnit && o.Distance <= _maxDistForRangeAttack
                                    let unit = o.ToUnit()
                                    where unit.Attackable
                                        && unit.IsAlive
                                        && unit.Combat
                                        && (!unit.IsPlayer || unit.ToPlayer().IsHorde != _me.IsHorde)
                                        && !IsMeOrMyGroup(unit)
                                        && (IsTargetingMeOrMyGroup(unit) || unit.CreatureType == WoWCreatureType.Totem)
                                    select unit
                                    ).ToList();
                        Dlog("CheckForAdds():  PVE list built has {0} entries within {1:F1} yds", _mobList.Count, _maxDistForRangeAttack);
                    }
                }

                Dlog("CheckForAdds() can see:");
                try
                {
                    foreach (WoWUnit unit in _mobList)
                    {
                        if (unit == null || !unit.IsAlive)  // check again incase one died since making list
                            continue;

                        if (unit.Distance < 5)
                            _countMeleeEnemy++;
                        else
                            _countRangedEnemy++;

                        if (unit.Distance < 10)     // special case for 10 yard checks
                            _countTenYardEnemy++;

                        if (_me.GotTarget)
                        {
                            if (unit.Location.Distance(_me.CurrentTarget.Location) <= 12)
                            {
                                _countAoe12Enemy++;
                                if (unit.Location.Distance(_me.CurrentTarget.Location) <= 8)
                                    _countAoe8Enemy++;
                            }
                        }

                        if (unit.IsPlayer)
                        {
                            Dlog("  " + (unit.IsTargetingMeOrPet ? "*" : " ") + "PLAYER: (" + (unit.ToPlayer().IsHorde ? "H" : "A") + ") " + unit.Race + " " + unit.Class + " - " + unit.Name + "[" + unit.Level + "]  dist: " + _me.Location.Distance(unit.Location).ToString("F2"));
                            _OpposingPlayerGanking = !_pvp.IsBattleground();
                            if (_OpposingPlayerGanking && (!_me.GotTarget || !_me.CurrentTarget.IsPlayer))
                            {
                                Safe_SetCurrentTarget(unit);
                            }
                        }
                        else
                        {
                            string sType = "NPC";
                            if (Safe_IsProfileMob(unit))
                            {
                                _countMobs++;
                                sType = "MOB";
                            }

                            Dlog("  " + (unit.IsTargetingMeOrPet ? "*" : " ") + sType + ": " + unit.Class + " - " + unit.Name + "[" + unit.Level + "]  dist: " + _me.Location.Distance(unit.Location).ToString("F2"));

                            if (unit.Elite || ((unit.Level - _me.Level) >= _cfg.PVE_LevelsAboveAsElite))
                                _BigScaryGuyHittingMe = true;
                        }
                    }
                }
                catch (ThreadAbortException) { throw; }
                catch (Exception e)
                {
                    Log(Color.Red, "An Exception occured. Check debug log for details.");
                    Logging.WriteDebug("EXCEPTION in code doing CheckForAdds(1)");
                    Logging.WriteException(e);
                }
            }
            catch (ThreadAbortException) { throw; }
            catch (Exception e)
            {
                Log(Color.Red, "An Exception occured. Check debug log for details.");
                Logging.WriteDebug("HB EXCEPTION in CheckForAdds(2)");
                Logging.WriteException(e);
            }

            Dlog("   ## Total  {0}/{1} melee/ranged in Combat - CheckForAdds took {2} ms", _countMeleeEnemy, _countRangedEnemy, timerCFA.ElapsedMilliseconds);
            if (!IsRAF())
            {
                if (countEnemy > 1)
                    Slog(">>> MULTIPLE TARGETS:  " + _countMeleeEnemy + " melee,  " + _countRangedEnemy + " ranged");
                if (_BigScaryGuyHittingMe)
                    Slog(">>> BIG Scary Guy Hitting Me (elite or {0}+ levels)", _cfg.PVE_LevelsAboveAsElite);
                if (_OpposingPlayerGanking)
                    Slog(">>> Opposing PLAYER is Attacking!!!!");
            }

            return;
        }

        private WoWUnit CheckForTotems()
        {
            Stopwatch timerCFA = new Stopwatch();
            timerCFA.Start();
            WoWUnit totem = null;

            totem = ObjectManager.GetObjectsOfType<WoWUnit>(false).Find(
            unit => unit != null
                && Safe_IsHostile(unit)
                && unit.IsAlive
                && unit.Distance <= Targeting.PullDistance
                && unit.CreatureType == WoWCreatureType.Totem
                );

            return totem;
        }



        #endregion

        #region RACIALS

        private bool Warstomp()
        {
            if (!Safe_KnownSpell("War Stomp"))
                ;
            else if (_countTenYardEnemy < 1)
                ;
            else if (Safe_CastSpell("War Stomp", SpellRange.NoCheck, SpellWait.NoWait))
            {
                Slog("War Stomp: BOOM!");
                return true;
            }

            return false;
        }

        private bool Berserking()
        {

            if (_pvp.IsBattleground() || IsFightStressful() || !_cfg.PVE_SaveForStress_DPS_Racials)
            {
                if (!Safe_KnownSpell("Berserking"))
                    ;
                else if (Safe_CastSpell("Berserking", SpellRange.NoCheck, SpellWait.NoWait))
                {
                    Slog("Berserking: just broke out a can of whoop a$$!");
                    return true;
                }
            }

            return false;
        }

        private bool BloodFury()
        {
            if (_pvp.IsBattleground() || IsFightStressful() || !_cfg.PVE_SaveForStress_DPS_Racials)
            {
                if (!Safe_KnownSpell("Blood Fury"))
                    ;
                else if (Safe_CastSpell("Blood Fury", SpellRange.NoCheck, SpellWait.NoWait))
                {
                    Slog("Blood Fury: just broke out a can of whoop a$$!");
                    return true;
                }
            }

            return false;
        }

        private bool GiftOfTheNaaru()
        {
            if (!Safe_KnownSpell("Gift of the Naaru"))
                ;
            else if (Safe_CastSpell("Gift of the Naaru", SpellRange.NoCheck, SpellWait.NoWait))
            {
                Slog("Gift of the Naaru: it's good to be Draenei!");
                return true;
            }

            return false;
        }

        private bool Lifeblood()
        {
            if (!Safe_KnownSpell("Lifeblood"))
                ;
            else if (Safe_CastSpell("Lifeblood", SpellRange.NoCheck, SpellWait.NoWait))
            {
                Slog("Lifeblood: the benefit of being a flower picker!");
                return true;
            }

            return false;
        }

        #endregion

        /*
         * TotemExist()  checks to see if a totem of the specified class exists
         * 
         */
        private const int TOTEM_FIRE = 1;
        private const int TOTEM_EARTH = 2;
        private const int TOTEM_WATER = 3;
        private const int TOTEM_AIR = 4;

        private readonly string[] _totemBar = new string[5];
        private readonly string[] _totemName = new string[5];
        private readonly bool[] _totemExist = new bool[5];

        private string TotemName(int indexTotem)
        {
            return _totemName[indexTotem];
        }

        private bool TotemExist(int indexTotem)
        {
            _totemName[indexTotem] = "";

            List<string> totemInfo = CallLUA("return GetTotemInfo(" + indexTotem + ")");

            if (Equals(null, totemInfo))
                return false;

            string haveTotem = totemInfo[0];
            string totemName = totemInfo[1];
            string startTime = totemInfo[2];
            string duration = totemInfo[3];
            string icon = totemInfo[4];

            if (_totemName == null || totemName == "nil" || totemName == "")
            {
                _totemExist[indexTotem] = false;
            }
            else
            {
                _totemExist[indexTotem] = true;
                _totemName[indexTotem] = totemName;
                Dlog("   My Totem(" + indexTotem + ") exists:  " + totemName);
            }

            return _totemExist[indexTotem];
        }

        private bool TotemCast(int indexTotem, string sTotemName)
        {
            _totemExist[indexTotem] = Safe_CastSpell(sTotemName, SpellRange.NoCheck, SpellWait.NoWait);
            if (_totemExist[indexTotem])
            {
                _totemName[indexTotem] = sTotemName;
                _ptTotems = _me.Location;
            }

            return _totemExist[indexTotem];
        }

        private void TotemDestroy(int indexTotem)
        {
            RunLUA("DestroyTotem(" + indexTotem + ")");
            _totemName[indexTotem] = "";
        }

        private bool TotemSelect(int indexTotem, string sSpell)
        {
            string sLua;

            _totemBar[indexTotem] = sSpell;
            // Dlog("Totem Bar:  called with #{0} to '{1}'", indexTotem, sSpell);

            if (!Safe_KnownSpell("Call of the Elements"))
                return false;

            if (sSpell == "" || "none" == sSpell.ToLower())
            {
                Dlog("Totem Bar:  removing slot #{0}", indexTotem);
                sLua = String.Format("SetMultiCastSpell({0})", 132 + indexTotem);
                RunLUA(sLua);
                return true;
            }

            if (!Safe_KnownSpell(sSpell))
            {
                Dlog("Totem Bar:  cannot set slot #{0} to unknown spell '{1}'", indexTotem, sSpell);
                return false;
            }

            Dlog("Totem Bar:  setting slot #{0} to '{1}'", indexTotem, sSpell);
            WoWSpell spell = SpellManager.Spells[sSpell];
            sLua = String.Format("SetMultiCastSpell({0}, {1})", 132 + indexTotem, spell.Id);
            RunLUA(sLua);

            return true;
        }

        private void TotemSetupBar()
        {
            _needTotemBarSetup = false;

            if (_pvp.IsBattleground())
            {
                _totemBar[TOTEM_EARTH] = _cfg.PVP_TotemEarth;
                _totemBar[TOTEM_FIRE] = _cfg.PVP_TotemFire;
                _totemBar[TOTEM_WATER] = _cfg.PVP_TotemWater;
                _totemBar[TOTEM_AIR] = _cfg.PVP_TotemAir;
            }
            else if (IsRAF())
            {
                _totemBar[TOTEM_EARTH] = _cfg.RAF_TotemEarth;
                _totemBar[TOTEM_FIRE] = _cfg.RAF_TotemFire;
                _totemBar[TOTEM_WATER] = _cfg.RAF_TotemWater;
                _totemBar[TOTEM_AIR] = _cfg.RAF_TotemAir;
            }
            else
            {
                _totemBar[TOTEM_EARTH] = _cfg.PVE_TotemEarth;
                _totemBar[TOTEM_FIRE] = _cfg.PVE_TotemFire;
                _totemBar[TOTEM_WATER] = _cfg.PVE_TotemWater;
                _totemBar[TOTEM_AIR] = _cfg.PVE_TotemAir;
            }
            /*
                        Dlog("value from cfgdlg:  Earth - {0}", _totemBar[TOTEM_EARTH]);
                        Dlog("value from cfgdlg:  Fire  - {0}", _totemBar[TOTEM_FIRE ]);
                        Dlog("value from cfgdlg:  Water - {0}", _totemBar[TOTEM_WATER]);
                        Dlog("value from cfgdlg:  Air   - {0}", _totemBar[TOTEM_AIR  ]);
            */
            // Earth - if configured spell is None or a real spellname, select that
            if (0 != String.Compare("Auto", _totemBar[TOTEM_EARTH], true))
                TotemSelect(TOTEM_EARTH, _totemBar[TOTEM_EARTH]);
            else if (_pvp.IsBattleground() && Safe_KnownSpell("Tremor Totem"))
                TotemSelect(TOTEM_EARTH, "Tremor Totem");
            else if (_typeShaman == ShamanType.Enhance && Safe_KnownSpell("Strength of Earth Totem"))
                TotemSelect(TOTEM_EARTH, "Strength of Earth Totem");
            else if (Safe_KnownSpell("Stoneskin Totem"))
                TotemSelect(TOTEM_EARTH, "Stoneskin Totem");
            else
                TotemSelect(TOTEM_EARTH, "None");

            // Fire - if configured spell is None or a real spellname, select that
            if (0 != String.Compare("Auto", _totemBar[TOTEM_FIRE], true))
                TotemSelect(TOTEM_FIRE, _totemBar[TOTEM_FIRE]);
            else if (Safe_KnownSpell("Flametongue Totem") && (_typeShaman == ShamanType.Resto || IsHealerOnly()))
                TotemSelect(TOTEM_FIRE, "Flametongue Totem");
            else if (Safe_KnownSpell("Searing Totem"))
                TotemSelect(TOTEM_FIRE, "Searing Totem");
            else
                TotemSelect(TOTEM_FIRE, "None");

            // Water - if configured spell is None or a real spellname, select that
            if (0 != String.Compare("Auto", _totemBar[TOTEM_WATER], true))
                TotemSelect(TOTEM_WATER, _totemBar[TOTEM_WATER]);
            else if (Safe_KnownSpell("Healing Stream Totem") && IsRAF())
                TotemSelect(TOTEM_WATER, "Healing Stream Totem");
            else if (Safe_KnownSpell("Totem of Tranquil Mind") && _pvp.IsBattleground())
                TotemSelect(TOTEM_WATER, "Totem of Tranquil Mind");
            else if (Safe_KnownSpell("Mana Spring Totem"))
                TotemSelect(TOTEM_WATER, "Mana Spring Totem");
            else
                TotemSelect(TOTEM_WATER, "None");

            // Air - if configured spell is None or a real spellname, select that
            if (0 != String.Compare("Auto", _totemBar[TOTEM_AIR], true))
                TotemSelect(TOTEM_AIR, _totemBar[TOTEM_AIR]);
            else if (Safe_KnownSpell("Grounding Totem") && _typeShaman == ShamanType.Resto)
                TotemSelect(TOTEM_AIR, "Grounding Totem");
            else if (Safe_KnownSpell("Windfury Totem") && _typeShaman == ShamanType.Enhance)
                TotemSelect(TOTEM_AIR, "Windfury Totem");
            else if (Safe_KnownSpell("Wrath of Air Totem") && (_typeShaman == ShamanType.Elemental || _typeShaman == ShamanType.Resto))
                TotemSelect(TOTEM_AIR, "Wrath of Air Totem");
            else
                TotemSelect(TOTEM_AIR, "None");

            Slog("^Totem Bar - Earth: {0}", _totemBar[TOTEM_EARTH]);
            Slog("^Totem Bar - Fire : {0}", _totemBar[TOTEM_FIRE]);
            Slog("^Totem Bar - Water: {0}", _totemBar[TOTEM_WATER]);
            Slog("^Totem Bar - Air  : {0}", _totemBar[TOTEM_AIR]);

        }


        /// <summary>
        /// SetTotemsAsNeeded() - manages casting totems as called for by environment and
        /// user configuration values. This code uses:
        ///	    http://www.yawb.info/2009/08/25/learning-about-totems-tips-to-aid-your-growing-shamanism/
        /// as a guideline for totem usage while leveling.
        /// </summary>
        private bool SetTotemsAsNeeded()
        {
            bool castTotem = false;

            try
            {
                if (_cfg.TotemsDisabled || _me.IsMoving)
                    return castTotem;

                // makes 4 LUA calls to get active totem info (1 per slot/family) so
                // .. make sure we don't get called more than once every 2 seconds
                // .. so we aren't spamming LUA calls
                if (_TotemCheckTimer.ElapsedMilliseconds < 4000)
                {
                    // if this isn't first time here since reset, then wait for 2 seconds
                    if (_TotemCheckTimer.ElapsedMilliseconds > 0)
                        return castTotem;

                    // otherwise, only 0 if first check after last .Reset
                    //  ..  so continue processing
                }

                if (TotemsWereSet() && _ptTotems.Distance(_me.Location) > _cfg.DistanceForTotemRecall)
                {
                    Dlog("Recalling Totems that were set {0:F1} yds away", _ptTotems.Distance(_me.Location));
                    RecallTotemsForMana();
                }

                _TotemCheckTimer.Reset();
                _TotemCheckTimer.Start();

                //-----
                // if you make it past the following gate, only limited tests are needed for each totem
                //-----
                if (_pvp.IsBattleground() || IsRAF())
                    ;
                else if (IsFightStressful() || !_cfg.PVE_SaveForStress_TotemsSelected)
                    ;
                else
                {
                    Dlog("not setting totems until a stressful situation");
                    return castTotem;
                }

                // check which totems exist
                _totemExist[TOTEM_EARTH] = TotemExist(TOTEM_EARTH);
                _totemExist[TOTEM_FIRE] = TotemExist(TOTEM_FIRE);
                _totemExist[TOTEM_WATER] = TotemExist(TOTEM_WATER);
                _totemExist[TOTEM_AIR] = TotemExist(TOTEM_AIR);
                bool bAnyTotemsUp = _totemExist[TOTEM_EARTH] || _totemExist[TOTEM_FIRE] || _totemExist[TOTEM_WATER] || _totemExist[TOTEM_AIR];

                Dlog("SetTotemsAsNeeded():  earth: " + _totemExist[TOTEM_EARTH] + "  fire: " + _totemExist[TOTEM_FIRE] + "  water: " + _totemExist[TOTEM_WATER] + "  air: " + _totemExist[TOTEM_AIR]);

                _WereTotemsSet = bAnyTotemsUp;  // _WereTotemsSet || removed because only matters if they exist

                // Quick scan for mobs that cast fear (only in RAF)
                //////////////////////////////////////////////////////////////////////
                _foundMobsThatFear = false;
                if (IsRAF())
                {
                    WoWUnit fearMob;

                    if (!IsHealerOnly())
                        fearMob = (from unit in _mobList where IsFearMob(unit) select unit).FirstOrDefault();
                    else
                        fearMob = (from o in ObjectManager.ObjectList
                                   where o is WoWUnit && o.Distance <= 50
                                   let unit = o.ToUnit()
                                   where unit.IsAlive
                                       && !unit.IsPlayer && !unit.IsPet
                                       && IsTargetingMeOrMyGroup(unit)
                                       && IsFearMob(unit)
                                   select unit
                                    ).FirstOrDefault();

                    if (fearMob != null)
                    {
                        _foundMobsThatFear = true;
                        Slog("Tremor Totem:  detected fear mob: {0}(#{1})", fearMob.Name, fearMob.Entry);
                    }
                }

                // Totem Bar Set
                // -- add handling for changing totem bar setup temporarily if needed for tremor totem
                //////////////////////////////////////////////////////////////////////
                if (!bAnyTotemsUp)
                {
                    string saveEarthTotemSetup = "";

                    // if mobs that fear are found and tremor not in bar setup already, add temporarily just for cast
                    if (_foundMobsThatFear && 0 != string.Compare(_totemBar[TOTEM_EARTH], "Tremor Totem", true))
                    {
                        saveEarthTotemSetup = _totemBar[TOTEM_EARTH];
                        TotemSelect(TOTEM_EARTH, "Tremor Totem");
                        StyxWoW.SleepForLagDuration();
                    }
                    const string totemBar = "Call of the Elements";
                    if (Safe_KnownSpell(totemBar) && Safe_CastSpell(totemBar, SpellRange.NoCheck, SpellWait.NoWait))
                    {
                        castTotem = true;
                        _WereTotemsSet = true;
                        _ptTotems = _me.Location;
                        _totemExist[TOTEM_FIRE] = TotemExist(TOTEM_FIRE);
                    }

                    // if we changed the earth totem on bar, restore back to configured value
                    if (!string.IsNullOrEmpty(saveEarthTotemSetup))
                    {
                        TotemSelect(TOTEM_EARTH, saveEarthTotemSetup);
                    }

                    // stop here if we did drop a full bar
                    if (castTotem)
                        return castTotem;
                }

                // Earth Totems First
                //////////////////////////////////////////////////////////////////////
#if false
            if (!_totemExist[ TOTEM_EARTH] && (_countMeleeEnemy > 1 || _BigScaryGuyHittingMe))
            {
                if (Safe_KnownSpell("Stoneclaw Totem"))
                {
                    _totemExist[ TOTEM_EARTH] = Safe_CastSpell("Stoneclaw Totem");
                }
            }
#endif
                if (_foundMobsThatFear && "TREMOR TOTEM" != _totemName[TOTEM_EARTH].ToUpper())
                {
                    castTotem = castTotem || TotemCast(TOTEM_EARTH, "Tremor Totem");
                }
                else if ("NONE" != _totemBar[TOTEM_EARTH].ToUpper())
                {
                    if (!_totemExist[TOTEM_EARTH] && Safe_KnownSpell(_totemBar[TOTEM_EARTH]))
                        castTotem = castTotem || TotemCast(TOTEM_EARTH, _totemBar[TOTEM_EARTH]);

                    if (!_totemExist[TOTEM_EARTH] && _typeShaman == ShamanType.Enhance && Safe_KnownSpell("Strength of Earth Totem"))
                        castTotem = castTotem || TotemCast(TOTEM_EARTH, "Strength of Earth Totem");

                    if (!_totemExist[TOTEM_EARTH] && _typeShaman != ShamanType.Enhance && Safe_KnownSpell("Stoneskin Totem"))
                        castTotem = castTotem || TotemCast(TOTEM_EARTH, "Stoneskin Totem");
                }

                // Fire Totems
                //////////////////////////////////////////////////////////////////////
                if (_countMeleeEnemy >= 3 && !IsHealerOnly())
                {
                    if (!_me.GotTarget || !IsImmunneToFire(_me.CurrentTarget))
                    {
                        if (!_totemExist[TOTEM_FIRE] || "FIRE ELEMENTAL TOTEM" != _totemName[TOTEM_EARTH].ToUpper())
                        {
                            if (Safe_KnownSpell("Magma Totem"))
                            {
                                castTotem = castTotem || TotemCast(TOTEM_FIRE, "Magma Totem");
                            }
                        }
                    }
                }

                if ("NONE" != _totemBar[TOTEM_FIRE].ToUpper() && !_totemExist[TOTEM_FIRE])
                {
                    if (!_totemExist[TOTEM_FIRE] && Safe_KnownSpell(_totemBar[TOTEM_FIRE]))
                        castTotem = castTotem || TotemCast(TOTEM_FIRE, _totemBar[TOTEM_FIRE]);
                    if (!_totemExist[TOTEM_FIRE] && _typeShaman != ShamanType.Resto && Safe_KnownSpell("Searing Totem"))
                        castTotem = castTotem || TotemCast(TOTEM_FIRE, "Searing Totem");
                    if (!_totemExist[TOTEM_FIRE] && Safe_KnownSpell("Flametongue Totem"))
                        castTotem = castTotem || TotemCast(TOTEM_FIRE, "Flametongue Totem");
                }

                // Water Totems
                //////////////////////////////////////////////////////////////////////
                if ("NONE" != _totemBar[TOTEM_WATER].ToUpper())
                {
                    if (!_totemExist[TOTEM_WATER] && Safe_KnownSpell(_totemBar[TOTEM_WATER]))
                        castTotem = castTotem || TotemCast(TOTEM_WATER, _totemBar[TOTEM_WATER]);

                    if (!_totemExist[TOTEM_WATER] && Safe_KnownSpell("Mana Spring Totem"))
                        castTotem = castTotem || TotemCast(TOTEM_WATER, "Mana Spring Totem");
                }

                // Air Totems
                //////////////////////////////////////////////////////////////////////
                if ("NONE" != _totemBar[TOTEM_AIR].ToUpper())
                {
                    if (!_totemExist[TOTEM_AIR] && Safe_KnownSpell(_totemBar[TOTEM_WATER]))
                        castTotem = castTotem || TotemCast(TOTEM_AIR, _totemBar[TOTEM_AIR]);
                    if (!_totemExist[TOTEM_AIR] && _typeShaman == ShamanType.Resto && Safe_KnownSpell("Grounding Totem"))
                        castTotem = castTotem || TotemCast(TOTEM_AIR, "Grounding Totem");
                    if (!_totemExist[TOTEM_AIR] && _typeShaman == ShamanType.Enhance && Safe_KnownSpell("Windfury Totem"))
                        castTotem = castTotem || TotemCast(TOTEM_AIR, "Windfury Totem");
                    if (!_totemExist[TOTEM_AIR] && _typeShaman == ShamanType.Elemental && Safe_KnownSpell("Wrath of Air Totem"))
                        castTotem = castTotem || TotemCast(TOTEM_AIR, "Wrath of Air Totem");
                }

                _WereTotemsSet = _WereTotemsSet || _totemExist[TOTEM_EARTH] || _totemExist[TOTEM_FIRE] || _totemExist[TOTEM_WATER] || _totemExist[TOTEM_AIR];

                if (!bAnyTotemsUp && _WereTotemsSet)
                    _ptTotems = _me.Location;
            }
            catch (ThreadAbortException) { throw; }
            catch (Exception e)
            {
                Log(Color.Red, "An Exception occured. Check debug log for details.");
                Logging.WriteDebug("HB EXCEPTION in SetTotemsAsNeeded()");
                Logging.WriteException(e);
            }

            return castTotem;
        }

        private static bool TotemsWereSet()
        {
            return _WereTotemsSet;
        }

        private double CheckDistanceForRecall()
        {
            double check = _cfg.DistanceForTotemRecall;
            if (_cfg.PVE_PullType == ConfigValues.TypeOfPull.Ranged || _typeShaman != ShamanType.Enhance)
            {
                check += _distForRangedPull * 0.75;
            }

            return check;
        }

        public void RecallTotemsForMana()
        {
            if (_WereTotemsSet)
            {
                if (Safe_KnownSpell("Totemic Recall"))
                {
                    // set totem flag based on whether can cast spell yet
                    _WereTotemsSet = !Safe_CastSpell("Totemic Recall", SpellRange.NoCheck, SpellWait.NoWait);
                    if (!_WereTotemsSet)
                    {
                        _totemName[TOTEM_FIRE] = "";
                        _totemName[TOTEM_EARTH] = "";
                        _totemName[TOTEM_WATER] = "";
                        _totemName[TOTEM_AIR] = "";
                    }
                }
                else
                {
                    _WereTotemsSet = false;

                    if (_totemExist[TOTEM_FIRE])
                        TotemDestroy(TOTEM_FIRE);
                    if (_totemExist[TOTEM_EARTH])
                        TotemDestroy(TOTEM_EARTH);
                    if (_totemExist[TOTEM_WATER])
                        TotemDestroy(TOTEM_WATER);
                    if (_totemExist[TOTEM_AIR])
                        TotemDestroy(TOTEM_AIR);
                }

                _TotemCheckTimer.Reset();
            }
        }

        #endregion



        #region Item / Spell ID Lists

        /*
         * Following list of id's come from:
         * 
         *      http://www.wowhead.com
         * 
         */

        // Potion EntryId's taken from WoWHead
        private readonly List<uint> _potionHealthEID = new List<uint>()
        {
            //=== RESTORATION POTIONS (HEALTH AND MANA)
            40077,  // Crazy Alchemist's Potion 3500 (Alchemist)
            34440,  // Mad Alchemist's Potion 2750   (Alchemist)
            40087,  // Powerful Rejuvenation Potion 4125
            22850,  // Super Rejuvenation Potion 2300
            18253,  // Major Rejuvenation Potion 1760
            9144,   // Wildvine Potion 1500
            2456,   // Minor Rejuvenation Potion 150

            //=== HEALTH POTIONS 
            33447,  // Runic Healing Potion 4500

            43569,  // Endless Healing Potion  2500
            43531,  // Argent Healing Potion  2500
            32947,  // Auchenai Healing Potion  2500
            39671,  // Resurgent Healing Potion 2500
            22829,  // Super Healing Potion 2500
            33934,  // Crystal Healing Potion 2500
            23822,  // Healing Potion Injector 2500
            33092,  // Healing Potion Injector 2500

            31852,  // Major Combat Healing Potion 1750
            31853,  // Major Combat Healing Potion 1750
            31839,  // Major Combat Healing Potion 1750
            31838,  // Major Combat Healing Potion 1750
            13446,  // Major Healing Potion 1750
            28100,  // Volatile Healing Potion 1750 

            18839,  // Combat Healing Potion 900 
            3928,   // Superior Healing Potion 900

            1710,   // Greater Healing Potion  585

            929,    // Healing Potion  360

            4596,   // Discolored Healing Potion 180
            858,    // Lesser Healing Potion 180

            118     // Minor Healing Potion 90
            
        };

        // Mana Potion EntryId's taken from WoWHead
        private readonly List<uint> _potionManaEID = new List<uint>()
        {
            //=== RESTORATION POTIONS (HEALTH AND MANA)
            40077,  // Crazy Alchemist's Potion 4400 (Alchemist)
            34440,  // Mad Alchemist's Potion 2750   (Alchemist)
            40087,  // Powerful Rejuvenation Potion 4125
            22850,  // Super Rejuvenation Potion 2300
            18253,  // Major Rejuvenation Potion 1760
            9144,   // Wildvine Potion 1500
            2456,   // Minor Rejuvenation Potion 150

            //=== MANA POTIONS 
	    43570, // 3000 Endless Mana Potion 
	    33448, // 4400 Runic Mana Potion
	    40067, // 3000 Icy Mana Potion
	    31677, // 3200 Fel Mana Potion
	    33093, // 3000 Mana Potion Injector
	    43530, // 3000 Argent Mana Potion
	    32948, // 3000 Auchenai Mana Potion
	    22832, // 3000 Super Mana Potion
	    28101, // 2250 Unstable Mana Potion
	    13444, // 2250 Major Mana Potion
	    13443, // 1500 Superior Mana Potion
	     6149, // 900 Greater Mana Potion
	     3827, // 585 Mana Potion
	     3385, // 360 Lesser Mana Potion
	     2455, // 180 Minor Mana Potion
	};

        // Bandage EntryId's taken from WoWHead
        private readonly List<uint> _bandageEID = new List<uint>()
        {
            // ID,	BANDAGE NAME,         (Level, Healing)
	    34722,	// Heavy Frostweave,	(400, 5800)
	    34721,	// Frostweave,		(350, 4800)
	    21991,	// Heavy Netherweave,	(325, 3400)
	    21990,	// Netherweave,		(300, 2800)
	    14530,	// Heavy Runecloth,	(225, 2000)
	    14529,	// Runecloth,		(200, 1360)
	    8545,	// Heavy Mageweave,	(175, 1104)
	    8544,	// Mageweave		(150, 800)
	    6451,	// Heavy Silk,		(125, 640)
	    6450,	// Silk			(100, 400)
	    3531,	// Heavy Wool,		( 75, 301)
            3530,	// Wool			( 50, 161)
	    2581,	// Heavy Linen,		( 20, 114)
	    1251,	// Linen		(  1, 66)
            
        };

        private readonly HashSet<uint> _listFrostImmune = new HashSet<uint>()
	{
	    24601,  // Steam Rager (65-71)
		17358,  // Fouled Water Spirit (18-19)
		3950,  // Minor Water Guardian (25)
		14269,  // Seeker Aqualon (21)
		3917,  // Befouled Water Elemental (23-25)
		10757,  // Boiling Elemental (27-28)
		10756,  // Scalding Elemental (28-29)
		2761,  // Cresting Exile (38-39)
		691,  // Lesser Water Elemental (35-37)
		5461,  // Sea Elemental (46-49)
		5462,  // Sea Spray (45-48)
		8837,  // Muck Splash (47-49))
		7132,  // Toxic Horror(53-54)
		14458,  // Watery Invader (56-58)
		20792,  // Bloodscale Elemental	(62-63)	
		20090,  // Bloodscale Sentry (62-63)
		20079,  // Darkcrest Sentry	(61-62)
		17153,  // Lake Spirit (64-65)
		17155,  // Lake Surger (64-66)
		17154,  // Muck Spawn (63-66)
		21059,  // Enraged Water Spirit	(68-69)
		25419,  // Boiling Spirit (68-70)
		25715,  // Frozen Elemental (65-71)
		23919,  // Ice Elemental (64-69)
		24228,  // Iceshard Elemental (70-71)	
		26316,  // Crystalline Ice Elemental (73-74)
		16570,  // Crazed Water Spirit (71-76)
		28411,  // Frozen Earth	(76-77)
		29436,  // Icetouched Earthrager (69-75)
		29844,  // Icebound Revenant (78-80)
		30633,  // Water Terror	(77-78)
	};

        private readonly HashSet<uint> _listFireImmune = new HashSet<uint>()
	{
	    6073, // Searing Infernal
		4038, // Burning Destroyer
		4037, // Burning Ravager
		4036, // Rogue Flame Spirit
		2760, // Burning Exile
		5850, // Blazing Elemental
		5852, // Inferno Elemental
		5855, // Magma Elemental
		9878, // Entropic Beast
		9879, // Entropic Horror
		14460, // Blazing Invader
		6521, // Living Blaze
		6520, // SScorching Elemental		
		20514, // Searing Elemental
		21061, // Enraged Fire Spirit	
		29504, // Seething Revenant	
		6073, // Searing Infernal
		7136, // Infernal Sentry
		7135, // Infernal Bodyguard
		21419, // Infernal Attacker

        19261, // Infernal Warbringer
	};

        private readonly HashSet<uint> _listNatureImmune = new HashSet<uint>()
	{
	    18062, // Enraged Crusher
	    11577, // Whirlwind Stormwalker
		11578, // Whirlwind Shredder
		11576, // Whirlwind Ripper
		4661, // Gelkis Rumbler
		832, // Dust Devil		
		4034, // Enraged Stone Spirit
		4035, // Furious Stone Spirit
		4499, // Rok'Alim the Pounder
		9377, // Swirling Vortex
		4120, // Thundering Boulderkin
		2258, // Stone Fury
		2592, // Rumbling Exile
		2762, // Thundering Exile
		2791, // Enraged Rock Elemental
		2919, // Fam'retor Guardian
		2736, // GGreater Rock Elemental	
		2735, // Lesser Rock Elemental
		92, // Rock Elemental	
		8667, // Gusting Vortex
		9396, // Ground Pounder
		5465, // Land Rager
		9397, // Living Storm
		14462, // Thundering Invader
		11745, // Cyclone Warrior	
		11746, // Desert Rumbler
		11744, // Dust Stormer
		14455, // Whirling Invader	
		17158, // Dust Howler	
		18062, // Enraged Crusher
		17160, // Living Cyclone	
		17157, // Shattered Rumbler
		17159, // Storm Rager
		17156, // Tortured Earth Spirit
		18882, // Sundered Thunderer
		20498, // Sundered Shard
		21060, // Enraged Air Spirit
		22115, // Enraged Earth Shard
		21050, // Enraged Earth Spirit		
		25415, // Enraged Tempest
		24229, // Howling Cyclone	
		24340, // Rampaging Earth Elemental		
		26407, // Lightning Sentry
		28784, // Altar Warden
		29124, // Lifeblood Elemental	
		28858, // Storm Revenant
	};

        private readonly HashSet<uint> _hashTremorTotemMobs = new HashSet<uint>()
    {
        31402,  // TEST ONLY!!!!
        31403,  // test only 
        31228,  // testonly

        // NPC Abilities Fear Abilities:  http://www.wowhead.com/spells=-8?filter=me=5;dt=1

		30284,	//  Ahn'kahet: Old Kingdom,	  Bonegrinder
		2256,	//  Alterac Mountains,	   Crushridge Enforcer
		19906,	//  Alterac Mountains,	  Usha Eyegouge
		11947,	//  Alterac Valley,	  Captain Galvangar
		30231,	//  Arathi Highlands,	  Radulf Leder
		19905,	//  Arathi Highlands,	  The Black Bride
		19908,	//  Ashenvale,	  Su'ura Swiftarrow
		6116,	//  Azshara,	  Highborne Apparition
		22855,	//  Black Temple,	  Illidari Nightlord
		9018,	//  Blackrock Depths,	   High Interrogator Gerstahn <Twilight's Hammer Interrogator>
		16059,	//  Blackrock Depths,	  Theldren
		10162,	//  Blackrock Spire/Blackwing Lair,	  Lord Victor Nefarius
		23353,	//  Blade's Edge Mountains,	   Braxxus
		20735,	//  Blade's Edge Mountains,	  Dorgok
		20889,	//  Blade's Edge Mountains,	  Ethereum Prisoner (Group Energy Ball)
		22204,	//  Blade's Edge Mountains,	  Fear Fiend
		23055,	//  Blade's Edge Mountains,	  Felguard Degrader
		8716,	//  Blasted Lands,	  Dreadlord
		17664,	//  Bloodmyst Isle,	  Matis the Cruel <Herald�of�Sironas>
		32322,	//  Dalaran,	  Gold Warrior
		32321,	//  Dalaran,	  Green Warrior
		34988,	//  Darnassus,	  Landuen Moonclaw
		34989,	//  Darnassus,	  Rissa Shadeleaf
		14325,	//  Dire Maul,	  Captain Kromcrush
		11455,	//  Dire Maul,	  Wildspawn Felsworn
		14324,	//  Dire Maul North,	  Cho'Rush the Observer
		26830,	//  Drak'Tharon Keep,	  Risen Drakkari Death Knight
		40195,	//  Durotar,	  Mindless Troll
		1200,	//  Duskwood,	   Morbent Fel
		202,	//  Duskwood,	   Skeletal Horror
		12339,	//  Eastern Plaguelands,	   Demetria <The Scarlet Oracle>
		8521,	//  Eastern Plaguelands,	  Blighted Horror
		8542,	//  Eastern Plaguelands,	  Death Singer
		8528,	//  Eastern Plaguelands,	  Dread Wearer
		8600,	//  Eastern Plaguelands,	  Plaguebat
		10938,	//  Eastern Plaguelands,	  Redpath the Corrupted
		113,	//  Elwynn Forest,	  Stonetusk Boar
		16329,	//  Ghostlands,	  Dar'khan Drathir
		11445,	//  Gordok Captain,	  
		21350,	//  Gruul's Lair,	  Gronn-Priest
		28961,	//  Halls of Lightning,	  Titanium Siegebreaker
		17000,	//  Hellfire Peninsula,	  Aggonis
		17478,	//  Hellfire Peninsula,	  Bleeding Hollow Scryer
		19424,	//  Hellfire Peninsula,	  Bleeding Hollow Tormenter
		17014,	//  Hellfire Peninsula,	  Collapsing Voidwalker
		2215,	//  Hillsbrad Foothills,	  High Executor Darthalia
		17968,	//  Hyjal Summit,	  Archimonde
		32278,	//  Icecrown,	  Harbinger of Horror
		31222,	//  Icecrown,	  Khit'rix the Dark Master
		31775,	//  Icecrown,	  Thexal Deathchill
		37955,	//  Icecrown Citidel,	   Blood-Queen Lana'thel
		34991,	//  Ironforge,	  Borim Goldhammer
		17521,	//  Karazhan,	  The Big Bad Wolf
		24558,	//  Magister's Terrace,	  Ellrys Duskhallow
		24559,	//  Magister's Terrace,	  Warlord Salaris
		11982,	//  Molten Core,	   Magmadar
		17152,	//  Nagrand,	   Felguard Legionnaire
		18870,	//  Netherstorm�,	  Voidshrieker
		17833,	//  Old Hillsbrad Foothills,	  Durnholde Warden
		34955,	//  Orgrimmar,	  Karg Skullgore
		30610,	//  Orgrimmar,	  War-Hunter Molog
		10508,	//  Ras Frostwhisperer,	  Scholomance
		15391,	//  Ruins of Ahn'Qiraj,	  Captain Qeez
		6490,	//  Scarlet Monestary,	   Azshir the Sleepless
		4542,	//  Scarlet Monestary,	  High Inquisitor Fairbanks
		10502,	//  Scholomance,	  Lady Illucia Barov
		10470,	//  Scholomance,	  Scholomance Neophyte
		8280,	//  Searing Gorge,	   Shleipnarr
		18325,	//  Sethekk Halls,	  Sethekk Prophet
		18796,	//  Shadow Labryinth,	  Fel Overseer
		18731,	//  Shadow Labyrinth,	  Ambassador Hellmaw
		19826,	//  Shadowmoon Valley,	  Dark Conclave Shadowmancer
		21166,	//  Shadowmoon Valley,	  Illidari Dreadlord
		22074,	//  Shadowmoon Valley,	  Illidari Mind Breaker <The�Crimson�Sigil>
		22006,	//  Shadowmoon Valley,	  Shadlowlord Deathwill
		21314,	//  Shadowmoon Valley,	  Terrormaster
		15200,	//  Silithus�,	  Twilight Keeper Mayna <Twilight's�Hammer>
		15308,	//  Silithus�,	  Twilight Prophet <Twilight's�Hammer>
		40413,	//  Silvermoon City,	  Alenjon Sunblade
		34998,	//  Stormwind City,	  Alison Devay
		30578,	//  Stormwind City,	  Bethany Aldire
		34997,	//  Stormwind City,	  Devin Fardale
		20381,	//  Stormwind City,	  Jovil
		1559,	//  Stranglethorn Vale,	   King Mukla
		680,	//  Stranglethorn Vale,	   Mosh'Ogg Lord
		2464,	//  Stranglethorn Vale,	  Commander Aggro'gosh
		469,	//  Stranglethorn Vale,	  Lieutenant Doren
		10812,	//  Stratholme,	  Grand Crusader Dathrohan
		11143,	//  Stratholme,	  Postmaster Malown
		16102,	//  Stratholme,	  Sothos
		5271,	//  Sunken Temple,	  Atal'ai Deathwalker
		25370,	//  Sunwell Plateau,	  Sunblade Dusk Priest
		15311,	//  Temple of Ahn'Qiraj,	  Anubisath Warder
		15543,	//  Temple of Ahn'Qiraj,	  Princess Yaui
		15252,	//  Temple of Ahn'Qiraj,	  Qiraji Champion
		23067,	//  Terokkar Forest,	  Talonpriest Skizzik
		21200,	//  Terokkar Forrest,	  Screeching Spirit
		18686,	//  Terrokar Forest,	  Doomsayer Jurim
		20912,	//  The Arcatraz,	  Harbinger Skyriss
		20875,	//  The Arcatraz,	  Negaton Screamer
		3393,	//  The Barrens,	  Captain Fairmount
		14781,	//  The Barrens,	  Captain Shatterskull
		3338,	//  The Barrens,	  Sergra Darkthorn
		21104,	//  The Black Morass,	  Rift Keeper
		642,	//  The Deadmines,	   Sneed's Shredder <Lumbermaster>
		30581,	//  The Exodar,	  Buhurda
		34987,	//  The Exodar,	  Hunara
		20118,	//  The Exodar,	  Jihi
		34986,	//  The Exodar,	  Liedel the Just
		20119,	//  The Exodar,	  Mahul
		20382,	//  The Exodar,	  Mitia
		35027,	//  The Exotar,	  Erutor
		36497,	//  The Forge of Souls,	  Bronjahm
		12496,	//  The Hinterlands,	  Dreamtracker
		26798,	//  The Nexus,	  Commander Kolurg
		26796,	//  The Nexus,	  Commander Stoutbeard
		17694,	//  The Shattered Halls,	  Shadowmoon Darkcaster
		16809,	//  The Shattered Halls,	  Warbringer O'mrogg
		17957,	//  The Slave Pens,	  Coilfang Champion
		17801,	//  The Steamvault,	  Coilfang Siren
		1663,	//  The Stockade,	  Dextren Ward
		34466,	//  Trial of the Crusader,	  Anthar Forgemender <Priest>
		34473,	//  Trial of the Crusader,	  Brienna Nightfell <Priest>
		34447,	//  Trial of the Crusader,	  Caiphus the Stern <Priest>
		34450,	//  Trial of the Crusader,	  Harkzog
		34474,	//  Trial of the Crusader,	  Serissa Grimdabbler
		34441,	//  Trial of the Crusader,	  Vivienne Blackwhisper <Priest>
		33515,	//  Ulduar,	  Auriaya
		33818,	//  Ulduar,	  Twilight Adherent
		34983,	//  Undercity,	  Deathstalker Fane
		347,	//  Undercity,	  Grizzle Halfmane
		2804,	//  Undercity,	  Kurden Bloodclaw
		20386,	//  Undercity,	  Lyrlia Blackshield
		35021,	//  Undercity,	  Marog
		31531,	//  Undercity,	  Perfidious Dreadlord
		32391,	//  Undercity,	  Perfidious Dreadlord
		30583,	//  Undercity,	  Sarah Forthright
		9167,	//  Un'Goro Crater,	   Frenzied Pterrordax
		9166,	//  Un'Goro Crater,	  Pterrordax
		26696,	//  Utgarde Pinnacle,	   Ymirjar Berserker
		5056,	//  Wailing Caverns,	   Deviate Dreadfang
		3654,	//  Wailing Caverns,	   Mutanus the Devourer
		1785,	//  Western Plaguelands,	  Skeletal Terror
		10200,	//  Winterspring,	   Rak'shiri
		24246,	//  Zul'Aman,	  Darkheart
		24239,	//  Zul'Aman,	  Hex Lord Malacrass
		7275,	//  Zul'Farrak,	   Shadowpriest Sezz'ziz
		11830,	//  Zul'Gurub,	  Hakkari Priest
		14517,	//  Zul'Gurub,	  High Priestess Jeklik
		11359,	//  Zul'Gurub,	  Soulflayer
    };


























        private readonly List<string> _enchantElemental = new List<string>
	{
	    "Flametongue Weapon",
	    "Windfury Weapon",
	    "Rockbiter Weapon",
	    "Frostbrand Weapon",
	};

        private readonly List<string> _enchantEnhancementPVE_Mainhand = new List<string>
	{
	    "Windfury Weapon",
	    "Flametongue Weapon",
	    "Rockbiter Weapon",
	    "Frostbrand Weapon"
	};

        private readonly List<string> _enchantEnhancementPVE_Offhand = new List<string>
	{
	    "Flametongue Weapon",
	    "Windfury Weapon",
	    "Rockbiter Weapon",
	    "Frostbrand Weapon"
	};

        private readonly List<string> _enchantEnhancementPVP_Mainhand = new List<string>
	{
	    "Windfury Weapon",
	    "Flametongue Weapon",
	    "Rockbiter Weapon",
	    "Frostbrand Weapon"
	};

        private readonly List<string> _enchantEnhancementPVP_Offhand = new List<string>
	{
	    "Frostbrand Weapon",
	    "Flametongue Weapon",
	    "Windfury Weapon",
	    "Rockbiter Weapon"
	};

        private readonly List<string> _enchantResto = new List<string>
	{
	    "Earthliving Weapon",
	    "Flametongue Weapon",
	    "Windfury Weapon",
	    "Rockbiter Weapon",
	    "Frostbrand Weapon"
	};

        #endregion
        #region RAID HEALING FUNCTION

        private static bool WillChainHealHop(WoWUnit healTarget)
        {
            WoWPlayer player = null;
            Stopwatch timer = new Stopwatch();
            double threshhold = _pvp.IsBattleground() ? _cfg.PVP_GroupNeedHeal : _cfg.RAF_GroupNeedHeal;
            timer.Start();

            if (healTarget == null)
                return false;

            try
            {
                player = ObjectManager.GetObjectsOfType<WoWPlayer>(false).Find(
                            p => p != null
                                && p.IsPlayer && !p.IsPet
                                && p != healTarget
                                && p.IsAlive
                                && p.HealthPercent < threshhold
                                && healTarget.Location.Distance(p.Location) <= 12
                            );
            }
            catch (ThreadAbortException) { throw; }
            catch (Exception e)
            {
                Log(Color.Red, "An Exception occured. Check debug log for details.");
                Logging.WriteDebug("HB EXCEPTION in WillChainHealHop()");
                Logging.WriteException(e);
            }

            Dlog("WillChainHealHop(): took {0} ms", timer.ElapsedMilliseconds);
            return player != null;
        }

        private bool HealRaid()
        {
            bool wasSpellCast = false;
            double healPct = IsRAF() ? _cfg.RAF_GroupNeedHeal : _cfg.PVP_GroupNeedHeal;

            WoWPlayer p = CC_PVP.chooseHealTarget(healPct, SpellRange.NoCheck);

            // if nobody in group needs a heal, so maintain position
            if (p == null)
            {
                wasSpellCast = DispelRaid();

                // if nobody was dispelled and we are healing a group/raid other than a battleground
                if (!wasSpellCast && IsRAF() && (_me.Combat || RaFHelper.Leader.Combat))
                {
                    MoveToHealTarget(RaFHelper.Leader, 35);
                    wasSpellCast = SetTotemsAsNeeded();
                }
            }
            else
            // somebody in our group needs a heal
            {
                if (!p.IsMe && p.Combat)
                    SetTotemsAsNeeded();

                double startHealth = p.HealthPercent;
                wasSpellCast = HealPlayer(p);

                // wait for lagged update of player health if we have the time and not urgent to heal again
                if (wasSpellCast && !_pvp.IsBattleground() && p.HealthPercent > 70)
                {
                    Stopwatch lagTimer = new Stopwatch();
                    lagTimer.Start();
                    while (lagTimer.ElapsedMilliseconds < 400 && Math.Abs(p.HealthPercent - startHealth) < 5)
                    {
                        Thread.Sleep(100);
                    }
                }
            }

            return wasSpellCast;
        }

        private bool DispelRaid()
        {
            if (CC_PVP._healTargets == null || CC_PVP._healTargets.Count == 0)
                return false;

            bool WasHealCast = false;


            bool knowCleanseSpirit = Safe_KnownSpell("Cleanse Spirit");
            bool canCleanMagic = _hasTalentImprovedCleanseSpirit;
            bool canCleanCurse = knowCleanseSpirit;

            WoWPlayer player = (from p in CC_PVP._healTargets
                                where
                                    p.Distance <= 30
                                    && (from dbf in p.Debuffs
                                        where
                                            (dbf.Value.Spell.DispelType == WoWDispelType.Curse && canCleanCurse)
                                            || (dbf.Value.Spell.DispelType == WoWDispelType.Magic && canCleanMagic)
                                            || (dbf.Value.Spell.DispelType == WoWDispelType.Magic && p.IsMe && _hasGlyphOfShamanisticRage)
                                        select dbf.Value
                                        ).Any()
                                select p
                ).FirstOrDefault();

            if (player != null)
            {
                WasHealCast = CleanseIfNeeded(player);
            }

            return WasHealCast;
        }

        private bool HealPlayer(WoWPlayer p)
        {
            SpellWait WAIT_FOR_HEAL_TO_COMPLETE = _me.Combat ? SpellWait.NoWait : SpellWait.Complete;

            if (p == null)
                p = _me;

            bool WasHealCast = false;
            double targetHealth = p.HealthPercent;

            Log("^Heal Target: {0}[{1}] at {2:F1}% dist: {3:F1} in-los: {4}", p.IsMe ? "-me-" : p.Class.ToString(), p.Level, p.HealthPercent, p.Distance, p.InLineOfSight);

            if (!p.IsAlive)
            {
                Dlog("HealPlayer: Heal target is dead");
                return WasHealCast;
            }

            if (!p.IsMe && !IsUnitInRange(p, 35))
            {
                WaitForCurrentSpell(null);
                Dlog("HealPlayer:  moving to heal target who is {0} yds away", p.Distance);
                MoveToUnit(RaFHelper.Leader);
                while (_me.IsMoving && p.IsAlive && !IsUnitInRange(RaFHelper.Leader, 35) && p.Distance < 60)
                {
                    // while running, if someone else needs a heal throw a riptide on them
                    if (Safe_KnownSpell("Riptide") && SpellManager.CanCast("Riptide"))
                    {
                        WoWPlayer otherTarget = CC_PVP.chooseNextHealTarget(p, _cfg.RAF_GroupNeedHeal);
                        if (otherTarget != null)
                        {
                            Slog("HealPlayer:  healing {0} while moving to heal target {1}", Safe_UnitName(otherTarget), Safe_UnitName(p));
                            Safe_CastSpell(otherTarget, "Riptide", SpellRange.Check, SpellWait.NoWait);
                            // StyxWoW.SleepForLagDuration();
                        }
                    }
                }

                if (_me.IsMoving)
                    Safe_StopMoving();

                Dlog("HealRaid:  stopping now that Heal Target {0} is {1} yds away", Safe_UnitName(p), RaFHelper.Leader.Distance);
            }
            else
            {
                Safe_Dismount();
                Safe_StopMoving();

                // wait for any current spell cast... this is overly cautious...
                // if in danger of dying, fastest heal possible
                if (targetHealth < _cfg.EmergencyHealthPercent && _me.Combat)
                {
                    if (Safe_KnownSpell("Nature's Swiftness"))
                    {
                        if (Safe_KnownSpell("Tidal Force"))
                        {
                            Safe_CastSpell("Tidal Force", SpellRange.NoCheck, WAIT_FOR_HEAL_TO_COMPLETE);
                        }

                        WasHealCast = Safe_CastSpell("Nature's Swiftness", SpellRange.NoCheck, WAIT_FOR_HEAL_TO_COMPLETE);
                        if (!WasHealCast)
                            Dlog(" Attempted Oh S@#$ heal but Nature's Swiftness not available");
                        else
                        {
                            if (!WasHealCast && SpellManager.HasSpell("Greater Healing Wave"))
	                            WasHealCast = Safe_CastSpell(p, "Greater Healing Wave", SpellRange.NoCheck, WAIT_FOR_HEAL_TO_COMPLETE);
                            if (!WasHealCast && SpellManager.HasSpell("Healing Surge"))
                                WasHealCast = Safe_CastSpell(p, "Healing Surge", SpellRange.NoCheck, WAIT_FOR_HEAL_TO_COMPLETE);
                            if (!WasHealCast && SpellManager.HasSpell("Healing Wave"))
                                WasHealCast = Safe_CastSpell(p, "Healing Wave", SpellRange.NoCheck, WAIT_FOR_HEAL_TO_COMPLETE);

                            if (WasHealCast)
                                Slog("Big Heals - clicked the Oh S@#$ button!");
                            else
                                Slog("Attempted Oh S@#$ heal but couldn't cast Healing Wave");
                        }
                    }
                }

                if (!WasHealCast && Safe_KnownSpell("Riptide"))
                {
                    WasHealCast = Safe_CastSpell(p, "Riptide", SpellRange.NoCheck, WAIT_FOR_HEAL_TO_COMPLETE);
                }

                if (_pvp.IsBattleground())
                {
                    if (!WasHealCast && !IsCombatOnly())    // if this toon is not supposed to heal others, skip Chain Heal test
                    {
                        if (Safe_KnownSpell("Chain Heal") && WillChainHealHop(p))
                        {
                            WasHealCast = Safe_CastSpell(p, "Chain Heal", SpellRange.NoCheck, WAIT_FOR_HEAL_TO_COMPLETE);
                        }
                    }
                    if (!WasHealCast && Safe_KnownSpell("Healing Surge"))
                    {
                        WasHealCast = Safe_CastSpell(p, "Healing Surge", SpellRange.NoCheck, WAIT_FOR_HEAL_TO_COMPLETE);
                    }
                    if (!WasHealCast)
                    {
                        if (Safe_KnownSpell("Healing Wave"))
                            WasHealCast = Safe_CastSpell(p, "Healing Wave", SpellRange.NoCheck, WAIT_FOR_HEAL_TO_COMPLETE);
                        else
                            Slog("HealPlayer:  need to heal but Healing Wave not trained yet");
                    }
                }
                else if (IsRAF())
                {
#if USE_CHAIN_HEAL_IN_RAF
                    if (!WasHealCast && !IsCombatOnly() && (p.HealthPercent > 65 || !p.Combat))    // if this toon is not supposed to heal others, skip Chain Heal test
                    {
                        if (Safe_KnownSpell("Chain Heal") && WillChainHealHop(p))
                        {
                            WasHealCast = Safe_CastSpell(p, "Chain Heal", SpellRange.NoCheck, WAIT_FOR_HEAL_TO_COMPLETE);
                        }
                    }
#endif
                    if (!WasHealCast && !_me.Combat && !p.Combat && Safe_KnownSpell("Healing Wave") )
                    {
                        Dlog(" Casting Healing Wave because Me and Heal Target are not in combat", countEnemy);
                        WasHealCast = Safe_CastSpell(p, "Healing Wave", SpellRange.NoCheck, WAIT_FOR_HEAL_TO_COMPLETE);
                    }

                    if (!WasHealCast && Safe_KnownSpell("Healing Surge"))
                    {
                        WasHealCast = Safe_CastSpell(p, "Healing Surge", SpellRange.NoCheck, WAIT_FOR_HEAL_TO_COMPLETE);
                    }

                    if (!WasHealCast)
                    {
                        if (Safe_KnownSpell("Healing Wave"))
                            WasHealCast = Safe_CastSpell(p, "Healing Wave", SpellRange.NoCheck, WAIT_FOR_HEAL_TO_COMPLETE);
                        else
                            Slog("HealPlayer:  need to heal but Healing Wave not trained yet");
                    }
                }
                else
                {
                    if (!WasHealCast && !p.Combat && p.HealthPercent < _cfg.EmergencyHealthPercent && Safe_KnownSpell("Greater Healing Wave"))
                    {
                        WasHealCast = Safe_CastSpell(p, "Greater Healing Wave", SpellRange.NoCheck, WAIT_FOR_HEAL_TO_COMPLETE);
                    }

                    if (!WasHealCast && Safe_KnownSpell("Healing Surge"))
                    {
                        WasHealCast = Safe_CastSpell(p, "Healing Surge", SpellRange.NoCheck, WAIT_FOR_HEAL_TO_COMPLETE);
                    }

                    if (!WasHealCast)
                    {
                        if (Safe_KnownSpell("Healing Wave"))
                            WasHealCast = Safe_CastSpell(p, "Healing Wave", SpellRange.NoCheck, WAIT_FOR_HEAL_TO_COMPLETE);
                        else
                            Slog("HealPlayer:  need to heal but Healing Wave not trained yet");
                    }
                }
            }

            return WasHealCast;
        }

        #endregion



        /// <summary>
        /// Custom Class PVP Support.  Provides methods useful for adding PVP support to PVE
        /// based Custom Classes in HonorBuddy.  This includes both general PVP considerations
        /// and healing.
        /// </summary>

        public class CC_PVP
        {
            private volatile LocalPlayer _me = ObjectManager.Me;

            public static List<WoWPlayer> _healTargets;
            private static readonly Stopwatch _refreshTimer = new Stopwatch();

            public static bool _isAlliance;
            public static bool _isHorde;

            public CC_PVP()
            {
                _isAlliance = _me.IsAlliance;
                _isHorde = _me.IsHorde;
                _refreshTimer.Reset();
            }

            ~CC_PVP()
            {
            }

            /// <summary>
            /// Checks to see if you are currently in a Battleground
            /// </summary>
            /// <returns>true if in a battleground, false otherwise</returns>
            public bool IsBattleground()
            {
                //            return false;
                return Battlegrounds.IsInsideBattleground;
            }

            /// <summary>
            /// Chooses a nearby target for healing.  Selection is based upon
            /// which nearby friendly needs heals.  Includes _me in list so
            /// will handle self-healing to you low
            /// </summary>
            /// <returns>WOWPlayer reference of nearby player needing heals</returns>
            public static WoWPlayer chooseHealTarget(double healLessThan, SpellRange rchk)
            {
                // use timer to ensure we aren't constantly rebuilding the list
                // .. player health and distance are very dynamic, but the number of players
                // .. in the vicinity won't change drastically in that time
                //--- NOTE:  timer is initially zero so first call builds list
                if (_refreshTimer.ElapsedMilliseconds > 5000)
                {
                    _refreshTimer.Reset();
                }

                // sort the list by healing priority
                if (!_refreshTimer.IsRunning)
                {
                    CreateHealTargetList();
                    _refreshTimer.Start();
                }

                _healTargets.Sort(CompareHealPriority);
#if LIST_HEAL_TARGETS
                for (int b = 0; b < _healTargets.Count; b++)
                {
                    Slog("  Found: {0}[{1}] at {2:F0}% dist: {3:F1} in-los: {4}", _healTargets[b].Name, _healTargets[b].Level, _healTargets[b].HealthPercent, _healTargets[b].Distance, _healTargets[b].InLineOfSight );
                }
                Slog("  Total of " + _healTargets.Count);
#endif

                for (int a = 0; a < _healTargets.Count; a++)
                {
                    try
                    {
                        if (!Safe_IsValid(_healTargets[a]))
                            continue;

                        // stop looking in sorted list if we reach 85% health
                        if (_healTargets[a].HealthPercent > healLessThan)
                        {
                            // Dlog( "Heal Group:  no player currently below {0}%, range check={1}", healLessThan, rchk);
                            break;
                        }

                        // since we don't rebuild the list each time, always need to retest for dead players
                        if (_healTargets[a].Dead || _healTargets[a].IsGhost || _healTargets[a].HealthPercent < 0.1)
                            ;
                        else if (rchk == SpellRange.NoCheck || (_healTargets[a].Distance < 37 && _healTargets[a].InLineOfSight))
                        {
                            // Slog("Heal Target: {0}[{1}] at {2:F0}% dist: {3:F1} in-los: {4}", _healTargets[a].Name, _healTargets[a].Level, _healTargets[a].HealthPercent, _healTargets[a].Distance, _healTargets[a].InLineOfSight);
                            return _healTargets[a];
                        }
                    }
                    catch (ThreadAbortException) { throw; }
                    catch
                    {
                        // if exception dealing with this WoWUnit, then try next in array
                    }
                }

                return null;
            }

            public static WoWPlayer chooseNextHealTarget(WoWUnit currHealTarget, double healLessThan)
            {
                // use timer to ensure we aren't constantly rebuilding the list
                // .. player health and distance are very dynamic, but the number of players
                // .. in the vicinity won't change drastically in that time
                //--- NOTE:  timer is initially zero so first call builds list
                for (int a = 0; a < _healTargets.Count; a++)
                {
                    try
                    {
                        if (!Safe_IsValid(_healTargets[a]))
                            continue;

                        if (_healTargets[a].HealthPercent > healLessThan)
                            break;

                        if (_healTargets[a].CurrentHealth <= 1)
                            ;
                        else if (_healTargets[a] == currHealTarget)
                            ;
                        else if (IsUnitInRange(_healTargets[a], 37))
                        {
                            // Slog("Heal Target: {0}[{1}] at {2:F0}% dist: {3:F1} in-los: {4}", _healTargets[a].Name, _healTargets[a].Level, _healTargets[a].HealthPercent, _healTargets[a].Distance, _healTargets[a].InLineOfSight);
                            return _healTargets[a];
                        }
                    }
                    catch (ThreadAbortException) { throw; }
                    catch
                    {
                        // if exception dealing with this WoWUnit, then try next in array
                        continue;
                    }
                }

                return null;
            }


            /// <summary>
            /// Chooses a nearby target for healing.  Selection is based upon
            /// which nearby friendly needs heals.  Includes _me in list so
            /// will handle self-healing to you low
            /// </summary>
            /// <returns>WOWPlayer reference of nearby player needing heals</returns>
            public static WoWPlayer chooseDispelTarget(double healLessThan)
            {
                // use timer to ensure we aren't constantly rebuilding the list
                // .. player health and distance are very dynamic, but the number of players
                // .. in the vicinity won't change drastically in that time
                //--- NOTE:  timer is initially zero so first call builds list


                for (int a = 0; a < _healTargets.Count; a++)
                {
                    try
                    {
                        if (!Safe_IsValid(_healTargets[a]))
                            continue;
                    }
                    catch (ThreadAbortException) { throw; }
                    catch
                    {
                        // if exception dealing with this WoWUnit, then try next in array
                        continue;
                    }

                    // stop looking in sorted list if we reach 85% health
                    if (_healTargets[a].HealthPercent > healLessThan)
                    {
                        break;
                    }

                    // since we don't rebuild the list each time, always need to retest for dead players
                    if (_healTargets[a].Dead || _healTargets[a].IsGhost || _healTargets[a].HealthPercent < 0.1)
                        ;
                    else if (_healTargets[a].Distance < 37 && _healTargets[a].InLineOfSight)
                    {
                        // Slog("Heal Target: {0}[{1}] at {2:F0}% dist: {3:F1} in-los: {4}", _healTargets[a].Name, _healTargets[a].Level, _healTargets[a].HealthPercent, _healTargets[a].Distance, _healTargets[a].InLineOfSight);
                        return _healTargets[a];
                    }
                }

                return null;
            }


            // sort in ascending order by Health percent
            //  ..  null pointers or dead's should be at end of list
            private static int CompareHealPriority(WoWUnit x, WoWUnit y)
            {
                try
                {
                    // handle nulls so that they fall to end of list
                    if (x == null || !x.IsAlive)
                        return (y == null || !y.IsAlive ? 0 : 1);
                    else if (y == null || !y.IsAlive)
                        return -1;

                    // sort 
                    double healthDiff = x.HealthPercent - y.HealthPercent;

                    if (healthDiff < 0.0)
                        return -1;

                    if (healthDiff > 0.0)
                        return 1;
                }
                catch (ThreadAbortException) { throw; }
                catch
                {
                    Dlog(">>> EXCEPTION: a heal target released or rezzed -- ignoring");
                }

                return 0;

                /*
                 * -- Eventually determine a priority based upon general health, 
                 * -- targets survivability, and targets savability (my word).
                 * -- this would factor in can they be saved, are they a plater 
                 * -- wearer, do they have a self-heal and mana, etc.
                 * 
                const double _priorityTiers = 5;

                int xHealthPriority = (int)Math.Ceiling(x.HealthPercent / _priorityTiers);
                int yHealthPriority = (int)Math.Ceiling(y.HealthPercent / _priorityTiers);

                return xHealthPriority - yHealthPriority;
                */
            }

            private static void CreateHealTargetList()
            {
                List<WoWPlayer> plist = ObjectManager.Me.RaidMembers.Count > 0 ? ObjectManager.Me.RaidMembers : ObjectManager.Me.PartyMembers;
                if (!plist.Contains(ObjectManager.Me))
                {
                    plist.Add(ObjectManager.Me);
                    Dlog("Create Heal List:  added Me to list");
                }
#if true
                _healTargets = plist.FindAll(
                    unit => unit.Distance <= 50
                            && unit.HealthPercent >= 1
                    // && unit.IsAlive 
                    // && unit.Guid != ObjectManager.Me.Guid
                    );
#else
            _healTargets = new List<WoWPlayer>();
            foreach (WoWPlayer p in plist)
            {
                if (p.Distance > 50 || p.Dead)
                    continue;

                if (p.Guid == _me.Guid)
                    continue;

                if (p.IsHorde != _me.IsHorde)
                    continue;

                if (p.HealthPercent < 0.1)         // players waiting on spirit rez pass the 
                    continue;                       //  .. the unit.Dead test above for some reason

                //               Slog("Heal target: " + p.Class + "-" + p.Name + "[" + p.Level + "]");
                _healTargets.Add(p);
            }
#endif
                if (_healTargets.Count > 1)
                {
                    _healTargets.Sort(CompareHealPriority);
                }
            }
        }

        public class CC_TalentGroup
        {
            public string[] _tabName = new string[4];
            public int[] _tabPoints = new int[4];
            public int _idxGroup;
            public Dictionary<uint, string> _glyphs;

            public int totalPoints
            {
                get
                {
                    int nPoints = 0;
                    for (int iTab = 1; iTab <= 3; iTab++)
                    {
                        nPoints += _tabPoints[iTab];
                    }

                    return nPoints;
                }
            }

            public int unspentPoints
            {
                get
                {
                    return Lua.GetReturnVal<int>("return GetUnspentTalentPoints(false,false," + _idxGroup + ")", 0);
                }

            }

            public int Spec()
            {
                int nSpec = 0;
                if (_tabPoints[1] > 0 && _tabPoints[1] >= _tabPoints[2] && _tabPoints[1] >= _tabPoints[3])
                    nSpec = 1;
                else if (_tabPoints[2] > 0 && _tabPoints[2] >= _tabPoints[1] && _tabPoints[2] >= _tabPoints[3])
                    nSpec = 2;
                else if (_tabPoints[3] > 0 && _tabPoints[3] >= _tabPoints[1] && _tabPoints[3] >= _tabPoints[2])
                    nSpec = 3;

                return nSpec;
            }

            public bool Load(int nGroup)
            {
                int nTab;

                _idxGroup = nGroup;

                LoadGlyphs(_idxGroup);
                for (nTab = 1; nTab <= 3; nTab++)
                {
                    try
                    {
                        string sLuaCmd = "return GetTalentTabInfo(" + nTab + ",false,false," + _idxGroup + ")";
                        // Dlog( sLuaCmd );
                        List<string> tabInfo = Lua.GetReturnValues(sLuaCmd, "hawker.lua");

                        if (Equals(null, tabInfo))
                            return false;

                        _tabName[nTab] = tabInfo[1];
                        _tabPoints[nTab] = Convert.ToInt32(tabInfo[4]);
                    }
                    catch (ThreadAbortException) { throw; }
                    catch (Exception ex)
                    {
                        Log(Color.Red, "An Exception occured. Check debug log for details.");
                        Logging.WriteDebug(">>> Exception in CC_TalentGroup.Load()");
                        Logging.WriteException(ex);
                    }

                }

                return false;
            }

            public bool IsActiveGroup()
            {
                return GetActiveGroup() == _idxGroup;
            }

            public int GetActiveGroup()
            {
                List<string> activeTalents = Lua.GetReturnValues("return GetActiveTalentGroup(false,false)", "hawker.lua");

                if (!Equals(null, activeTalents))
                {
                    int nActiveGroup = Convert.ToInt32(activeTalents[0]);
                    return nActiveGroup;
                }
                return 0;
            }

            public void ActivateGroup()
            {
                Lua.DoString("SetActiveTalentGroup(" + _idxGroup + ")", "hawker.lua");
            }

            public int GetNumGroups()
            {
                List<string> numberOfGroups = Lua.GetReturnValues("return GetNumTalentGroups(false,false)", "hawker.lua");

                if (!Equals(null, numberOfGroups))
                {
                    int cntGroup = Convert.ToInt32(numberOfGroups[0]);
                    return cntGroup;
                }
                return 0;
            }

            public int GetTalentInfo(int idxTab, int idxTalent)
            {
                string sLuaCmd = String.Format("return GetTalentInfo( {0}, {1});", idxTab, idxTalent);
                List<string> retList = Lua.GetReturnValues(sLuaCmd, "hawker.lua");
                if (Equals(null, retList))
                {
                    Slog("ERROR:  Talent {0}, {1} does not exist -- Notify CC developer", idxTab, idxTalent);
                    return 0;
                }

                return Convert.ToInt32(retList[4]);
            }

            private void LoadGlyphs(int iGroup)
            {
                int iSocket;
                uint countSockets = Lua.GetReturnVal<uint>("return GetNumGlyphSockets()", 0);

                _glyphs = new Dictionary<uint, string>();
                for (iSocket = 1; iSocket <= countSockets; iSocket++)
                {
                    List<string> ret = Lua.GetReturnValues("return GetGlyphSocketInfo(" + iSocket + ", " + iGroup + ")");
                    if (ret != null && ret.Count > 0)
                    {
                        if (ret[0] == "1" && !string.IsNullOrEmpty(ret[3]) && ret[3] != "nil")
                        {
                            uint glyphId = Convert.ToUInt32(ret[3]);
                            string glyphName = Lua.GetReturnVal<string>("return GetSpellInfo(" + glyphId + ")", 0);
                            if (!string.IsNullOrEmpty(glyphName) && glyphName != "nil")
                            {
                                _glyphs.Add(glyphId, glyphName);
                            }
                        }
                    }
                }
            }
        }


        // public override bool WantButton { get { return true; } }
        public override bool WantButton
        { get { return true; } }

#if    BUNDLED_WITH_HONORBUDDY

        public override void OnButtonPress()
        {
            Process.Start("\"" + ConfigFilename + "\"");
        }

#else
        private ConfigForm _frm;

        public override void OnButtonPress()
        {
            if (_frm == null)
                _frm = new ConfigForm();

            Dlog(" About to show dialog");
            System.Windows.Forms.DialogResult rc = _frm.ShowDialog();
            if (rc == System.Windows.Forms.DialogResult.OK)
            {
                _cfg.DebugDump();
                try
                {
                    _cfg.Save(ConfigFilename);
                    Log("Options saved to ShamWOW-realm-char.config");
                }
                catch (ThreadAbortException) { throw; }
                catch (Exception e)
                {
                    Log(Color.Red, "An Exception occured. Check debug log for details.");
                    Logging.WriteDebug("EXCEPTION Saving to ShamWOW-realm-char.config");
                    Logging.WriteException(e);
                }

                // TotemSetupBar(); // just for debug atm
                _needTotemBarSetup = true;
                _needClearWeaponEnchants = true;
            }
        }
#endif

    }
}

