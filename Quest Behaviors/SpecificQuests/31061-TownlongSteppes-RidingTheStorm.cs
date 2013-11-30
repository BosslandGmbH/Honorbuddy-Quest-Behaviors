//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

#region Summary and Documentation
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.RidingTheStorm
{
    [CustomBehaviorFileName(@"SpecificQuests\31061-TownlongSteppes-RidingTheStorm")]
    public class Blastranaar : CustomForcedBehavior
    {
        public Blastranaar(Dictionary<string, string> args)
            : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            try
            {
                QuestId = 31061;
                SpellIds = GetNumberedAttributesAsArray<int>("SpellId", 1, ConstrainAs.SpellId, null);
                SpellId = SpellIds.FirstOrDefault(id => SpellManager.HasSpell(id));
            }
            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                QBCLog.Exception(except);
                IsAttributeProblem = true;
            }
        }
        public int QuestId { get; set; }
        private bool _isBehaviorDone;
        public int MobIdCloudrunner = 62586;
        public int BronzeClawId = 83134;
        public int[] SpellIds { get; private set; }
        public int SpellId { get; private set; }
        private Composite _root;

        public override bool IsDone
        {
            get
            {
                return _isBehaviorDone;
            }
        }
        private LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
        }

        public override void OnStart()
        {
            OnStart_HandleAttributeProblem();
            if (!IsDone)
            {
                this.UpdateGoalText(QuestId);
            }
        }

        public List<WoWUnit> CloudrunnerOutRange
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == MobIdCloudrunner && !u.IsDead && u.Distance < 10000 && u.HealthPercent == 100).OrderBy(u => u.Distance).ToList();
            }
        }


        public List<WoWUnit> CloudrunnerInRange
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == MobIdCloudrunner && !u.IsDead && u.Distance < 10000).OrderBy(u => u.Distance).ToList();
            }
        }

        public WoWItem BronzeClaw { get { return (StyxWoW.Me.CarriedItems.FirstOrDefault(i => i.Entry == BronzeClawId)); } }


        public Composite DoneYet
        {
            get
            {
                return new Decorator(ret => Me.IsQuestObjectiveComplete(QuestId, 1),
                    new Action(delegate
                    {
                        TreeRoot.StatusText = "Finished!";
                        _isBehaviorDone = true;
                        return RunStatus.Success;
                    }));
            }
        }


        public Composite CloudrunnerKill
        {
            get
            {
                return new Decorator(ret => !Me.IsQuestObjectiveComplete(QuestId, 1),
                    new PrioritySelector(

			            new Decorator(ret => CloudrunnerOutRange[0].Location.Distance(Me.Location) > 20 && !Me.Combat,
                            new Action(c =>
			                {
			                    TreeRoot.StatusText = "Using Bronze Claw on CloudRunner";
			                    CloudrunnerOutRange[0].Target();

			                    if (BronzeClaw.Cooldown == 0)
			                    {
				                    BronzeClaw.UseContainerItem();
                                    StyxWoW.Sleep(1000);
				                }

                                return RunStatus.Success;
			                })),

			            new Decorator(ret => CloudrunnerInRange[0].Location.Distance(Me.Location) < 10, new Action(c =>
			            {
			                TreeRoot.StatusText = "Killing CloudRunner";
                    	    SpellManager.Cast(SpellId);
                            StyxWoW.Sleep(1000);

			                if (CloudrunnerInRange[0].IsFriendly)
				            {

				                TreeRoot.StatusText = "CloudRunner is friendly, switching to new one";
				                StyxWoW.Sleep(2000);
                        	    return RunStatus.Success;
				            }

			                if (Me.IsQuestObjectiveComplete(QuestId, 1))
				            {
                        	    TreeRoot.StatusText = "Finished!";
                        	    _isBehaviorDone = true;
                        	    return RunStatus.Success;
				            }

                            return RunStatus.Running;
			            }))));
            }
        }

		
        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, CloudrunnerKill, new ActionAlwaysSucceed())));
        }
    }
}
