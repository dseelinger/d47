using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Lore;
using D47.Core.Mining;
using D47.Core.Reminders;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.Core.Tests.Callouts;

/// <summary>The catalogue <see cref="ShippedCallouts.Build"/> returns, over an in-memory install.</summary>
public static class ShippedCatalogue
{
    public static (CalloutEngine Engine, SettingsService Settings) Build()
    {
        var install = new MemoryInstall();
        var surface = TestSurface.For(install);
        var data = install.Paths.Data;
        var files = install.Files;
        var fight = new NearbyFight();

        var engine = ShippedCallouts.Build(
            new ShippedCallouts.Sources
            {
                Settings = surface.Settings,
                Loggers = NullLoggerFactory.Instance,
                Checklists = surface.ChecklistService,
                Lore = new LoreBook(new LoreStore(Path.Combine(data, "lore.json"), files, NullLogger<LoreStore>.Instance)),
                LoreVisits = new LoreVisits(Path.Combine(data, "lore-visits.json"), files, NullLogger<LoreVisits>.Instance),
                Adventures = new AdventureBook(
                    new AdventureStore(Path.Combine(data, "adventures.json"), files, NullLogger<AdventureStore>.Instance),
                    NullLogger<AdventureBook>.Instance),
                ViewState = new ViewStateStore(install.Paths, files, NullLogger<ViewStateStore>.Instance),
                Ledger = new CommodityLedger(),
                CommunityGoal = new CommunityGoalSearch(),
                GameState = surface.GameState,
                Exobiology = new ExobiologyLedger(Path.Combine(data, "unsold-data.json"), files, NullLogger<ExobiologyLedger>.Instance),
                Cartography = new CartographyLedger(Path.Combine(data, "unsold-data.json"), files, NullLogger<CartographyLedger>.Instance),
                Crimes = new OutstandingCrimes(files, NullLogger<OutstandingCrimes>.Instance),
                Fight = fight,
                Scenes = new SceneTracker(),
                HandInOffer = new HandInOffer(),
                MarketBook = new MarketBook(Path.Combine(data, "markets.json"), files, NullLogger<MarketBook>.Instance),
                PlanBook = new RoutePlanBook(Path.Combine(data, "route-plans.json"), files, NullLogger<RoutePlanBook>.Instance),
                StoryClue = new StoryClueCallout(fight),
                JournalReminders = new JournalReminderStore(
                    Path.Combine(data, "journal-reminders.json"), files, NullLogger<JournalReminderStore>.Instance),
                StandingWarnings = new StandingWarnings(),
                MiningTargets = new MiningTargetStore(Path.Combine(data, "mining.json"), files, NullLogger<MiningTargetStore>.Instance),
                StoryOpening = _ => false,
            },
            new DateTimeOffset(3311, 1, 1, 12, 0, 0, TimeSpan.Zero));

        return (engine, surface.Settings);
    }
}
