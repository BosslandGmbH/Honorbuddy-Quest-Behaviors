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

using TreeSharp;

using Action = TreeSharp.Action;

namespace Styx.Bot.Quest_Behaviors
{
    public class ForcedMount : CustomForcedBehavior
    {
        private enum ForcedMountType
        {
            Ground,
            Flying,
            Water
        }
        Dictionary<string, object> recognizedAttributes = new Dictionary<string, object>()
        {

            {"QuestId",null},
            {"MountType",null},
        };

        private ForcedMountType MountType { get; set; }
        private uint QuestId { get; set; }
        public ForcedMount(Dictionary<string, string> args)
            : base(args)
        {
            CheckForUnrecognizedAttributes(recognizedAttributes);
            bool error = false;

            uint questId = 0;
            if (Args.ContainsKey("QuestId") && !uint.TryParse(Args["QuestId"], out questId))
            {
                Logging.Write("Parsing attribute 'QuestId' in ForcedMount behavior failed! please check your profile!");
                error = true;
            }

            if (!Args.ContainsKey("MountType"))
            {
                Logging.Write("Could not find attribute 'MountType' in ForcedMount behavior! please check your profile!");
                error = true;
            }

            if (error)
            {
                TreeRoot.Stop();
                return;
            }

            var type = (ForcedMountType)Enum.Parse(typeof(ForcedMountType), Args["MountType"], true);

            this.MountType = type;
            this.QuestId = questId;
        }

        public override bool IsDone
        {
            get
            {
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById(QuestId);

                return
                    _done || (QuestId > 0 && quest == null);
            }
        }

        private bool _done;
        private Composite _root;
        protected override Composite CreateBehavior()
        {
            if (_root == null)
            {
                _root = new Sequence(
                    CreateActualBehavior(),
                    new Action(ret => _done = true));
            }
            return _root;
        }

        private Composite CreateActualBehavior()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => MountType == ForcedMountType.Ground,
                    new Action(ret => Mount.MountUp())),

                new Decorator(
                    ret => MountType == ForcedMountType.Water && MountHelper.UnderwaterMounts.Count != 0 && StyxWoW.Me.IsSwimming,
                    new Action(ret => MountHelper.UnderwaterMounts.First().CreatureSpell.Cast())),

                new Decorator(
                    ret =>
                    MountType == ForcedMountType.Flying && (MountHelper.FlyingMounts.Count != 0 ||
                    (StyxWoW.Me.Class == WoWClass.Druid && (SpellManager.HasSpell("Flight Form") || SpellManager.HasSpell("Swift Flight Form")))),
                    new Action(ret => MountForFlying()))
                );
        }

        private void MountForFlying()
        {
            if (StyxWoW.Me.Class == WoWClass.Druid && (SpellManager.HasSpell("Flight Form") || SpellManager.HasSpell("Swift Flight Form")))
            {
                if (SpellManager.CanCast("Swift Flight Form"))
                    SpellManager.Cast("Swift Flight Form");
                else if (SpellManager.CanCast("Flight Form"))
                    SpellManager.Cast("Flight Form");
            }
            else
            {
                MountHelper.FlyingMounts.First().CreatureSpell.Cast();
                while (StyxWoW.Me.IsCasting)
                    Thread.Sleep(100);
            }

            // Hop off the ground. Kthx
            Navigator.PlayerMover.Move(WoWMovement.MovementDirection.JumpAscend);
            Thread.Sleep(250);
            Navigator.PlayerMover.MoveStop();
        }
    }
}
