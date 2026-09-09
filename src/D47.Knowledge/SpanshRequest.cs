using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using D47.Core.Knowledge;

namespace D47.Knowledge;

/// <summary>Turns a validated <see cref="GalaxyQuery"/> into the search body the service expects.</summary>
internal static class SpanshRequest
{
    public static string Search(GalaxyQuery query)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();

            writer.WriteStartObject("filters");

            foreach (var criterion in query.Criteria)
            {
                // The service's own key, which is not always the word d47 offers for it — "state" is sent as
                // controlling_minor_faction_state, because the field actually called "state" is honoured and
                // matches nothing.
                writer.WriteStartObject(criterion.Filter.Field);

                if (criterion.Filter.Kind == GalaxyFilterKind.Choice)
                {
                    writer.WriteStartArray("value");

                    foreach (var choice in criterion.Choices)
                    {
                        writer.WriteStringValue(choice);
                    }

                    writer.WriteEndArray();
                }
                else
                {
                    // Both ends are always written, with an absent bound becoming the widest value that still
                    // means "unbounded".
                    writer.WriteString("min", Number(criterion.Min ?? 0));
                    writer.WriteString("max", Number(criterion.Max ?? UnboundedMax));
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();

            writer.WriteStartArray("sort");
            writer.WriteStartObject();
            writer.WriteStartObject("distance");
            writer.WriteString("direction", "asc");
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndArray();

            writer.WriteNumber("size", query.Size);
            writer.WriteNumber("page", 0);

            if (query.ReferenceSystem is not null)
            {
                writer.WriteString("reference_system", query.ReferenceSystem);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>The station search body.</summary>
    public static string Stations(StationQuery query)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("filters");

            writer.WriteStartObject("distance");
            writer.WriteString("min", "0");
            writer.WriteString("max", Number(query.MaxDistance));
            writer.WriteEndObject();

            if (query.Ship is not null)
            {
                writer.WriteStartObject("ships");
                writer.WriteStartArray("value");
                writer.WriteStringValue(query.Ship);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            if (query.Module is not null)
            {
                writer.WriteStartObject("modules");

                WriteGroupMember(writer, "name", query.Module);

                if (query.ModuleClass is not null)
                {
                    WriteGroupMember(writer, "class", query.ModuleClass);
                }

                if (query.ModuleRating is not null)
                {
                    WriteGroupMember(writer, "rating", query.ModuleRating);
                }

                writer.WriteEndObject();
            }

            if (query.IsAboutTraders)
            {
                // Two shapes in three lines, and they are not the same one — which is the whole lesson of
                // this file restated. `services` is a group whose member takes the value array;
                // `material_trader` is a plain choice, and the group spelling that works one line above
                // returns 2 stations where the flat one returns 565.
                writer.WriteStartObject("services");
                WriteGroupMember(writer, "name", "Material Trader");
                writer.WriteEndObject();

                if (query.TraderType is { } traderType)
                {
                    WriteChoice(writer, "material_trader", traderType);
                }
            }

            if (query.LargePadOnly)
            {
                writer.WriteStartObject("has_large_pad");
                writer.WriteStartArray("value");
                writer.WriteStringValue("true");
                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndObject();

            writer.WriteStartArray("sort");
            writer.WriteStartObject();
            writer.WriteStartObject("distance");
            writer.WriteString("direction", "asc");
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndArray();

            writer.WriteNumber("size", query.Size);
            writer.WriteNumber("page", 0);

            if (query.ReferenceSystem is not null)
            {
                writer.WriteString("reference_system", query.ReferenceSystem);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>The body search body.</summary>
    public static string Bodies(BodyQuery query)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("filters");

            writer.WriteStartObject("distance");
            writer.WriteString("min", "0");
            writer.WriteString("max", Number(query.MaxDistance));
            writer.WriteEndObject();

            if (query.SystemNames.Count > 0)
            {
                // A plain choice taking every name at once — three systems in one call returned exactly their
                // 65 bodies.
                writer.WriteStartObject("system_name");
                writer.WriteStartArray("value");

                foreach (var name in query.SystemNames)
                {
                    writer.WriteStringValue(name);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            WriteChoice(writer, "subtype", query.Subtype);
            WriteChoice(writer, "rings", query.RingType);
            WriteChoice(writer, "reserve_level", query.ReserveLevel);

            if (query.Landable is { } landable)
            {
                WriteChoice(writer, "is_landable", landable ? "true" : "false");
            }

            if (query.Terraformable is { } terraformable)
            {
                // Not a boolean of its own: the service models this as a state, and "not terraformable" is
                // one of its four values rather than the absence of the filter.
                WriteChoice(writer, "terraforming_state", terraformable ? "Terraformable" : "Not terraformable");
            }

            WriteSignals(writer, "signals", query.Signal, query.SignalCount);
            WriteSignals(writer, "ring_signals", query.RingSignal, query.RingSignalCount);

            if (query.Material is { } material)
            {
                // A group, like modules and signals — and only the name member exists.
                writer.WriteStartObject("materials");
                WriteGroupMember(writer, "name", material);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();

            writer.WriteStartArray("sort");
            writer.WriteStartObject();
            writer.WriteStartObject("distance");
            writer.WriteString("direction", "asc");
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndArray();

            writer.WriteNumber("size", query.Size);
            writer.WriteNumber("page", 0);

            if (query.ReferenceSystem is not null)
            {
                writer.WriteString("reference_system", query.ReferenceSystem);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>The colonisation candidate scan: every system within claim range, least populated first.</summary>
    public static string Colonisation(ColonisationQuery query)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("filters");

            writer.WriteStartObject("distance");
            writer.WriteString("min", "0");
            writer.WriteString("max", Number(query.MaxDistance));
            writer.WriteEndObject();

            writer.WriteEndObject();

            writer.WriteStartArray("sort");

            writer.WriteStartObject();
            writer.WriteStartObject("population");
            writer.WriteString("direction", "asc");
            writer.WriteEndObject();
            writer.WriteEndObject();

            writer.WriteStartObject();
            writer.WriteStartObject("distance");
            writer.WriteString("direction", "asc");
            writer.WriteEndObject();
            writer.WriteEndObject();

            writer.WriteEndArray();

            writer.WriteNumber("size", ColonisationQuery.ScanSize);
            writer.WriteNumber("page", 0);
            writer.WriteString("reference_system", query.ReferenceSystem);

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteChoice(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null)
        {
            return;
        }

        writer.WriteStartObject(name);
        writer.WriteStartArray("value");
        writer.WriteStringValue(value);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>The market sweep behind d47's own trade planner (Phase 36).</summary>
    /// <param name="commodity">
    /// Narrows the search to stations that actually stock — or want — this, server-side (#156).
    /// </param>
    /// <param name="selling">
    /// Which side the bound goes on: supply for a Commander buying, demand for one selling.
    /// </param>
    /// <param name="includeCarriers">
    /// Fleet carriers, excluded server-side unless asked for (#308).
    /// </param>
    /// <param name="surfaceStations">
    /// Also planetary ports, outposts and settlements (#308).
    /// </param>
    /// <param name="maxStationDistance">
    /// Light seconds from the star, filtered server-side on <c>distance_to_arrival</c> — confirmed
    /// against the live index on 2026-09-06 to take the same min/max shape as <c>distance</c> (#308).
    /// </param>
    /// <param name="largePad">
    /// Forwarded the same way <see cref="Stations"/> already does (#308).
    /// </param>
    public static string Markets(
        string referenceSystem,
        double radius,
        int size,
        int page,
        string? commodity = null,
        bool selling = false,
        int minimum = 1,
        bool includeCarriers = false,
        bool surfaceStations = false,
        double? maxStationDistance = null,
        bool largePad = false)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("filters");

            writer.WriteStartObject("distance");
            writer.WriteString("min", "0");
            writer.WriteString("max", Number(radius));
            writer.WriteEndObject();

            if (!includeCarriers || !surfaceStations)
            {
                WriteStationTypeFilter(writer, includeCarriers, surfaceStations);
            }

            if (maxStationDistance is { } furthest)
            {
                writer.WriteStartObject("distance_to_arrival");
                writer.WriteString("min", "0");
                writer.WriteString("max", Number(furthest));
                writer.WriteEndObject();
            }

            WriteChoice(writer, "has_large_pad", largePad ? "true" : null);

            if (commodity is { Length: > 0 } wanted)
            {
                WriteMarketFilter(writer, wanted, selling, minimum);
            }

            writer.WriteEndObject();

            writer.WriteStartArray("sort");
            writer.WriteStartObject();
            writer.WriteStartObject("distance");
            writer.WriteString("direction", "asc");
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndArray();

            writer.WriteNumber("size", size);
            writer.WriteNumber("page", page);
            writer.WriteString("reference_system", referenceSystem);

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>
    /// Every station type the live index reports, confirmed against
    /// <c>/api/stations/field_values/type</c> on 2026-09-06.
    /// </summary>
    private static readonly string[] AllStationTypes =
    [
        "Asteroid base", "Coriolis Starport", "Dodec Starport", "Drake-Class Carrier", "Mega ship",
        "Ocellus Starport", "Orbis Starport", "Outpost", "Planetary Construction Depot",
        "Planetary Outpost", "Planetary Port", "Settlement", "Space Construction Depot",
        "Surface Settlement",
    ];

    /// <summary>
    /// Excludes carriers and/or surface stations server-side by naming every type that is neither,
    /// rather than by naming what to drop — <c>type</c> is a group filter like <c>modules</c> and
    /// <c>signals</c>, which this file has repeatedly measured as an inclusion list rather than a
    /// comparison (#308).
    /// </summary>
    private static void WriteStationTypeFilter(Utf8JsonWriter writer, bool includeCarriers, bool surfaceStations)
    {
        var allowed = AllStationTypes.Where(type =>
            (includeCarriers || !MarketSnapshot.IsCarrierType(type))
            && (surfaceStations || !MarketSnapshot.IsSurfaceType(type)));

        writer.WriteStartObject("type");
        writer.WriteStartArray("value");

        foreach (var type in allowed)
        {
            writer.WriteStringValue(type);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>The commodity filter, always with a bound on it (#156).</summary>
    private static void WriteMarketFilter(Utf8JsonWriter writer, string commodity, bool selling, int minimum)
    {
        writer.WriteStartArray("market");
        writer.WriteStartObject();
        writer.WriteString("name", commodity);

        // The floor is the Commander's when they set one (#296), and 1 — "has any" — when they did not.
        writer.WriteStartObject(selling ? "demand" : "supply");
        writer.WriteStartArray("value");
        writer.WriteStringValue(Math.Max(1, minimum).ToString(CultureInfo.InvariantCulture));
        writer.WriteStringValue("1000000000");
        writer.WriteEndArray();
        writer.WriteString("comparison", "<=>");
        writer.WriteEndObject();

        writer.WriteEndObject();
        writer.WriteEndArray();
    }

    private static void WriteSignals(Utf8JsonWriter writer, string group, string? name, int? count)
    {
        if (name is null)
        {
            return;
        }

        writer.WriteStartObject(group);
        WriteGroupMember(writer, "name", name);

        if (count is not null)
        {
            // A number, not a range object.
            writer.WriteNumber("count", count.Value);
        }

        writer.WriteEndObject();
    }

    private static void WriteGroupMember(Utf8JsonWriter writer, string name, string value)
    {
        writer.WriteStartObject(name);
        writer.WriteStartArray("value");
        writer.WriteStringValue(value);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>Stands in for "no upper bound".</summary>
    private const double UnboundedMax = 1_000_000_000_000;

    private static string Number(double value) =>
        value == Math.Floor(value) && Math.Abs(value) < 1e15
            ? ((long)value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.####", CultureInfo.InvariantCulture);
}
