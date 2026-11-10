using System.Globalization;
using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>The spoken description of a <see cref="NavTarget"/> other than a system.</summary>
public static class TargetAnswer
{
    public const string NothingTargeted = "Nothing is targeted.";

    /// <summary>Standard gravity in m/s², which the journal's <c>SurfaceGravity</c> is divided by to give g.</summary>
    private const double StandardGravity = 9.80665;

    /// <summary>Pascals in one atmosphere; the journal writes pressure in pascals, the galaxy record in atmospheres.</summary>
    private const double Atmosphere = 101_325;

    private const string FromScans = "From your own scans: ";

    private const string FromRecord = "From the galaxy record: ";

    /// <summary>The answer for <paramref name="target"/>; <paramref name="unavailable"/> follows an unknown target's sentence.</summary>
    public static string Describe(NavTarget target, string? unavailable = null) => target switch
    {
        NavTarget.Body body => DescribeBody(body),
        NavTarget.Station station => DescribeStation(station.Record),
        NavTarget.Unknown { Name: { } name } => Joined(
            $"{name} is targeted, but there is no record of it.", unavailable),
        _ => NothingTargeted,
    };

    /// <summary>The locked ship's hull, pilot, rank, faction, legal status and bounty. A Commander's name is never spoken.</summary>
    public static string DescribeShip(TargetedShip ship)
    {
        var hull = ship.Ship is { Length: > 0 } named ? named : "unknown hull";

        if (ship.IsCommander)
        {
            return Joined(
                $"Your locked ship is flown by a Commander. Hull {hull}.",
                ship.LegalStatus is { Length: > 0 } clean ? $"Legal status {clean}." : null);
        }

        var details = new List<string> { $"hull {hull}" };

        if (ship.IsNameable)
        {
            details.Add($"pilot {ship.PilotName}");
        }

        if (ship.Rank is { Length: > 0 } rank)
        {
            details.Add($"rank {rank}");
        }

        if (ship.Faction is { Length: > 0 } faction)
        {
            details.Add($"faction {faction}");
        }

        if (ship.LegalStatus is { Length: > 0 } status)
        {
            details.Add($"legal status {status}");
        }

        if (ship.Bounty is > 0 and var bounty)
        {
            details.Add($"bounty {bounty.ToString("N0", CultureInfo.InvariantCulture)} credits");
        }

        return $"Your locked ship: {string.Join("; ", details)}.";
    }

    /// <summary>The ship sentence first, then the navigation answer when one is selected.</summary>
    public static string WithShip(TargetedShip? ship, string? navigation) => (ship, navigation) switch
    {
        (null, null) => NothingTargeted,
        (null, _) => navigation!,
        (_, null) => DescribeShip(ship),
        _ => Joined(DescribeShip(ship), navigation),
    };

    /// <summary>The journal's m/s² as g, "0.56 g".</summary>
    public static string GravityFromScan(double metresPerSecondSquared) => Gravity(metresPerSecondSquared / StandardGravity);

    /// <summary>The galaxy record's g, already in g.</summary>
    public static string Gravity(double g) => g.ToString("0.00", CultureInfo.InvariantCulture) + " g";

