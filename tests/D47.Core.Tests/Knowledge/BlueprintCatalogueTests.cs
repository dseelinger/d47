using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The generated blueprint table, against the real embedded resource.
/// <para>
/// The load-bearing property is not that recipes parse — it is that <b>only per-application
/// recipes get multiplied</b>. EDEngineer keeps modifications, experimentals, synthesis, tech
/// broker unlocks and Odyssey vendor stock in one list, and multiplying any of the others by a
/// roll count produces a number with no meaning, stated as confidently as a correct one.
/// </para>
/// </summary>
public class BlueprintCatalogueTests
{
    [Fact]
    public void TheShippedTableCarriesEveryKindOfRecipe()
    {
        var byKind = BlueprintCatalogue.All
            .GroupBy(b => b.Kind)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(786, byKind[BlueprintKind.Modification]);
        Assert.True(byKind[BlueprintKind.Experimental] > 140);
        Assert.True(byKind[BlueprintKind.Synthesis] > 50);
        Assert.True(byKind[BlueprintKind.Unlock] > 20);
        Assert.False(byKind.ContainsKey(BlueprintKind.Unknown));
    }

    [Fact]
    public void EveryIngredientResolvesToAMaterialTheTableKnows()
    {
        // A blueprint asking for something no id list names cannot be totalled, and a short
        // total is worse than none. The generator fails on this; so does the shipped table.
        var unresolved = BlueprintCatalogue.All
            .SelectMany(b => b.Ingredients)
            .Select(i => i.Symbol)
            .Distinct(StringComparer.Ordinal)
            .Where(symbol => MaterialCatalogue.Find(symbol) is null)
            .ToArray();

        Assert.Empty(unresolved);
    }

    [Fact]
    public void ModificationIngredientsArePerApplicationAndNothingElseIs()
    {
        // The rule that makes "ingredients x rolls" exact. 57 graded rows break it and every one
        // is synthesis — munitions, refills, limpets, SRV repair — which is why kind exists.
        var modifications = BlueprintCatalogue.All.Where(b => b.Kind == BlueprintKind.Modification);

        Assert.All(modifications, b => Assert.All(b.Ingredients, i => Assert.Equal(1, i.Size)));

        var synthesis = BlueprintCatalogue.All
            .Where(b => b.Kind == BlueprintKind.Synthesis)
            .SelectMany(b => b.Ingredients);

        Assert.Contains(synthesis, i => i.Size > 1);
    }

    [Fact]
    public void AGradeFiveTotalIsTheRecipeTimesTheRollsThatRankTakes()
    {
        var blueprint = Assert.Single(
            BlueprintCatalogue.Named("Increased FSD Range", "Frame Shift Drive"), b => b.Grade == 5);

        Assert.Equal(3, blueprint.Ingredients.Count);
        Assert.All(blueprint.Ingredients, i => Assert.Equal(1, i.Size));

        // Rank 5 on a grade 5 blueprint is five rolls, so three ingredients become five each.
        var atRankFive = blueprint.TotalFor(5);

        Assert.NotNull(atRankFive);
        Assert.All(atRankFive, i => Assert.Equal(5, i.Size));

        // Rank 4 cannot roll grade 5 at all, which is a gate and not a bigger number.
        Assert.Null(blueprint.TotalFor(4));
    }

    [Fact]
    public void NothingButAModificationCanBeTotalled()
    {
        // Multiplying a synthesis recipe or an engineer's tribute by a roll count is arithmetic
        // on the wrong kind of thing, so it answers null rather than a plausible number.
        foreach (var kind in new[] { BlueprintKind.Synthesis, BlueprintKind.Experimental, BlueprintKind.Unlock })
        {
            var recipe = BlueprintCatalogue.All.First(b => b.Kind == kind);

            Assert.Null(recipe.TotalFor(5));
        }
    }

