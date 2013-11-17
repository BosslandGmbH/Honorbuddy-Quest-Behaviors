// Behavior originally contributed by Raphus.
//
// DOCUMENTATION:
//     
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Styx.Plugins;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.DBC;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.UseHearthstone
{
    [CustomBehaviorFileName(@"UseHearthstone")]
    public class UseHearthstone : CustomForcedBehavior
    {
        /// <summary>
        /// Allows you to use Transports.
        /// ##Syntax##
        /// TransportId: ID of the transport.
        /// TransportStart: Start point of the transport that we will get on when its close enough to that point.
        /// TransportEnd: End point of the transport that we will get off when its close enough to that point.
        /// WaitAt: Where you wish to wait the transport at
        /// GetOff: Where you wish to end up at when transport reaches TransportEnd point
        /// StandOn: The point you wish the stand while you are in the transport
        /// </summary>
        ///
        public UseHearthstone(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.


                WaitOnCd = GetAttributeAsNullable<bool>("WaitForCD", false, null, null) ?? false;
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
        public bool WaitOnCd { get; private set; }


        // Private variables for internal state
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private Composite _root;

        // Private properties
        private LocalPlayer Me { get { return (StyxWoW.Me); } }

        // DON'T EDIT THESE--they are auto-populated by Subversion


        ~UseHearthstone()
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

                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }


        public void BotEvents_OnBotStop(EventArgs args)
        {
            Dispose();
        }

//thanks to dungonebuddy
        private uint CheckId(uint uint_13)
        {
            AreaTable table = AreaTable.FromId(uint_13);
            while (table.ParentAreaId != 0)
            {
                table = AreaTable.FromId(table.ParentAreaId);
            }
            return table.AreaId;
        }



        private bool IsInHearthStoneArea
        {
            get
            {
                uint hearthstoneAreaId = StyxWoW.Me.HearthstoneAreaId;
                uint zoneId = Me.ZoneId;
                if (hearthstoneAreaId == 0)
                {
                    return false;
                }
                if (CheckId(hearthstoneAreaId) != CheckId(zoneId))
                {
                    Logging.WriteDiagnostic("Zone: {0}, hearthAreaId: {1}", new object[] { zoneId, hearthstoneAreaId });
                }
                return (CheckId(hearthstoneAreaId) == CheckId(zoneId));
            }
        }



        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = new PrioritySelector(DoneYet,UseHearthstoneComposite, new ActionAlwaysSucceed()));
        }


        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        public override bool IsDone
        {
            get { return (_isBehaviorDone); }
        }


        public WoWItem Hearthstone
        {
            get
            {
                return Me.BagItems.FirstOrDefault(r => r.Entry == 6948);
            }
        }

        public Composite UseHearthstoneComposite
        {
            get
            {
                return new PrioritySelector(
                    new Decorator(r => Hearthstone.CooldownTimeLeft.TotalSeconds > 0,new ActionAlwaysSucceed()),
                    new Decorator(r => Hearthstone.CooldownTimeLeft.TotalSeconds <= 0,new Action(r=>Hearthstone.Use()))
                    );
            }
        }

        public Composite DoneYet
        {
            get
            {
                return
                    new Decorator(ret => (IsInHearthStoneArea || Hearthstone == null) || (Hearthstone.CooldownTimeLeft.TotalSeconds > 0 && !WaitOnCd), new Action(delegate
                    {
                        TreeRoot.StatusText = "Finished!";
                        _isBehaviorDone = true;
                        return RunStatus.Success;
                    }));

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
                TreeRoot.GoalText = "Using hearthstone";
            }
        }


        #endregion
    }
}

