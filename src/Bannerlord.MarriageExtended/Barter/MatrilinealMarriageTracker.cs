using System.Collections.Generic;

using TaleWorlds.CampaignSystem;

namespace MarriageExtended.Barter
{
    /// <summary>
    /// Tracks matrilineal marriage selections made during barter.
    /// This is used to communicate between the MatrilinealBarterable and the GetClanAfterMarriage patch.
    /// </summary>
    public static class MatrilinealMarriageTracker
    {
        // Tracks pending matrilineal marriages (bride -> groom)
        private static readonly Dictionary<Hero, Hero> _pendingMatrilinealMarriages = new();

        /// <summary>
        /// Marks a marriage as matrilineal (husband joins wife's clan).
        /// Called when MatrilinealBarterable.Apply() is executed.
        /// </summary>
        public static void SetMatrilinealMarriage(Hero bride, Hero groom)
        {
            _pendingMatrilinealMarriages[bride] = groom;
        }

        /// <summary>
        /// Checks if a matrilineal marriage is pending for the given heroes.
        /// </summary>
        public static bool IsMatrilinealMarriage(Hero hero1, Hero hero2)
        {
            // Check both directions since we don't know the order
            Hero? female = hero1.IsFemale ? hero1 : (hero2.IsFemale ? hero2 : null);
            Hero? male = !hero1.IsFemale ? hero1 : (!hero2.IsFemale ? hero2 : null);

            if (female == null || male == null)
                return false;

            return _pendingMatrilinealMarriages.TryGetValue(female, out var pendingGroom) 
                   && pendingGroom == male;
        }

        /// <summary>
        /// Clears the matrilineal flag for the given heroes after the marriage is processed.
        /// </summary>
        public static void ClearMatrilinealMarriage(Hero hero1, Hero hero2)
        {
            Hero? female = hero1.IsFemale ? hero1 : (hero2.IsFemale ? hero2 : null);
            
            if (female != null)
            {
                _pendingMatrilinealMarriages.Remove(female);
            }
        }

        /// <summary>
        /// Gets the bride for a pending matrilineal marriage if one exists.
        /// Returns null if no matrilineal marriage is pending.
        /// </summary>
        public static Hero? GetBrideIfMatrilineal(Hero hero1, Hero hero2)
        {
            if (IsMatrilinealMarriage(hero1, hero2))
            {
                return hero1.IsFemale ? hero1 : hero2;
            }
            return null;
        }
    }
}
