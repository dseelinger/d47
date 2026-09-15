using System.Runtime.CompilerServices;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace D47.Core.Conversation;

public enum TurnRoute
{
    /// <summary>Answered by the model-free keyword router.</summary>
    KeywordRouter,

    /// <summary>A settings change made by the model-free router.</summary>
    SettingCommand,

    /// <summary>A game action performed by the model-free router from a declared phrase.</summary>
    ActionCommand,

    /// <summary>An answer to the offer d47 left standing.</summary>
    Offer,

    /// <summary>Answered by the language model.</summary>
    Model,

    /// <summary>Nothing could answer it.</summary>
    NoCapability,
}

public enum TurnOutcome
{
    Answered,

    /// <summary>An explicit result, not a score.</summary>
    Unsure,

    Failed,
}

public sealed record TurnResult(
    TurnOutcome Outcome,
    TurnRoute Route,
    string Text,
    ThinkingEffort? Effort,
    TurnCost? Cost);

public abstract record TurnEvent
{
    private TurnEvent()
    {
    }

    /// <summary>Emitted as soon as routing is decided, before any work.</summary>
    public sealed record Routed(TurnRoute Route, ThinkingEffort? Effort) : TurnEvent;

    public sealed record TextDelta(string Text) : TurnEvent;

    public sealed record ThinkingDelta(string Text) : TurnEvent;

    /// <summary>About to try again after a transient failure.</summary>
    public sealed record Retrying(int Attempt, int Of, TimeSpan Wait, string Because) : TurnEvent;

    /// <summary>A tool the model asked for is about to run.</summary>
    public sealed record ToolStarted(string Tool) : TurnEvent;

    /// <summary>A tool the model asked for has finished.</summary>
    public sealed record ToolFinished(string Tool, bool Succeeded) : TurnEvent;

    public sealed record Completed(TurnResult Result) : TurnEvent;
}

