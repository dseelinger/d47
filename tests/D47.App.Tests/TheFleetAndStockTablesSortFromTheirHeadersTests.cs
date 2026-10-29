using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using Xunit;

namespace D47.App.Tests;

/// <summary>Stored modules, Materials, Crew and Commanders sort from their column headers (#1115).</summary>
public sealed class TheFleetAndStockTablesSortFromTheirHeadersTests
{
    private static IReadOnlyList<StoredModulesPage.Held> Held(bool here, params StoredModule[] modules) =>
        [.. modules.Select(module => new StoredModulesPage.Held(module, here))];

    private static StoredModule Module(string name, string? moduleClass = null, long? cost = null, int? time = null) =>
        new(name, "Sol") { Class = moduleClass, TransferCost = cost, TransferTime = time };

    [Fact]
    public void TransferCostOrdersAGroupCheapestFirstAndFreeModulesLead()
    {
        var rows = Held(true, Module("A", cost: 900), Module("B"), Module("C", cost: 50));

        var sorted = ColumnSort.Sorted(rows, StoredModulesPage.ColumnHeadings, new SortOrder(2, SortDirection.Ascending));

        Assert.Equal(["B", "C", "A"], sorted.Select(row => row.Module.Name));
    }

    [Fact]
    public void ClassOrdersBySizeAndRatingNotAlphabetically()
    {
        var rows = Held(false, Module("A", "5A"), Module("B", "6E"), Module("C", "5B"), Module("D"));

        var sorted = ColumnSort.Sorted(rows, StoredModulesPage.ColumnHeadings, new SortOrder(1, SortDirection.Descending));

        Assert.Equal(["B", "A", "C", "D"], sorted.Select(row => row.Module.Name));
    }

    [Fact]
    public void ShortOrdersMaterialsByTheLargestShortfallFirst()
    {
        static GapBuildLine Line(string name, int needed, int held) =>
            new(new MaterialEntry { Symbol = name.ToLowerInvariant(), Name = name }, needed, held);

        IReadOnlyList<GapBuildLine> lines = [Line("Iron", 10, 9), Line("Gold", 10, 0), Line("Tin", 5, 5)];

        var sorted = ColumnSort.Sorted(lines, MaterialsPage.ColumnHeadings, new SortOrder(3, SortDirection.Descending));

        Assert.Equal(["Gold", "Iron", "Tin"], sorted.Select(line => line.Material.Name));
    }

    [Fact]
    public void HeldAndNeedSortsByHeld()
    {
        static GapBuildLine Line(string name, int needed, int held) =>
            new(new MaterialEntry { Symbol = name.ToLowerInvariant(), Name = name }, needed, held);

        IReadOnlyList<GapBuildLine> lines = [Line("Iron", 50, 2), Line("Gold", 3, 7)];

        var sorted = ColumnSort.Sorted(lines, MaterialsPage.ColumnHeadings, new SortOrder(2, SortDirection.Descending));

        Assert.Equal(["Gold", "Iron"], sorted.Select(line => line.Material.Name));
    }

    [Fact]
    public void CombatRankOrdersCrewByRankHighestFirst()
    {
        IReadOnlyList<CrewMember> crew =
        [
            new("Ada", 1, 3),
            new("Bo", 2, 7),
            new("Cy", 3, 4),
            new("Di", 4, 8),
            new("Ed", 5),
        ];

        var sorted = ColumnSort.Sorted(crew, CrewPage.ColumnHeadings, new SortOrder(1, SortDirection.Descending));

        Assert.Equal(["Di", "Bo", "Cy", "Ada", "Ed"], sorted.Select(member => member.Name));
    }

    [Fact]
    public void NameOrdersCommandersAndAThirdPressReturnsToLastDetected()
    {
        var now = DateTimeOffset.UtcNow;
        IReadOnlyList<CommanderSighting> roster =
        [
            new("F1", "Zed", now, "Sol", null),
            new("F3", "Moe", now.AddDays(-1), "Sol", null),
            new("F2", "Amy", now.AddDays(-2), "Sol", null),
        ];

        var press = ColumnSort.Next(CommandersPage.Heads, null, 0);
        Assert.Equal(["Amy", "Moe", "Zed"], ColumnSort.Sorted(roster, CommandersPage.Heads, press).Select(s => s.Name));

        press = ColumnSort.Next(CommandersPage.Heads, press, 0);
        Assert.Equal(["Zed", "Moe", "Amy"], ColumnSort.Sorted(roster, CommandersPage.Heads, press).Select(s => s.Name));

        press = ColumnSort.Next(CommandersPage.Heads, press, 0);
        Assert.Null(press);
        Assert.Equal(["Zed", "Moe", "Amy"], ColumnSort.Sorted(roster, CommandersPage.Heads, press).Select(s => s.Name));
    }
}
