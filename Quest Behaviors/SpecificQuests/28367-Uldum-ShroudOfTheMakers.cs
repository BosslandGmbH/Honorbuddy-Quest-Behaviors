// Behavior originally contributed by mastahg.
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
using System.Numerics;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.ShroudOfTheMakers
{
    [CustomBehaviorFileName(@"SpecificQuests\28367-Uldum-ShroudOfTheMakers")]
    public class Shroud : CustomForcedBehavior
    {
        public Shroud(Dictionary<string, string> args)
            : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                QuestId = 28367;
                QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = QuestInLogRequirement.InLog;
                MobIds = new uint[] { 50635, 50638, 50643, 50636 };
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

        // DON'T EDIT THIS--it is auto-populated by Git
        public override string VersionId => QuestBehaviorBase.GitIdToVersionId("$Id$");


        // Attributes provided by caller
        public uint[] MobIds { get; private set; }
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }
        public Vector3 Location { get; private set; }
        public int FlightSpot;
        public int State = 4;
        public Pair Target;
        public Vector3[] FlightPath = new Vector3[]
        {
            new Vector3(0f,0f,0f),
            new Vector3(-9065.746f, -178.7902f, 186.2216f),
            new Vector3(-8767.904f, -47.82138f, 186.2216f),
            new Vector3(-8839.586f, 94.10641f, 186.2216f),
            new Vector3(-8977.347f, 180.7038f, 186.2216f),
            new Vector3(-9150.972f, 66.71052f, 186.2216f)
        };

        public Pair[] SafeSpots = new Pair[]
        {
            new Pair(new Vector3(-8865.017f, -67.25845f, 142.4352f),
                    new Vector3(-8879.103f, -27.9558f, 141.0528f)),
            new Pair(new Vector3(-8880.18f, 100.8362f, 142.3729f),
                    new Vector3(-8923.264f, 81.26907f, 141.0495f)),
            new Pair(new Vector3(-9009.845f, 136.4343f, 141.2677f),
                    new Vector3(-9020.673f, 102.5993f, 141.0485f)),
            new Pair(new Vector3(-9055.021f, 111.2494f, 142.7882f),
                    new Vector3(-9050.942f, 78.64673f, 141.0491f)),
            new Pair(new Vector3(-9055.021f, 111.2494f, 142.7882f),
                    new Vector3(-9058.617f, 60.17788f, 141.0492f)),
            new Pair(new Vector3(-9098.441f, -107.1487f, 142.0959f),
                    new Vector3(-9074.733f, -81.41763f, 141.049f)),
            new Pair(new Vector3(-8897.744f, -86.20571f, 142.4366f),
                    new Vector3(-8927.161f, -55.22403f, 141.0703f)),
            new Pair(new Vector3(-8867.855f, -68.69208f, 142.4369f),
                    new Vector3(-8878.626f, -28.24805f, 141.0537f)),
            new Pair(new Vector3(-8903.402f, 116.398f, 142.4555f),
                    new Vector3(-8913.154f, 105.3958f, 141.0488f)),
            new Pair(new Vector3(-8883.854f, 100.8373f, 141.4563f),
                    new Vector3(-8912.104f, 82.27669f, 141.0491f))
        };



        // Private variables for internal state
        private bool _isBehaviorDone;
        private Composite _root;


        // Private properties
        private LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
        }

        public struct Pair
        {
            public Vector3 LandingSpot, BarrelSpot;

            public Pair(Vector3 l, Vector3 b)
            {
                LandingSpot = l;
                BarrelSpot = b;
            }
        }

        #region Overrides of CustomForcedBehavior

        public Composite DoneYet
        {
            get
            {
                return new Decorator(ret => Me.IsQuestComplete(QuestId),
                    new Action(delegate
                    {
                        TreeRoot.StatusText = "Finished!";
                        _isBehaviorDone = true;
                        return RunStatus.Success;
                    }));
            }
        }


        public WoWUnit Dragon
        {
            get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == 48444 && u.IsAlive); }
        }


        public Composite GetToCurrent
        {
            get
            {
                return
                    new Decorator(r => Me.Location.Distance(FlightPath[Math.Abs(FlightSpot)]) > 1, new Action(r => Flightor.MoveTo(FlightPath[Math.Abs(FlightSpot)])));
            }
        }

        public Composite SetNext
        {
            get { return new Action(r => IncreaseFlight()); }
        }


        public void IncreaseFlight()
        {
            FlightSpot = Math.Abs(FlightSpot);
            if (FlightSpot == 5)
            {
                FlightSpot = 1;
            }
            else
            {
                FlightSpot++;
            }
        }

        public bool ValidSafeSpot()
        {
            var myLoc = Me.Location;
            var query = from x in SafeSpots
                        where myLoc.DistanceSquared(x.LandingSpot) < 60 * 60
                        let barrel = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(
                            u => u.Entry == 207127 && u.Location.DistanceSquared(x.BarrelSpot) < u.InteractRangeSqr)
                        where barrel != null
                        select x;

            foreach (var x in query)
            {
                Target = x;
                return true;
            }
            return false;
        }


        public Composite CheckSafeSpots
        {
            get
            {
                return
                    new Decorator(r => ValidSafeSpot(), new Action(r => State = 1));
            }
        }


        public Composite MountUp
        {
            get
            {
                return
                    new Decorator(r => !Me.Mounted, new Action(r => Flightor.MountHelper.MountUp()));
            }
        }

        public Composite Circle
        {
            get
            {
                return new Decorator(r => State == 4, new PrioritySelector(MountUp, CheckSafeSpots, GetToCurrent, SetNext));
            }
        }

        public Composite StateOne
        {
            get
            {
                return new Decorator(r => State == 1 || State == 3, new PrioritySelector(GetThere, StepIncrease));
            }
        }

        public Composite StepIncrease
        {
            get
            {
                return
                    new Decorator(r => Me.Location.Distance(Target.LandingSpot) <= 1, new Action(delegate
                                                                                                     {
                                                                                                         Flightor.MountHelper.Dismount();
                                                                                                         RemoveCloak();
                                                                                                         State++;
                                                                                                     }));
            }
        }


        public WoWItem ShroudItem
        {
            get { return Me.BagItems.FirstOrDefault(x => x.Entry == 63699); }
        }


        public Composite GetCloaked
        {
            get
            {
                return
                    new Decorator(r => !Me.HasAura("Shroud of the Makers"), new Action(r => ShroudItem.Use()));
            }
        }


        public WoWGameObject Barrel
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(
                                                    u =>
                                                    u.Entry == 207127 &&
                                                    u.Location.Distance(Target.BarrelSpot) < u.InteractRange);
            }
        }

        public Composite ClickIt
        {
            get
            {
                return new Action(delegate
                                      {
                                          if (Barrel != null)
                                          {
                                              Barrel.Interact();
                                          }
                                      });
            }
        }


        public Composite GoBack
        {
            get
            {
                return
                    new Decorator(r => Barrel == null, new Action(r => State = 3));
            }
        }

        public Composite StateTwo
        {
            get
            {
                return new Decorator(r => State == 2 && Dragon != null && Dragon.Distance > 100, new PrioritySelector(Removepet, GoBack, GetCloaked, MoveOut, ClickIt));
            }
        }

        public Composite Removepet
        {
            get
            {
                return
                    new Decorator(r => Me.Pet != null, new Action(r => Lua.DoString("PetDismiss();")));
            }
        }


        public Composite StateTwoDragon
        {
            get
            {
                return new Decorator(r => State == 2 && Dragon != null && Dragon.Distance < 100, new PrioritySelector(GetThere));
            }
        }


        public Composite MoveOut
        {
            get
            {
                return
                    new Decorator(r => Me.Location.Distance(Target.BarrelSpot) > 1, new Action(r => WoWMovement.ClickToMove(Target.BarrelSpot)));
            }
        }

        public Composite GetThere
        {
            get
            {
                return
                    new Decorator(r => Me.Location.Distance(Target.LandingSpot) > 1, new Action(r => WoWMovement.ClickToMove(Target.LandingSpot)));
            }
        }


        protected Composite CreateBehavior_CombatMain()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone && !Me.IsActuallyInCombat && Me.IsAlive, new PrioritySelector(DoneYet, StateOne, StateTwo, StateTwoDragon, Circle, new ActionAlwaysSucceed())));
        }


        public override void OnFinished()
        {
            TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_CombatMain());
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }


        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone // normal completion
                        || !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
            }
        }

        public void RemoveCloak()
        {
            var aura = Me.GetAllAuras().FirstOrDefault(x => x.SpellId == 90139);
            if (aura != null)
            {
                Lua.DoString("RunMacroText(\"/cancelaura " + aura.Name + "\")");
            }
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
                TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_CombatMain());

                //Find the current closest flight spot;
                float distance = float.MaxValue;
                FlightSpot = 0;
                var myLoc = Me.Location;

                for (int i = 0; i < FlightPath.Length; i++)
                {
                    if (myLoc.Distance(FlightPath[i]) < distance)
                    {
                        distance = myLoc.Distance(FlightPath[i]);
                        FlightSpot = i;
                    }
                }

                this.UpdateGoalText(QuestId);
            }
        }

        #endregion
    }
}