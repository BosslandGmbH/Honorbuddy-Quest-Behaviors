using System.Collections.Generic;
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
    public class NoCombatMoveTo : CustomForcedBehavior
    {
        #region Overrides of CustomForcedBehavior

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"X",null},
            {"Y",null},
            {"Z",null},
            {"QuestId",null},

        };

        bool success = true;

        public NoCombatMoveTo(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);

            WoWPoint location = new WoWPoint(0, 0, 0);
            int questId = 0;

            success = success && GetXYZAttributeAsWoWPoint("X", "Y", "Z", true, new WoWPoint(0, 0, 0), out location);
            success = success && GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);

            QuestId = (uint)questId;
            MovePoint = location;

            Counter = 0;
        }

        public WoWPoint MovePoint { get; private set; }
        public int Counter { get; set; }
        public uint QuestId { get; set; }

        public static LocalPlayer me = ObjectManager.Me;

        public List<WoWUnit> npcList;

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                    new Decorator(ret => Counter >= 1,
                        new Action(ret => _isDone = true)),

                        new PrioritySelector(

                            new Decorator(ret => Counter == 0,
                                new Action(delegate
                                {

                                    WoWPoint destination1 = new WoWPoint(MovePoint.X, MovePoint.Y, MovePoint.Z);
                                    WoWPoint[] pathtoDest1 = Styx.Logic.Pathing.Navigator.GeneratePath(me.Location, destination1);

                                    foreach (WoWPoint p in pathtoDest1)
                                    {
                                        while (!me.Dead && p.Distance(me.Location) > 3)
                                        {
                                            if (me.Combat)
                                            {
                                                break;
                                            }
                                            Thread.Sleep(100);
                                            WoWMovement.ClickToMove(p);
                                        }

                                        if (me.Combat)
                                        {
                                            break;
                                        }
                                    }

                                    if (me.Combat)
                                    {

                                        return RunStatus.Success;
                                    }
                                    else if (!me.Combat)
                                    {
                                        Counter++;
                                        return RunStatus.Success;
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

