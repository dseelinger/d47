using System.Globalization;

namespace D47.Core.Journal;

/// <summary>
/// A point on a body's surface, and the distance between two of them.
/// </summary>
/// <param name="Latitude">Degrees.</param>
/// <param name="Longitude">Degrees.</param>
/// <param name="RadiusMetres">
/// The body's own radius, from <c>Status.json</c>. Carried on the fix rather than looked up, because
/// a distance computed against the wrong body's radius is wrong by whatever ratio those bodies
/// differ by — and nothing about the number would show it.
/// </param>
public readonly record struct SurfaceFix(double Latitude, double Longitude, double RadiusMetres)
{
    /// <summary>
    /// Great-circle distance in metres, which is the only correct answer on a sphere — a Commander
    /// driving 400 metres around a small moon has covered noticeably more angle than the same drive
    /// on a large one, and a flat approximation gets steadily worse the further apart the two points
    /// are.
    /// <para>
    /// Uses the haversine form rather than the spherical law of cosines: the two agree everywhere
    /// except at short range, and short range is the entire use of this function.
    /// </para>
    /// </summary>
    public double MetresTo(SurfaceFix other)
    {
        var radius = RadiusMetres > 0 ? RadiusMetres : other.RadiusMetres;

        if (radius <= 0)
        {
            return 0;
        }

        var lat1 = Radians(Latitude);
        var lat2 = Radians(other.Latitude);
        var dLat = lat2 - lat1;
        var dLon = Radians(other.Longitude - Longitude);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return 2 * radius * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    private static double Radians(double degrees) => degrees * Math.PI / 180;
}

/// <summary>
/// One genus being sampled on one body.
/// <para>
/// A run is <c>Log</c>, <c>Sample</c>, <c>Sample</c>, <c>Analyse</c> — 94 of the 101 runs measured —
/// so three specimens and a fourth event that banks them. The count a Commander wants is of
/// specimens, which makes <c>Analyse</c> a completion rather than a fourth sample.
/// </para>
/// </summary>
public sealed record GenusProgress(string Genus)
{
    public string? Species { get; init; }

    /// <summary>Specimens taken: 1, 2 or 3. <c>Analyse</c> does not add one.</summary>
    public int Taken { get; init; }

    public bool Complete { get; init; }

    /// <summary>
    /// Where the last specimen was taken, if d47 had a position at that moment.
    /// <para>
    /// <b><c>ScanOrganic</c> carries no position at all</b> — measured across all 632 events, which
    /// carry the genus, the species, the variant and the body id and nothing spatial. So d47 stamps
    /// one from <c>Status.json</c> as the event lands, and the figure is therefore only as good as
    /// how closely those two writes track each other. Null where no position was being reported,
    /// which is honest rather than convenient.
    /// </para>
    /// </summary>
    public SurfaceFix? LastAt { get; init; }

    /// <summary>
    /// How far the Commander travelled between the previous specimen and this one, in metres.
    /// <para>
    /// Recorded by the fold rather than computed afterwards, because by the time anything else looks
    /// at this record <see cref="LastAt"/> has already moved to the new specimen and the gap is
    /// gone. Null on the first specimen of a run, and wherever d47 had no position for one end.
    /// </para>
    /// </summary>
    public double? LastGapMetres { get; init; }

    public DateTimeOffset SeenAt { get; init; }

    public static int Required => 3;

    public int Remaining => Math.Max(0, Required - Taken);
}

/// <summary>
/// What has been sampled on one body, keyed the way Elite keys it.
/// </summary>
/// <param name="SystemAddress">
/// With <paramref name="BodyId"/>, the body's identity. <b><c>ScanOrganic</c> writes
/// <c>"Body": 37</c> — an id, not a name</b>, so this is a numeric key and any attempt to match it
/// against a body name has to go through the journal's own naming elsewhere.
/// </param>
public sealed record BodySampling(long SystemAddress, int BodyId)
{
    public IReadOnlyDictionary<string, GenusProgress> Genera { get; init; } =
        new Dictionary<string, GenusProgress>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<GenusProgress> InProgress =>
        [.. Genera.Values.Where(genus => !genus.Complete).OrderByDescending(genus => genus.SeenAt)];

    public IReadOnlyList<GenusProgress> Completed =>
        [.. Genera.Values.Where(genus => genus.Complete).OrderByDescending(genus => genus.SeenAt)];
}

/// <summary>
/// Organic sampling, per body and per genus (list.md Phase 18, "Exobiology sampling").
/// <para>
/// <b>The spacing rule is the feature, and d47 does not ship a table of it.</b> The required distance
/// between specimens is published by the game in the species' own Codex entry, and every
/// machine-readable rendering of it outside the game is a community wiki — which the exobiology plan
/// already ruled on: what web search finds stays a sentence, and the same sentence hand-copied into
/// a shipped table is d47 laundering somebody's forum post into its own voice. So d47 reports the
/// two things it can measure exactly — <b>how far you have moved since the last specimen</b>, and
/// <b>how many you have taken</b> — and leaves the threshold to the Codex, which is the authority.
/// </para>
/// <para>
/// <b>And it learns.</b> A specimen that Elite accepted is evidence that the distance travelled was
/// enough, so the smallest accepted gap for a genus is an upper bound on that genus's requirement,
/// measured from the Commander's own play and carried with its sample size. That needs no source at
/// all and gets better the more they sample — which is the honest shape of a number nobody will
/// licence.
/// </para>
/// </summary>
public sealed record OrganicSampling
{
    public static readonly OrganicSampling Empty = new();

    private IReadOnlyDictionary<(long System, int Body), BodySampling> Bodies { get; init; } =
        new Dictionary<(long, int), BodySampling>();

    /// <summary>
    /// The smallest gap at which a specimen of each genus was accepted, and how many gaps that rests
    /// on. An upper bound on the requirement, never presented as the requirement itself.
    /// </summary>
    private IReadOnlyDictionary<string, (double Metres, int Samples)> Observed { get; init; } =
        new Dictionary<string, (double, int)>(StringComparer.OrdinalIgnoreCase);

    public bool IsKnown => Bodies.Count > 0;

    public IReadOnlyList<BodySampling> All => [.. Bodies.Values];

    /// <summary>
    /// Puts back one genus on one body, for <see cref="SamplingStore"/> to restore a session.
    /// <para>
    /// Restoring rather than replaying: the events are in yesterday's journal, which the spine no
    /// longer tails. The learned spacing is deliberately <em>not</em> restored — it is an inference
    /// over gaps, and rebuilding it from stored end points would compound one estimate on another
    /// with nothing left to say which samples it rested on.
    /// </para>
    /// </summary>
    public OrganicSampling Restore(long systemAddress, int bodyId, GenusProgress genus)
    {
        var key = (systemAddress, bodyId);
        var body = Bodies.GetValueOrDefault(key) ?? new BodySampling(systemAddress, bodyId);

        var genera = new Dictionary<string, GenusProgress>(body.Genera, StringComparer.OrdinalIgnoreCase)
        {
            [genus.Genus] = genus,
        };

        var bodies = new Dictionary<(long, int), BodySampling>(Bodies)
        {
            [key] = body with { Genera = genera },
        };

        return this with { Bodies = bodies };
    }

    public BodySampling? On(long systemAddress, int bodyId) =>
        Bodies.GetValueOrDefault((systemAddress, bodyId));

    /// <summary>The body most recently sampled, which is almost always the one being asked about.</summary>
    public BodySampling? MostRecent =>
        Bodies.Values
            .OrderByDescending(body => body.Genera.Values.Max(genus => genus.SeenAt))
            .FirstOrDefault();

    /// <summary>
    /// The closest d47 has seen a specimen of this genus accepted, with its sample size. Null until
    /// two specimens of that genus have been taken with a position on both.
    /// </summary>
    public (double Metres, int Samples)? ClosestAccepted(string? genus) =>
        genus is not null && Observed.TryGetValue(genus, out var seen) ? seen : null;

    /// <param name="at">
    /// Where the Commander was when the event landed, from <c>Status.json</c>. Null where no position
    /// was being reported — the sample still counts, it simply carries no distance.
    /// </param>
    public OrganicSampling Apply(JournalEvent journalEvent, SurfaceFix? at = null)
    {
        if (journalEvent.Kind != "ScanOrganic"
            || journalEvent.Named("Genus") is not { } genusName
            || journalEvent.Long("SystemAddress") is not { } systemAddress
            || journalEvent.Int("Body") is not { } bodyId)
        {
            return this;
        }

        var scanType = journalEvent.String("ScanType");
        var key = (systemAddress, bodyId);
        var body = Bodies.GetValueOrDefault(key) ?? new BodySampling(systemAddress, bodyId);
        var known = body.Genera.GetValueOrDefault(genusName);

        // Analyse banks the run rather than adding a fourth specimen. Counting it would report
        // "4 of 3" on every completed genus.
        var isSpecimen = scanType is "Log" or "Sample";

        // A fresh Log after a completed run is a second run of the same genus on the same body,
        // which the corpus does contain — so the count restarts rather than climbing past three.
        var restarting = scanType == "Log" && known is null or { Complete: true };

        var taken = isSpecimen ? (restarting ? 1 : (known?.Taken ?? 0) + 1) : known?.Taken ?? 0;

        var observed = Observed;
        double? gap = null;

        // The learning step. A specimen Elite accepted proves the gap was sufficient, so the
        // smallest accepted gap is an upper bound — recorded only where both ends had a position
        // and where this is a continuation rather than the start of a new run.
        if (isSpecimen && !restarting && at is { } here && known?.LastAt is { } previous)
        {
            var metres = previous.MetresTo(here);

            if (metres > 0)
            {
                gap = metres;

                var updated = new Dictionary<string, (double Metres, int Samples)>(
                    observed, StringComparer.OrdinalIgnoreCase);

                updated[genusName] = observed.TryGetValue(genusName, out var seen)
                    ? (Math.Min(seen.Metres, metres), seen.Samples + 1)
                    : (metres, 1);

                observed = updated;
            }
        }

        var progress = new GenusProgress(genusName)
        {
            Species = journalEvent.Named("Species") ?? known?.Species,
            Taken = taken,
            Complete = scanType == "Analyse" || taken >= GenusProgress.Required,
            LastAt = isSpecimen ? at ?? known?.LastAt : known?.LastAt,
            LastGapMetres = isSpecimen ? gap : known?.LastGapMetres,
            SeenAt = journalEvent.Timestamp,
        };

        var genera = new Dictionary<string, GenusProgress>(body.Genera, StringComparer.OrdinalIgnoreCase)
        {
            [genusName] = progress,
        };

        var bodies = new Dictionary<(long, int), BodySampling>(Bodies)
        {
            [key] = body with { Genera = genera },
        };

        return this with { Bodies = bodies, Observed = observed };
    }

    /// <summary>How far the Commander is now from their last specimen of a genus, in metres.</summary>
    public double? MetresSinceLast(GenusProgress genus, SurfaceFix? now) =>
        genus.LastAt is { } last && now is { } here ? last.MetresTo(here) : null;

    public static string Metres(double value) =>
        value >= 1000
            ? (value / 1000).ToString("0.0", CultureInfo.InvariantCulture) + " kilometres"
            : value.ToString("0", CultureInfo.InvariantCulture) + " metres";
}
