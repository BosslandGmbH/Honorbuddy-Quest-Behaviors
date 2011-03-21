using System;
using System.Collections.Generic;
using System.Threading;

using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.Logic.BehaviorTree;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class ItemWhileMoving : CustomForcedBehavior
    {
        /// <summary>
        /// ItemWhileMoving by Natfoth
        /// Will use the same item over and over until a location is reached.
        /// ##Syntax##
        /// QuestId: Id of the quest.
        /// ItemId: Item to use Over and Over until Location is reached.
        /// X,Y,Z: The general location where theese objects can be found
        /// </summary>
        /// 
        public ItemWhileMoving(Dictionary<string, string> args)
            : base(args)
        {
			try
			{
                int         itemId;
                WoWPoint    location;
                int         questId;


                CheckForUnrecognizedAttributes(new Dictionary<string, object>()
                                                {
                                                    { "ItemId",     null},
                                                    { "QuestId",    null},
                                                    { "X",          null},
                                                    { "Y",          null},
                                                    { "Z",          null},
                                                });

                _isAttributesOkay = true;
                _isAttributesOkay &= GetAttributeAsInteger("ItemId", true, "0", 0, int.MaxValue, out itemId);
                _isAttributesOkay &= GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
                _isAttributesOkay &= GetXYZAttributeAsWoWPoint(true, WoWPoint.Empty, out location);

                // Semantic coherency --
                if (_isAttributesOkay)
                {
                    if (itemId == 0)
                    {
                        UtilLogMessage("error", "\"ItemId\" may not be zero");
                        _isAttributesOkay = false;
                    }
                }


                if (_isAttributesOkay)
                {
                    Counter = 0;
                    ItemID = itemId;
                    Location = location;
                    MovedToTarget = false;
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


        public int          Counter { get; set; }
        public int          ItemID { get; set; }
        public WoWPoint     Location { get; private set; }
        public bool         MovedToTarget;
        public int          NumberOfTimes { get; set; }
        public uint         QuestId { get; set; }

        private bool        _isAttributesOkay;
        private bool        _isBehaviorDone;
        private Composite   _root;

        public static LocalPlayer   s_me = ObjectManager.Me;


        public WoWItem wowItem
        {
            get
            {
                List<WoWItem> inventory = ObjectManager.GetObjectsOfType<WoWItem>(false);

                foreach (WoWItem item in inventory)
                {
                    if (item.Entry == ItemID)
                        return item;
                }

                return inventory[0];
            }
        }

        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                            new Decorator(ret => (Counter > NumberOfTimes) || (s_me.QuestLog.GetQuestById(QuestId) != null && s_me.QuestLog.GetQuestById(QuestId).IsCompleted),
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new WaitContinue(120,
                                        new Action(delegate
                                        {
                                            _isBehaviorDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )),

                            new Decorator(c => Location.Distance(s_me.Location) > 3,
                                new Action(c =>
                                {
                                    if (Location.Distance(s_me.Location) <= 3)
                                    {
                                        _isBehaviorDone = true;
                                        return RunStatus.Success;
                                    }
                                    TreeRoot.StatusText = "Moving To Location: Using Item - " + wowItem.Name;

                                    WoWPoint[] pathtoDest1 = Styx.Logic.Pathing.Navigator.GeneratePath(s_me.Location, Location);

                                    foreach (WoWPoint p in pathtoDest1)
                                    {
                                        while (!s_me.Dead && p.Distance(s_me.Location) > 2)
                                        {
                                            Thread.Sleep(100);
                                            WoWMovement.ClickToMove(p);
                                            wowItem.Interact();
                                        }
                                    }


                                    return RunStatus.Running;
                                }))
                    ));
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
                    { TreeRoot.GoalText = string.Format("{0} - \"{1}\"", this.GetType().Name, quest.Name); }

                else
                    { TreeRoot.GoalText = string.Format("{0}: Running", this.GetType().Name); }
            }
        }

        #endregion
    }
}
