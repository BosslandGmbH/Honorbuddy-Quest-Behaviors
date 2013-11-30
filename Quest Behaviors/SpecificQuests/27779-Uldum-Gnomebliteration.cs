// Behavior originally contributed by mastahg.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

#region Summary and Documentation
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.Gnomebliteration
{
    [CustomBehaviorFileName(@"SpecificQuests\27779-Uldum-Gnomebliteration")]
    public class KillGnomes : CustomForcedBehavior
    {
        ~KillGnomes()
        {
            Dispose(false);
        }

        public KillGnomes(Dictionary<string, string> args)
            : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                QuestId = 27779;
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
                QBCLog.Exception(except);
                IsAttributeProblem = true;
            }
        }


        // Attributes provided by caller
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }

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


        public Composite DoDps
        {
            get
            {
                return new PrioritySelector(
                    new Decorator(ret => RoutineManager.Current.CombatBehavior != null, RoutineManager.Current.CombatBehavior),
                    new Action(c => RoutineManager.Current.Combat()));
            }
        }


        #region Overrides of CustomForcedBehavior

        public Composite DoneYet
        {
            get
            {
                return
                    new Decorator(ret => Me.IsQuestComplete(QuestId),
                        new Action(delegate
                        {
                            TreeRoot.StatusText = "Finished!";
                            _isBehaviorDone = true;
                            return RunStatus.Success;
                        }));
            }
        }

        public List<WoWUnit> Enemies
        {
            get
            {
                var myLoc = Me.Location;
                return (from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
                        where unit.Entry == 46384 && unit.IsAlive && unit.HealthPercent >= 100 && !Blacklist.Contains(unit, BlacklistFlags.Interact)
                        let loc = unit.Location
                        where unit.Location.DistanceSqr(BadBomb) > 60*60
                        orderby loc.DistanceSqr(myLoc)
                        select unit).ToList();
            }
        }



        public Composite Combat
        {
            get
            {
                return new Decorator(ret => Me.Combat,DoDps);
            }
        }


        private readonly WaitTimer _exitTimer = new WaitTimer(TimeSpan.FromSeconds(10));
        public Composite RunEmOver
        {
            get
            {
                return 
                    new PrioritySelector(ctx => Enemies,
                        new Decorator(
                            ret => WoWMovement.ActiveMover.MovementInfo.IsFalling,
                            new ActionAlwaysSucceed()),
                        new Decorator(
                            ret => _exitTimer.IsFinished && !((List<WoWUnit>)ret).Any(),
                            new Action(ret => Lua.DoString("VehicleExit()"))),
                        new ActionFail(ret =>
                            {
                                var closeBys = ((List<WoWUnit>) ret).Where(u => u.DistanceSqr < 10f*10f);

                                foreach (var unit in closeBys)
                                {
                                    Blacklist.Add(unit, BlacklistFlags.Interact, TimeSpan.FromMinutes(10));
                                }
                            }),
                        new Decorator(ret => Me.IsOnTransport && ((List<WoWUnit>)ret).Any(),
                            new Action(r => Navigator.MoveTo(((List<WoWUnit>)r)[0].Location))));
            }
        }

        //<Vendor Name="Fusion Core" Entry="46750" Type="Repair" X="" />

        WoWPoint OrbLoc = new WoWPoint(-10641.33,-2344.599,144.8416);
        //<Vendor Name="Crazed Gnome" Entry="46384" Type="Repair" X="-10542.87" Y="-2411.554" Z="88.44117" />
        WoWPoint BadBomb = new WoWPoint(-10561.68, -2429.371, 91.56037);
        public WoWUnit Orb
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == 46750);
            }
        }
        public Composite FlyClose
        {
            get
            {
                return new Decorator(ret => !Me.IsOnTransport && (Orb == null || Orb.DistanceSqr > 5 * 5),
                    new Action(r => Flightor.MoveTo(OrbLoc)));
            }
        }

        public Composite Interact
        {
            get
            {
                return new PrioritySelector(
                    ctx => Orb,
                    new Decorator(
                        ret => !Me.IsOnTransport && ret != null && ((WoWUnit)ret).Distance <= 5,
                        new Sequence(
                            new DecoratorContinue(ctx => Me.Mounted, 
                                new Sequence(
                                    new Action(ctx => Mount.Dismount("interacting with orb")),
                                    new WaitContinue(2, ctx => !Me.Mounted, new ActionAlwaysSucceed()))),
                        new Action(ctx => ((WoWUnit)ctx).Interact()),                                    
                        new WaitContinue(2, ctx => GossipFrame.Instance.IsVisible, new ActionAlwaysSucceed()),
                        new Action(
                            delegate
                            {
                                _exitTimer.Reset();
                                Lua.DoString("SelectGossipOption(1)");
                            }))));
            }
        }

  

        protected Composite CreateBehavior_CombatMain()
        {

            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, Combat, FlyClose,Interact,RunEmOver)));
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
                TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_CombatMain());

                this.UpdateGoalText(QuestId);
            }
        }
        #endregion
    }
}