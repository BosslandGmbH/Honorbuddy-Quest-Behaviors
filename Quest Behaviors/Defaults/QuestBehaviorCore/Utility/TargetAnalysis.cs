// Behavior originally contributed by Nesox / completely reworked by Chinajade
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using Styx.WoWInternals.WoWObjects;

#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public class TargetExclusionAnalysis
    {
        // 30May2013-08:11UTC chinajade
        public static string Analyze(
            XElement xElement,
            Func<IEnumerable<WoWObject>> candidateTargets,
            Func<WoWObject, List<string>> targetExclusionChecksProvider)
        {
            Contract.Requires(candidateTargets != null, context => "candidateTargets != null");
            Contract.Requires(targetExclusionChecksProvider != null, context => "funcTargetExclusionCheck != null");

            var excludedUnitReasons = new StringBuilder();

            foreach (var wowObject in candidateTargets())
            {
                List<string> reasons = targetExclusionChecksProvider(wowObject);

                if (reasons.Count > 0)
                {
                    excludedUnitReasons.AppendFormat("    {0} [{1}]{2}",
                        wowObject.SafeName(),
                        string.Join(", ", reasons),
                        Environment.NewLine);
                }
            }

            if (excludedUnitReasons.Length > 0)
            {
                excludedUnitReasons.Insert(0, string.Format("{0}Excluded Units:{0}", Environment.NewLine));
                excludedUnitReasons.AppendFormat("{0}    {1}",
                    Environment.NewLine,
                    QBCLog.GetXmlFileReference(xElement));
            }

            return excludedUnitReasons.ToString();
        }


        // 30May2013-08:11UTC chinajade
        public static void CheckAuras(
            List<string> exclusionReasons,
            WoWObject wowObject,
            IEnumerable<int> auraIdsRequired,
            IEnumerable<int> auraIdsUnwanted)
        {
            Contract.Requires(exclusionReasons != null, context => "reasons != null");

            if (Query.IsViable(wowObject))
            {
                auraIdsRequired = auraIdsRequired ?? Enumerable.Empty<int>();
                auraIdsUnwanted = auraIdsUnwanted ?? Enumerable.Empty<int>();

                var wowUnit = wowObject.ToUnit();
                if (wowUnit != null)
                {
                    var wowUnitAuras = wowUnit.GetAllAuras().ToList();

                    if (!Query.IsStateMatch_AurasWanted(wowObject, auraIdsRequired))
                    {
                        exclusionReasons.Add(string.Format("MissingRequiredAura({0})",
                            string.Join(",", auraIdsRequired.Select(i => i.ToString()))));
                    }

                    if (!Query.IsStateMatch_AurasMissing(wowObject, auraIdsUnwanted))
                    {
                        exclusionReasons.Add(string.Format("HasUnwantedAura({0})",
                            string.Join(",", wowUnitAuras.Where(a => auraIdsUnwanted.Contains(a.SpellId)).Select(a => a.SpellId.ToString()))
                            ));
                    }
                }
            }
        }


        // 30May2013-08:11UTC chinajade
        public static List<string> CheckCore(
            WoWObject wowObject,
            QuestBehaviorBase coreAttributes)
        {
            var exclusionReasons = new List<string>();

            if (!Query.IsViable(wowObject))
            {
                exclusionReasons.Add("[NotViable]");
                return exclusionReasons;
            }

            if (Query.IsBlacklistedForCombat(wowObject))
                { exclusionReasons.Add("BlacklistedForCombat"); }

            if (Query.IsBlacklistedForInteraction(wowObject))
                { exclusionReasons.Add("BlacklistedForInteract"); }

            if (!Query.IsStateMatch_IgnoreMobsInBlackspots(wowObject, coreAttributes.IgnoreMobsInBlackspots))
            {
                // TODO: Would be better to identify the offending blackspots, rather than the object location...
                exclusionReasons.Add(string.Format("InBlackspot(object @{0})", wowObject.Location));
            }

            if (!Query.IsStateMatch_MeshNavigable(wowObject, coreAttributes.MovementBy))
                { exclusionReasons.Add("NotMeshNavigable"); }

            if (Query.IsInCompetition(wowObject, coreAttributes.NonCompeteDistance))
            {
                int playerCount = Query.FindPlayersNearby(wowObject.Location, coreAttributes.NonCompeteDistance).Count();

                if (playerCount > 0)
                {
                    exclusionReasons.Add(string.Format("InCompetition({0} players within {1:F1})",
                        playerCount,
                        coreAttributes.NonCompeteDistance));
                }

                var wowUnit = wowObject.ToUnit();
                bool isTagged = (wowUnit != null) && !Query.IsSharedWorldResource(wowUnit) && !wowUnit.IsUntagged();
                if (isTagged)
                {
                    exclusionReasons.Add("TaggedByOtherPlayer");
                }
            }

            return exclusionReasons;
        }


        // 30May2013-08:11UTC chinajade
        public static void CheckMobState(
            List<string> exclusionReasons,
            WoWObject wowObject,
            MobStateType mobState,
            double mobLowHealthThreshold)
        {
            var wowUnit = wowObject.ToUnit();

            if (Query.IsViable(wowUnit))
            {
                if (!Query.IsStateMatch_MobState(wowUnit, mobState, mobLowHealthThreshold))
                {
                    exclusionReasons.Add(mobState == MobStateType.BelowHp
                        ? string.Format("!{0}({1}%)", mobState, mobLowHealthThreshold)
                        : string.Format("!{0}", mobState));
                }
            }
        }
    }
}
