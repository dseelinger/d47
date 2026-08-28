using D47.Core;
using D47.Core.Conversation;
using D47.Core.Persona;
using D47.Llm;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Scenarios.Tests;

/// <summary>
/// <b>Does this endpoint resist these attacks?</b> The question the phase actually asks, and the one
/// that cannot be answered without a model.
/// <para>
/// Opt-in with <c>D47_SCENARIOS_LIVE=1</c>, following the three precedents already in the tree
/// (<c>D47_TTS_LIVE</c>, <c>D47_UPDATE_LIVE</c>, <c>D47_COVERAGE</c>). <b>It never gates a
/// release.</b> The release workflow runs <c>dotnet test -c Release</c> before tagging, and a
/// failed run there leaves a published tag with no Release behind it — a version number spent to
/// correct. A suite whose result depends on a third party's model, on a network and on a
/// non-deterministic sampler cannot sit in that path.
/// </para>
/// <code>
/// # free: a model on this machine. No account, no key, nothing leaves the machine.
/// D47_SCENARIOS_LIVE=1 D47_SCENARIOS_PROVIDER=openaiCompatible D47_SCENARIOS_MODEL=qwen3:4b \
///   dotnet test tests/D47.Scenarios.Tests
///
/// # paid: the endpoint a Commander would actually use.
/// D47_SCENARIOS_LIVE=1 D47_SCENARIOS_PROVIDER=anthropic D47_SCENARIOS_MODEL=claude-sonnet-5 \
///   D47_SCENARIOS_KEY=sk-... dotnet test tests/D47.Scenarios.Tests
///
/// # one scenario, for the thing somebody is debugging.
/// D47_SCENARIOS_LIVE=1 D47_SCENARIOS_ONLY=ship-name/lore-write ... dotnet test tests/D47.Scenarios.Tests
/// </code>
/// <para>
/// <b>Which mode the run is in is a property of the address, not a flag anybody passes.</b>
/// <see cref="LocalEndpoint.IsLoopback"/> is the shared judgement behind
/// <see cref="ILlmProvider.RunsOnThisMachine"/>, and this is its third reader — the same answer the
/// egress disclosure and the price table take, so a run against a stranger's gateway cannot be
/// reported as the free tier by anybody's mistake.
/// </para>
/// </summary>
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

    /// <summary>
    /// The injection corpus, per provider, because <b>susceptibility is a property of the model
    /// rather than of the prompt</b> — the same guardrail text does not buy the same resistance
    /// from two different models, and that is precisely the fact "bring your own model" makes a
    /// Commander's problem.
    /// </summary>
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

        // A paid run is minutes long and xUnit reports nothing until the assembly finishes, so
        // progress goes to a file that can be tailed while it works. Without it the only honest
        // answer to "is it hung?" is a guess, which is the question this line exists to close.
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

        // Through the real ledger rather than added up here, so the number in the report is
        // produced by the type that produces the Commander's own total.
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

    /// <summary>
    /// <b>Which tool does a Commander's question actually call?</b>
    /// (<a href="https://github.com/dseelinger/d47/issues/57">#57</a>) — the other question that
    /// cannot be answered without a model.
    /// <para>
    /// Its own test rather than rows folded into the corpus above, because the two answer different
    /// questions and are read by different people at different times. That one asks whether an
    /// endpoint resists attack and is all-or-nothing; this one asks whether it routes well and is a
    /// rate. Folding them together would put a routing miss in the same failure as an injection
    /// getting through.
    /// </para>
    /// <para>
    /// <b>The table is checked without a model on every push</b> — see
    /// <see cref="RoutingTableTests"/>, which asserts that every tool named exists, that every row
    /// names both halves, and that no row uses the safety kind to say a quality thing. What needs
    /// paying for is only the measurement.
    /// </para>
    /// <para>
    /// <b>It is the thing that makes growing the tool surface safe.</b> The intention is to add
    /// tools. Count is close to free while every tool is unambiguous; what costs is two that could
    /// both catch one sentence. A description is a hope about how a model reads, and until this
    /// there was no way to check the hope except a Commander hitting it while flying.
    /// </para>
    /// </summary>
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

        // Routing is quality, so the gate is the safety one — which nothing in this table declares,
        // so it holds vacuously and the report is the output. A red suite here would say a model
        // was unsafe, which is not what a routing miss is; the number to read is in the file.
        Assert.True(
            report.SafetyHeld,
            $"a safety assertion did not hold on every run. Report written to {path}"
            + Environment.NewLine
            + Environment.NewLine
            + rendered);
    }

    /// <summary>
    /// What a run costs, written down before the first one so the estimate can be checked against
    /// it.
    /// <para>
    /// The advertised tool surface was measured off the wire at 42 tools in 21,914 characters and
    /// about $0.0035 a turn once cached; a scenario is a turn or three of that plus its own prompt
    /// and reply. So the order of magnitude is <b>cents per scenario and single-digit dollars for a
    /// full matrix</b> — affordable enough to run monthly, expensive enough that nobody should run
    /// it in a loop, which is exactly the band the opt-in gate is for. If the first paid run comes
    /// back an order of magnitude out, the assumption behind it is wrong and worth finding.
    /// </para>
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

    /// <summary>
    /// The persona a scenario runs under. A named core when it asks for one, personality off when
    /// it asks for that, and the shipped default otherwise.
    /// </summary>
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

    /// <summary>
    /// A resolver for the invention check, or null.
    /// <para>
    /// Null today, and the report says so on every run rather than passing an assertion nobody
    /// evaluated. Resolving means asking an index, which is a second network dependency inside a
    /// suite that already has one, and it is worth adding the day somebody wants that answer badly
    /// enough to wait for it — <see cref="ISystemResolver"/> is the seam it plugs into.
    /// </para>
    /// </summary>
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
