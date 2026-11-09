using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using D47.Core.Conversation;
using D47.Core.Goals;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using Microsoft.Extensions.Logging;

namespace D47.Core.Adventures;

/// <summary>How far from here a generated story may go.</summary>
public enum AdventureReach
{
    NearHere,
    Session,
    Anywhere,
}

/// <summary>Which structure a generated story takes.</summary>
public enum AdventureLength
{
    /// <summary>Setup, turn, resolution.</summary>
    Short,

    /// <summary>Setup, complication, turn, setback, resolution.</summary>
    Evening,

    /// <summary>Opening, setup, complication, hesitation, turn, setback, resolution, closing.</summary>
    Long,
}

/// <summary>
/// What the Commander decided on the form, and the brief they spoke (Phase 47). <see cref="LastDraftRefused"/> is why
/// the draft before this one could not be used, for the writer to avoid. <see cref="Draft"/> is this draft's number, for the log.
/// <see cref="Barred"/> are activities a short story may not ask for. <see cref="CrimeRefused"/> is set when the Commander's Crime rating is 0.
/// When <see cref="Plan"/> is set, a planned chapter's words are written against it rather than a new plan.
/// </summary>
public sealed record AdventureAsk(
    AdventureReach Reach = AdventureReach.NearHere,
    AdventureLength Length = AdventureLength.Evening,
    bool ThisShipOnly = false,
    string? Brief = null,
    AdventureChapter? Chapter = null,
    AdventureStory? Story = null,
    AdventureRewrite? Rewrite = null,
    string? LastDraftRefused = null,
    StoryProgress? Progress = null,
    int Draft = 1,
    IReadOnlyList<string>? Barred = null,
    AdventureChapter? Failed = null,
    AdventureSetAside? SetAside = null,
    bool CrimeRefused = false,
    ChapterPlan? Plan = null);

/// <summary>
/// A chapter the Commander set aside, to be written again: the chapter, the titles of its objectives already done, the
/// Commander's words for a detour, and the system they are in now. <see cref="Words"/> is null when they asked for the
/// chapter to be written again from where they are.
/// </summary>
public sealed record AdventureSetAside(AdventureChapter Chapter, IReadOnlyList<string> Done, string? Words, string? Here);

/// <summary>
/// A begun chapter whose beats from <see cref="From"/> on are to be written again; the beats before it are kept.
/// <see cref="Step"/> is the step of that beat the Commander was on; only that step is replaced when the beat has several.
/// <see cref="ClosedMarketId"/> is a station whose docks are offline, which no new beat may dock at. A <see cref="Remark"/>
/// is what the Commander would rather do instead of the beat, after the <see cref="Exchange"/> so far; without one the beat was refused.
/// </summary>
public sealed record AdventureRewrite(
    Adventure Chapter,
    int From,
    long? ClosedMarketId = null,
    string? Remark = null,
    IReadOnlyList<AdventureRemark>? Exchange = null,
    int Step = 0)
{
    /// <summary>The place the <see cref="Remark"/> asks to travel to, found by the galaxy search; null when it names none or none was found.</summary>
    public AdventureHeading? Heading { get; init; }

    /// <summary>The system the <see cref="Remark"/> named that the galaxy search does not know; null when it named none or it was found.</summary>
    public string? Unfound { get; init; }

    /// <summary>The drawable activity the <see cref="Remark"/> asks for, with the count it said or one d47 set; null when it names none.</summary>
    public DrawnActivity? Activity { get; init; }

    /// <summary>What d47 says to the <see cref="Remark"/>, as the step that read it wrote it; null when it was not read.</summary>
    public string? Reply { get; init; }

    /// <summary>Whether the <see cref="Remark"/> was read into a place or an activity to plan the new objectives from.</summary>
    public bool Constrained => Heading is not null || Activity is not null;
}

/// <summary>A system a changed objective may travel toward. <see cref="Home"/> is set when it is Sol or Colonia standing in for a home system the Commander has not set.</summary>
public sealed record AdventureHeading(string System, long SystemAddress, StarPosition Position, bool Home = false)
{
    /// <summary>Whether the Commander asked to go home, rather than naming the system.</summary>
    public bool AskedHome { get; init; }
}

/// <summary>The Guardian beacon system an act-one chapter ends at.</summary>
public sealed record AdventureBeacon(long SystemAddress, string System, double? EasierWith = null, string? SwapTo = null)
{
    /// <summary>The hull symbol of the owned ship <see cref="SwapTo"/> names; null when it names none.</summary>
    public string? SwapHull { get; init; }

    /// <summary>The system the owned ship <see cref="SwapTo"/> names is parked in; null when unknown.</summary>
    public string? SwapSystem { get; init; }

    /// <summary>The beacon scan the chapter ends on.</summary>
    public AdventureTrigger Trigger() => new() { Kind = TriggerKind.Beacon, SystemAddress = SystemAddress, System = System, JumpRange = EasierWith };

    /// <summary>The board into the owned ship that can make the trip; null when the ship flown can.</summary>
    public AdventureTrigger? Swap() => SwapHull is { } hull ? new AdventureTrigger { Kind = TriggerKind.Board, ShipType = hull } : null;
}

/// <summary>The landable body a story's finale ends on, named in its first finale chapter.</summary>
public sealed record AdventureDestination(long SystemAddress, string System, int BodyId, string Body)
{
    /// <summary>Hops at the chapter's reach the destination may be from the Commander, per finale chapter after the first and at least once.</summary>
    public const int HopsPerChapter = 5;

    /// <summary>The farthest the destination may be from the Commander when finale chapter 1 names it.</summary>
    public static double Limit(double reach, int finaleChapters) => reach * HopsPerChapter * Math.Max(1, finaleChapters - 1);

    /// <summary>A land beat on the destination.</summary>
    public AdventureTrigger Landing() => new() { Kind = TriggerKind.Land, SystemAddress = SystemAddress, BodyId = BodyId, System = System, Body = Body };
}

/// <summary>The Guardian beacon an act-one chapter works toward but cannot reach, and why.</summary>
public sealed record AdventureBeaconAway(string System, double LightYears, string Why, bool Unarmed = false, bool Carrier = false, double? EasierWith = null)
{
    /// <summary>The objective met by any ship that can make the trip.</summary>
    public AdventureTrigger Trigger() => new()
    {
        Kind = TriggerKind.Reach,
        System = System,
        JumpRange = Carrier ? null : EasierWith,
        Unarmed = Unarmed,
        Carrier = Carrier ? true : null,
    };
}

/// <summary>An activity a chapter must contain one beat of.</summary>
public sealed record AdventureActivity(string Name, TriggerKind Kind, string? MissionFamily = null);

/// <summary>
/// The stock story a chapter belongs to: both layers, how long it has run, where it stands in its beat sheet,
/// and the beacon act one works toward.
/// </summary>
public sealed record AdventureStory(
    string Id,
    string Title,
    string Public,
    string Hidden,
    int Chapter,
    int DaysRunning,
    int? DaysSinceBeacon,
    AdventureBeacon? Beacon = null,
    string? Level = null,
    string? Stage = null,
    IReadOnlyList<(string Key, string Line)>? StageBeats = null,
    int? FinaleChapter = null,
    AdventureBeaconAway? BeaconAway = null,
    string Length = "1 year",
    int FinaleChapters = 4,
    string? Genre = null,
    IReadOnlyList<string>? GenreElements = null,
    string? ChapterSize = null,
    IReadOnlyList<DrawnActivity>? Slots = null,
    AdventureDestination? Destination = null,
    IReadOnlyList<string>? Refused = null,
    IReadOnlyList<AdventureSpeaker>? Cast = null,
    string? Core = null,
    bool CoreIsStock = false,
    string? Home = null,
    bool Remembered = false,
    IReadOnlyList<GoalClimbStep>? Goals = null,
    IReadOnlyList<string>? Aboard = null,
    IReadOnlyList<Stories.StorySwitch>? Due = null,
    IReadOnlyList<NotablePlace>? Sights = null)
{
    /// <summary>The cast ids aboard with no seat in the Commander's current hull, heard over comms.</summary>
    public IReadOnlyList<string> OnComms { get; init; } = [];

    /// <summary>The cast ids on the Commander's fleet carrier as the chapter opens.</summary>
    public IReadOnlyList<string> OnCarrier { get; init; } = [];

    /// <summary>Whether a beat the chapter's stage hands out lies in a span the story marks as in the black, where neither the Sol band nor the home leg applies.</summary>
    public bool InTheBlack { get; init; }

    /// <summary>The destination of the span the chapter lies in, once the beacon has been scanned; null for a chapter outside the black or in a span with none.</summary>
    public string? Toward { get; init; }

    /// <summary>Where <see cref="Toward"/> is, when the span named the position; null for a <see cref="StoryDestinations"/> name.</summary>
    public StarPosition? TowardAt { get; init; }

    /// <summary>The system the Commander named when changing an objective and has not reached; null when none or when the chapter has a purpose of its own.</summary>
    public AdventureHeading? Chosen { get; init; }

    /// <summary>The span in the black the draw opened, in the phase this chapter plays; null when none is open.</summary>
    public Stories.DrawnSpan? Span { get; init; }

    /// <summary>The reach a chapter that travels in the black takes in place of the ordinary reach, and the ship that gives it; null where none applies or none is known.</summary>
    public SpanReach? SpanReach { get; init; }
}

/// <summary>A story's cast member as the chapter writer is told it: the id a line names, the name, and who they are.</summary>
public sealed record AdventureSpeaker(string Id, string Name, string Who)
{
    /// <summary>Who the member is to the Commander, public; null when the story has none.</summary>
    public string? Role { get; init; }

    /// <summary>The Commander's notes on the member, which add to <see cref="Who"/>; null for none.</summary>
    public string? Notes { get; init; }

    /// <summary>Whether the member's span is on the Commander's fleet carrier.</summary>
    public bool OnCarrier { get; init; }

    /// <summary>The language the member answers in; null for English.</summary>
    public string? Language { get; init; }
}

/// <summary>The finished adventure a new chapter follows, and the chapters before it, oldest first.</summary>
public sealed record AdventureChapter(Adventure Previous, IReadOnlyList<Adventure> Earlier)
{
    /// <summary>The wars and elections the Commander took part in for the chapter, and how each ended once seen to.</summary>
    public IReadOnlyList<ConflictPart> Conflicts { get; init; } = [];

    /// <summary>The notable things the Commander did while the chapter ran.</summary>
    public IReadOnlyList<Notable> Notables { get; init; } = [];

