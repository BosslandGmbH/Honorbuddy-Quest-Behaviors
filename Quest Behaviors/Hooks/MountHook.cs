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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Bots.DungeonBuddy.Helpers;
using Bots.Quest;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using Honorbuddy.Quest_Behaviors.ForceSetVendor;
using Styx.Common.Helpers;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Profiles.Quest.Order;
using Styx.Pathing;
using Styx.WoWInternals;
using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.Hooks
{
    [CustomBehaviorFileName(@"Hooks\MountHook")]
    public class MountHook : CustomForcedBehavior
    {
        public MountHook(Dictionary<string, string> args)
            : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            //True = hook running, false = hook stopped
            _state = GetAttributeAsNullable<bool>("state", true, null, null) ?? false;
        }

        // DON'T EDIT THIS--it is auto-populated by Git
        public override string VersionId => QuestBehaviorBase.GitIdToVersionId("$Id");

        private bool _state;

        public override bool IsDone { get { return true; } }

        private static LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
        }

        private readonly ProfileHelperFunctionsBase _profileHelpers = new ProfileHelperFunctionsBase();

        #region Trainers
        //Hellfire
        private const int HordeFlight = 35093;
        private const int AllianceFlight = 35100;

        private const int StormwindTrainer = 43769;
        private const int OrgrimmarTrainer = 44919;
        private const int UndercityTrainer = 4773;
        #endregion

        private RidingLevelType RidingLevel => (RidingLevelType)Me.GetSkill(SkillLine.Riding).CurrentValue;

        private const int HellfirePeninsulaId = 530;
        private const int KalimdorId = 1;
        private const int EasternKingdomsId = 0;

        private bool InHellfire => Me.MapId == HellfirePeninsulaId;

        private bool InKalimdor => Me.MapId == KalimdorId;

        private bool InEasternKingdoms => Me.MapId == EasternKingdomsId;

        private bool InOldWorld => InKalimdor || InEasternKingdoms;

        //Return the trainer we want based on faction and location and skill.
        private int GetTrainerId()
        {
            if (InKalimdor)
            {
                return Me.IsAlliance ? 0 : OrgrimmarTrainer;
            }

            if (InEasternKingdoms)
            {
                // Horde can train ground mounts in Undercity
                bool isGround = RidingLevel < RidingLevelType.JourneyManRiding;
                if (isGround && Me.IsHorde)
                    return UndercityTrainer;

                return Me.IsAlliance ? StormwindTrainer : 0;
            }

            if (InHellfire)
            {
                return Me.IsAlliance ? AllianceFlight : HordeFlight;
            }

            return 0;
        }

        //Races that give us problems:
        //Horde
        //Blood elf -> Silvermoon
        //Undead -> Undercity
        //Alliance
        //Goats -> Goat land
        //Night elf ->Darnassus
        //Worgen -> ???


        private bool TrainInOldWorld
        {
            get
            {
                var ridingLevel = RidingLevel;
                return InOldWorld && ((Me.Level >= 20 && Me.Gold >= 5 && ridingLevel < RidingLevelType.ApprenticeRiding)
                    || (Me.Level >= 40 && Me.Gold >= 55 && ridingLevel < RidingLevelType.JourneyManRiding)
                    || (Me.Level >= 60 && Me.Gold >= 278 && ridingLevel < RidingLevelType.ExpertRiding));
            }
        }

        private bool TrainInOutland { get { return InHellfire && Me.Level >= 60 && Me.Gold >= 278 && RidingLevel < RidingLevelType.ExpertRiding; } }

        public override void OnStart()
        {
            OnStart_HandleAttributeProblem();

            if (_state == true)
            {
                if (_myHook == null)
                {
                    QBCLog.Info("Inserting hook");
                    _myHook = CreateHook();
                    TreeHooks.Instance.InsertHook("Questbot_Profile", 0, _myHook);
                }
                else
                {
                    QBCLog.Info("Insert was requested, but was already present");
                }
            }
            else
            {
                if (_myHook != null)
                {
                    QBCLog.Info("Removing hook");
                    TreeHooks.Instance.RemoveHook("Questbot_Profile", _myHook);
                    _myHook = null;
                }
                else
                {
                    QBCLog.Info("Remove was requested, but hook was not present");
                }
            }
        }


        public static Composite _myHook;
        public Composite CreateHook()
        {
            return new ActionRunCoroutine(ctx => HookHelpers.ExecuteHook(this, MainCoroutine));
        }

        private async Task<bool> MainCoroutine()
        {
            if (await TrainMount())
                return true;

            if (await PurchaseMount())
                return true;

            return false;
        }

        #region PurchaseMountBehavior

        private readonly WaitTimer _purchasedMountTimer = new WaitTimer(TimeSpan.FromMinutes(5));

        private bool _purchaseMount;
        private async Task<bool> PurchaseMount()
        {
            // Worgens have a ground mount from racial, paladin and warlock have class based mounts so
            // they do not need to purchase any ground mounts
            if (RidingLevel > RidingLevelType.None && Me.Race != WoWRace.Worgen && Me.Class != WoWClass.Paladin && Me.Class != WoWClass.Warlock)
            {
                // _purchasedMountTimer pervents double purchasing multiple mounts because Mount.GroundMounts is cached.
                if (!Mount.GroundMounts.Any() && _purchasedMountTimer.IsFinished)
                {
                    _purchaseMount = true;
                    return await PurchaseGroundMount();
                }

                // we need to hearth after purchasing our mount
                if (_purchaseMount)
                {
                    var onCooldown = false;
                    await UtilityCoroutine.UseHearthStone(
                        hearthOnCooldownAction: () => onCooldown = true,
                        hearthCastedAction: () => _purchaseMount = false,
                        inHearthAreaAction: () => _purchaseMount = false);

                    if (onCooldown)
                    {
                        TreeRoot.StatusText = "Waiting on Hearthstone cooldown";
                        return true;
                    }
                }
            }

            // Druids have flightform so do not need to purchase a flying mount.
            if (RidingLevel >= RidingLevelType.ExpertRiding && Me.Class != WoWClass.Druid && !Mount.FlyingMounts.Any() && _purchasedMountTimer.IsFinished)
                return await PurchaseFlyingMount();

            return false;
        }

        private async Task<bool> PurchaseGroundMount()
        {
            return await (Me.IsAlliance ? PurchaseGroundMount_Alliance() : PurchaseGroundMount_Horde());
        }

        private async Task<bool> PurchaseFlyingMount()
        {
            return await (Me.IsAlliance ? PurchaseFlyingMount_Alliance() : PurchaseFlyingMount_Horde());
        }


        #region Alliance

        private const int AreaId_StormwindInnkeeper = 5148;

        private const int QuestId_LearnToRide_Human = 32618;
        private const int QuestId_LearnToRide_Pandaren = 32665;
        private const int QuestId_LearnToRide_Gnome = 32663;
        private const int QuestId_LearnToRide_Dwarf = 32662;
        private const int QuestId_LearnToRide_NightElf = 32664;
        private const int QuestId_LearnToRide_Draenei = 32664;
        private const int QuestId_LearnToRideAtTheExodar = 14082;

        private const int ItemId_PintoBridle = 2414;
        private const int ItemId_ReinsoftheBlackDragonTurtle = 87795;
        private const int ItemId_BlueMechanostrider = 8595;
        private const int ItemId_WhiteRam = 5873;
        private const int ItemId_ReinsOfTheStripedNightsaber = 8629;
        private const int ItemId_BrownElekk = 28481;
        private const int ItemId_SnowyGryphon = 25472;

        private const int MobId_InnkeeperAllison = 6740;
        private const int MobId_RandalHunter = 4732;
        private const int MobId_KatieHunter = 384;
        private const int MobId_MeiLin = 70296;
        private const int MobId_OldWhitenose = 65068;
        private const int MobId_BinjyFeatherwhistle = 7954;
        private const int MobId_MilliFeatherwhistle = 7955;
        private const int MobId_UlthamIronhorn = 4772;
        private const int MobId_VeronAmberstill = 1261;
        private const int MobId_Jartsam = 4753;
        private const int MobId_Lelanai = 4730;
        private const int MobId_Aalun = 20914;
        private const int MobId_ToralliusThePackHandler = 17584;
        private const int MobId_GrundaBronzewing = 35101;
        private const int MobId_TannecStonebeak = 43768;

        private const int GameObjectId_Ship_TheBravery = 176310;
        private const int GameObjectId_PortalToExodar = 207995;

        private readonly WoWPoint _stormwindInnkeeperLoc = new WoWPoint(-8867.786, 673.6729, 97.90324);
        private readonly WoWPoint _randalHunterLoc = new WoWPoint(-9442.742, -1390.666, 46.87045);
        private readonly WoWPoint _katieHunterLoc = new WoWPoint(-9455.365, -1385.327, 47.12818);
        private readonly WoWPoint _meiLinLoc = new WoWPoint(-8212.221, 547.569, 117.1947);
        private readonly WoWPoint _oldWhitenoseLoc = new WoWPoint(-8209.379, 546.0261, 117.7684);
        private readonly WoWPoint _binjyFeatherwhistleLoc = new WoWPoint(-5454.171, -621.048, 393.3968);
        private readonly WoWPoint _milliFeatherwhistleLoc = new WoWPoint(-5454.171, -621.048, 393.3968);
        private readonly WoWPoint _ulthamIronhornLoc = new WoWPoint(-5524.354, -1349.868, 398.6641);
        private readonly WoWPoint _veronAmberstillLoc = new WoWPoint(-5539.55, -1322.55, 398.8653);
        private readonly WoWPoint _jartsamLoc = new WoWPoint(10129.78, 2526.595, 1324.828);
        private readonly WoWPoint _lelanaiLoc = new WoWPoint(10129.91, 2533.245, 1323.271);
        private readonly WoWPoint _aalunLoc = new WoWPoint(-3981.769, -11929.14, -0.2419412);
        private readonly WoWPoint _toralliusThePackHandlerLoc = new WoWPoint(-3981.769, -11929.14, -0.2419412);
        private readonly WoWPoint _grundaBronzewingLoc = new WoWPoint(-674.4774, 2743.128, 93.9173);
        private readonly WoWPoint _tannecStonebeakLoc = new WoWPoint(-8829.18, 482.34, 109.616);

        private readonly WoWPoint _theBraveryStartLoc = new WoWPoint(-8650.719, 1346.051, -0.0382334);
        private readonly WoWPoint _theBraveryEndLoc = new WoWPoint(8162.587, 1005.365, 0.0474023);
        private readonly WoWPoint _theBraveryWaitAtLoc = new WoWPoint(-8640.556, 1330.829, 5.233207);
        private readonly WoWPoint _theBraveryStandAtLoc = new WoWPoint(-8644.952, 1348.11, 6.143094);
        private readonly WoWPoint _theBraveryGetOffAtLoc = new WoWPoint(8177.54, 1003.079, 6.646164);

        private readonly WoWPoint _exodarPortalLoc = new WoWPoint(9655.252, 2509.33, 1331.598);

        private async Task<bool> PurchaseGroundMount_Alliance()
        {
            switch (Me.Race)
            {
                case WoWRace.Human:
                    return await TurninQuestAndBuyMount(
                                MobId_RandalHunter,
                                _randalHunterLoc,
                                QuestId_LearnToRide_Human,
                                MobId_KatieHunter,
                                _katieHunterLoc,
                                ItemId_PintoBridle);
                case WoWRace.Pandaren:
                    return await TurninQuestAndBuyMount(
                                MobId_MeiLin,
                                _meiLinLoc,
                                QuestId_LearnToRide_Pandaren,
                                MobId_OldWhitenose,
                                _oldWhitenoseLoc,
                                ItemId_ReinsoftheBlackDragonTurtle);
                case WoWRace.Gnome:
                    return await TurninQuestAndBuyMount(
                                MobId_BinjyFeatherwhistle,
                                _binjyFeatherwhistleLoc,
                                QuestId_LearnToRide_Gnome,
                                MobId_MilliFeatherwhistle,
                                _milliFeatherwhistleLoc,
                                ItemId_BlueMechanostrider);
                case WoWRace.Dwarf:
                    return await TurninQuestAndBuyMount(
                                MobId_UlthamIronhorn,
                                _ulthamIronhornLoc,
                                QuestId_LearnToRide_Dwarf,
                                MobId_VeronAmberstill,
                                _veronAmberstillLoc,
                                ItemId_WhiteRam);
                case WoWRace.NightElf:
                    if (InEasternKingdoms)
                    {
                        return await UtilityCoroutine.UseTransport(
                                    GameObjectId_Ship_TheBravery,
                                    _theBraveryStartLoc,
                                    _theBraveryEndLoc,
                                    _theBraveryWaitAtLoc,
                                    _theBraveryStandAtLoc,
                                    _theBraveryGetOffAtLoc);
                    }

                    if (!InKalimdor)
                        return false;

                    return await TurninQuestAndBuyMount(
                                MobId_Jartsam,
                                _jartsamLoc,
                                QuestId_LearnToRide_NightElf,
                                MobId_Lelanai,
                                _lelanaiLoc,
                                ItemId_ReinsOfTheStripedNightsaber);

                case WoWRace.Draenei:
                    if (InEasternKingdoms)
                    {
                        return await UtilityCoroutine.UseTransport(
                                    GameObjectId_Ship_TheBravery,
                                    _theBraveryStartLoc,
                                    _theBraveryEndLoc,
                                    _theBraveryWaitAtLoc,
                                    _theBraveryStandAtLoc,
                                    _theBraveryGetOffAtLoc);
                    }

                    // port over to Exodar
                    if (InKalimdor && Me.ZoneId != 3557)
                    {
                        var portal = ObjectManager.GetObjectsOfType<WoWGameObject>()
                            .FirstOrDefault(g => g.Entry == GameObjectId_PortalToExodar);

                        if (portal == null || !portal.WithinInteractRange)
                            return await (UtilityCoroutine.MoveTo(portal != null ? portal.Location : _exodarPortalLoc, "Exodar portal"));

                        portal.Interact();
                        await CommonCoroutines.SleepForLagDuration();
                        return true;
                    }
                    if (Me.ZoneId != 3557)
                        return false;

                    // Turnin the 'Learn To Ride At The Exodar' quest if in log
                    if (_profileHelpers.HasQuest(QuestId_LearnToRideAtTheExodar) && _profileHelpers.IsQuestCompleted(QuestId_LearnToRideAtTheExodar))
                        return await UtilityCoroutine.TurninQuest(MobId_Aalun, _aalunLoc, QuestId_LearnToRideAtTheExodar);

                    return await TurninQuestAndBuyMount(
                        MobId_Aalun,
                        _aalunLoc,
                        QuestId_LearnToRide_Draenei,
                        MobId_ToralliusThePackHandler,
                        _toralliusThePackHandlerLoc,
                        ItemId_BrownElekk);
            }

            return false;
        }

        private async Task<bool> PurchaseFlyingMount_Alliance()
        {
            if (Me.MapId == 530)
                return await BuyMount(MobId_GrundaBronzewing, _grundaBronzewingLoc, ItemId_SnowyGryphon);

            if (Me.MapId == 0)
                return await BuyMount(MobId_TannecStonebeak, _tannecStonebeakLoc, ItemId_SnowyGryphon);

            return false;
        }

        #endregion

        #region Horde
        private const int QuestId_LearnToRide_Undead = 32672;
        private const int QuestId_LearnToRide_HordePanda = 32667;

        private const int AreaId_OrgrimmarInnkeeper = 5170;
        private const int ZoneId_SilverMoonCity = 3487;
        private const int ZoneId_EversongWoods = 3430;

        private const int MobId_InnkeeperGryshka = 6929;
        private const int MobId_OgunaroWolfrunner = 3362;
        private const int MobId_KallWorthaton = 48510;
        private const int MobId_Zjolnir = 7952;
        private const int MobId_HarbClawhoof = 3685;
        private const int MobId_ZachariahPost = 4731;
        private const int MobId_Winaestra = 16264;
        private const int MobId_VelmaWarnam = 4773;
        private const int MobId_Softpaws = 70301;
        private const int MobId_TurtlemasterOdai = 66022;
        private const int MobId_BanaWildmane = 35099;
        private const int MobId_Drakma = 44918;

        private const int ItemId_HornOfTheDireWolf = 5665;
        private const int ItemId_GoblinTrikeKey = 62461;
        private const int ItemId_WhistleOfTheEmeraldRaptor = 8588;
        private const int ItemId_GrayKodo = 15277;
        private const int ItemId_BlackSkeletalHorse = 46308;
        private const int ItemId_BlackHawkstrider = 29221;
        private const int ItemId_ReinsOfTheGreenDragonTurtle = 91004;
        private const int ItemId_TawnyWindRider = 25474;

        private readonly WoWPoint _orgrimmarInnkeeperLoc = new WoWPoint(1573.266, -4439.158, 16.05631);
        private readonly WoWPoint _ogunaroWolfrunnerLoc = new WoWPoint(2076.602, -4568.632, 49.25319);
        private readonly WoWPoint _kallWorthatonLoc = new WoWPoint(1475.32, -4140.98, 52.51);
        private readonly WoWPoint _zjolnirLoc = new WoWPoint(-852.78, -4885.40, 22.03);
        private readonly WoWPoint _harbClawhoofLoc = new WoWPoint(-2279.796, -392.0697, -9.396863);
        private readonly WoWPoint _zachariahPostLoc = new WoWPoint(2275.08, 237.00, 33.69);
        private readonly WoWPoint _winaestraLoc = new WoWPoint(9244.59, -7491.566, 36.91401);
        private readonly WoWPoint _velmaWarnamLoc = new WoWPoint(2275.08, 236.997, 33.69074);
        private readonly WoWPoint _softpawsLoc = new WoWPoint(2010.891, -4722.866, 29.3442);
        private readonly WoWPoint _turtlemasterOdaiLoc = new WoWPoint(2009.267, -4721.249, 29.51483);
        private readonly WoWPoint _banaWildmaneLoc = new WoWPoint(47.76153, 2742.022, 85.27119);
        private readonly WoWPoint _drakmaLoc = new WoWPoint(1806.94, -4340.67, 102.0506);

        private readonly WoWPoint _theThundercallerKalimdorLoc = new WoWPoint(1833.509, -4391.543, 152.7679);
        private readonly WoWPoint _theThundercallerKalimdorWaitLoc = new WoWPoint(1845.187, -4395.555, 135.2306);
        private readonly WoWPoint _theThundercallerKalimdorBoardLoc = new WoWPoint(1835.509, -4385.785, 135.0436);
        private readonly WoWPoint _theThundercallerEKLoc = new WoWPoint(2062.376, 292.998, 114.973);
        private readonly WoWPoint _theThundercallerEKWaitLoc = new WoWPoint(2065.049, 283.1381, 97.03156);
        private readonly WoWPoint _theThundercallerEKBoardLoc = new WoWPoint(2067.672f, 294.2617f, 97.20473f);

        private readonly WoWPoint _silvermoonCityPortalLoc = new WoWPoint(1805.877, 345.0006, 70.79002);

        private const int GameObjectId_Ship_TheThundercaller = 164871;
        private const uint GameObjectId_OrbOfTranslocation = 184503;

        private async Task<bool> PurchaseGroundMount_Horde()
        {
            Func<Task<MoveToAreaResult>> moveToArea;
            Func<Task<bool>> buyMount;

            switch (Me.Race)
            {
                case WoWRace.Orc:
                    moveToArea = MoveToKalimdorHorde;
                    buyMount = () => BuyMount(MobId_OgunaroWolfrunner, _ogunaroWolfrunnerLoc, ItemId_HornOfTheDireWolf);
                    break;
                case WoWRace.Goblin:
                    moveToArea = MoveToKalimdorHorde;
                    buyMount = () => BuyMount(MobId_KallWorthaton, _kallWorthatonLoc, ItemId_GoblinTrikeKey);
                    break;
                case WoWRace.Troll:
                    moveToArea = MoveToKalimdorHorde;
                    buyMount = () => BuyMount(MobId_Zjolnir, _zjolnirLoc, ItemId_WhistleOfTheEmeraldRaptor);
                    break;
                case WoWRace.Tauren:
                    moveToArea = MoveToKalimdorHorde;
                    buyMount = () => BuyMount(MobId_HarbClawhoof, _harbClawhoofLoc, ItemId_GrayKodo);
                    break;
                case WoWRace.Pandaren:
                    moveToArea = MoveToKalimdorHorde;

                    buyMount = () => TurninQuestAndBuyMount(
                        MobId_Softpaws,
                        _softpawsLoc,
                        QuestId_LearnToRide_HordePanda,
                        MobId_TurtlemasterOdai,
                        _turtlemasterOdaiLoc,
                        ItemId_ReinsOfTheGreenDragonTurtle);

                    break;
                case WoWRace.Undead:
                    moveToArea = MoveToEasternKingdomsHorde;

                    buyMount = () => TurninQuestAndBuyMount(
                        MobId_VelmaWarnam,
                        _velmaWarnamLoc,
                        QuestId_LearnToRide_Undead,
                        MobId_ZachariahPost,
                        _zachariahPostLoc,
                        ItemId_BlackSkeletalHorse);

                    break;
                case WoWRace.BloodElf:
                    moveToArea = MoveToSilvermoonCityHorde;
                    buyMount = () => BuyMount(MobId_Winaestra, _winaestraLoc, ItemId_BlackHawkstrider);
                    break;
                default:
                    return false;
            }

            MoveToAreaResult result = await moveToArea();
            if (result == MoveToAreaResult.Moving)
                return true;

            if (result == MoveToAreaResult.InArea)
                return await buyMount();

            return false;
        }

        private async Task<bool> PurchaseFlyingMount_Horde()
        {
            if (InHellfire)
                return await BuyMount(MobId_BanaWildmane, _banaWildmaneLoc, ItemId_TawnyWindRider);

            if (InKalimdor)
                return await BuyMount(MobId_Drakma, _drakmaLoc, ItemId_TawnyWindRider);

            return false;
        }

        private enum MoveToAreaResult
        {
            InArea,
            Moving,
            Failure,
        }

        private async Task<MoveToAreaResult> MoveToEasternKingdomsHorde()
        {
            if (InKalimdor)
            {
                return await MoveFromKalimdorToEKHorde() ? MoveToAreaResult.Moving : MoveToAreaResult.Failure;
            }

            return InEasternKingdoms ? MoveToAreaResult.InArea : MoveToAreaResult.Failure;
        }

        private async Task<MoveToAreaResult> MoveToKalimdorHorde()
        {
            if (InEasternKingdoms)
            {
                return await MoveFromEKToKalimdorHorde() ? MoveToAreaResult.Moving : MoveToAreaResult.Failure;
            }

            return InKalimdor ? MoveToAreaResult.InArea : MoveToAreaResult.Failure;
        }

        private async Task<MoveToAreaResult> MoveToSilvermoonCityHorde()
        {
            if (InKalimdor)
            {
                return await MoveFromKalimdorToEKHorde() ? MoveToAreaResult.Moving : MoveToAreaResult.Failure;
            }

            if (InEasternKingdoms)
            {
                return await MoveFromEKToSilvermoonCityHorde() ? MoveToAreaResult.Moving : MoveToAreaResult.Failure;
            }

            if (Me.ZoneId != ZoneId_SilverMoonCity && Me.ZoneId != ZoneId_EversongWoods)
                return MoveToAreaResult.Failure;

            return MoveToAreaResult.InArea;
        }

        private async Task<bool> MoveFromKalimdorToEKHorde()
        {
            return await UtilityCoroutine.UseTransport(
                GameObjectId_Ship_TheThundercaller,
                _theThundercallerKalimdorLoc,
                _theThundercallerEKLoc,
                _theThundercallerKalimdorWaitLoc,
                _theThundercallerKalimdorBoardLoc,
                _theThundercallerEKWaitLoc);
        }

        private async Task<bool> MoveFromEKToKalimdorHorde()
        {
            return await UtilityCoroutine.UseTransport(
                GameObjectId_Ship_TheThundercaller,
                _theThundercallerEKLoc,
                _theThundercallerKalimdorLoc,
                _theThundercallerEKWaitLoc,
                _theThundercallerEKBoardLoc,
                _theThundercallerKalimdorWaitLoc);
        }

        private async Task<bool> MoveFromEKToSilvermoonCityHorde()
        {
            var portal = ObjectManager.GetObjectsOfType<WoWGameObject>()
                                      .FirstOrDefault(g => g.Entry == GameObjectId_OrbOfTranslocation);

            if (portal == null || !portal.WithinInteractRange)
                return await (UtilityCoroutine.MoveTo(portal?.Location ?? _silvermoonCityPortalLoc,
                                                      "Silvermoon City portal"));

            await CommonCoroutines.StopMoving();
            portal.Interact();
            await Coroutine.Sleep(3000);
            return true;
        }

        #endregion

        #endregion

        #region Utility

        private async Task<bool> TurninQuestAndBuyMount(
            int turninId,
            WoWPoint turninLoc,
            uint questId,
            int vendorId,
            WoWPoint vendorLocation,
            int itemId)
        {
            // Turnin the 'Learn to Ride' quest if in log
            if (_profileHelpers.HasQuest(questId))
                return await UtilityCoroutine.TurninQuest(turninId, turninLoc, questId);

            // buy the mount
            return await BuyMount(vendorId, vendorLocation, itemId);
        }

        private async Task<bool> TrainMount()
        {
            if (!TrainInOutland && !TrainInOldWorld)
                return false;

            var trainerId = GetTrainerId();
            if (trainerId == 0)
                return false;

            var trainer = ObjectManager.GetObjectsOfType<WoWUnit>()
                                 .Where(u => u.Entry == trainerId && !u.IsDead)
                                 .OrderBy(u => u.DistanceSqr).FirstOrDefault();
            WoWPoint trainerLoc;
            string trainerName;
            if (trainer == null)
            {
                var traderEntry = Styx.CommonBot.ObjectDatabase.Query.GetNpcById((uint)trainerId);
                if (traderEntry == null)
                    return false;
                trainerLoc = traderEntry.Location;
                trainerName = traderEntry.Name;
            }
            else
            {
                trainerLoc = trainer.Location;
                trainerName = trainer.SafeName;
            }

            if (trainer == null || !trainer.WithinInteractRange)
                return (await UtilityCoroutine.MoveTo(trainerLoc, "Riding Trainer: " + trainerName));

            if (await CommonCoroutines.StopMoving())
                return true;

            // Turnin any quests since they can interfer with training.
            if (trainer.HasQuestTurnin())
                return await UtilityCoroutine.TurninQuest(trainer, trainer.Location);

            if (!TrainerFrame.Instance.IsVisible)
            {
                trainer.Interact();
                await CommonCoroutines.SleepForLagDuration();
                return true;
            }

            TrainerFrame.Instance.BuyAll();
            await CommonCoroutines.SleepForRandomUiInteractionTime();
            return true;
        }

        private async Task<bool> BuyMount(int vendorId, WoWPoint vendorLocation, int itemId)
        {
            var item = Me.BagItems.FirstOrDefault(i => i.Entry == itemId);

            if (item == null)
            {
                return await UtilityCoroutine.BuyItem(
                            vendorId,
                            vendorLocation,
                            itemId,
                            1,
                            noVendorFrameAction: () => QBCLog.Fatal("Npc ({0}) does not offer a vendor frame", vendorId),
                            itemNotFoundAction: () => QBCLog.Fatal("Npc ({0}) does not sell the item with ID: {1}", vendorId, itemId),
                            insufficientFundsAction: () => QBCLog.Fatal("Toon does not have enough funds to buy {0} from {1}", itemId, vendorId));
            }
            item.Use();
            _purchasedMountTimer.Reset();
            await CommonCoroutines.SleepForRandomUiInteractionTime();
            return true;
        }

        #endregion

        private enum RidingLevelType
        {
            None = 0,
            // 60% ground speed @ level 20
            ApprenticeRiding = 75,
            // 100% ground speed @ level 40
            JourneyManRiding = 150,
            // 150% flying speed @ level 60
            ExpertRiding = 225,
            // 280% flying speed @ level 70
            ArtisanRiding = 300,
            // 310% flying speed @ level 80
            MasterFlying = 375,
        }
    }
}
