using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hera.Helpers
{
    public static class MSH
    {
        public static string RawSetting { get; set; }
        public static bool OnAdds { get { return RawSetting.Contains("only on adds"); } }
        public static bool Always { get { return RawSetting.Contains("always"); } }
        public static bool Never { get { return RawSetting.Contains("never"); } }
        public static bool OnRunners { get { return RawSetting.Contains("on runners"); } }
        public static bool IfCasting { get { return RawSetting.Contains("is casting"); } }
    }
}
