using System;
using System.Collections.Generic;
using System.Threading;

using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.Logic.BehaviorTree ;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class GoThruPortal : CustomForcedBehavior
    {
        /// <summary>
        /// GoThurPortal by Bobby53
        /// 
        /// Supports walk through portals in a way that does not result
        /// in red error messages in WoW or in the HB log/debug files.
        /// 
        /// xyz should be a position as close as possible to portal entrance 
        /// without entering.
        /// 
        /// ##Syntax##
        /// [Optional] QuestId: The id of the quest (0 is default)
        /// [Optional] QuestName: The name of the quest.
        /// [Optional] Timeout: time in milliseconds it allows for completing (10000 is default)
        /// X,Y,Z: used with current location to create a vector it moves along
        /// 
        /// ##Example##
        /// use RunTo to get start position, then GoThruPortal to run throuhg xyz vector
        /// on way through portal.
        /// 
        ///     <RunTo X="4646.201" Y="-3685.043" Z="954.2496" />
        ///     <CustomBehavior File="GoThruPortal" X="4656.928" Y="-3685.472" Z="957.185" />
        /// 
        /// </summary>
       public GoThruPortal(Dictionary<string, string> args)
            : base(args)
        {
			try
			{
                WoWPoint    location;
                int         questId;
                int         timeOut;


                CheckForUnrecognizedAttributes(new Dictionary<string, object>()
                                                {
                                                    { "QuestId",    null },
                                                    { "QuestName",  null },
                                                    { "Timeout",    null },
                                                    { "X",          null },
                                                    { "Y",          null },
                                                    { "Z",          null },
                                                });


                _isAttributesOkay = true;
                _isAttributesOkay &= GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
                _isAttributesOkay &= GetAttributeAsInteger("Timeout", false, "10000", 1, 60000, out timeOut);
                _isAttributesOkay &= GetXYZAttributeAsWoWPoint(true, WoWPoint.Empty, out location);

                if (_isAttributesOkay)
                {
                    _zoneText = StyxWoW.Me.ZoneText;
                    MovePoint = WoWMovement.CalculatePointFrom(location, -15);
                    QuestId = (uint)questId;
                    Timeout =  System.Environment.TickCount + timeOut;
                }
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
				_isAttributesOkay = false;
			}
        }


        public int      Counter { get; set; }
        public WoWPoint MovePoint { get; private set; }
        public uint     QuestId { get; set; }
        public int      Timeout { get; set; }

        private bool        _isAttributesOkay;
        private bool        _isBehaviorDone;
        private bool        _isInPortal = false;
        private Composite   _root;
        private string      _zoneText;

        public static LocalPlayer s_me = ObjectManager.Me;


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                    // first state catches if we are done just in case
                    new Decorator(ret => _isBehaviorDone,
                        new Action(delegate
                        {
                            return RunStatus.Success;
                        })),

                    // if we hit the load screen and we are back in game
                    new Decorator(ret => _isInPortal && ObjectManager.IsInGame && StyxWoW.Me != null,
                        new Action(delegate
                        {
                            _isBehaviorDone = true;
                            UtilLogMessage("info", "Went thru portal.");
                            Thread.Sleep(500);
                            WoWMovement.MoveStop();
                            Thread.Sleep(500);
                            return RunStatus.Success;
                        })),

                    // if zone name changed
                    new Decorator(ret => _zoneText != StyxWoW.Me.ZoneText,
                        new Action(ret => _isInPortal = true)),

                    // if load screen is visible
                    new Decorator(ret => !ObjectManager.IsInGame || StyxWoW.Me == null,
                        new Action(ret => _isInPortal = true)),

                    // if we are within 2 yards of calculated end point we should never reach
                    new Decorator(ret => MovePoint.Distance(s_me.Location) < 2,
                        new Action(delegate
                        {
                            _isBehaviorDone = true;
                            WoWMovement.MoveStop();
                            UtilLogMessage("error", "GoThruPortal: ERROR reached end point. Failed to go through portal.\n"
                                                    + "Stopping Honorbuddy.");
                            TreeRoot.Stop();
                            return RunStatus.Success;
                        })),

                    new Decorator(ret => Timeout != 0 && Timeout < System.Environment.TickCount,
                        new Action(delegate
                        {
                            _isBehaviorDone = true;
                            WoWMovement.MoveStop();
                            UtilLogMessage("error", string.Format("ERROR timed out after {0} ms. Failed to go through portal\n"
                                                                  + "Stopping Honorbuddy.",
                                                                  Timeout));
                            TreeRoot.Stop();
                            return RunStatus.Success;
                        })),

                    new Decorator(ret => !StyxWoW.Me.IsMoving,
                        new Action(delegate
                        {
                            UtilLogMessage("info", string.Format("Moving to {0}", MovePoint));
                            WoWMovement.ClickToMove(MovePoint);
                            return RunStatus.Success;
                        }))
                    )
                );

        }


        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone    // normal completion
                        ||  !UtilIsProgressRequirementsMet((int)QuestId, 
                                                           QuestInLogRequirement.InLog, 
                                                           QuestCompleteRequirement.NotComplete));
            }
        }


        public override void OnStart()
		{
			if (!_isAttributesOkay)
			{
				UtilLogMessage("error", "Stopping Honorbuddy.  Please repair the profile!");

                // *Never* want to stop Honorbuddy (e.g., TreeRoot.Stop()) in the constructor --
                // This would defeat the "ProfileDebuggingMode" configurable that builds an instance of each
                // used behavior when the profile is loaded.
				TreeRoot.Stop();
			}
		}

   		#endregion
    }
}

