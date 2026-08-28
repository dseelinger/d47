using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// The limpet reminder (Phase 41, asked for 2026-08-21).
/// <para>
/// Every condition here was measured against the Commander's own 148 limpet purchases rather than
/// guessed — see <see cref="LimpetCallout"/> for the numbers and for the three-in-136 carrier miss
/// they ruled to accept.
/// </para>
/// </summary>
public class LimpetCalloutTests
{
    private static JournalEvent Event(string kind, params (string Key, object? Value)[] fields)
    {
        var payload = new Dictionary<string, object?>
        {
            ["timestamp"] = "2026-08-21T17:00:00Z",
            ["event"] = kind,
        };

        foreach (var (key, value) in fields)
        {
            payload[key] = value;
        }

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(payload), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    /// <summary>A docking, with or without the service that gates Advanced Maintenance.</summary>
    private static JournalEvent Docked(bool sellsLimpets = true) =>
        Event(
            "Docked",
            ("StationName", "Reiter City"),
            ("StarSystem", "Oppi"),
            ("StationType", "Coriolis"),
            ("StationServices", sellsLimpets
                ? new[] { "dock", "refuel", "repair", "rearm", "commodities" }
                : new[] { "dock", "refuel", "commodities" }));

    /// <summary>
    /// A Commander in a ship of <paramref name="capacity"/> tonnes carrying
    /// <paramref name="limpets"/>.
    /// <para>
    /// <b>Built through <see cref="JournalSpine"/> with a real <c>Cargo.json</c> on disk</b>, the
    /// way <c>ColonisationCapabilityTests</c> does it — because that is how the hold actually
    /// arrives. The journal cannot supply it: not one of the 3,781 dockings in the corpus is
    /// followed within fifteen seconds by a <c>Cargo</c> event carrying an inventory.
    /// </para>
    /// </summary>
    private static CommanderGameState Commander(int capacity, int? limpets, string vessel = "Ship")
    {
        var install = new TempInstall();

        File.WriteAllLines(
            Path.Combine(install.Root, "Journal.2026-08-21T170000.01.log"),
            [
                """{"timestamp":"2026-08-21T17:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
                $$"""{"timestamp":"2026-08-21T17:00:01Z","event":"Loadout","Ship":"python","ShipID":7,"CargoCapacity":{{capacity}},"Modules":[]}""",
            ]);

        if (limpets is { } aboard)
        {
            var inventory = aboard == 0
                ? ""
                : $$"""{ "Name":"drones", "Name_Localised":"Limpet", "Count":{{aboard}}, "Stolen":0 }""";

            File.WriteAllText(
                Path.Combine(install.Root, CargoManifestReader.ManifestFile),
                $$"""{ "timestamp":"2026-08-21T17:00:11Z", "event":"Cargo", "Vessel":"{{vessel}}", "Count":{{aboard}}, "Inventory":[ {{inventory}} ] }""");
        }

        var store = new GameStateStore();
        new JournalSpine(install.Root, store, NullLoggerFactory.Instance).Poll();

        return store.Active!;
    }

    private static CalloutContext Context(CommanderGameState state, bool priming = false) =>
        new(DateTimeOffset.UnixEpoch, priming, state, GameStatus.Unknown, NavRoute.None, [Docked()]);

    private static LimpetCallout Callout() => new() { Floor = () => 64, Percent = () => 5 };

    [Fact]
    public void ABigHoldWithNoLimpetsIsReminded()
    {
        var said = Assert.Single(Callout().Examine(Context(Commander(256, 0))));

        Assert.Equal(LimpetCallout.Key, said.Key);
        Assert.Contains("No limpets aboard", said.Text, StringComparison.Ordinal);
        Assert.Contains("this station sells them", said.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Percent of cargo capacity</b> — the Commander's ruling, 2026-08-21, and the one thing a
    /// test has to name rather than merely exercise. At 5%, a 256 tonne hold is low under 12.8
    /// limpets and not low at 13.
    /// </summary>
    [Theory]
    [InlineData(12, true)]
    [InlineData(13, false)]
    public void TheThresholdIsAPercentageOfCargoCapacity(int limpets, bool reminded)
    {
        var said = Callout().Examine(Context(Commander(256, limpets))).ToList();

        Assert.Equal(reminded, said.Count == 1);
    }

    /// <summary>And the same count is fine in a small hold and low in a large one.</summary>
    [Fact]
    public void TheSameCountReadsDifferentlyAgainstADifferentHold()
    {
        Assert.Empty(Callout().Examine(Context(Commander(128, 8))));
        Assert.Single(Callout().Examine(Context(Commander(512, 8))));
    }

    [Fact]
    public void ASmallHoldIsNeverReminded()
    {
        // 64 is the floor and the comparison is strictly greater, so 64 itself says nothing.
        Assert.Empty(Callout().Examine(Context(Commander(64, 0))));
        Assert.Empty(Callout().Examine(Context(Commander(16, 0))));
    }

    /// <summary>
    /// <b>The `rearm` service is the signal.</b> Measured: present at 133 of the 136 dockings in
    /// the corpus where limpets were actually bought, and the three misses are one fleet carrier.
    /// </summary>
    [Fact]
    public void AStationWithoutAdvancedMaintenanceSaysNothing()
    {
        var state = Commander(256, 0);

        Assert.Empty(Callout().Examine(new CalloutContext(
            DateTimeOffset.UnixEpoch, false, state, GameStatus.Unknown, NavRoute.None, [Docked(sellsLimpets: false)])));
    }

    /// <summary>
    /// <b>An unread hold is not an empty one.</b> A Commander whose <c>Cargo.json</c> has never
    /// been written is not a Commander with no limpets, and shopping advice on that basis would be
    /// inventing the reading.
    /// </summary>
    [Fact]
    public void AHoldThatHasNeverBeenReadSaysNothing()
    {
        Assert.Empty(Callout().Examine(Context(Commander(256, limpets: null))));
    }

    /// <summary>An SRV's hold is not the ship's, and the file says which it is describing.</summary>
    [Fact]
    public void AnSrvHoldIsNotTheShipsHold()
    {
        Assert.Empty(Callout().Examine(Context(Commander(256, 0, vessel: "SRV"))));
    }

    /// <summary>
    /// Priming replays the session's dockings, and a reminder for a station left an hour ago is
    /// one the Commander cannot act on.
    /// </summary>
    [Fact]
    public void PrimingSaysNothing()
    {
        Assert.Empty(Callout().Examine(Context(Commander(256, 0), priming: true)));
    }

    /// <summary>
    /// <b>No price is quoted.</b> Reported as always the same and it very nearly is — but across
    /// 148 purchases it is 101, 95, 82, 201, 97 and 91, and d47 has no reading of this station's
    /// price at the pad anyway.
    /// </summary>
    [Fact]
    public void TheReminderQuotesNoPrice()
    {
        var said = Assert.Single(Callout().Examine(Context(Commander(256, 0)))).Text;

        Assert.DoesNotContain("credit", said, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("101", said, StringComparison.Ordinal);
    }

    /// <summary>The thresholds are read through, so moving a slider takes effect at once.</summary>
    [Fact]
    public void TheThresholdsAreReadEveryTimeRatherThanCaptured()
    {
        var percent = 5;
        var callout = new LimpetCallout { Floor = () => 64, Percent = () => percent };

        Assert.Empty(callout.Examine(Context(Commander(256, 20))));

        percent = 50;

        Assert.Single(callout.Examine(Context(Commander(256, 20))));
    }
}
