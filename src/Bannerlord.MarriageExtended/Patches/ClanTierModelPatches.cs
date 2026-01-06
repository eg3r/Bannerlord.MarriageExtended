using HarmonyLib;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace MarriageExtended.Patches
{
    /// <summary>
    /// Harmony patches for DefaultClanTierModel to exclude companion spouses from companion limit.
    /// </summary>
    [HarmonyPatch]
    internal static class ClanTierModelPatches
    {
        /// <summary>
        /// Patches GetCompanionLimit to add extra slots for companion spouses.
        /// This way, companions who married into the clan don't count against the regular limit.
        /// </summary>
        [HarmonyPatch(typeof(DefaultClanTierModel), nameof(DefaultClanTierModel.GetCompanionLimit))]
        [HarmonyPostfix]
        private static void GetCompanionLimit_Postfix(Clan clan, ref int __result)
        {
            if (clan == null || clan != Clan.PlayerClan)
                return;

            // Add extra slots equal to the number of companion spouses
            // This effectively makes them "free" and not count against the limit
            int spouseCount = CompanionSpouseHelper.GetCompanionSpouseCount(clan);
            __result += spouseCount;
        }
    }
}
