using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Styx.Helpers;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    public class BasicInteractWith : CustomForcedBehavior
    {
        #region Overrides of CustomForcedBehavior.

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"NpcID",null},
            {"MoveTo",null},
            {"Faction",null},
            {"QuestId",null},

        };

        bool success = true;

        public BasicInteractWith(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);

            int npcID = 0;
            int moveto = 0;
            int usefaction = 0;
            int questId = 0;
            
            success = success && GetAttributeAsInteger("NpcID", true, "0", 0, int.MaxValue, out npcID);
            success = success && GetAttributeAsInteger("MoveTo", false, "0", 0, int.MaxValue, out moveto);
            success = success && GetAttributeAsInteger("Faction", false, "0", 0, int.MaxValue, out usefaction);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);

            NPCID = npcID;
            FactionID = usefaction;
            QuestId = (uint)questId;
            MoveToNPC = moveto;

            Counter = 0;


        }

        public WoWPoint MovePoint { get; private set; }
        public int Counter { get; set; }
        public int NPCID { get; set; }
        public int FactionID { get; set; }
        public int MoveToNPC { get; set; }
        public uint QuestId { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        public List<WoWUnit> npcList;

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                new Decorator(ret => (QuestId != 0 && me.QuestLog.GetQuestById(QuestId) != null &&
                         me.QuestLog.GetQuestById(QuestId).IsCompleted),
                        new Action(ret => _isDone = true)),

                    new Decorator(ret => Counter >= 1,
                        new Action(ret => _isDone = true)),

                        new PrioritySelector(

                            new Decorator(ret => Counter == 0,
                                new Action(delegate
                                {

                                    if (FactionID > 1)
                                    {
                                        npcList = ObjectManager.GetObjectsOfType<WoWUnit>()
                                            .Where(u => u.Entry == NPCID && !u.Dead && u.FactionId == FactionID)
                                            .OrderBy(u => u.Distance).ToList();
                                    }
                                    else
                                    {

                                        npcList = ObjectManager.GetObjectsOfType<WoWUnit>()
                                            .Where(u => u.Entry == NPCID && !u.Dead)
                                            .OrderBy(u => u.Distance).ToList();
                                    }
                                    if (npcList.Count >= 1)
                                    {
                                        if (MoveToNPC == 1)
                                        {
                                            if (npcList[0].Location.Distance(me.Location) < 3)
                                            {
                                                npcList[0].Interact();
                                                Thread.Sleep(300);
                                                Counter++;
                                                return RunStatus.Success;
                                            }
                                            else
                                            {
                                                Navigator.MoveTo(npcList[0].Location);
                                                Thread.Sleep(300);
                                                return RunStatus.Running;
                                            }
                                        }
                                        else
                                        {
                                            npcList[0].Interact();
                                            Thread.Sleep(300);
                                            Counter++;
                                            return RunStatus.Success;
                                        }
                                    }
                                    return RunStatus.Running;
                                })
                                ),

                            new Action(ret => Logging.Write(""))
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

