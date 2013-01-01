using System.Collections.Generic;
using System.Linq;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

namespace DarkSkies
{
    public class DarkSkies : CustomForcedBehavior
    {
        public DarkSkies(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                QuestId = 31216;//GetAttributeAsQuestId("QuestId", true, null) ?? 0;
            }
            catch
            {
                Logging.Write("Problem parsing a QuestId in behavior: Dark Skies");
            }
        }
        public int QuestId { get; set; }
        private bool _isBehaviorDone;

        public uint[] Mobs = new uint[] { 63635, 63613, 63615, 63636, 65455 };
        public uint[] Mobs2 = new uint[] { 63625, 63637 };
        //<Vendor Name="Krik'thik Battletank" Entry="63625" Type="Repair" X="160.1806" Y="3963.259" Z="231.228" />
        //<Vendor Name="Ik'thik Kunchong" Entry="63637" Type="Repair" X="-325.4088" Y="2503.123" Z="145.5118" />
        public int MobIdKunchong = 63625;

        //<Vendor Name="Ik'thik Warrior" Entry="63635" Type="Repair" X="-339.1094" Y="2557.443" Z="138.0953" />
        //<Vendor Name="Ik'thik Slayer" Entry="63636" Type="Repair" X="-339.316" Y="2848.663" Z="136.8539" />

        public int Xaril = 62151;
        private Composite _root;
        public WoWPoint Location = new WoWPoint(138.3817, 225.952, 214.7609);
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

        public WoWUnit Mantid
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>(true).Where(
                        u => Mobs.Contains(u.Entry) && !u.IsDead).OrderBy(u => u.Distance).
                        FirstOrDefault();
            }
        }
        public WoWUnit Kunchong
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>(true).Where(u => Mobs2.Contains(u.Entry) && !u.IsDead).OrderBy(u => u.Distance).FirstOrDefault();
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
                        Lua.DoString("CastPetAction(6)");
                        TreeRoot.StatusText = "Finished!";
                        _isBehaviorDone = true;
                        return RunStatus.Success;
                    }));

            }
        }


        public Composite KillOne
        {
            get
            {
                return new Decorator(r => !IsObjectiveComplete(3, (uint)QuestId) && Kunchong != null, new Action(r =>
                                                                                                {
                                                                                                    Lua.DoString(
                                                                                                        "CastPetAction(2)");
                                                                                                    SpellManager.
                                                                                                        ClickRemoteLocation
                                                                                                        (Kunchong.
                                                                                                             Location);
                                                                                                    Lua.DoString(
                                                                                                        "CastPetAction(1)");
                                                                                                    SpellManager.
                                                                                                        ClickRemoteLocation
                                                                                                        (Kunchong.
                                                                                                             Location);
                                                                                                }));
            }
        }


        public Composite KillTwo
        {
            get
            {
                return new Decorator(r => !IsObjectiveComplete(2, (uint)QuestId) && Mantid != null, new Action(r =>
                {
                    Lua.DoString("CastPetAction(1)");
                    SpellManager.ClickRemoteLocation(Mantid.Location);
                }));
            }
        }



        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, KillOne, KillTwo, new ActionAlwaysSucceed())));
        }
    }
}
