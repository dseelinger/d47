using D47.Core.Configuration;
using D47.Core.Conversation;

namespace D47.Core.Persona;

/// <summary>
/// Why a core is about to speak on being selected.
/// </summary>
public enum PersonaArrival
{
    /// <summary>
    /// First time this session. It introduces itself in its own voice, from its authored intro.
    /// <para>
    /// <b>Overturns a comment.</b> This said the authored text was "spoken rather than prompted,
    /// because putting written material through a model to get it back slightly worse is not a
    /// use of a turn" — which is right about finished writing and wrong about what this is.
    /// <c>guardian-personas.md</c> files these under <em>sample lines, not intended to be
    /// verbatim</em>: they are a demonstration of how a core sounds, not the script it reads.
    /// Every Commander who picked Warden heard the same paragraph, word for word, every time.
    /// </para>
    /// <para>
    /// So the authored line is put through the model when there is one, as a rewording rather
    /// than a composition — the substance is the persona pack's and only the sentences are the
    /// model's. It stays the fallback, so a Commander with no provider, no personality or a
    /// failed call hears exactly what they heard before.
    /// </para>
    /// </summary>
    Introduction,

    /// <summary>
    /// Selected again after a long time away. The pack calls this a gap reaction and it replaces
    /// the switch-in bark: the core returns to a ship that has moved, and it explains the missing
    /// time according to its own damage. Needs a model, because the reaction is to a telemetry
    /// delta that no authored line could anticipate; with none, it falls back to the authored
    /// return line.
    /// <para>
    /// <b>Long means <see cref="PersonaHost.GapAfter"/>, and it used to mean any measurable
    /// stretch at all.</b> A core remarking on the missing time every single time it was picked
    /// made the reaction the normal case, which is the opposite of what a reaction is. The
    /// Commander's correction, taken on 2026-08-19 with Phase 35: occasional, not automatic.
    /// </para>
    /// </summary>
    Gap,

    /// <summary>
    /// Aboard, and saying nothing. Three arrivals are silent and they are all the same argument
    /// — the core is not new to this Commander and nothing has happened worth remarking on.
    /// <list type="bullet">
    /// <item>A core reselected inside <see cref="PersonaHost.GapAfter"/>.</item>
    /// <item>A core arriving because the Commander boarded the ship they bound it to. They said
    /// what should fly this ship; d47 doing it is keeping the deal rather than announcing
    /// it (Phase 35).</item>
    /// <item>The binding for the ship d47 finds them already in at startup.</item>
    /// </list>
    /// The switch is still real — the voice, the wake word and the transcript all change. What
    /// is skipped is a line, and with it a model call.
    /// </summary>
    Quiet,
}

/// <summary>
/// Why the core changed. Not <em>who</em> asked — the caller trust question is the settings
/// surface's and is answered by <see cref="Configuration.SettingsCaller"/> — but what kind of
/// event this is, which is what decides whether the arriving core says anything (Phase 35).
/// </summary>
public enum PersonaSwitch
{
    /// <summary>The Commander picked this core: a panel row, a phrase, a gesture.</summary>
    Selected,

    /// <summary>They boarded a ship they had bound to this core.</summary>
    Ship,

    /// <summary>
    /// The binding for the ship they were already in when d47 started. Silent, and it does not
    /// spend the core's introduction either: a core that has never spoken to this Commander has
    /// still never spoken to them, and it introduces itself the next time it arrives.
    /// </summary>
    Adopted,
}

/// <summary>What a returning core is reacting to. All of it is already-known ship telemetry.</summary>
/// <param name="Away">
/// How long the core was not running. Passed in rather than read from a clock, because no Core
/// component reads the clock — that is what makes the replay harness possible.
/// </param>
public sealed record PersonaGap(TimeSpan Away, string? TelemetryDelta);

/// <summary>Emitted when the Commander changes core. The surface decides what to do about it.</summary>
public sealed record PersonaChanged(Persona? Previous, Persona Current, PersonaArrival Arrival, PersonaGap? Gap);

/// <summary>
/// Which core is aboard, what it sounds like, and what it remembers.
/// <para>
/// <b>Separate memory per core is the requirement, not a nicety.</b> If the cores share a
/// transcript the fiction collapses inside one session — a core references something it could
/// only have learned while another was active, and the premise holding the entire cast
/// together is that none of them knows the others exist. The only shared state is ship
/// telemetry: position, hull, cargo, credits. That asymmetry is the design. They all see the
/// same instrument panel and none of them see each other (guardian-personas.md).
/// </para>
/// <para>
/// Owns no thread and reads no clock. The elapsed time a gap reaction needs is handed in by
/// the caller that does have a clock, for the same reason the journal reader exposes
/// <c>Poll()</c> rather than ticking itself.
/// </para>
/// </summary>
public sealed class PersonaHost
{
    private readonly Dictionary<string, List<ConversationMessage>> _transcripts = new(StringComparer.Ordinal);

