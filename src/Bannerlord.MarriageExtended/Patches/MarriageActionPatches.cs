using System;
using System.Reflection;

using HarmonyLib;

using Microsoft.Extensions.Logging;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Library;

namespace MarriageExtended.Patches
{
    /// <summary>
    /// Harmony patches for MarriageAction to handle companion marriages properly.
    /// - When a companion's spouse joins player clan: convert spouse to companion
    /// - When a companion leaves to join spouse's clan (matrilineal): properly clean up
    /// 
    /// KEY INSIGHT: For companions, Hero.Clan returns CompanionOf (not _clan).
    /// MarriageAction sets _clan but doesn't touch CompanionOf, leaving the hero
    /// in an invalid state where CompanionOf still points to PlayerClan but 
    /// _clan points to the new clan. We need to detect and fix this.
    /// 
    /// CRITICAL: We must fix the state IMMEDIATELY (not deferred) because the map screen
    /// loads right after the conversation ends and needs valid hero data. However, we must
    /// bypass property setters to avoid triggering UI notifications during barter flow.
    /// We manipulate backing fields directly for atomic, safe cleanup.
    /// </summary>
    [HarmonyPatch]
    internal static class MarriageActionPatches
    {
        private static ILogger Log => LogFactory.Get<SubModule>();

        // Cached fields for direct manipulation (bypassing property setters)
        private static FieldInfo? _clanField;
        private static FieldInfo? _companionOfField;
        private static FieldInfo? _companionsCacheField;
        private static FieldInfo? _heroesCacheField;

        // Cached delegate for calling internal Clan method
        private static Action<Clan, Hero>? _onLordRemoved;
        private static Action<Clan, Hero>? _onLordAdded;

        /// <summary>
        /// After a marriage is applied, handle companion-related clan changes.
        /// </summary>
        [HarmonyPatch(typeof(MarriageAction), nameof(MarriageAction.Apply))]
        [HarmonyPostfix]
        private static void Apply_Postfix(Hero firstHero, Hero secondHero)
        {
            // Case 1: Check if a former companion's _clan was changed but CompanionOf wasn't cleared
            // Fix immediately using direct field manipulation to avoid UI crashes
            FixCompanionClanMismatch(firstHero);
            FixCompanionClanMismatch(secondHero);

            // Case 2: Check if a spouse joined player clan and needs to be converted to companion
            HandleSpouseJoiningCompanion(firstHero, secondHero);
        }

        #region Field Access Helpers

