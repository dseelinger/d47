using System.Globalization;

namespace D47.Core.Journal;

/// <summary>A point on a body's surface, and the distance between two of them.</summary>
/// <param name="Latitude">Degrees.</param>
/// <param name="Longitude">Degrees.</param>
/// <param name="RadiusMetres">The body's own radius, from <c>Status.json</c>.</param>
public readonly record struct SurfaceFix(double Latitude, double Longitude, double RadiusMetres)
{
    /// <summary>
    /// Great-circle distance in metres, which is the only correct answer on a sphere — a Commander
    /// driving 400 metres around a small moon has covered noticeably more angle than the same drive on
    /// a large one, and a flat approximation gets steadily worse the further apart the two points are.
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

/// <summary>One genus being sampled on one body.</summary>
public sealed record GenusProgress(string Genus)
{
    public string? Species { get; init; }

    /// <summary>Specimens taken: 1, 2 or 3.</summary>
    public int Taken { get; init; }

    public bool Complete { get; init; }

    /// <summary>Where the last specimen was taken, if d47 had a position at that moment.</summary>
    public SurfaceFix? LastAt { get; init; }

    /// <summary>Where each specimen of the set was taken, for the ones d47 had a position for.</summary>
    public IReadOnlyList<SurfaceFix> Specimens { get; init; } = [];

    /// <summary>How far the Commander travelled between the previous specimen and this one, in metres.</summary>
    public double? LastGapMetres { get; init; }

    public DateTimeOffset SeenAt { get; init; }

    public static int Required => 3;

    public int Remaining => Math.Max(0, Required - Taken);
}

/// <summary>What has been sampled on one body, keyed the way Elite keys it.</summary>
/// <param name="SystemAddress">With <paramref name="BodyId"/>, the body's identity.</param>
public sealed record BodySampling(long SystemAddress, int BodyId)
{
    public IReadOnlyDictionary<string, GenusProgress> Genera { get; init; } =
        new Dictionary<string, GenusProgress>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<GenusProgress> InProgress =>
        [.. Genera.Values.Where(genus => !genus.Complete).OrderByDescending(genus => genus.SeenAt)];

    public IReadOnlyList<GenusProgress> Completed =>
        [.. Genera.Values.Where(genus => genus.Complete).OrderByDescending(genus => genus.SeenAt)];
}

/// <summary>Sets lost when a specimen of a different species was taken, and when.</summary>
public sealed record AbandonedSets(DateTimeOffset At, IReadOnlyList<GenusProgress> Sets);

/// <summary>Organic sampling, per body and per genus (Phase 18, "Exobiology sampling").</summary>
public sealed record OrganicSampling
{
    public static readonly OrganicSampling Empty = new();

    private IReadOnlyDictionary<(long System, int Body), BodySampling> Bodies { get; init; } =
        new Dictionary<(long, int), BodySampling>();

    /// <summary>
    /// The smallest gap at which a specimen of each genus was accepted, and how many gaps that rests
    /// on.
    /// </summary>
    private IReadOnlyDictionary<string, (double Metres, int Samples)> Observed { get; init; } =
        new Dictionary<string, (double, int)>(StringComparer.OrdinalIgnoreCase);

    public bool IsKnown => Bodies.Count > 0;

    /// <summary>The sets the latest specimen of a different species abandoned, or null.</summary>
    public AbandonedSets? Abandoned { get; init; }

    public IReadOnlyList<BodySampling> All => [.. Bodies.Values];

    /// <summary>Puts back one genus on one body, for <see cref="SamplingStore"/> to restore a session.</summary>
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

    /// <summary>The closest d47 has seen a specimen of this genus accepted, with its sample size.</summary>
    public (double Metres, int Samples)? ClosestAccepted(string? genus) =>
        genus is not null && Observed.TryGetValue(genus, out var seen) ? seen : null;

    /// <summary>
    /// <param name="at"> Where the Commander was when the event landed, from <c>Status.json</c>.
    /// </summary>
    /// <param name="at">Where the Commander was when the event landed, from <c>Status.json</c>.</param>
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
        var species = journalEvent.Named("Species");
        var key = (systemAddress, bodyId);

        // Analyse banks the run rather than adding a fourth specimen.
        var isSpecimen = scanType is "Log" or "Sample";

        // The sampler holds one species: a specimen of another drops every open set, on any body.
        var sampling = isSpecimen && species is not null ? Abandon(species, journalEvent.Timestamp) : this;

        var body = sampling.Bodies.GetValueOrDefault(key) ?? new BodySampling(systemAddress, bodyId);
        var known = body.Genera.GetValueOrDefault(genusName);

        // A fresh Log after a completed run is a second run of the same genus on the same body, which the
        // corpus does contain — so the count restarts rather than climbing past three.
        var restarting = scanType == "Log" && known is null or { Complete: true };

        var taken = isSpecimen ? (restarting ? 1 : (known?.Taken ?? 0) + 1) : known?.Taken ?? 0;

        var observed = Observed;
        double? gap = null;

        // The learning step.
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
            Specimens = !isSpecimen ? known?.Specimens ?? []
                : at is not { } fix ? (restarting ? [] : known?.Specimens ?? [])
                : restarting ? [fix]
                : [.. known?.Specimens ?? [], fix],
            LastGapMetres = isSpecimen ? gap : known?.LastGapMetres,
            SeenAt = journalEvent.Timestamp,
        };

        var genera = new Dictionary<string, GenusProgress>(body.Genera, StringComparer.OrdinalIgnoreCase)
        {
            [genusName] = progress,
        };

        var bodies = new Dictionary<(long, int), BodySampling>(sampling.Bodies)
        {
            [key] = body with { Genera = genera },
        };

        return sampling with { Bodies = bodies, Observed = observed };
    }

    /// <summary>Drops every set of one or two specimens of a species other than <paramref name="species"/>.</summary>
    private OrganicSampling Abandon(string species, DateTimeOffset at)
    {
        var lost = new List<GenusProgress>();
        var bodies = new Dictionary<(long, int), BodySampling>(Bodies);

        foreach (var (key, body) in Bodies)
        {
            var open = body.Genera.Values
                .Where(genus => genus is { Complete: false, Taken: > 0 }
                    && genus.Species is { } held
                    && !string.Equals(held, species, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (open.Count == 0)
            {
                continue;
            }

            lost.AddRange(open);

            var genera = new Dictionary<string, GenusProgress>(body.Genera, StringComparer.OrdinalIgnoreCase);

            foreach (var genus in open)
            {
                genera.Remove(genus.Genus);
            }

            if (genera.Count == 0)
            {
                bodies.Remove(key);
            }
            else
            {
                bodies[key] = body with { Genera = genera };
            }
        }

        return lost.Count == 0
            ? this with { Abandoned = null }
            : this with { Bodies = bodies, Abandoned = new AbandonedSets(at, lost) };
    }

    /// <summary>How far the Commander is now from their last specimen of a genus, in metres.</summary>
    public double? MetresSinceLast(GenusProgress genus, SurfaceFix? now) =>
        genus.LastAt is { } last && now is { } here ? last.MetresTo(here) : null;

    public static string Metres(double value) =>
        value >= 1000
            ? (value / 1000).ToString("0.0", CultureInfo.InvariantCulture) + " kilometres"
            : value.ToString("0", CultureInfo.InvariantCulture) + " metres";
}
