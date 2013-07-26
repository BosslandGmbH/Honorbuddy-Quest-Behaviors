using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CommonBehaviors.Actions;
using Styx;
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


namespace QuestBehaviors.SpecificQuests.Kalimdor.Teldrassil
{
    [CustomBehaviorFileName(@"SpecificQuests\Kalimdor\Teldrassil\14005-TheVengeanceOfElune")]
    public class TheVengeanceOfElune : CustomForcedBehavior
    {
        public TheVengeanceOfElune(Dictionary<string, string> args)
            : base(args)
        {

            try
            {
                QuestId = GetAttributeAsNullable("QuestId", false, ConstrainAs.QuestId(this), null) ?? 14005;
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
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }

        // Private variables for internal state
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private Composite _root;

        // Private properties
        private LocalPlayer Me { get { return (StyxWoW.Me); } }

        public static int MobId = 34521;

        public static WoWPoint MobLocation = new WoWPoint(9111.133f, 1795.15f, 1323.46f);

        public WoWUnit BoughOfCorruption
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => MobId == u.Entry && !u.IsDead)
                                     .OrderBy(u => u.Distance).FirstOrDefault());
            }
        }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id: 14005-TheVengeanceOfElune.cs 664 2013-07-23 12:44:32Z Dogan $"); } }
        public override string SubversionRevision { get { return ("$Revision: 664 $"); } }


        ~TheVengeanceOfElune()
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
                                 ret => BoughOfCorruption == null,
                                         new Sequence(
                                             new Action(ret => TreeRoot.StatusText = "Moving to Bough of Corruption"),
                                             new Action(ret => Navigator.MoveTo(MobLocation))
                                          )),

                            new Decorator(
                                   ret => StyxWoW.Me.HealthPercent < 30 || StyxWoW.Me.HasAura(65606),
                                   new PrioritySelector(
                                       new Decorator(ret => Me.IsMoving,
                                           new Action(ret => WoWMovement.MoveStop())),
                                       new Decorator(ret => StyxWoW.Me.HealthPercent < 30,
                                           new Sequence(
                                                new Action(ret => Lua.DoString("RunMacroText('/click OverrideActionBarButton1')")),
                                                new Action(ret => SpellManager.ClickRemoteLocation(StyxWoW.Me.Location)))))),

  
                            new Decorator(
                                   ret => BoughOfCorruption != null && BoughOfCorruption.Distance >= 30,
                                       new Action(ret => Navigator.MoveTo(BoughOfCorruption.Location))),

                            new Decorator(
                                   ret => BoughOfCorruption != null && BoughOfCorruption.Distance < 30 && !StyxWoW.Me.IsCasting,
                                   new PrioritySelector(
                                       new Decorator(ret => Me.IsMoving,
                                           new Action(ret => WoWMovement.MoveStop())),
                                       new Sequence(
                                            new Action(ret => Lua.DoString("RunMacroText('/click OverrideActionBarButton2')")),
                                            new Action(ret => Thread.Sleep(2000)),
                                            new Action(ret => Lua.DoString("RunMacroText('/click OverrideActionBarButton3')"))))),

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