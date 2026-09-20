using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Loadout;

/// <summary>The same-line material one grade up, and what it trades for at the fixed rate.</summary>
public sealed record MaterialTradeDown(MaterialEntry From, MaterialExchange Rate);

/// <summary>How many rows in a card carry one origin.</summary>
public sealed record MaterialOriginCount(string Origin, int Rows);

/// <summary>One catalogue row of a material tracker card, held and needed included even at zero.</summary>
public sealed record MaterialRow(MaterialEntry Material, int Held, int Needed)
{
    /// <summary>Every plan that asked for it, most demanding first.</summary>
    public IReadOnlyList<GapDemand> Wanted { get; init; } = [];

    /// <summary>What a trader could cover the shortfall with, or null.</summary>
    public TradeOffer? Trade { get; init; }

    /// <summary>What one grade up in the same line trades for, or null off that line, at grade 5, or on foot.</summary>
    public MaterialTradeDown? TradeDown { get; init; }

    public int Short => Math.Max(0, Needed - Held);

    /// <summary>The per-grade cap, for ship materials only.</summary>
    public int? Capacity =>
        Material.Ledger == MaterialLedger.Material && Material.Grade is { } grade
            ? MaterialGrades.CapacityOfGrade(grade)
            : null;

    /// <summary>Whether this alone forces more than one trip.</summary>
    public bool ExceedsCapacity => Capacity is { } capacity && Needed > capacity;
}

/// <summary>One group of rows on a material tracker page — a ship grade band, Guardian, Thargoid, or an on-foot kind.</summary>
public sealed record MaterialCard(string Name, IReadOnlyList<MaterialRow> Rows)
{
    public int UnitsShort => Rows.Sum(row => row.Short);

    /// <summary>The three origins carried by the most rows, most first, ties by name.</summary>
    public IReadOnlyList<MaterialOriginCount> Sources { get; init; } = [];
}

/// <summary>Every catalogue material and ship-locker row, grouped into tracker cards.</summary>
public sealed record MaterialTrackerReport(IReadOnlyList<MaterialCard> Ship, IReadOnlyList<MaterialCard> OnFoot);

/// <summary>
/// Every ship material and ship-locker row, held and needed alike, rather than the shortfall
/// <see cref="PlanGap"/> reports (#301).
/// </summary>
public static class MaterialTracker
{
    private static readonly string[] ShipCards = ["Raw", "Manufactured", "Encoded", "Guardian", "Thargoid"];

    private static readonly (string Category, string Name)[] OnFootCards =
    [
        ("Item", "Items"),
        ("Component", "Components"),
        ("Consumable", "Consumables"),
        ("Data", "Data"),
    ];

    public static MaterialTrackerReport Of(CommanderGameState? state, GapReport gap)
    {
        var lines = gap.Ledgers
            .SelectMany(ledger => ledger.Lines)
            .ToDictionary(line => line.Material.Symbol, StringComparer.OrdinalIgnoreCase);

        var ship = ShipCards
            .Select(name => Card(
                name,
                MaterialCatalogue.All
                    .Where(entry => entry.Ledger == MaterialLedger.Material && ShipCardOf(entry) == name)
                    .OrderBy(entry => entry.Grade)
                    .ThenBy(entry => entry.Name, StringComparer.Ordinal),
                state,
                lines,
                tradeDown: true))
            .ToList();

        var onFoot = OnFootCards
            .Select(card => Card(
                card.Name,
                MaterialCatalogue.All
                    .Where(entry => entry.Ledger == MaterialLedger.ShipLocker
                        && string.Equals(entry.Category, card.Category, StringComparison.Ordinal))
                    .OrderBy(entry => entry.Name, StringComparer.Ordinal),
                state,
                lines,
                tradeDown: false))
            .ToList();

        return new MaterialTrackerReport(ship, onFoot);
    }

    private static MaterialCard Card(
        string name,
        IEnumerable<MaterialEntry> entries,
        CommanderGameState? state,
        IReadOnlyDictionary<string, GapLine> lines,
        bool tradeDown)
    {
        var rows = entries.Select(entry => Row(entry, state, lines, tradeDown)).ToList();

        return new MaterialCard(name, rows) { Sources = Sources(rows) };
    }

    private static MaterialRow Row(
        MaterialEntry material,
        CommanderGameState? state,
        IReadOnlyDictionary<string, GapLine> lines,
        bool tradeDown)
    {
        var gapLine = lines.GetValueOrDefault(material.Symbol);

        return new MaterialRow(material, HeldCount(material, state), gapLine?.Needed ?? 0)
        {
            Wanted = gapLine?.Wanted ?? [],
            Trade = gapLine?.Trade,
            TradeDown = tradeDown ? TradeDownFor(material) : null,
        };
    }

    private static int HeldCount(MaterialEntry material, CommanderGameState? state) =>
        state is null
            ? 0
            : material.Ledger == MaterialLedger.Material
                ? state.Materials.CountOf(material.Symbol)
                : state.Suit.CountOf(material.Symbol);

    /// <summary>The catalogue's own grouping, with a Line-less material row reassigned to Guardian or Thargoid.</summary>
    private static string ShipCardOf(MaterialEntry material) =>
        material.Line is not null
            ? material.Category ?? "Unknown"
            : IsThargoid(material.Symbol) ? "Thargoid" : "Guardian";

    private static bool IsThargoid(string symbol) =>
        symbol.StartsWith("tg_", StringComparison.Ordinal) || symbol.StartsWith("unknown", StringComparison.Ordinal);

    private static MaterialTradeDown? TradeDownFor(MaterialEntry material)
    {
        if (!material.IsTradeable || material.Grade is not { } grade || grade >= 5)
        {
            return null;
        }

        var from = MaterialCatalogue.InLine(material.Line).FirstOrDefault(entry => entry.Grade == grade + 1);

        return from is not null && EngineeringRules.TradeRate(grade + 1, grade, sameLine: true) is { } rate
            ? new MaterialTradeDown(from, rate)
            : null;
    }

    /// <summary>The three origins carried by the most rows in the card, most first, ties by name.</summary>
    private static IReadOnlyList<MaterialOriginCount> Sources(IReadOnlyList<MaterialRow> rows) =>
        [.. rows
            .SelectMany(row => row.Material.Origins.Distinct(StringComparer.Ordinal))
            .GroupBy(origin => origin, StringComparer.Ordinal)
            .Select(group => new MaterialOriginCount(group.Key, group.Count()))
            .OrderByDescending(count => count.Rows)
            .ThenBy(count => count.Origin, StringComparer.Ordinal)
            .Take(3)];
}
