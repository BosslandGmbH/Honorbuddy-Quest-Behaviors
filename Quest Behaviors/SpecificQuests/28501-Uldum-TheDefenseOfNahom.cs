// Behavior originally contributed by mastahg.
//
// DOCUMENTATION:
//     
//

using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.TheDefenseOfNahom
{
    [CustomBehaviorFileName(@"SpecificQuests\28501-Uldum-TheDefenseOfNahom")]
    public class Defend : CustomForcedBehavior // The Defense of Nahom - Uldum
    {

        private bool _isBehaviorDone;
        private bool _isDisposed;
        private Composite _root;

        public Defend(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                QuestId = 28501;
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
                LogMessage(
                    "error",
                    "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message + "\nFROM HERE:\n" + except.StackTrace +
                    "\n");
                IsAttributeProblem = true;
            }
        }


        // Attributes provided by caller
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }
        // Private variables for internal state

        // Private properties
        static private LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
        }

        public bool IsQuestComplete()
        {
            var quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
            return quest == null || quest.IsCompleted || quest.IsFailed;
        }

        #region Overrides of CustomForcedBehavior

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
                TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_CombatMain());
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
                TreeRoot.GoalText = GetType().Name + ": " +
                                    ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
            }
        }

        #endregion

        #region Behaviors

        private Composite CreateBehavior_CombatMain()
        {
            var info = new EncounterInfo();

            return _root ?? (_root =
                new Decorator(
                    ctx => !_isBehaviorDone,
                    new PrioritySelector(
                        ctx => info.Update(),
                        CreateBehavior_CheckQuestCompletion(),

                        new Decorator(ctx => info.RadianceTarget != WoWPoint.Empty && CanCastPetSpell(3),
                            new Action(
                                ctx =>
                                {
                                    CastPetSpell(3, info.RadianceTarget);
                                   // return RunStatus.Failure;
                                })),

                        new Decorator(ctx => info.VolleyPosition != WoWPoint.Empty && CanCastPetSpell(2),
                            new Action(
                                ctx =>
                                {
                                    CastPetSpell(2, info.VolleyPosition);
                                    //return RunStatus.Failure;
                                })),

                        new Decorator(ctx => info.ChampionRallyPosition != WoWPoint.Empty && CanCastPetSpell(1),
                            new Action(ctx => CastPetSpell(1, info.ChampionRallyPosition)))

                        )));
        }


        private Composite CreateBehavior_CheckQuestCompletion()
        {
            return new Decorator(
                ctx => IsQuestComplete() ,
                new Sequence(
                    new Action(ctx => _isBehaviorDone = true)));
        }

        /// <summary>
        /// checks if a pet spell can be casted
        /// </summary>
        /// <param name="slot">1-based index into the pet spell bar</param>
        /// <returns></returns>
        bool CanCastPetSpell(int slot)
        {
            return !Me.PetSpells[slot - 1].Cooldown;
        }

        void CastPetSpell(int slot, WoWPoint targetPosition = new WoWPoint())
        {
            QBCLog.Info("[Pet] Casting {0}", Me.PetSpells[slot - 1].Spell.Name);
            Lua.DoString("CastPetAction({0})", slot);
            if (targetPosition != WoWPoint.Zero)
                SpellManager.ClickRemoteLocation(targetPosition);
        }

        private class EncounterInfo
        {
            private const uint GreaterColossusId = 48490;
            private const uint EnsorceledColossusId = 45586;
            private const uint NefersetInfantryId = 45543;

            private const uint RamkahenChampionId = 45643;
            private const uint RamkahenArcherId = 45679;
            private readonly WoWPoint _encounterLocaction = new WoWPoint(-9762.981, -1693.467, 22.2556);

            public WoWPoint VolleyPosition { get; private set; }
            public WoWPoint ChampionRallyPosition { get; private set; }
            public WoWPoint RadianceTarget { get; private set; }

            internal EncounterInfo Update()
            {
                var hostileForces = (from unit in ObjectManager.GetObjectsOfTypeFast<WoWUnit>()
                                     where unit.FactionId == 2334 && unit.IsAlive
                                     let loc = unit.Location
                                     orderby loc.DistanceSqr(_encounterLocaction)
                                     // project WoWUnit.Location to minimize the number of injections. 
                                     select new { Location = loc, Unit = unit }).ToList();

                var friendlyForces = (from unit in ObjectManager.GetObjectsOfTypeFast<WoWUnit>()
                                      where unit.FactionId == 2333 && unit.IsAlive
                                      let loc = unit.Location
                                      orderby loc.DistanceSqr(_encounterLocaction)
                                      select new { Location = loc, Unit = unit }).ToList();

                var bestVolleyTarget =
                    hostileForces.OrderByDescending(
                        u => hostileForces.Count(v => v != u && v.Location.DistanceSqr(u.Location) < 10 * 10)).FirstOrDefault();

                VolleyPosition = bestVolleyTarget != null
                    ? bestVolleyTarget.Location.RayCast(
                        WoWMathHelper.NormalizeRadian(bestVolleyTarget.Unit.Rotation),
                        bestVolleyTarget.Location.Distance(_encounterLocaction) *
                        bestVolleyTarget.Unit.MovementInfo.CurrentSpeed * 0.04f)
                    : WoWPoint.Empty;

                var nearbyHostileUnit =
                    hostileForces.FirstOrDefault(u => u.Location.DistanceSqr(_encounterLocaction) <= 30 * 30);

                ChampionRallyPosition = nearbyHostileUnit != null &&
                                        !friendlyForces.Any(
                                            u =>
                                                u.Unit.Entry == RamkahenChampionId &&
                                                u.Location.DistanceSqr(nearbyHostileUnit.Location) < 12 * 12)
                    ? nearbyHostileUnit.Location
                    : WoWPoint.Empty;

                var radianceTargetUnit =
                    friendlyForces.Where(u => u.Unit.HealthPercent < 70).OrderByDescending(u => friendlyForces.Count(v => v.Unit.HealthPercent < 70))
                        .FirstOrDefault() ?? nearbyHostileUnit;

                RadianceTarget = radianceTargetUnit != null ? radianceTargetUnit.Location : WoWPoint.Empty;
                return this;
            }
        }

        #endregion

        #region Cleanup

        ~Defend()
        {
            Dispose(false);
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
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
                    TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_CombatMain());
                }

                // Clean up unmanaged resources (if any) here...
                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }

        #endregion
    }
}