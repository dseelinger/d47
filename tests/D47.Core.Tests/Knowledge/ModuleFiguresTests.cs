using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// What tells two modules apart, and what a blueprint does — asserted against the shipped tables.
/// <para>
/// Remediation 15 items 2b, 8 and 9. Reported as "I should be able to tell the first two lasers
/// apart by something besides the price. The more expensive one should be more powerful, and I
/// should know how", and "what's special about a Guardian Distributor? It should say".
/// </para>
/// </summary>
public class ModuleFiguresTests
{
    private static string Figure(string symbol, string name) =>
        EliteSpecifications.Module(symbol)?.Figures
            .FirstOrDefault(pair => pair.Name == name).Value ?? string.Empty;

    [Fact]
    public void DamagePerSecondMatchesAnIndependentReference()
    {
        // The formula is coriolis's own, lifted from MIT-licensed code rather than derived here,
        // and this is the check that the reading was right: EDOMH shows a 1G turreted Pulse Laser
        // at 3.97HP/s from a rate of fire of 3.33/s, and the table computes 3.97 from a damage of
        // 1.19 and a fire interval of 0.3.
        Assert.Equal("3.97", Figure("hpt_pulselaser_turret_small", "damage per second"));
        Assert.Equal("1.19", Figure("hpt_pulselaser_turret_small", "damage"));
        Assert.Equal("0.3", Figure("hpt_pulselaser_turret_small", "fire interval"));
    }

    [Fact]
    public void AContinuousWeaponsDamageIsAlreadyPerSecond()
    {
        // Eighteen weapons carry no fire interval at all — every beam laser and every mining
        // laser — because they do not fire shots. Dividing by a missing interval would be
        // arithmetic on nothing; coriolis's own fallback lands on damage unchanged.
        var beam = EliteSpecifications.Module("hpt_beamlaser_fixed_small");

        Assert.NotNull(beam);
        Assert.Equal(string.Empty, Figure("hpt_beamlaser_fixed_small", "fire interval"));
        Assert.Equal(
            Figure("hpt_beamlaser_fixed_small", "damage"),
            Figure("hpt_beamlaser_fixed_small", "damage per second"));
    }

    [Fact]
    public void DamageTypeIsAProportionExceptWhereItIsAMarker()
    {
        // 156 of 178 distributions are real proportions and 22 are not: those 22 carry `X` for
        // anti-xeno and sum to 2, so `X` sits beside a real type rather than taking a share of it.
        // Rendering it as a percentage would print "100% anti-xeno, 100% kinetic".
        Assert.Equal("thermal", Figure("hpt_pulselaser_turret_small", "damage type"));
        Assert.Equal("67% thermal, 33% kinetic", Figure("hpt_railgun_fixed_small", "damage type"));

        var xeno = Figure("hpt_atmulticannon_fixed_medium", "damage type");

        Assert.Contains("effective against Thargoids", xeno, StringComparison.Ordinal);
        Assert.DoesNotContain("%", xeno, StringComparison.Ordinal);
    }

    [Fact]
    public void ADistributorCarriesDistributorFiguresRatherThanWeaponOnes()
    {
        // The correction item 9 made to item 2b: the first list of figures was hardpoint-specific
        // and applied to no distributor at all. Per kind, not one set for everything.
        Assert.Equal("43", Figure("int_guardianpowerdistributor_size7", "weapons capacity"));
        Assert.Equal("8.5", Figure("int_guardianpowerdistributor_size7", "weapons recharge"));

        // And the standard one, so the Guardian's trade is legible: smaller capacitors, faster
        // recharge, which is exactly what Frontier's own description says it does.
        Assert.Equal("61", Figure("int_powerdistributor_size7_class5", "weapons capacity"));
        Assert.Equal("6.1", Figure("int_powerdistributor_size7_class5", "weapons recharge"));

        Assert.Equal(string.Empty, Figure("int_guardianpowerdistributor_size7", "damage"));
    }

    [Fact]
    public void FrontiersOwnDescriptionAnswersWhatIsSpecialAboutIt()
    {
        // Ask #10, answered in one sentence from an id-keyed field, in Frontier's words — which is
        // the shape the game-data invariant prefers and NOTICE already practises.
        var guardian = EliteSpecifications.Module("int_guardianpowerdistributor_size7");

        Assert.NotNull(guardian);
        Assert.Contains("Guardian technology", guardian.About!, StringComparison.Ordinal);
        Assert.Contains("smaller capacitors", guardian.About!, StringComparison.Ordinal);

        // Most modules have one, and a module without one says nothing rather than inventing.
        var described = EliteSpecifications.Modules.Count(module => module.About is { Length: > 0 });

        Assert.True(described > 600, $"{described} modules carry a description");
    }

    [Fact]
    public void ABlueprintSaysWhatItDoesGainsBeforeCosts()
    {
        // Item 8, and the ordering is free: the effects column carries a good-or-bad flag per line,
        // so d47 never has to be taught which way is better.
        var lightweight = BlueprintCatalogue.All
            .Where(recipe => recipe.Module == "Armour" && recipe.Name == "Lightweight")
            .OrderByDescending(recipe => recipe.Grade ?? 0)
            .First();

        var said = lightweight.Describe();

        Assert.Contains("at the cost of", said, StringComparison.Ordinal);

        // Frontier's own attribute names, which match the outfitting screen — the same argument the
        // slot headings already won.
        Assert.Contains("Mass", said, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTopGradeSaysEverythingTheOtherGradesDo()
    {
        // The one thing item 8 said to check rather than assume. 34 of 160 module-and-blueprint
        // pairs change their attribute set across grades, and in every one the highest grade's set
        // contains all the others — so the general line is the top grade's, with real numbers,
        // rather than a union of five.
        var short_ = BlueprintCatalogue.All
            .Where(recipe => recipe.Kind == BlueprintKind.Modification && recipe.Grade is not null)
            .GroupBy(recipe => (recipe.Module, recipe.Name));

        foreach (var group in short_)
        {
            var top = group.OrderByDescending(recipe => recipe.Grade).First()
                .Effects.Select(effect => effect.Property).ToHashSet(StringComparer.Ordinal);

            var union = group.SelectMany(recipe => recipe.Effects)
                .Select(effect => effect.Property).ToHashSet(StringComparer.Ordinal);

            Assert.True(
                union.IsSubsetOf(top),
                $"{group.Key.Module} / {group.Key.Name}: {string.Join(", ", union.Except(top))} "
                + "appears at a lower grade and not at the top one");
        }
    }
}
