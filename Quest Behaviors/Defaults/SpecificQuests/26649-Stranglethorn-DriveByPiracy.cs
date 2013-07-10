using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.DriveByPiracy
{
    [CustomBehaviorFileName(@"SpecificQuests\26649-Stranglethorn-DriveByPiracy")]
    public class q26649 : CustomForcedBehavior
    {
        public q26649(Dictionary<string, string> args)
            : base(args) { }


        public static LocalPlayer me = StyxWoW.Me;
        static public bool Obj1Done { get { return Lua.GetReturnVal<int>("a,b,c=GetQuestLogLeaderBoard(1,GetQuestLogIndexByID(26649));if c==1 then return 1 else return 0 end", 0) == 1; } }
        public double angle = 0;
        public double CurentAngle = 0;
        public WoWUnit gooby
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>()
                                    .Where(u => (u.Entry == 43596 && !u.IsDead))
                                    .OrderBy(u => u.Distance2D).FirstOrDefault();
            }
        }


        private uint QuestId = 26649;

        public bool IsQuestComplete()
        {
            var quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
            return quest == null || quest.IsCompleted;
        }
        private bool IsObjectiveComplete(int objectiveId, uint questId)
        {
            if (StyxWoW.Me.QuestLog.GetQuestById(questId) == null)
            {
                return false;
            }
            int returnVal = Lua.GetReturnVal<int>("return GetQuestLogIndexByID(" + questId + ")", 0);
            return
                Lua.GetReturnVal<bool>(
                    string.Concat(new object[] { "return GetQuestLogLeaderBoard(", objectiveId, ",", returnVal, ")" }), 2);
        }

        WoWPoint wp = new WoWPoint(-14878.15, 296.5315, 0.93627);
        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(


                    new Decorator(ret => IsQuestComplete(),
                        new Sequence(
                            new Action(ret => TreeRoot.StatusText = "Finished!"),
                            new Action(ret => Lua.DoString("CastPetAction({0})", 5)),
                            new WaitContinue(120,
                            new Action(delegate
                            {
                                _isDone = true;
                                return RunStatus.Success;
                            }))
                            )),

                    new Decorator(ret => gooby != null,
                    new Action(ret =>
                        {

                            var status = StyxWoW.Me.CurrentTarget;
                            if (status == null || status.Entry != 43596 || status.Distance2D > 100)
                                gooby.Target();

                            //WoWMovement.ClickToMove(gooby.Location);
                            WoWMovement.ConstantFace(me.CurrentTarget.Guid);
                            angle = -((me.Z - me.CurrentTarget.Z)/(me.CurrentTarget.Location.Distance(me.Location))) +
                                    ((me.CurrentTarget.Location.Distance2D(me.Location) - 20)/
                                     me.CurrentTarget.Location.Distance(me.Location)/10);
                            CurentAngle = Lua.GetReturnVal<double>("return VehicleAimGetAngle()", 0);
                            if (CurentAngle < angle)
                            {
                                Lua.DoString(string.Format("VehicleAimIncrement(\"{0}\")", (angle - CurentAngle)));
                            }
                            if (CurentAngle > angle)
                            {
                                Lua.DoString(string.Format("VehicleAimDecrement(\"{0}\")", (CurentAngle - angle)));
                            }
                            Lua.DoString("CastPetAction(1) CastPetAction(2) CastPetAction(3)");

                    }
                    ))
                )
            );
        }






        private bool _isDone;
        public override bool IsDone
        {
            get { return _isDone; }
        }

    }
}

