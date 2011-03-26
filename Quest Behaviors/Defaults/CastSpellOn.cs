using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.Logic.Combat;
using Styx.Logic.BehaviorTree;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class CastSpellOn : CustomForcedBehavior
    {
        /// <summary>
        /// CastSpellOn by Natfoth
        /// Allows you to use a Specific Spell on a Target, useful for Dummies and Starting Quests.
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// SpellId: Spell you wish to cast on the Target
        /// NumOfTimes: How many times before the script finishes
        /// HpLeftAmount: How low the HP should be before casting a spell on it. Such as wounded targets
        /// MinRange: If the spell has a minRange to it
        /// Range: Range to cast spell at
        /// X,Y,Z: The general location where these objects can be found
        /// </summary>
        /// 
        public CastSpellOn(Dictionary<string, string> args)
            : base(args)
        {
			try
			{
                int spellId;
                int mobId;
                int numberOfTimes;
                int hpLeftAmount;
                int minRange;
                int range;
                int questId;
                WoWPoint location;

                CheckForUnrecognizedAttributes(new Dictionary<string, object>()
                                                {
                                                    { "HpLeftAmount",   null },
                                                    { "MinRange",       null },
                                                    { "Range",          null },
                                                    { "MobId",          null },
                                                    { "NpcId",          null },
                                                    { "NumOfTimes",     null },
                                                    { "QuestId",        null },
                                                    { "SpellId",        null },
                                                    { "X",              null },
                                                    { "Y",              null },
                                                    { "Z",              null },
                                                });

                _isAttributesOkay = true;
                _isAttributesOkay &= GetAttributeAsInteger("HpLeftAmount", false, "110", 0, int.MaxValue, out hpLeftAmount); ;
                _isAttributesOkay &= GetAttributeAsInteger("MinRange", false, "3", 0, int.MaxValue, out minRange);
                _isAttributesOkay &= GetAttributeAsInteger("Range", false, "25", 0, int.MaxValue, out range);
                _isAttributesOkay &= GetAttributeAsInteger("NumOfTimes", false, "1", 1, int.MaxValue, out numberOfTimes);
                _isAttributesOkay &= GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
                _isAttributesOkay &= GetAttributeAsInteger("SpellId", true, "0", 0, int.MaxValue, out spellId);
                _isAttributesOkay &= GetXYZAttributeAsWoWPoint(true, WoWPoint.Empty, out location);

                // "NpcId" is allowed for legacy purposes --
                // If it was not supplied, then its new name "MobId" is required.
                _isAttributesOkay &= GetAttributeAsInteger("NpcId", false, "0", 0, int.MaxValue, out mobId);
                if (mobId == 0)
                    { _isAttributesOkay &= GetAttributeAsInteger("MobId", true, "0", 0, int.MaxValue, out mobId); }


                // Weed out Profile Writer sloppiness --
                if (_isAttributesOkay)
                {
                    if (mobId == 0)
                    {
                        UtilLogMessage("error", "MobId may not be zero");
                        _isAttributesOkay = false;
                    }

                    if (spellId == 0)
                    {
                        UtilLogMessage("error", "SpellId may not be zero");
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
                    Counter = 1;
                    HPLeftAmount = hpLeftAmount;
                    Location = location;
                    MinRange = minRange;
                    Range = range;
                    MobId = mobId;
                    MovedToTarget = false;
                    NumberOfTimes = numberOfTimes;
                    QuestId = (uint)questId;
                    SpellID = spellId;
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
        public int      HPLeftAmount { get; set; }
        public WoWPoint Location { get; private set; }
        public int      MinRange { get; set; }
        public int      Range { get; set; }
        public int      MobId { get; set; }
        public bool     MovedToTarget { get; set; }
        public int      NumberOfTimes { get; set; }
        public uint     QuestId { get; set; }
        public int      SpellID { get; set; }

        private bool        _isAttributesOkay;
        private bool        _isBehaviorDone;
        private Composite   _root;

        public static LocalPlayer s_me = ObjectManager.Me;

        public List<WoWUnit> mobList
        {
            get
            {
                if (HPLeftAmount > 0)
                {
                    return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                         .Where(u => u.Entry == MobId && !u.Dead && u.HealthPercent <= HPLeftAmount)
                                         .OrderBy(u => u.Distance).ToList());
                }
                else
                {
                    return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                         .Where(u => u.Entry == MobId && !u.Dead)
                                         .OrderBy(u => u.Distance).ToList());
                }
            }
        }


        #region Overrides of CustomForcedBehavior

        private Composite CreateRootBehavior()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => !IsDone && StyxWoW.Me.IsAlive,
                    new PrioritySelector(
                        new Decorator(ret => Counter > NumberOfTimes && QuestId == 0,
                                        new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Finished!"),
                                            new WaitContinue(120,
                                                new Action(delegate
                                                {
                                                    _isBehaviorDone = true;
                                                    return RunStatus.Success;
                                                }))
                                            )),

                        new Decorator(ret => mobList.Count > 0 && !s_me.IsCasting && SpellManager.CanCast(SpellID),
                            new Sequence(
                                new DecoratorContinue(ret => mobList[0].Location.Distance(s_me.Location) >= MinRange && mobList[0].Location.Distance(s_me.Location) <= 25 && mobList[0].InLineOfSightOCD,
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Casting Spell - " + SpellID + " On Mob: " + mobList[0].Name + " Yards Away " + mobList[0].Location.Distance(s_me.Location)),
                                        new Action(ret => WoWMovement.MoveStop()),
                                        new Action(ret => Thread.Sleep(300)),
                                        new Decorator(c => !s_me.IsCasting, CreateSpellBehavior)
                                        )
                                ),
                                new DecoratorContinue(ret => mobList[0].Location.Distance(s_me.Location) > Range || !mobList[0].InLineOfSightOCD,
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Moving To Mob - " + mobList[0].Name + " Yards Away: " + mobList[0].Location.Distance(s_me.Location)),
                                        new Action(ret => Navigator.MoveTo(mobList[0].Location))
                                        )
                                ),

                                new DecoratorContinue(ret => mobList[0].Location.Distance(s_me.Location) < MinRange,
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Too Close, Backing Up"),
                                        new Action(ret => mobList[0].Face()),
                                        new Action(ret => Thread.Sleep(100)),
                                        new Action(ret => WoWMovement.Move(WoWMovement.MovementDirection.Backwards)),
                                        new Action(ret => Thread.Sleep(100))
                                        ))
                                ))
                )));
        }

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(
                    
                        new Decorator(ret => mobList.Count == 0,
                            new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Moving To Location - X: " + Location.X + " Y: " + Location.Y),
                                    new Action(ret => Navigator.MoveTo(Location)),
                                    new Action(ret => Thread.Sleep(300))
                                )
                            )
                            
                        )
                    );
        }


        Composite CreateSpellBehavior
        {
            get
            {
                return new Action(c =>
                {
                    if (SpellID > 0)
                    {
                        mobList[0].Target();
                        mobList[0].Face();
                        Thread.Sleep(300);
                        SpellManager.Cast(SpellID);

                        if (s_me.QuestLog.GetQuestById(QuestId) == null || QuestId == 0)
                        {
                            Counter++;
                        }
                        Thread.Sleep(300);
                        return RunStatus.Success;
                    }
                    else
                    {
                        _isBehaviorDone = true;
                        return RunStatus.Success;
                    }
                });
            }
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

            else if (!IsDone)
            {
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

                if (quest != null)
                    {  TreeRoot.GoalText = string.Format("{0} for \"{1}\"", this.GetType().Name, quest.Name); }
                else
                    { TreeRoot.GoalText = string.Format("{0}: Running", this.GetType().Name); }

                if (TreeRoot.Current != null && TreeRoot.Current.Root != null && TreeRoot.Current.Root.LastStatus != RunStatus.Running)
                {
                    var currentRoot = TreeRoot.Current.Root;
                    if (currentRoot is GroupComposite)
                    {
                        var root = (GroupComposite)currentRoot;
                        root.InsertChild(0, CreateRootBehavior());
                    }
                }
            }
        }

        #endregion
    }
}