    /// <summary>
    /// Which cores have already introduced themselves, so a second selection gets a gap reaction
    /// rather than the introduction again.
    /// <para>
    /// <b>Remembered across sessions, which it deliberately was not.</b> It used to be
    /// session-scoped like the transcripts beside it, on the reasoning that d47 has never
    /// persisted conversation history and starting to do so was not what that phase was asked
    /// for. That reasoning was about the <em>conversation</em>, and this is not one: it is a
    /// single fact per core — has this one said its opening line — with no wording in it and
    /// nothing of what was discussed. Persisting it costs a list of ids and buys a cast who stop
    /// re-introducing themselves every launch
    /// (docs/plans/change-requests.md item 7).
    /// </para>
    /// <para>
    /// The transcripts are untouched by this and still die with the session.
    /// </para>
    /// </summary>
    private readonly HashSet<string> _introduced;

    /// <summary>
    /// Where the set is kept between runs, or null for a host that forgets — the designer, the
    /// replay harness, and every test that is not about this.
    /// </summary>
    private readonly IIntroductionMemory? _memory;

    private string? _shipNameOverride;

    /// <summary>Whether the humor line rides the block (#243). Set with the name, on every apply.</summary>
    private bool _humor;

    public PersonaHost(Persona? current = null, IIntroductionMemory? memory = null)
    {
        Current = current ?? PersonaCatalog.Resolve(null);
        _memory = memory;
        _introduced = new HashSet<string>(memory?.Load() ?? [], StringComparer.Ordinal);
    }

    /// <summary>The core aboard. Never null — "personality off" is a prompt decision, not an empty seat.</summary>
    public Persona Current { get; private set; }

    /// <summary>
    /// How long a core has to have been away before it remarks on the missing time.
    /// <para>
    /// A month, set by the Commander on 2026-08-19: <em>comments on gaps should normally be
    /// occasional, not automatic</em>. It was previously any measurable stretch, which made a
    /// core switched away from and back inside an evening open by explaining its own absence.
    /// </para>
    /// <para>
    /// A threshold rather than a dice roll, because "occasional" has to be a property somebody
    /// can predict: a core that reacts to a fortnight one time in four is a core whose silence
    /// the Commander cannot read. And a month is the span that makes the reaction true — the
    /// telemetry delta it is reacting to is a ship that really has moved on.
    /// </para>
    /// <para>
    /// Reachable only across sessions, which is why the elapsed time handed in has to survive a
    /// restart. Nothing in Core does that; <c>ViewState.CoresLastAboard</c> does.
    /// </para>
    /// </summary>
    public static readonly TimeSpan GapAfter = TimeSpan.FromDays(30);

    /// <summary>
    /// The cores that have already introduced themselves, in catalog order. Exposed because the
    /// panel offers to forget them, and an offer to clear something has to be able to say what
    /// there is to clear.
    /// </summary>
    public IReadOnlyList<Persona> Introduced =>
        [.. PersonaCatalog.All.Where(p => _introduced.Contains(p.Id))];

    /// <summary>
    /// Forgets every introduction at once, so the next selection of any core is its authored
    /// intro again rather than a gap reaction.
    /// <para>
    /// The alternative was restarting d47, which is what hearing a second core's opening line
    /// used to cost. It clears the whole set rather than one core because the thing being
    /// checked is the cast, not a member of it.
    /// </para>
    /// <para>
    /// Includes the core currently aboard, but hearing that one again still means switching
    /// away and back: selecting the core that is already running is not a switch and raises
    /// nothing, which is the same rule that stops an unrelated settings edit re-introducing it.
    /// </para>
    /// <para>
    /// Now the <em>only</em> way back. A restart used to reset every introduction as a side
    /// effect of forgetting them, which made this button a convenience; it is the mechanism.
    /// </para>
    /// </summary>
    public void ForgetIntroductions()
    {
        _introduced.Clear();
        Remember();
    }

    /// <summary>
    /// Writes the set out, if this host was given somewhere to write it. Called on both edges —
    /// a core introducing itself and the whole set being forgotten — because a forget that is
    /// not written down comes back at the next launch, which is the failure the button exists
    /// to prevent.
    /// </summary>
    private void Remember() => _memory?.Save([.. _introduced]);

    /// <summary>Raised after the switch, with everything the surface needs to speak about it.</summary>
    public event Action<PersonaChanged>? Changed;

    /// <summary>
    /// What the Commander calls the ship's AI: their own name for it, or the core's own
    /// (Phase 11, "Ship AI Naming"). Asked rather than stored, so it follows the
    /// persona when nothing has been overridden.
    /// </summary>
    public string ShipName =>
        string.IsNullOrWhiteSpace(_shipNameOverride) ? Current.Name : _shipNameOverride.Trim();

    /// <summary>
    /// The current core's transcript. This is the list <see cref="TurnLoop"/> appends to, handed
    /// over by reference on purpose: the alternative is copying it back on every switch, and a
    /// copy that is one turn stale is a core remembering a conversation that did not happen.
    /// </summary>
    public List<ConversationMessage> Transcript => TranscriptFor(Current.Id);

