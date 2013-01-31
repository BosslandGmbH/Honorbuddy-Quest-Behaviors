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


namespace Styx.Bot.Quest_Behaviors
{
    public class AbyssalShelf : CustomForcedBehavior
    {
        ~AbyssalShelf()
        {
            Dispose(false);
        }

        public AbyssalShelf(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                //Location = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ??WoWPoint.Empty;
                QuestId = GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
                MobId = GetAttributeAsNullable<int>("MobId", true, ConstrainAs.MobId, null) ?? 0;
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

        public WoWObject GatewayShaadraz
        {
            get
            {

                return ObjectManager.ObjectList.FirstOrDefault(r => r.Entry == 19292);
                /*return
                    ObjectManager.GetObjectsOfType<WoWGameObject>().Where(u => u.Entry == 183351).OrderBy(
                        ret => ret.Distance).FirstOrDefault();*/
            }
        }
        public WoWObject GatewayMurketh
        {
            get
            {
               //var x = ObjectManager.ObjectList.Where(r => r.Name.Contains("Legion Transporter: Alpha"));

                return ObjectManager.ObjectList.FirstOrDefault(r => r.Entry == 19291);

                /* return
                    ObjectManager.GetObjectsOfType<WoWGameObject>().Where(u => u.Entry == 19291).OrderBy(
                        ret => ret.Distance).FirstOrDefault();*/
            }
        }

        public WoWUnit MoargOverseer
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 19397 && u.IsAlive).OrderBy(ret => ret.Distance).FirstOrDefault();
            }
        }
        public WoWUnit GanArgPeon
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 19398 && u.IsAlive).OrderBy(ret => ret.Distance).FirstOrDefault();
            }
        }
        public WoWUnit FelCannon
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 19399 && u.IsAlive).OrderBy(ret => ret.Distance).FirstOrDefault();
            }
        }



       public WoWUnit Flyer
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == MobId);
            }
        }

        // Attributes provided by caller
        public int MobId { get; private set; }
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
                    new Decorator(ret => IsQuestComplete() && !Me.Combat, new Action(delegate
                    {
                        TreeRoot.StatusText = "Finished!";
                        _isBehaviorDone = true;
                        return RunStatus.Success;
                    }));

            }
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


        public WoWItem Bomb
        {
            get { return Me.BagItems.FirstOrDefault(r => r.Entry == 28132); }
        }

        public Composite GetOn
        {
            get
            {
                return
                    new Decorator(r=> !Me.IsOnTransport,new Action(
                        r=>
                            {
                                Flyer.Interact();
                                GossipFrame.Instance.SelectGossipOption(0);




                            }
                        
                        
                        ));
            }
        }


        public Composite BombOne
        {
            get
            {
                return
                    new Decorator(r => !IsObjectiveComplete(1, (uint)QuestId) && GatewayMurketh != null,
                        new Action(r=>
                                       {
                                           Bomb.Use();
                                           SpellManager.ClickRemoteLocation(GatewayMurketh.Location);


                                       })
                        
                        
                        
                        );
            }
        }

        public Composite BombTwo
        {
            get
            {
                return
                    new Decorator(r => !IsObjectiveComplete(2, (uint)QuestId) && GatewayShaadraz != null,
                        new Action(r =>
                        {
                            Bomb.Use();
                            SpellManager.ClickRemoteLocation(GatewayShaadraz.Location);


                        })



                        );
            }
        }

        //Sigh, were on a taxi so we need todo dirty shit
        public override void OnTick()
        {
            while (!IsDone)
            {
                ObjectManager.Update();

                if (!Me.OnTaxi)
                {
                    Flyer.Interact();
                    GossipFrame.Instance.SelectGossipOption(0);
                }
                else if (Bomb.Cooldown > 0)
                {
                    
                }
                else if (!IsObjectiveComplete(1, (uint)QuestId) && GanArgPeon != null)
                {
                    Bomb.Use();
                    SpellManager.ClickRemoteLocation(GanArgPeon.Location);
                }
                else if (!IsObjectiveComplete(2, (uint)QuestId) && MoargOverseer != null)
                {
                    Bomb.Use();
                    SpellManager.ClickRemoteLocation(MoargOverseer.Location);
                }
                else if (!IsObjectiveComplete(3, (uint)QuestId) && FelCannon != null)
                {
                    Bomb.Use();
                    SpellManager.ClickRemoteLocation(FelCannon.Location);
                }
            }

        }


        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet,GetOn, BombOne,BombTwo , new ActionAlwaysSucceed())));
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

                // Me.QuestLog.GetQuestById(27761).GetObjectives()[2].

                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = this.GetType().Name + ": " +
                                    ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
            }




        }







        #endregion
    }
}