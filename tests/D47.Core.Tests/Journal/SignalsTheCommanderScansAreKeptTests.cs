using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class SignalsTheCommanderScansAreKeptTests
{
    private static string Jump(string time, long address, string system, double x) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"FSDJump\",\"StarSystem\":\"" + system
        + "\",\"SystemAddress\":" + address + ",\"StarPos\":[" + x + ",2,3]}";

    private static string Signal(string time, long address, string type, string name, string? localised = null) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"FSSSignalDiscovered\",\"SystemAddress\":" + address
        + ",\"SignalName\":\"" + name + "\""
        + (localised is null ? string.Empty : ",\"SignalName_Localised\":\"" + localised + "\"")
        + ",\"SignalType\":\"" + type + "\"}";

    private static SignalSightings Sightings() => new(new MemoryFileSystem(), NullLogger.Instance);

    private static SignalSightings Folded(params string[] lines)
    {
        var sightings = Sightings();
        sightings.Fold(lines);
        return sightings;
    }

    [Theory]
    [InlineData("NavBeacon", "$MULTIPLAYER_SCENARIO42_TITLE;", "Nav beacon")]
    [InlineData("NavBeacon", "Nav Beacon", "Nav beacon")]
    [InlineData("NavBeacon", "$MULTIPLAYER_SCENARIO80_TITLE;", "Compromised nav beacon")]
    [InlineData("ResourceExtraction", "$MULTIPLAYER_SCENARIO14_TITLE;", "Resource extraction site")]
    [InlineData("ResourceExtraction", "$MULTIPLAYER_SCENARIO77_TITLE;", "Low RES")]
    [InlineData("ResourceExtraction", "$MULTIPLAYER_SCENARIO78_TITLE;", "High RES")]
    [InlineData("ResourceExtraction", "$MULTIPLAYER_SCENARIO79_TITLE;", "Hazardous RES")]
    [InlineData("Generic", "$EXT_PANEL_ColonisationBeacon_Deployed;", "Colonisation beacon")]
    [InlineData("TouristBeacon", "Sightseeing Stop", "Tourist beacon")]
    [InlineData("Installation", "Mining Platform", "Installation")]
    [InlineData("Megaship", "Kolo Gryphon", "Megaship")]
    [InlineData("Titan", "Typhon", "Titan")]
    [InlineData("Generic", "$ListeningPost_Spy;", "Listening post")]
    [InlineData("Generic", "$NumberStation_One;", "Unregistered comms beacon")]
    [InlineData("Codex", "$Fixed_Event_Life_Cloud;", "Notable stellar phenomena")]
    public void ACodeGivesItsKind(string type, string name, string kind) =>
        Assert.Equal(kind, SignalKind.Classify(type, name)?.Name);

    [Fact]
    public void AnyResMatchesTheFourResKinds() =>
        Assert.Equal(
            ["Resource extraction site", "Low RES", "High RES", "Hazardous RES"],
            SignalKind.AnyRes.Select(kind => kind.Name));

    [Fact]
    public void ACheckpointAndACarrierAreDropped()
    {
        Assert.True(SignalKind.Drops("Generic", "$FIXED_EVENT_CHECKPOINT;"));
        Assert.True(SignalKind.Drops("FleetCarrier", "ABC-123"));

        var sightings = Folded(
            Jump("2026-10-01T10:00:00Z", 1, "Alpha", 1),
            Signal("2026-10-01T10:01:00Z", 1, "Generic", "$FIXED_EVENT_CHECKPOINT;"),
            Signal("2026-10-01T10:02:00Z", 1, "FleetCarrier", "ABC-123"));

        Assert.Empty(sightings.Snapshot());
    }

    [Fact]
    public void AnUnknownCodeIsDroppedAndNeverShown()
    {
        var sightings = Folded(
            Jump("2026-10-01T10:00:00Z", 1, "Alpha", 1),
            Signal("2026-10-01T10:01:00Z", 1, "Generic", "$Warzone_Low;", "Warzone"));

        Assert.Empty(sightings.Snapshot());
    }

    [Fact]
    public void ACodeIsShownAsItsKindsNameAndAPlainNameAsItsOwn()
    {
        var sightings = Folded(
            Jump("2026-10-01T10:00:00Z", 1, "Alpha", 1),
            Signal("2026-10-01T10:01:00Z", 1, "ResourceExtraction", "$MULTIPLAYER_SCENARIO78_TITLE;", "High Yield RES"),
            Signal("2026-10-01T10:02:00Z", 1, "Installation", "mining", "Mining Platform"));

        Assert.Equal(
            ["High RES", "Mining Platform"],
            sightings.Snapshot().Select(sighting => sighting.SignalName).Order());
    }

    [Fact]
    public void EachSightingTakesItsSystemsPosition()
    {
        var sightings = Folded(
            Jump("2026-10-01T10:00:00Z", 1, "Alpha", 1),
            Signal("2026-10-01T10:01:00Z", 1, "NavBeacon", "Nav Beacon"),
            Signal("2026-10-01T10:02:00Z", 1, "Installation", "Mining Platform"),
            Jump("2026-10-01T10:10:00Z", 2, "Beta", 7),
            Signal("2026-10-01T10:11:00Z", 2, "TouristBeacon", "Sightseeing Stop"));

        var all = sightings.Snapshot();

        Assert.Equal(3, all.Count);
        Assert.All(all.Where(s => s.SystemName == "Alpha"), s => Assert.Equal(new StarPosition(1, 2, 3), s.Position));
        Assert.Equal(new StarPosition(7, 2, 3), Assert.Single(all, s => s.SystemName == "Beta").Position);
    }

    [Fact]
    public void ASignalInAnUnplacedSystemIsDropped() =>
        Assert.Empty(Folded(Signal("2026-10-01T10:01:00Z", 9, "NavBeacon", "Nav Beacon")).Snapshot());

    [Fact]
    public void ALiveSignalAfterTheBackfillIsKept()
    {
        var system = new MemoryFileSystem();
        system.WriteText("j/Journal.1.log", Jump("2026-10-01T10:00:00Z", 1, "Alpha", 1));
        var sightings = new SignalSightings(system, NullLogger.Instance);

        Assert.False(sightings.HistoryFolded);
        sightings.FoldHistory(["j/Journal.1.log"], TestContext.Current.CancellationToken);
        Assert.True(sightings.HistoryFolded);
        Assert.Empty(sightings.Snapshot());

        Assert.True(JournalEvent.TryParse(
            Signal("2026-10-02T09:00:00Z", 1, "NavBeacon", "Nav Beacon"), NullLogger.Instance, out var live));
        sightings.Apply([live!]);

        Assert.Equal("Alpha", Assert.Single(sightings.Snapshot()).SystemName);
    }

    [Fact]
    public void AMegashipSeenInTwoSystemsKeepsOnlyTheLaterOne()
    {
        var sightings = Folded(
            Jump("2026-10-01T10:00:00Z", 1, "Alpha", 1),
            Signal("2026-10-01T10:01:00Z", 1, "Megaship", "Kolo Gryphon"),
            Jump("2026-10-05T10:00:00Z", 2, "Beta", 7),
            Signal("2026-10-05T10:01:00Z", 2, "Megaship", "Kolo Gryphon"));

        Assert.Equal("Beta", Assert.Single(sightings.Snapshot()).SystemName);
    }
}
