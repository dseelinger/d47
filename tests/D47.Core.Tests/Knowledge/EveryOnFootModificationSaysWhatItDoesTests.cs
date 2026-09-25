using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// Each on-foot modification carries one sentence on what it does, and the tool says it (#466).
/// </summary>
public class EveryOnFootModificationSaysWhatItDoesTests
{
    [Fact]
    public void EveryModificationRowHasASentence()
    {
        var modifications = OnFootCatalogue.All.Where(entry => entry.IsModification).ToArray();

        Assert.Equal(25, modifications.Length);
        Assert.All(modifications, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Detail), entry.Symbol));
    }

    [Fact]
    public void EveryRecipeOfAModificationFindsItsSentence()
    {
        var modifications = OnFootCatalogue.All.Where(entry => entry.IsModification).ToArray();

        Assert.All(modifications, entry =>
        {
            var recipes = OnFootCatalogue.RecipesFor(entry.Symbol);

            Assert.NotEmpty(recipes);
            Assert.All(recipes, recipe => Assert.Equal(entry.Detail, OnFootCatalogue.WhatItDoes(recipe.Name)));
        });
    }

    [Fact]
    public void AManufacturerRecipeFindsTheFamilySentence() =>
        Assert.Equal(
            "Reduces weapon sway, improving accuracy",
            OnFootCatalogue.WhatItDoes("Improved Hip Fire Accuracy (Kinematic kinetic)"));

    [Fact]
    public void FasterHandlingShortensTheTimeRatherThanTheSpeed() =>
        Assert.Contains("draw/stow time", OnFootCatalogue.WhatItDoes("Faster handling"), StringComparison.Ordinal);

    [Fact]
    public async Task TheToolSaysWhatStowedReloadingDoesOnce()
    {
        var content = await Ask("Stowed reloading");

        var does = content.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("Does:", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(["Does: Automatically reloads a stowed weapon after 5 seconds"], does);
    }

    [Fact]
    public async Task HigherAccuracyAsksWhichWeapons() =>
        Assert.Equal("Higher Accuracy — Kinetic, Plasma, or Thermal weapons?", await Ask("Higher Accuracy"));

    [Fact]
    public async Task NamingTheWeaponsAnswersForThatRecipe() =>
        Assert.StartsWith(
            "Higher Accuracy (Kinetic Weapons) — a weapon modification.",
            await Ask("Higher Accuracy kinetic"),
            StringComparison.Ordinal);

    [Fact]
    public async Task AFragmentAcrossTwoModificationsIsNotAskedAboutAsOne() =>
        Assert.DoesNotContain(" — ", await Ask("accuracy"), StringComparison.Ordinal);

    private static async Task<string> Ask(string modification)
    {
        var tool = Assert.Single(
            OnFootCapability.Create(() => null).Tools, tool => tool.Name == "get_on_foot_engineering");

        var result = await tool.Handler(
            new ToolArguments(new Dictionary<string, string> { ["modification"] = modification }),
            CancellationToken.None);

        return result.Content;
    }
}
