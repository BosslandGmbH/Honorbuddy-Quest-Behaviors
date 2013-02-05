// Behavior originally contributed by Natfoth.
//
// WIKI DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_FireFromTheSky
//
// QUICK DOX:
//      Used for the Dwarf Quest SI7: Fire From The Sky
//
//  Notes:
//      * Make sure to Save Gizmo.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Timers;
using CommonBehaviors.Actions;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Timer = System.Timers.Timer;


namespace Styx.Bot.Quest_Behaviors
{
    public class FourWindsLessonInBravery : CustomForcedBehavior
    {
        public FourWindsLessonInBravery(Dictionary<string, string> args)
            : base(args)
        {

            try
            {
                QuestId = GetAttributeAsNullable("QuestId", false, ConstrainAs.QuestId(this), null) ?? 29918;
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

        private bool _onBird;

        public static WoWPoint RopeGameObjectLocation = new WoWPoint(238.4929, -393.3196, 247.5843);

        private WoWItem Item { get { return (StyxWoW.Me.CarriedItems.FirstOrDefault(i => i.Entry == 75208)); } }

        public WoWUnit Enemy
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => u.Entry == 56171 && !u.IsDead && u.Distance < 199)
                                     .OrderBy(u => u.Distance).FirstOrDefault());
            }
        }

        public WoWGameObject RopeGameObject
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWGameObject>()
                                     .Where(u => u.Entry == 215319)
                                     .OrderBy(u => u.Distance).FirstOrDefault());
            }
        }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id: CombatUseItemOn.cs 249 2012-09-19 01:31:37Z natfoth $"); } }
        public override string SubversionRevision { get { return ("$Revision: 249 $"); } }

        public static Common.Helpers.WaitTimer GetOnTimer = new Common.Helpers.WaitTimer(TimeSpan.FromSeconds(1));


        ~FourWindsLessonInBravery()
        {
            Dispose(false);
        }


        public void Dispose(bool isExplicitlyInitiatedDispose)
        {
            if (!_isDisposed)
            {

                ClawClick.Enabled = false;
                ClawClick = null;
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


        //<Vendor Name="Great White Plainshawk" Entry="56171" Type="Repair" X="279.5976" Y="-490.9407" Z="323.0689" />
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

        public WoWItem Rope
        {
            get { return Me.BagItems.FirstOrDefault(r => r.Entry == 75208); }
        }
        public WoWUnit GiantAssBird
        {
            
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>().Where(r => r.IsAlive && r.Entry == 56171).OrderBy(
                        r => r.Distance).FirstOrDefault();
            }
        }
        public Composite GetOnBird
        {
            get
            {
                return
                    new Decorator(ret => !Me.InVehicle, new PrioritySelector(
                        new Decorator(r => GiantAssBird != null, new Action(r => { 
                            
                            GiantAssBird.Target();
                                                                                     Navigator.MoveTo(
                                                                                         GiantAssBird.Location);
                                                                                     Rope.Use();



                        }))


                                                            ));

            }
        }
        public Composite KillBird
        {
            get
            {
                return
                    new Decorator(ret => Me.InVehicle, new PrioritySelector(

                        new Decorator(r=>!Me.GotTarget || Me.CurrentTarget != GiantAssBird,new Action(r=>GiantAssBird.Target())),

                        RoutineManager.Current.CombatBuffBehavior,
                RoutineManager.Current.CombatBehavior


                                                           ));

            }
        }

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, GetOnBird,KillBird, new ActionAlwaysSucceed())));
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

        private Timer ClawClick;
        void ClawClick_Elapsed(object sender, ElapsedEventArgs e)
        {
           if (Me.InVehicle)
           {
               Lua.DoString("RunMacroText('/click ExtraActionButton1')");
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

                ClawClick = new Timer();
                ClawClick.Elapsed += ClawClick_Elapsed;
                ClawClick.Interval = 5000;
                ClawClick.Enabled = true;


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
