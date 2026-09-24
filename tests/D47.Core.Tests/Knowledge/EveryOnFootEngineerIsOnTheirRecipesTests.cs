using System.Text.RegularExpressions;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public partial class EveryOnFootEngineerIsOnTheirRecipesTests
{
    [GeneratedRegex(@"\s*\([^)]*\)$")]
    private static partial Regex Suffix();

    private static string Unsuffixed(string name) => Suffix().Replace(name, "").Trim();

    private static IEnumerable<Blueprint> OnFoot =>
        BlueprintCatalogue.All.Where(blueprint => blueprint.Kind is BlueprintKind.Suit or BlueprintKind.Weapon);

    [Fact]
    public void EverySpecialityHasARecipeNamingItsEngineer()
    {
        var missing = EngineerDirectory.All
            .Where(engineer => engineer.IsOnFoot)
            .SelectMany(engineer => engineer.Specialities.Select(speciality => (engineer.Name, speciality.Kind)))
            .Where(pair => !OnFoot.Any(recipe =>
                string.Equals(Unsuffixed(recipe.Name), Unsuffixed(pair.Kind), StringComparison.OrdinalIgnoreCase)
                && recipe.Engineers.Contains(pair.Name)))
            .Select(pair => $"{pair.Name}: {pair.Kind}")
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void NightVisionNamesYiShenAsWellAsOdenGeiger()
    {
        var recipe = Assert.Single(OnFoot, blueprint => blueprint.Name == "Night vision");

        Assert.Equal(["Oden Geiger", "Yi Shen"], recipe.Engineers.Order());
    }

    [Fact]
    public void MagazineSizeNamesEleanorBresa()
    {
        var recipe = Assert.Single(OnFoot, blueprint => blueprint.Name == "Magazine size");

        Assert.Equal(["Eleanor Bresa", "Jude Navarro", "Kit Fowler"], recipe.Engineers.Order());
    }
}