    private static string DescribeBody(NavTarget.Body body)
    {
        var scan = body.Scan;
        var record = body.Record;
        var scanned = new List<string>();
        var recorded = new List<string>();

        void Take(string? fromScan, string? fromRecord)
        {
            if (fromScan is not null)
            {
                scanned.Add(fromScan);
            }
            else if (fromRecord is not null)
            {
                recorded.Add(fromRecord);
            }
        }

        var planetScan = scan?.PlanetClass is { Length: > 0 };
        var planetRecord = record?.Type == "Planet";

        Take(planetScan ? scan!.PlanetClass : null, record?.SubType);
        Take(
            planetScan ? (scan!.Landable ? "landable" : "not landable") : null,
            planetRecord ? (record!.IsLandable ? "landable" : "not landable") : null);
        Take(
            scan?.SurfaceGravity is { } metres ? $"gravity {GravityFromScan(metres)}" : null,
            record?.Gravity is { } g ? $"gravity {Gravity(g)}" : null);
        Take(
            planetScan ? Labelled(scan!.Atmosphere, "atmosphere", "no atmosphere") : null,
            planetRecord ? Labelled(record!.Atmosphere, "atmosphere", null) : null);
        Take(
            scan?.SurfaceTemperature is { } scanKelvin ? $"surface temperature {Kelvin(scanKelvin)}" : null,
            record?.SurfaceTemperature is { } kelvin ? $"surface temperature {Kelvin(kelvin)}" : null);
        Take(
            scan?.SurfacePressure is { } pascals and > 0 ? $"surface pressure {Atmospheres(pascals / Atmosphere)}" : null,
            record?.SurfacePressure is { } atmospheres and > 0 ? $"surface pressure {Atmospheres(atmospheres)}" : null);
        Take(
            planetScan ? Labelled(scan!.Volcanism, "volcanism", "no volcanism") : null,
            planetRecord ? Labelled(record!.Volcanism, "volcanism", null) : null);
        Take(
            planetScan ? (scan!.TerraformState is { Length: > 0 } state ? state : "not terraformable") : null,
            planetRecord && record!.TerraformingState is { Length: > 0 } terraforming ? terraforming : null);
        Take(
            scan?.Radius is { } radiusMetres ? $"radius {Kilometres(radiusMetres / 1000)}" : null,
            record?.Radius is { } radiusKilometres ? $"radius {Kilometres(radiusKilometres)}" : null);
        Take(
            null,
            record?.DistanceToArrival is { } lightSeconds
                ? $"{lightSeconds.ToString("N0", CultureInfo.InvariantCulture)} Ls from arrival"
                : null);
        Take(
            scan is { Rings.Count: > 0 }
                ? "rings: " + string.Join(", ", scan.Rings.Select(ring => Ring(Short(ring.Name, body.Name), RingClass(ring.RingClass))))
                : null,
            record is { Rings.Count: > 0 }
                ? "rings: " + string.Join(", ", record.Rings.Select(ring => Ring(Short(ring.Name, body.Name), ring.Type)))
                : null);
        Take(
            body.Signals is { Signals.Count: > 0 } signals
                ? "signals: " + string.Join(", ", signals.Signals.Select(signal => Signal(signal.Count, signal.Type)))
                : null,
            record is { Signals.Count: > 0 }
                ? "signals: " + string.Join(", ", record.Signals.Select(signal => Signal(signal.Value, signal.Key)))
                : null);
        Take(
            body.Signals is { Genera.Count: > 0 } found ? "genera: " + string.Join(", ", found.Genera) : null,
            record is { Genera.Count: > 0 } ? "genera: " + string.Join(", ", record.Genera) : null);
        Take(
            null,
            body.Stations.Count > 0
                ? "on it: " + string.Join(", ", body.Stations.Select(station => $"{station.Name} ({StationType(station)})"))
                : null);
        Take(
            scan is { Materials.Count: > 0 }
                ? Materials(scan.Materials.Select(material => (MaterialCatalogue.Find(material.Name)?.Name ?? material.Name, material.Percent)))
                : null,
            record is { Materials.Count: > 0 }
                ? Materials(record.Materials.Select(material => (material.Key, material.Value)))
                : null);

        return Joined(
            $"Your target is {body.Name}.",
            scanned.Count > 0 ? FromScans + string.Join("; ", scanned) + "." : null,
            recorded.Count > 0 ? FromRecord + string.Join("; ", recorded) + "." : null,
            scanned.Count == 0 && recorded.Count == 0 ? "Nothing more is on record about it." : null);
    }

