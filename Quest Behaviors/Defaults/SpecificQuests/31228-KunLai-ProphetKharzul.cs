﻿// Behavior originally contributed by HighVoltz.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

// NOTICE: This behavior is writen specificly for the Prophet Khar'zul quest ( http://www.wowhead.com/quest=31228 ) and should not be used for anything else.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;

namespace GetOutOfGroundEffectAndAuras
{
    internal class ProphetKharzulBehavior : CustomForcedBehavior
    {
        private const uint ProphetKharzulId = 61541;

        private const int QuestId = 31228;
        private const uint InvisibleManId = 64693;
        private const uint CrowStormId = 64678;
        private readonly WoWPoint _platformPoint = new WoWPoint(5236.239, 58.70723, 33.64487);
        private Composite _behaviorTreeHook_Combat;
        private bool _isDisposed;

        private IPlayerMover _orginalPlayerMover;
        private IStuckHandler _orginalStuckHandler;
        private WoWPoint _prophetLocation = new WoWPoint(5246.656, 54.33482, 31.962);
        public ProphetKharzulBehavior(Dictionary<string, string> args) : base(args) { }

        private LocalPlayer Me
        {
            get { return StyxWoW.Me; }
        }

        public override bool IsDone
        {
            get
            {
                WoWDescriptorQuest questData;
                var quest = Me.QuestLog.GetQuestById(QuestId);
                //return false;
                return quest == null || quest.IsCompleted; //|| quest.GetData(out questData) && questData.ObjectivesDone[1] == 1;
            }
        }

