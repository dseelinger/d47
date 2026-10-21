using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests;

public sealed class NotableBodiesTests
{
    private static readonly string[] Premium = ["carbon", "germanium", "arsenic", "niobium", "yttrium", "polonium"];

    private static BodyScan Body(string name = "A 1", int id = 1) => new(name) { SystemAddress = 1, BodyId = id };

    private static IReadOnlyList<NotableKind> Kinds(BodyScan body, params BodyScan[] others) =>
        [.. NotableBodies.Of(body, [body, .. others]).Select(trait => trait.Kind)];

    private static BodyScan Twin(string name, int id, double axis, double radius, double e = 0) =>
        Body(name, id) with { Parents = [("Null", 10)], SemiMajorAxis = axis, Radius = radius, Eccentricity = e };

    [Fact]
    public void ABinaryThatOverlapsAtClosestApproachCollides()
    {
        var a = Twin("A", 1, 500, 600);
        var b = Twin("B", 2, 500, 600);

        Assert.Equal("B", Assert.Single(NotableBodies.Of(a, [a, b])).Detail);
        Assert.Equal("A", Assert.Single(NotableBodies.Of(b, [a, b])).Detail);
    }

    [Fact]
    public void ABinaryThatClearsAtClosestApproachDoesNotCollide()
    {
        var a = Twin("A", 1, 500, 500);
        var b = Twin("B", 2, 500, 500);

        Assert.Empty(Kinds(a, b));
    }

    [Fact]
    public void EccentricityBringsABinaryToCollide()
    {
        var a = Twin("A", 1, 500, 600, 0.5);
        var b = Twin("B", 2, 500, 600, 0.5);

        Assert.Contains(NotableKind.CollidingBinary, Kinds(a, b));
    }

    [Fact]
    public void AThirdBodyOnTheBarycentreEndsTheCollision()
    {
        var a = Twin("A", 1, 500, 600);
        var b = Twin("B", 2, 500, 600);
        var c = Twin("C", 3, 5000, 10);

        Assert.DoesNotContain(NotableKind.CollidingBinary, Kinds(a, b, c));
    }

    [Fact]
    public void AnUnscannedPartnerMeansNoCollision() => Assert.Empty(Kinds(Twin("A", 1, 500, 600)));

    [Fact]
    public void AMoonInsideItsPlanetsRingIsAShepherd()
    {
        var planet = Body("P", 5) with { Rings = [("P A Ring", "Rocky", 100, 1000)] };
        var moon = Body("M", 6) with { Parents = [("Planet", 5)], SemiMajorAxis = 999 };

        Assert.Equal([NotableKind.ShepherdMoon], Kinds(moon, planet));
    }

    [Fact]
    public void AMoonOutsideTheRingIsNotAShepherd()
    {
        var planet = Body("P", 5) with { Rings = [("P A Ring", "Rocky", 100, 1000)] };
        var moon = Body("M", 6) with { Parents = [("Planet", 5)], SemiMajorAxis = 1000 };

        Assert.Empty(Kinds(moon, planet));
    }

    [Fact]
    public void ABeltDoesNotMakeAShepherd()
    {
        var planet = Body("P", 5) with { Rings = [("P A Belt", "Rocky", 100, 1000)] };
        var moon = Body("M", 6) with { Parents = [("Planet", 5)], SemiMajorAxis = 500 };

        Assert.Empty(Kinds(moon, planet));
    }

    [Fact]
    public void AnUnscannedParentMeansNoShepherd() =>
        Assert.Empty(Kinds(Body("M", 6) with { Parents = [("Planet", 5)], SemiMajorAxis = 500 }));

    [Fact]
    public void AMoonOfAMoonIsNested()
    {
        Assert.Equal(
            [NotableKind.NestedMoon],
            Kinds(Body() with { Parents = [("Planet", 2), ("Planet", 3), ("Star", 0)] }));
        Assert.Empty(Kinds(Body() with { Parents = [("Planet", 2), ("Star", 0)] }));
    }

