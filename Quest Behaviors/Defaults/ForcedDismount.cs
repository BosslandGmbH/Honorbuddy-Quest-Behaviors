using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;

using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    public class ForcedDismount : CustomForcedBehavior
    {
        private enum ForcedDismountType
        {
            Any,
            Ground,
            Flying,
            Water
        }

        public LocalPlayer Me { get { return ObjectManager.Me; } }
        private ForcedDismountType MountType { get; set; }
        private uint QuestId { get; set; }

        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"QuestId",null},
            {"MountType",null},
        };

        public ForcedDismount(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);
            bool error = false;

            if (!Args.ContainsKey("QuestId"))
            {
                Logging.Write("Could not find attribute 'QuestId' in ForcedDismount behavior! please check your profile!");
                error = true;
            }

            uint questId = 0;
            if ( Args.ContainsKey( "QuestId") && !uint.TryParse(Args["QuestId"], out questId))
            {
                Logging.Write("Parsing attribute 'QuestId' in ForcedDismount behavior failed! please check your profile!");
                error = true;
            }

            ForcedDismountType typeMount = ForcedDismountType.Any;
            if (Args.ContainsKey("MountType"))
                typeMount = (ForcedDismountType)Enum.Parse(typeof(ForcedDismountType), Args["MountType"], true);

            if (error)
            {
                TreeRoot.Stop();
                return;
            }

            this.QuestId = questId;
            this.MountType = typeMount;
        }

        public override bool IsDone
        {
            get
            {
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);
                return  _done || (QuestId != 0 && quest != null);
            }
        }

        private bool _done;
        private Composite _root;
        protected override Composite CreateBehavior()
        {
            if (_root == null)
            {
                _root = new PrioritySelector(
                    new Decorator(
                        ret => !Me.Mounted,
                        new Action(ret => _done = true)),
                    new Decorator(
                        ret => Me.Mounted,
                        new Action(ret => Dismount()))
                );
            }
            return _root;
        }

        private void Dismount()
        {
            // if in the air, 
            if (StyxWoW.Me.IsFlying)
            {
                Logging.WriteDebug("ForcedDismount:  descending before dismount");
                Navigator.PlayerMover.Move(WoWMovement.MovementDirection.Descend);
                while (StyxWoW.Me.IsFlying)
                    Thread.Sleep(250);

                Navigator.PlayerMover.MoveStop();
            }

            if (StyxWoW.Me.Auras.ContainsKey("Flight Form"))
            {
                Logging.WriteDebug("ForcedDismount:  cancelling Flight Form");
                CancelAura("Flight Form");
            }
            else if (StyxWoW.Me.Auras.ContainsKey("Swift Flight Form"))
            {
                Logging.WriteDebug("ForcedDismount:  cancelling Swift Flight Form");
                CancelAura("Swift Flight Form");
            }
            else
            {
                Logging.WriteDebug("ForcedDismount:  dismounting");
                Mount.Dismount();
            }
        }

        private void CancelAura(string sAura)
        {
            Lua.DoString(string.Format("RunMacroText(\"/cancelaura {0}\")", sAura), 0);
        }

    }
}
