// Behavior originally contributed by Natfoth.
//
// WIKI DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_FireFromTheSky
//
// QUICK DOX:
//      Used for the Dwarf Quest SI7: Fire From The Sky
//
//  Notes:
//      * Make sure to Save Gizmo.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CommonBehaviors.Actions;
using CommonBehaviors.Decorators;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.CleaningHouse
{
    [CustomBehaviorFileName(@"SpecificQuests\30078-FourWinds-CleaningHouse")]
    public class FourWindsCleaningHouse : CustomForcedBehavior
    {
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private Composite _root;

        public FourWindsCleaningHouse(Dictionary<string, string> args) : base(args)
        {
            try
            {
                QuestId = GetAttributeAsNullable("QuestId", false, ConstrainAs.QuestId(this), null) ?? 30078;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ??
                                           QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message + "\nFROM HERE:\n" + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }


        // Attributes provided by caller
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }

        // Private variables for internal state

        // Private properties
        private LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
        }


        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId
        {
            get { return ("$Id: 30078-FourWinds-CleaningHouse.cs 501 2013-05-10 16:29:10Z chinajade $"); }
        }

        public override string SubversionRevision
        {
            get { return ("$Revision: 501 $"); }
        }


        ~FourWindsCleaningHouse()
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
                    TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_MainCombat());
                    Targeting.Instance.IncludeTargetsFilter -= Instance_IncludeTargetsFilter;
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

        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone // normal completion
                        || !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
            }
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
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
                TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_MainCombat());
                Targeting.Instance.IncludeTargetsFilter += Instance_IncludeTargetsFilter;
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint) QuestId);

                TreeRoot.GoalText = GetType().Name + ": " + ((quest != null) ? quest.Name : "In Progress");
            }
        }

        private void Instance_IncludeTargetsFilter(List<WoWObject> incomingUnits, HashSet<WoWObject> outgoingUnits)
        {
            foreach (var unit in incomingUnits.OfType<WoWUnit>())
            {
                if ((unit.Entry == EddyId || unit.Entry == JoogaId || unit.Entry == FizzyYellowSpiritId || unit.Entry == FizzyYellowAlementalId) &&
                    !Blacklist.Contains(unit, BlacklistFlags.Combat))
                    outgoingUnits.Add(unit);
            }
        }

        #endregion

        #region CreateBehavior_MainCombat

        private const int ChenOutsideID = 56133;

        private Composite OutdoorsBehavior
        {
            get
            {
                var chenOutsideLoc = new WoWPoint(-709.4158, 1266.86, 136.0237);
                WoWUnit chenOutside = null;
                // moveto and talk to Chen when outside the dungeon. 
                return new Decorator(
                    ctx => Me.IsOutdoors && Targeting.Instance.FirstUnit == null,
                    new PrioritySelector(
                        ctx => chenOutside = ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(u => u.Entry == ChenOutsideID),
                        new Decorator(ctx => chenOutside == null, new Action(ctx => Navigator.MoveTo(chenOutsideLoc))),
                        new Decorator(
                            ctx => chenOutside != null,
                            new PrioritySelector(
                                new Decorator(ctx => !chenOutside.WithinInteractRange, new Action(ctx => Navigator.MoveTo(chenOutside.Location))),
                                new Decorator(
                                    ctx => !GossipFrame.Instance.IsVisible,
                                    new Sequence(
                                        new Action(ctx => chenOutside.Interact()),
                                        new WaitContinue(2, ctx => GossipFrame.Instance.IsVisible, new ActionAlwaysSucceed()))),
                                new Decorator(ctx => GossipFrame.Instance.IsVisible, new Action(ctx => GossipFrame.Instance.SelectGossipOption(0)))))));
            }
        }

        private Composite IndoorsBehavior
        {
            get
            {
                return new Decorator(
                    ctx => !Me.IsOutdoors,
                    new PrioritySelector(
                        // have to call CR's pull because quest bot will otherwise ignore Targeting.Instance.FirstUnit or POI.Kill 
                        new Decorator(
                            ctx => !Me.Combat && Query.IsViableForFighting(Targeting.Instance.FirstUnit),
                            new PrioritySelector(RoutineManager.Current.RestBehavior, RoutineManager.Current.PullBehavior)),
                        EddyBehavior,
                        JoogaBehavior,
                        FizzyYellowAlementalBehavior));
            }
        }

        #region Eddy

        private const int EddyId = 58014;
        private const int TinyBubbleId = 210231;

        private Composite EddyBehavior
        {
            get
            {
                WoWUnit eddy = null;
                WoWPoint tinyBubbleMoveTo = WoWPoint.Zero;
                const int wateryShieldPopSpellId = 110198;
                const int wateryShieldSpellId = 110189;

                return new PrioritySelector(
                    ctx => eddy = ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(u => u.Entry == EddyId),
                    new Decorator(
                        ctx => eddy != null,
                        new PrioritySelector(
                            ctx => tinyBubbleMoveTo = GetTinyBubbleMoveTo(),
                            // pop some bubbles until the watery shield can be removed from Eddy
                            new Decorator(ctx => eddy.HasAura(wateryShieldSpellId) && tinyBubbleMoveTo != WoWPoint.Zero, new Action(ctx => Navigator.MoveTo(tinyBubbleMoveTo))),
                            // pop the watery shield on eddy.
                            new Decorator(
                                ctx => eddy.HasAura(wateryShieldPopSpellId),
                                new PrioritySelector(
                                    new Decorator(ctx => !eddy.WithinInteractRange, new Action(ctx => Navigator.MoveTo(eddy.Location))),
                                    new Decorator(ctx => Me.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
                                    new Action(ctx => eddy.Interact()))))));
            }
        }

        private WoWPoint GetTinyBubbleMoveTo()
        {
            var myLoc = Me.Location;
            return (from bubble in ObjectManager.ObjectList.Where(o => o.Entry == TinyBubbleId && !Blacklist.Contains(o, BlacklistFlags.Interact))
                let loc = bubble.Location
                let distanceSqurared = loc.DistanceSqr(myLoc)
                where IsElegibleBubble(bubble, distanceSqurared)
                orderby distanceSqurared
                select loc).FirstOrDefault();
        }

        private bool IsElegibleBubble(WoWObject bubble, float bubbleDistanceSquared)
        {
            if (bubbleDistanceSquared > 4*4)
                return true;
            Blacklist.Add(bubble, BlacklistFlags.Interact, TimeSpan.FromSeconds(5));
            return false;
        }

        #endregion

        #region Jooga

        private const uint JoogaId = 58015;
        private const uint EmptyKegId = 210230;

        private Composite JoogaBehavior
        {
            get
            {
                WoWUnit jooga = null;
                WoWGameObject emptyKeg = null;

                return new PrioritySelector(
                    ctx => jooga = ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(u => u.Entry == JoogaId),
                    new Decorator(
                        ctx => jooga != null,
                        new PrioritySelector(
                            // throw a keg on Jooga's head to make him stop chasing the panda cub.
                            new Decorator(
                                ctx => Me.Combat && jooga.HasAura("Slippery"),
                                new PrioritySelector(
                                    ctx =>
                                        emptyKeg =
                                            ObjectManager.GetObjectsOfTypeFast<WoWGameObject>().Where(g => g.Entry == EmptyKegId).OrderBy(g => g.DistanceSqr).FirstOrDefault(),
                                    new Decorator(
                                        ctx => emptyKeg != null,
                                        new PrioritySelector(
                                            new Decorator(ctx => !emptyKeg.WithinInteractRange, new Action(ctx => Navigator.MoveTo(emptyKeg.Location))),
                                            new Decorator(ctx => Me.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
                                            new Action(ctx => emptyKeg.Interact()))))))));
            }
        }

        #endregion

        #region Fizzy Yellow Alemental

        private const uint FizzyYellowAlementalId = 58017;
        private const uint FizzyYellowSpiritId = 58253;
        // these are poisonous fumes that move around and are hard to see but do very small damage. will just ignore them.
        private const uint BurpId = 58304;

        private Composite FizzyYellowAlementalBehavior
        {
            get
            {
                WoWUnit fizzyYellowAlemental = null;
                // Tank and spank!
                return new PrioritySelector(
                    
                    
                    
                    // ctx => fizzyYellowAlemental = ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(u => u.Entry == FizzyYellowAlementalId),
                    // new Decorator(ctx => fizzyYellowAlemental != null, new PrioritySelector())
                    );
            }
        }

        #endregion

        private Composite CreateBehavior_MainCombat()
        {
            return _root ?? (_root = new PrioritySelector(OutdoorsBehavior, IndoorsBehavior));
        }

        #endregion
    }
}