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
using System.Threading.Tasks;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Styx.Bot.Quest_Behaviors
{
    [CustomBehaviorFileName(@"ArgentTournament\BattleofTheCitadel")]
    public class BattleofTheCitadel : CustomForcedBehavior
    {
        public BattleofTheCitadel(Dictionary<string, string> args)
            : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                Location = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
                QuestId = GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
                QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = QuestInLogRequirement.InLog;
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

        private uint[] _mounts = new uint[] { 34125 };


        private const uint ItemId_ArgentLance = 46106;

        private WoWItem ArgentLance { get { return Me.CarriedItems.FirstOrDefault(x => x.Entry == ItemId_ArgentLance); } }

        // Attributes provided by caller
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }
        public WoWPoint Location { get; private set; }

        // Private variables for internal state
        private bool _isBehaviorDone;
        private Composite _root;



        // Private properties
        private LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
        }

        #region Overrides of CustomForcedBehavior

        public Composite DoneYet
        {
            get
            {
                return
                    new Decorator(r => Me.IsQuestComplete(QuestId), new PrioritySelector(
                        new Decorator(r => Me.Location.Distance(Location) > 3, new Action(r => Navigator.MoveTo(Location))),
                        new Decorator(ret => Me.Location.Distance(Location) < 3,
                            new Action(delegate
                            {
                                Lua.DoString("RunMacroText(\"/leavevehicle\")");

                                if (Query.IsViable(_mainhand) && Me.Inventory.Equipped.MainHand != _mainhand)
                                    _mainhand.UseContainerItem();

                                if (Query.IsViable(_offhand) && Me.Inventory.Equipped.OffHand != _offhand)
                                    _offhand.UseContainerItem();
                                TreeRoot.StatusText = "Finished!";
                                _isBehaviorDone = true;
                                return RunStatus.Success;
                            }))));
            }
        }



        public void UsePetSkill(string action)
        {
            var spell = StyxWoW.Me.PetSpells.FirstOrDefault(p => p.ToString() == action);
            if (spell == null)
                return;
            QBCLog.Info("[Pet] Casting {0}", action);
            Lua.DoString("CastPetAction({0})", spell.ActionBarIndex + 1);
        }

        private WoWUnit Mount
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>().Where(
                        x => _mounts.Contains(x.Entry) && x.NpcFlags == 16777216).OrderBy(x => x.Distance).FirstOrDefault();
            }
        }

        //WoWPoint endspot = new WoWPoint(1076.7,455.7638,-44.20478);
        // WoWPoint spot = new WoWPoint(1109.848,462.9017,-45.03053);
        //WoWPoint MountSpot = new WoWPoint(8426.872,711.7554,547.294);
        private WoWPoint _badarea = new WoWPoint(6225.503, 2275.372, 492.3038);

        private Composite GetNearMounts
        {
            get
            {
                return new PrioritySelector(
                    new Decorator(r => Me.Location.Distance(Location) > 15, new Action(r => Navigator.MoveTo(Location))),
                     new Decorator(r => Me.Location.Distance(Location) < 15, new Action(r => Mount.Interact()))
                        );
            }
        }

        private Composite MountUp
        {
            get
            {
                return new Decorator(r => !Me.IsOnTransport, GetNearMounts);
            }
        }

        private WoWUnit MyMount
        {
            get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(x => x.CreatedByUnitGuid == Me.Guid); }
        }



        private Composite BuffUp
        {
            get
            {
                return new Decorator(r => !Me.Combat && (!MyMount.ActiveAuras.ContainsKey("Defend") || (MyMount.ActiveAuras.ContainsKey("Defend") && MyMount.ActiveAuras["Defend"].StackCount < 3)), new Action(r => UsePetSkill("Defend")));
            }
        }


        //33429 = lt.
        //33438 = boneguard
        //34127 = commander
        private WoWUnit HostileScout
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(x => x.Entry == 33550 && x.GotTarget && x.CurrentTarget == MyMount && x.IsAlive);
            }
        }
        private WoWUnit Lt
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>().Where(x => x.Entry == 33429 && x.IsAlive).OrderBy(u => u.Distance).FirstOrDefault();
            }
        }

        private WoWUnit Cm
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>().Where(x => x.Entry == 34127 && x.IsAlive && x.Location.Distance(_badarea) > 100 && x.Location.Distance(_spots[_spot]) < 75).OrderBy(u => u.Distance).FirstOrDefault();
            }
        }

        private WoWUnit Scout
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>().Where(x => x.Entry == 33550 && x.IsAlive).OrderBy(u => u.Distance).FirstOrDefault();
            }
        }


        private WoWUnit HostileCm
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(x => x.Entry == 34127 && x.IsAlive && (x.GetThreatInfoFor(MyMount).ThreatValue > 0 || (x.TaggedByMe) || (x.GotTarget && (x.CurrentTarget == MyMount || x.CurrentTarget == Me))));
            }
        }

        private WoWUnit HostileBg
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(x => x.Entry == 33438 && x.GotTarget && x.CurrentTarget == MyMount && x.IsAlive);
            }
        }

        private WoWUnit HostileLt
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(x => x.Entry == 33429 && x.GotTarget && x.CurrentTarget == MyMount && x.IsAlive);
            }
        }

        private WoWUnit _pretarget;
        private Composite Fight
        {
            get
            {
                return
                    new Decorator(r => Me.Combat, new Action(r =>
                                                                 {
                                                                     ObjectManager.Update();
                                                                     if (HostileLt != null)
                                                                     {
                                                                         Navigator.MoveTo(Location);
                                                                         if (Location.Distance(Me.Location) < 3)
                                                                         {
                                                                             WoWMovement.MoveStop();
                                                                             Lua.DoString("RunMacroText(\"/leavevehicle\")");
                                                                         }
                                                                         return;
                                                                     }


                                                                     if (Me.GotTarget)
                                                                     {
                                                                         if (HostileScout == null && Me.CurrentTarget.Entry != 33550 && (!MyMount.ActiveAuras.ContainsKey("Defend") || (MyMount.ActiveAuras.ContainsKey("Defend") && MyMount.ActiveAuras["Defend"].StackCount < 2)))
                                                                         {
                                                                             UsePetSkill("Defend");
                                                                         }


                                                                         var loc = Me.CurrentTarget.Location;
                                                                         //Scouts
                                                                         if (Me.CurrentTarget.Entry == 33550)
                                                                         {
                                                                             if (Me.CurrentTarget.Distance2D < 7)
                                                                             {
                                                                                 //WoWMovement.MoveStop();
                                                                                 //WoWMovement.StopFace();
                                                                                 //WoWMovement.ClickToMove(Me.Location);
                                                                                 WoWMovement.Move(WoWMovement.MovementDirection.Backwards);
                                                                                 Me.CurrentTarget.Face();
                                                                                 UsePetSkill("Shield-Breaker");
                                                                             }
                                                                             else if (Me.CurrentTarget.Distance2D < 20)
                                                                             {
                                                                                 WoWMovement.MoveStop();
                                                                                 //WoWMovement.StopFace();
                                                                                 WoWMovement.ClickToMove(Me.Location);
                                                                                 Me.CurrentTarget.Face();
                                                                                 UsePetSkill("Shield-Breaker");
                                                                             }
                                                                             else
                                                                             {
                                                                                 Navigator.MoveTo(loc);
                                                                             }
                                                                         }
                                                                         else if (Me.CurrentTarget.Entry == 33429) //Lt
                                                                         {
                                                                             if (Me.CurrentTarget.Distance2D < 20)
                                                                             {
                                                                                 WoWMovement.MoveStop();
                                                                                 //WoWMovement.StopFace();
                                                                                 //WoWMovement.ClickToMove(Me.Location);
                                                                                 UsePetSkill("Shield-Breaker");
                                                                                 UsePetSkill("Thrust");
                                                                                 WoWMovement.ClickToMove(loc);
                                                                             }
                                                                             else
                                                                             {
                                                                                 Navigator.MoveTo(loc);
                                                                             }
                                                                         }
                                                                         else if (Me.CurrentTarget.Entry == 33438) //boneguard soldier
                                                                         {
                                                                             /*if (Me.CurrentTarget.Distance2D )
																			 {
																				 WoWMovement.MoveStop();
																				 //WoWMovement.StopFace();
																				 WoWMovement.ClickToMove(Me.Location);
																				 UsePetSkill("Shield-Breaker");
																				 UsePetSkill("Thrust");
																			 }
																			 else
																			 {*/
                                                                             //Navigator.MoveTo(loc);
                                                                             WoWMovement.ClickToMove(loc);
                                                                             //}
                                                                         }
                                                                         else if (Me.CurrentTarget.Entry == 34127)
                                                                         {
                                                                             if (HostileScout != null)
                                                                             {
                                                                                 _pretarget = Me.CurrentTarget;
                                                                                 HostileScout.Target();
                                                                                 return;
                                                                             }

                                                                             var moveTo = WoWMathHelper.CalculatePointAtSide(StyxWoW.Me.CurrentTarget.Location, Me.CurrentTarget.Rotation, 20f, false);
                                                                             if (Navigator.CanNavigateFully(StyxWoW.Me.Location, moveTo))
                                                                             {
                                                                                 Navigator.MoveTo(moveTo);
                                                                             }


                                                                             if (!MyMount.ActiveAuras.ContainsKey("Defend") || (MyMount.ActiveAuras.ContainsKey("Defend") && MyMount.ActiveAuras["Defend"].StackCount < 1))
                                                                             {
                                                                                 UsePetSkill("Defend");
                                                                                 UsePetSkill("Charge");
                                                                             }
                                                                             else
                                                                             {
                                                                                 if (Me.CurrentTarget.Distance > 8)
                                                                                     Me.CurrentTarget.Face();
                                                                                 using (StyxWoW.Memory.AcquireFrame())
                                                                                 {
                                                                                     UsePetSkill("Thrust");
                                                                                     UsePetSkill("Charge");
                                                                                     UsePetSkill("Shield-Breaker");
                                                                                 }
                                                                             }
                                                                         }
                                                                     }
                                                                     else
                                                                     {
                                                                         if (_pretarget != null)
                                                                         {
                                                                             _pretarget.Target();
                                                                             _pretarget = null;
                                                                         }
                                                                         else if (HostileCm != null)
                                                                             HostileCm.Target();
                                                                         else if (HostileScout != null)
                                                                             HostileScout.Target();
                                                                         else if (HostileLt != null)
                                                                             HostileLt.Target();
                                                                         else if (HostileBg != null)
                                                                             HostileBg.Target();
                                                                     }
                                                                 }
                        ))



                    ;
            }
        }


        private readonly WoWPoint[] _spots = {
                                       new WoWPoint(6393.416, 2356.46, 469.2245), new WoWPoint(6448.314, 2181.319, 489.4585),
                                       new WoWPoint(6391.853, 2146.777, 494.4465),new WoWPoint(6270.915, 2245.348, 487.9521),
                                       new WoWPoint(6199.627, 2223.347, 503.8197),
                                   };

        private async Task<bool> LanceUp()
        {
            var mainHand = Me.Inventory.Equipped.MainHand;
            if (mainHand != null && mainHand.Entry == ItemId_ArgentLance)
                return false;

            var lance = ArgentLance;
            if (lance == null)
                QBCLog.Fatal("No Argent Lance in bags");
            else
                lance.UseContainerItem();
            return true;
        }

        private Composite HealUp
        {
            get
            {
                return new Decorator(r => !Me.Combat && MyMount.HealthPercent < 50, new Action(r => UsePetSkill("Refresh Mount")));
            }
        }

        private int _spot = 0;
        private Composite PickFight
        {
            get
            {
                return new Decorator(r => !Me.Combat && !MyMount.Combat, new PrioritySelector(
           new Decorator(r => !Me.IsQuestObjectiveComplete(QuestId, 1), new Action(r =>
                                                                                   {
                                                                                       ObjectManager.Update();
                                                                                       if (!Me.GotTarget || (Me.GotTarget && !Me.CurrentTarget.IsHostile))
                                                                                       {
                                                                                           if (Cm != null)
                                                                                           {
                                                                                               Navigator.PlayerMover.MoveStop();
                                                                                               Cm.Target();
                                                                                               if (Cm.Distance > 20)
                                                                                               {
                                                                                                   Navigator.MoveTo(Cm.Location);
                                                                                               }
                                                                                           }
                                                                                           else
                                                                                           {
                                                                                            //Move to where we can find scouts
                                                                                            Navigator.MoveTo(_spots[_spot]);
                                                                                               if (Me.Location.Distance(_spots[_spot]) < 10)
                                                                                               {
                                                                                                   _spot++;
                                                                                                   if (_spot >= _spots.Length)
                                                                                                       _spot = 0;
                                                                                               }
                                                                                           }
                                                                                       }
                                                                                       else
                                                                                       {
                                                                                        //We have a target, get in range and pull with shield breaker
                                                                                        if (Me.CurrentTarget.Distance > 20)
                                                                                           {
                                                                                               Navigator.MoveTo(Me.CurrentTarget.Location);
                                                                                           }
                                                                                           else
                                                                                           {
                                                                                               QBCLog.Info("in range");
                                                                                               Navigator.PlayerMover.MoveStop();
                                                                                               Me.CurrentTarget.Face();
                                                                                               UsePetSkill("Charge");
                                                                                           }
                                                                                       }
                                                                                   }))

                        ));
            }
        }


        protected Composite CreateBehavior_QuestbotMain()
        {
            return _root ??
                   (_root =
                       new Decorator(
                           ret => !_isBehaviorDone,
                           new PrioritySelector(
                               DoneYet,
                               new ActionRunCoroutine(ctx => LanceUp()),
                               MountUp,
                               BuffUp,
                               HealUp,
                               PickFight,
                               Fight,
                               new ActionAlwaysSucceed())));
        }

        public override void OnFinished()
        {
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            TreeHooks.Instance.RemoveHook("Questbot_Main", CreateBehavior_QuestbotMain());
            base.OnFinished();
        }

        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone     // normal completion
                        || !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
            }
        }

        private WoWItem _mainhand;
        private WoWItem _offhand;
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
                TreeHooks.Instance.InsertHook("Questbot_Main", 0, CreateBehavior_QuestbotMain());

                _mainhand = Me.Inventory.Equipped.MainHand;
                _offhand = Me.Inventory.Equipped.OffHand;

                this.UpdateGoalText(QuestId);
            }
        }
        #endregion
    }
}