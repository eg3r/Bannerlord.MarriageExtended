using HarmonyLib;

using MarriageExtended.Barter;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace MarriageExtended.Patches
{
    /// <summary>
    /// Harmony patches for DefaultMarriageModel to extend marriage eligibility.
    /// </summary>
    [HarmonyPatch]
    internal static class MarriageModelPatches
    {
        /// <summary>
        /// Patches IsSuitableForMarriage to allow player companions to be eligible for marriage.
        /// In vanilla, only heroes with Occupation.Lord can marry. This patch allows companions too.
        /// </summary>
        [HarmonyPatch(typeof(DefaultMarriageModel), nameof(DefaultMarriageModel.IsSuitableForMarriage))]
        [HarmonyPostfix]
        private static void IsSuitableForMarriage_Postfix(Hero maidenOrSuitor, ref bool __result)
        {
            // If vanilla already said yes, don't change it
            if (__result)
                return;

            // Check if this is a player companion who would otherwise be eligible
            if (maidenOrSuitor.IsPlayerCompanion && IsCompanionSuitableForMarriage(maidenOrSuitor))
            {
                __result = true;
            }
        }

        /// <summary>
        /// Checks if a companion meets the basic requirements for marriage (excluding the IsLord check).
        /// This mirrors the vanilla IsSuitableForMarriage logic but for companions.
        /// </summary>
        private static bool IsCompanionSuitableForMarriage(Hero companion)
        {
            if (!companion.IsActive)
                return false;

            if (companion.Spouse != null)
                return false;

            if (companion.IsTemplate)
                return false;

            // Check if in combat
            var party = companion.PartyBelongedTo;
            if (party?.MapEvent != null)
                return false;

            // Check if in army
            if (party?.Army != null)
                return false;

            // Check minimum age
            var marriageModel = Campaign.Current.Models.MarriageModel;
            int minAge = companion.IsFemale 
                ? marriageModel.MinimumMarriageAgeFemale 
                : marriageModel.MinimumMarriageAgeMale;

            if (companion.Age < minAge)
                return false;

            return true;
        }

        /// <summary>
        /// Patches ShouldNpcMarriageBetweenClansBeAllowed to use our configurable minimum relations.
        /// Vanilla uses -50, we allow the player to configure this value.
        /// </summary>
        [HarmonyPatch(typeof(DefaultMarriageModel), nameof(DefaultMarriageModel.ShouldNpcMarriageBetweenClansBeAllowed))]
        [HarmonyPrefix]
        private static bool ShouldNpcMarriageBetweenClansBeAllowed_Prefix(
            Clan consideringClan, 
            Clan targetClan, 
            ref bool __result)
        {
            // Use our configurable minimum relations instead of vanilla's -50
            int minRelations = Settings.Instance?.MinRelationsForMarriage ?? -50;

            __result = targetClan != consideringClan 
                && !consideringClan.IsAtWarWith(targetClan) 
                && consideringClan.GetRelationWithClan(targetClan) >= minRelations;

            // Skip original method
            return false;
        }

        /// <summary>
        /// Patches GetClanAfterMarriage to support matrilineal marriage option.
        /// Matrilineal: husband joins wife's clan (opposite of default patrilineal arrangement).
        /// Note: We don't clear the tracker here because GetClanAfterMarriage can be called multiple times
        /// (during barter calculation AND during actual marriage). The tracker is cleared via
        /// CampaignEvents.OnHeroesMarried or when the barterable is un-offered.
        /// </summary>
        [HarmonyPatch(typeof(DefaultMarriageModel), nameof(DefaultMarriageModel.GetClanAfterMarriage))]
        [HarmonyPostfix]
        private static void GetClanAfterMarriage_Postfix(Hero firstHero, Hero secondHero, ref Clan __result)
        {
            // Check if a matrilineal marriage was selected in barter
            Hero? matrilinealBride = MatrilinealMarriageTracker.GetBrideIfMatrilineal(firstHero, secondHero);
            
            if (matrilinealBride != null)
            {
                // Determine the groom (male partner)
                Hero groom = firstHero.IsFemale ? secondHero : firstHero;
                
                // SAFETY CHECK: Never allow a clan leader to transfer clans
                // This prevents breaking the game by leaving a clan leaderless
                if (groom.Clan?.Leader == groom)
                {
                    // Groom is clan leader - cannot transfer, keep vanilla result
                    // The matrilineal option should have been blocked, but this is a safeguard
                    return;
                }
                
                // Override: husband joins wife's clan (matrilineal)
                __result = matrilinealBride.Clan;
                // Don't clear tracker here - it may be called multiple times
            }
        }
    }
}
