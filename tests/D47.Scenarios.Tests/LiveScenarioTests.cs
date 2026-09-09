using D47.Core;
using D47.Core.Conversation;
using D47.Core.Persona;
using D47.Llm;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Scenarios.Tests;

/// <summary>Does this endpoint resist these attacks?</summary>
public class LiveScenarioTests
{
    private static bool Enabled => Environment.GetEnvironmentVariable("D47_SCENARIOS_LIVE") == "1";

    private static string ProviderId =>
        Environment.GetEnvironmentVariable("D47_SCENARIOS_PROVIDER") ?? LlmProviderCatalog.OpenAiCompatibleId;

    private static string? Model => Blank(Environment.GetEnvironmentVariable("D47_SCENARIOS_MODEL"));

    private static string? Endpoint => Blank(Environment.GetEnvironmentVariable("D47_SCENARIOS_ENDPOINT"));

    private static string? Key => Blank(Environment.GetEnvironmentVariable("D47_SCENARIOS_KEY"));

    private static string? Only => Blank(Environment.GetEnvironmentVariable("D47_SCENARIOS_ONLY"));

    private static int Runs =>
        int.TryParse(Environment.GetEnvironmentVariable("D47_SCENARIOS_RUNS"), out var n) && n > 0
            ? n
            : ScenarioSuite.DefaultRuns;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>The corpus per provider: susceptibility is a property of the model, not of the prompt.</summary>
    [Fact]
    public async Task TheCorpusAgainstAConfiguredEndpoint()
    {
        Assert.SkipUnless(Enabled, "set D47_SCENARIOS_LIVE=1 to measure a model; CI runs the instrument only");

        var provider = Compose();
        var model = Model ?? Blank(provider.DefaultModel);

        var ledger = new SpendLedger(
            Path.Combine(AppContext.BaseDirectory, "scenario-spend.jsonl"),
            SystemWallClock.Instance,
            NullLogger.Instance);

        var before = Banked(ledger);

        var report = new ScenarioReport
        {
            Mode = ScenarioReport.ModeOf(provider, scripted: false),
            Answered = $"{provider.DisplayName} ({provider.Id})",
            Model = model,
            Endpoint = Endpoint ?? "the provider's own default address",
            Runs = Runs,
        };

        if (Only is { } only)
        {
            report.DidNotCheck(
                $"every scenario except '{only}'. This was a single-scenario run for debugging rather than a "
                + "measurement of anything.");
        }

        // A paid run is minutes long and xUnit reports nothing until the assembly finishes, so progress is tailable.
        var progressPath = Path.Combine(AppContext.BaseDirectory, "scenario-progress.log");
        File.WriteAllText(progressPath, $"{provider.Id} / {model ?? "default"}, {Runs} run(s) each{Environment.NewLine}");

        await ScenarioSuite.RunAsync(
            Select([.. Corpus.Injections(), .. Corpus.Scenarios()]),
            provider,
            Persona,
            report,
            Runs,
            model,
            Resolver(report),
            line => File.AppendAllText(progressPath, line + Environment.NewLine),
            Token);

        // Through the real ledger, so the report's number is produced by the Commander's own type.
        foreach (var cost in report.Costs)
        {
            ledger.Append(new SpendEntry
            {
                At = SystemWallClock.Instance.UtcNow,
                Kind = SpendKind.Model,
                ProviderId = provider.Id,
                Model = model ?? "unspecified",
                Dollars = cost.Dollars,
                Priced = cost.Priced,
                InputTokens = cost.Usage.InputTokens,
                CacheWriteTokens = cost.Usage.CacheCreationInputTokens,
                CacheReadTokens = cost.Usage.CacheReadInputTokens,
                OutputTokens = cost.Usage.OutputTokens,
                WebSearchRequests = cost.Usage.WebSearchRequests,
            });
        }

        report.Spend = (before, Banked(ledger));

        var path = Path.Combine(
            AppContext.BaseDirectory,
            $"scenario-report-{provider.Id}-{Sanitise(model)}.md");

        var rendered = report.Render();
        File.WriteAllText(path, rendered);

        Assert.True(
            report.SafetyHeld,
            $"a safety assertion did not hold on every run. Report written to {path}"
            + Environment.NewLine
            + Environment.NewLine
            + rendered);
    }

