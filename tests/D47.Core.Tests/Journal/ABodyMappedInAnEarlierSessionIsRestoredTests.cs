using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

[Trait("Category", "Integration")]
public sealed class ABodyMappedInAnEarlierSessionIsRestoredTests : IDisposable
{
    private const string Fid = "F9001";

    private const long Mapped = 3333;

    private const long Elsewhere = 4444;

    private const int BodyId = 8;

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-biology-backfill", Guid.NewGuid().ToString("N"));

    public ABodyMappedInAnEarlierSessionIsRestoredTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static string Line(string day, string time, string kind, string fields) =>
        "{ \"timestamp\":\"2025-07-" + day + "T" + time + "Z\", \"event\":\"" + kind + "\", " + fields + " }";

    private static string Login(string day) => Line(day, "09:00:00", "LoadGame", $"\"FID\":\"{Fid}\", \"Commander\":\"Jameson\"");

    private static readonly string[] FirstSession =
    [
        Login("10"),
        Line("10", "09:10:00", "FSDJump", $"\"StarSystem\":\"Hyades Sector AB-C d3\", \"SystemAddress\":{Mapped}"),
        Line("10", "09:11:00", "Scan", $"\"ScanType\":\"Detailed\", \"BodyName\":\"Hyades Sector AB-C d3 8\", \"BodyID\":{BodyId}, \"SystemAddress\":{Mapped}, "
            + "\"PlanetClass\":\"Icy body\", \"Atmosphere\":\"thin neon atmosphere\", \"Volcanism\":\"\", "
            + "\"SurfaceGravity\":1.25, \"SurfaceTemperature\":65.0, \"SurfacePressure\":150.0"),
        Line("10", "09:20:00", "SAASignalsFound", $"\"BodyName\":\"Hyades Sector AB-C d3 8\", \"BodyID\":{BodyId}, \"SystemAddress\":{Mapped}, "
            + "\"Signals\":[{\"Type\":\"$SAA_SignalType_Biological;\", \"Type_Localised\":\"Biological\", \"Count\":1}], "
            + "\"Genuses\":[{\"Genus\":\"$Codex_Ent_Fonticulus_Genus_Name;\", \"Genus_Localised\":\"Fonticulua\"}]"),
    ];

    private static readonly string[] SecondSession =
    [
        Login("11"),
        Line("11", "09:00:05", "Location", $"\"StarSystem\":\"Hyades Sector AB-C d4\", \"SystemAddress\":{Elsewhere}"),
    ];

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    [Fact]
    public void ABodyMappedInTheFirstJournalIsRestoredWithItsGeneraAndConditions()
    {
        var first = Path.Combine(_folder, "Journal.2025-07-10T090000.01.log");
        var second = Path.Combine(_folder, "Journal.2025-07-11T090000.01.log");
        File.WriteAllLines(first, FirstSession);
        File.WriteAllLines(second, SecondSession);

        var walked = BiologyBodiesBackfill.FromHistory(new DiskFileSystem(), [first, second], NullLogger.Instance, TestContext.Current.CancellationToken)[Fid];
        var store = new GameStateStore { RestoreBiology = _ => walked };

        foreach (var line in SecondSession)
        {
            store.Apply(Event(line));
        }

        var body = store.Active!.Biology.For(Mapped, BodyId)!;

        Assert.True(body.Mapped);
        Assert.Equal("Hyades Sector AB-C d3 8", body.BodyName);
        Assert.Equal(["Fonticulua"], body.Genera);
        Assert.Equal("Icy body", body.PlanetClass);
        Assert.Equal("thin neon atmosphere", body.Atmosphere);
        Assert.Equal(1.25, body.SurfaceGravity);
        Assert.Equal(65.0, body.SurfaceTemperature);
        Assert.Equal(150.0, body.SurfacePressure);
        Assert.True(store.Active!.Biology.IsWhole);
        Assert.Empty(store.Active!.Bodies.All);
    }
}
