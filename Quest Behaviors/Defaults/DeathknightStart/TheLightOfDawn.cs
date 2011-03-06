using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CommonBehaviors.Actions;
using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;

using Action = TreeSharp.Action;
using Styx.Logic.Inventory.Frames.Gossip;
using System;

namespace Styx.Bot.Quest_Behaviors
{
    public class TheLightOfDawn : CustomForcedBehavior
    {
        readonly Dictionary<string, object> _recognizedAttributes = new Dictionary<string, object>()
        {
            {"QuestId",null},
        };

        private readonly bool _success = true;

		public TheLightOfDawn(Dictionary<string, string> args)
            : base(args)
        {
		    CheckForUnrecognizedAttributes(_recognizedAttributes);

		    int questId = 0;
		    _success = _success && GetAttributeAsInteger("QuestId", false, "0", int.MinValue, int.MaxValue, out questId);

            if (!_success)
            {
                Logging.Write(Color.Red, "Error parsing tag for AnEndToAllThings. {0}", Element);
                TreeRoot.Stop();
            }

		    QuestId = (uint)questId;
        }

        public uint QuestId { get; private set; }
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
		private static WoWUnit HighWarlordDarion { get { return ObjectManager.GetObjectsOfType<WoWUnit>(false, false).FirstOrDefault(u => u.Entry == 29173); } }

        #region Overrides of CustomForcedBehavior

        private readonly Styx.Helpers.WaitTimer _waitTimer = new Styx.Helpers.WaitTimer(TimeSpan.FromMinutes(10));

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ??(_root = 
                new PrioritySelector(

                    new Decorator(ret => !_waitTimer.IsFinished,
                        new Sequence(
                            new Action(ret => TreeRoot.GoalText = "Waiting for the story to end"),
                            new Action(ret => Logging.Write("Waiting for the story to end")),
                            new ActionAlwaysSucceed())
                            ), 
                        
                    new Decorator(ret => HighWarlordDarion != null && HighWarlordDarion.CanGossip,
                        new PrioritySelector(
                            new Decorator(ret => !HighWarlordDarion.WithinInteractRange,
                                new Sequence(
                                   new Action(ret => TreeRoot.GoalText = "Moving to High Warlord Darion"),
                                   new Action(ret => Navigator.MoveTo(HighWarlordDarion.Location)))),
								
                            new Sequence(
                                new Action(ret => TreeRoot.GoalText = "Talking to High Warlord Darion"),
                                new DecoratorContinue(ret => Me.IsMoving,
                                    new Sequence(
                                        new Action(ret => WoWMovement.MoveStop()),
                                        new Action(ret => StyxWoW.SleepForLagDuration())
                                        )),

                                new Action(ret => HighWarlordDarion.Interact()),
                                new WaitContinue(5, ret => GossipFrame.Instance.IsVisible,
                                    new Sequence(
                                        new DecoratorContinue(ret => GossipFrame.Instance.GossipOptionEntries == null,
                                            new Action(ret => _waitTimer.Reset())
                                            ),

                                        new DecoratorContinue(ret => GossipFrame.Instance.GossipOptionEntries != null,
                                            new Sequence(
                                                new Action(ret => GossipFrame.Instance.SelectGossipOption(0)),
                                                new Action(ret => StyxWoW.SleepForLagDuration())
                                                )))
                                    )))),

							new Action(ret => _waitTimer.Reset())
                    ));
        }

        public override bool IsDone
        {
            get
            {
                var quest = Me.QuestLog.GetQuestById(QuestId);
                return quest != null && quest.IsCompleted;
            }
        }

        #endregion
    }
}
