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
using System.Linq;

using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public abstract partial class QuestBehaviorBase
    {
        /// <summary>
        /// <para>Returns true if you can cast the PETACTIONNAME; otherwise, false.</para>
        /// <para>This method checks for both spell existence, and if the spell is on cooldown.</para>
        /// <para>Notes:<list type="bullet">
        /// 
        /// <item><description><para>* To return 'true', the PETACTIONNAME spell must be hot-barred!  Existence in the spellbook
        /// is insufficient.  This is a limitation of the WoWclient and HB APIs.</para></description></item>
        /// </list></para>
        /// </summary>
        /// <param name="petActionName">may not be null. Examples include: "Follow", "Attack", "Passive", "Growl".</param>
        /// <returns></returns>
        // 24Feb2013-07:42UTC chinajade
        public static bool CanCastPetAction(string petActionName)
        {
            ContractRequires(!string.IsNullOrEmpty(petActionName), context => "petActionName may not be null or empty");

            WoWPetSpell petAction = Me.PetSpells.FirstOrDefault(p => p.ToString() == petActionName);
            if (petAction == null)
                { return false; }
            if ((petAction.SpellType == WoWPetSpell.PetSpellType.Spell) && (petAction.Spell == null))
                { return false; }

            return (petAction.SpellType == WoWPetSpell.PetSpellType.Spell)
                ? !petAction.Spell.Cooldown
                : true;
        }


        // 15May2013-07:42UTC chinajade
        public static bool CanPetBeDismissed()
        {
            return Lua.GetReturnVal<bool>("return PetCanBeDismissed()", 0);
        }


        /// <summary>
        /// <para>Casts the PETACTIONNAME.</para>
        /// <para>Notes:<list type="bullet">
        /// <item><description><para>* The PETACTIONNAME spell must be hot-barred!  Existence in the spellbook
        /// is insufficient.  This is a limitation of the WoWclient and HB APIs.</para></description></item>
        /// <item><description><para>* If PETACTIONNAME doesn't exist, or is not hot-barred
        /// an error message is emitted.  To avoid this, use CanCastPetAction() as an entry condition to use of this method.</para></description></item>
        /// <item><description><para>* If PETACTIONNAME is on cooldown, no action is performed.</para></description></item>
        /// </list></para>
        /// </summary>
        /// <param name="petActionName">may not be null. Examples include: "Follow", "Attack", "Passive", "Growl".</param>
        // 24Feb2013-07:42UTC chinajade
        public static void CastPetAction(string petActionName)
        {
            ContractRequires(!string.IsNullOrEmpty(petActionName), context => "petActionName may not be null or empty");

            WoWPetSpell petAction = Me.PetSpells.FirstOrDefault(p => p.ToString() == petActionName);
            if (petAction == null)
            {
                LogMaintenanceError("or [USER ERROR]: PetAction('{0}') is either not known, or not hot-barred.",
                    petActionName);
                return;
            }

            LogDeveloperInfo("Instructing pet to \"{0}\"", petActionName);
            Lua.DoString("CastPetAction({0})", petAction.ActionBarIndex +1);
        }


        /// <summary>
        /// <para>Casts the PETACTIONNAME on WOWUNIT.</para>
        /// <para>Notes:<list type="bullet">
        /// <item><description><para>* The PETACTIONNAME spell must be hot-barred!  Existence in the spellbook
        /// is insufficient.  This is a limitation of the WoWclient and HB APIs.</para></description></item>
        /// <item><description><para>* If PETACTIONNAME doesn't exist, or is not hot-barred
        /// an error message is emitted.  To avoid this, use CanCastPetAction() as an entry condition to use of this method.</para></description></item>
        /// <item><description><para>* If PETACTIONNAME is on cooldown, no action is performed.</para></description></item>
        /// </list></para>
        /// </summary>
        /// <param name="petActionName">may not be null. Examples include: "Follow", "Attack", "Passive", "Growl".</param>
        /// <param name="wowUnit">may not be null</param>
        // 24Feb2013-07:42UTC chinajade
        public static void CastPetAction(string petActionName, WoWUnit wowUnit)
        {
            ContractRequires(!string.IsNullOrEmpty(petActionName), context => "petActionName may not be null or empty");
            ContractRequires(wowUnit != null, context => "wowUnit may not be null");

            WoWPetSpell petAction = Me.PetSpells.FirstOrDefault(p => p.ToString() == petActionName);
            if (petAction == null)
            {
                LogMaintenanceError("or [USER ERROR]: PetAction('{0}') is either not known, or not hot-barred.",
                    petActionName);
                return;
            }

            LogDeveloperInfo("Instructing pet \"{0}\" on {1}", petActionName, wowUnit.Name);
            uint originalFocus = Me.CurrentFocus;
            StyxWoW.Me.SetFocus(wowUnit);
            Lua.DoString("CastPetAction({0}, 'focus')", petAction.ActionBarIndex +1);
            StyxWoW.Me.SetFocus(originalFocus);
        }


        // 15May2013-07:42UTC chinajade
        public static void DismissPet()
        {
            Lua.DoString("PetDismiss()");

            // Remove totems (or DK's ghoul--which is Totem #1)...
            // Ref: http://wowprogramming.com/docs/api/DestroyTotem
            for (int totemNum = 1; totemNum <= 4;  ++totemNum)
                { Lua.DoString(string.Format("DestroyTotem({0})", totemNum)); }

            // TODO: Need to eliminate minions here, also
        }


        /// <summary>
        /// <para>Returns true if the pet is executing the PETACTIONNAME.</para>
        /// <para>The way the WoWclient works, because a PetAction is active doesn't necessarily mean it is being immediately obeyed.</para>
        /// <para>For instance, a pet's "Passive" ability may be active, but the pet may be attacking due to an explicit "Attack" command temporarily overriding it.</para>
        /// <para>During the attack, the "Passive" ability still shows active.</para>
        /// <para>Notes:<list type="bullet">
        /// <item><description><para>* The PETACTIONNAME spell must be hot-barred!  Existence in the spellbook
        /// is insufficient.  This is a limitation of the WoWclient and HB APIs.</para></description></item>
        /// <item><description><para>* If PETACTIONNAME doesn't exist, or is not hot-barred, 'false' is returned.</para></description></item>
        /// </list></para>
        /// </summary>
        /// <param name="petActionName">may not be null. Examples include: "Follow", "Attack", "Passive"</param>
        /// <returns></returns>
        // 24Feb2013-07:42UTC chinajade
        public static bool IsPetActionActive(string petActionName)
        {
            ContractRequires(!string.IsNullOrEmpty(petActionName), context => "petActionName may not be null or empty");

            WoWPetSpell petAction = Me.PetSpells.FirstOrDefault(p => p.ToString() == petActionName);
            if (petAction == null)
                { return false; }

            return Lua.GetReturnVal<bool>(string.Format("return GetPetActionInfo({0})", petAction.ActionBarIndex +1), 4);
        }


        /// <summary>
        /// <para>Sends the user's pet to attack the target identified by WOWUNITDELEGATE.</para>
        /// <para>Notes:<list type="bullet">
        /// <item><description><para>* This behavior performs all appropriate checks: pet exists and is alive, target is viable and not friendly, etc.</para></description></item>
        /// <item><description><para>* The 'attack' command will continue to be issued until the pet obeys (by targeting the mob).</para></description></item>
        /// <item><description><para>* The pet's "Attack" command must be hot-barred!  Existence in the spellbook
        /// is insufficient.  This is a limitation of the WoWclient and HB APIs.</para></description></item>
        /// <item><description><para>* The returned Composite is suitable for use in a behavior tree (Priority)Selector container
        /// (i.e., placing it in a Sequence container will not yield the desired results).</para></description></item>
        /// </list></para>
        /// </summary>
        /// <param name="wowUnitDelegate">may not be null</param>
        /// <returns>a behavior tree Composite suitable for use in a (Priority)Selector container</returns>
        public static Composite UtilityBehaviorPS_PetActionAttack(ProvideWoWUnitDelegate wowUnitDelegate)
        {
            ContractRequires(wowUnitDelegate != null, context => "wowUnitDelegate may not be null");

            const string spellName = "Attack";

            // NB: We can't issue "Attack" directive while mounted, so don't try...
            return new Decorator(context => Me.GotAlivePet
                                            && !Me.Mounted
                                            && IsViableForFighting(wowUnitDelegate(context))
                                            && (Me.Pet.CurrentTarget != wowUnitDelegate(context))
                                            && CanCastPetAction(spellName),
                new Action(context => CastPetAction(spellName, wowUnitDelegate(context))));
        }


        /// <summary>
        /// <para>Instructs the user's pet to follow its owner.</para>
        /// <para>Notes:<list type="bullet">
        /// <item><description><para>* This behavior performs all appropriate checks: pet exists and is alive, etc.</para></description></item>
        /// <item><description><para>* If the pet is attacking a mob, the 'follow' command will continue to be issued until the pet obeys.</para></description></item>
        /// <item><description><para>* The pet's "Follow" command must be hot-barred!  Existence in the spellbook
        /// is insufficient.  This is a limitation of the WoWclient and HB APIs.</para></description></item>
        /// <item><description><para>* The returned Composite is suitable for use in a behavior tree (Priority)Selector container
        /// (i.e., placing it in a Sequence container will not yield the desired results).</para></description></item>
        /// </list></para>
        /// </summary>
        /// <returns>a behavior tree Composite suitable for use in a (Priority)Selector container</returns>
        public static Composite UtilityBehaviorPS_PetActionFollow()
        {
            const string spellName = "Follow";

            // NB: We can't issue "Follow" directive while mounted, so don't try...
            return new Decorator(context => Me.GotAlivePet
                                            && !Me.Mounted
                                            && CanCastPetAction(spellName)
                                            && (!IsPetActionActive(spellName) || IsViableForFighting(Me.Pet.CurrentTarget)),
                new Action(context => CastPetAction(spellName)));
        }
        

        /// <summary>
        /// <para>Instructs the user's pet to assume one of the following stances: "Assist", "Defensive", "Passive"</para>
        /// <para>Notes:<list type="bullet">
        /// <item><description><para>* This behavior performs all appropriate checks: pet exists and is alive, etc.</para></description></item>
        /// <item><description><para>* The pet's "Assist", "Defensive", and "Passive" commands must be hot-barred!  Existence in the spellbook
        /// is insufficient.  This is a limitation of the WoWclient and HB APIs.</para></description></item>
        /// <item><description><para>* The returned Composite is suitable for use in a behavior tree (Priority)Selector container
        /// (i.e., placing it in a Sequence container will not yield the desired results).</para></description></item>
        /// </list></para>
        /// </summary>
        /// <param name="petStanceNameDelegate"></param>
        /// <returns>a behavior tree Composite suitable for use in a (Priority)Selector container</returns>
        public static Composite UtilityBehaviorPS_PetSetStance(ProvideStringDelegate petStanceNameDelegate)
        {
            string[] knownStanceNames = { "Assist", "Defensive", "Passive" };

            // We can't change pet stance while mounted, so don't try...
            return new Decorator(context => !Me.Mounted,
                new PrioritySelector(petStanceNameContext => petStanceNameDelegate(petStanceNameContext),
                    new Decorator(petStanceNameContext => !knownStanceNames.Contains((string)petStanceNameContext),
                        new Action(petStanceNameContext =>
                        {
                            LogMaintenanceError("Unknown pet stance '{0}'.  Must be one of: {1}",
                                (string)petStanceNameContext,
                                string.Join(", ", knownStanceNames));
                        })),

                    new Decorator(petStanceNameContext => Me.GotAlivePet
                                                            && CanCastPetAction((string)petStanceNameContext)
                                                            && !IsPetActionActive((string)petStanceNameContext),
                        new Action(petStanceNameContext => { CastPetAction((string)petStanceNameContext); }))
                ));
        }
    }
}