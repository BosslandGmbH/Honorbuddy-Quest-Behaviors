using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

/* This behavior is for killing Thane noobface in Grizzly Hills (Horde 12259 and Alliance 12255) 
		This behavior was developed by Kickazz006
		Code was taken from Shak
		How I used it in this behavior was chop each in half and take the bits that I needed
		Feel free to re-use the code to your liking (anyone else)
	*/


namespace QuestBehaviors.SpecificQuests.Kalimdor.Darkshore
{
    [CustomBehaviorFileName(@"SpecificQuests\Kalimdor\Darkshore\13512-StrategicStrikes")]
    public class StrategicStrikes : CustomForcedBehavior
    {
        public StrategicStrikes(Dictionary<string, string> args)
            : base(args)
        {

            try
            {
                KillSheya = GetAttributeAsNullable<bool>("KillSheya", false, null, null) ?? true;
                KillLorenth = GetAttributeAsNullable<bool>("KillLorenth", false, null, null) ?? false;
                QuestId = GetAttributeAsNullable("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
                                    + "\nFROM HERE:\n"
                                    + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }


        // Attributes provided by caller
        public bool KillLorenth { get; private set; }
        public bool KillSheya { get; private set; }
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }

        // Private variables for internal state
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private Composite _root;

        // Private properties
        private int Counter { get; set; }
        private LocalPlayer Me { get { return (StyxWoW.Me); } }

        static readonly WaitTimer MoveTimer = WaitTimer.OneSecond;
        private static readonly WaitTimer Timer = new WaitTimer(TimeSpan.FromSeconds(3));

        public WoWPoint KillLocation
        {
            get
            {
                if (KillSheya)
                {
                    return new WoWPoint(7345.96f, -942.14f, 16.92f);
                }

                return new WoWPoint(7102.91f, -648.68f, 61.13f);
            }
        }

        public WoWUnit KillUnit
        {
            get
            {
                if (KillSheya)
                {
                    return (ObjectManager.GetObjectsOfType<WoWUnit>()
                        .Where(u => 32869 == u.Entry && !u.IsDead)
                        .OrderBy(u => u.Distance).FirstOrDefault());
                }

                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => 32868 == u.Entry && !u.IsDead)
                                     .OrderBy(u => u.Distance).FirstOrDefault());
            }
        }

        public WoWItem Item { get { return Me.CarriedItems.FirstOrDefault(i => i.Entry == 44995 && i.Cooldown == 0); } }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id: 13512-StrategicStrikes.cs 501 2013-05-10 16:29:10Z chinajade $"); } }
        public override string SubversionRevision { get { return ("$Revision: 501 $"); } }


        ~StrategicStrikes()
        {
            Dispose(false);
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
                    // empty, for now
                }

                // Clean up unmanaged resources (if any) here...
                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }




        #region Overrides of CustomForcedBehavior



        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(
                    new Decorator(
                        ret => !_isBehaviorDone,
                        new PrioritySelector(
                            new Decorator(ret => Me.QuestLog.GetQuestById((uint)QuestId) != null && Me.QuestLog.GetQuestById((uint)QuestId).IsCompleted,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new WaitContinue(120,
                                        new Action(delegate
                                        {
                                            _isBehaviorDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )),

                             new Decorator(
                                 ret => KillUnit == null,
                                             new Action(ret => TreeRoot.StatusText = String.Format("Waiting for {0} to Spawn", KillSheya ? "Sheya" : "Lorenth"))
                                          ),

                            new Decorator(
                                   ret => StyxWoW.Me.HasAura("Unstable Lightning Blast") && Timer.IsFinished,
                                   new Sequence(
                                       new Action(ret => Timer.Reset()),
                                       new Action(ret => MoveTimer.Reset()),
                                       new Action(ret => WoWMovement.Move(WoWMovement.MovementDirection.StrafeLeft)))),

                            new Decorator(
                                   ret => (StyxWoW.Me.IsMoving && KillSheya) || (StyxWoW.Me.IsMoving && !KillSheya && MoveTimer.IsFinished),
                                   new Sequence(
                                       new Action(ret => WoWMovement.MoveStop()),
                                       new ActionAlwaysSucceed())),

  
                            new Decorator(
                                   ret => KillUnit != null && KillUnit.Location.Distance(KillLocation) > 5,
                                   new Sequence(
                                       new Action(ret => _isBehaviorDone = true),
                                       new ActionAlwaysSucceed())),

                            new Decorator(
                                   ret => KillUnit != null && KillUnit.Location.Distance(KillLocation) < 5 && Item != null,
                                   new Sequence(
                                       new Action(ret => KillUnit.Target()),
                                       new Action(ret => StyxWoW.SleepForLagDuration()),
                                       new Action(ret => Item.UseContainerItem()),
                                       new ActionAlwaysSucceed())),

                            new ActionAlwaysSucceed()


                    ))));
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
                return (_isBehaviorDone     // normal completion
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
                if (TreeRoot.Current != null && TreeRoot.Current.Root != null && TreeRoot.Current.Root.LastStatus != RunStatus.Running)
                {
                    var currentRoot = TreeRoot.Current.Root;
                    if (currentRoot is GroupComposite)
                    {
                        var root = (GroupComposite)currentRoot;
                        root.InsertChild(0, CreateBehavior());
                    }
                }


                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = GetType().Name + ": " + ((quest != null) ? quest.Name : "In Progress");
            }
        }

        #endregion
    }
}