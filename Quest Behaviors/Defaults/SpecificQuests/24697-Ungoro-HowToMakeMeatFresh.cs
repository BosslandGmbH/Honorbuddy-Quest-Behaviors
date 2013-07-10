﻿// Behavior originally contributed by mastahg.
//
// DOCUMENTATION:
//     
//				<CustomBehavior File="StandAndKill" QuestId="25553" MobId="40974" X="3772.889" Y="-3233.83" Z="975.3411" /> // originally made for hyjal behavior


using System;
using System.Collections.Generic;
using System.Linq;
using Bots.Grind;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.howtomakemeatfresh
{
    [CustomBehaviorFileName(@"SpecificQuests\24697-ungoro-howtomakemeatfresh")]
    public class howtomakemeatfresh : CustomForcedBehavior
    {
        ~howtomakemeatfresh()
        {
            Dispose(false);
        }

        public howtomakemeatfresh(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.

                QuestId = 24697;
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
                LogMessage("error",
                           "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message + "\nFROM HERE:\n" + except.StackTrace +
                           "\n");
                IsAttributeProblem = true;
            }
        }


        // Attributes provided by caller
        public int MobIds { get; private set; }
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }
        public WoWPoint Location { get; private set; }



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
                    // empty, for now
                }

                // Clean up unmanaged resources (if any) here...
                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }
            Logging.Write("Disposing");

            LevelBot.BehaviorFlags |= BehaviorFlags.Combat;

            _isDisposed = true;
        }



        #region Overrides of CustomForcedBehavior




        public Composite DoneYet
        {
            get
            {
                return
                    new Decorator(ret => IsQuestComplete(), new Action(delegate
                    {

                        TreeRoot.StatusText = "Finished!";
                        _isBehaviorDone = true;
                        return RunStatus.Success;
                    }));

            }
        }

        public bool IsQuestComplete()
        {
            if (QuestId == 0)
                return false;

            var quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
            return quest == null || quest.IsCompleted;
        }


        public Composite DoDps
        {
            get
            {
                return
                    new PrioritySelector(RoutineManager.Current.HealBehavior, RoutineManager.Current.CombatBuffBehavior, RoutineManager.Current.CombatBehavior);
            }
        }



        //<Vendor Name="Young Diemetradon" Entry="9162" Type="Repair" X="-7518.554" Y="-1351.945" Z="-270.8233" />
        public WoWUnit Dino
        {
            get { return ObjectManager.GetObjectsOfType<WoWUnit>().Where(r => (r.Entry == 9162 || r.Entry == 9163) && r.IsAlive).OrderBy(r=>r.Distance).FirstOrDefault(); }
        }



        //<Vendor Name="dasd" Entry="0" Type="Repair" X="-7531.042" Y="-1459.93" Z="-279.5515" />
        public WoWPoint Gooby = new WoWPoint(-7531.042,-1459.93,-279.5515);
        //<Vendor Name="start" Entry="0" Type="Repair" X="-7537.472" Y="-1421.281" Z="-272.6569" />
        public WoWPoint start = new WoWPoint(-7537.472,-1421.281,-272.6569);


        public WoWItem Meat
        {
            get { return Me.BagItems.FirstOrDefault(r => r.Entry == 50430); }
        }

        public bool gotthere = false;
        public Composite GetToSpot
        {
            get
            {
                return new Decorator(r => !gotthere,new PrioritySelector(
                    new Decorator(r=> start.Distance(Me.Location) > 10, new Action(r=>Navigator.MoveTo(start))),
                    new Decorator(r => start.Distance(Me.Location) < 10, new Action(r => gotthere = true))
                    
                    ));
            }
        }

        public Composite GoobyPls
        {
            
            get
            {
                return new PrioritySelector(
                    new Decorator(r=>Me.Combat,new Action(r=>Navigator.MoveTo(Gooby))),
                    new Decorator(r=>!Me.Combat && Dino != null && Dino.Distance < 40,new Action(r =>
                        {
                            Navigator.PlayerMover.MoveStop();
                            Dino.Target();
                            Meat.Use();

                        }
                                                                                          )),
                     new Decorator(r=>!Me.Combat && Dino != null && Dino.Distance > 40,new Action(r=>Navigator.MoveTo(Dino.Location))),
                     new Decorator(r=>!Me.Combat && Dino == null, new Action(r=>Navigator.MoveTo(start)))
                     
                        
                    
                    
                    );
            }
        }


        protected override Composite CreateBehavior()
        {

            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, GetToSpot,GoobyPls, new ActionAlwaysSucceed())));
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

                LevelBot.BehaviorFlags &= ~BehaviorFlags.Combat;


                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = this.GetType().Name + ": " +
                                    ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
            }
        }

        #endregion
    }
}