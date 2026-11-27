using D47.Core.Knowledge;

namespace D47.Core.Journal;

/// <summary>The kinds of item in a <see cref="SystemWorthAnswer"/>, in the order they are listed.</summary>
public enum WorthKind
{
    /// <summary>A notable stellar phenomenon, or a body with Guardian or Thargoid signals.</summary>
    Unusual,

    Biology,

    Mapping,

    Geology,
}

/// <summary>One thing still worth doing in a system.</summary>
/// <param name="BodyName">The body, or the signal's name for a notable stellar phenomenon.</param>
/// <param name="Names">
/// Biology: the genera not yet sampled, or the genera its conditions predict where no surface scan named any.
/// Geology: the likely features not in the codex. Unusual: the signal types.
/// </param>
/// <param name="Low">Biology: the low end of the value range, where genera are named.</param>
/// <param name="High">Biology: the high end of the value range. Mapping: the value if mapped efficiently.</param>
/// <param name="FirstMapping">Mapping: the body's scan says no one has mapped it.</param>
public sealed record WorthItem(
    WorthKind Kind,
    string BodyName,
    int? BodyId,
    IReadOnlyList<string> Names,
    long? Low = null,
    long? High = null,
    bool FirstMapping = false);

/// <param name="IsKnown">Whether any body of the system has a scan or a signal count on record.</param>
/// <param name="FirstDiscoveries">Scanned stars and planets whose scan says no one discovered them before.</param>
public sealed record SystemWorthAnswer(
    long SystemAddress,
    bool IsKnown,
    IReadOnlyList<WorthItem> Items,
    int FirstDiscoveries);

/// <summary>What is still worth doing in one system, from state already folded. Does no I/O.</summary>
public static class SystemWorth
{
    /// <param name="position">Where the system is, for the region; null gives no possible species and no regional codex.</param>
    /// <param name="cartography">Where planets are priced and own maps are kept; null lists no mapping items.</param>
    /// <param name="mappingThreshold">The least value, in credits, a planet is listed for mapping at.</param>
    /// <param name="signals">The FSS signals scanned, from any system.</param>
    public static SystemWorthAnswer For(
        CommanderGameState state,
        long systemAddress,
        StarPosition? position,
        CartographyLedger? cartography,
        long mappingThreshold,
        IReadOnlyList<SignalSighting> signals)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(signals);

        var scans = state.Scans.InSystem(systemAddress).OrderBy(scan => scan.BodyId).ToList();
        var bodies = state.Bodies.All
            .Where(body => body.SystemAddress == systemAddress)
            .OrderBy(body => body.BodyId)
            .ToList();
        var region = position is { } starPos ? GalacticRegions.Find(starPos) : null;

        var items = new List<WorthItem>();
        items.AddRange(Unusual(systemAddress, bodies, signals));
        items.AddRange(Biology(state, systemAddress, region).OrderByDescending(item => item.High ?? -1));
        items.AddRange(Mapping(state, systemAddress, scans, cartography, mappingThreshold).OrderByDescending(item => item.High));
        items.AddRange(Geology(state, systemAddress, scans, position));

        var known = scans.Count > 0 || bodies.Count > 0 || state.Biology.InSystem(systemAddress).Count > 0;
        var firsts = scans.Count(scan => scan.WasDiscovered == false && (scan.StarType is not null || scan.PlanetClass is not null));

        return new SystemWorthAnswer(systemAddress, known, items, firsts);
    }

    private static readonly string[] UnusualBodySignals = ["Guardian", "Thargoid"];

    private static IEnumerable<WorthItem> Unusual(
        long systemAddress, IReadOnlyList<BodyBiology> bodies, IReadOnlyList<SignalSighting> signals)
    {
        foreach (var sighting in signals
            .Where(sighting => sighting.SystemAddress == systemAddress
                && (sighting.Kind == SignalKind.NotableStellarPhenomena
                    || string.Equals(sighting.SignalType, "Codex", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(sighting => sighting.LastSeen))
        {
            yield return new WorthItem(WorthKind.Unusual, sighting.SignalName, null, [SignalKind.NotableStellarPhenomena.Name]);
        }

        foreach (var body in bodies)
        {
            var kinds = UnusualBodySignals
                .Where(kind => body.Signals.Any(signal => signal.Count > 0 && signal.Type.Contains(kind, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (kinds.Count > 0)
            {
                yield return new WorthItem(WorthKind.Unusual, body.BodyName, body.BodyId, kinds);
            }
        }
    }

    private static IEnumerable<WorthItem> Biology(CommanderGameState state, long systemAddress, string? region)
    {
        foreach (var body in BiologyInSystem.For(state, systemAddress, region, null, null).Bodies)
        {
            if (body.BiologicalCount == 0 && body.Genera.Count == 0)
            {
                continue;
            }

            var left = body.Genera.Where(genus => genus.State != GenusState.Done).Select(genus => genus.Genus).ToList();

            if (body.Genera.Count > 0 && left.Count == 0)
            {
                continue;
            }

            var scan = state.Scans.For(systemAddress, body.BodyId);

            if (scan is { Landable: false })
            {
                continue;
            }

            var estimate = scan is null
                ? null
                : BiologyPotential.For(scan, Math.Max(body.BiologicalCount - body.Done, left.Count), left, region);

            yield return new WorthItem(
                WorthKind.Biology,
                body.BodyName,
                body.BodyId,
                left.Count > 0 ? left : estimate?.Genera ?? [],
                estimate?.Low,
                estimate is null ? null : estimate.High ?? estimate.BestCase);
        }
    }

    private static IEnumerable<WorthItem> Mapping(
        CommanderGameState state,
        long systemAddress,
        IReadOnlyList<BodyScan> scans,
        CartographyLedger? cartography,
        long threshold)
    {
        if (cartography is null || state.Fitted(ShipLoadout.SurfaceScanner, "Scan", null) is false)
        {
            yield break;
        }

        foreach (var scan in scans)
        {
            if (scan.BodyId is not { } bodyId
                || scan.PlanetClass is null
                || cartography.IfMapped(systemAddress, bodyId) is not { } value
                || value < threshold
                || cartography.Mapped(systemAddress, bodyId, state.Identity.FrontierId)
                || state.Bodies.Named(scan.BodyName) is { Source: BodySignalSource.SurfaceScan })
            {
                continue;
            }

            yield return new WorthItem(WorthKind.Mapping, scan.BodyName, bodyId, [], High: value, FirstMapping: scan.WasMapped == false);
        }
    }

    private static IEnumerable<WorthItem> Geology(
        CommanderGameState state, long systemAddress, IReadOnlyList<BodyScan> scans, StarPosition? position)
    {
        foreach (var scan in scans)
        {
            if (scan.BodyId is { } bodyId
                && GeologyCatalogue.NotInCodex(state, systemAddress, bodyId, position) is { Count: > 0 } missing)
            {
                yield return new WorthItem(WorthKind.Geology, scan.BodyName, bodyId, [.. missing.Select(feature => feature.Name)]);
            }
        }
    }
}
