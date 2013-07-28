using System.Collections.Generic;
using System.Linq;

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Database;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Profiles.Quest.Order;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Honorbuddy.Quest_Behaviors.ForceSetVendor;
using Honorbuddy.Quest_Behaviors.InteractWith;
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
        //Horde
        private static int HordeOwFlightVendor = 44918;
        private static int HordeHfFlightVendor = 35099;
        private static int HordeFlightMount = 25474;

        private static int BloodElfVendor;
        private static int OrcVendor;
        private static int TaurenVendor;
        private static int TrollVendor;
        private static int GoblinVendor;
        private static int UndeadVendor;
        private static int HPandaVendor;

        //Alliance
        private static int AllianceOwFlightVendor = 43768;
        private static int AllianceHfFlightVendor = 35099;
        private static int AllianceFlightMount = 25471;

        private static int HumanVendor = 384;
        private static int DwarfVendor = 1261;
        private static int NightElfVendor;
        private static int GnomeVendor = 7955;
        private static int GoatVendor;
        private static int WorgenVendor;
        private static int APandaVendor = 65068; //http://www.wowhead.com/npc=65068

        private static int HumanVendorMount = 2411;
        private static int DwarfVendorMount = 5873;
        private static int NightElfVendorMount;
        private static int GnomeVendorMount = 8595;
        private static int GoatVendorMount;
        private static int WorgenVendorMount;
        private static int APandaVendorMount = 87795; //http://www.wowhead.com/npc=65068



        #endregion

        private static bool CanLearn
        {
            get
            {
                
                var gby = Me.BagItems.FirstOrDefault(r => r.Entry == 25474);

                if (gby != null)
                {
                    //Ignore the spell 'learning'
                    var mountId = gby.ItemInfo.SpellId.FirstOrDefault(r => r != 55884);
                    
                    if (Mount.Mounts.Any(r => r.CreatureSpellId == mountId))
                        return false;
                    return true;
                }

                return false;
            }
        }
        private static int FlightLevel
        {
            get
            {
                

                if (SpellManager.HasSpell(ColdWeatherFlying))
                    return 3;

                if (SpellManager.HasSpell(ExpertRiding))
                    return 2;

                if (SpellManager.HasSpell(ApprenticeRiding) || SpellManager.HasSpell(JourneyManRiding))
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
        private static int MountId
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

        private static int VendorId
        {
            get
            {
                if (OldWorld)
                {
                    if (Me.Level >= 60)
                    {
                        if (Me.IsAlliance)
                        {
                            return AllianceOwFlightVendor;
                        }
                        return HordeOwFlightVendor;
                    }
                    else
                    {
                        if (Me.IsAlliance)
                        {
                            return AllianceOwFlightVendor;
                        }
                        return HordeOwFlightVendor;
                    }
                }
                if (Hellfire)
                {
                    return Me.IsAlliance ? AllianceFlight : HordeFlight;
                }


                return 0;
            }
        }

        //Races that give us problems:
        //Horde
        //Blood elf -> Silvermoon
        //Undead -> Undercity
        //Alliance
        //Goats -> Goat land
        //Night elf ->Darnassus
        //Worgen -> ???


        private static void SetupTrainer()
        {
            Logging.Write(System.Windows.Media.Colors.Aquamarine, "Creating ForceTrainRiding object");
            var args =  new Dictionary<string,string> {{"MobId", TrainerId.ToString()}};

            gooby = new ForceTrainRiding(args);
            gooby.OnStart();
            TreeHooks.Instance.ReplaceHook("GoobyHook", gooby.Branch);
        }

        private static void SetupPurchaseMount()
        {
            Logging.Write(System.Windows.Media.Colors.Aquamarine, "Creating InteractWith object");
            var args = new Dictionary<string, string> { { "MobId", TrainerId.ToString() } };

            gooby = new InteractWith.InteractWith(args);
            gooby.OnStart();
            TreeHooks.Instance.ReplaceHook("GoobyHook", gooby.Branch);
        }

        private static void CleanUpCustomForcedBehavior()
        {
            Logging.Write(System.Windows.Media.Colors.Aquamarine, "Cleaningup CustomForcedBehavior object");
            TreeHooks.Instance.RemoveHook("GoobyHook", gooby.Branch);
            gooby.Dispose();
            gooby = null;
        }


        //Composites
        private static CustomForcedBehavior gooby = null;
        private static Composite runOtherComposite
        {
            get
            {
                return new Decorator(r=> gooby != null, 
                new PrioritySelector(
                    new Decorator(r => gooby.IsDone, new Action(r => CleanUpCustomForcedBehavior())),
                    new Decorator(r => !gooby.IsDone, new HookExecutor("GoobyHook"))
            ));
            }
        }


        private static Composite BuyMount
        {
            get
            {

                
                return new PrioritySelector(
                    new Decorator(r=> FlightLevel == 1 && !Mount.GroundMounts.Any(), new Action()),
                    new Decorator(r=> FlightLevel == 1 && !Mount.GroundMounts.Any(), new Action())
                    
                    );

            }
        }

        private static uint VendorTarget;
        private static NpcResult RidingTrainer { get { return (NpcQueries.GetNpcById(VendorTarget)); } }
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
                    _myHook = new Decorator(r => !Me.Combat, new PrioritySelector(runOtherComposite, OldWordComposite, HellfireComposite));
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
