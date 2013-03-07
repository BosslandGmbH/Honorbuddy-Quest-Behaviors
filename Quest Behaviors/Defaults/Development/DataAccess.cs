// Behavior originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

//==================================================
// COPY WHAT YOU NEED FROM THIS FILE INTO YOUR QUEST BEHAVIOR:
// These are functional code snippets to be used for Quest Behavior (or other) development.
// Please copy whatever snippets you need from this file into TEMPLATE.cs.  We did not place them
// in TEMPLATE.cs to prevent unnecessarily bloating the base file.
// BE CERTAIN, that your namespace is unique, as other behaviors may be using the same snippets. (i.e.,
// Honorbuddy does not provide a location for common methods that are highly reusable in Quest Behaviors).
//==================================================

#region Usings
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using Styx.Helpers;
#endregion


namespace Honorbuddy.QuestBehaviors.TEMPLATE_QB
{
    public partial class TEMPLATE_QB
    {
        /// <summary>
        /// <para>Reads the "Quest Behaviors/DATA/AuraIds_OccupiedVehicle.xml" file, and returns an IEnumerable
        /// of all the AuraIds that are represent Vehicles that are occupied.</para>
        /// <para>If the da file has malformed entries (which it should never be), error messages
        /// will be emitted.</para>
        /// </summary>
        /// <returns>the IEnumerable may be empty, but it will never be null.</returns>
        //  7Mar2013-02:28UTC chinajade
        public IEnumerable<int> GetOccupiedVehicleAuraIds()
        {
            List<int> occupiedVehicleAuraIds = new List<int>();
            string auraDataFileName = Path.Combine(GlobalSettings.Instance.QuestBehaviorsPath, "DATA", "AuraIds_OccupiedVehicle.xml");

            if (!File.Exists(auraDataFileName))
            {
                LogWarning("Unable to locate Occupied Vehicle Aura database (in {0}).  Vehicles will be unqualified"
                    + "--this may cause us to follow vehicles occupied by other players.",
                    auraDataFileName);
                return occupiedVehicleAuraIds;
            }

            XDocument xDoc = XDocument.Load(auraDataFileName);

            foreach (XElement aura in xDoc.Descendants("Auras").Elements())
            {
                string elementAsString = aura.ToString();

                XAttribute spellIdAttribute = aura.Attribute("SpellId");
                if (spellIdAttribute == null)
                {
                    LogError("Unable to locate SpellId attribute for {0}", elementAsString);
                    continue;
                }

                int auraSpellId;
                if (!int.TryParse(spellIdAttribute.Value, out auraSpellId))
                {
                    LogError("Unable to parse SpellId attribute for {0}", elementAsString);
                    continue;
                }

                occupiedVehicleAuraIds.Add(auraSpellId);
            }

            return occupiedVehicleAuraIds;
        }
    }
}
