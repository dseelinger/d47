using D47.Core.Knowledge;

namespace D47.Core.Journal;

public enum GenusState
{
    NotStarted,
    Sampling,
    Done,
    Abandoned,
}

public enum SpeciesSource
{
    /// <summary>The species of the Commander's own set.</summary>
    Sampled,

    /// <summary>Spansh or Canonn lists it on the body.</summary>
    Reported,

    /// <summary>The body's conditions admit it.</summary>
    Possible,
}

public sealed record SpeciesLine(string Species, SpeciesSource Source, long? EntryId, int? Count = null, long? Value = null);

/// <summary>One genus on one body; <paramref name="Taken"/> is the lost count when abandoned.</summary>
public sealed record GenusLine(string Genus, GenusState State, int Taken, IReadOnlyList<SpeciesLine> Species);

/// <param name="GeneraReported">True when the body is not mapped and its genera come from Spansh's report.</param>
/// <param name="ReportedValue">Spansh's total for the body's biology.</param>
public sealed record BodyLine(
    string BodyName,
    int BodyId,
    int BiologicalCount,
    bool Mapped,
    IReadOnlyList<GenusLine> Genera,
    bool GeneraReported = false,
    long? ReportedValue = null)
{
    public int Done => Genera.Count(genus => genus.State == GenusState.Done);
}

public sealed record SystemBiologyAnswer(long SystemAddress, IReadOnlyList<BodyLine> Bodies)
{
    public bool IsKnown => Bodies.Count > 0;
}

/// <summary>The biology on each body of one system and how far the Commander has sampled it. Does no I/O.</summary>
public static class BiologyInSystem
{
    /// <summary>
    /// Any system the Commander's records hold. <paramref name="region"/> is a <see cref="GalacticRegions.Find"/> name, and
    /// null gives no possible species; <paramref name="surveyed"/> is Spansh's answer and <paramref name="earlier"/> Canonn's.
    /// </summary>
    public static SystemBiologyAnswer For(
        CommanderGameState state,
        long systemAddress,
        string? region,
        SystemBiology? surveyed,
        IReadOnlyList<EarlierFind>? earlier)
    {
        ArgumentNullException.ThrowIfNull(state);

        var own = state.Biology.InSystem(systemAddress).ToList();
        var known = own.Select(body => body.BodyId).ToHashSet();
        var reported = surveyed?.Bodies
            .Where(row => !known.Contains(row.BodyId))
            .GroupBy(row => row.BodyId)
            .Select(group => group.First())
            .Select(row => new BiologyBody(systemAddress, row.BodyId, row.Name)
            {
                BiologicalCount = row.Species.Select(species => species.Genus).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            })
            ?? [];

        var bodies = own
            .Concat(reported)
            .OrderBy(body => body.BodyId)
            .Select(body => Line(body, state.Sampling.On(systemAddress, body.BodyId), region, surveyed, earlier))
            .ToList();

        return new SystemBiologyAnswer(systemAddress, bodies);
    }

