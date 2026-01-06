using Helpers;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace MarriageExtended.Barter
{
    /// <summary>
    /// A barterable item that represents the choice of matrilineal marriage.
    /// When this item is included in the barter, the husband joins the wife's clan
    /// instead of the default patrilineal arrangement (wife joins husband's clan).
    /// 
    /// Two scenarios:
    /// 1. Female companion + Male from other clan: Player PAYS to keep her (and get him)
    /// 2. Male companion + Female from other clan: Player GETS PAID to send him away
    /// </summary>
    public class MatrilinealBarterable : Barterable
    {
        private readonly Hero _bride;
        private readonly Hero _groom;
        private readonly bool _brideIsFromPlayerClan;

        public override string StringID => "matrilineal_marriage";

        public override TextObject Name
        {
            get
            {
                // Check if groom is clan leader (this marriage would be invalid)
                if (_groom.Clan?.Leader == _groom)
                {
                    TextObject text = new TextObject("{=MarriageExtended_MatrilinealBlocked}Matrilineal Marriage (BLOCKED: {GROOM.NAME} is clan leader)", null);
                    StringHelpers.SetCharacterProperties("GROOM", _groom.CharacterObject, text);
                    return text;
                }
                
                TextObject normalText = new TextObject("{=MarriageExtended_Matrilineal}Matrilineal Marriage ({GROOM.NAME} joins {BRIDE.NAME}'s clan)", null);
                StringHelpers.SetCharacterProperties("BRIDE", _bride.CharacterObject, normalText);
                StringHelpers.SetCharacterProperties("GROOM", _groom.CharacterObject, normalText);
                return normalText;
            }
        }

        public MatrilinealBarterable(Hero owner, PartyBase ownerParty, Hero bride, Hero groom)
            : base(owner, ownerParty)
        {
            _bride = bride;
            _groom = groom;
            // Check if bride is from player's clan (not just companion - could be clan member)
            _brideIsFromPlayerClan = bride.Clan == Clan.PlayerClan;
        }

        public Hero Bride => _bride;
        public Hero Groom => _groom;

        /// <summary>
        /// Returns true if this matrilineal marriage would be invalid (e.g., groom is clan leader).
        /// </summary>
        public bool IsBlocked => _groom.Clan?.Leader == _groom;

        public override int GetUnitValueForFaction(IFaction faction)
        {
            // Block if groom is clan leader - can't transfer a clan leader
            if (IsBlocked)
                return 0;

            // Get settings for matrilineal cost calculation
            int baseCost = Settings.Instance?.BaseMarriageCost ?? 15000;
            float heroValueFactor = Settings.Instance?.HeroValueFactor ?? 0.5f;
            int tierDiffValue = Settings.Instance?.TierDifferenceValue ?? 3000;
            float matrilinealMultiplier = Settings.Instance?.MatrilinealCostMultiplier ?? 1.0f;
            int minDowry = Settings.Instance?.MinMarriageDowry ?? 2500;

            // In matrilineal marriage, the GROOM transfers to bride's clan (opposite of default)
            // So we calculate based on groom's value
            Hero transferringHero = _groom;
            Clan losingClan = _groom.Clan;
            Clan gainingClan = _bride.Clan;

            // Calculate the value of the matrilineal option
            // This represents the CHANGE in value from default (patrilineal) to matrilineal
            int heroValue = (int)Campaign.Current.Models.DiplomacyModel.GetValueOfHeroForFaction(transferringHero, losingClan, true);
            
            int matrilinealValue = baseCost;
            matrilinealValue += (int)(heroValue * heroValueFactor);
            matrilinealValue += (losingClan.Tier - gainingClan.Tier) * tierDiffValue;

            // Apply clan relations adjustment
            int clanRelation = FactionManager.GetRelationBetweenClans(_bride.Clan, _groom.Clan);
            float relationMultiplier = 1f - (clanRelation / 1000f);
            matrilinealValue = (int)(matrilinealValue * relationMultiplier);

            // Apply matrilineal-specific multiplier
            matrilinealValue = (int)(matrilinealValue * matrilinealMultiplier);

            // Ensure minimum value
            matrilinealValue = MathF.Max(minDowry, matrilinealValue);

            // 4 scenarios based on who GETS the character when M is selected:
            // 1. WE offer male (no M) → we pay [handled by game]
            // 2. WE offer male + M → THEY pay (they get our male)
            // 3. WE offer female (no M) → they pay [handled by game]  
            // 4. WE offer female + M → WE pay (we get their male)
            //
            // _brideIsFromPlayerClan tells us which person is from our clan:
            // - true = bride (female) is ours → we're offering female
            // - false = groom (male) is ours → we're offering male

            if (_brideIsFromPlayerClan)
            {
                // FEMALE from player clan scenario (we have bride)
                // With M: WE GET their male (groom joins our clan) → WE PAY
                if (faction == _groom.Clan)
                    return -matrilinealValue;   // Negative for NPC = they want compensation
                else if (faction == _bride.Clan)
                    return matrilinealValue;    // Positive for us = we're willing to pay
            }
            else
            {
                // MALE from player clan scenario (we have groom)
                // With M: THEY GET our male (groom joins their clan) → THEY PAY
                if (faction == _bride.Clan)
                    return matrilinealValue;    // Positive for NPC = they're willing to pay
                else if (faction == _groom.Clan)
                    return -matrilinealValue;   // Negative for us = we want compensation
            }

            return 0;
        }

        public override ImageIdentifier GetVisualIdentifier()
        {
            // Show the person who is changing clans (the groom in matrilineal)
            return new CharacterImageIdentifier(CharacterCode.CreateFrom(_groom.CharacterObject));
        }

        public override string GetEncyclopediaLink()
        {
            return _groom.EncyclopediaLink;
        }

        public override void Apply()
        {
            // Ensure the tracker is set when the barter is finalized
            // (it should already be set from SetIsOffered via Harmony patch, but this is a safeguard)
            MatrilinealMarriageTracker.SetMatrilinealMarriage(_bride, _groom);
        }
    }
}
