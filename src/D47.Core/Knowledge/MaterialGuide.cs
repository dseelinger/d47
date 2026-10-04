using System.Globalization;
using D47.Core.Journal;

namespace D47.Core.Knowledge;

public enum TradeDirection
{
    /// <summary>To the next grade up, in the same trade line.</summary>
    Up,

    /// <summary>To the next grade down, in the same trade line.</summary>
    Down,

    /// <summary>To the same grade in another line of the same category.</summary>
    Across,
}

/// <summary>One material trader exchange: give <see cref="Give"/> of this material, get <see cref="Get"/> of the target.</summary>
public sealed record MaterialTrade(TradeDirection Direction, int ToGrade, int Give, int Get);

/// <summary>Everything the material detail answer says about one material.</summary>
/// <param name="Held">What the Commander holds, or null before the journal's materials snapshot.</param>
/// <param name="Capacity">The most the Commander can hold, or null for a material with no grade.</param>
/// <param name="HowToFarm">One step per line, best first.</param>
/// <param name="Trades">Empty for a material with no trader line.</param>
public sealed record MaterialDetail(
    MaterialEntry Entry,
    MaterialLedger Ledger,
    string? Category,
    int? Grade,
    int? Held,
    int? Capacity,
    IReadOnlyList<string> HowToFarm,
    IReadOnlyList<MaterialTrade> Trades);

public enum MaterialPlaceKind
{
    Body,
    System,
}

/// <summary>One place a search found a material, with a note such as a share or a population.</summary>
public sealed record MaterialPlace(string Place, string System, string Note, double? Distance);

/// <summary>
/// The places a galaxy search found, nearest or richest first. <see cref="Message"/> says why there are
/// none when a search ran and came back empty or could not run.
/// </summary>
public sealed record MaterialPlaces(
    MaterialPlaceKind Kind,
    string Heading,
    IReadOnlyList<MaterialPlace> Places,
    string? Message)
{
    public static readonly MaterialPlaces None = new(MaterialPlaceKind.Body, string.Empty, [], null);

    /// <summary>The system a clipboard offer should name.</summary>
    public string? FirstSystem => Places.Count > 0 ? Places[0].System : null;
}

public static class MaterialGuide
{
    /// <summary>The detail for a material by symbol or name, or null where the catalogue does not know it.</summary>
    public static MaterialDetail? For(string symbol, CommanderGameState? state)
    {
        if (MaterialCatalogue.Find(symbol) is not { } entry)
        {
            return null;
        }

        int? held = state?.Materials is { SnapshotSeen: true } inventory ? inventory.CountOf(entry.Symbol) : null;
        int? capacity = entry.Grade is { } grade ? MaterialGrades.CapacityOfGrade(grade) : null;

        return new MaterialDetail(
            entry,
            entry.Ledger,
            entry.Category,
            entry.Grade,
            held,
            capacity,
            FarmingAdvice.HowToObtain(entry),
            Trades(entry));
    }

    private static List<MaterialTrade> Trades(MaterialEntry entry)
    {
        var trades = new List<MaterialTrade>();

        if (!entry.IsTradeable || entry.Grade is not { } grade)
        {
            return trades;
        }

        Add(trades, TradeDirection.Up, grade, grade + 1, sameLine: true);
        Add(trades, TradeDirection.Down, grade, grade - 1, sameLine: true);
        Add(trades, TradeDirection.Across, grade, grade, sameLine: false);

        return trades;
    }

    private static void Add(List<MaterialTrade> trades, TradeDirection direction, int from, int to, bool sameLine)
    {
        if (EngineeringRules.TradeRate(from, to, sameLine) is { } exchange)
        {
            trades.Add(new MaterialTrade(direction, to, exchange.Paid, exchange.Received));
        }
    }

