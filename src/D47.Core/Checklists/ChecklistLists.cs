using System.Globalization;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Checklists;

/// <summary>The group a list sits under on the Lists page, in reading order.</summary>
public enum ChecklistListGroup
{
    Yours,
    Ships,
    Systems,
    SuitsAndWeapons,
    NothingOpen,
}

/// <summary>One list of lines: the Commander's notes, an engineer's unlocks, a ship, a system, a suit or a weapon.</summary>
/// <param name="Id">Stable across redraws: <c>notes</c>, <c>engineers</c>, or the scope's ordering key.</param>
/// <param name="IsEngineerUnlocks">Whether this is the derived Universal list of engineer prerequisites.</param>
/// <param name="Name">The ship's name, the system, the suit or weapon, "Your notes" or "Engineer unlocks".</param>
/// <param name="Kind">The line under the name: the hull, "Colonisation", "Suit · G3".</param>
/// <param name="Open">Lines not yet Done.</param>
/// <param name="Next">The first open line as said, or null where none is open.</param>
/// <param name="Home">The group the list belongs to when it has something open.</param>
public sealed record ChecklistList(
    string Id,
    ChecklistScope Scope,
    bool IsEngineerUnlocks,
    string Name,
    string Kind,
    int Open,
    int Done,
    string? Next,
    bool IsHere,
    ChecklistListGroup Home)
{
    /// <summary>Where the list is shown: its home group, or Nothing open when every line is done.</summary>
    public ChecklistListGroup Group => Open == 0 ? ChecklistListGroup.NothingOpen : Home;
}

/// <summary>Builds <see cref="ChecklistList"/>s from the arranged lines.</summary>
public static class ChecklistLists
{
    public const string NotesId = "notes";

    public const string EngineersId = "engineers";

    /// <summary>The list a line belongs to, or null for a mission line, which is in no list.</summary>
    public static string? IdOf(ChecklistItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.Source == ChecklistSource.Mission)
        {
            return null;
        }

        if (item.Scope.Group != ChecklistGroup.Universal)
        {
            return ChecklistOrdering.Key(item.Scope);
        }

        return item.Source == ChecklistSource.EngineerPrerequisite ? EngineersId : NotesId;
    }

    /// <summary>Lists in page order: groups in reading order, here first, most open first, then Nothing open.</summary>
    public static IReadOnlyList<ChecklistList> Build(IReadOnlyList<ChecklistItem> arranged, CommanderGameState? state)
    {
        ArgumentNullException.ThrowIfNull(arranged);

        var built = new List<ChecklistList>();

        foreach (var lines in arranged.Where(item => IdOf(item) is not null).GroupBy(item => IdOf(item)!))
        {
            built.Add(Make(lines.Key, [.. lines], state));
        }

        var appeared = built.Select((list, index) => (list, index)).ToDictionary(e => e.list, e => e.index);

        return
        [
            .. built
                .OrderBy(list => list.Open == 0 ? 1 : 0)
                .ThenBy(list => (int)list.Home)
                .ThenBy(list => list.Home == ChecklistListGroup.Yours ? (list.IsEngineerUnlocks ? 1 : 0) : 0)
                .ThenBy(list => list.IsHere ? 0 : 1)
                .ThenBy(list => list.Home == ChecklistListGroup.Yours ? 0 : -list.Open)
                .ThenBy(list => appeared[list]),
        ];
    }

    private static ChecklistList Make(string id, IReadOnlyList<ChecklistItem> lines, CommanderGameState? state)
    {
        var first = lines[0];
        var scope = first.Scope;
        var open = lines.Where(item => !item.IsComplete).ToList();
        var engineers = id == EngineersId;

        var (name, kind, home) = scope.Group switch
        {
            ChecklistGroup.Universal when engineers =>
                ("Engineer unlocks", "Prerequisites", ChecklistListGroup.Yours),
            ChecklistGroup.Universal =>
                ("Your notes", "Written down · you tick them", ChecklistListGroup.Yours),
            ChecklistGroup.Ship => Ship(first, state),
            ChecklistGroup.System => (scope.Key ?? "A system", "Colonisation", ChecklistListGroup.Systems),
            ChecklistGroup.Suit => Suit(scope, state),
            _ => Weapon(scope, state),
        };

        return new ChecklistList(
            id,
            scope,
            engineers,
            name,
            kind,
            open.Count,
            lines.Count - open.Count,
            open.Count > 0 ? ChecklistWording.Said(open[0], state) : null,
            ChecklistOrdering.IsHere(scope, state),
            home);
    }

    private static (string, string, ChecklistListGroup) Ship(ChecklistItem item, CommanderGameState? state)
    {
        var (called, type) = ChecklistWording.ShipParts(item.Scope, item.Hull, state);
        var name = called ?? ChecklistWording.Where(item.Scope, item.Hull, state);

        return (name, type ?? string.Empty, ChecklistListGroup.Ships);
    }

    private static (string, string, ChecklistListGroup) Suit(ChecklistScope scope, CommanderGameState? state)
    {
        var owned = state is not null
            && long.TryParse(scope.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            && state.Kit.Suits.TryGetValue(id, out var suit)
                ? suit
                : null;

        return (
            owned is null ? scope.ToString() : Said(owned.Symbol),
            Kind("Suit", owned?.Grade),
            ChecklistListGroup.SuitsAndWeapons);
    }

    private static (string, string, ChecklistListGroup) Weapon(ChecklistScope scope, CommanderGameState? state)
    {
        var owned = state is not null
            && long.TryParse(scope.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            && state.Kit.Weapons.TryGetValue(id, out var weapon)
                ? weapon
                : null;

        return (
            owned is null ? scope.ToString() : Said(owned.Symbol),
            Kind("Weapon", owned?.Grade),
            ChecklistListGroup.SuitsAndWeapons);
    }

    private static string Said(string symbol) =>
        OnFootCatalogue.Find(symbol)?.Name ?? ModuleNames.Readable(symbol);

    private static string Kind(string word, int? grade) =>
        grade is { } g ? $"{word} · G{g.ToString(CultureInfo.InvariantCulture)}" : word;
}