    [Theory]
    [InlineData(0.8, true)]
    [InlineData(0.79, false)]
    public void EccentricityIsNotableFromPointEight(double eccentricity, bool notable) =>
        Assert.Equal(notable, Kinds(Body() with { Eccentricity = eccentricity }).Contains(NotableKind.EccentricOrbit));

    [Theory]
    [InlineData(21599, true)]
    [InlineData(-21599, true)]
    [InlineData(21600, false)]
    public void ASpinUnderSixHoursIsFast(double period, bool notable) =>
        Assert.Equal(notable, Kinds(Body() with { RotationPeriod = period }).Contains(NotableKind.FastRotator));

    [Fact]
    public void AFastSpinNamesItsHoursAndATidalLockIsNotFast()
    {
        Assert.Equal("2", Assert.Single(NotableBodies.Of(Body() with { RotationPeriod = -7200 }, [])).Detail);
        Assert.Empty(Kinds(Body() with { RotationPeriod = 100, TidalLock = true }));
    }

    [Theory]
    [InlineData(101, true)]
    [InlineData(100, false)]
    public void ARingWiderThanTenRadiiIsWide(double outer, bool notable) =>
        Assert.Equal(
            notable,
            Kinds(Body() with { Radius = 10, Rings = [("A A Ring", "Icy", 900, 900 + outer)] })
                .Contains(NotableKind.WideRings));

    [Fact]
    public void AWideBeltIsNotWide() =>
        Assert.Empty(Kinds(Body() with { Radius = 10, Rings = [("A A Belt", "Icy", 0, 5000)] }));

    [Fact]
    public void SixPremiumMaterialsNameNothingMissing()
    {
        var body = Body() with { Landable = true, Materials = [.. Premium.Select(name => (name, 1.0))] };

        var trait = Assert.Single(NotableBodies.Of(body, []));

        Assert.Equal(NotableKind.PremiumFsdInjection, trait.Kind);
        Assert.Null(trait.Detail);
    }

    [Fact]
    public void FivePremiumMaterialsNameTheMissingOne()
    {
        var body = Body() with { Landable = true, Materials = [.. Premium.Take(5).Select(name => (name, 1.0))] };

        Assert.Equal("Polonium", Assert.Single(NotableBodies.Of(body, [])).Detail);
    }

    [Fact]
    public void FourPremiumMaterialsOrAnUnlandableBodyAreNotNotable()
    {
        var materials = Premium.Take(4).Select(name => (name, 1.0)).ToArray();

        Assert.Empty(Kinds(Body() with { Landable = true, Materials = materials }));
        Assert.Empty(Kinds(Body() with { Materials = [.. Premium.Select(name => (name, 1.0))] }));
    }

    [Fact]
    public void ThePremiumRecipeIsFoundInTheCatalogue() =>
        Assert.Equal(
            Premium.Order(),
            BlueprintCatalogue.All
                .Single(blueprint =>
                    blueprint.Kind == BlueprintKind.Synthesis && blueprint.Module == "FSD Injection" && blueprint.Grade == 3)
                .Ingredients.Select(ingredient => ingredient.Symbol).Order());

    [Fact]
    public void ALandableAtmosphereIsNotable()
    {
        Assert.Equal([NotableKind.LandableAtmosphere], Kinds(Body() with { Landable = true, Atmosphere = "thin argon" }));
        Assert.Empty(Kinds(Body() with { Landable = true, Atmosphere = "" }));
        Assert.Empty(Kinds(Body() with { Landable = true }));
        Assert.Empty(Kinds(Body() with { Atmosphere = "thin argon" }));
    }

    [Fact]
    public void TraitsComeRarestFirst()
    {
        var body = Body() with
        {
            Landable = true,
            Atmosphere = "thin argon",
            Eccentricity = 0.9,
            Parents = [("Planet", 2), ("Planet", 3)],
        };

        Assert.Equal(
            [NotableKind.NestedMoon, NotableKind.EccentricOrbit, NotableKind.LandableAtmosphere],
            Kinds(body));
    }

    [Fact]
    public void ABodyWithNothingScannedHasNoTraits() => Assert.Empty(Kinds(Body()));
}
