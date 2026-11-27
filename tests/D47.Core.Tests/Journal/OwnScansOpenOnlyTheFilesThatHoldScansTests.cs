using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// <see cref="OwnScanIndex.Read"/> opens only the files holding a system's scans, and answers as a fold of every
/// exploration event in every file would.
/// </summary>
public sealed class OwnScansOpenOnlyTheFilesThatHoldScansTests
{
    private const long Home = 1000;

    private const long Route = 2000;

    private const long Far = 3000;

    private static readonly HashSet<string> Exploration = new(StringComparer.Ordinal)
    {
        "Scan", "ScanBaryCentre", "FSSDiscoveryScan", "FSSAllBodiesFound", "FSSBodySignals", "SAASignalsFound",
        "SAAScanComplete", "FSDJump", "Location", "CarrierJump",
    };

    private static readonly JsonSerializerOptions Json = new() { IncludeFields = true, MaxDepth = 64 };

    private static string Line(string time, string body) => "{\"timestamp\":\"2026-10-" + time + "Z\"," + body + "}";

    private static string Login(string time, string fid = "F1") =>
        Line(time, "\"event\":\"LoadGame\",\"FID\":\"" + fid + "\",\"Commander\":\"Jameson\"");

    private static string Erase(string time, string fid = "F1") =>
        Line(time, "\"event\":\"NewCommander\",\"FID\":\"" + fid + "\",\"Name\":\"Jameson\"");

    private static string Entry(string time, long system, long population = 0, string kind = "FSDJump", double x = 1.5) =>
        Line(time, "\"event\":\"" + kind + "\",\"StarSystem\":\"Ocrinus " + system + "\",\"SystemAddress\":" + system
            + ",\"StarPos\":[" + x.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",-2.25,30.0],\"Population\":" + population
            + ",\"Factions\":[{\"Name\":\"Ocrinus Crimson Order\",\"Influence\":0.4}]");

    private static string Honk(string time, long system, int bodies) =>
        Line(time, "\"event\":\"FSSDiscoveryScan\",\"Progress\":0.5,\"BodyCount\":" + bodies + ",\"NonBodyCount\":3,\"SystemName\":\"Ocrinus "
            + system + "\",\"SystemAddress\":" + system);

    private static string AllFound(string time, long system) =>
        Line(time, "\"event\":\"FSSAllBodiesFound\",\"SystemName\":\"Ocrinus " + system + "\",\"SystemAddress\":" + system + ",\"Count\":4");

    private static string Scan(string time, long system, int bodyId) =>
        Line(time, "\"event\":\"Scan\",\"ScanType\":\"Detailed\",\"BodyName\":\"Ocrinus " + system + " " + bodyId + "\",\"BodyID\":" + bodyId
            + ",\"StarSystem\":\"Ocrinus " + system + "\",\"SystemAddress\":" + system
            + ",\"PlanetClass\":\"Icy body\",\"TerraformState\":\"\",\"Landable\":true,\"WasDiscovered\":false,\"WasMapped\":false,\"WasFootfalled\":false");

    private static string RingSignals(string time, long system, int bodyId) =>
        Line(time, "\"event\":\"SAASignalsFound\",\"BodyName\":\"Ocrinus " + system + " " + bodyId + " A Ring\",\"BodyID\":" + bodyId
            + ",\"SystemAddress\":" + system + ",\"Signals\":[{\"Type\":\"Platinum\",\"Count\":2}]");