        /// <summary>
        /// Gets the actual _clan backing field value (bypassing the CompanionOf override).
        /// </summary>
        internal static Clan? GetActualClanField(Hero hero)
        {
            try
            {
                _clanField ??= AccessTools.Field(typeof(Hero), "_clan");
                return _clanField?.GetValue(hero) as Clan;
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to get _clan field for {hero.Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Sets the _companionOf backing field directly, bypassing the property setter.
        /// This avoids triggering Clan.OnCompanionRemoved/OnCompanionAdded.
        /// </summary>
        private static bool SetCompanionOfFieldDirect(Hero hero, Clan? value)
        {
            try
            {
                _companionOfField ??= AccessTools.Field(typeof(Hero), "_companionOf");
                _companionOfField?.SetValue(hero, value);
                return true;
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to set _companionOf field for {hero.Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the _companionsCache from a clan directly.
        /// </summary>
        private static MBList<Hero>? GetCompanionsCache(Clan clan)
        {
            try
            {
                _companionsCacheField ??= AccessTools.Field(typeof(Clan), "_companionsCache");
                return _companionsCacheField?.GetValue(clan) as MBList<Hero>;
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to get _companionsCache for {clan.Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the _heroesCache from a clan directly.
        /// </summary>
        private static MBList<Hero>? GetHeroesCache(Clan clan)
        {
            try
            {
                _heroesCacheField ??= AccessTools.Field(typeof(Clan), "_heroesCache");
                return _heroesCacheField?.GetValue(clan) as MBList<Hero>;
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to get _heroesCache for {clan.Name}: {ex.Message}");
                return null;
            }
        }

        #endregion

        /// <summary>
        /// Detects and fixes the case where a companion's _clan field was changed (by MarriageAction)
        /// but CompanionOf was NOT cleared. This leaves them in an invalid state.
        /// 
        /// We fix this IMMEDIATELY using direct field manipulation to avoid:
        /// 1. UI notification cascades that crash the barter VM
        /// 2. Invalid state when map screen loads after conversation
        /// 
        /// Detection: CompanionOf is set (hero thinks they're a companion)
        ///            BUT _clan field != CompanionOf (actual clan was changed)
        /// </summary>
        private static void FixCompanionClanMismatch(Hero hero)
        {
            // Check if hero has CompanionOf set (thinks they're still a companion)
            Clan? companionOf = hero.CompanionOf;
            if (companionOf == null)
                return;

            // Get the actual _clan field (what MarriageAction set)
            Clan? actualClan = GetActualClanField(hero);
            if (actualClan == null)
                return;

            // If _clan matches CompanionOf, there's no mismatch - they're a valid companion
            if (actualClan == companionOf)
                return;

            // MISMATCH DETECTED: Fix immediately using direct field manipulation
            Log.LogInformation($"Detected clan mismatch for {hero.Name}: " +
                $"CompanionOf={companionOf.Name}, _clan={actualClan.Name}. " +
                $"Performing atomic cleanup.");

            PerformAtomicCompanionCleanup(hero, companionOf, actualClan);
        }

        /// <summary>
        /// Performs atomic cleanup of a companion who has been moved to another clan.
        /// Uses direct field manipulation to avoid triggering UI notifications.
        /// 
        /// Steps:
        /// 1. Remove hero from old clan's _companionsCache (direct)
        /// 2. Remove hero from old clan's _heroesCache (direct) 
        /// 3. Set hero's _companionOf to null (direct)
        /// 4. Change occupation from Wanderer to Lord
        /// 5. Add hero to new clan's lords cache (they're already in _heroesCache via MarriageAction)
        /// </summary>
        private static void PerformAtomicCompanionCleanup(Hero hero, Clan oldCompanionClan, Clan newClan)
        {
            try
            {
                // Step 1: Remove from old clan's companions cache
                var companionsCache = GetCompanionsCache(oldCompanionClan);
                if (companionsCache != null && companionsCache.Contains(hero))
                {
                    companionsCache.Remove(hero);
                    Log.LogDebug($"Removed {hero.Name} from {oldCompanionClan.Name}'s companions cache");
                }

                // Step 2: Remove from old clan's heroes cache
                // (companions are in heroes cache of their CompanionOf clan)
                var oldHeroesCache = GetHeroesCache(oldCompanionClan);
                if (oldHeroesCache != null && oldHeroesCache.Contains(hero))
                {
                    oldHeroesCache.Remove(hero);
                    Log.LogDebug($"Removed {hero.Name} from {oldCompanionClan.Name}'s heroes cache");
                }

                // Step 3: Clear _companionOf field directly (no property setter = no events)
                SetCompanionOfFieldDirect(hero, null);
                Log.LogDebug($"Cleared _companionOf for {hero.Name}");

                // Step 4: Change occupation from Wanderer to Lord
                hero.SetNewOccupation(Occupation.Lord);
                Log.LogDebug($"Changed {hero.Name}'s occupation to Lord");

                // Step 5: Ensure hero is in new clan's lords cache
                // MarriageAction.HandleClanChangeAfterMarriageForHero already added them to _heroesCache
                // but we need to ensure they're in the lords cache too
                EnsureInLordsCache(newClan, hero);

                Log.LogInformation($"Atomic cleanup complete: {hero.Name} is now a Lord of {newClan.Name}");
            }
            catch (Exception ex)
            {
                Log.LogError($"Error during atomic companion cleanup for {hero.Name}: {ex.Message}");
                // Fallback: try the standard property setter approach
                try
                {
                    hero.CompanionOf = null;
                    hero.SetNewOccupation(Occupation.Lord);
                }
                catch
                {
                    // If even fallback fails, log but don't crash
                    Log.LogError($"Fallback cleanup also failed for {hero.Name}");
                }
            }
        }

        /// <summary>
        /// Ensures the hero is in the clan's lords cache.
        /// </summary>
        private static void EnsureInLordsCache(Clan clan, Hero hero)
        {
            try
            {
                // Call OnLordAdded to add to lords cache if not already there
                _onLordAdded ??= AccessTools.MethodDelegate<Action<Clan, Hero>>(
                    AccessTools.Method(typeof(Clan), "OnLordAdded"));

                _onLordAdded?.Invoke(clan, hero);
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to add {hero.Name} to Lords cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles the case where a spouse joined a companion in the player clan.
        /// The spouse should be converted to a companion as well.
        /// </summary>
        private static void HandleSpouseJoiningCompanion(Hero firstHero, Hero secondHero)
        {
            // Check if either hero is now a player companion
            bool firstIsCompanion = firstHero.IsPlayerCompanion;
            bool secondIsCompanion = secondHero.IsPlayerCompanion;

            // If neither is a companion, nothing to do
            if (!firstIsCompanion && !secondIsCompanion)
                return;

            // If both are already companions, nothing to do
            if (firstIsCompanion && secondIsCompanion)
                return;

            // One is a companion, one is not - find them
            Hero companion = firstIsCompanion ? firstHero : secondHero;
            Hero spouse = firstIsCompanion ? secondHero : firstHero;

            // Only proceed if the spouse is now in the player clan (they joined via marriage)
            if (spouse.Clan != Clan.PlayerClan)
                return;

            // Only convert if the spouse is a Lord (not already a companion/wanderer)
            if (!spouse.IsLord)
                return;

            Log.LogInformation($"Converting {spouse.Name} to companion after marrying companion {companion.Name}");

            // Convert the spouse to a companion
            ConvertLordToCompanion(spouse);
        }

        /// <summary>
        /// Converts a Lord hero to a companion of the player clan.
        /// This removes them from the Lords list, changes their occupation, and adds them as a companion.
        /// </summary>
        private static void ConvertLordToCompanion(Hero hero)
        {
            Clan clan = hero.Clan;

            // First, remove from the Lords cache by calling internal OnLordRemoved
            // This is necessary because SetNewOccupation doesn't update clan caches
            RemoveFromLordsCache(clan, hero);

            // Change occupation from Lord to Wanderer (companion occupation)
            hero.SetNewOccupation(Occupation.Wanderer);

            // Add them as a companion to the player clan
            // This will add them to the companions cache
            AddCompanionAction.Apply(clan, hero);

            Log.LogDebug($"{hero.Name} is now a companion of {clan.Name}");
        }

        /// <summary>
        /// Removes a hero from the clan's Lords cache using reflection to call internal method.
        /// </summary>
        private static void RemoveFromLordsCache(Clan clan, Hero hero)
        {
            try
            {
                // Create delegate for internal method if not cached
                _onLordRemoved ??= AccessTools.MethodDelegate<Action<Clan, Hero>>(
                    AccessTools.Method(typeof(Clan), "OnLordRemoved"));

                _onLordRemoved?.Invoke(clan, hero);
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to remove {hero.Name} from Lords cache: {ex.Message}");
            }
        }
    }
}
