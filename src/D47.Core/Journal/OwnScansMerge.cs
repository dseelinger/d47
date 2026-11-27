using D47.Core.Knowledge;

namespace D47.Core.Journal;

/// <summary>Joins the Commander's own record of a system with the service's, body by body.</summary>
public static class OwnScansMerge
{
    /// <summary>Spansh's record with the Commander's own scans joined in; null only when both are null. A record for another system is returned unchanged.</summary>
    public static StarSystemProfile? Into(StarSystemProfile? record, OwnSystem? own)
    {
        if (own is null)
        {
            return record;
        }

        if (record is null)
        {
            return FromOwn(own);
        }

        if (record.SystemAddress != own.SystemAddress)
        {
            return record;
        }

        var bodies = Bodies(record.Bodies, own);

        return record with
        {
            BodyCount = Larger(record.BodyCount, own.BodyCount),
            Bodies = bodies,
            Barycentres = Barycentres(record.Barycentres, own.Barycentres, bodies),
        };
    }

    private static StarSystemProfile FromOwn(OwnSystem own) => new()
    {
        Name = own.Name ?? string.Empty,
        SystemAddress = own.SystemAddress,
        Position = own.Position,
        BodyCount = own.BodyCount,
        Bodies = own.Bodies,
        Barycentres = own.Barycentres,
        ReportedAt = own.LatestAt,
    };

    private static int? Larger(int? first, int? second) =>
        first is { } a && second is { } b ? Math.Max(a, b) : first ?? second;

    private static List<BodyProfile> Bodies(IReadOnlyList<BodyProfile> reported, OwnSystem own)
    {
        var merged = reported.ToDictionary(body => body.BodyId);

        foreach (var scanned in own.Bodies)
        {
            merged[scanned.BodyId] = merged.TryGetValue(scanned.BodyId, out var spansh)
                ? Body(spansh, scanned, own.Own.GetValueOrDefault(scanned.BodyId))
                : scanned;
        }

        return [.. merged.Values.OrderBy(body => body.BodyId)];
    }

    private static BodyProfile Body(BodyProfile spansh, BodyProfile scanned, OwnBody? record)
    {
        var scannedAt = record?.ScannedAt ?? scanned.ReportedAt;
        var ownWins = Later(scannedAt, spansh.ReportedAt);
        var (winner, loser) = ownWins ? (scanned, spansh) : (spansh, scanned);
        var family = winner.Parents.Count > 0 || loser.Parents.Count == 0 ? winner : loser;

        var body = winner with
        {
            BodyAddress = winner.BodyAddress ?? loser.BodyAddress,
            SubType = winner.SubType ?? loser.SubType,
            ParentId = family.ParentId,
            Parents = family.Parents,
            SpectralClass = winner.SpectralClass ?? loser.SpectralClass,
            Luminosity = winner.Luminosity ?? loser.Luminosity,
            IsMainStar = spansh.IsMainStar,
            SolarMasses = winner.SolarMasses ?? loser.SolarMasses,
            SolarRadius = winner.SolarRadius ?? loser.SolarRadius,
            EarthMasses = winner.EarthMasses ?? loser.EarthMasses,
            Radius = winner.Radius ?? loser.Radius,
            Gravity = winner.Gravity ?? loser.Gravity,
            SurfaceTemperature = winner.SurfaceTemperature ?? loser.SurfaceTemperature,
            SurfacePressure = winner.SurfacePressure ?? loser.SurfacePressure,
            Volcanism = winner.Volcanism ?? loser.Volcanism,
            Atmosphere = winner.Atmosphere ?? loser.Atmosphere,
            TerraformingState = winner.TerraformingState ?? loser.TerraformingState,
            IsLandable = winner.IsLandable || loser.IsLandable,
            ReserveLevel = winner.ReserveLevel ?? loser.ReserveLevel,
            Rings = winner.Rings.Count > 0 ? winner.Rings : loser.Rings,
            Belts = winner.Belts.Count > 0 ? winner.Belts : loser.Belts,
            Materials = winner.Materials.Count > 0 ? winner.Materials : loser.Materials,
            AtmosphereComposition = winner.AtmosphereComposition.Count > 0 ? winner.AtmosphereComposition : loser.AtmosphereComposition,
            SolidComposition = winner.SolidComposition.Count > 0 ? winner.SolidComposition : loser.SolidComposition,
            RotationalPeriod = winner.RotationalPeriod ?? loser.RotationalPeriod,
            TidallyLocked = winner.TidallyLocked ?? loser.TidallyLocked,
            AxialTilt = winner.AxialTilt ?? loser.AxialTilt,
            Age = winner.Age ?? loser.Age,
            AbsoluteMagnitude = winner.AbsoluteMagnitude ?? loser.AbsoluteMagnitude,
            ReportedAt = winner.ReportedAt ?? loser.ReportedAt,
        };

        body = WithDistance(body, Pick(winner, loser, source => source.DistanceToArrivalAt, source => source.DistanceToArrival is null));
        body = WithOrbit(body, Pick(winner, loser, source => source.MeanAnomalyAt, HasNoOrbit));

        return WithSignals(body, spansh, scanned, record);
    }

