// Behavior originally contributed by mastahg.
//
// DOCUMENTATION:
//     
//

using System;
using System.Collections.Generic;
using System.Linq;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class Defend : CustomForcedBehavior // The Defense of Nahom - Uldum
    {
        ~Defend()
        {
            Dispose(false);
        }

        public Defend(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                Location = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
                QuestId = 28501;//GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
                //MobIds = GetAttributeAsNullable<int>("MobId", true, ConstrainAs.MobId, null) ?? 0;
                QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = QuestInLogRequirement.InLog;

                MobIds = new uint[] { 45543, 45586, 48490, 48462, 48463 };
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
                                                        _isBehaviorDone = true;
                                                        return RunStatus.Success;
                                                    }));

            }
        }




        public void UsePetAbility(string action, params WoWPoint[] Spot)
        {

            var spell = StyxWoW.Me.PetSpells.FirstOrDefault(p => p.ToString() == action);
            if (spell == null)
                return;

            Logging.Write("[Pet] Casting {0}", action);
            Lua.DoString("CastPetAction({0})", spell.ActionBarIndex + 1);
            //if (action == "Move Ramkahen Infantry" || action == "Flame Arrows")
            SpellManager.ClickRemoteLocation(Spot[0]);

        }



        public int GuardsTooFar
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>().Count(
                        u => u.IsAlive && u.Entry == 45643 && u.Location.Distance(Location) > 10);
            }

        }

        public int GuardsLowHealth
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>().Count(u => u.IsAlive && u.Entry == 45643 && u.HealthPercent <= 30);
            }
        }

        public int NearbyEnemies
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>().Count(u => u.IsAlive && u.FactionId == 2334 && u.Location.Distance(Location) < 30);
            }
        }


        public List<WoWUnit> Enemies
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.FactionId == 2334 && u.IsAlive).OrderBy(u => u.Distance).ToList();
                //ObjectManager.GetObjectsOfType<WoWUnit>().Where(u=> MobIds.Contains(u.Entry) && u.IsAlive).OrderBy(u => u.Distance).ToList();
            }
        }


        public Composite BunchUp
        {
            get
            {
                return new Decorator(r => GuardsTooFar > 0, new Action(r => UsePetAbility("Move Ramkahen Infantry", Location)));
            }
        }

        public Composite ShootArrows
        {
            get
            {
                return new Decorator(r => Me.PetSpells[1].Cooldown == false && Enemies.Count > 0, new Action(r => UsePetAbility("Flame Arrows", Enemies[0].Location.RayCast(Enemies[0].Rotation, (float)(Enemies[0].MovementInfo.CurrentSpeed * 2.5)))));
            }
        }

        public Composite Lazor
        {
            get
            {
                return new Decorator(r => (NearbyEnemies > 10 || GuardsLowHealth >= 1) && Me.PetSpells[2].Cooldown == false, new Action(r => UsePetAbility("Sun's Radiance", Location)));
            }
        }

        protected override Composite CreateBehavior()
        {
            //return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(ShootArrows,Lazor, BunchUp, new ActionAlwaysSucceed())));
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone,new PrioritySelector(new Action(ret => Loopstuff()))));
        }



        public void Loopstuff()
        {
            while (true)
            {
                ObjectManager.Update();
                if (IsQuestComplete())
                {
                    _isBehaviorDone = true;
                    break;
                }

                if (Me.PetSpells[1].Cooldown == false && Enemies.Count > 0)
                {
                    UsePetAbility("Flame Arrows",
                                  Enemies[0].Location.RayCast(Enemies[0].Rotation,
                                                              (float) (Enemies[0].MovementInfo.CurrentSpeed*2.5)));
                }
                if ((NearbyEnemies > 10 || GuardsLowHealth >= 1) && Me.PetSpells[2].Cooldown == false)
                {
                    UsePetAbility("Sun's Radiance", Location);
                }
                if (GuardsTooFar > 0)
                {
                    UsePetAbility("Move Ramkahen Infantry", Location);
                }

            }
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

        public bool cancast(int spot)
        {
            var x = Lua.GetReturnValues("return GetPetActionCooldown(" + (spot + 1) + ")");
            if (x[0] != "0")
                return false;

            return true;
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

                TreeRoot.GoalText = this.GetType().Name + ": " +
                                    ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
            }




        }







        #endregion
    }
}