    private static BodyLine Line(
        BiologyBody body,
        BodySampling? sampling,
        string? region,
        SystemBiology? surveyed,
        IReadOnlyList<EarlierFind>? earlier)
    {
        var names = new List<string>();

        foreach (var genus in body.Genera
            .Concat(sampling?.Genera.Keys ?? [])
            .Concat(sampling?.Lost.Keys ?? []))
        {
            if (!names.Contains(genus, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(genus);
            }
        }

        var row = surveyed?.Bodies.FirstOrDefault(row => row.BodyId == body.BodyId);
        var generaReported = !body.Mapped && row is not null && row.Species.Count > 0;

        if (generaReported)
        {
            foreach (var genus in row!.Species.Select(species => species.Genus))
            {
                if (!names.Contains(genus, StringComparer.OrdinalIgnoreCase))
                {
                    names.Add(genus);
                }
            }
        }

        var reported = Reported(body, surveyed, earlier);
        IReadOnlyList<ExobiologyEntry> possible = region is null ? [] : Possible(body, region);

        var genera = names
            .Select(genus => Genus(genus, sampling, reported, possible))
            .OrderBy(line => Rank(line.State))
            .ToList();

        return new BodyLine(body.BodyName, body.BodyId, body.BiologicalCount, body.Mapped, genera, generaReported, row?.LandmarkValue);
    }

    private static int Rank(GenusState state) => state switch
    {
        GenusState.Sampling => 0,
        GenusState.Abandoned => 1,
        GenusState.NotStarted => 2,
        _ => 3,
    };

    private static GenusLine Genus(
        string genus,
        BodySampling? sampling,
        IReadOnlyList<(string Genus, SpeciesLine Line)> reported,
        IReadOnlyList<ExobiologyEntry> possible)
    {
        var set = sampling?.Genera.GetValueOrDefault(genus);
        var lost = sampling?.Lost.GetValueOrDefault(genus);

        if (set is not null && lost is not null)
        {
            if (set.SeenAt >= lost.SeenAt)
            {
                lost = null;
            }
            else
            {
                set = null;
            }
        }

        var (state, taken, held) = set switch
        {
            { Complete: true } => (GenusState.Done, set.Taken, set),
            { Taken: > 0 } => (GenusState.Sampling, set.Taken, set),
            _ when lost is not null => (GenusState.Abandoned, lost.Taken, lost),
            _ => (GenusState.NotStarted, 0, (GenusProgress?)null),
        };

        return new GenusLine(genus, state, taken, Species(genus, held, reported, possible));
    }

    private static IReadOnlyList<SpeciesLine> Species(
        string genus,
        GenusProgress? held,
        IReadOnlyList<(string Genus, SpeciesLine Line)> reported,
        IReadOnlyList<ExobiologyEntry> possible)
    {
        if (CodexSpecies.Of(held?.Species) is { } sampled)
        {
            var entryId = held!.Variant is { } variant ? CodexVariants.Named(variant) : null;

            var spansh = reported.Select(row => row.Line).FirstOrDefault(line => Same(line.Species, sampled) && line.Value is not null);
            var value = spansh is not null
                ? spansh.Value
                : ExobiologyCatalogue.All.FirstOrDefault(entry => Same(entry.Species, sampled))?.Value;

            return [new SpeciesLine(sampled, SpeciesSource.Sampled, entryId, spansh?.Count, value)];
        }

        var here = reported
            .Where(row => Same(row.Genus, genus))
            .Select(row => row.Line)
            .ToList();

        if (here.Count > 0)
        {
            return here;
        }

        return
        [
            .. possible
                .Where(entry => Same(entry.Genus, genus))
                .Select(entry => entry.Species)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.Ordinal)
                .Select(species => new SpeciesLine(species, SpeciesSource.Possible, null)),
        ];
    }

    /// <summary>
    /// The species Spansh lists for the body's id and Canonn lists for its name, one line each, in name order.
    /// Where Canonn logged several variants of a species, the lowest EntryID is carried.
    /// </summary>
    private static IReadOnlyList<(string Genus, SpeciesLine Line)> Reported(
        BiologyBody body,
        SystemBiology? surveyed,
        IReadOnlyList<EarlierFind>? earlier)
    {
        var genusOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var entryOf = new Dictionary<string, long?>(StringComparer.OrdinalIgnoreCase);
        var figures = new Dictionary<string, (int Count, long Value)>(StringComparer.OrdinalIgnoreCase);

        foreach (var species in surveyed?.Bodies.Where(row => row.BodyId == body.BodyId).SelectMany(row => row.Species) ?? [])
        {
            if (CodexSpecies.Of(species.Name) is { } name)
            {
                genusOf.TryAdd(name, species.Genus);
                entryOf.TryAdd(name, null);
                figures.TryAdd(name, (species.Count, species.Value));
            }
        }

        foreach (var find in earlier?.Where(find => string.Equals(find.Body, body.BodyName, StringComparison.OrdinalIgnoreCase)) ?? [])
        {
            if (CodexSpecies.Of(find.Name) is not { } name || GenusOfSpecies(name) is not { } genus)
            {
                continue;
            }

            genusOf.TryAdd(name, genus);

            entryOf[name] = entryOf.GetValueOrDefault(name) is { } held ? Math.Min(held, find.EntryId) : find.EntryId;
        }

        return
        [
            .. genusOf.Keys
                .Order(StringComparer.Ordinal)
                .Select(name => (genusOf[name], new SpeciesLine(
                    name,
                    SpeciesSource.Reported,
                    entryOf[name],
                    figures.TryGetValue(name, out var figure) ? figure.Count : null,
                    figures.TryGetValue(name, out figure) ? figure.Value : null))),
        ];
    }

    private static string? GenusOfSpecies(string species) =>
        ExobiologyCatalogue.All.FirstOrDefault(entry => Same(entry.Species, species))?.Genus;

    private static IReadOnlyList<ExobiologyEntry> Possible(BiologyBody body, string region)
    {
        if (body.SurfaceGravity is not { } gravity || body.SurfaceTemperature is not { } temperature)
        {
            return [];
        }

        return ExobiologyCatalogue.Possible(
            new BodyConditions
            {
                PlanetClass = body.PlanetClass,
                Atmosphere = body.Atmosphere,
                Volcanism = body.Volcanism,
                Gravity = gravity / BiologyPotential.StandardGravity,
                Temperature = temperature,
                Pressure = (body.SurfacePressure ?? 0) / BiologyPotential.StandardAtmosphere,
            },
            region);
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
