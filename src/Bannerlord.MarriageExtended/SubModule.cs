using Bannerlord.ButterLib.Common.Extensions;
using Bannerlord.UIExtenderEx;

using MarriageExtended.Behaviors;
using MarriageExtended.Patches;

using Microsoft.Extensions.Logging;

using Serilog.Events;

using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace MarriageExtended
{
    /// <summary>
    /// Main entry point for the mod. Inherits from MBSubModuleBase to hook into game lifecycle.
    /// </summary>
    public sealed class SubModule : MBSubModuleBase
    {
        // Version extracted from assembly (set in .csproj)
        public static readonly string Version = $"v{typeof(SubModule).Assembly.GetName().Version!.ToString(3)}";

        // Mod identity
        public static readonly string Name = typeof(SubModule).Namespace!;
        public static readonly string DisplayName = new TextObject("{=MarriageExtendedId}MarriageExtended").ToString();
        
        // Harmony domain IDs (used for patching)
        public static readonly string MainHarmonyDomain = "bannerlord." + Name.ToLower();
        public static readonly string CampaignHarmonyDomain = MainHarmonyDomain + ".campaign";

        // Standard text color for InformationManager messages
        internal static readonly Color StdTextColor = Color.FromUint(0x00F16D26); // Orange

        // Singleton instance
        internal static SubModule Instance { get; set; } = default!;

        // Logger instance
        private static ILogger Log { get; set; } = default!;

        private bool _hasLoaded;

        /// <summary>
        /// Called when the module is first loaded.
        /// Use this for: registering UI extensions, applying Harmony patches, setting up logging.
        /// </summary>
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            Instance = this;

            // Register UI extensions (if using UIExtenderEx)
            var extender = UIExtender.Create(Name);
            extender.Register(typeof(SubModule).Assembly);
            extender.Enable();

            // Set up Serilog logging through ButterLib
            this.AddSerilogLoggerProvider($"{Name}.log", new[] { $"{Name}.*" }, config => config.MinimumLevel.Is(LogEventLevel.Verbose));
            Log = LogFactory.Get<SubModule>();
            Log.LogInformation($"Loading {Name} {Version}...");

            // Apply Harmony patches that should be active from game start
            PatchManager.ApplyMainPatches(MainHarmonyDomain);
        }

        /// <summary>
        /// Called when the module is unloaded.
        /// </summary>
        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();
            Log.LogInformation($"Unloaded {Name} {Version}!");
        }

        /// <summary>
        /// Called before the main menu is shown. Good place for one-time initialization messages.
        /// </summary>
        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();

            if (!_hasLoaded)
            {
                _hasLoaded = true;
                Log.LogInformation($"Loaded {Name} {Version}!");

                // Show a message to the player that the mod loaded
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=MarriageExtendedLoaded}Loaded {NAME}").SetTextVariable("NAME", DisplayName).ToString(), 
                    StdTextColor));
            }
        }

        /// <summary>
        /// Called when a game (campaign or other) starts.
        /// Use this for: adding campaign behaviors, models, and campaign-specific patches.
        /// </summary>
        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            if (game.GameType is Campaign)
            {
                // Apply campaign-specific Harmony patches
                // PatchManager.ApplyCampaignPatches(CampaignHarmonyDomain);

                var gameStarter = (CampaignGameStarter)gameStarterObject;

                // Add campaign behaviors
                gameStarter.AddBehavior(new MatrilinealMarriageBehavior());

                // Add/replace game models
                // gameStarter.AddModel(new YourCustomModel());

                Log.LogDebug("Campaign session started.");
            }
        }

        /// <summary>
        /// Called when a game ends.
        /// </summary>
        public override void OnGameEnd(Game game)
        {
            base.OnGameEnd(game);

            if (game.GameType is Campaign)
            {
                Log.LogDebug("Campaign session ended.");
            }
        }
    }
}
