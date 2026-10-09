using System.Globalization;
using System.Text.Json;
using D47.Core;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Persona;
using D47.Knowledge;
using D47.Llm;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Scenarios.Tests.ModelComparison;

/// <summary>
/// How two models compare on d47's own calls, against the Commander's real journals. Paid and opt-in: it
/// runs only with <c>D47_COMPARE=1</c>, stops at <c>D47_COMPARE_CAP</c> dollars across every run that shares
/// the ledger, and writes one JSON line per run for the report and the judge.
/// </summary>
[Trait("Category", "Integration")]
public class ModelComparisonTests
{
    private static string? Env(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;

    private static string Required(string name) =>
        Env(name) ?? throw new InvalidOperationException($"Set {name}.");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheCasesAgainstEachModel()
    {
        Assert.SkipUnless(Env("D47_COMPARE") == "1", "set D47_COMPARE=1 to compare models; this spends money");

        var output = Required("D47_COMPARE_OUT");
        Directory.CreateDirectory(output);

        var cap = decimal.Parse(Env("D47_COMPARE_CAP") ?? "4.50", CultureInfo.InvariantCulture);
        var runs = int.Parse(Env("D47_COMPARE_RUNS") ?? "3", CultureInfo.InvariantCulture);
        var quietRuns = int.Parse(Env("D47_COMPARE_QUIET_RUNS") ?? runs.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        var models = (Env("D47_COMPARE_MODELS") ?? "claude-sonnet-5-5,claude-haiku-5-5").Split(',');
        var only = Env("D47_COMPARE_ONLY")?.Split(',');

        // A dry run answers every request with one scripted line, to check the world and the routing for free.
        var meter = Env("D47_COMPARE_DRY") == "1"
            ? new MeteredLlmProvider(
                new D47.Core.Tests.Conversation.FakeLlmProvider(
                    new LlmStreamEvent.TextDelta("Scripted reply."),
                    new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed)),
                Path.Combine(output, "dry-spend.jsonl"),
                cap)
            : new MeteredLlmProvider(Compose(), Path.Combine(output, "spend.jsonl"), cap);

        var services = new ScenarioServices
        {
            SeedFrom = Env("D47_COMPARE_DATA"),
            Galaxy = new GalaxySearchNames(new SpanshGalaxyService(NullLogger<SpanshGalaxyService>.Instance)),
            Routes = new SpanshRouteService(NullLogger<SpanshRouteService>.Instance),
            StarSystems = new SpanshStarSystemService(NullLogger<SpanshStarSystemService>.Instance),
            VisitedStars = new VisitedStarsBook(VisitedStarsCache.DefaultFolder()),
            Screen = Env("D47_COMPARE_SCREEN") is { } screen ? new FileScreen(screen) : null,
            Now = () => DateTimeOffset.Now,
        };

        var runner = new ComparisonRunner(
            meter,
            Journal(Required("D47_COMPARE_JOURNALS")),
            services,
            PersonaCatalog.Resolve(Env("D47_COMPARE_PERSONA") ?? "warden"));

        var results = Path.Combine(output, "results.jsonl");
        var progress = Path.Combine(output, "progress.log");

        void Log(string line) => File.AppendAllText(progress, $"{DateTimeOffset.Now:HH:mm:ss} {line}{Environment.NewLine}");

        void Write(RunRecord record)
        {
            File.AppendAllText(results, JsonSerializer.Serialize(record) + Environment.NewLine);
            Log($"{record.Case} {record.Model} #{record.Run}: {record.Route ?? record.Area} ${record.Dollars:0.0000}, spent ${meter.Spent:0.0000}");
        }

        bool Chosen(string id) => only is null || only.Any(prefix => id.StartsWith(prefix, StringComparison.Ordinal));

        Log($"models {string.Join(", ", models)}; {runs} run(s) of each turn, {quietRuns} of each background call; cap ${cap}; spent so far ${meter.Spent:0.0000}");

        var turns = ComparisonCases.Turns.Where(turn => Chosen(turn.Id)).ToList();
        var quiet = ComparisonCases.Quiet.Where(call => Chosen(call.Id)).ToList();

        using (var shared = runner.NewWorld())
        {
            Log($"world: {shared.Registry.All.Sum(capability => capability.Descriptor.Tools.Count)} tools; commander {shared.GameState.Active?.Identity.Name ?? "none"} in {shared.GameState.Active?.Location.StarSystem ?? "nowhere"}");

            foreach (var turn in turns)
            {
                if (turn.Id == "new.screen" && services.Screen is null)
                {
                    Log($"{turn.Id}: skipped, no D47_COMPARE_SCREEN");
                    continue;
                }

                foreach (var model in models)
                {
                    for (var run = 1; run <= runs; run++)
                    {
                        if (meter.Spent >= cap)
                        {
                            Log("stopped: the cap is reached");
                            return;
                        }

                        if (turn.Mutates)
                        {
                            using var fresh = runner.NewWorld();
                            Write(await runner.RunTurnAsync(turn, fresh, model, run, Token));
                        }
                        else
                        {
                            Write(await runner.RunTurnAsync(turn, shared, model, run, Token));
                        }
                    }
                }
            }

            foreach (var call in quiet)
            {
                if (call.Callout is { } callout
                    && D47.Core.Callouts.FlavourBriefs.For(callout, personalityEnabled: true, shared.GameState.Active?.Identity.Name) is null)
                {
                    Log($"{call.Id}: skipped, the app says it as written");
                    continue;
                }

                foreach (var model in models)
                {
                    for (var run = 1; run <= quietRuns; run++)
                    {
                        if (meter.Spent >= cap)
                        {
                            Log("stopped: the cap is reached");
                            return;
                        }

                        Write(await runner.RunQuietAsync(call, shared, model, run, Token));
                    }
                }
            }
        }

        Log($"done; spent ${meter.Spent:0.0000}");
    }

    /// <summary>Every journal line in the folder, oldest file first, that parses.</summary>
    private static List<JournalEvent> Journal(string folder)
    {
        var events = new List<JournalEvent>();

        foreach (var file in Directory.EnumerateFiles(folder, "Journal.*.log").Order(StringComparer.Ordinal))
        {
            foreach (var line in File.ReadLines(file))
            {
                if (line.Length > 0 && JournalEvent.TryParse(line, NullLogger.Instance, out var parsed) && parsed is not null)
                {
                    events.Add(parsed);
                }
            }
        }

        return events;
    }

    /// <summary>
    /// The Anthropic provider, through the app's own factory. The key comes from <c>D47_COMPARE_KEY</c>, or
    /// from d47's own encrypted store under <c>D47_COMPARE_SECRETS_FROM</c>, read in this process and never
    /// written anywhere.
    /// </summary>
    private static ILlmProvider Compose()
    {
        var info = LlmProviderCatalog.Find(LlmProviderCatalog.AnthropicId)
                   ?? throw new InvalidOperationException("No Anthropic provider in the catalog.");

        var key = Env("D47_COMPARE_KEY");

        if (key is null && Env("D47_COMPARE_SECRETS_FROM") is { } root)
        {
            var store = new SecretStore(new AppPaths(root), new DpapiSecretProtector(), NullLogger<SecretStore>.Instance);
            key = store.TryGet(info.KeySecretName!, out var stored) ? stored : null;
        }

        return LlmProviderFactory.Create(info, key ?? throw new InvalidOperationException(
                   "No key: set D47_COMPARE_KEY or D47_COMPARE_SECRETS_FROM."), info.DefaultEndpoint)
               ?? throw new InvalidOperationException(LlmProviderFactory.ReasonForNoClient(info));
    }

    /// <summary>A fixed picture of the screen, from a JPEG on disk.</summary>
    private sealed class FileScreen(string path) : IScreenCapture
    {
        public ScreenCaptureResult Take()
        {
            var jpeg = File.ReadAllBytes(path);
            var (width, height) = JpegSize(jpeg);
            return new ScreenCaptureResult(new ScreenPicture(jpeg, width, height, ScreenPictures.FromWindow), null);
        }

        /// <summary>The size in a JPEG's start-of-frame segment.</summary>
        private (int Width, int Height) JpegSize(byte[] jpeg)
        {
            var at = 2;

            while (at + 9 < jpeg.Length)
            {
                if (jpeg[at] != 0xFF)
                {
                    at++;
                    continue;
                }

                var marker = jpeg[at + 1];
                var length = (jpeg[at + 2] << 8) | jpeg[at + 3];

                if (marker is >= 0xC0 and <= 0xC3)
                {
                    return ((jpeg[at + 7] << 8) | jpeg[at + 8], (jpeg[at + 5] << 8) | jpeg[at + 6]);
                }

                at += 2 + length;
            }

            throw new InvalidOperationException($"{path} is not a JPEG with a frame header.");
        }
    }
}
