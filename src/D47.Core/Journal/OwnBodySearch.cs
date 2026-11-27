using D47.Core.Knowledge;

namespace D47.Core.Journal;

/// <summary>Brings a body search's rows up to the Commander's own scans, and drops a row a newer scan shows does not match.</summary>
public static class OwnBodySearch
{
    private static readonly string[] Terraformable = ["Terraformable", "Terraforming", "Terraformed"];

    /// <summary>
    /// Each body the Commander has scanned takes its fields by <see cref="OwnScansMerge"/>'s rule, then is dropped when
    /// they no longer pass <paramref name="query"/>; each drop lowers <see cref="BodySearchResult.Total"/> by one. A read
    /// that throws counts as no own scans. Reads journal files: call off the tick and the UI thread.
    /// </summary>
    public static BodySearchResult Apply(
        BodySearchResult result,
        BodyQuery query,
        Func<long, CancellationToken, OwnSystem?>? read,
        CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(query);

        if (read is null || result.Bodies.Count == 0)
        {
            return result;
        }

        var systems = new Dictionary<long, OwnSystem?>();
        var bodies = new List<BodySummary>(result.Bodies.Count);

        foreach (var body in result.Bodies)
        {
            if (body.SystemAddress is not { } address || body.BodyId is not { } bodyId)
            {
                bodies.Add(body);
                continue;
            }

            if (!systems.TryGetValue(address, out var own))
            {
                own = Read(read, address, cancellation);
                systems[address] = own;
            }

            if (own is null || !own.Bodies.Any(scanned => scanned.BodyId == bodyId))
            {
                bodies.Add(body);
                continue;
            }

            var merged = Merge(body, address, bodyId, own);

            if (Passes(merged, query))
            {
                bodies.Add(merged);
            }
        }

        var dropped = result.Bodies.Count - bodies.Count;

        return dropped == 0 && bodies.Zip(result.Bodies).All(pair => ReferenceEquals(pair.First, pair.Second))
            ? result
            : result with { Bodies = bodies, Total = Math.Max(0, result.Total - dropped) };
    }

