using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using Styx;
using Styx.Logic.Combat;
using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors.MountHyjal
{
    public class MentalTraining : CustomForcedBehavior
    {
        /// <summary>
        /// MentalTraining by Bobby53
        /// 
        /// Completes the quest http://www.wowhead.com/quest=25299
        /// This behavior completes the quest by correctly responding to
        /// 10 yes/no questions by checking the toons question aura.
        /// 
        /// Requires you to already be in position at the quest give Instructor Mylva X="4524.021" Y="-4731.176" Z="887.9406"
        /// 
        /// ##Syntax##
        /// QuestId: Id of the quest (default is 0)
        /// [Optional] QuestName: optional quest name (documentation only)
        /// 
        /// </summary>
        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {
            {"QuestId",null},               //  should be 25299
            {"QuestName",null},             //  (doc only - not used)
        };

        public LocalPlayer Me { get { return ObjectManager.Me; } }

        public MentalTraining(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);

            bool error = false;
            
            uint questId;
            if (!uint.TryParse(Args["QuestId"], out questId))
            {
                Logging.Write("Parsing attribute 'QuestId' in BaronGeddon behavior failed! please check your profile!");
                error = true;
            }

            if (error)
                TreeRoot.Stop();

            QuestId = questId;
        }

        public uint QuestId { get; private set; }

        static int lineCount = 0;

        public static void Log(string msg, params object[] args)
        {
            // following linecount hack is to stop dup suppression of Log window
            Logging.Write(Color.Green, "[MentalTraining] " + msg + (++lineCount % 2 == 0 ? "" : " "), args);
        }

        public static void DLog(string msg, params object[] args)
        {
            // following linecount hack is to stop dup suppression of Log window
            Logging.Write(Color.Green, "(MentalTraining) " + msg + (++lineCount % 2 == 0 ? "" : " "), args);
        }

        public bool DoWeHaveQuest()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
            return quest != null;
        }

        public bool IsQuestComplete()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
            return quest == null || quest.IsCompleted;
        }

        public bool HasAura(WoWUnit unit, int auraId)
        {
            WoWAura aura = (from a in unit.Auras
                            where a.Value.SpellId == auraId
                            select a.Value).FirstOrDefault();
            return aura != null;
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

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
            if (quest != null )
            {
                TreeRoot.GoalText = string.Format("Doing quest: {0}", quest.Name);
            }
        }

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                    // check if we have finished 10 questions (marked complete)
                    new Decorator(ret => IsQuestComplete(),
                        new PrioritySelector(
                            new Decorator(ret => Me.HasAura( "Mental Training"),
                                new Action(delegate 
                                { 
                                    Log( "Mental Training complete - exiting Orb");
                					Lua.DoString("RunMacroText(\"/click BonusActionButton4\")");
                                    StyxWoW.SleepForLagDuration();
                                    return RunStatus.Success;
                                })
                            ),
                            new Action(ret => _isDone = true)
                        )
                    ),

                    // if we don't have vehicle buff, use Orb of Ascension
                    new Decorator(ret => !Me.HasAura( "Mental Training"),
                        new Action(delegate 
                        { 
                            Log( "Using Orb of Ascension");
                            // WoWItem orb =  Me.Inventory.Items.FirstOrDefault( i => i != null && i.Entry == 52828 );
                            WoWItem orb = ObjectManager.GetObjectsOfType<WoWItem>().Where(u => u.Entry == 52828).FirstOrDefault();
                            if (orb == null)
                            {
                                Logging.Write("MentalTraining.cs:  ERROR - quest item Orb of Ascension not in inventory");
                                TreeRoot.Stop();
                            }

                            orb.Use(true);
                            StyxWoW.SleepForLagDuration();
                            return RunStatus.Success;
                        })
                    ),

                    // if we have YES aura 74008, then click yes
                    new Decorator(ret => HasAura(Me, 74008),
                        new Action(delegate
                        {
                            Log( "Answering YES");
                            Thread.Sleep( 500 );
                            Lua.DoString("RunMacroText(\"/click BonusActionButton1\")");
                            StyxWoW.SleepForLagDuration();
                            return RunStatus.Success;
                        })
                    ),

                    // if we have NO aura 74009, then click no
                    new Decorator(ret => HasAura(Me, 74009),
                        new Action(delegate
                        {
                            Log( "Answering NO");
                            Thread.Sleep( 500 );
                            Lua.DoString("RunMacroText(\"/click BonusActionButton2\")");
                            StyxWoW.SleepForLagDuration();
                            return RunStatus.Success;
                        })
                    ),
                new Action(delegate
                {
                    return RunStatus.Success;
                })
             )
          );
        }

        public override void Dispose()
        {
            base.Dispose();
        }

    }
}
