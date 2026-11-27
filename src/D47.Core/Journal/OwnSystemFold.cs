using System.Globalization;
using System.Text.Json;
using D47.Core.Knowledge;

namespace D47.Core.Journal;

/// <summary>Folds one system's journal events into an <see cref="OwnSystem"/>, each scan in Spansh's units and words.</summary>
internal static class OwnSystemFold
{
    /// <summary>The solar radius Spansh divides by, in metres.</summary>
    private const double SolarRadius = 695_500_000;

    private const double AstronomicalUnit = 149_597_870_700;

    private const double SecondsPerDay = 86_400;

    private sealed class BodyState
    {
        public BodyProfile? Profile { get; set; }

        public DateTimeOffset ScannedAt { get; set; }

        /// <summary>Whether a scan other than a Basic one has been folded, which a later Basic scan does not replace.</summary>
        public bool ScannedFully { get; set; }

        public bool? WasDiscovered { get; set; }

        public bool? WasMapped { get; set; }

        public bool MappedByCommander { get; set; }

        public string? Name { get; set; }

        public Dictionary<string, int>? Signals { get; set; }

        public IReadOnlyList<string> Genera { get; set; } = [];

        public bool SignalsFromSurfaceScan { get; set; }

        public DateTimeOffset? SignalsAt { get; set; }
    }

    /// <summary>
    /// The system as its events say, latest event last; a line given twice counts once. Null for no events.
    /// </summary>
    public static OwnSystem? Fold(long systemAddress, IReadOnlyList<JournalEvent> events)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ordered = events.Where(journalEvent => seen.Add(journalEvent.Raw.GetRawText())).OrderBy(journalEvent => journalEvent.Timestamp).ToList();

        if (ordered.Count == 0)
        {
            return null;
        }

        string? name = null;
        StarPosition? position = null;
        int? bodyCount = null;
        var bodies = new Dictionary<int, BodyState>();
        var barycentres = new Dictionary<int, BarycentreProfile>();

        foreach (var journalEvent in ordered)
        {
            switch (journalEvent.Kind)
            {
                case "FSDJump" or "Location" or "CarrierJump":
                    name = journalEvent.String("StarSystem") ?? name;
                    position = StarPosition.Read(journalEvent.Raw) ?? position;
                    break;

                case "FSSDiscoveryScan":
                    name = journalEvent.String("SystemName") ?? name;
                    bodyCount = journalEvent.Int("BodyCount") ?? bodyCount;
                    break;

                case "FSSAllBodiesFound":
                    name = journalEvent.String("SystemName") ?? name;
                    break;

                case "Scan":
                    name = journalEvent.String("StarSystem") ?? name;

                    if (journalEvent.Int("BodyID") is { } scanned && Profile(journalEvent, scanned) is { } profile)
                    {
                        var state = State(bodies, scanned);
                        var basic = journalEvent.String("ScanType") == "Basic";

                        if (!basic || !state.ScannedFully)
                        {
                            state.Profile = profile;
                            state.ScannedAt = journalEvent.Timestamp;
                            state.Name = profile.Name;
                            state.ScannedFully |= !basic;
                        }

                        state.WasDiscovered = Sticky(state.WasDiscovered, Flag(journalEvent, "WasDiscovered"));
                        state.WasMapped = Sticky(state.WasMapped, Flag(journalEvent, "WasMapped"));
                    }

                    break;

                case "ScanBaryCentre":
                    if (journalEvent.Int("BodyID") is { } centre)
                    {
                        barycentres[centre] = Barycentre(journalEvent, centre);
                    }

                    break;

                case "FSSBodySignals" or "SAASignalsFound":
                    if (journalEvent.Int("BodyID") is { } signalled)
                    {
                        var state = State(bodies, signalled);
                        var surface = journalEvent.Kind == "SAASignalsFound";
                        state.Name ??= journalEvent.String("BodyName");

                        if (surface || !state.SignalsFromSurfaceScan)
                        {
                            state.Signals = Signals(journalEvent);
                            state.SignalsAt = journalEvent.Timestamp;
                            state.SignalsFromSurfaceScan = surface;
                            state.Genera = surface ? Genera(journalEvent) : state.Genera;
                        }
                    }

                    break;

                case "SAAScanComplete":
                    if (journalEvent.Int("BodyID") is { } mapped)
                    {
                        State(bodies, mapped).MappedByCommander = true;
                    }

                    break;
            }
        }

        var ringSignals = bodies.Values
            .Where(state => state.Profile is null && state.Signals is not null && state.Name is not null)
            .GroupBy(state => state.Name!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().Signals!, StringComparer.Ordinal);

        var scannedBodies = bodies
            .Where(entry => entry.Value.Profile is not null)
            .OrderBy(entry => entry.Key)
            .ToList();

        var profiles = scannedBodies.Select(entry => Finished(entry.Value, ringSignals)).ToList();
        var chains = profiles.Select(body => body.Parents).ToList();

