using System;
using System.Collections.Generic;
using System.Drawing;
using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Questing;

namespace Styx.Bot.Quest_Behaviors
{
    /// <summary>
    /// Behavior for forcing train/mail/vendor/repair
    /// Example usage: <CustomBehavior QuestId="14324" File="ForceSetVendor" VendorType="Train" />
    /// QuestId is optional, if you don't use it make sure you put this tag inside an 'If'
    /// </summary>
    public class ForceSetVendor : CustomForcedBehavior
    {
        public enum VendorType
        {
            Mail,
            Repair,
            Sell,
            Train,
        }

        #region Overrides of CustomForcedBehavior

        public ForceSetVendor(Dictionary<string, string> args)
            : base(args)
        {
            if(Args.ContainsKey("QuestId"))
            {
                uint questId;
                if(!uint.TryParse(Args["QuestId"], out questId))
                {
                    Logging.Write(Color.Red, "Unable to parse attribute 'QuestId' in ForceSetVendor tag: {0}", Element.ToString());
                    TreeRoot.Stop();
                }

                QuestId = questId;
            }

            try
            {
                var type = (VendorType)Enum.Parse(typeof(VendorType), Args["VendorType"], true);
                Type = type;
            }
            catch (Exception)
            {
                Logging.Write(Color.Red, "Unable to parse attribute 'VendorType' in ForceSetVendor tag: {0}", Element.ToString());
                TreeRoot.Stop();
            }
        }

        /// <summary>
        /// The QuestId for this behavior, if any. (not required)
        /// </summary>
        public uint QuestId { get; private set; }

        /// <summary>
        /// The vendor type for this behavior.
        /// Mail/Repair/Sell/Train
        /// </summary>
        public VendorType Type { get; private set; }
        
        public override void OnStart()
        {
            if (!IsDone || QuestId == 0)
            {
                switch (Type)
                {
                    case VendorType.Mail:
                        Vendors.ForceMail = true;
                        break;

                    case VendorType.Repair:
                        Vendors.ForceRepair = true;
                        break;

                    case VendorType.Sell:
                        Vendors.ForceSell = true;
                        break;

                    case VendorType.Train:
                        Vendors.ForceTrainer = true;
                        break;
                }

                _isDone = true;
            }
        }

        private bool _isDone;
        public override bool IsDone
        {
            get
            {
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
				return _isDone || (QuestId > 0 && quest == null) || (quest != null && quest.IsCompleted);
            }
        }

        #endregion
    }
}
