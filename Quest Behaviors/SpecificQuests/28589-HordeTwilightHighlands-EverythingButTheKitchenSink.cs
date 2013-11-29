// Behavior originally contributed by mastahg.
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

using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.EverythingButTheKitchenSink
{
    [CustomBehaviorFileName(@"SpecificQuests\28589-HordeTwilightHighlands-EverythingButTheKitchenSink")]
    public class KitchenSink : CustomForcedBehavior
    {
        ~KitchenSink()
        {
            Dispose(false);
        }

        public KitchenSink(Dictionary<string, string> args)
            : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                QuestId = 28589;
                QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = QuestInLogRequirement.InLog;
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


        // Attributes provided by caller
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }

        // Private variables for internal state
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private Composite _root;


        // Private properties
        private LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
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


        public Composite DoDps
        {
            get
            {
                return
                    new PrioritySelector(
                        new Decorator(ret => RoutineManager.Current.CombatBehavior != null,
                                      RoutineManager.Current.CombatBehavior),
                        new Action(c => RoutineManager.Current.Combat()));
            }
        }


        #region Overrides of CustomForcedBehavior

        public WoWUnit Watcher
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 49683 && u.IsAlive).OrderBy(
                        u => u.Distance).FirstOrDefault();
            }
        }

        public Composite TargetNew
        {
            get
            {
                return new Action(delegate
                {
                    if (Watcher != null)
                        Watcher.Target();
                });
            }
        }

        public Composite pewpew
        {
            get
            {
                return new Action(delegate
                {
                    //WoWMovement.ClickToMove(Me.CurrentTarget.Location);
                    WoWMovement.ClickToMove(Me.CurrentTarget.Location.RayCast(Me.CurrentTarget.Rotation,20));
var x= ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(z => z.CharmedByUnit == Me);

                    Tripper.Tools.Math.Vector3 v = Me.CurrentTarget.Location - Me.Location;
                    v.Normalize();
                    Lua.DoString(
                        string.Format(
                            "VehicleAimIncrement({0} - VehicleAimGetAngle()); CastPetAction({1});",
                            Math.Asin(v.Z).ToString(), 1));

                });
            }
        }





        public Composite LockOn
        {
            get { return new Decorator(r => Me.CurrentTarget == null || Me.CurrentTarget.Distance > 100, TargetNew); }
        }


        private WoWPoint endspot = new WoWPoint(1076.7, 455.7638, -44.20478);
        private WoWPoint spot = new WoWPoint(1109.848, 462.9017, -45.03053);




        protected Composite CreateBehavior_QuestbotMain()
        {
            //return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(ShootArrows,Lazor, BunchUp, new ActionAlwaysSucceed())));
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(new Action(ret => Loopstuff()))));
        }



        public void Loopstuff()
        {
            while (true)
            {
                ObjectManager.Update();
                if (Me.IsQuestComplete(QuestId))
                {
                    _isBehaviorDone = true;
                    break;
                }

                if (Me.CurrentTarget != null && Me.CurrentTarget.Distance < 100)
                {
                    //WoWMovement.ClickToMove(Me.CurrentTarget.Location);
                    WoWMovement.ClickToMove(Me.CurrentTarget.Location.RayCast(Me.CurrentTarget.Rotation, 20));
                    var x =
                        ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(z => z.CharmedByUnit == Me);

                    Tripper.Tools.Math.Vector3 v = Me.CurrentTarget.Location - x.Location;
                    v.Normalize();
                    Lua.DoString(
                        string.Format(
                            "VehicleAimIncrement(({0} - VehicleAimGetAngle())); CastPetAction({1});",
                            Math.Asin(v.Z).ToString(), 1));
                }
                else
                {
                    if (Watcher != null)
                        Watcher.Target();
                }


            }
        }


        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone // normal completion
                        || !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
            }
        }


        public override void OnStart()
        {
            // This reports problems, and stops BT processing if there was a problem with attributes...
            // We had to defer this action, as the 'profile line number' is not available during the element's
            // constructor call.
            OnStart_HandleAttributeProblem();

            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (!IsDone)
            {
                TreeHooks.Instance.InsertHook("Questbot_Main", 0, CreateBehavior_QuestbotMain());

                this.UpdateGoalText(QuestId);
            }
        }

        #endregion
    }
}