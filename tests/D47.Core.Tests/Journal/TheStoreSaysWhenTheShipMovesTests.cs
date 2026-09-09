using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// The signal a surface reading live state needs: the system the Commander is in changed (#93).
/// </summary>
public class TheStoreSaysWhenTheShipMovesTests
{
    private static string FixturesDirectory { get; } = FindFixturesDirectory();

    private static GameStateStore Counting(out Func<int> moves)
    {
        var store = new GameStateStore();
        var count = 0;

        store.SystemChanged += () => count++;
        moves = () => count;

        return store;
    }

    private static void Feed(GameStateStore store, params string[] lines)
    {
        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }
    }

    [Fact]
    public void OneRaisePerLocationChangeRatherThanOnePerEvent()
    {
        using var install = new TempInstall();

        File.Copy(
            Path.Combine(FixturesDirectory, "Journal.2026-02-10T090000.01.log"),
            Path.Combine(install.Root, "Journal.2026-02-10T090000.01.log"));

        var store = Counting(out var moves);
        var events = new JournalSpine(install.Root, store, NullLoggerFactory.Instance).Poll();

        // The fixture holds eight parsable events and moves the ship twice: the opening Location, and the
        // FSDJump. The Docked that follows names the system it is already in, and the Undocked names none.
        Assert.Equal(8, events.Count);
        Assert.Equal(2, moves());
    }

    [Fact]
    public void ADockingInTheSameSystemRaisesNothing()
    {
        var store = Counting(out var moves);

        Feed(
            store,
            """{"timestamp":"2026-09-01T08:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
            """{"timestamp":"2026-09-01T08:00:01Z","event":"Location","StarSystem":"Laksak","Docked":false}""",
            """{"timestamp":"2026-09-01T08:00:02Z","event":"SupercruiseExit","StarSystem":"Laksak","Body":"Laksak A 1"}""",
            """{"timestamp":"2026-09-01T08:00:03Z","event":"Docked","StarSystem":"Laksak","StationName":"Trader's Rest","MarketID":128}""",
            """{"timestamp":"2026-09-01T08:00:04Z","event":"Undocked","StationName":"Trader's Rest"}""");

        Assert.Equal(1, moves());
    }

    [Fact]
    public void AJumpRaisesIt()
    {
        var store = Counting(out var moves);

        Feed(
            store,
            """{"timestamp":"2026-09-01T08:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
            """{"timestamp":"2026-09-01T08:00:01Z","event":"Location","StarSystem":"Shinrarta Dezhra","Docked":false}""",
            """{"timestamp":"2026-09-01T08:00:02Z","event":"FSDJump","StarSystem":"Laksak","FuelLevel":28.3}""");

        Assert.Equal(2, moves());
        Assert.Equal("Laksak", store.Active!.Location.StarSystem);
    }

    /// <summary>A carrier jump moves the Commander with the carrier, and folds through Location's arm.</summary>
    [Fact]
    public void ACarrierJumpRaisesIt()
    {
        var store = Counting(out var moves);

        Feed(
            store,
            """{"timestamp":"2026-09-01T08:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
            """{"timestamp":"2026-09-01T08:00:01Z","event":"Location","StarSystem":"Shinrarta Dezhra","Docked":true,"StationName":"Ghost","StationType":"FleetCarrier"}""",
            """{"timestamp":"2026-09-01T08:00:02Z","event":"CarrierJump","StarSystem":"Laksak","Docked":true,"StationName":"Ghost","StationType":"FleetCarrier"}""");

        Assert.Equal(2, moves());
    }

    /// <summary>
    /// A second Commander standing somewhere else is a change of where "here" is, on the same terms.
    /// </summary>
    [Fact]
    public void ASwitchToACommanderStandingElsewhereRaisesIt()
    {
        var store = Counting(out var moves);

        Feed(
            store,
            """{"timestamp":"2026-09-01T08:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
            """{"timestamp":"2026-09-01T08:00:01Z","event":"Location","StarSystem":"Laksak","Docked":false}""",
            """{"timestamp":"2026-09-01T09:00:00Z","event":"Commander","FID":"F2","Name":"Hicks"}""",
            """{"timestamp":"2026-09-01T09:00:01Z","event":"Location","StarSystem":"Deciat","Docked":false}""");

        // Laksak, then the switch to a Commander who is nowhere yet, then Deciat.
        Assert.Equal(3, moves());
    }

    private static string FindFixturesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new InvalidOperationException($"Could not find the repository root above {AppContext.BaseDirectory}.")
            : Path.Combine(directory.FullName, "tests", "fixtures", "journal");
    }
}
