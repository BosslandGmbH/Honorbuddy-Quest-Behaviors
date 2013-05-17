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

using Honorbuddy.QuestBehaviorCore.XmlElements;
using Styx;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public abstract partial class QuestBehaviorBase
    {
        //----------
        // Commonly used 'Constants'
        //
        protected static TimeSpan Delay_AfterItemUse { get { return TimeSpan.FromMilliseconds(_random.Next(400, 900)); } }
        protected static TimeSpan Delay_AfterInteraction { get { return TimeSpan.FromMilliseconds(_random.Next(600, 1700)); } }
        protected static readonly TimeSpan Delay_LagDuration = TimeSpan.FromMilliseconds((StyxWoW.WoWClient.Latency * 2) + 150);
        protected static readonly TimeSpan Throttle_WoWClientMovement = TimeSpan.FromMilliseconds(100);
        protected static readonly TimeSpan Throttle_UserUpdate = TimeSpan.FromMilliseconds(1000);
        protected static LocalPlayer Me { get { return StyxWoW.Me; } }
        public static readonly Random _random = new Random((int)DateTime.Now.Ticks);


        //----------
        // Types
        //
        public enum MobStateType
        {
            // NPC states
            Alive,
            AliveNotInCombat,
            BelowHp,
            Dead,

            DontCare,
        }


        public enum MobType
        {
            Npc,
            GameObject,
        }


        public enum MountStrategyType
        {
            CancelShapeshift,
            Dismount,
            DismountOrCancelShapeshift,
            Mount,
            None,
        }


        public enum MovementByType
        {
            ClickToMoveOnly,
            FlightorPreferred,
            NavigatorOnly,
            NavigatorPreferred,
            None
        }


        // Delegates
        public delegate bool ProvideBoolDelegate(object context);
        public delegate int ProvideIntDelegate(object context);
        public delegate double ProvideDoubleDelegate(object context);
        public delegate string ProvideStringDelegate(object context);
        public delegate HuntingGroundsType ProvideHuntingGroundsDelegate(object context);
        public delegate WoWItem ProvideWoWItemDelegate(object context);
        public delegate WoWObject ProvideWoWObjectDelegate(object context);
        public delegate WoWPoint ProvideWoWPointDelegate(object context);
        public delegate WoWUnit ProvideWoWUnitDelegate(object context);


        public class ContractException : SystemException
        {
            public ContractException(string message = null)
                : base(message ?? string.Empty)
            {
                // empty   
            }
        }
    }
}