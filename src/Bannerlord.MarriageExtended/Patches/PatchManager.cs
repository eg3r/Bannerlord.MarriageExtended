using HarmonyLib;

using Microsoft.Extensions.Logging;

namespace MarriageExtended.Patches
{
    /// <summary>
    /// Manages Harmony patch application for the mod.
    /// </summary>
    internal static class PatchManager
    {
        private static ILogger Log => LogFactory.Get<SubModule>();

        private static Harmony? _mainHarmony;
        private static Harmony? _campaignHarmony;

        /// <summary>
        /// Applies patches that should be active from game start.
        /// Called in OnSubModuleLoad.
        /// </summary>
        public static void ApplyMainPatches(string harmonyId)
        {
            Log.LogInformation($"Applying main Harmony patches with ID: {harmonyId}");

            _mainHarmony = new Harmony(harmonyId);
            _mainHarmony.PatchAll(typeof(PatchManager).Assembly);

            Log.LogInformation("Main Harmony patches applied successfully.");
        }

        /// <summary>
        /// Applies campaign-specific patches.
        /// Called in OnGameStart when a campaign starts.
        /// </summary>
        public static void ApplyCampaignPatches(string harmonyId)
        {
            Log.LogDebug($"Applying campaign Harmony patches with ID: {harmonyId}");

            _campaignHarmony = new Harmony(harmonyId);
            
            // Add campaign-specific patches here if needed
            // _campaignHarmony.Patch(...);

            Log.LogDebug("Campaign Harmony patches applied successfully.");
        }

        /// <summary>
        /// Removes all applied patches.
        /// </summary>
        public static void UnpatchAll()
        {
            _mainHarmony?.UnpatchAll(_mainHarmony.Id);
            _campaignHarmony?.UnpatchAll(_campaignHarmony.Id);

            Log.LogInformation("All Harmony patches removed.");
        }
    }
}