    /// <summary>
    /// The nearest places for a raw material (landable bodies, richest first) or a high-grade-emission
    /// material (systems). Makes no request and returns <see cref="MaterialPlaces.None"/> when
    /// <paramref name="galaxy"/> is null, which is how the caller says Knowledge.GalaxySearch is off. Throws
    /// <see cref="GalaxyUnavailableException"/> when the search fails. Not for a tick.
    /// </summary>
    public static async Task<MaterialPlaces> NearestAsync(
        string symbol,
        CommanderGameState? state,
        IGalaxyService? galaxy,
        CancellationToken cancellationToken,
        string? near = null)
    {
        if (galaxy is null || MaterialCatalogue.Find(symbol) is not { } material)
        {
            return MaterialPlaces.None;
        }

        near ??= state?.Location.StarSystem;

        if (string.Equals(material.Category, "Raw", StringComparison.OrdinalIgnoreCase))
        {
            return await BodiesAsync(galaxy, material, near, cancellationToken).ConfigureAwait(false);
        }

        if (EmissionRules.Holding(material.Symbol) is { } group)
        {
            return await SystemsAsync(galaxy, group, near, cancellationToken).ConfigureAwait(false);
        }

        return MaterialPlaces.None;
    }

    private static MaterialPlaces Said(MaterialPlaceKind kind, string message) => new(kind, string.Empty, [], message);

    private static async Task<MaterialPlaces> BodiesAsync(
        IGalaxyService galaxy,
        MaterialEntry material,
        string? near,
        CancellationToken cancellationToken)
    {
        if (BodyCatalogue.MatchSurfaceMaterial(material.Name) is not { } indexed)
        {
            return Said(
                MaterialPlaceKind.Body,
                $"The body index does not carry {material.Name}, so I cannot search for it — that is a "
                + "gap in the index rather than a shortage in the galaxy.");
        }

        var result = await galaxy
            .FindBodiesAsync(BodyQuery.ForMaterial(near, indexed, maxDistance: 50, size: 20), cancellationToken)
            .ConfigureAwait(false);

        if (result.Bodies.Count == 0)
        {
            return Said(
                MaterialPlaceKind.Body,
                $"No landable body within 50 light years is recorded as carrying {material.Name}.");
        }

        // The index cannot sort or filter on share, so the ranking is over the bodies fetched.
        var ranked = result.Bodies
            .Select(body => (Body: body, Share: body.Materials
                .Where(entry => string.Equals(entry.Name, indexed, StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.Share)
                .DefaultIfEmpty(0)
                .Max()))
            .OrderByDescending(pair => pair.Share)
            .Take(5)
            .Select(pair => new MaterialPlace(
                pair.Body.Name,
                pair.Body.SystemName,
                pair.Share.ToString("0.0", CultureInfo.InvariantCulture) + "%",
                pair.Body.Distance))
            .ToArray();

        return new MaterialPlaces(
            MaterialPlaceKind.Body,
            $"Richest of the {result.Bodies.Count} nearest landable bodies carrying it"
            + (result.Reference is { } reference ? $", from {reference}:" : ":"),
            ranked,
            null);
    }

    private static async Task<MaterialPlaces> SystemsAsync(
        IGalaxyService galaxy,
        EmissionGroup group,
        string? near,
        CancellationToken cancellationToken)
    {
        var requested = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["distance"] = "50",
            ["allegiance"] = group.Allegiance,
        };

        if (group.States.Count > 0)
        {
            requested["state"] = string.Join(",", group.States.Select(FarmingAdvice.Spaced));
        }

        // Asked for well beyond the five kept, because the population floor is applied afterwards.
        if (!GalaxyQuery.TryParse(near, requested, size: 50, out var query, out var failure))
        {
            return Said(MaterialPlaceKind.System, failure);
        }

        var result = await galaxy.SearchAsync(query, cancellationToken).ConfigureAwait(false);

        var populous = result.Systems
            .Where(system => system.Population is { } people && people >= EmissionRules.MinimumPopulation)
            .Take(5)
            .ToList();

        var described =
            $"{group.Allegiance}-aligned"
            + (group.States.Count > 0
                ? $", in {string.Join(" or ", group.States.Select(FarmingAdvice.Spaced))}"
                : string.Empty)
            + $", over {EmissionRules.MinimumPopulation.ToString("N0", CultureInfo.InvariantCulture)} people";

        if (populous.Count == 0)
        {
            return Said(MaterialPlaceKind.System, $"No system within 50 light years is {described}.");
        }

        return new MaterialPlaces(
            MaterialPlaceKind.System,
            $"Nearest systems reported {described}:",
            [.. populous.Select(system => new MaterialPlace(
                system.Name,
                system.Name,
                system.Population is { } population
                    ? "population " + population.ToString("N0", CultureInfo.InvariantCulture)
                    : string.Empty,
                system.Distance))],
            null);
    }
}
