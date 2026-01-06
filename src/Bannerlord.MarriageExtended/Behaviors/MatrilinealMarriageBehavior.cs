using System.Collections.Generic;

using MarriageExtended.Barter;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Party;

namespace MarriageExtended.Behaviors
{
    /// <summary>
    /// Campaign behavior that adds matrilineal marriage option to the barter screen.
    /// Matrilineal marriage means the husband joins the wife's clan (opposite of default).
    /// </summary>
    public class MatrilinealMarriageBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.BarterablesRequested.AddNonSerializedListener(this, OnBarterablesRequested);
            CampaignEvents.OnBarterAcceptedEvent.AddNonSerializedListener(this, OnBarterAccepted);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No persistent data to sync
        }

        /// <summary>
        /// Called after barter is accepted. Clears the matrilineal tracker.
        /// This ensures the tracker doesn't have stale data.
        /// </summary>
        private void OnBarterAccepted(Hero offerer, Hero other, List<Barterable> barters)
        {
            // Clear tracker for any matrilineal barterables that were part of this barter
            foreach (var barterable in barters)
            {
                if (barterable is MatrilinealBarterable matrilineal)
                {
                    MatrilinealMarriageTracker.ClearMatrilinealMarriage(matrilineal.Bride, matrilineal.Groom);
                }
            }
        }

        private void OnBarterablesRequested(BarterData barterData)
        {
            // Check if matrilineal option is enabled
            if (!(Settings.Instance?.EnableMatrilinealOption ?? true))
                return;

            // Check if this is a marriage barter by looking for MarriageBarterable
            MarriageBarterable? marriageBarterable = null;
            foreach (var barterable in barterData.GetBarterables())
            {
                if (barterable is MarriageBarterable mb)
                {
                    marriageBarterable = mb;
                    break;
                }
            }

            // Not a marriage barter
            if (marriageBarterable == null)
                return;

            Hero proposingHero = marriageBarterable.ProposingHero;
            Hero heroBeingProposed = marriageBarterable.HeroBeingProposedTo;

            // Determine who is the bride and groom
            Hero? bride = null;
            Hero? groom = null;

            if (proposingHero.IsFemale && !heroBeingProposed.IsFemale)
            {
                bride = proposingHero;
                groom = heroBeingProposed;
            }
            else if (!proposingHero.IsFemale && heroBeingProposed.IsFemale)
            {
                bride = heroBeingProposed;
                groom = proposingHero;
            }

            // Only add options if we have a female-male pair
            if (bride == null || groom == null)
                return;

            // Don't offer matrilineal if the GROOM is a clan leader
            // (can't transfer a clan leader to another clan)
            if (groom.Clan?.Leader == groom)
                return;

            // Use clan membership instead of IsPlayerCompanion for consistent detection
            bool brideIsFromPlayerClan = bride.Clan == Clan.PlayerClan;
            bool groomIsFromPlayerClan = groom.Clan == Clan.PlayerClan;

            // Add matrilineal option when it would change the outcome:
            // 1. Female from player clan + Male from other clan
            // 2. Male from player clan + Female from other clan
            bool shouldOfferMatrilineal = 
                (brideIsFromPlayerClan && !groomIsFromPlayerClan) ||  // Our female + their male
                (groomIsFromPlayerClan && !brideIsFromPlayerClan);    // Our male + their female

            if (shouldOfferMatrilineal)
            {
                // Always put the matrilineal item on the PLAYER's side
                // The value calculation in the barterable handles who pays
                Hero owner = Hero.MainHero;
                PartyBase? ownerParty = PartyBase.MainParty;

                var matrilinealBarterable = new MatrilinealBarterable(
                    owner,
                    ownerParty,
                    bride,
                    groom
                );
                barterData.AddBarterable<OtherBarterGroup>(matrilinealBarterable, false);
            }
        }
    }
}
