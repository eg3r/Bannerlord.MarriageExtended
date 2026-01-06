using HarmonyLib;

using MarriageExtended.Barter;

using TaleWorlds.CampaignSystem.BarterSystem.Barterables;

namespace MarriageExtended.Patches
{
    /// <summary>
    /// Harmony patches for Barterable base class to track when matrilineal marriage option is offered.
    /// </summary>
    [HarmonyPatch]
    internal static class BarterablePatches
    {
        /// <summary>
        /// Patches SetIsOffered to update our matrilineal marriage tracker.
        /// This ensures GetClanAfterMarriage returns the correct clan during barter value calculations.
        /// </summary>
        [HarmonyPatch(typeof(Barterable), nameof(Barterable.SetIsOffered))]
        [HarmonyPostfix]
        private static void SetIsOffered_Postfix(Barterable __instance, bool value)
        {
            // Check if this is a matrilineal barterable
            if (__instance is MatrilinealBarterable matrilineal)
            {
                if (value)
                {
                    // Item was added to the offer - set the tracker
                    MatrilinealMarriageTracker.SetMatrilinealMarriage(matrilineal.Bride, matrilineal.Groom);
                }
                else
                {
                    // Item was removed from the offer - clear the tracker
                    MatrilinealMarriageTracker.ClearMatrilinealMarriage(matrilineal.Bride, matrilineal.Groom);
                }
            }
        }
    }
}
