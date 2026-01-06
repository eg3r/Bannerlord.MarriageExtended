using System.Linq;

using TaleWorlds.CampaignSystem;

namespace MarriageExtended.Patches
{
    /// <summary>
    /// Helper methods for identifying and managing companion spouses.
    /// A companion spouse is someone who married a player's companion and 
    /// was converted to a companion themselves (so they don't take up a "Lord" slot in the clan).
    /// These spouses should not count against the companion limit.
    /// </summary>
    internal static class CompanionSpouseHelper
    {
        /// <summary>
        /// Checks if a hero is a companion spouse (married to another companion in the same clan).
        /// </summary>
        /// <param name="hero">The hero to check.</param>
        /// <returns>True if the hero is a companion whose spouse is also a companion in the player clan.</returns>
        public static bool IsCompanionSpouse(Hero hero)
        {
            if (hero == null || !hero.IsPlayerCompanion)
                return false;

            // Check if their spouse is also a player companion
            var spouse = hero.Spouse;
            if (spouse == null || !spouse.IsPlayerCompanion)
                return false;

            // Both are companions in the same clan - one of them is the "spouse" who joined
            // We consider the one who was NOT originally a wanderer as the spouse
            // However, since we change their occupation to Wanderer, we can't rely on that anymore
            // Instead, we'll just count one of each married companion pair as a "spouse"
            // To be deterministic, we'll say the one with the higher StringId is the spouse
            return string.CompareOrdinal(hero.StringId, spouse.StringId) > 0;
        }

        /// <summary>
        /// Gets the count of companion spouses in a clan.
        /// These are companions who married into the clan and should not count against the limit.
        /// </summary>
        /// <param name="clan">The clan to check.</param>
        /// <returns>Number of companion spouses.</returns>
        public static int GetCompanionSpouseCount(Clan clan)
        {
            if (clan == null)
                return 0;

            return clan.Companions.Count(IsCompanionSpouse);
        }
    }
}
