using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.RampageAgainstTheMachine
{
    [CustomBehaviorFileName(@"SpecificQuests\31808-DreadWastes-RampageAgainstTheMachine")]
    public class RampageAgainstTheMachine : CustomForcedBehavior
    {
        public RampageAgainstTheMachine(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                QuestId = 31808;//GetAttributeAsQuestId("QuestId", true, null) ?? 0;
            }
            catch
            {
                Logging.Write("Problem parsing a QuestId in behavior: Rampage Against The Machine");
            }
        }
        public int QuestId { get; set; }
        private bool _isBehaviorDone;
        public int MobIdMantid1 = 67035;
        public int MobIdMantid2 = 67034;
        public int MobIdKunchong = 63625;
        public int Xaril = 63765;
        private Composite _root;
        public QuestCompleteRequirement questCompleteRequirement = QuestCompleteRequirement.NotComplete;
        public QuestInLogRequirement questInLogRequirement = QuestInLogRequirement.InLog;
        static public bool InVehicle { get { return Lua.GetReturnVal<int>("if IsPossessBarVisible() or UnitInVehicle('player') or not(GetBonusBarOffset()==0) then return 1 else return 0 end", 0) == 1; } }
        public override bool IsDone
        {
            get
            {
                return _isBehaviorDone;
            }
        }
        private LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
        }

        public override void OnStart()
        {
            OnStart_HandleAttributeProblem();
            if (!IsDone)
            {
                TreeHooks.Instance.InsertHook("Combat", 0, CreateBehavior_CombatMain());

                PlayerQuest Quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
                TreeRoot.GoalText = ((Quest != null) ? ("\"" + Quest.Name + "\"") : "In Progress");
            }
        }

        public List<WoWUnit> Mantid
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => (u.Entry == MobIdMantid1 || u.Entry == MobIdMantid2) && !u.IsDead && u.Distance < 350).OrderBy(u => u.Distance).ToList();
            }
        }
        public List<WoWUnit> Kunchong
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == MobIdKunchong && !u.IsDead && u.Distance < 500).OrderBy(u => u.Distance).ToList();
            }
        }

        public WoWUnit Kovok
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>(true)
                                    .Where(o => o.Entry == Xaril)
                                    .OrderBy(o => o.Distance)
                                    .FirstOrDefault();
            }
        }

        public bool IsQuestComplete()
        {
            var quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
            return quest == null || quest.IsCompleted;
        }
        private bool IsObjectiveComplete(int objectiveId, uint questId)
        {
            if (Me.QuestLog.GetQuestById(questId) == null)
            {
                return false;
            }
            int returnVal = Lua.GetReturnVal<int>("return GetQuestLogIndexByID(" + questId + ")", 0);
            return
                Lua.GetReturnVal<bool>(
                    string.Concat(new object[] { "return GetQuestLogLeaderBoard(", objectiveId, ",", returnVal, ")" }), 2);
        }

        public Composite DoneYet
        {
            get
            {
                return
                    new Decorator(ret => IsObjectiveComplete(2, (uint)QuestId), new Action(delegate
                    {
                        TreeRoot.StatusText = "Finished!";
                        _isBehaviorDone = true;
                        return RunStatus.Success;
                    }));

            }
        }


        public Composite MantidKill
        {
            get
            {
                return
                    new Decorator(ret => !IsObjectiveComplete(2, (uint)QuestId), new Action(c =>
                    {
                        TreeRoot.StatusText = "Moving to Attack";
                        //<Vendor Name="Klaxxi Kunchong Destroyer" Entry="64834" Type="Repair" X="-58.25938" Y="3466.082" Z="113.1098" />
                        var hostile =
                            ObjectManager.GetObjectsOfType<WoWUnit>().Where(r => r.Entry != 64834 && r.GotTarget && r.CurrentTarget == Me.CharmedUnit).OrderBy(r=>r.Distance).FirstOrDefault();


                        //<Vendor Name="Dread Behemoth" Entry="67039" Type="Repair" X="-153.9916" Y="3373.808" Z="103.9902" />
                        //<Vendor Name="Ik'thik Kunchong" Entry="67036" Type="Repair" X="-142.2761" Y="3578.237" Z="118.6222" />
                        WoWUnit tar;

                        if (hostile != null)
                        {
                            tar = hostile;
                        }
                        else if (Mantid.Count > 0)
                        {
                            tar = Mantid.FirstOrDefault();
                        }
                        else
                        {
                            var xtra =
                                ObjectManager.GetObjectsOfType<WoWUnit>().Where(
                                    r => (r.Entry == 67039 || r.Entry == 67036) && r.IsAlive).OrderBy(
                                        r => r.Distance).FirstOrDefault();

                            if (xtra != null)
                            {
                                tar = xtra;
                            }
                            else
                            {
                                Logging.Write("No viable targets, waiting.");
                                return RunStatus.Failure;
                            }
                        }

                        if (tar.Location.Distance(Me.CharmedUnit.Location) > 15)
                        {
                            WoWMovement.ClickToMove(tar.Location);
                            tar.Target();
                            tar.Face();
                        }
                        else
                        {
                            //WoWMovement.ClickToMove(tar.Location);
                            tar.Target();
                            //WoWMovement.MoveStop();
                            //StyxWoW.Sleep(400);
                            tar.Face();
                            Lua.DoString("CastPetAction(1)");
                            Lua.DoString("CastPetAction(3)");
                            //WoWMovement.MoveStop();
                            if (Me.IsMoving || Me.CharmedUnit.IsMoving)
                                WoWMovement.ClickToMove(Me.CharmedUnit.Location);
                        }






                        return RunStatus.Failure;
                    }));

            }
        }








        protected Composite CreateBehavior_CombatMain()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, MantidKill, new ActionAlwaysSucceed())));
        }

         #region Cleanup

        ~RampageAgainstTheMachine()
        {
            Dispose(false);
        }

        private bool _isDisposed;

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
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
                    TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_CombatMain());
                }

                // Clean up unmanaged resources (if any) here...
                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }

        #endregion
    }
}



