// Behavior originally contributed by mastahg.
//
// DOCUMENTATION:
//     
//

using System;
using System.Collections.Generic;
using System.Linq;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class BomberMan : CustomForcedBehavior
    {
        ~BomberMan()
        {
            Dispose(false);
        }

        public BomberMan(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                //Location = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ??WoWPoint.Empty;
                QuestId = 27761; //GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
                //MobIds = GetAttributeAsNullable<int>("MobId", true, ConstrainAs.MobId, null) ?? 0;
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
                LogMessage("error",
                           "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message + "\nFROM HERE:\n" + except.StackTrace +
                           "\n");
                IsAttributeProblem = true;
            }
        }


        // Attributes provided by caller
        public uint[] MobIds { get; private set; }
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }
        public WoWPoint Location { get; private set; }

        // Private variables for internal state
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private Composite _root;

        private WoWPoint[] BombSpots = { new WoWPoint(-10702.17, -2455.62, 123.2423), new WoWPoint(-10678.92, -2452.069, 100.3572), new WoWPoint(-10588.04, -2439.058, 93.703) };


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

        public bool IsQuestComplete()
        {
            var quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
            return quest == null || quest.IsCompleted;
        }


        public Composite DoneYet
        {
            get
            {
                return
                    new Decorator(ret => IsQuestComplete() && Me.Mounted, new Action(delegate
                    {
                        TreeRoot.StatusText = "Finished!";
                        _isBehaviorDone = true;
                        return RunStatus.Success;
                    }));

            }
        }







        public List<WoWUnit> Enemies
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.FactionId == 2334 && u.IsAlive).OrderBy(u => u.Distance).ToList();
            }
        }


        public WoWUnit ClosestBomb()
        {
            return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 46888 && u.IsAlive && u.Location.Distance(BadBomb) > 10).OrderBy(u => u.Distance).FirstOrDefault();
        }


        public int Hostiles
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>().Count(
                        u => u.IsHostile && u.Location.Distance(ClosestBomb().Location) <= 10);
            }

        }

        public Composite DeployHologram
        {
            get
            {
                return new Decorator(ret => Hostiles > 0,
                    new Action(r => Hologram().Use()));
            }
        }


        public Composite FindBomb
        {
            get
            {
                return new Decorator(ret => Me.Location.Distance(ClosestBomb().Location) > 12,
                    new Action(delegate
                    {
                        var x = ClosestBomb().Location;
                        x.Z += 10;
                        Flightor.MoveTo(x);
                    }));
            }
        }


        public Composite BreakCombat
        {
            get
            {
                return new Decorator(ret => Me.Combat,
                    new Action(r => Hologram().Use()));
            }
        }

        public Composite Mount
        {
            get
            {
                return new Decorator(ret => !Me.Mounted && ClosestBomb().Distance > 10,
                    new Action(r => Flightor.MountHelper.MountUp()));
            }
        }

        public Composite UseAndGo
        {
            get
            {
                return new Action(delegate { 
                    ClosestBomb().Interact();
                    Flightor.MountHelper.MountUp();
                });
            }
        }

        public WoWItem Hologram()
        {
            return Me.BagItems.FirstOrDefault(x => x.Entry == 62398);
        }

        protected override Composite CreateBehavior()
        {

            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(BreakCombat,Mount, DoneYet,FindBomb, DeployHologram, UseAndGo)));
        }

        WoWPoint BadBomb = new WoWPoint(-10561.68, -2429.371, 91.56037);

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

                // Me.QuestLog.GetQuestById(27761).GetObjectives()[2].

                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = this.GetType().Name + ": " +
                                    ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
            }




        }







        #endregion
    }
}