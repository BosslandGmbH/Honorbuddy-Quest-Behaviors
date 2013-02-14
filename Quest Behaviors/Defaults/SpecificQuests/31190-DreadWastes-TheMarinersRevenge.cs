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
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class squid : CustomForcedBehavior
    {
        ~squid()
        {
            Dispose(false);
        }

        public squid(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                //Location = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ??WoWPoint.Empty;
                QuestId = 31190; //GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
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
                        CharacterSettings.Instance.UseMount = true;
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


        WoWUnit enemy(int stage)
        {
            return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(r => r.Entry == parts[stage]); 
        }

        uint[] parts = new uint[] { 0, 64222, 64228, 64229, 64230 };

        //bow = front - 1 - 64222
        //port = right - 2 - 64228
        //Starboard = left -3 - 64229
        //stern = back - 4 - 64230
        public Composite Obj1
        {
            get
            {
                var x = 1;
                return new Decorator(r => enemy(x) != null && !IsObjectiveComplete(x, (uint)QuestId), new Action(

                   r =>
                   {
                       Logging.Write("bow");
                       WoWMovement.ClickToMove(enemy(x).Location);
                       CastSpell("Harpoon Cannon");
                   }

                ));
            }
        }


        private void adjustangle()
        {
            var angle = 0.22;
            var CurentAngle = Lua.GetReturnVal<double>("return VehicleAimGetAngle()", 0);
            if (CurentAngle < angle)
            {
                Lua.DoString(string.Format("VehicleAimIncrement(\"{0}\")", (angle - CurentAngle)));
            }
            if (CurentAngle > angle)
            {
                Lua.DoString(string.Format("VehicleAimDecrement(\"{0}\")", (CurentAngle - angle)));
            }
        }

        public Composite Obj2
        {
            get
            {
                var x = 2;
                return new Decorator(r => enemy(x) != null && !IsObjectiveComplete(x, (uint)QuestId), new Action(

                   r =>
                       {
                           adjustangle();
                       Logging.Write("port");
                       WoWMovement.ClickToMove(enemy(x).Location);
                       CastSpell("Harpoon Cannon");
                   }

                ));
            }
        }
        public Composite Obj3
        {
            get
            {
                var x = 3;
                return new Decorator(r => enemy(x) != null && !IsObjectiveComplete(x, (uint)QuestId), new Action(

                   r =>
                   {
                       adjustangle();
                       Logging.Write("Starboard");
                       WoWMovement.ClickToMove(enemy(x).Location);
                       CastSpell("Harpoon Cannon");
                   }

                ));
            }
        }
        public Composite Obj4
        {
            get
            {
                var x = 4;
                return new Decorator(r => enemy(x) != null && !IsObjectiveComplete(x, (uint)QuestId), new Action(

                   r =>
                   {

                       adjustangle();
                       Logging.Write("stern");
                       WoWMovement.ClickToMove(enemy(x).Location);
                       CastSpell("Harpoon Cannon");
                   }

                ));
            }
        }
        
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, Obj1, Obj2, Obj3, Obj4, new ActionAlwaysSucceed())));
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

                CharacterSettings.Instance.UseMount = false;
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