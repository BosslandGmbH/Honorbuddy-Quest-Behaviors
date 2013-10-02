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


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.FindingYourCenter
{
    [CustomBehaviorFileName(@"SpecificQuests\29890-JadeForest-FindingYourCenter")]
    public class FindingYourCenter : CustomForcedBehavior
    {
        private bool _isBehaviorDone;


        private Composite _root;
        private bool _useMount;
        private bool _isDisposed;
        public QuestCompleteRequirement questCompleteRequirement = QuestCompleteRequirement.NotComplete;
        public QuestInLogRequirement questInLogRequirement = QuestInLogRequirement.InLog;

        public FindingYourCenter(Dictionary<string, string> args) : base(args)
        {
            try
            {
                QuestId = 29890; //GetAttributeAsQuestId("QuestId", true, null) ?? 0;
            }
            catch
            {
                Logging.Write("Problem parsing a QuestId in behavior: Rampage Against The Machine");
            }
        }

        ~FindingYourCenter()
        {
            Dispose(false);
        }

        public int QuestId { get; set; }


        public override bool IsDone
        {
            get { return _isBehaviorDone; }
        }


        private LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
        }

        public Composite DoneYet
        {
            get
            {
                return new Decorator(
                    ret => IsQuestComplete(),
                    new Action(
                        delegate
                        {
                            TreeRoot.StatusText = "Finished!";
                            _isBehaviorDone = true;
                            return RunStatus.Success;
                        }));
            }
        }


        private int power
        {
            get { return Lua.GetReturnVal<int>("return UnitPower(\"player\", ALTERNATE_POWER_INDEX)", 0); }
        }

        public Composite Balance
        {
            get
            {
                return new Decorator(ctx => Me.InVehicle,
                    new PrioritySelector(
                        new Decorator(r => power <= 40, new Action(r => UsePetAbility("Focus"))),
                        new Decorator(r => power >= 60, new Action(r => UsePetAbility("Relax")))));
            }
        }

        private Composite DrinkBrew
        {
            get
            {
                var brewLoc = new WoWPoint(-631.5737, -2365.238, 22.87861);
                WoWGameObject brew = null;
                const uint brewId = 213754;

                return
                    new PrioritySelector(
                        new Decorator(
                            ctx => !Me.InVehicle,
                            new PrioritySelector(
                                ctx => brew = ObjectManager.GetObjectsOfTypeFast<WoWGameObject>().FirstOrDefault(g => g.Entry == brewId),
                                new Decorator(ctx => brew != null && !brew.WithinInteractRange, new Action(ctx => Navigator.MoveTo(brew.Location))),
                                new Decorator(
                                    ctx => brew != null && brew.WithinInteractRange,
                                    new PrioritySelector(
                                        new Decorator(ctx => Me.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
                                        new Sequence(new Action(ctx => brew.Interact()), new WaitContinue(3, ctx => false, new ActionAlwaysSucceed())))))));
            }
        }


        public override void OnStart()
        {
            OnStart_HandleAttributeProblem();
            if (!IsDone)
            {
                TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_MainCombat());

                _useMount = CharacterSettings.Instance.UseMount;
                CharacterSettings.Instance.UseMount = false;

                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint) QuestId);
                TreeRoot.GoalText = ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
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
                    TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_MainCombat());
                    CharacterSettings.Instance.UseMount = _useMount;
                }

                // Clean up unmanaged resources (if any) here...
                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public bool IsQuestComplete()
        {
            var quest = StyxWoW.Me.QuestLog.GetQuestById((uint) QuestId);
            return quest == null || quest.IsCompleted;
        }

        private bool IsObjectiveComplete(int objectiveId, uint questId)
        {
            if (Me.QuestLog.GetQuestById(questId) == null)
            {
                return false;
            }
            int returnVal = Lua.GetReturnVal<int>("return GetQuestLogIndexByID(" + questId + ")", 0);
            return Lua.GetReturnVal<bool>(string.Concat(new object[] {"return GetQuestLogLeaderBoard(", objectiveId, ",", returnVal, ")"}), 2);
        }

        public void UsePetAbility(string action)
        {
            var spell = StyxWoW.Me.PetSpells.FirstOrDefault(p => p.ToString() == action);
            if (spell == null)
                return;

            Logging.Write("[Pet] Casting {0}", action);
            Lua.DoString("CastPetAction({0})", spell.ActionBarIndex + 1);
        }


        protected Composite CreateBehavior_MainCombat()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet,
                DrinkBrew,
                Balance)));
        }
    }
}