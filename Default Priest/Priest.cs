using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using System.Net;
using System.Windows.Forms;
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
        #region Variables

        private static bool wasCasting;
        private static string CurrentSpec;
        public delegate bool CastRequirements(WoWUnit unit);
        private static TalentManager _talentManager = new TalentManager();
        public DefaultPriestSettings Settings { get; set; }
        public static Priest Instance { get; set; }
        private static LocalPlayer Me { get { return ObjectManager.Me; } }
        private static WoWUnit CT { get { return Me.CurrentTarget; } }
        #endregion

        #region Overrides

        private static readonly Version pVersion = new Version(1, 0, 0);
        public override sealed string Name { get { return string.Format("DefaultPriest - by raphus", pVersion); } }
        public override WoWClass Class
        {   
            get 
            {
                return WoWClass.Priest;
            }
        }
        public override bool WantButton { get { return false; } }
        public override void OnButtonPress()
        {
        }

        #endregion

        #region Init

        public Priest()
        {
            Instance = this;
            Settings = new DefaultPriestSettings();
        }

        public override void Initialize()
        {
            Lua.Events.AttachEvent("ACTIVE_TALENT_GROUP_CHANGED", HandleTalentChange);
            Lua.Events.AttachEvent("COMBAT_LOG_EVENT_UNFILTERED", HandleCombatLogEvent);
            
            //Lua.Events.AttachEvent("UNIT_SPELLCAST_SUCCEEDED", HandleSpellSucceededEvent);
            //Lua.Events.AttachEvent("UNIT_SPELLCAST_CHANNEL_STOP", HandleSpellSucceededEvent);

            Log("DefaultPriest has been loaded successfully");
            CurrentSpec = ((PriestTalentSpec)_talentManager.Spec).ToString();
            Log("Your current spec is {0}", CurrentSpec);
        }

        #endregion

        #region Pull Buff Behavior

        private Composite _pullBuffBehavior;
        public override Composite PullBuffBehavior
        {
            get { if (_pullBuffBehavior == null) { _pullBuffBehavior = CreatePullBuffBehavior; } return _pullBuffBehavior; }
        }

        #endregion

        #region Pull Behavior

        private Composite _pullBehavior;
        public override Composite PullBehavior
        {
            get { if (_pullBehavior == null) { _pullBehavior = CreatePullBehavior; } return _pullBehavior; }
        }

        #endregion

        #region Combat Behavior

        private Composite _combatBehavior;
        public override Composite CombatBehavior
        {
            get { if (_combatBehavior == null) { _combatBehavior = CreateCombatBehavior; } return _combatBehavior; }
        }

        #endregion

        #region Combat Buff Behavior

        private Composite _combatBuffBehavior;
        public override Composite CombatBuffBehavior
        {
            get { if (_combatBuffBehavior == null) { _combatBuffBehavior = CreateCombatBuffBehavior; } return _combatBuffBehavior; }
        }

        #endregion

        #region Heal Behavior

        private Composite _healBehavior;
        public override Composite HealBehavior
        {
            get { if (_healBehavior == null) { _healBehavior = CreateHealBehavior; } return _healBehavior; }
        }   

        #endregion

        #region Rest Behavior

        private Composite _restBehavior;
        public override Composite RestBehavior
        {
            get { if (_restBehavior == null) { _restBehavior = CreateRestBehavior; } return _restBehavior; }
        }

        #endregion

        #region Pre Combat Buff Behavior

        private Composite _preCombatBuffBehavior;
        public override Composite PreCombatBuffBehavior
        {
            get { if (_preCombatBuffBehavior == null) { _preCombatBuffBehavior = CreatePreCombatBuffsBehavior; } return _preCombatBuffBehavior; }
        }

        #endregion

        #region Pulse

        public override void Pulse()
        {
        }

        #endregion
    }
}
