// Behavior originally contributed by Natfoth.
//
// WIKI DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_BasicInteractWith
//
// QUICK DOX:
//      Allows you to Interact with (e.g., 'right-click') mobs that are nearby.
//
//  Parameters (required, then optional--both listed alphabetically):
//      MobId: Id of the mob to interact with.
//
//      FactionId [Default:any faction]: The faction the mobs needs to be before interacting.
//      MoveTo [Default:false]: "true" causes the behavior to move to the mob's Location.
//      QuestId [Default:none]:
//      QuestCompleteRequirement [Default:NotComplete]:
//      QuestInLogRequirement [Default:InLog]:
//              A full discussion of how the Quest* attributes operate is described in
//              http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
//      UseLuaTarget [Default:false]: Should be used for those Mobs that are inside vehicles
//              or otherwise return a location near 0,0,0
//
using System;
using System.Collections.Generic;
using System.Linq;

using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.BasicInteractWith
{
    [CustomBehaviorFileName(@"Deprecated\BasicInteractWith")]
    [CustomBehaviorFileName(@"BasicInteractWith")]  // Old location--do not use
    public class BasicInteractWith : CustomForcedBehavior
    {
        public BasicInteractWith(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                FactionId = GetAttributeAsNullable<int>("FactionId", false, ConstrainAs.FactionId, new[] { "Faction" }) ?? 0;
                IsMoveToMob = GetAttributeAsNullable<bool>("MoveTo", false, null, new[] { "UseCTM" }) ?? false;
                MobId = GetAttributeAsNullable<int>("MobId", true, ConstrainAs.MobId, new[] { "NpcId", "NpcID" }) ?? 0;
                QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
                UseLuaTarget = GetAttributeAsNullable<bool>("UseLuaTarget", false, null, new[] { "LUATarget" }) ?? false;


                WoWUnit mob = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(unit => unit.Entry == MobId);

                MobName = ((mob != null) && !string.IsNullOrEmpty(mob.Name))
                                ? mob.Name
                                : ("Mob(" + MobId + ")");

                QuestBehaviorBase.DeprecationWarning_Behavior(this, "InteractWith", BuildReplacementArguments());
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

        private List<Tuple<string, string>> BuildReplacementArguments()
        {
            var replacementArgs = new List<Tuple<string, string>>();

            QuestBehaviorBase.BuildReplacementArgs_QuestSpec(replacementArgs, QuestId, QuestRequirementComplete, QuestRequirementInLog);
            QuestBehaviorBase.BuildReplacementArg(replacementArgs, MobId, "MobId", 0);
            QuestBehaviorBase.BuildReplacementArg(replacementArgs, FactionId, "FactionId", 0);

            var navMode =
                IsMoveToMob
                ? MovementByType.ClickToMoveOnly
                : MovementByType.FlightorPreferred;
            QuestBehaviorBase.BuildReplacementArg(replacementArgs, navMode, "MovementBy", MovementByType.FlightorPreferred);
   
            return replacementArgs;
        }


        // Attributes provided by caller
        public int FactionId { get; private set; }
        public bool IsMoveToMob { get; private set; }
        public int MobId { get; private set; }
        public string MobName { get; private set; }
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }
        public bool UseLuaTarget { get; private set; }

        // Private variables for internal state
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private Composite _root;

        // Private properties
        private int Counter { get; set; }
        private LocalPlayer Me { get { return (StyxWoW.Me); } }
        private List<WoWUnit> MobList
        {
            get
            {
                if (FactionId > 1)
                {
                    return ObjectManager.GetObjectsOfType<WoWUnit>()
                                    .Where(u => u.Entry == MobId && !u.IsDead && u.FactionId == FactionId)
                                    .OrderBy(u => u.Distance).ToList();
                }
                else
                {
                    return ObjectManager.GetObjectsOfType<WoWUnit>()
                                            .Where(u => u.Entry == MobId && !u.IsDead)
                                            .OrderBy(u => u.Distance).ToList();
                }
            }
        }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id: BasicInteractWith.cs 580 2013-06-30 06:53:32Z chinajade $"); } }
        public override string SubversionRevision { get { return ("$Revision: 580 $"); } }


        ~BasicInteractWith()
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

                    new Decorator(ret => Counter >= 1,
                        new Action(ret => _isBehaviorDone = true)),

                        new PrioritySelector(

                            new Decorator(ret => Counter > 0,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new Action(ret => _isBehaviorDone = true),
                                    new WaitContinue(1,
                                        new Action(delegate
                                        {
                                            _isBehaviorDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )
                                ),

                             new Decorator(ret => MobList.Count > 0 && !MobList[0].WithinInteractRange && IsMoveToMob,
                                new Sequence(
                                    new DecoratorContinue(ret => IsMoveToMob,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Moving To Mob MyCTM - " + MobList[0].Name + " X: " + MobList[0].X + " Y: " + MobList[0].Y + " Z: " + MobList[0].Z),
                                            new Action(ret => WoWMovement.ClickToMove(MobList[0].Location))
                                            )),

                                      new DecoratorContinue(ret => !IsMoveToMob,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Moving To Mob MyCTM - " + MobList[0].Name + " X: " + MobList[0].X + " Y: " + MobList[0].Y + " Z: " + MobList[0].Z),
                                            new Action(ret => Navigator.MoveTo(MobList[0].Location))
                                            ))


                                    )),

                            new Decorator(ret => MobList.Count > 0 && MobList[0].WithinInteractRange,
                                new Sequence(
                                    new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                        new Action(ret =>
                                        {
                                            WoWMovement.MoveStop();
                                            StyxWoW.SleepForLagDuration();
                                        })),
                                        new Action(ret => MobList[0].Interact()),
                                        new Action(ret => Counter++)
                                    )
                            ),

                            new Decorator(ret => (MobList.Count > 0) && !IsMoveToMob && !UseLuaTarget,
                                new Sequence(
                                    new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                        new Action(ret =>
                                        {
                                            WoWMovement.MoveStop();
                                            StyxWoW.SleepForLagDuration();
                                        })),
                                        new Action(ret => MobList[0].Interact()),
                                        new Action(ret => Counter++)
                                    )
                            ),

                            new Decorator(ret => MobList.Count > 0 && UseLuaTarget,
                                new Sequence(
                                    new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                        new Action(ret =>
                                        {
                                            WoWMovement.MoveStop();
                                            StyxWoW.SleepForLagDuration();
                                        })),
                                        new Action(ret => Lua.DoString("TargetNearest()")),
                                        new Action(ret => Me.CurrentTarget.Interact()),
                                        new Action(ret => Counter++)
                                    )
                            )
                    )));
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
                TreeRoot.GoalText = "Interacting with " + MobName;
            }
        }

        #endregion
    }
}

