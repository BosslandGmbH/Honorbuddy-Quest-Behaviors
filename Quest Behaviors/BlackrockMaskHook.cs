using System.Collections.Generic;
using System.Linq;
using Styx;
using Styx.Common;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.BlackrockMaskHook
{
    [CustomBehaviorFileName(@"BlackrockMaskHook")]
    public class BlackrockMaskHook : CustomForcedBehavior
    {
        public BlackrockMaskHook(Dictionary<string, string> args)
            : base(args)
        {
        }

        public override bool IsDone
        {
            get
            {
                return _inserted;
            }
        }

        private bool _inserted;

        private static WoWItem Disguise
        {
            get
            {
                return StyxWoW.Me.BagItems.FirstOrDefault(r => r.Entry == 63357);
            }
        }

        private static bool Disguised
        {
            get { return StyxWoW.Me.HasAura(89261); }
        }

        private static Composite _myHook;

        private static Composite MyHook
        {
            get
            {
                return _myHook ??
                       (_myHook =
                        new Decorator(
                            r =>
                            Disguise != null && StyxWoW.Me.IsAlive && !StyxWoW.Me.Combat && StyxWoW.Me.ZoneId == 46 &&
                            !Disguised, new Action(r =>
                            {
                                Navigator.PlayerMover.MoveStop();
                                Disguise.Use();
                            })));
            }
            set
            {
                _myHook = value;
            }
        }

        public override void OnStart()
        {
            OnStart_HandleAttributeProblem();

            if (_myHook == null)
            {
                Logging.Write("Inserting blackrock hook - gfrsa");
                TreeHooks.Instance.InsertHook("Questbot_Main", 0, MyHook);
            }
            else
            {
                Logging.Write("removing blackrock hook - gfrsa");
                TreeHooks.Instance.RemoveHook("Questbot_Main", MyHook);
                MyHook = null;
            }
            _inserted = true;

        }
    }
}
