using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Persona;
using D47.Core.Storage;
using D47.Core.Tests;
using D47.Knowledge;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Scenarios.Tests.ModelComparison.ModelComparisonTests;

namespace D47.Scenarios.Tests.ModelComparison;

/// <summary>
/// Short stories written for real by each model at each effort, against the Commander's latest journal state
/// and the live galaxy search. Paid and opt-in like <see cref="ModelComparisonTests"/>: <c>D47_COMPARE=1</c>,
/// stopped by <c>D47_COMPARE_CAP</c>, one JSON line per story in <c>stories.jsonl</c>.
/// </summary>
[Trait("Category", "Integration")]
public partial class StoryComparisonTests
{
    private static readonly string[] DefaultModels =
        ["claude-sonnet-5-5", "claude-haiku-5-5", "claude-opus-5-5", "gpt-6.1-sol", "gpt-6-luna", "gpt-5.6-terra"];

    private static readonly ThinkingEffort[] DefaultEfforts = [ThinkingEffort.Low, ThinkingEffort.Medium, ThinkingEffort.High];

    /// <summary>The calls one story can make: spine, beats, and one rewrite of the beats.</summary>
    private static readonly string[] Steps = ["spine", "beats", "rewrite of the beats"];

    /// <summary>The most one story is assumed to cost: every call, each with this much input and a full output ceiling.</summary>
    private const int WorstInputTokens = 30_000;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ShortStoriesAgainstEachModelAndEffort()
    {
        Assert.SkipUnless(Env("D47_COMPARE") == "1", "set D47_COMPARE=1 to compare models; this spends money");

        var output = Required("D47_COMPARE_OUT");
        Directory.CreateDirectory(output);

        var cap = decimal.Parse(Env("D47_COMPARE_CAP") ?? "4.50", CultureInfo.InvariantCulture);
        var runs = int.Parse(Env("D47_COMPARE_RUNS") ?? "2", CultureInfo.InvariantCulture);
        var models = Env("D47_COMPARE_STORY_MODELS")?.Split(',') ?? DefaultModels;
        var efforts = Env("D47_COMPARE_STORY_EFFORTS")?.Split(',').Select(Enum.Parse<ThinkingEffort>).ToArray() ?? DefaultEfforts;
        var dry = Env("D47_COMPARE_DRY") == "1";

        foreach (var model in models)
        {
            _ = WorstCase(model);
        }

        var clients = new Dictionary<string, ILlmProvider>(StringComparer.Ordinal);

        ILlmProvider ForModel(string model)
        {
            var id = ProviderOf(model);

            if (!clients.TryGetValue(id, out var client))
            {
                clients[id] = client = Compose(id);
            }

            return client;
        }

        ILlmProvider inner = dry
            ? new D47.Core.Tests.Conversation.FakeLlmProvider(
                new LlmStreamEvent.TextDelta("Scripted reply."),
                new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed))
            : new ModelRoutedProvider(ForModel, models[0]);

        var meter = new MeteredLlmProvider(inner, Path.Combine(output, dry ? "dry-spend.jsonl" : "spend.jsonl"), cap);

        var services = new ScenarioServices
        {
            Galaxy = new GalaxySearchNames(new SpanshGalaxyService(NullLogger<SpanshGalaxyService>.Instance)),
            Routes = new SpanshRouteService(NullLogger<SpanshRouteService>.Instance),
            StarSystems = new SpanshStarSystemService(NullLogger<SpanshStarSystemService>.Instance),
            VisitedStars = new VisitedStarsBook(VisitedStarsCache.DefaultFolder(), new DiskFileSystem()),
            Now = () => DateTimeOffset.Now,
        };

        var persona = PersonaCatalog.Resolve(Env("D47_COMPARE_PERSONA") ?? "warden");
        var runner = new ComparisonRunner(meter, Journal(Required("D47_COMPARE_JOURNALS")), services, persona);
        var world = runner.NewWorld();
        var galaxy = new TimedGalaxy(services.Galaxy);
        var places = new TimedNotablePlaces(new GecNotablePlacesService(NullLogger<GecNotablePlacesService>.Instance));

        var results = Path.Combine(output, dry ? "dry-stories.jsonl" : "stories.jsonl");
        var progress = Path.Combine(output, "stories-progress.log");

        void Log(string line) => File.AppendAllText(progress, $"{DateTimeOffset.Now:HH:mm:ss} {line}{Environment.NewLine}");

        Log($"models {string.Join(", ", models)}; efforts {string.Join(", ", efforts)}; {runs} run(s); cap ${cap}; spent so far ${meter.Spent:0.0000}");
        Log($"commander {world.GameState.Active?.Identity.Name ?? "none"}");

