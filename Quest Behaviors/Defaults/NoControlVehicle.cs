using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Combat;
using Styx.Helpers;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// NoControlVehicle by Nesox, allows you to do quests that
    /// requires you to use a vehicle you can't control eg; Bomb Runs etc. 
    /// ##Syntax##
    /// QuestId: Id of the quest
    /// MobId: Id of the mobs to kill/throw bombs at.
    /// NumOfTimes: Number of times to cast bombs.
    /// AttackIndex: The index of the attack/bomb spell usally 1
    /// HomeIndex: Normally you return by yourself when ure done but incase you have to click to do so put the index of the 'home' here.
    /// MaxRange: The maxrange of the bombs/attack spell
    /// WaitTime: How long time to wait before attacking/throwing another bomb.
    /// </summary>
    public class NoControlVehicle : CustomForcedBehavior
    {
        public NoControlVehicle(Dictionary<string, string> args)
            : base(args)
        {
            bool error = false;

            uint questId;
            if (!uint.TryParse(Args["QuestId"], out questId))
            {
                Logging.Write("Parsing attribute 'QuestId' in BombRun behavior failed! please check your profile!");
                error = true;
            }

            uint mobId;
            if (!uint.TryParse(Args["MobId"], out mobId))
            {
                Logging.Write("Parsing attribute 'MobId' in BombRun behavior failed! please check your profile!");
                error = true;
            }

            int numOfTimes;
            if (!int.TryParse(Args["NumOfTimes"], out numOfTimes))
            {
                Logging.Write("Parsing attribute 'NumOfTimes' in BombRun behavior failed! please check your profile!");
                error = true;
            }

            int attackIndex;
            if (!int.TryParse(Args["AttackIndex"], out attackIndex))
            {
                Logging.Write("Parsing attribute 'AttackIndex' in BombRun behavior failed! please check your profile!");
                error = true;
            }

            int homeIndex;
            if (!int.TryParse(Args["HomeIndex"], out homeIndex))
            {
                Logging.Write("Parsing attribute 'HomeIndex' in BombRun behavior failed! please check your profile!");
                error = true;
            }

            int maxRange;
            if (!int.TryParse(Args["MaxRange"], out maxRange))
            {
                Logging.Write("Parsing attribute 'MaxRange' in BombRun behavior failed! please check your profile!");
                error = true;
            }

            int waitTime;
            if (!int.TryParse(Args["WaitTime"], out waitTime))
            {
                Logging.Write("Parsing attribute 'WaitTime' in BombRun behavior failed! please check your profile!");
                error = true;
            }

            if (error)
            {
                TreeRoot.Stop();
                Logging.Write(Color.Red, "Honorbuddy Stopped!");
            }

            QuestId = questId;
            MobId = mobId;
            NumOfTimes = numOfTimes;
            AttackIndex = attackIndex;
            HomeIndex = homeIndex;
            MaxRange = maxRange;
            WaitTime = waitTime;
        }

        /// <summary>
        /// Id of the quest
        /// </summary>
        public uint QuestId { get; private set; }

        /// <summary>
        /// Id of the mobs to kill/throw bombs at.
        /// </summary>
        public uint MobId { get; private set; }

        /// <summary>
        /// Number of times to cast bombs.
        /// </summary>
        public int NumOfTimes { get; private set; }

        /// <summary>
        /// The index of the attack/bomb spell usally 1
        /// </summary>
        public int AttackIndex { get; private set; }

        /// <summary>
        /// Normally you return by yourself when ure done but incase you have to click to do so put the index of the 'home' here.
        /// </summary>
        public int HomeIndex { get; private set; }

        /// <summary>
        /// The maxrange of the bombs/attack spell
        /// </summary>
        public int MaxRange { get; private set; }

        /// <summary>
        /// How long time to wait before attacking/throwing another bomb.
        /// </summary>
        public int WaitTime { get; private set; }

        /// <summary> True if player is in a vehicle. </summary>
        public bool IsInVehicle { get { return Lua.GetReturnVal<bool>("return UnitInVehicle('player')", 0); } }

        /// <summary> List of mobs to be killed. </summary>
        public List<WoWUnit> Mobs
        {
            get
            {
                WoWObject transport = StyxWoW.Me.Transport;
                WoWPoint location = StyxWoW.Me.Location;

                if (transport != null)
                {
                    return ObjectManager.GetObjectsOfType<WoWUnit>().Where(
                        ret => ret.Entry == MobId && !ret.IsSwimming && ret.IsAlive && transport.Location.Distance(ret.Location) <= MaxRange).ToList();
                }

                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(
                    ret => ret.Entry == MobId && !ret.IsSwimming && ret.IsAlive && location.Distance(ret.Location) <= MaxRange).ToList();
            }
        }

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

            if (quest != null)
                TreeRoot.GoalText = "BombRun for quest - " + quest.Name;
        }

        private int _counter;

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                    new Wait(60, ret => IsInVehicle,
                        new PrioritySelector(

                            new Decorator(ret => _counter >= NumOfTimes,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Going home!"),
                                    new Action(ret => Lua.DoString("CastPetAction({0})", HomeIndex)),
                                    new WaitContinue(120, ret => !IsInVehicle,
                                        new Action(delegate
                                        {
                                            _isDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )),

                            new Decorator(ret => Mobs.Count > 0,
                                new Sequence(

                                    new Action(ret => TreeRoot.StatusText = "Killing shit from above!"),
                                    new Action(ret => Lua.DoString("CastPetAction({0})", AttackIndex)),
                                    new Action(ret => Thread.Sleep(150)),
                                    new Action(ret => LegacySpellManager.ClickRemoteLocation(Mobs[0].Location)),
                                    new Action(ret => Thread.Sleep(WaitTime)),
                                    new Action(ret => _counter++)
                                    )
                            )
                        ))
                    ));
        }

        private bool _isDone;
        public override bool IsDone
        {
            get
            {
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

                return
                    _isDone ||
                    (quest != null && quest.IsCompleted) ||
                    quest == null;
            }
        }
    }
}
