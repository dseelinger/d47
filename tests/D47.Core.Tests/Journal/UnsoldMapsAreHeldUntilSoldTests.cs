using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>The unsold mapped bodies: priced on the DSS scan, cut by a sale, cleared by a death or a reset (#527).</summary>
public class UnsoldMapsAreHeldUntilSoldTests
{
    private const long SystemAddress = 3274702866819;

    private const string System = "Praea Euq BF-A d95";

    private const string Commander =
        """{"timestamp":"2026-09-26T19:00:00Z","event":"LoadGame","FID":"F1","Commander":"Fixture"}""";

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Scan(string at, int body, string scanType, bool wasMapped = true, bool wasDiscovered = true) =>
        $$"""
          {"timestamp":"{{at}}","event":"Scan","ScanType":"{{scanType}}","BodyName":"{{System}} {{body}}",
           "BodyID":{{body}},"StarSystem":"{{System}}","SystemAddress":{{SystemAddress}},
           "PlanetClass":"High metal content body","TerraformState":"","MassEM":1.738816,
           "WasDiscovered":{{(wasDiscovered ? "true" : "false")}},"WasMapped":{{(wasMapped ? "true" : "false")}}}
          """;

    private static string Mapped(string at, int body, int probes = 5, int target = 7) =>
        $$"""
          {"timestamp":"{{at}}","event":"SAAScanComplete","BodyName":"{{System}} {{body}}",
           "SystemAddress":{{SystemAddress}},"BodyID":{{body}},"ProbesUsed":{{probes}},"EfficiencyTarget":{{target}}}
          """;

    private static string Sale(string at, params string[] systems) =>
        $$"""
          {"timestamp":"{{at}}","event":"MultiSellExplorationData","Discovered":[{{string.Join(",",
              systems.Select(system => $$"""{"SystemName":"{{system}}","NumBodies":1}"""))}}],
           "BaseValue":1,"Bonus":0,"TotalEarnings":1}
          """;

    private static string Died(string at) => $$"""{"timestamp":"{{at}}","event":"Died"}""";

    private static CartographyLedger Ledger(params string[] lines)
    {
        var ledger = new CartographyLedger(null, new MemoryFileSystem(), NullLogger.Instance);
        ledger.FoldHistory([], TestContext.Current.CancellationToken);
        ledger.Apply([.. new[] { Commander }.Concat(lines).Select(Parse)]);
        return ledger;
    }

    /// <summary>A high metal content body of 1.738816 Earth masses, already discovered and mapped, mapped efficiently.</summary>
    private static readonly long Efficient = CartographicValue.Planet(
        "High metal content body", "", 1.738816, wasDiscovered: true, wasMapped: true, mapped: true, efficient: true);

    // -------------------------------------------------------------- pricing

    [Fact]
    public void AMappedBodyIsPricedFromItsScan()
    {
        var ledger = Ledger(Scan("2026-09-26T20:00:00Z", 1, "AutoScan"), Mapped("2026-09-26T20:05:00Z", 1));

        var held = Assert.Single(ledger.Unsold("F1").Held);

        Assert.Equal(Efficient, held.Value);
        Assert.True(held.Efficient);
        Assert.Equal(System, held.System);
    }

    [Fact]
    public void MoreProbesThanTheTargetIsNotEfficientAndPaysLess()
    {
        var ledger = Ledger(Scan("2026-09-26T20:00:00Z", 1, "AutoScan"), Mapped("2026-09-26T20:05:00Z", 1, probes: 9));

        var held = Assert.Single(ledger.Unsold("F1").Held);

        Assert.False(held.Efficient);
        Assert.True(held.Value < Efficient);
    }

    /// <summary>Praea Euq FV-Y c4 8: AutoScan true, the Detailed scan after the DSS false, and the sale paid as mapped.</summary>
    [Fact]
    public void TheDetailedScanAfterMappingDoesNotMakeTheBodyFirstMapped()
    {
        var ledger = Ledger(
            Scan("2026-09-26T20:04:40Z", 1, "AutoScan", wasMapped: true),
            Mapped("2026-09-26T20:05:54Z", 1),
            Scan("2026-09-26T20:05:55Z", 1, "Detailed", wasMapped: false));

        Assert.Equal(Efficient, Assert.Single(ledger.Unsold("F1").Held).Value);
    }

    [Fact]
    public void ABodyNobodyHadMappedPaysTheFirstMapperMultiplier()
    {
        var ledger = Ledger(
            Scan("2026-09-26T20:04:40Z", 1, "AutoScan", wasMapped: false),
            Mapped("2026-09-26T20:05:54Z", 1));

        Assert.True(Assert.Single(ledger.Unsold("F1").Held).Value > Efficient * 2);
    }

    [Fact]
    public void WithNoScanTheMapIsHeldButLeftOutOfTheTotal()
    {
        var unsold = Ledger(Mapped("2026-09-26T20:05:54Z", 1)).Unsold("F1");

        Assert.Single(unsold.Unpriced);
        Assert.Equal(0, unsold.Total);
    }

    // ------------------------------------------------------ sale, death, reset

    [Fact]
    public void ASaleRemovesTheMapsInEverySystemItNames()
    {
        var ledger = Ledger(
            Scan("2026-09-26T20:00:00Z", 1, "AutoScan"),
            Mapped("2026-09-26T20:05:00Z", 1),
            Sale("2026-09-27T00:18:27Z", "Praea Euq FV-Y c4", System));

        Assert.Empty(ledger.Unsold("F1").Held);
    }

    [Fact]
    public void ASaleOfOtherSystemsKeepsTheMap()
    {
        var ledger = Ledger(
            Scan("2026-09-26T20:00:00Z", 1, "AutoScan"),
            Mapped("2026-09-26T20:05:00Z", 1),
            Sale("2026-09-27T00:18:27Z", "Praea Euq FV-Y c4"));

        Assert.Single(ledger.Unsold("F1").Held);
    }

    [Fact]
    public void AMapAfterTheSaleIsStillHeld()
    {
        var ledger = Ledger(
            Scan("2026-09-26T20:00:00Z", 1, "AutoScan"),
            Mapped("2026-09-26T20:05:00Z", 1),
            Sale("2026-09-27T00:18:27Z", System),
            Scan("2026-09-27T01:00:00Z", 2, "AutoScan"),
            Mapped("2026-09-27T01:05:00Z", 2));

        Assert.Equal($"{System} 2", Assert.Single(ledger.Unsold("F1").Held).BodyName);
    }

    [Fact]
    public void DeathLosesEveryMapHeld()
    {
        var ledger = Ledger(
            Scan("2026-09-26T20:00:00Z", 1, "AutoScan"),
            Mapped("2026-09-26T20:05:00Z", 1),
            Died("2026-09-26T23:00:00Z"));

        Assert.Empty(ledger.Unsold("F1").Held);
    }

    [Fact]
    public void AResetClearsTheTotalSurvivesARestartAndLeavesTheExobiologyResetAlone()
    {
        var files = new MemoryFileSystem();
        var path = Path.Combine(@"C:\d47-test", "unsold-data.json");
        var at = DateTimeOffset.Parse("2026-09-26T22:00:00Z", global::System.Globalization.CultureInfo.InvariantCulture);

        new ExobiologyLedger(path, files, NullLogger.Instance).Reset("F1", at);

        var ledger = new CartographyLedger(path, files, NullLogger.Instance);
        ledger.Apply([Parse(Commander), Parse(Scan("2026-09-26T20:00:00Z", 1, "AutoScan")), Parse(Mapped("2026-09-26T21:00:00Z", 1))]);
        ledger.Reset("F1", at);

        Assert.Empty(ledger.Unsold("F1").Held);

        var restarted = new CartographyLedger(path, files, NullLogger.Instance);
        restarted.Load();
        restarted.Apply([
            Parse(Commander),
            Parse(Scan("2026-09-26T20:00:00Z", 1, "AutoScan")),
            Parse(Mapped("2026-09-26T21:00:00Z", 1)),
            Parse(Scan("2026-09-26T22:30:00Z", 2, "AutoScan")),
            Parse(Mapped("2026-09-26T23:00:00Z", 2)),
        ]);

        Assert.Equal($"{System} 2", Assert.Single(restarted.Unsold("F1").Held).BodyName);

        var file = files.ReadText(path);
        Assert.Contains("ExobiologyResetAt", file, StringComparison.Ordinal);
        Assert.Contains("CartographyResetAt", file, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------- the rebuild

    [Fact]
    public void TheTotalIsRebuiltFromOlderJournalsWithoutCountingTheLiveOneTwice()
    {
        var files = new MemoryFileSystem();

        var older = Path.Combine(@"C:\d47-test", "Journal.2026-09-25T190000.01.log");
        var current = Path.Combine(@"C:\d47-test", "Journal.2026-09-26T190000.01.log");

        files.WriteText(older, string.Join("\n", [
            """{"timestamp":"2026-09-25T19:00:00Z","event":"LoadGame","FID":"F1","Commander":"Fixture"}""",
            Scan("2026-09-25T20:00:00Z", 1, "AutoScan").ReplaceLineEndings(" "),
            Mapped("2026-09-25T21:00:00Z", 1).ReplaceLineEndings(" "),
        ]) + "\n");

        files.WriteText(current, string.Join("\n", [
            Commander,
            Scan("2026-09-26T20:00:00Z", 2, "AutoScan").ReplaceLineEndings(" "),
            Mapped("2026-09-26T21:00:00Z", 2).ReplaceLineEndings(" "),
        ]) + "\n");

        var ledger = new CartographyLedger(null, files, NullLogger.Instance);

        // The live tick reads the current journal before the walk has finished.
        ledger.Apply([.. files.ReadText(current)!.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(Parse)]);
        ledger.FoldHistory([older, current], TestContext.Current.CancellationToken);

        var unsold = ledger.Unsold("F1");

        Assert.Equal(2, unsold.Held.Count);
        Assert.Equal(Efficient * 2, unsold.Total);
    }

    // -------------------------------------------------------------- the callout

    private static IReadOnlyList<Announcement> Said(CartographyLedger ledger, MappingCallout callout, params string[] lines)
    {
        var events = lines.Select(Parse).ToList();
        ledger.Apply(events);

        var store = new GameStateStore();
        store.Apply(Parse(Commander));

        var context = new CalloutContext(
            DateTimeOffset.UnixEpoch,
            IsPriming: false,
            State: store.Active,
            Status: GameStatus.Unknown,
            Route: NavRoute.None,
            Events: events);

        return [.. callout.Examine(context)];
    }

    [Fact]
    public void TheCalloutWaitsForTheDetailedScanThenSaysTheValueAndTheTotal()
    {
        var ledger = Ledger(Scan("2026-09-26T20:00:00Z", 1, "AutoScan"), Mapped("2026-09-26T20:05:00Z", 1));
        var callout = new MappingCallout { Ledger = ledger };

        Assert.Empty(Said(ledger, callout, Scan("2026-09-26T20:27:56Z", 2, "AutoScan"), Mapped("2026-09-26T21:45:15Z", 2)));

        var said = Assert.Single(Said(ledger, callout, Scan("2026-09-26T21:45:15Z", 2, "Detailed", wasMapped: false)));

        // Both are under a million, which the callout says with "Cr" after the banded figure.
        var each = SpokenCredits.Band(Efficient) + " Cr";
        var total = SpokenCredits.Band(Efficient * 2) + " Cr";

        Assert.Equal($"{System} 2 mapped, efficiently. About {each}. About {total} unsold.", said.Text);
    }

    [Fact]
    public void AnInefficientMappingSaysOnlyMapped()
    {
        var ledger = Ledger();
        var callout = new MappingCallout { Ledger = ledger };

        var said = Assert.Single(Said(
            ledger,
            callout,
            Scan("2026-09-26T21:40:00Z", 2, "AutoScan"),
            Mapped("2026-09-26T21:45:15Z", 2, probes: 9),
            Scan("2026-09-26T21:45:15Z", 2, "Detailed")));

        Assert.StartsWith($"{System} 2 mapped. About ", said.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ADetailedScanWithNoMappingSaysNothing()
    {
        var ledger = Ledger();

        Assert.Empty(Said(ledger, new MappingCallout { Ledger = ledger }, Scan("2026-09-26T21:45:15Z", 2, "Detailed")));
    }

    // ---------------------------------------------------------------- the tools

    private static Task<ToolResult> Ask(CartographyLedger ledger, string tool, ToolCaller caller = ToolCaller.Commander)
    {
        var gameState = new GameStateStore();
        gameState.Apply(Parse(Commander));

        return CapabilityRegistry
            .Build([JournalCapability.Create(gameState, cartography: ledger)])
            .InvokeAsync(tool, ToolArguments.FromJson("{}"), TestContext.Current.CancellationToken, caller);
    }

    [Fact]
    public async Task TheCommanderCanAskWhatTheyAreCarrying()
    {
        var ledger = Ledger(
            Scan("2026-09-26T20:00:00Z", 1, "AutoScan"),
            Mapped("2026-09-26T20:05:00Z", 1),
            Scan("2026-09-26T20:10:00Z", 2, "AutoScan"),
            Mapped("2026-09-26T20:15:00Z", 2, probes: 9));

        var answer = (await Ask(ledger, "get_unsold_exploration")).Content;

        Assert.Contains("credits of mapped bodies unsold, from 2 bodies. 1 of them was mapped efficiently.", answer, StringComparison.Ordinal);
        Assert.Contains($"{System} 1 — about ", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheResetIsRefusedToTheModelAndDoneForTheCommander()
    {
        var ledger = Ledger(Scan("2026-09-26T20:00:00Z", 1, "AutoScan"), Mapped("2026-09-26T20:05:00Z", 1));

        var refused = await Ask(ledger, JournalCapability.ResetExplorationTool, ToolCaller.Model);

        Assert.True(refused.IsError);
        Assert.Single(ledger.Unsold("F1").Held);

        var done = await Ask(ledger, JournalCapability.ResetExplorationTool);

        Assert.False(done.IsError);
        Assert.Empty(ledger.Unsold("F1").Held);
    }
}
