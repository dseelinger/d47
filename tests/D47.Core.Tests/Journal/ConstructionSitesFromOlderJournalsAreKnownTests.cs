using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>A construction site docked at in an earlier session is known from the start of this one (#798).</summary>
public class ConstructionSitesFromOlderJournalsAreKnownTests
{
    private const string Fid = "F1234567";

    private const long MarketId = 3960809986;

    private const string LoadGame =
        """{ "timestamp":"2026-09-05T10:00:00Z", "event":"LoadGame", "FID":"F1234567", "Commander":"Fixture" }""";

    private const string LaterLoadGame =
        """{ "timestamp":"2026-09-07T10:00:00Z", "event":"LoadGame", "FID":"F1234567", "Commander":"Fixture" }""";

    private const string DockedAtShip =
        """{ "timestamp":"2026-09-05T10:05:00Z", "event":"Docked", "StationName":"$EXT_PANEL_ColonisationShip; Apianus's Pride", "StationType":"SurfaceStation", "StarSystem":"Col 285 Sector AB-C d1", "SystemAddress":123, "MarketID":3960809986 }""";

    private const string Undocked =
        """{ "timestamp":"2026-09-05T10:30:00Z", "event":"Undocked", "StationName":"$EXT_PANEL_ColonisationShip; Apianus's Pride", "MarketID":3960809986 }""";

    private const string Contribution =
        """{ "timestamp":"2026-09-05T10:07:00Z", "event":"ColonisationContribution", "MarketID":3960809986, "Contributions":[{"Name":"$Steel_name;","Name_Localised":"Steel","Amount":40}] }""";

    private const string Elsewhere =
        """{ "timestamp":"2026-09-07T10:01:00Z", "event":"Location", "StarSystem":"Sol", "SystemAddress":10477373803, "Docked":false }""";

    [Trait("Category", "Integration")]
    [Fact]
    public void ASiteInAnEarlierJournalIsFoundWithItsSystemAndStation()
    {
        using var install = new TempInstall();
        Write(install, "Journal.2026-09-05T100000.01.log", LoadGame, DockedAtShip, Depot("2026-09-05T10:06:00Z", 0.25), Undocked);
        Write(install, "Journal.2026-09-07T100000.01.log", LaterLoadGame, Elsewhere);

        var site = Assert.Single(Sites(install)[Fid].All);

        Assert.Equal(MarketId, site.MarketId);
        Assert.Equal("Col 285 Sector AB-C d1", site.StarSystem);
        Assert.Equal("Apianus's Pride", site.StationName);
        Assert.Equal(0.25, site.Progress);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void ASiteSeenLiveMoreRecentlyKeepsTheLiveFigures()
    {
        using var install = new TempInstall();
        Write(install, "Journal.2026-09-05T100000.01.log", LoadGame, DockedAtShip, Depot("2026-09-05T10:06:00Z", 0.25));

        var backfill = Backfill(install);
        var store = StoreOver(backfill);

        store.Apply(Event(LaterLoadGame));
        store.Apply(Event(DockedAtShip.Replace("2026-09-05T10:05:00Z", "2026-09-07T10:05:00Z", StringComparison.Ordinal)));
        store.Apply(Event(Depot("2026-09-07T10:06:00Z", 0.75)));

        backfill.Run(TestContext.Current.CancellationToken);
        store.RestoreLate();

        var site = Assert.Single(store.Active!.Colonisation.All);

        Assert.Equal(0.75, site.Progress);
        Assert.Equal(DateTimeOffset.Parse("2026-09-07T10:06:00Z", System.Globalization.CultureInfo.InvariantCulture), site.SeenAt);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void TheCurrentJournalFoldedByBothTheWalkAndTheReaderChangesNothing()
    {
        using var install = new TempInstall();
        string[] journal = [LoadGame, DockedAtShip, Depot("2026-09-05T10:06:00Z", 0.25), Contribution];
        Write(install, "Journal.2026-09-05T100000.01.log", journal);

        var backfill = Backfill(install);
        var store = StoreOver(backfill);

        foreach (var line in journal)
        {
            store.Apply(Event(line));
        }

        var before = store.Active!.Colonisation;

        backfill.Run(TestContext.Current.CancellationToken);
        store.RestoreLate();

        var after = store.Active!.Colonisation;
        var site = Assert.Single(after.All);

        Assert.Equal(Assert.Single(before.All), site);
        Assert.Equal(40, after.MineDeliveredTo(MarketId)["steel"]);
    }

    [Fact]
    public void TheColonisationShipIsNamedWithoutItsToken()
    {
        var location = JournalLocation.Unknown.Apply(Event(DockedAtShip));

        Assert.Equal("Apianus's Pride", location.StationName);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void TheWalkIsTimedAsAStepOfStartup()
    {
        using var install = new TempInstall();
        var steps = new List<string>();

        var backfill = new HistoryBackfill
        {
            Directory = install.Root,
            Loggers = NullLoggerFactory.Instance,
            Step = name =>
            {
                steps.Add(name);
                return new Done();
            },
        };

        backfill.Run(TestContext.Current.CancellationToken);

        Assert.Contains("colonisation backfill", steps);
    }

    private static string Depot(string at, double progress) =>
        $$"""{ "timestamp":"{{at}}", "event":"ColonisationConstructionDepot", "MarketID":3960809986, "ConstructionProgress":{{progress.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, "ConstructionComplete":false, "ConstructionFailed":false, "ResourcesRequired":[{"Name":"$steel_name;","Name_Localised":"Steel","RequiredAmount":500,"ProvidedAmount":80,"Payment":3239}] }""";

    private static IReadOnlyDictionary<string, ColonisationSites> Sites(TempInstall install) =>
        ColonisationBackfill.FromHistory(install.Root, NullLogger.Instance, TestContext.Current.CancellationToken);

    private static HistoryBackfill Backfill(TempInstall install) =>
        new() { Directory = install.Root, Loggers = NullLoggerFactory.Instance };

    private static GameStateStore StoreOver(HistoryBackfill backfill) =>
        new() { RestoreColonisation = fid => backfill.Colonisation?.GetValueOrDefault(fid) };

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static void Write(TempInstall install, string name, params string[] lines) =>
        File.WriteAllLines(Path.Combine(install.Root, name), lines);

    private sealed class Done : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
