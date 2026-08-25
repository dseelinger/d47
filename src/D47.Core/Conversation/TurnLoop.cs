using System.Runtime.CompilerServices;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace D47.Core.Conversation;

public enum TurnRoute
{
    /// <summary>Answered by the model-free keyword router. No provider was contacted.</summary>
    KeywordRouter,

    /// <summary>
    /// A settings change made by the model-free router. Its own route because it is the one
    /// path by which a protected setting can be reached by voice (architecture.md §7), and a
    /// path like that should be legible in the transcript rather than filed under something else.
    /// </summary>
    SettingCommand,

    /// <summary>
    /// A game action performed by the model-free router from a declared phrase. Its own route
    /// for the same reason <see cref="SettingCommand"/> is: this is the path that presses a key
    /// in the Commander's ship without a model in it, and a path like that should be legible in
    /// the transcript rather than filed under something else.
    /// </summary>
    ActionCommand,

    /// <summary>Answered by the language model.</summary>
    Model,

    /// <summary>Nothing could answer it. Produces an unsure turn, not an error.</summary>
    NoCapability,
}

public enum TurnOutcome
{
    Answered,

    /// <summary>
    /// An explicit result, not a score. The checklist is specific about why: models produce
    /// confident-sounding confidence numbers that do not mean anything, so this is a state the
    /// turn can be in rather than a threshold someone has to pick.
    /// </summary>
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

    /// <summary>
    /// About to try again after a transient failure. Carries the wait so a surface can say how
    /// long it is about to be quiet for, which is the whole point of the item: silence that
    /// nobody has accounted for is indistinguishable from a hang (list.md Phase 5).
    /// </summary>
    public sealed record Retrying(int Attempt, int Of, TimeSpan Wait, string Because) : TurnEvent;

    /// <summary>
    /// A tool the model asked for is about to run. Emitted before the call rather than after,
    /// because the gap is the point: a lookup that reaches a network can take seconds, and a
    /// surface with nothing to show during it is indistinguishable from a hang — the same
    /// reasoning as <see cref="Retrying"/>.
    /// </summary>
    public sealed record ToolStarted(string Tool) : TurnEvent;

    /// <summary>
    /// A tool the model asked for has finished. <paramref name="Succeeded"/> is the handler's own
    /// verdict, not the turn's: a tool that failed is a fact the model is told about and usually
    /// works around, so this is transcript material rather than a turn outcome.
    /// </summary>
    public sealed record ToolFinished(string Tool, bool Succeeded) : TurnEvent;

    public sealed record Completed(TurnResult Result) : TurnEvent;
}