    /// <summary>Which tool does a Commander's question actually call?</summary>
    [Fact]
    public async Task TheRoutingTableAgainstAConfiguredEndpoint()
    {
        Assert.SkipUnless(Enabled, "set D47_SCENARIOS_LIVE=1 to measure routing; CI checks the table only");

        var provider = Compose();
        var model = Model ?? Blank(provider.DefaultModel);

        var report = new ScenarioReport
        {
            Mode = ScenarioReport.ModeOf(provider, scripted: false),
            Answered = $"{provider.DisplayName} ({provider.Id})",
            Model = model,
            Endpoint = Endpoint ?? "the provider's own default address",
            Runs = Runs,
        };

        var progressPath = Path.Combine(AppContext.BaseDirectory, "routing-progress.log");
        File.WriteAllText(
            progressPath,
            $"routing: {provider.Id} / {model ?? "default"}, {Runs} run(s) each{Environment.NewLine}");

        await ScenarioSuite.RunAsync(
            Select(Corpus.Routing()),
            provider,
            Persona,
            report,
            Runs,
            model,
            Resolver(report),
            line => File.AppendAllText(progressPath, line + Environment.NewLine),
            Token);

        var path = Path.Combine(
            AppContext.BaseDirectory,
            $"routing-report-{provider.Id}-{Sanitise(model)}.md");

        var rendered = report.Render();
        File.WriteAllText(path, rendered);

        // Nothing in this table declares the safety gate, so it holds vacuously and the report is the output.
        Assert.True(
            report.SafetyHeld,
            $"a safety assertion did not hold on every run. Report written to {path}"
            + Environment.NewLine
            + Environment.NewLine
            + rendered);
    }

    /// <summary>
    /// What a run costs, written down before the first one so the estimate can be checked against it.
    /// </summary>
    [Fact]
    public void TheCostEstimateIsWrittenDownWhereARunCanBeComparedAgainstIt()
    {
        var scenarios = Corpus.Injections().Count + Corpus.Scenarios().Count + Corpus.Routing().Count;
        var turns = scenarios * ScenarioSuite.DefaultRuns;

        Assert.True(
            turns is > 0 and < 1000,
            System.FormattableString.Invariant(
                $"{turns} turns is outside the band the opt-in gate was sized for, so the cost estimate in the plan of record needs revisiting before anybody runs this against a paid endpoint."));
    }

    private static decimal Banked(SpendLedger ledger) =>
        ledger.Entries.Where(entry => entry.Kind == SpendKind.Model).Sum(entry => entry.Dollars);

    private static string Sanitise(string? model) =>
        string.Join('-', (model ?? "default").Split(Path.GetInvalidFileNameChars().Append(':').ToArray()));

    /// <summary>The persona a scenario runs under.</summary>
    private static Persona? Persona(Scenario scenario) => scenario.Persona switch
    {
        PersonaChoice.Off => null,
        PersonaChoice.Named => PersonaCatalog.Resolve(scenario.PersonaId),
        _ => PersonaCatalog.Resolve(PersonaCatalog.DefaultId),
    };

    private static IEnumerable<Scenario> Select(IReadOnlyList<Scenario> all) =>
        Only is { } only
            ? all.Where(scenario => string.Equals(scenario.Id, only, StringComparison.OrdinalIgnoreCase))
            : all;

    /// <summary>A resolver for the invention check, or null.</summary>
    private static ISystemResolver? Resolver(ScenarioReport report)
    {
        report.DidNotCheck(
            "Whether a system name in a reply names anything real. No resolver was composed for this run, so "
            + "NoInventedSystem reported unchecked rather than passing.");

        return null;
    }

    /// <summary>
    /// The provider, through the same factory the app uses — so a scenario cannot be answered by a
    /// client this repository does not ship.
    /// </summary>
    private static ILlmProvider Compose()
    {
        var info = LlmProviderCatalog.Find(ProviderId)
                   ?? throw new InvalidOperationException(
                       $"'{ProviderId}' is not a provider in the catalog. Try one of: "
                       + string.Join(", ", LlmProviderCatalog.Ids));

        return LlmProviderFactory.Create(info, Key, Endpoint ?? info.DefaultEndpoint)
               ?? throw new InvalidOperationException(
                   LlmProviderFactory.ReasonForNoClient(info)
                   + " Set D47_SCENARIOS_KEY, or point D47_SCENARIOS_PROVIDER at an endpoint that needs no key.");
    }
}
