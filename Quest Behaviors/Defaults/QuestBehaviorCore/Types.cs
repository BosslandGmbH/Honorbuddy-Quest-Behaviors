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
        public static LocalPlayer Me { get { return StyxWoW.Me; } }
        public static readonly Random _random = new Random((int)DateTime.Now.Ticks);
    }


    public static class Delay
    {

        //----------
        // Commonly used 'Constants'
        //
        public static TimeSpan AfterWeaponFire
        {
            get
            {
                return (_random.Next(1, 100) < 70)
                        ? TimeSpan.FromMilliseconds(_random.Next(100, 350))
                        : TimeSpan.FromMilliseconds(_random.Next(250, 1200));
            }
        }
        public static TimeSpan AfterItemUse { get { return TimeSpan.FromMilliseconds(_random.Next(400, 900)); } }
        public static TimeSpan AfterInteraction { get { return TimeSpan.FromMilliseconds(_random.Next(600, 1700)); } }
        public static TimeSpan BeforeButtonClick { get { return TimeSpan.FromMilliseconds(_random.Next(400, 900)); } }
        public static readonly TimeSpan LagDuration = TimeSpan.FromMilliseconds((StyxWoW.WoWClient.Latency * 2) + 150);

        private static readonly Random _random = new Random((int)DateTime.Now.Ticks +1);
    }

        
    public static class Throttle
    {
        public static readonly TimeSpan WoWClientMovement = TimeSpan.FromMilliseconds(100);
        public static readonly TimeSpan UserUpdate = TimeSpan.FromMilliseconds(1000);
    }


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