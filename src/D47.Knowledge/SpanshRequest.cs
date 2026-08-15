using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using D47.Core.Knowledge;

namespace D47.Knowledge;

/// <summary>
/// Turns a validated <see cref="GalaxyQuery"/> into the search body the service expects.
/// <para>
/// The shapes were established against the live service on 2026-08-14, because there is no
/// published API — the endpoints are reverse-engineered by every third party that uses them. Two
/// facts that request bodies here depend on, both measured rather than assumed:
/// </para>
/// <list type="bullet">
/// <item>A choice filter is <c>{"value":["Federation"]}</c>. Passing the bare string is a 400.</item>
/// <item>A range filter is <c>{"min":"0","max":"20"}</c>, with the bounds as <em>strings</em>.</item>
/// </list>
/// <para>
/// Nothing here validates. By the time a <see cref="GalaxyQuery"/> exists its filters are known
/// to be real ones, which is the property that stops this from building a request the service
/// would silently answer wrong.
/// </para>
/// </summary>
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
                // The service's own key, which is not always the word d47 offers for it — "state"
                // is sent as controlling_minor_faction_state, because the field actually called
                // "state" is honoured and matches nothing.
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
                    // Both ends are always written, with an absent bound becoming the widest
                    // value that still means "unbounded". The service treats a missing key as a
                    // filter it does not recognise, which is the silent-ignore case all over
                    // again — so nothing is left out.
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

    /// <summary>
    /// The station search body.
    /// <para>
    /// Two filter shapes, and they are not the same one. A ship is a plain choice —
    /// <c>ships: {"value":["Krait MkII"]}</c> — while a module is a <em>group</em> whose members
    /// each take their own value array:
    /// <c>modules: {"name":{"value":["Frame Shift Drive"]},"class":{"value":["5"]}}</c>. Getting
    /// that wrong is the silent-ignore failure again: measured on 2026-08-14, the group shape
    /// returned 3,402 stations within 100 light years of Sol and the flat shape returned 10,000 —
    /// the unfiltered total.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// The body search body.
    /// <para>
    /// A <em>third</em> group shape, and the one that punishes guessing hardest. Signals are
    /// <c>signals: {"name":{"value":["Biological"]},"count":2}</c> — a choice member beside a
    /// <b>bare number</b>, not a range object. Measured on 2026-08-14: the range spelling
    /// <c>{"Biological":{"min":"1","max":"40"}}</c> returned the unfiltered 1,315 bodies within
    /// 20 light years of Sol, exactly as a bogus key did, while <c>count</c> written as a range
    /// returned zero every time. Written as a number it matches <em>exactly</em> that many — 1
    /// gave 41 bodies, 2 gave 14, 3 gave none and 4 gave 2, each carrying precisely the count
    /// asked for.
    /// </para>
    /// <para>
    /// Rings are the plain choice shape, <c>rings: {"value":["Icy"]}</c>. The group spelling that
    /// works for modules and signals — <c>{"type":{"value":["Icy"]}}</c> — is a 500 here, which is
    /// at least a loud failure rather than a quiet one.
    /// </para>
    /// </summary>
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

            WriteChoice(writer, "subtype", query.Subtype);
            WriteChoice(writer, "rings", query.RingType);
            WriteChoice(writer, "reserve_level", query.ReserveLevel);

            if (query.Landable is { } landable)
            {
                WriteChoice(writer, "is_landable", landable ? "true" : "false");
            }

            if (query.Terraformable is { } terraformable)
            {
                // Not a boolean of its own: the service models this as a state, and "not
                // terraformable" is one of its four values rather than the absence of the filter.
                WriteChoice(writer, "terraforming_state", terraformable ? "Terraformable" : "Not terraformable");
            }

            WriteSignals(writer, "signals", query.Signal, query.SignalCount);
            WriteSignals(writer, "ring_signals", query.RingSignal, query.RingSignalCount);

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
            // A number, not a range object. The range spelling is accepted and answers nothing.
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

    /// <summary>
    /// Stands in for "no upper bound". Larger than the galaxy is wide in light years and larger
    /// than any populated system's population, which are the two ranges that exist.
    /// </summary>
    private const double UnboundedMax = 1_000_000_000_000;

    private static string Number(double value) =>
        value == Math.Floor(value) && Math.Abs(value) < 1e15
            ? ((long)value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.####", CultureInfo.InvariantCulture);
}
