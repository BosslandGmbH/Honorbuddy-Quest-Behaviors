// Behavior originally contributed by Natfoth.
//
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_NoControlVehicle
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Styx.Logic.BehaviorTree;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Tripper.Tools.Math;
using Action = TreeSharp.Action;
using System.Globalization;


namespace Styx.Bot.Quest_Behaviors
{
    public class NoControlVehicle : CustomForcedBehavior
    {
        /// <summary>
        /// For Vehicles you do not have to move, such as Cannons, Horses, Bombings, and even ground targeting cannons.
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// NpcMountID: MobId of the vehicle before it is mounted.
        /// VehicleId: Between 0 - 99 The lower the number the closer to the ground it will be
        /// TargetId, TargetId2, ...TargetIdN: Mob of the actual Vehicle, sometimes it will be the some but sometimes it will not be.
        /// SpellIndex: Button bar Number starting from 1 
        /// OftenToUse: This is used for a few quests that the mob is flying but respawns fast, So the bot can fire in the same spot over and over.
        /// TimesToUse: Where you want to be at when you fire.
        /// TypeId: Where you want to aim.
        /// PreviousFireLocation Coords: This should only be used if you are already inside of the vehicle when you call the behaviors again, and
        ///                                 should be the same coords as FireLocation on the call before it, Check the Wiki for more info or examples.
        /// </summary>
        ///
        public NoControlVehicle(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.

                AttackButton = GetAttributeAsNullable<int>("AttackButton", true, ConstrainAs.HotbarButton, new[] { "AttackIndex", "SpellIndex" }) ?? 0;
                AttackButton2 = GetAttributeAsNullable<int>("AttackButtonSecondary", false, ConstrainAs.HotbarButton, new[] { "AttackIndexSecondary", "SpellIndexSecondary" }) ?? 0;
                GoHomeButton = GetAttributeAsNullable<int>("GoHomeButton", false, ConstrainAs.HotbarButton, new[] { "HomeIndex" }) ?? 0;
                MaxRange = GetAttributeAsNullable<double>("MaxRange", false, ConstrainAs.Range, null) ?? 1;
                MountedPoint = WoWPoint.Empty;
                NumOfTimes = GetAttributeAsNullable<int>("NumOfTimes", false, ConstrainAs.RepeatCount, new[] { "TimesToUse" }) ?? 1;
                Range = GetAttributeAsNullable<double>("Range", false, ConstrainAs.Range, null) ?? 400;
                OftenToUse = GetAttributeAsNullable<int>("OftenToUse", false, ConstrainAs.Milliseconds, null) ?? 1000;
                QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
                SpellType = GetAttributeAsNullable<int>("TypeId", false, new ConstrainTo.Domain<int>(0, 6), null) ?? 2;
                ItemId = GetAttributeAsNullable<int>("ItemId", false, ConstrainAs.ItemId, null) ?? 1;
                TargetIds = GetNumberedAttributesAsArray<int>("TargetId", 1, ConstrainAs.MobId, new[] { "MobId", "NpcId" });
                VehicleId = GetAttributeAsNullable<int>("VehicleId", false, ConstrainAs.VehicleId, null) ?? 0;
                VehicleMountId = GetAttributeAsNullable<int>("VehicleMountId", false, ConstrainAs.VehicleId, new[] { "NpcMountId", "NpcMountID" }) ?? 1;
                WaitTime = GetAttributeAsNullable<int>("WaitTime", false, ConstrainAs.Milliseconds, null) ?? 0;


                Counter = 1;
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
                                    + "\nFROM HERE:\n"
                                    + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }


        // Attributes provided by caller
        public int AttackButton { get; private set; }
        public int AttackButton2 { get; private set; }
        public int GoHomeButton { get; private set; }
        public double MaxRange { get; private set; }
        public WoWPoint MountedPoint { get; private set; }
        public int OftenToUse { get; private set; }
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }
        public int SpellType { get; private set; }
        public int ItemId { get; private set; }
        public int[] TargetIds { get; private set; }
        public int NumOfTimes { get; private set; }
        public int WaitTime { get; private set; }
        public int VehicleId { get; private set; }
        public int VehicleMountId { get; private set; }
        public double Range { get; private set; }

