using System.Collections.Generic;

using HarmonyLib;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;

namespace MarriageExtended.Patches
{
    /// <summary>
    /// Harmony patches for RomanceCampaignBehavior to include companions in marriage options.
    /// </summary>
    [HarmonyPatch]
    internal static class RomanceBehaviorPatches
    {
        /// <summary>
        /// Patches FindPlayerRelativesEligibleForMarriage to include companions.
        /// In vanilla, this only returns clan lords (AliveLords). We add companions too.
        /// </summary>
        [HarmonyPatch(typeof(RomanceCampaignBehavior), "FindPlayerRelativesEligibleForMarriage")]
        [HarmonyPostfix]
        private static void FindPlayerRelativesEligibleForMarriage_Postfix(
            Clan withClan,
            ref List<CharacterObject> __result)
        {
            var marriageModel = Campaign.Current.Models.MarriageModel;

            // Add eligible companions to the list
            foreach (var companion in Clan.PlayerClan.Companions)
            {
                // Skip if already in the list (shouldn't happen, but be safe)
                if (__result.Contains(companion.CharacterObject))
                    continue;

                // Check if any hero in the target clan would be a suitable match
                bool hasMatch = false;
                foreach (var targetHero in withClan.AliveLords)
                {
                    if (marriageModel.IsCoupleSuitableForMarriage(targetHero, companion))
                    {
                        hasMatch = true;
                        break;
                    }
                }

                if (hasMatch)
                {
                    __result.Add(companion.CharacterObject);
                }
            }
        }
    }
}
