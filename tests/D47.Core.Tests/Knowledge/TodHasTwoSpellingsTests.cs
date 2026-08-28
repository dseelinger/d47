using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// One engineer is spelled two ways by two upstreams, and three call sites used to compare those
/// strings directly (#133). <c>Engineers.tsv</c> takes identity from EDCD/FDevIDs and says
/// <c>Tod 'The Blaster' McQuinn</c>; <c>Blueprints.tsv</c> takes recipes from EDEngineer and says
/// <c>Tod McQuinn</c>. Neither is wrong and the join was.
/// <para>
/// These assert the fix at the <b>join</b>, which is where the report's symptom lived: standing in
/// Wolf 397 with multi-cannon work planned, the <i>"What Tod 'The Blaster' McQuinn can do here"</i>
/// filter was never offered, because no recipe row matched him for any blueprint in any system.
/// </para>
/// </summary>
public class TodHasTwoSpellingsTests
{
    private static Engineer Tod =>
        EngineerDirectory.ByName("Tod 'The Blaster' McQuinn")
        ?? throw new InvalidOperationException("The directory has no Tod at all, which is a different defect.");

    /// <summary>
    /// The premise. If this ever fails the two upstreams have converged, and the rest of these
    /// tests are asserting something that can no longer happen — which is worth being told about
    /// rather than discovering as three green tests that prove nothing.
    /// </summary>
    [Fact]
    public void TheTwoTablesStillSpellHimDifferently()
    {
        Assert.Equal("Tod 'The Blaster' McQuinn", Tod.Name);

        var recipeSpellings = BlueprintCatalogue.All
            .SelectMany(blueprint => blueprint.Engineers)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.Contains("Tod McQuinn", recipeSpellings);
        Assert.DoesNotContain("Tod 'The Blaster' McQuinn", recipeSpellings);
    }

    /// <summary>
    /// The fix itself: the recipe table's spelling resolves to the directory's row. A caller that
    /// goes back to comparing raw strings fails here rather than shipping a silently empty filter.
    /// </summary>
    [Fact]
    public void TheRecipeSpellingResolvesToTheDirectoryRow()
    {
        Assert.True(EngineerDirectory.IsNamedIn(["Tod McQuinn"], Tod));

        // And the directory's own spelling still works, since the recipe table is not the only
        // caller: a name arriving from anywhere else must not be broken by fixing this one.
        Assert.True(EngineerDirectory.IsNamedIn(["Tod 'The Blaster' McQuinn"], Tod));
    }

    /// <summary>
    /// The symptom, one level up from the string: he grades multi-cannons to 5, and until this was
    /// fixed his ceiling for every one of them was null.
    /// </summary>
    [Fact]
    public void HeHasACeilingForTheWorkHeActuallyDoes()
    {
        var his = BlueprintCatalogue.All
            .Where(blueprint => EngineerDirectory.IsNamedIn(blueprint.Engineers, Tod))
            .ToList();

        Assert.NotEmpty(his);
        Assert.Contains(his, blueprint => blueprint.Grade == 5);
    }

    /// <summary>
    /// Nobody else is matched by accident. The resolver goes through the spoken-name matcher, which
    /// is deliberately forgiving, so the risk of fixing this by matching is that some other pair of
    /// names now collides — a recipe naming one engineer must name exactly that one.
    /// </summary>
    [Fact]
    public void ARecipeNamesTheOneEngineerItMeans()
    {
        foreach (var spelling in BlueprintCatalogue.All
                     .SelectMany(blueprint => blueprint.Engineers)
                     .Distinct(StringComparer.Ordinal))
        {
            var matched = EngineerDirectory.All
                .Where(engineer => EngineerDirectory.IsNamedIn([spelling], engineer))
                .ToList();

            Assert.True(
                matched.Count == 1,
                $"'{spelling}' resolves to {matched.Count} engineers: {string.Join(", ", matched.Select(e => e.Name))}");
        }
    }

    /// <summary>
    /// <b>The gate.</b> This was found by hand, from a screenshot and a bug report, and should not
    /// need finding by hand again: every engineer the recipe table names has a row in the directory.
    /// A fourth spelling arriving from either upstream fails here.
    /// <para>
    /// The reverse does not hold and must not be asserted: Baltanos, Eleanor Bresa, Rosa Dayette
    /// and Yi Shen are in the directory and in no recipe, because they legitimately grade no ship
    /// module.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryBlueprintEngineerIsInTheDirectory()
    {
        var unresolved = BlueprintCatalogue.All
            .SelectMany(blueprint => blueprint.Engineers)
            .Distinct(StringComparer.Ordinal)
            .Where(spelling => EngineerDirectory.ByName(spelling) is null)
            .OrderBy(spelling => spelling, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            unresolved.Count == 0,
            "Blueprints.tsv names engineers the directory has no row for: " + string.Join(", ", unresolved));
    }
}
