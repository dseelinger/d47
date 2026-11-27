using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary><see cref="OwnScanIndex"/> over inline journal lines in memory.</summary>
public sealed class TheCommandersOwnScansAreReadBackTests
{
    private const long System = 1000;

    private const long Elsewhere = 2000;

    private static string Line(string time, string body) => "{\"timestamp\":\"2026-10-" + time + "Z\"," + body + "}";

    private static string Login(string time, string fid = "F1") =>
        Line(time, "\"event\":\"LoadGame\",\"FID\":\"" + fid + "\",\"Commander\":\"Jameson\"");

    private static string Scan(string time, int bodyId, string name, long system = System, string extra = "") =>
        Line(time, "\"event\":\"Scan\",\"ScanType\":\"Detailed\",\"BodyName\":\"" + name + "\",\"BodyID\":" + bodyId
            + ",\"StarSystem\":\"Ocrinus\",\"SystemAddress\":" + system + ",\"PlanetClass\":\"Icy body\",\"TerraformState\":\"\"" + extra);

    private static string BodySignals(string time, int bodyId, string kind, int count) =>
        Line(time, "\"event\":\"FSSBodySignals\",\"BodyName\":\"Ocrinus 1\",\"BodyID\":" + bodyId + ",\"SystemAddress\":" + System
            + ",\"Signals\":[{\"Type\":\"" + kind + "\",\"Count\":" + count + "}]");

    private static string SurfaceSignals(string time, int bodyId) =>
        Line(time, "\"event\":\"SAASignalsFound\",\"BodyName\":\"Ocrinus 1\",\"BodyID\":" + bodyId + ",\"SystemAddress\":" + System
            + ",\"Signals\":[{\"Type\":\"$SAA_SignalType_Biological;\",\"Count\":2}],"
            + "\"Genuses\":[{\"Genus\":\"$Codex_Ent_Bacterial_Genus_Name;\"},{\"Genus\":\"$Codex_Ent_Shrubs_Genus_Name;\"}]");

    private static (OwnScanIndex Index, MemoryFileSystem Files, List<string> Paths) Index(params string[][] files)
    {
        var fileSystem = new MemoryFileSystem();
        var paths = new List<string>();

        for (var i = 0; i < files.Length; i++)
        {
            var path = $"j/Journal.2026-10-{10 + i:00}T000000.01.log";
            fileSystem.WriteLines(path, files[i]);
            paths.Add(path);
        }

        var index = new OwnScanIndex(fileSystem, NullLogger.Instance);
        index.FoldHistory(paths, TestContext.Current.CancellationToken);

        return (index, fileSystem, paths);
    }

    private static OwnSystem? Read(OwnScanIndex index, string fid = "F1", long system = System) =>
        index.Read(fid, system, TestContext.Current.CancellationToken);

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    [Fact]
    public void ASystemRevisitedInALaterJournalReadsBackWithTheBodiesOfBoth()
    {
        var (index, _, _) = Index(
            [Login("10T09:00:00"), Scan("10T09:01:00", 1, "Ocrinus 1")],
            [Login("11T09:00:00"), Scan("11T09:01:00", 2, "Ocrinus 2")]);

        var system = Read(index);

        Assert.NotNull(system);
        Assert.Equal(["Ocrinus 1", "Ocrinus 2"], system.Bodies.Select(body => body.Name));
    }

    [Fact]
    public void ABarycentreIsKeptAndAMoonsParentSkipsIt()
    {
        var (index, _, _) = Index(
        [
            Login("10T09:00:00"),
            Line("10T09:00:30", "\"event\":\"ScanBaryCentre\",\"StarSystem\":\"Ocrinus\",\"SystemAddress\":" + System
                + ",\"BodyID\":5,\"SemiMajorAxis\":149597870700,\"Eccentricity\":0.1,\"OrbitalInclination\":2,\"Periapsis\":30,"
                + "\"OrbitalPeriod\":864000,\"AscendingNode\":4,\"MeanAnomaly\":5"),
            Scan("10T09:01:00", 6, "Ocrinus 1 a", extra: ",\"Parents\":[{\"Null\":5},{\"Planet\":3},{\"Star\":0}]"),
        ]);

        var system = Read(index)!;
        var barycentre = Assert.Single(system.Barycentres);

        Assert.Equal((5, 1.0, 10.0), (barycentre.BodyId, barycentre.SemiMajorAxis!.Value, barycentre.OrbitalPeriod!.Value));
        Assert.Equal([("Planet", 3), ("Star", 0)], barycentre.Parents);
        Assert.Equal(3, Assert.Single(system.Bodies).ParentId);
    }

    [Fact]
    public void AMappingSetsMappedByCommander()
    {
        var (index, _, _) = Index(
        [
            Login("10T09:00:00"),
            Scan("10T09:01:00", 1, "Ocrinus 1"),
            Line("10T09:02:00", "\"event\":\"SAAScanComplete\",\"BodyName\":\"Ocrinus 1\",\"BodyID\":1,\"SystemAddress\":" + System + ",\"ProbesUsed\":4"),
        ]);

        Assert.True(Read(index)!.Own[1].MappedByCommander);
    }

    [Fact]
    public void ASurfaceScanSetsItsSignalsAndGenera()
    {
        var (index, _, _) = Index([Login("10T09:00:00"), Scan("10T09:01:00", 1, "Ocrinus 1"), SurfaceSignals("10T09:02:00", 1)]);

        var system = Read(index)!;
        var body = Assert.Single(system.Bodies);

        Assert.True(system.Own[1].SignalsFromSurfaceScan);
        Assert.Equal(["Bacterium", "Frutexa"], body.Genera);
    }

