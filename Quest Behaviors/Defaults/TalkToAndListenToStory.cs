using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Styx.Database;
using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// TalkToAndListenToStory by Nesox
    /// Allows you to interact and click thru the gossip of a number of npc's
    /// ##Syntax##
    /// QuestId: Id of the quest.
    /// NpcIds: Whitespace seperated list of id's of npc's to use.
    /// </summary>
    public class TalkToAndListenToStory : CustomForcedBehavior
    {
        #region Overrides of CustomForcedBehavior

        private Composite _root;

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"QuestId",null},
            {"NpcIds",null},
            {"MobIds",null}
        };

        public TalkToAndListenToStory(Dictionary<string, string> args)
            : base(args)
        {
            UtilLogMessage("warning",   "*****\n"
                                        + "* THIS BEHAVIOR IS DEPRECATED, and may be retired in a near, future release.\n"
                                        + "*\n"
                                        + "* TalkToAndListenToStory adds _no_ _additonal_ _value_ over the InteractWith behavior (with the \"GossipOption\" attribute).\n"
                                        + "* Please update the profile to use InteractWith in preference to the TalkToAndListenToStory behavior.\n"
                                        + "*****");

            CheckForUnrecognizedAttributes(recognizedAttributes);

            uint questId;
            if (!uint.TryParse(Args["QuestId"], out questId))
                Logging.Write("Unable to parse value of attribute QuestId!");

            QuestId = questId;

            if (Args.ContainsKey("NpcIds"))
            {
                foreach (string s in Args["NpcIds"].Split(' '))
                {
                    uint id;
                    if (uint.TryParse(s, out id))
                        _npcResults.Enqueue(NpcQueries.GetNpcById(id));
                    else
                        Logging.Write("Unable to parse {0} as an integer! check your profile", s);
                }
            }
            else if (Args.ContainsKey("MobIds"))
            {
                foreach (string s in Args["MobIds"].Split(' '))
                {
                    uint id;
                    if (uint.TryParse(s, out id))
                        _npcResults.Enqueue(NpcQueries.GetNpcById(id));
                    else
                        Logging.Write("Unable to parse {0} as an integer! check your profile", s);
                }
            }
            else
            {
                Logging.Write("Could not find attribute: NpcIds in TalkToAndListenToStory custom behavior!");
                Thread.CurrentThread.Abort();
            }
        }

        public uint QuestId;

        /// <summary>
        /// A Queue for npc's we need to talk to
        /// </summary>
        private readonly Queue<NpcResult> _npcResults = new Queue<NpcResult>();

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(ret => _npcResults.Count != 0 ? _npcResults.Peek() : null,

                    new Decorator(ret => ret == null,
                        new Action(ret => _isDone = true)),

                    // Move to it if we are too far away.
                    new Decorator(ret => ret is NpcResult && ((NpcResult)ret).Location.Distance(StyxWoW.Me.Location) > 3,
                        new Sequence(
                            new Action(ret => TreeRoot.StatusText = "Moving towards - " + ((NpcResult)ret).Location),
                            new Action(ret => Navigator.MoveTo(((NpcResult)ret).Location)))
                            )
                        ,

                    new Sequence(ret => ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(unit => unit.Entry == ((NpcResult)ret).Entry),
                        new DecoratorContinue(ret => ret != null,
                            new Sequence(

                                new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                    new Action(ret => 
                                    {
                                        WoWMovement.MoveStop();
                                        StyxWoW.SleepForLagDuration();
                                    })
                                    ),

                                new DecoratorContinue(ret => !GossipFrame.Instance.IsVisible,
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Interacting with - " + ((WoWUnit)ret).Name),
                                        new Action(ret => ((WoWUnit)ret).Interact()))
                                        ),

                                new WaitContinue(3, ret => !GossipFrame.Instance.IsVisible,
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Clicking thru gossip"),
                                        new Action(delegate
                                        {
                                            Lua.DoString("GossipTitleButton1:Click()");
                                            Thread.Sleep(1500);

                                            if (GossipFrame.Instance.IsVisible)
                                                return RunStatus.Running;

                                            _npcResults.Dequeue();
                                            return RunStatus.Success;
                                        })
                                        ))

                            )))
                    ));
        }

        public override void OnStart()
        {
            TreeRoot.GoalText = "Running TalkToAndListenToStory behavior";
        }

        private bool _isDone;
        public override bool IsDone
        {
            get
            {
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

                return
                    _isDone ||
                    (quest != null && quest.IsCompleted) ||
                    quest == null;
            }
        }

        #endregion
    }
}
