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
using System.Numerics;
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
        public override string VersionId => QuestBehaviorBase.GitIdToVersionId("$Id$");

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

            if (_state)
            {
                if (_myHook == null)
                {
                    BotEvents.OnBotStopped += BotEvents_OnBotStopped;
                    BotEvents.Profile.OnNewProfileLoaded += Profile_OnNewProfileLoaded;

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
                if (!RemoveHook())
                {
                    QBCLog.Info("Remove was requested, but hook was not present");
                }
            }
        }

        private bool RemoveHook()
        {
            if (_myHook == null)
                return false;

                    QBCLog.Info("Removing hook");
                    TreeHooks.Instance.RemoveHook("Questbot_Profile", _myHook);

            BotEvents.OnBotStopped -= BotEvents_OnBotStopped;
            BotEvents.Profile.OnNewProfileLoaded -= Profile_OnNewProfileLoaded;
                    _myHook = null;
            return true;
                }

        private void Profile_OnNewProfileLoaded(BotEvents.Profile.NewProfileLoadedEventArgs args)
                {
            RemoveHook();
                }

        private void BotEvents_OnBotStopped(EventArgs args)
        {
            RemoveHook();
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
        private const int ItemId_ReinsOfTheBlackDragonTurtle = 91008;
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

        private readonly Vector3 _stormwindInnkeeperLoc = new Vector3(-8867.786f, 673.6729f, 97.90324f);
        private readonly Vector3 _randalHunterLoc = new Vector3(-9442.742f, -1390.666f, 46.87045f);
        private readonly Vector3 _katieHunterLoc = new Vector3(-9455.365f, -1385.327f, 47.12818f);
        private readonly Vector3 _meiLinLoc = new Vector3(-8212.221f, 547.569f, 117.1947f);
        private readonly Vector3 _oldWhitenoseLoc = new Vector3(-8209.379f, 546.0261f, 117.7684f);
        private readonly Vector3 _binjyFeatherwhistleLoc = new Vector3(-5454.171f, -621.048f, 393.3968f);
        private readonly Vector3 _milliFeatherwhistleLoc = new Vector3(-5454.171f, -621.048f, 393.3968f);
        private readonly Vector3 _ulthamIronhornLoc = new Vector3(-5524.354f, -1349.868f, 398.6641f);
        private readonly Vector3 _veronAmberstillLoc = new Vector3(-5539.55f, -1322.55f, 398.8653f);
        private readonly Vector3 _jartsamLoc = new Vector3(10129.78f, 2526.595f, 1324.828f);
        private readonly Vector3 _lelanaiLoc = new Vector3(10129.91f, 2533.245f, 1323.271f);
        private readonly Vector3 _aalunLoc = new Vector3(-3981.769f, -11929.14f, -0.2419412f);
        private readonly Vector3 _toralliusThePackHandlerLoc = new Vector3(-3981.769f, -11929.14f, -0.2419412f);
        private readonly Vector3 _grundaBronzewingLoc = new Vector3(-674.4774f, 2743.128f, 93.9173f);
        private readonly Vector3 _tannecStonebeakLoc = new Vector3(-8829.18f, 482.34f, 109.616f);

        private readonly Vector3 _theBraveryStartLoc = new Vector3(-8650.719f, 1346.051f, -0.0382334f);
        private readonly Vector3 _theBraveryEndLoc = new Vector3(8162.587f, 1005.365f, 0.0474023f);
        private readonly Vector3 _theBraveryWaitAtLoc = new Vector3(-8640.556f, 1330.829f, 5.233207f);
        private readonly Vector3 _theBraveryStandAtLoc = new Vector3(-8644.952f, 1348.11f, 6.143094f);
        private readonly Vector3 _theBraveryGetOffAtLoc = new Vector3(8177.54f, 1003.079f, 6.646164f);

        private readonly Vector3 _exodarPortalLoc = new Vector3(9655.252f, 2509.33f, 1331.598f);

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
                                ItemId_ReinsOfTheBlackDragonTurtle);
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
        private const int ItemId_TawnyWindRider = 25474;

        private readonly Vector3 _orgrimmarInnkeeperLoc = new Vector3(1573.266f, -4439.158f, 16.05631f);
        private readonly Vector3 _ogunaroWolfrunnerLoc = new Vector3(2076.602f, -4568.632f, 49.25319f);
        private readonly Vector3 _kallWorthatonLoc = new Vector3(1475.32f, -4140.98f, 52.51f);
        private readonly Vector3 _zjolnirLoc = new Vector3(-852.78f, -4885.40f, 22.03f);
        private readonly Vector3 _harbClawhoofLoc = new Vector3(-2279.796f, -392.0697f, -9.396863f);
        private readonly Vector3 _zachariahPostLoc = new Vector3(2275.08f, 237.00f, 33.69f);
        private readonly Vector3 _winaestraLoc = new Vector3(9244.59f, -7491.566f, 36.91401f);
        private readonly Vector3 _velmaWarnamLoc = new Vector3(2275.08f, 236.997f, 33.69074f);
        private readonly Vector3 _softpawsLoc = new Vector3(2010.891f, -4722.866f, 29.3442f);
        private readonly Vector3 _turtlemasterOdaiLoc = new Vector3(2009.267f, -4721.249f, 29.51483f);
        private readonly Vector3 _banaWildmaneLoc = new Vector3(47.76153f, 2742.022f, 85.27119f);
        private readonly Vector3 _drakmaLoc = new Vector3(1806.94f, -4340.67f, 102.0506f);

        private readonly Vector3 _theThundercallerKalimdorLoc = new Vector3(1833.509f, -4391.543f, 152.7679f);
        private readonly Vector3 _theThundercallerKalimdorWaitLoc = new Vector3(1845.187f, -4395.555f, 135.2306f);
        private readonly Vector3 _theThundercallerKalimdorBoardLoc = new Vector3(1835.509f, -4385.785f, 135.0436f);
        private readonly Vector3 _theThundercallerEKLoc = new Vector3(2062.376f, 292.998f, 114.973f);
        private readonly Vector3 _theThundercallerEKWaitLoc = new Vector3(2065.049f, 283.1381f, 97.03156f);
        private readonly Vector3 _theThundercallerEKBoardLoc = new Vector3(2067.672f, 294.2617f, 97.20473f);

        private readonly Vector3 _silvermoonCityPortalLoc = new Vector3(1805.877f, 345.0006f, 70.79002f);

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
                        ItemId_ReinsOfTheBlackDragonTurtle);

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
            Vector3 turninLoc,
            uint questId,
            int vendorId,
            Vector3 vendorLocation,
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
            Vector3 trainerLoc;
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

        private async Task<bool> BuyMount(int vendorId, Vector3 vendorLocation, int itemId)
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
