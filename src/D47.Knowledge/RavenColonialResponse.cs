using System.Globalization;
using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Knowledge;

/// <summary>Reads Raven Colonial's answers, keeping counts and figures and leaving every name and note.</summary>
internal static class RavenColonialResponse
{
    /// <summary>The sites of a system, before their projects are read. A site with no id, name or known status is left out.</summary>
    public static IReadOnlyList<PlannedSite> ReadSites(JsonDocument document, IReadOnlyDictionary<int, string> bodies)
    {
        var sites = new List<PlannedSite>();

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return sites;
        }

        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (element.String("id") is not { Length: > 0 } id
                || element.String("name") is not { Length: > 0 } name
                || ReadStatus(element.String("status")) is not { } status)
            {
                continue;
            }

            var bodyNum = Integer(element, "bodyNum");

            sites.Add(new PlannedSite(id, name, status)
            {
                BuildType = element.String("buildType"),
                BodyNum = bodyNum,
                BodyName = bodyNum is { } number && bodies.TryGetValue(number, out var body) ? body : null,
                BuildId = element.String("buildId") is { Length: > 0 } buildId ? buildId : null,
                MarketId = Whole(element, "marketId"),
            });
        }

        return sites;
    }

    /// <summary>Body number to body name.</summary>
    public static IReadOnlyDictionary<int, string> ReadBodies(JsonDocument document)
    {
        var bodies = new Dictionary<int, string>();

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return bodies;
        }

        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (Integer(element, "num") is { } number && element.String("name") is { Length: > 0 } name)
            {
                bodies[number] = name;
            }
        }

        return bodies;
    }

    /// <summary>The project's figures, merged onto the site. The Commanders are counted and not read.</summary>
    public static PlannedSite ReadProject(PlannedSite site, JsonDocument project, JsonDocument? carriers)
    {
        var root = project.RootElement;

        return site with
        {
            Due = Moment(root, "timeDue"),
            IsPrimaryPort = root.Bool("isPrimaryPort"),
            MarketId = site.MarketId ?? Whole(root, "marketId"),
            BodyNum = site.BodyNum ?? Integer(root, "bodyNum"),
            BodyName = site.BodyName ?? root.String("bodyName"),
            Needed = Tonnes(root.Object("commodities")),
            LinkedCarriers = Count(root, "linkedFC", JsonValueKind.Array),
            Commanders = Count(root, "commanders", JsonValueKind.Object),
            OnLinkedCarriers = carriers is null ? new Dictionary<string, int>() : CarrierCargo(carriers),
        };
    }

    private static PlanStatus? ReadStatus(string? status) => status?.ToLowerInvariant() switch
    {
        "plan" => PlanStatus.Plan,
        "build" => PlanStatus.Build,
        "complete" => PlanStatus.Complete,
        "demolish" => PlanStatus.Demolish,
        _ => null,
    };

    private static Dictionary<string, int> Tonnes(JsonElement? commodities)
    {
        var tonnes = new Dictionary<string, int>();

        if (commodities is { ValueKind: JsonValueKind.Object } values)
        {
            foreach (var commodity in values.EnumerateObject())
            {
                Add(tonnes, commodity.Name, commodity.Value);
            }
        }

        return tonnes;
    }

    /// <summary>Carrier market id to commodity to tonnes, summed over the carriers.</summary>
    private static Dictionary<string, int> CarrierCargo(JsonDocument document)
    {
        var tonnes = new Dictionary<string, int>();

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return tonnes;
        }

        foreach (var carrier in document.RootElement.EnumerateObject())
        {
            if (carrier.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var commodity in carrier.Value.EnumerateObject())
            {
                Add(tonnes, commodity.Name, commodity.Value);
            }
        }

        return tonnes;
    }

    private static void Add(Dictionary<string, int> tonnes, string name, JsonElement value)
    {
        if (JournalJson.Symbol(name) is { } symbol && Whole(value) is > 0 and <= int.MaxValue and var amount)
        {
            tonnes[symbol] = tonnes.GetValueOrDefault(symbol) + (int)amount;
        }
    }

    private static int Count(JsonElement element, string property, JsonValueKind kind)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != kind)
        {
            return 0;
        }

        return kind == JsonValueKind.Array ? value.GetArrayLength() : value.EnumerateObject().Count();
    }

    private static int? Integer(JsonElement element, string property) =>
        Whole(element, property) is >= int.MinValue and <= int.MaxValue and var number ? (int)number : null;

    private static long? Whole(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value)
            ? Whole(value)
            : null;

    /// <summary>A whole number that arrived as a JSON number or as a string of digits.</summary>
    private static long? Whole(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number when value.TryGetInt64(out var number) => number,
        JsonValueKind.String when long.TryParse(
            value.GetString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) => number,
        _ => null,
    };

    private static DateTimeOffset? Moment(JsonElement element, string property) =>
        element.String(property) is { } text
        && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var moment)
            ? moment
            : null;
}
