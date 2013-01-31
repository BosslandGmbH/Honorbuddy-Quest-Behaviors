// Behavior originally contributed by mastahg.
//
// DOCUMENTATION:
//     
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CommonBehaviors.Actions;
using Styx;

using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;


namespace HealingofShenZinSu
{
    public class HealingofShenZinSu : CustomForcedBehavior
    {
        ~HealingofShenZinSu()
        {
            Dispose(false);
        }

        public HealingofShenZinSu(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                //Location = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ??WoWPoint.Empty;
                QuestId = 29799; //GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
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
                    new Decorator(ret => IsQuestComplete(), new Action(delegate
                    {
                        TreeRoot.StatusText = "Finished!";
                        //CharacterSettings.Instance.UseMount = true;
                        _isBehaviorDone = true;
                        return RunStatus.Success;
                    }));

            }
        }



        public void CastSpell(string action)
        {

            var spell = StyxWoW.Me.PetSpells.FirstOrDefault(p => p.ToString() == action);
            if (spell == null)
                return;

            Logging.Write("[Pet] Casting {0}", action);
            Lua.DoString("CastPetAction({0})", spell.ActionBarIndex + 1);

        }

        bool IsObjectiveComplete(int objectiveId, uint questId)
        {
            if (this.Me.QuestLog.GetQuestById(questId) == null)
            {
                return false;
            }
            int returnVal = Lua.GetReturnVal<int>("return GetQuestLogIndexByID(" + questId + ")", 0);
            return Lua.GetReturnVal<bool>(string.Concat(new object[] { "return GetQuestLogLeaderBoard(", objectiveId, ",", returnVal, ")" }), 2);
        }





        //<Vendor Name="Alliance Priest" Entry="60877" Type="Repair" X="208.6163" Y="3913.861" Z="61.57914" />
        //<Vendor Name="Horde Druid" Entry="60770" Type="Repair" X="318.3125" Y="3896.318" Z="78.3259" />
        //16777216

        //209691 - sniper rifle
        public WoWUnit PriestsClickable
        {
            get
            {//559104
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>().Where(r => (r.Entry == 60877 || r.Entry == 60770) && r.Flags == 32768).OrderBy(r => r.Distance).FirstOrDefault();
            }

        }

        public WoWUnit Wreckage
        {
            get
            {//559104
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>().Where(r => r.Entry == 60848).OrderBy(r => r.Distance).FirstOrDefault();
            }

        }


        
        public WoWUnit InCombat
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>().Where(r => (r.Entry == 60877 || r.Entry == 60770) && r.Flags == 559104).OrderBy(r=>r.Distance).FirstOrDefault();
            }

        }


       


        

        private bool spoke = false;



    
        private Composite GetonPole
        {
            get
            {
                return new PrioritySelector(
                    new Decorator(r => Me.Combat, RoutineManager.Current.CombatBehavior),

                    //Sometimes wreckage would popup and make us stray, if theres a nearby healer click that first.
                    new Decorator(r => PriestsClickable != null && PriestsClickable.Distance < 5, new Action(r=>PriestsClickable.Interact(true))),
                    new Decorator(r => Wreckage != null, new Action(r =>
                                                                    {Navigator.MoveTo(Wreckage.Location);
                                                                        Wreckage.Interact();
                                                                    } )),
                    new Decorator(r => PriestsClickable != null, new Action(r =>
                                                                                {

                                                                                    Navigator.MoveTo(PriestsClickable.Location);

                                                                                    if (PriestsClickable.WithinInteractRange)
                                                                                    {
                                                                                        PriestsClickable.Interact(true);
                                                                                    }
                                                                                    
                                                                                })),
                    new Decorator(r => Me.CurrentTarget == null && InCombat != null && InCombat.GotTarget, new Action(r =>
                                                                                                                          {
                                                                                                                              Logging.Write("Attacking nearby enemy");
                                                                                                                              InCombat
                                                                                                                                  .
                                                                                                                                  CurrentTarget
                                                                                                                                  .
                                                                                                                                  Target
                                                                                                                                  ();

                                                                                                                              InCombat.CurrentTarget.Interact();
                                                                                                                              Navigator
                                                                                                                                  .
                                                                                                                                  MoveTo
                                                                                                                                  (InCombat
                                                                                                                                       .
                                                                                                                                       CurrentTarget
                                                                                                                                       .
                                                                                                                                       Location);
                                                                                                                          }
                        ))
                    
                    );
            }
        }





        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, GetonPole,new ActionAlwaysSucceed())));
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

                //CharacterSettings.Instance.UseMount = false;

                if (TreeRoot.Current != null && TreeRoot.Current.Root != null && TreeRoot.Current.Root.LastStatus != RunStatus.Running)
                {
                    var currentRoot = TreeRoot.Current.Root;
                    if (currentRoot is GroupComposite)
                    {
                        var root = (GroupComposite)currentRoot;
                        root.InsertChild(0, CreateBehavior());
                    }
                }

                //TreeRoot.TicksPerSecond = 30;
                // Me.QuestLog.GetQuestById(27761).GetObjectives()[2].

                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = this.GetType().Name + ": " +
                                    ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
            }




        }







        #endregion
    }
}