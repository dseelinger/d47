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

            WriteCriteria(writer, GalaxySearchKind.Systems, query.Criteria);

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

    /// <summary>Writes each criterion under its key on <paramref name="kind"/>, in its filter's shape.</summary>
    public static void WriteCriteria(Utf8JsonWriter writer, GalaxySearchKind kind, IReadOnlyList<GalaxyCriterion> criteria)
    {
        foreach (var criterion in criteria)
        {
            // The service's own key, which is not always the word d47 offers for it — "state" is sent as
            // controlling_minor_faction_state, because the field actually called "state" is honoured and
            // matches nothing.
            writer.WriteStartObject(criterion.Filter.FieldOn(kind));

            switch (criterion.Filter.Kind)
            {
                case GalaxyFilterKind.Choice or GalaxyFilterKind.Name:
                    writer.WriteStartArray("value");

                    foreach (var choice in criterion.Choices)
                    {
                        writer.WriteStringValue(choice);
                    }

                    writer.WriteEndArray();
                    break;

                case GalaxyFilterKind.Flag:
                    writer.WriteStartArray("value");
                    writer.WriteStringValue("true");
                    writer.WriteEndArray();
                    break;

                case GalaxyFilterKind.Comparison:
                    writer.WriteStartArray("value");
                    writer.WriteStringValue(Number(criterion.Min ?? 0));
                    writer.WriteStringValue(Number(criterion.Max ?? UnboundedMax));
                    writer.WriteEndArray();
                    writer.WriteString("comparison", "<=>");
                    break;

                default:
                    // Both ends are always written, with an absent bound becoming the widest value that still
                    // means "unbounded".
                    writer.WriteString("min", Number(criterion.Min ?? 0));
                    writer.WriteString("max", Number(criterion.Max ?? UnboundedMax));
                    break;
            }

            writer.WriteEndObject();
        }
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
            writer.WriteString("max", Number(query.Unbounded ? UnboundedMax : query.MaxDistance));
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
            }

            if (query.Services.Count > 0)
            {
                // One object per service, which matches stations with all of them; several names in one
                // object's value array matches stations with any.
                writer.WriteStartArray("services");

                foreach (var service in query.Services)
                {
                    writer.WriteStartObject();
                    WriteGroupMember(writer, "name", service);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            WriteChoice(writer, "material_trader", query.TraderType);
            WriteChoice(writer, "technology_broker", query.TechnologyBroker);

            if (query.LargePadOnly || query.MinPad == PadSize.Large)
            {
                writer.WriteStartObject("has_large_pad");
                writer.WriteStartArray("value");
                writer.WriteStringValue("true");
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            else if (query.MinPad == PadSize.Medium)
            {
                WriteComparison(writer, "medium_pads", 1, UnboundedMax);
            }

            if (query.MaxStationDistance is { } furthest)
            {
                WriteComparison(writer, "distance_to_arrival", 0, furthest);
            }

            if (query.Kinds is { } kinds)
            {
                WriteTypes(writer, AllStationTypes.Where(type => kinds.Contains(SpanshStarSystemService.KindOf(type))));
            }

            WriteCriteria(writer, GalaxySearchKind.Stations, query.Criteria);

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

            if (query.Landable is true)
            {
                WriteChoice(writer, "is_landable", "true");
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

            WriteChoice(writer, "volcanism_type", query.Volcanism);
            WriteChoice(writer, "atmosphere", query.Atmosphere);
            WriteChoice(writer, "is_rotational_period_tidally_locked", query.TidallyLocked is true ? "true" : null);

            // The comparison shape: in the min/max shape none of these filtered correctly.
            WriteComparison(writer, "gravity", query.GravityMin, query.GravityMax);
            WriteComparison(writer, "surface_temperature", query.TemperatureMin, query.TemperatureMax);
            WriteComparison(writer, "distance_to_arrival", 0, query.MaxArrivalDistance);

            WriteCriteria(writer, GalaxySearchKind.Bodies, query.Criteria);

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

    /// <summary>The systems an Exploited, Fortified or Stronghold state covers within a distance, nearest first.</summary>
    public static string PowerplayNear(string referenceSystem, double lightYears)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("filters");

            writer.WriteStartObject("distance");
            writer.WriteString("min", "0");
            writer.WriteString("max", Number(lightYears));
            writer.WriteEndObject();

            writer.WriteStartObject("power_state");
            writer.WriteStartArray("value");
            writer.WriteStringValue("Exploited");
            writer.WriteStringValue("Fortified");
            writer.WriteStringValue("Stronghold");
            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WriteEndObject();

            writer.WriteStartArray("sort");
            writer.WriteStartObject();
            writer.WriteStartObject("distance");
            writer.WriteString("direction", "asc");
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndArray();

            writer.WriteNumber("size", PowerplayNeighbourhood.Limit);
            writer.WriteNumber("page", 0);
            writer.WriteString("reference_system", referenceSystem);

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

    /// <summary>A comparison filter from 0 or <paramref name="min"/>, unbounded above unless <paramref name="max"/> is set; nothing when neither narrows.</summary>
    private static void WriteComparison(Utf8JsonWriter writer, string name, double? min, double? max)
    {
        if (max is null && (min is null || min == 0))
        {
            return;
        }

        writer.WriteStartObject(name);
        writer.WriteStartArray("value");
        writer.WriteStringValue(Number(min ?? 0));
        writer.WriteStringValue(Number(max ?? UnboundedMax));
        writer.WriteEndArray();
        writer.WriteString("comparison", "<=>");
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
    /// Light seconds from the star, filtered server-side on <c>distance_to_arrival</c> as a
    /// <c>value</c> pair with <c>comparison</c> <c>&lt;=&gt;</c>. A min/max object is ignored by the
    /// index: on 2026-10-03, 20 ly around Sol returned 3,601 stations unfiltered and 3,596 with
    /// 0–1,000 Ls, against 2,394 with the comparison shape (#808).
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
                WriteComparison(writer, "distance_to_arrival", 0, furthest);
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
        WriteTypes(writer, AllStationTypes.Where(type =>
            (includeCarriers || !MarketSnapshot.IsCarrierType(type))
            && (surfaceStations || !MarketSnapshot.IsSurfaceType(type))));
    }

    private static void WriteTypes(Utf8JsonWriter writer, IEnumerable<string> types)
    {
        writer.WriteStartObject("type");
        writer.WriteStartArray("value");

        foreach (var type in types)
        {
            writer.WriteStringValue(type);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>A numeric span in the <c>{"value":[min,max],"comparison":"&lt;=&gt;"}</c> shape.</summary>
    private static void WriteComparison(Utf8JsonWriter writer, string name, double min, double max)
    {
        writer.WriteStartObject(name);
        writer.WriteStartArray("value");
        writer.WriteStringValue(Number(min));
        writer.WriteStringValue(Number(max));
        writer.WriteEndArray();
        writer.WriteString("comparison", "<=>");
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
