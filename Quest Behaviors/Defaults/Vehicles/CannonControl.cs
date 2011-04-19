using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using System.Diagnostics;

using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// CannonControl by HighVoltz
    /// Shoots a Cannon
    /// ##Syntax##
    /// VehicleId: ID of the vehicle
    /// QuestId: Id of the quest to perform this behavior on
    /// MaxAngle: Maximum Angle to aim, use /dump VehicleAimGetNormAngle() in game to get the angle
    /// MinAngle: Minimum Angle to aim, use /dump VehicleAimGetNormAngle() in game to get the angle
    /// Buttons:A series of numbers that represent the buttons to press in order of importance, separated by comma, for example Buttons ="2,1" 
    /// ExitButton: (Optional)Button to press to exit the cannon. 1-12
    /// </summary>
    public class CannonControl : CustomForcedBehavior
    {
        public CannonControl(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                Buttons         = GetAttributeAsIntegerArray("Buttons", true, -1, 12, null) ?? new int[0];
                ExitButton      = GetAttributeAsHotbarButton("ExitButton", true, null) ?? 0; 
                MaxAngle        = GetAttributeAsDouble("MaxAngle", true, 0.0, 1.5, null) ?? 0;
                MinAngle        = GetAttributeAsDouble("MinAngle", true, 0.0, 1.5, null) ?? 0;
                QuestId         = GetAttributeAsQuestId("QuestId", true, null) ?? 0;
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;
                VehicleId       = GetAttributeAsMobId("VehicleId", true, null) ?? 0;

                ExitButton += 120;

                for (int i = 0;  i < Buttons.Length;  ++i)
                    { Buttons[i] += 120; }
			}

			catch (Exception except)
			{
				// Maintenance problems occur for a number of reasons.  The primary two are...
				// * Changes were made to the behavior, and boundary conditions weren't properly tested.
				// * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
				// In any case, we pinpoint the source of the problem area here, and hopefully it
				// can be quickly resolved.
				UtilLogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
										+ "\nFROM HERE:\n"
										+ except.StackTrace + "\n");
				IsAttributeProblem = true;
			}
        }


        public int[]                    Buttons { get; private set; }
        public int                      ExitButton { get; private set; }
        public double                   MaxAngle { get; private set; }
        public double                   MinAngle { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }
        public List<int>                SpellIds { get; private set; }
        public int                      VehicleId { get; private set; }

        private bool                _aimed;
        private bool                _isBehaviorDone;
        private Composite           _root;
        readonly Stopwatch          _thottleTimer = new Stopwatch();


        public WoWObject Vehicle
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWObject>(true).Where(o => o.Entry == VehicleId).
                    OrderBy(o => o.Distance).FirstOrDefault();
            }
        }

        private static bool IsInVehicle
        {
            get { return Lua.GetReturnVal<bool>("return UnitInVehicle('player')", 0); }
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = 
                new PrioritySelector(
                    new Decorator(c => Vehicle == null,
                            new Action(c => UtilLogMessage("fatal", "No cannons found."))
                        ),

                    new Decorator(c => Vehicle != null && !IsInVehicle,
                        new Action(c =>
                        {
                            if (!Vehicle.WithinInteractRange)
                            {
                                Navigator.MoveTo(Vehicle.Location);
                                UtilLogMessage("info", "Moving to Cannon");
                            }
                            else
                                Vehicle.Interact();
                        })
                    ),
                    new Decorator(c => IsInVehicle && Vehicle != null,
                        new Action(c =>
                        {
                            // looping since current versions of HB seem to be unresponsive for periods of time
                            while (true)
                            {
                                var quest = ObjectManager.Me.QuestLog.GetQuestById((uint)QuestId);
                                if (quest.IsCompleted)
                                {
                                    if (ExitButton > 0)
                                        Lua.DoString("local _,s,_ = GetActionInfo({0}) CastSpellByID(s)", ExitButton);
                                    else
                                        Lua.DoString("VehicleExit()");
                                    _isBehaviorDone = true;
                                    return RunStatus.Success;
                                }
                                
                                if (!_aimed)
                                {
                                    Lua.DoString("VehicleAimRequestNormAngle({0})", MinAngle);
                                    _aimed = true;
                                }
                                else
                                {
                                    Lua.DoString("VehicleAimRequestNormAngle({0})", MaxAngle);
                                    _aimed = false;
                                }
                                foreach (int s in Buttons)
                                {
                                    Lua.DoString("local _,s,_ = GetActionInfo({0}) CastSpellByID(s) ", s);
                                }
                                   
                                Thread.Sleep(100);
                                
                                _thottleTimer.Reset();
                                _thottleTimer.Start();
                            }
                        }))
                ));
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
            }
        }

        #endregion
    }
}
