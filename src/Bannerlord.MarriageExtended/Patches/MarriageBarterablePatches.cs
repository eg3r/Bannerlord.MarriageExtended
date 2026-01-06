using HarmonyLib;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace MarriageExtended.Patches
{
    /// <summary>
    /// Harmony patches for MarriageBarterable to fix barter value calculation.
    /// 
    /// Problem: The base game's value calculation doesn't properly handle the case
    /// where a player clan member (e.g., female companion) will LEAVE to join
    /// the other clan. In this case, the other clan should compensate us.
    /// 
    /// The base game calculates values based on ProposingHero vs HeroBeingProposedTo,
    /// but this doesn't align with who is actually transferring clans (determined by
    /// MarriageModel.GetClanAfterMarriage - typically patrilineal: wife joins husband).
    /// </summary>
    [HarmonyPatch]
    internal static class MarriageBarterablePatches
    {
        /// <summary>
        /// Patches GetUnitValueForFaction to fix value direction based on who actually
        /// transfers clans, using settings-based value calculation.
        /// </summary>
        [HarmonyPatch(typeof(MarriageBarterable), nameof(MarriageBarterable.GetUnitValueForFaction))]
        [HarmonyPostfix]
        private static void GetUnitValueForFaction_Postfix(
            MarriageBarterable __instance,
            IFaction faction,
            ref int __result)
        {
            Hero proposingHero = __instance.ProposingHero;
            Hero heroBeingProposedTo = __instance.HeroBeingProposedTo;

            bool proposingHeroIsPlayerClan = proposingHero.Clan == Clan.PlayerClan;
            bool heroBeingProposedToIsPlayerClan = heroBeingProposedTo.Clan == Clan.PlayerClan;

            // Only intervene when player clan is involved
            if (!proposingHeroIsPlayerClan && !heroBeingProposedToIsPlayerClan)
                return;

            // Determine who will transfer clans after marriage
            // Default game behavior: wife joins husband's clan (patrilineal)
            Clan clanAfterMarriage = Campaign.Current.Models.MarriageModel.GetClanAfterMarriage(heroBeingProposedTo, proposingHero);

            Hero? transferringHero = null;
            Clan? losingClan = null;
            Clan? gainingClan = null;

            if (clanAfterMarriage != heroBeingProposedTo.Clan)
            {
                // HeroBeingProposedTo will transfer to clanAfterMarriage
                transferringHero = heroBeingProposedTo;
                losingClan = heroBeingProposedTo.Clan;
                gainingClan = clanAfterMarriage;
            }
            else if (clanAfterMarriage != proposingHero.Clan)
            {
                // ProposingHero will transfer to clanAfterMarriage
                transferringHero = proposingHero;
                losingClan = proposingHero.Clan;
                gainingClan = clanAfterMarriage;
            }
            else
            {
                // Same clan marriage - no clan transfer, use vanilla logic with multipliers
                ApplyMinimumDowryAndMultipliers(__instance, faction, ref __result);
                return;
            }

            // Get settings
            int baseCost = Settings.Instance?.BaseMarriageCost ?? 15000;
            float heroValueFactor = Settings.Instance?.HeroValueFactor ?? 0.5f;
            int tierDiffValue = Settings.Instance?.TierDifferenceValue ?? 3000;

            // Calculate the compensation value based on settings
            // 1. Start with base cost
            int compensationValue = baseCost;

            // 2. Add hero's value (scaled by factor)
            int heroValue = (int)Campaign.Current.Models.DiplomacyModel.GetValueOfHeroForFaction(transferringHero, losingClan, true);
            compensationValue += (int)(heroValue * heroValueFactor);

            // 3. Add clan tier difference bonus (higher tier clan losing = more compensation)
            int tierDifference = losingClan.Tier - gainingClan.Tier;
            compensationValue += tierDifference * tierDiffValue;

            // 4. Adjust by clan relations (good relations = discount, bad = premium)
            int clanRelation = FactionManager.GetRelationBetweenClans(proposingHero.Clan, heroBeingProposedTo.Clan);
            // -100 to +100 relation -> -10% to +10% adjustment
            float relationMultiplier = 1f - (clanRelation / 1000f);
            compensationValue = (int)(compensationValue * relationMultiplier);

            // Ensure minimum value
            int minDowry = Settings.Instance?.MinMarriageDowry ?? 2500;
            compensationValue = MathF.Max(minDowry, compensationValue);

            // Determine the correct value direction based on which faction is asking
            // and whether player clan is losing or gaining
            bool playerClanIsLosing = losingClan == Clan.PlayerClan;

            if (playerClanIsLosing)
            {
                // Player is LOSING a clan member → other clan should pay us
                if (faction == Clan.PlayerClan)
                {
                    // For player: negative = we want compensation (bar goes toward them paying)
                    __result = -compensationValue;
                }
                else
                {
                    // For NPC clan: positive = they're willing to pay for the hero
                    __result = compensationValue;
                }
            }
            else
            {
                // Player is GAINING a clan member → we should pay them
                if (faction == Clan.PlayerClan)
                {
                    // For player: negative = we need to pay
                    __result = -compensationValue;
                }
                else
                {
                    // For NPC clan: negative = they want compensation
                    __result = -compensationValue;
                }
            }

            // Apply companion multiplier if applicable
            ApplyCompanionMultiplier(__instance, faction, ref __result);
        }

        private static void ApplyCompanionMultiplier(
            MarriageBarterable __instance,
            IFaction faction,
            ref int __result)
        {
            // Only apply to player clan's costs
            if (faction != Clan.PlayerClan)
                return;

            float companionMultiplier = Settings.Instance?.CompanionSpouseCostMultiplier ?? 1.25f;

            // Check if this is a companion marriage where the spouse is a Lord
            bool isCompanionMarriage = (__instance.ProposingHero.Clan == Clan.PlayerClan && __instance.ProposingHero.IsPlayerCompanion) ||
                                       (__instance.HeroBeingProposedTo.Clan == Clan.PlayerClan && __instance.HeroBeingProposedTo.IsPlayerCompanion);
            bool spouseIsLord = __instance.HeroBeingProposedTo.IsLord || __instance.ProposingHero.IsLord;

            // Apply companion multiplier when a companion is marrying a lord
            // (converting a lord to wanderer is expensive)
            if (isCompanionMarriage && spouseIsLord && companionMultiplier > 1f)
            {
                // Only apply to negative values (player paying)
                if (__result < 0)
                {
                    __result = (int)(__result * companionMultiplier);
                }
            }
        }

        private static void ApplyMinimumDowryAndMultipliers(
            MarriageBarterable __instance,
            IFaction faction,
            ref int __result)
        {
            // Only apply to player clan
            if (faction != Clan.PlayerClan)
                return;

            int minDowry = Settings.Instance?.MinMarriageDowry ?? 2500;

            // Enforce minimum cost when player is paying
            if (minDowry > 0 && __result < 0 && __result > -minDowry)
            {
                __result = -minDowry;
            }

            ApplyCompanionMultiplier(__instance, faction, ref __result);
        }
    }
}