    /// <summary>The chain ending at <paramref name="key"/>, walked back through <see cref="Adventure.Follows"/>; null when the key is not on file.</summary>
    public static AdventureChapter? Of(IReadOnlyList<Adventure> adventures, string key)
    {
        ArgumentNullException.ThrowIfNull(adventures);

        if (Find(key) is not { } previous)
        {
            return null;
        }

        var earlier = new List<Adventure>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { previous.Key };
        var at = previous.Follows;

        while (at is not null && Find(at) is { } one && seen.Add(one.Key))
        {
            earlier.Add(one);
            at = one.Follows;
        }

        earlier.Reverse();
        return new AdventureChapter(previous, earlier);

        Adventure? Find(string wanted) =>
            adventures.FirstOrDefault(adventure => string.Equals(adventure.Key, wanted, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>One round of the revision loop: what the Commander said and what the core answered.</summary>
public sealed record AdventureRemark(string Remark, string? Reply);

/// <summary>
/// What asking for a story produced: a draft and the core's reply, or a refusal naming what could not
/// stand.
/// </summary>
public sealed record AdventureOutcome(Adventure? Draft, string? Reply, string? Refusal, IReadOnlyList<string> Notes)
{
    public const string NotWritten = "The chapter could not be written. Write it again and I will try a new draft.";

    /// <summary>The write was cancelled: no draft and no refusal.</summary>
    public static readonly AdventureOutcome Stopped = new(null, null, null, []);

    public bool Succeeded => Draft is not null;

    /// <summary>True when <see cref="Refusal"/> is written for the model and the log, not the Commander.</summary>
    public bool Internal { get; init; }

    /// <summary>True when no answer came back to read, so <see cref="Refusal"/> says nothing about a draft.</summary>
    public bool Unanswered { get; init; }

    /// <summary>What the Commander is shown and told; the detail stays in <see cref="Refusal"/>.</summary>
    public string? Message => Refusal is null ? null : ContextMessage ?? (Internal ? NotWritten : Refusal);

    /// <summary>Set when a reply stopped at the endpoint's context; names the counts and the length to set.</summary>
    public string? ContextMessage { get; init; }

    /// <summary>Whether another draft may stand: an internal failure other than the endpoint's context running out.</summary>
    public bool Redraftable => Draft is null && Internal && ContextMessage is null;

    /// <summary>The raw reply the generator could not read; null on success and when nothing came back.</summary>
    public string? UnreadReply { get; init; }

    /// <summary>The step whose reply is <see cref="UnreadReply"/>.</summary>
    public string? UnreadStep { get; init; }

    /// <summary>The plan a failed planned chapter's words were written against, for the next draft to keep; null for any other write.</summary>
    public ChapterPlan? Plan { get; init; }

    /// <summary>What the next draft is told was wrong with this one; null when no answer came back to be wrong.</summary>
    public string? ForNextDraft => Unanswered ? null : Refusal;

    /// <summary>The context a story call needs at least, and the one d47 loads a local model with before writing.</summary>
    public const int StoryContextTokens = 16384;

    /// <summary>The context length that fits a prompt and a 6,144-token reply, in 4096-token steps and at least <see cref="StoryContextTokens"/>.</summary>
    public static int ContextNeeded(int? promptTokens) =>
        Math.Max(StoryContextTokens, (((promptTokens ?? 0) + 6144 + 4095) / 4096) * 4096);

    /// <summary>The load a model needs before a story call, given what the server reports.</summary>
    public enum ContextStep
    {
        Nothing,
        Load,
        Reload,
    }

    /// <summary>Whether a model loaded as <paramref name="read"/> is left alone, loaded, or reloaded at <see cref="StoryContextTokens"/>.</summary>
    public static ContextStep StepFor(LoadedContext? read) => read switch
    {
        null => ContextStep.Nothing,
        { Instances: > 1 } => ContextStep.Nothing,
        { Maximum: < StoryContextTokens } => ContextStep.Nothing,
        { Tokens: null } => ContextStep.Load,
        { Tokens: < StoryContextTokens } => ContextStep.Reload,
        _ => ContextStep.Nothing,
    };

    internal static string ReloadFailedMessage(string model) =>
        $"d47 could not load {model} with {StoryContextTokens:N0} tokens of context. {SetContextSentence(StoryContextTokens)}";

    /// <summary>One sentence telling the Commander to set the context to at least <paramref name="tokens"/> and where.</summary>
    public static string SetContextSentence(int tokens) =>
        $"Set the context to at least {tokens:N0} tokens: in LM Studio, set the model's context length in the loader, "
        + "tick \"Remember settings for\" that model and load it again; on any other server, set it in that server's own settings.";

    internal static string ContextLimitMessage(FlavourCutOff cutOff) =>
        $"The model's context ran out. The prompt took {cutOff.PromptTokens} tokens and the reply was cut off after "
        + $"{cutOff.ReplyTokens}. {SetContextSentence(ContextNeeded(cutOff.PromptTokens))}";

    internal static string ContextOverflowMessage(int? promptTokens) =>
        (promptTokens is { } tokens
            ? $"The prompt took {tokens:N0} tokens, more than the model's context holds. "
            : "The prompt was larger than the model's context. ")
        + SetContextSentence(ContextNeeded(promptTokens));

    /// <summary>The finale's destination, when the draft is finale chapter 1.</summary>
    public AdventureDestination? Destination { get; init; }

    /// <summary>The place a changed objective's new objectives travel toward, when the answer said they head for it.</summary>
    public AdventureHeading? Heading { get; init; }
}

/// <summary>
/// Writes an adventure with the model, once, for a person to agree to (Phase 47, "Written, generated or
/// imported, and each records how it arrived").
/// </summary>
public sealed partial class AdventureGenerator(
    Func<ILlmProvider?> provider,
    Func<string?> model,
    Func<string?> persona,
    Func<string?> personaId,
    Func<string?> aboutMe,
    Func<CommanderGameState?> state,
    Func<IGalaxyService?> galaxy,
    Func<INotablePlacesService?> places,
    SpendTracker? spend,
    PriceTable? prices,
    ILogger logger,
    Func<string, string, ThinkingEffort?>? storyEffort = null,
    Func<ISignalIndexSource?>? signalIndex = null,
    VisitedStarsBook? visited = null,
    Func<string, string, bool>? writesStories = null)
{
    /// <summary>Output ceiling for each lines, check and repair turn; adaptive thinking spends from it too.</summary>
    public const int BeatsOutputCeiling = 32000;

    /// <summary>Overrides the effort every call asks for; null uses the catalog's story effort for the model, else Medium.</summary>
    public ThinkingEffort? Effort { get; init; }

    /// <summary>Slowest output rate, in tokens per second, a call is expected to sustain.</summary>
    public const int SlowestTokensPerSecond = 80;

    /// <summary>The least a call on a provider that is not on this machine may run.</summary>
    public static readonly TimeSpan MinimumCallLimit = TimeSpan.FromSeconds(120);

    /// <summary>How long a call asking for <paramref name="budget"/> output tokens may run on a provider that is not on this machine.</summary>
    public static TimeSpan CallLimitFor(int budget) =>
        TimeSpan.FromSeconds(Math.Max(MinimumCallLimit.TotalSeconds, (double)budget / SlowestTokensPerSecond));

    /// <summary>Overrides how long one call may run on a provider that is not on this machine; null uses <see cref="CallLimitFor"/>.</summary>
    public TimeSpan? CallLimit { get; init; }

    /// <summary>How long a model call on a provider that is not on this machine may go with no text or thinking arriving.</summary>
    public TimeSpan SilenceLimit { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>How long a model call on a provider that is not on this machine may wait for its first text or thinking.</summary>
    public TimeSpan FirstReplyLimit { get; init; } = TimeSpan.FromSeconds(90);

    /// <summary>The clock <see cref="CallLimit"/>, <see cref="FirstReplyLimit"/> and <see cref="SilenceLimit"/> run on.</summary>
    public TimeProvider Time { get; init; } = TimeProvider.System;

    /// <summary>The activities the Commander flying has changed away from often enough to bar them; a short story may not ask for one.</summary>
    public Func<IReadOnlyList<string>>? Barred { get; set; }

    /// <summary>The home system the Commander flying has set, which "home" in a changed objective means; null for none.</summary>
    public Func<string?>? HomeSystem { get; set; }

    /// <summary>The system name search, which matches a name in any case; null finds a changed objective's destination through the galaxy search alone.</summary>
    public Func<IStarSystemService?>? Systems { get; set; }

    /// <summary>Output ceiling for the step that reads where a changed objective asks to go.</summary>
    public const int HeadingOutputCeiling = 4000;

    /// <summary>The most notable places a chapter is offered.</summary>
    public const int MostNotable = 12;

    /// <summary>Light years for a reach, from what the Commander can actually move.</summary>
    public static double Radius(AdventureReach reach, double? jumpRange, bool carrier)
    {
        var range = Math.Max(jumpRange ?? 20, 10);

        return reach switch
        {
            AdventureReach.NearHere => Math.Max(80, range * 3),
            AdventureReach.Session => Math.Max(300, range * 12),
            _ => carrier ? 5000 : 1500,
        };
    }

    /// <summary>
    /// The longest one hop of a chapter of <paramref name="ask"/> may be: the story's <see cref="AdventureStory.SpanReach"/> on a leg that
    /// travels at it, or the <see cref="Radius"/> of its reach. A drawn span's chapter at its target takes the session reach.
    /// </summary>
    public static double ReachOf(AdventureAsk ask, StarPosition? position, double? jumpRange, bool carrier) =>
        SpanReachOf(ask, position)?.LightYears
        ?? Radius(ask.Story?.Span is { Phase: Stories.DrawnSpanPhase.There } ? AdventureReach.Session : ask.Reach, jumpRange, carrier);

    /// <summary>The story's <see cref="AdventureStory.SpanReach"/> where the chapter's leg travels at it; null otherwise.</summary>
    public static SpanReach? SpanReachOf(AdventureAsk ask, StarPosition? position)
    {
        ArgumentNullException.ThrowIfNull(ask);

        return ask.Story?.SpanReach is { } reach && ChapterPull.Of(ask, position) is { AtSpanReach: true } leg && (leg.Drawn || !leg.Arrived) ? reach : null;
    }

    /// <summary>
    /// A short story in up to <see cref="StoryDirector.Drafts"/> drafts, as a chapter is written: an internal failure
    /// is written again, told what was wrong, with <paramref name="writingAgain"/> raised before each later draft.
    /// </summary>
    public async Task<AdventureOutcome> DraftAsync(
        AdventureAsk ask, DateTimeOffset now, Action<string>? writingAgain, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ask);

        AdventureOutcome? last = null;

        for (var draft = 1; draft <= StoryDirector.Drafts; draft++)
        {
            if (last is not null)
            {
                logger.LogInformation("Writing draft {Draft} of {Drafts} of a short story", draft, StoryDirector.Drafts);
                ask.Progress?.NextDraft();
                writingAgain?.Invoke(StoryDirector.WritingAgainLine(draft, last.ForNextDraft is not null));
            }

            // A throw counts as a failed draft, as it does for a chapter.
            try
            {
                last = await GenerateAsync(ask with { LastDraftRefused = last?.ForNextDraft, Draft = draft, Plan = last?.Plan ?? ask.Plan }, now, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Writing a short story failed");
                last = new AdventureOutcome(null, null, ex.Message, []) { Internal = true, Unanswered = true };
            }

            if (last.Draft is null && cancellationToken.IsCancellationRequested)
            {
                return AdventureOutcome.Stopped;
            }

            if (last.Succeeded)
            {
                ask.Progress?.Done();
            }

            if (!last.Redraftable || cancellationToken.IsCancellationRequested)
            {
                return last;
            }
        }

        logger.LogWarning("A short story was not written: {Refusal}", StoryDirector.NoDraftStood);
        return new AdventureOutcome(null, null, StoryDirector.NoDraftStood, last!.Notes);
    }

    public async Task<AdventureOutcome> GenerateAsync(AdventureAsk ask, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ask);

        if (ask.Story is null && Barred?.Invoke() is { Count: > 0 } barred)
        {
            ask = ask with { Barred = [.. (ask.Barred ?? []).Union(barred)] };
        }

        var (opened, refused) = await OpenAsync(ask, cancellationToken).ConfigureAwait(false);

        if (opened is null)
        {
            return refused!;
        }

        var (started, facts, notes) = opened;

        if (ask.Rewrite is { Remark: { } said } changing && !string.IsNullOrWhiteSpace(said))
        {
            ask = ask with { Rewrite = await ChangeAsync(ask, changing, facts, notes, cancellationToken).ConfigureAwait(false) };

            if (cancellationToken.IsCancellationRequested)
            {
                return AdventureOutcome.Stopped;
            }

            if (ask.Rewrite.Heading is { } heading)
            {
                facts = await DestinationDistanceAsync(Facts.Of(state(), ask), ask, cancellationToken).ConfigureAwait(false);

                if (facts.Position is null)
                {
                    logger.LogWarning("Draft {Draft}, the Commander's position is unknown, so the new objectives are not sent toward {System}", ask.Draft, heading.System);
                }
            }
        }

        return await PlannedAsync(ask, facts, now, notes, started, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reasoning with the AI about a draft before it is accepted.</summary>
    public async Task<AdventureOutcome> ReviseAsync(
        Adventure draft,
        AdventureAsk ask,
        IReadOnlyList<AdventureRemark> exchange,
        string remark,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(ask);
        ArgumentException.ThrowIfNullOrWhiteSpace(remark);

        return await ReviseShortAsync(draft, ask, exchange, remark, now, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A write begun: when it started, the facts it is written from, and its notes.</summary>
    private sealed record Opened(long Started, Facts Facts, List<string> Notes);

    /// <summary>The write begun at the places stage, or the outcome that refuses it.</summary>
    private async Task<(Opened? Opened, AdventureOutcome? Refused)> OpenAsync(AdventureAsk ask, CancellationToken cancellationToken)
    {
        if (Blocked() is { } blocked)
        {
            return (null, new AdventureOutcome(null, null, blocked, []));
        }

        if (await RoomForAStoryAsync(ask, cancellationToken).ConfigureAwait(false) is { } noRoom)
        {
            return (null, noRoom);
        }

        var started = Stopwatch.GetTimestamp();
        Enter(ask, WriteStage.Places);

        return (new Opened(started, await DestinationDistanceAsync(Facts.Of(state(), ask), ask, cancellationToken).ConfigureAwait(false), []), null);
    }

    /// <summary>
    /// One call reading each objective's lines, and the ending against the last objective, against what its trigger confirms;
    /// a refusal for each objective whose lines state something else. A reply that cannot be read objects to nothing.
    /// An objective is numbered with its further steps under it.
    /// </summary>
    private async Task<IReadOnlyList<Refusal>> FidelityAsync(AdventureAsk ask, Facts facts, AdventureSpine spine, IReadOnlyList<ReadBeat> beats, CancellationToken cancellationToken)
    {
        var heads = Heads(beats);
        var asked = await AskJsonAsync(FidelityStep, FidelityInstruction(facts, spine, beats, ask.Story is not null), BeatsOutputCeiling, ask.Progress, cancellationToken).ConfigureAwait(false);

        if (asked.Json is null)
        {
            logger.LogInformation("Draft {Draft}, fidelity step not read: {Why}", ask.Draft, asked.Why);

            return [];
        }

        return ReadJson<IReadOnlyList<Refusal>>(asked.Json, allowTrailingCommas: false, root =>
        {
            if (!root.TryGetProperty("objections", out var list) || list.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var reasons = new SortedDictionary<int, List<string>>();

            foreach (var objection in list.EnumerateArray())
            {
                var number = (Integer(objection, "objective") ?? 0) - 1;
                var index = number >= 0 && number < heads.Count ? heads[number] : -1;

                if (index < 0 || index >= beats.Count || !Checked(objection, beats, index))
                {
                    continue;
                }

                var reason = Text(objection, "reason") ?? "its lines state something its trigger does not confirm";

                if (!reasons.TryGetValue(index, out var kept))
                {
                    reasons[index] = kept = [];
                }

                if (!kept.Contains(reason))
                {
                    kept.Add(reason);
                }
            }

            return reasons
                .Select(pair => new Refusal(Unconfirm(heads.IndexOf(pair.Key) + 1, beats[pair.Key].Title, pair.Value, []), [pair.Key]) { Reasons = pair.Value })
                .ToList();
        }) ?? [];
    }

    private static string Unconfirm(int number, string title, IReadOnlyList<string> reasons, IReadOnlyList<string> earlier)
    {
        var text = $"{Unconfirmed}Objective {number.ToString(CultureInfo.InvariantCulture)} ({title}) states something its trigger does not confirm: {string.Join(" ", reasons)}";

        return earlier.Count == 0 ? text : $"{text} Earlier rounds also objected: {string.Join(" ", earlier)}";
    }

    /// <summary>Whether an objection names one of the three checked kinds and quotes words that are in the lines its objective speaks.</summary>
    private static bool Checked(JsonElement objection, IReadOnlyList<ReadBeat> beats, int index)
    {
        if (Text(objection, "kind") is not ("action" or "history" or "event") || Text(objection, "quote") is not { } quote
            || Letters(quote) is not { Length: > 0 } quoted)
        {
            return false;
        }

        var head = index;

        while (head > 0 && beats[head].Continues)
        {
            head--;
        }

        return Letters(string.Join(" ", beats[head].Lines.Select(line => line.Text))).Contains(quoted, StringComparison.Ordinal);
    }

    /// <summary>The index of each objective's first step in <paramref name="beats"/>, in order.</summary>
    private static List<int> Heads(IReadOnlyList<ReadBeat> beats) => [.. Enumerable.Range(0, beats.Count).Where(index => !beats[index].Continues)];

    private static StoryCall FidelityInstruction(Facts facts, AdventureSpine spine, IReadOnlyList<ReadBeat> beats, bool story)
    {
        var text = new StringBuilder();

        text.AppendLine(
            "You are checking a draft of a story the Commander will fly in Elite Dangerous. Each objective waits for one event in the "
            + "Commander's journal, its trigger. When the trigger fires, the objective's lines are spoken as written. Below, each objective "
            + "has what its trigger confirms, then its lines.");
        text.AppendLine();
        text.AppendLine(TriggerConfirms.Rule);
        text.AppendLine(
            "List an objective only where its lines state, as fact, one of the three checked things that its objective does not confirm "
            + "and the game state below does not show. A line that only sets a scene or a mood states nothing to check.");
        text.AppendLine();
        text.Append(facts.Describe());
        text.AppendLine();
        text.AppendLine(
            "A line that tells the Commander which missions to take, such as a mission whose target settlement is run by an Anarchy faction, "
            + "states nothing to check. A line that says the missions' targets were Anarchy-run is still a claim about how a mission went.");
        text.AppendLine();
        text.AppendLine($"The story's ending: {spine.Ending}");
        text.AppendLine();
        text.AppendLine("Objectives:");

        var heads = Heads(beats);

        foreach (var (head, number) in heads.Select((head, number) => (head, number)))
        {
            var beat = beats[head];
            var further = beats.Skip(head + 1).TakeWhile(next => next.Continues).ToList();

            text.AppendLine($"{number + 1}. {beat.Title}{(number == heads.Count - 1 ? " (the last objective: its lines are the ending)" : string.Empty)} — {beat.Describe()}");
            text.AppendLine($"   Confirms: {TriggerConfirms.Of(beat.Kind, beat.Trigger.Filter, story, beat.Written().System, beat.Trigger.MissionFamily)}");

            foreach (var step in further)
            {
                text.AppendLine($"   Then — {step.Describe()}");
                text.AppendLine($"   Confirms: {TriggerConfirms.Of(step.Kind, step.Trigger.Filter, story, step.Written().System, step.Trigger.MissionFamily)}");
            }

            text.AppendLine($"   Lines{(further.Count > 0 ? " (spoken after its last step)" : string.Empty)}: {Quoted(beat.Lines)}");
        }

        text.AppendLine();
        text.AppendLine(
            "Answer with one JSON object and nothing else: " + StoryAnswer.FidelityShape
            + ". \"objective\" is the objective's number; \"quote\" is the words of its lines that make the statement, copied exactly; "
            + "\"kind\" is \"action\" for what the Commander did, \"history\" for where the Commander has been or their history with a place, "
            + "\"event\" for how a game event went, and \"fiction\" for anything the cast say, send, offer, remember or own, a record in the "
            + "story, or an opinion; \"reason\" is one plain sentence saying what the lines state that the trigger does not confirm. "
            + "Use an empty list when every objective stands.");

        return new StoryCall(text.ToString(), StoryAnswer.FidelitySchema());
    }

    /// <summary>Which check raised the refusals a repair round answers; each has its own round count.</summary>
    private enum Check
    {
        Places,
        Fidelity,
        Plain,
    }

    /// <summary>A named piece of the chapter's wording the plain check reads; <see cref="Index"/> is its objective's index in the beats as written, or -1 for the opening.</summary>
    private sealed record PlainPart(string Name, string Text, int? Index = null);

    private sealed record PlainObjection(string Part, string Quote, string Kind, string Reason)
    {
        /// <summary>The quoted words written plainly, to stand in their place; null when the checker gave none or the kind is "invented".</summary>
        public string? Plain { get; init; }

        public string Describe() => $"{Part}: \"{Quote}\" is {(Kind == "invented" ? "a feature the game does not have" : "figurative")}; {Reason}";

        /// <summary>Whether the checker gave a rewrite that differs from the quoted words.</summary>
        public bool HasRewrite => Plain is { Length: > 0 } plain && !string.Equals(plain, Quote.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static List<PlainPart> SpineParts(string name, AdventureSpine spine) =>
    [
        new("title", name),
        new("premise", spine.Premise ?? string.Empty),
        new("want", spine.Want ?? string.Empty),
        new("stake", spine.Stake ?? string.Empty),
        new("turn", spine.Turn ?? string.Empty),
        new("ending", spine.Ending ?? string.Empty),
    ];

    /// <summary>The opening's and each objective's wording, one part per objective holding its further steps too.</summary>
    private static List<PlainPart> BeatParts(IReadOnlyList<AdventureLine> opening, IReadOnlyList<ReadBeat> beats)
    {
        var parts = new List<PlainPart>();

        if (opening.Count > 0)
        {
            parts.Add(new PlainPart("opening", string.Join(" ", opening.Select(line => line.Text)), -1));
        }

        foreach (var (head, number) in Heads(beats).Select((head, number) => (head, number)))
        {
            var beat = beats[head];
            var text = new StringBuilder()
                .Append("Title: ").Append(beat.Title).Append(". Lines: ").Append(string.Join(" ", beat.Lines.Select(line => line.Text)))
                .Append(" Reason: ").Append(beat.Reason).Append(" Task: ").Append(beat.Describe());

            foreach (var step in beats.Skip(head + 1).TakeWhile(next => next.Continues))
            {
                text.Append(", then ").Append(step.Describe());
            }

            parts.Add(new PlainPart($"objective {(number + 1).ToString(CultureInfo.InvariantCulture)}", text.ToString(), head));
        }

        return parts;
    }

    private static string Letters(string text) =>
        string.Join(' ', Regex.Split(text.ToLowerInvariant(), @"[^\p{L}\p{N}]+").Where(word => word.Length > 0));

    /// <summary>
    /// One call reading the wording of <paramref name="parts"/> against <see cref="PlainWords.Rule"/>; an objection whose quote is not
    /// in the part it names is dropped. A reply that cannot be read objects to nothing.
    /// </summary>
    private async Task<IReadOnlyList<PlainObjection>> PlainAsync(AdventureAsk ask, IReadOnlyList<PlainPart> parts, CancellationToken cancellationToken)
    {
        if (parts.Count == 0)
        {
            return [];
        }

        var asked = await AskJsonAsync(PlainStep, PlainInstruction(parts), BeatsOutputCeiling, ask.Progress, cancellationToken).ConfigureAwait(false);

        if (asked.Json is null)
        {
            logger.LogInformation("Draft {Draft}, plain step not read: {Why}", ask.Draft, asked.Why);

            return [];
        }

        return ReadJson<IReadOnlyList<PlainObjection>>(asked.Json, allowTrailingCommas: false, root =>
        {
            if (!root.TryGetProperty("objections", out var list) || list.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var found = new List<PlainObjection>();

            foreach (var objection in list.EnumerateArray())
            {
                var kind = Text(objection, "kind");

                if (Text(objection, "part") is not { } named || Text(objection, "quote") is not { } quote || Letters(quote) is not { Length: > 0 } quoted
                    || kind is not ("figurative" or "invented")
                    || parts.FirstOrDefault(part => string.Equals(part.Name, named.Trim(), StringComparison.OrdinalIgnoreCase)) is not { } part
                    || !Letters(part.Text).Contains(quoted, StringComparison.Ordinal))
                {
                    continue;
                }

                found.Add(new PlainObjection(part.Name, quote, kind, Text(objection, "reason") ?? "the wording is not plain")
                {
                    Plain = kind == "figurative" && Text(objection, "plain") is { Length: > 0 } plain ? plain.Trim() : null,
                });
            }

            return found;
        }) ?? [];
    }

    /// <summary>The part a plain refusal is on: <see cref="SpineIndex"/> for the title and spine, -1 for the opening, else its objective's index.</summary>
    private static int PartOf(Refusal refusal) => refusal.Spine ? SpineIndex : refusal.Opening ? -1 : refusal.Objectives[0];

    /// <summary>Adds each plain refusal's findings to those raised on its part.</summary>
    private static void Raise(Dictionary<int, List<string>> raised, IEnumerable<Refusal> unplain)
    {
        foreach (var refusal in unplain)
        {
            var found = raised.TryGetValue(PartOf(refusal), out var known) ? known : raised[PartOf(refusal)] = [];

            found.AddRange(refusal.Unplain.Select(objection => objection.Describe()).Where(described => !found.Contains(described)));
        }
    }

    /// <summary>The plain findings raised earlier on <paramref name="parts"/> that <paramref name="refusals"/> do not repeat.</summary>
    private static IReadOnlyList<string> Earlier(IReadOnlyDictionary<int, List<string>> raised, IEnumerable<int> parts, IReadOnlyList<Refusal> refusals)
    {
        var now = refusals.SelectMany(refusal => refusal.Unplain).Select(found => found.Describe()).ToHashSet();

        return [.. parts.SelectMany(part => raised.TryGetValue(part, out var found) ? found : []).Where(found => !now.Contains(found)).Distinct()];
    }

    /// <summary>The plain-wording findings raised earlier on the words a repair writes again, listed so the new words do not bring them back.</summary>
    private static void AppendEarlier(StringBuilder text, IReadOnlyList<string> earlier)
    {
        if (earlier.Count == 0)
        {
            return;
        }

        text.AppendLine();
        text.AppendLine("Earlier checks of these words also refused the following. The words written again must not bring any of them back, in these words or others:");

        foreach (var found in earlier)
        {
            text.Append("- ").AppendLine(found);
        }
    }

    /// <summary>
    /// <paramref name="text"/> with the first occurrence of <paramref name="quote"/>, ignoring case, replaced by
    /// <paramref name="plain"/>; null when the quote is not in it.
    /// </summary>
    private static string? Swapped(string? text, string quote, string plain)
    {
        var at = text?.IndexOf(quote, StringComparison.OrdinalIgnoreCase) ?? -1;

        return at < 0 ? null : string.Concat(text![..at], plain, text[(at + quote.Length)..]);
    }

    /// <summary>The lines with the quote replaced in the first line that holds it; null when none does.</summary>
    private static IReadOnlyList<AdventureLine>? Swapped(IReadOnlyList<AdventureLine> lines, string quote, string plain)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            if (Swapped(lines[index].Text, quote, plain) is { } text)
            {
                return [.. lines.Take(index), lines[index] with { Text = text }, .. lines.Skip(index + 1)];
            }
        }

        return null;
    }

    /// <summary>The beat with the quote replaced where its title, reason or lines hold it; null when none does.</summary>
    private static ReadBeat? Swapped(ReadBeat beat, string quote, string plain)
    {
        if (Swapped(beat.Lines, quote, plain) is { } lines)
        {
            return beat with { Lines = lines };
        }

        if (Swapped(beat.Title, quote, plain) is { } title)
        {
            return beat with { Title = title };
        }

        if (Swapped(beat.Reason, quote, plain) is { } reason)
        {
            return beat with { Reason = reason };
        }

        return null;
    }

    /// <summary>
    /// The draft with each plain refusal's rewrites put in place, the indexes changed (-1 for the opening), and the refusals left
    /// for the writer: any with an objection that has no rewrite or whose quote is not where the rewrite can go.
    /// </summary>
    private static (ReadAnswer Read, IReadOnlySet<int> Changed, IReadOnlyList<Refusal> Left) Rewritten(ReadAnswer read, IReadOnlyList<Refusal> refusals)
    {
        var beats = read.Beats.ToList();
        var opening = read.Opening;
        var changed = new HashSet<int>();
        var left = new List<Refusal>();

        foreach (var refusal in refusals)
        {
            if (!refusal.Plain || refusal.Unplain.Count == 0 || refusal.Unplain.Any(found => found.Plain is null))
            {
                left.Add(refusal);
                continue;
            }

            var index = refusal.Opening ? -1 : refusal.Objectives[0];
            var (trialBeats, trialOpening) = (beats.ToList(), opening);
            var applied = true;

            foreach (var found in refusal.Unplain)
            {
                if (index < 0)
                {
                    var lines = Swapped(trialOpening, found.Quote, found.Plain!);
                    applied = lines is not null;
                    trialOpening = lines ?? trialOpening;
                }
                else
                {
                    var beat = Swapped(trialBeats[index], found.Quote, found.Plain!);
                    applied = beat is not null;
                    trialBeats[index] = beat ?? trialBeats[index];
                }

                if (!applied)
                {
                    break;
                }
            }

            if (!applied)
            {
                left.Add(refusal);
                continue;
            }

            (beats, opening) = (trialBeats, trialOpening);
            changed.Add(index);
        }

        return (read with { Beats = beats, Opening = opening }, changed, left);
    }

    /// <summary>The title and spine with each objection's rewrite put in place, and the objections left for the spine to be written again.</summary>
    private static (string Name, AdventureSpine Spine, IReadOnlyList<PlainObjection> Left) Rewritten(string name, AdventureSpine spine, IReadOnlyList<PlainObjection> objections)
    {
        var left = new List<PlainObjection>();

        foreach (var found in objections)
        {
            var plain = found.Plain;
            var swapped = plain is null ? null : found.Part switch
            {
                "title" => Swapped(name, found.Quote, plain) is { } title ? (name = title) : null,
                "premise" => Swapped(spine.Premise, found.Quote, plain) is { } premise ? (spine = spine with { Premise = premise }).Premise : null,
                "want" => Swapped(spine.Want, found.Quote, plain) is { } want ? (spine = spine with { Want = want }).Want : null,
                "stake" => Swapped(spine.Stake, found.Quote, plain) is { } stake ? (spine = spine with { Stake = stake }).Stake : null,
                "turn" => Swapped(spine.Turn, found.Quote, plain) is { } turn ? (spine = spine with { Turn = turn }).Turn : null,
                "ending" => Swapped(spine.Ending, found.Quote, plain) is { } ending ? (spine = spine with { Ending = ending }).Ending : null,
                _ => null,
            };

            if (swapped is null)
            {
                left.Add(found);
            }
        }

        return (name, spine, left);
    }

    private static StoryCall PlainInstruction(IReadOnlyList<PlainPart> parts)
    {
        var text = new StringBuilder();

        text.AppendLine(
            "You are checking the wording of a draft of a story the Commander will fly in Elite Dangerous. The words below are read or "
            + "spoken to the Commander. The rule they were written under:");
        text.AppendLine();
        text.AppendLine(PlainWords.Rule);
        text.AppendLine();
        text.AppendLine(
            "List a phrase only where it breaks that rule. \"figurative\" is a metaphor or an image standing for a game thing or event, "
            + "such as a rank called a line in a book, or a closing line that counts books. \"invented\" is a feature a game system does not "
            + "have, such as an address line on a Powerplay rank. A real object in the story, such as a logbook, a note or a station's door, "
            + "is not an objection.");
        text.AppendLine();
        text.AppendLine("Parts:");

        foreach (var part in parts)
        {
            text.AppendLine($"[{part.Name}] {part.Text}");
        }

        text.AppendLine();
        text.AppendLine(
            "Answer with one JSON object and nothing else: " + StoryAnswer.PlainShape
            + ". \"part\" is the name in brackets; \"quote\" is the words that break the rule, copied exactly from that part; \"reason\" is "
            + "one plain sentence saying what they stand for or what the game does not have; \"plain\" is the quoted words written again in "
            + "the game's own terms, to be put in their place as they stand, so they fit the sentence around them and state nothing the part "
            + "did not. For \"invented\", give \"\" for \"plain\". Use an empty list when every part is plain.");

        return new StoryCall(text.ToString(), StoryAnswer.PlainSchema());
    }

    /// <summary>Rounds of replacements a draft's plain-wording objections get, counted apart from the other two.</summary>
    public const int PlainRepairRounds = 2;

    private const string NotPlain = "The story is not written plainly: ";

    /// <summary>Rounds of replacements a draft's refused objectives get before the draft fails.</summary>
    public const int RepairRounds = 2;

    /// <summary>Rounds of replacements a draft's fidelity objections get, counted apart from <see cref="RepairRounds"/>.</summary>
    public const int FidelityRepairRounds = 2;

    private void LogRefusals(AdventureAsk ask, string round, string step, IReadOnlyList<Refusal> refusals)
    {
        foreach (var refusal in refusals)
        {
            logger.LogInformation("Draft {Draft}, {Round} round, {Step} step refused: {Refusal}", ask.Draft, round, step, refusal.Text);
        }
    }

    private AdventureOutcome Failed(string? reply, string detail, IReadOnlyList<string> notes, long started, Asked? asked = null)
    {
        var seconds = Stopwatch.GetElapsedTime(started).TotalSeconds;

        if (asked is null)
        {
            logger.LogWarning("A story was not written: {Refusal} ({Seconds:F1}s since the write began)", detail, seconds);
        }
        else
        {
            logger.LogWarning(
                "A story was not written at the {Step} step by {Model}: {Refusal} {Why} ({Seconds:F1}s since the write began)",
                asked.Step,
                asked.Model,
                detail,
                asked.Why,
                seconds);
        }

        return new AdventureOutcome(null, reply, detail, notes)
        {
            Internal = true,
            UnreadReply = asked?.Reply.Line,
            UnreadStep = asked?.Reply.Line is null ? null : asked.Step,
            ContextMessage = asked?.Reply switch
            {
                { CutOff: { AtContext: true } cutOff } => AdventureOutcome.ContextLimitMessage(cutOff),
                { ContextExceeded: true } refused => AdventureOutcome.ContextOverflowMessage(refused.PromptTokens),
                _ => null,
            },
        };
    }

    /// <summary>The outcome when the model gave no answer: stopped when the write was cancelled, otherwise failed with <paramref name="detail"/>.</summary>
    private AdventureOutcome Unanswered(string detail, IReadOnlyList<string> notes, long started, Asked asked, CancellationToken cancellationToken) =>
        cancellationToken.IsCancellationRequested ? AdventureOutcome.Stopped : Failed(null, detail, notes, started, asked) with { Unanswered = true };

    private const string UnknownSpeaker = "A line is given to ";

    private const string Unconfirmed = "Fidelity check: ";

    /// <summary>The dry run with a refusal added for each line a story chapter gives to a speaker the story does not have.</summary>
    private static Resolved Speakers(Resolved resolved, ReadAnswer read, AdventureAsk ask)
    {
        if (ask.Story is not { } story)
        {
            return resolved with { Opening = [.. read.Opening.Select(line => line with { Speaker = null })] };
        }

        var offset = ask.Rewrite?.From ?? 0;
        var opening = ask.Rewrite is null ? read.Opening : [];
        var unknown = read.Beats.SelectMany(beat => beat.Lines.Select(line => line.Speaker))
            .Concat(opening.Select(line => line.Speaker))
            .Where(speaker => speaker is not null && Speaker(speaker, story) is null)
            .Distinct(StringComparer.Ordinal)
            .Select(speaker => new Refusal(
                $"{UnknownSpeaker}\"{speaker}\", who is not one of the story's speakers: {string.Join(", ", Speakers(story))}.",
                [.. read.Beats.Select((beat, index) => (beat, index)).Where(written => written.beat.Lines.Any(line => line.Speaker == speaker)).Select(written => written.index)])
            {
                Opening = opening.Any(line => line.Speaker == speaker),
            })
            .ToList();

        var spoken = resolved with
        {
            Beats = [.. resolved.Beats.Select(beat => beat with { Lines = [.. beat.Lines.Select(line => line with { Speaker = Speaker(line.Speaker, story) })] })],
            Opening = [.. opening.Select(line => line with { Speaker = Speaker(line.Speaker, story) })],
        };
        var selfNamed = SelfNaming(spoken, story)
            .Select(found => Charged(
                found.Objective,
                $"{SelfNamed}\"{found.Member.Name}\" in {Where(found.Objective, offset)}: a line given to \"{found.Member.Id}\" is what {found.Member.Name} says, in the first person; "
                + $"narration that describes {found.Member.Name} is given to \"{Stories.StorySpeaker.Narrator}\"."));

        var firstPerson = NarratedFirstPerson(spoken, story)
            .Select(objective => Charged(
                objective,
                $"{NarratorAsItself}{Capital(Where(objective, offset))} is read by the narrator and speaks in the first person or to the Commander; "
                + "the narrator tells the story in the third person from outside the cockpit, never says \"I\", \"me\" or \"we\", never speaks to the Commander, "
                + "and never offers, asks or waits for anything. Rewrite it as narration."));

        var unplayed = UnplayedRecordings(spoken, story)
            .Select(objective => Charged(
                objective,
                $"{Unplayed}{Capital(Where(objective, offset))} says a recording, call or message is played or heard, and nobody in it says that person's words. "
                + "Give the person's words a line of their own, given to them, in the same place, or leave the recording out."));

        var narratedOffers = NarratedOffers(spoken, story, ask)
            .Select(objective => Charged(
                objective,
                $"{Capital(Where(objective, offset))} begins with a choice, and the line before it, which offers the choice, would be read by the narrator. "
                + "The narrator never offers anything: give the last line before the choice to the ship or a character."));

        return spoken with { Refusals = [.. resolved.Refusals, .. unknown, .. selfNamed, .. firstPerson, .. unplayed, .. narratedOffers] };
    }

    /// <summary>The objectives that begin with a choice whose offering line, the last line before it, the narrator would read.</summary>
    private static IEnumerable<int> NarratedOffers(Resolved spoken, AdventureStory story, AdventureAsk ask)
    {
        bool Narrated(string? speaker) =>
            speaker == Stories.StorySpeaker.Narrator || (story.CoreIsStock && (speaker is null || speaker == Stories.StorySpeaker.Ship));

        IReadOnlyList<AdventureLine> before = ask.Rewrite is { } rewrite
            ? rewrite.From > 0 ? rewrite.Chapter.Beats[rewrite.From - 1].Lines : rewrite.Chapter.OpeningLines
            : spoken.Opening;

        for (var position = 0; position < spoken.Beats.Count; position++)
        {
            var joins = position < spoken.Joins.Count && spoken.Joins[position];

            if (!joins && spoken.Beats[position].OpensWithChoice && (before.Count == 0 || Narrated(before[^1].Speaker)))
            {
                yield return position < spoken.Stood.Count ? spoken.Stood[position] : position;
            }

            if (!joins)
            {
                before = spoken.Beats[position].Lines;
            }
        }
    }

    private const string NarratorAsItself = "The narrator speaks as itself: ";

    private const string Unplayed = "A recording is mentioned and not played: ";

    private static readonly Regex Sentences = new(@"[^.!?]+", RegexOptions.CultureInvariant);

    private static readonly Regex Playing = new(
        @"\b(?:play|plays|played|playing|replay|replays|replayed|replaying|hear|hears|heard|hearing|listen|listens|listened|listening)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Recorded = new(
        @"\b(?:recordings?|messages?|calls?|tapes?|logs?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Whether one sentence of the text says a recording, call or message is played or heard.</summary>
    private static bool PlaysARecording(string? text) =>
        !string.IsNullOrEmpty(text)
        && Sentences.Matches(text).Any(sentence => Playing.IsMatch(sentence.Value) && Recorded.IsMatch(sentence.Value));

    /// <summary>The opening and the beats where a line not given to the cast plays a recording and no cast member speaks.</summary>
    private static IEnumerable<int> UnplayedRecordings(Resolved spoken, AdventureStory story)
    {
        var cast = (story.Cast ?? []).Select(member => member.Id).ToHashSet(StringComparer.Ordinal);

        if (cast.Count == 0)
        {
            yield break;
        }

        foreach (var (lines, _, objective) in Said(spoken))
        {
            if (!lines.Any(line => line.Speaker is { } speaker && cast.Contains(speaker))
                && lines.Any(line => PlaysARecording(line.Text)))
            {
                yield return objective;
            }
        }
    }

    private static readonly Regex Words = new(@"[A-Za-z]+(?:['’][A-Za-z]+)*", RegexOptions.CultureInvariant);

    private static readonly HashSet<string> FirstPersonWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "me", "my", "mine", "myself", "we", "we're", "we'll", "we've", "us", "our", "ours",
    };

    private static readonly HashSet<string> FirstPersonI = new(StringComparer.Ordinal) { "I", "I'm", "I'll", "I've", "I'd" };

    /// <summary>Whether the text holds a first-person word as a whole word; "I" only in capitals.</summary>
    internal static bool FirstPerson(string? text) =>
        !string.IsNullOrEmpty(text)
        && Words.Matches(text).Any(word =>
        {
            var plain = word.Value.Replace('’', '\'');

            return FirstPersonI.Contains(plain) || FirstPersonWords.Contains(plain);
        });

    /// <summary>The opening and the beats whose narrator-read lines are in the first person; a stock core's ship lines, and a line with no speaker, are the narrator's.</summary>
    private static IEnumerable<int> NarratedFirstPerson(Resolved spoken, AdventureStory story)
    {
        bool Narrated(string? speaker) =>
            speaker == Stories.StorySpeaker.Narrator || (story.CoreIsStock && (speaker is null || speaker == Stories.StorySpeaker.Ship));

        foreach (var (lines, _, objective) in Said(spoken))
        {
            if (lines.Any(line => Narrated(line.Speaker) && FirstPerson(line.Text)))
            {
                yield return objective;
            }
        }
    }

    /// <summary>
    /// The opening's lines at position -1, then each beat that stood at its position, each with the index of the
    /// objective it was written as; the opening's is -1.
    /// </summary>
    private static IEnumerable<(IReadOnlyList<AdventureLine> Lines, int Position, int Objective)> Said(Resolved resolved) =>
        resolved.Beats
            .Select((beat, position) => (beat.Lines, position, position < resolved.Stood.Count ? resolved.Stood[position] : position))
            .Prepend((resolved.Opening, -1, -1));

    /// <summary>"the opening" or "objective N", numbered in the chapter, for a refusal.</summary>
    private static string Where(int index, int offset) => index < 0 ? "the opening" : $"objective {offset + index + 1}";

    private static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>A refusal charged to the objective at <paramref name="index"/>, or to the opening when it is -1.</summary>
    private static Refusal Charged(int index, string text) => index < 0 ? new Refusal(text, []) { Opening = true } : new Refusal(text, [index]);

    /// <summary>Drops the narrator first-person and unplayed-recording refusals, so a line still refused after the last repair stands.</summary>
    private static Resolved KeepWhatTheRewriteKept(Resolved resolved) =>
        resolved with
        {
            Refusals =
            [
                .. resolved.Refusals.Where(refusal =>
                    !refusal.Text.StartsWith(NarratorAsItself, StringComparison.Ordinal) && !refusal.Text.StartsWith(Unplayed, StringComparison.Ordinal)),
            ],
        };

    private const string SelfNamed = "A line names its own speaker: ";

    /// <summary>
    /// The lines given to a cast member that name that member by their shown name other than to introduce
    /// themselves, by beat position, objective and line; the opening is position and objective -1.
    /// </summary>
    private static IEnumerable<(int Position, int Objective, int Line, AdventureSpeaker Member)> SelfNaming(Resolved resolved, AdventureStory story)
    {
        foreach (var (lines, position, objective) in Said(resolved))
        {
            for (var line = 0; line < lines.Count; line++)
            {
                var said = lines[line];

                if ((story.Cast ?? []).FirstOrDefault(member => member.Id == said.Speaker) is { } member
                    && AdventureMention.Holds(WithoutIntroductions(said.Text, member.Name), member.Name))
                {
                    yield return (position, objective, line, member);
                }
            }
        }
    }

    /// <summary>The text with each self-introduction by this name taken out: "This is Ines Harrow", "my name is …", "Harrow here".</summary>
    private static string WithoutIntroductions(string text, string name)
    {
        var shown = name.Trim();

        if (shown.StartsWith("The ", StringComparison.OrdinalIgnoreCase))
        {
            shown = shown[4..];
        }

        var named = Regex.Escape(shown);

        return Regex.Replace(
            text,
            $@"\b(?:(?:this is|my name is|I am|I['’]m|it['’]s)\s+{named}\b(?!['’])|{named}\s+here\b)",
            " ",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>Gives each line that still names its own speaker to the narrator and drops that refusal.</summary>
    private static Resolved NarrateSelfNaming(Resolved resolved, AdventureAsk ask)
    {
        if (ask.Story is not { } story)
        {
            return resolved;
        }

        var named = SelfNaming(resolved, story).Select(found => (found.Position, found.Line)).ToHashSet();

        return resolved with
        {
            Opening = [.. resolved.Opening.Select((line, at) => named.Contains((-1, at)) ? line with { Speaker = Stories.StorySpeaker.Narrator } : line)],
            Beats =
            [
                .. resolved.Beats.Select((beat, index) => beat with
                {
                    Lines = [.. beat.Lines.Select((line, at) => named.Contains((index, at)) ? line with { Speaker = Stories.StorySpeaker.Narrator } : line)],
                }),
            ],
            Refusals = [.. resolved.Refusals.Where(refusal => !refusal.Text.StartsWith(SelfNamed, StringComparison.Ordinal))],
        };
    }

    /// <summary>The cast ids aboard for a story chapter: those the story puts aboard, then those the chapter adds, in the cast's own spelling.</summary>
    private static IReadOnlyList<string> Aboard(AdventureStory story, IReadOnlyList<string> added) =>
    [
        .. (story.Aboard ?? [])
            .Concat(added.Select(id => (story.Cast ?? []).FirstOrDefault(member => string.Equals(member.Id, id.Trim(), StringComparison.OrdinalIgnoreCase))?.Id).OfType<string>())
            .Distinct(StringComparer.Ordinal),
    ];

    /// <summary>The ids a story chapter's lines may name: the ship, the narrator and the cast.</summary>
    private static IEnumerable<string> Speakers(AdventureStory story) =>
        [Stories.StorySpeaker.Ship, Stories.StorySpeaker.Narrator, .. (story.Cast ?? []).Select(member => member.Id)];

    /// <summary>The story's own spelling of a speaker id the model wrote in any case, or the id of the one cast member whose full name, first word or last word it matches; null otherwise.</summary>
    private static string? Speaker(string? written, AdventureStory? story)
    {
        if (written is null || story is null)
        {
            return null;
        }

        var trimmed = written.Trim();

        if (Speakers(story).FirstOrDefault(id => string.Equals(id, trimmed, StringComparison.OrdinalIgnoreCase)) is { } known)
        {
            return known;
        }

        var named = (story.Cast ?? [])
            .Where(member =>
            {
                var words = member.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                return words.Length > 0
                    && (string.Equals(member.Name.Trim(), trimmed, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(words[0], trimmed, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(words[^1], trimmed, StringComparison.OrdinalIgnoreCase));
            })
            .Select(member => member.Id)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return named.Count == 1 ? named[0] : null;
    }

    /// <summary>
    /// The rewrite with its remark read into constraints: the place it asks to travel to, found by the galaxy search, or the system it
    /// named that the search does not know; the drawable activity it asks for, with the count it said or one d47 sets; and the reply.
    /// Unchanged when the remark could not be read. A place is read only where <see cref="ChapterPull.MayHead"/> allows one.
    /// </summary>
    private async Task<AdventureRewrite> ChangeAsync(AdventureAsk ask, AdventureRewrite rewrite, Facts facts, List<string> notes, CancellationToken cancellationToken)
    {
        var home = HomeSystem?.Invoke() is { } set && !string.IsNullOrWhiteSpace(set) ? set.Trim() : null;
        var heads = ChapterPull.MayHead(ask.Story);
        var drawable = Drawable(ask);
        var asked = await AskJsonAsync(HeadingStep, HeadingInstruction(rewrite, facts, home, heads, drawable), HeadingOutputCeiling, ask.Progress, cancellationToken, ThinkingEffort.Low).ConfigureAwait(false);

        if ((asked.Json is null ? null : ReadHeading(asked.Json)) is not { } read)
        {
            logger.LogInformation("Draft {Draft}, the change step was not read: {Why}", ask.Draft, asked.Why);
            return rewrite;
        }

        rewrite = rewrite with { Reply = read.Reply };

        if (read.Activity is { } id && drawable.FirstOrDefault(choice => choice.Id == id) is { } chosen)
        {
            var row = chosen.Row;
            var count = read.Count is > 0 and var said
                ? said
                : ChapterDraw.CountOf(row, state()?.Ship ?? ShipLoadout.Unknown, ChapterDraw.Seed(rewrite.Chapter.Key, rewrite.From, rewrite.Step));

            rewrite = rewrite with { Activity = new DrawnActivity(row.Kind, row.Family, count) { Organic = row.Organic, PlaceBefore = row.PlaceBefore } };
        }

        var (system, standIn) = !heads ? (null, false) : read.Home ? (home ?? HomeLeg.TargetFrom(facts.Position), home is null) : (read.System, false);
        logger.LogInformation(
            "Draft {Draft}, the change step answered home {Home}, system {System}, activity {Activity}, count {Count}; the place is {Place}",
            ask.Draft, read.Home, read.System ?? "none", read.Activity ?? "none", read.Count?.ToString(CultureInfo.InvariantCulture) ?? "none", system ?? "none");

        if (system is null || galaxy() is not { } search)
        {
            return rewrite;
        }

        try
        {
            if (await LookUpAsync(search, system, facts, cancellationToken).ConfigureAwait(false) is { } found)
            {
                logger.LogInformation("Draft {Draft}, the changed objective asks to go to {System}", ask.Draft, found.Name);
                return rewrite with { Heading = new AdventureHeading(found.Name, found.Address, found.Position, standIn) { AskedHome = read.Home } };
            }
        }
        catch (GalaxyUnavailableException ex)
        {
            logger.LogWarning("Draft {Draft}, the galaxy search could not look up {System}: {Message}", ask.Draft, system, ex.Message);
            notes.Add($"The galaxy search could not look up {system} ({ex.Message}), so the new objectives were not sent toward it.");
            return rewrite;
        }

        logger.LogInformation("Draft {Draft}, the changed objective names {System}, which the galaxy search does not know", ask.Draft, system);
        return rewrite with { Unfound = system };
    }

    /// <summary>A drawable activity a changed objective may ask for, by the id the change step names it with.</summary>
    private sealed record DrawableChoice(string Id, DrawableActivity Row, string Phrase);

    /// <summary>The activities a changed objective may ask for: every row of <see cref="ChapterActivities"/> not refused or barred, and no crime when the Commander refused crime.</summary>
    private static IReadOnlyList<DrawableChoice> Drawable(AdventureAsk ask)
    {
        var refused = ask.Story?.Refused ?? ask.Barred ?? [];

        return
        [
            .. ChapterActivities.All
                .Where(row => !RefusedActivities.Refuses(refused, row.Kind, row.Family)
                    && !(ask.CrimeRefused && row.Kind == TriggerKind.Mission && (MissionFamilies.IsIllegal(row.Family) || CrimeObjectives.IsFamily(row.Family))))
                .Select(row => (Row: row, Key: RefusedActivities.Key(row.Kind, row.Family)))
                .Where(pair => pair.Key is not null)
                .Select(pair => new DrawableChoice(
                    pair.Row.Organic is { } organic ? $"{pair.Key}:{(organic ? "organic" : "cartographic")}" : pair.Key!,
                    pair.Row,
                    RefusedActivities.Phrase(pair.Key!) + (pair.Row.Organic is { } kind ? (kind ? ", organic data" : ", cartographic data") : string.Empty)))
                .DistinctBy(choice => choice.Id, StringComparer.Ordinal),
        ];
    }

    /// <summary>A system named in any case, as the galaxy search spells it, with its address and position; null when the search does not know it.</summary>
    private async Task<(string Name, long Address, StarPosition Position)?> LookUpAsync(IGalaxyService search, string system, Facts facts, CancellationToken cancellationToken)
    {
        // The galaxy search matches a reference system only in its exact case; a home system is typed in any case.
        if (Systems?.Invoke() is { } names
            && (await names.MatchNamesAsync(system, cancellationToken).ConfigureAwait(false))
                .FirstOrDefault(match => string.Equals(match.Name, system, StringComparison.OrdinalIgnoreCase)) is { } named)
        {
            return (named.Name, named.SystemAddress, named.Position);
        }

        // The search answers 400 for a name it has no system for, which SearchAsync reports as an outage; a position lookup reports it as unknown.
        if (await search.PositionAsync(system, cancellationToken).ConfigureAwait(false) is { } position
            && await new AdventureResolver(search, facts.Standings) { Time = Time }.SystemAsync(system, cancellationToken).ConfigureAwait(false) is { } found)
        {
            return (found.Name, found.Address, position);
        }

        return null;
    }

    private static StoryCall HeadingInstruction(AdventureRewrite rewrite, Facts facts, string? home, bool heads, IReadOnlyList<DrawableChoice> drawable)
    {
        var text = new StringBuilder();
        var current = rewrite.Chapter.Beats[rewrite.From];

        text.AppendLine(
            "The Commander is flying a story and wants to change the objective they were on. Read what they said into what d47 builds the new "
            + "objectives from: the place they ask to travel to, the activity they ask for, and how many times when they say a number. "
            + "Do not judge whether it suits the story, and do not choose a place or an activity they did not ask for.");
        text.AppendLine();
        text.AppendLine($"The objective they are changing: {current.Steps[Math.Min(rewrite.Step, current.Steps.Count - 1)].Trigger.Describe()}.");

        if (heads)
        {
            text.AppendLine();
            text.AppendLine("Places they may mean without naming a system:");
            text.AppendLine(home is null ? "- Home: they have set no home system." : $"- Home: {home}.");

            if (facts.Carrier is { Owned: true, StarSystem: { Length: > 0 } carrier })
            {
                text.AppendLine($"- Their fleet carrier: in {carrier}.");
            }

            text.AppendLine("- Sol: the centre of the bubble, populated space.");
            text.AppendLine("- Colonia: the settled region 22,000 light years from Sol.");
            text.AppendLine($"- Where they are now: {facts.System ?? "unknown"}.");
        }

        text.AppendLine();
        text.AppendLine("The activities they may ask for, by id:");

        foreach (var choice in drawable)
        {
            text.AppendLine($"- {choice.Id}: {choice.Phrase}");
        }

        AppendExchange(text, rewrite.Exchange, rewrite.Remark);
        text.AppendLine();
        text.AppendLine(
            "Answer with one JSON object and nothing else: " + StoryAnswer.HeadingShape([.. drawable.Select(choice => choice.Id)]) + ". "
            + (heads
                ? "\"home\" is true when they ask to go home. Otherwise \"system\" is the star system they ask to go to, spelt as the game spells it: one they name, "
                  + "or the system of a place above that they mean. \"home\" is false and \"system\" is null when they name no place to go. "
                : "This chapter cannot travel somewhere new, so \"home\" is false and \"system\" is null. ")
            + "\"activity\" is the id of the activity above they ask for, or null when they ask for none of them. "
            + "\"count\" is the number they said for it, or null when they said none; d47 sets the count then. "
            + "\"reply\" is what you say to the Commander, in your own voice, in one or two sentences: what the new objectives will have them do, "
            + "or, when they named no place and no activity above, ask them to name one. Your reply never states the hidden story.");

        return new StoryCall(text.ToString(), StoryAnswer.HeadingSchema([.. drawable.Select(choice => choice.Id)]));
    }

    /// <summary>The change step's answer, or null when it cannot be read.</summary>
    private static (string? System, bool Home, string? Activity, int? Count, string? Reply)? ReadHeading(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true });
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var system = Text(root, "system");

            return (
                string.IsNullOrWhiteSpace(system) ? null : system.Trim(),
                root.TryGetProperty("home", out var home) && home.ValueKind == JsonValueKind.True,
                Text(root, "activity") is { Length: > 0 } activity ? activity.Trim() : null,
                Integer(root, "count"),
                Text(root, "reply") is { Length: > 0 } reply ? reply.Trim() : null);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The facts with the light years from the Commander to the story's destination, when it has one and both can be measured.</summary>
    private Task<Facts> DestinationDistanceAsync(Facts facts, AdventureAsk ask, CancellationToken cancellationToken) =>
        ask.Story?.Destination is { } destination ? DestinationDistanceAsync(facts, destination, cancellationToken) : Task.FromResult(facts);

    private async Task<Facts> DestinationDistanceAsync(Facts facts, AdventureDestination destination, CancellationToken cancellationToken)
    {
        if (facts.System is not { } here || galaxy() is not { } search)
        {
            return facts;
        }

        if (string.Equals(here, destination.System, StringComparison.OrdinalIgnoreCase))
        {
            return facts with { DestinationLightYears = 0 };
        }

        try
        {
            return facts with { DestinationLightYears = await search.DistanceAsync(here, destination.System, cancellationToken).ConfigureAwait(false) };
        }
        catch (GalaxyUnavailableException)
        {
            return facts;
        }
    }

    /// <summary>
    /// Loads or reloads the story model at <see cref="AdventureOutcome.StoryContextTokens"/> when its server reports it
    /// below that; null when the write may go on, else the outcome that stops it.
    /// </summary>
    private async Task<AdventureOutcome?> RoomForAStoryAsync(AdventureAsk ask, CancellationToken cancellationToken)
    {
        if (provider() is not { } asking || asking is not IModelLoader loader)
        {
            return null;
        }

        var chosen = model() ?? asking.DefaultModel;
        var step = AdventureOutcome.StepFor(await loader.LoadedContextAsync(chosen, cancellationToken).ConfigureAwait(false));

        if (step == AdventureOutcome.ContextStep.Nothing)
        {
            return null;
        }

        ask.Progress?.Reloading(chosen, loaded: step == AdventureOutcome.ContextStep.Reload);
        var reload = await loader.ReloadAsync(chosen, AdventureOutcome.StoryContextTokens, cancellationToken).ConfigureAwait(false);

        if (reload.Loaded)
        {
            return null;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return AdventureOutcome.Stopped;
        }

        logger.LogWarning("{Model} could not be loaded with {Tokens} tokens of context: {Detail}", chosen, AdventureOutcome.StoryContextTokens, reload.Detail);

        return new AdventureOutcome(null, null, AdventureOutcome.ReloadFailedMessage(chosen), [])
        {
            Internal = true,
            Unanswered = true,
            ContextMessage = AdventureOutcome.ReloadFailedMessage(chosen),
        };
    }

    private string? Blocked()
    {
        if (provider() is null)
        {
            return "No language model is configured, and a short story has to be written by one.";
        }

        if (galaxy() is null)
        {
            return "Galaxy search is off. A generated short story needs it to check that its places are real.";
        }

        return null;
    }

    /// <summary>
    /// The best-rated notable places within reach of the start; for a chapter that travels toward a target, those around the
    /// waypoint, nearest it first.
    /// </summary>
    private async Task<IReadOnlyList<NotablePlace>> NotableAsync(AdventureAsk ask, Facts facts, CancellationToken cancellationToken, List<string> notes)
    {
        if (places() is not { } catalogue || facts.Position is not { } here)
        {
            return [];
        }

        try
        {
            if (ChapterPull.Of(ask, here) is { Arrived: false } leg)
            {
                var waypoint = ChapterPull.Waypoint(here, leg.There, facts.RadiusLightYears);
                var around = await catalogue.NearAsync(waypoint, ChapterPull.PlacesWithin * facts.RadiusLightYears, int.MaxValue, cancellationToken).ConfigureAwait(false);

                return [.. around.OrderBy(place => place.DistanceFrom(waypoint)).Take(MostNotable)];
            }

            return await catalogue.NearAsync(here, facts.RadiusLightYears, MostNotable, cancellationToken).ConfigureAwait(false);
        }
        catch (GalaxyUnavailableException ex)
        {
            notes.Add($"The catalogue of notable places could not be read ({ex.Message}), so the stops came from the galaxy search alone.");
            return [];
        }
    }

    /// <summary>
    /// The step back to a <see cref="ShipBase"/>: a dock at <see cref="MarketId"/> where the station is known by name,
    /// otherwise an arrival in the system. <see cref="Position"/> is null when the galaxy search could not place it.
    /// </summary>
    private sealed record BaseStep(ShipBase Base, string System, long? SystemAddress, string? Station, long? MarketId, StarPosition? Position)
    {
        public bool Docks => Station is not null && MarketId is not null;

        /// <summary>Whether <paramref name="trigger"/> is this step.</summary>
        public bool Is(AdventureTrigger? trigger) =>
            Docks
                ? trigger is { Kind: TriggerKind.Dock } && trigger.MarketId == MarketId
                : trigger is { Kind: TriggerKind.Arrive }
                  && (SystemAddress is { } address ? trigger.SystemAddress == address : string.Equals(trigger.System, System, StringComparison.OrdinalIgnoreCase));

        /// <summary>The step as the writer is told it.</summary>
        public string Step => Docks ? $"\"dock\" at {Station} in {System}" : $"\"arrive\" in {System}";

        /// <summary>What the base is, after its system's name.</summary>
        public string What => Base.IsHomeSystem ? "the Commander's home system" : "where most of the Commander's stored ships are";
    }

    private enum InBandState
    {
        /// <summary>No band applies, so nothing was searched.</summary>
        NotSearched,
        Listed,
        NoneFound,
        Unavailable,
    }

    /// <summary>The stations and landable bodies inside a chapter's Sol band and within reach of its start.</summary>
    private sealed record InBand(InBandState State, IReadOnlyList<StationSummary> Stations, IReadOnlyList<BodySummary> Bodies)
    {
        public static readonly InBand NotSearched = new(InBandState.NotSearched, [], []);

        /// <summary>Whether the draft is held to the band: false only when the search found no place inside it.</summary>
        public bool Holds => State != InBandState.NoneFound;
    }

    /// <summary>The stations and landable bodies around the waypoint of a travelling chapter.</summary>
    private sealed record PullPlaces(InBandState State, IReadOnlyList<StationSummary> Stations, IReadOnlyList<BodySummary> Bodies)
    {
        public static readonly PullPlaces NotSearched = new(InBandState.NotSearched, [], []);

        /// <summary>Whether the chapter is held to the rule: false when no place was searched for or found.</summary>
        public bool Holds => State == InBandState.Listed;
    }

    private const int InBandPerKind = 5;

    /// <summary>The most systems whose bodies and biology are read for a drawn map, signal or organic activity.</summary>
    public const int SupplySystems = 3;

    /// <summary>The most stations, and the most landable bodies, searched for around a sight.</summary>
    public const int PlacesPerSight = 10;

    /// <summary>
    /// The places a drawn chapter of <paramref name="ask"/> may use, with the facts <see cref="ChapterPlanner"/> reads, searched only as
    /// far as the chapter needs. Searches that do not depend on each other run together.
    /// </summary>
    public Task<ChapterPlaces> ChapterPlacesAsync(AdventureAsk ask, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ask);

        return ChapterPlacesAsync(ask, ask.Story?.Slots ?? [], cancellationToken);
    }

    /// <summary>The places a chapter of <paramref name="ask"/> may use for <paramref name="slots"/>; a rewrite and a short story also gather stations for their stops.</summary>
    private async Task<ChapterPlaces> ChapterPlacesAsync(AdventureAsk ask, IReadOnlyList<DrawnActivity> slots, CancellationToken cancellationToken)
    {
        var facts = Facts.Of(state(), ask);
        var sights = ask.Story?.Sights ?? [];
        var used = UsedSystems(ask.Chapter, ask.Story?.Id);
        var notes = Enumerable.Range(0, 14).Select(_ => new List<string>()).ToArray();
        var notableTask = NotableAsync(ask, facts, cancellationToken, notes[0]);

        if (galaxy() is not { } search || facts.System is not { } here)
        {
            return ChapterPlaces.None with { Notable = await notableTask.ConfigureAwait(false), Sights = sights, Used = used };
        }

        AdventureResolver Resolver() => new(search, facts.Standings) { ClosedMarketId = facts.ClosedMarketId, Time = Time };

        var crime = !ask.CrimeRefused && slots.Any(slot => slot.Kind == TriggerKind.Mission && (MissionFamilies.IsIllegal(slot.Family) || CrimeObjectives.IsFamily(slot.Family)));
        var war = slots.Any(slot => slot.Kind is TriggerKind.Bond or TriggerKind.Conflict);
        var supply = slots.Any(slot => slot.Kind is TriggerKind.Map or TriggerKind.Signal or TriggerKind.Organic);
        var docks = Travels(ask) || ask.Rewrite is not null || ask.Story is null || slots.Any(slot => slot.PlaceBefore || (ChapterActivities.Find(slot.Kind, slot.Family, slot.Organic) is { } row && ChapterActivities.NeedsStation(row)));

        var anarchyTask = crime ? AnarchyAsync(search, facts, here, cancellationToken, notes[1]) : Task.FromResult<IReadOnlyList<SystemSummary>>([]);
        var sitesTask = SitesAsync(search, facts, here, cancellationToken, notes[2]);
        var frontlineTask = slots.Any(slot => slot.Kind == TriggerKind.OnFootBond)
            ? FrontlineAsync(search, facts, here, Resolver(), cancellationToken, notes[3])
            : Task.FromResult<IReadOnlyList<StationSummary>>([]);
        var stationsTask = docks ? NearStationsAsync(search, facts, here, Resolver(), cancellationToken, notes[4]) : Task.FromResult<IReadOnlyList<StationSummary>>([]);
        var bodiesTask = NearBodiesAsync(search, facts, here, cancellationToken, notes[5]);
        var inBandTask = InBandAsync(search, Resolver(), ask, facts, cancellationToken, notes[6]);
        var destinationAt = ask.Story is { FinaleChapter: > 1, Destination: { } destination }
            ? await DestinationPositionAsync(search, destination, cancellationToken).ConfigureAwait(false)
            : null;
        var destinationsTask = DestinationBodiesAsync(search, ask, facts, cancellationToken, notes[12]);
        var pullTask = PullAsync(
            search,
            Resolver(),
            ask,
            facts,
            cancellationToken,
            notes[7],
            destinationAt is { } there && facts.Position is { } start && start.DistanceTo(there) > facts.RadiusLightYears
                ? new ChapterLeg(ask.Story!.Destination!.System, there, Home: false, Arrived: false)
                : null);
        var baseTask = ShipBases.Applies(ask) ? BaseAsync(search, ask, facts, cancellationToken) : Task.FromResult<BaseStep?>(null);
        var sightTask = AroundSightsAsync(search, sights, facts, Resolver(), docks, cancellationToken, notes[8]);
        var missionsTask = MissionStopsAsync(search, facts, cancellationToken, notes[9]);
        var swapTask = ask.Story is { FinaleChapter: null, Beacon.SwapSystem: { } parked } ? SwapPositionAsync(search, parked, cancellationToken, notes[13]) : Task.FromResult<StarPosition?>(null);
        var warTask = war ? AtWarNearAsync(search, facts, here, cancellationToken, notes[10]) : Task.FromResult<IReadOnlyList<SystemSummary>>([]);

        await Task.WhenAll(notableTask, warTask, anarchyTask, sitesTask, frontlineTask, stationsTask, bodiesTask, inBandTask, pullTask, baseTask, sightTask, missionsTask, destinationsTask, swapTask).ConfigureAwait(false);

        var notable = notableTask.Result;
        var anarchy = anarchyTask.Result;
        var sites = sitesTask.Result;
        var frontline = frontlineTask.Result;
        var inBand = inBandTask.Result;
        var pull = pullTask.Result;
        var home = baseTask.Result;
        var (missions, missionStations, closedMarkets) = missionsTask.Result;

        var stations = stationsTask.Result.Concat(frontline).Concat(inBand.Stations).Concat(pull.Stations).Concat(sightTask.Result.Stations).Concat(missionStations)
            .DistinctBy(station => (station.MarketId, station.Name))
            .ToList();
        var bodies = bodiesTask.Result.Concat(sites.Guardian).Concat(inBand.Bodies).Concat(pull.Bodies).Concat(sightTask.Result.Bodies)
            .DistinctBy(body => (body.SystemAddress, body.BodyId, body.Name))
            .ToList();

        var listed = new List<(string Name, long? Address, StarPosition? Position)>();

        listed.AddRange(stations.Select(station => (station.SystemName, station.SystemAddress, station.Position)));
        listed.AddRange(bodies.Select(body => (body.SystemName, body.SystemAddress, body.Position)));
        listed.AddRange(anarchy.Concat(warTask.Result).Select(system => (system.Name, system.SystemAddress, system.Position)));
        listed.AddRange(sites.Tourist.Concat(sites.Generation).Select(site => (site.System, (long?)site.SystemAddress, (StarPosition?)null)));
        listed.AddRange(notable.Concat(sights).Select(place => (place.System, place.SystemAddress, (StarPosition?)place.Position)));
        listed.AddRange(missions.Select(stop => (stop.System, stop.SystemAddress, stop.Position)));

        if (home is not null)
        {
            listed.Add((home.System, home.SystemAddress, home.Position));
        }

        var places = listed
            .GroupBy(place => place.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => (Name: group.First().Name, Address: group.Select(place => place.Address).FirstOrDefault(address => address is not null), Position: group.Select(place => place.Position).FirstOrDefault(at => at is not null)))
            .ToList();
        var nearest = places
            .Where(place => place.Position is not null && facts.Position is not null)
            .OrderBy(place => facts.Position!.Value.DistanceTo(place.Position!.Value))
            .ToList();
        var atWar = frontline.Select(station => station.SystemName).Concat(warTask.Result.Select(system => system.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var supplied = supply
            ? await Task.WhenAll(nearest.Where(place => place.Address is not null && !Closed(place.Name, place.Address, facts)).Take(SupplySystems).Select(async place =>
            {
                try
                {
                    var found = search.SystemBodiesAsync(place.Address!.Value, cancellationToken);
                    var biology = search.SystemBiologyAsync(place.Address!.Value, cancellationToken);

                    await Task.WhenAll(found, biology).ConfigureAwait(false);

                    return new Supply(
                        place.Name,
                        found.Result.Count(body => !IsStar(body)),
                        found.Result.Count(body => body.Signals.Count > 0 || body.Rings.Any(ring => ring.Hotspots.Count > 0)),
                        biology.Result.Bodies.Sum(body => body.Species.Count),
                        null);
                }
                catch (GalaxyUnavailableException ex)
                {
                    return new Supply(place.Name, null, null, null, $"The galaxy search could not list the bodies of {place.Name} ({ex.Message}), so no map, signal or organic activity is held to it.");
                }
            })).ConfigureAwait(false)
            : [];

        notes[11].AddRange(supplied.Select(found => found.Note).OfType<string>());

        var frontierId = state()?.Identity.FrontierId;
        var anarchyNames = anarchy.Select(system => system.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var systems = places.Select(place =>
        {
            var system = new ChapterSystem(place.Name, place.Address, place.Position)
            {
                Government = anarchyNames.Contains(place.Name) ? "Anarchy" : null,
                AtWar = atWar.Contains(place.Name),
            };

            return supplied.FirstOrDefault(found => string.Equals(found.System, place.Name, StringComparison.OrdinalIgnoreCase)) is { } read
                ? system with { Mappable = read.Mappable, Signals = read.Signals, Species = read.Species }
                : system;
        }).ToList();
        var seen = places
            .Where(place => place.Address is { } known && visited?.Find(frontierId, known).State == VisitState.Visited)
            .Select(place => place.Name)
            .ToList();

        foreach (var note in notes.SelectMany(list => list))
        {
            logger.LogInformation("Chapter places: {Note}", note);
        }

        return new ChapterPlaces
        {
            Systems = systems,
            Stations = stations,
            Bodies = bodies,
            ClosedMarkets = closedMarkets,
            Sites =
            [
                .. sites.Guardian.Select(body => new ChapterSite(AdventureTrigger.GuardianSite, body.SystemName, body.SystemAddress!.Value, body.Position, body.Name) { BodyId = body.BodyId }),
                .. sites.Tourist.Select(site => new ChapterSite(AdventureTrigger.TouristSite, site.System, site.SystemAddress, null, site.Name)),
                .. sites.Generation.Select(site => new ChapterSite(AdventureTrigger.GenerationSite, site.System, site.SystemAddress, null, site.Name)),
            ],
            Notable = notable,
            Sights = sights,
            InBand = Area(inBand.State, inBand.Stations, inBand.Bodies),
            Pull = Area(pull.State, pull.Stations, pull.Bodies),
            Base = home is { } step ? new ChapterBase(step.System, step.SystemAddress, step.Station, step.MarketId, step.Position) : null,
            Missions = missions,
            Visited = seen,
            Used = used,
            DestinationAt = destinationAt,
            Destinations = destinationsTask.Result,
            SwapAt = swapTask.Result,
        };

        static ChapterArea Area(InBandState state, IReadOnlyList<StationSummary> stations, IReadOnlyList<BodySummary> bodies) => new(
            state switch
            {
                InBandState.Listed => ChapterAreaState.Listed,
                InBandState.NoneFound => ChapterAreaState.NoneFound,
                InBandState.Unavailable => ChapterAreaState.Unavailable,
                _ => ChapterAreaState.NotSearched,
            },
            stations,
            bodies);
    }

    /// <summary>Where the system an act-one chapter's swap is parked in is, or null with a note when it cannot be found.</summary>
    private static async Task<StarPosition?> SwapPositionAsync(IGalaxyService search, string system, CancellationToken cancellationToken, List<string> notes)
    {
        try
        {
            if (await search.PositionAsync(system, cancellationToken).ConfigureAwait(false) is { } at)
            {
                return at;
            }

            notes.Add($"The galaxy search does not know {system}, where the ship to swap into is parked, so the chapter's hops are measured from the Commander.");
        }
        catch (GalaxyUnavailableException ex)
        {
            notes.Add($"The galaxy search could not find {system}, where the ship to swap into is parked ({ex.Message}), so the chapter's hops are measured from the Commander.");
        }

        return null;
    }

    /// <summary>Points searched around for finale chapter 1's destination.</summary>
    private const int DestinationPoints = 6;

    /// <summary>
    /// For finale chapter 1 with no destination, the open landable bodies around points between half the limit and the limit from
    /// the Commander, in directions drawn from the chapter's seed; empty otherwise.
    /// </summary>
    private async Task<IReadOnlyList<BodySummary>> DestinationBodiesAsync(
        IGalaxyService search, AdventureAsk ask, Facts facts, CancellationToken cancellationToken, List<string> notes)
    {
        if (ask.Story is not { FinaleChapter: 1, Destination: null } story || facts.Position is not { } start)
        {
            return [];
        }

        var limit = AdventureDestination.Limit(facts.RadiusLightYears, story.FinaleChapters);
        var random = new Random32(PlanSeed(ask));
        var radius = Math.Max(facts.RadiusLightYears, limit / DestinationPoints);
        var points = Enumerable.Range(0, DestinationPoints).Select(_ =>
        {
            var away = limit * (0.5 + (0.5 * random.NextDouble()));
            var angle = 2 * Math.PI * random.NextDouble();

            return new StarPosition(start.X + (away * Math.Cos(angle)), start.Y, start.Z + (away * Math.Sin(angle)));
        }).ToList();

        var found = await Task.WhenAll(points.Select(async point =>
        {
            try
            {
                var result = await search.FindBodiesAsync(BodyQuery.LandableNear(point, radius, PlacesPerSight), cancellationToken).ConfigureAwait(false);

                return (Bodies: OpenBodies(result.Bodies.Where(body => body.Position is not null), facts, int.MaxValue), Failure: (string?)null);
            }
            catch (GalaxyUnavailableException ex)
            {
                return (Bodies: (IReadOnlyList<BodySummary>)[], Failure: ex.Message);
            }
        })).ConfigureAwait(false);

        notes.AddRange(found.Select(point => point.Failure).OfType<string>().Distinct().Select(failure => $"The galaxy search could not list landable bodies for the finale's destination ({failure})."));

        return [.. found.SelectMany(point => point.Bodies).DistinctBy(body => (body.SystemAddress, body.BodyId, body.Name))];
    }

    /// <summary>A system's gathered supply, or the note saying why it could not be read.</summary>
    private sealed record Supply(string System, int? Mappable, int? Signals, int? Species, string? Note);

    /// <summary>A mission held's destination as gathered: the stop, the station it docks at, whether war closed it, or the note saying why it could not be placed.</summary>
    private sealed record MissionRead(ChapterMissionStop? Stop, StationSummary? Station, bool Closed, string? Note);

    /// <summary>Up to 20 systems at war or in civil war within reach of <paramref name="here"/>, by the search the chapter draw gates a bond on.</summary>
    private async Task<IReadOnlyList<SystemSummary>> AtWarNearAsync(
        IGalaxyService search, Facts facts, string here, CancellationToken cancellationToken, List<string> notes)
    {
        var requested = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["distance"] = $"0-{facts.RadiusLightYears.ToString("0", CultureInfo.InvariantCulture)}",
            ["faction_state"] = "War,Civil War",
        };

        if (!GalaxyQuery.TryParse(here, requested, size: 20, out var query, out _))
        {
            return [];
        }

        try
        {
            var found = await search.SearchAsync(query, cancellationToken).ConfigureAwait(false);

            return [.. found.Systems.Where(system => system.AtWar && !Closed(system.Name, system.SystemAddress, facts))];
        }
        catch (GalaxyUnavailableException ex)
        {
            notes.Add($"The galaxy search could not list the systems at war within reach ({ex.Message}), so no bond or conflict has a place.");
            return [];
        }
    }

    /// <summary>The open stations within reach of <paramref name="here"/>, nearest first.</summary>
    private async Task<IReadOnlyList<StationSummary>> NearStationsAsync(
        IGalaxyService search, Facts facts, string here, AdventureResolver resolver, CancellationToken cancellationToken, List<string> notes)
    {
        try
        {
            var found = await search.FindStationsAsync(StationQuery.Near(here, facts.RadiusLightYears, 20), cancellationToken).ConfigureAwait(false);

            return await OpenStationsAsync(found.Stations, facts, resolver, int.MaxValue, cancellationToken).ConfigureAwait(false);
        }
        catch (GalaxyUnavailableException ex)
        {
            notes.Add($"The galaxy search could not list the stations within reach ({ex.Message}).");
            return [];
        }
    }

    /// <summary>The open landable bodies within reach of <paramref name="here"/>, nearest first.</summary>
    private async Task<IReadOnlyList<BodySummary>> NearBodiesAsync(
        IGalaxyService search, Facts facts, string here, CancellationToken cancellationToken, List<string> notes)
    {
        try
        {
            var found = await search.FindBodiesAsync(BodyQuery.LandableNear(here, facts.RadiusLightYears, 20), cancellationToken).ConfigureAwait(false);

            return OpenBodies(found.Bodies, facts, int.MaxValue);
        }
        catch (GalaxyUnavailableException ex)
        {
            notes.Add($"The galaxy search could not list the landable bodies within reach ({ex.Message}).");
            return [];
        }
    }

    /// <summary>
    /// The landable bodies, and the open stations when the chapter docks, around each sight within reach; when none is, around each
    /// point a hop apart on the way to the nearest, up to <see cref="ChapterPlanner.MostSightHops"/> of them.
    /// </summary>
    private async Task<(IReadOnlyList<StationSummary> Stations, IReadOnlyList<BodySummary> Bodies)> AroundSightsAsync(
        IGalaxyService search, IReadOnlyList<NotablePlace> sights, Facts facts, AdventureResolver resolver, bool docks, CancellationToken cancellationToken, List<string> notes)
    {
        var stations = new List<StationSummary>();
        var bodies = new List<BodySummary>();

        if (sights.Count == 0 || facts.Position is not { } start)
        {
            return (stations, bodies);
        }

        var reach = facts.RadiusLightYears;
        var near = sights.Where(sight => sight.DistanceFrom(start) <= reach).Select(sight => (Centre: sight.Position, Radius: reach)).ToList();

        if (near.Count == 0 && sights.MinBy(sight => sight.DistanceFrom(start)) is { } nearest)
        {
            var at = start;

            for (var hop = 0; hop < ChapterPlanner.MostSightHops && at.DistanceTo(nearest.Position) > reach; hop++)
            {
                at = ChapterPull.Waypoint(at, nearest.Position, reach);
                near.Add((at, ChapterPull.PlacesWithin * reach));
            }

            near.Add((nearest.Position, reach));
        }

        foreach (var (centre, radius) in near)
        {
            try
            {
                if (docks)
                {
                    var found = await search.FindStationsAsync(StationQuery.Near(centre, radius, PlacesPerSight), cancellationToken).ConfigureAwait(false);

                    stations.AddRange(await OpenStationsAsync(found.Stations.Where(station => station.Position is not null), facts, resolver, int.MaxValue, cancellationToken).ConfigureAwait(false));
                }

                var landable = await search.FindBodiesAsync(BodyQuery.LandableNear(centre, radius, PlacesPerSight), cancellationToken).ConfigureAwait(false);

                bodies.AddRange(OpenBodies(landable.Bodies.Where(body => body.Position is not null), facts, int.MaxValue));
            }
            catch (GalaxyUnavailableException ex)
            {
                notes.Add($"The galaxy search could not list places on the way to a sight ({ex.Message}).");
            }
        }

        return (stations, bodies);
    }

    /// <summary>The destinations of missions held within twice the reach, the stations they name, and which of those war has closed.</summary>
    private async Task<(IReadOnlyList<ChapterMissionStop> Stops, IReadOnlyList<StationSummary> Stations, IReadOnlyList<long> Closed)> MissionStopsAsync(
        IGalaxyService search, Facts facts, CancellationToken cancellationToken, List<string> notes)
    {
        var held = (facts.Missions ?? []).Where(mission => mission.DestinationSystem is { Length: > 0 }).ToList();

        if (held.Count == 0 || facts.Position is not { } start)
        {
            return ([], [], []);
        }

        var read = await Task.WhenAll(held.Select(async mission =>
        {
            var destination = mission.DestinationSystem!;
            var resolver = new AdventureResolver(search, facts.Standings) { ClosedMarketId = facts.ClosedMarketId, Time = Time };

            try
            {
                if (await search.PositionAsync(destination, cancellationToken).ConfigureAwait(false) is not { } at || start.DistanceTo(at) > 2 * facts.RadiusLightYears)
                {
                    return new MissionRead(null, null, false, null);
                }

                var station = mission.DestinationStation is { Length: > 0 } named
                    ? await resolver.StationOfAsync(new AdventureTrigger { Kind = TriggerKind.Dock, System = destination, Station = named }, cancellationToken).ConfigureAwait(false)
                    : null;
                var address = station?.SystemAddress ?? (await resolver.SystemAsync(destination, cancellationToken).ConfigureAwait(false))?.Address;
                var closed = station is not null && await resolver.ClosedByWarAsync(station, cancellationToken).ConfigureAwait(false);

                return new MissionRead(
                    new ChapterMissionStop(mission.Id, station?.SystemName ?? destination, address, at, station?.Name, station?.MarketId),
                    station is { MarketId: not null } ? station with { Position = station.Position ?? at } : null,
                    closed,
                    null);
            }
            catch (GalaxyUnavailableException ex)
            {
                return new MissionRead(null, null, false, $"The galaxy search could not place the destination of mission {mission.Id} in {destination} ({ex.Message}), so it is not a stop.");
            }
        })).ConfigureAwait(false);

        notes.AddRange(read.Select(found => found.Note).OfType<string>());

        return (
            [.. read.Select(found => found.Stop).OfType<ChapterMissionStop>()],
            [.. read.Select(found => found.Station).OfType<StationSummary>()],
            [.. read.Where(found => found.Closed && found.Station?.MarketId is not null).Select(found => found.Station!.MarketId!.Value)]);
    }

    /// <summary>The systems the chapters before this one in story <paramref name="storyId"/> placed a step in, oldest first.</summary>
    private static IReadOnlyList<string> UsedSystems(AdventureChapter? chapter, string? storyId) =>
        chapter is null || storyId is null
            ? []
            : [.. chapter.Earlier.Append(chapter.Previous)
                .Where(adventure => string.Equals(adventure.StoryId, storyId, StringComparison.OrdinalIgnoreCase))
                .SelectMany(adventure => adventure.Beats)
                .SelectMany(beat => beat.Steps)
                .Select(step => step.Trigger.System)
                .OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// The step back to the Commander's base for a story chapter held to one or exempt from one, or null. A station known only by its market id is
    /// named from the galaxy search's stations in its system; where none matches, or the base is the Home system setting, the step is an arrival.
    /// </summary>
    private async Task<BaseStep?> BaseAsync(IGalaxyService search, AdventureAsk ask, Facts facts, CancellationToken cancellationToken)
    {
        if (facts.Carrier.Owned)
        {
            return null;
        }

        var ships = state()?.Fleet.Ships ?? [];
        var home = HomeSystem?.Invoke();
        var positions = new Dictionary<string, StarPosition?>(StringComparer.OrdinalIgnoreCase);

        async Task<StarPosition?> PositionAsync(string system)
        {
            if (!positions.TryGetValue(system, out var position))
            {
                try
                {
                    position = await search.PositionAsync(system, cancellationToken).ConfigureAwait(false);
                }
                catch (GalaxyUnavailableException ex)
                {
                    logger.LogWarning("Draft {Draft}, the galaxy search could not place {System}: {Message}", ask.Draft, system, ex.Message);
                    position = null;
                }

                positions[system] = position;
            }

            return position;
        }

        if (ShipBases.Busiest(ships) is { Count: > 1 } tied && facts.Position is not null)
        {
            foreach (var system in tied.Select(station => station.System).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                await PositionAsync(system).ConfigureAwait(false);
            }
        }

        if (ShipBases.Of(ships, carrier: false, home, system => facts.Position is { } here && positions.GetValueOrDefault(system) is { } at ? here.DistanceTo(at) : null) is not { } chosen)
        {
            return null;
        }

        BaseStep step;

        try
        {
            if (chosen.IsHomeSystem)
            {
                step = await LookUpAsync(search, chosen.System, facts, cancellationToken).ConfigureAwait(false) is { } found
                    ? new BaseStep(chosen, found.Name, found.Address, null, null, found.Position)
                    : new BaseStep(chosen, chosen.System, null, null, null, null);
            }
            else
            {
                var station = chosen.MarketId is { } market && market != facts.ClosedMarketId
                    ? (await search.FindStationsAsync(StationQuery.Near(chosen.System, 1, 20), cancellationToken).ConfigureAwait(false)).Stations
                        .FirstOrDefault(listed => listed.MarketId == market && string.Equals(listed.SystemName, chosen.System, StringComparison.OrdinalIgnoreCase))
                    : null;
                var address = station?.SystemAddress
                    ?? (await new AdventureResolver(search, facts.Standings) { Time = Time }.SystemAsync(chosen.System, cancellationToken).ConfigureAwait(false))?.Address;

                step = new BaseStep(chosen, chosen.System, address, station?.Name, station?.MarketId, await PositionAsync(chosen.System).ConfigureAwait(false));
            }
        }
        catch (GalaxyUnavailableException ex)
        {
            logger.LogWarning("Draft {Draft}, the galaxy search could not look up the base in {System}: {Message}", ask.Draft, chosen.System, ex.Message);
            step = new BaseStep(chosen, chosen.System, null, null, null, null);
        }

        logger.LogInformation(
            "Draft {Draft}, the chapter ends near the base in {System}: {Step}{Unplaced}",
            ask.Draft, step.System, step.Step, step.Position is null ? "; the galaxy search could not place it, so the step is needed unless the last stop is in that system" : string.Empty);

        return step;
    }

    /// <summary>For a chapter that travels, the open stations and landable bodies nearest the waypoint on the way to its target.</summary>
    /// <summary>Where the finale's destination is, from the galaxy search; null where it cannot say.</summary>
    private static async Task<StarPosition?> DestinationPositionAsync(IGalaxyService search, AdventureDestination destination, CancellationToken cancellationToken)
    {
        try
        {
            return await search.PositionAsync(destination.System, cancellationToken).ConfigureAwait(false);
        }
        catch (GalaxyUnavailableException)
        {
            return null;
        }
    }

    /// <summary>The places around the waypoint toward <paramref name="toward"/>, or toward the chapter's own leg when it is null.</summary>
    private async Task<PullPlaces> PullAsync(
        IGalaxyService search, AdventureResolver resolver, AdventureAsk ask, Facts facts, CancellationToken cancellationToken, List<string> notes, ChapterLeg? toward = null)
    {
        if ((toward ?? ChapterPull.Of(ask, facts.Position)) is not { Arrived: false } leg || facts.Position is not { } start)
        {
            return PullPlaces.NotSearched;
        }

        var centre = ChapterPull.Waypoint(start, leg.There, facts.RadiusLightYears);
        var radius = ChapterPull.PlacesWithin * facts.RadiusLightYears;

        try
        {
            var stations = await search.FindStationsAsync(StationQuery.Near(centre, radius, ChapterPull.PlacesPerKind), cancellationToken).ConfigureAwait(false);
            var bodies = await search.FindBodiesAsync(BodyQuery.LandableNear(centre, radius, ChapterPull.PlacesPerKind), cancellationToken).ConfigureAwait(false);

            var foundStations = stations.Stations.Where(station => station.Position is not null).ToList();
            var foundBodies = bodies.Bodies.Where(body => body.Position is not null).ToList();

            if (leg is { Asked: true, Home: false })
            {
                var there = await search.FindStationsAsync(StationQuery.Near(leg.Target, 1, ChapterPull.PlacesPerKind), cancellationToken).ConfigureAwait(false);
                var landHere = await search.FindBodiesAsync(BodyQuery.LandableNear(leg.Target, 1, ChapterPull.PlacesPerKind), cancellationToken).ConfigureAwait(false);

                foundStations.AddRange(there.Stations
                    .Where(station => string.Equals(station.SystemName, leg.Target, StringComparison.OrdinalIgnoreCase) && !foundStations.Any(found => found.MarketId == station.MarketId && found.Name == station.Name))
                    .Select(station => station with { Position = station.Position ?? leg.There }));
                foundBodies.AddRange(landHere.Bodies
                    .Where(body => string.Equals(body.SystemName, leg.Target, StringComparison.OrdinalIgnoreCase) && !foundBodies.Any(found => found.Name == body.Name))
                    .Select(body => body with { Position = body.Position ?? leg.There }));
            }

            var open = await OpenStationsAsync(foundStations, facts, resolver, int.MaxValue, cancellationToken).ConfigureAwait(false);
            var landable = OpenBodies(foundBodies, facts, int.MaxValue);

            if (open.Count > 0 || landable.Count > 0)
            {
                return new PullPlaces(InBandState.Listed, open, landable);
            }
        }
        catch (GalaxyUnavailableException ex)
        {
            notes.Add($"The galaxy search could not list places on the way to {leg.Target} ({ex.Message}), so none was offered and the chapter was not held to a distance from {leg.Target}.");
            return new PullPlaces(InBandState.Unavailable, [], []);
        }

        notes.Add($"No station or landable body was found near the point on the way to {leg.Target}, so the chapter was not held to a distance from {leg.Target}.");
        return new PullPlaces(InBandState.NoneFound, [], []);
    }

    /// <summary>Up to <paramref name="most"/> of <paramref name="stations"/> a new objective may dock at, in their order.</summary>
    private async Task<IReadOnlyList<StationSummary>> OpenStationsAsync(
        IEnumerable<StationSummary> stations, Facts facts, AdventureResolver resolver, int most, CancellationToken cancellationToken)
    {
        var open = new List<StationSummary>();

        foreach (var station in stations.Where(station =>
                     station.IsFixed
                     && !Closed(station.SystemName, station.SystemAddress, facts)
                     && (facts.NeedsPad() is not { } pad || station.Admits(pad))
                     && (facts.ClosedMarketId is null || station.MarketId != facts.ClosedMarketId)))
        {
            if (open.Count >= most)
            {
                break;
            }

            if (!await resolver.ClosedByWarAsync(station, cancellationToken).ConfigureAwait(false))
            {
                open.Add(station);
            }
        }

        return open;
    }

    /// <summary>Up to <paramref name="most"/> of <paramref name="bodies"/> a new objective may land on or scan, in their order.</summary>
    private IReadOnlyList<BodySummary> OpenBodies(IEnumerable<BodySummary> bodies, Facts facts, int most) =>
        [.. bodies.Where(body => !Closed(body.SystemName, body.SystemAddress, facts)).Take(most)];

    /// <summary>The point <paramref name="lightYears"/> from <paramref name="start"/> along the line to Sol.</summary>
    private static StarPosition TowardSol(StarPosition start, double lightYears)
    {
        var scale = 1 - (lightYears / start.DistanceTo(StarPosition.Origin));

        return new StarPosition(start.X * scale, start.Y * scale, start.Z * scale);
    }

    private static bool Inside(StarPosition? position, ChapterBand.Range band) =>
        position is { } at && at.DistanceTo(StarPosition.Origin) is var fromSol && fromSol >= band.Min && fromSol <= band.Max;

    /// <summary>
    /// For a chapter held to a Sol band, the open stations and landable bodies inside it: first around the point 150 light years
    /// nearer Sol within 50, which lies wholly in the band, then, when that leaves nothing, around the point 200 nearer within 100.
    /// </summary>
    private async Task<InBand> InBandAsync(
        IGalaxyService search, AdventureResolver resolver, AdventureAsk ask, Facts facts, CancellationToken cancellationToken, List<string> notes)
    {
        if (ChapterBand.Of(facts.Position, ask) is not { } band || facts.Position is not { } start)
        {
            return InBand.NotSearched;
        }

        try
        {
            foreach (var (along, radius) in new[] { (150.0, 50.0), (200.0, 100.0) })
            {
                var centre = TowardSol(start, along);
                var stations = await search.FindStationsAsync(StationQuery.Near(centre, radius, 20), cancellationToken).ConfigureAwait(false);
                var bodies = await search.FindBodiesAsync(BodyQuery.LandableNear(centre, radius, 20), cancellationToken).ConfigureAwait(false);

                var open = await OpenStationsAsync(
                    stations.Stations.Where(station => Inside(station.Position, band)).OrderBy(station => start.DistanceTo(station.Position!.Value)),
                    facts, resolver, InBandPerKind, cancellationToken).ConfigureAwait(false);
                var landable = OpenBodies(
                    bodies.Bodies.Where(body => Inside(body.Position, band)).OrderBy(body => start.DistanceTo(body.Position!.Value)),
                    facts, InBandPerKind);

                if (open.Count > 0 || landable.Count > 0)
                {
                    return new InBand(InBandState.Listed, open, landable);
                }
            }
        }
        catch (GalaxyUnavailableException ex)
        {
            notes.Add($"The galaxy search could not list the places inside the band toward Sol ({ex.Message}), so none was offered and the band still applies.");
            return new InBand(InBandState.Unavailable, [], []);
        }

        notes.Add($"No station or landable body between {band.Min:0} and {band.Max:0} light years from Sol was found within reach, so the chapter was not held to that band.");
        return new InBand(InBandState.NoneFound, [], []);
    }

    /// <summary>Up to five open stations within reach that have Frontline Solutions in a system at war, nearest first.</summary>
    private async Task<IReadOnlyList<StationSummary>> FrontlineAsync(
        IGalaxyService search, Facts facts, string here, AdventureResolver resolver, CancellationToken cancellationToken, List<string> notes)
    {
        try
        {
            var found = await search.FindStationsAsync(AdventureResolver.FrontlineNear(here, facts.RadiusLightYears, 20), cancellationToken).ConfigureAwait(false);
            var open = await OpenStationsAsync(found.Stations.Where(AdventureResolver.HasFrontlineSolutions), facts, resolver, int.MaxValue, cancellationToken).ConfigureAwait(false);
            var listed = new List<StationSummary>();

            foreach (var station in open)
            {
                if (listed.Count >= 5)
                {
                    break;
                }

                if (await resolver.FrontlineAtWarAsync(station, cancellationToken).ConfigureAwait(false))
                {
                    listed.Add(station);
                }
            }

            return listed;
        }
        catch (GalaxyUnavailableException ex)
        {
            notes.Add($"The galaxy search could not list Frontline Solutions stations ({ex.Message}), so the story avoids kill bonds on foot.");
            return [];
        }
    }

    private async Task<IReadOnlyList<SystemSummary>> AnarchyAsync(
        IGalaxyService search, Facts facts, string here, CancellationToken cancellationToken, List<string> notes)
    {
        var requested = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["government"] = "Anarchy",
            ["distance"] = $"0-{facts.RadiusLightYears.ToString("0", CultureInfo.InvariantCulture)}",
        };

        if (!GalaxyQuery.TryParse(here, requested, size: 5, out var query, out _))
        {
            return [];
        }

        try
        {
            var found = await search.SearchAsync(query, cancellationToken).ConfigureAwait(false);

            return [.. found.Systems.Where(system => IsAnarchy(system.Government) && !Closed(system.Name, system.SystemAddress, facts)).Take(5)];
        }
        catch (GalaxyUnavailableException ex)
        {
            notes.Add($"The galaxy search could not list Anarchy systems ({ex.Message}), so the story avoids illegal missions.");
            return [];
        }
    }

    /// <summary>A tourist beacon or generation ship from the signal index, listed by its system.</summary>
    private sealed record TouristSite(long SystemAddress, string System, string Name, double LightYears);

    /// <summary>The unvisited Guardian sites, tourist beacons and generation ships within reach, nearest first.</summary>
    private sealed record Sites(IReadOnlyList<BodySummary> Guardian, IReadOnlyList<TouristSite> Tourist, IReadOnlyList<TouristSite> Generation)
    {
        public static readonly Sites None = new([], [], []);

        public bool IsEmpty => Guardian.Count == 0 && Tourist.Count == 0 && Generation.Count == 0;

        /// <summary>The listed sites as a refusal names them.</summary>
        public string Listed() => string.Join(
            "; ",
            Guardian.Select(body => $"Guardian site {body.Name} (\"filter\": \"guardian\", \"system\": \"{body.SystemName}\", \"body\": \"{body.Name}\")")
                .Concat(Tourist.Select(site => $"tourist beacon in {site.System} (\"filter\": \"tourist\", \"system\": \"{site.System}\")"))
                .Concat(Generation.Select(site => $"generation ship {site.Name} (\"filter\": \"generation\", \"system\": \"{site.System}\")")));
    }

    private const string GenerationShipPrefix = "Generation Ship ";

    private const int SitesPerKind = 3;

    private async Task<Sites> SitesAsync(IGalaxyService search, Facts facts, string here, CancellationToken cancellationToken, List<string> notes)
    {
        var frontierId = state()?.Identity.FrontierId;
        var radius = facts.RadiusLightYears;
        bool Visited(long address) => visited?.Find(frontierId, address).State == VisitState.Visited;

        IReadOnlyList<BodySummary> guardian = [];

        try
        {
            var found = await search.FindBodiesAsync(BodyQuery.LandableNear(here, radius, 20) with { Signal = "Guardian" }, cancellationToken).ConfigureAwait(false);

            guardian =
            [
                .. found.Bodies
                    .Where(body => body is { SystemAddress: not null, BodyId: not null, IsLandable: true } && !Closed(body.SystemName, body.SystemAddress, facts) && !Visited(body.SystemAddress.Value))
                    .OrderBy(body => body.Distance ?? double.MaxValue)
                    .Take(SitesPerKind),
            ];
        }
        catch (GalaxyUnavailableException ex)
        {
            notes.Add($"The galaxy search could not list Guardian sites ({ex.Message}), so none was offered.");
        }

        IReadOnlyList<TouristSite> tourist = [];
        IReadOnlyList<TouristSite> generation = [];

        if (signalIndex?.Invoke() is not { } source)
        {
            notes.Add("The signal index is off, so no tourist beacon or generation ship was offered.");
        }
        else if (await source.GetAsync(cancellationToken).ConfigureAwait(false) is not { } index)
        {
            notes.Add("The signal index is missing, so no tourist beacon or generation ship was offered.");
        }
        else if (facts.Position is { } position)
        {
            tourist = await Task.Run(
                () => (IReadOnlyList<TouristSite>)[
                    .. index.Within(position, radius)
                        .Where(sighting => sighting.Kind == SignalKind.TouristBeacon && !Closed(sighting.SystemName, sighting.SystemAddress, facts) && !Visited(sighting.SystemAddress))
                        .Select(sighting => new TouristSite(sighting.SystemAddress, sighting.SystemName, sighting.SignalName, position.DistanceTo(sighting.Position)))
                        .OrderBy(site => site.LightYears)
                        .DistinctBy(site => site.SystemAddress)
                        .Take(SitesPerKind),
                ],
                cancellationToken).ConfigureAwait(false);

            generation = await Task.Run(
                () => (IReadOnlyList<TouristSite>)[
                    .. index.Within(position, radius)
                        .Where(sighting => sighting.Kind == SignalKind.Megaship && sighting.SignalName.StartsWith(GenerationShipPrefix, StringComparison.OrdinalIgnoreCase))
                        .GroupBy(sighting => sighting.SignalName, StringComparer.OrdinalIgnoreCase)
                        .Select(ship => ship.MaxBy(sighting => sighting.LastSeen)!)
                        .Where(sighting => !Closed(sighting.SystemName, sighting.SystemAddress, facts) && !Visited(sighting.SystemAddress))
                        .Select(sighting => new TouristSite(sighting.SystemAddress, sighting.SystemName, sighting.SignalName, position.DistanceTo(sighting.Position)))
                        .OrderBy(site => site.LightYears)
                        .Take(SitesPerKind),
                ],
                cancellationToken).ConfigureAwait(false);
        }

        return new Sites(guardian, tourist, generation);
    }

    /// <summary>The model that writes: the chosen one, or the provider's default when the catalog marks the chosen one as not writing stories.</summary>
    private string? WritingModel(ILlmProvider? asking)
    {
        var chosen = model();

        return asking is { RunsOnThisMachine: false } && chosen is not null && writesStories is not null && !writesStories(asking.Id, chosen)
            ? asking.DefaultModel
            : chosen;
    }

    private void Enter(AdventureAsk ask, WriteStage stage) =>
        ask.Progress?.Enter(stage, StoryPace.For(
            WritingModel(provider()) ?? provider()?.DefaultModel,
            local: provider() is { RunsOnThisMachine: true },
            chapter: ask.Story is not null));

    private const string FidelityStep = "fidelity";
    private const string PlainStep = "plain";
    private const string HeadingStep = "heading";

    /// <summary>One model call: the step it served, the model asked, the reply, and the JSON object read out of it.</summary>
    private sealed record Asked(string Step, string Model, FlavourReply Reply, string? Json)
    {
        public string Why => Reply.Line is null
            ? $"Nothing came back ({Reply.Miss}{(string.IsNullOrWhiteSpace(Reply.Message) ? "" : $": {Reply.Message}")})."
            : Reply.CutOff is { } cutOff
                ? $"{Reply.Line.Length} characters came back, cut off {cutOff.Describe()}, and could not be read."
                : $"{Reply.Line.Length} characters came back and could not be read.";
    }

    private async Task<Asked> AskJsonAsync(string step, StoryCall call, int budget, StoryProgress? progress, CancellationToken cancellationToken, ThinkingEffort? effort = null)
    {
        var asking = provider();
        var writing = WritingModel(asking);
        var chosen = writing ?? asking?.DefaultModel ?? "no model";
        var reply = await FlavourTurn.AskForAsync(
            asking,
            writing,
            persona(),
            aboutMe(),
            call.Text,
            gameState: null,
            spend,
            prices,
            logger,
            cancellationToken,
            maxOutputTokens: budget,
            effort: Effort
                ?? effort
                ?? (asking is null ? null : storyEffort?.Invoke(asking.Id, chosen))
                ?? ThinkingEffort.Medium,

            // Cold, although what comes back is a story (#98).
            sampling: LlmSampling.Adventure,
            // A model on this machine may be on a CPU, so it is never stopped for being slow.
            limits: asking is { RunsOnThisMachine: true } ? null : new FlavourLimits(CallLimit ?? CallLimitFor(budget), SilenceLimit) { First = FirstReplyLimit, Time = Time },
            received: progress is null ? null : progress.Received,
            responseSchema: call.Schema).ConfigureAwait(false);

        if (reply.Miss == FlavourMiss.TimedOut)
        {
            progress?.CallStopped();
        }

        return new Asked(step, chosen, reply, reply.Line is null ? null : Unfence(reply.Line));
    }

    /// <summary>Models fence JSON in markdown whatever they are told; the object is what is wanted.</summary>
    internal static string? Unfence(string reply)
    {
        var start = reply.IndexOf('{');
        var end = reply.LastIndexOf('}');

        return start >= 0 && end > start ? reply[start..(end + 1)] : null;
    }

    // ---- the instructions ------------------------------------------------------------------

    /// <summary>
    /// What the chapter before asked of the Commander, how it ended and what the Commander asked during it, and how many
    /// chapters came before that. Its wording is not given.
    /// </summary>
    private static void AppendChapter(StringBuilder text, AdventureChapter chapter, Facts facts, bool stock, bool replacing = false)
    {
        var previous = chapter.Previous;

        text.AppendLine();
        text.AppendLine("This story is the next chapter of one the Commander has already flown to its end.");

        if (chapter.Earlier.Count > 0)
        {
            text.AppendLine(chapter.Earlier.Count == 1
                ? "One chapter came before it."
                : $"{chapter.Earlier.Count.ToString(CultureInfo.InvariantCulture)} chapters came before it.");
        }

        text.AppendLine();
        text.AppendLine("The chapter just finished:");

        if (previous.Ending is { Name: not AdventureEnding.Failed } reached)
        {
            text.AppendLine($"It ended in the ending \"{reached.Name}\", one of its own: {reached.Reason} Write the next chapter from that ending.");
        }

        if (previous.Spine?.Ending is { Length: > 0 } meant)
        {
            text.AppendLine($"How its spine said it ends: {meant}");
        }

        text.AppendLine("Objectives: " + string.Join("; ", previous.Beats.Select((beat, index) => $"{index + 1}. {beat.Title}")));
        text.AppendLine("What those objectives asked for: " + string.Join("; ", previous.Beats.Select((beat, index) => $"{index + 1}. {string.Join(", then ", beat.Steps.Select(step => step.Trigger.Describe()))}")));

        var asides = previous.Told.Where(told => told.Kind == AdventureToldKind.Aside && told.Asked is { Length: > 0 }).ToList();

        if (asides.Count > 0)
        {
            text.AppendLine("What the Commander asked during it, and the reply each got, oldest first:");

            foreach (var told in asides)
            {
                text.AppendLine($"- The Commander asked \"{told.Asked}\": {told.Text}");
            }
        }

        AppendNotables(text, chapter);
        AppendStandings(text, chapter, facts.Standings);

        if (replacing)
        {
            return;
        }

        text.AppendLine();
        text.AppendLine(
            stock
                ? "Write the next chapter, not a retelling. It follows from what the Commander did in the last chapter and from "
                    + "the story's public and hidden layers and the beat-sheet lines for its stage below. People and places in the "
                    + "story's cast and layers may return; what it settled stays settled."
                : "Write the next chapter, not a retelling. It follows from what the Commander did in the last chapter. "
                    + "People and places it established may return; what it settled stays settled.");
    }

    /// <summary>The title and the spine's premise and want, then its stake, turn and ending when <paramref name="whole"/>.</summary>
    private static void AppendSpine(StringBuilder text, string name, AdventureSpine? spine, bool whole = true)
    {
        text.AppendLine($"Title: {name}");
        text.AppendLine($"Premise: {spine?.Premise}");
        text.AppendLine($"Want: {spine?.Want}");

        if (whole)
        {
            text.AppendLine($"Stake: {spine?.Stake}");
            text.AppendLine($"Turn: {spine?.Turn}");
            text.AppendLine($"Ending: {spine?.Ending}");
        }
    }

    /// <summary>The attempt at this chapter that failed, why, and what is asked of the chapter that takes its place.</summary>
    private static void AppendFailed(StringBuilder text, AdventureChapter failed)
    {
        var attempt = failed.Previous;

        text.AppendLine();
        text.AppendLine("An earlier attempt at this chapter failed:");
        AppendSpine(text, attempt.Name, attempt.Spine, whole: false);
        text.AppendLine("Objectives: " + string.Join("; ", attempt.Beats.Select((beat, index) => $"{index + 1}. {beat.Title}")));
        text.AppendLine($"Why it failed: {attempt.Ending?.Reason}");
        AppendNotables(text, failed);
        text.AppendLine();
        text.AppendLine(
            "Write a chapter that takes its place, written from what the Commander did. Do not retell the failed "
            + "chapter or ask for its objectives again; let what happened shape this chapter's want and stake.");
    }

    /// <summary>The chapter the Commander set aside, what of it they did, where they are now, and what is asked of the chapter written in its place.</summary>
    private static void AppendSetAside(StringBuilder text, AdventureSetAside setAside)
    {
        var attempt = setAside.Chapter.Previous;

        text.AppendLine();
        text.AppendLine(setAside.Words is null
            ? "The Commander asked for this chapter to be written again from where they are now. The earlier version:"
            : "The Commander set an earlier version of this chapter aside to go somewhere else:");
        AppendSpine(text, attempt.Name, attempt.Spine, whole: false);
        text.AppendLine("Objectives: " + string.Join("; ", attempt.Beats.Select((beat, index) => $"{index + 1}. {beat.Title}")));
        text.AppendLine(setAside.Done.Count > 0
            ? "Objectives already done: " + string.Join("; ", setAside.Done)
            : "Objectives already done: none");

        if (setAside.Words is { } words)
        {
            text.AppendLine($"Why they set it aside, in their words: {words}");
        }

        if (!string.IsNullOrWhiteSpace(setAside.Here))
        {
            text.AppendLine($"Where the Commander is now: {setAside.Here}");
        }

        AppendNotables(text, setAside.Chapter);
        text.AppendLine();
        text.AppendLine(setAside.Words is null
            ? "Write this chapter again from where the Commander is now. What they already did stays done; do not ask for "
              + "those objectives again. Their activities may be asked for again. Carry on the chapter's want and stake from here."
            : "Write this chapter again from where the Commander is now. What they already did stays done; do not ask for "
              + "those objectives again. Carry on the chapter's want and stake from here.");
    }

    /// <summary>How many chapters back <see cref="RecentActivities"/> looks, the chapter just finished included.</summary>
    public const int RecentChaptersVaried = 3;

    /// <summary>The activity keys the last <see cref="RecentChaptersVaried"/> chapters asked for, newest first, each once.</summary>
    internal static IReadOnlyList<string> RecentActivities(AdventureChapter chapter) =>
        [.. new[] { chapter.Previous }
            .Concat(chapter.Earlier.Reverse())
            .Take(RecentChaptersVaried)
            .SelectMany(adventure => adventure.Beats)
            .SelectMany(beat => beat.Steps)
            .Select(step => RefusedActivities.Key(step.Trigger))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)];

    /// <summary>What the Commander did while the chapter ran, newest first.</summary>
    private static void AppendNotables(StringBuilder text, AdventureChapter chapter)
    {
        var told = ChapterNotables.Told(chapter.Notables);

        if (told.Count == 0)
        {
            return;
        }

        text.AppendLine();
        text.AppendLine("What the Commander did while they flew that chapter, newest first:");

        foreach (var notable in told)
        {
            text.Append("- ").AppendLine(notable.Text);
        }

        text.AppendLine(
            "Refer to at least one of these where it fits the story, in a line said by a cast member, the narrator or the ship. "
            + "Do not name anyone who is not in the story. A reference is a line, not a reason to ask for the same activity again.");
    }

    /// <summary>How the Commander's conflicts ended, and whether each faction they worked for gained influence between visits.</summary>
    private static void AppendStandings(StringBuilder text, AdventureChapter chapter, SystemStandings? standings)
    {
        var factions = chapter.Previous.Beats
            .SelectMany(beat => beat.Steps)
            .Where(step => step.Trigger is { Kind: TriggerKind.Faction, Faction: { Length: > 0 } })
            .Select(step => step.Trigger.Faction!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (chapter.Conflicts.Count == 0 && factions.Count == 0)
        {
            return;
        }

        text.AppendLine();
        text.AppendLine("How the Commander's work in the world turned out, as d47 has seen it:");

        foreach (var part in chapter.Conflicts)
        {
            text.Append("- ").AppendLine(part.Describe());
        }

        foreach (var faction in factions)
        {
            text.Append("- ").AppendLine(FactionChange(faction, standings));
        }
    }

    private static string FactionChange(string faction, SystemStandings? standings)
    {
        var changes = (standings?.Systems ?? [])
            .Select(system => (System: system, Change: standings!.InfluenceChange(system, faction)))
            .Where(entry => entry.Change is not null)
            .Select(entry => $"{(entry.Change > 0 ? "rose" : entry.Change < 0 ? "fell" : "held")}"
                + $"{(entry.Change == 0 ? string.Empty : $" by {Math.Abs(entry.Change!.Value * 100).ToString("0.0", CultureInfo.InvariantCulture)} points")} in {entry.System}")
            .ToList();

        return changes.Count == 0
            ? $"The Commander worked missions for {faction}; d47 has not seen its influence on two visits a day apart."
            : $"The Commander worked missions for {faction}; between visits its influence {string.Join(", ", changes)}.";
    }

    /// <summary>
    /// Both layers of a stock story, its stage and that stage's beats, and in act one the beacon its last beat
    /// arrives at or the reason it cannot yet.
    /// </summary>
    /// <summary>The story the chapter belongs to; when <paramref name="lines"/>, only what a planned chapter's words are written from.</summary>
    private static void AppendStory(StringBuilder text, AdventureStory story)
    {
        text.AppendLine();
        text.AppendLine(
            $"This story is chapter {story.Chapter.ToString(CultureInfo.InvariantCulture)} of \"{story.Title}\", a stock story the "
            + $"Commander chose. It runs for {story.Length} or more, one chapter at a time, as they play, and follows the Save the Cat beats.");

        if (story.Stage is { Length: > 0 } stage)
        {
            text.AppendLine(story.FinaleChapter is { } finale
                ? $"The story stands in its finale: this is finale chapter {finale.ToString(CultureInfo.InvariantCulture)} of {story.FinaleChapters.ToString(CultureInfo.InvariantCulture)}{(finale >= story.FinaleChapters ? ", the story's last chapter" : string.Empty)}."
                : $"The story stands at {stage}. Write this chapter within that stage; do not reach a later beat.");
        }

        if (story.StageBeats is { Count: > 0 } beats)
        {
            text.AppendLine("The beat-sheet lines for this stage, which this chapter carries forward:");

            foreach (var (key, line) in beats)
            {
                text.AppendLine($"- {key}: {line}");
            }
        }
        text.AppendLine();
        text.AppendLine("What the Commander knows — the public layer, which they chose the story by:");
        text.AppendLine(story.Public);
        text.AppendLine();
        text.AppendLine(story.Hidden);

        if (story.Level is { Length: > 0 } level)
        {
            text.AppendLine();
            text.AppendLine(
                $"The story was written for a {level} Commander. A Commander may pick it at any level and advance during the "
                + "year. Where the public layer disagrees with what is true right now about ships, credits or ranks, what is "
                + "true right now wins, and the story's people and events stay.");
        }

        AppendGenre(text, story);
        AppendGoals(text, story);

        text.AppendLine();
        text.Append($"The story has been running for {story.DaysRunning.ToString(CultureInfo.InvariantCulture)} days");
        text.AppendLine(story.DaysSinceBeacon is { } scanned
            ? $", and {scanned.ToString(CultureInfo.InvariantCulture)} days have run since the Commander scanned the Guardian beacon, not counting days it was paused."
            : ", and the Commander has not yet scanned a Guardian beacon.");

        if (story.DaysSinceBeacon is not null)
        {
            text.AppendLine(
                "The Guardian beacon scan has already happened, and the Guardian cores came aboard with it. This chapter begins after "
                + "it, whatever the chapter before was working toward. No premise, objective, reason or line sends the Commander to a "
                + "Guardian beacon, asks for a beacon scan, or tells the trip to the beacon as still to come.");

            if (story.Remembered)
            {
                text.AppendLine(
                    "The Commander scanned a Guardian beacon in an earlier story, before this one opened. Where act one's beats lead "
                    + "toward a beacon, that trip is already behind them: tell it as the past that brought the core aboard.");
            }
        }

        text.AppendLine("Never write a line that states the hidden story. A chapter may echo a clue the Commander has had.");

        if (story.BeaconAway is { } away)
        {
            text.AppendLine();
            text.AppendLine(
                $"This is an act-one chapter, and the Guardian beacon the story needs, in {away.System}, is "
                + $"{away.LightYears.ToString("0", CultureInfo.InvariantCulture)} light years away and out of reach: {away.Why}. "
                + "The chapter works toward a ship that can make the trip: credits, a better ship, a laser or railgun, a fuel scoop or a cargo rack. "
                + "Its last objective is met once the Commander's ship can make the trip.");
        }

        if (story.Beacon is { } beacon)
        {
            text.AppendLine();
            text.AppendLine(
                $"This chapter ends act one at a Guardian beacon: its last objective is \"beacon\", the Commander "
                + $"collecting the Ancient Key at the Guardian beacon in {beacon.System} after charging its three pylons with laser or railgun fire and scanning its core. Collecting the key is when the "
                + "Guardian cores come aboard. The reason to go is the public layer's beacon line.");

            if (beacon.SwapTo is { } swap)
            {
                text.AppendLine(
                    $"The ship the Commander is in cannot make the trip, but a ship they own can: {swap}. "
                    + "The chapter's first objective is the swap into that ship.");
            }
        }
    }

    /// <summary>For a chapter exempt from the base rule that starts or heads beyond the near-here reach of a base holding stored ships, the reason to give and the ships to transfer.</summary>
    private static void AppendAway(StringBuilder text, BaseStep? home, AdventureAsk ask, Facts facts)
    {
        if (home is null || home.Base.IsHomeSystem || home.Position is not { } there || facts.Position is not { } here)
        {
            return;
        }

        var near = ShipBases.Near(facts.Ship.MaxJumpRange);
        var target = ChapterPull.Of(ask, here)?.There;

        if (here.DistanceTo(there) <= near && (target is null || target.Value.DistanceTo(there) <= near))
        {
            return;
        }

        text.AppendLine();
        text.AppendLine(
            $"The Commander has no fleet carrier, so their ships stay where they are stored, mostly {(home.Docks ? $"at {home.Station} in {home.System}" : $"in {home.System}")}, "
            + $"and this chapter is not held to ending near them. Its opening must say why the story stays away from {home.System}, "
            + "and tell the Commander to transfer there the ships they will need. Once the ships are moved, the base moves with them.");
    }

    /// <summary>
    /// The boardings and leavings this answer may place: the story's due ones, less those a rewrite keeps at the opening or
    /// on an objective before the ones it writes, and the boardings no hull can seat.
    /// </summary>
    private static IReadOnlyList<Stories.StorySwitch> Due(AdventureAsk ask, Facts facts)
    {
        if (ask.Story?.Due is not { Count: > 0 } all)
        {
            return [];
        }

        var due = all.Where(entry => entry.Seats is not { } seats || SeatHulls(seats, facts).Count > 0).ToList();

        if (ask.Rewrite is not { } rewrite)
        {
            return due;
        }

        var kept = rewrite.Chapter.Beats.Take(rewrite.From).ToList();
        var boarded = rewrite.Chapter.OpeningBoards.Concat(kept.SelectMany(beat => beat.Boards)).ToHashSet(StringComparer.Ordinal);
        var left = rewrite.Chapter.OpeningLeaves.Concat(kept.SelectMany(beat => beat.Leaves)).ToHashSet(StringComparer.Ordinal);

        return [.. due.Where(entry => !(entry.Boards ? boarded : left).Contains(entry.Id))];
    }

    /// <summary>The hulls with at least this many seats that the Commander owns or can afford to board, cheapest first, five at most.</summary>
    private static IReadOnlyList<string> SeatHulls(int seats, Facts facts) =>
        [.. EliteSpecifications.Ships
            .Where(ship => ship.Crew >= seats && ChapterFit.BoardWhy(ship.Symbol, facts.Owns(ship.Symbol), facts.Credits) is null)
            .OrderBy(ship => facts.Owns(ship.Symbol) ? 0 : ship.Cost ?? long.MaxValue)
            .ThenBy(ship => ship.Name, StringComparer.Ordinal)
            .Take(5)
            .Select(ship => ship.Name)];

    /// <summary>The boardings and leavings an objective of a draft carries, as the repair turn shows them; empty for none.</summary>
    private static string Switched(IReadOnlyList<string> boards, IReadOnlyList<string> leaves) =>
        (boards.Count > 0 ? $" — boards: {string.Join(", ", boards)}" : string.Empty)
        + (leaves.Count > 0 ? $" — leaves: {string.Join(", ", leaves)}" : string.Empty);

    /// <summary>Who may speak a story chapter's lines, and how the ship's lines are read while a stock core is aboard.</summary>
    private static void AppendSpeakers(StringBuilder text, AdventureStory story, IReadOnlyList<Stories.StorySwitch> due, bool opening, Facts facts)
    {
        text.AppendLine();
        text.AppendLine(
            "Every line, the opening included, has a speaker, and is written as that speaker says it: \"ship\", the core aboard"
            + (story.Core is { Length: > 0 } core ? $" ({core})" : string.Empty)
            + "; \"narrator\", who tells the story in the third person from outside the cockpit, never says \"I\", \"me\" or \"we\", "
            + "never speaks to the Commander, and never offers, asks or waits for anything; or one of the story's cast: in the cockpit while they are aboard the ship, "
            + "otherwise over comms, in a recording or a message. Give a line to the cast only where the Commander would hear that person. Most lines are the ship's. "
            + "A cast member's line is that person's own words, in the first person, as they would say them: it never names them and "
            + "never describes them, except that they may give their own name as a person does when introducing themselves "
            + "(\"This is …\", \"My name is …\"). Narration, including any line that describes a cast member, what they did or what they left behind, "
            + "is given to \"narrator\". When the Commander hears a cast member — a recording plays, a call comes in, a message is read out — "
            + "that person's words are a line of their own, given to them; never report what they say in another speaker's line "
            + "(\"a woman's voice reads out coordinates and gives her name\"). "
            + "A line that says a recording, call or message is played or heard has that person's words beside it, in the same "
            + "place, as their own line; never say one was played, or played before the story began, without them. "
            + "The opening and each objective have one to three lines, spoken in the order given. Most have one; give a second "
            + "or third only where a cast member speaks and the scene also needs narrating.");

        if (story.CoreIsStock)
        {
            text.AppendLine(
                "The core aboard is stock and does not tell this story: a \"ship\" line is read by the narrator, so it is the "
                + "narrator's line and follows the same rule: narration in the third person, about the Commander rather than to them, "
                + "never \"I\", \"me\" or \"we\", and never an offer, a question or a wait.");
        }

        if (story.Cast is { Count: > 0 } cast)
        {
            text.AppendLine(
                "The narrator says who each cast member is just before their first line, from the role given beside their name, "
                + "so never write that introduction. " + StorySpeaker.MannerRule);

            foreach (var member in cast)
            {
                text.AppendLine(member.Role is { Length: > 0 } role
                    ? $"- \"{member.Id}\": {member.Name}, {role} — {member.Who}"
                    : $"- \"{member.Id}\": {member.Name} — {member.Who}");
            }

            var aboard = cast.Where(member => story.Aboard?.Contains(member.Id, StringComparer.Ordinal) == true).ToList();
            var seated = aboard.Where(member => !story.OnComms.Contains(member.Id, StringComparer.Ordinal)).ToList();
            var onComms = aboard.Except(seated).ToList();
            var onCarrier = cast.Where(member => story.OnCarrier.Contains(member.Id, StringComparer.Ordinal)).ToList();

            text.AppendLine(
                (aboard.Count > 0
                    ? $"Aboard the Commander's ship as this chapter opens: {string.Join(", ", aboard.Select(member => member.Name))}. "
                      + (seated.Count > 0
                          ? $"{string.Join(", ", seated.Select(member => member.Name))} {(seated.Count == 1 ? "is" : "are")} in the cockpit with the Commander: "
                            + "write their lines as spoken there, in person, never over comms. "
                          : string.Empty)
                      + (onComms.Count > 0
                          ? $"The Commander's ship has no seat for {string.Join(", ", onComms.Select(member => member.Name))}: write their lines as heard over comms. "
                          : string.Empty)
                      + (onCarrier.Count > 0 ? string.Empty : "Every other cast member is elsewhere. ")
                    : "No cast member is aboard the ship as this chapter opens; "
                      + (onCarrier.Count > 0 ? "each other cast member is elsewhere. " : "each is elsewhere. "))
                + (onCarrier.Count > 0
                    ? $"On the Commander's fleet carrier as this chapter opens: {string.Join(", ", onCarrier.Select(member => member.Name))}. "
                      + "Write each of their lines to read correctly both in person, as when the Commander is docked at the carrier, and over the carrier's comms. "
                      + "They are never in the cockpit unless \"aboard\" lists them. "
                      + (aboard.Count > 0 ? "Every other cast member is elsewhere. " : string.Empty)
                    : string.Empty)
                + "\"aboard\" lists the id of each other cast member who rides along in the cockpit for this chapter alone, whose lines are then "
                + "written as spoken there. Most chapters list none.");

            AppendSwitches(text, cast, due, opening, facts);
        }
    }

    /// <summary>Each boarding and leaving the chapter may place, by name and beat, and how the lines around it are spoken.</summary>
    private static void AppendSwitches(StringBuilder text, IReadOnlyList<AdventureSpeaker> cast, IReadOnlyList<Stories.StorySwitch> due, bool opening, Facts facts)
    {
        if (due.Count == 0)
        {
            return;
        }

        text.AppendLine(
            "The story moves these cast members on or off the Commander's ship at a beat of this stage. Place each on the objective"
            + (opening ? ", or the opening," : string.Empty)
            + " where that beat happens in this chapter, by listing the member's id in that objective's "
            + (opening ? "\"boards\" or \"leaves\" (\"openingBoards\" or \"openingLeaves\" for the opening)" : "\"boards\" or \"leaves\"")
            + ". If this chapter does not reach the beat, list it nowhere: a later chapter places it. A boarding marked held is due in this chapter "
            + "although its beat has passed: place it on the opening or an objective where the member joins the ship.");

        foreach (var entry in due)
        {
            var name = cast.FirstOrDefault(member => member.Id == entry.Id)?.Name ?? entry.Id;

            var carrier = cast.FirstOrDefault(member => member.Id == entry.Id)?.OnCarrier == true;
            var boards = carrier ? "comes aboard the carrier" : "comes aboard";

            var line = entry.Passed
                ? $"- {name} (\"{entry.Id}\") {boards} (held: the {entry.Beat} beat has passed)."
                : $"- {name} (\"{entry.Id}\") {(entry.Boards ? boards : carrier ? "leaves the carrier" : "leaves the ship")} at the {entry.Beat} beat.";

            text.AppendLine(entry.Seats is { } seats
                ? line + $" The Commander's ship has no seat for {name}: place the boarding only on or after an objective that boards a ship with {seats} seats or more, such as {string.Join(", ", SeatHulls(seats, facts))}."
                : line);
        }

        text.AppendLine(
            "A member's lines from the objective they board at on are spoken in the cockpit, and before it over comms. A member's lines "
            + "before the objective they leave at are spoken in the cockpit, and from it on over comms. Leave every other objective's lists empty.");
    }

    /// <summary>The most missions a story chapter is told of.</summary>
    public const int MissionsShown = 10;

    /// <summary>The story's genre and its elements.</summary>
    private static void AppendGenre(StringBuilder text, AdventureStory story)
    {
        if (story.Genre is { Length: > 0 } genre)
        {
            text.AppendLine();
            text.Append($"The story's genre is {genre}");
            text.AppendLine(story.GenreElements is { Count: > 0 } elements
                ? $", and every chapter keeps its three elements in play: {string.Join(", ", elements)}."
                : ".");

            if (genre == "Buddy Love")
            {
                text.AppendLine(
                    "The partner exists only in the fiction. A chapter may offer hiring a crew member (a \"crew\" objective) as part of it, "
                    + "but never requires it.");
            }
        }
    }

    /// <summary>The Commander's goals the chapter works, and which drawn activities work each.</summary>
    private static void AppendGoals(StringBuilder text, AdventureStory story)
    {
        if (story.Goals is { Count: > 0 } goals)
        {
            text.AppendLine();

            foreach (var goal in goals)
            {
                var drawn = goals.Count > 1 && Earning(goal, goals, story.Slots) is { Length: > 0 } earning ? $"The drawn activities to {earning}" : "The drawn activities";

                text.AppendLine(goal.Objective is { } objective
                    ? $"This chapter's Goals objective works the Commander's goal \"{goal.Name}\", and the chapter must contain it: {GoalLine(objective)}. Its want and its turn give the Commander a reason for it."
                    : Careers.IsNavy(goal.Draw)
                        ? $"{drawn} work the Commander's goal \"{goal.Name}\": the missions are for factions of the {(goal.Draw == Careers.Empire ? "Empire" : "Federation")}, which is what earns {Careers.Word(goal.Draw)} rank, so the story gives the Commander a reason to work for them."
                        : $"{drawn} work the Commander's goal \"{goal.Name}\".");
            }

            if (goals.Count > 1)
            {
                text.AppendLine(
                    $"The chapter works both goals, \"{goals[0].Name}\" and \"{goals[1].Name}\", in one story line: its want and its turn give the Commander one reason for both.");
            }
        }
    }

    /// <summary>The phrases of the drawn slots credited to <paramref name="goal"/>, joined with "and".</summary>
    private static string Earning(GoalClimbStep goal, IReadOnlyList<GoalClimbStep> goals, IReadOnlyList<DrawnActivity>? slots) =>
        string.Join(" and ", (slots ?? [])
            .Zip(GoalClimb.Credited(goals, slots))
            .Where(pair => pair.Second == goal)
            .Select(pair => RefusedActivities.Phrase(pair.First.Kind, pair.First.Family))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal));

    /// <summary>A Goals objective as the writer is told it, and as a refusal names it.</summary>
    private static string GoalLine(AdventureTrigger objective) => objective.Kind switch
    {
        TriggerKind.Rank => $"a \"rank\" objective for {Careers.RankSaid(objective.Career, objective.Rank)}",
        TriggerKind.PowerRank => $"a \"powerrank\" objective for rank {objective.Rank?.ToString(CultureInfo.InvariantCulture)}",
        TriggerKind.Pledge => "a \"pledge\" objective, to any Power the story gives the Commander a reason to serve",
        TriggerKind.Engineer => $"an \"engineer\" objective for {objective.Engineer} at the stage {objective.Stage}",
        TriggerKind.Board => $"a \"board\" objective for a {EliteSpecifications.HullName(objective.ShipType) ?? objective.ShipType}",
        _ => $"a \"{objective.Kind.ToString().ToLowerInvariant()}\" objective",
    };

    /// <summary>An event slot as the writer is told it: the event, what it means, the field test in words and the count.</summary>
    private static string EventSlotLine(DrawnActivity slot)
    {
        var row = StepEventCatalogue.Default.Drawable.FirstOrDefault(entry => entry.Event == slot.Event);
        var what = $"{slot.Event}: {row?.Description.TrimEnd('.')}";
        var pairs = string.Join(" and ", slot.Where.Select(pair => $"{pair.Field} is {pair.Value}"));
        var count = slot.Count.ToString(CultureInfo.InvariantCulture);
        var line = $"an \"event\" objective for the event {what}{(pairs.Length > 0 ? $", where {pairs}" : string.Empty)}, count {count}";

        return slot.Say is { } say
            ? $"{line}. The Commander runs it by saying \"{say}\": the \"task\" must tell them to say it"
            : line;
    }

    private static void AppendExchange(StringBuilder text, IReadOnlyList<AdventureRemark>? exchange, string? remark)
    {
        if (exchange is { Count: > 0 })
        {
            text.AppendLine();
            text.AppendLine("The conversation about it so far:");

            foreach (var round in exchange)
            {
                text.Append("Commander: ").AppendLine(round.Remark);

                if (!string.IsNullOrWhiteSpace(round.Reply))
                {
                    text.Append("You: ").AppendLine(round.Reply);
                }
            }
        }

        text.AppendLine();
        text.AppendLine($"The Commander now says: \"{remark}\"");
    }

    /// <summary>The items in an order fixed by <paramref name="seed"/>, so one draft's prompts agree and the next draft's differ.</summary>
    internal static IReadOnlyList<T> Shuffled<T>(IEnumerable<T> items, string seed)
    {
        var hash = 2166136261u;

        foreach (var character in seed)
        {
            hash = (hash ^ character) * 16777619u;
        }

        var list = items.ToList();
        new Random(unchecked((int)hash)).Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list));
        return list;
    }

    /// <summary>A beat's steps as the model is shown them: each described, in order.</summary>
    private static string Steps(AdventureBeat beat) => string.Join(", then ", beat.Steps.Select(step => step.Trigger.Describe()));

    /// <summary>A beat's lines as the model is shown them: each in quotes, after its speaker when one is named.</summary>
    private static string Quoted(IReadOnlyList<AdventureLine> lines) =>
        string.Join(" / ", lines.Select(line => line.Speaker is { Length: > 0 } speaker ? $"{speaker}: \"{line.Text}\"" : $"\"{line.Text}\""));

    // ---- reading the answers ---------------------------------------------------------------

    /// <summary>What <paramref name="read"/> takes from the root of <paramref name="json"/>, or the default when it is not JSON.</summary>
    private static T? ReadJson<T>(string json, bool allowTrailingCommas, Func<JsonElement, T?> read)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = allowTrailingCommas });

            return read(document.RootElement);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    /// <summary>One step of a planned objective: its trigger, and the words written for the objective it belongs to.</summary>
    private sealed record ReadBeat(string Title, string? Function, AdventureTrigger Trigger, string? Reason, IReadOnlyList<AdventureLine> Lines)
    {
        public TriggerKind Kind => Trigger.Kind;

        /// <summary>Whether this is a further step of the objective before it and not an objective of its own.</summary>
        public bool Continues { get; init; }

        /// <summary>For an event step: the phrase the model wrote for what the Commander does.</summary>
        public string? Task { get; init; }

        /// <summary>For a tool run step: the phrase the Commander says, which the task has to hold.</summary>
        public string? Say { get; init; }

        /// <summary>The cast ids the model wrote as boarding at this objective.</summary>
        public IReadOnlyList<string> Boards { get; init; } = [];

        /// <summary>The cast ids the model wrote as leaving at this objective.</summary>
        public IReadOnlyList<string> Leaves { get; init; } = [];

        /// <summary>The title, marked when this is a further step.</summary>
        public string Named => Continues ? $"{Title}, a further step" : Title;

        /// <summary>A counted or engineer step's trigger without its place fields.</summary>
        public AdventureTrigger Written() => new()
        {
            Kind = Kind,
            Count = Trigger.Count,
            Faction = Trigger.Faction,
            MissionFamily = Trigger.MissionFamily,
            Commodity = Trigger.Commodity,
            Filter = Trigger.Filter,
            Organic = Trigger.Organic,
            New = Trigger.New,
            System = Kind is TriggerKind.Uss or TriggerKind.Map or TriggerKind.Signal or TriggerKind.Organic && !string.IsNullOrWhiteSpace(Trigger.System) ? Trigger.System.Trim() : null,
            Engineer = Trigger.Engineer,
            Stage = Trigger.Stage,
            Rank = Kind == TriggerKind.PowerRank ? Trigger.Rank : null,
            Handle = Trigger.Handle,
            Station = AdventureTrigger.IsMissionHandleKind(Kind) ? Trigger.Station : null,
            Event = Kind == TriggerKind.Event ? Trigger.Event : null,
            Where = Kind == TriggerKind.Event ? Trigger.Where : EventFilter.None,
            Task = Kind == TriggerKind.Event ? Task : null,
        };

        /// <summary>The step as the checks are shown it.</summary>
        public string Describe() => AdventureTrigger.IsCountedKind(Kind) || AdventureTrigger.IsOnceKind(Kind) || AdventureTrigger.IsMissionHandleKind(Kind) || Kind is TriggerKind.Engineer or TriggerKind.Unlock or TriggerKind.Pledge or TriggerKind.PowerRank ? Written().Describe() : Kind switch
        {
            TriggerKind.Rank => $"rank: {Careers.Word(Careers.Match(Trigger.Career) ?? Trigger.Career)} {Trigger.Rank?.ToString(CultureInfo.InvariantCulture) ?? "?"}",
            TriggerKind.Dock => $"dock: {Trigger.Station ?? "?"} in {Trigger.System ?? "?"}",
            TriggerKind.Land => $"land: {Trigger.Body ?? "?"} in {Trigger.System ?? "?"}",
            TriggerKind.Scan => $"scan: {Trigger.Body ?? "?"} in {Trigger.System ?? "?"}",
            TriggerKind.Board => $"board: {EliteSpecifications.HullName(Trigger.ShipType) ?? Trigger.ShipType ?? "?"}",
            TriggerKind.Beacon => "beacon",
            TriggerKind.Reach => "reach",
            TriggerKind.Site => "site",
            TriggerKind.Choice => Trigger.Describe(),
            _ => $"arrive: {Trigger.System ?? "?"}",
        };
    }

    private sealed record ReadAnswer(IReadOnlyList<AdventureLine> Opening, string? Reply, IReadOnlyList<ReadBeat> Beats)
    {
        /// <summary>The ids the chapter's "aboard" lists, as written.</summary>
        public IReadOnlyList<string> Aboard { get; init; } = [];

        /// <summary>The ids "openingBoards" lists, as written.</summary>
        public IReadOnlyList<string> OpeningBoards { get; init; } = [];

        /// <summary>The ids "openingLeaves" lists, as written.</summary>
        public IReadOnlyList<string> OpeningLeaves { get; init; } = [];
    }

    /// <summary>The strings of the list <paramref name="property"/>, as written; empty when there is none.</summary>
    private static IReadOnlyList<string> Ids(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var list) && list.ValueKind == JsonValueKind.Array
            ? [.. list.EnumerateArray().Where(id => id.ValueKind == JsonValueKind.String).Select(id => id.GetString()!)]
            : [];

    /// <summary>The opening's lines: the <c>opening</c> list a story chapter is asked for, or the single <c>opening</c> and <c>openingSpeaker</c>.</summary>
    private static IReadOnlyList<AdventureLine> ReadOpening(JsonElement root)
    {
        if (root.TryGetProperty("opening", out var opening) && opening.ValueKind == JsonValueKind.Array && LinesOf(opening) is { Count: > 0 } read)
        {
            return read;
        }

        return Text(root, "opening") is { } text ? [new AdventureLine { Text = text, Speaker = Text(root, "openingSpeaker") }] : [];
    }

    /// <summary>A list of lines, each a string or a text and speaker, blank ones dropped, at most <see cref="AdventureLimits.MaxLinesPerBeat"/>.</summary>
    private static List<AdventureLine> LinesOf(JsonElement lines) =>
        [
            .. lines.EnumerateArray()
                .Select(line => line.ValueKind == JsonValueKind.String
                    ? new AdventureLine { Text = line.GetString() ?? string.Empty }
                    : line.ValueKind == JsonValueKind.Object
                        ? new AdventureLine { Text = Text(line, "text") ?? Text(line, "line") ?? string.Empty, Speaker = Text(line, "speaker") }
                        : null)
                .OfType<AdventureLine>()
                .Where(line => !string.IsNullOrWhiteSpace(line.Text))
                .Take(AdventureLimits.MaxLinesPerBeat),
        ];

    private static string? Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() is { } text
        && !string.IsNullOrWhiteSpace(text)
            ? text.Trim()
            : null;

    private static int? Integer(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };
    }

    // ---- the checked chapter ---------------------------------------------------------------

    private sealed record Resolved(IReadOnlyList<AdventureBeat> Beats, IReadOnlyList<Refusal> Refusals, AdventureDestination? Destination)
    {
        /// <summary>The opening's lines with their speakers resolved.</summary>
        public IReadOnlyList<AdventureLine> Opening { get; init; } = [];

        /// <summary>For each beat in <see cref="Beats"/>, the index of the objective it was written as.</summary>
        public IReadOnlyList<int> Stood { get; init; } = [];

        /// <summary>For each beat in <see cref="Beats"/>, whether it is a further step of the one before it.</summary>
        public IReadOnlyList<bool> Joins { get; init; } = [];
    }

    /// <summary>
    /// Why a check refused something, and what a repair may write again to clear it: <see cref="Objectives"/> by index in
    /// the beats, the opening, or the title and spine.
    /// </summary>
    private sealed record Refusal(string Text, IReadOnlyList<int> Objectives)
    {
        public IReadOnlyList<string>? Reasons { get; init; }

        public bool Opening { get; init; }

        /// <summary>Whether it falls on the title or the spine.</summary>
        public bool Spine { get; init; }

        /// <summary>Whether the plain-wording check raised it.</summary>
        public bool Plain { get; init; }

        /// <summary>The plain-wording objections behind it, with any rewrites the checker gave.</summary>
        public IReadOnlyList<PlainObjection> Unplain { get; init; } = [];
    }

    /// <summary>The resolved objectives as beats: each further step joins the beat before it.</summary>
    private static IReadOnlyList<AdventureBeat> Grouped(Resolved resolved)
    {
        var beats = new List<AdventureBeat>();

        for (var index = 0; index < resolved.Beats.Count; index++)
        {
            var beat = resolved.Beats[index];

            if (index < resolved.Joins.Count && resolved.Joins[index] && beats.Count > 0)
            {
                beats[^1] = beats[^1] with { Steps = [.. beats[^1].Steps, .. beat.Steps] };
            }
            else
            {
                beats.Add(beat);
            }
        }

        return beats;
    }

    /// <summary>Whether the trigger is in the sight's system: by <c>SystemAddress</c> when the sight has one, otherwise by name.</summary>
    private static bool AtSight(NotablePlace sight, AdventureTrigger trigger) =>
        sight.SystemAddress is { } address
            ? trigger.SystemAddress == address
            : string.Equals(sight.System, trigger.System, StringComparison.OrdinalIgnoreCase);

    /// <summary>The system of the first arrive, land or scan objective at one of the sights, or null.</summary>
    private static string? SightSeen(IReadOnlyList<NotablePlace>? sights, IEnumerable<AdventureBeat> beats) =>
        sights is not { Count: > 0 }
            ? null
            : beats.SelectMany(beat => beat.Steps)
                .Select(step => step.Trigger)
                .FirstOrDefault(trigger => trigger.Kind is TriggerKind.Arrive or TriggerKind.Land or TriggerKind.Scan && sights.Any(sight => AtSight(sight, trigger)))?.System;

    private static bool IsAnarchy(string? government) =>
        string.Equals(government, "Anarchy", StringComparison.OrdinalIgnoreCase);

    // ---- what d47 reads rather than asks ---------------------------------------------------

    /// <summary>
    /// Where the Commander is, what they can move, and what they hold — read from the game state, never
    /// asked, because asking would be asking them to describe their own ships.
    /// </summary>
    internal sealed record Facts(
        string? System,
        StarPosition? Position,
        double RadiusLightYears,
        AdventureReach Reach,
        bool ThisShipOnly,
        ShipLoadout Ship,
        IReadOnlyList<(string Describe, string? Pad, bool Here)> Fleet,
        CarrierState Carrier,
        RankState Ranks,
        long? Credits,
        double? DestinationLightYears = null,
        IReadOnlyList<string>? OwnedHulls = null,
        SystemStandings? Standings = null,
        IReadOnlyList<Mission>? Missions = null,
        long? ClosedMarketId = null,
        SpanReach? SpanReach = null,
        bool SpanTotal = false)
    {
        public static Facts Of(CommanderGameState? state, AdventureAsk ask)
        {
            var ship = state?.Ship ?? ShipLoadout.Unknown;
            var carrier = state?.Carrier ?? CarrierState.None;

            var fleet = new List<(string, string?, bool)>();

            if (ship.IsKnown)
            {
                fleet.Add((ship.Describe() ?? "the ship you are in", PadOf(ship.Type), true));
            }

            foreach (var stored in state?.Fleet.Ships ?? [])
            {
                fleet.Add((stored.Describe() + (stored.Here ? ", stored here" : $", stored at {stored.StarSystem}"), PadOf(stored.Type), false));
            }

            return new Facts(
                state?.Location.StarSystem,
                state?.Location.StarPos,
                ReachOf(ask, state?.Location.StarPos, ship.MaxJumpRange, carrier.Owned),
                ask.Reach,
                ask.ThisShipOnly,
                ship,
                fleet,
                carrier,
                state?.Ranks ?? RankState.Empty,
                state?.Session.Balance,
                OwnedHulls: [.. new[] { ship.Type }.Concat((state?.Fleet.Ships ?? []).Select(stored => stored.Type)).OfType<string>().Select(type => EliteSpecifications.HullSymbol(type) ?? type)],
                Standings: state?.Standings,
                Missions: state?.Missions.Missions,
                ClosedMarketId: ask.Rewrite?.ClosedMarketId,
                SpanReach: SpanReachOf(ask, state?.Location.StarPos),
                SpanTotal: ask.Story?.Span is not null);
        }

        public bool Owns(string symbol) =>
            OwnedHulls?.Any(hull => string.Equals(hull, symbol, StringComparison.OrdinalIgnoreCase)) == true;

        private static string? PadOf(string? type) => EliteSpecifications.Ship(type)?.Pad;

        /// <summary>The pad a dock beat's station must have for the hull flown there (null: the ship aboard); null where that pad is unknown.</summary>
        public PadSize? NeedsPad(string? flownHull = null) =>
            ParsePad(PadOf(flownHull ?? Ship.Type));

        private static PadSize? ParsePad(string? pad) =>
            Enum.TryParse<PadSize>(pad, ignoreCase: true, out var size) ? size : null;

        public string Describe()
        {
            var text = new StringBuilder();

            text.AppendLine("What is true right now, read from the Commander's journal:");
            text.AppendLine($"- Position: {System ?? "unknown"}.");
            if (SpanReach is { } span)
            {
                var source = span.Ship is null ? "the reach the Commander's fleet carrier gives" : "a hundred jumps of the best jump range in the Commander's fleet";

                text.AppendLine(SpanTotal
                    ? $"- Reach: about {RadiusLightYears:0} light years, {source}. It is both the longest one hop may be and the most the chapter's hops may add up to."
                    : $"- Reach: about {RadiusLightYears:0} light years, {source}, the longest one hop may be. A place farther away is reached over several hops.");

                if (span.Ship is { Aboard: false } giver)
                {
                    var name = giver.Name is { Length: > 0 } named ? $" \"{named}\"" : string.Empty;

                    text.AppendLine($"- That reach is the {giver.Hull ?? "ship"}{name}'s, which is stored {(giver.Where is { } where ? $"in {where}" : "where the journal does not say")}; the Commander is not aboard it.");
                }
            }
            else
            {
                text.AppendLine($"- Reach: {Reach switch { AdventureReach.NearHere => "near here", AdventureReach.Session => "a session's flying", _ => "anywhere" }} — about {RadiusLightYears:0} light years, the longest one hop may be. A place farther away is reached over several hops.");
            }

            if (Fleet.Count > 0)
            {
                text.AppendLine(ThisShipOnly
                    ? $"- Ship: {Fleet.First(f => f.Here).Describe} ({Fleet.First(f => f.Here).Pad ?? "unknown"} pad), and the story stays in this ship."
                    : "- Ships the Commander owns, any of which the story may send them to fetch: "
                      + string.Join("; ", Fleet.Select(f => $"{f.Describe} ({f.Pad ?? "unknown"} pad{(f.Here ? ", aboard" : string.Empty)})")) + ".");
            }

            if (Ship.MaxJumpRange is { } jump)
            {
                text.AppendLine($"- Jump range of the ship aboard: {jump.ToString("0.0", CultureInfo.InvariantCulture)} light years.");
            }

            if (Credits is { } credits)
            {
                text.AppendLine($"- Credits at the last load: {credits.ToString("N0", CultureInfo.InvariantCulture)}.");
            }

            if (Carrier.Owned)
            {
                text.AppendLine($"- Fleet carrier: {Carrier.Name ?? Carrier.CallSign}, at {Carrier.StarSystem ?? "an unknown system"}. It moves 500 light years a jump and carries the stored ships aboard it.");
            }

            if (Ranks.IsKnown)
            {
                text.AppendLine("- Ranks held: " + string.Join(", ", Ranks.Standings
                    .Where(standing => Careers.Keys.Contains(standing.Career, StringComparer.OrdinalIgnoreCase))
                    .Select(standing => standing.Say(withPercent: false))) + ".");
            }

            return text.ToString();
        }
    }

    private static bool IsStar(BodySummary body) =>
        body.Subtype?.Contains("Star", StringComparison.OrdinalIgnoreCase) == true
        || body.Subtype?.Contains("Black Hole", StringComparison.OrdinalIgnoreCase) == true;

    private bool Closed(string? system, long? address, Facts facts) =>
        PermitSystemTable.ClosedTo(system, facts.Ranks, () => address is { } id && visited?.Find(state()?.Identity.FrontierId, id).State == VisitState.Visited);
}
