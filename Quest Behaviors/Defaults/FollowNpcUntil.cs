using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Styx.Database;
using Styx.Helpers;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    public class FollowNpcUntil : CustomForcedBehavior
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


        public FollowNpcUntil(Dictionary<string, string> args)
            : base(args)
        {

            CheckForUnrecognizedAttributes(recognizedAttributes);

            int npcID = 0;
            int questId = 0;
            WoWPoint location = new WoWPoint(0, 0, 0);

            success = success && GetAttributeAsInteger("NpcId", true, "1", 0, int.MaxValue, out npcID);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
            success = success && GetXYZAttributeAsWoWPoint("X", "Y", "Z", true, new WoWPoint(0, 0, 0), out location);

            QuestId = (uint)questId;
            NPCID = npcID;
            Counter = 1;
            MovedToTarget = false;
            Location = location;
        }

        public WoWPoint Location { get; private set; }
        public int Counter { get; set; }
        public int NPCID { get; set; }
        public bool MovedToTarget;
        public uint QuestId { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        public List<WoWUnit> npcList;
        public List<WoWUnit> endList;

        /// <summary>
        /// A Queue for npc's we need to talk to
        /// </summary>
        //private WoWUnit CurrentUnit { get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(unit => unit.Distance < 100 && unit.Entry == MobId); } }

        #region Overrides of CustomForcedBehavior

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(
				
				new Decorator(ret => (QuestId != 0 && me.QuestLog.GetQuestById(QuestId) != null &&
                         me.QuestLog.GetQuestById(QuestId).IsCompleted),
                        new Action(ret => _isDone = true)),

                    new Decorator(ret => Counter > 1,
                        new Action(ret => _isDone = true)),

                        new PrioritySelector(

                           new Decorator(ret => !MovedToTarget,
                                new Action(delegate
                                {
                                    ObjectManager.Update();

                                    npcList = ObjectManager.GetObjectsOfType<WoWUnit>()
                                        .Where(u => u.Entry == NPCID)
                                        .OrderBy(u => u.Distance).ToList();

                                    PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
                                    if (quest.IsCompleted)
                                    {
                                        Counter++;
                                        return RunStatus.Success;
                                    }
                                    else if (npcList.Count >= 1)
                                    {
                                        Navigator.MoveTo(npcList[0].Location);

                                    }
                                    else
                                    {
                                        Navigator.MoveTo(Location);
                                    }

                                    return RunStatus.Running;

                                })
                                ),

                            new Action(ret => Navigator.MoveTo(Location))
                        )
                    ));
        }

        private bool _isDone;
        public override bool IsDone
        {
            get { return _isDone; }
        }

        #endregion
    }
}
