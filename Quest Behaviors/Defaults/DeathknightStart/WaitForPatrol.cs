using System.Collections.Generic;
using System.Linq;
using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Styx.Logic;

using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// WaitForPatrol by HighVoltz (cleaned up by Nesox)
    /// Waits at a safe location until an NPC is X distance way from you.. Useful for the quest in dk starter area where you have to ninja a horse but have to stay away from the stable master
    /// ##Syntax##
    /// MobId: This is the ID of the bad boy you want to stay clear of 
    /// QuestId: (Optional) The Quest to perform this behavior on
    /// Distance: The Distance to stay away from 
    /// X,Y,Z: The Safe Location location where you want wait at.
    /// </summary>
    public class WaitForPatrol : CustomForcedBehavior
    {
        readonly Dictionary<string, object> _recognizedAttributes = new Dictionary<string, object>()
        {
            {"MobId", null},
            {"Distance", null},
            {"QuestId", null},
            {"X", null},
            {"Y", null},
            {"Z", null},
        };

        readonly bool _success = true;

        public WaitForPatrol(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(_recognizedAttributes);

            int mobId = 0;
            int distance = 0;
            int questId= 0;
            WoWPoint point = WoWPoint.Empty;

            _success = _success && GetAttributeAsInteger("MobId", true, "0", 0, int.MaxValue, out mobId);
            _success = _success && GetAttributeAsInteger("Distance", true, "0", 0, int.MaxValue, out distance);
            _success = _success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
            _success = _success && GetXYZAttributeAsWoWPoint("X", "Y", "Z", true, WoWPoint.Empty, out point);

            if (!_success)
                Err("Error loading Profile\nStoping HB");

            MobId = mobId;
            QuestId = questId;
            Distance = distance;
            Location = point;
        }

        /// <summary> Id of the mob to avoid. </summary>
        public int MobId { get; private set; }

        /// <summary> Distance to stay away from </summary>
        public int Distance { get; private set; }

        /// <summary> Id of the quest, 0 otherwise </summary>
        public int QuestId { get; private set; }

        /// <summary> Safespot. </summary>
        public WoWPoint Location { get; private set; }

        /// <summary> The Npc we want to avoid </summary>
        public WoWObject Npc
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>(true).Where(o => o.Entry == MobId).
                    OrderBy(o => o.Distance).FirstOrDefault();
            }
        }

        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        #region Overrides of CustomForcedBehavior

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ??(_root = 
                new PrioritySelector(
                    
                    new Decorator(c => Me.Location.Distance(Location) > 4,

                        new PrioritySelector(
                            
                            new Decorator(ret => !Me.Mounted && Mount.CanMount() && LevelbotSettings.Instance.UseMount && Me.Location.Distance(Location) > 35,
                                new Sequence(
                                    new DecoratorContinue(ret => Me.IsMoving,
                                        new Sequence(
                                            new Action(ret => WoWMovement.MoveStop()),
                                            new Action(ret => StyxWoW.SleepForLagDuration()) 
                                                )),

                                    new Action(ret => Mount.MountUp()))),

                            new Action(ret => Navigator.MoveTo(Location)))),
                                    
                            new Decorator(c => Npc != null && Npc.Distance <= Distance,
                                new Action(c => Log("Waiting on {0} to move {1} distance away", Npc, Distance))),

                            new Decorator(c => Npc == null || Npc.Distance > Distance,
                                new Action(c => _isDone = true))
                            )
                );
        }


        private static void Err(string format, params object[] args)
        {
            Logging.Write(System.Drawing.Color.Red, "WaitForPatrol: " + format, args);
            TreeRoot.Stop();
        }

        private static void Log(string format, params object[] args)
        {
            Logging.Write("WaitForPatrol: " + format, args);
        }

        private bool _isDone;
        public override bool IsDone
        {
            get
            {
                var quest = ObjectManager.Me.QuestLog.GetQuestById((uint)QuestId);
                return _isDone || (QuestId > 0 && ((quest != null && quest.IsCompleted) || quest == null));
            }
        }

        public override void OnStart()
        {
            TreeRoot.GoalText = string.Format("Moving to safepoint {0} then waiting there until Npc {1} moves {2} distance away",Location,MobId,Distance);
        }

        #endregion
    }
}
