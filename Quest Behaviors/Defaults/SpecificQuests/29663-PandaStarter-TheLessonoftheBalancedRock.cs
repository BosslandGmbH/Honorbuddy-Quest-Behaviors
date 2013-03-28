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


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.TheLessonoftheBalancedRock
{
    [CustomBehaviorFileName(@"SpecificQuests\29663-PandaStarter-TheLessonoftheBalancedRock")]
    public class TheLessonoftheBalancedRock : CustomForcedBehavior
    {
        ~TheLessonoftheBalancedRock()
        {
            Dispose(false);
        }

        public TheLessonoftheBalancedRock(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                //Location = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ??WoWPoint.Empty;
                QuestId = 29663; //GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
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
        private LocalPlayer Me { get { return StyxWoW.Me; } }
        private WoWUnit SelectedMonk { get; set; }
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private Composite _root;


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


        //<Vendor Name="Pearlfin Poolwatcher" Entry="55709" Type="Repair" X="-100.9809" Y="-2631.66" Z="2.150823" />
        //<Vendor Name="Pearlfin Poolwatcher" Entry="55711" Type="Repair" X="-130.8297" Y="-2636.422" Z="1.639656" />

        //209691 - sniper rifle
        public WoWUnit FindMonk()
        {
            return
                ObjectManager.GetObjectsOfType<WoWUnit>()
                .Where(r => (r.Entry == 55019 || r.Entry == 65468) && !r.IsFriendly)
                .OrderBy(r=>r.Distance)
                .FirstOrDefault();
        }

        
        public IEnumerable<WoWUnit> Poles
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>()
                    .Where(r => (r.Entry == 54993 || r.Entry == 57626) && r.NpcFlags == 16777216);
            }
        }


        private int stage = 0;
        WoWPoint spot = new WoWPoint(966.1218,3284.928,126.7932);
        

        private bool spoke = false;


        private Composite GetonPole
        {
            get
            {
                return new PrioritySelector(
                    new Decorator(r=> !Me.InVehicle && Me.Location.Distance(spot) > 10,
                        new Action(r=>Navigator.MoveTo(spot))),
                    new Decorator(r=> !Me.InVehicle,
                        new Action(r=>Poles.OrderBy(z=>z.Distance).FirstOrDefault().Interact(true)))             
                    );
            }
        }


        // 24Feb2013-08:11UTC chinajade
        private bool IsViable(WoWObject wowObject)
        {
            return
                (wowObject != null)
                && wowObject.IsValid;
        }
        
        
        // 24Feb2013-08:11UTC chinajade
        private bool IsViableForFighting(WoWUnit wowUnit)
        {
            return
                IsViable(wowUnit)
                && wowUnit.IsAlive
                && !wowUnit.IsFriendly
                && !Blacklist.Contains(wowUnit, BlacklistFlags.Combat);
        }


        private Composite PoleCombat
        {
            get
            {
                return new PrioritySelector(
                    new Decorator(context => !IsViableForFighting(SelectedMonk),
                        new Action(context => { SelectedMonk = FindMonk(); })),

                    new Decorator(r=> SelectedMonk.Distance <= 5,
                        new PrioritySelector(
                            new Decorator(context => Me.CurrentTarget != SelectedMonk,
                                new Action(context => { SelectedMonk.Target(); })),

                            new Decorator(context => !Me.IsSafelyFacing(SelectedMonk),
                                new Action(context => { SelectedMonk.Face(); })),

                            // Poor man's combat routine (in case main CR can't handle being in a vehicle)...
                            new Action(context =>
                            {
                                SelectedMonk.Interact();  // "Auto-attack" at a minimum...
                                return RunStatus.Failure; // fall through
                            }),
                            new Decorator(context => SpellManager.CanCast(3044),
								new Action(context => { SpellManager.Cast(3044); })), // Hunter - Arcane Shot
                            new Decorator(context => SpellManager.CanCast(56641),
								new Action(context => { SpellManager.Cast(56641); })), // Hunter - Steady Shot
                            new Decorator(context => SpellManager.CanCast(44614),
                                new Action(context => { SpellManager.Cast(44614); })), // Mage   - Frostfire Bolt
                            new Decorator(context => SpellManager.CanCast(2136),
                                new Action(context => { SpellManager.Cast(2136); })), // Mage   - Fire Blast
                            new Decorator(context => SpellManager.CanCast(100780),
                                new Action(context => { SpellManager.Cast(100780); })), // Monk   - Jab
                            new Decorator(context => SpellManager.CanCast(100787),
                                new Action(context => { SpellManager.Cast(100787); })), // Monk   - Tiger Palm
                            new Decorator(context => SpellManager.CanCast(585),
                                new Action(context => { SpellManager.Cast(585); })), // Priest - Smite
                            new Decorator(context => SpellManager.CanCast(1752),
                                new Action(context => { SpellManager.Cast(1752); })), // Rogue  - Sinister Strike
                            new Decorator(context => SpellManager.CanCast(2098),
                                new Action(context => { SpellManager.Cast(2098); })), // Rogue  - Eviscerate
                            new Decorator(context => SpellManager.CanCast(403),
                                new Action(context => { SpellManager.Cast(403); })), // Shaman - Lightning Bolt
                            new Decorator(context => SpellManager.CanCast(73899),
                                new Action(context => { SpellManager.Cast(73899); })), // Shaman - Primal Strike
                            new Decorator(context => SpellManager.CanCast(78),
                                new Action(context => { SpellManager.Cast(78); })), // Warrior- Heroic Strike
                            new Decorator(context => SpellManager.CanCast(34428),
                                new Action(context => { SpellManager.Cast(34428); })), // Warrior- Victory Rush

                            // Most combat routines will fail here, because being on the pole is considered being in a vehicle...
                            new Decorator(targetContext => RoutineManager.Current.CombatBehavior != null,
                                RoutineManager.Current.CombatBehavior),
                            new Action(targetContext => { RoutineManager.Current.Combat(); })
                        )),

                    new Decorator(r=> SelectedMonk.Distance > 5,
                        new Action(delegate
                        {                                                                 
                            var Pole =
                                Poles.Where(r => r.WithinInteractRange)
                                    .OrderBy(r => r.Location.Distance(SelectedMonk.Location))
                                    .FirstOrDefault();
                            Pole.Interact(true);
                        }))
                );
            }
        }


        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new Decorator(ret => !_isBehaviorDone,
                    new PrioritySelector(DoneYet, GetonPole,PoleCombat, new ActionAlwaysSucceed()))
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
