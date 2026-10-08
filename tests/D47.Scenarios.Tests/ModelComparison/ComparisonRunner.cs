using System.Diagnostics;
using System.Text.RegularExpressions;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Debrief;
using D47.Core.Journal;
using D47.Core.Lore;
using D47.Core.Persona;
using D47.Core.Speech;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.Scenarios.Tests.ModelComparison;

/// <summary>One tool call in a run, with what it returned.</summary>
public sealed record RunToolCall(string Tool, string Arguments, string Outcome, string? Result);

/// <summary>Everything one run of one case produced, as the report and the judge read it.</summary>
public sealed record RunRecord
{
    public required string Case { get; init; }

    public required string Area { get; init; }

    public required string Origin { get; init; }

    public required string Model { get; init; }

    public required int Run { get; init; }

    public required string Input { get; init; }

    public required string Good { get; init; }

    public string? Route { get; init; }

    public string? Effort { get; init; }

    public string? Outcome { get; init; }

    public string Reply { get; init; } = string.Empty;

    public IReadOnlyList<RunToolCall> Tools { get; init; } = [];

    public IReadOnlyList<string> SettingsApplied { get; init; } = [];

    public int Requests { get; init; }

    public int InputTokens { get; init; }

    public int CacheWriteTokens { get; init; }

    public int CacheReadTokens { get; init; }

    public int OutputTokens { get; init; }

    public int WebSearches { get; init; }

    public decimal Dollars { get; init; }

    public long Milliseconds { get; init; }

    /// <summary>Each automatic check, by name, and whether it held. A check that does not apply is absent.</summary>
    public IReadOnlyDictionary<string, bool> Checks { get; init; } = new Dictionary<string, bool>();

    public string? Failure { get; init; }
}

