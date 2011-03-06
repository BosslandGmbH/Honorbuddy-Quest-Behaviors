using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;

namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// CastSpellOn by Nesox
    /// Allows you to cast a spell on an object.
    /// ##Syntax##
    /// QuestId: Id of the quest.
    /// SpellId: Id of the spell to use.
    /// NpcId: If of the Npc to cast said spell on.
    /// NumOfTimes: Number of times to cast spell.
    /// MinRange: the distance from the unit before attempting to cast the spell.
    /// X,Y,Z: The precise location where this object can be found
    /// </summary>
    public class CastSpellOn : CustomForcedBehavior
    {
        public CastSpellOn(Dictionary<string, string> args)
            : base(args)
        {
            uint questId;
            if (!uint.TryParse(Args["QuestId"], out questId))
            {
                Logging.Write("Parsing QuestID in UseGameObject behavior failed! please check your profile!");
            }

            int spellId;
            if (!int.TryParse(Args["SpellId"], out spellId))
            {
                Logging.Write("Parsing mobid in CastSpellOn behavior failed! please check your profile!");
            }

            int npcId;
            if (!int.TryParse(Args["NpcId"], out npcId))
            {
                Logging.Write("Parsing mobid in CastSpellOn behavior failed! please check your profile!");
            }

            int numOfTimes;
            if (!int.TryParse(Args["NumOfTimes"], out numOfTimes))
            {
                Logging.Write("Parsing mobid in CastSpellOn behavior failed! please check your profile!");
            }

            int minRange;
            if (!int.TryParse(Args["MinRange"], out minRange))
            {
                Logging.Write("Parsing mobid in CastSpellOn behavior failed! please check your profile!");
            }

            float x;
            if (!float.TryParse(Args["X"], out x))
                Logging.Write("Parsing X in CastSpellOn behavior failed! please check your profile!");

            float y;
            if (!float.TryParse(Args["Y"], out y))
                Logging.Write("Parsing Y in CastSpellOn behavior failed! please check your profile!");

            float z;
            if (!float.TryParse(Args["Z"], out z))
                Logging.Write("Parsing Z in CastSpellOn behavior failed! please check your profile!");

            QuestId = questId;
            SpellId = spellId;
            NpcId = npcId;

            MinRange = minRange;
            NumOfTimes = numOfTimes;
            Location = new WoWPoint(x, y, z);
        }

        public uint QuestId { get; private set; }
        public int SpellId { get; private set; }
        public int NpcId { get; private set; }
        public int MinRange { get; private set; }
        public int NumOfTimes { get; private set; }
        public WoWPoint Location { get; private set; }
        public int Counter { get; private set; }

        public WoWObject CurrentObject
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWObject>(true).FirstOrDefault(
                        ret => ret.Entry == NpcId && ret.Location.Distance(Location) < 2);
            }
        }


        #region Overrides of CustomForcedBehavior

        private Composite _root;
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
            new PrioritySelector(

                new Decorator(ret => Counter >= NumOfTimes,
                    new Action(ret => _isDone = true)),

                    new PrioritySelector(

                        new Decorator(ret => CurrentObject != null && (CurrentObject.Distance > MinRange || !CurrentObject.InLineOfSight),
                            new Sequence(
                                new Action(delegate { TreeRoot.StatusText = "Moving to cast spell on - " + CurrentObject.Name; }),
                                new Action(ret => Navigator.MoveTo(CurrentObject.Location))
                                )
                            ),

                        new Decorator(ret => CurrentObject != null && CurrentObject.Distance <= MinRange && CurrentObject.InLineOfSight,
                            
                            // Set the context to the spell
                            new Sequence(ret => WoWSpell.FromId(SpellId),
                                
                                new DecoratorContinue(ret => ret == null,
                                    new Action(delegate
                                                   {
                                                       TreeRoot.StatusText = "Casting spell on - " + CurrentObject.Name;
                                                       WoWSpell spell = WoWSpell.FromId(SpellId);
                                                       if (spell == null)
                                                       {
                                                           Logging.Write(Color.Red, "Could not find spell with id:{0} for CastSpellOn behavior!", SpellId);
                                                           Logging.Write(Color.Red, "Honorbuddy stopped!");
                                                           TreeRoot.Stop();
                                                           return;
                                                       }
                                                   })),

                                new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                    new Action(delegate
                                    {
                                        WoWMovement.MoveStop();
                                        StyxWoW.SleepForLagDuration();
                                    })),

                                    // Cast the spell
                                    new Action(ret => SpellManager.Cast(SpellId, CurrentObject.ToUnit())),
                                    new Action(ret => StyxWoW.SleepForLagDuration()),
                                    
                                    // Increase the counter.
                                    new WaitContinue(5, ret => !StyxWoW.Me.IsCasting,
                                        new Action(ret => Counter++))
                                    
                                    )),

                        new Sequence(
                            new Action(delegate { TreeRoot.StatusText = "Moving towards - " + Location; }),
                            new Action(ret => Navigator.MoveTo(Location))))
                ));
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
            if (quest != null)
                TreeRoot.GoalText = string.Format("Casting spell on Mob Id:{0} {1} Times for quest:{2}", NpcId, NumOfTimes, quest.Name);
        }

        #endregion
    }
}