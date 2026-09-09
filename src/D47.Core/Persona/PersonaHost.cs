using D47.Core.Configuration;
using D47.Core.Conversation;

namespace D47.Core.Persona;

/// <summary>Why a core is about to speak on being selected.</summary>
public enum PersonaArrival
{
    /// <summary>First time this session.</summary>
    Introduction,

    /// <summary>Selected again after a long time away.</summary>
    Gap,

    /// <summary>Aboard, and saying nothing.</summary>
    Quiet,
}

/// <summary>Why the core changed.</summary>
public enum PersonaSwitch
{
    /// <summary>The Commander picked this core: a panel row, a phrase, a gesture.</summary>
    Selected,

    /// <summary>They boarded a ship they had bound to this core.</summary>
    Ship,

    /// <summary>The binding for the ship they were already in when d47 started.</summary>
    Adopted,
}

/// <summary>What a returning core is reacting to.</summary>
/// <param name="Away">How long the core was not running.</param>
public sealed record PersonaGap(TimeSpan Away, string? TelemetryDelta);

/// <summary>Emitted when the Commander changes core.</summary>
public sealed record PersonaChanged(Persona? Previous, Persona Current, PersonaArrival Arrival, PersonaGap? Gap);

/// <summary>Which core is aboard, what it sounds like, and what it remembers.</summary>
public sealed class PersonaHost
{
    private readonly Dictionary<string, List<ConversationMessage>> _transcripts = new(StringComparer.Ordinal);

    /// <summary>
    /// Which cores have already introduced themselves, so a second selection gets a gap reaction rather
    /// than the introduction again.
    /// </summary>
    private readonly HashSet<string> _introduced;

    /// <summary>
    /// Where the set is kept between runs, or null for a host that forgets — the designer, the replay
    /// harness, and every test that is not about this.
    /// </summary>
    private readonly IIntroductionMemory? _memory;

    private string? _shipNameOverride;

    /// <summary>Whether the humor line rides the block (#243).</summary>
    private bool _humor;

    public PersonaHost(Persona? current = null, IIntroductionMemory? memory = null)
    {
        Current = current ?? PersonaCatalog.Resolve(null);
        _memory = memory;
        _introduced = new HashSet<string>(memory?.Load() ?? [], StringComparer.Ordinal);
    }

    /// <summary>The core aboard.</summary>
    public Persona Current { get; private set; }

    /// <summary>How long a core has to have been away before it remarks on the missing time.</summary>
    public static readonly TimeSpan GapAfter = TimeSpan.FromDays(30);

    /// <summary>The cores that have already introduced themselves, in catalog order.</summary>
    public IReadOnlyList<Persona> Introduced =>
        [.. PersonaCatalog.All.Where(p => _introduced.Contains(p.Id))];

    /// <summary>
    /// Forgets every introduction at once, so the next selection of any core is its authored intro
    /// again rather than a gap reaction.
    /// </summary>
    public void ForgetIntroductions()
    {
        _introduced.Clear();
        Remember();
    }

    /// <summary>Writes the set out, if this host was given somewhere to write it.</summary>
    private void Remember() => _memory?.Save([.. _introduced]);

    /// <summary>Raised after the switch, with everything the surface needs to speak about it.</summary>
    public event Action<PersonaChanged>? Changed;

    /// <summary>
    /// What the Commander calls the ship's AI: their own name for it, or the core's own (Phase 11,
    /// "Ship AI Naming").
    /// </summary>
    public string ShipName =>
        string.IsNullOrWhiteSpace(_shipNameOverride) ? Current.Name : _shipNameOverride.Trim();

    /// <summary>The current core's transcript.</summary>
    public List<ConversationMessage> Transcript => TranscriptFor(Current.Id);

    /// <summary>
    /// Drops every core's transcript, so the next turn with any of them starts from nothing (Phase 44:
    /// "The old transcript goes away, a new one is created.").
    /// </summary>
    public void ForgetTranscripts() => _transcripts.Clear();

    /// <summary>The persona block for the assembled prompt, or null when personality is off.</summary>
    public string? RenderBlock(bool personalityEnabled) =>
        personalityEnabled ? Current.RenderBlock(_shipNameOverride, _humor) : null;

    /// <summary>Applies the persona and ship-name settings.</summary>
    /// <param name="away">How long the incoming core has been off, for its gap reaction.</param>
    /// <param name="telemetryDelta">
    /// What changed aboard while it was away, already rendered.
    /// </param>
    /// <param name="cause">What kind of switch this is.</param>
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

    /// <summary>Which of the three arrivals this is, and the only place that decides it.</summary>
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

/// <summary>Where introductions are kept between runs (docs/plans/change-requests.md item 7).</summary>
public interface IIntroductionMemory
{
    /// <summary>The cores already introduced, or empty when nothing has been written yet.</summary>
    IReadOnlyCollection<string> Load();

    /// <summary>Records the set as it now stands.</summary>
    void Save(IReadOnlyCollection<string> introduced);
}
