namespace D47.Core.Journal;

/// <summary>
/// One kind of signal a surface scan found, and how many.
/// <para>
/// The type is Elite's own — <c>Biological</c>, <c>Geological</c>, <c>Human</c>, <c>Other</c>, and
/// the named minerals — so d47 never invents a classification for something the game has already
/// labelled.
/// </para>
/// </summary>
public sealed record BodySignal(string Type, int Count);

/// <summary>
/// Which event told d47 about a body's signals. The FSS resolves a body before anyone flies to it;
/// the surface scan is later and fuller — it alone names genera — so a surface scan always outranks
/// an FSS row for the same body, and an FSS row must never overwrite one (#275).
/// </summary>
public enum BodySignalSource
{
    Fss,
    SurfaceScan,
}

/// <summary>
/// What Elite has said is on one body — from the FSS, a surface scan, or both (Phase 18, "Find the
/// exobiology"; widened to the FSS by #275).
/// <para>
/// <b>The game's own answer, and it outranks any prediction d47 could make.</b> <c>FSSBodySignals</c>
/// says how many signals a body carries the moment the scanner resolves it; <c>SAASignalsFound</c>,
/// once the body is mapped, says the same and adds which genera. Either way "is this worth landing
/// on" stops being an inference from <c>Scan</c> properties and becomes reading the journal back.
/// Prediction is for <em>before</em> either event; this is after, and after wins.
/// </para>
/// <para>
/// <b>It names the genus and never the species, and that ceiling is load-bearing.</b> All 792
/// <c>SAASignalsFound</c> events measured carry <c>Genuses</c>, and every row in it is a genus —
/// <em>Bacterium</em>, <em>Stratum</em>, <em>Brain Trees</em>. The species is what sets the price,
/// and the game does not say it until the sample is taken. So this can list what is down there and
/// must not quote a figure; the route plotter, which does know species, is the half that may.
/// </para>
/// </summary>
public sealed record BodyBiology(string BodyName)
{
    public long? SystemAddress { get; init; }

    public int? BodyId { get; init; }

    /// <summary>Every signal type on the body, as Elite labelled it.</summary>
    public IReadOnlyList<BodySignal> Signals { get; init; } = [];

    /// <summary>
    /// The genera present. The FSS never carries this, so empty means "no biology" only when
    /// <see cref="Source"/> is <see cref="BodySignalSource.SurfaceScan"/> — for an
    /// <see cref="BodySignalSource.Fss"/> row, empty means "not mapped yet", not "found nothing".
    /// </summary>
    public IReadOnlyList<string> Genera { get; init; } = [];

    /// <summary>Which event this row came from, needed because an empty <see cref="Genera"/> means
    /// two different things depending on the source.</summary>
    public BodySignalSource Source { get; init; }

    public DateTimeOffset SeenAt { get; init; }

    /// <summary>
    /// How many biological signals the scan counted. This is the number that matters and it is not
    /// the same as the genus count — a body can carry three signals of two genera.
    /// </summary>
    public int BiologicalCount =>
        Signals.Where(signal => signal.Type.Contains("Biological", StringComparison.OrdinalIgnoreCase))
            .Sum(signal => signal.Count);

    public bool HasBiology => BiologicalCount > 0 || Genera.Count > 0;
}

/// <summary>
/// Every body this Commander has surface-scanned, and what was on it.
/// <para>
/// Keyed on the body name rather than on <c>BodyID</c>, which is only unique within a system. The
/// name is what a Commander says out loud, and it is what the event carries on all 792 occurrences
/// measured.
/// </para>
/// </summary>
public sealed record BodySignals
{
    public static readonly BodySignals Empty = new();

    private IReadOnlyDictionary<string, BodyBiology> Bodies { get; init; } =
        new Dictionary<string, BodyBiology>(StringComparer.OrdinalIgnoreCase);

    public bool IsKnown => Bodies.Count > 0;

    /// <summary>Most recently scanned first, which is almost always the one being asked about.</summary>
    public IReadOnlyList<BodyBiology> All =>
        [.. Bodies.Values.OrderByDescending(body => body.SeenAt)];

    /// <summary>The most recent scan that found biology, which is the "is this one worth it" case.</summary>
    public BodyBiology? MostRecentWithBiology => All.FirstOrDefault(body => body.HasBiology);

    /// <summary>
    /// The body a name refers to. Exact first, then a suffix match, because a Commander says
    /// "7 b" for <c>Opet 7 b</c> when they are already in Opet.
    /// </summary>
    public BodyBiology? Named(string? spoken)
    {
        if (string.IsNullOrWhiteSpace(spoken))
        {
            return null;
        }

        var wanted = spoken.Trim();

        if (Bodies.TryGetValue(wanted, out var exact))
        {
            return exact;
        }

        var matches = All
            .Where(body => body.BodyName.EndsWith(" " + wanted, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    public BodySignals Apply(JournalEvent journalEvent)
    {
        var source = journalEvent.Kind switch
        {
            "SAASignalsFound" => BodySignalSource.SurfaceScan,
            "FSSBodySignals" => BodySignalSource.Fss,
            _ => (BodySignalSource?)null,
        };

        if (source is null || journalEvent.String("BodyName") is not { } bodyName)
        {
            return this;
        }

        // An FSS row is the earlier, thinner answer — it must never replace a surface scan
        // already on file for this body.
        if (source == BodySignalSource.Fss
            && Bodies.TryGetValue(bodyName, out var mapped)
            && mapped.Source == BodySignalSource.SurfaceScan)
        {
            return this;
        }

        var signals = journalEvent
            .Items("Signals")
            // Spoken rather than Named: a ring's mineral usually arrives with no localised name,
            // and Tritium is the one of the twelve that Elite writes in lower case — so without
            // this one list says "4 tritium" and "2 Alexandrite" about the same kind of thing.
            .Select(row => new BodySignal(row.Spoken("Type") ?? "an unnamed signal", row.Int("Count") ?? 0))
            .ToList();

        var genera = journalEvent
            .Items("Genuses")
            .Select(row => row.Named("Genus"))
            .OfType<string>()
            .ToList();

        var body = new BodyBiology(bodyName)
        {
            SystemAddress = journalEvent.Long("SystemAddress"),
            BodyId = journalEvent.Int("BodyID"),
            Signals = signals,
            Genera = genera,
            Source = source.Value,
            SeenAt = journalEvent.Timestamp,
        };

        var updated = new Dictionary<string, BodyBiology>(Bodies, StringComparer.OrdinalIgnoreCase)
        {
            [bodyName] = body,
        };

        return this with { Bodies = updated };
    }
}
