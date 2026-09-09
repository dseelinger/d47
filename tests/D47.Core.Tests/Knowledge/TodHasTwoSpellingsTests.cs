using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>One engineer is spelled two ways by two upstreams.</summary>
public class TodHasTwoSpellingsTests
{
    private static Engineer Tod =>
        EngineerDirectory.ByName("Tod 'The Blaster' McQuinn")
        ?? throw new InvalidOperationException("The directory has no Tod at all, which is a different defect.");

    /// <summary>The premise.</summary>
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

    /// <summary>The fix itself: the recipe table's spelling resolves to the directory's row.</summary>
    [Fact]
    public void TheRecipeSpellingResolvesToTheDirectoryRow()
    {
        Assert.True(EngineerDirectory.IsNamedIn(["Tod McQuinn"], Tod));

        // And the directory's own spelling still works, since the recipe table is not the only caller: a name
        // arriving from anywhere else must not be broken by fixing this one.
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

    /// <summary>Nobody else is matched by accident.</summary>
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

    /// <summary>The gate.</summary>
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
