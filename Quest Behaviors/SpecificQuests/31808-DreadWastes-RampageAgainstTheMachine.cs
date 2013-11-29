//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

#region Summary and Documentation
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.RampageAgainstTheMachine
{
    [CustomBehaviorFileName(@"SpecificQuests\31808-DreadWastes-RampageAgainstTheMachine")]
    public class RampageAgainstTheMachine : CustomForcedBehavior
    {
        public RampageAgainstTheMachine(Dictionary<string, string> args)
            : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            try
            {
                QuestId = 31808;//GetAttributeAsQuestId("QuestId", true, null) ?? 0;
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                QBCLog.Error("[MAINTENANCE PROBLEM]: " + except.Message
                        + "\nFROM HERE:\n"
                        + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }


        public int QuestId { get; set; }
        private bool _isBehaviorDone;
        public int MobIdMantid1 = 67035;
        public int MobIdMantid2 = 67034;
        public int MobIdKunchong = 63625;
        public int Xaril = 63765;
        private Composite _root;
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
                TreeHooks.Instance.InsertHook("Questbot_Main", 0, CreateBehavior_QuestbotMain());

                this.UpdateGoalText(QuestId);
            }
        }

        public List<WoWUnit> Mantid
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => (u.Entry == MobIdMantid1 || u.Entry == MobIdMantid2) && !u.IsDead && u.Distance < 350).OrderBy(u => u.Distance).ToList();
            }
        }


        public Composite DoneYet
        {
            get
            {
                return
                    new Decorator(ret => Me.IsQuestObjectiveComplete(QuestId, 2),
                        new Action(delegate
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
                return new Decorator(ret => !Me.IsQuestObjectiveComplete(QuestId, 2),
                    new Action(c =>
                    {
                        TreeRoot.StatusText = "Moving to Attack";
                        //<Vendor Name="Klaxxi Kunchong Destroyer" Entry="64834" Type="Repair" X="-58.25938" Y="3466.082" Z="113.1098" />
                        var hostile =
                            ObjectManager.GetObjectsOfType<WoWUnit>()
                            .Where(r => r.Entry != 64834 && r.GotTarget && r.CurrentTarget == Me.CharmedUnit)
                            .OrderBy(r=>r.Distance)
                            .FirstOrDefault();

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
                                QBCLog.Info("No viable targets, waiting.");
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
                            tar.Target();
                            tar.Face();
                            Lua.DoString("CastPetAction(1)");
                            Lua.DoString("CastPetAction(3)");
                            if (Me.IsMoving || Me.CharmedUnit.IsMoving)
                                WoWMovement.ClickToMove(Me.CharmedUnit.Location);
                        }

                        return RunStatus.Failure;
                    }));

            }
        }


        protected Composite CreateBehavior_QuestbotMain()
        {
            return _root ?? (_root =
                new Decorator(ret => !_isBehaviorDone,
                    new PrioritySelector(DoneYet, MantidKill, new ActionAlwaysSucceed())));
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
                    TreeHooks.Instance.RemoveHook("Questbot_Main", CreateBehavior_QuestbotMain());
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



