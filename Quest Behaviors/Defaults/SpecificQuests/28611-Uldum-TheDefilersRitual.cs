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
    public class Zakahn : CustomForcedBehavior
    {
        ~Zakahn()
        {
            Dispose(false);
        }

        public Zakahn(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                Location = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
                QuestId = 28611;//GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
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

        private WoWUnit Zah
        {
            get { return (ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == 49148)); }
        }


        private List<WoWUnit> Guards
        {
            get { return (ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 49156 && !u.IsDead).ToList()); }
        }

        public bool isMelee
        {
            get
            {
                return Me.Class == WoWClass.Rogue || Me.Class == WoWClass.DeathKnight || Me.Class == WoWClass.Paladin ||
                       Me.Class == WoWClass.Warrior ||
                       (Me.Class == WoWClass.Shaman && SpellManager.HasSpell("Lava Lash")) ||
                       (Me.Class == WoWClass.Druid && SpellManager.HasSpell("Mangle"));
            }
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

        public Composite DoDps
        {
            get
            {
                return
                    new PrioritySelector(
                        new Decorator(ret => RoutineManager.Current.CombatBehavior != null, RoutineManager.Current.CombatBehavior),
                        new Action(c => RoutineManager.Current.Combat()));
            }
        }

        public Composite DoPull
        {
            get
            {
                return
                    new PrioritySelector(
                        new Decorator(ret => RoutineManager.Current.PullBehavior != null, RoutineManager.Current.PullBehavior),
                        new Action(c => RoutineManager.Current.Pull()));
            }
        }

        public Composite CheckSpot
        {
            get
            {
                return new Decorator(ret => !Me.Combat && Me.Location.Distance(Location) > 1, new Action(ret => Navigator.MoveTo(Location)));
            }
        }


        public void SetPetMode(string action)
        {

            var spell = StyxWoW.Me.PetSpells.FirstOrDefault(p => p.ToString() == action);
            if (spell == null)
                return;

            Logging.Write("[Pet] Casting {0}", action);
            Lua.DoString("CastPetAction({0})", spell.ActionBarIndex + 1);

        }

        public void PullMob()
        {
            string spell = "";

            switch (Me.Class)
            {
                case WoWClass.Mage:
                    spell = "Ice Lance";
                    break;
                case WoWClass.Druid:
                    spell = "Moonfire";
                    break;
                case WoWClass.Paladin:
                    spell = "Judgement";
                    break;
                case WoWClass.Priest:
                    spell = "Shadow Word: Pain";
                    break;
                case WoWClass.Shaman:
                    spell = "Flame Shock";
                    break;
                case WoWClass.Warlock:
                    if (Me.GotAlivePet)
                        SetPetMode("Passive");

                    spell = "Corruption";
                    break;
                case WoWClass.DeathKnight:
                    if (Me.GotAlivePet)
                        SetPetMode("Passive");

                    spell = "Dark Command";
                    break;
                case WoWClass.Hunter:
                    if (Me.GotAlivePet)
                        SetPetMode("Passive");

                    spell = "Arcane Shot";
                    break;
                case WoWClass.Warrior:
                    if (SpellManager.CanCast("Shoot"))
                        spell = "Shoot";
                    if (SpellManager.CanCast("Throw"))
                        spell = "Throw";
                    break;
                case WoWClass.Rogue:
                    if (SpellManager.CanCast("Shoot"))
                        spell = "Shoot";
                    if (SpellManager.CanCast("Throw"))
                        spell = "Throw";
                    break;

            }

            if (!String.IsNullOrEmpty(spell))
            {
                SpellManager.Cast(spell);
            }


        }
        public Composite PullOne
        {
            get
            {
                return new Decorator(ret => !Me.Combat, new Action(delegate
                {
                    Navigator.PlayerMover.MoveStop();
                    Guards[0].Target();
                    Guards[0].Face();
                    PullMob();
                }));
            }
        }


        public Composite KillIt
        {
            get
            {
                return new Decorator(ret => (Me.CurrentTarget != null && Me.CurrentTarget.Distance < 1) || Me.Class == WoWClass.Hunter, Bots.Grind.LevelBot.CreateCombatBehavior());
            }
        }


        public Composite KillAdds
        {
            get
            {
                return new Decorator(ret => Guards.Count > 0, new PrioritySelector(CheckSpot, PullOne, KillIt));
            }
        }



        public Composite TargetHim
        {
            get
            {
                return new Decorator(ret => Me.CurrentTarget == null && Zah != null, new Action(delegate
                {
                    Zah.Target();
                    Zah.Face();
                    if (Me.GotAlivePet)
                        SetPetMode("Assist");

                }))


                ;
            }
        }



        public Composite Pullhim
        {
            get { return new Decorator(ret => !Me.Combat, DoPull); }
        }

        public Composite KillBoss
        {
            get
            {
                return new Decorator(ret => Zah != null && !Zah.IsDead, new PrioritySelector(TargetHim, Pullhim, Bots.Grind.LevelBot.CreateCombatBehavior()));
            }
        }

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, KillAdds, KillBoss, new ActionAlwaysSucceed())));
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




                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = this.GetType().Name + ": " +
                                    ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
            }




        }







        #endregion
    }
}