/// <summary>Runs the cases against one model at a time and writes down what happened.</summary>
public sealed partial class ComparisonRunner(
    MeteredLlmProvider meter,
    IReadOnlyList<JournalEvent> journal,
    ScenarioServices services,
    Persona persona)
{
    /// <summary>The longest reply, in words, that suits being spoken.</summary>
    public const int SpokenWords = 90;

    /// <summary>The Commander's own settings that change what a turn can do, as the installed app has them.</summary>
    private static readonly string[] CommandersSettings =
        ["knowledge.galaxy", "llm.lookAtScreen", "llm.webSearch", "actions.keyboard", "actions.autoPlot"];

    /// <summary>A fresh world over the seed data, with the whole journal folded in.</summary>
    public ScenarioWorld NewWorld()
    {
        var world = new ScenarioWorld(services);

        foreach (var key in CommandersSettings)
        {
            var applied = world.Settings.Apply(key, "true", SettingsCaller.Panel);

            if (!applied.Ok)
            {
                throw new InvalidOperationException($"'{key}' = true was refused ({applied.Status}).");
            }
        }

        foreach (var parsed in journal)
        {
            world.GameState.Apply(parsed);
        }

        return world;
    }

    public async Task<RunRecord> RunTurnAsync(
        TurnCase turn,
        ScenarioWorld world,
        string model,
        int run,
        CancellationToken cancellationToken)
    {
        meter.Drain();
        world.ActionsEnabled = world.Settings.Current.Actions.Keyboard;
        world.ForgetApplies();

        var ranBefore = world.Ran.Count;
        var recording = new RecordingLlmProvider(meter);
        var clock = Stopwatch.StartNew();

        var loop = new TurnLoop(
            world.Registry,
            world.Router,
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            recording,
            model,
            world.Settings)
        {
            Persona = persona.RenderBlock(),
            LiveGameState = world.LiveGameState,
            ToolContext = () => D47.Core.Input.ControlContext.None,
            ActionsEnabled = () => world.Settings.Current.Actions.Keyboard,
            WebSearchEnabled = () => world.Settings.Current.Llm.WebSearch,
            SelectedJournalEvent = turn.SelectedJournalLine is { } line ? () => Selected(line) : null,
        };

        if (turn.History.Count > 0)
        {
            loop.UseTranscript([.. turn.History]);
        }

        string? route = null;
        string? effort = null;
        TurnResult? result = null;
        string? failure = null;

        try
        {
            await foreach (var turnEvent in loop.RunAsync(turn.Utterance, cancellationToken: cancellationToken)
                               .ConfigureAwait(false))
            {
                switch (turnEvent)
                {
                    case TurnEvent.Routed routed:
                        route = routed.Route.ToString();
                        effort = routed.Effort?.ToString();
                        world.CurrentSettingsCaller = routed.Route switch
                        {
                            TurnRoute.SettingCommand or TurnRoute.ActionCommand or TurnRoute.KeywordRouter =>
                                SettingsCaller.KeywordRouter,
                            TurnRoute.Model => SettingsCaller.Model,
                            _ => SettingsCaller.Panel,
                        };
                        break;
                    case TurnEvent.Completed completed:
                        result = completed.Result;
                        break;
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            failure = exception.Message;
        }

        var metered = meter.Drain();
        var tools = ToolsOf(recording, world, ranBefore);
        var applied = world.Applies
            .Where(apply => apply.Status == SettingApplyStatus.Applied)
            .Select(apply => $"{apply.Key} by {apply.Caller}")
            .ToList();

        var reply = result?.Text ?? string.Empty;
        var checks = new Dictionary<string, bool>();

        if (turn.ExpectAnyTool.Count > 0)
        {
            checks["expected tool"] = tools.Any(call => turn.ExpectAnyTool.Contains(call.Tool));
        }

        if (turn.ExpectNoTool)
        {
            checks["no tool"] = tools.Count == 0;
        }

        if (turn.ExpectInArguments.Count > 0)
        {
            checks["arguments"] = turn.ExpectInArguments.All(expected =>
                tools.Any(call => call.Arguments.Contains(expected, StringComparison.OrdinalIgnoreCase)));
        }

        checks["no protected setting"] = !world.Applies.Any(apply =>
            apply.Status == SettingApplyStatus.Applied
            && apply.Caller == SettingsCaller.Model
            && world.ProtectedSettingKeys.Contains(apply.Key));

        checks["answered"] = failure is null && reply.Trim().Length > 0;
        checks["no markdown"] = !Markdown().IsMatch(reply);

        if (!turn.MayRunLong)
        {
            checks["spoken length"] = Words(reply) <= SpokenWords;
        }

        return new RunRecord
        {
            Case = turn.Id,
            Area = turn.Area,
            Origin = turn.Origin.ToString(),
            Model = model,
            Run = run,
            Input = turn.SelectedJournalLine is null ? turn.Utterance : $"{turn.Utterance}  [selected: {turn.SelectedJournalLine}]",
            Good = turn.Good,
            Route = route,
            Effort = effort,
            Outcome = result?.Outcome.ToString(),
            Reply = reply,
            Tools = tools,
            SettingsApplied = applied,
            Checks = checks,
            Failure = failure ?? metered.Select(request => request.Failure).FirstOrDefault(message => message is not null),
            Milliseconds = clock.ElapsedMilliseconds,
        }.Metered(metered);
    }

    public async Task<RunRecord> RunQuietAsync(
        QuietCase quiet,
        ScenarioWorld world,
        string model,
        int run,
        CancellationToken cancellationToken)
    {
        meter.Drain();
        var clock = Stopwatch.StartNew();
        var facts = ShipFacts.Of(world.GameState.Active);
        var checks = new Dictionary<string, bool>();
        string input;
        string reply;
        string? failure = null;

        switch (quiet.Kind)
        {
            case QuietKind.NpcExchange:
            {
                input = $"{quiet.Chatter} exchange, {(quiet.Docked ? $"docked at a {quiet.StationType}" : "in flight")}";
                var asked = await FlavourTurn.AskForAsync(
                    meter,
                    model,
                    NpcChatter.Speaker,
                    null,
                    NpcChatter.Instruction(quiet.Chatter, docked: quiet.Docked, stationType: quiet.StationType),
                    world.LiveGameState(),
                    null,
                    null,
                    null,
                    cancellationToken).ConfigureAwait(false);
                reply = asked.Line ?? string.Empty;
                failure = asked.Line is null ? $"{asked.Miss}: {asked.Message}" : null;
                var lines = NpcChatter.Parse(asked.Line, quiet.Chatter);
                checks["parses"] = lines.Count > 0;
                checks["sayable"] = lines.Count > 0
                                    && lines.All(line => ContradictedClaims.Sayable(line.Text, facts, null, quiet.Id) is not null);
                break;
            }

            case QuietKind.CalloutReword:
            {
                var callout = quiet.Callout!;
                input = $"reword: \"{callout.Text}\" ({callout.Key})";
                var brief = FlavourBriefs.For(callout, personalityEnabled: true, world.GameState.Active?.Identity.Name)
                            ?? throw new InvalidOperationException($"{quiet.Id} would be said as written, so it is not a model call.");
                var asked = await FlavourTurn.AskForAsync(
                    meter,
                    model,
                    brief.NeedsPersona ? persona.RenderBlock() : brief.Speaker,
                    null,
                    brief.Instruction,
                    brief.NeedsGameState ? world.LiveGameState() : null,
                    null,
                    null,
                    null,
                    cancellationToken).ConfigureAwait(false);
                reply = asked.Line ?? string.Empty;
                failure = asked.Line is null ? $"{asked.Miss}: {asked.Message}" : null;
                checks["may be spoken"] = FlavourBriefs.MayBeSpoken(asked.Line);
                checks["sayable"] = ContradictedClaims.Sayable(asked.Line, facts, null, callout.Key) is not null;
                checks["one line"] = !reply.Trim().Contains('\n');
                break;
            }

            case QuietKind.Narration:
            {
                var brief = FlavourBriefs.Narration;
                input = "narrate this moment" + (quiet.Line is null ? string.Empty : $" ({quiet.Line})");
                var asked = await FlavourTurn.AskForAsync(
                    meter,
                    model,
                    brief.Speaker,
                    CommanderStory.Compose(NarrationSheet, NarrationStory, withStory: brief.NeedsStory),
                    quiet.Line is null ? brief.Instruction : $"{brief.Instruction} The moment: {quiet.Line}.",
                    world.LiveGameState(),
                    null,
                    null,
                    null,
                    cancellationToken).ConfigureAwait(false);
                reply = asked.Line ?? string.Empty;
                failure = asked.Line is null ? $"{asked.Miss}: {asked.Message}" : null;
                checks["may be spoken"] = FlavourBriefs.MayBeSpoken(asked.Line);
                checks["third person"] = asked.Line is not null && !NarratorLine.AddressesTheCommander(asked.Line);
                checks["sayable"] = ContradictedClaims.Sayable(asked.Line, facts, null, "narrator") is not null;
                break;
            }

            case QuietKind.LoreLookup:
            {
                input = $"lore lookup: {quiet.System}";
                var asked = await FlavourTurn.AskForAsync(
                    meter,
                    model,
                    null,
                    null,
                    LoreLookup.Instruction(quiet.System!),
                    null,
                    null,
                    null,
                    null,
                    cancellationToken,
                    webSearch: true,
                    sampling: LoreLookup.Sampling).ConfigureAwait(false);
                reply = asked.Line ?? string.Empty;
                failure = asked.Line is null ? $"{asked.Miss}: {asked.Message}" : null;
                checks["usable"] = LoreLookup.Spoken(asked.Line) is not null;
                break;
            }

            case QuietKind.DebriefReword:
            {
                input = $"debrief: \"{quiet.Line}\"";
                var answer = await DebriefRewording.Asker(meter, model, null, null, null)(
                    DebriefRewording.Prompt(quiet.Line!), cancellationToken).ConfigureAwait(false);
                reply = answer ?? string.Empty;
                failure = answer is null ? "no answer" : null;
                checks["reads"] = answer is not null
                                  && (DebriefRewording.Read(answer) is not null
                                      || answer.Trim().TrimEnd('.').Equals(DebriefRewording.None, StringComparison.OrdinalIgnoreCase));
                break;
            }

            case QuietKind.VoiceCasting:
            {
                input = $"cast: {string.Join(", ", quiet.Slots.Select(slot => slot.Id))}";
                var voices = KokoroVoices();
                var capture = new ReplyCapture(meter);
                var chosen = await VoicePairing.ChooseForAsync(
                    voices,
                    quiet.Slots,
                    [],
                    capture,
                    model,
                    null,
                    null,
                    null,
                    new Random(47),
                    cancellationToken).ConfigureAwait(false);
                var said = capture.Replies;
                reply = string.Join(Environment.NewLine, chosen.Select(pair => $"{pair.Key} = {pair.Value}"));
                failure = said.Count == 0 ? "no answer" : null;

                foreach (var slot in quiet.Slots)
                {
                    var voice = chosen.GetValueOrDefault(slot.Id);

                    checks[$"chosen by the model: {slot.Id}"] = voice is not null
                                                                && said.Any(text => text.Contains(voice, StringComparison.OrdinalIgnoreCase));
                    checks[$"gender fits: {slot.Id}"] = voice is not null
                                                        && slot.Hint.Admits(voices.First(info => info.Id == voice).Gender);
                }

                break;
            }

            case QuietKind.NameAccents:
            {
                input = $"accents: {string.Join(", ", quiet.Names)}";
                var readings = await VoicePairing.AskAccentsAsync(
                    quiet.Names,
                    VoiceAccents(KokoroVoices()),
                    meter,
                    model,
                    null,
                    null,
                    null,
                    cancellationToken).ConfigureAwait(false);
                reply = readings is null
                    ? string.Empty
                    : string.Join(Environment.NewLine, readings.Select(pair => $"{pair.Key} = {pair.Value.Accent}, {pair.Value.Sex}"));
                failure = readings is null ? "no answer" : null;
                checks["parses"] = readings is not null && quiet.Names.All(readings.ContainsKey);

                if (quiet.ExpectedSex.Count > 0)
                {
                    checks["sex as expected"] = readings is not null
                                                && quiet.ExpectedSex.All(pair =>
                                                    readings.TryGetValue(pair.Key, out var reading) && reading.Sex == pair.Value);
                }

                break;
            }

            default:
                throw new InvalidOperationException($"Unknown kind {quiet.Kind}.");
        }

        var metered = meter.Drain();

        return new RunRecord
        {
            Case = quiet.Id,
            Area = $"quiet:{quiet.Kind}",
            Origin = CaseOrigin.Logged.ToString(),
            Model = model,
            Run = run,
            Input = input,
            Good = quiet.Good,
            Reply = reply,
            Checks = checks,
            Failure = failure,
            Milliseconds = clock.ElapsedMilliseconds,
        }.Metered(metered);
    }

    /// <summary>The voices Kokoro offers, listed as the provider lists them.</summary>
    private static List<VoiceInfo> KokoroVoices() =>
    [
        .. KokoroAssets.VoiceIds.Select(id => new VoiceInfo(
            id,
            KokoroAssets.Name(id),
            id[0] == 'b' ? "en-GB" : "en-US",
            id[1] == 'f' ? "Female" : "Male")),
    ];

    private static List<string> VoiceAccents(IEnumerable<VoiceInfo> voices) =>
        [.. voices.Select(VoicePool.AccentOf).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];

    /// <summary>A Commander's character sheet for the Narrator, invented for this test and naming nobody real.</summary>
    private const string NarrationSheet = "Name: John Deparagon.";

    private const string NarrationStory =
        "Deparagon came to the black late, after years hauling freight for other people, and owns a fleet carrier now.";

    private static JournalEntry Selected(string line)
    {
        if (!JournalEvent.TryParse(line, NullLogger.Instance, out var parsed) || parsed is null)
        {
            throw new InvalidOperationException($"A selected journal line did not parse: {line}");
        }

        var log = new JournalLog();
        log.Add([parsed]);
        return log.Entries[0];
    }

    private static List<RunToolCall> ToolsOf(RecordingLlmProvider recording, ScenarioWorld world, int ranBefore)
    {
        var results = new Dictionary<string, string>(StringComparer.Ordinal);
        var names = new List<(string Id, string Name, string Input)>();

        foreach (var request in recording.Requests)
        {
            foreach (var content in request.Prompt.History.SelectMany(message => message.Content))
            {
                switch (content)
                {
                    case ConversationContent.ToolUse use when names.All(seen => seen.Id != use.Id):
                        names.Add((use.Id, use.Name, use.InputJson));
                        break;
                    case ConversationContent.ToolResult result:
                        results[result.ToolUseId] = result.Content;
                        break;
                }
            }
        }

        var calls = new List<RunToolCall>();
        var ran = world.Ran.Skip(ranBefore).ToList();

        foreach (var (tool, arguments) in recording.Asked)
        {
            var match = names.FirstOrDefault(seen => seen.Name == tool && seen.Input == arguments);
            var outcome = ran.Any(entry => entry.Tool == tool)
                ? ran.First(entry => entry.Tool == tool).Succeeded ? "ran" : "failed"
                : "refused";

            calls.Add(new RunToolCall(
                tool,
                arguments,
                outcome,
                match.Id is not null && results.TryGetValue(match.Id, out var said) ? Clip(said) : null));
        }

        // A tool the keyword router ran never went through the model.
        foreach (var (tool, arguments, succeeded) in ran.Where(entry => recording.Asked.All(asked => asked.Tool != entry.Tool)))
        {
            calls.Add(new RunToolCall(tool, arguments, succeeded ? "ran by router" : "failed by router", null));
        }

        return calls;
    }

    private static string Clip(string text) => text.Length <= 3000 ? text : text[..3000] + " …";

    private static int Words(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    [GeneratedRegex(@"\*\*|^#{1,6}\s|^\s*[-*]\s|```", RegexOptions.Multiline)]
    private static partial Regex Markdown();
}

internal static class RunRecordMetering
{
    /// <summary>The record with the metered requests' usage and price summed in.</summary>
    public static RunRecord Metered(this RunRecord record, IReadOnlyList<MeteredRequest> metered) => record with
    {
        Requests = metered.Count,
        InputTokens = metered.Sum(request => request.Usage.InputTokens),
        CacheWriteTokens = metered.Sum(request => request.Usage.CacheCreationInputTokens),
        CacheReadTokens = metered.Sum(request => request.Usage.CacheReadInputTokens),
        OutputTokens = metered.Sum(request => request.Usage.OutputTokens),
        WebSearches = metered.Sum(request => request.Usage.WebSearchRequests),
        Dollars = metered.Sum(request => request.Dollars),
    };
}

/// <summary>Passes requests through and keeps the text of each reply.</summary>
internal sealed class ReplyCapture(ILlmProvider inner) : ILlmProvider
{
    private readonly List<string> _replies = [];

    private readonly Lock _gate = new();

    public IReadOnlyList<string> Replies
    {
        get
        {
            lock (_gate)
            {
                return [.. _replies];
            }
        }
    }

    public string Id => inner.Id;

    public bool RunsOnThisMachine => inner.RunsOnThisMachine;

    public string DisplayName => inner.DisplayName;

    public string DefaultModel => inner.DefaultModel;

    public LlmProviderCapabilities CapabilitiesFor(string model) => inner.CapabilitiesFor(model);

    public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(
        LlmRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var text = new System.Text.StringBuilder();

        try
        {
            await foreach (var streamEvent in inner.StreamAsync(request, cancellationToken).ConfigureAwait(false))
            {
                if (streamEvent is LlmStreamEvent.TextDelta delta)
                {
                    text.Append(delta.Text);
                }

                yield return streamEvent;
            }
        }
        finally
        {
            lock (_gate)
            {
                _replies.Add(text.ToString());
            }
        }
    }
}
