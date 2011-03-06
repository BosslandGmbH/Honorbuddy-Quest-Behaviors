using System.Collections.Generic;
using System.Linq;
using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using System.Diagnostics;
using Styx.Logic.Combat;
using Styx.Logic;

using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// CombatMoveTo by CombatMoveTo
    /// Moves to location while in a vehicle
    /// ##Syntax##
    /// MinHp:(optional) This is the min Health precent at which this behavior will alow the CC to take care of aggro, (default): 25
    /// QuestId:(optional) Id of the quest this behavior belongs to
    /// X,Y,Z: The location where you want to move to
    /// </summary>
    /// 
    public class CombatMoveTo : CustomForcedBehavior
    {
        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {
            {"QuestId",null},
            {"MinHp",null},
            {"X",null},
            {"Y",null},
            {"Z",null},
        };
        bool success = true;
        public CombatMoveTo(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);
            int questId = 0;
            int minHp = 0;
            WoWPoint point = WoWPoint.Empty;

            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
            success = success && GetAttributeAsInteger("MinHp", false, "25", 0, int.MaxValue, out minHp);
            success = success && GetXYZAttributeAsWoWPoint("X", "Y", "Z", true, WoWPoint.Empty, out point);
            if (!success)
                Err("Invalid or missing Attributes, Stopping HB");
            QuestId = questId;
            Location = point;
            MinHp = minHp;
        }

        public int QuestId { get; private set; }
        public int MinHp { get; private set; }
        public WoWPoint Location { get; private set; }
        LocalPlayer me = ObjectManager.Me;

        #region Overrides of CustomForcedBehavior
        private Composite root;
        protected override Composite CreateBehavior()
        {
            return root ??
                (root = new PrioritySelector(
                    new Action(c =>
                    {  
                        if (me.HealthPercent <= MinHp && me.Combat)
                            return RunStatus.Failure;
                        if (me.Location.Distance(Location) > 4)
                        {
                            if (!me.Mounted && Mount.CanMount() && Styx.Helpers.LevelbotSettings.Instance.UseMount &&
                                me.Location.Distance(Location) > 35)
                            {
                                if (ObjectManager.Me.IsMoving)
                                {
                                    WoWMovement.MoveStop();
                                    return RunStatus.Running;
                                }
                                Mount.MountUp();
                            }
                            else
                            {
                                Navigator.MoveTo(Location);
                            }
                            return RunStatus.Running;
                        }
                        else
                            isDone = true;
                        return RunStatus.Success;
                    })
                ));
        }

        void Err(string format, params object[] args)
        {
            Logging.Write(System.Drawing.Color.Red, "CombatMoveTo: " + format, args);
            TreeRoot.Stop();
        }

        void Log(string format, params object[] args)
        {
            Logging.Write("CombatMoveTo: " + format, args);
        }

        private bool isDone = false;

        public override bool IsDone
        {
            get
            {
                var quest = ObjectManager.Me.QuestLog.GetQuestById((uint)QuestId);
                return isDone || (QuestId > 0 && ((quest != null && quest.IsCompleted) || quest == null));
            }
        }
        public override void OnStart()
        {
            TreeRoot.GoalText = string.Format("Moving to:{0}",Location);
        }

        #endregion
    }
}
