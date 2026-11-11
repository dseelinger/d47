using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class BiologyBodiesAreKeptAcrossSessionsTests
{
    private const long Here = 1111;

    private const long There = 2222;

    private const int BodyId = 5;

    private static string Line(string time, string kind, string fields = "") =>
        "{ \"timestamp\":\"2025-07-10T" + time + "Z\", \"event\":\"" + kind + "\"" + (fields.Length > 0 ? ", " + fields : "") + " }";

    private static string Jump(string time, long system) =>
        Line(time, "FSDJump", $"\"StarSystem\":\"Hyades Sector AB-C d{system}\", \"SystemAddress\":{system}");

    private static string Scan(string time, long system = Here, int bodyId = BodyId) =>
        Line(time, "Scan", $"\"ScanType\":\"Detailed\", \"BodyName\":\"Hyades Sector AB-C d{system} {bodyId}\", \"BodyID\":{bodyId}, \"SystemAddress\":{system}, "
            + "\"PlanetClass\":\"Rocky body\", \"Atmosphere\":\"thin carbon dioxide atmosphere\", \"Volcanism\":\"\", "
            + "\"SurfaceGravity\":2.5, \"SurfaceTemperature\":180.5, \"SurfacePressure\":2000.0");

    private static string Fss(string time, int biological, long system = Here, int bodyId = BodyId) =>
        Line(time, "FSSBodySignals", $"\"BodyName\":\"Hyades Sector AB-C d{system} {bodyId}\", \"BodyID\":{bodyId}, \"SystemAddress\":{system}, "
            + $"\"Signals\":[{{\"Type\":\"$SAA_SignalType_Biological;\", \"Type_Localised\":\"Biological\", \"Count\":{biological}}}]");

    private static string Mapped(string time, long system = Here, int bodyId = BodyId) =>
        Line(time, "SAASignalsFound", $"\"BodyName\":\"Hyades Sector AB-C d{system} {bodyId}\", \"BodyID\":{bodyId}, \"SystemAddress\":{system}, "
            + "\"Signals\":[{\"Type\":\"$SAA_SignalType_Biological;\", \"Type_Localised\":\"Biological\", \"Count\":2}], "
            + "\"Genuses\":[{\"Genus\":\"$Codex_Ent_Bacterial_Genus_Name;\", \"Genus_Localised\":\"Bacterium\"}, "
            + "{\"Genus\":\"$Codex_Ent_Stratum_Genus_Name;\", \"Genus_Localised\":\"Stratum\"}]");

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static BiologyBodies Fold(params string[] lines) =>
        lines.Aggregate(BiologyBodies.None, (bodies, line) => bodies.Apply(Event(line)));

    [Fact]
    public void AScanThenASurfaceScanGivesAMappedBodyWithItsConditionsAndGenera()
    {
        var body = Fold(Jump("09:00:00", Here), Scan("09:01:00"), Mapped("09:05:00")).For(Here, BodyId)!;

        Assert.True(body.Mapped);
        Assert.Equal(2, body.BiologicalCount);
        Assert.Equal(["Bacterium", "Stratum"], body.Genera);
        Assert.Equal("Rocky body", body.PlanetClass);
        Assert.Equal("thin carbon dioxide atmosphere", body.Atmosphere);
        Assert.Equal(2.5, body.SurfaceGravity);
        Assert.Equal(180.5, body.SurfaceTemperature);
        Assert.Equal(2000.0, body.SurfacePressure);
        Assert.Equal(DateTimeOffset.Parse("2025-07-10T09:05:00Z"), body.SeenAt);
    }

    [Fact]
    public void AnFssCountAloneGivesAnUnmappedBodyWithItsCount()
    {
        var body = Assert.Single(Fold(Jump("09:00:00", Here), Fss("09:01:00", 3)).InSystem(Here));

        Assert.False(body.Mapped);
        Assert.Equal(3, body.BiologicalCount);
        Assert.Empty(body.Genera);
        Assert.Null(body.PlanetClass);
    }

    [Fact]
    public void ALaterFssCountDoesNotReplaceASurfaceScan()
    {
        var body = Fold(Jump("09:00:00", Here), Scan("09:01:00"), Mapped("09:05:00"), Fss("09:10:00", 4)).For(Here, BodyId)!;

        Assert.True(body.Mapped);
        Assert.Equal(2, body.BiologicalCount);
        Assert.Equal(2, body.Genera.Count);
    }

    [Fact]
    public void ABodyWithNoBiologyIsNotKept()
    {
        var bodies = Fold(Jump("09:00:00", Here), Scan("09:01:00"), Fss("09:02:00", 0));

        Assert.Null(bodies.For(Here, BodyId));
        Assert.Empty(bodies.All);
    }

    [Fact]
    public void ConditionsScannedInOneSystemAreNotCopiedOntoABodyInAnother()
    {
        var bodies = Fold(Jump("09:00:00", Here), Scan("09:01:00"), Jump("09:10:00", There), Fss("09:11:00", 2, There));

        var body = bodies.For(There, BodyId)!;

        Assert.False(body.HasConditions);
    }

    [Fact]
    public void ConditionsAreDroppedWhenTheSystemChanges()
    {
        var bodies = Fold(Jump("09:00:00", Here), Scan("09:01:00"), Jump("09:10:00", There), Jump("09:20:00", Here), Fss("09:21:00", 2));

        Assert.False(bodies.For(Here, BodyId)!.HasConditions);
    }

    [Fact]
    public void AScanAfterTheSignalsFillsInTheConditions()
    {
        var body = Fold(Jump("09:00:00", Here), Fss("09:01:00", 2), Scan("09:02:00")).For(Here, BodyId)!;

        Assert.Equal("Rocky body", body.PlanetClass);
        Assert.Equal(2, body.BiologicalCount);
    }

    [Fact]
    public void AMergeKeepsTheLaterRowAndNeverLetsAnFssRowReplaceASurfaceScan()
    {
        var earlier = Fold(Jump("09:00:00", Here), Fss("09:01:00", 2));
        var later = Fold(Jump("10:00:00", Here), Fss("10:01:00", 5));
        var mapped = Fold(Jump("08:00:00", Here), Scan("08:01:00"), Mapped("08:05:00"));
        var fssAfterMapping = Fold(Jump("11:00:00", Here), Fss("11:01:00", 7));

        Assert.Equal(5, earlier.With(later).For(Here, BodyId)!.BiologicalCount);
        Assert.Equal(5, later.With(earlier).For(Here, BodyId)!.BiologicalCount);

        var kept = mapped.With(fssAfterMapping).For(Here, BodyId)!;

        Assert.True(kept.Mapped);
        Assert.Equal(2, kept.BiologicalCount);
        Assert.True(fssAfterMapping.With(mapped).For(Here, BodyId)!.Mapped);
        Assert.True(mapped.With(fssAfterMapping).IsWhole);
    }

    [Fact]
    public void AMergeKeepsConditionsTheWinningRowLacks()
    {
        var scanned = Fold(Jump("09:00:00", Here), Scan("09:01:00"), Fss("09:02:00", 2));
        var mappedWithoutScan = Fold(Mapped("10:00:00"));

        var body = scanned.With(mappedWithoutScan).For(Here, BodyId)!;

        Assert.True(body.Mapped);
        Assert.Equal("Rocky body", body.PlanetClass);
    }

    [Fact]
    public void ANewCommanderClearsTheBodies()
    {
        var store = new GameStateStore();
        store.Apply(Event(Line("08:59:00", "LoadGame", "\"FID\":\"F1\", \"Commander\":\"Jameson\"")));
        store.Apply(Event(Jump("09:00:00", Here)));
        store.Apply(Event(Fss("09:01:00", 2)));

        Assert.NotEmpty(store.Active!.Biology.All);

        store.Apply(Event(Line("09:30:00", "NewCommander", "\"FID\":\"F1\", \"Name\":\"Jameson\", \"Package\":\"Default\"")));

        Assert.Empty(store.Active!.Biology.All);
    }

    [Fact]
    public void AJumpOrAScanIsNoChangeToTheBiologyBodies()
    {
        var store = new GameStateStore();
        store.Apply(Event(Line("08:59:00", "LoadGame", "\"FID\":\"F1\", \"Commander\":\"Jameson\"")));

        var jumped = store.Apply(Event(Jump("09:00:00", Here)));
        var scanned = store.Apply(Event(Scan("09:01:00")));
        var signalled = store.Apply(Event(Fss("09:02:00", 2)));

        Assert.DoesNotContain(jumped.Changes, change => change.Part == nameof(CommanderGameState.Biology));
        Assert.DoesNotContain(scanned.Changes, change => change.Part == nameof(CommanderGameState.Biology));
        Assert.Contains(signalled.Changes, change => change.Part == nameof(CommanderGameState.Biology));
    }
}
