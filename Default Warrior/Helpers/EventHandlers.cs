using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using Hera.SpellsMan;
using Styx;
using Styx.Helpers;
using Styx.Logic;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Hera.Helpers
{
    public static class EventHandlers
    {
        private static LocalPlayer Me { get { return ObjectManager.Me; } }
        private static WoWUnit CT { get { return Me.CurrentTarget; } }

        public static void CombatLogEventHander(object sender, LuaEventArgs args)
        {
            foreach (object arg in args.Args)
            {
                if (arg is String)
                {
                    var s = (string)arg;
                    if (s.ToUpper() == "EVADE")
                    {
                        if (Me.GotTarget)
                        {
                            Logging.Write("My target is Evade bugged, blacking " + CT.Name);
                            Target.BlackList(3600);
                            Lua.DoString("StopAttack() PetStopAttack() PetFollow()");
                            StyxWoW.Me.ClearTarget();
                        }
                    }
                }
            }
        }

        public static void TalentPointEventHander(object sender, LuaEventArgs args)
        {
            ClassHelper.ClassSpec = (ClassType)Talents.Spec;
        }

     
    }
}