    private static (OwnScanIndex Index, Counting Files, List<string> Paths) Index(params string[][] files)
    {
        var memory = new MemoryFileSystem();
        var paths = new List<string>();

        for (var i = 0; i < files.Length; i++)
        {
            var path = $"j/Journal.2026-10-{10 + i:00}T000000.01.log";
            memory.WriteLines(path, files[i]);
            paths.Add(path);
        }

        var counting = new Counting(memory);
        var index = new OwnScanIndex(counting, NullLogger.Instance);
        index.FoldHistory(paths, TestContext.Current.CancellationToken);

        return (index, counting, paths);
    }

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    /// <summary>A fold of every exploration event for the Commander and system in every file, then the live events.</summary>
    private static OwnSystem? EveryEvent(string[][] files, IEnumerable<string> live, string fid, long system)
    {
        var found = new List<JournalEvent>();

        foreach (var file in files)
        {
            var current = string.Empty;

            foreach (var journalEvent in OperationMarker.Mark(file.Select(Event)))
            {
                if (journalEvent.Kind is "LoadGame" or "Commander" or "NewCommander")
                {
                    current = journalEvent.String("FID")!;

                    if (journalEvent.Kind == "NewCommander" && current == fid)
                    {
                        found.Clear();
                    }

                    continue;
                }

                if (current == fid && !journalEvent.InOperation && Exploration.Contains(journalEvent.Kind)
                    && journalEvent.Long("SystemAddress") == system)
                {
                    found.Add(journalEvent);
                }
            }
        }

        var liveFid = string.Empty;

        foreach (var journalEvent in live.Select(Event))
        {
            if (journalEvent.Kind is "LoadGame")
            {
                liveFid = journalEvent.String("FID")!;
            }
            else if (liveFid == fid && Exploration.Contains(journalEvent.Kind) && journalEvent.Long("SystemAddress") == system)
            {
                found.Add(journalEvent);
            }
        }

        return OwnSystemFold.Fold(system, found);
    }

    private static string Describe(OwnSystem? system, long address) =>
        system is null
            ? "null"
            : string.Join(
                " | ",
                JsonSerializer.Serialize(system, Json),
                JsonSerializer.Serialize(system.Scans.InSystem(address).OrderBy(body => body.BodyId), Json),
                JsonSerializer.Serialize(system.Signals.All, Json));

    [Fact]
    public void EveryReadMatchesAFoldOfEveryEventInEveryFile()
    {
        string[][] files =
        [
            [Login("10T09:00:00"), Entry("10T09:00:10", Home, kind: "Location"), Scan("10T09:01:00", Home, 1), Honk("10T09:02:00", Home, 5),
                Entry("10T09:03:00", Route, population: 4000), Scan("10T09:03:05", Route, 1)],
            [Login("11T09:00:00"), Entry("11T09:00:10", Home, population: 9000, x: 2.5), Honk("11T09:01:00", Home, 6),
                AllFound("11T09:02:00", Home), Entry("11T09:03:00", Far)],
            [Login("12T09:00:00"), Scan("12T09:01:00", Home, 2), RingSignals("12T09:02:00", Home, 2), RingSignals("12T09:03:00", Home, 2)],
            [Login("13T09:00:00", "F2"), Entry("13T09:00:10", Home), Scan("13T09:01:00", Home, 3), Erase("13T09:02:00", "F2"),
                Login("13T09:03:00", "F2"), Entry("13T09:04:00", Route)],
            [Login("14T09:00:00"), Entry("14T09:00:10", Home, kind: "CarrierJump"), Entry("14T09:01:00", Far, population: 10),
                Entry("14T09:02:00", Far)],
        ];

        string[] live = [Login("15T09:00:00"), Entry("15T09:00:10", Home, population: 9000), Scan("15T09:01:00", Home, 4)];

        var (index, _, _) = Index(files);
        index.Apply([.. live.Select(Event)]);

        foreach (var fid in new[] { "F1", "F2" })
        {
            foreach (var system in new[] { Home, Route, Far, 4000L })
            {
                var expected = Describe(EveryEvent(files, live, fid, system), system);

                Assert.Equal(expected, Describe(index.Read(fid, system, TestContext.Current.CancellationToken), system));
                Assert.Equal(expected, Describe(index.Read(fid, system, TestContext.Current.CancellationToken), system));
            }
        }
    }

    [Fact]
    public void AFileWithOnlyEntriesAndDiscoveryScansIsNotOpened()
    {
        var (index, files, paths) = Index(
            [Login("10T09:00:00"), Entry("10T09:00:10", Home, kind: "Location"), Scan("10T09:01:00", Home, 1)],
            [Login("11T09:00:00"), Entry("11T09:00:10", Home), Honk("11T09:01:00", Home, 5)],
            [Login("12T09:00:00"), Entry("12T09:00:10", Home), Honk("12T09:01:00", Home, 6), AllFound("12T09:02:00", Home)]);

        files.Opened.Clear();
        var system = index.Read("F1", Home, TestContext.Current.CancellationToken)!;

        Assert.Equal(
            (paths[0], 6, new DateTimeOffset(2026, 10, 12, 9, 2, 0, TimeSpan.Zero)),
            (string.Join(", ", files.Opened), system.BodyCount, system.LatestAt));
    }