        return new OwnSystem(systemAddress)
        {
            Name = name,
            Position = position,
            BodyCount = bodyCount,
            Bodies = profiles,
            Barycentres = [.. barycentres.OrderBy(entry => entry.Key).Select(entry => entry.Value with { Parents = BodyParents.OfBarycentre(entry.Key, chains) })],
            Own = scannedBodies.ToDictionary(
                entry => entry.Key,
                entry => new OwnBody(entry.Value.ScannedAt)
                {
                    WasDiscovered = entry.Value.WasDiscovered,
                    WasMapped = entry.Value.WasMapped,
                    MappedByCommander = entry.Value.MappedByCommander,
                    SignalsFromSurfaceScan = entry.Value.SignalsFromSurfaceScan,
                    SignalsAt = entry.Value.SignalsAt,
                }),
            LatestAt = ordered[^1].Timestamp,
        };
    }

    private static BodyState State(Dictionary<int, BodyState> bodies, int bodyId)
    {
        if (!bodies.TryGetValue(bodyId, out var state))
        {
            state = new BodyState();
            bodies[bodyId] = state;
        }

        return state;
    }

    private static BodyProfile Finished(BodyState state, Dictionary<string, Dictionary<string, int>> ringSignals)
    {
        var profile = state.Profile!;

        return profile with
        {
            Signals = state.Signals ?? profile.Signals,
            SignalsSeen = state.SignalsAt,
            Genera = state.Genera,
            Rings = [.. profile.Rings.Select(ring => ringSignals.TryGetValue(ring.Name, out var signals) ? ring with { Signals = signals } : ring)],
        };
    }

    /// <summary>The scan as a <see cref="BodyProfile"/>; null for a scan that is neither a star nor a planet.</summary>
    internal static BodyProfile? Profile(JournalEvent scan, int bodyId)
    {
        var starType = scan.String("StarType");
        var planetClass = scan.String("PlanetClass");

        if (scan.String("BodyName") is not { } bodyName || (starType is not { Length: > 0 } && planetClass is not { Length: > 0 }))
        {
            return null;
        }

        var star = starType is { Length: > 0 };
        var parents = Parents(scan);
        var rings = scan.Items("Rings").Where(ring => ring.String("Name") is not null).ToList();
        var at = scan.Timestamp;

        var body = new BodyProfile
        {
            BodyId = bodyId,
            Name = bodyName,
            Type = star ? "Star" : "Planet",
            DistanceToArrival = scan.Double("DistanceFromArrivalLS"),
            DistanceToArrivalAt = at,
            ParentId = BodyParents.Nearest(parents),
            Parents = parents,
            SurfaceTemperature = scan.Double("SurfaceTemperature"),
            Rings = [.. rings.Where(ring => !IsBelt(ring)).Select(ring => new RingProfile(ring.String("Name")!, RingType(ring), new Dictionary<string, int>())
            {
                InnerRadius = ring.Double("InnerRad"),
                OuterRadius = ring.Double("OuterRad"),
                Mass = ring.Double("MassMT"),
            })],
            Belts = [.. rings.Where(IsBelt).Select(ring => new BeltProfile(
                ring.String("Name")!,
                RingType(ring),
                ring.Double("MassMT"),
                ring.Double("InnerRad"),
                ring.Double("OuterRad")))],
            OrbitalPeriod = scan.Double("OrbitalPeriod") / SecondsPerDay,
            SemiMajorAxis = scan.Double("SemiMajorAxis") / AstronomicalUnit,
            OrbitalEccentricity = scan.Double("Eccentricity"),
            OrbitalInclination = scan.Double("OrbitalInclination"),
            ArgOfPeriapsis = scan.Double("Periapsis"),
            AscendingNode = scan.Double("AscendingNode"),
            MeanAnomaly = scan.Double("MeanAnomaly"),
            MeanAnomalyAt = scan.Double("MeanAnomaly") is null ? null : at,
            RotationalPeriod = scan.Double("RotationPeriod") / SecondsPerDay,
            TidallyLocked = scan.Raw.TryGetProperty("TidalLock", out _) ? scan.Bool("TidalLock") : null,
            AxialTilt = scan.Double("AxialTilt") * 180 / Math.PI,
            ReportedAt = at,
        };

        return star
            ? body with
            {
                SubType = CodexKinds.SubTypeOfStar(starType!),
                SpectralClass = scan.Int("Subclass") is { } subclass
                    ? starType!.Split('_')[0] + subclass.ToString(CultureInfo.InvariantCulture)
                    : null,
                Luminosity = scan.String("Luminosity"),
                IsMainStar = bodyId == 0,
                SolarMasses = scan.Double("StellarMass"),
                SolarRadius = scan.Double("Radius") / SolarRadius,
                Age = scan.Double("Age_MY"),
                AbsoluteMagnitude = scan.Double("AbsoluteMagnitude"),
            }
            : body with
            {
                SubType = CodexKinds.PlanetClasses.GetValueOrDefault(planetClass!, planetClass!),
                EarthMasses = scan.Double("MassEM"),
                Radius = scan.Double("Radius") / 1_000,
                Gravity = scan.Double("SurfaceGravity") / TargetAnswer.StandardGravity,
                SurfacePressure = scan.Double("SurfacePressure") / TargetAnswer.Atmosphere,
                Atmosphere = CodexKinds.AtmosphereOf(scan.String("Atmosphere"), scan.String("AtmosphereType")),
                Volcanism = Volcanism(scan.String("Volcanism")),
                TerraformingState = scan.String("TerraformState") is { } terraform
                    ? terraform.Length == 0 ? "Not terraformable" : terraform
                    : null,
                IsLandable = scan.Bool("Landable"),
                Materials = Materials(scan),
                SolidComposition = Composition(scan),
            };
    }

    private static BarycentreProfile Barycentre(JournalEvent journalEvent, int bodyId) => new()
    {
        BodyId = bodyId,
        SemiMajorAxis = journalEvent.Double("SemiMajorAxis") / AstronomicalUnit,
        OrbitalEccentricity = journalEvent.Double("Eccentricity"),
        OrbitalInclination = journalEvent.Double("OrbitalInclination"),
        ArgOfPeriapsis = journalEvent.Double("Periapsis"),
        AscendingNode = journalEvent.Double("AscendingNode"),
        OrbitalPeriod = journalEvent.Double("OrbitalPeriod") / SecondsPerDay,
        MeanAnomaly = journalEvent.Double("MeanAnomaly"),
        MeanAnomalyAt = journalEvent.Double("MeanAnomaly") is null ? null : journalEvent.Timestamp,
    };

    private static bool IsBelt(JsonElement ring) => ring.String("Name")!.EndsWith(" Belt", StringComparison.Ordinal);

    private static string? RingType(JsonElement ring) =>
        ring.String("RingClass") is { } ringClass ? CodexKinds.RingClasses.GetValueOrDefault(ringClass) : null;

    /// <summary>Spansh's volcanism words: "Minor Rocky Magma" for "minor rocky magma volcanism", "No volcanism" for none.</summary>
    private static string? Volcanism(string? volcanism)
    {
        if (volcanism is null)
        {
            return null;
        }

        var words = volcanism.Trim();

        if (words.EndsWith(" volcanism", StringComparison.OrdinalIgnoreCase))
        {
            words = words[..^" volcanism".Length];
        }

        if (words.Length == 0)
        {
            return "No volcanism";
        }

        return BodyCatalogue.Volcanism.FirstOrDefault(known => string.Equals(known, words, StringComparison.OrdinalIgnoreCase))
            ?? CultureInfo.InvariantCulture.TextInfo.ToTitleCase(words);
    }

    private static List<(string Kind, int BodyId)> Parents(JournalEvent scan) =>
    [
        .. scan.Items("Parents")
            .Where(parent => parent.ValueKind == JsonValueKind.Object)
            .SelectMany(parent => parent.EnumerateObject())
            .Where(entry => entry.Value.ValueKind == JsonValueKind.Number && entry.Value.TryGetInt32(out _))
            .Select(entry => (entry.Name, entry.Value.GetInt32())),
    ];

    private static Dictionary<string, double> Materials(JournalEvent scan)
    {
        var materials = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var material in scan.Items("Materials"))
        {
            if (material.String("Name") is { Length: > 0 } name && material.Double("Percent") is { } percent)
            {
                materials[char.ToUpperInvariant(name[0]) + name[1..]] = percent;
            }
        }

        return materials;
    }

    private static Dictionary<string, double> Composition(JournalEvent scan)
    {
        var composition = new Dictionary<string, double>(StringComparer.Ordinal);

        if (scan.Object("Composition") is { } parts)
        {
            foreach (var part in parts.EnumerateObject())
            {
                if (part.Value.ValueKind == JsonValueKind.Number && part.Value.TryGetDouble(out var fraction))
                {
                    composition[part.Name] = fraction * 100;
                }
            }
        }

        return composition;
    }

    private static Dictionary<string, int> Signals(JournalEvent journalEvent)
    {
        var signals = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var signal in journalEvent.Items("Signals"))
        {
            if (signal.String("Type") is { Length: > 0 } type && signal.Int("Count") is { } count)
            {
                signals[type] = count;
            }
        }

        return signals;
    }

    private static List<string> Genera(JournalEvent journalEvent) =>
    [
        .. journalEvent.Items("Genuses")
            .Select(genus => genus.String("Genus"))
            .OfType<string>()
            .Select(CodexGenus.Name),
    ];

    /// <summary>True once any scan said true; otherwise the latest scan that wrote the flag.</summary>
    private static bool? Sticky(bool? held, bool? scanned) => held == true ? true : scanned ?? held;

    /// <summary>True or false where the journal wrote the flag, null where it omitted it.</summary>
    private static bool? Flag(JournalEvent journalEvent, string property) =>
        journalEvent.Raw.TryGetProperty(property, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            }
            : null;
}
