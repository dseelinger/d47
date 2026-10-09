using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>A beacon beat fires on a data-link scan made in its Guardian beacon system, and on nothing else.</summary>
public sealed class ABeaconBeatFiresOnTheScanTests : IDisposable
{
    private const long Beacon = 13872878396833;

    private const long OtherBeacon = 4208161886922;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "d47-beacon-beat", Guid.NewGuid().ToString("N"));

    public ABeaconBeatFiresOnTheScanTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static Adventure ChapterOne(DateTimeOffset? acceptedAt = null) => new()
    {
        Key = "chapter-one",
        Name = "Chapter One",
        Source = AdventureSource.Generated,
        Beats =
        [
            Beat("The Lantern", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" }, "Scoop here."),
            Beat("The Beacon", "resolution", new AdventureTrigger { Kind = TriggerKind.Beacon, SystemAddress = Beacon, System = "IC 2391 Sector MX-T b3-6" }, "Scan it."),
        ],
        AcceptedAt = acceptedAt,
    };

    private static JournalEvent DataScanned(DateTimeOffset at, string type = "$Datascan_AncientBeacon;") =>
        Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"DataScanned", "Type":"{{type}}" }""");

    private static AdventureStanding Run(Adventure adventure, IEnumerable<JournalEvent> events) =>
        events.Aggregate(AdventureFold.Start(adventure), AdventureFold.Apply);

    [Fact]
    public void ArrivingIsNotEnoughAndTheScanFinishesTheChapter()
    {
        var arrived = Run(ChapterOne(Accepted), [Jump(Lantern, Accepted.AddMinutes(1)), Jump(Beacon, Accepted.AddMinutes(2))]);

        Assert.Equal(1, arrived.Current);
        Assert.Equal(TriggerKind.Beacon, arrived.CurrentBeat!.Trigger.Kind);
        Assert.False(arrived.IsDone);

        var scanned = AdventureFold.Apply(arrived, DataScanned(Accepted.AddMinutes(3)));

        Assert.True(scanned.IsDone);
        Assert.Equal(Accepted.AddMinutes(3), scanned.FinishedAt);
    }

    [Fact]
    public void AScanInAnotherBeaconSystemOrNoneDoesNotFireIt()
    {
        var elsewhere = Run(ChapterOne(Accepted),
        [
            Jump(Lantern, Accepted.AddMinutes(1)),
            DataScanned(Accepted.AddMinutes(2)),
            Jump(OtherBeacon, Accepted.AddMinutes(3)),
            DataScanned(Accepted.AddMinutes(4)),
        ]);

        Assert.Equal(1, elsewhere.Current);
        Assert.False(elsewhere.IsDone);
    }

    [Fact]
    public void ACommanderAlreadyThereAtAcceptanceNeedsOnlyTheScan()
    {
        var chapter = ChapterOne(Accepted) with
        {
            Beats = [Beat("The Beacon", "resolution", new AdventureTrigger { Kind = TriggerKind.Beacon, SystemAddress = Beacon }, "Scan it.")],
        };

        var standing = Run(chapter, [Location(Beacon, Accepted.AddMinutes(-5)), DataScanned(Accepted.AddMinutes(1))]);

        Assert.True(standing.IsDone);
    }

    [Fact]
    public void BeginningInTheBeaconSystemCountsTheSystemOnTheLivePath()
    {
        var book = new AdventureBook(
            new AdventureStore(Path.Combine(_folder, "adventures.json"), NullLogger<AdventureStore>.Instance),
            NullLogger<AdventureBook>.Instance);

        book.Observe(Location(Beacon, Accepted.AddMinutes(-5)), "F1");
        book.Write("F1", ChapterOne() with
        {
            Beats = [Beat("The Beacon", "resolution", new AdventureTrigger { Kind = TriggerKind.Beacon, SystemAddress = Beacon }, "Scan it.")],
        });

        Assert.Null(book.Begin("F1", "chapter-one", Accepted));

        book.Observe(DataScanned(Accepted.AddMinutes(1)), "F1");

        Assert.True(book.Standing("F1", "chapter-one")!.IsDone);
    }

    [Fact]
    public void TheCatchUpReachesTheSameStandingAsTheLiveFold()
    {
        JournalEvent[] journal =
        [
            Commander("F1", Accepted.AddMinutes(-6)),
            Location(Lantern, Accepted.AddMinutes(-5)),
            Jump(Lantern, Accepted.AddMinutes(1)),
            Jump(Beacon, Accepted.AddMinutes(2)),
            DataScanned(Accepted.AddMinutes(3)),
        ];

        var path = Path.Combine(_folder, "Journal.2026-08-22T194000.01.log");
        File.WriteAllLines(path, journal.Select(e => e.Raw.GetRawText()));

        AdventureBook Book(string name) => new(
            new AdventureStore(Path.Combine(_folder, name), NullLogger<AdventureStore>.Instance),
            NullLogger<AdventureBook>.Instance);

        var live = Book("live.json");
        live.Write("F1", ChapterOne(Accepted));

        foreach (var journalEvent in journal)
        {
            live.Observe(journalEvent, "F1");
        }

        var walked = Book("walked.json");
        walked.Write("F1", ChapterOne(Accepted));
        walked.CatchUp([path]);

        var fromLive = live.Standing("F1", "chapter-one")!;
        var fromWalk = walked.Standing("F1", "chapter-one")!;

        Assert.True(fromLive.IsDone);
        Assert.Equal(fromLive.Fired, fromWalk.Fired);
        Assert.Equal(fromLive.SystemAddress, fromWalk.SystemAddress);
    }

    [Fact]
    public void ABeaconBeatIsResolvedOnlyInABeaconSystem()
    {
        Assert.True(new AdventureTrigger { Kind = TriggerKind.Beacon, SystemAddress = Beacon }.IsResolved);
        Assert.False(new AdventureTrigger { Kind = TriggerKind.Beacon, SystemAddress = Lantern }.IsResolved);
        Assert.False(new AdventureTrigger { Kind = TriggerKind.Beacon }.IsResolved);

        Assert.Equal(
            "scan the Guardian beacon in IC 2391 Sector MX-T b3-6",
            new AdventureTrigger { Kind = TriggerKind.Beacon, SystemAddress = Beacon }.Describe());
    }

    [Fact]
    public void AWrittenBeaconBeatOutsideABeaconSystemIsRefused()
    {
        var chapter = ChapterOne() with
        {
            Beats = [Beat("Wrong", null, new AdventureTrigger { Kind = TriggerKind.Beacon, SystemAddress = Lantern, System = "Ossen's Lantern" }, "Scan it.")],
        };

        Assert.Contains(
            "Objective 1 (Wrong) waits for a Guardian beacon scan in a system with no Guardian beacon.",
            AdventureValidation.Problems(chapter));
        Assert.Empty(AdventureValidation.Problems(ChapterOne()));
        Assert.True(AdventureValidation.TryKind("beacon", out var kind));
        Assert.Equal(TriggerKind.Beacon, kind);
    }

    [Trait("Category", "Gate")]
    [Fact]
    public void TheStoryAndTheBeatShareOneRuleForABeaconScan()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);

        while (root is not null && !File.Exists(Path.Combine(root.FullName, "d47.slnx")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);

        foreach (var file in new[] { "Stories/StoryDirector.cs", "Adventures/AdventureFold.cs" })
        {
            var source = File.ReadAllText(Path.Combine(root.FullName, "src", "D47.Core", file));

            Assert.Contains("GuardianCores.IsBeaconScan(journalEvent,", source);
            Assert.DoesNotContain("\"DataScanned\"", source);
        }

        Assert.True(GuardianCores.IsBeaconScan(DataScanned(Accepted), Beacon));
        Assert.False(GuardianCores.IsBeaconScan(DataScanned(Accepted), Lantern));
        Assert.False(GuardianCores.IsBeaconScan(DataScanned(Accepted), null));
        Assert.False(GuardianCores.IsBeaconScan(Jump(Beacon, Accepted), Beacon));
    }
}
