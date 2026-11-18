using System.Globalization;
using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Knowledge;

/// <summary>Reads Raven Colonial's answers. Fields that name or link to another person are not read; Commanders are counted.</summary>
internal static class RavenColonialResponse
{
    /// <summary>The sites of a system, before their projects are read. A site with no id, name or known status is left out.</summary>
    public static IReadOnlyList<PlannedSite> ReadSites(JsonDocument document, IReadOnlyList<PlannedBody> bodies)
    {
        var names = bodies.GroupBy(body => body.Num).ToDictionary(group => group.Key, group => group.First().Name);
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
                BodyName = bodyNum is { } number && names.TryGetValue(number, out var body) ? body : null,
                BuildId = element.String("buildId") is { Length: > 0 } buildId ? buildId : null,
                MarketId = Whole(element, "marketId"),
            });
        }

        return sites;
    }

    /// <summary>The system's bodies. A body with no number or name is left out.</summary>
    public static IReadOnlyList<PlannedBody> ReadBodies(JsonDocument document)
    {
        var bodies = new List<PlannedBody>();

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return bodies;
        }

        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (Integer(element, "num") is not { } number || element.String("name") is not { Length: > 0 } name)
            {
                continue;
            }

            bodies.Add(new PlannedBody(number, name)
            {
                DistanceLs = Double(element, "distLS"),
                Parents = Numbers(element, "parents").Select(parent => (int)parent).ToList(),
                Type = element.String("type"),
                SubType = element.String("subType"),
                Features = Strings(element, "features"),
                Radius = Double(element, "radius"),
                Temperature = Double(element, "temp"),
                Gravity = Double(element, "gravity"),
            });
        }

        return bodies;
    }

    /// <summary>The project's figures, merged onto the site. The Commanders are counted and not read.</summary>
    public static PlannedSite ReadProject(PlannedSite site, JsonDocument project, JsonDocument? carriers)
    {
        var root = project.RootElement;
        var cargo = carriers is null ? new Dictionary<long, Dictionary<string, int>>() : ReadCarrierCargo(carriers);

        return site with
        {
            Due = Moment(root, "timeDue"),
            IsPrimaryPort = root.Bool("isPrimaryPort"),
            BuildType = site.BuildType ?? root.String("buildType"),
            BuildId = site.BuildId ?? root.String("buildId"),
            MarketId = site.MarketId ?? Whole(root, "marketId"),
            BodyNum = site.BodyNum ?? Integer(root, "bodyNum"),
            BodyName = site.BodyName ?? root.String("bodyName"),
            Needed = Tonnes(root.Object("commodities")),
            LinkedCarriers = Count(root, "linkedFC", JsonValueKind.Array),
            Commanders = Count(root, "commanders", JsonValueKind.Object),
            OnLinkedCarriers = SumOf(cargo),
            Timestamp = Moment(root, "timestamp"),
            ETag = root.String("eTag"),
            SumNeed = Integer(root, "sumNeed"),
            MaxNeed = Integer(root, "maxNeed"),
            Complete = root.Bool("complete"),
            Ready = Strings(root, "ready"),
            Carriers = ReadCarriers(root, cargo),
            BuildName = root.String("buildName"),
            SystemAddress = Whole(root, "systemAddress"),
            SystemName = root.String("systemName"),
            StarPos = Doubles(root, "starPos"),
            FactionName = root.String("factionName"),
            Completed = Moment(root, "timeCompleted"),
            Started = Moment(root, "timestarted"),
            BodyType = root.String("bodyType"),
            BodyFeatures = Strings(root, "bodyFeatures"),
            SystemFeatures = Strings(root, "systemFeatures"),
            ReserveLevel = root.String("reserveLevel"),
        };
    }

    /// <summary>Carrier market id to commodity to tonnes.</summary>
    public static Dictionary<long, Dictionary<string, int>> ReadCarrierCargo(JsonDocument document)
    {
        var carriers = new Dictionary<long, Dictionary<string, int>>();

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return carriers;
        }

        foreach (var carrier in document.RootElement.EnumerateObject())
        {
            if (carrier.Value.ValueKind != JsonValueKind.Object
                || !long.TryParse(carrier.Name, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var marketId))
            {
                continue;
            }

            var tonnes = new Dictionary<string, int>();

            foreach (var commodity in carrier.Value.EnumerateObject())
            {
                Add(tonnes, commodity.Name, commodity.Value);
            }

            carriers[marketId] = tonnes;
        }

        return carriers;
    }

    private static List<PlannedCarrier> ReadCarriers(JsonElement project, Dictionary<long, Dictionary<string, int>> cargo)
    {
        var carriers = new List<PlannedCarrier>();

        foreach (var element in Elements(project, "linkedFC"))
        {
            if (Whole(element, "marketId") is not { } marketId || element.String("name") is not { Length: > 0 } name)
            {
                continue;
            }

            carriers.Add(new PlannedCarrier(marketId, name)
            {
                Assign = Strings(element, "assign"),
                LastRefresh = Moment(element, "lastRefresh"),
                Cargo = cargo.TryGetValue(marketId, out var aboard) ? aboard : new Dictionary<string, int>(),
            });
        }

        return carriers;
    }

    private static Dictionary<string, int> SumOf(Dictionary<long, Dictionary<string, int>> carriers)
    {
        var tonnes = new Dictionary<string, int>();

        foreach (var cargo in carriers.Values)
        {
            foreach (var (symbol, amount) in cargo)
            {
                tonnes[symbol] = tonnes.GetValueOrDefault(symbol) + amount;
            }
        }

        return tonnes;
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

    private static double? Double(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) ? Double(value) : null;

    /// <summary>A number that arrived as a JSON number or as a string.</summary>
    private static double? Double(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number when value.TryGetDouble(out var number) => number,
        JsonValueKind.String when double.TryParse(
            value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) => number,
        _ => null,
    };

    private static IEnumerable<JsonElement> Elements(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
            : [];

    private static List<double> Doubles(JsonElement element, string property) =>
        Elements(element, property).Select(item => Double(item)).OfType<double>().ToList();

    private static List<long> Numbers(JsonElement element, string property) =>
        Elements(element, property).Select(item => Whole(item)).OfType<long>().ToList();

    private static List<string> Strings(JsonElement element, string property) =>
        Elements(element, property)
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .ToList();

    private static DateTimeOffset? Moment(JsonElement element, string property) =>
        element.String(property) is { } text
        && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var moment)
            ? moment
            : null;
}
