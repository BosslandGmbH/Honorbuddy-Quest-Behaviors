﻿// Behavior originally contributed by Chinajade.
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
// DOCUMENTATION:
//      http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_ButtonPressOnChat
//
// QUICK DOX:
//      This behavior interacts with an Item, Mob, or Object to initiate a chat query-response
//      cycle.  The Item/Mob/Object (hereafter referred to as 'target') will ask a question or
//      issue a directive, to which the toon is expected to press a particular button.
//      All phrases supplied to this behavior are specific to the WoWclient's locale.  The profile
//      writer must be mindful of this.  The Wiki page contains thorough docuementation of the
//      considerations for using this behavior.
//
//  Parameters (required, then optional--both listed alphabetically):
//      (***One or more of the following two attributes must be specified***).
//      ButtonMPhraseN: [one entry REQUIRED] Specifies which button should be pressed when a
//              particular aura is seen on a target.  The value of M must be between 1 and 12,
//              and it represents a button position on the hotbar when the quest has replaced
//              the user's normal hotbar.  N may be omitted, or any positive integer--this
//              implies that you may have multiple auras associated with the same button.
//      ItemId [REQUIRED, if MobId not specified]: identifies the Item that should be used
//              to initiate the chat query-response sequence.
//      MobId [REQUIRED, if ItemId not specified]: identifies the Mob/Object that should be
//              used to initiate the chat query-rsponse sequence.
//      QuestId [REQUIRED, Default:none]: this behavior always requires a quest.  As, there are
//              no known uses of this behavior outside the context of a quest.
//
//      (***These attibutes are completely optional***)
//      ButtonOnQuestComplete [Default: none]: This specifies a button that should be pressed
//              when the quest complete.  The value for this attribute must be on the closed
//              interval of [1..12], and represents a button position on the hotbar when the quest
//              has replaced the user's normal hotbar.
//      DebugShowText [Default: false]: Enabling this option, the profile writer is able to see
//              the complete dialog as emitted by the target.  This can be useful for
//              analyzing which phrases should act as triggers for the planned responses.
//      ExitVehicleAtQuestComplete [Default: true]: if enabled, the 'exit vehicle' command is
//              executed when the quest completes.  Even though many quests of this type don't
//              _appear_ to take place inside a vehicle, the WoWclient treats them as such.
//      FeedbackPhraseFailN [Default: none]: a phrase indicating that the wrong button was
//              pressed as a consequence of responding to the target's query.
//              The behavior will continue to spam the identified button until either the
//              success or failure feedback phrase is encountered.
//      FeedbackPhraseSuccessN [ Default: none]: a phrase indicating that the correct button was
//              pressed as a consequence of responding to the target's query.
//              The behavior will continue to spam the identified button until either the
//              success or failure feedback phrase is encountered.
//      InputResponseDelay [Default: 3000ms]: the maximum delay that should be used when
//              responding to the target's query.  To appear more human-like, the delay
//              between the target-issued query and the behavior's response is randomized
//              with this value.
//      MonitorStartPhraseN [Default: none]: identifies a phrase that should be used to
//              trigger the listening for ButtonMPhraseN phrases.  The behavior ignores
//              any information emitted by the target prior to seeing this phrase.
//              If this attribute is not specified, then the behavior starts 'hot'--i.e.,
//              it immediately starts listening for ButtonMPhraseN phrases.
//      MonitorStopPhraseN [Default: none]: identifies a phrase that should be use to stop
//              listening for ButtonMPhraseN phrases.
//      QuestCompleteRequirement [Default:NotComplete]:
//      QuestInLogRequirement [Default:InLog]:
//              A full discussion of how the Quest* attributes operate is described in
//              http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
//      SupportedLocales [Default: enUS]: Specifies the locale of the WoWclient for which
//              the profile writer has provided support.  If the user has an unsupported locale
//              then the profile will stop on any attempt to execute this behavior.
//      X/Y/Z [Default: toon's current location]: the general area where a target may be found
//              to initate the query-response cycle.
//
#endregion


#region Examples
#endregion


