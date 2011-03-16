using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using Styx.Database;
using Styx.Logic.Combat;
using Styx.Helpers;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Pathing;
using Styx.Logic.Profiles.Quest;
using Styx.Logic.Questing;
using Styx.Logic.BehaviorTree;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;
using System.Xml;

namespace Styx.Bot.Quest_Behaviors
{
    public class Halt : CustomForcedBehavior
    {
        /// <summary>
        /// Halt by Bobby53
        /// 
        /// Stops the Quest Bot.  Will write 'Msg' to the log and Goal Text.
        /// Also write the line number it halted at for easily locating in profile.
        /// 
        /// Useful for testing assumptions in quest profile and during profile
        /// development to force profile to automatically stop at designated point
        /// 
        /// ##Syntax##
        /// [optional] QuestId: Id of the quest (default is 0)
        /// [optional] Msg: text value to display (default says stopped by profile)
        /// [optional] Color: color to use for message in log (default is red)
        /// 
        /// Note:  QuestId behaves the same as on every other behavior.  If 0, then
        /// halt always occurs.  Otherwise, for non-zero QuestId only halts if the
        /// character has the quest and its not completed
        /// </summary>
        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {
            {"Msg",null},
            {"QuestId",null},
            {"Color",null},
        };


        public Halt(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);

            bool error = false;

            uint questId = 0;
            if (Args.ContainsKey("QuestId") && !uint.TryParse(Args["QuestId"], out questId))
            {
                Logging.Write(
                    "Parsing attribute 'QuestId' in Halt behavior failed! please check your profile!");
                error = true;
            }

            Color clr = Color.Red;
            if (Args.ContainsKey("Color") )
            {
                clr = (Color)Enum.Parse(typeof(Color), Args["Color"], true);
            }

            string msg = "Quest Profile HALT";
            GetAttributeAsString("Msg", false, "Halt: Stopped by profile quest behavior", out msg);

            if (error)
                TreeRoot.Stop();

            Msg = msg;
            Color = clr;
            QuestId = questId;
        }

        public string Msg { get; set; }
        public Color Color { get; set; }
        public uint QuestId { get; set; }

        public override void OnStart()
        {
            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
            if (QuestId == 0 || (quest != null && !quest.IsCompleted))
            {
                Logging.Write(Color, "\n{0}", Msg);
                TreeRoot.GoalText = Msg;

                if (((IXmlLineInfo)Element).HasLineInfo())
                {
                    Logging.Write(Color, "stopped @ line {0}\n", ((IXmlLineInfo)Element).LineNumber);
                }

                Logging.Write(" ");
                TreeRoot.Stop();
            }
        }

        protected override Composite CreateBehavior()
        {
            return null;
        }

        public override bool IsDone
        {
            get
            {
                return true;
            }
        }
    }
}

