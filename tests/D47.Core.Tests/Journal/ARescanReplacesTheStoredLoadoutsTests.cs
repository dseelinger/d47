using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>A rescan rebuilds the stored loadouts from the journals and answers with the rescan sentence.</summary>
public class ARescanReplacesTheStoredLoadoutsTests
{
    private const string Folder = @"C:\journals";

    private static string Boarding(string timestamp, string hull, int id) =>
        $$"""{"timestamp":"{{timestamp}}","event":"Loadout","Ship":"{{hull}}","ShipID":{{id}},"ShipName":"Kestrel","ShipIdent":"KE-01","MaxJumpRange":42.5,"HullValue":100,"ModulesValue":200,"Rebuy":15,"HullHealth":1.0,"UnladenMass":400.0,"CargoCapacity":64,"FuelCapacity":{"Main":32.0,"Reserve":1.07},"Modules":[{"Slot":"MainEngines","Item":"int_engine_size7_class2","On":true,"Priority":1,"Health":1.0,"Value":900}]}""";

    private static MemoryFileSystem TwoJournals()
    {
        var files = new MemoryFileSystem();
        files.CreateFolder(Folder);
        files.WriteText(
            $@"{Folder}\Journal.2026-01-02T100000.01.log",
            """{"timestamp":"2026-01-02T10:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""
            + "\n" + Boarding("2026-01-02T10:05:00Z", "anaconda", 51) + "\n");
        files.WriteText(
            $@"{Folder}\Journal.2026-01-03T100000.01.log",
            Boarding("2026-01-03T10:05:00Z", "type9_military", 53) + "\n");

        return files;
    }

    private static GameStateStore Known()
    {
        var state = new GameStateStore();
        const string line = """{"timestamp":"2026-01-01T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""";
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        state.Apply(parsed!);

        return state;
    }

    private static LoadoutRescanner Rescanner(MemoryFileSystem files, GameStateStore state, LoadoutStore store) =>
        new(state, store, files, Folder, NullLoggerFactory.Instance, () => new DateTimeOffset(2026, 1, 4, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task TheStoredLoadoutsAreReplacedAndTheSentenceIsReturned()
    {
        var files = TwoJournals();
        var state = Known();
        var store = new LoadoutStore(@"C:\data\loadouts.json", files, NullLogger<LoadoutStore>.Instance);

        var answer = await Rescanner(files, state, store).RescanAsync(new Progress<double>(), TestContext.Current.CancellationToken);

        var found = LoadoutBackfill.Rescan(files, Folder, NullLogger.Instance);

        Assert.Equal(2, found.Files);
        Assert.Equal(LoadoutRescanner.Sentence(found, state.Active?.Identity.FrontierId), answer);
        Assert.Equal(found.Ships, store.All.Values.Sum(ships => ships.Ships.Count));
        Assert.Equal(found.ByCommander["F1"].Ships.Count, state.Active!.Loadouts.Ships.Count);
        Assert.True(found.Ships > 0);
    }

    [Fact]
    public async Task ASecondRescanStartedWhileOneRunsIsRefused()
    {
        var files = TwoJournals();
        var rescanner = Rescanner(
            files, new GameStateStore(), new LoadoutStore(Path.Combine(Folder, "loadouts.json"), files, NullLogger<LoadoutStore>.Instance));
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();

        var first = rescanner.RescanAsync(new Holding(started, release), TestContext.Current.CancellationToken);
        Assert.True(started.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

        var second = await rescanner.RescanAsync(new Progress<double>(), TestContext.Current.CancellationToken);
        release.Set();
        await first;

        Assert.Equal("A rescan is already running.", second);
    }

    /// <summary>Holds the first rescan on the pool until released.</summary>
    private sealed class Holding(ManualResetEventSlim started, ManualResetEventSlim release) : IProgress<double>
    {
        public void Report(double value)
        {
            started.Set();
            release.Wait(TimeSpan.FromSeconds(30));
        }
    }
}
