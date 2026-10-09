using D47.Core.Activities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Activities;

[Trait("Category", "Integration")]
public sealed class WhenEachActivityWasLastDoneTests : IDisposable
{
    private const string Fid = "F123";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "d47-activities-" + Guid.NewGuid().ToString("N"));

    public WhenEachActivityWasLastDoneTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, true);

    private static string Line(string at, string kind, string extra = "") =>
        $"{{\"timestamp\":\"{at}\",\"event\":\"{kind}\"{extra}}}";

    private string Journal(params string[] lines)
    {
        var file = Path.Combine(_dir, $"Journal.{Guid.NewGuid():N}.log");
        File.WriteAllLines(file, lines);
        return file;
    }

    private ActivityLedger Ledger() => new(Path.Combine(_dir, "activities.json"), NullLogger.Instance);

    private static readonly (string Key, string Kind, string Extra)[] Dating =
    [
        ("mining", "MiningRefined", ""),
        ("mining", "AsteroidCracked", ""),
        ("exploration", "SAAScanComplete", ""),
        ("exploration", "MultiSellExplorationData", ""),
        ("exploration", "SellExplorationData", ""),
        ("bounty-hunting", "Bounty", ""),
        ("combat-zones", "FactionKillBond", ""),
        ("powerplay", "PowerplayMerits", ""),
        ("on-foot", "CollectItems", ""),
        ("on-foot", "SellMicroResources", ""),
        ("exobiology", "ScanOrganic", ""),
        ("exobiology", "SellOrganicData", ""),
        ("trading", "MarketBuy", ""),
        ("engineering", "EngineerCraft", ""),
        ("carrier", "CarrierJumpRequest", ""),
        ("missions", "MissionCompleted", ""),
        ("search-and-rescue", "SearchAndRescue", ""),
        ("fighter", "LaunchFighter", ",\"PlayerControlled\":true"),
        ("colonisation", "ColonisationContribution", ""),
    ];

    [Fact]
    public void EachDatingEventDatesItsActivity()
    {
        var lines = new List<string> { Line("2026-01-01T00:00:00Z", "LoadGame", $",\"FID\":\"{Fid}\"") };
        lines.AddRange(Dating.Select((d, i) => Line($"2026-02-{i + 1:00}T00:00:00Z", d.Kind, d.Extra)));

        var ledger = Ledger();
        ledger.FoldHistory([Journal([.. lines])], TestContext.Current.CancellationToken);

        foreach (var key in ActivityCatalogue.All.Select(entry => entry.Key))
        {
            var latest = Dating.Select((d, i) => (d.Key, At: DateTimeOffset.Parse($"2026-02-{i + 1:00}T00:00:00Z")))
                .Where(d => d.Key == key).Max(d => d.At);

            Assert.Equal(latest, ledger.LastDone(key, Fid));
        }

        Assert.Equal(DateTimeOffset.Parse("2026-01-01T00:00:00Z"), ledger.CorpusFrom(Fid));
    }

    [Fact]
    public void EventsWrittenWithoutDoingTheActivityDateNothing()
    {
        var ledger = Ledger();
        ledger.FoldHistory([Journal(
            Line("2026-01-01T00:00:00Z", "LoadGame", $",\"FID\":\"{Fid}\""),
            Line("2026-01-02T00:00:00Z", "ProspectedAsteroid"),
            Line("2026-01-03T00:00:00Z", "ShipTargeted"),
            Line("2026-01-04T00:00:00Z", "Scan"),
            Line("2026-01-05T00:00:00Z", "CarrierJump"),
            Line("2026-01-06T00:00:00Z", "LaunchFighter", ",\"PlayerControlled\":false"))], TestContext.Current.CancellationToken);

        Assert.All(ActivityCatalogue.All, entry => Assert.Null(ledger.LastDone(entry.Key, Fid)));
    }

    [Fact]
    public void TheStalestThreeAreTheOldestSuggestedOnesOldestFirst()
    {
        var ledger = Ledger();
        ledger.FoldHistory([Journal(
            Line("2026-01-01T00:00:00Z", "LoadGame", $",\"FID\":\"{Fid}\""),
            Line("2026-01-02T00:00:00Z", "Bounty"),
            Line("2026-01-03T00:00:00Z", "MarketBuy"),
            Line("2026-01-04T00:00:00Z", "MiningRefined"),
            Line("2026-01-05T00:00:00Z", "EngineerCraft"),
            Line("2026-01-06T00:00:00Z", "MissionCompleted"))], TestContext.Current.CancellationToken);

        ledger.SetSuggested("bounty-hunting", Fid, false);

        Assert.Equal(["trading", "mining", "engineering"], ledger.Stalest(3, Fid).Select(a => a.Key));
        Assert.Equal(DateTimeOffset.Parse("2026-01-02T00:00:00Z"), ledger.LastDone("bounty-hunting", Fid));
    }

    [Fact]
    public void ASuggestionChoiceSurvivesAReload()
    {
        Ledger().SetSuggested("mining", Fid, false);

        var reloaded = Ledger();
        reloaded.Load();

        Assert.False(reloaded.IsSuggested("mining", Fid));
        Assert.True(reloaded.IsSuggested("trading", Fid));

        reloaded.SetSuggested("mining", Fid, true);
        var again = Ledger();
        again.Load();

        Assert.True(again.IsSuggested("mining", Fid));
    }

    [Fact]
    public void ALiveEventMovesTheDateForward()
    {
        var ledger = Ledger();
        ledger.FoldHistory([Journal(
            Line("2026-01-01T00:00:00Z", "LoadGame", $",\"FID\":\"{Fid}\""),
            Line("2026-01-02T00:00:00Z", "MarketBuy"))], TestContext.Current.CancellationToken);

        var live = new[] { D47.Core.Journal.JournalEvent.TryParse(Line("2026-03-01T00:00:00Z", "MarketBuy"), NullLogger.Instance, out var e) ? e! : null! };
        ledger.Apply(live, Fid);

        Assert.Equal(DateTimeOffset.Parse("2026-03-01T00:00:00Z"), ledger.LastDone("trading", Fid));
    }
}

public sealed class TheCatalogueFindsTheWordsACommanderUsesTests
{
    [Fact]
    public void EveryKeyNameAndPhraseFindsItsEntry()
    {
        foreach (var entry in ActivityCatalogue.All)
        {
            foreach (var term in ActivityCatalogue.Terms(entry))
            {
                Assert.Equal(entry.Key, ActivityCatalogue.Find(term)?.Key);
                Assert.Equal(entry.Key, ActivityCatalogue.Find(term.ToUpperInvariant())?.Key);
            }
        }
    }

    [Fact]
    public void NoPhraseBelongsToTwoEntries()
    {
        var shared = ActivityCatalogue.All
            .SelectMany(entry => ActivityCatalogue.Terms(entry).Select(term => (term, entry.Key)))
            .GroupBy(pair => pair.term, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(pair => pair.Key).Distinct().Count() > 1)
            .Select(group => group.Key);

        Assert.Empty(shared);
    }

    [Fact]
    public void TheLongestMatchWins() =>
        Assert.Equal("carrier", ActivityCatalogue.Find("fleet carrier")?.Key);

    [Theory]
    [InlineData("combat")]
    [InlineData("racing")]
    [InlineData("hauling")]
    [InlineData("")]
    public void AWordTheCatalogueDoesNotHoldFindsNothing(string text) =>
        Assert.Null(ActivityCatalogue.Find(text));
}