        private WoWUnit ProphetKharzul
        {
            get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == ProphetKharzulId && u.IsAlive); }
        }

        public override void OnStart()
        {
            BotEvents.OnBotStop += BotEvents_OnBotStop;
            _behaviorTreeHook_Combat = CreateCombatBehavior();
            TreeHooks.Instance.InsertHook("Combat_Main", 0, _behaviorTreeHook_Combat);
        }

        protected override Composite CreateBehavior()
        {
            return new PrioritySelector(
                ctx => ProphetKharzul,
                new Decorator(
                    ctx => Targeting.Instance.FirstUnit == null && ctx == null || ((WoWUnit)ctx).Distance > 30, new Action(ctx => Navigator.MoveTo(_prophetLocation))),
                new Decorator<WoWUnit>(
                    prophet => prophet.Distance <= 30 && !prophet.Combat && Targeting.Instance.FirstUnit == null && BotPoi.Current.Type == PoiType.None,
                    new Action<WoWUnit>(prophet => BotPoi.Current = new BotPoi(prophet, PoiType.Kill))));
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected Composite CreateCombatBehavior()
        {
            // NB: This behavior is hooked in at a 'higher priority' than Combat_Main.  We need this
            // because it is sometimes more important to avoid things than fight.

            // NB: We might be in combat with nothing to fight immediately after initiating the event.
            // Be aware of this when altering this behavior.
            bool shadowFogActive = false;
            // WoWUnit prophet = null;
            WoWUnit crowStorm = null;
            return new PrioritySelector(
                ctx =>
                {
                    shadowFogActive = ObjectManager.GetObjectsOfType<WoWUnit>().Any(u => u.Entry == InvisibleManId && u.HasAura("Shadow Fog"));
                    return ProphetKharzul;
                },
                new Decorator<WoWUnit>(
                    prophet => prophet != null && prophet.ThreatInfo.ThreatValue > 0,
                    new PrioritySelector(
                        new Decorator(
                            ctx => shadowFogActive,
                            new PrioritySelector(
                // check if we need to move to platform during Shadow Fog. Ignore Shadow Fog if the prophet is not within melee range of the platform and I'm a melee.
                                new Decorator<WoWUnit>(
                                    prophet =>
                                    Me.Location.Distance(_platformPoint) > 2 ||
                                    Me.Z < 33,
                                    new PrioritySelector(
                                        new Decorator(ctx => _platformPoint.Distance(Me.Location) > 20, new Action(ctx => Navigator.MoveTo(_prophetLocation))),
                                        new Decorator(
                                            ctx => _platformPoint.Distance(Me.Location) <= 20,
                                            new Action<WoWUnit>(
                                                prophet =>
                                                {
                                                    if (IsMeleeSpec(Me.Specialization) && prophet.Location.Distance(_platformPoint) >= MeleeRange(prophet))
                                                    {
                                                        Logging.Write("I am melee and Prophet K is not within melee range of platform so I will ignore shadow fog");
                                                        return RunStatus.Failure;
                                                    }
                                                    TreeRoot.StatusText = "Moving to platform";

                                                    Navigator.PlayerMover.MoveTowards(_platformPoint);
                                                    // we need to jump to get up on the platform
                                                    if (!Me.MovementInfo.IsAscending && _platformPoint.Distance(Me.Location) <= 7 && Me.Z < 33)
                                                        WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
                                                    return RunStatus.Success;
                                                })))),
                // stop jumping after we're on top of platform.
                                new Decorator(ctx => Me.MovementInfo.IsAscending, new Action(ctx => WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend))))),
                // Handle Crow Storm. Assume the crow storm is placed on top of platform...
                        new Decorator(
                            ctx => (crowStorm = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == CrowStormId && u.Distance < 6.5f)) != null,
                            new PrioritySelector(
                                new ActionSetActivity("Moving out of Crow Storm"),
                                new Action<WoWUnit>(prophet => Navigator.PlayerMover.MoveTowards(WoWMathHelper.CalculatePointFrom(prophet.Location, crowStorm.Location, 7.5f))))))));
        }


        ~ProphetKharzulBehavior()
        {
            Dispose(false);
        }


        public void Dispose(bool isExplicitlyInitiatedDispose)
        {
            if (!_isDisposed)
            {
                // NOTE: we should call any Dispose() method for any managed or unmanaged
                // resource, if that resource provides a Dispose() method.

                // Clean up managed resources, if explicit disposal...
                if (isExplicitlyInitiatedDispose)
                {
                    // empty, for now
                }

                // Clean up unmanaged resources (if any) here...
                if (_behaviorTreeHook_Combat != null)
                {
                    TreeHooks.Instance.RemoveHook("Combat_Main", _behaviorTreeHook_Combat);
                    _behaviorTreeHook_Combat = null;
                }

                BotEvents.OnBotStop -= BotEvents_OnBotStop;
                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }

        public void BotEvents_OnBotStop(EventArgs args)
        {
            Dispose();
        }

        private static bool IsRangeSpec(WoWSpec spec)
        {
            switch (spec)
            {
                case WoWSpec.HunterBeastMastery:
                case WoWSpec.HunterMarksmanship:
                case WoWSpec.HunterSurvival:

                case WoWSpec.MageArcane:
                case WoWSpec.MageFire:
                case WoWSpec.MageFrost:

                case WoWSpec.PriestDiscipline:
                case WoWSpec.PriestHoly:
                case WoWSpec.PriestShadow:

                case WoWSpec.WarlockAffliction:
                case WoWSpec.WarlockDemonology:
                case WoWSpec.WarlockDestruction:

                case WoWSpec.ShamanElemental:
                case WoWSpec.ShamanRestoration:

                case WoWSpec.PaladinHoly:

                case WoWSpec.DruidBalance:
                case WoWSpec.DruidRestoration:

                case WoWSpec.MonkMistweaver:

                    return true;
                default:
                    return false;
            }
        }

        private static bool IsMeleeSpec(WoWSpec spec)
        {
            return !IsRangeSpec(spec);
        }

        private static float MeleeRange(WoWUnit unit)
        {
            if (unit == null)
                return 0f;
            if (unit.IsPlayer)
                return 3.5f;

            return Math.Max(4.5f, StyxWoW.Me.CombatReach + 1f + unit.CombatReach);
        }

        #region Tree Sharp Extensions.

        /// <summary>
        ///     Uses a strongly typed context
        /// </summary>
        /// <typeparam name="T"> The context type </typeparam>
        public class Action<T> : Action
        {
            private readonly Func<T, RunStatus> _actionRunner;
            private readonly System.Action<T> _successRunner;

            public Action(Func<T, RunStatus> actionRunner)
            {
                _actionRunner = actionRunner;
            }

            public Action(System.Action<T> successRunner)
            {
                _successRunner = successRunner;
            }

            protected override RunStatus Run(object context)
            {
                if (!(context is T))
                    return RunStatus.Failure;

                if (_actionRunner != null)
                    return _actionRunner((T)context);

                _successRunner((T)context);
                return RunStatus.Success;
            }
        }

        /// <summary>
        ///     Uses a strongly typed context
        /// </summary>
        /// <typeparam name="T"> The context type </typeparam>
        public class Decorator<T> : Decorator
        {
            private readonly Predicate<T> _canRun;

            public Decorator(Predicate<T> canRun, Composite decorated)
                : base(decorated)
            {
                _canRun = canRun;
            }

            protected override bool CanRun(object context)
            {
                return context is T && _canRun((T)context);
            }
        }

        #endregion
    }
}