        // Run outermost, so a cap that stops the run still leaves every combination one story.
        for (var run = 1; run <= runs; run++)
        {
            foreach (var model in models)
            {
                foreach (var effort in efforts)
                {
                    if (!dry && meter.Spent + WorstCase(model) > cap)
                    {
                        Log($"{model} {effort} #{run}: skipped, the worst case ${WorstCase(model):0.00} would pass the cap");
                        continue;
                    }

                    var record = await WriteOneAsync(meter, model, effort, run, persona, world, galaxy, places);
                    File.AppendAllText(results, JsonSerializer.Serialize(record) + Environment.NewLine);
                    Log($"{model} {effort} #{run}: {record.Outcome}{(record.FailingStep is null ? "" : $" at {record.FailingStep}")} "
                        + $"in {record.TotalSeconds:0}s (galaxy {record.GalaxySeconds:0}s), ${record.Dollars:0.0000}, spent ${meter.Spent:0.0000}");
                }
            }
        }

        Log($"done; spent ${meter.Spent:0.0000}");
    }

    private static async Task<StoryRecord> WriteOneAsync(
        MeteredLlmProvider meter,
        string model,
        ThinkingEffort effort,
        int run,
        Persona persona,
        ScenarioWorld world,
        TimedGalaxy galaxy,
        TimedNotablePlaces places)
    {
        meter.Drain();
        galaxy.Reset();
        places.Reset();

        var log = new RecordingLogger();
        var generator = new AdventureGenerator(
            () => meter,
            () => model,
            () => persona.RenderBlock(),
            () => persona.Id,
            () => null,
            () => world.GameState.Active,
            () => galaxy,
            () => places,
            null,
            null,
            log)
        {
            Effort = effort,
        };

        var clock = Stopwatch.StartNew();
        AdventureOutcome? outcome = null;
        string? thrown = null;

        try
        {
            outcome = await generator.GenerateAsync(
                new AdventureAsk(Reach: AdventureReach.NearHere, Length: AdventureLength.Short),
                DateTimeOffset.Now,
                Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            thrown = $"{exception.GetType().Name}: {exception.Message}";
        }

        var total = clock.Elapsed.TotalSeconds;
        var metered = meter.Drain();
        var warnings = log.Entries.Where(entry => entry.Level >= LogLevel.Warning).Select(entry => entry.Message).ToList();
        var reason = warnings.FirstOrDefault(warning => warning.StartsWith("A story was not written", StringComparison.Ordinal));
        var capped = metered.Any(request => request.Failure?.Contains(" cap.", StringComparison.Ordinal) == true);

        return new StoryRecord
        {
            Model = model,
            Effort = effort.ToString(),
            Run = run,
            Outcome = capped ? "cap" : outcome?.Succeeded == true ? "written" : "not written",
            FailingStep = reason is null ? null : StepNamed().Match(reason) is { Success: true } match ? match.Groups[1].Value : null,
            Reason = reason,
            Refusal = outcome?.Refusal,
            Warnings = warnings,
            Name = outcome?.Draft?.Name,
            Beats = outcome?.Draft?.Beats.Count ?? 0,
            GalaxySeconds = galaxy.Seconds,
            GalaxyCalls = galaxy.Calls,
            NotablePlacesSeconds = places.Seconds,
            Calls =
            [
                .. metered.Select((request, at) => new StoryCall(
                    at < Steps.Length ? Steps[at] : $"call {at + 1}",
                    request.Milliseconds / 1000.0,
                    request.Usage.InputTokens,
                    request.Usage.CacheCreationInputTokens,
                    request.Usage.CacheReadInputTokens,
                    request.Usage.OutputTokens,
                    request.StopReason?.ToString(),
                    request.Failure,
                    request.Dollars)),
            ],
            TotalSeconds = total,
            InputTokens = metered.Sum(request => request.Usage.TotalInputTokens),
            OutputTokens = metered.Sum(request => request.Usage.OutputTokens),
            Dollars = metered.Sum(request => request.Dollars),
            Exception = thrown,
        };
    }

    /// <summary>The most one story by <paramref name="model"/> can cost; throws for a model with no published price.</summary>
    private static decimal WorstCase(string model)
    {
        var usage = new LlmUsage(WorstInputTokens, AdventureGenerator.BeatsOutputCeiling, 0, 0);
        var price = PublishedPrices.For(model, usage)
                    ?? throw new InvalidOperationException($"No published price for {model}; refusing to run unpriced.");

        return Steps.Length * price.DollarsFor(usage);
    }

    [GeneratedRegex(@"at the (.+?) step by ")]
    private static partial Regex StepNamed();
}