    /// <summary>
    /// Drops every core's transcript, so the next turn with any of them starts from nothing
    /// (Phase 44: <i>"The old transcript goes away, a new one is created."</i>).
    /// <para>
    /// Every core's rather than the one aboard, because the conversation each of them holds was
    /// with the Commander who just logged out — a second core brought aboard by the new Commander
    /// would otherwise pick up a conversation they were never part of, which is the isolation
    /// model failing in a direction it was never built to watch.
    /// </para>
    /// <para>
    /// New lists rather than cleared ones, on purpose: <see cref="Transcript"/> is handed over by
    /// reference, and a caller that still holds the old list is holding the old Commander's
    /// conversation — it has to ask for the new one, and <c>TurnLoop.UseTranscript</c> is that ask.
    /// The introductions are untouched: a core that has said its opening line has said it.
    /// </para>
    /// </summary>
    public void ForgetTranscripts() => _transcripts.Clear();

    /// <summary>
    /// The persona block for the assembled prompt, or null when personality is off.
    /// <para>
    /// Null rather than a neutral block, because position 3 being absent is what "personality
    /// off" means structurally — and the guardrails at position 2 are untouched either way,
    /// which is the property the whole arrangement exists to guarantee (architecture.md §6).
    /// </para>
    /// </summary>
    public string? RenderBlock(bool personalityEnabled) =>
        personalityEnabled ? Current.RenderBlock(_shipNameOverride, _humor) : null;

    /// <summary>
    /// Applies the persona and ship-name settings. Called on every settings change; does
    /// nothing and raises nothing unless the core actually differs, so a Commander editing an
    /// unrelated row does not make their companion introduce itself again.
    /// </summary>
    /// <param name="away">
    /// How long the incoming core has been off, for its gap reaction. Null on a first
    /// selection, and null when the caller has no clock to measure it with.
    /// </param>
    /// <param name="telemetryDelta">
    /// What changed aboard while it was away, already rendered. The one thing every core can
    /// see regardless of which of them was running.
    /// </param>
    /// <param name="cause">
    /// What kind of switch this is. Decides only whether the arriving core speaks — the switch
    /// itself is identical either way, which is what keeps the isolation model untouched: a core
    /// still has no way to know why it was switched on or what was on before it.
    /// </param>
    /// <returns>True if the core changed.</returns>
    public bool Apply(
        PersonaSettings settings,
        TimeSpan? away = null,
        string? telemetryDelta = null,
        PersonaSwitch cause = PersonaSwitch.Selected)
    {
        _shipNameOverride = settings.ShipName;
        _humor = settings.Humor;

        var incoming = PersonaCatalog.Resolve(settings.Id);

        if (ReferenceEquals(incoming, Current))
        {
            return false;
        }

        var previous = Current;
        Current = incoming;

        var arrival = Arriving(incoming, away, cause);

        Changed?.Invoke(new PersonaChanged(
            previous,
            incoming,
            arrival,
            arrival == PersonaArrival.Gap ? new PersonaGap(away!.Value, telemetryDelta) : null));

        return true;
    }

    /// <summary>
    /// Which of the three arrivals this is, and the only place that decides it.
    /// <para>
    /// Read as a ladder rather than as three cases. A core this Commander has never had aboard
    /// introduces itself however it arrived — that is the one line worth hearing and it is heard
    /// once ever. Everything after that is silent unless the core has genuinely been gone, which
    /// is <see cref="GapAfter"/>.
    /// </para>
    /// <para>
    /// A startup adoption is outside the ladder entirely: it neither speaks nor spends the
    /// introduction, because nothing was heard and an introduction nobody heard is not spent.
    /// </para>
    /// </summary>
    private PersonaArrival Arriving(Persona incoming, TimeSpan? away, PersonaSwitch cause)
    {
        if (cause == PersonaSwitch.Adopted)
        {
            return PersonaArrival.Quiet;
        }

        if (_introduced.Add(incoming.Id))
        {
            Remember();
            return PersonaArrival.Introduction;
        }

        return cause == PersonaSwitch.Selected && away is { } gap && gap >= GapAfter
            ? PersonaArrival.Gap
            : PersonaArrival.Quiet;
    }

    private List<ConversationMessage> TranscriptFor(string id)
    {
        if (!_transcripts.TryGetValue(id, out var transcript))
        {
            transcript = [];
            _transcripts[id] = transcript;
        }

        return transcript;
    }
}

/// <summary>
/// Where introductions are kept between runs (docs/plans/change-requests.md item 7).
/// <para>
/// A port rather than a store, because <see cref="PersonaHost"/> is Core and Core is the thing
/// the replay harness runs with nothing behind it. A host handed no memory behaves exactly as
/// it did before this existed, which is what keeps every test that is not about persistence
/// from having to grow a file.
/// </para>
/// </summary>
public interface IIntroductionMemory
{
    /// <summary>The cores already introduced, or empty when nothing has been written yet.</summary>
    IReadOnlyCollection<string> Load();

    /// <summary>Records the set as it now stands. Replaces rather than merges.</summary>
    void Save(IReadOnlyCollection<string> introduced);
}
