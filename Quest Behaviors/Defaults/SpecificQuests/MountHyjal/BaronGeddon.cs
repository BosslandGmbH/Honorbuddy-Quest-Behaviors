// Behavior originally contributed by Bobby53.
//
// DOCUMENTATION:
//     
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using CommonBehaviors.Actions;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.MountHyjal.BaronGeddon
{
    /// <summary>
    /// Allows safely completing the http://www.wowhead.com/quest=25464 .  Can also be used
    /// on similar quest if one discovered.
    /// 
    /// Moves to XYZ
    /// Locates MobId
    /// If MobId has AuraId, run to XYZ
    /// Otherwise run to MobId and use ItemId
    /// At end, waits for Living Bomb before continuing
    /// 
    /// Note: to minimize damage, it will cast ItemId for a max of 5 seconds 
    /// then run to xyz and wait even if no aura is present.  the duration betwen
    /// aoe casts (aura present) varies and waiting for it to appear before
    /// running out results in a very weak toon (and possible death from living bomb)
    /// 
    /// ##Syntax##
    /// QuestId: The id of the quest.
    /// [Optional] MobId: The id of the object.
    /// [Optional] ItemId: The id of the item to use.
    /// [Optional] AuraId: Spell id of the aura on MobId that signals we should run
    /// [Optional] CollectionDistance: distance at xyz to search for MobId
    /// [Optional] Range: Distance to use item at
    /// X,Y,Z: safe point (location we run to when target has auraid) must be in LoS of MobId
    /// </summary>
    [CustomBehaviorFileName(@"SpecificQuests\MountHyjal\BaronGeddon")]
    public class BaronGeddon : CustomForcedBehavior
    {
        public BaronGeddon(Dictionary<string, string> args)
            : base(args)
        {

                QuestId = 25464;//GetAttributeAsQuestId("QuestId", true, null) ?? 0;
 
        }
        public int QuestId { get; set; }
        private bool _isBehaviorDone;

        //<Vendor Name="Zhao-Ren" Entry="55786" Type="Repair" X="713.9167" Y="4168.126" Z="213.846" />
        //<Vendor Name="Baron Geddon" Entry="40147" Type="Repair" X="5434.25" Y="-2800.19" Z="1516.105" />

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

                PlayerQuest Quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
                TreeRoot.GoalText = ((Quest != null) ? ("\"" + Quest.Name + "\"") : "In Progress");
            }
        }


        public WoWUnit Barron
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>(true).FirstOrDefault(u => u.Entry == 40147);
            }
        }

        public WoWItem Rod
        {
            get { return StyxWoW.Me.BagItems.FirstOrDefault(r => r.Entry == 54463); }
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
                    new Decorator(ret => IsQuestComplete() && safe.Distance(Me.Location) < 3 && !Me.Combat, new Action(delegate
                    {
                        TreeRoot.StatusText = "Finished!";
                        _isBehaviorDone = true;
                        return RunStatus.Success;
                    }));

            }
        }

        //Safe
        //<Vendor Name="dd" Entry="0" Type="Repair" X="" />
        WoWPoint safe = new WoWPoint(5410.753,-2771.448,1516.072);
        //Attack
        //<Vendor Name="dd" Entry="0" Type="Repair" X="" />
        WoWPoint attack = new WoWPoint(5417.539,-2792.542,1515.283);
        public Composite DpsHim
        {
            get
            {
                return new Decorator(r => !Barron.HasAura("Inferno"), new PrioritySelector(
                    
                    new Decorator(r=>attack.Distance(Me.Location) > 3, new Action(r=>Navigator.MoveTo(attack))),
                    //new Decorator(r=>!Me.GotTarget || Me.CurrentTarget != Barron, new Action(r=>Barron.Target())),
                    new Decorator(r=> Me.IsCasting || Me.IsChanneling, new ActionAlwaysSucceed()),
                    new Decorator(r=> Rod != null && Rod.Cooldown <= 0, new Action(r=>Rod.Use(Barron.Guid)))

                    
                    
                    ));
            }
        }
        public Composite RunAway
        {
            get
            {
                return new Decorator(r => Barron == null || Barron.HasAura("Inferno") || IsQuestComplete(),

                                     new Decorator(r => safe.Distance(Me.Location) > 3,new Action(r => Navigator.MoveTo(safe))));

            }
        }




        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet,RunAway,DpsHim, new ActionAlwaysSucceed())));
        }
        
    }
}
