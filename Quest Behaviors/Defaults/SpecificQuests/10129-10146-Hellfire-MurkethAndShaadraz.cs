// Behavior originally contributed by mastahg / rework by chinajadeb
//
// DOCUMENTATION:
//     
//

#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using Bots.Grind;

using CommonBehaviors.Actions;

using Honorbuddy.QuestBehaviorCore;

using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.MurkethAndShaadraz
{
    [CustomBehaviorFileName(@"SpecificQuests\10129-10146-Hellfire-MurkethAndShaadraz")]
    public class MurkethAndShaadraz : QuestBehaviorBase
    {
        #region Constructor and Argument Processing
        public MurkethAndShaadraz(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // NB: Core attributes are parsed by QuestBehaviorBase parent (e.g., QuestId, NonCompeteDistance, etc)

                // Behavior-specific attributes...
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

        protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
        {
            // empty
        }

        protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
        {
            // empty
        }


        // Attributes provided by caller
        private double BombRange = 90.0;    // Range is actually 100.0, but we allow for factor of safety
        private const int ItemId_Bomb = 28038;
        private int MobId_FlightMaster { get; set; }    // changes based on faction--set in OnStart()
        private const int MobId_GatewayMurketh = 19291;
        private const int MobId_GatewayShaadraz = 19292;
        private WoWPoint WaitLocation { get; set; }     // changes based on faction--set in OnStart()
        #endregion


        #region Private and Convenience variables
        private WoWItem Bomb
        {
            get
            {
                return _bomb ?? (_bomb = Me.CarriedItems.FirstOrDefault(o => o.Entry == ItemId_Bomb));
            }
        }

        private WoWUnit FlightMaster { get; set; }
        private WoWObject SelectedTarget { get; set; }

        private Composite _behaviorTreeHook_TaxiCheck = null;
        private WoWItem _bomb = null;
        #endregion


        #region Destructor, Dispose, and cleanup
        ~MurkethAndShaadraz()
        {
            Dispose(false);
        }

        protected override void Dispose(bool isExplicitlyInitiatedDispose)
        {
            if (!_isDisposed)
            {
                if (_behaviorTreeHook_TaxiCheck != null)
                {
                    TreeHooks.Instance.RemoveHook("Taxi_Check", _behaviorTreeHook_TaxiCheck);
                    _behaviorTreeHook_TaxiCheck = null;
                }

                base.Dispose(isExplicitlyInitiatedDispose);
            }
        }
        #endregion


        #region Overrides of CustomForcedBehavior
        public override void OnStart()
        {
            // Let QuestBehaviorBase do basic initializaion of the behavior, deal with bad or deprecated attributes,
            // capture configuration state, install BT hooks, etc.  This will also update the goal text.
            OnStart_QuestBehaviorCore(string.Empty);

            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (!IsDone)
            {
                MobId_FlightMaster = Me.IsAlliance
                    ? 19409     // Wing Commander Dabir'ee
                    : 19401;    // Wing Commander Brack
                WaitLocation =
                    Me.IsAlliance
                    ? new WoWPoint(-670.4271, 1851.844, 66.9099).FanOutRandom(4.0)      // Wing Commander Dabir'ee
                    : new WoWPoint(-24.09538, 2125.857, 112.7034).FanOutRandom(4.0);    // Wing Commander Brack

                _behaviorTreeHook_TaxiCheck = new ExceptionCatchingWrapper(this, CreateBehavior_TaxiCheck());
                TreeHooks.Instance.InsertHook("Taxi_Check", 0, _behaviorTreeHook_TaxiCheck);
            }
        }
        #endregion


        #region Main Behaviors
        private Composite CreateBehavior_TaxiCheck()
        {
            return new Decorator(context => !IsDone && Me.OnTaxi,
                new PrioritySelector(
                    // Disable combat while on the Taxi...
                    // NB: We don't use "&=" with BehaviorFlags, so the change will be properly recorded in the HB log.
                    new Decorator(context => ((LevelBot.BehaviorFlags & BehaviorFlags.Combat) != 0),
                        new Action(context => { LevelBot.BehaviorFlags = LevelBot.BehaviorFlags & ~BehaviorFlags.Combat; })),

                    // Just wait, if bomb is on cooldown...
                    new Decorator(context => Bomb.Cooldown > 0,
                        new ActionAlwaysSucceed()),

                    // Bomb Targets...
                    SubBehavior_BombTarget(context => 1, context => MobId_GatewayMurketh),
                    SubBehavior_BombTarget(context => 2, context => MobId_GatewayShaadraz)
                ));
        }


        protected override Composite CreateMainBehavior()
        {
            return new PrioritySelector(
                // PvP server considerations...
                // Combat is disabled while on the Taxi.  If on the ground, we want it enabled
                // in case we get attacked on a PvP server.
                // NB: We don't use "|=" with BehaviorFlags, so the change will be properly recorded in the HB log.
                new Decorator(context => ((LevelBot.BehaviorFlags & BehaviorFlags.Combat) == 0),
                    new Action(context => { LevelBot.BehaviorFlags = LevelBot.BehaviorFlags | BehaviorFlags.Combat; })),

                // Move to flight master, and interact to take taxi ride...
                new Decorator(context => !Me.OnTaxi,
                    new PrioritySelector(context =>
                    {
                        FlightMaster =
                            Query.FindMobsAndFactions(Utility.ToEnumerable<int>(MobId_FlightMaster))
                            .FirstOrDefault()
                            as WoWUnit;

                        return context;
                    },

                        // If flight master not in view, move to where he should be...
                        new Decorator(context => FlightMaster == null,
                            new UtilityBehaviorPS.MoveTo(
                                context => WaitLocation,
                                context => "FlightMaster location",
                                context => MovementBy)),

                        // Make certain the bombs are in our backpack...
                        new UtilityBehaviorPS.WaitForInventoryItem(
                            context => ItemId_Bomb,
                            context =>
                            {
                                QBCLog.ProfileError("Cannot continue without required item: {0}",
                                                    Utility.GetItemNameFromId(ItemId_Bomb));
                                BehaviorDone();
                            }),

                        // Move to flightmaster, and gossip to hitch a ride...
                        new Decorator(context => FlightMaster != null,
                            new PrioritySelector(
                                new Decorator(context => !FlightMaster.WithinInteractRange,
                                    new UtilityBehaviorPS.MoveTo(
                                        context => FlightMaster.Location,
                                        context => FlightMaster.SafeName(),
                                        context => MovementBy)),
                                new UtilityBehaviorPS.MoveStop(),
                                new Decorator(context => Me.Mounted,
                                    new Action(context => { Mount.Dismount(); })),
                                new Decorator(context => !GossipFrame.Instance.IsVisible,
                                    new Action(context => { FlightMaster.Interact(); })),
                                new Action(context => { GossipFrame.Instance.SelectGossipOption(0); })
                            ))
                    ))
            );
        }
        #endregion


        private Composite SubBehavior_BombTarget(ProvideIntDelegate questObjectiveIndexDelegate,
                                                 ProvideIntDelegate mobIdDelegate)
        {
            Contract.Requires(questObjectiveIndexDelegate != null, context => "questObjectiveIndexDelegate != null");
            Contract.Requires(mobIdDelegate != null, context => "mobIdDelegate != null");

            return new Decorator(context =>
            {
                var questObjectiveIndex = questObjectiveIndexDelegate(context);
                var mobId = mobIdDelegate(context);

                if (Me.IsQuestObjectiveComplete(QuestId, questObjectiveIndex))
                    { return false; }

                SelectedTarget = ObjectManager.ObjectList.FirstOrDefault(o => o.Entry == mobId);
                if (!Query.IsViable(SelectedTarget))
                    { return false; }

                return Me.Location.Distance(SelectedTarget.Location) <= BombRange;
            },
                    new Action(context =>
                    {
                        QBCLog.DeveloperInfo("Bombing {0}", SelectedTarget.SafeName());
                        Bomb.Use();
                        // NB: The "FanOutRandom()" simulates imperfect human aim.
                        SpellManager.ClickRemoteLocation(SelectedTarget.Location.FanOutRandom(7.5));
                    }));
        }
    }
}