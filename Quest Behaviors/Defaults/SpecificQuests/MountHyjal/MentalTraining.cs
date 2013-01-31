// Behavior originally contributed by Bobby53.
//
// DOCUMENTATION:
//     
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using CommonBehaviors.Actions;


namespace Styx.Bot.Quest_Behaviors.MountHyjal
{
    public class MentalTraining : CustomForcedBehavior
    {
        /// <summary>
        /// Completes the quest http://www.wowhead.com/quest=25299
        /// This behavior completes the quest by correctly responding to
        /// 10 yes/no questions by checking the toons question aura.
        /// 
        /// Requires you to already be in position at the quest give Instructor Mylva X="4524.021" Y="-4731.176" Z="887.9406"
        /// 
        /// ##Syntax##
        /// QuestId: Id of the quest (default is 0)
        /// [Optional] QuestName: optional quest name (documentation only)
        /// </summary>
        /// 
        public MentalTraining(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                QuestId = GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
                /* */
                GetAttributeAs<string>("QuestName", false, ConstrainAs.StringNonEmpty, null);            // (doc only - not used)
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
        private static int _lineCount;
        private Composite _root;

        // Private properties
        private LocalPlayer Me { get { return (StyxWoW.Me); } }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id: MentalTraining.cs 254 2012-12-04 19:17:46Z dogan $"); } }
        public override string SubversionRevision { get { return ("$Revision: 254 $"); } }


        ~MentalTraining()
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


        public bool DoWeHaveQuest()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
            return quest != null;
        }

        public bool IsQuestComplete()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
            return quest == null || quest.IsCompleted;
        }

        public bool HasAura(WoWUnit unit, int auraId)
        {
            WoWAura aura = (from a in unit.Auras
                            where a.Value.SpellId == auraId
                            select a.Value).FirstOrDefault();
            return aura != null;
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                    // check if we have finished 10 questions (marked complete)
                    new Decorator(ret => IsQuestComplete(),
                        new PrioritySelector(
                            new Decorator(ret => Me.HasAura("Mental Training"),
                                new Sequence(
                                    new Action( ret => Logging.Write(LogLevel.Normal,"Mental Training complete - exiting Orb")),
                                    new Action( ret => Lua.DoString("RunMacroText(\"/click OverrideActionBarButton4\")")),
                                    CreateWaitForLagDuration()
                                    )
                                ),
                            new Action(ret => _isBehaviorDone = true)
                        )
                    ),

                    // if we don't have vehicle buff, use Orb of Ascension
                    new Decorator(ret => !Me.HasAura("Mental Training"),
                        new Sequence( 
                            new Action( delegate {
                                Logging.Write("Using Orb of Ascension");
                                WoWItem orb = ObjectManager.GetObjectsOfType<WoWItem>().Where(u => u.Entry == 52828).FirstOrDefault();
                                if (orb == null)
                                { LogMessage("fatal", "Quest item \"Orb of Ascension\" not in inventory."); }

                                orb.Use(true);
                                return RunStatus.Success;
                                }),
                            new WaitContinue( 1, ret => Me.HasAura("Mental Training"), new ActionAlwaysSucceed())
                            )
                        ),

                    // if we have YES aura 74008, then click yes
                    new Decorator(ret => HasAura(Me, 74008),
                        new Sequence(
                            new Action(ret => Logging.Write("Answering YES")),
                            new WaitContinue( TimeSpan.FromMilliseconds(500), ret => false, new ActionAlwaysSucceed()),
                            new Action(ret => Lua.DoString("RunMacroText(\"/click OverrideActionBarButton1\")")),
                            new WaitContinue( 1, ret => !HasAura(Me, 74008), new ActionAlwaysSucceed())
                            )
                        ),

                    // if we have NO aura 74009, then click no
                    new Decorator(ret => HasAura(Me, 74009),
                        new Sequence(
                            new Action(ret => Logging.Write("Answering NO")),
                            new WaitContinue(TimeSpan.FromMilliseconds(500), ret => false, new ActionAlwaysSucceed()),
                            new Action(ret => Lua.DoString("RunMacroText(\"/click OverrideActionBarButton2\")")),
                            new WaitContinue(1, ret => !HasAura(Me, 74009), new ActionAlwaysSucceed())
                            )
                        ),

                    new Action(delegate
                    {
                        return RunStatus.Success;
                    })
                )
            );
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
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = this.GetType().Name + ": " + ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
            }
        }

        #endregion

        /// <summary>
        /// This is meant to replace the 'SleepForLagDuration()' method. Should only be used in a Sequence
        /// </summary>
        /// <returns></returns>
        public static Composite CreateWaitForLagDuration()
        {
            return new WaitContinue(TimeSpan.FromMilliseconds((StyxWoW.WoWClient.Latency * 2) + 150), ret => false, new ActionAlwaysSucceed());
        }

    }
}
