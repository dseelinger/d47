using System.Text.Json;
using D47.Core.Knowledge;

namespace D47.Knowledge;

/// <summary>
/// Reads the parts of a Spansh response d47 uses, and nothing else.
/// <para>
/// Written against <see cref="JsonDocument"/> rather than deserialised into types, deliberately.
/// A search result carries every body in every system it returns — three systems came back as
/// 44 KB, one system record on its own is 268 KB — and modelling that shape would mean owning a
/// large surface of somebody else's undocumented schema, all of it untrusted (architecture.md
/// §7). Reading six fields by name is smaller, and a field that disappears becomes a null rather
/// than a deserialisation failure that takes the turn down.
/// </para>
/// </summary>
internal static class SpanshResponse
{
    public static GalaxySearchResult ReadSearch(JsonDocument document)
    {
        var root = document.RootElement;

        var systems = new List<SystemSummary>();

        if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            foreach (var result in results.EnumerateArray())
            {
                systems.Add(ReadSystem(result));
            }
        }

        return new GalaxySearchResult(
            ReadReference(root),
            root.TryGetProperty("count", out var count) && count.TryGetInt32(out var total) ? total : systems.Count,
            systems);
    }

    /// <summary>
    /// What the service decided distances were measured from. Echoed back because a Commander who
    /// asked for "near me" and a service that quietly defaulted to Sol have not had the same
    /// question answered.
    /// <para>
    /// Read from <c>reference.name</c> and deliberately <em>not</em> from the sibling
    /// <c>search_reference</c>, which sounds like the same thing and is not: it is a GUID
    /// identifying the search, so a summary built from it would tell the Commander their
    /// distances were measured from <c>4FF6E786-9829-11F1-A270-E7F8D53241C7</c>.
    /// </para>
    /// </summary>
    private static string? ReadReference(JsonElement root) =>
        root.TryGetProperty("reference", out var reference)
        && reference.ValueKind == JsonValueKind.Object
        && reference.TryGetProperty("name", out var name)
        && name.ValueKind == JsonValueKind.String
            ? name.GetString()
            : null;

    public static StationSearchResult ReadStations(JsonDocument document)
    {
        var root = document.RootElement;

        var stations = new List<StationSummary>();

        if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            foreach (var result in results.EnumerateArray())
            {
                stations.Add(ReadStation(result));
            }
        }

        return new StationSearchResult(
            ReadReference(root),
            root.TryGetProperty("count", out var count) && count.TryGetInt32(out var total) ? total : stations.Count,
            stations);
    }

    public static BodySearchResult ReadBodies(JsonDocument document)
    {
        var root = document.RootElement;

        var bodies = new List<BodySummary>();

        if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            foreach (var result in results.EnumerateArray())
            {
                bodies.Add(ReadBody(result));
            }
        }

        return new BodySearchResult(
            ReadReference(root),
            root.TryGetProperty("count", out var count) && count.TryGetInt32(out var total) ? total : bodies.Count,
            bodies);
    }

    private static BodySummary ReadBody(JsonElement element) => new()
    {
        Name = String(element, "name") ?? "an unnamed body",
        SystemName = String(element, "system_name") ?? "an unnamed system",
        Distance = Number(element, "distance"),
        DistanceToArrival = Number(element, "distance_to_arrival"),
        Subtype = String(element, "subtype"),
        IsLandable = Boolean(element, "is_landable"),
        TerraformingState = String(element, "terraforming_state"),
        ReserveLevel = String(element, "reserve_level"),
        MappingValue = Integer(element, "estimated_mapping_value"),
        Signals = ReadSignals(element),
        Rings = ReadRings(element),
    };

    /// <summary>
    /// The signal counts, dropped into pairs. Read defensively for the same reason everything
    /// else here is: a signal whose entry has no name is skipped rather than becoming a blank
    /// line in something a Commander is about to hear read aloud.
    /// </summary>
    private static IReadOnlyList<(string Kind, int Count)> ReadSignals(JsonElement element)
    {
        if (!element.TryGetProperty("signals", out var signals) || signals.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var read = new List<(string, int)>();

        foreach (var signal in signals.EnumerateArray())
        {
            if (String(signal, "name") is { } name)
            {
                read.Add((name, (int)(Integer(signal, "count") ?? 0)));
            }
        }

        return read;
    }

    private static IReadOnlyList<RingSummary> ReadRings(JsonElement element)
    {
        if (!element.TryGetProperty("rings", out var rings) || rings.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var read = new List<RingSummary>();

        foreach (var ring in rings.EnumerateArray())
        {
            read.Add(new RingSummary(String(ring, "name") ?? "an unnamed ring", String(ring, "type"))
            {
                Hotspots = ReadSignals(ring),
                SignalsSeen = Timestamp(ring, "signals_updated_at"),
            });
        }

        return read;
    }

    private static StationSummary ReadStation(JsonElement element) => new()
    {
        Name = String(element, "name") ?? "an unnamed station",
        SystemName = String(element, "system_name") ?? "an unnamed system",
        Distance = Number(element, "distance"),
        DistanceToArrival = Number(element, "distance_to_arrival"),
        Type = String(element, "type"),
        HasLargePad = Boolean(element, "has_large_pad"),

        // Which timestamp depends on what was asked for, and both are worth having: a shipyard
        // seen last year and an outfitting bay seen last week are different kinds of answer.
        StockLastSeen = Timestamp(element, "outfitting_updated_at") ?? Timestamp(element, "shipyard_updated_at"),
    };

    private static DateTimeOffset? Timestamp(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
        && DateTimeOffset.TryParse(
            value.GetString(),
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;

    private static SystemSummary ReadSystem(JsonElement element) => new()
    {
        Name = String(element, "name") ?? "an unnamed system",
        Distance = Number(element, "distance"),
        Allegiance = String(element, "allegiance"),
        Government = String(element, "government"),
        PrimaryEconomy = String(element, "primary_economy"),
        Security = String(element, "security"),
        Population = Integer(element, "population"),
        NeedsPermit = Boolean(element, "needs_permit"),

        // The station list is read for its length and then dropped. How many there are is worth
        // saying out loud; which ones they are is the next question, and answering it unasked is
        // what makes a result too big to speak.
        StationCount = element.TryGetProperty("stations", out var stations)
                       && stations.ValueKind == JsonValueKind.Array
            ? stations.GetArrayLength()
            : null,
    };

    /// <summary>
    /// Where the search measured from, in galactic coordinates.
    /// <para>
    /// This comes off a <em>search</em> response rather than a system endpoint, because there is
    /// no lookup-by-name endpoint — <c>api/system/name/Colonia</c> is a 404, and the by-id
    /// endpoint needs an id64 the Commander does not have. Naming a system as the search's
    /// reference and reading back the coordinates it resolved to is the one call that turns a
    /// spoken name into a position.
    /// </para>
    /// </summary>
    public static (double X, double Y, double Z)? ReadReferenceCoordinates(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("reference", out var reference)
            || reference.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var x = Number(reference, "x");
        var y = Number(reference, "y");
        var z = Number(reference, "z");

        return x is null || y is null || z is null ? null : (x.Value, y.Value, z.Value);
    }

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static double? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var number)
            ? number
            : null;

    private static long? Integer(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt64(out var number)
            ? number
            : null;

    private static bool Boolean(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
