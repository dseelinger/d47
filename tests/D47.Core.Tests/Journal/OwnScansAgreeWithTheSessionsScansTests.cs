using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// <see cref="OwnScanIndex"/> reads a system from the real journal lines in
/// <c>tests/fixtures/exobiology/sampled-bodies.log</c> with the bodies <see cref="BodyScans"/> folds from them.
/// </summary>
[Trait("Category", "Integration")]
public sealed class OwnScansAgreeWithTheSessionsScansTests
{
    private const long Iowhail = 25552008267724;

    [Fact]
    public void ASystemFromRealJournalsReadsBackWithTheSessionsBodies()
    {
        var lines = EmbeddedFixture.Lines("exobiology.sampled-bodies.log");

        var session = BodyScans.Empty;

        foreach (var line in lines)
        {
            if (JournalEvent.TryParse(line, NullLogger.Instance, out var parsed) && parsed is not null)
            {
                session = session.Apply(parsed);
            }
        }

        var files = new MemoryFileSystem();
        files.WriteLines(
            "j/Journal.2025-07-14T000000.01.log",
            ["{\"timestamp\":\"2025-07-14T00:00:00Z\",\"event\":\"LoadGame\",\"FID\":\"F1\",\"Commander\":\"Jameson\"}", .. lines]);

        var index = new OwnScanIndex(files, NullLogger.Instance);
        index.FoldHistory(["j/Journal.2025-07-14T000000.01.log"], TestContext.Current.CancellationToken);

        var system = index.Read("F1", Iowhail, TestContext.Current.CancellationToken);
        var expected = session.InSystem(Iowhail).OrderBy(scan => scan.BodyId).ToList();

        Assert.NotNull(system);
        Assert.NotEmpty(expected);
        Assert.Equal(
            expected.Select(scan => (scan.BodyId!.Value, scan.BodyName, scan.Radius / 1_000, scan.SurfaceTemperature, scan.Landable)),
            system.Bodies.Select(body => (body.BodyId, body.Name, body.Radius, body.SurfaceTemperature, body.IsLandable)));
        Assert.Equal(
            expected.Select(scan => (scan.WasDiscovered, scan.WasMapped)),
            system.Bodies.Select(body => (system.Own[body.BodyId].WasDiscovered, system.Own[body.BodyId].WasMapped)));
        Assert.Equal(
            expected.Select(scan => string.Join(",", scan.Parents)),
            system.Bodies.Select(body => string.Join(",", body.Parents)));
        Assert.All(system.Bodies, body => Assert.True(system.Own[body.BodyId].SignalsFromSurfaceScan, body.Name));
    }
}
