using System.Collections.Generic;
using System.Linq;

using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Profiles.Quest.Order;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Honorbuddy.Quest_Behaviors.ForceSetVendor;
using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.MountHook
{
    [CustomBehaviorFileName(@"MountHook")]
    [CustomBehaviorFileName(@"SpecificQuests\MountHook")]
    public class MountHook : CustomForcedBehavior
    {
        public MountHook(Dictionary<string, string> args)
            : base(args)
        {

            QuestId = 0;//GetAttributeAsQuestId("QuestId", true, null) ?? 0;
            //True = hook running, false = hook stopped
            state = GetAttributeAsNullable<bool>("state", true, null, null) ?? false;

        }
        public int QuestId { get; set; }
        private bool _isBehaviorDone;


        private bool state;

        private Composite _root;

        public QuestCompleteRequirement questCompleteRequirement = QuestCompleteRequirement.NotComplete;
        public QuestInLogRequirement questInLogRequirement = QuestInLogRequirement.InLog;

        public override bool IsDone
        {
            get
            {
                return inserted;
            }
        }

        private bool inserted = false;
        private static LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
        }


        private static int ApprenticeRiding = 33388;//20
        private static int JourneyManRiding = 33391;//40
        private static int ExpertRiding = 34090;//60
        private static int ColdWeatherFlying = 54197;//68
        private static int FlightMastersLic = 90267;//60


        #region Trainers
        //Hellfire
        private static int HordeFlight = 35093;
        private static int AllianceFlight = 35100;

        //Stormwind
        private static int AllianceLowbie = 43769;
        //Orgimar
        private static int HordieLowbie = 44919;
        #endregion

        #region Vendors
        //Hellfire


        //Stormwind

        //Orgimar


        //Horde mounts

        //Alliance mounts

        #endregion


        private static int FlightLevel
        {
            get
            {
                if (SpellManager.HasSpell(ColdWeatherFlying))
                    return 3;

                if (SpellManager.HasSpell(ExpertRiding))
                    return 2;

                if (SpellManager.HasSpell(ApprenticeRiding))
                    return 1;

                return 0;

            }

        }

        private static bool Hellfire
        {
            get
            {
                return StyxWoW.Me.MapId == 530;
            }
        }

        private static bool OldWorld
        {
            get
            {
                return (StyxWoW.Me.MapId == 0 || StyxWoW.Me.MapId == 1);
            }
        }

        //Return the trainer we want based on faction and location and skill.
        private static int TrainerId
        {
            get
            {
                if (OldWorld)
                {
                    return Me.IsAlliance ? AllianceLowbie : HordieLowbie;
                }
                if (Hellfire)
                {
                    return Me.IsAlliance ? AllianceFlight : HordeFlight;
                }


                return 0;
            }
        }

        private static void SetupTrainer()
        {
            Logging.Write(System.Windows.Media.Colors.Aquamarine,"Creating ForceTrainRiding object");
            var args =  new Dictionary<string,string> {{"MobId", TrainerId.ToString()}};

            gooby = new ForceTrainRiding(args);
            gooby.OnStart();
            TreeHooks.Instance.ReplaceHook("GoobyHook", gooby.Branch);
        }

        private static void CleanUpTrainer()
        {
            Logging.Write(System.Windows.Media.Colors.Aquamarine, "Cleaningup ForceTrainRiding object");
            TreeHooks.Instance.RemoveHook("GoobyHook", gooby.Branch);
            gooby.Dispose();
            gooby = null;
        }


        //Composites
        private static ForceTrainRiding gooby = null;
        private static Composite TrainRiding
        {
            get
            {
                return new Decorator(r=> gooby != null, 
                new PrioritySelector(
                    new Decorator(r => gooby.IsDone, new Action(r => CleanUpTrainer())),
                    new Decorator(r => !gooby.IsDone, new HookExecutor("GoobyHook"))
            ));
            }
        }

        private static Composite HellfireComposite
        {
            get
            {
                return new Decorator(r => Hellfire && Me.Level >= 60 && Me.Gold >= 268 && FlightLevel < 2, new Action(r => SetupTrainer()));

            }
        }

        private static Composite OldWordComposite
        {
            get
            {
                return new Decorator(r => OldWorld && ((Me.Level >= 20 && Me.Gold >= 5 && FlightLevel < 1) || (Me.Level >= 60 && Me.Gold >= 268 && FlightLevel < 2)), new Action(r => SetupTrainer()));

            }
        }

        public static Composite _myHook;
        public static Composite myHook
        {
            get
            {
                if (_myHook == null)
                {
                    _myHook = new Decorator(r => !Me.Combat, new PrioritySelector(TrainRiding, OldWordComposite, HellfireComposite));
                    return _myHook;
                }
                else
                {
                    return _myHook;
                }
            }
            set
            {
                _myHook = value;
            }
        }

        public override void OnStart()
        {
            OnStart_HandleAttributeProblem();

            Logging.Write("FlyHook:{0}  - zxbca", state);
            if (state == true)
            {
                if (_myHook == null)
                {
                    Logging.Write("FlyHook:Inserting hook - zxbca");
                    TreeHooks.Instance.InsertHook("Questbot_Main", 0, myHook);
                }
                else
                {
                    Logging.Write("FlyHook:Insert was requested, but was already present - zxbca");
                }
                

            }
            else
            {
                if (_myHook != null)
                {
                    Logging.Write("FlyHook:Removing hook - zxbca");
                    TreeHooks.Instance.RemoveHook("Questbot_Main", myHook);
                    myHook = null;
                }
                else
                {
                    Logging.Write("FlyHook:Remove was requested, but hook was not present - zxbca");
                }

            }


            /*if (_myHook == null)
            {
                Logging.Write("BlackrockMaskHook:Inserting hook - gfrsa");
                TreeHooks.Instance.InsertHook("Questbot_Main", 0, myHook);
            }
            else
            {
                Logging.Write("BlackrockMaskHook:Removing hook - gfrsa");
                TreeHooks.Instance.RemoveHook("Questbot_Main", myHook);
                myHook = null;
            }*/
            inserted = true;

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
                        _isBehaviorDone = true;
                        return RunStatus.Success;
                    }));

            }
        }








    }
}