/// <summary>One turn, start to finish.</summary>
public sealed class TurnLoop(
    CapabilityRegistry capabilities,
    KeywordRouter keywordRouter,
    LlmAvailabilityState availability,
    SpendTracker spend,
    PriceTable prices,
    ILogger<TurnLoop> logger,
    ILlmProvider? provider = null,
    string? model = null,
    SettingsService? settings = null,
    ITurnClock? clock = null,
    OfferWindow? offers = null)
{
    private readonly ITurnClock _clock = clock ?? SystemTurnClock.Instance;

    private List<ConversationMessage> _history = [];

    private string? _lastModelUsed;

    /// <summary>An offer to learn a phrase, waiting on the Commander's next reply (#169).</summary>
    private (string Said, string Phrase)? _pendingLearn;

    /// <summary>Utterances declined for learning this session, reduced (#169).</summary>
    private readonly HashSet<string> _declinedLearn = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>How hard to try before saying so out loud.</summary>
    public RetryPolicy Retry { get; set; } = RetryPolicy.Default;

    /// <summary>
    /// Business the Commander has left open, in one line, or null when there is none (remediation.md
    /// 10, item 10).
    /// </summary>
    public Func<string?>? Standing { get; set; }

    /// <summary>
    /// Told when <see cref="Standing"/>'s line was actually appended, so the thing that wrote it can
    /// decay it (#154).
    /// </summary>
    public Action? StandingSaid { get; set; }

    /// <summary>How many times in one turn the model may ask for tools and be answered.</summary>
    public int MaxToolRounds { get; set; } = 8;

    public IReadOnlyList<ConversationMessage> History => _history;

    /// <summary>
    /// Records something d47 said without being asked, so the next turn knows it said it
    /// (remediation.md 17, item 4).
    /// </summary>
    public void Said(string line, string? asked = null)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        lock (_spokenLock)
        {
            SpokenLine added = new(
                string.IsNullOrWhiteSpace(asked) ? null : asked.Trim(),
                line.Trim());

            _spoken.Add(added);

            // A Commander who does not ask anything for an hour is not owed every ambient line of it.
            bool SameKind(SpokenLine entry) => (entry.Asked is null) == (added.Asked is null);

            while (_spoken.Count(SameKind) > SpokenCarried)
            {
                _spoken.RemoveAt(_spoken.FindIndex(SameKind));
            }
        }
    }

    /// <summary>The block that carries those lines into the turn, and empties itself doing it.</summary>
    private string Spoken()
    {
        List<SpokenLine> said;

        lock (_spokenLock)
        {
            if (_spoken.Count == 0)
            {
                return string.Empty;
            }

            said = [.. _spoken];
            _spoken.Clear();
        }

        List<string> parts = [];

        if (said.Where(entry => entry.Asked is null).ToList() is { Count: > 0 } unprompted)
        {
            parts.Add(
                "Since the last exchange you spoke these lines to the Commander without "
                + "being asked. They are your own words, and the Commander heard them.\n"
                + string.Join('\n', unprompted.Select(entry => $"- {entry.Line}")));
        }

        if (said.Where(entry => entry.Asked is not null).ToList() is { Count: > 0 } exchanges)
        {
            parts.Add(
                "Since the last exchange the Commander asked for these and one of your own "
                + "commands answered, without you being consulted. They are exchanges that "
                + "happened, not lines you spoke unprompted.\n"
                + string.Join(
                    '\n',
                    exchanges.Select(entry =>
                        $"- The Commander said \"{entry.Asked}\", and you answered \"{entry.Line}\"."))
                + $"\nThe last of those — \"{exchanges[^1].Asked}\" — was the Commander's most "
                + "recent request before the words that follow in this message, and they are a "
                + "follow-up to it rather than to any earlier request."
                + "\nEach line above is what was true when you said it, and nothing more. Where a "
                + "tool answers on this turn, its answer is what is true now and replaces anything "
                + "above it — including a refusal, which describes one attempt and never what you "
                + "are able to do.");
        }

        return "<said-aloud>\n" + string.Join('\n', parts) + "\n</said-aloud>\n\n";
    }

    /// <summary>
    /// One line d47 spoke, and the Commander's own words that asked for it, or null where nothing did
    /// (#415).
    /// </summary>
    private sealed record SpokenLine(string? Asked, string Line);

    /// <summary>How many lines of each kind are carried into the next turn.</summary>
    private const int SpokenCarried = 8;

    /// <summary>How many messages of one persona's transcript are kept (remediation.md 17, item 4).</summary>
    public const int TranscriptKept = 80;

    /// <summary>
    /// Trims the transcript to <see cref="TranscriptKept"/>, never leaving a tool call whose result was
    /// dropped with it.
    /// </summary>
    private void Bound()
    {
        if (_history.Count <= TranscriptKept)
        {
            return;
        }

        var cut = _history.Count - TranscriptKept;

        // Forward past any message that would leave a dangling half: a tool result whose call is being
        // dropped, the assistant call it belongs to, and a provider block tied to either.
        while (cut < _history.Count
               && _history[cut].Content.Any(part => part is ConversationContent.ToolResult
                                                     or ConversationContent.ToolUse
                                                     or ConversationContent.Opaque))
        {
            cut++;
        }

        // A transcript that is all one enormous tool conversation would otherwise be emptied.
        if (cut >= _history.Count)
        {
            return;
        }

        _history.RemoveRange(0, cut);
    }

    private readonly List<SpokenLine> _spoken = [];

    /// <summary>
    /// Callouts are spoken from the tick loop and turns run on their own thread, so the two genuinely
    /// race.
    /// </summary>
    private readonly Lock _spokenLock = new();

    /// <summary>Points the loop at a different transcript.</summary>
    public void UseTranscript(List<ConversationMessage> transcript) => _history = transcript;

    /// <summary>The provider answering turns, or null for none.</summary>
    public ILlmProvider? Provider { get; set; } = provider;

    /// <summary>The pinned model, or null for the provider's own default.</summary>
    public string? Model { get; set; } = model;

    /// <summary>
    /// The model for calls the Commander is not waiting on — ambient remarks, the opening brief, the
    /// gap reaction, the two lore lookups, voice casting (Phase 54).
    /// </summary>
    public string? BackgroundModel { get; set; } = model;

    /// <summary>
    /// The least effort a turn may run at, or null for whatever <see cref="EffortRouter"/> answered
    /// (Phase 54).
    /// </summary>
    public ThinkingEffort? EffortFloor { get; set; }

    /// <summary>
    /// The most effort a turn may run at, or null for whatever <see cref="EffortRouter"/> answered
    /// (Phase 54).
    /// </summary>
    public ThinkingEffort? EffortCeiling { get; set; }

    /// <summary>The persona block, or null for "personality off".</summary>
    public string? Persona { get; set; }

    /// <summary>
    /// What this Commander's transcriber reliably gets wrong, applied to a spoken utterance before
    /// anything reads it (#134).
    /// </summary>
    public Func<string, string>? Heard { get; set; }

    /// <summary>
    /// The phrase this Commander has taught d47 an utterance stands for, or null for nothing learned.
    /// Applied to typed and spoken input alike after <see cref="Heard"/>, and only when the phrase is still
    /// in <see cref="KeywordRouter.Book"/> (#169).
    /// </summary>
    public Func<string, string?>? LearnedPhraseFor { get; set; }

    /// <summary>The Frontier id of whoever is flying, or null when none is known (#169).</summary>
    public Func<string?>? CommanderId { get; set; }

    /// <summary>Records that an utterance stands for a phrase, for whoever is flying (#169).</summary>
    public Action<string, string>? LearnPhrase { get; set; }

    /// <summary>
    /// The choices put to the Commander, read before any other route. Shared with a capability that
    /// opens its own offer directly, such as <see cref="Capabilities.Builtin.HelpCapability"/> (#168).
    /// </summary>
    public OfferWindow Offers { get; } = offers ?? new();

    public string? AboutMe { get; set; }

    /// <summary>
    /// What d47 remembers about the Commander, already bounded and labelled by <see
    /// cref="Memory.MemoryRecall"/> (Phase 31).
    /// </summary>
    public string? Recall { get; set; }

    /// <summary>Position 6 — the standing directions the Commander adopted by hand (#162).</summary>
    public string? Directions { get; set; }

    /// <summary>
    /// Whether the voice that will speak this turn performs delivery direction, and so whether the
    /// model is told it may write any (<see cref="PromptAssembly.CanBeDirected"/>, #291).
    /// </summary>
    public Func<bool>? CanBeDirected { get; set; }

    /// <summary>Live game state for the turn about to run.</summary>
    public Func<string?>? LiveGameState { get; set; }

    /// <summary>The mode the Commander is in, for choosing the tool profile.</summary>
    public Func<Input.ControlContext>? ToolContext { get; set; }

    /// <summary>Whether the Commander has allowed d47 to press keys.</summary>
    public Func<bool>? ActionsEnabled { get; set; }

    /// <summary>Whether the Commander has allowed the model to search the web.</summary>
    public Func<bool>? WebSearchEnabled { get; set; }

    public async IAsyncEnumerable<TurnEvent> RunAsync(
        string input,
        InputSource source = InputSource.Typed,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        availability.BeginTurn();

        // 0.
        if (source == InputSource.Spoken && Heard is { } heard)
        {
            var corrected = heard(input);

            if (!string.Equals(corrected, input, StringComparison.Ordinal))
            {
                logger.LogInformation(
                    "Heard \"{Said}\" and read it as \"{Meant}\"", input, corrected);

                input = corrected;
            }
        }

        if (LearnedPhraseFor?.Invoke(input) is { } taught && InBook(taught))
        {
            logger.LogInformation("Read \"{Said}\" as the learned phrase \"{Phrase}\"", input, taught);
            input = taught;
        }

        if (_pendingLearn is { } pending)
        {
            _pendingLearn = null;

            if (IsAffirmative(input))
            {
                LearnPhrase?.Invoke(pending.Said, pending.Phrase);

                foreach (var turnEvent in Offered("Learned.", input))
                {
                    yield return turnEvent;
                }
            }
            else
            {
                _declinedLearn.Add(KeywordRouter.Utterance(pending.Said));

                foreach (var turnEvent in Offered("Dropped.", input))
                {
                    yield return turnEvent;
                }
            }

            yield break;
        }

        switch (Offers.Read(input))
        {
            case OfferReading.Picked { Choice.Target: OfferTarget.RoutePhrase route }:
                var picked = new Routing();
                TurnResult? ran = null;

                await foreach (var turnEvent in ModelFreeAsync(route.Phrase, source, picked, cancellationToken)
                                   .ConfigureAwait(false))
                {
                    if (turnEvent is TurnEvent.Completed completed)
                    {
                        ran = completed.Result;
                        continue;
                    }

                    yield return turnEvent;
                }

                if (!picked.Handled)
                {
                    foreach (var turnEvent in Offered("That isn't available right now.", input, TurnOutcome.Unsure))
                    {
                        yield return turnEvent;
                    }

                    yield break;
                }

                if (route.Said is { } said && ShouldOfferToLearn(said, route.Phrase))
                {
                    _pendingLearn = (said, route.Phrase);

                    var question = $"Want me to remember '{said}' as another way to say that?";
                    var text = $"{ran!.Text} {question}";

                    yield return new TurnEvent.TextDelta(" " + question);
                    yield return new TurnEvent.Completed(ran with { Text = text });
                    yield break;
                }

                yield return new TurnEvent.Completed(ran!);
                yield break;

            case OfferReading.Picked { Choice.Target: OfferTarget.Answer answer }:
                var answered = answer.Respond();

                if (answered.Next is { } next)
                {
                    Offers.Open(next);
                }

                foreach (var turnEvent in Offered(answered.Text, input))
                {
                    yield return turnEvent;
                }

                yield break;

            case OfferReading.Declined:
                foreach (var turnEvent in Offered("Dropped.", input))
                {
                    yield return turnEvent;
                }

                yield break;

            case OfferReading.Unclear:
                foreach (var turnEvent in Offered("Which one?", input))
                {
                    yield return turnEvent;
                }

                yield break;
        }

        var routed = new Routing();

        await foreach (var turnEvent in ModelFreeAsync(input, source, routed, cancellationToken)
                           .ConfigureAwait(false))
        {
            yield return turnEvent;
        }

        if (routed.Handled)
        {
            yield break;
        }

        // Near misses: one unguarded Equivalent runs, anything else is offered.
        var candidates = keywordRouter.Book.Candidates(input, source);

        if (candidates is [{ Match: PhraseMatch.Equivalent, Guarded: false } only])
        {
            logger.LogInformation("Read \"{Said}\" as the phrase \"{Phrase}\"", input, only.Phrase);

            var ran = new Routing();

            await foreach (var turnEvent in ModelFreeAsync(only.Phrase, source, ran, cancellationToken)
                               .ConfigureAwait(false))
            {
                yield return turnEvent;
            }

            if (ran.Handled)
            {
                yield break;
            }
        }
        else if (candidates.Count > 0)
        {
            List<PhraseCandidate> offered = [.. candidates.Take(OfferedAtMost)];

            logger.LogInformation(
                "Offered {Phrases} for \"{Said}\"", string.Join(", ", offered.Select(c => c.Phrase)), input);

            Offers.Open(new Offer(
            [
                .. offered.Select(candidate => new OfferChoice(
                    candidate.Phrase, new OfferTarget.RoutePhrase(candidate.Phrase, candidate.Guarded, Said: input))),
            ]));

            foreach (var turnEvent in Offered(DidYouMean(offered), input))
            {
                yield return turnEvent;
            }

            yield break;
        }

        // 3.5. "How do I ...": land on a feature by the words that reach it, rather than the model
        // (#170). A goal matching nothing falls through to the model below when one is configured, and
        // to the top level of the spoken map when none is.
        if (Help.HowDoI.GoalFor(input) is { } goal)
        {
            var matched = Help.HowDoI.Match(goal, capabilities);

            if (matched.Count == 1)
            {
                var text = Capabilities.Builtin.HelpCapability.Answer(matched[0], capabilities).Text;

                foreach (var turnEvent in AnsweredByRouter(text, input))
                {
                    yield return turnEvent;
                }

                yield break;
            }

            if (matched.Count is 2 or 3)
            {
                Offers.Open(new Offer(
                [
                    .. matched.Select(node => new OfferChoice(
                        node.Name,
                        new OfferTarget.Answer(() => Capabilities.Builtin.HelpCapability.Answer(node, capabilities)))),
                ]));

                foreach (var turnEvent in Offered(Ways(matched), input))
                {
                    yield return turnEvent;
                }

                yield break;
            }

            if (matched.Count is 0 or > 3 && (Provider is null || !availability.CanAttemptModelTurn))
            {
                var (text, offer) = Capabilities.Builtin.HelpCapability.LevelAnswer(Help.HelpTaxonomy.Top, capabilities);
                Offers.Open(offer);

                foreach (var turnEvent in AnsweredByRouter(text, input))
                {
                    yield return turnEvent;
                }

                yield break;
            }
        }

        // 4.
        var activeProvider = Provider;

        if (activeProvider is null || !availability.CanAttemptModelTurn)
        {
            var reason = availability.Reason ?? "No language model provider is configured.";
            logger.LogInformation("No model available for this turn: {Reason}", reason);

            var text = NoCapabilityText(input, reason);

            yield return new TurnEvent.Routed(TurnRoute.NoCapability, Effort: null);
            yield return new TurnEvent.TextDelta(text);
            yield return new TurnEvent.Completed(new TurnResult(
                TurnOutcome.Unsure, TurnRoute.NoCapability, text, Effort: null, Cost: null));
            yield break;
        }

        await foreach (var turnEvent in RunModelTurnAsync(input, activeProvider, cancellationToken)
                           .ConfigureAwait(false))
        {
            yield return turnEvent;
        }
    }

    /// <summary>How many near misses one offer names.</summary>
    private const int OfferedAtMost = 3;

    /// <summary>"Did you mean 'a'?", "Did you mean 'a', or 'b'?", and so on.</summary>
    private static string DidYouMean(IReadOnlyList<PhraseCandidate> offered)
    {
        var quoted = offered.Select(candidate => $"'{candidate.Phrase}'").ToList();

        return quoted.Count == 1
            ? $"Did you mean {quoted[0]}?"
            : $"Did you mean {string.Join(", ", quoted.SkipLast(1))}, or {quoted[^1]}?";
    }

    /// <summary>
    /// With no model, names the #166 area nearest the utterance and offers its list, or says there is
    /// nothing for it when no leaf shares enough words (#172).
    /// </summary>
    private string NoCapabilityText(string input, string reason)
    {
        var matched = Help.HowDoI.Match(input, capabilities);

        if (matched.Count == 0)
        {
            return $"I have nothing for that. Say 'what can you do' for the list. {reason}";
        }

        var category = Help.HelpTaxonomy.CategoryOf(matched[0]) ?? matched[0];

        Offers.Open(new Offer(
        [
            new OfferChoice(
                category.Name,
                new OfferTarget.Answer(() => Capabilities.Builtin.HelpCapability.Answer(category, capabilities))),
        ]));

        return $"I have nothing for that. The closest area is {category.Name}; want the list? {reason}";
    }

    /// <summary>"Two ways: 'a', or 'b'. Which one?" for a "how do I" that matched two or three leaves.</summary>
    private static string Ways(IReadOnlyList<Help.HelpNode> matched)
    {
        var names = matched.Select(node => node.Name).ToArray();
        var count = names.Length == 2 ? "Two" : "Three";

        var listed = names.Length == 2
            ? $"{names[0]}, or {names[1]}"
            : $"{names[0]}, {names[1]}, or {names[2]}";

        return $"{count} ways: {listed}. Which one?";
    }

    /// <summary>A model-free answer that is not a reply to a standing offer, such as a "how do I" leaf.</summary>
    private IEnumerable<TurnEvent> AnsweredByRouter(string text, string input)
    {
        yield return new TurnEvent.Routed(TurnRoute.KeywordRouter, Effort: null);

        Said(text, input);

        yield return new TurnEvent.TextDelta(text);
        yield return new TurnEvent.Completed(new TurnResult(
            TurnOutcome.Answered, TurnRoute.KeywordRouter, text, Effort: null, Cost: null));
    }

    /// <summary>Whether a model-free route answered.</summary>
    private sealed class Routing
    {
        public bool Handled { get; set; }
    }

    /// <summary>
    /// Whether a run from a near miss (#167) is worth asking to learn: a Commander is known, the wording
    /// has not already been declined this session, is more than one word, is not already a declared phrase,
    /// and is not already learned for something else (#169).
    /// </summary>
    private bool ShouldOfferToLearn(string said, string phrase)
    {
        if (CommanderId?.Invoke() is not { Length: > 0 })
        {
            return false;
        }

        var reduced = KeywordRouter.Utterance(said);

        if (KeywordRouter.Words(reduced).Length <= 1)
        {
            return false;
        }

        if (_declinedLearn.Contains(reduced))
        {
            return false;
        }

        if (string.Equals(reduced, KeywordRouter.Utterance(phrase), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (InBook(said))
        {
            return false;
        }

        return LearnedPhraseFor?.Invoke(said) is null;
    }

    /// <summary>Whether a phrase (as opposed to an arbitrary utterance) is one the router still declares.</summary>
    private bool InBook(string phrase)
    {
        var reduced = KeywordRouter.Utterance(phrase);

        return keywordRouter.Book.Entries.Any(entry =>
            string.Equals(KeywordRouter.Utterance(entry.Phrase), reduced, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Reads a yes/no answer the same way the offer window would, for the one standing choice.</summary>
    private static bool IsAffirmative(string input)
    {
        var probe = new OfferWindow();
        probe.Open(new Offer([new OfferChoice("that", new OfferTarget.Answer(() => new OfferAnswer(string.Empty)))]));

        return probe.Read(input) is OfferReading.Picked;
    }

    private IEnumerable<TurnEvent> Offered(string text, string input, TurnOutcome outcome = TurnOutcome.Answered)
    {
        yield return new TurnEvent.Routed(TurnRoute.Offer, Effort: null);

        Said(text, input);

        yield return new TurnEvent.TextDelta(text);
        yield return new TurnEvent.Completed(new TurnResult(outcome, TurnRoute.Offer, text, Effort: null, Cost: null));
    }

    /// <summary>Steps 1-3, which never reach the model.</summary>
    private async IAsyncEnumerable<TurnEvent> ModelFreeAsync(
        string input,
        InputSource source,
        Routing routing,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // 1.
        if (settings is not null && keywordRouter.MatchSetting(input) is { } settingCommand)
        {
            routing.Handled = true;
            yield return new TurnEvent.Routed(TurnRoute.SettingCommand, Effort: null);

            var applied = settings.Apply(settingCommand.Row.Key, settingCommand.Value, SettingsCaller.KeywordRouter);

            logger.LogInformation(
                "Keyword router applied {Key} from the phrase \"{Phrase}\": {Status}",
                settingCommand.Row.Key,
                settingCommand.Phrase,
                applied.Status);

            // Recorded, so a follow-up lands in a conversation that knows this happened (remediation.md 17,
            // item 4). *"Stop calling things out"* answered by the router, and then *"why did you do that?"*,
            // reproduces the reported transcript by a second road: none of these four routes ever wrote a
            // word into history.
            Said(applied.Message, input);

            yield return new TurnEvent.TextDelta(applied.Message);
            yield return new TurnEvent.Completed(new TurnResult(
                applied.Ok ? TurnOutcome.Answered : TurnOutcome.Failed,
                TurnRoute.SettingCommand,
                applied.Message,
                Effort: null,
                Cost: null));
            yield break;
        }

        // 2.
        if (keywordRouter.MatchToolCommand(input) is { } toolCommand)
        {
            routing.Handled = true;
            yield return new TurnEvent.Routed(TurnRoute.ActionCommand, Effort: null);

            var actioned = await capabilities
                .InvokeAsync(toolCommand.ToolName, toolCommand.Arguments, cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation(
                "Keyword router performed {Tool} from the phrase \"{Phrase}\"",
                toolCommand.ToolName,
                toolCommand.Phrase);

            // With what was asked for, as above (#415).
            Said(actioned.Spoken, input);

            yield return new TurnEvent.TextDelta(actioned.Spoken);
            yield return new TurnEvent.Completed(new TurnResult(
                actioned.IsError ? TurnOutcome.Failed : TurnOutcome.Answered,
                TurnRoute.ActionCommand,
                actioned.Spoken,
                Effort: null,
                Cost: null));
            yield break;
        }

        // 3.
        if (keywordRouter.Match(input, source) is { } match)
        {
            routing.Handled = true;
            yield return new TurnEvent.Routed(TurnRoute.KeywordRouter, Effort: null);

            var result = await capabilities
                .InvokeAsync(match.ToolName, match.Arguments, cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation(
                "Keyword router answered with {Capability}/{Tool}", match.CapabilityId, match.ToolName);

            // With what was asked for, as above (#415).
            Said(result.Spoken, input);

            yield return new TurnEvent.TextDelta(result.Spoken);
            yield return new TurnEvent.Completed(new TurnResult(
                result.IsError ? TurnOutcome.Failed : TurnOutcome.Answered,
                TurnRoute.KeywordRouter,
                result.Spoken,
                Effort: null,
                Cost: null));
        }
    }

    private async IAsyncEnumerable<TurnEvent> RunModelTurnAsync(
        string input,
        ILlmProvider activeProvider,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var chosenModel = Model ?? activeProvider.DefaultModel;
        // What the Commander asked for, held between what they will pay for (Phase 54).
        var effort = ThinkingEffortRange.Clamp(EffortRouter.ChooseFor(input), EffortFloor, EffortCeiling);

        // A cold prefix is only sanctioned on the first turn and after a model change.
        var coldPrefixExpected = _lastModelUsed != chosenModel;
        _lastModelUsed = chosenModel;

        // Which tools ship is a choice between pre-declared profiles, never between individual tools (Phase
        // 10) — a per-turn set would rewrite position 1 and invalidate the whole cached prefix.
        var providerCapabilities = activeProvider.CapabilitiesFor(chosenModel);

        var advertised = providerCapabilities.SupportsToolCalls
            ? ToolSurface.ForMode(
                capabilities,
                ToolContext?.Invoke() ?? Input.ControlContext.None,
                ActionsEnabled?.Invoke() ?? false).Tools
            : [];

        // Both halves, and the endpoint half is not the Commander's doing: pointing llm.endpoint at a gateway
        // turns this off whatever the setting says, because a server-side tool is the provider's to offer.
        var webSearch = providerCapabilities.SupportsWebSearch && (WebSearchEnabled?.Invoke() ?? false);

        // What this turn says it thought at, which is not always what it asked for (Phase 54).
        var effortReported = providerCapabilities.SupportsThinkingEffort ? effort : (ThinkingEffort?)null;

        // What this turn has said so far, tool rounds included.
        List<ConversationMessage> pending = [new ConversationMessage(ConversationRole.User, Spoken() + input)];

        yield return new TurnEvent.Routed(TurnRoute.Model, effortReported);

        var usage = LlmUsage.None;
        var answer = string.Empty;
        var stopReason = LlmStopReason.Completed;

        // Taken before the model is asked anything, so that a turn which resolves it can be told apart from a
        // turn which merely says it did.
        var standingBefore = Standing?.Invoke();

        // A tool the model already called this turn, keyed by name and exact arguments, so a repeat is
        // answered rather than run again — the second identical call is not tried, and whatever it does the
        // first time (an announcement included) does not happen twice (#87).
        var triedThisTurn = new Dictionary<string, ToolResult>();

        // Whether the round that just finished spoke any text, so the next round's text is not run onto
        // the end of it without a space (#87).
        var previousRoundSpoke = false;

        // The last round's content when it asked for no tools, and the standing offer appended to its answer.
        IReadOnlyList<ConversationContent>? closingContent = null;
        string? standingAdded = null;

        for (var round = 1; ; round++)
        {
            // The last round is offered no tools at all.
            var lastRound = round > MaxToolRounds;

            if (round > 1 && previousRoundSpoke)
            {
                yield return new TurnEvent.TextDelta(" ");
            }

            var request = new LlmRequest
            {
                Model = chosenModel,
                Effort = effort,

                // Warm, on every round including the last one (#98).
                Sampling = LlmSampling.Conversation,

                // Withdrawn on the last round with the tools, and for the same reason: that round exists to
                // force an answer out of what is already known.
                WebSearch = webSearch && !lastRound,
                Prompt = new PromptAssembly
                {
                    Tools = lastRound ? [] : advertised,
                    Persona = Persona,
                    CanBeDirected = CanBeDirected?.Invoke() == true,
                    AboutMe = AboutMe,
                    Recall = Recall,
                    Directions = Directions,
                    History = [.. _history, .. pending],
                    LiveGameState = LiveGameState?.Invoke(),
                },
            };

            var outcome = new RoundOutcome();

            await foreach (var turnEvent in RunRoundAsync(request, activeProvider, outcome, cancellationToken)
                               .ConfigureAwait(false))
            {
                yield return turnEvent;
            }

            if (outcome.Failure is not null)
            {
                // Said out loud in the current voice, because the alternative is silence, and silence here is
                // indistinguishable from a model with nothing to say.
                var text = Retry.Attempts > 1
                    ? $"I couldn't reach the model after {Retry.Attempts} tries. {outcome.Failure}"
                    : $"I couldn't reach the model just then. {outcome.Failure}";

                yield return new TurnEvent.TextDelta(text);
                yield return new TurnEvent.Completed(new TurnResult(
                    TurnOutcome.Failed, TurnRoute.Model, text, effortReported, Cost: null));
                yield break;
            }

            // Every round is a billed request, so usage accumulates across the whole turn.
            usage = Add(usage, outcome.Usage);
            answer = outcome.Reply.ToString().Trim();
            stopReason = outcome.StopReason;
            previousRoundSpoke = answer.Length > 0;

            if (outcome.ToolUses.Count == 0)
            {
                closingContent = outcome.Content;
                break;
            }

            // The assistant's own turn, carrying the calls it asked for, in the order they arrived.
            pending.Add(new ConversationMessage(ConversationRole.Assistant, [.. outcome.Content]));

            var results = new List<ConversationContent>();

            // Results the tool asked to have spoken as written (#111).
            var relayed = new List<string>();

            foreach (var call in outcome.ToolUses)
            {
                yield return new TurnEvent.ToolStarted(call.Name);

                var key = call.Name + " " + call.InputJson;
                ToolResult result;

                if (triedThisTurn.TryGetValue(key, out var prior))
                {
                    // Not run again: whatever the first attempt did — an announcement included — happens once,
                    // and the model is told plainly that asking again will not change the answer (#87).
                    result = ToolResult.Error(
                        "Already tried once this turn with the same arguments, and the answer was: "
                        + $"{prior.Content} Asking again will not change it.");

                    logger.LogInformation(
                        "Model called {Tool} in round {Round}: skipped, already tried this turn",
                        call.Name,
                        round);
                }
                else
                {
                    // The one call site that says Model.
                    result = await capabilities
                        .InvokeAsync(
                            call.Name,
                            ToolArguments.FromJson(call.InputJson),
                            cancellationToken,
                            ToolCaller.Model)
                        .ConfigureAwait(false);

                    triedThisTurn[key] = result;

                    if (result.IsError)
                    {
                        logger.LogInformation(
                            "Model called {Tool} in round {Round}: error, arguments {Arguments}",
                            call.Name,
                            round,
                            call.InputJson);
                    }
                    else
                    {
                        logger.LogInformation(
                            "Model called {Tool} in round {Round}: ok",
                            call.Name,
                            round);
                    }
                }

                yield return new TurnEvent.ToolFinished(call.Name, !result.IsError);

                results.Add(new ConversationContent.ToolResult(call.Id, result.Content, result.IsError));

                if (result.Relayed)
                {
                    relayed.Add(result.Spoken);
                }
            }

            pending.Add(new ConversationMessage(ConversationRole.User, results));

            // The turn ends on the tool's own sentence, so the model is never asked to report an action it
            // has just taken. Any other result in the same round is committed to history unspoken, and a
            // follow-up next turn is answered from the transcript rather than by running the tool again.
            if (relayed.Count > 0)
            {
                var said = string.Join(' ', relayed);

                if (previousRoundSpoke)
                {
                    yield return new TurnEvent.TextDelta(" ");
                }

                yield return new TurnEvent.TextDelta(said);

                answer = previousRoundSpoke ? $"{answer} {said}" : said;
                break;
            }
        }

        availability.MarkAvailable();

        // Still open, and the model has finished talking about it.
        if (answer.Length > 0
            && standingBefore is { Length: > 0 }
            && string.Equals(standingBefore, Standing?.Invoke(), StringComparison.Ordinal))
        {
            yield return new TurnEvent.TextDelta(" " + standingBefore);
            answer = $"{answer} {standingBefore}";
            standingAdded = standingBefore;

            StandingSaid?.Invoke();
        }

        // A model on the Commander's own machine is free, and that is a fact about the address rather than
        // about the model id — no table row could hold it, because the id is whatever the local server
        // happens to call the weights it loaded (Phase 29).
        var price = activeProvider.RunsOnThisMachine
            ? PriceTable.Free
            : prices.For(activeProvider.Id, chosenModel);

        // Usage the provider never sent is unpriced even when the model is in the table.
        var cost = price is null || !usage.Reported
            ? TurnCost.Unpriced(usage)
            : new TurnCost(usage, price.DollarsFor(usage), true);
        spend.Record(cost, coldPrefixExpected, activeProvider.Id, chosenModel, Warmth(usage, providerCapabilities));

        // A refusal is an unsure turn, not an error: the model declined, which is a real answer about what it
        // will do rather than a fault in the pipeline.
        var turnOutcome = stopReason is LlmStopReason.Refusal or LlmStopReason.Paused || answer.Length == 0
            ? TurnOutcome.Unsure
            : TurnOutcome.Answered;

        if (turnOutcome == TurnOutcome.Answered)
        {
            // The tool rounds are committed too, not just the question and the answer.
            _history.AddRange(pending);
            _history.Add(Closing(answer, closingContent, standingAdded));

            Bound();
        }

        logger.LogInformation(
            "Model turn {Outcome} at {Effort} effort; {Input} in ({CacheRead} cached), {Output} out, {Cost}",
            turnOutcome,
            effort,
            usage.TotalInputTokens,
            usage.CacheReadInputTokens,
            usage.OutputTokens,
            cost.Priced ? cost.Dollars.ToString("C4") : "unpriced");

        yield return new TurnEvent.Completed(new TurnResult(turnOutcome, TurnRoute.Model, answer, effortReported, cost));
    }

    /// <summary>
    /// The assistant message that ends a turn: the answer as prose, or the closing round's content in order
    /// when it holds a provider block.
    /// </summary>
    private static ConversationMessage Closing(
        string answer,
        IReadOnlyList<ConversationContent>? closingContent,
        string? standingAdded)
    {
        if (closingContent is null || !closingContent.Any(part => part is ConversationContent.Opaque))
        {
            return new ConversationMessage(ConversationRole.Assistant, answer);
        }

        List<ConversationContent> content = [.. closingContent];

        if (standingAdded is not null)
        {
            content.Add(new ConversationContent.Text(standingAdded));
        }

        return new ConversationMessage(ConversationRole.Assistant, content);
    }

    /// <summary>What one round of the model turn produced.</summary>
    private sealed class RoundOutcome
    {
        private readonly System.Text.StringBuilder _run = new();

        /// <summary>All of the round's text, for speaking.</summary>
        public System.Text.StringBuilder Reply { get; } = new();

        /// <summary>Text runs, provider blocks and tool calls, in the order they arrived.</summary>
        public List<ConversationContent> Content { get; } = [];

        public List<ConversationContent.ToolUse> ToolUses { get; } = [];

        public LlmUsage Usage { get; set; } = LlmUsage.None;

        public LlmStopReason StopReason { get; set; } = LlmStopReason.Completed;

        public string? Failure { get; set; }

        public void AddText(string text)
        {
            Reply.Append(text);
            _run.Append(text);
        }

        public void Add(ConversationContent part)
        {
            EndRun();
            Content.Add(part);

            if (part is ConversationContent.ToolUse call)
            {
                ToolUses.Add(call);
            }
        }

        /// <summary>Closes the text run in progress. Call once the stream has ended.</summary>
        public void EndRun()
        {
            var text = _run.ToString().Trim();
            _run.Clear();

            if (text.Length > 0)
            {
                Content.Add(new ConversationContent.Text(text));
            }
        }

        public void Reset()
        {
            _run.Clear();
            Reply.Clear();
            Content.Clear();
            ToolUses.Clear();
            Usage = LlmUsage.None;
            StopReason = LlmStopReason.Completed;
            Failure = null;
        }
    }

    /// <summary>One turn is several rounds, and the bill is their sum.</summary>
    private static PrefixWarmth Warmth(LlmUsage usage, LlmProviderCapabilities capabilities)
    {
        if (!usage.Reported || !capabilities.SupportsPromptCaching)
        {
            return PrefixWarmth.Unknown;
        }

        if (usage.CacheCreationInputTokens > 0)
        {
            return PrefixWarmth.Cold;
        }

        if (usage.CacheReadInputTokens > 0)
        {
            return PrefixWarmth.Warm;
        }

        return usage.TotalInputTokens >= capabilities.MinimumCacheablePrefixTokens
            ? PrefixWarmth.Cold
            : PrefixWarmth.Unknown;
    }

    private static LlmUsage Add(LlmUsage running, LlmUsage round) => new(
        running.InputTokens + round.InputTokens,
        running.OutputTokens + round.OutputTokens,
        running.CacheCreationInputTokens + round.CacheCreationInputTokens,
        running.CacheReadInputTokens + round.CacheReadInputTokens)
    {
        WebSearchRequests = running.WebSearchRequests + round.WebSearchRequests,

        // One silent round makes the whole turn unpriced, not partly priced.
        Reported = running.Reported && round.Reported,
    };

    /// <summary>One request to the provider, retried where retrying is warranted.</summary>
    private async IAsyncEnumerable<TurnEvent> RunRoundAsync(
        LlmRequest request,
        ILlmProvider activeProvider,
        RoundOutcome outcome,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var transient = false;

        for (var attempt = 1; attempt <= Math.Max(1, Retry.Attempts); attempt++)
        {
            if (attempt > 1)
            {
                var wait = Retry.WaitBefore(attempt);

                yield return new TurnEvent.Retrying(attempt, Retry.Attempts, wait, outcome.Failure ?? "no answer");
                logger.LogInformation(
                    "Retrying the model turn, attempt {Attempt} of {Total}, after {Wait}",
                    attempt,
                    Retry.Attempts,
                    wait);

                await _clock.DelayAsync(wait, cancellationToken).ConfigureAwait(false);
            }

            outcome.Reset();
            transient = false;
            var spokeThisAttempt = false;

            await foreach (var streamEvent in AttemptAsync(request, activeProvider, cancellationToken)
                               .ConfigureAwait(false))
            {
                switch (streamEvent)
                {
                    case LlmStreamEvent.TextDelta text:
                        outcome.AddText(text.Text);
                        spokeThisAttempt = true;
                        yield return new TurnEvent.TextDelta(text.Text);
                        break;

                    case LlmStreamEvent.ThinkingDelta thinking:
                        yield return new TurnEvent.ThinkingDelta(thinking.Text);
                        break;

                    case LlmStreamEvent.ToolUse toolUse:
                        outcome.Add(new ConversationContent.ToolUse(toolUse.Id, toolUse.Name, toolUse.InputJson));
                        break;

                    case LlmStreamEvent.Opaque opaque:
                        outcome.Add(new ConversationContent.Opaque(activeProvider.Id, opaque.Json));
                        break;

                    case LlmStreamEvent.Completed completed:
                        outcome.Usage = completed.Usage;
                        outcome.StopReason = completed.StopReason;
                        break;

                    case LlmStreamEvent.Failed failed:
                        outcome.Failure = failed.Message;
                        transient = failed.Transient;
                        availability.MarkFailed(failed.Message, failed.Transient);
                        logger.LogWarning(
                            "Model turn failed ({Kind}): {Message}",
                            failed.Transient ? "transient" : "configuration",
                            failed.Message);
                        break;
                }
            }

            outcome.EndRun();

            // A configuration failure will fail identically next time, so retrying it only spends the
            // Commander's silence.
            if (outcome.Failure is null || spokeThisAttempt || !transient)
            {
                break;
            }
        }
    }

    /// <summary>One attempt, with a stall turned into an ordinary failure event.</summary>
    private async IAsyncEnumerable<LlmStreamEvent> AttemptAsync(
        LlmRequest request,
        ILlmProvider activeProvider,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var timeout = _clock.CreateTimeout(Retry.AttemptTimeout, cancellationToken);

        await using var events = activeProvider
            .StreamAsync(request, timeout.Token)
            .GetAsyncEnumerator(timeout.Token);

        while (true)
        {
            LlmStreamEvent? current = null;
            LlmStreamEvent.Failed? failed = null;
            var ended = false;

            try
            {
                if (await events.MoveNextAsync().ConfigureAwait(false))
                {
                    current = events.Current;
                }
                else
                {
                    ended = true;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The Commander called the turn off.
                throw;
            }
            catch (OperationCanceledException)
            {
                // Ours tripped, not the caller's: the attempt ran out of time.
                failed = new LlmStreamEvent.Failed(
                    $"it did not answer within {Retry.AttemptTimeout.TotalSeconds:0} seconds",
                    Transient: true);
            }
            catch (Exception ex)
            {
                // A provider throwing rather than reporting is still just a failed turn.
                failed = new LlmStreamEvent.Failed(ex.Message, Transient: true);
            }

            // Yielded out here because a `yield` cannot sit in a `catch` either — the value is decided inside
            // the guarded region and emitted outside it.
            if (failed is not null)
            {
                yield return failed;
                yield break;
            }

            if (ended)
            {
                yield break;
            }

            yield return current!;
        }
    }
}