    [Fact]
    public void ASystemOnlyJumpedThroughReadsBackWithItsNameAndLatestVisitAndOpensNoFile()
    {
        var (index, files, _) = Index(
            [Login("10T09:00:00"), Entry("10T09:00:10", Route, x: 1.5)],
            [Login("11T09:00:00"), Entry("11T09:00:10", Route, x: 2.5)]);

        files.Opened.Clear();
        var system = index.Read("F1", Route, TestContext.Current.CancellationToken)!;

        Assert.Equal(
            ("Ocrinus 2000", 2.5, new DateTimeOffset(2026, 10, 11, 9, 0, 10, TimeSpan.Zero), 0, 0),
            (system.Name, system.Position!.Value.X, system.LatestAt, system.Bodies.Count, files.Opened.Count));
    }

    [Fact]
    public void APopulationSeenOnlyInAJumpFileStillMarksALaterScanAsFootfalled()
    {
        var (index, _, _) = Index(
            [Login("10T09:00:00"), Entry("10T09:00:10", Home, kind: "Location"), Scan("10T09:01:00", Home, 1)],
            [Login("11T09:00:00"), Entry("11T09:00:10", Home, population: 9000)],
            [Login("12T09:00:00"), Scan("12T09:01:00", Home, 2)]);

        var scans = index.Read("F1", Home, TestContext.Current.CancellationToken)!.Scans;

        Assert.Equal((false, true), (scans.For(Home, 1)!.WasFootfalled, scans.For(Home, 2)!.WasFootfalled));
    }

    [Fact]
    public void AnEraseDropsTheSystemsEntries()
    {
        var (index, _, _) = Index([Login("10T09:00:00"), Entry("10T09:00:10", Route), Honk("10T09:01:00", Route, 3), Erase("10T09:02:00")]);

        Assert.Null(index.Read("F1", Route, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ASecondReadOpensNoFileAndTakesTheLiveEvents()
    {
        var (index, files, _) = Index([Login("10T09:00:00"), Entry("10T09:00:10", Home, kind: "Location"), Scan("10T09:01:00", Home, 1)]);

        files.Opened.Clear();
        index.Read("F1", Home, TestContext.Current.CancellationToken);
        index.Apply([Event(Login("11T09:00:00")), Event(Scan("11T09:01:00", Home, 2))]);

        var system = index.Read("F1", Home, TestContext.Current.CancellationToken)!;

        Assert.Equal((1, 2), (files.Opened.Count, system.Bodies.Count));
    }

    private sealed class Counting(IFileSystem inner) : IFileSystem
    {
        public List<string> Opened { get; } = [];

        public FileState? Stat(string path) => inner.Stat(path);

        public DateTime? FolderWritten(string folder) => inner.FolderWritten(folder);

        public string? ReadText(string path) => inner.ReadText(path);

        public byte[]? ReadBytes(string path) => inner.ReadBytes(path);

        public Stream? OpenRead(string path)
        {
            Opened.Add(path);
            return inner.OpenRead(path);
        }

        public void WriteText(string path, string contents) => inner.WriteText(path, contents);

        public void WriteBytes(string path, byte[] contents) => inner.WriteBytes(path, contents);

        public void AppendText(string path, string contents) => inner.AppendText(path, contents);

        public void CreateFolder(string folder) => inner.CreateFolder(folder);

        public void DeleteFolder(string folder) => inner.DeleteFolder(folder);

        public void Delete(string path) => inner.Delete(path);

        public void Copy(string from, string to) => inner.Copy(from, to);

        public IReadOnlyList<string> Enumerate(string folder, string pattern, bool recursive = false) =>
            inner.Enumerate(folder, pattern, recursive);
    }
}