    [Fact]
    public void AnExperimentalHasADifferentRecipePerModuleType()
    {
        // The reason coriolis cannot be the authority here. It models one recipe per effect and
        // the game does not — Double Braced has eight, and a beam laser's is not a power plant's.
        var beamLaser = Assert.Single(BlueprintCatalogue.Named("Double Braced", "Beam Laser"));
        var powerPlant = Assert.Single(BlueprintCatalogue.Named("Double Braced", "Power Plant"));

        Assert.Equal(BlueprintKind.Experimental, beamLaser.Kind);
        Assert.NotEqual(
            beamLaser.Ingredients.Select(i => i.Symbol).Order(StringComparer.Ordinal),
            powerPlant.Ingredients.Select(i => i.Symbol).Order(StringComparer.Ordinal));

        // And the module's own list is what a Commander can actually apply to it.
        Assert.Contains(BlueprintCatalogue.ExperimentalsFor("Beam Laser"), b => b.Name == "Double Braced");
        Assert.DoesNotContain(BlueprintCatalogue.ExperimentalsFor("Beam Laser"), b => b.Grade is not null);
    }

    [Fact]
    public void TheSameBlueprintNameOnDifferentModulesIsDifferentBlueprints()
    {
        // "Lightweight" is a dozen different recipes. Answering with one of them because the
        // name matched would be a wrong ingredient list delivered confidently.
        var everywhere = BlueprintCatalogue.Named("Lightweight");
        var onArmour = BlueprintCatalogue.Named("Lightweight", "Armour");

        Assert.True(everywhere.Select(b => b.Module).Distinct().Count() > 3);
        Assert.All(onArmour, b => Assert.Equal("Armour", b.Module));
        Assert.Equal([1, 2, 3, 4, 5], onArmour.Select(b => b.Grade));
    }

    [Fact]
    public void ABlueprintSaysWhatItDoesAndWhoOffersIt()
    {
        var blueprint = Assert.Single(
            BlueprintCatalogue.Named("Increased FSD Range", "Frame Shift Drive"), b => b.Grade == 5);

        Assert.Contains("Felicity Farseer", blueprint.Engineers);

        var optimalMass = blueprint.Effects.Single(e => e.Property == "Optimal Mass");

        Assert.Equal("+55%", optimalMass.Change);
        Assert.True(optimalMass.IsGood);

        // Long range costs power draw, integrity and mass. A blueprint that only listed its
        // upside would read as free.
        Assert.Contains(blueprint.Effects, e => e is { Property: "Mass", IsGood: false });
    }

    [Fact]
    public void AnIngredientCarriesItsMaterialSoALedgerMistakeIsImpossible()
    {
        // Gold x200 in a tribute is two hundred tonnes of cargo, and the ingredient can say so
        // without the caller having to look it up separately.
        var tribute = BlueprintCatalogue.All
            .First(b => b.Kind == BlueprintKind.Unlock && b.Name.Contains("Lei Cheung", StringComparison.Ordinal));

        var gold = tribute.Ingredients.Single(i => i.Symbol == "gold");

        Assert.Equal(200, gold.Size);
        Assert.Equal(MaterialLedger.Cargo, gold.Material?.Ledger);
    }

    [Fact]
    public void ABlueprintIsFoundByWhatAPersonWouldSay()
    {
        // Through the same relaxed matching every other name in this namespace uses.
        Assert.NotEmpty(BlueprintCatalogue.Named("increased fsd range", "frame shift drive"));
        Assert.NotEmpty(BlueprintCatalogue.ForModule("frame shift drive"));
        Assert.Empty(BlueprintCatalogue.Named("a blueprint nobody has heard of"));
        Assert.Empty(BlueprintCatalogue.ForModule(null));
    }

    [Fact]
    public void TheTickMarkEffectsSurvivedTheirEncoding()
    {
        // EDEngineer's Effects were recorded as double-encoded, arriving as "âœ“". They are not:
        // the file carries real U+2713 and reads cleanly as UTF-8, and âœ“ is exactly what those
        // bytes look like decoded as cp1252. This is the check that the table was read and
        // written as UTF-8 all the way through.
        var ticks = BlueprintCatalogue.All
            .SelectMany(b => b.Effects)
            .Where(e => e.Change.Contains('✓'))
            .ToArray();

        Assert.NotEmpty(ticks);
        Assert.DoesNotContain(
            BlueprintCatalogue.All.SelectMany(b => b.Effects),
            e => e.Change.Contains('Â') || e.Change.Contains('â'));
    }
}
