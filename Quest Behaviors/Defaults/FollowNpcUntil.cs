using System;
using System.Collections.Generic;
using System.Linq;

using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class FollowNpcUntil : CustomForcedBehavior
    {
        public FollowNpcUntil(Dictionary<string, string> args)
            : base(args)
        {
			try
			{
                WoWPoint    location;
                int         mobId;
                int         questId;

                UtilLogMessage("warning",   "*****\n"
                                          + "* THIS BEHAVIOR IS DEPRECATED, and may be retired in a near, future release.\n"
                                          + "*\n"
                                          + "* Escort is the replacement behavior for FollowNpcUntil.\n"
                                          + "* Please update the profile to use Escort in preference to this behavior.\n"
                                          + "*****");


                CheckForUnrecognizedAttributes(new Dictionary<string, object>()
                                                {
                                                    { "MobId",      null },
                                                    { "MobName",    null },
                                                    { "NpcId",      null },
                                                    { "QuestId",    null },
                                                    { "X",          null },
                                                    { "Y",          null },
                                                    { "Z",          null },
                                                });

                _isAttributesOkay = true;
                _isAttributesOkay &= GetAttributeAsString("MobName", false, "", out _mobName);
                _isAttributesOkay &= GetAttributeAsInteger("QuestId", false, "0", 0, int.MaxValue, out questId);
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

                    if (location == WoWPoint.Empty)
                    {
                        UtilLogMessage("error", "X-Y-Z may not be zero");
                        _isAttributesOkay = false;
                    }
                }


                if (_isAttributesOkay)
                {
                    Counter = 1;
                    Location = location;
                    MovedToTarget = false;
                    MobId = mobId;
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


        public int      Counter { get; set; }
        public WoWPoint Location { get; private set; }
        public bool     MovedToTarget { get; private set; }
        public int      MobId { get; set; }
        public uint     QuestId { get; set; }

        private bool            _isAttributesOkay;
        private bool            _isBehaviorDone;
        private List<WoWUnit>   _npcList;
        private string          _mobName;
        private Composite       _root;

        public static LocalPlayer   s_me = ObjectManager.Me;


        /// <summary>
        /// A Queue for npc's we need to talk to
        /// </summary>
        //private WoWUnit CurrentUnit { get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(unit => unit.Distance < 100 && unit.Entry == MobId); } }

        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(
				
				new Decorator(ret => (QuestId != 0 && s_me.QuestLog.GetQuestById(QuestId) != null &&
                         s_me.QuestLog.GetQuestById(QuestId).IsCompleted),
                        new Action(ret => _isBehaviorDone = true)),

                    new Decorator(ret => Counter > 1,
                        new Action(ret => _isBehaviorDone = true)),

                        new PrioritySelector(

                           new Decorator(ret => !MovedToTarget,
                                new Action(delegate
                                {
                                    ObjectManager.Update();

                                    _npcList = ObjectManager.GetObjectsOfType<WoWUnit>()
                                        .Where(u => u.Entry == MobId)
                                        .OrderBy(u => u.Distance).ToList();

                                    PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
                                    if (quest.IsCompleted)
                                    {
                                        Counter++;
                                        return RunStatus.Success;
                                    }
                                    else if (_npcList.Count >= 1)
                                    {
                                        Navigator.MoveTo(_npcList[0].Location);

                                    }
                                    else
                                    {
                                        Navigator.MoveTo(Location);
                                    }

                                    return RunStatus.Running;

                                })
                                ),

                            new Action(ret => Navigator.MoveTo(Location))
                        )
                    ));
        }


        public override bool IsDone
        {
            get { return (_isBehaviorDone); }
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
                if (string.IsNullOrEmpty(_mobName))
                    { TreeRoot.GoalText = string.Format("Following MobId {0}", MobId); }
                else
                    { TreeRoot.GoalText = string.Format("Following '{0}'", _mobName); }
            }
		}

        #endregion
    }
}
