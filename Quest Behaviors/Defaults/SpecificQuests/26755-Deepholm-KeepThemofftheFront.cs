using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Honorbuddy.QuestBehaviorCore;

using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.KeepThemofftheFront
{
    [CustomBehaviorFileName(@"SpecificQuests\26755-Deepholm-KeepThemofftheFront")]
    public class KeepThemofftheFront : CustomForcedBehavior
    {
        private WeaponArticulation weaponArticulation;
        private VehicleWeapon rock;



        public KeepThemofftheFront(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                QuestId = 26755;//GetAttributeAsQuestId("QuestId", true, null) ?? 0;
            }
            catch
            {
                Logging.Write("Problem parsing a QuestId in behavior: Rampage Against The Machine");
            }



             weaponArticulation = new WeaponArticulation();
             rock = new VehicleWeapon(1, weaponArticulation);


        }
        public int QuestId { get; set; }
        private bool _isBehaviorDone;



        //<Vendor Name="Stone Trogg Reinforcement" Entry="43960" Type="Repair" X="1047.2" Y="1870.36" Z="305.8879" />
        //<Vendor Name="Fungal Terror" Entry="43954" Type="Repair" X="1070.227" Y="1856.843" Z="303.6579" />


        public uint[] MobIds = new uint[] { 43960, 43954 };


        private Composite _root;
        public QuestCompleteRequirement questCompleteRequirement = QuestCompleteRequirement.NotComplete;
        public QuestInLogRequirement questInLogRequirement = QuestInLogRequirement.InLog;


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

                if (TreeRoot.Current != null && TreeRoot.Current.Root != null && TreeRoot.Current.Root.LastStatus != RunStatus.Running)
                {
                    var currentRoot = TreeRoot.Current.Root;
                    if (currentRoot is GroupComposite)
                    {
                        var root = (GroupComposite)currentRoot;
                        root.InsertChild(0, CreateBehavior());
                    }
                }
                CharacterSettings.Instance.UseMount = false;

                PlayerQuest Quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
                TreeRoot.GoalText = ((Quest != null) ? ("\"" + Quest.Name + "\"") : "In Progress");
            }
        }



        public WoWUnit timmah
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => MobIds.Contains(u.Entry) && u.IsAlive && u.Distance > 20).OrderBy(u => u.Distance).FirstOrDefault();
            }
        }


        public bool IsQuestComplete()
        {
            var quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
            return quest == null || quest.IsCompleted;
        }
        private bool IsObjectiveComplete(int objectiveId, uint questId)
        {
            if (Me.QuestLog.GetQuestById(questId) == null)
            {
                return false;
            }
            int returnVal = Lua.GetReturnVal<int>("return GetQuestLogIndexByID(" + questId + ")", 0);
            return
                Lua.GetReturnVal<bool>(
                    string.Concat(new object[] { "return GetQuestLogLeaderBoard(", objectiveId, ",", returnVal, ")" }), 2);
        }

        public Composite DoneYet
        {
            get
            {
                return
                    new Decorator(ret => IsQuestComplete(), new Action(delegate
                    {
                        TreeRoot.StatusText = "Finished!";


                        Lua.DoString("VehicleExit()");
                        _isBehaviorDone = true;
                        return RunStatus.Success;

                    }));

            }
        }


        private void shoot(WoWUnit who)
        {

            var WeaponChoice = rock;



            var projectileFlightTime = WeaponChoice.CalculateTimeOfProjectileFlight(who.Location);
                var anticipatedLocation = who.AnticipatedLocation(projectileFlightTime);
                var isAimed = WeaponChoice.WeaponAim(anticipatedLocation);

                if (isAimed)
                {
                    WeaponChoice.WeaponFire(anticipatedLocation);


                }
            
        }




        public Composite KillSoldier
        {
            get
            {
                return


                    new Decorator(r => timmah != null,new Action(r => shoot(timmah)));


            }

        }


        public Composite EnsureTarget
        {
            get
            {
                return new Decorator(r => Me.GotTarget && !Me.CurrentTarget.IsHostile, new Action(r => Me.ClearTarget()));
            }
        }

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, EnsureTarget, KillSoldier)));
        }
    }
}