    private static OwnSystem? Read(Func<long, CancellationToken, OwnSystem?> read, long address, CancellationToken cancellation)
    {
        try
        {
            return read(address, cancellation);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static BodySummary Merge(BodySummary body, long address, int bodyId, OwnSystem own)
    {
        var record = new StarSystemProfile
        {
            Name = body.SystemName,
            SystemAddress = address,
            Bodies = [Profile(body, bodyId)],
        };

        var merged = OwnScansMerge.Into(record, own)!.Bodies.First(one => one.BodyId == bodyId);

        return body with
        {
            Subtype = merged.SubType,
            DistanceToArrival = merged.DistanceToArrival,
            IsLandable = merged.IsLandable,
            TerraformingState = merged.TerraformingState,
            ReserveLevel = merged.ReserveLevel,
            Rings =
            [
                .. merged.Rings.Select(ring => body.Rings.FirstOrDefault(old => old.Name == ring.Name) is { } old
                    ? old with { Type = ring.Type }
                    : new RingSummary(ring.Name, ring.Type)),
            ],
            Materials = [.. merged.Materials.Select(material => (material.Key, material.Value))],
            Volcanism = merged.Volcanism,
            Atmosphere = merged.Atmosphere,
            Gravity = merged.Gravity,
            SurfaceTemperature = merged.SurfaceTemperature,
            IsTidallyLocked = merged.TidallyLocked ?? body.IsTidallyLocked,
            SpectralClass = merged.SpectralClass,
            Luminosity = merged.Luminosity,
            SolarMasses = merged.SolarMasses,
            SolarRadius = merged.SolarRadius,
            EarthMasses = merged.EarthMasses,
            Radius = merged.Radius,
            Age = merged.Age,
            SurfacePressure = merged.SurfacePressure,
            AtmosphereComposition = merged.AtmosphereComposition,
            SolidComposition = merged.SolidComposition,
            OrbitalPeriod = merged.OrbitalPeriod,
            SemiMajorAxis = merged.SemiMajorAxis,
            OrbitalEccentricity = merged.OrbitalEccentricity,
            OrbitalInclination = merged.OrbitalInclination,
            ArgOfPeriapsis = merged.ArgOfPeriapsis,
            AscendingNode = merged.AscendingNode,
            MeanAnomaly = merged.MeanAnomaly,
            RotationalPeriod = merged.RotationalPeriod,
            AxialTilt = merged.AxialTilt,
            ReportedAt = merged.ReportedAt,
        };
    }

    private static BodyProfile Profile(BodySummary body, int bodyId) => new()
    {
        BodyId = bodyId,
        Name = body.Name,
        Type = body.Type ?? "Planet",
        SubType = body.Subtype,
        DistanceToArrival = body.DistanceToArrival,
        DistanceToArrivalAt = body.ReportedAt,
        SpectralClass = body.SpectralClass,
        Luminosity = body.Luminosity,
        IsMainStar = body.IsMainStar,
        SolarMasses = body.SolarMasses,
        SolarRadius = body.SolarRadius,
        EarthMasses = body.EarthMasses,
        Radius = body.Radius,
        Gravity = body.Gravity,
        SurfaceTemperature = body.SurfaceTemperature,
        SurfacePressure = body.SurfacePressure,
        Volcanism = body.Volcanism,
        Atmosphere = body.Atmosphere,
        TerraformingState = body.TerraformingState,
        IsLandable = body.IsLandable,
        ReserveLevel = body.ReserveLevel,
        Rings = [.. body.Rings.Select(ring => new RingProfile(ring.Name, ring.Type, new Dictionary<string, int>()))],
        Materials = Shares(body.Materials),
        AtmosphereComposition = body.AtmosphereComposition,
        SolidComposition = body.SolidComposition,
        OrbitalPeriod = body.OrbitalPeriod,
        SemiMajorAxis = body.SemiMajorAxis,
        OrbitalEccentricity = body.OrbitalEccentricity,
        OrbitalInclination = body.OrbitalInclination,
        ArgOfPeriapsis = body.ArgOfPeriapsis,
        AscendingNode = body.AscendingNode,
        MeanAnomaly = body.MeanAnomaly,
        MeanAnomalyAt = body.MeanAnomaly is null ? null : body.ReportedAt,
        RotationalPeriod = body.RotationalPeriod,
        TidallyLocked = body.IsTidallyLocked,
        AxialTilt = body.AxialTilt,
        Age = body.Age,
        ReportedAt = body.ReportedAt,
    };

    private static Dictionary<string, double> Shares(IReadOnlyList<(string Name, double Share)> materials)
    {
        var shares = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var (name, share) in materials)
        {
            shares[name] = share;
        }

        return shares;
    }

    /// <summary>Whether the body still passes the filters its scan can answer; a field the scan leaves empty is not held against it.</summary>
    private static bool Passes(BodySummary body, BodyQuery query)
    {
        if (query.Subtype is { } subtype && body.Subtype is { } kind && !Same(kind, subtype))
        {
            return false;
        }

        if (query.LuminosityClasses.Count > 0
            && body.Luminosity is { } luminosity
            && !query.LuminosityClasses.Any(one => Same(one, luminosity)))
        {
            return false;
        }

        if (query.Landable is true && !body.IsLandable)
        {
            return false;
        }

        if (query.Terraformable is { } terraformable && body.TerraformingState is { } state
            && terraformable != Terraformable.Any(one => Same(one, state)))
        {
            return false;
        }

        if ((query.Volcanism is { } volcanism && body.Volcanism is { } has && !Same(has, volcanism))
            || (query.Atmosphere is { } atmosphere && body.Atmosphere is { } air && !Same(air, atmosphere)))
        {
            return false;
        }

        if (query.TidallyLocked is true && !body.IsTidallyLocked)
        {
            return false;
        }

        return Within(body.Gravity, query.GravityMin, query.GravityMax)
               && Within(body.SurfaceTemperature, query.TemperatureMin, query.TemperatureMax)
               && Within(body.DistanceToArrival, null, query.MaxArrivalDistance);
    }

    private static bool Same(string first, string second) => string.Equals(first, second, StringComparison.OrdinalIgnoreCase);

    private static bool Within(double? value, double? min, double? max) =>
        value is not { } v || ((min is not { } low || v >= low) && (max is not { } high || v <= high));
}
