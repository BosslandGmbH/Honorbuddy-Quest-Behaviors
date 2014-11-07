#region Usings
using Styx;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
	public static partial class UtilityCoroutine
	{
		// Convenience properties...
		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}
	}
}