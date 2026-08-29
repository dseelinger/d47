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

            if (query.IsAboutTraders)
            {
                // Two shapes in three lines, and they are not the same one — which is the whole
                // lesson of this file restated. `services` is a group whose member takes the
                // value array; `material_trader` is a plain choice, and the group spelling that
                // works one line above returns 2 stations where the flat one returns 565.
                // Measured 2026-08-15. Shape is per field and cannot be inferred from a family.
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

            if (query.SystemNames.Count > 0)
            {
                // A plain choice taking every name at once — three systems in one call returned
                // exactly their 65 bodies. The group spelling that works for modules and signals
                // is a 500 here, and the key without the `_name` suffix is accepted and ignored,
                // returning every body in range: the same silent-drop this file keeps meeting.
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
                // Not a boolean of its own: the service models this as a state, and "not
                // terraformable" is one of its four values rather than the absence of the filter.
                WriteChoice(writer, "terraforming_state", terraformable ? "Terraformable" : "Not terraformable");
            }

            WriteSignals(writer, "signals", query.Signal, query.SignalCount);
            WriteSignals(writer, "ring_signals", query.RingSignal, query.RingSignalCount);

            if (query.Material is { } material)
            {
                // A group, like modules and signals — and only the name member exists. Share is
                // neither filterable nor sortable: percentage, value and count beside the name are
                // all silently ignored, and a sort on the material is dropped. Measured
                // 2026-08-15: the group returns 152 landable bodies within 20 ly of Sol where the
                // obvious flat spelling returns the unfiltered 703.
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

    /// <summary>
    /// The colonisation candidate scan: every system within claim range, least populated first.
    /// <para>
    /// <b>The sort is doing the filter's job, and it has to.</b> <c>population</c> is a field this
    /// index knows — its own <c>field_values</c> reports a min and a max for it — and a range
    /// filter on it is <em>silently dropped</em>. Measured 2026-08-16 within 15 light years of
    /// Sol, where 48 of the 51 systems are populated: <c>{"min":"1","max":"1000000000000"}</c>
    /// returned 51, <c>{"min":"0","max":"0"}</c> returned 51, numeric bounds returned 51, and a
    /// key that does not exist at all returned 51. Written as a choice instead it is honoured and
    /// matches nothing — 0 results for both <c>"0"</c> and a population a system in range actually
    /// has. So there is no spelling that works, and the deciding is done over the response.
    /// </para>
    /// <para>
    /// Sorting on the same field <em>does</em> work, and a second key holds distance order within
    /// the ties, so the page that comes back is the nearest unpopulated systems in order. Neither
    /// <c>is_colonised</c> nor <c>is_being_colonised</c> is ever sent: they are presence flags
    /// whose value is discarded, and asking for <c>"false"</c> returns precisely the systems that
    /// are true.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// The market sweep behind d47's own trade planner (Phase 36).
    /// <para>
    /// The same endpoint the station search uses, asked a different question: no module, no ship,
    /// no service — just everything within a radius, for the <c>market</c> array each result
    /// carries. Measured against the live service on 2026-08-19: Sol's Walz Depot came back with
    /// 342 priced commodities, and a search answers in 1.1 to 1.3 seconds <em>whatever</em> it
    /// returns — 25 stations at 125 KiB took 1.07s and 100 stations at 519 KiB took 1.27s. So the
    /// bill is the number of requests and hardly the size of them, which is why the pages here are
    /// as large as the service will give.
    /// </para>
    /// <para>
    /// <b>No pad filter, deliberately.</b> It would narrow the pull, and it would also drop the
    /// station the Commander is standing on when that station is an outpost — leaving the planner
    /// unable to price the market it is planning from. The pad rule is applied by
    /// <see cref="TradePlanner"/> instead, which knows to exempt the origin.
    /// </para>
    /// <para>
    /// <b>And no price or demand bound</b>, because the service accepts them and ignores them:
    /// 203 stations came back for <c>demand &gt;= 1</c>, 203 for <c>demand &gt;= 50000</c> and 203
    /// for no bound at all, and every sort shape tried against a commodity's price answered
    /// HTTP 400. The shortlist arrives unranked and d47 ranks it, which is the arrangement this
    /// phase wanted anyway.
    /// </para>
    /// </summary>
    /// <param name="commodity">
    /// Narrows the search to stations that actually stock — or want — this, server-side (#156).
    /// Null for the general sweep, which is what trade planning and colonisation sourcing want.
    /// </param>
    /// <param name="selling">
    /// Which side the bound goes on: supply for a Commander buying, demand for one selling.
    /// <b>Demand bounds are honoured on this endpoint</b>, measured against Eurybia on
    /// 2026-08-28 — 12 stations within 15 ly for <c>demand &gt;= 1</c> against 449 unfiltered.
    /// That had to be probed rather than assumed, because the note on
    /// <see cref="Core.Knowledge.CommodityMarketSearch"/> records demand bounds being
    /// <em>accepted and ignored</em> — true, and measured on the <em>trade</em> endpoint, which
    /// is a different one.
    /// </param>
    public static string Markets(
        string referenceSystem,
        double radius,
        int size,
        int page,
        string? commodity = null,
        bool selling = false)
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

            if (commodity is { Length: > 0 } wanted)
            {
                WriteMarketFilter(writer, wanted, selling);
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
    /// The commodity filter, <b>always with a bound on it</b> (#156).
    /// <para>
    /// <b>The bound is what makes the 150-station budget worth spending.</b> The name on its own
    /// is honoured — 26 stations within 15 ly of Eurybia carry a Landmines row against 449
    /// unfiltered, measured 2026-08-28 — but a row is not stock: it matches stations quoting
    /// supply 0 and demand 0, which is most of them. With <c>supply &gt;= 1</c> the same search
    /// returns 8. The issue reported the name-only shape as silently ignored on the evidence that
    /// the count stayed at 10,000; that count is the endpoint's own cap and says nothing either
    /// way, which is why this was measured again at a radius small enough to be under it.
    /// </para>
    /// <para>
    /// The upper bound is deliberately far past anything a market holds. It exists because the
    /// filter is a range rather than a comparison, not because anything is being excluded by it.
    /// </para>
    /// </summary>
    private static void WriteMarketFilter(Utf8JsonWriter writer, string commodity, bool selling)
    {
        writer.WriteStartArray("market");
        writer.WriteStartObject();
        writer.WriteString("name", commodity);

        writer.WriteStartObject(selling ? "demand" : "supply");
        writer.WriteStartArray("value");
        writer.WriteStringValue("1");
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
