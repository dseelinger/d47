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
/// Short stories, or story chapters, written for real by each model at each effort, against the Commander's latest
/// journal state and the live galaxy search. Paid and opt-in like <see cref="ModelComparisonTests"/>: <c>D47_COMPARE=1</c>,
/// stopped by <c>D47_COMPARE_CAP</c>, one JSON line per story in <c>stories.jsonl</c> or <c>chapters.jsonl</c>.
/// </summary>
[Trait("Category", "Integration")]
public partial class StoryComparisonTests
{
    private static readonly string[] DefaultModels =
        ["claude-sonnet-5-5", "claude-haiku-5-5", "claude-opus-5-5", "gpt-6.1-sol", "gpt-6-luna", "gpt-5.6-terra"];

    private static readonly ThinkingEffort[] DefaultEfforts = [ThinkingEffort.Low, ThinkingEffort.Medium, ThinkingEffort.High];

    /// <summary>The calls one story can make: spine, beats, and two rounds of replacements.</summary>
    private static readonly string[] Steps = ["spine", "beats", "replacements", "replacements"];

    /// <summary>The most one story is assumed to cost: every call, each with this much input and a full output ceiling.</summary>
    private const int WorstInputTokens = 30_000;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public Task ShortStoriesAgainstEachModelAndEffort() => RunAsync(chapters: false);

    /// <summary>Chapter two of an invented stock story, with chapter one before it.</summary>
    [Fact]
    public Task StoryChaptersAgainstEachModelAndEffort() => RunAsync(chapters: true);

    private static async Task RunAsync(bool chapters)
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

        var label = Env("D47_COMPARE_LABEL") ?? "";
        var drafts = Path.Combine(output, "drafts");
        Directory.CreateDirectory(drafts);