/// <summary>
/// One turn, start to finish. Owns no thread and reads no clock: it is an async stream the
/// caller drives, so the UI, a test and a replay harness all drive it the same way.
/// <para>
/// Routing order is deliberate. The keyword router gets first refusal on every input because
/// some commands must never reach the model at all — protected settings are a property of the
/// caller, not the modality (architecture.md §7). Only unmatched input reaches the model.
/// </para>
/// </summary>
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
    ITurnClock? clock = null)
{
    private readonly ITurnClock _clock = clock ?? SystemTurnClock.Instance;

    private List<ConversationMessage> _history = [];

    private string? _lastModelUsed;

    /// <summary>
    /// How hard to try before saying so out loud. Settable so a settings change applies to the
    /// next turn without a restart (list.md Phase 4).
    /// </summary>
    public RetryPolicy Retry { get; set; } = RetryPolicy.Default;

    /// <summary>
    /// Business the Commander has left open, in one line, or null when there is none
    /// (remediation.md 10, item 10).
    /// <para>
    /// <b>Asked twice — once before the model speaks and once after — and appended only when the
    /// same answer comes back both times.</b> That is what makes it a statement of fact rather
    /// than a nag: if the turn resolved the thing, the second answer differs and nothing is added.
    /// </para>
    /// <para>
    /// It exists because the prompt-side defences are not enough and were demonstrated not to be.
    /// A checklist proposal is <see cref="Capabilities.ToolDefinition.Protected"/>, the model is
    /// told every turn that it cannot accept on the Commander's behalf, the reply it is answering
    /// says "I cannot make this change myself", and the guardrails say never to claim an action
    /// that was not taken. All four were in place, and a model still answered "Accepted. Removed
    /// from the list" for a removal that never ran. Text the model writes cannot be trusted to
    /// describe what the model did; this line is written by the thing that knows.
    /// </para>
    /// </summary>
    public Func<string?>? Standing { get; set; }

    /// <summary>
    /// How many times in one turn the model may ask for tools and be answered.
    /// <para>
    /// A stop, not a tuning knob. Each round is a billed request, and a model that answers every
    /// tool result with another tool call would spend the Commander's money in a loop with
    /// nothing to show for it. Generous enough that no honest question reaches it — "the nearest
    /// station selling this module" is two or three — and low enough that a loop is caught while
    /// it is still cheap. Reaching it ends the tool rounds and asks for an answer in words,
    /// rather than failing the turn: the model usually has enough by then to say something true.
    /// </para>
    /// </summary>
    public int MaxToolRounds { get; set; } = 8;

    public IReadOnlyList<ConversationMessage> History => _history;

    /// <summary>
    /// Records something d47 said without being asked, so the next turn knows it said it
    /// (remediation.md 17, item 4).
    /// <para>
    /// Reported with the transcript: a route callout said <em>"Elvira Martuuk is one stop away"</em>,
    /// the Commander asked <em>"why would I care about that?"</em>, and d47 answered <em>"I have no
    /// record of what I said before this"</em>. It was telling the truth. History was written in
    /// exactly one place — the end of an answered model turn — so callouts, the continuity line,
    /// habit remarks, reminders, autonomous actions and the keyword router's own replies all went
    /// to the speaker and to the panel and nowhere else. <b>The panel and the prompt are two
    /// transcripts, and only one of them is the conversation.</b>
    /// </para>
    /// <para>
    /// <b>Carried into the next user turn rather than appended as an assistant message.</b> An
    /// assistant message with no user message before it, or two in a row, is not a shape every
    /// endpoint accepts, and Phase 29 means d47 talks to more than one. Folding it into the turn
    /// that follows is what this codebase already does with live game state, needs no provider to
    /// be taught anything, and — because that user message is what gets committed — the line
    /// persists in history for every later turn as well.
    /// </para>
    /// <para>
    /// <b>Only d47's own words reach here.</b> The caller gates on
    /// <see cref="Callouts.Announcement.ConversationLine"/>, which is d47 speaking and nothing
    /// else: a re-voiced in-game message is another Commander's text, and architecture.md §7 names
    /// in-game comms as the source whose attacker is any player in range. Laundering that into the
    /// assistant's own voice would be the most trusted position in the transcript.
    /// </para>
    /// </summary>
    public void Said(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        lock (_spokenLock)
        {
            _spoken.Add(line.Trim());

            // A Commander who does not ask anything for an hour is not owed every ambient line of
            // it. The most recent few are what a follow-up question can possibly be about.
            while (_spoken.Count > SpokenCarried)
            {
                _spoken.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// The block that carries those lines into the turn, and empties itself doing it.
    /// <para>
    /// Labelled rather than merely prepended, because it is arriving inside a user message and is
    /// not something the Commander said. The label is the same shape the game-state block uses.
    /// </para>
    /// </summary>
    private string Spoken()
    {
        List<string> said;

        lock (_spokenLock)
        {
            if (_spoken.Count == 0)
            {
                return string.Empty;
            }

            said = [.. _spoken];
            _spoken.Clear();
        }

        return
            "<said-aloud>\nSince the last exchange you spoke these lines to the Commander without "
            + "being asked. They are your own words, and the Commander heard them.\n"
            + string.Join('\n', said.Select(line => $"- {line}"))
            + "\n</said-aloud>\n\n";
    }

    /// <summary>
    /// How many unprompted lines are carried into the next turn. Ambient chatter fires on a timer,
    /// so an evening's flying could otherwise carry hundreds of them into a prompt.
    /// </summary>
    private const int SpokenCarried = 8;

    /// <summary>
    /// How many messages of one persona's transcript are kept (remediation.md 17, item 4).
    /// <para>
    /// <b>It was unbounded, and nothing had noticed.</b> Every answered turn appended its
    /// question, its tool rounds and its answer for the life of the session, and the whole of it
    /// is re-sent every turn — so a long evening's flying paid for its own morning, over and over.
    /// It was survivable only because a Commander asks a handful of questions an hour; now that
    /// d47's own unprompted lines join the transcript, it is not something to leave open.
    /// </para>
    /// <para>
    /// <b>Messages rather than turns, and dropped from the front.</b> A turn is one to several
    /// messages depending on how many tools it called, so a turn count is not a size. The oldest
    /// go first, which is the ordinary meaning of a conversation you can still follow.
    /// </para>
    /// </summary>
    public const int TranscriptKept = 80;

    /// <summary>
    /// Trims the transcript to <see cref="TranscriptKept"/>, <b>never leaving a tool call whose
    /// result was dropped with it.</b>
    /// <para>
    /// That pairing is the whole difficulty: an assistant message carrying a
    /// <see cref="ConversationContent.ToolUse"/> and the user message carrying its
    /// <see cref="ConversationContent.ToolResult"/> are two messages, and a cut between them
    /// leaves the model shown a call it never got an answer to — which providers reject outright
    /// rather than merely finding odd. So the cut is moved forward until nothing above it is
    /// answered below it.
    /// </para>
    /// </summary>
    private void Bound()
    {
        if (_history.Count <= TranscriptKept)
        {
            return;
        }

        var cut = _history.Count - TranscriptKept;

        // Forward past any message that would leave a dangling half: a tool result whose call is
        // being dropped, and the assistant call it belongs to.
        while (cut < _history.Count
               && _history[cut].Content.Any(part => part is ConversationContent.ToolResult
                                                     or ConversationContent.ToolUse))
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

    private readonly List<string> _spoken = [];

    /// <summary>
    /// Callouts are spoken from the tick loop and turns run on their own thread, so the two
    /// genuinely race. The one piece of <see cref="TurnLoop"/> that is touched from two threads.
    /// </summary>
    private readonly Lock _spokenLock = new();

    /// <summary>
    /// Points the loop at a different transcript. This is how separate memory per persona works
    /// (guardian-personas.md): each core owns a list, the host hands over the incoming one, and
    /// nothing is copied — a copy that is one turn stale is a core remembering a conversation
    /// that did not happen.
    /// <para>
    /// The list is appended to in place, so the caller keeps hold of it and gets the turns back
    /// for free. Swapping mid-turn is not supported and does not need to be: a switch is a
    /// settings change, and settings changes are applied between turns.
    /// </para>
    /// </summary>
    public void UseTranscript(List<ConversationMessage> transcript) => _history = transcript;

    /// <summary>
    /// The provider answering turns, or null for none. Settable because a key or an endpoint
    /// can change mid-session and the next turn has to use it — "apply every setting without a
    /// restart" reaches in here (list.md Phase 4).
    /// </summary>
    public ILlmProvider? Provider { get; set; } = provider;

    /// <summary>The pinned model, or null for the provider's own default.</summary>
    public string? Model { get; set; } = model;

    /// <summary>The persona block, or null for "personality off". Never reaches the guardrails.</summary>
    public string? Persona { get; set; }

    public string? AboutMe { get; set; }

    /// <summary>
    /// What d47 remembers about the Commander, already bounded and labelled by
    /// <see cref="Memory.MemoryRecall"/> (list.md Phase 31).
    /// <para>
    /// <b>A value rather than a source, unlike everything below it, and that is the point.</b>
    /// <see cref="LiveGameState"/> is a function because it must be as fresh as the turn; this must
    /// be as <em>stale</em> as the last time it actually changed. It sits above the cache breakpoint,
    /// so re-reading it per turn is how the whole cached prefix goes cold on a turn where nothing
    /// about the Commander has changed. The owner assigns it when the rendered text differs and not
    /// otherwise.
    /// </para>
    /// </summary>
    public string? Recall { get; set; }

    /// <summary>
    /// Live game state for the turn about to run. A source rather than a value: the tick loop
    /// is folding journal events continuously, so anything assigned here would be as old as the
    /// last time somebody remembered to assign it. Asked once per turn, at the moment the
    /// prompt is built.
    /// </summary>
    public Func<string?>? LiveGameState { get; set; }

    /// <summary>
    /// The mode the Commander is in, for choosing the tool profile. A source rather than a
    /// value for the same reason <see cref="LiveGameState"/> is: it changes several times a
    /// minute underneath this.
    /// </summary>
    public Func<Input.ControlContext>? ToolContext { get; set; }

    /// <summary>
    /// Whether the Commander has allowed d47 to press keys. When they have not, no action tool
    /// ships in any mode — advertising a tool that will refuse every call is paying for a
    /// refusal on every turn.
    /// </summary>
    public Func<bool>? ActionsEnabled { get; set; }

    /// <summary>
    /// Whether the Commander has allowed the model to search the web. A source like the others,
    /// but for a different reason: this one barely changes, and it is read per turn only so that
    /// switching it off takes effect on the next turn rather than the next session.
    /// </summary>
    public Func<bool>? WebSearchEnabled { get; set; }

    public async IAsyncEnumerable<TurnEvent> RunAsync(
        string input,
        InputSource source = InputSource.Typed,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        availability.BeginTurn();

        // 1. A declared settings phrase is the most specific thing an input can be, and the one
        //    path allowed to reach a protected row without hands on the panel.
        if (settings is not null && keywordRouter.MatchSetting(input) is { } settingCommand)
        {
            yield return new TurnEvent.Routed(TurnRoute.SettingCommand, Effort: null);

            var applied = settings.Apply(settingCommand.Row.Key, settingCommand.Value, SettingsCaller.KeywordRouter);

            logger.LogInformation(
                "Keyword router applied {Key} from the phrase \"{Phrase}\": {Status}",
                settingCommand.Row.Key,
                settingCommand.Phrase,
                applied.Status);

            // Recorded, so a follow-up lands in a conversation that knows this happened
            // (remediation.md 17, item 4). *"Stop calling things out"* answered by the router, and
            // then *"why did you do that?"*, reproduces the reported transcript by a second road:
            // none of these four routes ever wrote a word into history.
            Said(applied.Message);

            yield return new TurnEvent.TextDelta(applied.Message);
            yield return new TurnEvent.Completed(new TurnResult(
                applied.Ok ? TurnOutcome.Answered : TurnOutcome.Failed,
                TurnRoute.SettingCommand,
                applied.Message,
                Effort: null,
                Cost: null));
            yield break;
        }

        // 2. A declared action phrase. Above the general router because it is more specific,
        //    and above the model because "gear down" should not cost a network round trip at
        //    the moment the Commander is landing.
        if (keywordRouter.MatchToolCommand(input) is { } toolCommand)
        {
            yield return new TurnEvent.Routed(TurnRoute.ActionCommand, Effort: null);

            var actioned = await capabilities
                .InvokeAsync(toolCommand.ToolName, toolCommand.Arguments, cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation(
                "Keyword router performed {Tool} from the phrase \"{Phrase}\"",
                toolCommand.ToolName,
                toolCommand.Phrase);

            Said(actioned.Content);

            yield return new TurnEvent.TextDelta(actioned.Content);
            yield return new TurnEvent.Completed(new TurnResult(
                actioned.IsError ? TurnOutcome.Failed : TurnOutcome.Answered,
                TurnRoute.ActionCommand,
                actioned.Content,
                Effort: null,
                Cost: null));
            yield break;
        }

        // 3. The rest of the model-free path, before anything reaches a provider.
        if (keywordRouter.Match(input, source) is { } match)
        {
            yield return new TurnEvent.Routed(TurnRoute.KeywordRouter, Effort: null);

            var result = await capabilities
                .InvokeAsync(match.ToolName, ToolArguments.Empty, cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation(
                "Keyword router answered with {Capability}/{Tool}", match.CapabilityId, match.ToolName);

            Said(result.Content);

            yield return new TurnEvent.TextDelta(result.Content);
            yield return new TurnEvent.Completed(new TurnResult(
                result.IsError ? TurnOutcome.Failed : TurnOutcome.Answered,
                TurnRoute.KeywordRouter,
                result.Content,
                Effort: null,
                Cost: null));
            yield break;
        }

        // 4. The model, if there is one to ask.
        // Captured once: the property can be swapped by a settings change between turns, and a
        // turn should run against the provider it started with.
        var activeProvider = Provider;

        if (activeProvider is null || !availability.CanAttemptModelTurn)
        {
            var reason = availability.Reason ?? "No language model provider is configured.";
            logger.LogInformation("No model available for this turn: {Reason}", reason);

            var text =
                $"I'm not sure — I have no way to work that out right now. {reason} " +
                "Ask me something one of my own capabilities covers and I can still answer.";

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

    private async IAsyncEnumerable<TurnEvent> RunModelTurnAsync(
        string input,
        ILlmProvider activeProvider,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var chosenModel = Model ?? activeProvider.DefaultModel;
        var effort = EffortRouter.ChooseFor(input);

        // A cold prefix is only sanctioned on the first turn and after a model change. Anything
        // else writing cache is a regression the running total surfaces rather than hides.
        var coldPrefixExpected = _lastModelUsed != chosenModel;
        _lastModelUsed = chosenModel;

        // Which tools ship is a choice between pre-declared profiles, never between individual
        // tools (list.md Phase 10) — a per-turn set would rewrite position 1 and invalidate the
        // whole cached prefix. The profile is quantized by mode, so a Commander who stays in
        // supercruise pays for one cache entry and reads it.
        //
        // Gated on the provider actually being able to execute a tool_use reply. Advertising a
        // tool the loop would silently drop is worse than not offering it: the model then tells
        // the Commander it has done something that never happened.
        var providerCapabilities = activeProvider.CapabilitiesFor(chosenModel);

        var advertised = providerCapabilities.SupportsToolCalls
            ? ToolProfiles.For(
                capabilities,
                ToolContext?.Invoke() ?? Input.ControlContext.None,
                ActionsEnabled?.Invoke() ?? false).Tools
            : [];

        // Both halves, and the endpoint half is not the Commander's doing: pointing llm.endpoint
        // at a gateway turns this off whatever the setting says, because a server-side tool is
        // the provider's to offer. Capabilities are state, so that is a capability that is off
        // rather than a turn that fails (architecture.md §6).
        var webSearch = providerCapabilities.SupportsWebSearch && (WebSearchEnabled?.Invoke() ?? false);

        // What this turn says it thought at, which is not always what it asked for (list.md
        // Phase 54). A model with no effort dial — Haiku 4.5, or anything an endpoint has
        // refused the field for — is still sent the chosen rung in the request, because the
        // provider is the only thing that knows whether it can carry it. What it must not do is
        // report an effort nobody applied: Routed.Effort and TurnResult.Effort are both nullable
        // and already render with no effort clause, so the honest answer is available for free.
        //
        // This is SupportsThinkingEffort's first reader in src/ — it has been assigned in three
        // providers and read nowhere, which is how the Haiku defect stayed invisible.
        var effortReported = providerCapabilities.SupportsThinkingEffort ? effort : (ThinkingEffort?)null;

        // What this turn has said so far, tool rounds included. Kept apart from _history until
        // the turn succeeds, so a turn that fails commits nothing — a half-written exchange
        // ending in a tool call nobody answered is worse than no memory of it at all.
        List<ConversationMessage> pending = [new ConversationMessage(ConversationRole.User, Spoken() + input)];

        yield return new TurnEvent.Routed(TurnRoute.Model, effortReported);

        var usage = LlmUsage.None;
        var answer = string.Empty;
        var stopReason = LlmStopReason.Completed;

        // Taken before the model is asked anything, so that a turn which resolves it can be told
        // apart from a turn which merely says it did.
        var standingBefore = Standing?.Invoke();

        for (var round = 1; ; round++)
        {
            // The last round is offered no tools at all. That is what turns the ceiling into an
            // answer rather than a cutoff: a model that cannot call anything else says what it
            // has, and the Commander gets a reply instead of silence with a bill behind it.
            var lastRound = round > MaxToolRounds;

            var request = new LlmRequest
            {
                Model = chosenModel,
                Effort = effort,

                // Withdrawn on the last round with the tools, and for the same reason: that
                // round exists to force an answer out of what is already known. A model that
                // could still search could still spend a penny and come back with more to
                // think about instead of the reply the ceiling was supposed to produce.
                WebSearch = webSearch && !lastRound,
                Prompt = new PromptAssembly
                {
                    Tools = lastRound ? [] : advertised,
                    Persona = Persona,
                    AboutMe = AboutMe,
                    Recall = Recall,
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
                // Said out loud in the current voice, because the alternative is silence, and
                // silence here is indistinguishable from a model with nothing to say.
                var text = Retry.Attempts > 1
                    ? $"I couldn't reach the model after {Retry.Attempts} tries. {outcome.Failure}"
                    : $"I couldn't reach the model just then. {outcome.Failure}";

                yield return new TurnEvent.TextDelta(text);
                yield return new TurnEvent.Completed(new TurnResult(
                    TurnOutcome.Failed, TurnRoute.Model, text, effortReported, Cost: null));
                yield break;
            }

            // Every round is a billed request, so usage accumulates across the whole turn.
            // Reporting only the last one would price an eight-call lookup as though it were a
            // single question, which is the number the Commander is least able to check.
            usage = Add(usage, outcome.Usage);
            answer = outcome.Reply.ToString().Trim();
            stopReason = outcome.StopReason;

            if (outcome.ToolUses.Count == 0)
            {
                break;
            }

            // The assistant's own turn, carrying the calls it asked for. It has to go back
            // verbatim next round: a tool_result with no tool_use above it is a protocol error,
            // not a recoverable one.
            var asked = new List<ConversationContent>();

            if (answer.Length > 0)
            {
                asked.Add(new ConversationContent.Text(answer));
            }

            asked.AddRange(outcome.ToolUses);
            pending.Add(new ConversationMessage(ConversationRole.Assistant, asked));

            var results = new List<ConversationContent>();

            foreach (var call in outcome.ToolUses)
            {
                yield return new TurnEvent.ToolStarted(call.Name);

                // The one call site that says Model. Everything else reaching the registry came
                // from the panel, a hotkey or the model-free router, and a protected tool is
                // refused here rather than merely left out of the advertisement.
                var result = await capabilities
                    .InvokeAsync(
                        call.Name,
                        ToolArguments.FromJson(call.InputJson),
                        cancellationToken,
                        ToolCaller.Model)
                    .ConfigureAwait(false);

                logger.LogInformation(
                    "Model called {Tool} in round {Round}: {Status}",
                    call.Name,
                    round,
                    result.IsError ? "error" : "ok");

                yield return new TurnEvent.ToolFinished(call.Name, !result.IsError);

                results.Add(new ConversationContent.ToolResult(call.Id, result.Content, result.IsError));
            }

            pending.Add(new ConversationMessage(ConversationRole.User, results));
        }

        availability.MarkAvailable();

        // Still open, and the model has finished talking about it. Appended rather than
        // substituted: the reply may well be right, and the Commander is owed the state either
        // way. Nothing is added to an empty answer -- a model that said nothing has not
        // misdescribed anything.
        if (answer.Length > 0
            && standingBefore is { Length: > 0 }
            && string.Equals(standingBefore, Standing?.Invoke(), StringComparison.Ordinal))
        {
            yield return new TurnEvent.TextDelta(" " + standingBefore);
            answer = $"{answer} {standingBefore}";
        }

        // A model on the Commander's own machine is free, and that is a fact about the address
        // rather than about the model id — no table row could hold it, because the id is whatever
        // the local server happens to call the weights it loaded (list.md Phase 29).
        var price = activeProvider.RunsOnThisMachine
            ? PriceTable.Free
            : prices.For(activeProvider.Id, chosenModel);

        // Usage the provider never sent is unpriced even when the model is in the table. Zero
        // would report a paid session as free while claiming to be priced, which is the one
        // reading worse than admitting the number is not known.
        var cost = price is null || !usage.Reported
            ? TurnCost.Unpriced(usage)
            : new TurnCost(usage, price.DollarsFor(usage), true);
        spend.Record(cost, coldPrefixExpected, activeProvider.Id, chosenModel, Warmth(usage, providerCapabilities));

        // A refusal is an unsure turn, not an error: the model declined, which is a real answer
        // about what it will do rather than a fault in the pipeline. A paused turn is unsure for
        // a different reason — the text is real but it stopped part-way, and the one thing that
        // must not happen is passing a truncation off as a finished answer.
        var turnOutcome = stopReason is LlmStopReason.Refusal or LlmStopReason.Paused || answer.Length == 0
            ? TurnOutcome.Unsure
            : TurnOutcome.Answered;

        if (turnOutcome == TurnOutcome.Answered)
        {
            // The tool rounds are committed too, not just the question and the answer. The model
            // is shown its own calls and what came back, so a follow-up question lands in a
            // conversation that accounts for how the last one was answered.
            _history.AddRange(pending);
            _history.Add(new ConversationMessage(ConversationRole.Assistant, answer));

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
    /// What one round of the model turn produced. A mutable carrier rather than a return value
    /// because the round streams: the events have to reach the caller as they arrive, so the
    /// method is an iterator, and an iterator cannot also return a result.
    /// </summary>
    private sealed class RoundOutcome
    {
        public System.Text.StringBuilder Reply { get; } = new();

        public List<ConversationContent.ToolUse> ToolUses { get; } = [];

        public LlmUsage Usage { get; set; } = LlmUsage.None;

        public LlmStopReason StopReason { get; set; } = LlmStopReason.Completed;

        public string? Failure { get; set; }
    }

    /// <summary>
    /// One turn is several rounds, and the bill is their sum.
    /// <para>
    /// <b>Every field has to be named here or it is silently dropped.</b> Searches are counted
    /// on an <c>init</c> property rather than as a positional field, which is what let this
    /// method keep compiling unchanged when they arrived — and a turn that searched twice was
    /// then billed as though it had searched none. That is the exact error the count exists to
    /// prevent, so the tests pin the money rather than the field.
    /// </para>
    /// </summary>
    /// <summary>
    /// Whether the prefix was there, in terms that mean the same thing on every provider
    /// (list.md Phase 29, seam 3). Two signals, because no provider sends both.
    /// <para>
    /// A <b>cache write</b> is a cold prefix wherever one is reported, which is Anthropic and
    /// GPT-5.6 onward. Reading <b>nothing</b> is the inverse signal and the one that works
    /// everywhere else — if the prefix had not changed, something should have been read.
    /// </para>
    /// <para>
    /// The minimum-cacheable check is what keeps that inverse signal honest. A prompt too short
    /// to cache reads nothing every time and is not evidence of anything going wrong, so it is
    /// reported as unmeasured rather than counted as a regression the Commander cannot fix.
    /// </para>
    /// </summary>
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

        // One silent round makes the whole turn unpriced, not partly priced. A sum of the rounds
        // that did report is a smaller number than the truth wearing the same confidence as a
        // complete one, which is the failure this flag exists to prevent (list.md Phase 29).
        Reported = running.Reported && round.Reported,
    };

    /// <summary>
    /// One request to the provider, retried where retrying is honest.
    /// <para>
    /// Retry lives here rather than around the whole turn because of one asymmetry: once a word
    /// has been streamed it has probably already been spoken, and there is no such thing as
    /// un-saying it. So an attempt that produced text is never retried, however transient the
    /// failure that ended it looked (list.md Phase 5). A tool call is not subject to that rule —
    /// nothing has run yet when the round ends, because execution is the caller's job — so a
    /// round that failed part-way through assembling calls is safe to attempt again.
    /// </para>
    /// </summary>
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

            outcome.Reply.Clear();
            outcome.ToolUses.Clear();
            outcome.Usage = LlmUsage.None;
            outcome.StopReason = LlmStopReason.Completed;
            outcome.Failure = null;
            transient = false;
            var spokeThisAttempt = false;

            await foreach (var streamEvent in AttemptAsync(request, activeProvider, cancellationToken)
                               .ConfigureAwait(false))
            {
                switch (streamEvent)
                {
                    case LlmStreamEvent.TextDelta text:
                        outcome.Reply.Append(text.Text);
                        spokeThisAttempt = true;
                        yield return new TurnEvent.TextDelta(text.Text);
                        break;

                    case LlmStreamEvent.ThinkingDelta thinking:
                        yield return new TurnEvent.ThinkingDelta(thinking.Text);
                        break;

                    case LlmStreamEvent.ToolUse toolUse:
                        outcome.ToolUses.Add(
                            new ConversationContent.ToolUse(toolUse.Id, toolUse.Name, toolUse.InputJson));
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

            // A configuration failure will fail identically next time, so retrying it only
            // spends the Commander's silence. Only transient failures are worth waiting on.
            if (outcome.Failure is null || spokeThisAttempt || !transient)
            {
                break;
            }
        }
    }

    /// <summary>
    /// One attempt, with a stall turned into an ordinary failure event.
    /// <para>
    /// A provider that hangs is the case this exists for. Left alone it produces no events at
    /// all — not an error, just a turn that never ends — which is the single worst thing this
    /// app can do, because the Commander has no way to tell it apart from having been ignored.
    /// The per-attempt timeout converts that into a <see cref="LlmStreamEvent.Failed"/> the
    /// retry loop can act on and the voice can report.
    /// </para>
    /// <para>
    /// Written with an explicit enumerator because a `yield` cannot sit inside a `try` that has
    /// a `catch`, and the whole point here is catching around the provider.
    /// </para>
    /// </summary>
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
                // The Commander called the turn off. Propagated rather than turned into a
                // failure event, and this clause has to come first: without it the general
                // catch below claims the cancellation, reports it as a transient failure, and
                // d47 announces "I couldn't reach the model" about a turn that was working
                // fine until it was told to stop.
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
                // A provider throwing rather than reporting is still just a failed turn. It
                // must not take the app down, and it must not be swallowed into silence.
                failed = new LlmStreamEvent.Failed(ex.Message, Transient: true);
            }

            // Yielded out here because a `yield` cannot sit in a `catch` either — the value is
            // decided inside the guarded region and emitted outside it.
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
