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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml.Linq;

using Styx.Helpers;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public partial class QuestBehaviorBase
    {
        /// <summary>
        /// <para>Reads the "Quest Behaviors/DATA/AuraIds_OccupiedVehicle.xml" file, and returns an IEnumerable
        /// of all the AuraIds that are represent Vehicles that are occupied.</para>
        /// <para>If the da file has malformed entries (which it should never be), error messages
        /// will be emitted.</para>
        /// </summary>
        /// <returns>the IEnumerable may be empty, but it will never be null.</returns>
        //  7Mar2013-02:28UTC chinajade
        public static IEnumerable<int> GetOccupiedVehicleAuraIds()
        {
            var occupiedVehicleAuraIds = new List<int>();

            string auraDataFileName = GetDataFileFullPath("AuraIds_OccupiedVehicle.xml");

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


        private static string GetDataFileFullPath(string fileName)
        {
            // NB: We use the absolute path here.  If we don't, then QBs get confused if there are additional
            // QBs supplied in the Honorbuddy/Default Profiles/. directory.
            return Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName),
                                GlobalSettings.Instance.QuestBehaviorsPath,
                                "QuestBehaviorCore",
                                "Data",
                                fileName);
        }
    }
}