    [Fact]
    public void ALaterFullSpectrumCountDoesNotReplaceASurfaceScan()
    {
        var (index, _, _) = Index(
        [
            Login("10T09:00:00"),
            SurfaceSignals("10T09:00:30", 1),
            Scan("10T09:01:00", 1, "Ocrinus 1"),
            BodySignals("10T09:05:00", 1, "$SAA_SignalType_Geological;", 7),
        ]);

        var system = Read(index)!;

        Assert.True(system.Own[1].SignalsFromSurfaceScan);
        Assert.Equal(new Dictionary<string, int> { ["$SAA_SignalType_Biological;"] = 2 }, Assert.Single(system.Bodies).Signals);
    }

    [Fact]
    public void AnotherCommandersScansInTheSameFileAreNotRead()
    {
        var (index, _, _) = Index(
        [
            Login("10T09:00:00"),
            Scan("10T09:01:00", 1, "Ocrinus 1"),
            Login("10T10:00:00", "F2"),
            Scan("10T10:01:00", 2, "Ocrinus 2"),
        ]);

        Assert.Equal(["Ocrinus 1"], Read(index)!.Bodies.Select(body => body.Name));
    }

    [Fact]
    public void ScansInsideAnOperationAreNotRead()
    {
        var (index, _, _) = Index(
        [
            Login("10T09:00:00"),
            Line("10T09:00:10", "\"event\":\"GameModeChange\",\"GameMode\":\"MainGame\""),
            Scan("10T09:01:00", 1, "Ocrinus 1"),
            Line("10T09:02:00", "\"event\":\"GameModeChange\",\"GameMode\":\"Operation\""),
            Scan("10T09:03:00", 2, "Ocrinus 2"),
            Line("10T09:04:00", "\"event\":\"GameModeChange\",\"GameMode\":\"MainGame\""),
        ]);

        Assert.Equal(["Ocrinus 1"], Read(index)!.Bodies.Select(body => body.Name));
    }

    [Fact]
    public void AnEventInAFileAndInTheLiveListReadsAsOne()
    {
        string[] lines = [Login("10T09:00:00"), BodySignals("10T09:00:30", 1, "$SAA_SignalType_Geological;", 3), Scan("10T09:01:00", 1, "Ocrinus 1")];
        var (index, _, _) = Index(lines);

        index.Apply([.. lines.Select(Event)]);

        var system = Read(index)!;

        Assert.Equal(
            (1, 3),
            (system.Bodies.Count, Assert.Single(system.Bodies).Signals["$SAA_SignalType_Geological;"]));
    }

    [Fact]
    public void TheVersionChangesOnALiveEventForTheSystemOnly()
    {
        var index = new OwnScanIndex(new MemoryFileSystem(), NullLogger.Instance);
        index.Apply([Event(Login("10T09:00:00"))]);

        var before = (index.Version("F1", System), index.Version("F1", Elsewhere));

        index.Apply([Event(Scan("10T09:01:00", 1, "Ocrinus 1"))]);

        Assert.Equal(
            (true, false),
            (index.Version("F1", System) != before.Item1, index.Version("F1", Elsewhere) != before.Item2));
    }

    [Fact]
    public void AnEraseDropsTheCommandersScans()
    {
        var (index, _, _) = Index(
        [
            Login("10T09:00:00"),
            Scan("10T09:01:00", 1, "Ocrinus 1"),
            Line("10T09:02:00", "\"event\":\"NewCommander\",\"FID\":\"F1\",\"Name\":\"Jameson\""),
            Login("10T09:03:00"),
            Scan("10T09:04:00", 2, "Ocrinus 2"),
        ]);

        Assert.Equal(["Ocrinus 2"], Read(index)!.Bodies.Select(body => body.Name));
    }

    [Fact]
    public void ALaterBasicScanDoesNotReplaceADetailedOne()
    {
        var (index, _, _) = Index(
        [
            Login("10T09:00:00"),
            Scan("10T09:01:00", 1, "Ocrinus 1", extra: ",\"Materials\":[{\"Name\":\"iron\",\"Percent\":20.0}]"),
            Scan("10T09:05:00", 1, "Ocrinus 1").Replace("\"Detailed\"", "\"Basic\"", StringComparison.Ordinal),
        ]);

        Assert.Equal(["Iron"], Assert.Single(Read(index)!.Bodies).Materials.Keys);
    }

    [Fact]
    public void TheVersionChangesWhenTheCommanderIsErased()
    {
        var index = new OwnScanIndex(new MemoryFileSystem(), NullLogger.Instance);
        index.Apply([Event(Login("10T09:00:00")), Event(Scan("10T09:01:00", 1, "Ocrinus 1"))]);

        var before = index.Version("F1", System);

        index.Apply([Event(Line("10T09:02:00", "\"event\":\"NewCommander\",\"FID\":\"F1\",\"Name\":\"Jameson\""))]);

        Assert.NotEqual(before, index.Version("F1", System));
    }

    [Fact]
    public void ASystemWithNoEventsReadsAsNull()
    {
        var (index, _, _) = Index([Login("10T09:00:00"), Scan("10T09:01:00", 1, "Ocrinus 1", system: Elsewhere)]);

        Assert.Null(Read(index));
    }
}
