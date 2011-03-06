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
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic;

using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// TalkAndFollow by TalkAndFollow
    /// Talks to a NPC and follows around until quest completes
    /// ##Syntax##
    /// NpcId: Id of the NPC to talk to and follow until quest completes
    /// QuestId: Id of the quest to perform this behavior on
    /// X,Y,Z: The location where you want to move to
    /// </summary>
    public class TalkAndFollow : CustomForcedBehavior
    {
        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {
            {"NpcId",null},
            {"QuestId",null},
            {"X",null},
            {"Y",null},
            {"Z",null},
        };
        bool success = true;
        public TalkAndFollow(Dictionary<string, string> args)
            : base(args)
        {
            // tba. dictionary format is not documented.
            CheckForUnrecognizedAttributes(recognizedAttributes);
            int npcId = 0;
            int questId = 0;
            WoWPoint point = WoWPoint.Empty;

            success = success && GetAttributeAsInteger("NpcId", true, "0", 0, int.MaxValue, out npcId);
            success = success && GetAttributeAsInteger("QuestId", true, "0", 0, int.MaxValue, out questId);
            success = success && GetXYZAttributeAsWoWPoint("X", "Y", "Z", true, WoWPoint.Empty, out point);
            if (!success)
                Err("An error occurred while loading profile\nStopping HB");
            NpcId = npcId;
            QuestId = questId;
            Location = point;
        }

        public int NpcId { get; private set; }
        public int QuestId { get; private set; }
        public WoWPoint Location { get; private set; }
        int followDist = 10;
        bool talked = false;
         #region Overrides of CustomForcedBehavior
        private Composite root;

        protected override Composite CreateBehavior()
        {
            return root ??
                (root = new PrioritySelector(
                    new Decorator(c => Npc != null && !talked,
                        new PrioritySelector(
                            new Decorator( c=> !Npc.WithinInteractRange,
                                new Action(c => {Navigator.MoveTo(Npc.Location);})),
                            new Decorator(c=> GossipFrame.Instance == null || !GossipFrame.Instance.IsVisible,
                                new Action(c=>{Npc.Interact();})),
                            new Decorator(c=> GossipFrame.Instance != null && GossipFrame.Instance.IsVisible,
                                new Action(c=>{
                                    foreach (var entry in GossipFrame.Instance.GossipOptionEntries)
                                    {
                                        GossipFrame.Instance.SelectGossipOption(entry.Index);
                                    }
                                    talked = true;
                                })))
                    ),
                    new Decorator( c=> Npc !=null && Npc.Distance > followDist,
                        new Action(c=>{Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(ObjectManager.Me.Location,Npc.Location,followDist));} )),
                    new Decorator( c=> ObjectManager.Me.Location.Distance(Location) > 5,
                        new Action(c=>{
                            if (!ObjectManager.Me.Mounted && Mount.CanMount() && Styx.Helpers.LevelbotSettings.Instance.UseMount &&
                            ObjectManager.Me.Location.Distance(Location) > 35)
                            {
                                if (ObjectManager.Me.IsMoving)
                                {
                                    WoWMovement.MoveStop();
                                }
                                Mount.MountUp();
                            }
                            else
                            {
                                Navigator.MoveTo(Location);
                            }
                        }))
                ));
        }

        WoWUnit Npc 
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().OrderBy(u => u.Distance).FirstOrDefault(u => u.Entry == NpcId);
            }
        }


        void Err(string format, params object[] args)
        {
            Logging.Write(System.Drawing.Color.Red, "TalkAndFollow: " + format, args);
            TreeRoot.Stop();
        }

        void Log(string format, params object[] args)
        {
            Logging.Write("TalkAndFollow: " + format, args);
        }

        private bool isDone = false;
        public override bool IsDone
        {
            get
            {
                var quest = ObjectManager.Me.QuestLog.GetQuestById((uint)QuestId);
                return isDone || quest == null;
            }
        }
        public override void OnStart()
        {
            TreeRoot.GoalText = string.Format("Talking to NPC:{0} and following around until quest:{1} completes",NpcId,QuestId);
        }

        #endregion
    }
}