        await using var proxy = Env("D47_COMPARE_PROXY_PORT") is { } port
            ? new LoopbackProxy(
                int.Parse(port, CultureInfo.InvariantCulture),
                Env("D47_COMPARE_PROXY_TARGET") ?? "http://127.0.0.1:1234/",
                Env("D47_COMPARE_PROXY_VARIANT") ?? "",
                Path.Combine(output, "proxy", label.Length > 0 ? label : DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)))
            : null;

        var kind = chapters ? "chapter" : "short story";
        var results = Path.Combine(output, (dry ? "dry-" : "") + (chapters ? "chapters.jsonl" : "stories.jsonl"));
        var progress = Path.Combine(output, "stories-progress.log");

        void Log(string line) => File.AppendAllText(progress, $"{DateTimeOffset.Now:HH:mm:ss} {line}{Environment.NewLine}");

        Log($"{kind}s; models {string.Join(", ", models)}; efforts {string.Join(", ", efforts)}; {runs} run(s); cap ${cap}; spent so far ${meter.Spent:0.0000}");
        Log($"commander {world.GameState.Active?.Identity.Name ?? "none"}");

        if (Env("D47_COMPARE_OVERFLOW_FIRST") == "1")
        {
            foreach (var model in models.Where(model => LocalModels.Contains(model, StringComparer.Ordinal)))
            {
                Log($"{model}: overflow first: {await OverflowAsync(ForModel(model), model).ConfigureAwait(false)}");
            }
        }

        var chapterAsk = Env("D47_COMPARE_CHAPTER") == "6" ? ChapterSixAsk : ChapterAsk;
        var lastRefused = Env("D47_COMPARE_LAST_REFUSED");

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

                    var ask = (chapters
                        ? chapterAsk
                        : new AdventureAsk(
                            Reach: AdventureReach.NearHere,
                            Length: Enum.Parse<AdventureLength>(Env("D47_COMPARE_LENGTH") ?? nameof(AdventureLength.Short)),
                            Brief: Env("D47_COMPARE_BRIEF"))) with { LastDraftRefused = lastRefused };
                    var record = await WriteOneAsync(meter, model, effort, run, persona, world, galaxy, places, ask, kind, label, drafts);
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
        TimedNotablePlaces places,
        AdventureAsk ask,
        string kind,
        string label,
        string drafts)
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

            // Off, so a slow call is measured to its end rather than stopped.
            CallLimit = Timeout.InfiniteTimeSpan,
            SilenceLimit = Timeout.InfiniteTimeSpan,
        };

        var clock = Stopwatch.StartNew();
        AdventureOutcome? outcome = null;
        string? thrown = null;

        try
        {
            outcome = await generator.GenerateAsync(ask, DateTimeOffset.Now, Token).ConfigureAwait(false);
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
        var draftFile = Path.Combine(
            drafts,
            $"{(label.Length > 0 ? label + "-" : "")}{(kind == "chapter" ? "chapter" : "story")}-{model.Replace('/', '_')}-{effort}-{run}-{DateTimeOffset.Now:HHmmss}.json");

        File.WriteAllText(draftFile, JsonSerializer.Serialize(
            new
            {
                label,
                kind,
                model,
                effort = effort.ToString(),
                run,
                lastDraftRefused = ask.LastDraftRefused,
                draft = outcome?.Draft,
                reply = outcome?.Reply,
                refusal = outcome?.Refusal,
                contextMessage = outcome?.ContextMessage,
                notes = outcome?.Notes,
            },
            DraftJson));

        return new StoryRecord
        {
            Kind = kind,
            Label = label,
            Model = model,
            Effort = effort.ToString(),
            Run = run,
            Outcome = capped ? "cap" : outcome?.Succeeded == true ? "written" : "not written",
            FailingStep = reason is null ? null : StepNamed().Match(reason) is { Success: true } match ? match.Groups[1].Value : null,
            Reason = reason,
            Refusal = outcome?.Refusal,
            ContextMessage = outcome?.ContextMessage,
            DraftFile = Path.GetFileName(draftFile),
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
                    request.Dollars,
                    request.FirstOutputMilliseconds / 1000.0,
                    request.LongestSilenceMilliseconds / 1000.0)),
            ],
            TotalSeconds = total,
            InputTokens = metered.Sum(request => request.Usage.TotalInputTokens),
            OutputTokens = metered.Sum(request => request.Usage.OutputTokens),
            Dollars = metered.Sum(request => request.Dollars),
            Exception = thrown,
        };
    }

    /// <summary>Chapter two of a year-long story in its setup stage, asked the way the story director asks for one.</summary>
    private static AdventureAsk ChapterAsk => new(
        AdventureReach.Session,
        AdventureLength.Evening,
        Chapter: new AdventureChapter(
            new Adventure
            {
                Key = "the-quiet-manifest",
                Name = "The Quiet Manifest",
                Source = AdventureSource.Generated,
                StoryId = "the-salt-ledger",
                Spine = new AdventureSpine
                {
                    Premise = "A freight manifest lists a cargo nobody loaded.",
                    Want = "To find who signed for it.",
                    Stake = "Whether the ship's voice is keeping something from the Commander.",
                    Turn = "The signature is the Commander's own, dated before they bought the ship.",
                    Ending = "The voice says it does not remember, and hums instead.",
                },
                Beats =
                [
                    new AdventureBeat
                    {
                        Title = "The Courier Run",
                        Function = "setup",
                        Trigger = new AdventureTrigger { Kind = TriggerKind.Mission, MissionFamily = "Mission_Courier", Count = 1 },
                        Lines = [new AdventureLine { Text = "A packet for a station that has not answered in a week." }],
                    },
                    new AdventureBeat
                    {
                        Title = "The Ore",
                        Function = "resolution",
                        Trigger = new AdventureTrigger { Kind = TriggerKind.Mine, Count = 20 },
                        Lines = [new AdventureLine { Text = "Twenty tonnes, and the voice counts them twice.", Speaker = "ship" }],
                    },
                ],
            },
            []),
        Story: new AdventureStory(
            "the-salt-ledger",
            "The Salt Ledger",
            "You bought a second-hand Cobra from a broker who would not meet your eye. Its ship's voice is older than "
                + "the hull, and it keeps a ledger of debts that are not yours. Somewhere in it is a name you know.",
            "The ship's voice was trained on the logs of a trader who vanished owing a Guardian-site salvage crew. "
                + "The debt in the ledger is a promise to bring something back to a beacon. The trader was the Commander's "
                + "parent, and the voice has been waiting for someone with the same voiceprint. Clues so far: the voice hums "
                + "a lullaby; a dock hand at a frontier station recognises the ship's registry. Neither layer has named the "
                + "parent yet. The voice must never say outright what it knows; it remembers in fragments.",
            Chapter: 2,
            DaysRunning: 6,
            DaysSinceBeacon: null,
            Level: "midrange",
            Stage: "setup",
            StageBeats:
            [
                ("SetUp", "The Commander learns the ship's voice keeps a ledger of debts that are not theirs."),
                ("Catalyst", "An entry in the ledger names a station the Commander has never visited."),
            ],
            Genre: "Whydunit",
            ChapterSize: "one to three play sessions",
            Cast:
            [
                new AdventureSpeaker("dock-hand", "Ren", "A tired dock hand at a frontier station, short words.") { Role = "a dock hand who knows the ship" },
                new AdventureSpeaker("broker", "Mara Venn", "The broker who sold the ship, smooth and evasive."),
            ],
            Core: "archivist",
            CoreIsStock: true));

    private static readonly JsonSerializerOptions DraftJson = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// One call with a prompt larger than any context the server can load, so the provider learns the context from
    /// the refusal and clamps every later call's output to a quarter of it.
    /// </summary>
    private static async Task<string> OverflowAsync(ILlmProvider provider, string model)
    {
        var tokens = int.Parse(Env("D47_COMPARE_OVERFLOW_TOKENS") ?? "140000", CultureInfo.InvariantCulture);
        var filler = string.Join(' ', Enumerable.Range(0, tokens / 15).Select(at => $"The quick brown fox jumps over the lazy dog number {at}."));
        var reply = await FlavourTurn.AskForAsync(
            provider, model, null, null, filler + " Reply with OK.", null, null, null, null, Token, maxOutputTokens: 100).ConfigureAwait(false);

        return reply.Line is null ? $"{reply.Miss}: {reply.Message}" : $"answered {reply.Line.Length} characters";
    }

    /// <summary>
    /// Chapter six of the same story, after five invented chapters: the four before the last shown by name and
    /// premise, the last in full, and a hidden layer grown by five chapters of clues.
    /// </summary>
    private static AdventureAsk ChapterSixAsk
    {
        get
        {
            var two = ChapterAsk.Chapter!.Previous;

            static Adventure Earlier(string key, string name, string premise) => new()
            {
                Key = key,
                Name = name,
                Source = AdventureSource.Generated,
                StoryId = "the-salt-ledger",
                Spine = new AdventureSpine { Premise = premise },
            };

            var earlier = new[]
            {
                Earlier("the-broker", "The Broker's Receipt", "The receipt for the ship lists a previous owner whose name has been scratched out of the hull plate."),
                two,
                Earlier("the-frontier-dock", "Ren's Dock", "A dock hand on the frontier recognises the ship's registry and will not say from where."),
                Earlier("the-lullaby", "The Lullaby", "The ship's voice hums the same four notes whenever the Commander enters a system with a Guardian site."),
            };

            var five = two with
            {
                Key = "the-second-signature",
                Name = "The Second Signature",
                Spine = new AdventureSpine
                {
                    Premise = "A second ledger entry, older than the first, is signed by someone with the Commander's voiceprint.",
                    Want = "To find the station the entry names and what was promised there.",
                    Stake = "Whether the Commander is paying off someone else's debt without being told.",
                    Turn = "The station is abandoned, and its last log is in the ship's voice.",
                    Ending = "The voice asks to be taken somewhere quiet, and will not say why.",
                },
            };

            var story = ChapterAsk.Story! with
            {
                Chapter = 6,
                DaysRunning = 41,
                Stage = "fun and games",
                Hidden = ChapterAsk.Story.Hidden
                    + " Since then: the broker's receipt names a previous owner scratched from the hull plate; Ren at the frontier "
                    + "dock knew the registry and went quiet; the voice hums four notes in any system with a Guardian site; a "
                    + "second ledger entry, older than the first, carries the Commander's own voiceprint; the abandoned station's "
                    + "last log is in the ship's voice. The Commander has started to suspect the voice is waiting for something.",
                StageBeats =
                [
                    ("SetUp", "The Commander learns the ship's voice keeps a ledger of debts that are not theirs."),
                    ("Catalyst", "An entry in the ledger names a station the Commander has never visited."),
                    ("Debate", "The Commander weighs whether to keep a ship that remembers someone else."),
                    ("BreakIntoTwo", "The Commander decides to follow the ledger rather than sell the ship."),
                    ("FunAndGames", "Each entry the Commander settles opens another, further out toward the beacons."),
                ],
            };

            return new AdventureAsk(
                AdventureReach.Session,
                AdventureLength.Evening,
                Chapter: new AdventureChapter(five, earlier),
                Story: story);
        }
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
