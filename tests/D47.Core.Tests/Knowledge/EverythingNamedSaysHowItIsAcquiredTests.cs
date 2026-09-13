using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>One answer for how anything is acquired, across the five catalogues (#176).</summary>
public class EverythingNamedSaysHowItIsAcquiredTests
{
    [Fact]
    public void AnAnacondaIsBoughtAtAShipyard()
    {
        var anaconda = AcquisitionGuide.For("Anaconda");

        Assert.NotNull(anaconda);
        Assert.Equal(AcquisitionKind.Ship, anaconda.Kind);
        Assert.Equal([AcquisitionMethod.Shipyard], anaconda.Methods);
    }

    [Fact]
    public void AGuardianFsdBoosterIsAModuleFromATechBroker()
    {
        var booster = AcquisitionGuide.For("Guardian FSD Booster");

        Assert.NotNull(booster);
        Assert.Equal(AcquisitionKind.Module, booster.Kind);
        Assert.Equal([AcquisitionMethod.TechBroker], booster.Methods);
        Assert.Equal("a Guardian tech broker", booster.Gate);
    }

    [Theory]
    [InlineData("Guardian Gauss Cannon", "a Guardian tech broker")]
    [InlineData("Shock Cannon", "a human tech broker")]
    [InlineData("Enzyme Missile Rack", "a human tech broker")]
    public void ATechBrokerModuleSaysWhatTheUnlockCosts(string module, string gate)
    {
        var unlocked = AcquisitionGuide.For(module);

        Assert.NotNull(unlocked);
        Assert.Equal([AcquisitionMethod.TechBroker], unlocked.Methods);
        Assert.Equal(gate, unlocked.Gate);
        Assert.False(string.IsNullOrWhiteSpace(unlocked.Detail));
    }

    [Fact]
    public void ABasicDiscoveryScannerCanNoLongerBeBought()
    {
        var scanner = AcquisitionGuide.For("Basic Discovery Scanner");

        Assert.NotNull(scanner);
        Assert.Equal([AcquisitionMethod.Unobtainable], scanner.Methods);
    }

    [Fact]
    public void APowerplayModuleNeedsAPledge()
    {
        var pledged = EliteSpecifications.Modules.First(module => module.NeedsPledge);

        var acquisition = AcquisitionGuide.For(pledged.Name);

        Assert.NotNull(acquisition);
        Assert.Equal([AcquisitionMethod.Outfitting], acquisition.Methods);
        Assert.Equal("a Powerplay pledge", acquisition.Gate);
    }

    [Fact]
    public void EveryOutfittingGateInTheTableIsPlacedByARule()
    {
        var entitlements = EliteSpecifications.Modules
            .Select(module => module.Entitlement)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.All(entitlements, entitlement =>
            Assert.True(AcquisitionGuide.Places(entitlement), $"no rule places '{entitlement}'"));
    }

    [Fact]
    public void AKarmaAr50IsBoughtAtPioneerSupplies()
    {
        var rifle = AcquisitionGuide.For("Karma AR-50");

        Assert.NotNull(rifle);
        Assert.Equal(AcquisitionKind.HandWeapon, rifle.Kind);
        Assert.Equal([AcquisitionMethod.PioneerSupplies], rifle.Methods);
    }

    [Fact]
    public void AGeneticSamplerComesWithTheArtemisSuit()
    {
        var sampler = AcquisitionGuide.For("Genetic Sampler");

        Assert.NotNull(sampler);
        Assert.Equal(AcquisitionKind.SuitTool, sampler.Kind);
        Assert.Equal([AcquisitionMethod.ComesWithSuit], sampler.Methods);
        Assert.Contains("Artemis Suit", sampler.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void CarbonIsTradedAtAMaterialTrader()
    {
        var carbon = AcquisitionGuide.For("Carbon");

        Assert.NotNull(carbon);
        Assert.Contains(AcquisitionMethod.MaterialTrader, carbon.Methods);
    }

    [Fact]
    public void AerogelIsFoundInSettlementsAndSoldByTheBartender()
    {
        var aerogel = AcquisitionGuide.For("Aerogel");

        Assert.NotNull(aerogel);
        Assert.Contains(AcquisitionMethod.Settlement, aerogel.Methods);
        Assert.Contains(AcquisitionMethod.Bartender, aerogel.Methods);
    }

    [Fact]
    public void LowTemperatureDiamondsAreRingMined()
    {
        var diamonds = AcquisitionGuide.For("Low Temperature Diamonds");

        Assert.NotNull(diamonds);
        Assert.Contains(AcquisitionMethod.RingMining, diamonds.Methods);
    }

    [Fact]
    public void EdenApplesOfAerialAreSoldAtAndradeLegacy()
    {
        var apples = AcquisitionGuide.For("Eden Apples of Aerial");

        Assert.NotNull(apples);
        Assert.Equal(AcquisitionKind.RareCommodity, apples.Kind);
        Assert.Equal([AcquisitionMethod.RareMarket], apples.Methods);
        Assert.Contains("Andrade Legacy", apples.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownNameIsOfferedTheNearestNames()
    {
        Assert.Null(AcquisitionGuide.For("Anacondo"));
        Assert.Contains("Anaconda", AcquisitionGuide.Near("Anacondo"));
    }
}