#region Usings

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.WoWInternals.World;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.ButtonPress.ButtonPressOnChat
{
    [CustomBehaviorFileName(@"ButtonPress\ButtonPressOnChat")]
    public class ButtonPressOnChat : CustomForcedBehavior
    {
        public ButtonPressOnChat(Dictionary<string, string> args)
            : base(args)
        {
            QBCLog.BehaviorLoggingContext = this;

            try
            {
                int tmpItemId;
                string[] tmpPhrases;

                // Look for these phrases in the captured text...
                for (int i = 1; i <= 12; ++i)
                {
                    string attributeName = string.Format("Button{0}Phrase", i);

                    tmpPhrases = GetNumberedAttributesAsArray(attributeName, 0, ConstrainAs.StringNonEmpty, null);
                    UtilPopulateMapWithPhrases(_simpleTextToButtonMap, i, tmpPhrases);
                }


                ButtonOnQuestComplete = GetAttributeAsNullable<int>("ButtonOnQuestComplete", false, ConstrainAs.HotbarButton, null);
                DebugShowText = GetAttributeAsNullable<bool>("DebugShowText", false, null, null) ?? false;
                ExitVehicleAtQuestComplete = GetAttributeAsNullable<bool>("ExitVehicleAtQuestComplete", false, null, null) ?? true;
                FeedbackPhraseFailures = GetNumberedAttributesAsArray<string>("FeedbackPhraseFail", 0, ConstrainAs.StringNonEmpty, null);
                FeedbackPhraseSuccesses = GetNumberedAttributesAsArray<string>("FeedbackPhraseSuccess", 0, ConstrainAs.StringNonEmpty, null);
                InputResponseDelay = GetAttributeAsNullable<int>("InputResponseDelay", false, new ConstrainTo.Domain<int>(500, 15000), null) ?? 3000;
                tmpItemId = GetAttributeAsNullable<int>("ItemId", false, ConstrainAs.ItemId, null) ?? 0;
                MobId = GetAttributeAsNullable<int>("MobId", false, ConstrainAs.MobId, null) ?? 0;
                HuntingGroundAnchor = GetAttributeAsNullable<Vector3>("", false, ConstrainAs.Vector3NonEmpty, null) ?? Me.Location;
                MonitorStartPhrases = GetNumberedAttributesAsArray<string>("MonitorStartPhrase", 0, ConstrainAs.StringNonEmpty, null);
                MonitorStopPhrases = GetNumberedAttributesAsArray<string>("MonitorStopPhrase", 0, ConstrainAs.StringNonEmpty, null);
                QuestId = GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
                SupportedLocales = GetNumberedAttributesAsArray<string>("SupportedLocale", 0, ConstrainAs.StringNonEmpty, null);
                if (SupportedLocales.Count() == 0)
                { SupportedLocales = new[] { "enUS" }; }


                // Semantic coherency...
                if (_simpleTextToButtonMap.Count() <= 0)
                {
                    QBCLog.Error("You must supply at least one 'ButtonNPhraseM' attribute.");
                    IsAttributeProblem = true;
                }

                if ((MobId == 0) && (tmpItemId == 0))
                {
                    QBCLog.Error("Either ItemId and MobId must be specified.");
                    IsAttributeProblem = true;
                }

                if ((MobId != 0) && (tmpItemId != 0))
                {
                    QBCLog.Error("ItemId and MobId are mutually exclusive--please specify one, but not both.");
                    IsAttributeProblem = true;
                }


                // Install our chat handlers...
                Chat.MonsterEmote += HandleChatMonster;
                Chat.MonsterParty += HandleChatMonster;
                Chat.MonsterSay += HandleChatMonster;
                Chat.MonsterWhisper += HandleChatMonster;
                Chat.MonsterYell += HandleChatMonster;
                Chat.RaidBossEmote += HandleChatMonster;
                Chat.RaidBossWhisper += HandleChatMonster;
                Chat.RaidWarning += HandleChatLanguageSpecific;
                Chat.System += HandleChatSimpleMessage;

                // If no MonitoringStartPhrases, we start 'hot'...
                _buttonAction = _buttonEmpty;
                if (MonitorStartPhrases.Count() <= 0)
                { _isMonitoringEnabled = true; }

                // Final initialization...
                if (tmpItemId > 0)
                {
                    Item = Me.CarriedItems.FirstOrDefault(i => i.Entry == tmpItemId);
                    if (Item == null)
                    {
                        QBCLog.Error("ItemId({0}) is not in our inventory", tmpItemId);
                        IsAttributeProblem = true;
                    }
                }

                _behavior_HuntingGround = new HuntingGroundBehavior(ViableTargets,
                                                                    HuntingGroundAnchor,
                                                                    1000.0);
                _wowClientLocale = GetWoWClientLocale();

                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
                QuestName = (quest != null) ? quest.Name : string.Format("QuestId({0})", QuestId);
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


        // Attributes provided by caller...
        public string[] FeedbackPhraseFailures { get; private set; }
        public string[] FeedbackPhraseSuccesses { get; private set; }
        public int? ButtonOnQuestComplete { get; private set; }
        public bool DebugShowText { get; private set; }
        public bool ExitVehicleAtQuestComplete { get; private set; }
        public Vector3 HuntingGroundAnchor { get; private set; }
        public int InputResponseDelay { get; private set; }
        public WoWItem Item { get; private set; }
        public int MobId { get; private set; }
        public string[] MonitorStartPhrases { get; private set; }
        public string[] MonitorStopPhrases { get; private set; }
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }
        public string[] SupportedLocales { get; private set; }

        // Private Properties & data...
        private const int BonusActionButtonOffset = (12 /*buttons_per_hotbar*/ * 10 /*hotbars*/);
        private KeyValuePair<string, int> _buttonEmpty = new KeyValuePair<string, int>(string.Empty, 0);
        private WoWObject CurrentTarget { get { return (_behavior_HuntingGround.CurrentTarget); } }
        private TimeSpan Delay_WowClientLagTime { get { return (TimeSpan.FromMilliseconds((StyxWoW.WoWClient.Latency * 2) + 150)); } }
        private TimeSpan Delay_InputResponse { get { return (TimeSpan.FromMilliseconds((StyxWoW.Random.NextDouble() * InputResponseDelay) + 450)); } }
        private static LocalPlayer Me { get { return (StyxWoW.Me); } }
        private string QuestName { get; set; }
        private readonly Dictionary<string, int> _simpleTextToButtonMap = new Dictionary<string, int>();

        private Composite _behaviorRoot;
        private HuntingGroundBehavior _behavior_HuntingGround;
        private KeyValuePair<string, int> _buttonAction;
        private ConfigMemento _configMemento;
        private bool _isBehaviorInProgress;
        private bool _isBehaviorDone;
        private bool _isInteracting;
        private bool _isMonitoringEnabled;
        private Queue<string> _messagesPending = new Queue<string>();
        private readonly string _wowClientLocale;

        // Private LINQ queries...
        private IEnumerable<WoWObject> ViableTargets()
        {
            return ObjectManager.GetObjectsOfType<WoWObject>(true, false)
                    .Where(target => (target.IsValid
                                      && (target.Entry == MobId)
                                      && !target.IsLocallyBlacklisted()))
                    .OrderBy(target => target.PathTraversalCost());
        }

        // The same phrase must no be used for multiple roles...
        // We enforce the constraint here.
        private void CheckPhrases()
        {
            List<KeyValuePair<string, string>> allPhrases = new List<KeyValuePair<string, string>>();

            foreach (var phrase in FeedbackPhraseFailures)
            { allPhrases.Add(new KeyValuePair<string, string>("ButtonFeedbackFail", phrase)); }

            foreach (var phrase in FeedbackPhraseSuccesses)
            { allPhrases.Add(new KeyValuePair<string, string>("ButtonFeedbackSuccess", phrase)); }

            foreach (var phrase in MonitorStartPhrases)
            { allPhrases.Add(new KeyValuePair<string, string>("MonitorStartPhrase", phrase)); }

            foreach (var phrase in MonitorStopPhrases)
            { allPhrases.Add(new KeyValuePair<string, string>("MonitorStopPhrase", phrase)); }

            foreach (var kvp in _simpleTextToButtonMap)
            { allPhrases.Add(new KeyValuePair<string, string>(string.Format("Button{0}Phrase", kvp.Value), kvp.Key)); }


            var distinctPhrases = allPhrases.Select(kvp => kvp.Value).Distinct();

            foreach (var phrase in distinctPhrases)
            {
                var phraseSources = allPhrases.Where(kvp => (kvp.Value == phrase)).Select(kvp => kvp.Key);

                if (phraseSources.Count() <= 1)
                { continue; }

                QBCLog.Error("You may only use a phrase for one trigger condition.\n"
                            + "The phrase \"{0}\" was used for the following: {1}.\n",
                            phrase, ("'" + string.Join("', '", phraseSources.ToArray()) + "'"));
                IsAttributeProblem = true;
            }
        }


        private string GetWoWClientLocale()
        {
            return (Lua.GetReturnVal<string>("return GetLocale()", 0));
        }


        private void HandleChatMonster(Chat.ChatMonsterEventArgs args)
        {
            ProcessMessage(args.Message);
        }


        private void HandleChatLanguageSpecific(Chat.ChatLanguageSpecificEventArgs args)
        {
            ProcessMessage(args.Message);
        }


        private void HandleChatSimpleMessage(Chat.ChatSimpleMessageEventArgs args)
        {
            ProcessMessage(args.Message);
        }


        private void GuiShowProgress(string completionReason)
        {
            if (completionReason != null)
            {
                QBCLog.DeveloperInfo("Behavior done (" + completionReason + ")");
                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;
                _isBehaviorDone = true;
            }
        }


        private void PressButton(int buttonNumber)
        {
            Lua.DoString("local _,s,_ = GetActionInfo({0}) CastSpellByID(s) ",
                         buttonNumber + BonusActionButtonOffset);
        }


        private void ProcessMessage(string message)
        {
            if (DebugShowText && !string.IsNullOrEmpty(message))
            { QBCLog.Info("Saw message '{0}'", message); }

            // Look for our 'start monitoring' trigger phrase...
            if (!_isMonitoringEnabled && (MonitorStartPhrases.Count() > 0))
            {
                string triggerPhrase = MonitorStartPhrases
                                                .FirstOrDefault(phrase => message.Contains(phrase));

                if (triggerPhrase != null)
                {
                    QBCLog.DeveloperInfo("Monitoring turned on by trigger phrase '{0}'", triggerPhrase);
                    _isMonitoringEnabled = true;
                }
            }

            // Look for our 'stop monitoring' trigger phrase...
            if (_isMonitoringEnabled && (MonitorStopPhrases.Count() > 0))
            {
                string triggerPhrase = MonitorStopPhrases
                                                .FirstOrDefault(phrase => message.Contains(phrase));

                if (triggerPhrase != null)
                {
                    QBCLog.DeveloperInfo("Monitoring turned off by trigger phrase '{0}'", triggerPhrase);
                    _isMonitoringEnabled = false;
                }
            }

            // Does phrase match a button-push action?
            if (_isMonitoringEnabled)
            {
                string phraseFail = FeedbackPhraseFailures.FirstOrDefault(phrase => message.Contains(phrase));
                string phraseMatch = _simpleTextToButtonMap.Keys.FirstOrDefault(phrase => message.Contains(phrase));
                string phraseSucceed = FeedbackPhraseSuccesses.FirstOrDefault(phrase => message.Contains(phrase));

                if (phraseMatch != null)
                { _buttonAction = new KeyValuePair<string, int>(phraseMatch, _simpleTextToButtonMap[phraseMatch]); }

                if ((_buttonAction.Value > 0) && (phraseFail != null))
                {
                    QBCLog.DeveloperInfo("Phrase '{0}' failed with Button {1}.", _buttonAction.Key, _buttonAction.Value);
                    _buttonAction = _buttonEmpty;
                }

                if ((_buttonAction.Value > 0) && (phraseSucceed != null))
                {
                    QBCLog.DeveloperInfo("Phrase '{0}' succeeded with Button {1}.", _buttonAction.Key, _buttonAction.Value);
                    _buttonAction = _buttonEmpty;
                }
            }
        }


        private void UtilPopulateMapWithPhrases(Dictionary<string, int> phraseToButtonMap,
                                                       int buttonNum,
                                                       string[] phrases)
        {
            foreach (string phrase in phrases)
            {
                if (phraseToButtonMap.ContainsKey(phrase))
                {
                    if (phraseToButtonMap[phrase] != buttonNum)
                    {
                        QBCLog.Error("Phrase(\"{0}\") cannot be associated with two different buttons."
                                    + "  (Attempted to associate with Button{1}Phrase and Button{2}Phrase.)",
                                    phrase, phraseToButtonMap[phrase], buttonNum);
                        IsAttributeProblem = true;
                    }

                    continue;
                }

                phraseToButtonMap.Add(phrase, buttonNum);
            }
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return (_behaviorRoot ?? (_behaviorRoot =
                new PrioritySelector(

                    // If the quest is complete, and we need to press a final button...
                    new Decorator(ret => Me.IsQuestComplete(QuestId),
                        new Sequence(
                            new DecoratorContinue(ret => ButtonOnQuestComplete.HasValue,
                                new Sequence(
                                    new Action(delegate
                                    {
                                        TreeRoot.StatusText = string.Format("Pressing Button {0} at Quest Complete.",
                                                                            ButtonOnQuestComplete.Value);
                                        PressButton(ButtonOnQuestComplete.Value);
                                    }),
                                    new WaitContinue(Delay_WowClientLagTime, ret => false, new ActionAlwaysSucceed())
                                    )),

                            new DecoratorContinue(ret => ExitVehicleAtQuestComplete,
                                new Action(delegate
                                {
                                    QBCLog.DeveloperInfo("Exiting Vehicle");
                                    Lua.DoString("VehicleExit()");
                                })),

                            // If behavior done, bail...
                            // Note that this is also an implicit "is quest complete" exit criteria, also.
                            new Action(delegate
                            {
                                GuiShowProgress("quest complete");
                                _isBehaviorDone = true;
                            })
                            )),

                    // If we're to use an item, move to destination, and use it...
                    new Decorator(ret => (!_isInteracting && (Item != null)),
                        new PrioritySelector(
                            // Move to destination...
                            _behavior_HuntingGround.CreateBehavior_MoveToLocation(() => HuntingGroundAnchor),

                            // If the item is on cooldown, wait for it...
                            new Decorator(ret => (Item.Cooldown > 0),
                                new Action(delegate
                                {
                                    TreeRoot.StatusText = string.Format("Waiting for {0} cooldown in {1:0} seconds.",
                                                                        Item.SafeName, Item.Cooldown);
                                })),


                            // Use the item...
                            new Sequence(
                                new Action(delegate { WoWMovement.MoveStop(); }),
                                new WaitContinue(Delay_WowClientLagTime, ret => false, new ActionAlwaysSucceed()),
                                new Action(delegate
                                {
                                    TreeRoot.StatusText = string.Format("Using Item '{0}' to initiate interaction", Item.SafeName);
                                    Item.UseContainerItem();
                                    _isInteracting = true;
                                })
                            )
                        )),

                    // If we haven't interacted with the mob, move to it, and interact...
                    new Decorator(ret => !_isInteracting & (MobId > 0),
                        new PrioritySelector(

                            // Select and move to target...
                            _behavior_HuntingGround.CreateBehavior_SelectTarget(),
                            _behavior_HuntingGround.CreateBehavior_MoveToTarget(),

                            // Interact with target...
                            new Sequence(
                                new Action(delegate { WoWMovement.MoveStop(); }),
                                new DecoratorContinue(ret => !Me.IsSafelyFacing(CurrentTarget),
                                    new Action(delegate { WoWMovement.Face(CurrentTarget.Guid); })),
                                new Action(delegate { _behavior_HuntingGround.MobEngaged(CurrentTarget); }),
                                new WaitContinue(Delay_WowClientLagTime, ret => false, new ActionAlwaysSucceed()),
                                new Action(delegate
                                {
                                    CurrentTarget.Interact();
                                    _isInteracting = true;
                                }),
                                new WaitContinue(Delay_WowClientLagTime, ret => false, new ActionAlwaysSucceed())
                                )
                            )),

                    // Process any new messages that have arrived...
                    new Decorator(ret => (_messagesPending.Count() > 0),
                        new Action(delegate { ProcessMessage(_messagesPending.Dequeue()); })),

                    // If we've a button to press, take action...
                    new Decorator(ret => (_buttonAction.Value > 0),
                        new Sequence(
                            // Need to give the WoWserver time to accept our response to the input...
                            // It also needs to have some variance, so it doesn't look like a bot's doing it.
                            new WaitContinue(Delay_InputResponse, ret => false, new ActionAlwaysSucceed()),

                            // If the action is still valid, act on it...
                            new Decorator(ret => (_buttonAction.Value > 0),
                                new Action(delegate
                                {
                                    TreeRoot.StatusText = string.Format("Pressing button {0} due to phrase '{1}'",
                                                                        _buttonAction.Value,
                                                                        _buttonAction.Key);
                                    PressButton(_buttonAction.Value);
                                }))
                        ))
                )));
        }


        public override void OnFinished()
        {
            if (_configMemento != null)
            {
                _configMemento.Dispose();
                _configMemento = null;
            }
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }


        public override bool IsDone
        {
            get
            {
                bool isDone = _isBehaviorDone;

                // Once the behavior starts, the behavior alone needs to make the determination
                // of when it is completes...
                // This is needed because the behavior has some 'cleaning up' to do when the quest
                // completes.
                if (!_isBehaviorInProgress)
                {
                    isDone = isDone || !UtilIsProgressRequirementsMet(QuestId,
                                                                      QuestRequirementInLog,
                                                                      QuestRequirementComplete);
                }

                return (isDone);
            }
        }


        public override void OnStart()
        {
            // We needed to defer this from constructor due to lack of Element (location) information...
            CheckPhrases();

            // Check locale
            if (!SupportedLocales.Contains(_wowClientLocale) && !SupportedLocales.Contains("*") /*any locale*/)
            {
                QBCLog.Fatal("This profile does not support the locale of your WoWClient ({0}),"
                            + " and will be unable to continue.  Supported locales include: {1}.\n"
                            + "Your choices are:\n"
                            + " 1) complete Quest \"{2}\" by hand, and restart the profile after its complete, or\n"
                            + " 2) Ask the profile writer to support your WoWclient's locale ({0})",
                            _wowClientLocale,
                            ("'" + string.Join("', '", SupportedLocales) + "'"),
                            QuestName);
                IsAttributeProblem = true;
            }


            // This reports problems, and stops BT processing if there was a problem with attributes...
            // We had to defer this action, as the 'profile line number' is not available during the element's
            // constructor call.
            OnStart_HandleAttributeProblem();

            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (!IsDone)
            {
                _configMemento = new ConfigMemento();

                CharacterSettings.Instance.PullDistance = 1;


                this.UpdateGoalText(QuestId);

                _isBehaviorInProgress = true;
                GuiShowProgress(null);
            }
        }

        #endregion      // Overrides of CustomForcedBehavior
    }


    #region Reusable behaviors
    // The behaviors in this section were designed to be efficient and robust.
    // The robustness results in some larger-than-normal, but swift code.
    // We also designed them to be reused in other behaviors--just copy, paste,
    // and call them as-needed.

    public class HuntingGroundBehavior
    {
        public delegate bool BehaviorFailIfNoTargetsDelegate();
        public delegate double DistanceDelegate();
        public delegate Vector3 LocationDelegate();
        public delegate void LoggerDelegate(string messageType, string format, params object[] args);
        public delegate IEnumerable<WoWObject> ViableTargetsDelegate();
        public delegate WoWObject WoWObjectDelegate();


        public HuntingGroundBehavior(ViableTargetsDelegate viableTargets,
                                     Vector3 huntingGroundAnchor,
                                     double collectionDistance)
        {
            CollectionDistance = collectionDistance;
            HuntingGroundAnchor = huntingGroundAnchor;
            ViableTargets = viableTargets;

            UseHotspots(null);
        }


        public void MobEngaged(WoWObject wowObject)
        {
            if (wowObject == CurrentTarget)
            { _currentTargetAutoBlacklistTimer.Stop(); }
        }


        // Public properties...
        public double CollectionDistance { get; private set; }
        public WoWObject CurrentTarget { get; private set; }
        public Vector3 HuntingGroundAnchor { get; private set; }


        // Private properties & data...
        private const string AuraName_DruidAquaticForm = "Aquatic Form";
        private readonly TimeSpan _delay_AutoBlacklist = TimeSpan.FromMinutes(7);
        private readonly TimeSpan _delay_PositionUpdateThrottle = TimeSpan.FromMilliseconds(3000);
        private readonly TimeSpan _delay_RepopWait = TimeSpan.FromMilliseconds(500);
        private readonly TimeSpan _delay_WoWClientMovementThrottle = TimeSpan.FromMilliseconds(0);
        private TimeSpan Delay_WowClientLagTime { get { return (TimeSpan.FromMilliseconds((StyxWoW.WoWClient.Latency * 2) + 150)); } }
        private static LocalPlayer Me { get { return (StyxWoW.Me); } }
        private const double MinDistanceToUse_DruidAquaticForm = 27.0;
        private int _spellId_DruidAquaticForm = 1066;
        public ViableTargetsDelegate ViableTargets { get; private set; }

        private TimeSpan _currentTargetAutoBlacklistTime = TimeSpan.FromSeconds(1);
        private readonly Stopwatch _currentTargetAutoBlacklistTimer = new Stopwatch();
        private Queue<Vector3> _hotSpots = new Queue<Vector3>();
        private Vector3 _huntingGroundWaitPoint;
        private readonly Stopwatch _repopWaitingTime = new Stopwatch();


        /// <summary>
        /// The created behavior was meant to be used in a PrioritySelector.
        /// It may also have uses inside other TreeSharp Composites.
        /// </summary>
        ///
        /// <returns>
        /// <para>* RunStatus.Failure, if current target is viable.
        /// It will also return Failure if no targets could be located and failIfNoTargets is true</para>
        /// <para>* RunStatus.Success, if acquiring a target (or waiting for them to repop)</para>
        /// </returns>
        ///
        public Composite CreateBehavior_SelectTarget()
        {
            return (CreateBehavior_SelectTarget(() => false));
        }

        public Composite CreateBehavior_SelectTarget(BehaviorFailIfNoTargetsDelegate failIfNoTargets)
        {
            return (
            new PrioritySelector(

                // If we haven't engaged the mob when the auto-blacklist timer expires, give up on it and move on...
                new Decorator(ret => ((CurrentTarget != null)
                                        && (_currentTargetAutoBlacklistTimer.Elapsed > _currentTargetAutoBlacklistTime)),
                    new Action(delegate
                    {
                        QBCLog.Warning("Taking too long to engage '{0}'--blacklisting", CurrentTarget.SafeName);
                        CurrentTarget.LocallyBlacklist(_delay_AutoBlacklist);
                        CurrentTarget = null;
                    })),


                // If we don't have a current target, select a new one...
                // Once we select a target, its 'locked in' (unless it gets blacklisted).  This prevents us
                // from running back and forth between two equidistant targets.
                new Decorator(ret => ((CurrentTarget == null)
                                      || !CurrentTarget.IsValid
                                      || CurrentTarget.IsLocallyBlacklisted()),
                    new PrioritySelector(context => CurrentTarget = ViableTargets().FirstOrDefault(),

                        // If we found next target, we're done...
                        new Decorator(ret => (CurrentTarget != null),
                            new Action(delegate
                            {
                                _huntingGroundWaitPoint = Vector3.Zero;

                                // We use the target to determine if/when to reset the auto-blacklist timer...
                                // If we get in battle, then the target changes from what we set it (or cleared it).
                                // Combat changes the target, so we use this to determine if we need more time
                                // before auto-blacklisting something.
                                if (CurrentTarget is WoWUnit)
                                { CurrentTarget.ToUnit().Target(); }
                                else
                                { Me.ClearTarget(); }

                                _currentTargetAutoBlacklistTime = CalculateAutoBlacklistTime(CurrentTarget);
                                _currentTargetAutoBlacklistTimer.Reset();
                                _currentTargetAutoBlacklistTimer.Start();
                            })),

                        // If we've exhausted mob/object supply in area, and we need to wait, do so...
                        new Decorator(ret => !failIfNoTargets(),

                            // Move back to hunting ground anchor --
                            new PrioritySelector(

                                // If we've more than one hotspot, head to the next one...
                                new Decorator(ret => (_hotSpots.Count() > 1),
                                    new Sequence(context => FindNextHotspot(),
                                        new DecoratorContinueThrottled(_delay_PositionUpdateThrottle, ret => true,
                                            new Action(nextHotspot => TreeRoot.StatusText = "No targets--moving to hotspot "
                                                                                         + (Vector3)nextHotspot)),
                                        CreateBehavior_InternalMoveTo(() => FindNextHotspot())
                                        )),

                                // We find a point 'near' our anchor at which to wait...
                                // This way, if multiple people are using the same profile at the same time,
                                // they won't be standing on top of each other.
                                new Decorator(ret => (_huntingGroundWaitPoint == Vector3.Zero),
                                    new Action(delegate
                                        {
                                            TreeRoot.StatusText = "No targets--moving near hunting ground anchor point to wait";

                                            _huntingGroundWaitPoint = HuntingGroundAnchor.FanOutRandom(CollectionDistance * 0.25);
                                            _repopWaitingTime.Reset();
                                            _repopWaitingTime.Start();
                                        })),

                                // Move to our selected random point...
                                new Decorator(ret => !Navigator.AtLocation(_huntingGroundWaitPoint),
                                    new Sequence(
                                        new DecoratorContinueThrottled(_delay_PositionUpdateThrottle, ret => true,
                                            new Action(delegate
                                            {
                                                TreeRoot.StatusText = string.Format("No targets--moving near hunting ground anchor point to wait (distance {0})",
                                                                                  Me.Location.Distance(_huntingGroundWaitPoint));
                                            })),
                                        CreateBehavior_InternalMoveTo(() => _huntingGroundWaitPoint)
                                        )),

                                // Tell user what's going on...
                                new Sequence(
                                    new Action(delegate
                                        {
                                            TreeRoot.GoalText = this.GetType().Name + ": Waiting for Repops";
                                            TreeRoot.StatusText = "No targets in area--waiting for repops.  " + BuildTimeAsString(_repopWaitingTime.Elapsed);
                                        }),
                                    new WaitContinue(_delay_RepopWait, ret => false, new ActionAlwaysSucceed()))
                                ))
                        )),

                // Re-select target, if it was changed (perhaps, due to combat)...
                new Action(delegate
                {
                    bool isWoWUnit = (CurrentTarget is WoWUnit);

                    // If target is not what we set it to be, then we've been in combat, and need to
                    // refresh the auto-blacklist timer.
                    if ((isWoWUnit && (Me.CurrentTarget != CurrentTarget))
                        || (!isWoWUnit && (Me.CurrentTarget != null)))
                    {
                        if (isWoWUnit)
                        { CurrentTarget.ToUnit().Target(); }
                        else
                        { Me.ClearTarget(); }

                        QBCLog.DeveloperInfo("Auto-blacklist timer restarted due to combat.");
                        _currentTargetAutoBlacklistTime = CalculateAutoBlacklistTime(CurrentTarget);
                        _currentTargetAutoBlacklistTimer.Reset();
                        _currentTargetAutoBlacklistTimer.Start();
                    }

                    return (RunStatus.Failure);
                })
            ));
        }


        public Composite CreateBehavior_MoveNearTarget(WoWObjectDelegate target,
                                                          DistanceDelegate minRange,
                                                          DistanceDelegate maxRange)
        {
            return (
            new PrioritySelector(context => target(),

                // If we're too far from target, move closer...
                new Decorator(wowObject => (((WoWObject)wowObject).Distance > maxRange()),
                    new Sequence(
                        new DecoratorContinueThrottled(_delay_PositionUpdateThrottle, ret => true,
                            new Action(wowObject =>
                            {
                                TreeRoot.StatusText = string.Format("Moving to {0} (distance: {1:0.0}) ",
                                                                    ((WoWObject)wowObject).SafeName,
                                                                    ((WoWObject)wowObject).Distance);
                            })),

                        CreateBehavior_InternalMoveTo(() => target().Location)
                    )),

                // If we're too close to target, back up...
                new Decorator(wowObject => (((WoWObject)wowObject).Distance < minRange()),
                    new PrioritySelector(

                        // If backing up, make sure we're facing the target...
                        new Decorator(ret => Me.MovementInfo.MovingBackward,
                            new Action(wowObject => WoWMovement.Face(((WoWObject)wowObject).Guid))),

                        // Start backing up...
                        new Action(wowObject =>
                        {
                            TreeRoot.StatusText = "Too close to \"" + ((WoWObject)wowObject).SafeName + "\"--backing up";
                            WoWMovement.MoveStop();
                            WoWMovement.Face(((WoWObject)wowObject).Guid);
                            WoWMovement.Move(WoWMovement.MovementDirection.Backwards);
                        })
                        )),

                // We're between MinRange and MaxRange, stop movement and face the target...
                new Decorator(ret => Me.IsMoving,
                    new Sequence(
                        new Action(delegate { WoWMovement.MoveStop(); }),
                        new Action(wowObject => WoWMovement.Face(((WoWObject)wowObject).Guid)),
                        new WaitContinue(Delay_WowClientLagTime, ret => false, new ActionAlwaysSucceed()),
                        new ActionAlwaysFail()      // fall through to next element
                        ))
            ));
        }


        public Composite CreateBehavior_MoveToLocation(LocationDelegate location)
        {
            return (
            new PrioritySelector(context => location(),

                // If we're not at location, move to it...
                new Decorator(Vector3 => !Navigator.AtLocation((Vector3)Vector3),
                    new Sequence(
                        new DecoratorContinueThrottled(_delay_PositionUpdateThrottle, ret => true,
                            new Action(Vector3 => TreeRoot.StatusText = "Moving to location " + (Vector3)Vector3)),
                        CreateBehavior_InternalMoveTo(() => location())
                    ))
            ));
        }


        public Composite CreateBehavior_MoveToTarget()
        {
            return (CreateBehavior_MoveToTarget(() => CurrentTarget));
        }

        public Composite CreateBehavior_MoveToTarget(WoWObjectDelegate target)
        {
            return (
            new PrioritySelector(context => target(),

                // If we're not at target, move to it...
                new Decorator(wowObject => (((WoWObject)wowObject).Distance > ((WoWObject)wowObject).InteractRange),
                    new Sequence(
                        new DecoratorContinueThrottled(_delay_PositionUpdateThrottle, ret => true,
                            new Action(wowObject =>
                            {
                                TreeRoot.StatusText = string.Format("Moving to {0} (distance: {1:0.0}) ",
                                                                    ((WoWObject)wowObject).SafeName,
                                                                    ((WoWObject)wowObject).Distance);
                            })),
                        CreateBehavior_InternalMoveTo(() => target().Location)
                    ))
            ));
        }


        private static string BuildTimeAsString(TimeSpan timeSpan)
        {
            string formatString = string.Empty;

            if (timeSpan.Hours > 0)
            { formatString = "{0:D2}h:{1:D2}m:{2:D2}s"; }
            else if (timeSpan.Minutes > 0)
            { formatString = "{1:D2}m:{2:D2}s"; }
            else
            { formatString = "{2:D2}s"; }

            return (string.Format(formatString, timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds));
        }


        private TimeSpan CalculateAutoBlacklistTime(WoWObject wowObject)
        {
            double timeToWowObject = ((Me.Location.Distance(wowObject.Location) / Me.MovementInfo.SwimmingForwardSpeed)
                                           * 2.5);     // factor of safety

            timeToWowObject = Math.Max(timeToWowObject, 20.0);  // 20sec hard lower-limit

            return (TimeSpan.FromSeconds(timeToWowObject));
        }


        private Composite CreateBehavior_InternalMoveTo(LocationDelegate locationDelegate)
        {
            return (
            new Sequence(context => locationDelegate(),

                // Druids, switch to Aquatic Form if swimming and distance dictates...
                new DecoratorContinue(ret => (SpellManager.CanCast(_spellId_DruidAquaticForm)
                                                && !Me.HasAura(AuraName_DruidAquaticForm)
                                                && (Me.Location.Distance(locationDelegate()) > MinDistanceToUse_DruidAquaticForm)),
                    new Action(delegate { SpellManager.Cast(_spellId_DruidAquaticForm); })),

                // Move...
                new Action(delegate
                {
                    // Try to use Navigator to get there...
                    Vector3 location = locationDelegate();
                    MoveResult moveResult = Navigator.MoveTo(location);

                    // If Navigator fails, fall back to click-to-move...
                    if ((moveResult == MoveResult.Failed) || (moveResult == MoveResult.PathGenerationFailed))
                    { WoWMovement.ClickToMove(location); }
                }),

                new WaitContinue(_delay_WoWClientMovementThrottle, ret => false, new ActionAlwaysSucceed())
                )
            );
        }


        private Vector3 FindNearestHotspot()
        {
            Vector3 nearestHotspot = _hotSpots.OrderBy(hotspot => Me.Location.Distance(hotspot)).FirstOrDefault();

            // Rotate the hotspot queue such that the nearest hotspot is on top...
            while (_hotSpots.Peek() != nearestHotspot)
            {
                Vector3 tmpVector3 = _hotSpots.Dequeue();

                _hotSpots.Enqueue(tmpVector3);
            }

            return (nearestHotspot);
        }


        private Vector3 FindNextHotspot()
        {
            Vector3 currentHotspot = _hotSpots.Peek();

            // If we haven't reached the current hotspot, it is still the 'next' one...
            if (!Navigator.AtLocation(currentHotspot))
            { return (currentHotspot); }

            // Otherwise, rotate to the next hotspot in the list...
            _hotSpots.Enqueue(currentHotspot);
            _hotSpots.Dequeue();

            return (_hotSpots.Peek());
        }


        public void UseHotspots(IEnumerable<Vector3> _hotspots)
        {
            _hotspots = _hotspots ?? new Vector3[0];

            _hotSpots = new Queue<Vector3>(_hotspots);

            if (_hotSpots.Count() <= 0)
            { _hotSpots.Enqueue(HuntingGroundAnchor); }

            FindNearestHotspot();
        }
    }

    #endregion      // Reusable behaviors


    #region Extensions to HBcore

    public class DecoratorContinueThrottled : DecoratorContinue
    {
        public DecoratorContinueThrottled(TimeSpan throttleTime,
                                          CanRunDecoratorDelegate canRun,
                                          Composite composite)
            : base(canRun, composite)
        {
            _throttleTime = throttleTime;

            _throttle = new Stopwatch();
            _throttle.Reset();
            _throttle.Start();
        }


        protected override bool CanRun(object context)
        {
            if (_throttle.Elapsed < _throttleTime)
            { return (false); }

            _throttle.Reset();
            _throttle.Start();

            return (base.CanRun(context));
        }


        private Stopwatch _throttle;
        private TimeSpan _throttleTime;
    }


    public class DecoratorThrottled : Decorator
    {
        public DecoratorThrottled(TimeSpan throttleTime,
                                  CanRunDecoratorDelegate canRun,
                                  Composite composite)
            : base(canRun, composite)
        {
            _throttleTime = throttleTime;

            _throttle = new Stopwatch();
            _throttle.Reset();
            _throttle.Start();
        }


        protected override bool CanRun(object context)
        {
            if (_throttle.Elapsed < _throttleTime)
            { return (false); }

            _throttle.Reset();
            _throttle.Start();

            return (base.CanRun(context));
        }


        private Stopwatch _throttle;
        private TimeSpan _throttleTime;
    }


    // The HBcore 'global' blacklist will also prevent looting.  We don't want that.
    // Since the HBcore blacklist is not built to instantiate, we have to roll our
    // own.  <sigh>
    public class LocalBlackList
    {
        public LocalBlackList(TimeSpan maxSweepTime)
        {
            _maxSweepTime = maxSweepTime;
            _stopWatchForSweeping.Start();
        }

        private Dictionary<WoWGuid, DateTime> _blackList = new Dictionary<WoWGuid, DateTime>();
        private TimeSpan _maxSweepTime;
        private Stopwatch _stopWatchForSweeping = new Stopwatch();


        public void Add(WoWGuid guid, TimeSpan timeSpan)
        {
            if (_stopWatchForSweeping.Elapsed > _maxSweepTime)
            { RemoveExpired(); }

            _blackList[guid] = DateTime.Now.Add(timeSpan);
        }


        public bool Contains(WoWGuid guid)
        {
            if (_stopWatchForSweeping.Elapsed > _maxSweepTime)
            { RemoveExpired(); }

            return (_blackList.ContainsKey(guid));
        }


        public void RemoveExpired()
        {
            DateTime now = DateTime.Now;

            List<WoWGuid> expiredEntries = (from key in _blackList.Keys
                                            where (_blackList[key] < now)
                                            select key).ToList();

            foreach (WoWGuid entry in expiredEntries)
            { _blackList.Remove(entry); }

            _stopWatchForSweeping.Reset();
            _stopWatchForSweeping.Start();
        }
    }


    public static class WoWObject_Extensions
    {
        private static LocalPlayer Me { get { return (StyxWoW.Me); } }

        // We provide our own 'local' blacklist.  If we use the global one maintained by HBcore,
        // that will prevent us from looting also.
        private static LocalBlackList s_blackList = new LocalBlackList(TimeSpan.FromSeconds(30));
        private static LocalBlackList s_blackListLooting = new LocalBlackList(TimeSpan.FromSeconds(30));

        public static void LocallyBlacklist(this WoWObject wowObject,
                                                 TimeSpan timeSpan)
        {
            s_blackList.Add(wowObject.Guid, timeSpan);
        }

        public static void LootingBlacklist(this WoWObject wowObject,
                                                 TimeSpan timeSpan)
        {
            s_blackListLooting.Add(wowObject.Guid, timeSpan);
        }


        public static bool IsLocallyBlacklisted(this WoWObject wowObject)
        {
            return (s_blackList.Contains(wowObject.Guid));
        }

        public static bool IsLootingBlacklisted(this WoWObject wowObject)
        {
            return (s_blackListLooting.Contains(wowObject.Guid));
        }
    }


    public static class WoWUnit_Extensions
    {
        private static LocalPlayer Me { get { return (StyxWoW.Me); } }

        public static bool IsInOurParty(this WoWUnit wowUnit)
        {
            return ((Me.PartyMembers.FirstOrDefault(partyMember => (partyMember.Guid == wowUnit.Guid))) != null);
        }
    }

    #endregion      // Extensions to HBcore
}
