using System.Text.Json;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Web;
using Xunit;

namespace D47.Core.Tests.Web;

/// <summary>
/// A section with nothing behind it is left out rather than written as zeros, the HUD matrix travels only
/// with the HUD colours theme, and the document holds copies.
/// </summary>
public sealed class ASnapshotSaysOnlyWhatIsKnownTests
{
    [Fact]
    public void ACommanderWithNothingKnownHasNoSections()
    {
        var state = new CommanderGameState(new CommanderIdentity("F9900500", "Cass Tenby"));

        var snapshot = WebSnapshotBuilder.Build(state, new WebSnapshotStores(), "elite", null, TimeZoneInfo.Utc, SnapshotBench.Now);
        var json = JsonSerializer.Serialize(snapshot, WebSnapshot.Json);

        Assert.Equal(
            """{"format":1,"frontierId":"F9900500","name":"Cass Tenby","takenAt":"2026-11-12T10:00:00+00:00","theme":"elite"}""",
            json);
    }

    [Fact]
    public void EmptyStoresGiveNoWatchesNoPlansAndNoLists()
    {
        var snapshot = new SnapshotBench().Build();

        Assert.Null(snapshot.Watches);
        Assert.Null(snapshot.RoutePlans);
        Assert.Null(snapshot.Checklist!.Lists);
        Assert.NotNull(snapshot.Checklist.Goals);
    }

    [Fact]
    public void TheHudMatrixTravelsOnlyWithTheHudColoursTheme()
    {
        var bench = new SnapshotBench();
        var hud = new GuiColourMatrix(0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9);

        Assert.Equal([0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9], bench.Build(ThemeCatalog.ElitePaletteId, hud).HudMatrix!);
        Assert.Equal(ThemeCatalog.ElitePaletteId, bench.Build(ThemeCatalog.ElitePaletteId, hud).Theme);
        Assert.Null(bench.Build("elite", hud).HudMatrix);
    }

    [Fact]
    public void LaterEventsDoNotChangeABuiltSnapshot()
    {
        var bench = new SnapshotBench();
        var snapshot = bench.Build();
        var before = JsonSerializer.Serialize(snapshot, WebSnapshot.Json);

        bench.State.Apply(SnapshotBench.Event(
            """{ "timestamp":"2026-11-12T09:10:00Z", "event":"MaterialCollected", "Category":"Raw", "Name":"carbon", "Count":3 }"""));
        bench.State.Apply(SnapshotBench.Event(
            """{ "timestamp":"2026-11-12T09:11:00Z", "event":"MissionAbandoned", "Name":"Mission_Sightseeing_name", "MissionID":990002 }"""));
        bench.State.Apply(SnapshotBench.Event(
            """{ "timestamp":"2026-11-12T09:12:00Z", "event":"Undocked", "StationName":"Jameson Memorial", "StationType":"Orbis", "MarketID":128666762 }"""));

        Assert.NotEqual(before, JsonSerializer.Serialize(bench.Build(), WebSnapshot.Json));
        Assert.Equal(before, JsonSerializer.Serialize(snapshot, WebSnapshot.Json));
    }
}
