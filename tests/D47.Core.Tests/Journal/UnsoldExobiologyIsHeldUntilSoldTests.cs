using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>The unsold organic data total: priced on analysis, cut by a sale, cleared by a death or a reset (#526).</summary>
public class UnsoldExobiologyIsHeldUntilSoldTests
{
    private const long SystemAddress = 3274702866819;

    private const string Commander =
        """{"timestamp":"2026-09-26T19:00:00Z","event":"LoadGame","FID":"F1","Commander":"Fixture"}""";

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string BodyScan(string at, int body, string scanType, bool footfalled) =>
        $$"""
          {"timestamp":"{{at}}","event":"Scan","ScanType":"{{scanType}}","BodyName":"Praea Euq BF-A d95 {{body}}",
           "BodyID":{{body}},"SystemAddress":{{SystemAddress}},"Landable":true,"WasFootfalled":{{(footfalled ? "true" : "false")}}}
          """;

    private static string Analyse(string at, int body, string species, string symbol) =>
        $$"""
          {"timestamp":"{{at}}","event":"ScanOrganic","ScanType":"Analyse",
           "Genus":"$Codex_Ent_{{species.Split(' ')[0]}}_Genus_Name;","Genus_Localised":"{{species.Split(' ')[0]}}",
           "Species":"{{symbol}}","Species_Localised":"{{species}}","SystemAddress":{{SystemAddress}},"Body":{{body}}}
          """;

    private static string Cactoida(string at, int body = 41) =>
        Analyse(at, body, "Cactoida Cortexum", "$Codex_Ent_Cactoid_01_Name;");

    private static string Frutexa(string at, int body = 41) =>
        Analyse(at, body, "Frutexa Acus", "$Codex_Ent_Shrubs_02_Name;");

    /// <summary>A species the table does not carry.</summary>
    private static string Unpriced(string at, int body = 41) =>
        Analyse(at, body, "Tessera Nova", "$Codex_Ent_Tessera_01_Name;");

    private static string Sale(string at, params string[] symbols) =>
        $$"""
          {"timestamp":"{{at}}","event":"SellOrganicData","MarketID":4209053443,"BioData":[{{string.Join(",",
              symbols.Select(symbol => $$"""{"Species":"{{symbol}}","Value":1,"Bonus":0}"""))}}]}
          """;

    private static string Died(string at) => $$"""{"timestamp":"{{at}}","event":"Died"}""";

    private static ExobiologyLedger Ledger(params string[] lines)
    {
        var ledger = new ExobiologyLedger(null, NullLogger.Instance);
        ledger.FoldHistory([], TestContext.Current.CancellationToken);
        ledger.Apply([.. new[] { Commander }.Concat(lines).Select(Parse)]);
        return ledger;
    }

    // -------------------------------------------------------------- pricing

    [Fact]
    public void AnUnwalkedBodyPaysFiveTimesTheTableValue()
    {
        var ledger = Ledger(BodyScan("2026-09-26T20:00:00Z", 41, "Detailed", false), Cactoida("2026-09-26T21:00:00Z"));

        var held = Assert.Single(ledger.Unsold("F1").Held);

        Assert.Equal(3_667_600 * 5, held.Worth);
        Assert.Equal(1, ledger.Unsold("F1").WithBonus);
    }

    [Fact]
    public void AWalkedBodyPaysTheTableValueAlone()
    {
        var ledger = Ledger(BodyScan("2026-09-26T20:00:00Z", 41, "AutoScan", true), Cactoida("2026-09-26T21:00:00Z"));

        Assert.Equal(3_667_600, Assert.Single(ledger.Unsold("F1").Held).Worth);
    }

    /// <summary>Praea Euq BF-A d95 5 e: AutoScan true, Detailed false, and the sale paid no bonus.</summary>
    [Fact]
    public void AScanSayingTheBodyWasWalkedOnOutranksOneSayingItWasNot()
    {
        var ledger = Ledger(
            BodyScan("2026-09-26T20:27:56Z", 41, "AutoScan", true),
            BodyScan("2026-09-26T21:45:15Z", 41, "Detailed", false),
            Cactoida("2026-09-26T22:17:47Z"));

        var held = Assert.Single(ledger.Unsold("F1").Held);

        Assert.False(held.FirstFootfall);
        Assert.Equal(3_667_600, held.Worth);
    }

    [Fact]
    public void TwoScansInOneSecondStillRuleTheBonusOut()
    {
        var ledger = Ledger(
            BodyScan("2026-09-26T20:27:56Z", 41, "AutoScan", true),
            BodyScan("2026-09-26T20:27:56Z", 41, "Detailed", false),
            Cactoida("2026-09-26T22:17:47Z"));

        Assert.False(Assert.Single(ledger.Unsold("F1").Held).FirstFootfall);
    }

    [Fact]
    public void WithNoScanOfTheBodyTheBonusIsNotAssumed()
    {
        var ledger = Ledger(Cactoida("2026-09-26T21:00:00Z"));

        var held = Assert.Single(ledger.Unsold("F1").Held);

        Assert.Null(held.FirstFootfall);
        Assert.Equal(3_667_600, held.Worth);
        Assert.Equal(1, ledger.Unsold("F1").BonusUnknown);
    }

    [Fact]
    public void ASpeciesWithNoValueIsCountedButLeftOutOfTheTotal()
    {
        var ledger = Ledger(Cactoida("2026-09-26T21:00:00Z"), Unpriced("2026-09-26T21:10:00Z"));

        var unsold = ledger.Unsold("F1");

        Assert.Equal(2, unsold.Held.Count);
        Assert.Equal(3_667_600, unsold.Total);
        Assert.Equal("Tessera Nova", Assert.Single(unsold.Unpriced).Species);
    }

    // ------------------------------------------------------ sale, death, reset

    [Fact]
    public void ASaleRemovesEveryAnalysisOfTheSpeciesItNames()
    {
        var ledger = Ledger(
            Cactoida("2026-09-26T21:00:00Z", body: 41),
            Cactoida("2026-09-26T22:00:00Z", body: 42),
            Frutexa("2026-09-26T22:10:00Z"),
            Sale("2026-09-27T00:27:49Z", "$Codex_Ent_Cactoid_01_Name;"));

        var held = Assert.Single(ledger.Unsold("F1").Held);

        Assert.Equal("Frutexa Acus", held.Species);
    }

    [Fact]
    public void AnAnalysisAfterTheSaleIsStillHeld()
    {
        var ledger = Ledger(
            Cactoida("2026-09-26T21:00:00Z"),
            Sale("2026-09-27T00:27:49Z", "$Codex_Ent_Cactoid_01_Name;"),
            Cactoida("2026-09-27T02:00:00Z"));

        Assert.Single(ledger.Unsold("F1").Held);
    }

    [Fact]
    public void DeathLosesEverythingHeld()
    {
        var ledger = Ledger(
            Cactoida("2026-09-26T21:00:00Z"),
            Frutexa("2026-09-26T22:00:00Z"),
            Died("2026-09-26T23:00:00Z"));

        Assert.Empty(ledger.Unsold("F1").Held);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void AResetClearsTheTotalAndSurvivesARestart()
    {
        using var install = new TempInstall();
        var path = Path.Combine(install.Paths.Data, "unsold-data.json");

        var ledger = new ExobiologyLedger(path, NullLogger.Instance);
        ledger.Apply([Parse(Commander), Parse(Cactoida("2026-09-26T21:00:00Z"))]);
        ledger.Reset("F1", DateTimeOffset.Parse("2026-09-26T22:00:00Z", global::System.Globalization.CultureInfo.InvariantCulture));

        Assert.Empty(ledger.Unsold("F1").Held);

        var restarted = new ExobiologyLedger(path, NullLogger.Instance);
        restarted.Load();
        restarted.Apply([
            Parse(Commander),
            Parse(Cactoida("2026-09-26T21:00:00Z")),
            Parse(Frutexa("2026-09-26T23:00:00Z")),
        ]);

        Assert.Equal("Frutexa Acus", Assert.Single(restarted.Unsold("F1").Held).Species);
    }

    // ------------------------------------------------------------- the rebuild

    [Trait("Category", "Integration")]
    [Fact]
    public void TheTotalIsRebuiltFromOlderJournalsWithoutCountingTheLiveOneTwice()
    {
        using var install = new TempInstall();

        var older = Path.Combine(install.Root, "Journal.2026-09-25T190000.01.log");
        var current = Path.Combine(install.Root, "Journal.2026-09-26T190000.01.log");

        File.WriteAllLines(older, [
            """{"timestamp":"2026-09-25T19:00:00Z","event":"LoadGame","FID":"F1","Commander":"Fixture"}""",
            BodyScan("2026-09-25T20:00:00Z", 41, "Detailed", false).ReplaceLineEndings(" "),
            Cactoida("2026-09-25T21:00:00Z").ReplaceLineEndings(" "),
        ]);

        File.WriteAllLines(current, [
            Commander,
            Frutexa("2026-09-26T21:00:00Z").ReplaceLineEndings(" "),
        ]);

        var ledger = new ExobiologyLedger(null, NullLogger.Instance);

        // The live tick reads the current journal before the walk has finished.
        ledger.Apply([.. File.ReadAllLines(current).Select(Parse)]);
        ledger.FoldHistory([older, current], TestContext.Current.CancellationToken);

        var unsold = ledger.Unsold("F1");

        Assert.Equal(2, unsold.Held.Count);
        Assert.Equal((3_667_600 + 7_774_700) * 5, unsold.Total);
    }

    // -------------------------------------------------------------- the callout

    private static string Said(ExobiologyLedger ledger, string analyse)
    {
        var store = new GameStateStore();
        store.Apply(Parse(Commander));
        store.Apply(Parse(analyse));

        var context = new CalloutContext(
            DateTimeOffset.UnixEpoch,
            IsPriming: false,
            State: store.Active,
            Status: GameStatus.Unknown,
            Route: NavRoute.None,
            Events: [Parse(analyse)]);

        return Assert.Single(new SamplingCallout { Ledger = ledger }.Examine(context)).Text;
    }

    [Fact]
    public void TheAnalysisCalloutSaysTheValueAndTheUnsoldTotal()
    {
        var analyse = Cactoida("2026-09-26T22:17:47Z");
        var ledger = Ledger(
            BodyScan("2026-09-26T21:45:15Z", 41, "Detailed", false),
            Frutexa("2026-09-26T22:03:40Z"),
            analyse);

        Assert.Equal(
            "Cactoida Cortexum analysed. That run is complete. Worth 18.3 million with the first footfall bonus. "
            + "57.2 million unsold.",
            Said(ledger, analyse));
    }

    [Fact]
    public void TheCalloutSaysWhenTheBonusIsNotKnown()
    {
        var analyse = Cactoida("2026-09-26T22:17:47Z");

        Assert.Contains(
            "Worth 3.7 million; whether the first footfall bonus applies is not known.",
            Said(Ledger(analyse), analyse),
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheCalloutNamesASpeciesItCannotPriceAndSaysWhatTheTotalLeavesOut()
    {
        var analyse = Unpriced("2026-09-26T22:17:47Z");

        var said = Said(Ledger(Cactoida("2026-09-26T21:00:00Z"), analyse), analyse);

        Assert.Contains("I have no value for Tessera Nova.", said, StringComparison.Ordinal);
        Assert.Contains("3.7 million unsold, not counting 1 sample I have no value for.", said, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- the tools

    private static Task<ToolResult> Ask(ExobiologyLedger ledger, string tool, ToolCaller caller = ToolCaller.Commander)
    {
        var gameState = new GameStateStore();
        gameState.Apply(Parse(Commander));

        using var install = new TempInstall();
        var settings = TestSurface.For(install).Settings;

        return CapabilityRegistry
            .Build([ExobiologyCapability.Create(null, () => gameState.Active, settings, ledger: ledger)])
            .InvokeAsync(tool, ToolArguments.FromJson("{}"), TestContext.Current.CancellationToken, caller);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheCommanderCanAskWhatTheyAreCarrying()
    {
        var ledger = Ledger(
            BodyScan("2026-09-26T20:00:00Z", 41, "Detailed", false),
            Cactoida("2026-09-26T21:00:00Z"),
            Frutexa("2026-09-26T21:10:00Z", body: 7),
            Unpriced("2026-09-26T21:20:00Z"));

        var answer = (await Ask(ledger, "get_unsold_exobiology")).Content;

        Assert.Contains("26,112,700 credits of organic data unsold, from 2 analyses.", answer, StringComparison.Ordinal);
        Assert.Contains("1 of them carries the first footfall bonus.", answer, StringComparison.Ordinal);
        Assert.Contains("Tessera Nova", answer, StringComparison.Ordinal);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheResetIsRefusedToTheModelAndDoneForTheCommander()
    {
        var ledger = Ledger(Cactoida("2026-09-26T21:00:00Z"));

        var refused = await Ask(ledger, ExobiologyCapability.ResetTool, ToolCaller.Model);

        Assert.True(refused.IsError);
        Assert.Single(ledger.Unsold("F1").Held);

        var done = await Ask(ledger, ExobiologyCapability.ResetTool);

        Assert.False(done.IsError);
        Assert.Empty(ledger.Unsold("F1").Held);
    }
}