    private static string DescribeStation(StationProfile station)
    {
        string?[] parts =
        [
            StationType(station),
            station.Body is { Length: > 0 } body ? $"on {body}" : null,
            station.DistanceToArrival is { } lightSeconds
                ? $"{lightSeconds.ToString("N0", CultureInfo.InvariantCulture)} Ls from arrival"
                : null,
            station.ControllingFaction is { Length: > 0 } faction ? $"controlled by {faction}" : null,
            station.Government is { Length: > 0 } government ? $"{government} government" : null,
            station.PrimaryEconomy is { Length: > 0 } economy ? $"{economy} economy" : null,
            station.LargestPad is { } pad ? $"largest pad {pad}" : "no landing pads on record",
            station.Services.Count > 0 ? "services: " + string.Join(", ", station.Services) : null,
        ];

        return Joined(
            $"Your target is {station.Name}.",
            FromRecord + string.Join("; ", parts.Where(part => part is not null)) + ".");
    }

    private static string StationType(StationProfile station) =>
        station.Type is { Length: > 0 } type
            ? type
            : station.Kind switch
            {
                StationKind.Starport => "Starport",
                StationKind.Outpost => "Outpost",
                StationKind.SurfacePort => "Surface port",
                StationKind.Settlement => "Settlement",
                StationKind.Megaship => "Megaship",
                StationKind.FleetCarrier => "Fleet carrier",
                _ => "Station",
            };

    /// <summary>"thin carbon dioxide atmosphere": the noun is added where the value lacks it; empty means <paramref name="none"/>.</summary>
    private static string? Labelled(string? value, string noun, string? none) =>
        string.IsNullOrWhiteSpace(value)
            ? none
            : value.Contains(noun, StringComparison.OrdinalIgnoreCase) ? value : $"{value} {noun}";

    private static string Kelvin(double kelvin) => kelvin.ToString("N0", CultureInfo.InvariantCulture) + " K";

    private static string Atmospheres(double atmospheres) =>
        atmospheres.ToString(atmospheres < 0.01 ? "0.####" : "0.##", CultureInfo.InvariantCulture) + " atm";

    private static string Kilometres(double kilometres) => kilometres.ToString("N0", CultureInfo.InvariantCulture) + " km";

    private static string Ring(string name, string? type) => type is { Length: > 0 } ? $"{name} ({type})" : name;

    /// <summary>"A Ring" from "Colonia 3 A Ring" on Colonia 3.</summary>
    private static string Short(string ring, string body) =>
        ring.StartsWith(body + " ", StringComparison.OrdinalIgnoreCase) ? ring[(body.Length + 1)..] : ring;

    private static string? RingClass(string journal) => journal switch
    {
        "eRingClass_Icy" => "Icy",
        "eRingClass_Rocky" => "Rocky",
        "eRingClass_Metalic" => "Metallic",
        "eRingClass_MetalRich" => "Metal Rich",
        { Length: > 0 } other => other.Replace("eRingClass_", "", StringComparison.Ordinal),
        _ => null,
    };

    /// <summary>"3 Biological": the galaxy record's <c>$SAA_SignalType_Biological;</c> said as the journal says it.</summary>
    private static string Signal(int count, string type)
    {
        var name = type.StartsWith("$SAA_SignalType_", StringComparison.Ordinal)
            ? type["$SAA_SignalType_".Length..].TrimEnd(';')
            : type.Trim('$', ';');

        return $"{count.ToString(CultureInfo.InvariantCulture)} {name}";
    }

    private static string Materials(IEnumerable<(string Name, double Percent)> materials) =>
        "materials: " + string.Join(
            ", ",
            materials
                .OrderByDescending(material => material.Percent)
                .Select(material => $"{material.Name} {material.Percent.ToString("0.#", CultureInfo.InvariantCulture)}%"));

    private static string Joined(params string?[] sentences) =>
        string.Join(" ", sentences.Where(sentence => !string.IsNullOrWhiteSpace(sentence)));
}