    /// <summary>The source whose stamp is later, a null stamp counting as older; with neither stamped, the winner unless it holds nothing.</summary>
    private static BodyProfile Pick(BodyProfile winner, BodyProfile loser, Func<BodyProfile, DateTimeOffset?> stamp, Func<BodyProfile, bool> empty)
    {
        if (stamp(winner) is null && stamp(loser) is null)
        {
            return empty(winner) ? loser : winner;
        }

        return Later(stamp(loser), stamp(winner)) ? loser : winner;
    }

    private static bool Later(DateTimeOffset? first, DateTimeOffset? second) =>
        first is { } a && (second is not { } b || a > b);

    private static bool HasNoOrbit(BodyProfile body) =>
        body.MeanAnomaly is null && body.SemiMajorAxis is null && body.OrbitalEccentricity is null && body.OrbitalInclination is null
        && body.ArgOfPeriapsis is null && body.AscendingNode is null && body.OrbitalPeriod is null;

    private static BodyProfile WithDistance(BodyProfile body, BodyProfile source) => body with
    {
        DistanceToArrival = source.DistanceToArrival,
        DistanceToArrivalAt = source.DistanceToArrivalAt,
    };

    private static BodyProfile WithOrbit(BodyProfile body, BodyProfile source) => body with
    {
        MeanAnomaly = source.MeanAnomaly,
        MeanAnomalyAt = source.MeanAnomalyAt,
        SemiMajorAxis = source.SemiMajorAxis,
        OrbitalEccentricity = source.OrbitalEccentricity,
        OrbitalInclination = source.OrbitalInclination,
        ArgOfPeriapsis = source.ArgOfPeriapsis,
        AscendingNode = source.AscendingNode,
        OrbitalPeriod = source.OrbitalPeriod,
    };

    /// <summary>
    /// The Commander's surface-scan counts and genera replace Spansh's; their FSS counts fill only a body Spansh gives no
    /// signals for; otherwise Spansh's stand.
    /// </summary>
    private static BodyProfile WithSignals(BodyProfile body, BodyProfile spansh, BodyProfile scanned, OwnBody? record)
    {
        if (record is { SignalsAt: not null, SignalsFromSurfaceScan: true })
        {
            return body with { Signals = scanned.Signals, SignalsSeen = record.SignalsAt, Genera = scanned.Genera };
        }

        if (record is { SignalsAt: not null } && spansh.Signals.Count == 0)
        {
            return body with { Signals = scanned.Signals, SignalsSeen = record.SignalsAt, Genera = spansh.Genera };
        }

        return body with { Signals = spansh.Signals, SignalsSeen = spansh.SignalsSeen, Genera = spansh.Genera };
    }

    private static List<BarycentreProfile> Barycentres(
        IReadOnlyList<BarycentreProfile> reported,
        IReadOnlyList<BarycentreProfile> scanned,
        IReadOnlyList<BodyProfile> bodies)
    {
        var merged = reported.ToDictionary(centre => centre.BodyId);

        foreach (var own in scanned)
        {
            merged[own.BodyId] = merged.TryGetValue(own.BodyId, out var spansh) ? Barycentre(spansh, own) : own;
        }

        var chains = bodies.Select(body => body.Parents).ToList();

        return
        [
            .. merged.Values
                .OrderBy(centre => centre.BodyId)
                .Select(centre => centre.Parents.Count > 0 ? centre : centre with { Parents = BodyParents.OfBarycentre(centre.BodyId, chains) }),
        ];
    }

    /// <summary>The later orbit as a whole, Spansh's when neither is stamped unless it holds none.</summary>
    private static BarycentreProfile Barycentre(BarycentreProfile spansh, BarycentreProfile own)
    {
        var chosen = spansh.MeanAnomalyAt is null && own.MeanAnomalyAt is null
            ? HasNoOrbit(spansh) ? own : spansh
            : Later(own.MeanAnomalyAt, spansh.MeanAnomalyAt) ? own : spansh;

        return chosen with { Parents = chosen.Parents.Count > 0 ? chosen.Parents : (ReferenceEquals(chosen, own) ? spansh : own).Parents };
    }

    private static bool HasNoOrbit(BarycentreProfile centre) =>
        centre.MeanAnomaly is null && centre.SemiMajorAxis is null && centre.OrbitalEccentricity is null && centre.OrbitalInclination is null
        && centre.ArgOfPeriapsis is null && centre.AscendingNode is null && centre.OrbitalPeriod is null;
}