        // Private variables for internal state
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private Composite _root;
        private Helpers.WaitTimer _timer;

        // Private properties
        private int Counter { get; set; }
        private bool InVehicle { get { return Lua.GetReturnVal<int>("if IsPossessBarVisible() or UnitInVehicle('player') then return 1 else return 0 end", 0) == 1; } }
        private LocalPlayer Me { get { return (ObjectManager.Me); } }

        private readonly List<ulong> _npcBlacklist = new List<ulong>();

        private List<WoWUnit> NpcList
        {
            get
            {
                if (VehicleList.Count > 0)
                {
                    return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                            .Where(u => TargetIds.Contains((int)u.Entry)
                                                        && (VehicleList[0].Location.Distance(u.Location) <= MaxRange) && !u.Dead && !_npcBlacklist.Contains(u.Guid))
                                            .OrderBy(u => u.Distance)
                                            .ToList());
                }
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                        .Where(u => TargetIds.Contains((int)u.Entry) && !u.Dead && !_npcBlacklist.Contains(u.Guid))
                                        .OrderBy(u => u.Distance)
                                        .ToList());
            }
        }
        private List<WoWUnit> NpcVehicleList
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                      .Where(ret => (ret.Entry == VehicleMountId) && !ret.Dead)
                                      .OrderBy(u => u.Distance)
                                      .ToList());
            }
        }
        private List<WoWUnit> VehicleList
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(ret => (ret.Entry == VehicleId) && !ret.Dead)
                                     .ToList());
            }
        }

        public WoWItem UseItem
        {
            get
            {
                return StyxWoW.Me.CarriedItems.FirstOrDefault(ret => ret.Entry == ItemId);
            }
        }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id: NoControlVehicle.cs 222 2012-03-23 20:44:33Z natfoth $"); } }
        public override string SubversionRevision { get { return ("$Revision: 222 $"); } }


        ~NoControlVehicle()
        {
            Dispose(false);
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


        double AimAngle
        {
            get
            {
                if (NpcList[0].Location.Distance(MountedPoint) < 10)
                { return 0; }

                else if (NpcList[0].Location.Distance(MountedPoint) >= 10 && NpcList[0].Location.Distance(MountedPoint) < 20)
                { return 0.2; }

                else if (NpcList[0].Location.Distance(MountedPoint) >= 20 && NpcList[0].Location.Distance(MountedPoint) < 30)
                { return 0.4; }

                else if (NpcList[0].Location.Distance(MountedPoint) >= 40 && NpcList[0].Location.Distance(MountedPoint) < 60)
                { return 0.5; }

                else
                { return 0.2; }
            }
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ??
                (_root = new PrioritySelector(
                    new Decorator(c => Counter > NumOfTimes,
                        new Action(c =>
                        {
                            TreeRoot.StatusText = "Finished!";
                            if (GoHomeButton > 0)
                            {
                                Lua.DoString("CastPetAction({0})", GoHomeButton);
                            }
                            _isBehaviorDone = true;
                        })
                    ),

                    new Decorator(ret => NpcVehicleList.Count > 0 && !InVehicle,
                            new PrioritySelector(
                                new Decorator(ret => !NpcVehicleList[0].WithinInteractRange,
                                    new Sequence(
                                        new Action(ret => Navigator.MoveTo(NpcVehicleList[0].Location)),
                                        new Action(ret => TreeRoot.StatusText = "Moving To Vehicle - " + NpcVehicleList[0].Name + " Yards Away: " + NpcVehicleList[0].Location.Distance(Me.Location))
                                        )),
                                new Decorator(ret => NpcVehicleList[0].WithinInteractRange,
                                    new Sequence(
                                        new Action(ret => NpcVehicleList[0].Interact()),
                                        new Action(ret => MountedPoint = Me.Location)
                                       // new Action(c => RunStatus.Success)
                                        )))

                   ),

                   new Decorator(ret => InVehicle && SpellType == 1,
                            new PrioritySelector(
                                new Decorator(ret => NpcList.Count == 0 || NpcList[0].Location.Distance(VehicleList[0].Location) > 15,
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Waiting for Mob to Come Into Range or Appear.")
                                        //new Action(c => RunStatus.Running)
                                            )),
                                new Decorator(ret => NpcList.Count >= 1 && NpcList[0].Location.Distance(VehicleList[0].Location) <= 15,
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Attacking: " + NpcList[0].Name + ", AttackButton: " + AttackButton),
                                        new Action(ret => NpcList[0].Target()),
                                        new Action(ret => Lua.DoString("CastPetAction({0})", AttackButton)),
                                        new Action(ret => Counter++)
                                       // new Action(ret => RunStatus.Success)
                                        )))

                   ),

                   new Decorator(ret => InVehicle && SpellType == 2 && _timer.IsFinished,
                            new PrioritySelector(
                                new Decorator(ret => (Counter > NumOfTimes && QuestId == 0) || (Me.QuestLog.GetQuestById((uint)QuestId) != null && Me.QuestLog.GetQuestById((uint)QuestId).IsCompleted && QuestId > 0),
                                    new Sequence(
                                        new Action(ret => Lua.DoString("VehicleExit()")),
                                        new Action(ret => _isBehaviorDone = true)
                                        //new Action(c => RunStatus.Running)
                                            )),
                                new Decorator(ret => NpcList.Count >= 1,
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Attacking: " + NpcList[0].Name + ", AttackButton: " + AttackButton + ", Times Used: " + Counter),
                                        new Action(ret => NpcList[0].Target()),
                                        new Action(ret => Lua.DoString("CastPetAction({0})", AttackButton)),
                                        new Action(ret => LegacySpellManager.ClickRemoteLocation(NpcList[0].Location)),
                                        new Action(ret => _timer.Reset()),
                                        new Action(ret => Counter++)//,
                                        //new Action(ret => RunStatus.Running)
                                        ))//,
                               /* new Decorator(ret => NpcList.Count == 0,
                                    new Sequence(
                                        new Action(ret => RunStatus.Running)
                                        ))*/

                   )),

                   new Decorator(c => InVehicle && SpellType == 3,
                       new PrioritySelector(
                           ret => NpcList.OrderBy(u => u.DistanceSqr).FirstOrDefault(u => Me.Transport.IsSafelyFacing(u)),
                            new Decorator(
                                ret => ret != null,
                                new PrioritySelector(
                                    new Decorator(
                                        ret => Me.CurrentTarget == null || Me.CurrentTarget != (WoWUnit)ret,
                                        new Action(ret => ((WoWUnit)ret).Target())),
                                    new Decorator(
                                        ret => !Me.Transport.IsSafelyFacing(((WoWUnit)ret), 10),
                                        new Action(ret => Me.CurrentTarget.Face())),
                                    new Action(ret =>
                                        {
                                            Vector3 v = Me.CurrentTarget.Location - StyxWoW.Me.Location;
                                            v.Normalize();
                                            Lua.DoString(string.Format(
                                                "local pitch = {0}; local delta = pitch - VehicleAimGetAngle(); VehicleAimIncrement(delta); CastPetAction({1});",
                                                Math.Asin(v.Z).ToString(CultureInfo.InvariantCulture), AttackButton));

                                            Thread.Sleep(WaitTime);
                                            Counter++;
                                            //return RunStatus.Success;
                                        }))))),

                    new Decorator(ret => InVehicle && SpellType == 4 && _timer.IsFinished,
                            new PrioritySelector(
                                new Decorator(ret => (Counter > NumOfTimes && QuestId == 0) || (Me.QuestLog.GetQuestById((uint)QuestId) != null && Me.QuestLog.GetQuestById((uint)QuestId).IsCompleted && QuestId > 0),
                                    new Sequence(
                                        new Action(ret => Lua.DoString("VehicleExit()")),
                                        new Action(ret => _isBehaviorDone = true)
                                        //new Action(c => RunStatus.Running)
                                            )),
                                new Decorator(ret => NpcList.Count >= 1,
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Attacking: " + NpcList[0].Name + ", AttackButton: " + AttackButton + ", Times Used: " + Counter),
                                        new Action(ret => NpcList[0].Target()),
                                        new Action(ret => WoWMovement.ClickToMove(NpcList[0].Location)),
                                        new Action(ret => Lua.DoString("CastPetAction({0})", AttackButton)),
                                        new Action(ret => LegacySpellManager.ClickRemoteLocation(NpcList[0].Location)),
                                        new Action(ret => _timer.Reset()),
                                        new Action(ret => Counter++)
                                        //new Action(ret => RunStatus.Running)
                                        ))//,
                                //new Decorator(ret => NpcList.Count == 0,
                                   // new Sequence(
                                        //new Action(ret => RunStatus.Running)
                                      //  ))

                   )),

                   new Decorator(ret => InVehicle && SpellType == 5 && _timer.IsFinished,
                            new PrioritySelector(
                                new Decorator(ret => (Counter > NumOfTimes && QuestId == 0) || (Me.QuestLog.GetQuestById((uint)QuestId) != null && Me.QuestLog.GetQuestById((uint)QuestId).IsCompleted && QuestId > 0),
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Finished Quest or Counter, Exiting Vehicle"),
                                        new Action(ret => Lua.DoString("VehicleExit()")),
                                        new Action(ret => _isBehaviorDone = true)
                                        //new Action(c => RunStatus.Running)
                                            )),
                                new Decorator(ret => NpcList.Count >= 1,
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Attacking: " + NpcList[0].Name + ", AttackButton: " + AttackButton + ", Times Used: " + Counter),
                                        new Action(ret => Lua.DoString("CastPetAction({0})", AttackButton2)),
                                        new Action(ret => NpcList[0].Target()),
                                        new Action(ret => WoWMovement.ConstantFace(Me.CurrentTargetGuid)),
                                        new Action(ret => Lua.DoString("CastPetAction({0})", AttackButton)),
                                        new Action(ret => _timer.Reset()),
                                        new Action(ret =>
                                                       {
                                                           if(QuestId == 0)
                                                                Counter++;
                                                       })
                                        //new Action(ret => RunStatus.Running)
                                        )),
                                new Decorator(ret => NpcList.Count == 0,
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Waiting for Mobs to Appear")
                                        //new Action(ret => RunStatus.Success)
                                        ))

                   )),

                   new Decorator(ret => InVehicle && SpellType == 6 && _timer.IsFinished,
                            new PrioritySelector(
                                new Decorator(ret => (Counter > NumOfTimes && QuestId == 0) || (Me.QuestLog.GetQuestById((uint)QuestId) != null && Me.QuestLog.GetQuestById((uint)QuestId).IsCompleted && QuestId > 0),
                                    new Sequence(
                                        new Action(ret => Lua.DoString("VehicleExit()")),
                                        new Action(ret => _isBehaviorDone = true)
                //new Action(c => RunStatus.Running)
                                            )),
                                new Decorator(ret => NpcList.Count >= 1 && NpcList[0].DistanceSqr <= Range * Range && UseItem != null && UseItem.Cooldown == 0,
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Attacking: " + NpcList[0].Name + ", AttackButton: " + AttackButton + ", Times Used: " + Counter),
                                        new Action(ret => UseItem.UseContainerItem()),
                                        new Action(ret => NpcList[0].Target()),
                                        new Action(ret => _npcBlacklist.Add(NpcList[0].Guid)),
                                        new Action(ret => LegacySpellManager.ClickRemoteLocation(NpcList[0].Location)),
                                        new Action(ret => _timer.Reset()),
                                        new Action(ret =>
                                        {
                                            if (QuestId == 0)
                                                Counter++;
                                        })
                //new Action(ret => RunStatus.Running)
                                        )),
                                new Decorator(ret => NpcList.Count == 0,
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Waiting for Mobs to Appear")
                //new Action(ret => RunStatus.Success)
                                        ))

                   ))

                ));
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
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = this.GetType().Name + ": " + ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");

                _timer = new Helpers.WaitTimer(new TimeSpan(0, 0, 0, 0, WaitTime));

                _timer.Reset();
            }

            if (TreeRoot.Current != null && TreeRoot.Current.Root != null && TreeRoot.Current.Root.LastStatus != RunStatus.Running)
            {
                var currentRoot = TreeRoot.Current.Root;
                if (currentRoot is GroupComposite)
                {
                    var root = (GroupComposite)currentRoot;
                    root.InsertChild(0, CreateBehavior());
                }
            }
        }

        #endregion
    }
}
