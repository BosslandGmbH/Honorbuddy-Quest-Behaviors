// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Usings
using System;
using System.Text;

using Styx.CommonBot;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public class QBCDebug
    {
        public static string DumpTargetList()
        {
            var buffer = new StringBuilder();
            var newLine = Environment.NewLine;
            const ulong guidMask = 0x0ffff;

            buffer.AppendFormat("Target List ({1} entries):{0}", newLine, Targeting.Instance.TargetList.Count);
            foreach (var target in Targeting.Instance.TargetList)
            {
                buffer.AppendFormat("    {1}.{2:X4}{0}", newLine, target.SafeName(), (target.Guid & guidMask));
            }

            return buffer.ToString();
        }
    }
}