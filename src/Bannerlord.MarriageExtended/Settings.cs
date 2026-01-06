using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

using TaleWorlds.Localization;

namespace MarriageExtended
{
    /// <summary>
    /// Mod Configuration Menu (MCM) settings.
    /// These settings are accessible in-game through the Mod Options menu.
    /// </summary>
    internal class Settings : AttributeGlobalSettings<Settings>
    {
        // Setting group names (use localization strings for multi-language support)
        private const string HeadingMarriage = "{=MarriageExtended_Marriage}Marriage";
        private const string HeadingBarter = "{=MarriageExtended_Barter}Marriage Barter";
        private const string HeadingAdvanced = "{=MarriageExtended_Advanced}Advanced";

        // =============================================
        // MCM IDENTITY (Required)
        // =============================================
        
        /// <summary>Unique identifier for settings persistence. Increment version when changing settings structure.</summary>
        public override string Id => "MarriageExtendedSettings_v1";
        
        /// <summary>Display name shown in MCM menu.</summary>
        public override string DisplayName => new TextObject("{=MarriageExtendedId}MarriageExtended").ToString();
        
        /// <summary>Folder name for settings files.</summary>
        public override string FolderName => "MarriageExtended";
        
        /// <summary>Settings file format.</summary>
        public override string FormatType => "json2";

        // =============================================
        // MARRIAGE SETTINGS
        // =============================================

        [SettingPropertyInteger(
            displayName: "{=MarriageExtended_MinRelations}Minimum Relations for Marriage", 
            minValue: -100, 
            maxValue: 100, 
            Order = 0, 
            RequireRestart = false, 
            HintText = "{=MarriageExtended_MinRelationsHint}Minimum clan relations required to allow marriage between clans. Base game uses -50. Default is -25.")]
        [SettingPropertyGroup(HeadingMarriage)]
        public int MinRelationsForMarriage { get; set; } = -50;

        [SettingPropertyBool(
            displayName: "{=MarriageExtended_EnableMatrilineal}Enable Matrilineal Marriage Option",
            Order = 1,
            RequireRestart = false,
            HintText = "{=MarriageExtended_EnableMatrilinealHint}When enabled, adds a matrilineal marriage option to the barter screen. Matrilineal means the husband joins the wife's clan instead of the default (wife joins husband's clan).")]
        [SettingPropertyGroup(HeadingMarriage)]
        public bool EnableMatrilinealOption { get; set; } = true;

        // =============================================
        // BARTER SETTINGS
        // =============================================

        [SettingPropertyInteger(
            displayName: "{=MarriageExtended_MinDowry}Minimum Marriage Dowry",
            minValue: 0,
            maxValue: 20000,
            Order = 0,
            RequireRestart = false,
            HintText = "{=MarriageExtended_MinDowryHint}Minimum gold value for marriage barter. Ensures a baseline cost regardless of other factors. Default is 2500.")]
        [SettingPropertyGroup(HeadingBarter)]
        public int MinMarriageDowry { get; set; } = 2500;

        [SettingPropertyInteger(
            displayName: "{=MarriageExtended_BaseMarriageCost}Base Marriage Cost",
            minValue: 1000,
            maxValue: 100000,
            Order = 1,
            RequireRestart = false,
            HintText = "{=MarriageExtended_BaseMarriageCostHint}Base gold value for marriage transactions. This is adjusted by hero value, clan tier, and relations. Default is 15000.")]
        [SettingPropertyGroup(HeadingBarter)]
        public int BaseMarriageCost { get; set; } = 15000;

        [SettingPropertyFloatingInteger(
            displayName: "{=MarriageExtended_HeroValueFactor}Hero Value Factor",
            minValue: 0f,
            maxValue: 2f,
            Order = 2,
            RequireRestart = false,
            HintText = "{=MarriageExtended_HeroValueFactorHint}How much the hero's combat/leadership value affects marriage cost. 0 = no effect, 1 = full effect. Default is 0.5.")]
        [SettingPropertyGroup(HeadingBarter)]
        public float HeroValueFactor { get; set; } = 0.5f;

        [SettingPropertyInteger(
            displayName: "{=MarriageExtended_TierDifferenceValue}Clan Tier Difference Value",
            minValue: 0,
            maxValue: 20000,
            Order = 3,
            RequireRestart = false,
            HintText = "{=MarriageExtended_TierDifferenceValueHint}Gold value per clan tier difference. Higher tier clan losing a member = more compensation. Default is 3000.")]
        [SettingPropertyGroup(HeadingBarter)]
        public int TierDifferenceValue { get; set; } = 3000;

        [SettingPropertyFloatingInteger(
            displayName: "{=MarriageExtended_CompanionSpouseMultiplier}Companion Spouse Cost Multiplier",
            minValue: 1f,
            maxValue: 5f,
            Order = 10,
            RequireRestart = false,
            HintText = "{=MarriageExtended_CompanionSpouseMultiplierHint}Additional cost multiplier when marrying a companion to a lord (lord becomes wanderer). 1.25 = 25% extra cost. Default is 1.25.")]
        [SettingPropertyGroup(HeadingBarter)]
        public float CompanionSpouseCostMultiplier { get; set; } = 1.25f;

        [SettingPropertyFloatingInteger(
            displayName: "{=MarriageExtended_MatrilinealCostMultiplier}Matrilineal Marriage Cost Multiplier",
            minValue: 0.5f,
            maxValue: 3f,
            Order = 11,
            RequireRestart = false,
            HintText = "{=MarriageExtended_MatrilinealCostMultiplierHint}Cost multiplier for matrilineal option. Values below 1 reduce cost, above 1 increase it. Default is 1.0.")]
        [SettingPropertyGroup(HeadingBarter)]
        public float MatrilinealCostMultiplier { get; set; } = 1.0f;
    }
}
