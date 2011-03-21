using System;
using System.Collections.Generic;
using System.Linq;

using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.Logic.Combat;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class CombatUseItemOn : CustomForcedBehavior
    {
        /// <summary>
        /// CombatUseItemOn by raphus
        /// Allows you to use an Item after you gain an Aura.
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// ItemId: Id of the Item you wish to use once you have an aura.
        /// HasAura: Aura ID of the aura on you when to use the item
        /// NpcHasAura: Aura ID of the aura on npc when to use the item
        /// NpcHPLeft: Hp of the npc when to use the item
        /// CastingSpellId: Spell ID of the spell that npc is casting when to use the item
        /// NpcId: NpcID of the mob that will attack you.
        /// NumOfTimes: How times needed to use the item.
        /// X,Y,Z: The general location where these objects can be found
        /// </summary>
        /// 
        public CombatUseItemOn(Dictionary<string, string> args)
            : base(args)
        {

			try
			{
                int         castingSpellId;
                int         hasAura;
                int         itemId;
                WoWPoint    location;
                int         mobId;
                int         npcHasAura;
                int         npcHPLeft;
                int         numberoftimes;
                int         questId;

                CheckForUnrecognizedAttributes(new Dictionary<string, object>()
                                                {
                                                    { "ItemId",         null },
                                                    { "NpcId",          null },
                                                    { "MobId",          null },
                                                    { "NumOfTimes",     null },
                                                    { "X",              null },
                                                    { "Y",              null },
                                                    { "Z",              null },
                                                    { "HasAura",        null },
                                                    { "NpcHPLeft",      null },
                                                    { "NpcHasAura",     null },
                                                    { "CastingSpellId", null },
                                                    { "QuestId",        null },
                                                });


                _isAttributesOkay = true;
                _isAttributesOkay &= GetAttributeAsInteger("ItemId", true, "0", 0, int.MaxValue, out itemId);
                _isAttributesOkay &= GetAttributeAsInteger("HasAura", false, "0", 0, int.MaxValue, out hasAura);
                _isAttributesOkay &= GetAttributeAsInteger("NumOfTimes", false, "1", 0, int.MaxValue, out numberoftimes);
                _isAttributesOkay &= GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
                _isAttributesOkay &= GetAttributeAsInteger("NpcHPLeft", false, "0", 0, int.MaxValue, out npcHPLeft);
                _isAttributesOkay &= GetAttributeAsInteger("NpcHasAura", false, "0", 0, int.MaxValue, out npcHasAura);
                _isAttributesOkay &= GetAttributeAsInteger("CastingSpellId", false, "0", 0, int.MaxValue, out castingSpellId);
                _isAttributesOkay &= GetXYZAttributeAsWoWPoint( true, WoWPoint.Empty, out location);

                // "NpcId" is allowed for legacy purposes --
                // If it was not supplied, then its new name "MobId" is required.
                _isAttributesOkay &= GetAttributeAsInteger("NpcId", false, "0", 0, int.MaxValue, out mobId);
                if (mobId == 0)
                    { _isAttributesOkay &= GetAttributeAsInteger("MobId", true, "0", 0, int.MaxValue, out mobId); }


                // Weed out Profile Writer sloppiness --
                if (_isAttributesOkay)
                {
                    if (itemId == 0)
                    {
                        UtilLogMessage("error", "ItemId may not be zero");
                        _isAttributesOkay = false;
                    }

                    if (mobId == 0)
                    {
                        UtilLogMessage("error", "MobId may not be zero");
                        _isAttributesOkay = false;
                    }

                    if (location == WoWPoint.Empty)
                    {
                        UtilLogMessage("error", "X-Y-Z may not be zero");
                        _isAttributesOkay = false;
                    }
                }


                if (_isAttributesOkay)
                {
                    CastingSpellId = castingSpellId;
                    Counter = 0;
                    HasAura = hasAura;
                    ItemID = itemId;
                    Location = location;
                    MobId = mobId;
                    NpcHasAura = npcHasAura;
                    NpcHPLeft = npcHPLeft;
                    NumberOfTimes = numberoftimes;
                    QuestId = (uint)questId;
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

        public int                  CastingSpellId { get; set; }
        public int                  Counter { get; set; }
        public int                  HasAura { get; set; }
        public int                  ItemID { get; set; }
        public WoWPoint             Location { get; private set; }
        public static LocalPlayer   Me { get { return StyxWoW.Me; } }
        public int                  MobId { get; set; }
        public bool                 MovedToTarget { get; set; }
        public int                  NpcHasAura { get; set; }
        public int                  NpcHPLeft { get; set; }
        public int                  NumberOfTimes { get; set; }
        public uint                 QuestId { get; set; }

        private bool        _isAttributesOkay;
        private bool        _isBehaviorDone;
        private Composite   _root;
     

        public WoWUnit Mob
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => u.Entry == MobId && !u.Dead)
                                     .OrderBy(u => u.Distance).FirstOrDefault());
            }
        }


        public WoWItem Item
        {
            get
            {
                return Me.CarriedItems.FirstOrDefault(i => i.Entry == ItemID && i.Cooldown == 0);
            }
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(
                    new Decorator(
                        ret => !Me.Combat,
                            new PrioritySelector(
                                new Decorator(
                                    ret => Mob == null,
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Moving to location"),
                                        new Action(ret => Navigator.MoveTo(Location)))),
                                new Decorator(
                                    ret => Me.CurrentTarget == null,
                                    new Action(ret => Mob.Target())),
                                new Decorator(
                                    ret => RoutineManager.Current.PullBehavior != null,
                                    RoutineManager.Current.PullBehavior),
                                new Action(ret => RoutineManager.Current.Pull()))),
                    RootCompositeOverride()
                ));
        }

        private Composite RootCompositeOverride()
        {
            return
                new PrioritySelector(
                    new Decorator(
                        ret => !_isBehaviorDone && Me.IsAlive,
                        new PrioritySelector(
                            new Decorator(ret => (Counter >= NumberOfTimes) || (Me.QuestLog.GetQuestById(QuestId) != null && Me.QuestLog.GetQuestById(QuestId).IsCompleted),
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new WaitContinue(120,
                                        new Action(delegate
                                        {
                                            _isBehaviorDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )),

                            new Decorator(
                                ret => Me.CurrentTarget != null && Item != null && Me.Combat,
                                new PrioritySelector(
                                    new Decorator(
                                        ret => (CastingSpellId != 0 && Me.CurrentTarget.CastingSpellId == CastingSpellId) ||
                                               (NpcHasAura != 0 && Me.CurrentTarget.Auras.Values.Any(a => a.SpellId == NpcHasAura)) ||
                                               (NpcHPLeft != 0 && Me.CurrentTarget.HealthPercent <= NpcHPLeft) ||
                                               (HasAura != 0 && Me.Auras.Values.Any(a => a.SpellId == HasAura)),
                                        new PrioritySelector(
                                            new Decorator(
                                                ret => Me.CurrentTarget.Distance > 4,
                                                new Action(ret => Navigator.MoveTo(Me.CurrentTarget.Location))),
                                            new Sequence(
                                                new Action(ret => Navigator.PlayerMover.MoveStop()),
                                                new Action(ret => StyxWoW.SleepForLagDuration()),
                                                new Action(ret => TreeRoot.StatusText = "Using item"),
                                                new Action(ret => Item.UseContainerItem()),
                                                new DecoratorContinue(
                                                    ret => QuestId == 0,
                                                    new Action(ret => Counter++)))))))

                    )));
        }


        public override bool IsDone
        {
            get
            {
                bool    isDone = (_isBehaviorDone    // normal completion
                                  ||  !UtilIsProgressRequirementsMet((int)QuestId, 
                                                                     QuestInLogRequirement.InLog, 
                                                                     QuestCompleteRequirement.NotComplete));

                if (isDone)
                    { _isBehaviorDone = true; }

                return (isDone);
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

            else
            {
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

                if (quest != null)
                    { TreeRoot.GoalText = string.Format("{0} - \"{1}\"", this.GetType().Name, quest.Name); }
                else
                    { TreeRoot.GoalText = string.Format("{0} in progress", this.GetType().Name); }

                if (TreeRoot.Current != null && TreeRoot.Current.Root != null && TreeRoot.Current.Root.LastStatus != RunStatus.Running)
                {
                    var currentRoot = TreeRoot.Current.Root;
                    if (currentRoot is GroupComposite)
                    {
                        var root = (GroupComposite)currentRoot;
                        root.InsertChild(0, RootCompositeOverride());
                    }
                }
            }
        }

        #endregion
    }
}
